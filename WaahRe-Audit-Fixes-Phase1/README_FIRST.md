# Waah रे Resume — Audit Fix 01

This is a **small source-level safety and scoring correction** for the existing project.
It is NOT the complete public deployment upgrade.

## Changes

- Backend startup now **fails closed in Production** until real authentication and durable private storage are implemented.
- Development requests must be loopback; global IP rate limit is 90 requests/minute.
- JD-required experience missing from a resume receives **0 of 20 experience points** rather than being excluded and redistributing weight.
- The word `project` alone no longer earns the full project score. The deterministic parser must detect real project evidence.
- Explicit `Required skills:` / `Competencies:` lists work for technical and nontechnical professions. Matching requires exact source wording; this remains a baseline and is not a validated hiring prediction.
- Adds a standalone scoring regression-check project.
- No changes to the Resume Parser extraction, Groq, React UI, PDF or Word export.

## Safe application

1. **Stop Visual Studio backend** (Shift+F5); stop the frontend if running.
2. Make a working ZIP backup of your current RoleFit-AI folder, excluding private `App_Data` if sharing it.
3. Put the extracted `WaahRe-Audit-Fixes-Phase1` directory inside your main `RoleFit-AI` directory.
4. Double-click `APPLY_AUDIT_FIX.bat`. The installer checks the two exact source files against the reviewed baseline and **refuses to overwrite if your local source differs**, avoiding old-version regression. Backups are created before copy.
5. In Visual Studio: Build → Rebuild Solution. Start the `RoleFit Local` launch profile.
6. At the main project root run `dotnet run --project .\tests\RoleFit.ScoringChecks\RoleFit.ScoringChecks.csproj`.
7. Also run the old parser regression tests: `dotnet run --project .\tests\RoleFit.ParserChecks\RoleFit.ParserChecks.csproj`.
8. Test ATS checks with the same JD/resumes before and after; existing stored scores are historical and won't be silently recalculated.

**Important:** Do not deploy this version publicly or process real candidate data through public URLs. The safety guard intentionally prevents production startup until a proper authentication/database phase is complete. It does NOT provide login, authorization, encrypted database, backups, or managed persistent storage.

If the installer refuses due to changed files, **do not force-copy replacements**. Your actual Windows source has newer edits than the reconstructed baseline; compare the two files or share the relevant current files to merge safely.
