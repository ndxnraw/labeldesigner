@echo off
setlocal
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /win32manifest:app.manifest /win32icon:app.ico /out:"..\ND-LabelDesigner.exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll Engine.cs App.cs EditorPreview.cs MoreBarcodes.cs BarcodeTables.cs AssemblyInfo.cs VectorArt.cs ModernUi.cs UiControls.cs ZplImport.cs ImportUi.cs Gs1.cs Gs1Ui.cs FastPreview.cs Symbols.cs DepositArt.cs Localization.cs StartupSettings.cs StartupLayout.cs Printing.cs PrintPanel.cs PaperOptions.cs BrandLogo.cs WordParts.cs /resource:translations.tsv,translations.tsv
if errorlevel 1 exit /b 1
echo ND-LabelDesigner.exe vytvoreny.

