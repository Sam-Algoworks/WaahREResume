using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RoleFit.Api.Models;

namespace RoleFit.Api.Services;

/// <summary>
/// Deterministic, source-bound project reconstruction. Works across long/short,
/// numbered/unnumbered, and multi-section resumes. Groq never supplies a project's
/// boundary, source description, or per-project technologies here.
/// </summary>
public static class SourceProjectReconciler
{
    private static readonly RegexOptions Rx = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
    private static readonly TimeSpan TimeLimit = TimeSpan.FromMilliseconds(250);

    private static readonly HashSet<string> ProjectHeadings = new(StringComparer.OrdinalIgnoreCase)
    {
        "projects", "project experience", "selected projects", "selected enterprise projects",
        "additional projects", "professional projects", "academic projects", "personal projects",
        "portfolio projects", "research projects", "key projects", "relevant projects",
        "projects undertaken", "selected client projects", "notable projects",
        "project portfolio", "featured projects"
    };

    // Only true section headings may stop a project block. Never stop because
    // ordinary narrative mentions "education", "leadership", or "skills".
    private static readonly HashSet<string> EndHeadings = new(StringComparer.OrdinalIgnoreCase)
    {
        "education", "academic qualifications", "academic background", "qualifications",
        "skills", "core skills", "key skills", "technical skills", "technical proficiency",
        "technology stack", "technology skills", "core competencies", "professional competencies",
        "professional experience", "work experience", "employment", "employment history",
        "certifications", "licenses and certifications", "certificates", "coursework",
        "relevant strengths", "summary", "professional summary", "executive summary",
        "profile", "professional profile", "career objective", "objective", "about me",
        "architecture & engineering leadership", "architecture and engineering leadership",
        "engineering leadership", "selected career impact", "career highlights",
        "selected tools & platforms", "selected tools and platforms", "tools & platforms",
        "interview-ready project themes", "interview ready project themes", "ats keywords",
        "awards", "achievements", "interests", "languages", "references", "publications",
        "professional development", "education & professional development",
        "education and professional development", "training", "volunteering", "important",
        "additional information", "leadership", "security", "contact"
    };

    private static readonly Regex NumberedTitle = new(
        @"^\s*(?<number>\d{1,3})\s*[.)|]\s+(?<title>.+)$", Rx | RegexOptions.Compiled, TimeLimit);
    private static readonly Regex LabeledTitle = new(
        @"^\s*(?:project\s*(?:\d{1,3})?|project title|project name|name of project)\s*[:\-–—]\s*(?<title>\S.+)$",
        Rx | RegexOptions.Compiled, TimeLimit);
    private static readonly Regex TechLabel = new(
        @"^(?:technologies?|technology stack(?: used)?|tech stack(?: used)?|tools(?: used)?|key technologies|frameworks?|programming languages?|platforms used|environment|(?:design|research|ux|project)\s+(?:tools|methods)(?: used)?|tools?\s*[/&]\s*methods?(?: used)?|methods?(?: used)?|techniques?(?: used)?)\s*[:\-–—]\s*",
        Rx | RegexOptions.Compiled, TimeLimit);
    private static readonly Regex BodyLabel = new(
        @"^(?:role|team(?: size)?|duration|client|employer|company|business context|project context|description|responsibilities|outcome|results?|impact|achievement|scope|year)\s*[:\-–—]",
        Rx | RegexOptions.Compiled, TimeLimit);
    private static readonly Regex Action = new(
        @"^(?:redesigned|prototyped|constructed|ran|drafted|validated|researched|developed|built|implemented|created|designed|integrated|managed|tested|configured|enhanced|optimized|optimised|worked|introduced|deployed|automated|delivered|led|reduced|increased|supported|assisted|participated|conducted|maintained|utilized|performed|contributed|collaborated|secured|mapped|observed|identified|provided|used|prepared|reviewed|improved|leveraged|completed|analyzed|analysed|generated|migrated|defined|established|partnered|consolidated|profiled|handled|moved|added|recommended|documented|owned|simplified|monitored|planned|launched|trained|facilitated|reworked|translated|produced|presented)\b",
        Rx | RegexOptions.Compiled, TimeLimit);
    private static readonly Regex YearPattern = new(@"\b(?:19|20)\d{2}\b", Rx | RegexOptions.Compiled, TimeLimit);
    private static readonly Regex EmailPattern = new(@"[\w.+-]+@[\w.-]+\.[a-z]{2,}", Rx | RegexOptions.Compiled, TimeLimit);
    private static readonly Regex PhonePattern = new(@"(?<!\d)(?:\+\d{1,3}[\s.-]?)?(?:\d[\s.-]?){9,13}\d(?!\d)", Rx | RegexOptions.Compiled, TimeLimit);
    private static readonly Regex PageDecoration = new(
        @"(?:^page\s*\d+(?:\s+of\s+\d+)?$|\bpage\s+\d+\s*$|^<parsed text for page:|^\d{1,3}\s*/\s*\d{1,3}\s*$)",
        Rx | RegexOptions.Compiled, TimeLimit);

