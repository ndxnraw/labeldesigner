$ErrorActionPreference = 'Stop'
$packageRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testBin = Join-Path $PSScriptRoot 'Bin'
$resultRoot = Join-Path $PSScriptRoot ('Results\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item $testBin,$resultRoot -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $packageRoot 'ND-LabelDesigner.exe') $testBin -Force
Push-Location $packageRoot
try {
  foreach ($name in @('Revision305Tests','Revision304Tests','Revision303Tests','Revision302Tests','Revision301Tests','Revision12Tests','Revision22Tests','SymbolTests','Revision25Tests','Revision26Tests','ImportTests','OutputTests','V3Tests','UiPerf')) {
    $testExe = Join-Path $testBin ($name + '.exe')
    & $compiler /nologo /target:winexe "/out:$testExe" "/reference:$testBin\ND-LabelDesigner.exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot ($name + '.cs'))
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $name" }
    Copy-Item (Join-Path $packageRoot 'ND-LabelDesigner.exe.config') ($testExe + '.config') -Force
    $caseRoot = Join-Path $resultRoot $name
    New-Item $caseRoot -ItemType Directory -Force | Out-Null
    if ($name -eq 'ImportTests') { Copy-Item (Join-Path $PSScriptRoot 'Fixtures\Z64.txt'),(Join-Path $PSScriptRoot 'Fixtures\B64.txt') $caseRoot }
    $process = Start-Process -FilePath $testExe -ArgumentList ('"' + $caseRoot + '"') -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0 -or !(Test-Path (Join-Path $caseRoot 'passed.txt'))) { throw "FAILED: $name; see $caseRoot" }
    Write-Host "PASS $name"
  }
  Write-Host "Results: $resultRoot"
} finally { Pop-Location }
