param([Parameter(Mandatory=$true)][string]$Results,[switch]$SkipBuild,[string]$Executable)
. (Join-Path $PSScriptRoot 'sdk-common.ps1')
$resultRoot=[IO.Path]::GetFullPath((Join-Path $repoRoot $Results))
New-Item $resultRoot -ItemType Directory -Force|Out-Null
if($Executable){
 $exe=(Resolve-Path -LiteralPath $Executable).Path
}else{
 if(!$SkipBuild){
  & $dotnet build (Join-Path $repoRoot 'tests/tools/PreviewBenchmark.csproj') -c Release --disable-build-servers --nologo
  if($LASTEXITCODE -ne 0){throw 'Benchmark compilation failed'}
 }
 $exe=Join-Path $repoRoot 'build/sdk-bin/PreviewBenchmark/Release/net10.0-windows/PrintPreviewBenchmark.exe'
}
$application=Join-Path (Split-Path $exe -Parent) 'ND-LabelDesigner.dll'
@{Runtime='net10.0-windows';StartedUtc=[DateTime]::UtcNow.ToString('o');ApplicationSHA256=(Get-FileHash $application -Algorithm SHA256).Hash;CoreSHA256=(Get-FileHash (Join-Path (Split-Path $exe -Parent) 'LabelDesigner.Core.dll') -Algorithm SHA256).Hash;BenchmarkSourceSHA256=(Get-FileHash (Join-Path $repoRoot 'tests/tools/PrintPreviewBenchmark.cs') -Algorithm SHA256).Hash}|ConvertTo-Json|Set-Content (Join-Path $resultRoot 'benchmark-run.json') -Encoding UTF8
$process=Start-Process -FilePath $exe -ArgumentList ('"'+$resultRoot+'"') -WorkingDirectory $repoRoot -WindowStyle Hidden -PassThru
if(!$process.WaitForExit(600000)){$process.Kill();throw 'Benchmark timeout'}
if($process.ExitCode -ne 0 -or !(Test-Path (Join-Path $resultRoot 'passed.txt'))){if(Test-Path (Join-Path $resultRoot 'failed.txt')){Get-Content (Join-Path $resultRoot 'failed.txt')};throw 'Preview benchmark failed'}
Write-Host "PASS preview benchmark: .NET 10 $resultRoot"
