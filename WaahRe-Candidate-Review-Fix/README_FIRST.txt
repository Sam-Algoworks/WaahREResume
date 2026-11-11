Waah रे Resume — Candidate Review / Rankings / History Layout Update

BACK UP YOUR WORKING PROJECT FIRST. Only changes the frontend source.

INSTALL:
1. Stop the Vite frontend (Ctrl+C) and Visual Studio backend if desired.
2. Extract this zip in a temporary folder.
3. Copy ALL files inside files/frontend/rolefit-ui/src into your existing
   RoleFit-AI/frontend/rolefit-ui/src, keeping their subfolders.
   When prompted, choose Replace for the same filenames.
4. In VS Code terminal (frontend/rolefit-ui):
      npm.cmd run build
      npm.cmd run dev
5. Hard refresh Edge with Ctrl+Shift+R.

FILES:
- App.tsx — ranking rows open dedicated review, no forced History navigation,
  no default top-ranked detail; append resume uploads one by one.
- components/CandidateReview.tsx — side-by-side original and ATS evidence,
  conditional recommendations, saved archive fallback, editable profile route.
- components/ResumeParserPage.tsx — Analyze & Align goes to side-by-side review.
- components/TalentLibrary.tsx — History drawer rendered via portal so the
  fixed overlay is not trapped inside the animated scrolling page.
- ReviewFlow.css — high-contrast scores, layout, animations, responsive behavior.
- main.tsx — includes new CSS.

Existing backend and data remain untouched. The current evaluation endpoints already
archive newly uploaded original files locally. Earlier records cannot recover
files that were never archived. This change does not claim official ATS scoring.

STEPS TO CHECK:
- Analyze page: upload resume + JD; select Analyze & Align.
  The review page should show the original file on the left and ATS evidence
  on the right. Use Back arrow to return.
- Rankings: upload 2 resumes one after another via Add more resumes, select a JD;
  analyze. Only rows should be shown. Click any row to open that specific file.
- History: click record; drawer must fit within viewport and scroll internally.
- 100/100: recommendations do not render. Lower scores with gaps show dropdown.

A live backend/API run is still required on your Windows setup.
