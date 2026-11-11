namespace RoleFit.Api.Models;

public sealed class JobRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AnalysisRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CandidateName { get; set; } = "";
    public string FileName { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string JobFingerprint { get; set; } = "";
    public int Score { get; set; }
    public int ApplicableWeight { get; set; }
    public List<string> JobSkills { get; set; } = [];
    public List<string> CandidateSkills { get; set; } = [];
    public List<string> MatchedSkills { get; set; } = [];
    public List<string> MissingSkills { get; set; } = [];
    public List<ScorePart> Breakdown { get; set; } = [];
    public string Notes { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record ScorePart(string Category, double Awarded, int Weight, bool Applicable);
public sealed record CreateJobRequest(string Title, string Description);
public sealed record UpdateJobRequest(string Title, string Description);
public sealed record ApiError(string Error);
public sealed record BatchFailure(string FileName, string Error);
public sealed record BatchResponse(List<AnalysisRecord> Results, List<BatchFailure> Failures);

public sealed class StoredData
{
    public List<JobRecord> Jobs { get; set; } = [];
    public List<AnalysisRecord> Analyses { get; set; } = [];
}
