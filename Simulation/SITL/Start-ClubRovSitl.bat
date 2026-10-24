@echo off
setlocal
title Club ROV Simulator
set "ROV_PWSH="
where pwsh.exe >nul 2>&1
if not errorlevel 1 set "ROV_PWSH=pwsh.exe"
if not defined ROV_PWSH if exist "%ProgramFiles%\PowerShell\7\pwsh.exe" set "ROV_PWSH=%ProgramFiles%\PowerShell\7\pwsh.exe"
if not defined ROV_PWSH if exist "%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe" set "ROV_PWSH=%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe"
if not defined ROV_PWSH goto missing
echo Open PoolTraining in Unity and enter Play Mode before continuing.
echo This window starts the simulator and connects Unity automatically.
echo.
"%ROV_PWSH%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-ClubRovSitl.ps1" -ConnectUnityEditor
set "ROV_RESULT=%ERRORLEVEL%"
if not "%ROV_RESULT%"=="0" pause
exit /b %ROV_RESULT%

:missing
echo PowerShell 7 was not found. Install PowerShell 7, then run this file again.
pause
exit /b 1
