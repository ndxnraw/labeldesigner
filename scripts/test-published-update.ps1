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

function Open-VerifiedAsset($asset, [long]$limit) {
    if ($asset.size -gt $limit) { throw "Published asset is too large: $($asset.name)" }
    $binaryHeaders = $headers.Clone()
    $binaryHeaders.Accept = 'application/octet-stream'
    $stream = (Invoke-WebRequest -Uri "$api/releases/assets/$($asset.id)" -Headers $binaryHeaders).RawContentStream
    try {
        if ($stream.Length -ne $asset.size) { throw "Published asset size mismatch: $($asset.name)" }
        $stream.Position = 0
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $digest = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
        finally { $sha.Dispose() }
        if ("sha256:$digest" -cne $asset.digest) { throw "Published asset checksum mismatch: $($asset.name)" }
        $stream.Position = 0
        return $stream
    } catch {
        $stream.Dispose()
        throw
    }
}

if ($PSBoundParameters.ContainsKey('PublishedTarget') -and $PublishedTarget -cnotmatch '^[0-9a-f]{40}$') {
    throw 'The publication event must specify a full 40-character commit SHA.'
}
$release = Invoke-RestMethod -Uri "$api/releases/tags/Pre-release" -Headers $headers
if ($release.id -le 0 -or $release.tag_name -cne 'Pre-release' -or $release.draft -or !$release.prerelease) {
    throw 'Expected the public Pre-release on GitHub.'
}
if (@($release.assets).Count -ne 3) {
    throw 'Pre-release must contain one ZIP, one checksum and one update index.'
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

$stream = Open-VerifiedAsset (Get-ReleaseAsset 'UPDATE-INDEX.xml') 512KB
try {
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
$offers = @($index.Packages.UpdateOffer)
if ($offers.Count -ne 1 -or $offers[0].Family -cne 'net10-x64') {
    throw 'Published update index must contain one .NET 10 x64 offer.'
}
$package = Get-ReleaseAsset 'ND-LabelDesigner-Pre-release-Windows-NET10-x64.zip'
$offer = $offers[0]
if ($offer.SourceCommit -cne $commit -or $offer.AssetName -cne $package.name -or
    $offer.PackageId -cnotmatch '^[0-9a-f]{64}$' -or $offer.Size -cne [string]$package.size -or
    "sha256:$($offer.Sha256)" -cne $package.digest) {
    throw 'Published update offer does not match the .NET 10 x64 ZIP.'
}
$checksum = Open-VerifiedAsset (Get-ReleaseAsset 'SHA256SUMS-NET10.txt') 16KB
try {
    $reader = [IO.StreamReader]::new($checksum)
    try { $line = $reader.ReadToEnd().Trim() }
    finally { $reader.Dispose() }
} finally { $checksum.Dispose() }
if ($line -cne "$($offer.Sha256)  $($package.name)") {
    throw 'Published offline checksum does not match the .NET 10 x64 ZIP.'
}
Write-Output "Verified Pre-release $commit with one .NET 10 x64 offer."
