using System.Net;
using Microsoft.AspNetCore.Mvc;
using RoleFit.Api.Services;

namespace RoleFit.Api.Controllers;

// Development-only local archive access. Production deployment requires authentication and per-document authorization.
[ApiController]
[Route("api/archive")]
public sealed class ArchiveController(ResumeArchiveStore archive, IWebHostEnvironment environment) : ControllerBase
{
    private bool IsLocalDevelopment() => environment.IsDevelopment() &&
        HttpContext.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip);

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!IsLocalDevelopment()) return NotFound();
        return Ok((await archive.ListAsync(ct)).Select(x => new {
            x.Id, x.CandidateName, x.Designation, x.FileName, x.ProfileId, x.AnalysisIds, x.UpdatedAt,
            hasOriginal = true
        }));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!IsLocalDevelopment()) return NotFound();
        try
        {
            var result = await archive.DeleteAsync(id, ct);
            if (!result.Found) return NotFound();
            return Ok(new
            {
                deleted = true,
                originalFileDeleted = result.OriginalFileDeleted,
                warning = result.OriginalFileDeleted ? null :
                    "History was deleted but the encrypted file could not be removed from disk. Inspect App_Data/secure-originals privately."
            });
        }
        catch (InvalidOperationException)
        {
            return StatusCode(409, new { error = "SQL history is not enabled. Deletion is unavailable." });
        }
    }

    [HttpGet("{id:guid}/original")]
    public async Task<IActionResult> Original(Guid id, CancellationToken ct)
    {
        if (!IsLocalDevelopment()) return NotFound();
        var found = await archive.ReadOriginalAsync(id, ct);
        return found is null ? NotFound() : File(found.Value.Bytes, found.Value.Entry.ContentType, found.Value.Entry.FileName);
    }

    [HttpGet("profile/{id:guid}/original")]
    public async Task<IActionResult> ProfileOriginal(Guid id, CancellationToken ct)
    {
        if (!IsLocalDevelopment()) return NotFound();
        var found = await archive.ReadOriginalByProfileAsync(id, ct);
        return found is null ? NotFound() : File(found.Value.Bytes, found.Value.Entry.ContentType, found.Value.Entry.FileName);
    }
}
