# RoleFit AI — UX Refresh (Direct Source Update)

This update is for the **existing working Phase 5** RoleFit AI project with the Resume Mode Switch changes applied. It is **not** a fresh installation. The existing GroqProfileService, ResumeProfileParser, print CSS, resume logo, saved data, and other backend services remain unchanged.

## What's new

- One button on the Resume Studio: Analyze & Build Profile. A valid JD triggers the existing `/api/resumes/align` endpoint (and ATS alignment), while no JD triggers `/api/resumes/parse`. The score remains based on the original resume evidence.
- Two **meaningfully different** modes: Algoworks (editable branded professional profile) and Original resume (read-only uploaded PDF or TXT; DOCX is an extracted text preview). The original mode has no added Algoworks logo.
- Auto-save newly parsed and aligned structured profile drafts in the existing `App_Data/profile-drafts.json` backend store. This does **not** save original resume binaries.
- One Talent Library sidebar item; Candidates and Evaluations tabs; clickable records; a details drawer; reopen saved profiles for editing. Analysis-only records show their saved ATS breakdown; names alone are never used to auto-merge records.
- Cleaner navigation and optional collapsible JD input; reduced-motion-aware CSS animations, hover effects, dark editorial SaaS design inspired by user's portfolio.

## Install (safe update, not a reset)

1. Close the API debugger (Shift+F5) and stop the Vite terminal (Ctrl+C).
2. Back up the existing RoleFit-AI folder (especially `backend/RoleFit.Api/App_Data`).
3. Extract this ZIP. Copy the extracted `RoleFit-UX-Refresh-Direct-Update` folder INSIDE your existing RoleFit-AI folder.
4. Double-click `APPLY_UPDATE.bat` inside the extracted update folder. The script makes per-file backups under its `backup` folder, preserving the original project and local records.
5. Open backend/RoleFit.slnx in Visual Studio Community and rebuild. Start with the RoleFit Local launch profile.
6. Open frontend/rolefit-ui in VS Code; run `npm.cmd run build`; then `npm.cmd run dev`.
7. Check the site at the Vite Local address (`http://127.0.0.1:5180` or the port actually shown).

**Alternative manual installation:** Copy the eight files inside `files` into the identical relative paths inside your existing RoleFit-AI directory. `TalentLibrary.tsx` and `Experience.css` are new; the remaining six are complete replacements. Do not copy the outer `files` folder over the project root as a single folder; copy its contents respecting the folder structure.

## Test cases

1. Dashboard should show 5 navigation links (Overview, Analyze & Align, Bulk Ranking, Job Descriptions, Talent Library).
2. Open Analyze & Align, upload a PDF resume, leave JD blank, press **Analyze & Build Profile** once. Verify profile appears, then switch to **Original resume**. The original PDF should appear without any added company branding.
3. In a fresh test upload, expand Add job description and enter a valid JD; press the same button once. The returned score uses the original source evidence. Switch modes and confirm both views differ.
4. Reopen Talent Library > Candidates. Parsed profile should be present without pressing Save Profile Draft. Click it and use Open editable profile.
5. Open Talent Library > Evaluations. Click an evaluation, inspect score and matched/missing skills. Older saved analyses should remain available.
6. Print Algoworks PDF and verify your established print stylesheet still works. The original source mode is only a viewer; Print Algoworks PDF intentionally exports the branded profile.
7. Verify Bulk Ranking, saved JDs and historical scores remain unchanged.

## Limitations / privacy

The existing local-only MVP still stores JSON records without user authentication. Do not expose this API on the public internet or upload sensitive resumes without appropriate safeguards. Original PDF/DOCX/TXT files are shown only during the live browser session; they are **not archived** or reconstructed later. DOCX preview shows extracted text, not exact page formatting. Scanned PDFs still need OCR. SQL Server, access permissions and protected file archiving remain future phases.

## Validation

The changed TSX/TS files passed TypeScript's syntax transpilation checks. Full npm/Vite build and .NET build could not run here (dependency install stalled and .NET SDK is absent), so rebuilding locally is required. No working Groq/parser algorithm file is replaced by this update.
