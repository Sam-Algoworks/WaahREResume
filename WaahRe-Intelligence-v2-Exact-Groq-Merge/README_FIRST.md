# Waah रे Resume — Intelligence v2.3 (exact Groq manual merge)

**Use this package instead of v2, v2.1 or v2.2. Do not force earlier installers.**

## Why the earlier update stopped

Your actual `GroqProfileService.cs` has newer fixes not present in the reviewed baseline, and Git's three-way merge correctly rejected overlapping edits. You supplied that exact file. This package uses it as the manually reviewed baseline and adds only the Intelligence v2 changes needed here:

1. Set `baseline.ExperienceYears = ResumeEvidence.DisplayYears(sourceText)` rather than trusting the AI-proposed decimal or leaving an inaccurate zero. The `ResumeEvidence` service is supplied in this package.
2. Require AI-project quote evidence to be part of the **same** existing project before applying an enriched project description.

**Preserved from your uploaded Groq service:** `DefaultIgnoreCondition.WhenWritingNull`, 10-project output limit and compact evidence, Groq `reasoning_effort`, 6000-token output limit and token-limit error reporting, designation preservation, existing skill union, independent project-heading verification, 85-word summary correction, no invented facts. The local-only API guard, SQL History, exports, and UI stay unchanged.

## Installation

1. Stop Visual Studio backend and Vite frontend. Privately back up your `RoleFit-AI` folder, SQL `WaahReHistory` and `App_Data`.
2. Extract **WaahRe-Intelligence-v2-Exact-Groq-Merge** into the **main RoleFit-AI folder**, beside `backend` and `frontend`.
3. Run `APPLY_INTELLIGENCE_V2.bat`. Git for Windows must be installed.
4. The installer stages **all** changes and checks source hashes. For other modified files it attempts a non-forcing three-way merge. No files are changed on `SAFE STOP`.
5. If every precheck succeeds, type `YES` to apply. A timestamped backup of affected source files will be created automatically.
6. From the RoleFit-AI root run:

```powershell
dotnet build .\backend\RoleFit.Api\RoleFit.Api.csproj
dotnet run --project .\tests\RoleFit.IntelligenceV2Checks\RoleFit.IntelligenceV2Checks.csproj
```

7. Test fresh parses of the supplied sample resumes: Aanya Sharma = 0 months, 2 projects; Kabir Sethi = 14 months, 2 projects; Neha Rao = 32 months, 2 projects; Riya Mehra = 28 months, 3 projects. Check ATS ranking using ONE consistent JD. Then check History, original-file downloads, and PDF/DOCX exports.

## Rollback and limitations

If .NET build or checks fail, stop the app and use `ROLLBACK_INTELLIGENCE_V2.bat` from this update folder. It restores the backed-up source, not database records. **Do not reparse real applicants until the checks pass.**

This package is tested with static source checks and Git merge simulations. A full Windows .NET/Groq/SQL build cannot be performed here. If another `SAFE STOP` appears, upload **only the exact current file named in that error**; don't bypass the safeguards.
