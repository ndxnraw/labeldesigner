$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME=Join-Path $repoRoot 'build/dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_NOLOGO='1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$localSdk=Join-Path $repoRoot 'build/dotnet/dotnet.exe'
if(Test-Path $localSdk){$dotnet=$localSdk}else{$sdkCommand=Get-Command dotnet -ErrorAction SilentlyContinue;if(!$sdkCommand){throw 'Install the .NET 10 SDK specified by global.json. The compatible legacy build still uses scripts/build.cmd.'};$dotnet=$sdkCommand.Source}
$env:DOTNET_ROOT=Split-Path $dotnet -Parent
$env:DOTNET_ROOT_X64=$env:DOTNET_ROOT
