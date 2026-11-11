using RoleFit.Api.Services;

var scorer = new AlignmentEngine();

var jd = """
Backend engineer with at least 5 years of experience.
Required skills: SQL Server, MongoDB
A project implementing database-backed software is required.
""";
var incomplete = """
Avery Example
Backend Engineer
Skills: SQL Server
Experience building applications.
Discussed project documentation with the team.
""";
var missing = scorer.Evaluate(jd, incomplete, "missing.txt");
Check(missing.Breakdown.Single(p => p.Category == "Experience").Applicable,
    "JD experience requirement is applicable even when resume years are absent");
Check(missing.Breakdown.Single(p => p.Category == "Experience").Awarded == 0,
    "Missing experience evidence receives zero, not redistributed weight");
Check(missing.Breakdown.Single(p => p.Category == "Projects").Awarded == 0,
    "Word project alone does not earn project points");
Check(missing.MissingSkills.Contains("MongoDB"), "MongoDB remains missing without source evidence");

var financeJd = """
Accountant position requiring 2 years of experience.
Required skills: payroll processing, financial reconciliation, SAP
""";
var financeResume = """
Morgan Example
Accounts Associate
3 years of experience in accounting and payroll processing.
Used SAP to prepare monthly reports.
""";
var finance = scorer.Evaluate(financeJd, financeResume, "finance.txt");
Check(finance.JobSkills.Contains("payroll processing"), "Nontechnical named JD skill extracted");
Check(finance.MatchedSkills.Contains("payroll processing"), "Nontechnical resume skill matched");
Check(finance.MissingSkills.Contains("financial reconciliation"), "Missing finance skill remains missing");

var relevant = scorer.Evaluate(jd, """
Avery Example
Backend Engineer
6 years of experience building backend services.
Skills: SQL Server and MongoDB
Projects
Order Management Platform
Built database-backed APIs with SQL Server and MongoDB for order processing.
""", "relevant.txt");
Check(relevant.Breakdown.Single(p => p.Category == "Projects").Awarded > 0,
    "Relevant documented project can receive partial or full project credit");
Check(relevant.Score > missing.Score, "Verified source evidence improves the estimated score");

Console.WriteLine("PASS — Waah Re Resume scoring regression checks completed.");
void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL — " + message);
    Console.WriteLine("PASS — " + message);
}
