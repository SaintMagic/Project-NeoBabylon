[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$CodexTag = 'rust-v0.155.1',
    [string]$CodexCommit = 'be2951ea34f0d295ed0becf97079f92fa5f6950e',
    [string]$SnapshotDate = '2026-09-22',
    [switch]$DownloadAppServer,
    [switch]$DownloadCodexCli
)

$ErrorActionPreference = 'Stop'
$headers = @{
    'User-Agent' = 'NeoBabylon-Phase0/1.0'
    'Accept' = 'application/vnd.github+json'
}

function Ensure-Directory {
    param([string]$Path)
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Save-RemoteFile {
    param(
        [string]$Url,
        [string]$Path,
        [switch]$AcceptExisting
    )

    if ((Test-Path -LiteralPath $Path) -and -not $AcceptExisting) {
        return [pscustomobject]@{
            url = $Url
            path = $Path
            action = 'existing'
            resolvedUrl = $null
        }
    }

    Invoke-WebRequest -Headers $headers -Uri $Url -OutFile $Path
    $response = Invoke-WebRequest -Headers $headers -Uri $Url -Method Head
    [pscustomobject]@{
        url = $Url
        path = $Path
        action = 'downloaded'
        resolvedUrl = $response.BaseResponse.ResponseUri.AbsoluteUri
    }
}

function Save-OptionalRemoteFile {
    param(
        [string]$Url,
        [string]$Path
    )

    try {
        if (-not (Test-Path -LiteralPath $Path)) {
            Invoke-WebRequest -Headers $headers -Uri $Url -OutFile $Path
        }
        [pscustomobject]@{
            url = $Url
            path = $Path
            status = 'captured'
            error = $null
        }
    }
    catch {
        if (Test-Path -LiteralPath $Path) {
            Remove-Item -LiteralPath $Path -Force
        }
        [pscustomobject]@{
            url = $Url
            path = $Path
            status = 'unavailable'
            error = $_.Exception.Message
        }
    }
}

$referenceRoot = Join-Path $Root 'reference'
$cacheRoot = Join-Path $referenceRoot 'cache'
$codexRoot = Join-Path $cacheRoot (Join-Path 'codex' $CodexTag)
$sourceRoot = Join-Path $codexRoot 'source'
$metadataRoot = Join-Path $codexRoot 'metadata'
$docsRoot = Join-Path $cacheRoot (Join-Path 'docs' $SnapshotDate)
$supplementalRoot = Join-Path $cacheRoot 'supplemental'
foreach ($path in @($sourceRoot, $metadataRoot, $docsRoot, $supplementalRoot)) {
    Ensure-Directory $path
}

$sourceZip = Join-Path $metadataRoot ("codex-$CodexTag-source.zip")
$sourceUrl = "https://github.com/openai/codex/archive/refs/tags/$CodexTag.zip"
if (-not (Test-Path -LiteralPath $sourceZip)) {
    Invoke-WebRequest -Headers $headers -Uri $sourceUrl -OutFile $sourceZip
}
if (-not (Get-ChildItem -LiteralPath $sourceRoot -Force -ErrorAction SilentlyContinue)) {
    $extractRoot = Join-Path $metadataRoot 'source-extract'
    if (Test-Path -LiteralPath $extractRoot) {
        throw "Refusing to reuse existing extraction directory: $extractRoot"
    }
    Expand-Archive -LiteralPath $sourceZip -DestinationPath $extractRoot
    $upstreamRoot = Get-ChildItem -LiteralPath $extractRoot -Force | Where-Object { $_.PSIsContainer } | Select-Object -First 1
    if ($null -eq $upstreamRoot) {
        throw "The source archive did not contain a top-level directory."
    }
    Get-ChildItem -LiteralPath $upstreamRoot.FullName -Force | Copy-Item -Destination $sourceRoot -Recurse -Force
    Remove-Item -LiteralPath $extractRoot -Recurse -Force
}

$releaseUrl = "https://api.github.com/repos/openai/codex/releases/tags/$CodexTag"
$tagUrl = "https://api.github.com/repos/openai/codex/git/ref/tags/$CodexTag"
$tagMetadataPath = Join-Path $metadataRoot 'tag-ref.json'
$releaseMetadataPath = Join-Path $metadataRoot 'release.json'
if (-not (Test-Path -LiteralPath $tagMetadataPath)) {
    Invoke-WebRequest -Headers $headers -Uri $tagUrl -OutFile $tagMetadataPath
}
if (-not (Test-Path -LiteralPath $releaseMetadataPath)) {
    Invoke-WebRequest -Headers $headers -Uri $releaseUrl -OutFile $releaseMetadataPath
}
$tagRef = Get-Content -Raw -LiteralPath $tagMetadataPath | ConvertFrom-Json
$tagObjectSha = $tagRef.object.sha
$tagObjectPath = Join-Path $metadataRoot 'tag-object.json'
if (-not (Test-Path -LiteralPath $tagObjectPath)) {
    Invoke-WebRequest -Headers $headers -Uri ("https://api.github.com/repos/openai/codex/git/tags/$tagObjectSha") -OutFile $tagObjectPath
}

$docs = [ordered]@{
    'llms.txt' = 'https://learn.chatgpt.com/docs/llms.txt'
    'codex-manual.md' = 'https://learn.chatgpt.com/docs/codex-manual.md'
    'app-server.md' = 'https://learn.chatgpt.com/docs/app-server.md'
    'config-advanced.md' = 'https://learn.chatgpt.com/docs/config-file/config-advanced.md'
    'open-source.md' = 'https://learn.chatgpt.com/docs/open-source.md'
    'agent-approvals-security.md' = 'https://learn.chatgpt.com/docs/agent-approvals-security.md'
    'permissions.md' = 'https://learn.chatgpt.com/docs/permissions.md'
    'permission-modes.md' = 'https://learn.chatgpt.com/docs/permission-modes.md'
    'sandboxing.md' = 'https://learn.chatgpt.com/docs/sandboxing.md'
    'features.md' = 'https://learn.chatgpt.com/docs/features.md'
    'models.md' = 'https://learn.chatgpt.com/docs/models.md'
    'app.md' = 'https://learn.chatgpt.com/docs/app.md'
    'codex-cli.md' = 'https://learn.chatgpt.com/docs/codex/cli.md'
    'hooks.md' = 'https://learn.chatgpt.com/docs/hooks.md'
    'build-skills.md' = 'https://learn.chatgpt.com/docs/build-skills.md'
    'build-plugins.md' = 'https://learn.chatgpt.com/docs/build-plugins.md'
    'mcp.md' = 'https://learn.chatgpt.com/docs/extend/mcp.md'
    'codex-sdk.md' = 'https://learn.chatgpt.com/docs/codex-sdk.md'
    'feature-maturity.md' = 'https://learn.chatgpt.com/docs/feature-maturity.md'
}
$docResults = foreach ($entry in $docs.GetEnumerator()) {
    $path = Join-Path $docsRoot $entry.Key
    Save-RemoteFile -Url $entry.Value -Path $path
}
$supplementalResults = @(
    Save-OptionalRemoteFile -Url 'https://openai.com/index/unlocking-the-codex-harness/' -Path (Join-Path $supplementalRoot 'unlocking-the-codex-harness.html')
    Save-OptionalRemoteFile -Url 'https://raw.githubusercontent.com/openai/codex/main/codex-rs/model-provider-info/src/lib.rs' -Path (Join-Path $supplementalRoot 'codex-main-model-provider-info-lib.rs')
    Save-OptionalRemoteFile -Url 'https://raw.githubusercontent.com/facebook/astryx/main/packages/core/README.md' -Path (Join-Path $supplementalRoot 'astryx-core-README.md')
    Save-OptionalRemoteFile -Url 'https://raw.githubusercontent.com/facebook/astryx/main/packages/core/package.json' -Path (Join-Path $supplementalRoot 'astryx-core-package.json')
    Save-OptionalRemoteFile -Url 'https://raw.githubusercontent.com/facebook/astryx/main/LICENSE' -Path (Join-Path $supplementalRoot 'astryx-LICENSE')
)
$supplementalResults | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $metadataRoot 'supplemental-status.json') -Encoding utf8NoBOM

