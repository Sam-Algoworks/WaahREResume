WAAH RE RESUME — FINAL PHASE 5 CHECKS

Purpose: clarify the ATS review heading and verify existing export/history functionality before hosting.

1) OPTIONAL ONE-LINE UI LABEL IMPROVEMENT
- Stop the frontend. Back up your working project.
- Extract this folder inside your RoleFit-AI root, next to frontend and backend.
- Run APPLY_GAP_LABEL.bat, type YES if the unique-heading source check passes.
- This modifies ONLY frontend/rolefit-ui/src/components/CandidateReview.tsx.
- It changes "Missing or weak evidence (4)" to something like "1 missing skill · 3 areas to improve".
- The ATS score and the underlying matched/missing arrays remain unchanged.
- If you see SAFE STOP, leave the working file intact; do not override it.

2) VERIFY FRONTEND BUILD
Open a PowerShell terminal in the RoleFit-AI root:
  cd .\frontend\rolefit-ui
  npm.cmd run build

3) PDF/DOCX ROUND TRIP (using the Phase 5 test you previously installed)
- Download PDF and DOCX for the SAME fictional candidate.
- Open PowerShell in the RoleFit-AI project root (NOT the backend folder).
- Run (replace both sample file paths with your actual downloaded files):
  dotnet run --project .\tests\RoleFit.ExportRoundtripChecks\RoleFit.ExportRoundtripChecks.csproj -- "C:\path\resume.pdf" "C:\path\resume.docx"
- Expect PASS for both files. If the test project is missing, re-apply the earlier
  WaahRe-Phase5-UX-Export-QA package or copy its tests folder to your project root.
- The test uses your own backend DocumentReader and does not contact Groq.
- Also manually check candidate name, verified skills, years, projects, and page breaks.

4) SQL HISTORY PERSISTENCE
- With the running app, create one synthetic candidate and save an analysis.
- Check it appears in History.
- Stop and restart the API, reload History and confirm the record remains.
- Delete ONLY your synthetic test record and confirm it stays deleted after restart.
- Keep a private database/App_Data backup. Never put real HR data in a public demo.

5) FINAL DEPLOYMENT GATE
- No crash or unexpected SQL TaskCanceledException during normal navigation.
- Backend build, frontend build, parser checks, scoring checks, export round-trip pass.
- No API keys, actual resumes, local DB credentials, or App_Data in GitHub.
- Public hosting without login must not expose real candidates or private history.

Rollback: replace CandidateReview.tsx from the latest Backups/Phase5GapLabel-... directory.
