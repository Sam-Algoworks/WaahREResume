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
        "Django REST Framework", "Django", "FastAPI", "Flask", "Bootstrap", "Plotly", "scikit-learn", "NumPy", "pandas", "Gunicorn", "Heroku", "Nginx", "AWS S3", "RESTful APIs", "ORM", "JWT Auth", "Pytest", "C", "HTML5", "CSS3", "Selenium", "Playwright", "Cypress", "JUnit", "NUnit", "xUnit", "CI/CD",
        "Jenkins", "Linux", "Bash", "Agile", "Figma", "UI/UX", "Kafka", "Spring Boot",
        "XGBoost", "LightGBM", "TensorFlow", "PyTorch", "MLflow", "SARIMAX", "Prophet", "Airflow", "Apache Spark",
        "dbt", "Redshift", "SageMaker", "AWS EMR", "AWS ECS", "OAuth2", "Spring Security", "Helm", "S3", "GraphQL"
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
        ["IoT"] = ["iot", "internet of things"],
        ["React.js"] = ["react.js"],
        ["Django REST Framework"] = ["django rest framework", "drf"],
        ["scikit-learn"] = ["scikit-learn", "sklearn"],
        ["RESTful APIs"] = ["restful apis"],
        ["JWT Auth"] = ["jwt auth", "jwt-secured", "jwt authentication"],
        ["AWS S3"] = ["aws s3", "s3 storage"],
        ["HTML5"] = ["html5"],
        ["CSS3"] = ["css3"]
    };

    private static readonly HashSet<string> KnownHeadings = new(StringComparer.OrdinalIgnoreCase)
    {
        "summary", "professional summary", "profile", "about me", "objective", "career objective",
        "skills", "core skills", "professional competencies", "technical skills", "key skills", "technologies", "technology stack", "technical proficiency",
        "experience", "work experience", "professional experience", "employment", "employment history",
        "projects", "project experience", "key projects", "academic projects", "personal projects", "selected projects", "relevant projects", "projects undertaken",
        "education", "academic qualifications", "qualifications", "academic background",
        "selected enterprise projects", "additional projects", "selected projects & certifications", "projects and certifications",
        "certifications", "certificates", "licenses and certifications", "coursework", "professional development", "relevant strengths", "awards", "achievements", "interests", "publications", "languages", "contact",
        "architecture & engineering leadership", "selected career impact", "interview-ready project themes", "selected tools & platforms", "ats keywords", "important"
    };
    private static readonly string[] ProjectHeadings =
    ["projects", "project experience", "key projects", "academic projects", "personal projects", "selected projects", "relevant projects", "projects undertaken", "selected enterprise projects", "additional projects"];
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
        // Zero means no explicit professional-years claim was found.
        // Internships and academic projects are preserved elsewhere and are not
        // silently converted into years of full-time experience.
        var profile = new ResumeProfile { SourceFileName = Path.GetFileName(fileName), ExperienceYears = 0 };

        var email = Search(text, @"\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b");
        if (email.Success) profile.Email = email.Value;
        var phone = Search(text, @"(?<!\d)(?:\+\d{1,3}[\s.\-]?)?(?:\(?\d{3}\)?[\s.\-]?)?\d{3}[\s.\-]?\d{4}(?!\d)");
        if (phone.Success) profile.Phone = phone.Value;

        // Factual experience comes from explicit professional durations or
        // the non-overlapping employment timeline, never from Groq.
        // Keep exact months for ATS scoring (ResumeEvidence.ExplicitProfessionalMonths).
        profile.ExperienceYears = ResumeEvidence.DisplayYears(text);

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
                // Header forms: "UI/UX DESIGNER | 2 YEARS 8 MONTHS" and
                // "UI/UX Designer | Noida | email@example.com".
                // Contact information and duration must NOT become the designation.
                var next = lines[index + 1];
                var candidate = next.Split('|', '·')[0].Trim();
                if (candidate.Length is >= 4 and <= 90 && !candidate.Contains('@') &&
                    !KnownHeadings.Contains(candidate.Trim(':')) &&
                    !candidate.Contains("http", StringComparison.OrdinalIgnoreCase) &&
                    !Search(candidate, @"\b(?:19|20)\d{2}\b").Success &&
                    !Search(candidate, @"^(?:\d+(?:\.\d+)?\s+years?|fresher)$").Success)
                    profile.Designation = candidate;
            }
        }

        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var projectLinesInOrder = new List<string>();
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
            if (ProjectHeadings.Contains(current, StringComparer.OrdinalIgnoreCase) && !IsPageDecoration(line))
                projectLinesInOrder.Add(line);
        }
        List<string> Section(params string[] names) => names.Where(sections.ContainsKey)
            .SelectMany(name => sections[name]).ToList();

        profile.TechnicalSkills = Skills.Where(s => IsSkill(text, s))
            .Concat(ResumeEvidence.SourceListedSkills(text))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToList();
        var summary = string.Join(" ", Section("professional summary", "summary", "profile", "about me", "objective", "career objective"));
        profile.ProfessionalSummary = TakeWords(summary, 85);
        // Some fresher resumes state the sought designation in the summary,
        // while an unrelated internship title appears later under EXPERIENCE.
        // A sought role is not evidence of full-time work in that role.
        if (string.IsNullOrWhiteSpace(profile.Designation))
        {
            var target = Search(summary,
                @"\b(?:seeking|looking\s+for|applying\s+for|targeting)\s+(?:an?\s+)?(?:SDE\s*/\s*)?(?<role>full[\s-]*stack\s+(?:developer|engineer)|software\s+engineer|data\s+(?:scientist|analyst)|backend\s+developer|frontend\s+developer|qa\s+engineer)\s+(?:role|position|opportunity)\b");
            if (target.Success)
                profile.Designation = Regex.Replace(target.Groups["role"].Value, @"\s+", " ").Trim();
            else
            {
                // Explicit "SDE / Full Stack Developer role" is a common form.
                var fullStack = Search(summary, @"\bfull[\s-]*stack\s+(?:developer|engineer)\b");
                if (fullStack.Success && Search(summary, @"\b(?:seeking|looking\s+for|aspiring|targeting)\b").Success)
                    profile.Designation = Regex.Replace(fullStack.Value, @"\s+", " ").Trim();
            }
        }
        profile.EmploymentHistory = Section("experience", "work experience", "professional experience", "employment", "employment history").Take(30).ToList();
        profile.Education = Section("education", "academic qualifications", "qualifications", "academic background").Take(20).ToList();
        profile.Certifications = Section("certifications", "certificates", "licenses and certifications").Take(20).ToList();

        var projectLines = projectLinesInOrder;
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
        // Headings are established before descriptions: a bullet inside a block
        // must not automatically become another project. Retain source order.
        var blocks = new List<List<string>>();
        List<string>? current = null;
        for (var i = 0; i < projectLines.Count; i++)
        {
            var raw = projectLines[i].Trim();
            if (raw.Length == 0 || IsPageDecoration(raw)) continue;
            var next = i + 1 < projectLines.Count ? projectLines[i + 1] : null;
            var starts = IsStart(raw, current is null, next);
            if (starts && current is { Count: > 0 })
            {
                blocks.Add(current);
                current = null;
            }
            if (starts || current is not null) (current ??= []).Add(raw);
        }
        if (current is { Count: > 0 }) blocks.Add(current);

        var result = new List<ResumeProject>();
        var seenSourceTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var block in blocks)
        {
            var project = ParseBlock(block);
            if (project is null) continue;
            // Deduplicate using the ORIGINAL source heading, not the generic
            // domain. Two unrelated projects may share the same domain name.
            var titleKey = ProjectTitleKey(block[0]);
            if (titleKey.Length > 0 && !seenSourceTitles.Add(titleKey)) continue;
            result.Add(project);
        }
        var selected = result.Select((project, i) => new { project, i });
        // Only reorder when the source contains MORE than the allowed maximum.
        if (result.Count > maxProjects && !string.IsNullOrWhiteSpace(jd))
            selected = selected.OrderByDescending(x => Relevance(x.project, jd)).ThenBy(x => x.i);
        return selected.Take(Math.Clamp(maxProjects, 0, 100)).Select(x => x.project).ToList();
    }

    private static bool IsPageDecoration(string line) =>
        Search(line, @"^(?:made with wahresume|wahresume|page\s+\d+|(?:.+?)\s+page\s+\d+)$").Success ||
        Search(line, @"^\s*(?:www\.)?wahresume\.com\b").Success;

    private static readonly string[] DescriptionStarters =
    [
        "built", "developed", "designed", "implemented", "created", "integrated", "managed", "used",
        "worked", "improved", "led", "responsible", "deployed", "automated", "optimized",
        "tested", "configured", "maintained", "trained", "reduced", "resolved", "introduced",
        "established", "partnered", "contributed", "collaborated", "served", "achieved", "added",
        "end-to-end", "stream processing", "content +", "content and", "business context",
        "role:", "result:", "team:", "technology", "tech stack", "tools", "environment"
    ];

    private static bool LooksLikeBulletTitle(string raw)
    {
        var text = Regex.Replace(raw.Trim(), @"^[•●\-]\s*", "").Trim();
        if (text.Length < 7 || text.Length > 115 || text.Contains(':') || text.EndsWith('.') ||
            text.EndsWith(';') || DescriptionStarters.Any(x => text.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
            return false;
        var words = Regex.Matches(text, @"[\p{L}][\p{L}0-9+\-]*").Select(m => m.Value).ToList();
        if (words.Count is < 2 or > 13) return false;
        var capitalized = words.Count(w => char.IsUpper(w[0]));
        return capitalized >= Math.Max(2, (int)Math.Ceiling(words.Count * 0.5));
    }

    // Skill lists and standalone technology names belong to the current project.
    // They are never project headings, even when PDF text extraction gives them
    // a line of their own or a pipe separates a tool from a technology list.
    private static bool LooksLikeTechnologyRow(string raw)
    {
        var line = Regex.Replace(raw.Trim(), @"^[•●\-]\s*", "").Trim();
        if (line.Length == 0) return false;
        if (Skills.Any(s => line.Equals(s, StringComparison.OrdinalIgnoreCase))) return true;

        var beforePipe = line.Split('|')[0].Trim();
        // "Java, XML, Firebase, HTML, CSS, JavaScript | Android Studio"
        // is a tech stack, not a project with title "Java, XML ...".
        var commaItems = beforePipe.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (commaItems.Length >= 3 &&
            Skills.Count(skill => IsSkill(beforePipe, skill)) >= 2)
            return true;

        // Also handle lines like "Python / Flask / SQL" without rejecting
        // valid titles such as "API Gateway / Partner Integration Hub".
        var slashItems = beforePipe.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (slashItems.Length >= 3 &&
            slashItems.Count(item => Skills.Any(s => item.Equals(s, StringComparison.OrdinalIgnoreCase))) >= 2)
            return true;
        return false;
    }

    private static bool LooksLikeProjectHeading(string line, bool first)
    {
        if (LooksLikeTechnologyRow(line)) return false;
        if (line.Length is < 5 or > 100 || line.Contains('|') || line.Contains(':') ||
            line.EndsWith('.') || line.EndsWith(';') || line.Contains(',') ||
            DescriptionStarters.Any(x => line.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
            return false;
        var words = Regex.Matches(line, @"[\p{L}][\p{L}0-9+\-]*").Select(m => m.Value).ToArray();
        if (words.Length is < 1 or > 12) return false;
        // Allow single-word project titles when they are the first item.
        if (words.Length == 1) return first && !LooksLikeTechnologyRow(line);
        var upperInitials = words.Count(w => char.IsUpper(w[0]));
        if (upperInitials >= Math.Max(2, (int)Math.Ceiling(words.Length * 0.60))) return true;
        // Some resumes use lowercase project headings. Require a recognizable
        // *type*, rather than accepting any short line as a new project.
        return Search(line,
            @"\b(?:project|application|app|platform|portal|website|dashboard|system|engine|player|toolkit|microservice|service|model)\s*$").Success;
    }

    private static bool IsStart(string line, bool first, string? next)
    {
        if (LooksLikeTechnologyRow(line)) return false;
        // Enterprise resume: "01 | Payments Platform — Client" or "09 | ..."
        if (Search(line, @"^\d{1,2}\s*\|\s*\S{3,}").Success) return true;
        // Conventional numbering (including 1. Title and 1) Title).
        if (Search(line, @"^\d{1,2}[.)]\s+\S{3,}").Success) return true;
        if (Search(line, @"^project\s*\d{0,2}\s*[:\-]\s*\S{3,}").Success) return true;
        if (Search(line, @"^(?:[-•●]\s*)?(?:name of project|project title)\s*:").Success) return true;
        if (Search(line, @"^[\w][\w &/.'+\-]{3,85}\s*\((?:19|20)\d{2}(?:\s*[-–]\s*(?:19|20)\d{2})?\)\s*[:\-]?").Success) return true;

        // Daniel-style projects: bullet title followed by a separate bullet
        // description; title case distinguishes a name from action bullets.
        if (Search(line, @"^[•●]\s+").Success)
            return LooksLikeBulletTitle(line);
        if (Search(line, @"^[-]\s+").Success) return false;

        // "Personal Finance Dashboard | Django, PostgreSQL | Code" or
        // "SwasthyaGuide Android developer | 2 months | Team of 4".
        // The left segment must look like a title; a comma-separated tech
        // list is not allowed to become an extra project.
        var separator = line.IndexOf('|');
        if (separator is >= 4 and <= 110)
        {
            var title = line[..separator].Trim();
            var remainder = line[(separator + 1)..].Trim();
            if (title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 16 &&
                !LooksLikeTechnologyRow(title) &&
                !Search(title, @"^(?:role|team|languages|frameworks|databases|technical skills|tools|concepts|cloud|ml / data)\b").Success &&
                remainder.Length > 0 &&
                LooksLikeProjectHeading(title, first))
                return true;
        }
        var emDash = line.IndexOf('—');
        if (emDash is >= 5 and <= 110 &&
            LooksLikeProjectHeading(line[..emDash].Trim(), first) &&
            Skills.Any(skill => IsSkill(line[(emDash + 1)..], skill)))
            return true;

        // Never split a project just because a short description/technology
        // line has no punctuation. Require a heading-like standalone title.
        return LooksLikeProjectHeading(line, first);
    }

    private static string ProjectTitleKey(string header)
    {
        var cleaned = Regex.Replace(header.Trim(), @"^(?:\d{1,2}\s*[.)|]\s+|[•●]\s*)", "").Trim();
        cleaned = Regex.Replace(cleaned,
            @"^(?:project\s*(?:\d{1,2})?|project name|name of project|project title)\s*[:\-]\s*", "", Rx);
        var bar = cleaned.IndexOf('|');
        if (bar > 3) cleaned = cleaned[..bar].Trim();
        return Regex.Replace(cleaned.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
    }

    private static ResumeProject? ParseBlock(List<string> block)
    {
        var full = string.Join(" ", block);
        var header = block[0];
        header = Regex.Replace(header, @"^(?:\d{1,2}\s*[.)|]\s+|[•●]\s*)", "").Trim();
        header = Regex.Replace(header, @"^(?:project\s*(?:\d{1,2})?|project name|name of project|project title)\s*[:\-]\s*", "", Rx).Trim();
        // Pipe-delimited heading: keep the title to the left and preserve the
        // technology list on the right in the block's evidence.
        var pipe = header.IndexOf('|');
        if (pipe > 3)
            header = header[..pipe].Trim();
        // Many resumes use an em dash to separate project title and local tech stack,
        // e.g. "Merchant Risk Platform — Java, Kafka, Redis".
        // Keep that stack in local evidence, but don't append it to the domain title.
        var emDash = header.IndexOf('—');
        if (emDash > 3 && emDash < header.Length - 3)
        {
            if (header.StartsWith("Personal Project", StringComparison.OrdinalIgnoreCase))
                header = header[(emDash + 1)..].Trim();
            else
                header = header[..emDash].Trim();
        }
        // First field is the title; the remainder is this project's first description line.
        var titleMatch = Search(header, @"^(?<title>[^:]{3,110}?)\s*(?:\((?<year>(?:19|20)\d{2}(?:\s*[-–]\s*(?:19|20)\d{2})?)\))?\s*(?::\s*(?<body>.+))?$");
        if (!titleMatch.Success) return null;
        var title = titleMatch.Groups["title"].Value.Trim(' ', '-', '.', '•', '●');
        if (title.Length < 4 || title.Length > 100 || Search(title, @"^(?:skills|responsibilit|technology|education|work experience|professional summary|tools|environment)\b").Success)
            return null;
        // The employer/company may appear after a dash in the heading. It
        // belongs to the private evidence, not the generic project title.
        title = Regex.Replace(title, @"\s+[—–]\s+[^—–]+$", "").Trim();
        var body = new List<string>();
        if (titleMatch.Groups["body"].Success) body.Add(titleMatch.Groups["body"].Value.Trim());
        body.AddRange(block.Skip(1));
        // Keep narrative lines even when a project begins with "Role: ...".
        // Greedy stripping from a concatenated string used to delete the entire
        // business context and bullets in enterprise-format resumes.
        var narrative = body
            .Where(line => !Search(line, @"^(?:role|team|technology stack used|technologies used|tech stack|tools|environment)\s*:").Success)
            .Select(line => Regex.Replace(line, @"^(?:business context|description|overview)\s*:\s*", "", Rx))
            .Select(line => Regex.Replace(line, @"^[•●\-]\s*", "").Trim())
            .Where(line => line.Length > 0 && line != "•" && !IsPageDecoration(line));
        var description = TakeWords(Regex.Replace(string.Join(" ", narrative), @"\s+", " ").Trim(), 34);
        var titleYears = Search(header, @"\b(?:19|20)\d{2}\b(?:\s*[-–]\s*\b(?:19|20)\d{2}\b)?");
        var localYear = titleYears.Success ? titleYears.Value : Search(full, @"\b(?:19|20)\d{2}\b(?:\s*[-–]\s*\b(?:19|20)\d{2}\b)?").Value;
        var years = Regex.Matches(localYear, @"(?:19|20)\d{2}").Select(m => m.Value).ToArray();
        // An ongoing year range has no verified final year.
        var year = years.Length == 0 || Search(full, @"(?:19|20)\d{2}\s*[-–]\s*(?:present|ongoing|current)\b").Success
            ? "" : years[^1];
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
            (["readmission risk", "readmission scoring"], "Patient Readmission Risk Prediction Platform"),
            (["demand forecasting", "multi-horizon demand"], "Retail Demand Forecasting Platform"),
            (["churn prediction", "retention engine"], "Customer Churn Prediction and Retention Platform"),
            (["recommendation microservice", "recommender"], "Recommendation Engine Microservice"),
            (["payments orchestration", "payment routing"], "Payment Orchestration Platform"),
            (["monolith modernization", "modernization"], "Enterprise Modernization Platform"),
            (["omnichannel order management"], "Omnichannel Order Management Platform"),
            (["identity & access", "identity and access"], "Identity and Access Management Platform"),
            (["dynamic pricing", "promotion engine"], "Pricing and Promotions Platform"),
            (["customer data platform"], "Customer Data Platform"),
            (["logistics tracking", "shipment"], "Logistics Tracking Platform"),
            (["insurance claims"], "Insurance Claims Management Platform"),
            (["retail inventory", "inventory availability"], "Retail Inventory Availability Platform"),
            (["api gateway", "partner integration hub"], "API Gateway and Partner Integration Platform"),
            (["data warehouse", "enterprise reporting"], "Enterprise Reporting and Data Warehouse"),
            (["document processing portal"], "Secure Document Processing Portal"),
            (["engineering delivery intelligence"], "Engineering Delivery Analytics Platform"),
            (["cloud cost guardrails"], "Cloud Cost Governance Toolkit"),
            (["amazon", "shopping", "ecommerce", "e-commerce", "retail", "marketplace", "checkout", "online store"], "E-commerce Website"),
            (["personal finance dashboard", "spending insights", "expense tracker"], "Personal Finance Dashboard"),
            (["fake news", "news detection", "tf-idf", "news classification"], "News Authenticity Detection System"),
            (["micro-blogging", "microblogging", "chirper", "follow-graph"], "Microblogging and Social Networking Platform"),
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
            (["government medical schemes", "medical schemes", "medical records", "healthcare services"], "Healthcare Services and Medical Records Application"),
            (["media player", "multimedia player", "multimedia application", "audio player", "video player"], "Media Player Application"),
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
        var titleOnly = title.ToLowerInvariant();
        foreach (var (tokens, domain) in cases) if (tokens.Any(titleOnly.Contains)) return domain;
        // A private/brand-specific source title can have no recognizable words.
        // Use the project's OWN evidence to infer a generic domain (never the
        // whole resume), e.g., a healthcare scheme project or media player.
        foreach (var (tokens, domain) in cases)
            if (tokens.Any(token => lower.Contains(token, StringComparison.OrdinalIgnoreCase)))
                return domain;
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
