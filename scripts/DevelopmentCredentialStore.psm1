function Read-DevelopmentCredential {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('openrouter', 'nvidia')]
        [string]$Provider,
        [Parameter(Mandatory)]
        [string]$CredentialRoot
    )

    $Provider = $Provider.ToLowerInvariant()
    $credentialDirectory = [IO.Path]::GetFullPath($CredentialRoot)
    $path = [IO.Path]::GetFullPath((Join-Path $credentialDirectory "$Provider.json"))
    $prefix = [IO.Path]::TrimEndingDirectorySeparator($credentialDirectory) + [IO.Path]::DirectorySeparatorChar
    if (-not $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Encrypted credential path escapes app-private development storage.'
    }

    $cursor = [IO.Path]::GetPathRoot($path)
    foreach ($component in $path.Substring($cursor.Length).Split(
        [char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar),
        [StringSplitOptions]::RemoveEmptyEntries)) {
        $cursor = [IO.Path]::Combine($cursor, $component)
        try {
            if (([IO.File]::GetAttributes($cursor) -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'Encrypted credential path contains a reparse point.'
            }
        }
        catch [IO.FileNotFoundException] { }
        catch [IO.DirectoryNotFoundException] { }
    }
    if (-not [IO.File]::Exists($path)) { return $null }

    try {
        $info = [IO.FileInfo]::new($path)
        if ($info.Length -gt 16384) { throw 'record too large' }
        $record = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8) | ConvertFrom-Json -ErrorAction Stop
        if ($record.schemaVersion -ne 1 -or $record.provider -cne $Provider -or
            $record.protection -cne 'WindowsCurrentUserDPAPI' -or
            $record.ciphertext -cnotmatch '^[0-9A-Fa-f]{64,}$') { throw 'record invalid' }
        $secureValue = ConvertTo-SecureString -String $record.ciphertext -ErrorAction Stop
        try {
            if ($secureValue.Length -eq 0) { return $null }
            $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
            try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
            finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
        }
        finally { $secureValue.Dispose() }
    }
    catch {
        throw "Encrypted $Provider credential record is invalid or cannot be decrypted for this Windows user; no provider request was made."
    }
}

function Remove-DevelopmentCredentialEnvironment {
    [CmdletBinding()]
    param([Parameter(Mandatory)][Diagnostics.ProcessStartInfo]$ProcessStartInfo)

    foreach ($name in @($ProcessStartInfo.Environment.Keys | Where-Object {
        $_.Contains('KEY', [StringComparison]::OrdinalIgnoreCase) -or
        $_.Contains('SECRET', [StringComparison]::OrdinalIgnoreCase) -or
        $_.Contains('TOKEN', [StringComparison]::OrdinalIgnoreCase)
    })) {
        $ProcessStartInfo.Environment.Remove($name) | Out-Null
    }
}

Export-ModuleMember -Function Read-DevelopmentCredential, Remove-DevelopmentCredentialEnvironment
