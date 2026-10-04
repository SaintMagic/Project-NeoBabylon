[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$failures = [System.Collections.Generic.List[string]]::new()
$rootPath = (Resolve-Path -LiteralPath $Root).Path
$manifestPath = Join-Path $rootPath 'reference\sources.json'
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json

function Add-Failure {
    param([string]$Message)
    $failures.Add($Message)
}

foreach ($record in @($manifest.records)) {
    foreach ($artifact in @($record.artifacts)) {
        if ($artifact.sha256 -notmatch '^[A-Fa-f0-9]{64}$') {
            Add-Failure "Artifact has no verifiable SHA-256: $($artifact.path)"
            continue
        }

        $artifactPath = Join-Path $rootPath ($artifact.path -replace '/', '\')
        if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
            Add-Failure "Missing artifact: $($artifact.path)"
            continue
        }

        $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $artifactPath).Hash
        if ($actual -ine $artifact.sha256) {
            Add-Failure "SHA-256 mismatch for $($artifact.path): expected $($artifact.sha256), got $actual"
        }

        $length = (Get-Item -LiteralPath $artifactPath).Length
        if ([int64]$length -ne [int64]$artifact.bytes) {
            Add-Failure "Byte length mismatch for $($artifact.path): expected $($artifact.bytes), got $length"
        }
    }
}

$sourceRecord = $manifest.records | Where-Object id -eq 'CODEX-SOURCE-RUST-0.155.1'
$tagRefPath = Join-Path $rootPath 'reference\cache\codex\rust-v0.155.1\metadata\tag-ref.json'
$tagObjectPath = Join-Path $rootPath 'reference\cache\codex\rust-v0.155.1\metadata\tag-object.json'
$releasePath = Join-Path $rootPath 'reference\cache\codex\rust-v0.155.1\metadata\release.json'
$tagRef = Get-Content -Raw -LiteralPath $tagRefPath | ConvertFrom-Json
$tagObject = Get-Content -Raw -LiteralPath $tagObjectPath | ConvertFrom-Json
$release = Get-Content -Raw -LiteralPath $releasePath | ConvertFrom-Json

if ($release.tag_name -ne 'rust-v0.155.1') {
    Add-Failure "Release metadata tag mismatch: $($release.tag_name)"
}
if ($tagRef.object.sha -ne '4e21628f9ec9ee656650cd2b62ef92225725b5ac') {
    Add-Failure "Annotated tag object mismatch: $($tagRef.object.sha)"
}
if ($tagObject.object.sha -ne $sourceRecord.commit) {
    Add-Failure "Resolved commit mismatch: $($tagObject.object.sha)"
}

$sourceRoot = Join-Path $rootPath 'reference\cache\codex\rust-v0.155.1\source'
if (Test-Path -LiteralPath (Join-Path $sourceRoot '.git')) {
    Add-Failure 'The extracted reference source contains a nested .git directory.'
}

$protocolRoot = Join-Path $rootPath 'reference\cache\codex\rust-v0.155.1\protocol-generated'
$expectedProtocolCounts = @{
    'stable-typescript' = 721
    'experimental-typescript' = 861
    'stable-json-schema' = 312
    'experimental-json-schema' = 437
}
foreach ($entry in $expectedProtocolCounts.GetEnumerator()) {
    $directory = Join-Path $protocolRoot $entry.Key
    $count = @(Get-ChildItem -File -Recurse -LiteralPath $directory).Count
    if ($count -ne $entry.Value) {
        Add-Failure "Protocol output count mismatch for $($entry.Key): expected $($entry.Value), got $count"
    }
}

$originalStartup = Join-Path $rootPath 'NEOBABYLON_STARTUP.md'
$preservedStartup = Join-Path $rootPath 'docs\history\NEOBABYLON_STARTUP.md'
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $originalStartup).Hash -ne (Get-FileHash -Algorithm SHA256 -LiteralPath $preservedStartup).Hash) {
    Add-Failure 'The preserved startup brief does not match the workspace startup brief.'
}

$appBinary = Join-Path $rootPath 'reference\cache\codex\rust-v0.155.1\binary-x86_64-pc-windows-msvc\codex-app-server-x86_64-pc-windows-msvc.exe'
$cliBinary = Join-Path $rootPath 'reference\cache\codex\rust-v0.155.1\binary-cli-x86_64-pc-windows-msvc\codex-x86_64-pc-windows-msvc.exe'
$appVersion = (& $appBinary --version | Out-String).Trim()
$cliVersion = (& $cliBinary --version | Out-String).Trim()
if ($appVersion -ne 'codex-app-server 0.155.1') {
    Add-Failure "Unexpected App Server version: $appVersion"
}
if ($cliVersion -ne 'codex-cli 0.155.1') {
    Add-Failure "Unexpected CLI version: $cliVersion"
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output 'PASS: reference manifest artifacts, baseline metadata, protocol outputs, startup preservation, and binary versions verified.'
Write-Output "Baseline: $($sourceRecord.release) / $($sourceRecord.commit)"
Write-Output "App Server: $appVersion"
Write-Output "CLI: $cliVersion"