    public static void RemoveContact(ResumeProfile profile)
    {
        profile.Email = "";
        profile.Phone = "";
        if (IsContactDesignation(profile.Designation)) profile.Designation = "";
        profile.ProfessionalSummary = RemovePrivateContact(profile.ProfessionalSummary);
        foreach (var p in profile.Projects)
        {
            p.Description = RemovePrivateContact(p.Description);
            p.Responsibilities = RemovePrivateContact(p.Responsibilities);
        }
    }

    private static bool IsContactDesignation(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim();
        return EmailPattern.IsMatch(s) || PhonePattern.IsMatch(s)
            || s.StartsWith("+", StringComparison.Ordinal)
            || Regex.IsMatch(s, @"^(?:phone|mobile|contact|address|location|city|linkedin|github|portfolio)\b", Rx, TimeLimit)
            || s.Contains("http", StringComparison.OrdinalIgnoreCase)
            || (Regex.IsMatch(s, @"\b(?:india|pune|delhi|mumbai|noida|gurugram|hyderabad|bengaluru)\b", Rx, TimeLimit)
                && !Regex.IsMatch(s, @"\b(?:designer|engineer|developer|architect|analyst|manager)\b", Rx, TimeLimit));
    }

    private static string CleanSkillLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return Regex.Replace(value.Trim(),
            @"^(?:(?:[\p{L}\p{N}/& ]+\s+)?(?:skills?|tools?|libraries?|methods?|platforms?|technologies|frameworks?|databases?)|libraries\s*/\s*tools)\s*[:\-–—]\s*",
            "", Rx, TimeLimit).Trim();
    }

    /// <summary>
    /// A resume that labels a credential as a *sample* is not proof that it was
    /// earned. Remove AI clauses asserting that very credential was held/earned.
    /// We cannot fully prove all AI-written prose here; this gate is conservative.
    /// </summary>
    private static string RemoveUnsupportedSampleCredentialClaims(string? summary, string source)
    {
        if (string.IsNullOrWhiteSpace(summary) || string.IsNullOrWhiteSpace(source)) return summary ?? "";
        var sampleLines = source.Replace("\r", "").Split('\n')
            .Where(line => Regex.IsMatch(line,
                @"\b(?:certificate|certification|credential|license)\b", Rx, TimeLimit)
                && Regex.IsMatch(line, @"\b(?:sample|fictional|hypothetical|example)\b", Rx, TimeLimit))
            .Take(12).ToArray();
        var cleaned = summary.Trim();
        foreach (var line in sampleLines)
        {
            var title = Regex.Replace(line, @"\([^)]*\b(?:sample|fictional|hypothetical|example)\b[^)]*\)", "", Rx, TimeLimit).Trim();
            title = Regex.Replace(title, @"^[^:]{0,50}:\s*", "", Rx, TimeLimit).Trim(' ', '.', '-', '–', '—');
            if (title.Length < 8 || title.Length > 90 || !cleaned.Contains(title, StringComparison.OrdinalIgnoreCase)) continue;

            // E.g. "... and holds a [sample] Certificate earned in 2025."
            // Remove only the unsupported clause, keeping earlier evidenced prose.
            var assertion = Regex.Match(cleaned,
                @"(?:,?\s+and\s+|\s+)(?:holds?|earned|obtained|received|awarded|completed|certified\s+in)\b[^.!?]{0,140}" +
                Regex.Escape(title) + @"[^.!?]*", Rx, TimeLimit);
            if (assertion.Success)
            {
                cleaned = (cleaned[..assertion.Index] + cleaned[(assertion.Index + assertion.Length)..]).Trim();
                cleaned = Regex.Replace(cleaned, @"[,;]\s*\.", ".").Trim();
                cleaned = Regex.Replace(cleaned, @"\s+\.", ".").Trim();
            }
            else
            {
                // A standalone unsupported credential sentence is omitted.
                var sentences = Regex.Split(cleaned, @"(?<=[.!?])\s+");
                cleaned = string.Join(" ", sentences.Where(sentence =>
                    !sentence.Contains(title, StringComparison.OrdinalIgnoreCase))).Trim();
            }
        }
        return cleaned;
    }

    private static string RemovePrivateContact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return Regex.Replace(PhonePattern.Replace(EmailPattern.Replace(value, ""), ""), @"\s{2,}", " ").Trim();
    }

    /// <summary>
    /// Must be called AFTER Groq enrichment and BEFORE saving or returning a draft.
    /// The original resume text is the canonical source for all project records.
    /// </summary>
    public static void Apply(ResumeProfile profile, string originalText)
    {
        RemoveContact(profile);
        profile.TechnicalSkills = (profile.TechnicalSkills ?? new List<string>())
            .Select(CleanSkillLabel).Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        profile.ProfessionalSummary = RemoveUnsupportedSampleCredentialClaims(
            profile.ProfessionalSummary, originalText);
        var verified = Extract(originalText);
        if (verified.Count > 0)
        {
            // Only add a skill when its wording occurs in THIS project's block;
            // a skill from another project must never migrate here.
            foreach (var project in verified)
            {
                foreach (var skill in profile.TechnicalSkills)
                {
                    if (!SupportedLocally(project.Evidence, skill)) continue;
                    if (project.TechnologyStack.Contains(skill, StringComparer.OrdinalIgnoreCase)) continue;
                    if (project.TechnologyStack.Count >= 25) break;
                    project.TechnologyStack.Add(skill);
                }
            }
            profile.Projects = SelectUpToTen(verified);
        }
        else
        {
            // If there is no independently identified source heading, retain
            // only already source-evidenced records; never invent a placeholder.
            profile.Projects = profile.Projects
                .Where(p => !string.IsNullOrWhiteSpace(p.Evidence) &&
                            p.Evidence.Length >= 20 &&
                            Normalize(originalText).Contains(Normalize(FirstLine(p.Evidence)), StringComparison.Ordinal))
                .Take(10).ToList();
        }
        RemoveContact(profile);
    }

    public static List<ResumeProject> Extract(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return [];
        var lines = source.Replace("\r", "").Split('\n')
            .Select(NormalizeLine).Where(x => x.Length > 0 && !PageDecoration.IsMatch(x)).ToList();
        if (lines.Count == 0) return [];

        var projectBlocks = new List<List<string>>();
        List<string>? current = null;
        var insideProjects = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var heading = NormalizeHeading(line);
            if (ProjectHeadings.Contains(heading))
            {
                Flush();
                insideProjects = true;
                continue;
            }
            if (EndHeadings.Contains(heading) || IsOtherSectionHeading(line))
            {
                Flush();
                insideProjects = false;
                continue;
            }
            if (!insideProjects) continue;

            var next = i + 1 < lines.Count ? lines[i + 1] : "";
            var afterNext = i + 2 < lines.Count ? lines[i + 2] : "";
            if (IsProjectHeading(line, next, afterNext))
            {
                Flush();
                current = [line];
            }
            else
            {
                current?.Add(line);
            }
        }
        Flush();

        var projects = new List<ResumeProject>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var block in projectBlocks)
        {
            var project = BuildProject(block);
            if (project is null) continue;
            // Deduplicate by actual independent heading, not by generic domain.
            var key = Normalize(CleanSourceTitle(block[0]));
            if (seen.Add(key)) projects.Add(project);
        }
        return projects;

        void Flush()
        {
            if (current is { Count: > 0 }) projectBlocks.Add(current);
            current = null;
        }
    }

    private static bool IsOtherSectionHeading(string raw)
    {
        // Recognize unseen *section headings* conservatively. Do not reject an
        // uppercase project title just because it is capitalized.
        var text = NormalizeHeading(raw);
        if (text.Length > 80 || text.Length < 3 || raw.Contains('|') || raw.Contains(':')) return false;
        return Regex.IsMatch(text,
            @"^(?:education|certification|leadership|skills|summary|career|professional development|training|awards|achievements|publications|references|interests|tools|architecture|interview|contact|personal details|additional information)\b.*$",
            Rx, TimeLimit) && !Regex.IsMatch(text, @"\b(?:platform|application|app|project|tool|system|dashboard|portal|engine|website|service|framework)\b", Rx, TimeLimit);
    }

    private static bool IsProjectHeading(string raw, string next, string afterNext)
    {
        var line = TrimBullet(raw);
        if (TechLabel.IsMatch(line) || BodyLabel.IsMatch(line) || Action.IsMatch(line)
            || EmailPattern.IsMatch(line) || PageDecoration.IsMatch(line)) return false;
        var numbered = NumberedTitle.Match(line);
        var labeled = LabeledTitle.Match(line);
        var explicitHeading = numbered.Success || labeled.Success;
        var title = CleanSourceTitle(line);
        if (title.Length is < 4 or > 145) return false;
        if (Regex.IsMatch(title, @"^(?:role|team|result|business context|technologies|tools|description)\b\s*:", Rx, TimeLimit)) return false;
        var words = Regex.Matches(title, @"[\p{L}][\p{L}\p{N}+#]*").Select(m => m.Value).ToArray();
        if (words.Length < 1 || words.Length > 19) return false;
        if (explicitHeading) return true;

        var nextLine = TrimBullet(next);
        var following = TrimBullet(afterNext);
        var supported = TechLabel.IsMatch(nextLine)
            || LooksLikeTechnologyList(nextLine)
            || BodyLabel.IsMatch(nextLine)
            || Action.IsMatch(nextLine)
            || IsBulletLine(next)
            || (IsSingleSkill(nextLine) && (Action.IsMatch(following) || IsBulletLine(afterNext)));
        if (!supported) return false;
        // A normal prose sentence must never become a new project heading.
        if (title.EndsWith(';') || (title.EndsWith('.') && !TechLabel.IsMatch(nextLine))) return false;
        if (title.Contains(',') && !Regex.IsMatch(title, @"\b(?:application|system|platform|service|project)\b", Rx, TimeLimit)) return false;
        var capitalized = words.Count(w => char.IsUpper(w[0]));
        var isTitleCase = capitalized >= Math.Max(1, (int)Math.Ceiling(words.Length * 0.42));
        var projectCue = Regex.IsMatch(title,
            @"\b(?:project|application|app|dashboard|portal|system|platform|tool|website|model|recommendation|booking|redesign|mapping|analytics|prototype|workspace|design|campaign|study|research|automation|integration|tracking|detection|toolkit|service|engine|modernization|modernisation|checkout|software|player|inventory|payment|website|e-commerce|healthcare|finance|sustainability)\b",
            Rx, TimeLimit);
        return isTitleCase || projectCue;
    }

    private static bool IsBulletLine(string text) =>
        Regex.IsMatch(text.TrimStart(), @"^[•●*\-]\s+\S", Rx, TimeLimit);

    private static bool LooksLikeTechnologyList(string raw)
    {
        var line = TrimBullet(raw);
        if (line.Length > 220 || line.Contains(':') || Action.IsMatch(line) || BodyLabel.IsMatch(line)) return false;
        var pieces = Regex.Split(line, @"\s*[,;|]\s*").Where(x => x.Length > 0).ToArray();
        if (pieces.Length is < 3 or > 25) return false;
        return pieces.Take(5).All(x => x.Length <= 45 && x.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 5);
    }

    private static bool IsSingleSkill(string text)
    {
        var t = text.Trim().ToLowerInvariant();
        return t is "python" or "java" or "javascript" or "typescript" or "c#" or "c++"
            or "react" or "sql" or "figma" or "excel" or "tableau" or "power bi"
            or "android" or "firebase" or "html" or "css";
    }

    private static ResumeProject? BuildProject(IReadOnlyList<string> block)
    {
        if (block.Count < 2) return null;
        var originalTitle = CleanSourceTitle(block[0]);
        if (originalTitle.Length < 4) return null;
        var firstYear = YearPattern.Match(block[0]);
        var year = firstYear.Success ? firstYear.Value : "";
        var actions = new List<string>();
        var context = new List<string>();
        var technologies = new List<string>();
        var techSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var metadata = new List<string>();
        var resultCount = 0;

        foreach (var raw in block.Skip(1))
        {
            var line = TrimBullet(raw);
            if (line.Length == 0) continue;
            var tech = TechLabel.Match(line);
            if (tech.Success)
            {
                AddTechnologyTokens(line[tech.Length..]);
                continue;
            }
            if (Regex.IsMatch(line, @"^role\s*:", Rx, TimeLimit))
            {
                metadata.Add(line);
                // Metadata often includes a local stack: Role | Dates | Java / Kafka / AWS.
                var pipe = line.Split('|', StringSplitOptions.TrimEntries);
                if (pipe.Length >= 3)
                {
                    var last = pipe[^1];
                    if (LooksLikeRoleStack(last)) AddTechnologyTokens(last);
                }
                if (year.Length == 0)
                {
                    var date = YearPattern.Match(line);
                    if (date.Success) year = date.Value;
                }
                continue;
            }
            if (Regex.IsMatch(line, @"^(?:team|duration|client|employer|company|year|project type)\s*:", Rx, TimeLimit))
            {
                metadata.Add(line);
                continue;
            }
            if (LooksLikeTechnologyList(line))
            {
                AddTechnologyTokens(line);
                continue;
            }
            if (IsSingleSkill(line))
            {
                AddTechnologyTokens(line);
                continue;
            }
            if (Regex.IsMatch(line, @"^(?:business|project) context\s*:", Rx, TimeLimit))
            {
                context.Add(Regex.Replace(line, @"^(?:business|project) context\s*:\s*", "", Rx, TimeLimit));
                continue;
            }
            if (Regex.IsMatch(line, @"^(?:result|outcome|impact)\s*:", Rx, TimeLimit))
            {
                resultCount++;
                continue;
            }
            if (BodyLabel.IsMatch(line)) continue;
            if (Action.IsMatch(line) || IsBulletLine(raw)) actions.Add(line);
            else if (!line.EndsWith(':') && line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 5)
                context.Add(line);
        }
        if (actions.Count == 0 && context.Count == 0 && technologies.Count == 0) return null;
        var sourceDescription = actions.Count > 0 ? actions : context;
        var description = CompleteSentences(sourceDescription, 2);
        if (description.Length == 0 && context.Count > 0) description = CompleteSentences(context, 2);
        var evidence = string.Join("\n", block);
        var domain = ProjectDomain(originalTitle, evidence);
        return new ResumeProject
        {
            Title = domain,
            Domain = domain,
            Year = year,
            Description = description,
            Responsibilities = "",
            TechnologyStack = technologies.Take(25).ToList(),
            Evidence = evidence
        };

        void AddTechnologyTokens(string text)
        {
            foreach (var piece in SplitTechnologyTerms(text))
            {
                var item = piece.Trim().Trim(' ', '.', ';', ':', '-', '–', '—');
                if (item.StartsWith("(", StringComparison.Ordinal) && item.EndsWith(")", StringComparison.Ordinal)
                    && technologies.Count > 0)
                {
                    technologies[^1] = technologies[^1] + " " + item;
                    continue;
                }
                if (!ValidTechnologyName(item)) continue;
                if (techSeen.Add(item)) technologies.Add(item);
            }
        }
    }

    private static bool LooksLikeRoleStack(string value)
    {
        var t = value.Trim();
        if (t.Length == 0 || t.Length > 140 || Action.IsMatch(t)) return false;
        if (YearPattern.IsMatch(t) || Regex.IsMatch(t, @"\b(?:engineers?|people|members?|months?|years?|present|current)\b", Rx, TimeLimit)) return false;
        return t.Contains(" / ", StringComparison.Ordinal) ||
               (t.Contains(',') && t.Split(',', StringSplitOptions.RemoveEmptyEntries).Length >= 2);
    }

    private static IEnumerable<string> SplitTechnologyTerms(string text)
    {
        var sb = new StringBuilder();
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '(') depth++;
            if (c == ')') depth = Math.Max(0, depth - 1);
            var splitSlash = c == '/' && i > 0 && i + 1 < text.Length &&
                             char.IsWhiteSpace(text[i - 1]) && char.IsWhiteSpace(text[i + 1]);
            if (depth == 0 && (c == ',' || c == ';' || c == '|' || splitSlash))
            {
                var token = sb.ToString().Trim();
                if (token.Length > 0) yield return token;
                sb.Clear();
                continue;
            }
            sb.Append(c);
        }
        var last = sb.ToString().Trim();
        if (last.Length > 0) yield return last;
    }

    private static bool ValidTechnologyName(string text)
    {
        if (text.Length is < 2 or > 64 || text.Contains('\n') || BodyLabel.IsMatch(text) || Action.IsMatch(text)) return false;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 7) return false;
        if (Regex.IsMatch(text, @"^(?:result|business context|technology|role|team|year)\s*:", Rx, TimeLimit)) return false;
        // These are narrative sentences, not technology names.
        if (Regex.IsMatch(text, @"[.!?]\s+[A-Z][a-z]+", Rx, TimeLimit)) return false;
        return true;
    }

    private static string CompleteSentences(IEnumerable<string> sentences, int maximum)
    {
        var result = new List<string>();
        foreach (var raw in sentences)
        {
            var s = NormalizeLine(raw).Trim('•', '●', '', '-', ' ');
            if (s.Length == 0) continue;
            if (!Regex.IsMatch(s, @"[.!?]$", RegexOptions.None, TimeLimit)) s += ".";
            result.Add(s);
            if (result.Count >= maximum) break;
        }
        return string.Join(" ", result);
    }

    private static string ProjectDomain(string title, string evidence)
    {
        var t = title.ToLowerInvariant();
        // Broad, reusable intent categories. Prefer a descriptive source title
        // over a generic "Software Application" fallback.
        if (Regex.IsMatch(t, @"\b(?:traveler|travel|tourism)\b", Rx, TimeLimit)) return "Travel Recommendation Platform";
        if (Regex.IsMatch(t, @"\b(?:carbon|emissions?|sustainability)\b", Rx, TimeLimit) || t.Contains("eco path", StringComparison.Ordinal))
            return "Carbon Emissions and Sustainability Platform";
        if (Regex.IsMatch(t, @"\b(?:astronom\w*|stargaz\w*|celestial)\b", Rx, TimeLimit)) return "Interactive Astronomy Visualization Tool";
        if (Regex.IsMatch(t, @"\b(?:accident|collision)\b", Rx, TimeLimit)) return "Accident Detection and Emergency Alert System";
        if (Regex.IsMatch(t, @"\b(?:analytics|dashboard)\b", Rx, TimeLimit) && t.Contains("workspace", StringComparison.Ordinal))
            return "Business Analytics Dashboard";
        if (Regex.IsMatch(t, @"\b(?:patient|appointment)\b", Rx, TimeLimit) && Regex.IsMatch(t, @"\b(?:booking|management|appointment)\b", Rx, TimeLimit))
            return "Patient Appointment Management Application";
        // DomainFor only receives this project's own evidence (never the whole CV).
        var classified = ResumeProfileParser.DomainFor(title, evidence);
        var overlyGeneric = classified is "Software Application" or "Web Application" or "Mobile Application" or "Professional Project";
        // "team collaboration" must not be inferred from the generic word workspace.
        if (string.Equals(classified, "Team Collaboration Platform", StringComparison.OrdinalIgnoreCase) &&
            !Regex.IsMatch(t, @"\b(?:team|collaboration|messaging|chat)\b", Rx, TimeLimit))
            overlyGeneric = true;
        if (!overlyGeneric) return classified;
        return title.Length <= 95 ? title : title[..95].TrimEnd();
    }

    private static string CleanSourceTitle(string raw)
    {
        var value = TrimBullet(raw);
        var numbered = NumberedTitle.Match(value);
        if (numbered.Success) value = numbered.Groups["title"].Value;
        var labeled = LabeledTitle.Match(value);
        if (labeled.Success) value = labeled.Groups["title"].Value;
        value = Regex.Replace(value, @"^personal\s+project\s*[—–:-]\s*", "", Rx, TimeLimit);
        // Vertical bar usually introduces year/role metadata, not part of a name.
        var pipe = value.IndexOf('|');
        if (pipe > 2) value = value[..pipe];
        value = Regex.Replace(value,
            @"\s*[—–-]\s*(?:academic|personal|student|capstone|internship|client)\s+project\s*$", "", Rx, TimeLimit);
        // Strip explicitly labelled employer suffixes, not meaningful subtitles.
        value = Regex.Replace(value,
            @"\s+[—–]\s+[^—–]{2,65}\b(?:technologies|solutions|systems|labs|studio|infoware|company|corp|ltd|inc|commerce|digital)\s*$",
            "", Rx, TimeLimit);
        value = Regex.Replace(value, @"\s*\((?:19|20)\d{2}(?:\s*[-–]\s*(?:19|20)\d{2})?\)\s*$", "", Rx, TimeLimit);
        value = Regex.Replace(value, @"\s*[—–-]\s*(?:19|20)\d{2}(?:\s*[—–-]\s*(?:19|20)\d{2})?\s*$", "", Rx, TimeLimit);
        return value.Trim(' ', '-', '–', '—', '.', ';');
    }

    private static List<ResumeProject> SelectUpToTen(IReadOnlyList<ResumeProject> verified)
    {
        if (verified.Count <= 10) return verified.ToList();
        // When a CV lists >10 genuine projects, rank by locally described work
        // and evidence richness; restore source order after picking the top ten.
        return verified.Select((project, index) => new { project, index, score = Quality(project) })
            .OrderByDescending(x => x.score).ThenBy(x => x.index)
            .Take(10).OrderBy(x => x.index).Select(x => x.project).ToList();
    }

    private static int Quality(ResumeProject p)
    {
        var block = p.Evidence.Split('\n');
        var actionCount = block.Count(x => Action.IsMatch(TrimBullet(x)));
        var score = Math.Min(4, actionCount) * 4 +
                    (block.Any(x => Regex.IsMatch(x, @"^role\s*:", Rx, TimeLimit)) ? 4 : 0) +
                    (block.Any(x => Regex.IsMatch(x, @"^business context\s*:", Rx, TimeLimit)) ? 3 : 0) +
                    (block.Any(x => Regex.IsMatch(x, @"^result\s*:", Rx, TimeLimit)) ? 3 : 0) +
                    Math.Min(4, p.TechnologyStack.Count) +
                    (p.Year.Length > 0 ? 1 : 0);
        if (Regex.IsMatch(FirstLine(p.Evidence), @"\bpersonal project\b", Rx, TimeLimit)) score -= 8;
        return score;
    }

    private static bool SupportedLocally(string evidence, string? skill)
    {
        if (string.IsNullOrWhiteSpace(skill) || skill.Length < 2 || skill.Length > 55) return false;
        // Complete skill phrase with word-boundaries; "C" cannot match "CSS".
        return Regex.IsMatch(evidence, @"(?<![\p{L}\p{N}])" + Regex.Escape(skill.Trim()) + @"(?![\p{L}\p{N}])", Rx, TimeLimit);
    }

    private static string Normalize(string value) =>
        Regex.Replace(value.ToLowerInvariant(), @"[^\p{L}\p{N}+#.]+", " ").Trim();
    private static string NormalizeHeading(string line) => line.Trim(' ', '\t', ':', '-', '–', '—', '•', '#').ToLowerInvariant();
    private static string NormalizeLine(string line) => Regex.Replace(line.Trim(), @"\s+", " ");
    private static string TrimBullet(string value) => Regex.Replace(value.Trim(), @"^[•●*\-]\s*", "", Rx, TimeLimit).Trim();
    private static string FirstLine(string evidence) => evidence.Split('\n')[0].Trim();
}
