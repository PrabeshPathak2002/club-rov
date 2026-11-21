@echo off
setlocal
for /f "delims=" %%p in ('wsl -d Ubuntu-22.04 -- wslpath -a "%~dp0start-prequal-record.sh"') do set "RECORD_SCRIPT=%%p"
if not defined RECORD_SCRIPT exit /b 1
echo Records camera frames with depth and heading for detector tuning. Press Ctrl+C to stop.
echo Examples: --video rtsp://192.168.2.2:8554/video_stream --mavlink udpin:0.0.0.0:14550 --hfov 80
wsl -d Ubuntu-22.04 -- bash "%RECORD_SCRIPT%" %*
set "RECORD_RESULT=%errorlevel%"
pause
exit /b %RECORD_RESULT%
