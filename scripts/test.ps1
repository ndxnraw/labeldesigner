param([string[]]$Suites)
$ErrorActionPreference = 'Stop'
$packageRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testBin = Join-Path $packageRoot 'build\tests'
$resultRoot = Join-Path $packageRoot ('build\test-results\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item $testBin,$resultRoot -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $packageRoot 'build\app\ND-LabelDesigner.exe') $testBin -Force
Copy-Item (Join-Path $packageRoot 'build\app\zxing.dll') $testBin -Force
Copy-Item (Join-Path $packageRoot 'build\app\LabelDesigner.Core.dll') $testBin -Force
$names=Get-Content (Join-Path $PSScriptRoot 'test-suites.json') -Raw|ConvertFrom-Json
if($Suites){foreach($suite in $Suites){if($suite -notin $names){throw "Unknown test suite: $suite"}}}
Push-Location $packageRoot
try {
  foreach ($name in $names) {
    if ($Suites -and $name -notin $Suites) { continue }
    $testExe = Join-Path $testBin ($name + '.exe')
    & $compiler /nologo /target:winexe "/out:$testExe" "/reference:$testBin\ND-LabelDesigner.exe" "/reference:$testBin\zxing.dll" "/reference:$testBin\LabelDesigner.Core.dll" /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Get-ChildItem (Join-Path $packageRoot 'tests') -Recurse -Filter ($name + '.cs')).FullName (Join-Path $packageRoot 'tests/tools/TestPaths.cs')
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $name" }
    Copy-Item (Join-Path $packageRoot 'build\app\ND-LabelDesigner.exe.config') ($testExe + '.config') -Force
    $caseRoot = Join-Path $resultRoot $name
    New-Item $caseRoot -ItemType Directory -Force | Out-Null
    if ($name -eq 'ImportTests') { Copy-Item (Join-Path $packageRoot 'tests\fixtures\Z64.txt'),(Join-Path $packageRoot 'tests\fixtures\B64.txt') $caseRoot }
    $process = Start-Process -FilePath $testExe -ArgumentList ('"' + $caseRoot + '"') -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0 -or !(Test-Path (Join-Path $caseRoot 'passed.txt'))) { throw "FAILED: $name; see $caseRoot" }
    Write-Host "PASS $name"
  }
  Write-Host "Results: $resultRoot"
} finally { Pop-Location }
