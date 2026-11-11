RoleFit AI - Phase 3 UPDATE (for your already-working project)

1. CLOSE the running RoleFit API and Frontend PowerShell windows.
2. Extract the whole RoleFit-AI-Phase3-Update folder INSIDE your existing RoleFit-AI folder.
3. Open RoleFit-AI\RoleFit-AI-Phase3-Update\ and double-click APPLY_UPDATE.bat.
4. Confirm 'UPDATE APPLIED SUCCESSFULLY'.
5. Open your original RoleFit-AI folder and double-click START_ROLEFIT.bat.
6. Go to the Local URL displayed by Vite (normally http://127.0.0.1:5180).
7. Click 'Resume Parser' in the sidebar, upload samples\candidate-a.txt and parse.
8. Click any field and type directly. Save profile draft when finished.
9. Check the JD upload buttons inside Resume Parser and Resume Analyzer.

Your existing App_Data JSON, previous analysis results, and node_modules are NOT overwritten.
The updater backs up the four existing edited source files into _phase3_backup.

IMPORTANT:
- This stage uses rule-based parsing; Groq AI and OCR are upcoming phases.
- Do not use real confidential candidate documents until authentication/security are implemented.
- The Algoworks styled wordmark is temporary until an authorized official logo file is provided.
