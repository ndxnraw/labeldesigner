param([Parameter(Mandatory=$true)][string]$ApplicationDirectory,[switch]$SelfContained)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$application=(Resolve-Path -LiteralPath $ApplicationDirectory).Path
$exe=Join-Path $application 'ND-LabelDesigner.exe'
if(!(Test-Path $exe)){throw 'Published application executable is missing.'}
if($SelfContained){foreach($name in @('hostfxr.dll','hostpolicy.dll','coreclr.dll','System.Private.CoreLib.dll','System.Windows.Forms.dll','PresentationCore.dll','System.Drawing.Common.dll')){if(!(Test-Path (Join-Path $application $name))){throw "Missing self-contained dependency: $name"}}}
$resultRoot=Join-Path $repoRoot ('build/published-test-results/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
New-Item $resultRoot -ItemType Directory -Force|Out-Null
$saved=@{}
foreach($name in @('DOTNET_ROOT','DOTNET_ROOT_X64','DOTNET_MULTILEVEL_LOOKUP')){$saved[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
try{
 if($SelfContained){$env:DOTNET_ROOT=Join-Path $repoRoot 'build/no-installed-runtime';$env:DOTNET_ROOT_X64=$env:DOTNET_ROOT;$env:DOTNET_MULTILEVEL_LOOKUP='0'}
 $process=Start-Process -FilePath $exe -WorkingDirectory $application -ArgumentList @('--self-test',('"'+$resultRoot+'"')) -WindowStyle Hidden -PassThru
 if(!$process.WaitForExit(180000)){$process.Kill();throw 'Published application self-test timed out.'}
 if($process.ExitCode -ne 0 -or !(Test-Path (Join-Path $resultRoot 'passed.txt'))){if(Test-Path (Join-Path $resultRoot 'failure.txt')){Get-Content (Join-Path $resultRoot 'failure.txt')};throw "Published application self-test failed: $resultRoot"}
 Write-Host "PASS published application: $resultRoot"
}finally{foreach($name in $saved.Keys){[Environment]::SetEnvironmentVariable($name,$saved[$name],'Process')}}
