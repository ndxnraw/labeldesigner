$ErrorActionPreference = 'Stop'
$check = Join-Path $PSScriptRoot '../../scripts/test-published-update.ps1'
$fixtureCommit = 'a' * 40
$otherCommit = 'b' * 40
$frameworkHash = 'c' * 64
$modernHash = 'd' * 64
$frameworkName = 'ND-LabelDesigner-Pre-release-Windows.zip'
$modernName = 'ND-LabelDesigner-Pre-release-Windows-NET10-x64.zip'
$release = [pscustomobject]@{
    id = 1; tag_name = 'Pre-release'; draft = $false; prerelease = $true
    target_commitish = $fixtureCommit; IndexBytes = $null
    assets = @(
        [pscustomobject]@{ name = 'UPDATE-INDEX.xml'; id = 42; size = 0; digest = '' }
        [pscustomobject]@{ name = $frameworkName; id = 43; size = 100; digest = "sha256:$frameworkHash" }
        [pscustomobject]@{ name = $modernName; id = 44; size = 200; digest = "sha256:$modernHash" }
    )
}

function Set-IndexCommit($indexCommit) {
    $xml = @"
<LabelDesignerUpdates>
  <Format>1</Format><Product>Pre-release</Product><SourceCommit>$indexCommit</SourceCommit>
  <Packages>
    <UpdateOffer><Family>framework</Family><SourceCommit>$indexCommit</SourceCommit><PackageId>$frameworkHash</PackageId><AssetName>$frameworkName</AssetName><Sha256>$frameworkHash</Sha256><Size>100</Size></UpdateOffer>
    <UpdateOffer><Family>net10-x64</Family><SourceCommit>$indexCommit</SourceCommit><PackageId>$modernHash</PackageId><AssetName>$modernName</AssetName><Sha256>$modernHash</Sha256><Size>200</Size></UpdateOffer>
  </Packages>
</LabelDesignerUpdates>
"@
    $release.IndexBytes = [Text.Encoding]::UTF8.GetBytes($xml)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha.ComputeHash($release.IndexBytes)).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
    $release.assets[0].size = $release.IndexBytes.Length
    $release.assets[0].digest = "sha256:$hash"
}

function Invoke-RestMethod($Uri, $Headers) {
    if ($Uri -match '/releases/tags/Pre-release(?:\?|$)') { return $release }
    if ($Uri -match '/git/ref/tags/Pre-release(?:\?|$)') { return @{ object = @{ type = 'commit'; sha = $fixtureCommit } } }
    throw "Unexpected request $Uri"
}

function Invoke-WebRequest($Uri, $Headers) {
    if ($Uri -notmatch '/releases/assets/42(?:\?|$)') { throw "Unexpected request $Uri" }
    [pscustomobject]@{ RawContentStream = [IO.MemoryStream]::new($release.IndexBytes) }
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
& $check -PublishedTarget $fixtureCommit
Write-Output 'PASS publication check rejects branch names and mismatched commits.'
