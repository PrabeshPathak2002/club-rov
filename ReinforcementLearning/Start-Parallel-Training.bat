@echo off
setlocal
for /f "delims=" %%P in ('wsl -d Ubuntu-22.04 -- wslpath -a "%~dp0start-parallel-training.sh"') do set "TRAINING_SCRIPT=%%P"
if not defined TRAINING_SCRIPT exit /b 1
wsl -d Ubuntu-22.04 -- bash "%TRAINING_SCRIPT%" %*
pause
