$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $compiler)) { throw 'Install .NET Framework 4.8 or 4.8.1 on 64-bit Windows.' }
$source = Join-Path $root 'src\LabelDesigner'
$assets = Join-Path $root 'assets'
$output = Join-Path $root 'build\app'
New-Item $output -ItemType Directory -Force | Out-Null
$arguments = @('/nologo','/target:winexe','/optimize+',"/win32manifest:$source\app.manifest","/win32icon:$assets\app.ico","/out:$output\ND-LabelDesigner.exe",'/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll',"/resource:$assets\app.ico,AppLogo.ico","/resource:$source\translations.tsv,translations.tsv")
foreach ($font in @('SourceSans3-Regular.ttf','SourceSans3-Bold.ttf','OFL.txt')) { $arguments += "/resource:$assets\fonts\$font,Fonts.$font" }
$arguments += (Get-ChildItem $source -Filter '*.cs' | Sort-Object Name).FullName
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
Copy-Item (Join-Path $source 'app.config') (Join-Path $output 'ND-LabelDesigner.exe.config') -Force
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $output 'ND-LabelDesigner.exe'))
if ($version.ProductVersion -ne 'prelease') { throw 'Incorrect product version.' }
Write-Host "Built ND LabelDesigner - prelease: $output"
