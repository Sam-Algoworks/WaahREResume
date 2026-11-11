# Waah रे Resume — Rakshit Accuracy and Contact Removal (Focused Fix)

This is a **source-only corrective update** for the already installed Intelligence v2.3 project. It does not overwrite the parser, Groq service, scorer, SQL repository, UI design, or History.

## Actual problem this addresses

The original Rakshit PDF contains **four** named project sections, but the current UI displays one empty project. The old parser rejects some titles with punctuation and cannot reliably recover sections after PDF text extraction. Separately, a phone/location line below the name was mistaken for the candidate's designation.

The new `SourceProjectReconciler` performs a second, source-only scan of headings within the Projects section. It reconstructs project-local descriptions and technology lists without adding model-invented projects. The same reconciliation is run after Groq enrichment so AI cannot silently change project counts. It also strips extracted phone/email fields and rejects a designation that appears to be contact information.

**For Rakshit:** The original PDF has four projects. It documents no professional employment, so **zero verified professional years is not an extraction failure** for that particular source. An educational year range or academic projects must not be converted to work experience. The employer-specific designation is not in that PDF; the profile should not use the phone/location line as a designation.

## Important scope and limitations

- Parsed Algoworks profiles will not include phone or email (including exports of older saved drafts through this UI). The **Original resume** viewer and original archived document remain unchanged.
- A fresh parse is needed to correct an old saved project's count. Old stored drafts are **not retroactively re-extracted**.
- The companion evidence checks are not a full model/ATS accuracy guarantee. In particular, PDF text extraction may not preserve headings in every scanned/complex layout.
- A `SAFE STOP` is expected if your current controller/editor/export utility differs from the reviewed structure. Please provide the named current file rather than forcing a replacement.
- This update does not modify ATS scoring, rankings, Groq API requests, history, SQL, credentials, or deployment restrictions.

## Install

1. Stop the backend and frontend.
2. Back up your RoleFit-AI folder, SQL database and App_Data folder.
3. Extract `WaahRe-Rakshit-Accuracy-Fix` into your main `RoleFit-AI` folder, beside backend and frontend.
4. Double-click `APPLY_ACCURACY_FIX.bat`.
5. After all prechecks pass, type `YES` to create source backups and apply.
6. Build: `dotnet build .\backend\RoleFit.Api\RoleFit.Api.csproj` from the project root.
7. Run regression checks: `dotnet run --project .\tests\RoleFit.ProjectRecoveryChecks\RoleFit.ProjectRecoveryChecks.csproj` from the project root.
8. Frontend: `cd frontend\rolefit-ui` then `npm.cmd run build`, `npm.cmd run dev`.
9. Start your backend under the usual `RoleFit Local` Development profile.

## Verify

- Re-parse the attached source PDF `rakshit_mehta_cv_n(2).pdf` as a **fresh** upload.
- Expect 4 distinct projects with substantive descriptions and project-local skills; no placeholder fifth project.
- Expect no phone/email in Algoworks preview, PDF or Word. The original resume tab retains the unmodified source.
- Test Riya (3 genuine projects), Aanya (2), Kabir (2), Neha (2), plus a non-IT source.
- Confirm the ATS Check, Rankings, History, Delete Record and export functions still work.

## Rollback

Source backups are stored under `WaahRe-Rakshit-Accuracy-Fix/backup_YYYYMMDD_HHMMSS`. Copy `ResumesController.cs`, `ResumeParserPage.tsx` and `resumeExport.ts` to their corresponding project paths, then delete the newly added `backend/RoleFit.Api/Services/SourceProjectReconciler.cs` and `tests/RoleFit.ProjectRecoveryChecks` folder. Rebuild. Never delete App_Data or your SQL database to undo a source patch.

## Verification performed by assistant

Static checks on modified source anchors, ZIP integrity, and source-based fixture analysis. The exact Windows .NET 10 / PdfPig / Groq runtime cannot be executed in the assistant environment, so please run the build and fresh parse locally before accepting the result as fixed.
