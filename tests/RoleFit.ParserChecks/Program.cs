using RoleFit.Api.Models;
using RoleFit.Api.Services;

var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples"));
if (!Directory.Exists(projectDir))
    projectDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "samples"));
var sampleFile = Path.Combine(projectDir, "project-heavy-resume.txt");
if (!File.Exists(sampleFile)) throw new Exception($"Missing fixture: {sampleFile}");
var parser = new ResumeProfileParser();
var profile = parser.Parse(File.ReadAllText(sampleFile), "project-heavy-resume.txt");
Check(profile.CandidateName == "Maya Rao", "Name extraction");
Check(profile.Designation == "Full Stack Developer", "Designation extraction");
Check(profile.ExperienceYears == 4.5, "Experience rounding 4.45 → 4.5");
Check(profile.Projects.Count == 10, "Cap at ten genuine projects");
var ecommerce = profile.Projects.FirstOrDefault(p => p.Domain == "E-commerce Website");
Check(ecommerce is not null, "Anonymize Amazon Website and retain as relevant project");
Check(ecommerce!.TechnologyStack.Contains("ASP.NET Core"), "Project-local ASP.NET Core");
Check(!ecommerce.TechnologyStack.Contains("Power BI"), "No global skill leakage into shopping project");
Check(profile.Projects.Any(p => p.Domain == "IoT Smart Farming System" && p.TechnologyStack.Contains("Power BI")), "Farming project keeps its own tools");
Check(profile.Projects.All(p => !string.IsNullOrWhiteSpace(p.Evidence)), "Project evidence attached");

var noProjects = parser.Parse("""
Robin Example
Software Engineer
Summary
Engineer with experience building APIs.
Experience
Built backend applications with SQL Server.
Education
BSc Computer Science
""", "no-projects.txt");
Check(noProjects.Projects.Count == 0, "Never fabricate project records");
Check(ExperiencePolicy.Display(4.4) == 4, "4.4 → 4");
Check(ExperiencePolicy.Display(4.45) == 4.5, "4.45 → 4.5");
Check(ExperiencePolicy.Display(4.6) == 5, "4.6 → 5");

var multiline = parser.Parse("""
Jordan Example
Software Developer
Projects
Personal Portfolio Website
Built a simple personal portfolio with HTML CSS and JavaScript.
IoT Smart Farming
Built a crop-monitoring system with IoT sensors and Python.
Education
B.Tech
""", "multiline.txt");
Check(multiline.Projects.Count == 2, "Two multiline project blocks");
Check(multiline.Projects[0].Domain == "Personal Portfolio Website", "Portfolio generic domain");
Check(multiline.Projects[1].TechnologyStack.Contains("Python"), "Second project Python extracted");
var selected = parser.Parse(File.ReadAllText(Path.Combine(projectDir,"selected-projects-multiline.txt")),"selected-projects-multiline.txt");
Check(selected.Projects.Count == 8, "SELECTED PROJECTS PDF-like format identifies eight projects");
Check(selected.Projects[0].Domain == "Merchant Risk and Fraud Detection Platform", "Risk project is renamed as generic domain");
Check(selected.Projects[0].TechnologyStack.Contains("Kafka"), "Local Java/Kafka stack from first project");
Check(!selected.Projects[0].TechnologyStack.Contains("MongoDB"), "Global MongoDB skill not assigned to first project");
Check(selected.Projects[0].Year.Length == 0, "Never invent project year");
Check(selected.Projects.All(p => p.Description.Length > 10), "Multiline descriptions grouped inside projects");
Console.WriteLine("PASS — All RoleFit AI parser regression checks completed.");

void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL — " + message);
    Console.WriteLine("PASS — " + message);
}
