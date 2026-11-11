@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0APPLY_PHASE3.ps1"
set "RESULT=%ERRORLEVEL%"
echo.
if not "%RESULT%"=="0" echo INSTALL DID NOT COMPLETE. Existing files were preserved or backed up.
pause
exit /b %RESULT%
