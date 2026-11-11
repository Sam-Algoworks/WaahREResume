using Microsoft.AspNetCore.Mvc;
using RoleFit.Api.Models;
using RoleFit.Api.Services;

namespace RoleFit.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(DocumentReader reader) : ControllerBase
{
    // Used by JD upload fields. Files are processed in-memory, not saved here.
    [HttpPost("read")]
    [RequestSizeLimit(DocumentReader.MaxBytes + 100_000)]
    public async Task<IActionResult> Read([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null) return BadRequest(new ApiError("Select a PDF, DOCX or TXT file."));
        try { return Ok(new { text = await reader.ReadAsync(file, ct), fileName = Path.GetFileName(file.FileName) }); }
        catch (ArgumentException ex) { return BadRequest(new ApiError(ex.Message)); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { return BadRequest(new ApiError("Unable to read this file. Check that it is not corrupted or scanned without a text layer.")); }
    }
}
