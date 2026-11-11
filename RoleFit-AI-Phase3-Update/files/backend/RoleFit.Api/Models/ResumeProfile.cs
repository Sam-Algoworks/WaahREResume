namespace RoleFit.Api.Models;

// Editable profile is a DRAFT, not verified evidence for ATS scoring.
public sealed class ResumeProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourceFileName { get; set; } = "";
    public string CandidateName { get; set; } = "";
    public string Designation { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public double? ExperienceYears { get; set; }
    public string ProfessionalSummary { get; set; } = "";
    public List<string> TechnicalSkills { get; set; } = [];
    public List<string> EmploymentHistory { get; set; } = [];
    public List<ResumeProject> Projects { get; set; } = [];
    public List<string> Education { get; set; } = [];
    public List<string> Certifications { get; set; } = [];
    public string Status { get; set; } = "Draft - requires verification";
    public string ParserNote { get; set; } = "Rules-based extraction only. Review against the source before approval.";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ResumeProject
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}
