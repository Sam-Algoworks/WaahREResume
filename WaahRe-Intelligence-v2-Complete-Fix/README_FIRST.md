# Waah रे Resume — Intelligence v2 Accuracy Fix

**Read this before installation.** This is ONE complete backend replacement package for your **existing** `RoleFit-AI` project. It reconstructs the reviewed backend from your uploaded `Program.cs` and the Phase 3/4/SQL-History source updates. It DOES NOT include a replacement frontend and DOES NOT create a new standalone project.

## Problems addressed

1. **Experience**: parses `2 years 8 months`, `1 year 2 months`, `2 years 4 months`, numeric years, fresher, and (when explicit years are absent) nonoverlapping professional employment dates. Original months are used for ATS scoring. The editable years field retains your display-rounding rule.
2. **Wrong projects**: detects `CORE SKILLS`, `PROFESSIONAL DEVELOPMENT`, `COURSEWORK`, and other section boundaries. Groq cannot add a project without an independently present heading, and cannot replace a project's description with evidence from another project. Genuine projects remain capped at ten.
3. **Universal competency reasoning**: with your existing Groq key, the new assessor compares JD requirements against source-backed semantic equivalents across professions (including non-IT). Direct/equivalent/partial/missing matches receive differentiated credit; every claim needs literal original-resume evidence.
4. **ATS experience**: explicit JD experience minima get accurate months-based credit and don't silently drop out of the score when a resume lacks them.
5. **Comparable bulk rankings**: one shared JD rubric is used for the batch; if any semantic assessment fails, the entire batch consistently uses the deterministic fallback rather than mixing scores from different methods.
6. **Transparency**: new results include `verifiedExperienceMonths`, `requiredExperienceMonths`, `semanticMatchingUsed`, and source `evidenceMatches` fields in API JSON. Existing UI/API fields are preserved.

**This is an estimated JD alignment score, not a certified ATS score or an automatic hiring decision.** Years are evaluated against the JD's minimum, not used to automatically favor the oldest or longest-serving candidate. Groq is not allowed to invent skills or projects to improve a candidate's ranking. Without Groq access, the existing rule-based scorer runs as fallback; it will not provide equivalent semantic reasoning.

## Before installation

- Close Visual Studio debugging / backend and the Vite frontend.
- Make a separate backup of your whole project, SQL `WaahReHistory` database and `App_Data` folder. Keep real candidate data private.
- Extract this `WaahRe-Intelligence-v2-Complete-Fix` folder INSIDE the **main** `RoleFit-AI` folder, beside `backend`, `frontend`, and `tests`.
- This update does **not** add a login or modify your local-only access restrictions. Do not deploy public real HR data.

## Apply

Double-click **`APPLY_INTELLIGENCE_V2.bat`**, review the prechecks, type **`YES`**. The script checks SHA256 values of the exact reviewed source versions and backs them up before copying the full replacement `.cs` files. If a source differs, it stops with `SAFE STOP` and changes nothing. Do not force a merge or delete source files.

## Build and run tests

Open PowerShell at the **main `RoleFit-AI` root**, not the `backend` subfolder:

```powershell
cd "C:\Users\SameerSaifi\Downloads\RoleFit-AI-Ready-to-Run-MVP\RoleFit-AI"
dotnet build .\backend\RoleFit.Api\RoleFit.Api.csproj
dotnet run --project .\tests\RoleFit.IntelligenceV2Checks\RoleFit.IntelligenceV2Checks.csproj
```

If installed, also run the earlier test projects:

```powershell
dotnet run --project .\tests\RoleFit.ParserChecks\RoleFit.ParserChecks.csproj
dotnet run --project .\tests\RoleFit.ProjectEvidenceChecks\RoleFit.ProjectEvidenceChecks.csproj
dotnet run --project .\tests\RoleFit.ScoringPhase4Checks\RoleFit.ScoringPhase4Checks.csproj
```

Start the API using `RoleFit Local` from Visual Studio and the frontend with `npm.cmd run dev` from `frontend/rolefit-ui`. Use **fresh parses** and **new ATS analyses**; older saved profiles and scores are snapshots and won't be overwritten.

## Test sequence

- Parse the included synthetic Aanya, Kabir, Neha and Riya fixtures. They should produce 0/14/32/28 factual experience months and 2/2/2/3 original projects. The editable experience-years display follows the current rounding rule.
- Check the source original and the parsed profile side by side. No duplicate project, stray skill row, or unverified AI-generated project is allowed.
- ATS Test: use a JD with a **2-year minimum**. 0-month candidate: 0/20 experience; 14-month candidate: partial experience; 32-month candidate: 20/20 experience.
- Try a JD requiring Web Design against an HTML/CSS/JS-only resume. Semantic matching may award *partial*, not automatic full credit. The model needs actual design evidence for full credit.
- Run bulk rankings using the **same JD** and verify all candidates have consistent `semanticMatchingUsed` values. If Groq is unavailable, they must all fall back together.
- Test History, SQL persistence, Delete Record, editable profile, PDF/DOCX downloads and re-upload. These were deliberately not changed.

### Important limits

- I did not receive your exact ranking JD. The tests use a synthetic 2+ years UI/UX JD; no exact ranking order is asserted for your original JD.
- The supplied environment does not have a .NET SDK, Windows SQL Server or your configured Groq account. Source/ZIP structure and sample-fixture logic are checked here, but the C# build and live model calls must be verified on your PC. The installer is guarded for this reason.
- Semantic scoring depends on an external Groq API request and may be slower than keyword scoring. It has a per-request timeout and deterministic fallback.
- The resume `experienceYears` display is rounded by your existing convention. ATS scoring uses exact source-supported *months*, not rounded display years.
- No frontend or database schema migration is included. New API JSON metadata is optional for existing clients.

## Rollback

Stop the app and run `ROLLBACK_INTELLIGENCE_V2.bat`. Type `ROLLBACK`. The update's source-code backup will be restored; the added test folder and new helper classes will be removed. No SQL or `App_Data` files are touched by either script.
