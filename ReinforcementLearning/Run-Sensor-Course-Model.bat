@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-Sensor-Course-Model.ps1" %*
if errorlevel 1 pause
