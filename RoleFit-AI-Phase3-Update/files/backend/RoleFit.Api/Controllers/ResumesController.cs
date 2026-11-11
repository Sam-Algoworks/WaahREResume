using Microsoft.AspNetCore.Mvc;
using RoleFit.Api.Models;
using RoleFit.Api.Services;

namespace RoleFit.Api.Controllers;

[ApiController]
[Route("api/resumes")]
public sealed class ResumesController(DocumentReader reader, ResumeProfileParser parser, ProfileDraftStore drafts) : ControllerBase
{
    [HttpPost("parse")]
    [RequestSizeLimit(DocumentReader.MaxBytes + 100_000)]
    public async Task<IActionResult> Parse([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null) return BadRequest(new ApiError("Select a resume."));
        try
        {
            var content = await reader.ReadAsync(file, ct);
            var profile = parser.Parse(content, file.FileName);
            // Return an unsaved draft. The recruiter chooses whether to persist it.
            return Ok(profile);
        }
        catch (ArgumentException ex) { return BadRequest(new ApiError(ex.Message)); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { return BadRequest(new ApiError("Resume could not be parsed. Verify the file content.")); }
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
            || profile.Projects.Count > 30 || profile.Education.Count > 40
            || profile.EmploymentHistory.Count > 60 || profile.Certifications.Count > 40)
            return BadRequest(new ApiError("One or more profile fields exceed the allowed limit."));
        profile.Status = "Draft - requires verification";
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await drafts.SaveAsync(profile, ct);
        return Ok(profile);
    }
}
