@echo off
setlocal
for /f "delims=" %%p in ('wsl -d Ubuntu-22.04 -- wslpath -a "%~dp0start-prequal-review.sh"') do set "REVIEW_SCRIPT=%%p"
if not defined REVIEW_SCRIPT exit /b 1
if "%~1"=="" (
  echo Usage: Review-Prequal-Footage.bat ^<recording folder^> [--hue LOW HIGH] [--gate-hue LOW HIGH] ...
  echo Recordings are in \\wsl$\Ubuntu-22.04\root\club-rov-ros2-ws\reports\
  pause
  exit /b 1
)
wsl -d Ubuntu-22.04 -- bash "%REVIEW_SCRIPT%" %*
set "REVIEW_RESULT=%errorlevel%"
pause
exit /b %REVIEW_RESULT%
