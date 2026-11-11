@echo off
setlocal
set "ROOT=%~dp0.."

if not exist "%ROOT%\backend\RoleFit.Api\Program.cs" (
  echo ERROR: The update folder must be INSIDE your existing RoleFit-AI project.
  echo.
  echo Example: RoleFit-AI\RoleFit-AI-Phase3-Update\APPLY_UPDATE.bat
  echo Your original backend and frontend must be next to the update folder.
  pause
  exit /b 1
)
if not exist "%ROOT%\frontend\rolefit-ui\src\App.tsx" (
  echo ERROR: Could not find the existing React project.
  pause
  exit /b 1
)

echo =======================================================
echo     RoleFit AI - Phase 3 Update
echo =======================================================
echo.
echo Close the two running API / Frontend PowerShell servers first.
echo This update preserves existing App_Data, candidates, and npm packages.
echo.

set "BACKUP=%ROOT%\_phase3_backup"
if not exist "%BACKUP%" mkdir "%BACKUP%"
copy /Y "%ROOT%\backend\RoleFit.Api\Program.cs" "%BACKUP%\Program.cs" >nul
copy /Y "%ROOT%\frontend\rolefit-ui\src\App.tsx" "%BACKUP%\App.tsx" >nul
copy /Y "%ROOT%\frontend\rolefit-ui\src\api.ts" "%BACKUP%\api.ts" >nul
copy /Y "%ROOT%\frontend\rolefit-ui\src\index.css" "%BACKUP%\index.css" >nul

xcopy "%~dp0files\backend\*" "%ROOT%\backend\" /E /I /Y >nul
if errorlevel 1 goto failed
xcopy "%~dp0files\frontend\*" "%ROOT%\frontend\" /E /I /Y >nul
if errorlevel 1 goto failed
copy /Y "%~dp0files\README.md" "%ROOT%\README.md" >nul
if errorlevel 1 goto failed

echo UPDATE APPLIED SUCCESSFULLY.
echo.
echo Now open your original RoleFit-AI folder and run START_ROLEFIT.bat.
echo Then open the frontend URL printed by Vite, and click Resume Parser.
echo.
pause
exit /b 0

:failed
echo.
echo ERROR: One or more files could not be copied.
echo Please check folder permissions or antivirus and retry.
pause
exit /b 1
