using System.Globalization;
using System.Text.RegularExpressions;
using RoleFit.Api.Models;

namespace RoleFit.Api.Services;

// Starter, evidence-only parser. No AI provider is called and missing values remain empty.
public sealed class ResumeProfileParser
{
    private static readonly string[] Skills =
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

    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ASP.NET Core"] = ["asp.net core", "aspnet core", "aspnetcore"],
        ["SQL Server"] = ["sql server", "mssql"],
        ["Node.js"] = ["node.js", "nodejs"],
        ["REST API"] = ["rest api", "restful api", "rest apis"],
        ["EF Core"] = ["entity framework core", "ef core"],
        ["CI/CD"] = ["ci/cd", "continuous integration"],
        ["AWS"] = ["aws", "amazon web services"]
    };

    private static readonly HashSet<string> KnownHeadings = new(StringComparer.OrdinalIgnoreCase)
    {
        "summary", "professional summary", "profile", "about me", "objective", "career objective",
        "skills", "technical skills", "key skills", "technologies", "technology stack",
        "experience", "work experience", "professional experience", "employment", "employment history",
        "projects", "project experience", "key projects", "academic projects",
        "education", "academic qualifications", "qualifications", "academic background",
        "certifications", "certificates", "licenses and certifications"
    };

    private static Match Match(string input, string pattern) => Regex.Match(input, pattern,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    private static bool ContainsSkill(string text, string skill) =>
        (Aliases.TryGetValue(skill, out var alts) ? alts : [skill]).Any(alias =>
            Regex.IsMatch(text, @"(?<![\p{L}\p{N}])" + Regex.Escape(alias) + @"(?![\p{L}\p{N}])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)));

    public ResumeProfile Parse(string text, string fileName)
    {
        var lines = text.Replace("\r", "").Split('\n')
            .Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        var profile = new ResumeProfile { SourceFileName = Path.GetFileName(fileName) };

        var email = Match(text, @"\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b");
        if (email.Success) profile.Email = email.Value;
        var phone = Match(text, @"(?<!\d)(?:\+\d{1,3}[\s.\-]?)?(?:\(?\d{3}\)?[\s.\-]?)?\d{3}[\s.\-]?\d{4}(?!\d)");
        if (phone.Success) profile.Phone = phone.Value;
        var years = Match(text, @"(?<!\d)(\d{1,2}(?:\.\d{1,2})?)\s*\+?\s*(?:years|yrs)\s+(?:of\s+)?(?:experience|exp)\b");
        if (!years.Success) years = Match(text, @"\b(?:experience|exp)\s*[:\-]?\s*(\d{1,2}(?:\.\d{1,2})?)\s*(?:years|yrs)\b");
        if (years.Success && double.TryParse(years.Groups[1].Value, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var parsedYears) && parsedYears <= 60)
            profile.ExperienceYears = ExperiencePolicy.Display(parsedYears);

        foreach (var line in lines.Take(8))
        {
            if (line.Length is < 4 or > 65 || line.Contains('@') || line.Any(char.IsDigit)
                || line.Contains('|') || line.Contains(':') || KnownHeadings.Contains(line)
                || line.Equals("resume", StringComparison.OrdinalIgnoreCase)
                || line.Equals("curriculum vitae", StringComparison.OrdinalIgnoreCase)) continue;
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length is >= 2 and <= 5 && words.All(x => x.All(c => char.IsLetter(c) || c is '.' or '-')))
            { profile.CandidateName = line; break; }
        }

        // Prefer explicitly labeled designation or title directly below the name.
        var designation = lines.Take(12).Select(line => Match(line,
            @"^(?:designation|role|job title|current role)\s*[:\-]\s*(.{3,80})$")).FirstOrDefault(m => m.Success);
        if (designation?.Success == true) profile.Designation = designation.Groups[1].Value.Trim();
        else if (profile.CandidateName.Length > 0)
        {
            var index = Array.FindIndex(lines, line => line.Equals(profile.CandidateName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0 && index + 1 < lines.Length)
            {
                var next = lines[index + 1];
                if (next.Length is >= 4 and <= 70 && !next.Contains('@') && !next.Any(char.IsDigit)
                    && !KnownHeadings.Contains(next) && !next.Contains("http", StringComparison.OrdinalIgnoreCase))
                    profile.Designation = next;
            }
        }

        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string current = "header";
        foreach (var line in lines)
        {
            var heading = line.Trim(':', '-', ' ').ToLowerInvariant();
            if (KnownHeadings.Contains(heading)) { current = heading; continue; }

            // Many resumes put the heading and its value on a single line,
            // for example "Summary: ..." or "Projects: ...".
            var inlineHeading = Match(line, @"^([A-Za-z][A-Za-z /-]{1,35})\s*:\s*(.+)$");
            if (inlineHeading.Success && KnownHeadings.Contains(inlineHeading.Groups[1].Value.Trim()))
            {
                current = inlineHeading.Groups[1].Value.Trim().ToLowerInvariant();
                if (!sections.TryGetValue(current, out var inlineEntries))
                    sections[current] = inlineEntries = [];
                inlineEntries.Add(inlineHeading.Groups[2].Value.Trim());
                continue;
            }
            if (!sections.TryGetValue(current, out var entries)) sections[current] = entries = [];
            entries.Add(line);
        }
        List<string> OfSection(params string[] names) => names
            .Where(sections.ContainsKey).SelectMany(n => sections[n]).ToList();

        profile.TechnicalSkills = Skills.Where(skill => ContainsSkill(text, skill))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        profile.ProfessionalSummary = string.Join(" ", OfSection(
            "professional summary", "summary", "profile", "about me", "objective", "career objective"))
            .Trim();
        if (profile.ProfessionalSummary.Length > 2000)
            profile.ProfessionalSummary = profile.ProfessionalSummary[..2000];
        profile.EmploymentHistory = OfSection("experience", "work experience", "professional experience", "employment", "employment history")
            .Take(25).ToList();
        profile.Education = OfSection("education", "academic qualifications", "qualifications", "academic background")
            .Take(15).ToList();
        profile.Certifications = OfSection("certifications", "certificates", "licenses and certifications")
            .Take(15).ToList();

        // Do not manufacture project titles; preserve each project line as an editable draft.
        profile.Projects = OfSection("projects", "project experience", "key projects", "academic projects")
            .Where(line => line.Length >= 8)
            .Take(10).Select(line => new ResumeProject { Title = line, Domain = line, Evidence = line }).ToList();
        return profile;
    }
}
