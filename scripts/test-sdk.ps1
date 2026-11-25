param([string[]]$Suites)
. (Join-Path $PSScriptRoot 'sdk-common.ps1')
$framework='net10.0-windows'
$names=Get-Content (Join-Path $PSScriptRoot 'test-suites.json') -Raw|ConvertFrom-Json
if($Suites){foreach($suite in $Suites){if($suite -notin $names){throw "Unknown test suite: $suite"}}}
$resultRoot=Join-Path $repoRoot ('build/sdk-test-results/'+$Framework+'/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item $resultRoot -ItemType Directory -Force|Out-Null
Push-Location $repoRoot
try{
 foreach($name in $names){
  if($Suites -and $name -notin $Suites){continue}
  $buildLog=Join-Path $resultRoot ($name+'-build.log')
  & $dotnet build tests/RegressionTests.csproj -c Release "-p:SuiteName=$name" --disable-build-servers --nologo *> $buildLog
  if($LASTEXITCODE -ne 0){Get-Content $buildLog -Tail 30;throw "Test compilation failed: $name"}
  $testExe=Join-Path $repoRoot "build/sdk-bin/RegressionTests/$name/Release/$Framework/$name.exe"
  $caseRoot=Join-Path $resultRoot $name;New-Item $caseRoot -ItemType Directory -Force|Out-Null
  if($name -eq 'ImportTests'){Copy-Item tests/fixtures/Z64.txt,tests/fixtures/B64.txt $caseRoot}
  $process=Start-Process -FilePath $testExe -ArgumentList ('"'+$caseRoot+'"') -WindowStyle Hidden -PassThru
  if(!$process.WaitForExit(600000)){$process.Kill();throw "Test timeout: $name; see $caseRoot"}
  if($process.ExitCode -ne 0 -or !(Test-Path (Join-Path $caseRoot 'passed.txt'))){if(Test-Path (Join-Path $caseRoot 'failed.txt')){Get-Content (Join-Path $caseRoot 'failed.txt')};throw "FAILED: $name ($Framework); see $caseRoot"}
  Write-Host "PASS $name ($Framework)"
 }
 Write-Host "Results: $resultRoot"
}finally{Pop-Location}
