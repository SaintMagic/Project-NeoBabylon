[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('openrouter', 'nvidia')]
    [string]$Provider,
    [switch]$ReplaceExisting
)

$ErrorActionPreference = 'Stop'
$Provider = $Provider.ToLowerInvariant()
$sourceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$appRoot = [IO.Path]::GetFullPath((Join-Path $sourceRoot '.local/App'))
$credentialRoot = [IO.Path]::GetFullPath((Join-Path $appRoot 'Data/Credentials'))
$credentialPath = Join-Path $credentialRoot "$Provider.json"
$appPrefix = [IO.Path]::TrimEndingDirectorySeparator($appRoot) + [IO.Path]::DirectorySeparatorChar
if (-not $credentialRoot.StartsWith($appPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Credential directory escapes the app-private development root.'
}

function Assert-NoReparseComponents([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $cursor = [IO.Path]::GetPathRoot($fullPath)
    $components = $fullPath.Substring($cursor.Length).Split(
        [char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar),
        [StringSplitOptions]::RemoveEmptyEntries)
    foreach ($component in $components) {
        $cursor = [IO.Path]::Combine($cursor, $component)
        try {
            if (([IO.File]::GetAttributes($cursor) -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'Credential path contains a reparse point; import was refused.'
            }
        }
        catch [IO.FileNotFoundException] { }
        catch [IO.DirectoryNotFoundException] { }
    }
}

Assert-NoReparseComponents $credentialRoot
Assert-NoReparseComponents $credentialPath
if ((Test-Path -LiteralPath $credentialPath) -and -not $ReplaceExisting) {
    throw "An encrypted $Provider credential already exists. Use -ReplaceExisting to retain an encrypted backup before replacement."
}
if (Test-Path -LiteralPath $credentialPath -PathType Container) {
    throw 'Credential destination exists as a directory; refusing replacement.'
}

New-Item -ItemType Directory -Force -Path $credentialRoot | Out-Null
Assert-NoReparseComponents $credentialRoot
$secureValue = Read-Host -AsSecureString -Prompt "Enter the $Provider API key (stored only as current-user DPAPI ciphertext)"
$stagePath = Join-Path $credentialRoot ".${Provider}.$([Guid]::NewGuid().ToString('N')).tmp"
$backupPath = $null
try {
    if ($secureValue.Length -eq 0) { throw 'An empty credential cannot be imported.' }
    $ciphertext = ConvertFrom-SecureString -SecureString $secureValue -ErrorAction Stop
    if ([string]::IsNullOrWhiteSpace($ciphertext) -or $ciphertext -cnotmatch '^[0-9A-Fa-f]{64,}$') {
        throw 'Current-user DPAPI did not produce a valid encrypted credential record.'
    }
    $record = [ordered]@{ schemaVersion = 1; provider = $Provider; protection = 'WindowsCurrentUserDPAPI'; ciphertext = $ciphertext }
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($record | ConvertTo-Json -Compress))
    Assert-NoReparseComponents $credentialRoot
    Assert-NoReparseComponents $credentialPath
    $stream = [IO.File]::Open($stagePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally { $stream.Dispose() }

    Assert-NoReparseComponents $credentialRoot
    Assert-NoReparseComponents $credentialPath
    if ([IO.File]::Exists($credentialPath)) {
        if (-not $ReplaceExisting) { throw 'Credential appeared during import; refusing replacement.' }
        $backupPath = Join-Path $credentialRoot "$Provider.$([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')).$([Guid]::NewGuid().ToString('N')).bak.json"
        [IO.File]::Replace($stagePath, $credentialPath, $backupPath, $true)
    }
    else {
        [IO.File]::Move($stagePath, $credentialPath)
    }
    Assert-NoReparseComponents $credentialPath
    Write-Host "Imported encrypted $Provider credential to app-private development storage; secret value withheld."
    if ($backupPath) { Write-Host "Previous encrypted credential retained at: $backupPath" }
}
finally {
    if ($null -ne $secureValue) { $secureValue.Dispose() }
    $ciphertext = $null
    $bytes = $null
    if ([IO.File]::Exists($stagePath)) { [IO.File]::Delete($stagePath) }
}
