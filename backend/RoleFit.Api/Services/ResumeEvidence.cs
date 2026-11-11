using System.Globalization;
using System.Text.RegularExpressions;
using RoleFit.Api.Models;

namespace RoleFit.Api.Services;

/// <summary>
/// Source-only, deterministic facts. AI cannot override these facts.
/// Months are retained at full precision for scoring; the editor's years field
/// keeps the existing display-rounding convention.
/// </summary>
public static class ResumeEvidence
{
    private static readonly RegexOptions O = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly TimeSpan T = TimeSpan.FromMilliseconds(200);

    private static readonly string[] EndSections =
    ["selected projects", "projects", "project experience", "academic projects", "key projects",
     "education", "qualifications", "certifications", "certificates", "coursework", "core skills",
     "skills", "technical skills", "relevant strengths", "professional development", "awards"];

    private static string BeforeProjects(string text)
    {
        var lines = text.Replace("\r", "").Split('\n');
        var result = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw.Trim().Trim(':').ToLowerInvariant();
            if (line is "selected projects" or "projects" or "project experience" or "academic projects"
                or "key projects" or "personal projects" or "relevant projects") break;
            result.Add(raw);
        }
        return string.Join("\n", result);
    }

    // Handles "2 years 8 months", "1 yr and 2 mos", "2.5 years",
    // "Experience: 2 years", and header lines such as "UI/UX | 2 YEARS 8 MONTHS".
    public static int? ExplicitProfessionalMonths(string resume)
    {
        if (string.IsNullOrWhiteSpace(resume)) return null;
        var source = BeforeProjects(resume);
        var lines = source.Replace("\r", "").Split('\n');
        var priority = new List<(int score, int months)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length > 700) continue;
            var m = Regex.Match(line,
                @"(?<!\d)(?<years>\d{1,2}(?:\.\d{1,2})?)\s*\+?\s*(?:years?|yrs?)\b",
                O, T);
            if (!m.Success) continue;
            if (!double.TryParse(m.Groups["years"].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var years) || years > 60) continue;
            // Avoid matching "2 years" in an unrelated line about project duration.
            var context = string.Join(" ", lines.Skip(Math.Max(0, i - 1)).Take(3));
            var isProfessional = Regex.IsMatch(context,
                @"\b(?:experience|experienced|professional|career|employment|work|developer|designer|engineer|architect|analyst|consultant|manager|lead|specialist|associate|years?\s+in)\b", O, T);
            var header = i <= 6;
            if (!isProfessional && !header) continue;
            var afterYears = line[(m.Index + m.Length)..];
            var monthMatch = Regex.Match(afterYears,
                @"^\s*(?:(?:and|&)\s*)?(?<months>\d{1,2})\s*(?:months?|mos?)\b", O, T);
            var hasMonths = monthMatch.Success;
            var yearPart = years;
            var months = hasMonths && int.TryParse(monthMatch.Groups["months"].Value, out var mv) && mv < 12 ? mv : 0;
            var total = (int)Math.Round(yearPart * 12 + months, MidpointRounding.AwayFromZero);
            if (total > 720) continue;
            var score = (header ? 2 : 0) + (isProfessional ? 3 : 0) + (hasMonths ? 2 : 0) +
                        (Regex.IsMatch(line, @"\b(?:experience|experienced|fresher|professional)\b", O, T) ? 3 : 0);
            priority.Add((score, total));
        }
        if (priority.Count > 0)
            return priority.OrderByDescending(x => x.score).First().months;

        // A one-line duration in months, e.g. "14 months of experience".
        var onlyMonths = Regex.Match(source,
            @"(?<!\d)(?<months>\d{1,3})\s*(?:months?|mos?)\s+(?:of\s+)?(?:professional\s+)?experience\b",
            O, T);
        if (onlyMonths.Success && int.TryParse(onlyMonths.Groups["months"].Value, out var solo) && solo <= 720)
            return solo;

        // Fresher status is not an invitation to infer work years from academic projects.
        if (Regex.IsMatch(source,
            @"\b(?:fresher|no\s+full[- ]time\s+professional\s+experience|zero\s+years?\s+of\s+experience|0\s+years?\s+of\s+experience)\b", O, T))
            return 0;

        return MonthsFromEmploymentDates(source);
    }

    private static int? MonthsFromEmploymentDates(string source)
    {
        var lines = source.Replace("\r", "").Split('\n');
        var section = false;
        var relevant = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw.Trim().Trim(':').ToLowerInvariant();
            if (line is "professional experience" or "work experience" or "employment history" or "employment")
            { section = true; continue; }
            if (!section) continue;
            if (EndSections.Contains(line)) break;
            relevant.Add(raw);
        }
        if (relevant.Count == 0) return null;
        var covered = new HashSet<int>();
        var range = new Regex(
            @"(?<sm>jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:tember)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\.?\s+(?<sy>20\d{2}|19\d{2})\s*[-–—]\s*(?:(?<em>jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:tember)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\.?\s+(?<ey>20\d{2}|19\d{2})|(?<present>present|current|ongoing|now))",
            O, T);
        for (var i = 0; i < relevant.Count; i++)
        {
            var line = relevant[i];
            // Do not convert an internship, training or volunteer placement into
            // full-time professional experience without explicit confirmation.
            var jobLine = i > 0 ? relevant[i - 1] + " " + line : line;
            if (Regex.IsMatch(jobLine, @"\b(?:internship|intern|training|academic|volunteer)\b", O, T)) continue;
            foreach (Match match in range.Matches(line))
            {
                if (!Month(match.Groups["sm"].Value, out var sm) || !int.TryParse(match.Groups["sy"].Value, out var sy)) continue;
                int em, ey;
                if (match.Groups["present"].Success)
                { em = DateTime.UtcNow.Month; ey = DateTime.UtcNow.Year; }
                else if (!Month(match.Groups["em"].Value, out em) || !int.TryParse(match.Groups["ey"].Value, out ey)) continue;
                var start = sy * 12 + sm;
                var end = ey * 12 + em;
                if (end < start || end - start > 720) continue;
                for (var month = start; month <= end; month++) covered.Add(month);
            }
        }
        return covered.Count == 0 ? null : covered.Count;
    }

    private static bool Month(string month, out int result)
    {
        result = 0;
        var prefix = month.Length >= 3 ? month[..3].ToLowerInvariant() : month.ToLowerInvariant();
        result = Array.IndexOf(new[] { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" }, prefix) + 1;
        return result > 0;
    }

    public static double DisplayYears(string resume) =>
        ExperiencePolicy.Display((ExplicitProfessionalMonths(resume) ?? 0) / 12d) ?? 0;

    public static int? RequiredMonths(string jd)
    {
        var mandatory = JdEvidenceRules.MandatoryDescription(jd);
        var m = Regex.Match(mandatory,
            @"(?<!\d)(?<years>\d{1,2}(?:\.\d{1,2})?)\s*\+?\s*(?:years?|yrs?)\b",
            O, T);
        if (m.Success && double.TryParse(m.Groups["years"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var years))
        {
            var suffix = mandatory[(m.Index + m.Length)..];
            var monthMatch = Regex.Match(suffix,
                @"^\s*(?:(?:and|&)\s*)?(?<months>\d{1,2})\s*(?:months?|mos?)\b", O, T);
            var months = monthMatch.Success && int.TryParse(monthMatch.Groups["months"].Value, out var additional)
                && additional < 12 ? additional : 0;
            return (int)Math.Round(years * 12 + months, MidpointRounding.AwayFromZero);
        }
        // Common word-based requirements in non-technical job descriptions.
        var spelled = Regex.Match(mandatory,
            @"\b(?<years>one|two|three|four|five|six|seven|eight|nine|ten)\s*(?:\+|or\s+more)?\s*years?\b",
            O, T);
        if (spelled.Success)
        {
            var words = new[] { "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };
            return (Array.IndexOf(words, spelled.Groups["years"].Value.ToLowerInvariant()) + 1) * 12;
        }
        if (Regex.IsMatch(mandatory, @"\b(?:fresher|entry[- ]level|no\s+experience\s+required)\b", O, T)) return 0;
        return null;
    }

    /// <summary>Additional tools/competencies in the actual source skills section.
    /// Does not take any skills from the target JD.</summary>
    public static List<string> SourceListedSkills(string resume)
    {
        var found = new List<string>();
        var lines = resume.Replace("\r", "").Split('\n');
        var inSkills = false;
        foreach (var raw in lines)
        {
            var l = raw.Trim();
            var key = l.Trim(':').ToLowerInvariant();
            if (key is "skills" or "core skills" or "technical skills" or "key skills" or
                "technologies" or "technology stack" or "professional competencies")
            { inSkills = true; continue; }
            if (!inSkills) continue;
            if (key is "experience" or "professional experience" or "work experience" or "employment history" or
                "projects" or "selected projects" or "education" or "certifications" or "coursework" or "summary")
            { inSkills = false; continue; }
            if (l.Length == 0) continue;
            var cleaned = Regex.Replace(l, @"^[•●*\-]\s*", "");
            var colon = cleaned.IndexOf(':');
            if (colon >= 0 && colon < 35) cleaned = cleaned[(colon + 1)..];
            if (cleaned.Length > 240) continue;
            foreach (var part in Regex.Split(cleaned, @"[,;|]"))
            {
                var skill = part.Trim().Trim('.', '•', '-', ' ');
                if (skill.Length is < 2 or > 64 || skill.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 8) continue;
                if (!found.Contains(skill, StringComparer.OrdinalIgnoreCase)) found.Add(skill);
            }
        }
        return found.Take(80).ToList();
    }
}
