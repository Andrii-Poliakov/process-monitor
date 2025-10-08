@echo off
setlocal enabledelayedexpansion

:: Requires admin
net session >nul 2>&1
if %errorlevel% neq 0 (
  echo This script must be run as Administrator.
  exit /b 1
)

:: Source = current working directory (where bat is run)
set "SRC=%cd%"
:: Destination folder
set "DEST=C:\ProgramData\ProcessWatcher"

echo Deploying files from:
echo    "%SRC%"
echo to "%DEST%"
if not exist "%DEST%" mkdir "%DEST%"

:: Copy all files and subfolders
:: /E = copy subdirectories, including empty ones
:: /R:1 = retry once if error
:: /W:1 = wait 1 second between retries
:: /NFL /NDL /NJH /NJS /NP = cleaner output
robocopy "%SRC%" "%DEST%" /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP

set "RC=%ERRORLEVEL%"
if %RC% GEQ 8 (
  echo ROBOCOPY failed with code %RC%.
  exit /b %RC%
)

echo Deploy completed successfully.
exit /b 0

pause