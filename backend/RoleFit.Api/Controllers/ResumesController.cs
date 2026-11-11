using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using RoleFit.Api.Models;
using RoleFit.Api.Services;

namespace RoleFit.Api.Controllers;

[ApiController]
[Route("api/resumes")]
public sealed class ResumesController(
    DocumentReader reader, ResumeProfileParser parser, GroqProfileService groq,
    AlignmentEngine scorer, LocalStore analyses, ProfileDraftStore drafts, ResumeArchiveStore archive,
    JdRequirementsService jdRequirements, SemanticMatchService semantic) : ControllerBase
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
            p.Id,
            p.CandidateName,
            p.Designation,
            p.SourceFileName,
            p.ExperienceYears,
            ProjectCount = p.Projects.Count,
            p.UpdatedAt,
            p.Status
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
            // Rebuild verified projects before Groq, then again after enrichment.
            // The source resume remains the only evidence for project boundaries.
            SourceProjectReconciler.Apply(baseline, source);
            var profile = await groq.EnrichAsync(baseline, source, null, false, ct);
            SourceProjectReconciler.Apply(profile, source);
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
            // JD alignment may highlight or reorder supported evidence, but must
            // NEVER change the candidate's verified years, name or designation.
            var originalExperienceYears = baseline.ExperienceYears;
            var originalCandidateName = baseline.CandidateName;
            var originalDesignation = baseline.Designation;
            // First enrich from the original resume ONLY. Alignment itself is conservative:
            // it simply brings already-supported skills and projects most relevant to the JD first.
            // We do not ask AI to rewrite work history, dates, experience, skills or credentials
            // according to what the employer wants.
            SourceProjectReconciler.Apply(baseline, source);
            var aligned = await groq.EnrichAsync(baseline, source, null, false, ct);
            // Reconcile before JD-based sorting, so the final order remains JD-prioritized.
            SourceProjectReconciler.Apply(aligned, source);
            aligned.TechnicalSkills = aligned.TechnicalSkills
                .Select((skill, index) => new { skill, index })
                .OrderByDescending(x => HasPhrase(jdText, x.skill) ? 1 : 0)
                .ThenBy(x => x.index)
                .Select(x => x.skill).ToList();
            aligned.Projects = aligned.Projects
                .Select((project, index) => new { project, index })
                .OrderByDescending(x => x.project.TechnologyStack.Count(t => HasPhrase(jdText, t)))
                .ThenBy(x => x.index)
                .Select(x => x.project).Take(10).ToList();
            // The JD's explicitly requested competencies are added to the draft
            // as pending verification, not into the claimed skill stack.
            var extractedJd = await jdRequirements.ExtractAsync(jdText, ct);
            aligned.JdSkillsToVerify = JdRequirementsService.MissingSkills(
                extractedJd.Skills, source, aligned.TechnicalSkills);
            aligned.DocumentStyle = "aligned";
            aligned.Status = "JD-prioritized draft - requires verification";
            aligned.ParserNote = $"JD prioritized verified evidence and identified {aligned.JdSkillsToVerify.Count} missing JD competencies for review. {extractedJd.Note} Unverified requirements are NOT included in the exported resume or ATS score.";
            aligned.ExperienceYears = originalExperienceYears;
            aligned.CandidateName = originalCandidateName;
            aligned.Designation = originalDesignation;
            await drafts.SaveAsync(aligned, ct);
            // ATS score is calculated from ORIGINAL source, not AI-rewritten prose or edited drafts.
            var comparison = await semantic.AssessAsync(jdText, source, ct);
            var report = scorer.EvaluateWithSemantic(jdText, source, file.FileName, comparison);
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
            || profile.JdSkillsToVerify.Count > 60 || profile.Projects.Count > 10 || profile.Education.Count > 40
            || profile.EmploymentHistory.Count > 60 || profile.Certifications.Count > 40)
            return BadRequest(new ApiError("One or more profile fields exceed the allowed limit (10 projects maximum)."));
        profile.Status = "Edited draft - requires verification";
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await drafts.SaveAsync(profile, ct);
        await archive.UpdateProfileAsync(profile.Id, profile.CandidateName, profile.Designation, ct);
        return Ok(profile);
    }
    // Phrase-boundary matching prevents SQL falsely matching as part of PostgreSQL.
    private static bool HasPhrase(string text, string phrase)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(phrase)) return false;
        return Regex.IsMatch(text, @"(?<![\p{L}\p{N}])" + Regex.Escape(phrase.Trim()) + @"(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
