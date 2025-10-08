@echo off
setlocal

:: --- Elevation check (must run as Administrator) ---
net session >nul 2>&1
if %errorlevel% neq 0 (
  echo This script must be run as Administrator.
  exit /b 1
)

:: --- Config ---
set "SVCNAME=ProcessWatcherWorkerService"
set "DEST=C:\ProgramData\ProcessWatcher"

echo.
echo ===== Uninstalling %SVCNAME% =====

:: 1) Stop service (ignore errors if not present or already stopped)
sc stop "%SVCNAME%" >nul 2>&1

:: 2) Uninstall service (ignore errors if already deleted)
sc delete "%SVCNAME%" >nul 2>&1

:: 3) Extra safety: kill process if still running (ignore errors)
taskkill /f /im "ProcessWatcherWorkerService.exe" >nul 2>&1

:: 4) Delete ONLY contents of DEST (keep the DEST folder itself)
if exist "%DEST%" (
  echo Cleaning contents of "%DEST%"...
  del /f /s /q "%DEST%\*" >nul 2>&1
  for /d %%D in ("%DEST%\*") do rd /s /q "%%D"
  echo Finished cleaning "%DEST%".
) else (
  echo DEST "%DEST%" not found. Skipping content cleanup.
)

echo.
echo ===== Done =====
exit /b 0

pause