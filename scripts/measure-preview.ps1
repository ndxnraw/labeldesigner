param([ValidateSet('compatible','modern')][string]$Runtime='compatible',[Parameter(Mandatory=$true)][string]$Results,[switch]$SkipBuild,[string]$Executable)
$ErrorActionPreference='Stop';$repoRoot=Split-Path $PSScriptRoot -Parent
$resultRoot=[IO.Path]::GetFullPath((Join-Path $repoRoot $Results));New-Item $resultRoot -ItemType Directory -Force|Out-Null
if($Executable){
 $exe=(Resolve-Path -LiteralPath $Executable).Path
 if($Runtime -eq 'modern'){. (Join-Path $PSScriptRoot 'sdk-common.ps1')}
}elseif($Runtime -eq 'compatible'){
 if(!$SkipBuild){& (Join-Path $PSScriptRoot 'build.ps1')}
 $bin=Join-Path $repoRoot 'build/preview-benchmark/compatible';New-Item $bin -ItemType Directory -Force|Out-Null
 Copy-Item (Join-Path $repoRoot 'build/app/*') $bin -Force
 $compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe';$exe=Join-Path $bin 'PrintPreviewBenchmark.exe'
 & $compiler /nologo /target:winexe /main:PrintPreviewBenchmark "/out:$exe" "/reference:$bin/ND-LabelDesigner.exe" "/reference:$bin/LabelDesigner.Core.dll" "/reference:$bin/zxing.dll" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $repoRoot 'tests/tools/PrintPreviewBenchmark.cs')
 if($LASTEXITCODE -ne 0){throw 'Benchmark compilation failed'}
 Copy-Item (Join-Path $bin 'ND-LabelDesigner.exe.config') ($exe+'.config') -Force
}else{
 . (Join-Path $PSScriptRoot 'sdk-common.ps1')
 & $dotnet build (Join-Path $repoRoot 'tests/tools/PreviewBenchmark.csproj') -c Release -p:TargetFrameworks=net10.0-windows --disable-build-servers --nologo
 if($LASTEXITCODE -ne 0){throw 'Benchmark compilation failed'}
 $exe=Join-Path $repoRoot 'build/sdk-bin/PreviewBenchmark/Release/net10.0-windows/PrintPreviewBenchmark.exe'
}
$application=Join-Path (Split-Path $exe -Parent) 'ND-LabelDesigner.dll';if(!(Test-Path $application)){$application=Join-Path (Split-Path $exe -Parent) 'ND-LabelDesigner.exe'}
@{Runtime=$Runtime;StartedUtc=[DateTime]::UtcNow.ToString('o');ApplicationSHA256=(Get-FileHash $application -Algorithm SHA256).Hash;CoreSHA256=(Get-FileHash (Join-Path (Split-Path $exe -Parent) 'LabelDesigner.Core.dll') -Algorithm SHA256).Hash;BenchmarkSourceSHA256=(Get-FileHash (Join-Path $repoRoot 'tests/tools/PrintPreviewBenchmark.cs') -Algorithm SHA256).Hash}|ConvertTo-Json|Set-Content (Join-Path $resultRoot 'benchmark-run.json') -Encoding UTF8
$process=Start-Process -FilePath $exe -ArgumentList ('"'+$resultRoot+'"') -WorkingDirectory $repoRoot -WindowStyle Hidden -PassThru
if(!$process.WaitForExit(600000)){$process.Kill();throw 'Benchmark timeout'}
if($process.ExitCode -ne 0 -or !(Test-Path (Join-Path $resultRoot 'passed.txt'))){if(Test-Path (Join-Path $resultRoot 'failed.txt')){Get-Content (Join-Path $resultRoot 'failed.txt')};throw 'Preview benchmark failed'}
Write-Host "PASS preview benchmark: $Runtime $resultRoot"
