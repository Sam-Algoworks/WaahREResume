using Microsoft.AspNetCore.Mvc;
using RoleFit.Api.Models;
using RoleFit.Api.Services;

namespace RoleFit.Api.Controllers;

[ApiController]
[Route("api/resumes")]
public sealed class ResumesController(
    DocumentReader reader, ResumeProfileParser parser, GroqProfileService groq,
    AlignmentEngine scorer, LocalStore analyses, ProfileDraftStore drafts, ResumeArchiveStore archive) : ControllerBase
{
    [HttpGet("ai/status")]
    public IActionResult AiStatus() => Ok(new
    {
        configured = groq.IsConfigured,
        note = groq.IsConfigured ? "Groq configured on the backend" : "Add GROQ_API_KEY to enable AI parsing and alignment"
    });

    // Local development record index; these drafts remain in App_Data,
    // never in wwwroot or publicly downloadable document folders.
    [HttpGet("drafts")]
    public async Task<IActionResult> ListDrafts(CancellationToken ct)
    {
        var records = await drafts.ListAsync(ct);
        return Ok(records.Select(p => new
        {
            p.Id, p.CandidateName, p.Designation, p.SourceFileName,
            p.ExperienceYears, ProjectCount = p.Projects.Count,
            p.UpdatedAt, p.Status
        }));
    }

    [HttpPost("parse")]
    [RequestSizeLimit(DocumentReader.MaxBytes + 100_000)]
    public async Task<IActionResult> Parse([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null) return BadRequest(new ApiError("Select a resume."));
        try
        {
            var source = await reader.ReadAsync(file, ct);
            var baseline = parser.Parse(source, file.FileName);
            var profile = await groq.EnrichAsync(baseline, source, null, false, ct);
            await drafts.SaveAsync(profile, ct);
            await archive.RememberAsync(file, profile.CandidateName, profile.Designation, profile.Id, null, ct);
            return Ok(profile);
        }
        catch (ArgumentException ex) { return BadRequest(new ApiError(ex.Message)); }
        catch (InvalidOperationException ex) { return StatusCode(502, new ApiError(ex.Message)); }
    }

    [HttpPost("align")]
    [RequestSizeLimit(DocumentReader.MaxBytes + 100_000)]
    public async Task<IActionResult> Align(
        [FromForm] IFormFile? file, [FromForm] string? jdText, CancellationToken ct)
    {
        if (file is null) return BadRequest(new ApiError("Choose a resume to align."));
        if (string.IsNullOrWhiteSpace(jdText) || jdText.Trim().Length < 30)
            return BadRequest(new ApiError("Upload or paste a JD (minimum 30 characters)."));
        if (!groq.IsConfigured)
            return StatusCode(409, new ApiError("Groq must be configured before you can align a resume to a JD. The original resume has not been modified."));
        try
        {
            var source = await reader.ReadAsync(file, ct);
            var baseline = parser.Parse(source, file.FileName, jdText);
            var aligned = await groq.EnrichAsync(baseline, source, jdText, true, ct);
            await drafts.SaveAsync(aligned, ct);
            // ATS score is calculated from ORIGINAL source, not AI-rewritten prose or edited drafts.
            var report = scorer.Evaluate(jdText, source, file.FileName);
            await analyses.UpdateAsync(data => { data.Analyses.Add(report); return true; }, ct);
            await archive.RememberAsync(file, aligned.CandidateName, aligned.Designation, aligned.Id, report.Id, ct);
            return Ok(new { profile = aligned, analysis = report });
        }
        catch (ArgumentException ex) { return BadRequest(new ApiError(ex.Message)); }
        catch (InvalidOperationException ex) { return StatusCode(502, new ApiError(ex.Message)); }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var profile = await drafts.GetAsync(id, ct);
        return profile is null ? NotFound(new ApiError("Draft not found.")) : Ok(profile);
    }

    [HttpPut("{id:guid}/draft")]
    public async Task<IActionResult> SaveDraft(Guid id, [FromBody] ResumeProfile profile, CancellationToken ct)
    {
        if (id != profile.Id) return BadRequest(new ApiError("Profile ID mismatch."));
        if (string.IsNullOrWhiteSpace(profile.SourceFileName)) return BadRequest(new ApiError("Source filename is required."));
        if (profile.CandidateName.Length > 160 || profile.Designation.Length > 160
            || profile.ProfessionalSummary.Length > 5000 || profile.TechnicalSkills.Count > 150
            || profile.Projects.Count > 10 || profile.Education.Count > 40
            || profile.EmploymentHistory.Count > 60 || profile.Certifications.Count > 40)
            return BadRequest(new ApiError("One or more profile fields exceed the allowed limit (10 projects maximum)."));
        profile.Status = "Edited draft - requires verification";
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await drafts.SaveAsync(profile, ct);
        await archive.UpdateProfileAsync(profile.Id, profile.CandidateName, profile.Designation, ct);
        return Ok(profile);
    }
}
