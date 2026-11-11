using Microsoft.AspNetCore.Mvc;
using RoleFit.Api.Models;
using RoleFit.Api.Services;

namespace RoleFit.Api.Controllers;

[ApiController]
[Route("api/analysis")]
public sealed class AnalysesController(DocumentReader documents, AlignmentEngine engine, LocalStore store, ResumeArchiveStore archive, SemanticMatchService semantic) : ControllerBase
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
            var comparison = await semantic.AssessAsync(jdText, text, ct);
            var result = engine.EvaluateWithSemantic(jdText, text, file.FileName, comparison);
            await store.UpdateAsync(data => { data.Analyses.Add(result); return result; }, ct);
            // Preserve the existing History / Download Original workflow.
            await archive.RememberAsync(file, result.CandidateName, "", null, result.Id, ct);
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
        // Keep the original upload so each newly scored result remains linked
        // to an encrypted source document in the existing History archive.
        var uploaded = new List<(IFormFile Original, string FileName, string Text)>();
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                uploaded.Add((file, file.FileName, await documents.ReadAsync(file, ct)));
            }
            catch (ArgumentException ex)
            { failures.Add(new BatchFailure(Path.GetFileName(file.FileName), ex.Message)); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { failures.Add(new BatchFailure(Path.GetFileName(file.FileName), "Could not read or analyze this document.")); }
        }
        // All candidates in one ranking MUST share the exact same rubric and
        // scoring method. Never mix semantic and fallback scores in one list.
        var comparisons = new List<SemanticComparison?>();
        List<SemanticRequirement> rubric = [];
        if (uploaded.Count > 0)
        {
            var first = await semantic.AssessAsync(jdText, uploaded[0].Text, ct);
            comparisons.Add(first);
            if (first?.Requirements.Count > 0)
                rubric = first.Requirements.Where(r => r.Importance == "required")
                    .Select(r => new SemanticRequirement
                    {
                        Name = r.Name, Category = r.Category, Importance = "required",
                        JdEvidence = r.JdEvidence
                    }).ToList();
        }
        // Compare everyone against the same rubric, not a fresh interpretation
        // of the JD per candidate. If ANY request fails, all use the fallback.
        for (var i = 1; i < uploaded.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            comparisons.Add(rubric.Count > 0
                ? await semantic.AssessAsync(jdText, uploaded[i].Text, ct, rubric)
                : null);
        }
        var useSemantic = rubric.Count > 0 && comparisons.Count == uploaded.Count &&
            comparisons.All(c => c?.Requirements.Count > 0);
        for (var i = 0; i < uploaded.Count; i++)
        {
            var source = uploaded[i];
            SemanticComparison? stable = null;
            if (useSemantic)
            {
                var local = comparisons[i]!.Requirements;
                stable = new SemanticComparison
                {
                    Requirements = rubric.Select(req =>
                    {
                        var verified = local.FirstOrDefault(r =>
                            r.Importance == "required" &&
                            r.Category.Equals(req.Category, StringComparison.OrdinalIgnoreCase) &&
                            r.Name.Equals(req.Name, StringComparison.OrdinalIgnoreCase));
                        return new SemanticRequirement
                        {
                            Name = req.Name, Category = req.Category, Importance = "required",
                            JdEvidence = req.JdEvidence,
                            Match = verified?.Match ?? "none",
                            ResumeEvidence = verified?.ResumeEvidence ?? "",
                            Reason = verified?.Reason ?? "No independently verified evidence in this candidate's comparison."
                        };
                    }).ToList()
                };
            }
            results.Add(engine.EvaluateWithSemantic(jdText, source.Text, source.FileName, stable));
        }
        if (results.Count > 0)
        {
            await store.UpdateAsync(data => { data.Analyses.AddRange(results); return results.Count; }, ct);
            // Preserve the source-document archive across bulk ranking too.
            // A failed archive is reported as a warning while the completed
            // analysis remains available; never fabricate an archived original.
            for (var i = 0; i < results.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await archive.RememberAsync(uploaded[i].Original,
                        results[i].CandidateName, "", null, results[i].Id, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failures.Add(new BatchFailure(Path.GetFileName(uploaded[i].FileName),
                        "Analysis completed, but archiving its original document failed."));
                }
            }
        }
        var ordered = results.OrderByDescending(a => a.Score)
            .ThenBy(a => a.CandidateName, StringComparer.OrdinalIgnoreCase).ToList();
        return Ok(new BatchResponse(ordered, failures));
    }
}
