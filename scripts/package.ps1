$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function File-Hash($filePath) {
  $inputStream = [IO.File]::OpenRead($filePath)
  $hasher = [Security.Cryptography.SHA256]::Create()
  try { return [BitConverter]::ToString($hasher.ComputeHash($inputStream)).Replace('-','') }
  finally { $inputStream.Dispose(); $hasher.Dispose() }
}
$output = Join-Path $root 'build\packages'
New-Item $output -ItemType Directory -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$path = Join-Path $output 'ND-LabelDesigner-Pre-release-Windows.zip'
$entries = [ordered]@{
  'ND-LabelDesigner.exe' = 'build/app/ND-LabelDesigner.exe'
  'ND-LabelDesigner.exe.config' = 'build/app/ND-LabelDesigner.exe.config'
  'zxing.dll' = 'build/app/zxing.dll'
  'LabelDesigner.Core.dll' = 'build/app/LabelDesigner.Core.dll'
  'USER_GUIDE.txt' = 'docs/USER_GUIDE.txt'
  'FEATURES.md' = 'docs/FEATURES.md'
  'PREVIEW_PERFORMANCE.md' = 'docs/PREVIEW_PERFORMANCE.md'
  'UPDATES.md' = 'docs/UPDATES.md'
  'ACCESSIBILITY.md' = 'docs/ACCESSIBILITY.md'
  'BARCODES_ZEBRA.md' = 'docs/BARCODES_ZEBRA.md'
  'BARCODE_CATALOG.md' = 'docs/BARCODE_CATALOG.md'
  'EDITOR_REFINEMENT.md' = 'docs/EDITOR_REFINEMENT.md'
  'PRINT_HISTORY.md' = 'docs/PRINT_HISTORY.md'
  'LICENSE' = 'LICENSE'
  'licenses/SourceSans3-OFL.txt' = 'assets/fonts/OFL.txt'
  'licenses/ZXing-LICENSE.txt' = 'src/third-party/ZXing/ZXing-LICENSE.txt'
  'licenses/ZXing-NOTICE.txt' = 'src/third-party/ZXing/THIRD-PARTY.txt'
}
. (Join-Path $PSScriptRoot 'update-metadata.ps1')
$manifest=Write-PackageManifest $entries 'framework' $root
$entries['package-manifest.xml']=$manifest.Substring($root.Length+1)
$stream = [IO.File]::Open($path,[IO.FileMode]::Create)
$zip = [IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create)
try {
  foreach ($entry in $entries.GetEnumerator()) {
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $root $entry.Value),$entry.Key) | Out-Null
  }
} finally { $zip.Dispose(); $stream.Dispose() }
$zip = [IO.Compression.ZipFile]::OpenRead($path)
try {
  if ($zip.Entries.Count -ne $entries.Count) { throw 'Unexpected package entry count.' }
  foreach ($entry in $entries.GetEnumerator()) {
    $file = $zip.GetEntry($entry.Key)
    if (!$file -or $file.Length -eq 0) { throw "Missing/empty package entry: $($entry.Key)" }
    $reader = $file.Open()
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $actual = [BitConverter]::ToString($sha.ComputeHash($reader)).Replace('-','') } finally { $reader.Dispose(); $sha.Dispose() }
    if ($actual -ne (File-Hash (Join-Path $root $entry.Value))) { throw "Package mismatch: $($entry.Key)" }
  }
} finally { $zip.Dispose() }
$hash = (File-Hash $path).ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS.txt'),"$hash  $([IO.Path]::GetFileName($path))`n")
Write-Host "Validated package: $path"
& (Join-Path $PSScriptRoot 'write-update-index.ps1')
