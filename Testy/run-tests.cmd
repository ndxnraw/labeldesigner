@echo off
setlocal
cd /d "%~dp0"
rem PrintFixTests sends a virtual job ONLY to Microsoft Print to PDF if installed.
set "compiler=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
"%compiler%" /nologo /target:winexe /out:PrintFixTests.exe /reference:..\ND-LabelDesigner.exe /reference:System.Drawing.dll /reference:System.Windows.Forms.dll PrintFixTests.cs
if errorlevel 1 exit /b 1
"%compiler%" /nologo /target:winexe /out:Revision26Tests.exe /reference:..\ND-LabelDesigner.exe /reference:System.Drawing.dll /reference:System.Windows.Forms.dll Revision26Tests.cs
if errorlevel 1 exit /b 1
copy /y "..\ND-LabelDesigner.exe" "ND-LabelDesigner.exe" >nul
start /wait "" PrintFixTests.exe "%~dp0printing-results"
if errorlevel 1 exit /b 1
start /wait "" Revision26Tests.exe "%~dp0regression-results"
if errorlevel 1 exit /b 1
start /wait "" ND-LabelDesigner.exe --self-test "%~dp0self-results"
if errorlevel 1 exit /b 1
echo All tests passed. No physical printer was used.


