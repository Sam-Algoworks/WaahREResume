# Waah रे Resume — Phase 5: UI polish and export round-trip QA

This is an incremental update to the current working project, **not** a new project or a backend replacement. It assumes the History Delete Manual Merge source and Phase 4 backend are installed. No login is added.

## Changes

- `frontend/rolefit-ui/src/App.tsx`: replace the unnecessary four-step ATS onboarding panel with a concise placeholder; remove the “not mock data” technical label and an unnecessary badge. The ATS calculation and navigation stay unchanged.
- `frontend/rolefit-ui/src/components/TalentLibrary.tsx`: correct the History modal focus behavior. Tab/Shift+Tab remain within the open drawer; Escape closes it; focus returns to the history row; background scrolling is disabled while open. Add `aria-labelledby` and `aria-pressed` semantics. Remove the old claim that production requires a sign-in page; no login or security guarantee is implied.
- `tests/RoleFit.ExportRoundtripChecks`: a standalone .NET test that **compiles the exact existing `DocumentReader.cs`** and feeds actual exported PDF and DOCX files through the same extraction logic used by the ATS backend. No resume content is printed to the terminal or sent to a server by the test.

## Install

1. Stop frontend/backend and save a copy of your project.
2. Copy this entire `WaahRe-Phase5-UX-Export-QA` folder next to `backend` and `frontend` in your working `RoleFit-AI` project.
3. Run `APPLY_PHASE5.bat`. It checks exact SHA-256 hashes of the two frontend files first and **stops without changes** if your files have changed. It backs up the original source files before applying updates. Never bypass SAFE STOP.
4. In `frontend/rolefit-ui`, run `npm.cmd run build` and then `npm.cmd run dev`.
5. Verify ATS Check, Rankings, History, Delete Record and resume exports are still functional.

## PDF/DOCX round-trip test

Download a PDF and a Word (.docx) file for the SAME fictional or test candidate. You can run this while the frontend is stopped; it only reads local files.

From the **RoleFit-AI root**, run:

```powershell
dotnet run --project .\tests\RoleFit.ExportRoundtripChecks\RoleFit.ExportRoundtripChecks.csproj -- "C:\path\candidate.pdf" "C:\path\candidate.docx"
```

Expected: `PASS .PDF ... characters`, `PASS .DOCX ... characters`, and a final `PASS`.

If PDF fails but DOCX passes, the PDF is not readable by your CURRENT ATS extractor: use the browser's built-in Save as PDF or Word's Save As PDF, then retest. This checker does NOT modify PDF files or add OCR. If both fail, inspect the exported documents and logs.

The check requires the .NET 10 SDK and the PdfPig NuGet dependency already used in the backend. Run it from the RoleFit-AI root, not the backend folder.

**Note:** a readable PDF does not prove every section is correct. Manually verify the candidate name, designation, experience years, 2-project/10-project counts, skills, page breaks and logo. Test multiple professions and narrow/mobile screens. Don't use real candidate data for public deployment demos.

## Rollback

Close the application and restore the two UI files from the newly created `Backups/Phase5-YYYYMMDD-HHMMSS/` folder. The standalone test folder may be deleted separately. No SQL tables, encrypted archives, backend logic or app settings are modified.

## Validation limits

The source was TypeScript-transpiled and the package inspected in the build container. Full Vite and .NET 10 builds must run on your Windows machine. Tests are executed only when you run the commands above; no live Groq or SQL operations are part of this patch.
