# Smoke tests

After starting both terminals:

1. Run `powershell.exe -ExecutionPolicy Bypass -File .\scripts\check-api.ps1` from the project root.
2. Open `http://127.0.0.1:5180` (or the alternate port printed by Vite).
3. The status badge should say API Connected.
4. Save a JD from `samples/backend-developer-jd.txt`.
5. Upload `samples/candidate-a.txt` in Single Analysis.
6. Upload candidate-a.txt and candidate-b.txt in Bulk Ranking.
7. Verify that results are saved in Analysis History and that the overview stats update.
8. Stop the two server windows and restart; saved records should still appear.

These are manual smoke checks, not a substitute for automated backend, frontend and security testing.
