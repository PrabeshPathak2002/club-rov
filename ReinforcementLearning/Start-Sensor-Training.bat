@echo off
setlocal
for /f "delims=" %%P in ('wsl -d Ubuntu-22.04 -- wslpath -a "%~dp0start-sensor-training.sh"') do set "SENSOR_SCRIPT=%%P"
if not defined SENSOR_SCRIPT exit /b 1
wsl -d Ubuntu-22.04 -- bash "%SENSOR_SCRIPT%" %*
pause
