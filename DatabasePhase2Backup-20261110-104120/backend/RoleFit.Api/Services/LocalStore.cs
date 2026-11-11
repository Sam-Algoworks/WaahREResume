using System.Text.Json;
using RoleFit.Api.Models;

namespace RoleFit.Api.Services;

// Single-machine, JSON-backed persistence for the first runnable MVP.
// Replace with SQL Server + EF Core before multi-user/production use.
public sealed class LocalStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public LocalStore(IWebHostEnvironment environment)
    {
        var folder = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "rolefit-local.json");
    }

    private async Task<StoredData> ReadInternalAsync(CancellationToken cancellation)
    {
        if (!File.Exists(_path)) return new StoredData();
        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<StoredData>(stream, _options, cancellation)
            ?? new StoredData();
    }

    private async Task SaveInternalAsync(StoredData data, CancellationToken cancellation)
    {
        var tempPath = _path + ".tmp";
        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write,
            FileShare.None, 4096, FileOptions.Asynchronous))
        {
            await JsonSerializer.SerializeAsync(stream, data, _options, cancellation);
        }
        File.Move(tempPath, _path, overwrite: true);
    }

    public async Task<T> ReadAsync<T>(Func<StoredData, T> select, CancellationToken cancellation)
    {
        await _gate.WaitAsync(cancellation);
        try { return select(await ReadInternalAsync(cancellation)); }
        finally { _gate.Release(); }
    }

    public async Task<T> UpdateAsync<T>(Func<StoredData, T> change, CancellationToken cancellation)
    {
        await _gate.WaitAsync(cancellation);
        try
        {
            var data = await ReadInternalAsync(cancellation);
            var result = change(data);
            await SaveInternalAsync(data, cancellation);
            return result;
        }
        finally { _gate.Release(); }
    }
}
