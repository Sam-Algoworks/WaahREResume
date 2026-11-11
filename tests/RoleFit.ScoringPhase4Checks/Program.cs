using RoleFit.Api.Services;

var scorer = new AlignmentEngine();

var required = """
Payroll Supervisor
We need someone with 3 years of experience working with payroll.
Required skills:
- Payroll processing
- Financial reconciliation
- SAP
Preferred skills:
- Power BI
Responsibilities:
- Reconcile and audit payroll records
""";
var partialResume = """
Morgan Example
Payroll Associate
2 years of experience working in payroll operations and payroll processing.
Used SAP to prepare monthly reports. Reconciled accounting statements.
""";
var partial = scorer.Evaluate(required, partialResume, "finance.txt");
Check(partial.JobSkills.Contains("Payroll processing"), "Finance skill from a bullet list is extracted");
Check(partial.JobSkills.Contains("Financial reconciliation"), "Second bullet competency extracted");
Check(partial.MissingSkills.Contains("Financial reconciliation"), "Missing explicit finance skill remains missing");
Check(!partial.JobSkills.Contains("Power BI"), "Preferred-only skill not incorrectly made mandatory");
Check(partial.Breakdown.Single(x => x.Category == "Experience").Awarded > 0 &&
      partial.Breakdown.Single(x => x.Category == "Experience").Awarded < 20,
      "Partial documented experience gets partial, not full, credit");
Check(partial.Breakdown.Single(x => x.Category == "Responsibilities").Awarded > 0,
      "Reconciled in resume matches reconcile in JD responsibilities");

var fullySupported = scorer.Evaluate(required, partialResume +
    "\nFinancial reconciliations completed quarterly for the department.\n", "finance.txt");
Check(fullySupported.MatchedSkills.Contains("Financial reconciliation"),
    "Conservative singular-plural match recognizes finance evidence");
Check(fullySupported.Score >= partial.Score,
    "Adding verified evidence never lowers the estimated score");

var teachingJd = """
School Teacher
Essential competencies:
* Lesson planning
* Classroom management
* Student assessment
Benefits:
Generous leave policy
""";
var teachingResume = """
Taylor Example
Teacher
Prepared lesson planning materials and classroom management guides.
Performed student assessments during teaching placements.
""";
var education = scorer.Evaluate(teachingJd, teachingResume, "teaching.txt");
Check(education.JobSkills.Count(s => s is "Lesson planning" or "Classroom management" or "Student assessment") == 3,
    "Education competencies extracted from star bullets");
Check(education.MissingSkills.Count == 0,
    "Education competencies match source evidence, including plural forms");

var boundaryJd = """
Operations Coordinator
Required skills:
- Inventory reconciliation
Responsibilities: Supervise inventory operations and manage warehouse teams
- Supervise team members
""";
var boundaryResult = scorer.Evaluate(boundaryJd, """
Chris Example
Operations Coordinator
Inventory reconciliation duties and warehouse supervision.
""", "boundary.txt");
Check(!boundaryResult.JobSkills.Contains("Supervise team members"),
    "Responsibility bullets do not become independent required skills");

var healthcareJd = """
Clinical Coordinator
Core Competencies:
- Patient assessment
- Care coordination
- Clinical documentation
Preferred skills:
- Epic EHR
""";
var healthcareResume = """
Jordan Example
Care Coordinator
Skilled in patient assessment and care coordination.
Completed clinical documentation daily.
""";
var healthcare = scorer.Evaluate(healthcareJd, healthcareResume, "clinical.txt");
Check(healthcare.JobSkills.Contains("Patient assessment"), "Healthcare competency extracted");
Check(healthcare.JobSkills.Contains("Clinical documentation"), "Healthcare documentation extracted");
Check(!healthcare.JobSkills.Contains("Epic EHR"), "Optional clinical system not scored as required");

var negativeJd = """
Data Analyst
Required skills: SQL Server, MongoDB
Minimum 2 years of experience in database analysis.
""";
var negativeResume = """
Alex Example
Data Analyst
2 years of experience with SQL Server and reporting.
No experience with MongoDB.
""";
var negative = scorer.Evaluate(negativeJd, negativeResume, "negative.txt");
Check(negative.MissingSkills.Contains("MongoDB"),
    "Explicitly negated MongoDB experience cannot earn verified-skill credit");
Check(!negative.CandidateSkills.Contains("MongoDB"),
    "Negated skill is not marked as a verified candidate skill");
Check(negative.Score == scorer.Evaluate(negativeJd, negativeResume, "negative.txt").Score,
    "Same original resume and JD give deterministic scores");

var conjunctionJd = """
Backend Engineer
Must-haves: SQL Server and MongoDB
Must have database application development experience.
""";
var conjunctionResult = scorer.Evaluate(conjunctionJd, """
Casey Example
Database Developer
Worked with SQL Server for reporting and deployments.
""", "conjunction.txt");
Check(conjunctionResult.JobSkills.Contains("SQL Server") && conjunctionResult.JobSkills.Contains("MongoDB"),
    "Two separately listed skills connected by and are extracted");
Check(conjunctionResult.MissingSkills.Contains("MongoDB"),
    "MongoDB stays missing when only SQL Server exists in the resume");

var optionalYears = """
Operations Specialist
Required skills: inventory reconciliation
Preferred skills:
- 10 years of experience
""";
var optional = scorer.Evaluate(optionalYears, """
Pat Example
Operations Specialist
Completed inventory reconciliation and stock control work.
""", "operations.txt");
Check(!optional.Breakdown.Single(p => p.Category == "Experience").Applicable,
    "Optional-only experience does not become required experience");

Console.WriteLine("PASS — Phase 4 scoring regression checks completed.");
static void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("FAIL — " + label);
    Console.WriteLine("PASS — " + label);
}
