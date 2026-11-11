using System.Text.RegularExpressions;

namespace RoleFit.Api.Services;

/// <summary>
/// Conservative evidence gate for projects suggested by an LLM but not found
/// in the deterministic project list. It only permits an additional project
/// when the original document includes a distinct, line-level project heading.
/// This is intentionally independent of industry-specific skill dictionaries.
/// </summary>
public static class ProjectHeadingEvidence
{
    private static readonly RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly HashSet<string> ProjectSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "projects", "selected projects", "key projects", "academic projects",
        "personal projects", "project experience", "projects undertaken",
        "relevant projects", "additional projects", "selected enterprise projects",
        "selected projects & certifications", "selected projects and certifications"
    };

    private static readonly HashSet<string> OtherSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "summary", "professional summary", "about me", "profile", "skills", "core skills", "professional competencies", "relevant strengths", "coursework", "professional development",
        "technical skills", "key skills", "technologies", "experience",
        "work experience", "professional experience", "employment",
        "education", "certifications", "licenses and certifications",
        "achievements", "awards", "languages", "interests", "publications"
    };

    private static readonly Regex ActionLine = new(
        @"^(?:built|developed|designed|created|implemented|integrated|managed|automated|led|worked|improved|tested|trained|delivered|supported|responsible|contributed|collaborated|used|configured|deployed|optimized|maintained)\b",
        Options | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    /// <param name="source">Full original resume text, not the generated draft.</param>
    /// <param name="proposedTitle">Original source heading proposed by Groq.</param>
    /// <param name="alreadyDetectedEvidence">Project-local source passages already represented in the draft.</param>
    public static bool HasIndependentHeading(
        string source, string proposedTitle, IEnumerable<string> alreadyDetectedEvidence)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(proposedTitle)) return false;
        var title = Normalize(proposedTitle);
        if (title.Length < 4 || title.Length > 120 || ActionLine.IsMatch(proposedTitle.Trim())) return false;

        // Reject a second record for any heading already contained in a known
        // project. A sentence mentioning that heading is not independent evidence.
        foreach (var evidence in alreadyDetectedEvidence ?? Array.Empty<string>())
        {
            if (Normalize(evidence ?? string.Empty).Contains(title, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        var inProjectSection = false;
        foreach (var raw in source.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var heading = Regex.Replace(line, @"^[#\s]+", "").Trim(' ', ':', '-', '•', '●');
            if (ProjectSections.Contains(heading))
            {
                inProjectSection = true;
                continue;
            }
            if (OtherSections.Contains(heading))
            {
                inProjectSection = false;
                continue;
            }

            // A "Project: Title" line can appear under Experience. An ordinary
            // sentence under Experience cannot create an additional project.
            var explicitlyLabeled = Regex.IsMatch(line,
                @"^(?:[•●-]\s*)?(?:project\s*\d{0,2}|project title|name of project)\s*[:\-]\s*\S",
                Options, TimeSpan.FromMilliseconds(100));
            if (!inProjectSection && !explicitlyLabeled) continue;

            var candidate = Regex.Replace(line, @"^(?:[•●\-]\s*|\d{1,2}\s*[.)|]\s*)+", "");
            candidate = Regex.Replace(candidate,
                @"^(?:project\s*\d{0,2}|project title|name of project)\s*[:\-]\s*", "",
                Options, TimeSpan.FromMilliseconds(100)).Trim();
            if (candidate.Length is < 4 or > 180 || ActionLine.IsMatch(candidate)) continue;
            if (Regex.IsMatch(candidate,
                @"^(?:role|responsibilities|description|tools|technology stack|technologies|environment|team|duration|skills)\s*:",
                Options, TimeSpan.FromMilliseconds(100))) continue;
            if (candidate.EndsWith('.') || candidate.EndsWith(';')) continue;

            // Original headings sometimes append a year, duration or technology
            // row: "Payroll Modernization | SAP | 2025".
            var left = Regex.Split(candidate, @"\s+[|—–]\s+")[0];
            left = Regex.Replace(left,
                @"\s*\((?:19|20)\d{2}(?:\s*[-–]\s*(?:19|20)\d{2})?\)\s*$", "").Trim();
            if (Normalize(left) == title || Normalize(candidate) == title) return true;
        }
        return false;
    }

    private static string Normalize(string value) =>
        Regex.Replace(value.ToLowerInvariant().Normalize(), @"[^\p{L}\p{N}+#.]+", " ").Trim();
}
