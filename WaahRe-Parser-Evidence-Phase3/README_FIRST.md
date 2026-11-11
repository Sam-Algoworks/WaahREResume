# Waah रे Resume — Phase 3: project evidence guard

## Purpose
Prevent Groq from multiplying resume projects by promoting a phrase in a description into a second project card. An AI-suggested project that the deterministic parser did **not** identify must correspond to a **distinct project heading** in the original uploaded resume. The test fixtures include software, healthcare, finance/operations, bullet, numbered and long-project-list formats. This is industry-independent and does **not** hardcode candidate names.

**Scope of this update:** Adds a small `ProjectHeadingEvidence.cs` helper and inserts one guard into the **existing** `GroqProfileService.cs`. Adds a separate parser regression console test project. Does not replace `ResumeProfileParser.cs`, which has already been corrected for the two-project case. Does not change UI, ATS, SQL, History, Groq credentials, or PDF/Word export.

## Installation

1. Stop Visual Studio debugging and the Vite frontend.
2. Make a private backup of your current project. Keep your SQL history and `App_Data` safe.
3. Extract the **WaahRe-Parser-Evidence-Phase3** folder inside the main `RoleFit-AI` directory (at the same level as `backend` and `frontend`).
4. Double-click `APPLY_PHASE3.bat`, read the checks, and type `YES`.
5. In Visual Studio Community, rebuild `RoleFit.Api` and launch it as usual.
6. In a terminal from the **main `RoleFit-AI` folder**, run:

   ```powershell
   dotnet run --project .\tests\RoleFit.ProjectEvidenceChecks\RoleFit.ProjectEvidenceChecks.csproj
   ```

7. Re-upload and freshly parse a **synthetic** two-project resume. Saved historical drafts do not change retroactively. Confirm the output contains exactly the two project headings and project-local technologies.

## Protection against overwriting your current work

- The installer **does not replace** `GroqProfileService.cs` with an older copy. It looks for one exact, known line and inserts the additional evidence guard immediately after it.
- If your service has changed in that part, the installer reports **SAFE STOP** and makes no changes. In that case, provide the latest `GroqProfileService.cs` for an exact merge.
- If the evidence helper or test folder already exists, the installer stops rather than overwriting them.
- On success, the original Groq service is saved in a dated `backup_...` folder next to the installer.

## Behavior

- The deterministic parser's project list remains the starting point.
- Groq can improve description and domain fields for an already recognized project.
- Groq may **only add** a project absent from that list when the **source text** contains a new independent project heading that the existing project evidence does not already cover.
- A project name in a sentence, a technology-only line, ordinary work experience, or another project's evidence cannot create an extra project.
- This is deliberately conservative. If PDF text extraction flattened a genuine heading into prose, this guard may prevent Groq from recovering it; the correct longer-term fix is layout-aware extraction and a human review option.

## Rollback

Stop the API. Locate the dated `backup_...` folder inside the update folder; copy its `GroqProfileService.cs` back to `backend/RoleFit.Api/Services/`. Then delete `backend/RoleFit.Api/Services/ProjectHeadingEvidence.cs` and the new `tests/RoleFit.ProjectEvidenceChecks/` directory. Rebuild the API.

## Verification limitations

The package and insertion algorithm were checked against the available project source, but this environment has no .NET SDK or your Windows SQL Server/Groq credentials, so **running the regression tests and a live parse on your machine is mandatory**. A passing test is not a guarantee of accuracy across every PDF layout. Never upload real HR resumes to a public anonymous demo.
