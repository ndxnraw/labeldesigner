param([switch]$SkipPublish)
. (Join-Path $PSScriptRoot 'sdk-common.ps1')
$published=Join-Path $repoRoot 'build/publish/net10-win-x64'
if(!$SkipPublish){
 & $dotnet publish (Join-Path $repoRoot 'src/LabelDesigner/LabelDesigner.csproj') -c Release -f net10.0-windows -p:TargetFrameworks=net10.0-windows -r win-x64 --self-contained true -p:PublishTrimmed=false -p:PublishSingleFile=false --disable-build-servers --nologo -o $published
 if($LASTEXITCODE -ne 0){throw 'Modern publish failed.'}
}
foreach($name in @('ND-LabelDesigner.exe','ND-LabelDesigner.dll','LabelDesigner.Core.dll','zxing.dll','ND-LabelDesigner.runtimeconfig.json','coreclr.dll','System.Private.CoreLib.dll')){if(!(Test-Path (Join-Path $published $name))){throw "Incomplete self-contained output: $name"}}
$version=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $published 'ND-LabelDesigner.exe'))
if($version.ProductVersion -cne 'Pre-release'){throw 'Incorrect modern product version.'}
& (Join-Path $PSScriptRoot 'test-published.ps1') -ApplicationDirectory $published -SelfContained
$output=Join-Path $repoRoot 'build/packages';New-Item $output -ItemType Directory -Force|Out-Null
# Build an exact file manifest; stale files in a previous staging directory are
# never included. Runtime files come exclusively from dotnet publish.
$entries=[ordered]@{}
foreach($file in Get-ChildItem $published -File -Recurse){$relative=$file.FullName.Substring($published.Length+1).Replace('\','/');$entries[$relative]=$file.FullName}
$entries['USER_GUIDE.txt']=Join-Path $repoRoot 'docs/USER_GUIDE.txt'
$entries['FEATURES.md']=Join-Path $repoRoot 'docs/FEATURES.md'
$entries['PREVIEW_PERFORMANCE.md']=Join-Path $repoRoot 'docs/PREVIEW_PERFORMANCE.md'
$entries['UPDATES.md']=Join-Path $repoRoot 'docs/UPDATES.md'
$entries['ACCESSIBILITY.md']=Join-Path $repoRoot 'docs/ACCESSIBILITY.md'
$entries['BARCODES_ZEBRA.md']=Join-Path $repoRoot 'docs/BARCODES_ZEBRA.md'
$entries['BARCODE_CATALOG.md']=Join-Path $repoRoot 'docs/BARCODE_CATALOG.md'
$entries['EDITOR_REFINEMENT.md']=Join-Path $repoRoot 'docs/EDITOR_REFINEMENT.md'
$entries['LICENSE']=Join-Path $repoRoot 'LICENSE'
$entries['licenses/SourceSans3-OFL.txt']=Join-Path $repoRoot 'assets/fonts/OFL.txt'
$entries['licenses/ZXing-LICENSE.txt']=Join-Path $repoRoot 'src/third-party/ZXing/ZXing-LICENSE.txt'
$entries['licenses/ZXing-NOTICE.txt']=Join-Path $repoRoot 'src/third-party/ZXing/THIRD-PARTY.txt'
$runtimeConfig=Get-Content (Join-Path $published 'ND-LabelDesigner.runtimeconfig.json') -Raw|ConvertFrom-Json
$coreVersion=($runtimeConfig.runtimeOptions.includedFrameworks|Where-Object name -EQ 'Microsoft.NETCore.App').version
$desktopVersion=($runtimeConfig.runtimeOptions.includedFrameworks|Where-Object name -EQ 'Microsoft.WindowsDesktop.App').version
if(!$coreVersion -or !$desktopVersion){throw 'Published runtime versions are missing.'}
$restore=Get-Content (Join-Path $repoRoot 'build/sdk-obj/LabelDesigner/project.assets.json') -Raw|ConvertFrom-Json
function Runtime-Notice($package,$version,$name){
 foreach($folder in $restore.packageFolders.PSObject.Properties.Name){$file=Join-Path $folder "$package/$version/$name";if(Test-Path -LiteralPath $file){return $file}}
 throw "Missing runtime notice: $package/$version/$name"
}
$entries['licenses/DOTNET-LICENSE.txt']=Runtime-Notice 'microsoft.netcore.app.runtime.win-x64' $coreVersion 'LICENSE.TXT'
$entries['licenses/DOTNET-NOTICES.txt']=Runtime-Notice 'microsoft.netcore.app.runtime.win-x64' $coreVersion 'THIRD-PARTY-NOTICES.TXT'
$entries['licenses/WINDOWSDESKTOP-LICENSE.txt']=Runtime-Notice 'microsoft.windowsdesktop.app.runtime.win-x64' $desktopVersion 'LICENSE'
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
. (Join-Path $PSScriptRoot 'update-metadata.ps1')
$entries['package-manifest.xml']=Write-PackageManifest $entries 'net10-x64' $repoRoot
$path=Join-Path $output 'ND-LabelDesigner-Pre-release-Windows-NET10-x64.zip'
$stream=[IO.File]::Open($path,[IO.FileMode]::Create);$zip=New-Object IO.Compression.ZipArchive($stream,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($entry in $entries.GetEnumerator()){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$entry.Value,$entry.Key)|Out-Null}}finally{$zip.Dispose();$stream.Dispose()}
$zip=[IO.Compression.ZipFile]::OpenRead($path)
try{
 if($zip.Entries.Count -ne $entries.Count){throw 'Unexpected modern ZIP entry count.'}
 foreach($entry in $entries.GetEnumerator()){
  $item=$zip.GetEntry($entry.Key);if(!$item -or $item.Length -eq 0){throw "Missing modern ZIP entry: $($entry.Key)"}
  $input=$item.Open();$sha=[Security.Cryptography.SHA256]::Create()
  try{$actual=[BitConverter]::ToString($sha.ComputeHash($input)).Replace('-','')}finally{$sha.Dispose();$input.Dispose()}
  if($actual -cne (Get-FileHash $entry.Value -Algorithm SHA256).Hash){throw "Modern ZIP content mismatch: $($entry.Key)"}
 }
}finally{$zip.Dispose()}
$hash=(Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS-NET10.txt'),$hash+'  '+[IO.Path]::GetFileName($path)+"`n")
Write-Host "Validated self-contained modern package: $path"
& (Join-Path $PSScriptRoot 'write-update-index.ps1')
