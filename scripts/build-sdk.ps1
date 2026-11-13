param([ValidateSet('all','net48','net10.0-windows')][string]$Framework='all')
. (Join-Path $PSScriptRoot 'sdk-common.ps1')
$arguments=@('build',(Join-Path $repoRoot 'LabelDesigner.sln'),'-c','Release','--nologo','--disable-build-servers')
if($Framework -ne 'all'){$arguments+=@('-p:TargetFrameworks='+$Framework)}
& $dotnet @arguments
if($LASTEXITCODE -ne 0){throw 'SDK build failed.'}
