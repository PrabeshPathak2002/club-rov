@echo off
setlocal
for /f "delims=" %%p in ('wsl -d Ubuntu-22.04 -- wslpath -a "%~dp0start-ros2.sh"') do set "ROS_SCRIPT=%%p"
if not defined ROS_SCRIPT exit /b 1
wsl -d Ubuntu-22.04 -- bash "%ROS_SCRIPT%" external_navigation:=true
if errorlevel 1 pause
