# RoleFit AI — Ready-to-Run Local Development MVP

**Included:** independent .NET 10 Web API + React/TypeScript frontend. No code copied from any previous Resume Parser project.

This ZIP is **a working starter/MVP**, not the completed 15-phase production recruitment platform. It is designed to replace the broken initial scaffold, prevent the 5173 port conflict, and provide real end-to-end baseline functionality without needing an AI API key or SQL Server configuration on day one.

## Windows quick start

1. Install the **.NET 10 SDK** and **Node.js 22.12+ (or 20.19+)**, if they are not already installed. .NET 10 backend development through Visual Studio needs Visual Studio 2026.
2. Extract the entire ZIP. **Do not merge this folder into the old `rolefit-ui` directory.** You can keep the previous project as a backup.
3. Double-click **`START_ROLEFIT.bat`**.
4. Two terminal windows will open, one for the API and one for React. First run downloads NuGet and npm dependencies and therefore requires internet.
5. When the frontend reports ready, open **http://127.0.0.1:5180**. If another application uses that port, Vite automatically selects the next one and prints its actual URL.
6. Confirm the top-right **API Connected** badge. You can also visit **http://127.0.0.1:5099/api/health**.

**Important:** Keep both server terminal windows open while using the application. Close them to stop the application.

### How to test actual functionality

1. Select **Job Descriptions** and save a new job, e.g. title `Backend Developer`, description `We need an ASP.NET Core developer with C#, SQL Server, REST API, Docker, and 3 years of experience. Responsibilities include design, develop and test web applications.`
2. Select **Resume Analyzer**, choose the saved job, and upload an actual `.pdf` with selectable text, `.docx`, or `.txt` resume. The backend extracts text and computes a transparent, deterministic initial alignment estimate. Results are **real calculations based on document text, not canned scores**.
3. Select **Bulk Ranking** and upload up to 10 resumes against the same job description. The ranked results include actual matched and missing recognized skills. Document failures are shown individually.
4. Open **Analysis History** to see saved analyses. The **Overview** statistics reflect saved jobs and analyses. Data persists in the API's local `App_Data/rolefit-local.json` file.

Test files are included in `samples/`: copy `backend-developer-jd.txt` into the JD field, then upload `candidate-a.txt` and `candidate-b.txt` to compare their rankings. These are synthetic examples, not real candidate profiles.

## What's included and what isn't

| Feature | Status |
|---|---|
| Clean .NET 10 backend and React UI | Included |
| Backend/frontend connection via Vite proxy | Included |
| Port 5173 conflict avoidance | Included (prefers 5180, auto-fallback) |
| Persistent local saved jobs and analyses | Included (JSON storage for one developer only) |
| Actual PDF text extraction, DOCX, TXT | Included (PDF requires selectable text) |
| Single-file baseline alignment scoring | Included (deterministic; explainable) |
| Bulk ranking (up to 10 resumes) | Included |
| Candidate directory and history | Included (based on saved analyses) |
| SQL Server + EF Core | **Not yet implemented** |
| Groq semantic extraction / prompt logic | **Not yet implemented** |
| Tesseract OCR for scanned PDFs | **Not yet implemented** |
| Verified skills workflow, JD versioning and editing | **Not yet implemented** |
| Resume editor and PDF/DOCX exporting | **Not yet implemented** |
| Authentication / RBAC / production privacy controls | **Not yet implemented** |

**This project is NOT production-ready.** It has no user login or backend authorization, no malware scanning, no configured data retention, and JSON-file persistence. Only run the API on localhost, do not expose it on the internet, and do not upload real third-party resumes without appropriate consent and safeguards.

### Scoring notes

The baseline score uses recognized skill evidence (35 points), explicitly mentioned experience (20), JD responsibility terms (20), a basic project-mention check (15), qualification terminology (5), and document readability (5). Criteria that cannot be evaluated are excluded and the remaining weights redistributed. The score is an internal **estimated JD-alignment** value out of 100, **not an industry-standard ATS score or hiring recommendation**. Semantics, negation, specialized skills, transferable skills and verified evidence need more work. Never use this as an automatic rejection decision.

## Developer commands

Backend (PowerShell):

```powershell
cd .\backend\RoleFit.Api
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5099'
dotnet run --no-launch-profile
```

Frontend (a second PowerShell):

```powershell
cd .\frontend\rolefit-ui
npm.cmd install
npm.cmd run dev
```

Backend code: `backend/RoleFit.Api` (open `.csproj` in Visual Studio).
Frontend code: `frontend/rolefit-ui` (open folder in VS Code).

### Why the original blank page happened

Your earlier console reported `Invalid hook call` and `Cannot read properties of null (reading useContext)`. This fresh frontend pins matching React/React DOM versions, keeps exactly one React project root, and includes a render error boundary to display errors rather than failing silently. It also avoids occupied port 5173 by using 5180 with automatic fallback.

## Troubleshooting

- **Port 5173 already in use:** Ignore it; this project uses 5180. If 5180 is occupied, use the fallback URL printed by Vite.
- **API Offline:** Look at the API PowerShell window and visit `http://127.0.0.1:5099/api/health`. API must run on 5099 for Vite proxy to work.
- **`dotnet` not recognized:** Install .NET 10 SDK and reopen terminals.
- **`npm` blocked by PowerShell execution policy:** Use `npm.cmd`, which is already built into the launcher.
- **NuGet / npm download errors:** Internet connection is needed on first run to restore external packages.
- **Blank page:** Open F12 → Console and report the first red error. `src/main.tsx` includes a React error boundary for application rendering failures.
- **PDF text unavailable:** Scanned or encrypted PDFs are not yet supported; use a DOCX/text-based PDF for this MVP.
- **Existing backend already on 5099:** Close that other API instance first. Do not terminate unrelated Windows processes.

## Development roadmap

Next: migrate JSON storage to SQL Server with EF Core, implement versioned jobs and candidate evidence, add a secure configurable Groq client, then OCR, full ATS scoring, editing and exports. Implement authentication, access control and privacy safeguards before any shared deployment.

## Phase 3 update — Resume Parser Studio + JD file upload

The sidebar now includes **Resume Parser**, which opens an Algoworks-styled professional
profile with direct inline editing. Upload a PDF with selectable text, DOCX or TXT
and click **Parse resume** to extract a baseline evidence-grounded profile.
Click straight into any visible field (no Edit mode), then use **Save profile draft**.
Edits are local drafts and do not automatically change official ATS scores.

JD file upload is also available in **Job Descriptions**, **Resume Analyzer**,
**Bulk Ranking**, and **Resume Parser**. Uploaded JD text becomes editable.

- New endpoints: `POST /api/documents/read`, `POST /api/resumes/parse`,
  `GET /api/resumes/{id}`, `PUT /api/resumes/{id}/draft`.
- Drafts are stored locally in `backend/RoleFit.Api/App_Data/profile-drafts.json`.
- SQL Server, Groq parsing, scanned-PDF OCR, official company logo asset,
  document exports, and verification workflow are *not* included yet.
- The company wordmark in the profile is a visual stand-in. Use an officially
  authorized logo asset before sharing externally.
- The development API is for localhost use only and is not production-secure.
