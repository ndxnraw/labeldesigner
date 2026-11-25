. (Join-Path $PSScriptRoot 'sdk-common.ps1')
$project = Join-Path $repoRoot 'tests/tools/UpdatePackageCheck.csproj'
& $dotnet build $project -c Release --disable-build-servers --nologo
if ($LASTEXITCODE -ne 0) { throw 'Update package checker build failed.' }
$checker = Join-Path $repoRoot 'build/sdk-bin/UpdatePackageCheck/Release/net10.0-windows/UpdatePackageCheck.exe'
$packages = Join-Path $repoRoot 'build/packages'
$results = Join-Path $repoRoot ('build/update-package-results/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$installed = Join-Path $repoRoot 'build/u10'
# The verifier appends a 64-character package ID. Keep the runtime path short.
& $checker $results (Join-Path $packages 'ND-LabelDesigner-Pre-release-Windows-NET10-x64.zip') `
    (Join-Path $packages 'SHA256SUMS-NET10.txt') (Join-Path $packages 'UPDATE-INDEX.xml') $installed
if ($LASTEXITCODE -ne 0) { throw 'Managed update package validation failed.' }
Write-Host "Update package results: $results"
