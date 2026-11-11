namespace RoleFit.Api.Models;

// Editable document draft. Never treat edits as verified ATS evidence without checking the source.
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
    public string ParserNote { get; set; } = "Review all extracted details against the original resume.";
    public string AiStatus { get; set; } = "not-configured";
    public string DocumentStyle { get; set; } = "algoworks";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ResumeProject
{
    // Keep Title as a compatibility property for older saved draft data.
    public string Title { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Year { get; set; } = "";
    public string Description { get; set; } = "";
    public string Responsibilities { get; set; } = "";
    public List<string> TechnologyStack { get; set; } = [];
    public string Evidence { get; set; } = "";
}

public static class ExperiencePolicy
{
    // User-requested display convention: 4.4 -> 4; 4.45 -> 4.5; 4.6 -> 5.
    // Values in the 0.45–0.55 fractional interval retain X.5.
    public static double? Display(double? years)
    {
        if (years is null || years < 0 || years > 60) return null;
        var value = (decimal)years.Value;
        var whole = decimal.Floor(value);
        var fractional = value - whole;
        if (fractional < 0.45m) return (double)whole;
        if (fractional <= 0.55m) return (double)(whole + 0.5m);
        return (double)(whole + 1m);
    }
}
