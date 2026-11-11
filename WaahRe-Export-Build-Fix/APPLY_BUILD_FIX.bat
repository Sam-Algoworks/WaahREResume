@echo off
setlocal
cd /d "%~dp0"
set "ROOT=%CD%"
if exist "%ROOT%\frontend\rolefit-ui\package.json" goto found
for %%I in ("%ROOT%\..") do set "ROOT=%%~fI"
if not exist "%ROOT%\frontend\rolefit-ui\package.json" (
  echo ERROR: Move this entire WaahRe-Export-Build-Fix folder INSIDE your existing RoleFit-AI project folder first.
  echo Expected: RoleFit-AI\WaahRe-Export-Build-Fix\APPLY_BUILD_FIX.bat
  pause
  exit /b 1
)
:found
set "SRC=%~dp0frontend\rolefit-ui\src"
set "DEST=%ROOT%\frontend\rolefit-ui\src"
if not exist "%SRC%\utils\resumeExport.ts" (
 echo ERROR: Fix file missing. Extract full ZIP before running.
 pause
 exit /b 1
)
if not exist "%DEST%\components\ResumeParserPage.tsx" (
 echo ERROR: Original frontend component not found.
 pause
 exit /b 1
)
set "BACKUP=%ROOT%\WaahRe-Export-Build-Fix-Backup"
if not exist "%BACKUP%" mkdir "%BACKUP%"
copy /y "%DEST%\components\ResumeParserPage.tsx" "%BACKUP%\ResumeParserPage.tsx" >nul
if exist "%DEST%\utils\resumeExport.ts" copy /y "%DEST%\utils\resumeExport.ts" "%BACKUP%\resumeExport.ts" >nul
if not exist "%DEST%\utils" mkdir "%DEST%\utils"
copy /y "%SRC%\utils\resumeExport.ts" "%DEST%\utils\resumeExport.ts" >nul
copy /y "%SRC%\components\ResumeParserPage.tsx" "%DEST%\components\ResumeParserPage.tsx" >nul
echo BUILD FIX APPLIED. Restart Vite and run: npm.cmd run build
pause
