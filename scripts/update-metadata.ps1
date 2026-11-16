$ErrorActionPreference='Stop'
function Write-PackageManifest([Collections.IDictionary]$Entries,[string]$Family,[string]$Checkout) {
 $commit=(& git -C $Checkout rev-parse HEAD).Trim();if($LASTEXITCODE -ne 0 -or $commit -notmatch '^[a-f0-9]{40}$'){throw 'Cannot identify package source commit.'}
 $files=@();[string[]]$names=@($Entries.Keys);[Array]::Sort($names,[StringComparer]::Ordinal)
 $canonical="1`nPre-release`n$Family`n$commit`n"
 foreach($name in $names){$path=$Entries[$name];if(![IO.Path]::IsPathRooted($path)){$path=Join-Path $Checkout $path};$info=Get-Item -LiteralPath $path;if($info.Length -le 0){throw 'Empty package payload'};$hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant();$files+=@{Path=$name;Size=$info.Length;Sha256=$hash};$canonical+=$name+'|'+$info.Length.ToString([Globalization.CultureInfo]::InvariantCulture)+'|'+$hash+"`n"}
 $sha=[Security.Cryptography.SHA256]::Create();try{$id=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
 $directory=Join-Path $Checkout 'build/package-metadata';New-Item $directory -ItemType Directory -Force|Out-Null;$path=Join-Path $directory ($Family+'.xml')
 $settings=New-Object Xml.XmlWriterSettings;$settings.Indent=$true;$settings.Encoding=New-Object Text.UTF8Encoding($false)
 $writer=[Xml.XmlWriter]::Create($path,$settings)
 try{$writer.WriteStartDocument();$writer.WriteStartElement('LabelDesignerPackage');foreach($pair in @(@('Format','1'),@('Product','Pre-release'),@('Family',$Family),@('SourceCommit',$commit),@('PackageId',$id))){$writer.WriteElementString($pair[0],$pair[1])};$writer.WriteStartElement('Files');foreach($file in $files){$writer.WriteStartElement('UpdateFile');$writer.WriteElementString('Path',$file.Path);$writer.WriteElementString('Sha256',$file.Sha256);$writer.WriteElementString('Size',$file.Size.ToString([Globalization.CultureInfo]::InvariantCulture));$writer.WriteEndElement()};$writer.WriteEndElement();$writer.WriteEndElement();$writer.WriteEndDocument()}finally{$writer.Dispose()}
 return $path
}
