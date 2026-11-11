Waah Re Resume - Frontend TypeScript Build Fix

This fix addresses exactly these errors:
1. TS2307 Cannot find ../utils/resumeExport.
2. TS6133 safeExperience is declared but never used.

INSTALL MANUALLY (recommended):
A. In VS Code, File > Open Folder > YOUR_PROJECT\frontend\rolefit-ui.
B. Expand src. If utils folder is missing, right-click src > New Folder > utils.
C. Copy frontend\rolefit-ui\src\utils\resumeExport.ts from this extracted fix into YOUR_PROJECT\frontend\rolefit-ui\src\utils.
D. Copy frontend\rolefit-ui\src\components\ResumeParserPage.tsx from this extracted fix into YOUR_PROJECT\frontend\rolefit-ui\src\components, replacing the existing one.
E. From rolefit-ui terminal run npm.cmd run build.

IMPORTANT: Keep the original file names exactly, including lowercase 'resumeExport.ts'.
This fix does not change the ASP.NET Core backend, parser, or PDF/Word generation logic.
