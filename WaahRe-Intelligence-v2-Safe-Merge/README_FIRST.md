# Waah रे Resume — Intelligence v2.2 Safe Source Merge

This package is a revision of the Intelligence v2.1 controller merge. **Do not run older v2/v2.1 installers.**

## What changed

Your screenshot shows the older installer stopped at `backend/RoleFit.Api/Services/ResumeProfileParser.cs`. Your file hash is `4B8CD76808E9CE35B8B3F098DF4AB4566E03838686D918CE4D5979F6C2E6F5D8`, different from the exact historical file that the previous package expected. That difference means we must not overwrite it blindly.

The v2.2 installer stages **all** source changes before applying anything. For exact reviewed hashes it stages the replacement as before. For unexpected source versions (including the parser), it attempts a non-forcing **Git three-way merge** against the corresponding original historical source and the improved v2 file. All original line-ending styles and your unchanged lines are preserved. **If any edit overlaps, it exits SAFE STOP with NO source modifications**. At that point, upload the specifically named current C# file; there is no safe way to know the overlapping modifications from a SHA-256 hash alone.

It also preserves the existing original-resume archive calls in single/bulk ATS evaluation (the v2.1 fix). The rest of the Intelligent v2 functionality is unchanged: source-grounded experience extraction, projects verification, semantic JD comparisons, same-rubric ranking, tests. No login UI, SQL migrations or frontend edits.

## Before running

1. Stop Visual Studio debugging and the frontend.
2. **Manually back up** the entire project and the private SQL `WaahReHistory` database / `App_Data` folder. Never upload those or your Groq API key.
3. Extract `WaahRe-Intelligence-v2-Safe-Merge` into the main `RoleFit-AI` directory beside `backend` and `frontend`.
4. Ensure **Git for Windows** is installed (`git --version` works in PowerShell).
5. Double-click `APPLY_INTELLIGENCE_V2.bat` in the NEW folder. Read the `MATCH`, `MERGE`, or `SAFE STOP` messages. Only type `YES` after all prechecks have passed.

This is a local-only update. **It does not make public deployment safe.**

## Build, tests, and rollback

In PowerShell, from the main `RoleFit-AI` root:

```
dotnet build .\backend\RoleFit.Api\RoleFit.Api.csproj
dotnet run --project .\tests\RoleFit.IntelligenceV2Checks\RoleFit.IntelligenceV2Checks.csproj
```

Test a fresh parse of the synthetic Aanya, Kabir, Neha and Riya resumes (expected professional months: 0, 14, 32, 28; expected genuine projects: 2, 2, 2, 3). Then ATS compare and bulk-rank against ONE consistent JD. Open History and verify original files are downloadable, plus PDF/Word exports. Older records and scores do not recompute automatically.

If the build or regression checks fail, stop and run `ROLLBACK_INTELLIGENCE_V2.bat` from this same update folder, type `ROLLBACK`, and share the exact build error. Source backups are created only after you type YES. No SQL or App_Data files are modified.

## Limitations

- This safe merge cannot infer what changed in an unuploaded source file. It **never** discards conflicting code just to make the installer pass.
- A clean textual merge does not guarantee a compiling or correct behavioral merge. The .NET build and tests must be run on your PC. The package was statically inspected and tested with synthetic merge fixtures; Windows + .NET + Groq testing has not been performed here.
- If `git merge-file` signals a conflict, the installer reports the exact file to upload. **Do not remove hash checks, delete the file, or force replacement.**
