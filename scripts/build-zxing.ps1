$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $root 'build\app'
New-Item $output -ItemType Directory -Force | Out-Null
$response = Join-Path $root 'build\zxing-sources.rsp'
try {
    $sources = Get-ChildItem (Join-Path $root 'src\third-party\ZXing\ZXingSource') -Recurse -Filter '*.cs' | Sort-Object FullName | ForEach-Object { '"' + $_.FullName + '"' }
    [IO.File]::WriteAllLines($response, $sources)
    & $compiler /nologo /target:library /optimize+ /define:NET40 /reference:System.Drawing.dll /reference:System.Numerics.dll ('/out:' + (Join-Path $output 'zxing.dll')) ('@' + $response)
    if ($LASTEXITCODE -ne 0) { throw 'ZXing build failed' }
} finally {
    if (Test-Path -LiteralPath $response) { Remove-Item -LiteralPath $response }
}
