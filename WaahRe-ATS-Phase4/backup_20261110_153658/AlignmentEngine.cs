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

    private static readonly string[] ResponsibilityTerms =
    ["develop", "design", "build", "test", "deploy", "manage", "analyze", "optimize", "maintain", "integrate", "collaborate", "automate", "troubleshoot"];

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

    // Named JD competencies are not limited to software. This conservative
    // fallback recognizes ONLY explicitly enumerated requirements, not general
    // prose, inferred expertise, or skills added to an aligned draft.
    private static List<string> ExplicitJdCompetencies(string jd)
    {
        var found = new List<string>();
        foreach (var line in jd.Replace("\r", "").Split('\n'))
        {
            var match = Regex.Match(line,
                @"^\s*(?:required skills?|preferred skills?|skills?|core competencies|competencies|technical skills?|tools?|must[- ]have skills?)\s*:\s*(.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(150));
            if (!match.Success) continue;
            foreach (var item in Regex.Split(match.Groups[1].Value, @"[,;•|]"))
            {
                var value = item.Trim(' ', '\t', '.', '-');
                if (value.Length is < 2 or > 65 ||
                    value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 7 ||
                    value.Contains(':')) continue;
                found.Add(value);
            }
        }
        return found.Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToList();
    }

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

    private static double? RequiredExperience(string text)
    {
        var match = Regex.Match(text,
            @"(?<!\d)(\d{1,2}(?:\.\d)?)\s*(?:\+|\-|to)?\s*(?:\d{1,2}\s*)?(?:years|yrs)\b",
            RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(150));
        if (match.Success && double.TryParse(match.Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture, out double years)) return years;
        return null;
    }

    public AnalysisRecord Evaluate(string jdText, string resumeText, string fileName)
    {
        if (string.IsNullOrWhiteSpace(jdText) || jdText.Trim().Length < 30)
            throw new ArgumentException("Provide a job description of at least 30 characters.");
        if (string.IsNullOrWhiteSpace(resumeText) || resumeText.Trim().Length < 30)
            throw new ArgumentException("Resume content is missing or too short.");

        var jobSkills = Skills(jdText)
            .Concat(ExplicitJdCompetencies(jdText))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var candidateSkills = Skills(resumeText)
            .Concat(jobSkills.Where(skill => HasTerm(resumeText, skill)))
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

        var neededYears = RequiredExperience(jdText);
        var candidateYears = RequiredExperience(resumeText);
        // Missing evidence of the JD's required years is a gap, not "N/A".
        // Do not redistribute those 20 points into unrelated criteria.
        Add("Experience", 20, neededYears is > 0
            ? (candidateYears is > 0 ? candidateYears.Value / neededYears.Value : 0.0)
            : null);

        var jdActions = ResponsibilityTerms.Where(term => HasTerm(jdText, term)).ToList();
        var foundActions = jdActions.Count(term => HasTerm(resumeText, term));
        Add("Responsibilities", 20, jdActions.Count > 0 ? (double)foundActions / jdActions.Count : null);

        // Score genuine, locally evidenced projects, not just the word "project".
        var jdRequiresProjects = HasTerm(jdText, "projects") || HasTerm(jdText, "project");
        Add("Projects", 15, jdRequiresProjects
            ? ProjectEvidenceScore(resumeText, fileName, jobSkills)
            : null);

        var jdQualifications = QualificationTerms.Where(term => HasTerm(jdText, term)).ToList();
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
            Notes = "Estimated JD alignment v1.1 (deterministic, not an official ATS score). " +
                    "Unverified qualifications do not earn points. Human review required; not a hiring recommendation."
        };
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
