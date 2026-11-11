using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using RoleFit.Api.Models;

namespace RoleFit.Api.Services;

// Baseline deterministic evaluator. It is NOT a verified third-party ATS score.
// No Groq calls, inferred qualifications, or demographic attributes are used.
public sealed partial class AlignmentEngine
{
    private static readonly string[] SkillVocabulary =
    [
        "ASP.NET Core", "C#", "C++", "Java", "JavaScript", "TypeScript", "Python",
        "React", "Angular", "Vue", "Node.js", "Next.js", ".NET", "HTML", "CSS",
        "SQL Server", "PostgreSQL", "MySQL", "MongoDB", "Redis", "SQLite",
        "Azure", "AWS", "GCP", "Docker", "Kubernetes", "Terraform", "Git", "GitHub",
        "REST API", "GraphQL", "Microservices", "Entity Framework", "EF Core",
        "Machine Learning", "Power BI", "Tableau", "Excel", "Snowflake",
        "Databricks", "Spark", "PySpark", "Airflow", "ETL", "SSIS", "SSMS",
        "Selenium", "Playwright", "Cypress", "JUnit", "NUnit", "xUnit", "CI/CD",
        "Jenkins", "Linux", "Bash", "Agile", "Scrum", "Figma", "UI/UX"
    ];

    // Domain-agnostic action vocabulary; words alone remain weak evidence.
    private static readonly string[] ResponsibilityTerms =
    ["develop", "design", "build", "test", "deploy", "manage", "analyze",
     "optimize", "maintain", "integrate", "collaborate", "automate", "troubleshoot",
     "assess", "reconcile", "audit", "teach", "train", "recruit", "onboard",
     "coordinate", "schedule", "forecast", "budget", "diagnose", "monitor",
     "support", "supervise", "facilitate", "procure", "sell", "document",
     "plan", "research", "evaluate", "review", "lead", "negotiate", "implement"];

