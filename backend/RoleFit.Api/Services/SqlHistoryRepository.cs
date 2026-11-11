using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RoleFit.Api.Models;

namespace RoleFit.Api.Services;

// Preserves the controller-facing Store contracts while moving searchable metadata into SQL Server.
// SQL mode is intended for one local app instance; it is not a multi-user public access solution.
public sealed class SqlHistoryRepository(IDbContextFactory<HistoryDbContext> factory, IWebHostEnvironment environment, ILogger<SqlHistoryRepository> logger)
{
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    // One-time import was performed on the first SQL-enabled run in Phase 2.
    // Do not re-import old JSON indexes on later startups: doing so could
    // resurrect records that HR intentionally deleted from SQL Server.
    // Apply this update ONLY after validating the SQL data import.
    public async Task InitializeAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
    }

    private async Task<T?> ReadJsonFileAsync<T>(string file, CancellationToken ct)
    {
        if (!File.Exists(file)) return default;
        // Original JSON files are deliberately retained as a rollback backup.
        await using var stream = File.OpenRead(file);
        return await JsonSerializer.DeserializeAsync<T>(stream, _json, ct);
    }
    private string Json<T>(T record) => JsonSerializer.Serialize(record, _json);
    private T Parse<T>(string payload) => JsonSerializer.Deserialize<T>(payload, _json)
        ?? throw new InvalidDataException("Invalid history record in SQL Server.");
    private HistoryJobRow ToJob(JobRecord x) => new() { Id = x.Id, Payload = Json(x) };
    private HistoryAnalysisRow ToAnalysis(AnalysisRecord x) => new() { Id = x.Id, CreatedAt = x.CreatedAt, Payload = Json(x) };
    private HistoryDraftRow ToDraft(ResumeProfile x) => new() { Id = x.Id, UpdatedAt = x.UpdatedAt, Payload = Json(x) };
    private HistoryArchiveRow ToArchive(ResumeArchiveEntry x) => new() { Id = x.Id, Sha256 = x.Sha256, Payload = Json(x) };

    // Only loads the JSON fields needed for this read. Unexpected cancellations are logged
    // and propagated; caller-initiated cancellation is never converted into empty history.
    public async Task<StoredData> ReadLocalAsync(CancellationToken ct)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var stage = "creating SQL context";
        try
        {
            ct.ThrowIfCancellationRequested();
            await using var db = await factory.CreateDbContextAsync(ct);

            stage = "reading Jobs";
            var jobPayloads = await db.Jobs.AsNoTracking()
                .Select(x => x.Payload).ToListAsync(ct);

            stage = "reading Analyses";
            var analysisPayloads = await db.Analyses.AsNoTracking()
                .Select(x => x.Payload).ToListAsync(ct);

            return new StoredData
            {
                Jobs = jobPayloads.Select(Parse<JobRecord>).ToList(),
                Analyses = analysisPayloads.Select(Parse<AnalysisRecord>).ToList()
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal browser disconnect / request cancellation. The API boundary handles it.
            logger.LogDebug("SQL history read cancelled by request after {ElapsedMs}ms at {Stage}",
                watch.ElapsedMilliseconds, stage);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // The request was NOT cancelled. Preserve the error so SQL timeouts or provider
            // problems do not appear as missing/deleted history.
            logger.LogError(ex, "Unexpected SQL history cancellation after {ElapsedMs}ms at {Stage}",
                watch.ElapsedMilliseconds, stage);
            throw;
        }
    }
    public async Task<T> UpdateLocalAsync<T>(Func<StoredData, T> update, CancellationToken ct)
    {
        await _writeGate.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var local = new StoredData
            {
                Jobs = (await db.Jobs.ToListAsync(ct)).Select(x => Parse<JobRecord>(x.Payload)).ToList(),
                Analyses = (await db.Analyses.ToListAsync(ct)).Select(x => Parse<AnalysisRecord>(x.Payload)).ToList()
            };
            var result = update(local);
            db.Jobs.RemoveRange(await db.Jobs.ToListAsync(ct));
            db.Analyses.RemoveRange(await db.Analyses.ToListAsync(ct));
            await db.SaveChangesAsync(ct); // clear before adding rows with the same IDs
            db.Jobs.AddRange(local.Jobs.Select(ToJob));
            db.Analyses.AddRange(local.Analyses.Select(ToAnalysis));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        finally { _writeGate.Release(); }
    }
    public async Task<List<ResumeProfile>> ReadDraftsAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return (await db.Drafts.AsNoTracking().ToListAsync(ct)).Select(x => Parse<ResumeProfile>(x.Payload)).ToList();
    }
    public async Task UpsertDraftAsync(ResumeProfile profile, CancellationToken ct)
    {
        await _writeGate.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var row = await db.Drafts.FirstOrDefaultAsync(x => x.Id == profile.Id, ct);
            if (row is null) db.Drafts.Add(ToDraft(profile));
            else { row.Payload = Json(profile); row.UpdatedAt = profile.UpdatedAt; }
            await db.SaveChangesAsync(ct);
        }
        finally { _writeGate.Release(); }
    }
    // Delete exactly one archive and its *unshared* referenced analysis/draft rows.
    // No database schema change is necessary; metadata deletion is one SQL transaction.
    // Original-file cleanup runs only after this transaction commits.
    public async Task<bool> DeleteArchiveBundleAsync(Guid archiveId, CancellationToken ct)
    {
        await _writeGate.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var allArchives = await db.Archives.ToListAsync(ct);
            var target = allArchives.FirstOrDefault(x => x.Id == archiveId);
            if (target is null) return false;
            var entry = Parse<ResumeArchiveEntry>(target.Payload);
            var otherEntries = allArchives.Where(x => x.Id != archiveId)
                .Select(x => Parse<ResumeArchiveEntry>(x.Payload)).ToList();
            var sharedAnalysisIds = otherEntries.SelectMany(x => x.AnalysisIds).ToHashSet();
            var toDeleteAnalyses = entry.AnalysisIds.Where(x => !sharedAnalysisIds.Contains(x)).ToHashSet();
            var analysisRows = await db.Analyses.Where(x => toDeleteAnalyses.Contains(x.Id)).ToListAsync(ct);
            db.Analyses.RemoveRange(analysisRows);
            if (entry.ProfileId is Guid profileId && !otherEntries.Any(x => x.ProfileId == profileId))
            {
                var draft = await db.Drafts.FirstOrDefaultAsync(x => x.Id == profileId, ct);
                if (draft is not null) db.Drafts.Remove(draft);
            }
            db.Archives.Remove(target);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }
        finally { _writeGate.Release(); }
    }

    public async Task<List<ResumeArchiveEntry>> ReadArchivesAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return (await db.Archives.AsNoTracking().ToListAsync(ct)).Select(x => Parse<ResumeArchiveEntry>(x.Payload)).ToList();
    }
    public async Task SaveArchivesAsync(List<ResumeArchiveEntry> entries, CancellationToken ct)
    {
        await _writeGate.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Archives.RemoveRange(await db.Archives.ToListAsync(ct));
            await db.SaveChangesAsync(ct);
            db.Archives.AddRange(entries.Select(ToArchive));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        finally { _writeGate.Release(); }
    }
}
