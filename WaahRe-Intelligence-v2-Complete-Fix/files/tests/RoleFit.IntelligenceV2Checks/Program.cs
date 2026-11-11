using RoleFit.Api.Services;

var parser = new ResumeProfileParser();
var scorer = new AlignmentEngine();
var failures = 0;
void Check(bool pass, string message)
{
    Console.WriteLine($"{(pass ? "PASS" : "FAIL")} {message}");
    if (!pass) failures++;
}

foreach (var (file, months, count) in new[]
{
    ("Aanya_Sharma.txt", 0, 2),
    ("Kabir_Sethi.txt", 14, 2),
    ("Neha_Rao.txt", 32, 2),
    ("Riya_Mehra.txt", 28, 3)
})
{
    var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", file));
    var actual = ResumeEvidence.ExplicitProfessionalMonths(text);
    var profile = parser.Parse(text, file);
    Check(actual == months, $"{file}: experience is {months} exact months (actual {actual})");
    Check(profile.Projects.Count == count, $"{file}: {count} genuine projects (actual {profile.Projects.Count})");
    Check(profile.ExperienceYears != 0 || months == 0,
        $"{file}: employed candidate is not shown as 0 years");
}

var jd = "UI UX Designer required: Figma, research, prototyping, responsive layout. At least 2 years of professional experience. Design customer interfaces and deliver usability testing.";
var aanya = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "Aanya_Sharma.txt"));
var kabir = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "Kabir_Sethi.txt"));
var neha = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "Neha_Rao.txt"));
var a = scorer.Evaluate(jd, aanya, "Aanya.txt");
var k = scorer.Evaluate(jd, kabir, "Kabir.txt");
var n = scorer.Evaluate(jd, neha, "Neha.txt");
double experiencePoints(RoleFit.Api.Models.AnalysisRecord r) => r.Breakdown.First(x => x.Category == "Experience").Awarded;
Check(experiencePoints(a) == 0, "Fresher earns 0/20 of explicitly required experience");
Check(experiencePoints(k) > 0 && experiencePoints(k) < 20, "14 months receives partial experience credit");
Check(experiencePoints(n) == 20, "32 months meets a 24-month minimum");

// Simulated, VERIFIED AI semantics. Production only uses these after both
// original-source quotes have passed SemanticMatchService validation.
var semantic = new SemanticComparison { Requirements =
[
    new SemanticRequirement { Name="Web interface design", Category="skill", Importance="required",
        Match="partial", ResumeEvidence="HTML, CSS, JavaScript" },
    new SemanticRequirement { Name="Figma", Category="skill", Importance="required",
        Match="direct", ResumeEvidence="Figma" }
] };
var exampleResume = "Frontend developer with 2 years experience. Skills: HTML, CSS, JavaScript and Figma. Built user interfaces for responsive websites.";
var exampleJd = "Role: Web Designer. Required skills: Web interface design and Figma. Minimum 2 years experience. Design responsive interfaces.";
var score = scorer.EvaluateWithSemantic(exampleJd, exampleResume, "example.txt", semantic);
Check(score.SemanticMatchingUsed, "Semantic comparison recorded on score");
Check(score.MissingSkills.Contains("Web interface design"), "Related HTML/CSS/JS receives only partial credit, not a fabricated direct match");
Check(score.MatchedSkills.Contains("Figma"), "Direct Figma skill recorded as matched");

var projectEvidence = "SELECTED PROJECTS\nCheckout Redesign | 2026\nCreated design prototypes.\nCORE SKILLS\nFigma, Sketch, Auto Layout, WCAG";
Check(!ProjectHeadingEvidence.HasIndependentHeading(projectEvidence, "Figma, Sketch, Auto Layout, WCAG", []),
    "Source skill rows cannot become AI-created projects");
Check(!ProjectHeadingEvidence.HasIndependentHeading(projectEvidence, "Invented Dashboard", []),
    "AI cannot create a project without a matching source heading");

if (failures > 0)
{
    Console.Error.WriteLine($"FAILED: {failures} accuracy regression(s). Do NOT deploy.");
    Environment.Exit(1);
}
Console.WriteLine("ALL V2 INTELLIGENCE CHECKS PASSED");
