using System.Globalization;
using System.Text.RegularExpressions;
using RoleFit.Api.Models;

namespace RoleFit.Api.Services;

// A fresh evidence-first parser for RoleFit AI. Its project boundaries are determined
// from project headings/entries, NOT from the first ten lines under a heading.
public sealed class ResumeProfileParser
{
    private static readonly string[] Skills =
    [
        "ASP.NET Core", "ASP.NET", "C#", "C++", "Java", "JavaScript", "TypeScript", "Python", "R",
        "React Native", "React", "Angular", "Vue", "Node.js", "Next.js", ".NET", "HTML", "CSS",
        "SQL Server", "PostgreSQL", "MySQL", "MongoDB", "Redis", "SQLite", "Oracle",
        "Azure", "AWS", "GCP", "Docker", "Kubernetes", "Terraform", "Git", "GitHub",
        "REST API", "GraphQL", "Microservices", "Entity Framework", "EF Core",
        "Machine Learning", "Power BI", "Tableau", "Excel", "Google Colab", "IoT", "Android",
        "Snowflake", "Databricks", "Spark", "PySpark", "Airflow", "ETL", "SSIS", "SSMS",
        "Selenium", "Playwright", "Cypress", "JUnit", "NUnit", "xUnit", "CI/CD",
        "Jenkins", "Linux", "Bash", "Agile", "Figma", "UI/UX", "Kafka", "Spring Boot"
    ];

