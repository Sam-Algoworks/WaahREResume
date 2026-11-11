using System.Text.RegularExpressions;
using RoleFit.Api.Models;

namespace RoleFit.Api.Services;

/// <summary>
/// A source-only second pass over the original resume's Projects section.
/// Repairs project headings that PDF readers split/flatten, without asking the LLM
/// to invent or count project entries. This does NOT modify ATS scoring.
/// </summary>
public static class SourceProjectReconciler
{
    private static readonly RegexOptions O = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(150);
    private static readonly HashSet<string> ProjectSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "projects", "selected projects", "personal projects", "academic projects", "project experience",
        "key projects", "relevant projects", "professional projects", "projects undertaken", "additional projects",
        "selected enterprise projects", "research projects", "portfolio projects"
    };
    private static readonly HashSet<string> OtherSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "education", "qualifications", "academic qualifications", "certifications", "certificates",
        "professional development", "skills", "core skills", "technical skills", "technology stack",
        "work experience", "professional experience", "employment history", "experience", "training",
        "awards", "achievements", "publications", "interests", "languages", "references", "coursework",
        "relevant strengths", "volunteering", "leadership", "about me", "profile", "professional summary"
    };
    private static readonly Regex Action = new(
        @"^(?:developed|built|implemented|created|designed|integrated|managed|tested|configured|enhanced|optimized|worked|introduced|deployed|automated|delivered|led|reduced|increased|responsible|supported|assisted|participated|conducted|maintained|utilized|performed|contributed|collaborated|secured|mapped|observed|identified|provided|used|prepared|reviewed|improved|leveraged|completed|analyzed|analysed|generated)\b",
        O | RegexOptions.Compiled, Timeout);
    private static readonly Regex Technologies = new(
        @"^(?:technology|technologies|technology stack(?: used)?|tech stack(?: used)?|tools(?: used)?|frameworks?|languages?|environment|design tools|methods|platforms|key technologies)\s*[:\-]",
        O | RegexOptions.Compiled, Timeout);
    private static readonly Regex Numbered = new(@"^\d{1,2}[.)]\s+", O | RegexOptions.Compiled, Timeout);
    private static readonly Regex Labeled = new(@"^(?:project(?:\s*\d{1,2})?|project title|project name|name of project)\s*[:\-]\s*", O | RegexOptions.Compiled, Timeout);
    private static readonly Regex Year = new(@"\b(?:19|20)\d{2}\b", O | RegexOptions.Compiled, Timeout);
    private static readonly Regex Phone = new(@"(?:\+\d{1,3}[\s.\-]?)?(?:\d[\s.\-]?){9,13}\d", O | RegexOptions.Compiled, Timeout);
    private static readonly Regex Email = new(@"[\w.+-]+@[\w.-]+\.[a-z]{2,}", O | RegexOptions.Compiled, Timeout);

    /// <summary>Contact details are deliberately absent from internal parsed drafts and exports.</summary>
    public static void RemoveContact(ResumeProfile profile)
    {
        profile.Email = "";
        profile.Phone = "";
        if (IsContactOrLocation(profile.Designation)) profile.Designation = "";
        profile.ProfessionalSummary = CleanPublicText(profile.ProfessionalSummary);
        foreach (var project in profile.Projects)
        {
            project.Description = CleanPublicText(project.Description);
            project.Responsibilities = CleanPublicText(project.Responsibilities);
        }
    }

    private static string CleanPublicText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        return Regex.Replace(Phone.Replace(Email.Replace(text, ""), ""), @"\s{2,}", " ").Trim();
    }

    private static bool IsContactOrLocation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var s = value.Trim();
        return Email.IsMatch(s) || Phone.IsMatch(s) || s.StartsWith("+", StringComparison.Ordinal) ||
            Regex.IsMatch(s, @"^(?:phone|mobile|contact|address|location|city|linkedin|github|portfolio)\b", O, Timeout) ||
            s.Contains("http", StringComparison.OrdinalIgnoreCase) ||
            (Regex.IsMatch(s, @"\b(?:india|pune|delhi|mumbai|noida|gurugram|hyderabad|bangalore|bengaluru)\b", O, Timeout)
              && !Regex.IsMatch(s, @"\b(?:designer|engineer|developer|architect|analyst|manager)\b", O, Timeout));
    }

    /// <summary>Preserves verified existing projects if the source has no safe, recognizable project section.</summary>
    public static void Apply(ResumeProfile profile, string originalText)
    {
        RemoveContact(profile);
        var recovered = Extract(originalText);
        if (recovered.Count == 0)
        {
            // Do not add placeholder projects merely because AI proposed one.
            profile.Projects = profile.Projects
                .Where(p => !string.IsNullOrWhiteSpace(p.Evidence) &&
                    p.Evidence.Length >= 20 && !string.IsNullOrWhiteSpace(p.Description))
                .Take(10).ToList();
            return;
        }

        // Canonical project records always come from project-local SOURCE evidence.
        // This deliberately discards any AI-added or cross-project descriptions.
        // Groq may still compose the professional summary, but it cannot change
        // which project sections actually exist in the source document.
        if (recovered.Count > 10)
        {
            // Preserve the parser's evidence-based top-ten selection if the source
            // contains more than ten genuine projects. Never turn the cap into a
            // target, and never include a project without its own source block.
            var prioritized = new List<ResumeProject>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var previous in profile.Projects)
            {
                var match = recovered.FirstOrDefault(r =>
                    !string.IsNullOrWhiteSpace(previous.Evidence) &&
                    ContainsNormalized(previous.Evidence, FirstLine(r.Evidence)));
                if (match is null || !used.Add(FirstLine(match.Evidence))) continue;
                prioritized.Add(match);
                if (prioritized.Count == 10) break;
            }
            foreach (var project in recovered)
            {
                if (prioritized.Count == 10) break;
                if (used.Add(FirstLine(project.Evidence))) prioritized.Add(project);
            }
            profile.Projects = prioritized;
        }
        else profile.Projects = recovered;
        profile.ParserNote = $"{profile.Projects.Count} project(s) recovered from identifiable source headings. Verify the descriptions before export.";
    }

    public static List<ResumeProject> Extract(string source)
    {
        var rawLines = Regex.Split(source.Replace("\r", ""), @"\n");
        var lines = rawLines.Select(s => Regex.Replace(s, @"\s+", " ").Trim())
            .Where(s => s.Length > 0 && s != "•" && s != "●" && s != "" && s != "")
            .ToList();
        var inSection = false;
        var projectLines = new List<string>();
        foreach (var line in lines)
        {
            var marker = NormalizeSection(line);
            if (ProjectSections.Contains(marker)) { inSection = true; continue; }
            if (OtherSections.Contains(marker)) { inSection = false; continue; }
            if (!inSection) continue;
            projectLines.Add(line);
        }
        if (projectLines.Count == 0) return [];
        var sections = new List<List<string>>();
        List<string>? current = null;
        for (var i = 0; i < projectLines.Count; i++)
        {
            var line = projectLines[i];
            var next = i + 1 < projectLines.Count ? projectLines[i + 1] : "";
            var afterNext = i + 2 < projectLines.Count ? projectLines[i + 2] : "";
            if (IsTitle(line, next, afterNext, current is null))
            {
                if (current is { Count: > 0 }) sections.Add(current);
                current = [line];
            }
            else current?.Add(line);
        }
        if (current is { Count: > 0 }) sections.Add(current);
        var result = new List<ResumeProject>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var block in sections)
        {
            var project = BuildProject(block);
            if (project is null) continue;
            if (!seen.Add(NormalizeTitle(FirstLine(project.Evidence)))) continue;
            result.Add(project);
        }
        return result;
    }

    private static bool IsTitle(string candidate, string next, string afterNext, bool first)
    {
        var t = StripBullet(candidate);
        if (t.Length is < 4 or > 140 || Technologies.IsMatch(t) || Action.IsMatch(t) || Email.IsMatch(t)) return false;
        if (Regex.IsMatch(t, @"^(?:duration|role|responsibilities|description|summary|project experience|technology stack|project type|team size|client|key skills)\s*:", O, Timeout)) return false;
        if (Regex.IsMatch(t, @"^(?:page\s*\d+|\d{4}(?:\s*[-–]\s*\d{4})?)$", O, Timeout)) return false;
        var explicitTitle = Numbered.IsMatch(t) || Labeled.IsMatch(t);
        t = Labeled.Replace(Numbered.Replace(t, ""), "").Trim();
        if (t.Length is < 4 or > 125) return false;
        var words = Regex.Matches(t, @"[\p{L}][\p{L}0-9+]*").Select(m => m.Value).ToArray();
        if (words.Length is < 1 or > 16) return false;
        var upper = words.Count(w => char.IsUpper(w[0]));
        var titleLike = upper >= Math.Max(2, (int)Math.Ceiling(words.Length * .45));
        var projectType = Regex.IsMatch(t,
            @"\b(?:project|application|app|dashboard|portal|system|platform|tool|website|model|recommendation|booking|redesign|mapping|analytics|prototype|workspace|design|campaign|study|research|automation|integration|tracking|detection|toolkit)\b", O, Timeout);
        var linkedStack = Technologies.IsMatch(next) || LooksLikeLooseTechRow(next) ||
            (LooksLikeSoloTechnology(next) && Action.IsMatch(StripBullet(afterNext)));
        var nextLooksBody = Action.IsMatch(StripBullet(next)) || next.StartsWith("•") || next.StartsWith("");
        // A lone technology name is not a project. A project title must have its
        // own evidence line, or an explicit numbered/project label.
        if (!explicitTitle && !linkedStack && !nextLooksBody) return false;
        if (!explicitTitle && !titleLike && !projectType && !first) return false;
        if (t.Contains(',') && !projectType && !explicitTitle) return false;
        return true;
    }

    // Some PDF resumes list "Java, Firebase, HTML, CSS | Android Studio" without
    // a "Technologies:" label. Accept it as stack evidence, never as a project title.
    private static bool LooksLikeSoloTechnology(string line)
    {
        var name = StripBullet(line).ToLowerInvariant();
        return name is "python" or "java" or "javascript" or "typescript" or "c#" or "c++"
            or "react" or "android" or "sql" or "figma" or "excel" or "tableau"
            or "power bi" or "photoshop" or "illustrator" or "firebase" or "html" or "css";
    }

    private static bool LooksLikeLooseTechRow(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || Action.IsMatch(StripBullet(line))) return false;
        var text = StripBullet(line).Split('|')[0];
        var pieces = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length < 3 || pieces.Length > 20) return false;
        return pieces.Take(4).All(p => p.Length <= 35 && p.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4);
    }

    private static ResumeProject? BuildProject(IReadOnlyList<string> block)
    {
        if (block.Count == 0) return null;
        var title = Labeled.Replace(Numbered.Replace(StripBullet(block[0]), ""), "").Trim();
        var yearMatch = Year.Match(title);
        var year = yearMatch.Success ? yearMatch.Value : "";
        title = Regex.Replace(title, @"\s*[|–—-]\s*(?:19|20)\d{2}(?:\s*[-–]\s*(?:19|20)\d{2})?\s*$", "").Trim();
        title = Regex.Replace(title, @"\s*\((?:19|20)\d{2}(?:[-–](?:19|20)\d{2})?\)\s*$", "").Trim();
        var metadataSeparator = title.IndexOf('|');
        if (metadataSeparator > 3) title = title[..metadataSeparator].Trim();
        title = title.TrimEnd(' ', '.');
        if (title.Length < 4) return null;
        var body = block.Skip(1).Where(s => s.Length > 0).ToArray();
        if (body.Length == 0) return null;
        var evidence = string.Join("\n", block);
        var narrative = new List<string>();
        var technology = new List<string>();
        var seenTech = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool followingTech = false;
        foreach (var raw in body)
        {
            var cleaned = StripBullet(raw);
            var match = Technologies.Match(cleaned);
            if (match.Success || LooksLikeLooseTechRow(cleaned) || LooksLikeSoloTechnology(cleaned))
            {
                followingTech = true;
                AddTechnology(match.Success ? cleaned[match.Length..] : cleaned.Replace('|', ','));
                continue;
            }
            if (followingTech && (cleaned.StartsWith("(") || cleaned.StartsWith(",")))
            {
                AddTechnology(cleaned); continue;
            }
            followingTech = false;
            if (cleaned.Length == 0 || Regex.IsMatch(cleaned, @"^(?:role|team|duration|client)\s*:", O, Timeout)) continue;
            narrative.Add(cleaned);
        }
        void AddTechnology(string text)
        {
            foreach (var piece in text.TrimStart(' ', ':', '-', '–', '—').Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var skill = piece.Trim().TrimEnd('.', ';');
                if (skill.Length is > 1 and < 75 && seenTech.Add(skill)) technology.Add(skill);
            }
        }
        var description = ResumeProfileParser.TakeWords(string.Join(" ", narrative), 34);
        if (description.Length == 0 && technology.Count == 0) return null;
        var domain = Domain(title, evidence);
        return new ResumeProject
        {
            Title = domain, Domain = domain, Year = year, Description = description,
            Responsibilities = "", TechnologyStack = technology.Take(25).ToList(), Evidence = evidence
        };
    }

    private static string Domain(string title, string localEvidence)
    {
        var t = title.ToLowerInvariant();
        if (t.Contains("travel") || t.Contains("traveler") || t.Contains("tourism")) return "Travel Recommendation Platform";
        if (t.Contains("carbon") || t.Contains("eco path") || t.Contains("emission") || t.Contains("sustainability")) return "Carbon Emissions and Sustainability Platform";
        if (t.Contains("star") || t.Contains("astronom") || t.Contains("celestial")) return "Interactive Astronomy Visualization Tool";
        if (t.Contains("accident") || t.Contains("road safety") || t.Contains("collision")) return "Accident Detection and Emergency Alert System";
        var generic = ResumeProfileParser.DomainFor(title, localEvidence);
        if (!string.Equals(generic, "Software Application", StringComparison.OrdinalIgnoreCase)) return generic;
        if (t.Contains("design") || t.Contains("prototype") || t.Contains("wireframe")) return "Digital Product Design Project";
        if (t.Contains("research") || t.Contains("study")) return "Research and Analysis Project";
        return "Professional Project";
    }

    private static string NormalizeSection(string value) => value.Trim(' ', ':', '-', '–', '—', '•').ToLowerInvariant();
    private static string StripBullet(string value) => Regex.Replace(value.Trim(), @"^[•●\-]\s*", "").Trim();
    private static string FirstLine(string value) => value.Split('\n')[0].Trim();
}
