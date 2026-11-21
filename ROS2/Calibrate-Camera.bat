@echo off
setlocal
for /f "delims=" %%p in ('wsl -d Ubuntu-22.04 -- wslpath -a "%~dp0start-calibrate-camera.sh"') do set "CALIBRATE_SCRIPT=%%p"
if not defined CALIBRATE_SCRIPT exit /b 1
echo Calibrates from ROS camera topics, --video source, or --video image-folder. No vehicle commands.
wsl -d Ubuntu-22.04 -- bash "%CALIBRATE_SCRIPT%" %*
set "CALIBRATE_RESULT=%errorlevel%"
pause
exit /b %CALIBRATE_RESULT%