if ($DownloadAppServer) {
    $assetPath = Join-Path $metadataRoot "codex-app-server-x86_64-pc-windows-msvc.exe.zip"
    $assetUrl = "https://github.com/openai/codex/releases/download/$CodexTag/codex-app-server-x86_64-pc-windows-msvc.exe.zip"
    if (-not (Test-Path -LiteralPath $assetPath)) {
        Invoke-WebRequest -Headers $headers -Uri $assetUrl -OutFile $assetPath
    }
    $binaryRoot = Join-Path $codexRoot 'binary-x86_64-pc-windows-msvc'
    Ensure-Directory $binaryRoot
    if (-not (Get-ChildItem -LiteralPath $binaryRoot -Force -ErrorAction SilentlyContinue)) {
        Expand-Archive -LiteralPath $assetPath -DestinationPath $binaryRoot
    }
}

if ($DownloadCodexCli) {
    $cliAssetPath = Join-Path $metadataRoot "codex-x86_64-pc-windows-msvc.exe.zip"
    $cliAssetUrl = "https://github.com/openai/codex/releases/download/$CodexTag/codex-x86_64-pc-windows-msvc.exe.zip"
    if (-not (Test-Path -LiteralPath $cliAssetPath)) {
        Invoke-WebRequest -Headers $headers -Uri $cliAssetUrl -OutFile $cliAssetPath
    }
    $cliRoot = Join-Path $codexRoot 'binary-cli-x86_64-pc-windows-msvc'
    Ensure-Directory $cliRoot
    if (-not (Get-ChildItem -LiteralPath $cliRoot -Force -ErrorAction SilentlyContinue)) {
        Expand-Archive -LiteralPath $cliAssetPath -DestinationPath $cliRoot
    }
}

$records = @()
$allFiles = Get-ChildItem -LiteralPath $codexRoot -File -Recurse | Sort-Object FullName
foreach ($file in $allFiles) {
    $relative = $file.FullName.Substring($Root.Length).TrimStart('\','/')
    $records += [pscustomobject]@{
        path = $relative
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant()
        bytes = $file.Length
    }
}
$recordsPath = Join-Path $metadataRoot 'files.sha256.json'
$records | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $recordsPath -Encoding utf8NoBOM

Write-Output "Acquired Codex reference snapshot $CodexTag ($CodexCommit)"
Write-Output "Source: $sourceRoot"
Write-Output "Docs: $docsRoot"
Write-Output "Files indexed: $($records.Count)"
Write-Output "Supplemental statuses: $((@($supplementalResults | Where-Object status -eq 'captured')).Count) captured, $((@($supplementalResults | Where-Object status -eq 'unavailable')).Count) unavailable"
