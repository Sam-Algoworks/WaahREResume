@echo off
setlocal
cd /d "%~dp0"
echo ======================================================
echo        ROLEFIT AI PHASE 4 - UPDATE INSTALLER
echo ======================================================
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0APPLY_PHASE4.ps1"
if errorlevel 1 (
  echo.
  echo UPDATE FAILED. The existing project was left unchanged where possible.
) else (
  echo.
  echo Update completed. Start RoleFit from its ORIGINAL project folder.
)
pause
