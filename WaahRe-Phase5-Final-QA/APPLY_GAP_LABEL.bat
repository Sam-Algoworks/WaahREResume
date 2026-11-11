@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0APPLY_GAP_LABEL.ps1"
if errorlevel 1 (
  echo.
  echo Update was NOT applied. Read the SAFE STOP message; do not force changes.
  pause
  exit /b 1
)
echo.
pause
