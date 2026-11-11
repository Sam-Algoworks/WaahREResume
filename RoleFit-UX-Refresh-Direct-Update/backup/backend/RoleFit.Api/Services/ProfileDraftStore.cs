using System.Text.Json;
using RoleFit.Api.Models;

namespace RoleFit.Api.Services;

// Local draft storage only. We will move this to authorized SQL Server storage later.
public sealed class ProfileDraftStore
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly string _path;
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public ProfileDraftStore(IWebHostEnvironment env)
    {
        var folder = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "profile-drafts.json");
    }

    private async Task<List<ResumeProfile>> ReadCoreAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return [];
        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<List<ResumeProfile>>(stream, _options, ct) ?? [];
    }

    public async Task<ResumeProfile?> GetAsync(Guid id, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try { return (await ReadCoreAsync(ct)).FirstOrDefault(p => p.Id == id); }
        finally { _mutex.Release(); }
    }

    public async Task SaveAsync(ResumeProfile profile, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var rows = await ReadCoreAsync(ct);
            var previous = rows.FindIndex(p => p.Id == profile.Id);
            if (previous >= 0) rows[previous] = profile;
            else rows.Add(profile);
            var tempPath = _path + ".tmp";
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                await JsonSerializer.SerializeAsync(stream, rows, _options, ct);
            File.Move(tempPath, _path, overwrite: true);
        }
        finally { _mutex.Release(); }
    }
}
