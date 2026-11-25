@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-sdk.ps1"
exit /b %errorlevel%
