. (Join-Path $PSScriptRoot 'sdk-common.ps1')
& $dotnet build (Join-Path $repoRoot 'LabelDesigner.sln') -c Release --nologo --disable-build-servers
if($LASTEXITCODE -ne 0){throw 'SDK build failed.'}
