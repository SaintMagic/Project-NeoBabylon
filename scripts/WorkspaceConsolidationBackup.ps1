[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Capture','Verify')][string]$Mode,
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [switch]$Fixture,
    [string]$FixtureRoot,
    [ValidateSet('ConfirmedAllPathOwningWritersAndRelaunchersStopped')][string]$QuiescenceAssertion,
    [string]$QuiescenceConfirmedAtUtc,
    [switch]$TestRemoveSidecarDuringCapture
)

$ErrorActionPreference = 'Stop'
$script:Utf8 = [Text.UTF8Encoding]::new($false)
$script:Task0Metadata = 'D:\CODING\NeoBabylon\.local\QA\Source-Snapshot-20260926-155823-757f70c7\snapshot-metadata.json'
$script:ApprovedManifestPath = 'D:\CODING\NeoBabylon\scripts\workspace-consolidation-pairs.json'
$script:ApprovedManifestSha256 = 'c5b6af1d297ec9b03544192834c29c8dc3f9496c12ec7768d9ad70e8467088e9'
$script:ApprovedQaRoot = 'D:\CODING\NeoBabylon\.local\QA'
$script:MarkerName = 'backup-ready.json'
$script:TestRemoveSidecarDuringCapture = [bool]$TestRemoveSidecarDuringCapture

