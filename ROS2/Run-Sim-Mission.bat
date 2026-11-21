@echo off
setlocal
for /f "delims=" %%p in ('wsl -d Ubuntu-22.04 -- wslpath -a "%~dp0start-sim-mission.sh"') do set "MISSION_SCRIPT=%%p"
if not defined MISSION_SCRIPT exit /b 1
echo Disable the QGC joystick and use the Empty Pool map with DVL SITL running.
wsl -d Ubuntu-22.04 -- bash "%MISSION_SCRIPT%" %*
set "MISSION_RESULT=%errorlevel%"
pause
exit /b %MISSION_RESULT%
