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
$path = Join-Path $output 'ND-LabelDesigner-prelease-Windows.zip'
$entries = [ordered]@{
  'ND-LabelDesigner.exe' = 'build/app/ND-LabelDesigner.exe'
  'ND-LabelDesigner.exe.config' = 'build/app/ND-LabelDesigner.exe.config'
  'USER_GUIDE.txt' = 'docs/USER_GUIDE.txt'
  'LICENSE' = 'LICENSE'
  'licenses/SourceSans3-OFL.txt' = 'assets/fonts/OFL.txt'
}
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
