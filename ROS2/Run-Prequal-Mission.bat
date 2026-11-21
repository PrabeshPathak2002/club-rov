@echo off
setlocal
for /f "delims=" %%p in ('wsl -d Ubuntu-22.04 -- wslpath -a "%~dp0start-prequal-mission.sh"') do set "MISSION_SCRIPT=%%p"
if not defined MISSION_SCRIPT exit /b 1
echo Start Simulation\SITL\Start-ClubRovSitl-Prequal.bat first and disable the QGC joystick.
wsl -d Ubuntu-22.04 -- bash "%MISSION_SCRIPT%" %*
set "MISSION_RESULT=%errorlevel%"
pause
exit /b %MISSION_RESULT%
