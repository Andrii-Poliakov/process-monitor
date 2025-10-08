@echo off
setlocal

:: Requires admin
net session >nul 2>&1
if %errorlevel% neq 0 (
  echo This script must be run as Administrator.
  exit /b 1
)

set "DEST=C:\ProgramData\ProcessWatcher"
set "EXE=%DEST%\ProcessWatcherWorkerService.exe"
set "SVCNAME=ProcessWatcherWorkerService"
set "SVCDISPLAY=Process Watcher Worker Service"

if not exist "%EXE%" (
  echo Executable not found: "%EXE%"
  exit /b 2
)

:: Check if service already exists
sc query "%SVCNAME%" >nul 2>&1
if %errorlevel% EQU 0 (
  echo Service "%SVCNAME%" already exists. Updating settings...

  :: Stop service if running
  sc stop "%SVCNAME%" >nul 2>&1

  :: Update binPath and set Auto start
  sc config "%SVCNAME%" binPath= "\"%EXE%\"" start= auto >nul
) else (
  echo Creating service "%SVCNAME%"...
  sc create "%SVCNAME%" binPath= "\"%EXE%\"" start= auto DisplayName= "%SVCDISPLAY%"
  if %errorlevel% neq 0 (
    echo Failed to create service.
    exit /b 3
  )
)

:: Start service
echo Starting "%SVCNAME%"...
sc start "%SVCNAME%"
if %errorlevel% neq 0 (
  echo Failed to start service.
  exit /b 4
)

echo Service "%SVCNAME%" is now running.
exit /b 0

pause
