param([string]$PublishedTarget)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$api = 'https://api.github.com/repos/ndxnraw/labeldesigner'
$headers = @{
    Accept = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
    'Cache-Control' = 'no-cache'
}

function Get-ReleaseAsset($name) {
    $assets = @($release.assets | Where-Object name -CEQ $name)
    if ($assets.Count -ne 1 -or $assets[0].id -le 0 -or $assets[0].size -le 0 -or
        $assets[0].digest -cnotmatch '^sha256:[0-9a-f]{64}$') {
        throw "Missing or invalid Pre-release asset: $name"
    }
    return $assets[0]
}

if ($PSBoundParameters.ContainsKey('PublishedTarget') -and $PublishedTarget -cnotmatch '^[0-9a-f]{40}$') {
    throw 'The publication event must specify a full 40-character commit SHA.'
}
$release = Invoke-RestMethod -Uri "$api/releases/tags/Pre-release" -Headers $headers
if ($release.id -le 0 -or $release.tag_name -cne 'Pre-release' -or $release.draft -or !$release.prerelease) {
    throw 'Expected the public Pre-release on GitHub.'
}
$commit = $release.target_commitish
if ($commit -cnotmatch '^[0-9a-f]{40}$') {
    throw 'Pre-release target_commitish must be a full 40-character commit SHA.'
}
if ($PublishedTarget -and $PublishedTarget -cne $commit) {
    throw 'The public Pre-release commit does not match the publication event.'
}

$ref = (Invoke-RestMethod -Uri "$api/git/ref/tags/Pre-release" -Headers $headers).object
for ($depth = 0; $ref.type -ceq 'tag' -and $depth -lt 5; $depth++) {
    if ($ref.sha -cnotmatch '^[0-9a-f]{40}$') { throw 'Invalid annotated Pre-release tag.' }
    $ref = (Invoke-RestMethod -Uri "$api/git/tags/$($ref.sha)" -Headers $headers).object
}
if ($ref.type -cne 'commit' -or $ref.sha -cne $commit) {
    throw 'Pre-release target_commitish does not match its Git tag.'
}

$asset = Get-ReleaseAsset 'UPDATE-INDEX.xml'
if ($asset.size -gt 512KB) { throw 'Published update index is too large.' }
$binaryHeaders = $headers.Clone()
$binaryHeaders.Accept = 'application/octet-stream'
$response = Invoke-WebRequest -Uri "$api/releases/assets/$($asset.id)" -Headers $binaryHeaders
$stream = $response.RawContentStream
try {
    if ($stream.Length -ne $asset.size) { throw 'Published update index size mismatch.' }
    $stream.Position = 0
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $digest = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
    if ("sha256:$digest" -cne $asset.digest) { throw 'Published update index checksum mismatch.' }
    $stream.Position = 0
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.MaxCharactersInDocument = 512KB
    $reader = [Xml.XmlReader]::Create($stream, $settings)
    try {
        $document = [Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($reader)
    } finally { $reader.Dispose() }
} finally { $stream.Dispose() }

$index = $document.LabelDesignerUpdates
if ($index.Format -cne '1' -or $index.Product -cne 'Pre-release' -or $index.SourceCommit -cne $commit) {
    throw 'Published update index does not match the Pre-release commit.'
}
$families = [ordered]@{
    'framework' = 'ND-LabelDesigner-Pre-release-Windows.zip'
    'net10-x64' = 'ND-LabelDesigner-Pre-release-Windows-NET10-x64.zip'
}
if (@($index.Packages.UpdateOffer).Count -ne $families.Count) {
    throw 'Published update index must identify both package families.'
}
foreach ($family in $families.Keys) {
    $offers = @($index.Packages.UpdateOffer | Where-Object Family -CEQ $family)
    $package = Get-ReleaseAsset $families[$family]
    if ($offers.Count -ne 1 -or $offers[0].SourceCommit -cne $commit -or
        $offers[0].AssetName -cne $package.name -or $offers[0].PackageId -cnotmatch '^[0-9a-f]{64}$' -or
        $offers[0].Size -cne [string]$package.size -or "sha256:$($offers[0].Sha256)" -cne $package.digest) {
        throw "Published update offer does not match the release: $family"
    }
}
Write-Output "Verified Pre-release $commit with both update offers."
