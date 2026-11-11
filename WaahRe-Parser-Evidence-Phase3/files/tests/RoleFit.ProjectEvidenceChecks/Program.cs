using RoleFit.Api.Services;

var parser = new ResumeProfileParser();

var healthcare = """
Sam Example
Application Developer
Projects
SwasthyaGuide
Built a healthcare services and medical records application using Java and Firebase.
Media Player Application
Developed a media player using Python for audio and video playback.
Education
BSc Computer Science
""";
var healthcareResult = parser.Parse(healthcare, "synthetic-healthcare.txt");
Check(healthcareResult.Projects.Count == 2,
    "Two real project headings produce exactly two project records");
Check(healthcareResult.Projects[0].TechnologyStack.Contains("Java"),
    "First project retains its own Java evidence");
Check(!healthcareResult.Projects[0].TechnologyStack.Contains("Python"),
    "Second project's Python does not leak to first project");
Check(healthcareResult.Projects[1].TechnologyStack.Contains("Python"),
    "Second project contains its own Python evidence");
Check(ProjectHeadingEvidence.HasIndependentHeading(healthcare, "SwasthyaGuide", []),
    "Unseen independent heading is eligible for AI recovery");
Check(!ProjectHeadingEvidence.HasIndependentHeading(healthcare, "SwasthyaGuide",
        ["SwasthyaGuide\nBuilt a healthcare services and medical records application"]),
    "AI cannot re-add a project already found by the parser");
Check(!ProjectHeadingEvidence.HasIndependentHeading(healthcare, "Python", []),
    "Technology words in descriptions are not new project headings");
Check(!ProjectHeadingEvidence.HasIndependentHeading(healthcare, "Medical Records", []),
    "Description phrases do not become project headings");

var bulletProjects = """
Taylor Example
Operations Analyst
Projects
• Payroll Reconciliation Automation
• Built a finance workflow using Excel and VBA.
• Supplier Invoice Review System
• Developed a vendor reconciliation workflow for invoices.
Education
BBA
""";
Check(parser.Parse(bulletProjects, "synthetic-operations.txt").Projects.Count == 2,
    "Non-IT bullet projects remain distinct");
Check(ProjectHeadingEvidence.HasIndependentHeading(bulletProjects, "Supplier Invoice Review System", []),
    "Independent non-IT project headings are recognized");
Check(!ProjectHeadingEvidence.HasIndependentHeading(bulletProjects, "Built a finance workflow using Excel and VBA", []),
    "Action bullets are not project headings");

var numberedProjects = """
Avery Example
Software Developer
Selected Projects
1. Sales Analytics Dashboard | Power BI | 2024
Created sales reports with Power BI.
2. Customer Support Portal | React | 2025
Built ticket tracking with React.
Experience
Software Developer
""";
Check(parser.Parse(numberedProjects, "synthetic-numbered.txt").Projects.Count == 2,
    "Numbered pipe-delimited project headings do not multiply");
Check(ProjectHeadingEvidence.HasIndependentHeading(numberedProjects, "Sales Analytics Dashboard", []),
    "Numbered headings with trailing technology metadata are accepted");
Check(!ProjectHeadingEvidence.HasIndependentHeading(numberedProjects, "Software Developer", []),
    "Experience section cannot create a new project");

var legacy = """
Robin Example
HR Specialist
Experience
Project: Employee Onboarding Workflow
Created a repeatable onboarding checklist and forms.
Education
BBA
""";
Check(ProjectHeadingEvidence.HasIndependentHeading(legacy, "Employee Onboarding Workflow", []),
    "An explicitly labeled project inside work history is eligible");
Check(!ProjectHeadingEvidence.HasIndependentHeading(legacy, "Created a repeatable onboarding checklist and forms", []),
    "Work-history bullet cannot become a project");

Console.WriteLine("PASS — Phase 3 project heading and duplication guards");

static void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL — " + label);
    Console.WriteLine("PASS — " + label);
}

var moreThanTen = new System.Text.StringBuilder("Morgan Example\nBusiness Analyst\nProjects\n");
for (int i = 1; i <= 14; i++)
{
    moreThanTen.AppendLine($"{i:00} | Operations Improvement Application {i:00}");
    moreThanTen.AppendLine($"Developed a distinct workflow with documented process improvements for department {i}.");
}
moreThanTen.AppendLine("Education\nBBA");
Check(parser.Parse(moreThanTen.ToString(), "synthetic-fourteen.txt").Projects.Count == 10,
    "More than ten genuine projects are capped at ten without inventing extras");