    private static readonly Dictionary<string, string[]> SkillAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ASP.NET Core"] = ["asp.net core", "aspnet core", "aspnetcore"],
        ["SQL Server"] = ["sql server", "mssql"],
        ["React Native"] = ["react native", "react-native"],
        ["Node.js"] = ["node.js", "nodejs"],
        ["Next.js"] = ["next.js", "nextjs"],
        ["REST API"] = ["rest api", "restful api", "rest apis"],
        ["EF Core"] = ["entity framework core", "ef core"],
        ["CI/CD"] = ["ci/cd", "continuous integration"],
        ["Google Colab"] = ["google colab", "colab"],
        ["AWS"] = ["aws", "amazon web services"],
        ["IoT"] = ["iot", "internet of things"]
    };

    private static readonly HashSet<string> KnownHeadings = new(StringComparer.OrdinalIgnoreCase)
    {
        "summary", "professional summary", "profile", "about me", "objective", "career objective",
        "skills", "technical skills", "key skills", "technologies", "technology stack", "technical proficiency",
        "experience", "work experience", "professional experience", "employment", "employment history",
        "projects", "project experience", "key projects", "academic projects", "personal projects", "selected projects", "relevant projects", "projects undertaken",
        "education", "academic qualifications", "qualifications", "academic background",
        "certifications", "certificates", "licenses and certifications", "awards", "achievements", "interests", "publications", "languages", "contact"
    };
    private static readonly string[] ProjectHeadings =
    ["projects", "project experience", "key projects", "academic projects", "personal projects", "selected projects", "relevant projects", "projects undertaken"];
    private static readonly RegexOptions Rx = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static Match Search(string text, string pattern) =>
        Regex.Match(text, pattern, Rx, TimeSpan.FromMilliseconds(300));

    private static bool IsSkill(string text, string name) =>
        (SkillAliases.TryGetValue(name, out var values) ? values : [name]).Any(alias =>
            Regex.IsMatch(text, @"(?<![\p{L}\p{N}])" + Regex.Escape(alias) + @"(?![\p{L}\p{N}])", Rx, TimeSpan.FromMilliseconds(100)));

    public ResumeProfile Parse(string text, string fileName, string? jd = null)
    {
        var lines = text.Replace("\r", "").Split('\n').Select(l => Regex.Replace(l.Trim(), @"\s+", " "))
            .Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        var profile = new ResumeProfile { SourceFileName = Path.GetFileName(fileName) };

        var email = Search(text, @"\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b");
        if (email.Success) profile.Email = email.Value;
        var phone = Search(text, @"(?<!\d)(?:\+\d{1,3}[\s.\-]?)?(?:\(?\d{3}\)?[\s.\-]?)?\d{3}[\s.\-]?\d{4}(?!\d)");
        if (phone.Success) profile.Phone = phone.Value;

        var years = Search(text, @"(?<!\d)(\d{1,2}(?:\.\d{1,2})?)\s*\+?\s*(?:years|yrs)\s+(?:of\s+)?(?:experience|exp)\b");
        if (!years.Success) years = Search(text, @"\b(?:experience|exp)\s*[:\-]?\s*(\d{1,2}(?:\.\d{1,2})?)\s*(?:years|yrs)\b");
        if (years.Success && double.TryParse(years.Groups[1].Value, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var value))
            profile.ExperienceYears = ExperiencePolicy.Display(value);

        foreach (var line in lines.Take(12))
        {
            if (line.Length is < 4 or > 65 || line.Contains('@') || line.Any(char.IsDigit) ||
                line.Contains('|') || line.Contains(':') || KnownHeadings.Contains(line.Trim(':')) ||
                line.Equals("resume", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("curriculum vitae", StringComparison.OrdinalIgnoreCase)) continue;
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length is >= 2 and <= 5 && words.All(w => w.All(c => char.IsLetter(c) || c is '.' or '-')))
            { profile.CandidateName = line; break; }
        }

        foreach (var line in lines.Take(12))
        {
            var explicitTitle = Search(line, @"^(?:designation|role|job title|current role)\s*[:\-]\s*(.{3,80})$");
            if (explicitTitle.Success) { profile.Designation = explicitTitle.Groups[1].Value.Trim(); break; }
        }
        if (profile.Designation.Length == 0 && profile.CandidateName.Length > 0)
        {
            var index = Array.FindIndex(lines, l => l.Equals(profile.CandidateName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0 && index + 1 < lines.Length)
            {
                var next = lines[index + 1];
                if (next.Length is >= 4 and <= 95 && !next.Contains('@') &&
                    !KnownHeadings.Contains(next.Trim(':')) && !next.Contains("http", StringComparison.OrdinalIgnoreCase) &&
                    !Search(next, @"\b\d{4}\b").Success) profile.Designation = next;
            }
        }

        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var current = "header";
        foreach (var line in lines)
        {
            var heading = line.Trim(':', '-', ' ', '•', '●').ToLowerInvariant();
            if (KnownHeadings.Contains(heading)) { current = heading; continue; }
            var inline = Search(line, @"^([A-Za-z][A-Za-z /-]{1,36})\s*:\s*(.+)$");
            if (inline.Success && KnownHeadings.Contains(inline.Groups[1].Value.Trim()))
            {
                current = inline.Groups[1].Value.Trim().ToLowerInvariant();
                if (!sections.TryGetValue(current, out var inlineList)) sections[current] = inlineList = [];
                inlineList.Add(inline.Groups[2].Value.Trim());
                continue;
            }
            if (!sections.TryGetValue(current, out var bucket)) sections[current] = bucket = [];
            bucket.Add(line);
        }
        List<string> Section(params string[] names) => names.Where(sections.ContainsKey)
            .SelectMany(name => sections[name]).ToList();

        profile.TechnicalSkills = Skills.Where(s => IsSkill(text, s)).ToList();
        var summary = string.Join(" ", Section("professional summary", "summary", "profile", "about me", "objective", "career objective"));
        profile.ProfessionalSummary = TakeWords(summary, 85);
        profile.EmploymentHistory = Section("experience", "work experience", "professional experience", "employment", "employment history").Take(30).ToList();
        profile.Education = Section("education", "academic qualifications", "qualifications", "academic background").Take(20).ToList();
        profile.Certifications = Section("certifications", "certificates", "licenses and certifications").Take(20).ToList();

        var projectLines = Section(ProjectHeadings);
        // Some CVs keep project headings inside a work-history block rather than a standalone Projects section.
        // Only explicit "Project:" or "Project 2 -" labels can trigger this secondary search.
        if (projectLines.Count == 0)
        {
            projectLines = lines.Where(l => Search(l, @"^(?:[•●\-]\s*)?(?:project\s*(?:\d{1,2})?\s*[:\-]|\d{1,2}[.)]\s+project\s*[:\-])").Success).ToList();
        }
        var allProjects = ExtractProjects(projectLines, null, 100);
        // When the source lists more than ten projects, select the ten most relevant
        // to the JD, or to the documented role/skill set when no JD is supplied.
        // For ten or fewer, preserve the source document order without unnecessary ranking.
        profile.Projects = allProjects.Count > 10
            ? ExtractProjects(projectLines, jd ?? (profile.Designation + " " + string.Join(" ", profile.TechnicalSkills)), 10)
            : allProjects;
        profile.ParserNote = profile.Projects.Count > 0
            ? $"Basic parser identified {profile.Projects.Count} genuine project candidate(s). Verify each title, year and local technology stack."
            : "No clearly separated projects found in the extracted document. A project is not fabricated from work-history bullets; check source text/layout or enable Groq for richer extraction.";
        return profile;
    }

    public static List<ResumeProject> ExtractProjects(IReadOnlyList<string> projectLines, string? jd, int maxProjects = 10)
    {
        var blocks = new List<List<string>>();
        List<string>? current = null;
        for (var i = 0; i < projectLines.Count; i++)
        {
            var raw = projectLines[i].Trim();
            if (raw.Length == 0) continue;
            var starts = IsStart(raw, i == 0, i + 1 < projectLines.Count ? projectLines[i + 1] : null);
            if (starts && current is { Count: > 0 }) { blocks.Add(current); current = null; }
            if (starts || current is not null) (current ??= []).Add(raw);
        }
        if (current is { Count: > 0 }) blocks.Add(current);

        var result = new List<ResumeProject>();
        foreach (var block in blocks)
        {
            var project = ParseBlock(block);
            if (project is not null && !result.Any(p => p.Evidence.Equals(project.Evidence, StringComparison.OrdinalIgnoreCase)))
                result.Add(project);
        }
        return result.Select((project, i) => new { project, i })
            .OrderByDescending(x => Relevance(x.project, jd ?? ""))
            .ThenBy(x => x.i).Select(x => x.project).Take(Math.Clamp(maxProjects, 0, 100)).ToList();
    }

    private static bool IsStart(string line, bool first, string? next)
    {
        if (Search(line, @"^(?:\d{1,2}[.)]\s+|project\s*(?:\d{1,2})?\s*[:\-]|[•●]\s*project\s*[:\-])").Success) return true;
        if (Search(line, @"^(?:[-•●]\s*)?(?:name of project|project title)\s*:").Success) return true;
        if (Search(line, @"^[\w][\w &/.'+\-]{3,85}\s*\((?:19|20)\d{2}(?:\s*[-–]\s*(?:19|20)\d{2})?\)\s*[:\-]?").Success) return true;
        // A short stand-alone title followed by a descriptive line is common in PDFs.
        // Exclude descriptive action sentences; never treat a work bullet as a new project.
        if (Search(line, @"^(?:built|developed|designed|implemented|created|integrated|managed|used|worked|improved|led|responsible|deployed|automated|optimized|tested|configured|maintained)\b").Success)
            return false;
        if (line.Length is >= 5 and <= 85 && !line.EndsWith('.') && !line.EndsWith(';') &&
            !line.StartsWith("- ") && !line.StartsWith("• ") && !line.Contains(':') &&
            !Search(line, @"^(?:tech(?:nologies)?|tools|environment|responsibilit|role|achievements|duration)\b").Success &&
            (first || !string.IsNullOrEmpty(next)) &&
            line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 10)
            return true;
        return false;
    }

    private static ResumeProject? ParseBlock(List<string> block)
    {
        var full = string.Join(" ", block);
        var header = block[0];
        header = Regex.Replace(header, @"^(?:\d{1,2}[.)]\s+|[•●]\s*)", "").Trim();
        header = Regex.Replace(header, @"^(?:project\s*(?:\d{1,2})?|project name|name of project|project title)\s*[:\-]\s*", "", Rx).Trim();
        // Many resumes use an em dash to separate project title and local tech stack,
        // e.g. "Merchant Risk Platform — Java, Kafka, Redis".
        // Keep that stack in local evidence, but don't append it to the domain title.
        var emDash = header.IndexOf('—');
        if (emDash > 3 && emDash < header.Length - 3)
            header = header[..emDash].Trim();
        // First field is the title; the remainder is this project's first description line.
        var titleMatch = Search(header, @"^(?<title>[^:]{3,110}?)\s*(?:\((?<year>(?:19|20)\d{2}(?:\s*[-–]\s*(?:19|20)\d{2})?)\))?\s*(?::\s*(?<body>.+))?$");
        if (!titleMatch.Success) return null;
        var title = titleMatch.Groups["title"].Value.Trim(' ', '-', '.', '•', '●');
        if (title.Length < 4 || title.Length > 100 || Search(title, @"^(?:skills|responsibilit|technology|education|work experience|professional summary|tools|environment)\b").Success)
            return null;
        var body = new List<string>();
        if (titleMatch.Groups["body"].Success) body.Add(titleMatch.Groups["body"].Value.Trim());
        body.AddRange(block.Skip(1));
        var description = string.Join(" ", body);
        // Remove labeled responsibility/technology clauses from a project description; they remain in evidence.
        description = Regex.Replace(description, @"\b(?:responsibilities|responsibility|role)\s*:\s*.*$", "", Rx).Trim();
        description = Regex.Replace(description, @"\b(?:tech stack|technology stack used|technologies used|technologies|tools|environment)\s*:\s*.*$", "", Rx).Trim();
        description = Regex.Replace(description, @"\s+", " ").Trim(' ', '-', '•', '●');
        var titleYears = Search(header, @"\b(?:19|20)\d{2}\b(?:\s*[-–]\s*\b(?:19|20)\d{2}\b)?");
        var localYear = titleYears.Success ? titleYears.Value : Search(full, @"\b(?:19|20)\d{2}\b(?:\s*[-–]\s*\b(?:19|20)\d{2}\b)?").Value;
        var years = Regex.Matches(localYear, @"(?:19|20)\d{2}").Select(m => m.Value).ToArray();
        var year = years.Length == 0 ? "" : years[^1];
        description = TakeWords(description, 34);
        var responsibilities = Search(full, @"\b(?:responsibility|responsibilities|role)\s*:\s*([^.;]+)");
        var sourceTitle = Regex.Replace(title, @"\s*\(\s*(?:19|20)\d{2}.*?\)", "").Trim();
        var domain = DomainFor(sourceTitle, full);
        return new ResumeProject
        {
            Title = domain, Domain = domain, Year = year, Description = description,
            Responsibilities = responsibilities.Success ? TakeWords(responsibilities.Groups[1].Value.Trim(), 20) : "",
            TechnologyStack = Skills.Where(s => IsSkill(full, s)).ToList(),
            Evidence = string.Join("\n", block)
        };
    }

    internal static string DomainFor(string title, string localEvidence)
    {
        var lower = (title + " " + localEvidence).ToLowerInvariant();
        (string[] tokens, string domain)[] cases =
        [
            (["amazon", "shopping", "ecommerce", "e-commerce", "retail", "marketplace", "checkout", "online store"], "E-commerce Website"),
            (["merchant risk", "fraud", "risk scoring"], "Merchant Risk and Fraud Detection Platform"),
            (["inventory", "stock", "warehouse"], "Inventory and Order Management System"),
            (["kyc", "onboarding"], "Customer Onboarding and Verification Portal"),
            (["notification", "email/sms/push", "push delivery"], "Event-Driven Notification Service"),
            (["team collaboration", "workspace", "channels", "mentions"], "Team Collaboration Platform"),
            (["subscription", "billing console", "invoices"], "Subscription and Billing Platform"),
            (["photography", "gallery", "galleries"], "Photography Portfolio and Booking Platform"),
            (["transcript", "youtube", "summariz", "speech to text"], "Video Transcript Summarization Application"),
            (["farming", "agricultur", "crop", "soil moisture"], "IoT Smart Farming System"),
            (["women safety", "emergency response", "sos", "safety alert"], "Personal Safety and Emergency Response Application"),
            (["hospital", "patient", "medical", "healthcare", "doctor"], "Healthcare Management Application"),
            (["university", "education", "course", "learning", "exam", "student"], "Education and Learning Platform"),
            (["bank", "finance", "financial", "transaction", "billing", "payment"], "Financial Services Application"),
            (["portfolio"], "Personal Portfolio Website"),
            (["warehouse", "logistics", "delivery", "routing", "fleet"], "Logistics and Delivery Platform"),
            (["hotel", "reservation", "booking"], "Booking and Reservation Platform"),
            (["helpdesk", "customer service", "support", "ticket"], "Customer Support Management Platform"),
            (["leave", "employee", "hrm", "human resources", "payroll"], "Employee Management System"),
            (["weather", "forecast"], "Weather Analytics Dashboard"),
            (["analytics", "dashboard", "visualization", "report"], "Business Analytics Dashboard"),
            (["recruit", "resume", "applicant", "candidate"], "Recruitment Management Platform"),
            (["chatbot", "assistant", "conversational"], "Conversational AI Application"),
            (["api", "microservice", "backend"], "Backend Services Platform"),
            (["website", "web portal", "web application"], "Web Application"),
            (["mobile", "android", "ios"], "Mobile Application")
        ];
        foreach (var (tokens, domain) in cases) if (tokens.Any(lower.Contains)) return domain;
        // Never expose a potentially private company/product name as the public title.
        return "Software Application";
    }

    internal static string TakeWords(string text, int count) => string.Join(" ",
        Regex.Split(text.Trim(), @"\s+").Where(w => w.Length > 0).Take(count));

    private static int Relevance(ResumeProject project, string jd)
    {
        if (string.IsNullOrWhiteSpace(jd)) return 0;
        var terms = Regex.Matches(jd.ToLowerInvariant(), @"[a-z][a-z0-9.+#/-]{2,}").Select(m => m.Value).ToHashSet();
        var local = project.Evidence + " " + project.Domain + " " + string.Join(" ", project.TechnologyStack);
        return Regex.Matches(local.ToLowerInvariant(), @"[a-z][a-z0-9.+#/-]{2,}").Select(m => m.Value).Distinct().Count(terms.Contains);
    }
}
