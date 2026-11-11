using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace RoleFit.Api.Services;

// Local-development storage. This is not a substitute for a production authentication and retention policy.
// File bodies are encrypted on disk with ASP.NET Core Data Protection; originals are never placed in wwwroot.
public sealed class ResumeArchiveStore
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _folder;
    private readonly string _indexPath;
    private readonly IDataProtector _protector;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public ResumeArchiveStore(IWebHostEnvironment environment, IDataProtectionProvider provider)
    {
        _folder = Path.Combine(environment.ContentRootPath, "App_Data", "secure-originals");
        _indexPath = Path.Combine(environment.ContentRootPath, "App_Data", "resume-archive.json");
        Directory.CreateDirectory(_folder);
        _protector = provider.CreateProtector("WaahRe.ResumeOriginal.v1");
    }

    private async Task<List<ResumeArchiveEntry>> ReadUnsafeAsync(CancellationToken ct)
    {
        if (!File.Exists(_indexPath)) return [];
        await using var stream = File.OpenRead(_indexPath);
        return await JsonSerializer.DeserializeAsync<List<ResumeArchiveEntry>>(stream, _json, ct) ?? [];
    }

    private async Task SaveUnsafeAsync(List<ResumeArchiveEntry> entries, CancellationToken ct)
    {
        var temp = _indexPath + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous))
            await JsonSerializer.SerializeAsync(stream, entries, _json, ct);
        File.Move(temp, _indexPath, true);
    }

    public async Task<IReadOnlyList<ResumeArchiveEntry>> ListAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try { return (await ReadUnsafeAsync(ct)).OrderByDescending(x => x.UpdatedAt).ToArray(); }
        finally { _lock.Release(); }
    }

    public async Task<ResumeArchiveEntry> RememberAsync(
        IFormFile file, string candidateName, string designation, Guid? profileId, Guid? analysisId, CancellationToken ct)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".pdf" or ".docx" or ".txt") || file.Length <= 0 || file.Length > DocumentReader.MaxBytes)
            throw new ArgumentException("Unsupported or oversized resume. Accepted: PDF, DOCX or TXT up to 5 MB.");

        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        await _lock.WaitAsync(ct);
        try
        {
            var entries = await ReadUnsafeAsync(ct);
            var entry = entries.FirstOrDefault(x => x.Sha256 == hash);
            if (entry is null)
            {
                entry = new ResumeArchiveEntry
                {
                    Sha256 = hash,
                    FileName = Path.GetFileName(file.FileName),
                    ContentType = extension == ".pdf" ? "application/pdf" : extension == ".docx" ?
                        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" : "text/plain"
                };
                // Encryption occurs before writing to the protected server-only folder.
                var encrypted = _protector.Protect(bytes);
                await File.WriteAllBytesAsync(Path.Combine(_folder, entry.Id.ToString("N") + ".bin"), encrypted, ct);
                entries.Add(entry);
            }
            entry.CandidateName = candidateName;
            if (!string.IsNullOrWhiteSpace(designation)) entry.Designation = designation;
            if (profileId.HasValue) entry.ProfileId = profileId;
            if (analysisId.HasValue && !entry.AnalysisIds.Contains(analysisId.Value)) entry.AnalysisIds.Add(analysisId.Value);
            entry.UpdatedAt = DateTimeOffset.UtcNow;
            await SaveUnsafeAsync(entries, ct);
            return entry;
        }
        finally { _lock.Release(); }
    }

    public async Task UpdateProfileAsync(Guid profileId, string name, string designation, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var entries = await ReadUnsafeAsync(ct);
            var entry = entries.FirstOrDefault(x => x.ProfileId == profileId);
            if (entry is null) return;
            entry.CandidateName = name;
            entry.Designation = designation;
            entry.UpdatedAt = DateTimeOffset.UtcNow;
            await SaveUnsafeAsync(entries, ct);
        }
        finally { _lock.Release(); }
    }

    public async Task<(byte[] Bytes, ResumeArchiveEntry Entry)?> ReadOriginalAsync(Guid id, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var entry = (await ReadUnsafeAsync(ct)).FirstOrDefault(x => x.Id == id);
            if (entry is null) return null;
            var file = Path.Combine(_folder, entry.Id.ToString("N") + ".bin");
            if (!File.Exists(file)) return null;
            return (_protector.Unprotect(await File.ReadAllBytesAsync(file, ct)), entry);
        }
        finally { _lock.Release(); }
    }

    public async Task<(byte[] Bytes, ResumeArchiveEntry Entry)?> ReadOriginalByProfileAsync(Guid profileId, CancellationToken ct)
    {
        var entry = (await ListAsync(ct)).FirstOrDefault(x => x.ProfileId == profileId);
        return entry is null ? null : await ReadOriginalAsync(entry.Id, ct);
    }
}

public sealed class ResumeArchiveEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sha256 { get; set; } = "";
    public string CandidateName { get; set; } = "";
    public string Designation { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public Guid? ProfileId { get; set; }
    public List<Guid> AnalysisIds { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
