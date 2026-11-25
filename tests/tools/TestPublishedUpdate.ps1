$ErrorActionPreference = 'Stop'
$check = Join-Path $PSScriptRoot '../../scripts/test-published-update.ps1'
$fixtureCommit = 'a' * 40
$otherCommit = 'b' * 40
$modernHash = 'd' * 64
$modernName = 'ND-LabelDesigner-Pre-release-Windows-NET10-x64.zip'
$release = [pscustomobject]@{
    id = 1; tag_name = 'Pre-release'; draft = $false; prerelease = $true
    target_commitish = $fixtureCommit; IndexBytes = $null; ChecksumBytes = $null
    assets = @(
        [pscustomobject]@{ name = 'UPDATE-INDEX.xml'; id = 42; size = 0; digest = '' }
        [pscustomobject]@{ name = $modernName; id = 44; size = 200; digest = "sha256:$modernHash" }
        [pscustomobject]@{ name = 'SHA256SUMS-NET10.txt'; id = 45; size = 117; digest = "sha256:$modernHash" }
    )
}

function Set-IndexCommit($indexCommit, $extraOffer = '') {
    $xml = @"
<LabelDesignerUpdates>
  <Format>1</Format><Product>Pre-release</Product><SourceCommit>$indexCommit</SourceCommit>
  <Packages>
    <UpdateOffer><Family>net10-x64</Family><SourceCommit>$indexCommit</SourceCommit><PackageId>$modernHash</PackageId><AssetName>$modernName</AssetName><Sha256>$modernHash</Sha256><Size>200</Size></UpdateOffer>
    $extraOffer
  </Packages>
</LabelDesignerUpdates>
"@
    $release.IndexBytes = [Text.Encoding]::UTF8.GetBytes($xml)
    Set-AssetContent $release.assets[0] $release.IndexBytes
}

function Set-AssetContent($asset, [byte[]]$bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
    $asset.size = $bytes.Length
    $asset.digest = "sha256:$hash"
}

function Set-Checksum($hash) {
    $release.ChecksumBytes = [Text.Encoding]::UTF8.GetBytes("$hash  $modernName`n")
    Set-AssetContent $release.assets[2] $release.ChecksumBytes
}

function Invoke-RestMethod($Uri, $Headers) {
    if ($Uri -match '/releases/tags/Pre-release(?:\?|$)') { return $release }
    if ($Uri -match '/git/ref/tags/Pre-release(?:\?|$)') { return @{ object = @{ type = 'commit'; sha = $fixtureCommit } } }
    throw "Unexpected request $Uri"
}

function Invoke-WebRequest($Uri, $Headers) {
    if ($Uri -match '/releases/assets/42(?:\?|$)') { return [pscustomobject]@{ RawContentStream = [IO.MemoryStream]::new($release.IndexBytes) } }
    if ($Uri -match '/releases/assets/45(?:\?|$)') { return [pscustomobject]@{ RawContentStream = [IO.MemoryStream]::new($release.ChecksumBytes) } }
    throw "Unexpected request $Uri"
}

function Expect-Rejection($message, [hashtable]$parameters = @{}) {
    try { & $check @parameters | Out-Null }
    catch {
        if ($_.Exception.Message -ne $message) { throw }
        return
    }
    throw "Expected rejection: $message"
}

Set-IndexCommit $fixtureCommit
$release.target_commitish = 'main'
Expect-Rejection 'Pre-release target_commitish must be a full 40-character commit SHA.'
$release.target_commitish = $otherCommit
Expect-Rejection 'Pre-release target_commitish does not match its Git tag.'
$release.target_commitish = $fixtureCommit
Set-IndexCommit $otherCommit
Expect-Rejection 'Published update index does not match the Pre-release commit.'
Set-IndexCommit $fixtureCommit
Expect-Rejection 'The publication event must specify a full 40-character commit SHA.' @{ PublishedTarget = 'main' }
Expect-Rejection 'The public Pre-release commit does not match the publication event.' @{ PublishedTarget = $otherCommit }
Set-IndexCommit $fixtureCommit '<UpdateOffer><Family>framework</Family></UpdateOffer>'
Expect-Rejection 'Published update index must contain one .NET 10 x64 offer.'
Set-IndexCommit $fixtureCommit
$validAssets = $release.assets
$release.assets += [pscustomobject]@{ name = 'ND-LabelDesigner-Pre-release-Windows.zip'; id = 46; size = 100; digest = "sha256:$modernHash" }
Expect-Rejection 'Pre-release must contain one ZIP, one checksum and one update index.'
$release.assets = $validAssets
Set-Checksum ('e' * 64)
Expect-Rejection 'Published offline checksum does not match the .NET 10 x64 ZIP.'
Set-Checksum $modernHash
& $check -PublishedTarget $fixtureCommit
Write-Output 'PASS publication check rejects mismatched identities, legacy offers, extra assets and stale offline checksums.'
