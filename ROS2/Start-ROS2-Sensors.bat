@echo off
setlocal
for /f "delims=" %%P in ('wsl -d Ubuntu-22.04 -- wslpath -a "%~dp0start-ros2.sh"') do set "ROS_SCRIPT=%%P"
if not defined ROS_SCRIPT exit /b 1
wsl -d Ubuntu-22.04 -- bash "%ROS_SCRIPT%" %*
pause
