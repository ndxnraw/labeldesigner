$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repoRoot 'build/packages'
$assetName = 'ND-LabelDesigner-Pre-release-Windows-NET10-x64.zip'
$path = Join-Path $output $assetName
$target = Join-Path $output 'UPDATE-INDEX.xml'

Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($path)
try {
    $entry = $archive.GetEntry('package-manifest.xml')
    if (!$entry -or $entry.Length -gt 512KB) { throw 'Missing or invalid package manifest.' }
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.MaxCharactersInDocument = 512KB
    $stream = $entry.Open()
    try {
        $reader = [Xml.XmlReader]::Create($stream, $settings)
        try {
            $document = [Xml.XmlDocument]::new()
            $document.XmlResolver = $null
            $document.Load($reader)
        } finally { $reader.Dispose() }
    } finally { $stream.Dispose() }
} finally { $archive.Dispose() }

$manifest = $document.LabelDesignerPackage
$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -cnotmatch '^[a-f0-9]{40}$' -or !$manifest -or
    $manifest.Format -cne '1' -or $manifest.Product -cne 'Pre-release' -or $manifest.Family -cne 'net10-x64' -or
    $manifest.SourceCommit -cne $commit -or $manifest.PackageId -cnotmatch '^[a-f0-9]{64}$') {
    throw 'ZIP manifest does not identify the current .NET 10 build.'
}
$size = (Get-Item -LiteralPath $path).Length
if ($size -le 0 -or $size -gt 256MB) { throw 'ZIP exceeds the updater size limit.' }
$offer = [ordered]@{
    Family = 'net10-x64'
    SourceCommit = $commit
    PackageId = [string]$manifest.PackageId
    AssetName = $assetName
    Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    Size = $size.ToString([Globalization.CultureInfo]::InvariantCulture)
}
$writerSettings = [Xml.XmlWriterSettings]::new()
$writerSettings.Indent = $true
$writerSettings.Encoding = [Text.UTF8Encoding]::new($false)
$temp = $target + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    $writer = [Xml.XmlWriter]::Create($temp, $writerSettings)
    try {
        $writer.WriteStartDocument()
        $writer.WriteStartElement('LabelDesignerUpdates')
        $writer.WriteElementString('Format', '1')
        $writer.WriteElementString('Product', 'Pre-release')
        $writer.WriteElementString('SourceCommit', $commit)
        $writer.WriteStartElement('Packages')
        $writer.WriteStartElement('UpdateOffer')
        foreach ($field in $offer.GetEnumerator()) { $writer.WriteElementString($field.Key, $field.Value) }
        $writer.WriteEndElement()
        $writer.WriteEndElement()
        $writer.WriteEndElement()
        $writer.WriteEndDocument()
    } finally { $writer.Dispose() }
    Move-Item -LiteralPath $temp -Destination $target -Force
} finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp }
}
Write-Host "Validated update index: $target"
