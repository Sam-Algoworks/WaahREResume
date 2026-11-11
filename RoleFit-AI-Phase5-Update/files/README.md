> **Phase 5 parser corrections available:** See [PHASE5_NOTES.md](PHASE5_NOTES.md) for exact profile layout, parser changes, Groq configuration and tests.

# RoleFit AI — Phase 4 Profile Studio (development build)

This is an incremental upgrade of the Phase 3 local working project. It preserves the Overview, Resume Analyzer, Bulk Ranking, Job Descriptions, Candidates, and Analysis History sections.

## Start on Windows

1. Install .NET 10 SDK and a supported Node.js version.
2. Double-click `CONFIGURE_GROQ.bat` once to securely enter your own Groq API key (optional for basic parsing; mandatory for AI enrichment and Align to Job). The key is stored in the Windows **user environment**, never inside the project. Close/restart old API terminals after setting it.
3. Double-click `START_ROLEFIT.bat`. Keep the API and frontend PowerShell windows open.
4. Open the **Local** address printed by Vite (normally http://127.0.0.1:5180); backend API health is http://127.0.0.1:5099/api/health.
5. Open Resume Parser. Choose a resume, optionally load/paste/upload a JD, then click **Parse Resume** or **Align to Job**.
6. Edit any profile field directly. Click **Save Profile Draft** to persist local draft, or **Print / Save PDF** and choose **Save as PDF** in the browser's print dialog. Select between **Algoworks** and **Normal / JD aligned** styles.

## What is implemented in this phase

- Uses the user-provided Algoworks logo in the editor and A4 print view.
- Inline editable name, exact designation, experience, 85-word summary, skills, projects (maximum 10), work history, education, certifications.
- Experience display: 4.4 -> 4, 4.45 -> 4.5, 4.6 -> 5. Values within +/- 0.05 of .5 retain the half year.
- Optional real Groq chat-completions extraction of summary and generic project domains, with a visible AI status. If no key is configured, **Parse Resume** uses a limited baseline and **Align to Job** refuses rather than creating fake AI output.
- Project relevance ordering against a JD, with stable ties and a ten-project cap.
- JD alignment produces a redesigned draft and a baseline ATS report scored against the ORIGINAL uploaded resume. Edited, unverified claims do not change the ATS score.
- Print-ready A4 Algoworks profile, with green section rules and a half-width two-column technology stack. The normal resume style hides the company logo.
- Existing dashboard and analysis endpoints stay intact.

## Important restrictions / known gaps

- Groq requires internet access and may incur API costs. Resume content is transmitted to Groq when AI features are enabled. Obtain appropriate permission/consent before uploading other people's private resumes.
- PDF files must have a selectable text layer. Scanned PDF/Tesseract OCR remains a future milestone.
- The AI summary targets exactly 85 words, but if the uploaded resume lacks enough supporting facts or the model returns a different count, the preview displays the actual word count and asks for review; it **never pads fabricated qualifications**.
- An AI project description is a **draft** anchored to an extracted evidence snippet; review its claims. Additional technologies typed inline are not independently verified.
- Experience is extracted primarily from explicitly listed years. Employment-date interval calculation is still being refined.
- The PDF feature uses the browser's print dialog (Save as PDF). DOCX direct export and authenticated SQL Server persistence are planned and are not implemented here.
- Current storage is a local JSON development store under `backend/RoleFit.Api/App_Data/` and the API binds to 127.0.0.1. Do not deploy publicly until authentication, private storage and data retention are implemented.
- The included logo is for authorized use. Ensure proper permission for any public distribution.
- This environment cannot run the .NET 10 build; compile and run on Windows and report any errors shown by `dotnet run`. Frontend source was syntax-checked.

## Testing

Try `samples/project-heavy-resume.txt` against `samples/fullstack-jd.txt` with Groq enabled. Check that only 10 source-backed projects appear, Amazon shopping website is generalized to an e-commerce domain, no skills are silently added, and the experience displays 4.5 years. Also verify the sample resumes from the earlier MVP.

For the Groq API and supported model information, see https://console.groq.com/docs/api-reference and https://console.groq.com/docs/structured-outputs .