if (-not ('WorkspaceConsolidationBackup.NativeFileInfo' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace WorkspaceConsolidationBackup {
  [StructLayout(LayoutKind.Sequential)] public struct NativeFileInfo {
    public uint FileAttributes; public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
    public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime; public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
    public uint VolumeSerialNumber; public uint FileSizeHigh; public uint FileSizeLow; public uint NumberOfLinks;
    public uint FileIndexHigh; public uint FileIndexLow;
  }
  public static class NativeFileInfoApi {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    public static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern bool GetFileInformationByHandle(SafeFileHandle handle, out NativeFileInfo info);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    public static extern bool CreateHardLink(string linkName, string existingName, IntPtr securityAttributes);
  }
}
'@
}

function Get-Sha256([string]$Path) {
    if (-not [IO.File]::Exists($Path)) { throw "Expected file is missing: $Path" }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-BytesSha256([byte[]]$Bytes) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()
}

function Get-FileLinkIdentity([string]$Path) {
    $handle = [WorkspaceConsolidationBackup.NativeFileInfoApi]::CreateFile($Path,0x80,7,[IntPtr]::Zero,3,0x02000000,[IntPtr]::Zero)
    if ($handle.IsInvalid) { throw "Cannot inspect file link count: $Path" }
    try {
        $info = [WorkspaceConsolidationBackup.NativeFileInfo]::new()
        if (-not [WorkspaceConsolidationBackup.NativeFileInfoApi]::GetFileInformationByHandle($handle,[ref]$info)) { throw "Cannot inspect file link count: $Path" }
        return [ordered]@{ count=[int]$info.NumberOfLinks; volumeSerial=('{0:x8}' -f $info.VolumeSerialNumber); fileIndex=('{0:x8}{1:x8}' -f $info.FileIndexHigh,$info.FileIndexLow) }
    } finally { $handle.Dispose() }
}

function Write-JsonAtomic([string]$Path,[object]$Value) {
    $tmp = "$Path.tmp-$([Guid]::NewGuid().ToString('N'))"
    $json = ConvertTo-Json -InputObject $Value -Depth 24
    [IO.File]::WriteAllText($tmp,$json,$script:Utf8)
    $stream = [IO.File]::Open($tmp,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    try { $stream.Flush($true) } finally { $stream.Dispose() }
    [IO.File]::Move($tmp,$Path)
}

function New-RestrictedEvidenceDirectory([string]$Path) {
    [IO.Directory]::CreateDirectory($Path) | Out-Null
    $acl = Get-Acl -LiteralPath $Path
    $acl.SetAccessRuleProtection($true,$false)
    foreach ($rule in @($acl.Access)) { [void]$acl.RemoveAccessRuleAll($rule) }
    $trusted = @(
        [Security.Principal.WindowsIdentity]::GetCurrent().User,
        [Security.Principal.SecurityIdentifier]::new('S-1-5-18'),
        [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
    )
    foreach ($sid in $trusted) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new(
            $sid,[Security.AccessControl.FileSystemRights]::FullControl,
            ([Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit),
            [Security.AccessControl.PropagationFlags]::None,[Security.AccessControl.AccessControlType]::Allow)
        [void]$acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $Path -AclObject $acl

    Assert-RestrictedEvidenceDacl $Path
}

function Assert-RestrictedEvidenceDacl([string]$Path) {
    $verified = Get-Acl -LiteralPath $Path
    if (-not $verified.AreAccessRulesProtected) { throw "Evidence directory DACL still inherits from its parent: $Path" }
    $allowedSids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $trusted = @(
        [Security.Principal.WindowsIdentity]::GetCurrent().User,
        [Security.Principal.SecurityIdentifier]::new('S-1-5-18'),
        [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
    )
    foreach ($sid in $trusted) { [void]$allowedSids.Add($sid.Value) }
    $seenSids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $verified.Access) {
        $sidValue = $entry.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
        if (-not $allowedSids.Contains($sidValue) -or $entry.IsInherited -or $entry.AccessControlType -ne [Security.AccessControl.AccessControlType]::Allow -or
            ($entry.FileSystemRights -band [Security.AccessControl.FileSystemRights]::FullControl) -ne [Security.AccessControl.FileSystemRights]::FullControl) {
            throw "Evidence directory DACL contains an unexpected or insufficient ACE: $Path"
        }
        [void]$seenSids.Add($sidValue)
    }
    foreach ($sid in $trusted) { if (-not $seenSids.Contains($sid.Value)) { throw "Evidence directory DACL is missing a required trusted principal: $Path" } }
}

function Assert-StrictChild([string]$Parent,[string]$Child,[string]$Label) {
    $p = [IO.Path]::GetFullPath($Parent).TrimEnd('\')
    $c = [IO.Path]::GetFullPath($Child).TrimEnd('\')
    if (-not $c.StartsWith($p + '\',[StringComparison]::OrdinalIgnoreCase)) { throw "$Label is outside the permitted fixture root." }
}

function Assert-NoReparseComponents([string]$Path,[bool]$IncludeLeaf) {
    $full = [IO.Path]::GetFullPath($Path)
    $root = [IO.Path]::GetPathRoot($full)
    $parts = @($full.Substring($root.Length) -split '[\\/]+' | Where-Object { $_ })
    $limit = $parts.Count
    if (-not $IncludeLeaf -and $limit -gt 0) { $limit-- }
    $current = $root
    for ($i=0; $i -lt $limit; $i++) {
        $current = Join-Path $current $parts[$i]
        $item = Get-Item -LiteralPath $current -Force -ErrorAction SilentlyContinue
        if ($null -ne $item -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Reparse point is not qualified: $current"
        }
    }
}

function Read-Task0Rules {
    if (-not [IO.File]::Exists($script:Task0Metadata)) { throw 'The authoritative Task0 snapshot metadata is unavailable.' }
    $metadata = Get-Content -LiteralPath $script:Task0Metadata -Raw | ConvertFrom-Json
    if ($metadata.Result -cne 'PASS' -or @($metadata.Sources).Count -ne 2 -or
        $metadata.Sources[0].Files -ne 201 -or $metadata.Sources[1].Files -ne 7631) {
        throw 'Task0 source-snapshot metadata does not match the accepted two-root snapshot.'
    }
    $directoryRules = @($metadata.ExclusionRules.Directories)
    $requiredPrefixes = @(
        'Any path component named .git or .local:',
        'Generated/cache/build path components:',
        'Credential directories:',
        'NeoBabylon exact generated/local paths:',
        'Runtime exact generated/local paths:'
    )
    if ($directoryRules.Count -ne $requiredPrefixes.Count) { throw 'Task0 directory-filter schema is unsupported.' }
    foreach ($prefix in $requiredPrefixes) { if (@($directoryRules | Where-Object { $_.StartsWith($prefix,[StringComparison]::Ordinal) }).Count -ne 1) { throw "Task0 directory-filter rule is missing or ambiguous: $prefix" } }
    if ($directoryRules[0] -cne 'Any path component named .git or .local: repository administration and local QA/runtime state.') { throw 'Task0 .git/.local directory-filter rule is unsupported.' }
    $fileRules = @($metadata.ExclusionRules.Files)
    $supportedFileRules = @(
        'Credential/config files: any .npmrc; .env and .env.* except .env.example; extensions .pem, .key, .pfx, .p12, .p7b, .p7c, .p8, .jks, .keystore; basename id_rsa/id_ed25519/id_ecdsa/id_dsa; files ending .secret.<ext> or .secrets.<ext>; basenames credentials/secrets with .json/.yaml/.yml/.toml/.xml/.ini.',
        'Local/generated noise: *.local.*, *.secrets.*, *.tsbuildinfo, *.log, npm-debug.*, yarn-error.*, yarn-debug.*, *.pyc, *.swp, *~, Thumbs.db, Desktop.ini, .DS_Store.'
    )
    if ($fileRules.Count -ne $supportedFileRules.Count -or $fileRules[0] -cne $supportedFileRules[0] -or $fileRules[1] -cne $supportedFileRules[1]) { throw 'Task0 file-filter schema is unsupported.' }
    $parseList = { param([string]$Prefix) $value = @($directoryRules | Where-Object { $_.StartsWith($Prefix,[StringComparison]::Ordinal) })[0].Substring($Prefix.Length).Trim(); @($value -split ',\s*') }
    return [pscustomobject]@{
        Directories=$directoryRules; Files=$fileRules
        ComponentDirectories=@('.git','.local') + (& $parseList 'Generated/cache/build path components:') + (& $parseList 'Credential directories:')
        ProductExactPaths=& $parseList 'NeoBabylon exact generated/local paths:'
        RuntimeExactPaths=& $parseList 'Runtime exact generated/local paths:'
        CredentialFilePatterns=@('*.npmrc','.env','.env.*','*.pem','*.key','*.pfx','*.p12','*.p7b','*.p7c','*.p8','*.jks','*.keystore','id_rsa','id_ed25519','id_ecdsa','id_dsa','*.secret.*','*.secrets.*','credentials.json','credentials.yaml','credentials.yml','credentials.toml','credentials.xml','credentials.ini','secrets.json','secrets.yaml','secrets.yml','secrets.toml','secrets.xml','secrets.ini')
        LocalFilePatterns=@('*.local.*','*.secrets.*','*.tsbuildinfo','*.log','npm-debug.*','yarn-error.*','yarn-debug.*','*.pyc','*.swp','*~','Thumbs.db','Desktop.ini','.DS_Store')
    }
}

function Test-Task0Excluded([string]$RelativePath,[object]$Rules,[ValidateSet('product','runtime')][string]$SourceKind = 'product') {
    $normalized = $RelativePath.Replace('\','/').Trim('/').ToLowerInvariant()
    $parts = @($normalized -split '/' | Where-Object { $_ })
    foreach ($part in $parts) { if ($part -in @($Rules.ComponentDirectories | ForEach-Object { $_.ToLowerInvariant() }) -or ($part -like 'bazel-*' -and 'bazel-*' -in $Rules.ComponentDirectories)) { return $true } }
    $exactPaths = if ($SourceKind -eq 'product') { $Rules.ProductExactPaths } else { $Rules.RuntimeExactPaths }
    foreach ($path in $exactPaths) { if ($normalized -eq $path -or $normalized.StartsWith($path + '/',[StringComparison]::Ordinal)) { return $true } }
    $leaf = if ($parts.Count) { $parts[-1] } else { '' }
    if ($leaf -eq '.env.example') { return $false }
    foreach ($pattern in @($Rules.CredentialFilePatterns) + @($Rules.LocalFilePatterns)) { if ($leaf -like $pattern.ToLowerInvariant()) { return $true } }
    return $false
}

function Read-Manifest([string]$Path) {
    if (-not [IO.File]::Exists($Path)) { throw "Manifest is not a file: $Path" }
    $raw = [IO.File]::ReadAllBytes([IO.Path]::GetFullPath($Path))
    $rows = @(ConvertFrom-Json -InputObject $script:Utf8.GetString($raw))
    if ($rows.Count -eq 0) { throw 'Backup manifest is empty.' }
    $pairs = [Collections.Generic.List[object]]::new()
    $sources = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $destinations = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($row in $rows) {
        if (($row.PSObject.Properties.Name | Sort-Object) -join ',' -cne 'destination,source') { throw 'Each manifest pair must contain exactly source and destination.' }
        $source = [IO.Path]::GetFullPath([string]$row.source).TrimEnd('\')
        $destination = [IO.Path]::GetFullPath([string]$row.destination).TrimEnd('\')
        if ($source -ieq $destination -or $source.StartsWith($destination+'\',[StringComparison]::OrdinalIgnoreCase) -or
            $destination.StartsWith($source+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest contains an overlapping or ambiguous root scope.' }
        if (-not $sources.Add($source) -or -not $destinations.Add($destination)) { throw 'Manifest contains duplicate roots.' }
        $pairs.Add([pscustomobject]@{ source=$source; destination=$destination })
    }
    foreach ($source in $sources) {
        foreach ($other in $sources) {
            if ($source -ine $other -and $other.StartsWith($source+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest contains nested source scopes.' }
        }
    }
    $destinationList = @($pairs | ForEach-Object destination)
    for ($i=0; $i -lt $destinationList.Count; $i++) {
        for ($j=$i+1; $j -lt $destinationList.Count; $j++) {
            if ($destinationList[$i].StartsWith($destinationList[$j]+'\',[StringComparison]::OrdinalIgnoreCase) -or
                $destinationList[$j].StartsWith($destinationList[$i]+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest contains nested destination scopes.' }
        }
    }
    foreach ($source in $sources) {
        foreach ($destination in $destinationList) {
            if ($source -ieq $destination -or $source.StartsWith($destination+'\',[StringComparison]::OrdinalIgnoreCase) -or
                $destination.StartsWith($source+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest source and destination scopes overlap.' }
        }
    }
    return [pscustomobject]@{ pairs=@($pairs); sha256=(Get-BytesSha256 $raw) }
}

function Get-TreeRows([string]$Root,[bool]$SourceArchive,[object]$Rules) {
    if (-not [IO.Directory]::Exists($Root)) { throw "Scope root is missing: $Root" }
    Assert-NoReparseComponents $Root $true
    $rows = [Collections.Generic.List[object]]::new()
    $sourceKind = if ([IO.Path]::GetFileName($Root) -ceq 'NeoBabylon-Runtime') { 'runtime' } else { 'product' }
    $stack = [Collections.Generic.Stack[string]]::new(); $stack.Push($Root)
    while ($stack.Count -gt 0) {
        $directory = $stack.Pop()
        foreach ($entry in [IO.Directory]::EnumerateFileSystemEntries($directory)) {
            $item = Get-Item -LiteralPath $entry -Force
            $relative = $entry.Substring($Root.Length).TrimStart('\')
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                $target = @($item.Target | ForEach-Object { [string]$_ })
                $rows.Add([ordered]@{ path=$relative; type='link'; linkType=[string]$item.LinkType; target=$target; attributes=[int]$item.Attributes; length=$null; sha256=$null; followed=$false })
                continue
            }
            if ($SourceArchive -and (Test-Task0Excluded $relative $Rules $sourceKind)) { continue }
            if ($item.PSIsContainer) {
                $rows.Add([ordered]@{ path=$relative; type='directory'; length=$null; sha256=$null; linkCount=1 })
                $stack.Push($entry)
            } else {
                $lengthBefore = [long]$item.Length
                $writeBefore = $item.LastWriteTimeUtc.Ticks
                $hash = Get-Sha256 $entry
                $after = Get-Item -LiteralPath $entry -Force
                if ($lengthBefore -ne [long]$after.Length -or $writeBefore -ne $after.LastWriteTimeUtc.Ticks) { throw "Source file changed during backup inventory: $entry" }
                $linkIdentity = Get-FileLinkIdentity $entry
                $rows.Add([ordered]@{ path=$relative; type='file'; length=$lengthBefore; sha256=$hash; linkCount=$linkIdentity.count; volumeSerial=$linkIdentity.volumeSerial; fileIndex=$linkIdentity.fileIndex })
            }
        }
    }
    return @($rows | Sort-Object path,type)
}

function Get-SqliteGroups([object[]]$Rows) {
    $byPath = @{}
    foreach ($row in $Rows) { if ($row.type -eq 'file') { $byPath[$row.path.ToLowerInvariant()] = $row } }
    $groups = [Collections.Generic.List[object]]::new()
    foreach ($row in $Rows | Where-Object { $_.type -eq 'file' -and $_.path -match '(?i)\.(sqlite3?|db)$' }) {
        $members = [Collections.Generic.List[object]]::new()
        foreach ($suffix in @('','-wal','-shm','-journal')) {
            $candidate = $row.path + $suffix
            $existing = $byPath[$candidate.ToLowerInvariant()]
            if ($null -ne $existing) { $members.Add([ordered]@{ path=$candidate; length=$existing.length; sha256=$existing.sha256 }) }
        }
        if (-not (@($members | Where-Object { $_.path -ceq $row.path }).Count)) { throw "SQLite database lacks its base file: $($row.path)" }
        $groups.Add([ordered]@{ database=$row.path; members=@($members); captureRule='Copy only after fresh external quiescence assertion; preserve every extant WAL, SHM, and rollback journal beside the database.' })
    }
    return @($groups)
}

function Get-ArtifactRecords([string]$Root,[object[]]$Rows,[string]$BackupRoot,[string]$Label) {
    $records = [Collections.Generic.List[object]]::new()
    $links = @($Rows | Where-Object { $_.type -eq 'link' })
    if ($links.Count -gt 0) { throw "Reparse link node was inventoried without traversal but its identity cannot be safely preserved and read back: $($links[0].path)" }
    $hardLinks = @($Rows | Where-Object { $_.type -eq 'file' -and $_.linkCount -gt 1 })
    if ($hardLinks.Count -gt 0) { throw "Hard-link topology was inventoried but cannot be safely preserved and read back by this fixture copier: $($hardLinks[0].path) (links=$($hardLinks[0].linkCount))" }
    foreach ($row in $Rows | Where-Object { $_.type -eq 'directory' }) {
        $targetRelative = Join-Path 'artifacts' (Join-Path $Label $row.path)
        [IO.Directory]::CreateDirectory((Join-Path $BackupRoot $targetRelative)) | Out-Null
    }
    foreach ($row in $Rows | Where-Object { $_.type -eq 'file' }) {
        $source = Join-Path $Root $row.path
        $targetRelative = Join-Path 'artifacts' (Join-Path $Label $row.path)
        $target = Join-Path $BackupRoot $targetRelative
        [IO.Directory]::CreateDirectory((Split-Path -Parent $target)) | Out-Null
        if ($script:TestRemoveSidecarDuringCapture -and $row.path -match '(?i)\.db-wal$') {
            Remove-Item -LiteralPath $source -Force
        }
        if (-not [IO.File]::Exists($source)) { throw "Captured source member disappeared (including a possible SQLite sidecar): $($row.path)" }
        [IO.File]::Copy($source,$target,$false)
        $sourceInfo = Get-Item -LiteralPath $source -Force
        $targetInfo = Get-Item -LiteralPath $target -Force
        $sourceHash = Get-Sha256 $source
        $targetHash = Get-Sha256 $target
        if ($sourceInfo.Length -ne $row.length -or $targetInfo.Length -ne $row.length -or $sourceHash -cne $row.sha256 -or $targetHash -cne $row.sha256) {
            throw "Backup copy or readback mismatch: $($row.path)"
        }
        $records.Add([ordered]@{ scope=$Label; relativePath=$row.path; backupPath=$targetRelative.Replace('\','/'); length=[long]$row.length; sha256=$targetHash; linkCount=$row.linkCount; volumeSerial=$row.volumeSerial; fileIndex=$row.fileIndex; readback='byte-hash-match' })
    }
    return @($records)
}

function Assert-Invocation([object]$Manifest) {
    if (-not [IO.Path]::IsPathFullyQualified($ManifestPath) -or -not [IO.Path]::IsPathFullyQualified($EvidenceRoot)) { throw 'ManifestPath and EvidenceRoot must be fully qualified.' }
    $manifestFull = [IO.Path]::GetFullPath($ManifestPath)
    $evidenceFull = [IO.Path]::GetFullPath($EvidenceRoot)
    if ($Fixture) {
        if (-not $FixtureRoot -or -not [IO.Path]::IsPathFullyQualified($FixtureRoot)) { throw 'Fixture mode requires a fully qualified FixtureRoot.' }
        $fixtureFull = [IO.Path]::GetFullPath($FixtureRoot).TrimEnd('\')
        $tempFull = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
        if ([IO.Path]::GetDirectoryName($fixtureFull) -ine $tempFull -or
            [IO.Path]::GetFileName($fixtureFull) -notmatch '^NeoBabylon-WorkspaceConsolidationBackup-[0-9a-f]{32}$' -or
            -not [IO.Directory]::Exists($fixtureFull)) { throw 'FixtureRoot must be a uniquely named direct temp-directory child.' }
        Assert-NoReparseComponents $fixtureFull $true
        Assert-NoReparseComponents $evidenceFull $true
        Assert-StrictChild $fixtureFull $manifestFull 'Fixture manifest'
        Assert-StrictChild $fixtureFull $evidenceFull 'Fixture evidence root'
        foreach ($pair in $Manifest.pairs) {
            if ($pair.source -ieq $evidenceFull -or $pair.source.StartsWith($evidenceFull+'\',[StringComparison]::OrdinalIgnoreCase) -or
                $evidenceFull.StartsWith($pair.source+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence root overlaps a source backup scope.' }
        }
        foreach ($pair in $Manifest.pairs) {
            Assert-StrictChild $fixtureFull $pair.source 'Fixture source'
            Assert-StrictChild $fixtureFull $pair.destination 'Fixture destination'
            if ([IO.Path]::GetPathRoot($pair.source) -ine [IO.Path]::GetPathRoot($pair.destination)) { throw 'Fixture backup roots are not on one volume.' }
        }
        if ($QuiescenceAssertion -or $QuiescenceConfirmedAtUtc) { throw 'Fixture quiescence is supplied by the fixture harness, not production assertion parameters.' }
        return [pscustomobject]@{ kind='fixture'; full=$fixtureFull; manifest=$manifestFull; evidence=$evidenceFull }
    }
    if ($FixtureRoot) { throw 'FixtureRoot is valid only with -Fixture.' }
    if ($manifestFull -ine $script:ApprovedManifestPath -or $Manifest.sha256 -cne $script:ApprovedManifestSha256 -or $Manifest.pairs.Count -ne 56) {
        throw 'Live backup requires the exact approved 56-root manifest.'
    }
    if ([IO.Path]::GetDirectoryName($evidenceFull) -ine $script:ApprovedQaRoot -or
        [IO.Path]::GetFileName($evidenceFull) -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{7,127}$') { throw 'Live EvidenceRoot must be a unique direct child of .local\QA.' }
    Assert-NoReparseComponents $script:ApprovedQaRoot $true
    Assert-NoReparseComponents $evidenceFull $true
    if (-not $QuiescenceAssertion -or -not $QuiescenceConfirmedAtUtc) { throw 'A fresh explicit operator quiescence assertion and UTC timestamp are required.' }
    $assertionTime = [DateTimeOffset]::Parse($QuiescenceConfirmedAtUtc).ToUniversalTime()
    $age = [DateTimeOffset]::UtcNow - $assertionTime
    if ($age -lt [TimeSpan]::Zero -or $age -gt [TimeSpan]::FromMinutes(10)) { throw 'Operator quiescence assertion is missing, future-dated, or older than 10 minutes.' }
    return [pscustomobject]@{ kind='live'; full=$null; manifest=$manifestFull; evidence=$evidenceFull; assertionTime=$assertionTime; assertion=$QuiescenceAssertion }
}

function Get-MarkerForBlockedLive([object]$Manifest,[object]$Scope) {
    $created = [DateTimeOffset]::UtcNow
    $oldLock = 'D:\CODING\NeoBabylon\runtime\runtime-lock.json'
    $oldLockHash = if ([IO.File]::Exists($oldLock)) { Get-Sha256 $oldLock } else { $null }
    $lockObject = if ($oldLockHash) { Get-Content -LiteralPath $oldLock -Raw | ConvertFrom-Json } else { $null }
    $productRoot = Split-Path -Parent (Split-Path -Parent $oldLock)
    $binaryPath = if ($lockObject) { [IO.Path]::GetFullPath((Join-Path $productRoot $lockObject.runtime.appServerBinaryRelativePath)) } else { $null }
    $binaryHash = if ($binaryPath -and [IO.File]::Exists($binaryPath)) { Get-Sha256 $binaryPath } else { $null }
    $lockExpected = if ($lockObject) { ([string]$lockObject.runtime.sha256).ToLowerInvariant() } else { $null }
    $stability = Get-RootStabilityObservation $Manifest
    $baselinePath = Join-Path $Scope.evidence 'backup-baseline.json'
    $baseline = [ordered]@{
        schemaVersion=1; status='incomplete'; createdUtc=$created.ToString('o'); manifestSha256=$Manifest.sha256
        coverage=[ordered]@{ complete=$false; reason='Live Data classification is unresolved; no source/Data artifact inventory was captured.' }
        quiescence=[ordered]@{ operatorAssertion=$Scope.assertion; assertedUtc=$Scope.assertionTime.ToString('o'); rootStability='recorded in backup marker; shallow root/immediate-child evidence only' }
    }
    Write-JsonAtomic $baselinePath $baseline
    $baselineHash = Get-Sha256 $baselinePath
    return [ordered]@{
        schemaVersion=1; markerType='NeoBabylon.WorkspaceConsolidationBackup'; status='not-ready'; ready=$false
        reason=if ($stability.stable) { 'Live Data remains unclassified for bounded complete recovery; no live backup was produced.' } else { 'Live Data remains unclassified and the shallow root stability check changed; no live backup was produced.' }
        createdUtc=$created.ToString('o'); manifest=[ordered]@{ path=$ManifestPath; sha256=$Manifest.sha256; pairCount=$Manifest.pairs.Count }
        baseline=[ordered]@{ path='backup-baseline.json'; sha256=$baselineHash; createdUtc=$created.ToString('o'); ageSeconds=([DateTimeOffset]::UtcNow-$created).TotalSeconds; freshness='independent but explicitly incomplete backup baseline; preflight freshness remains separate' }
        quiescence=[ordered]@{ operatorAssertion=$Scope.assertion; assertedUtc=$Scope.assertionTime.ToString('o'); rootStability=$stability; scriptProvesQuiescence=$false }
        artifacts=@(); scope=[ordered]@{ kind='live'; roots=@($Manifest.pairs | ForEach-Object { $_.source }); pairCount=$Manifest.pairs.Count; exclusions=[ordered]@{ task0MetadataPath=$script:Task0Metadata; task0MetadataSha256=(Get-Sha256 $script:Task0Metadata); sourceRules='Task0 ExclusionRules'; existingLocalSnapshotIncluded=$false; unclassifiedData='not omitted; blocks ready status' } }
        sqlite=[ordered]@{ consistency='not-captured'; note='Live SQLite members require external quiescence and group-consistent capture.'; groups=@() }
        linkPolicy=[ordered]@{ hardLinks='Inventory link count, volume serial, and file index; copier does not recreate topology; any count above one blocks readiness.'; reparse='Record raw link identity without traversal; fail closed unless exact metadata can be preserved and read back.'; knownHistoricalNodes=@('.tmpGofF8H\workspace\outside-junction','.tmpXmZlVB\workspace\outside-junction'); broaderAllowance=$false }
        reparsePolicy=[ordered]@{ traversal='never'; preserveAndReadback='unqualified'; readiness='blocked if any reparse node cannot be preserved and read back without traversal'; knownHistoricalNodes=@('.tmpGofF8H\workspace\outside-junction','.tmpXmZlVB\workspace\outside-junction') }
        gitArtifacts=@(); oldLock=[ordered]@{ path=$oldLock; sha256=$oldLockHash }; lockedBinary=[ordered]@{ path=$binaryPath; expectedSha256=$lockExpected; actualSha256=$binaryHash; matchesLock=($binaryHash -and $lockExpected -and $binaryHash -ceq $lockExpected) }
        preflightBinding=[ordered]@{ path=$null; sha256=$null; instruction='Task5B must bind a fresh unique preflight.json hash after backup; do not reuse a pre-backup preflight.' }
    }
}

function Get-RootStabilitySnapshot([object]$Manifest) {
    $rows = [Collections.Generic.List[string]]::new()
    foreach ($pair in $Manifest.pairs) {
        Assert-NoReparseComponents $pair.source $true
        $root = Get-Item -LiteralPath $pair.source -Force
        $rows.Add("root|$($pair.source)|$([int]$root.Attributes)|$($root.LastWriteTimeUtc.Ticks)")
        foreach ($entry in [IO.Directory]::EnumerateFileSystemEntries($pair.source)) {
            $item = Get-Item -LiteralPath $entry -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse point in stability scope: $entry" }
            $kind = if ($item.PSIsContainer) { 'directory' } else { 'file' }
            $length = if ($item.PSIsContainer) { 0 } else { [long]$item.Length }
            $rows.Add("child|$($entry.Substring($pair.source.Length).TrimStart('\'))|$kind|$length|$([int]$item.Attributes)|$($item.LastWriteTimeUtc.Ticks)")
        }
    }
    return Get-BytesSha256 ($script:Utf8.GetBytes((@($rows | Sort-Object) -join "`n")))
}

function Get-RootStabilityObservation([object]$Manifest) {
    $started = [DateTimeOffset]::UtcNow
    $before = Get-RootStabilitySnapshot $Manifest
    Start-Sleep -Seconds 2
    $after = Get-RootStabilitySnapshot $Manifest
    return [ordered]@{ scope='56 roots and their immediate children only'; intervalSeconds=([DateTimeOffset]::UtcNow-$started).TotalSeconds; beforeSha256=$before; afterSha256=$after; stable=($before -ceq $after); limitations='Does not enumerate descendant trees and does not prove external quiescence.' }
}

function Invoke-LiveCapture([object]$Manifest,[object]$Scope) {
    if (Test-Path -LiteralPath $Scope.evidence) { throw 'Live evidence root already exists; refusing overwrite.' }
    New-RestrictedEvidenceDirectory $Scope.evidence
    $marker = Get-MarkerForBlockedLive $Manifest $Scope
    Write-JsonAtomic (Join-Path $Scope.evidence $script:MarkerName) $marker
    Write-Output "Backup not ready: unclassified live Data blocks the complete recovery gate ($($Scope.evidence))."
}

function Invoke-FixtureCapture([object]$Manifest,[object]$Scope) {
    if (Test-Path -LiteralPath $Scope.evidence) { throw 'Fixture evidence root already exists; refusing overwrite.' }
    $rules = Read-Task0Rules
    $prepared = [Collections.Generic.List[object]]::new()
    foreach ($pair in $Manifest.pairs) {
        if (-not [IO.Directory]::Exists($pair.source)) { throw "Fixture source directory is missing: $($pair.source)" }
        if (Test-Path -LiteralPath $pair.destination) { throw "Fixture destination already exists: $($pair.destination)" }
        $leaf = [IO.Path]::GetFileName($pair.source)
        $isSourceArchive = $leaf -ceq 'NeoBabylon' -or $leaf -ceq 'NeoBabylon-Runtime'
        $rows = @(Get-TreeRows $pair.source $isSourceArchive $rules)
        $adminRoot = Join-Path $pair.source '.git'
        $adminRows = if ([IO.Directory]::Exists($adminRoot)) { @(Get-TreeRows $adminRoot $false $rules) } else { @() }
        $prepared.Add([pscustomobject]@{ pair=$pair; label=$leaf; sourceArchive=$isSourceArchive; rows=$rows; sqlite=@(Get-SqliteGroups $rows); adminRoot=$adminRoot; adminRows=$adminRows })
    }
    $created = [DateTimeOffset]::UtcNow
    $baselinePath = Join-Path $Scope.evidence 'backup-baseline.json'
    $baseline = [ordered]@{
        schemaVersion=1; createdUtc=$created.ToString('o'); manifestSha256=$Manifest.sha256
        scopes=@(
            @($prepared | ForEach-Object { [ordered]@{ root=$_.pair.source; label=$_.label; sourceArchive=$_.sourceArchive; entries=$_.rows } }) +
            @($prepared | Where-Object { [IO.Directory]::Exists($_.adminRoot) } | ForEach-Object { [ordered]@{ root=$_.adminRoot; label=($_.label+'-git-admin'); sourceArchive=$false; entries=$_.adminRows } })
        )
        linkPolicy=[ordered]@{ hardLinks='Inventory link count, volume serial, and file index; fail closed for count above one because topology recreation is unqualified.'; reparse='Record as opaque identity without traversal; fail closed because metadata preservation/readback is unqualified.'; broaderAllowance=$false }
        quiescence=[ordered]@{ operatorAssertion='temporary fixture is controlled by the test harness'; assertedUtc=$created.ToString('o'); scriptProvesQuiescence=$false }
    }
    New-RestrictedEvidenceDirectory $Scope.evidence
    Write-JsonAtomic $baselinePath $baseline
    $backupRoot = $Scope.evidence
    [IO.Directory]::CreateDirectory((Join-Path $backupRoot 'artifacts')) | Out-Null
    $allArtifacts = [Collections.Generic.List[object]]::new()
    $scopeRecords = [Collections.Generic.List[object]]::new()
    $sqliteGroups = [Collections.Generic.List[object]]::new()
    $gitRecords = [Collections.Generic.List[object]]::new()
    foreach ($preparedScope in $prepared) {
        $label = $preparedScope.label
        $records = @(Get-ArtifactRecords $preparedScope.pair.source $preparedScope.rows $backupRoot $label)
        foreach ($record in $records) { $allArtifacts.Add($record) }
        $scopeBytes = [long]0
        foreach ($row in $preparedScope.rows) { if ($row.type -eq 'file') { $scopeBytes += [long]$row.length } }
        $scopeRecords.Add([ordered]@{ label=$label; source=$preparedScope.pair.source; entryCount=$preparedScope.rows.Count; fileCount=@($preparedScope.rows | Where-Object type -eq 'file').Count; directoryCount=@($preparedScope.rows | Where-Object type -eq 'directory').Count; byteLength=$scopeBytes; sourceArchive=$preparedScope.sourceArchive; inventory=$preparedScope.rows })
        foreach ($group in $preparedScope.sqlite) { $sqliteGroups.Add([ordered]@{ scope=$label; database=$group.database; members=$group.members; captureRule=$group.captureRule }) }
        foreach ($pointer in $preparedScope.rows | Where-Object { $_.type -eq 'file' -and $_.path -match '(?i)(^|\\)\.git$' }) {
            $gitRecords.Add([ordered]@{ scope=$label; kind='linked-worktree-pointer'; relativePath=$pointer.path; sha256=$pointer.sha256; length=$pointer.length; opaque=$true })
        }
        if ([IO.Directory]::Exists($preparedScope.adminRoot)) {
            $adminRows = $preparedScope.adminRows
            $adminRecords = @(Get-ArtifactRecords $preparedScope.adminRoot $adminRows $backupRoot ($label+'-git-admin'))
            foreach ($record in $adminRecords) { $allArtifacts.Add($record) }
            $gitRecords.Add([ordered]@{ scope=$label; kind='git-administration'; relativePath='.git/'; fileCount=@($adminRows | Where-Object type -eq 'file').Count; sha256=(Get-BytesSha256 ($script:Utf8.GetBytes((ConvertTo-Json -InputObject $adminRows -Depth 8 -Compress)))); opaque=$true })
            foreach ($pointer in $adminRows | Where-Object { $_.type -eq 'file' -and $_.path -match '(?i)^worktrees\\[^\\]+\\gitdir$' }) {
                $gitRecords.Add([ordered]@{ scope=$label; kind='git-admin-backpointer'; relativePath=$pointer.path; sha256=$pointer.sha256; length=$pointer.length; opaque=$true })
            }
        }
    }
    $manifestHash = Get-Sha256 $ManifestPath
    $baselineHash = Get-Sha256 $baselinePath
    $marker = [ordered]@{
        schemaVersion=1; markerType='NeoBabylon.WorkspaceConsolidationBackup'; status='fixture-verified'; ready=$false
        createdUtc=[DateTimeOffset]::UtcNow.ToString('o'); manifest=[ordered]@{ path=$ManifestPath; sha256=$manifestHash; pairCount=$Manifest.pairs.Count }
        baseline=[ordered]@{ path='backup-baseline.json'; sha256=$baselineHash; createdUtc=$created.ToString('o'); ageSeconds=([DateTimeOffset]::UtcNow-$created).TotalSeconds; freshness='fixture capture baseline; live preflight binding is a separate Task5B step' }
        quiescence=[ordered]@{ operatorAssertion='temporary fixture is controlled by the test harness'; assertedUtc=$created.ToString('o'); rootStability='fixture read-before-copy and readback hashes matched'; scriptProvesQuiescence=$false }
        scope=[ordered]@{ kind='fixture'; roots=@($scopeRecords); pairCount=$Manifest.pairs.Count; exclusions=[ordered]@{ task0MetadataPath=$script:Task0Metadata; task0MetadataSha256=(Get-Sha256 $script:Task0Metadata); directories=@($rules.Directories); files=@($rules.Files); existingLocalSnapshotIncluded=$false; rule='Apply Task0 filters only to NeoBabylon and NeoBabylon-Runtime source archives; other fixture roots are captured in full.' } }
        artifacts=@($allArtifacts); sqlite=[ordered]@{ consistency='fixture-copy-after-controlled-quiescence-assertion'; groups=@($sqliteGroups) }
        gitArtifacts=@($gitRecords); oldLock=[ordered]@{ path=$null; sha256=$null }; lockedBinary=[ordered]@{ path=$null; sha256=$null }
        preflightBinding=[ordered]@{ path=$null; sha256=$null; instruction='Task5B binds a fresh preflight only after backup; marker does not depend on an older preflight.' }
        linkPolicy=[ordered]@{ hardLinks='Inventory link count, volume serial, and file index; copier does not recreate topology; any count above one blocks readiness.'; reparse='Record raw link identity without traversal; fail closed unless exact metadata can be preserved and read back.'; broaderAllowance=$false }
        liveQualification=[ordered]@{ status='not-ready'; reason='Fixture verification does not qualify the live Data tree or permit Move.'; unclassifiedLiveDataBlocksReady=$true }
    }
    Write-JsonAtomic (Join-Path $Scope.evidence $script:MarkerName) $marker
    Invoke-FixtureVerify $Manifest $Scope
    Write-Output "Fixture backup/readback verified: $($Scope.evidence) (pairs=$($Manifest.pairs.Count); live-ready=false)"
}

function Get-FixtureExpectedRecords([object]$Baseline) {
    $artifacts = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
    $sqlite = [Collections.Generic.List[object]]::new()
    foreach ($baselineScope in $Baseline.scopes) {
        foreach ($row in $baselineScope.entries | Where-Object type -eq 'file') {
            $key = [string]$baselineScope.label + '|' + [string]$row.path
            if ($artifacts.ContainsKey($key)) { throw "Baseline contains duplicate artifact identity: $key" }
            $artifacts.Add($key,[ordered]@{ scope=[string]$baselineScope.label; relativePath=[string]$row.path; backupPath=('artifacts/' + $baselineScope.label + '/' + $row.path.Replace('\','/')); length=[long]$row.length; sha256=[string]$row.sha256 })
        }
        foreach ($group in Get-SqliteGroups @($baselineScope.entries)) { $sqlite.Add([ordered]@{ scope=$baselineScope.label; database=$group.database; members=$group.members; captureRule=$group.captureRule }) }
    }
    return [pscustomobject]@{ artifacts=$artifacts; sqlite=@($sqlite | Sort-Object scope,database) }
}

function Invoke-FixtureVerify([object]$Manifest,[object]$Scope) {
    $markerPath = Join-Path $Scope.evidence $script:MarkerName
    if (-not [IO.File]::Exists($markerPath)) { throw 'Backup marker is missing.' }
    $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
    $manifestHash = Get-Sha256 $ManifestPath
    if ($marker.markerType -cne 'NeoBabylon.WorkspaceConsolidationBackup' -or $marker.scope.kind -cne 'fixture' -or
        $marker.manifest.sha256 -cne $manifestHash -or $marker.manifest.pairCount -ne $Manifest.pairs.Count) { throw 'Backup marker does not match this fixture manifest.' }
    if ($marker.ready -ne $false -or $marker.status -cne 'fixture-verified') { throw 'Fixture markers can never claim live readiness.' }
    if ((Get-Sha256 $script:Task0Metadata) -cne $marker.scope.exclusions.task0MetadataSha256) { throw 'Task0 source-filter authority changed after fixture capture.' }
    $baselinePath = Join-Path $Scope.evidence $marker.baseline.path
    if ((Get-Sha256 $baselinePath) -cne $marker.baseline.sha256) { throw 'Independent backup baseline hash mismatch.' }
    $baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    if ($baseline.manifestSha256 -cne $manifestHash -or @($baseline.scopes).Count -lt $Manifest.pairs.Count) { throw 'Fixture baseline does not reconcile to the manifest scope.' }
    $expectedRecords = Get-FixtureExpectedRecords $baseline
    $actualRecords = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($artifact in $marker.artifacts) {
        $key = [string]$artifact.scope + '|' + [string]$artifact.relativePath
        if (-not $actualRecords.Add($key)) { throw "Marker contains a duplicate artifact entry: $key" }
        if (-not $expectedRecords.artifacts.ContainsKey($key)) { throw "Marker contains an artifact absent from the protected baseline: $key" }
        $expected = $expectedRecords.artifacts[$key]
        if ($artifact.backupPath.Replace('\','/') -cne $expected.backupPath -or [long]$artifact.length -ne $expected.length -or $artifact.sha256 -cne $expected.sha256) { throw "Artifact record differs from the protected baseline: $key" }
        $path = Join-Path $Scope.evidence ($artifact.backupPath.Replace('/','\'))
        if (-not [IO.File]::Exists($path)) { throw "Backup readback file is missing: $($artifact.backupPath)" }
        $info = Get-Item -LiteralPath $path -Force
        if ([long]$info.Length -ne [long]$artifact.length -or (Get-Sha256 $path) -cne $artifact.sha256) { throw "Backup artifact hash or length mismatch: $($artifact.backupPath)" }
    }
    if ($actualRecords.Count -ne $expectedRecords.artifacts.Count) { throw 'Marker artifact set is incomplete relative to the protected baseline.' }
    $actualSqlite = ConvertTo-Json -InputObject @($marker.sqlite.groups | Sort-Object scope,database) -Depth 12 -Compress
    $expectedSqlite = ConvertTo-Json -InputObject @($expectedRecords.sqlite) -Depth 12 -Compress
    if ($actualSqlite -cne $expectedSqlite) { throw 'SQLite group/member inventory differs from the protected baseline.' }
    $listedPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($artifact in $marker.artifacts) { [void]$listedPaths.Add($artifact.backupPath.Replace('/','\')) }
    $actualPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $artifactRoot = Join-Path $Scope.evidence 'artifacts'
    if ([IO.Directory]::Exists($artifactRoot)) {
        $stack = [Collections.Generic.Stack[string]]::new(); $stack.Push($artifactRoot)
        while ($stack.Count -gt 0) {
            foreach ($entry in [IO.Directory]::EnumerateFileSystemEntries($stack.Pop())) {
                $item = Get-Item -LiteralPath $entry -Force
                if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse point found inside fixture artifact tree: $entry" }
                if ($item.PSIsContainer) { $stack.Push($entry) } else { [void]$actualPaths.Add($entry.Substring($Scope.evidence.Length).TrimStart('\')) }
            }
        }
    }
    if ($actualPaths.Count -ne $listedPaths.Count -or @($actualPaths | Where-Object { -not $listedPaths.Contains($_) }).Count -gt 0) { throw 'On-disk fixture artifact paths differ from the marker artifact list.' }
    foreach ($scopeRecord in $marker.scope.roots) {
        $pair = $Manifest.pairs | Where-Object { $_.source -ceq $scopeRecord.source } | Select-Object -First 1
        if ($null -eq $pair) { throw 'Backup marker names a source outside the manifest.' }
        $isSourceArchive = [bool]$scopeRecord.sourceArchive
        $rules = Read-Task0Rules
        $currentRows = @(Get-TreeRows $pair.source $isSourceArchive $rules)
        $expectedRows = ConvertTo-Json -InputObject @($scopeRecord.inventory) -Depth 8 -Compress
        $currentJson = ConvertTo-Json -InputObject @($currentRows) -Depth 8 -Compress
        if ($expectedRows -cne $currentJson) { throw "Fixture source inventory changed after backup capture: $($scopeRecord.label)" }
        foreach ($directory in $scopeRecord.inventory | Where-Object { $_.type -eq 'directory' }) {
            $backupDirectory = Join-Path $Scope.evidence (Join-Path 'artifacts' (Join-Path $scopeRecord.label $directory.path))
            if (-not [IO.Directory]::Exists($backupDirectory)) { throw "Backup readback directory is missing: $($directory.path)" }
        }
    }
    return $marker
}

function Invoke-LiveVerify([object]$Manifest) {
    $manifestFull = [IO.Path]::GetFullPath($ManifestPath)
    $evidenceFull = [IO.Path]::GetFullPath($EvidenceRoot)
    if ($manifestFull -ine $script:ApprovedManifestPath -or $Manifest.sha256 -cne $script:ApprovedManifestSha256 -or $Manifest.pairs.Count -ne 56) {
        throw 'Live marker verification requires the exact approved 56-root manifest.'
    }
    if ([IO.Path]::GetDirectoryName($evidenceFull) -ine $script:ApprovedQaRoot -or -not [IO.Directory]::Exists($evidenceFull)) { throw 'Live marker evidence must be an existing unique direct child of .local\QA.' }
    Assert-NoReparseComponents $evidenceFull $true
    Assert-RestrictedEvidenceDacl $evidenceFull
    $markerPath = Join-Path $evidenceFull $script:MarkerName
    if (-not [IO.File]::Exists($markerPath)) { throw 'Live backup marker is missing.' }
    $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
    if ($marker.markerType -cne 'NeoBabylon.WorkspaceConsolidationBackup' -or $marker.scope.kind -cne 'live' -or
        $marker.manifest.path -ine $manifestFull -or $marker.manifest.sha256 -cne $Manifest.sha256 -or $marker.manifest.pairCount -ne 56) { throw 'Live marker does not match the exact approved manifest.' }
    if ((Get-Sha256 $script:Task0Metadata) -cne $marker.scope.exclusions.task0MetadataSha256) { throw 'Task0 source-filter authority changed after marker creation.' }
    if ($marker.baseline.path -notmatch '^[^\\/]+\.json$') { throw 'Live backup baseline path is not a single evidence-root child.' }
    $baselinePath = Join-Path $evidenceFull $marker.baseline.path
    Assert-StrictChild $evidenceFull $baselinePath 'Live backup baseline'
    if ((Get-Sha256 $baselinePath) -cne $marker.baseline.sha256) { throw 'Live backup baseline hash mismatch.' }
    $baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    if ($marker.ready -eq $true) { throw 'Live-ready backup marker verification is not supported by Task5A.' }
    if ($marker.status -cne 'not-ready' -or $baseline.status -cne 'incomplete' -or $baseline.manifestSha256 -cne $Manifest.sha256 -or $baseline.coverage.complete -ne $false) {
        throw 'Live marker and hashed incomplete-baseline schema are inconsistent.'
    }
    if (@($marker.artifacts).Count -ne 0) { throw 'Task5A live markers must remain artifact-free and not-ready.' }
    foreach ($artifact in $marker.artifacts) {
        if ($artifact.backupPath -notmatch '^(artifacts[\\/])?[^:]+$') { throw 'Live marker artifact path is invalid.' }
        $artifactPath = [IO.Path]::GetFullPath((Join-Path $evidenceFull $artifact.backupPath.Replace('/','\')))
        Assert-StrictChild $evidenceFull $artifactPath 'Live backup artifact'
        Assert-NoReparseComponents $artifactPath $true
        if (-not [IO.File]::Exists($artifactPath) -or (Get-Item -LiteralPath $artifactPath).Length -ne [long]$artifact.length -or
            (Get-Sha256 $artifactPath) -cne $artifact.sha256) { throw "Live backup artifact failed external readback verification: $($artifact.backupPath)" }
    }
    foreach ($group in $marker.sqlite.groups) {
        foreach ($member in $group.members) {
            $artifact = $marker.artifacts | Where-Object { $_.scope -ceq $group.scope -and $_.relativePath -ceq $member.path } | Select-Object -First 1
            if ($null -eq $artifact -or $artifact.sha256 -cne $member.sha256 -or [long]$artifact.length -ne [long]$member.length) { throw "SQLite member is missing or differs from its captured sidecar set: $($member.path)" }
        }
    }
    $oldLock = $marker.oldLock
    if ($oldLock.path -and $oldLock.path -ine 'D:\CODING\NeoBabylon\runtime\runtime-lock.json') { throw 'Live marker names an unexpected old active lock.' }
    if ($oldLock.path -and ((Get-Sha256 $oldLock.path) -cne $oldLock.sha256)) { throw 'Old active lock no longer matches the backup marker.' }
    if ($marker.lockedBinary.path) {
        $actualBinaryHash = Get-Sha256 $marker.lockedBinary.path
        if ($actualBinaryHash -cne $marker.lockedBinary.actualSha256 -or
            ($marker.lockedBinary.expectedSha256 -and $actualBinaryHash -cne $marker.lockedBinary.expectedSha256)) { throw 'Locked binary no longer matches the backup marker or old lock.' }
    }
    return $marker
}

try {
    if ($TestRemoveSidecarDuringCapture -and -not $Fixture) { throw 'Test-only sidecar fault injection is fixture-only.' }
    $manifest = Read-Manifest $ManifestPath
    if ($Mode -eq 'Verify' -and -not $Fixture) {
        $verified = Invoke-LiveVerify $manifest
        Write-Output "Live backup marker independently verified: status=$($verified.status); ready=$($verified.ready)."
    } else {
      $scope = Assert-Invocation $manifest
      if ($Mode -eq 'Capture') {
        if ($Fixture) { Invoke-FixtureCapture $manifest $scope } else { Invoke-LiveCapture $manifest $scope }
      } elseif ($Fixture) {
        $verified = Invoke-FixtureVerify $manifest $scope
        Write-Output "Fixture marker and every listed artifact verified: $($Scope.evidence) (status=$($verified.status))."
      }
    }
} catch {
    [Console]::Error.WriteLine("WorkspaceConsolidationBackup $Mode failed closed: $($_.Exception.Message)")
    exit 1
}
