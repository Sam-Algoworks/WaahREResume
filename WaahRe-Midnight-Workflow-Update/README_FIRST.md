# Waah रे Resume — Midnight Workflow Update

This is a **source update for the working project**, not a fresh installation. It preserves the existing storage (`App_Data`), Groq configuration, parser services, saved history, company-logo assets, project extraction, and scoring engine.

## What's changed

- Entirely different **midnight navy + lime** interface, with subtle animations and reduced-motion support.
- Separate top navigation tabs: **Home · ATS Check · Rankings · Resume Parser · Jobs · History**.
- **ATS Check** requires a job description. It does not modify the uploaded resume. After scoring, the candidate review shows the original PDF/text and actual ATS evidence side by side.
- **Align to JD** is a separate action *after* ATS Check. It reorders existing, source-supported skills and project entries only. The API explicitly preserves the original name, designation, and experience-year value; it never fabricates skills or changes the original source-based score.
- **Resume Parser** is independent, requires no JD, and generates the editable Algoworks professional profile. No alignment or ATS scoring happens there.
- **Save draft** and **Download PDF** remain visible at the top of the editable preview rather than buried at the bottom.
- An A4-width, internally scrolling editor replaces the visually endless document. Printable A4 content uses a separate print host; it is hidden on-screen to avoid duplicated resumes and blank pages.
- History is retained without destructive migrations.

## Installation — Visual Studio Community + VS Code

1. Stop Visual Studio debugging and press Ctrl+C in the frontend terminal.
2. Back up your working `RoleFit-AI` project first.
3. Extract this ZIP and put the **entire `WaahRe-Midnight-Workflow-Update` folder** inside the existing `RoleFit-AI` folder, alongside `backend` and `frontend`.
4. Open the update folder and double-click **`APPLY_UPDATE.bat`**, then type **YES**. Only the listed source files are copied. Existing versions are backed up into `WAAHRE_SOURCE_BACKUP_<timestamp>`.
5. In Visual Studio Community, rebuild `backend/RoleFit.slnx` and run the `RoleFit Local` API profile.
6. In VS Code, open `frontend/rolefit-ui`, then run `npm.cmd run build` and `npm.cmd run dev`.
7. Open the exact Vite Local URL, commonly `http://127.0.0.1:5180`, and press Ctrl+Shift+R.

## Windows verification checklist

- **ATS Check** without JD: Analyze disabled, explaining a JD is required.
- **ATS Check** with JD + resume: "Check ATS Score" displays original file on left and score on right. No alignment happens by itself.
- **Align to JD** from candidate review: generates a JD-prioritized draft. Confirm the **experience years, name, designation, and original ATS score** are not rewritten. Needs a configured Groq backend.
- **Resume Parser** from navigation: upload a resume without a JD, press "Parse Resume", edit inline, and download PDF from the always-visible toolbar.
- Printed PDF should contain the Algoworks logo, professional summary, compact technology stack, genuine projects, no duplicate screen preview, and no blank extra pages. Browser print dialog: uncheck "Headers and footers".
- **Rankings** and **History** should still open the existing saved records; scores remain estimates, not hiring decisions.
- If a history evaluation no longer has its original JD stored, the Align action is disabled rather than using an unrelated JD.

## Important limitations

- A full React/Vite build and live .NET/Groq execution must be verified on your Windows machine. Source syntax and ZIP integrity were checked, but the environment preparing this package lacks the required npm dependencies and .NET SDK.
- The editable document now uses a bounded A4-style viewport. Exact on-screen multipage pagination is not implemented; printed PDFs use CSS page breaks.
- This patch does not change the actual ATS scoring weights or claim benchmark superiority over other systems.
- Previewed original PDFs may contain photos, but Algoworks-generated profiles do not embed the candidate photo.

## Rollback

Stop both servers. Restore the changed files from the `WAAHRE_SOURCE_BACKUP_<timestamp>` folder into the matching paths under the project, then rebuild. New stylesheet files can be removed if needed, along with their imports from `main.tsx`.