    private static readonly string[] QualificationTerms =
    ["bachelor", "master", "b.tech", "b.e.", "bsc", "b.sc", "m.tech", "mba", "degree", "diploma"];

    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ASP.NET Core"] = ["asp.net core", "asp net core", "aspnetcore"],
        [".NET"] = [".net", "dotnet"],
        ["Node.js"] = ["node.js", "nodejs"],
        ["Next.js"] = ["next.js", "nextjs"],
        ["REST API"] = ["rest api", "restful api", "restful apis", "rest apis"],
        ["SQL Server"] = ["sql server", "mssql", "microsoft sql server"],
        ["Entity Framework"] = ["entity framework", "entityframework"],
        ["EF Core"] = ["ef core", "entity framework core"],
        ["CI/CD"] = ["ci/cd", "continuous integration", "continuous delivery"],
        ["GCP"] = ["gcp", "google cloud platform"],
        ["AWS"] = ["aws", "amazon web services"],
        ["UI/UX"] = ["ui/ux", "user experience", "user interface"]
    };

    private static bool HasTerm(string text, string term)
    {
        var aliases = Aliases.TryGetValue(term, out var candidateAliases)
            ? candidateAliases : new[] { term };
        return aliases.Any(alias => Regex.IsMatch(text, @"(?<![\p{L}\p{N}])" +
            Regex.Escape(alias) + @"(?![\p{L}\p{N}])", RegexOptions.IgnoreCase,
            TimeSpan.FromMilliseconds(150)));
    }

    private static List<string> Skills(string text) => SkillVocabulary
        .Where(skill => HasTerm(text, skill))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    // Parse explicit required skill lists, including multiline bullets.
    // Labelled nice-to-have/preferred items are excluded from mandatory scoring.
    private static List<string> ExplicitJdCompetencies(string jd) =>
        JdEvidenceRules.ExplicitRequiredCompetencies(jd);

    private static double ProjectEvidenceScore(
        string resumeText, string fileName, IReadOnlyList<string> jdSkills)
    {
        // A lone mention of "project" is NOT evidence of relevant work.
        // Reuse the deterministic parser, not the AI-aligned resume.
        List<ResumeProject> projects;
        try
        {
            projects = new ResumeProfileParser().Parse(resumeText, fileName).Projects
                .Where(p => !string.IsNullOrWhiteSpace(p.Evidence) && p.Evidence.Length >= 40)
                .ToList();
        }
        catch (ArgumentException) { return 0; }
        if (projects.Count == 0) return 0;

        // If the JD names no recognizable competency, the fact that a project
        // exists is partial evidence; its relevance is not established.
        if (jdSkills.Count == 0) return 0.5;
        var relevant = projects.Count(p => jdSkills.Any(skill =>
            HasTerm(p.Evidence, skill) || HasTerm(p.Description, skill)));
        return relevant == 0 ? 0.25 : Math.Min(1, 0.5 + 0.25 * relevant);
    }

    public AnalysisRecord Evaluate(string jdText, string resumeText, string fileName)
    {
        if (string.IsNullOrWhiteSpace(jdText) || jdText.Trim().Length < 30)
            throw new ArgumentException("Provide a job description of at least 30 characters.");
        if (string.IsNullOrWhiteSpace(resumeText) || resumeText.Trim().Length < 30)
            throw new ArgumentException("Resume content is missing or too short.");

        // Scoring only: keep optional sections in stored JD, but do not count them as missing required skills.
        var mandatoryJd = JdEvidenceRules.MandatoryDescription(jdText);
        var jobSkills = Skills(mandatoryJd)
            .Concat(ExplicitJdCompetencies(mandatoryJd))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        // Match verified source mentions, not aligned drafts or negated "no experience" statements.
        var candidateSkills = Skills(resumeText)
            .Where(skill => JdEvidenceRules.HasAffirmativeMention(resumeText, skill,
                Aliases.TryGetValue(skill, out var aliases) ? aliases : null))
            .Concat(jobSkills.Where(skill => JdEvidenceRules.HasAffirmativeMention(resumeText, skill,
                Aliases.TryGetValue(skill, out var aliases) ? aliases : null)))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var matches = jobSkills.Where(s => candidateSkills.Contains(s, StringComparer.OrdinalIgnoreCase)).ToList();
        var missing = jobSkills.Except(matches, StringComparer.OrdinalIgnoreCase).ToList();
        var breakdown = new List<ScorePart>();

        void Add(string category, int weight, double? fraction)
        {
            breakdown.Add(new ScorePart(category,
                fraction is null ? 0 : Math.Round(weight * Math.Clamp(fraction.Value, 0, 1), 2),
                weight, fraction is not null));
        }

        Add("JD-required skills", 35, jobSkills.Count > 0 ? (double)matches.Count / jobSkills.Count : null);

        var neededMonths = ResumeEvidence.RequiredMonths(mandatoryJd);
        var candidateMonths = ResumeEvidence.ExplicitProfessionalMonths(resumeText);
        // Missing evidence of the JD's required years is a gap, not "N/A".
        // Do not redistribute those 20 points into unrelated criteria.
        Add("Experience", 20, neededMonths is > 0
            ? (double)(candidateMonths ?? 0) / neededMonths.Value
            : null);

        var jdActions = ResponsibilityTerms.Where(term => JdEvidenceRules.HasAction(mandatoryJd, term)).ToList();
        var foundActions = jdActions.Count(term => JdEvidenceRules.HasAction(resumeText, term));
        Add("Responsibilities", 20, jdActions.Count > 0 ? (double)foundActions / jdActions.Count : null);

        // Score genuine, locally evidenced projects, not just the word "project".
        var jdRequiresProjects = HasTerm(mandatoryJd, "projects") || HasTerm(mandatoryJd, "project");
        Add("Projects", 15, jdRequiresProjects
            ? ProjectEvidenceScore(resumeText, fileName, jobSkills)
            : null);

        var jdQualifications = QualificationTerms.Where(term => HasTerm(mandatoryJd, term)).ToList();
        Add("Qualifications", 5, jdQualifications.Count > 0
            ? (double)jdQualifications.Count(term => HasTerm(resumeText, term)) / jdQualifications.Count : null);

        // Readability checks only length and simple section clarity; not candidate quality.
        var readable = resumeText.Length >= 200 ? 0.5 : 0.0;
        readable += (HasTerm(resumeText, "experience") || HasTerm(resumeText, "education")
            || HasTerm(resumeText, "skills")) ? 0.5 : 0.0;
        Add("Document readability", 5, readable);

        var applicable = breakdown.Where(x => x.Applicable).Sum(x => x.Weight);
        var result = applicable == 0 ? 0 : (int)Math.Round(
            100 * breakdown.Sum(x => x.Awarded) / applicable,
            MidpointRounding.AwayFromZero);
        var name = GuessName(resumeText, fileName);
        return new AnalysisRecord
        {
            CandidateName = name,
            FileName = Path.GetFileName(fileName),
            JobTitle = GuessJobTitle(jdText),
            JobFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(jdText)))[..12],
            Score = Math.Clamp(result, 0, 100),
            ApplicableWeight = applicable,
            JobSkills = jobSkills,
            CandidateSkills = candidateSkills,
            MatchedSkills = matches,
            MissingSkills = missing,
            Breakdown = breakdown,
            VerifiedExperienceMonths = candidateMonths,
            RequiredExperienceMonths = neededMonths,
            Notes = "Estimated JD alignment v2 (deterministic fallback, not an official ATS score). " +
                    "Unverified qualifications do not earn points. Human review required; not a hiring recommendation."
        };
    }

    // One externally verified semantic comparison per ORIGINAL resume/JD.
    // The model cannot supply a score and cannot create factual evidence.
    public AnalysisRecord EvaluateWithSemantic(
        string jdText, string resumeText, string fileName, SemanticComparison? comparison)
    {
        var result = Evaluate(jdText, resumeText, fileName);
        if (comparison?.Requirements is null || comparison.Requirements.Count == 0) return result;
        var required = comparison.Requirements
            .Where(r => r.Importance == "required").ToList();
        if (required.Count == 0) return result;

        static double Credit(string match) => match switch
        {
            "direct" => 1.0, "equivalent" => 0.85, "partial" => 0.35, _ => 0.0
        };
        static string CategoryName(string key) => key switch
        {
            "skill" => "JD-required skills",
            "responsibility" => "Responsibilities",
            "qualification" => "Qualifications",
            "project" => "Projects",
            _ => ""
        };
        var breakdown = new List<ScorePart>(result.Breakdown);
        var evidence = new List<AnalysisEvidence>();
        foreach (var item in required)
        {
            evidence.Add(new AnalysisEvidence
            {
                Requirement = item.Name,
                Category = item.Category,
                Match = item.Match,
                SourceQuote = item.ResumeEvidence,
                Explanation = item.Reason
            });
        }
        foreach (var group in required.GroupBy(r => CategoryName(r.Category)))
        {
            if (group.Key.Length == 0) continue;
            var fraction = group.Average(r => Credit(r.Match));
            var index = breakdown.FindIndex(p => p.Category == group.Key);
            if (index < 0) continue;
            var previous = breakdown[index];
            breakdown[index] = new ScorePart(previous.Category,
                Math.Round(previous.Weight * fraction, 2), previous.Weight, true);
        }
        result.Breakdown = breakdown;
        result.JobSkills = required.Where(r => r.Category == "skill")
            .Select(r => r.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        result.MatchedSkills = required.Where(r => r.Category == "skill" && Credit(r.Match) >= 0.8)
            .Select(r => r.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        result.MissingSkills = required.Where(r => r.Category == "skill" && Credit(r.Match) < 0.8)
            .Select(r => r.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        result.EvidenceMatches = evidence;
        result.SemanticMatchingUsed = true;
        var applicable = breakdown.Where(p => p.Applicable).Sum(p => p.Weight);
        result.ApplicableWeight = applicable;
        result.Score = applicable <= 0 ? 0 : Math.Clamp((int)Math.Round(
            100 * breakdown.Sum(p => p.Awarded) / applicable, MidpointRounding.AwayFromZero), 0, 100);
        result.Notes = "Estimated JD alignment v2 (Groq semantic comparison, source quotes verified; human review required). " +
            "Equivalent skills earn partial-to-strong credit only with original resume evidence. " +
            "Experience uses deterministic months, not LLM guesses. Not a hiring decision.";
        return result;
    }

    private static string GuessName(string resume, string file)
    {
        foreach (var line in resume.Split('\n').Take(8))
        {
            var clean = line.Trim().Trim('\r');
            if (clean.Length is >= 3 and <= 55 && !clean.Contains('@')
                && !clean.Contains(':') && !clean.Any(char.IsDigit)
                && clean.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length is >= 2 and <= 5
                && !HasTerm(clean, "resume") && !HasTerm(clean, "curriculum vitae"))
                return clean;
        }
        return Path.GetFileNameWithoutExtension(file).Replace('_', ' ').Replace('-', ' ');
    }

    private static string GuessJobTitle(string jd)
    {
        var first = jd.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim()).FirstOrDefault();
        if (first is null) return "Job description";
        return first.Length > 70 ? "Job description" : first;
    }
}
