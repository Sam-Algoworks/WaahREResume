using Microsoft.AspNetCore.Mvc;
using RoleFit.Api.Models;
using RoleFit.Api.Services;

namespace RoleFit.Api.Controllers;

[ApiController]
[Route("api/analysis")]
public sealed class AnalysesController(DocumentReader documents, AlignmentEngine engine, LocalStore store) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(
        await store.ReadAsync(data => data.Analyses.OrderByDescending(a => a.CreatedAt).ToList(), ct));

    [HttpPost("single")]
    [RequestSizeLimit(DocumentReader.MaxBytes + 100_000)]
    public async Task<IActionResult> Single([FromForm] string jdText, [FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null) return BadRequest(new ApiError("Choose one resume file."));
        try
        {
            var text = await documents.ReadAsync(file, ct);
            var result = engine.Evaluate(jdText, text, file.FileName);
            await store.UpdateAsync(data => { data.Analyses.Add(result); return result; }, ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new ApiError(ex.Message)); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { return BadRequest(new ApiError("The document cannot be read. Verify that it is a valid PDF, DOCX or TXT file.")); }
    }

    [HttpPost("bulk")]
    [RequestSizeLimit(52_000_000)]
    public async Task<IActionResult> Bulk([FromForm] string jdText, [FromForm] List<IFormFile>? files, CancellationToken ct)
    {
        if (files is null || files.Count is 0 or > 10)
            return BadRequest(new ApiError("Upload between 1 and 10 resumes per batch."));
        if (string.IsNullOrWhiteSpace(jdText) || jdText.Trim().Length < 30)
            return BadRequest(new ApiError("Provide a job description of at least 30 characters."));
        var results = new List<AnalysisRecord>();
        var failures = new List<BatchFailure>();
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var text = await documents.ReadAsync(file, ct);
                results.Add(engine.Evaluate(jdText, text, file.FileName));
            }
            catch (ArgumentException ex)
            { failures.Add(new BatchFailure(Path.GetFileName(file.FileName), ex.Message)); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { failures.Add(new BatchFailure(Path.GetFileName(file.FileName), "Could not read or analyze this document.")); }
        }
        if (results.Count > 0)
            await store.UpdateAsync(data => { data.Analyses.AddRange(results); return results.Count; }, ct);
        var ordered = results.OrderByDescending(a => a.Score)
            .ThenBy(a => a.CandidateName, StringComparer.OrdinalIgnoreCase).ToList();
        return Ok(new BatchResponse(ordered, failures));
    }
}
