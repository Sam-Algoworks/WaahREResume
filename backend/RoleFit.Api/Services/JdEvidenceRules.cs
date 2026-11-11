using System.Text.RegularExpressions;

namespace RoleFit.Api.Services;

/// <summary>
/// Conservative, deterministic parsing of explicitly named job competencies.
/// No resume rewriting or AI-inferred qualifications. Generic across professions.
/// </summary>
public static class JdEvidenceRules
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(150);

    private static readonly Regex RequiredHeader = new(
        @"^\s*(?:(?:required|mandatory|essential|key|core|minimum)\s+)?(?:technical\s+|professional\s+)?(?:skills?|competencies|capabilities|expertise|tools(?:\s*(?:&|and)\s*technologies)?|technologies|must[- ]haves?|requirements|qualifications)\s*:?(?:\s+(.+))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);

    private static readonly Regex OptionalHeader = new(
        @"^\s*(?:preferred(?:\s+skills?|\s+qualifications?)?|desirable(?:\s+skills?)?|nice[- ]to[- ]have(?:\s+skills?)?|good[- ]to[- ]have(?:\s+skills?)?|bonus(?:\s+skills?)?|optional(?:\s+skills?)?|additional(?:\s+skills?)?)\s*:?(?:\s+.*)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);

    private static readonly Regex OtherHeader = new(
        @"^\s*(?:responsibilities|key responsibilities|duties|role overview|about(?: us| the role)?|benefits|compensation|salary|what we offer|who we are|work experience|education|company|summary|description|job description|application process|equal opportunity|location|employment type)\s*(?::\s*.*)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);

    private static readonly Regex Bullet = new(
        @"^\s*(?:[-*•●✓]|\d{1,2}[.)])\s+(.+)$",
        RegexOptions.CultureInvariant, Timeout);

    private static readonly Regex GenericRequirement = new(
        @"^(?:at least\s+)?\d+(?:[.-]\d+)?\+?\s*(?:years?|yrs?|months?)\b|^(?:bachelor|master|diploma|degree|b\.?tech|mba)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);

    /// <summary>
    /// Exclude explicitly labelled optional/preferred skills from mandatory skill scoring.
    /// Do not remove them from the original JD stored in History.
    /// </summary>
    public static string MandatoryDescription(string jd)
    {
        var output = new List<string>();
        var inOptionalSection = false;
        var sectionHadItems = false;
        foreach (var raw in jd.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (OptionalHeader.IsMatch(line))
            {
                inOptionalSection = true;
                sectionHadItems = false;
                continue;
            }
            if (RequiredHeader.IsMatch(line) || OtherHeader.IsMatch(line))
            {
                inOptionalSection = false;
                sectionHadItems = false;
            }
            if (inOptionalSection && line.Length == 0 && sectionHadItems)
            {
                inOptionalSection = false;
                continue;
            }
            if (inOptionalSection)
            {
                if (line.Length > 0) sectionHadItems = true;
                continue;
            }
            // Handle a single-line "Required skills: SQL. Preferred: MongoDB" JD.
            var optionalInline = Regex.Match(line,
                @"\s*[.;]\s*(?:preferred|nice[- ]to[- ]have|optional|bonus)\s*:\s*",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);
            if (optionalInline.Success) line = line[..optionalInline.Index];
            if (line.Length > 0) output.Add(line);
        }
        return string.Join('\n', output);
    }

    public static List<string> ExplicitRequiredCompetencies(string mandatoryJd)
    {
        var result = new List<string>();
        var inRequiredSection = false;
        var blankCount = 0;

        foreach (var raw in mandatoryJd.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                if (++blankCount >= 2) inRequiredSection = false;
                continue;
            }
            blankCount = 0;
            if (OptionalHeader.IsMatch(line) || OtherHeader.IsMatch(line))
            {
                inRequiredSection = false;
                continue;
            }
            var header = RequiredHeader.Match(line);
            if (header.Success)
            {
                inRequiredSection = true;
                var inline = header.Groups[1].Success ? header.Groups[1].Value : "";
                if (inline.Length > 0) AddCompetencies(inline, result);
                continue;
            }
            if (!inRequiredSection) continue;

            var bullet = Bullet.Match(line);
            if (bullet.Success)
            {
                AddCompetencies(bullet.Groups[1].Value, result);
            }
            else if (line.Length <= 90 && !line.EndsWith('.'))
            {
                // Unbulleted one-line competency under an explicit Skills heading.
                AddCompetencies(line, result);
            }
            else
            {
                inRequiredSection = false;
            }
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).Take(60).ToList();
    }

    private static void AddCompetencies(string raw, List<string> result)
    {
        // Only explicit list items; do not mine free-form responsibilities as facts.
        foreach (var item in Regex.Split(raw, @"[,;•|]", RegexOptions.None, Timeout))
        {
            // Conjunctions often join two explicitly listed skills, but some are a
            // single professional competency (e.g. research and development).
            var knownCompound = new[] { "research and development", "sales and marketing",
                "health and safety", "risk and compliance", "learning and development",
                "diversity and inclusion", "financial planning and analysis",
                "training and development", "recruitment and selection" };
            var parts = knownCompound.Any(x => item.Trim().Equals(x, StringComparison.OrdinalIgnoreCase))
                ? new[] { item }
                : Regex.Split(item, @"\s+and\s+", RegexOptions.IgnoreCase, Timeout);
            foreach (var part in parts)
            {
                var clean = part.Trim().TrimStart('-', '*', '•', '●', '✓', ' ');
                clean = Regex.Replace(clean, @"^(?:hands[- ]on\s+)?(?:experience|knowledge|proficiency|expertise|familiarity|skilled)\s+(?:with|of|in|using)\s+",
                "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);
                clean = Regex.Replace(clean, @"^(?:and|or)\s+", "",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);
                clean = clean.Trim().TrimEnd('.', ';', ':', ' ');
                if (clean.Length is < 2 or > 64 || clean.Contains(':') || GenericRequirement.IsMatch(clean)) continue;
                var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 7 || clean.Contains('!') || clean.Contains('?')) continue;
                result.Add(clean);
            }
        }
    }

    public static bool HasAffirmativeMention(string source, string term, IReadOnlyList<string>? aliases = null)
    {
        var terms = aliases is { Count: > 0 } ? aliases : new[] { term };
        foreach (var candidate in terms)
        {
            foreach (var variant in CandidateVariants(candidate))
            {
                var pattern = @"(?<![\p{L}\p{N}])" + Regex.Escape(variant) + @"(?![\p{L}\p{N}])";
                foreach (Match match in Regex.Matches(source, pattern,
                             RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout))
                {
                    var prefixStart = Math.Max(0, match.Index - 75);
                    var prefix = source[prefixStart..match.Index];
                    var lastBoundary = prefix.LastIndexOfAny(new[] { '.', ';', '\n', '\r', '!', '?' });
                    if (lastBoundary >= 0) prefix = prefix[(lastBoundary + 1)..];
                    if (Regex.IsMatch(prefix,
                        @"\b(?:not|no|never|without|lacks?|lack of)\b(?:(?![.;!?]).){0,55}$",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout)) continue;
                    return true;
                }
            }
        }
        return false;
    }

    private static IEnumerable<string> CandidateVariants(string skill)
    {
        yield return skill;
        var words = skill.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2) yield break;
        var last = words[^1];
        if (last.Length <= 4 || !char.IsLetter(last[^1])) yield break;
        // Matching trivial singular/plural variations is safer than AI synonym inference.
        yield return last.EndsWith('s') ? skill[..^1] : skill + "s";
    }

    public static bool HasAction(string text, string action)
    {
        var variants = new List<string> { action, action + "s", action + "d", action + "ed", action + "ing" };
        if (action.EndsWith('e')) variants.Add(action[..^1] + "ing");
        if (action == "plan") variants.AddRange(new[] { "planned", "planning" });
        if (action == "teach") variants.AddRange(new[] { "taught", "teaching" });
        if (action == "sell") variants.AddRange(new[] { "sold", "selling" });
        if (action == "analyze") variants.Add("analysed");
        return variants.Any(variant => HasAffirmativeMention(text, variant));
    }
}
