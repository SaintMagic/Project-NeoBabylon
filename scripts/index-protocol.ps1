[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path -LiteralPath $Root).Path
$protocolRoot = Join-Path $rootPath 'reference\cache\codex\rust-v0.155.1\protocol-generated'
$metadataRoot = Join-Path $rootPath 'reference\cache\codex\rust-v0.155.1\metadata'
$records = Get-ChildItem -File -Recurse -LiteralPath $protocolRoot | Sort-Object FullName | ForEach-Object {
    [pscustomobject]@{
        path = $_.FullName.Substring($rootPath.Length).TrimStart('\','/').Replace('\','/')
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
        bytes = $_.Length
    }
}
$outputPath = Join-Path $metadataRoot 'protocol-generated.sha256.json'
$records | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $outputPath -Encoding utf8NoBOM
Write-Output "Indexed $($records.Count) generated protocol files"
Write-Output "Manifest: $outputPath"
