using RoleFit.Api.Models;
using RoleFit.Api.Services;

var fixture = """
TEST CANDIDATE
+91 9999999999 City, India
candidate@example.test
EDUCATION
B.Tech Computer Science | 2022-2026
SKILLS
Python, SQL, Flask, Data analysis
PROJECTS
Solo Traveler Recommendation System
technologies: Flask, MySQL, Pandas, Geopy
• Developed a personalized travel recommendation engine using Haversine distance and interest-based filtering.
• Integrated real-time chat and trip recommendations for travelers.
Eco Path: Smart India Hackathon 2024.
Technologies: – HTML, CSS, JavaScript, PHP, MySQL
• Built a carbon-emission computation module for a sustainability challenge.
• Added carbon-credit estimation for planning.
Stargazer – Real-Time Interactive Star Mapping Tool
Technologies: Python, Streamlit, AstronomyAPI, Beautiful Soup
• Developed real-time interactive star mapping using location-based transformations.
Real-Time Accident Detection and Alert System
Technologies: Python, MPU6050, SIM800L GSM Module
• Built an edge-based accident detection model and automated emergency SMS alerts.
CERTIFICATIONS
Introduction to Python
""";
var projects = SourceProjectReconciler.Extract(fixture);
Expect(projects.Count == 4, "Rakshit-like source must give exactly four real project blocks");
Expect(projects.All(p => !string.IsNullOrWhiteSpace(p.Description)), "Each project must have local source description");
Expect(projects.All(p => p.TechnologyStack.Count > 0), "Each project must have its own local technologies");
Expect(projects.Select(p => p.Domain).Distinct().Count() == 4, "Domains must be distinct and meaningful");
Expect(projects[0].TechnologyStack.Contains("Geopy"), "Travel project must retain Geopy");
Expect(!projects[0].TechnologyStack.Contains("MPU6050"), "Travel project cannot inherit accident project technologies");
Expect(projects[3].TechnologyStack.Contains("MPU6050"), "Accident project must retain MPU6050");
Expect(ResumeEvidence.DisplayYears(fixture) == 0, "Academic projects are not professional employment");

var profile = new ResumeProfile
{
    CandidateName = "Test Candidate", Designation = "+91 9999999999 City, India",
    Phone = "+91 9999999999", Email = "candidate@example.test",
    ProfessionalSummary = "Contact me at candidate@example.test for work opportunities.",
    Projects = [new ResumeProject { Title = "Software Application", Domain = "Software Application", Evidence = "not a source project", Description = "" }]
};
SourceProjectReconciler.Apply(profile, fixture);
Expect(profile.Projects.Count == 4, "Failed parser placeholder should be replaced by four verified blocks");
Expect(profile.Phone == "" && profile.Email == "" && profile.Designation == "", "No phone/email can enter a generated profile designation or contact fields");
Expect(!profile.ProfessionalSummary.Contains('@'), "No email can remain in summary");

Check("""
FRESHER PERSON
UI/UX DESIGNER | FRESHER
EXPERIENCE
Fresher — no full-time professional design experience
SELECTED PROJECTS
Campus Events App — Academic Project | Jan 2026 - Apr 2026
• Designed twelve mobile event-registration screens using Figma.
Local Bakery Website — Personal Project | Jun 2026 - Aug 2026
• Created responsive desktop/mobile layout prototypes.
EDUCATION
B.Des Communication Design
""", 2, "fresher UI/UX");
Check("""
JUNIOR DESIGNER
JUNIOR UI/UX DESIGNER | 1 YEAR 2 MONTHS
SELECTED PROJECTS
E-commerce Checkout Simplification | 2026
• Redesigned cart and payment flows using Figma prototypes.
Service Booking Portal | 2025
• Mapped rescheduling journeys for a booking portal.
EDUCATION
B.Des Interaction Design
""", 2, "junior UI/UX");
Check("""
INTERMEDIATE DESIGNER
UI/UX AND PRODUCT DESIGNER | 2 YEARS 8 MONTHS
SELECTED PROJECTS
B2B Analytics Workspace | 2025 - 2026
• Reworked dashboard navigation and role-based permissions.
Patient Appointment Management App | 2024 - 2025
• Designed appointment booking and rescheduling flows.
EDUCATION
B.Tech
""", 2, "intermediate UI/UX");

Check("""
A SAMPLE RESUME
PROJECTS
SwasthyaGuide Android developer | 2 months | Team of 4
Java, XML, Firebase, HTML, CSS, JavaScript | Android Studio
• Built an Android healthcare application for record management and medical schemes.
Media Player Application
Python
• Designed and developed a modular multimedia player with a Python interface.
EDUCATION
B.Tech
""", 2, "healthcare and media projects with unlabeled technology rows");
Check("""
UI/UX DESIGNER
2 years 4 months of experience
SELECTED PROJECTS
E-commerce Checkout Redesign | 2026 | Responsive web application
Mapped checkout drop-off points and tested alternative Figma prototypes.
SaaS Analytics Dashboard | 2025 | B2B product
Created information architecture, task flows and reusable visualizations.
Clinic Appointment Booking App | 2024 | Mobile application
Designed appointment booking and rescheduling experiences.
CORE SKILLS
Figma, Adobe XD, responsive design
""", 3, "three-project UI/UX source");
Expect(SourceProjectReconciler.Extract("""
TEST CANDIDATE
SKILLS
Python, Flask, JavaScript, Figma
EDUCATION
B.Tech Computer Science
""").Count == 0, "Skill listings without a Projects section must not manufacture projects");

Console.WriteLine("PASS: all source-project and contact-redaction checks passed.");
void Check(string source, int expected, string title)
{
    var actual = SourceProjectReconciler.Extract(source).Count;
    Expect(actual == expected, $"{title}: expected {expected}, got {actual}");
}
void Expect(bool ok, string message)
{
    if (!ok) throw new Exception("FAIL: " + message);
    Console.WriteLine("PASS: " + message);
}
