# Waah रे Resume — Visual redesign + unified History

## What changed

- Entirely new daylight editorial aesthetic: soft ivory canvas, teal/coral accents, frosted toolbar, animated paper illustration, gentler cards and input controls.
- Five top navigation choices: Home, Analyze, Rankings, Jobs, History. Removed old dark sidebar, "Workspace" breadcrumb, refresh button and RF avatar.
- History is one searchable record list, not a Candidates/Evaluations tab switch. Click a record to see the editable profile and its nested JD evaluations. Existing legacy evaluations are *displayed* together by candidate name + source filename (not merged in storage).
- New PDF/DOCX/TXT uploads are encrypted in local ASP.NET Core Data Protection storage at `backend/RoleFit.Api/App_Data/secure-originals`, outside the web root. New archived originals may be downloaded through development-only localhost endpoints. Existing older uploads were not saved as binaries and cannot be reconstructed.
- Existing Groq parser, project extraction, original Algoworks logo and PDF print template are not replaced.

## Install (current working project)

1. Close the two server windows and Visual Studio debugging.
2. Keep your existing project; place this *entire update folder* inside the project root containing `backend` and `frontend`.
3. Double-click `APPLY_WAAHRE.bat`. It checks the project path, backs up affected source files, then applies the changes.
4. In Visual Studio Community, open `backend/RoleFit.slnx`, select `RoleFit Local`, then **Build -> Rebuild Solution** and start debugging (F5).
5. In VS Code open `frontend/rolefit-ui` and run `npm.cmd run build` followed by `npm.cmd run dev`.
6. Open the `Local:` URL printed by Vite, usually `http://127.0.0.1:5180`.

## Tests to do

- Home: new light theme, top navbar, no old avatar/refresh/breadcrumb.
- Analyze: upload resume, optional JD, one create action; profile editing, Algoworks print, original-document view preserved.
- History: one list; repeated historical evaluations grouped visually. Click record -> details and evaluation breakdown.
- Upload NEW PDF then parse -> History -> click record -> Download original (backend must remain running).
- Rankings and Jobs: functions unchanged, new styling.

## Important privacy limitation

The original resume archive is **development-only** and only returns files for loopback requests when ASPNETCORE_ENVIRONMENT=Development. The project still needs real login, role permissions, retention settings, SQL Server storage and production deployment hardening before handling recruitment data on a public website. Never publish `App_Data` or commit it to Git.

## Verification in this environment

The TSX files passed TypeScript syntax/transpilation diagnostics and the new CSS passed stylesheet parsing. The full React dependency install timed out, and there is no .NET 10 SDK available for a live build here. Windows builds remain mandatory to verify integration. This update does not claim production readiness.
