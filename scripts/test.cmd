@echo off
setlocal
call "%~dp0build.cmd"
if errorlevel 1 exit /b 1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0test.ps1"
exit /b %errorlevel%
