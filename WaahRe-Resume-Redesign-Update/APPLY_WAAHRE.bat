@echo off
setlocal
cd /d "%~dp0"
echo Waah Re Resume - source code refresh
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0APPLY_WAAHRE.ps1"
if errorlevel 1 (
 echo.
 echo Update did not finish. See the error above. Existing project data was not deliberately deleted.
)
pause
