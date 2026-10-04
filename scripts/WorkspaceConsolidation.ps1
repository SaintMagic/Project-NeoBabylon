[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Preflight','Move','Recover')][string]$Mode,
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [switch]$Fixture,
    [string]$FixtureRoot
)

$ErrorActionPreference = 'Stop'
$script:Utf8 = [Text.UTF8Encoding]::new($false)
$script:ApprovedManifestPath = 'D:\CODING\NeoBabylon\scripts\workspace-consolidation-pairs.json'
$script:ApprovedManifestSha256 = 'c5b6af1d297ec9b03544192834c29c8dc3f9496c12ec7768d9ad70e8467088e9'
$script:ApprovedQaRoot = 'D:\CODING\NeoBabylon\.local\QA'
$script:InventoryDeadlineUtc = [DateTime]::MaxValue

if (-not ('WorkspaceConsolidation.NativeFileInfo' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace WorkspaceConsolidation {
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
  }
}
'@
}

function Get-Sha256Text([string]$Text) {
    $bytes = $script:Utf8.GetBytes($Text)
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function Test-Exists([string]$Path) {
    return [IO.Directory]::Exists($Path) -or [IO.File]::Exists($Path)
}

function Assert-NoReparsePath([string]$Path, [bool]$IncludeLeaf) {
    $full = [IO.Path]::GetFullPath($Path)
    $root = [IO.Path]::GetPathRoot($full)
    $relative = $full.Substring($root.Length)
    $parts = @($relative -split '[\\/]+' | Where-Object { $_ })
    $upto = $parts.Count
    if (-not $IncludeLeaf -and $upto -gt 0) { $upto-- }
    $current = $root
    for ($i=0; $i -lt $upto; $i++) {
        $current = Join-Path $current $parts[$i]
        $item = Get-Item -LiteralPath $current -Force -ErrorAction SilentlyContinue
        if ($null -ne $item) {
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse point in path: $current" }
        }
    }
}

function Get-DirectoryIdentity([string]$Path) {
    Assert-NoReparsePath $Path $true
    if (-not [IO.Directory]::Exists($Path)) { throw "Directory identity target is missing: $Path" }
    $handle=[WorkspaceConsolidation.NativeFileInfoApi]::CreateFile($Path,0x80,7,[IntPtr]::Zero,3,0x02000000,[IntPtr]::Zero)
    if ($handle.IsInvalid) { throw "Cannot open directory identity: $Path" }
    try {
        $info=[WorkspaceConsolidation.NativeFileInfo]::new()
        if (-not [WorkspaceConsolidation.NativeFileInfoApi]::GetFileInformationByHandle($handle,[ref]$info)) { throw "Cannot inspect directory identity: $Path" }
        return [ordered]@{ volumeSerial=[uint32]$info.VolumeSerialNumber; fileIndexHigh=[uint32]$info.FileIndexHigh; fileIndexLow=[uint32]$info.FileIndexLow }
    } finally { $handle.Dispose() }
}

function Assert-DirectoryIdentity([string]$Path,[object]$Expected) {
    if ($null -eq $Expected) { throw "Recorded directory identity is missing: $Path" }
    $actual=Get-DirectoryIdentity $Path
    if ([uint32]$actual.volumeSerial -ne [uint32]$Expected.volumeSerial -or
        [uint32]$actual.fileIndexHigh -ne [uint32]$Expected.fileIndexHigh -or
        [uint32]$actual.fileIndexLow -ne [uint32]$Expected.fileIndexLow) { throw "Directory file ID or volume serial changed: $Path" }
}

function Assert-StrictChild([string]$Parent,[string]$Child,[string]$Label) {
    $parentFull=[IO.Path]::GetFullPath($Parent).TrimEnd('\')
    $childFull=[IO.Path]::GetFullPath($Child).TrimEnd('\')
    if (-not $childFull.StartsWith($parentFull + '\',[StringComparison]::OrdinalIgnoreCase)) { throw "$Label is outside the permitted root: $childFull" }
}

function Assert-InvocationScope {
    if (-not [IO.Path]::IsPathFullyQualified($ManifestPath) -or -not [IO.Path]::IsPathFullyQualified($EvidenceRoot)) { throw 'ManifestPath and EvidenceRoot must be fully qualified.' }
    $manifestFull=[IO.Path]::GetFullPath($ManifestPath).TrimEnd('\')
    $evidenceFull=[IO.Path]::GetFullPath($EvidenceRoot).TrimEnd('\')
    if ($Fixture) {
        if (-not [IO.Path]::IsPathFullyQualified($FixtureRoot)) { throw 'Fixture mode requires a fully qualified FixtureRoot.' }
        $fixtureFull=[IO.Path]::GetFullPath($FixtureRoot).TrimEnd('\')
        $tempFull=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
        if ($fixtureFull.StartsWith('D:\CODING\',[StringComparison]::OrdinalIgnoreCase) -or
            [IO.Path]::GetDirectoryName($fixtureFull) -ine $tempFull -or
            [IO.Path]::GetFileName($fixtureFull) -notmatch '^NeoBabylon-WorkspaceConsolidation-[0-9a-f]{32}$' -or
            -not [IO.Directory]::Exists($fixtureFull)) { throw 'FixtureRoot must be an existing, uniquely named direct child of the system temp directory, outside D:\CODING.' }
        Assert-NoReparsePath $fixtureFull $true
        Assert-StrictChild $fixtureFull $manifestFull 'Fixture manifest'
        Assert-StrictChild $fixtureFull $evidenceFull 'Fixture evidence root'
        $script:ScopedFixtureRoot=$fixtureFull
    } else {
        if ($FixtureRoot) { throw 'FixtureRoot is valid only with -Fixture.' }
        if ($manifestFull -ine $script:ApprovedManifestPath) { throw "Live manifest path must be the approved product manifest: $($script:ApprovedManifestPath)" }
        if ([IO.Path]::GetDirectoryName($evidenceFull) -ine $script:ApprovedQaRoot -or
            [IO.Path]::GetFileName($evidenceFull) -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{7,127}$') { throw 'Live EvidenceRoot must be a unique direct child of the product .local\QA directory.' }
    }
    Assert-NoReparsePath $manifestFull $true
    Assert-NoReparsePath $evidenceFull $true
}

function Assert-ManifestScope([object]$manifest) {
    if ($Fixture) {
        foreach ($pair in $manifest.Pairs) {
            Assert-StrictChild $script:ScopedFixtureRoot $pair.source 'Fixture source'
            Assert-StrictChild $script:ScopedFixtureRoot $pair.destination 'Fixture destination'
        }
    } elseif ($manifest.Pairs.Count -ne 56 -or $manifest.Sha256 -cne $script:ApprovedManifestSha256) {
        throw 'Live manifest content is not the exact approved 56-pair file.'
    }
}

function Read-Manifest {
    if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) { throw "Manifest is not a file: $ManifestPath" }
    $raw = [IO.File]::ReadAllBytes([IO.Path]::GetFullPath($ManifestPath))
    $text = $script:Utf8.GetString($raw)
    $items = @(ConvertFrom-Json -InputObject $text)
    if ($items.Count -eq 0) { throw 'Manifest must contain at least one pair.' }
    $pairs = [Collections.Generic.List[object]]::new()
    $sources = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $destinations = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($item in $items) {
        $keys = @($item.PSObject.Properties.Name | Sort-Object)
        if (($keys -join ',') -cne 'destination,source') { throw 'Each pair must contain exactly source and destination.' }
        foreach ($rawPath in @([string]$item.source,[string]$item.destination)) {
            if ([string]::IsNullOrWhiteSpace($rawPath) -or $rawPath.StartsWith('\\')) { throw "Invalid or UNC path: $rawPath" }
        }
        $source = [IO.Path]::GetFullPath([string]$item.source).TrimEnd('\')
        $destination = [IO.Path]::GetFullPath([string]$item.destination).TrimEnd('\')
        if ([IO.Path]::GetPathRoot($source) -ine [IO.Path]::GetPathRoot($destination)) { throw "Cross-volume pair is prohibited: $source -> $destination" }
        if ($source -ieq $destination -or $source.StartsWith($destination + '\',[StringComparison]::OrdinalIgnoreCase) -or $destination.StartsWith($source + '\',[StringComparison]::OrdinalIgnoreCase)) { throw "Overlapping source/destination pair: $source -> $destination" }
        if (-not $sources.Add($source) -or -not $destinations.Add($destination)) { throw 'Manifest contains duplicate source or destination paths.' }
        $pairs.Add([pscustomobject]@{ source=$source; destination=$destination })
    }
    foreach ($s in $sources) { if ($destinations.Contains($s)) { throw "A source is another pair's destination: $s" } }
    $script:PathAliases = [Collections.Generic.List[object]]::new()
    for ($i=0; $i -lt $pairs.Count; $i++) {
        $token = "<PAIR-$i>"
        foreach ($path in @($pairs[$i].source,$pairs[$i].destination)) {
            $script:PathAliases.Add([pscustomobject]@{ path=$path; token=$token })
            $script:PathAliases.Add([pscustomobject]@{ path=$path.Replace('\','/'); token=$token })
        }
    }
    return [pscustomobject]@{ Pairs=@($pairs); Sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($raw)).ToLowerInvariant() }
}

function Get-NormalizedGitTarget([string]$Content,[bool]$LinkedPointer) {
    $value=$Content.Trim()
    if ($LinkedPointer) {
        if ($value -notmatch '^gitdir:\s*(?<target>[A-Za-z]:[/\\].+)$') { throw 'Linked-worktree .git pointer has an unexpected format.' }
        $value=$Matches.target
    } elseif ($value -notmatch '^[A-Za-z]:[/\\].+$') { throw 'Git worktree backpointer has an unexpected format.' }
    return [IO.Path]::GetFullPath($value).TrimEnd('\')
}

function Get-GitBindings([object]$manifest) {
    $bindings=[Collections.Generic.List[object]]::new()
    $linkedLocations=[Collections.Generic.List[object]]::new()
    foreach ($ownerPair in $manifest.Pairs) {
        $directPointer=Join-Path $ownerPair.source '.git'
        if ([IO.File]::Exists($directPointer)) {
            $linkedLocations.Add([ordered]@{ ownerSource=$ownerPair.source; ownerDestination=$ownerPair.destination; linkedSource=$ownerPair.source; linkedDestination=$ownerPair.destination; relativePointer='.git' })
        }
        if ([IO.Path]::GetFileName($ownerPair.source) -ceq 'NeoBabylon-Data') {
            foreach ($relativeRoot in @(
                'NeoBabylon-Runtime-RouteControl-20260923',
                'qa\P2-13-classification-20260926-01\baseline\runtime',
                'qa\P5-01-clean-baseline-20260925\codex-rs'
            )) {
                $linkedLocations.Add([ordered]@{ ownerSource=$ownerPair.source; ownerDestination=$ownerPair.destination; linkedSource=(Join-Path $ownerPair.source $relativeRoot); linkedDestination=(Join-Path $ownerPair.destination $relativeRoot); relativePointer=(Join-Path $relativeRoot '.git') })
            }
        }
    }
    foreach ($location in $linkedLocations) {
        $pointerFile=Join-Path $location.linkedSource '.git'
        if (-not [IO.File]::Exists($pointerFile)) { throw "Approved linked-worktree pointer is missing: $pointerFile" }
        if (((Get-Item -LiteralPath $pointerFile -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparsed Git pointer is not qualified: $pointerFile" }
        $target=Get-NormalizedGitTarget ([IO.File]::ReadAllText($pointerFile)) $true
        $adminName=[IO.Path]::GetFileName($location.linkedSource)
        $matches=@($manifest.Pairs | Where-Object { (Join-Path $_.source ".git\worktrees\$adminName") -ieq $target })
        if ($matches.Count -ne 1) { throw "Linked-worktree pointer does not name exactly one manifest main checkout: $pointerFile" }
        $mainPair=$matches[0]
        if ($mainPair.source -ieq $location.linkedSource) { throw 'Git worktree pointer cannot refer to its own root.' }
        $backpointer=Join-Path $mainPair.source ".git\worktrees\$adminName\gitdir"
        if (-not [IO.File]::Exists($backpointer)) { throw "Git admin backpointer is missing: $backpointer" }
        $backTarget=Get-NormalizedGitTarget ([IO.File]::ReadAllText($backpointer)) $false
        if ($backTarget -ine (Join-Path $location.linkedSource '.git')) { throw "Git admin backpointer does not name the approved linked checkout: $backpointer" }
        $bindings.Add([ordered]@{ mainSource=$mainPair.source; mainDestination=$mainPair.destination; ownerSource=$location.ownerSource; ownerDestination=$location.ownerDestination; linkedSource=$location.linkedSource; linkedDestination=$location.linkedDestination; relativePointer=$location.relativePointer; adminName=$adminName })
    }
    $mainRoots=@($bindings | ForEach-Object { $_.mainSource } | Sort-Object -Unique)
    foreach ($mainRoot in $mainRoots) {
        $adminRoot=Join-Path $mainRoot '.git\worktrees'
        $actualAdmins=if ([IO.Directory]::Exists($adminRoot)) { @([IO.Directory]::EnumerateDirectories($adminRoot) | ForEach-Object { [IO.Path]::GetFileName($_) } | Sort-Object) } else { @() }
        $approvedAdmins=@($bindings | Where-Object { $_.mainSource -ieq $mainRoot } | ForEach-Object { $_.adminName } | Sort-Object)
        if (($actualAdmins -join '|') -cne ($approvedAdmins -join '|')) { throw "Main checkout has an unbound or missing linked-worktree admin entry: $adminRoot" }
    }
    Assert-ApprovedLiveBindings $bindings
    return @($bindings)
}

function Assert-ApprovedLiveBindings([object]$Bindings) {
    if ($Fixture) { return }
    $expected=@(
        'D:\CODING\NBRT-RouteControl',
        'D:\CODING\NeoBabylon-Data\NeoBabylon-Runtime-RouteControl-20260923',
        'D:\CODING\NeoBabylon-Data\qa\P2-13-classification-20260926-01\baseline\runtime',
        'D:\CODING\NeoBabylon-Data\qa\P5-01-clean-baseline-20260925\codex-rs'
    ) | Sort-Object
    $actual=@($Bindings | ForEach-Object { $_.linkedSource } | Sort-Object)
    if ($Bindings.Count -ne 4 -or ($expected -join '|') -cne ($actual -join '|') -or
        @($Bindings | Where-Object { $_.mainSource -ine 'D:\CODING\NeoBabylon-Runtime' }).Count -ne 0) {
        throw 'The four approved runtime linked-worktree identities are not present exactly once.'
    }
}

function Get-GitPointerKind([string]$Root,[string]$RelativePath) {
    $normalizedRelative=$RelativePath.Replace('/','\')
    foreach ($binding in @($script:GitBindings)) {
        if ($normalizedRelative -ieq $binding.relativePointer -and ($Root -ieq $binding.ownerSource -or $Root -ieq $binding.ownerDestination)) { return 'linked' }
        if ($normalizedRelative -ieq ".git\worktrees\$($binding.adminName)\gitdir" -and ($Root -ieq $binding.mainSource -or $Root -ieq $binding.mainDestination)) { return 'admin' }
    }
    return $null
}

function Assert-GitPointerIdentity([string]$Root,[string]$RelativePath,[string]$Content) {
    $normalizedRelative=$RelativePath.Replace('/','\')
    $linked=(Get-GitPointerKind $Root $normalizedRelative) -eq 'linked'
    $target=Get-NormalizedGitTarget $Content $linked
    foreach ($binding in @($script:GitBindings)) {
        if ($linked -and $normalizedRelative -ieq $binding.relativePointer -and ($Root -ieq $binding.ownerSource -or $Root -ieq $binding.ownerDestination)) {
            foreach ($mainPath in @($binding.mainSource,$binding.mainDestination)) {
                if ($target -ieq (Join-Path $mainPath ".git\worktrees\$($binding.adminName)")) { return }
            }
        }
        if (-not $linked -and $normalizedRelative -ieq ".git\worktrees\$($binding.adminName)\gitdir" -and
            ($Root -ieq $binding.mainSource -or $Root -ieq $binding.mainDestination)) {
            foreach ($linkedPath in @($binding.linkedSource,$binding.linkedDestination)) {
                if ($target -ieq (Join-Path $linkedPath '.git')) { return }
            }
        }
    }
    throw "Git pointer target is outside the recorded linked-worktree identity: $Root\$RelativePath"
}

function Assert-OriginalGitPointerTargets {
    foreach ($binding in @($script:GitBindings)) {
        $linkedPointer=Join-Path $binding.linkedSource '.git'
        $adminPointer=Join-Path $binding.mainSource ".git\worktrees\$($binding.adminName)\gitdir"
        Assert-NoReparsePath $linkedPointer $true
        Assert-NoReparsePath $adminPointer $true
        if ((Get-NormalizedGitTarget ([IO.File]::ReadAllText($linkedPointer)) $true) -ine
            (Join-Path $binding.mainSource ".git\worktrees\$($binding.adminName)")) {
            throw "Move requires the original Git pointer target: $linkedPointer"
        }
        if ((Get-NormalizedGitTarget ([IO.File]::ReadAllText($adminPointer)) $false) -ine
            (Join-Path $binding.linkedSource '.git')) {
            throw "Move requires the original Git admin backpointer target: $adminPointer"
        }
    }
}

function Get-BaselineDigest([object]$Pairs,[object]$GitBindings) {
    $payload=[ordered]@{ pairs=@($Pairs); gitBindings=@($GitBindings) }
    return Get-Sha256Text (ConvertTo-Json -InputObject $payload -Depth 12 -Compress)
}

function Get-HashMode([string]$RelativePath) {
    $normalized=$RelativePath.Replace('/','\')
    $leaf=[IO.Path]::GetFileName($normalized)
    if ($normalized -match '(^|\\)\.git(\\|$)' -or
        $leaf -match '^(Cargo\.lock|[^\\]+\.(sqlite|sqlite3|db)(-wal|-shm|-journal)?)$') { return 'sha256' }
    if ($normalized -match '(^|\\)(cache|caches|\.cache|node_modules|target|build|dist|bin|obj|tmp|temp)\\') { return 'metadata-only' }
    return 'sha256'
}

function Get-TreeFingerprint([string]$Root, [string]$CanonicalRoot) {
    if (-not [IO.Directory]::Exists($Root)) { throw "Expected directory is missing: $Root" }
    Assert-NoReparsePath $Root $true
    if ($Root.Length -gt 240 -or $CanonicalRoot.Length -gt 240) { throw "Source or projected destination root exceeds conservative MAX_PATH qualification limit: $Root -> $CanonicalRoot" }
    $rows = [Collections.Generic.List[object]]::new()
    $stack = [Collections.Generic.Stack[string]]::new(); $stack.Push($Root)
    $bytes = [long]0; $hashedBytes=[long]0; $metadataOnlyBytes=[long]0
    while ($stack.Count -gt 0) {
        if ([DateTime]::UtcNow -gt $script:InventoryDeadlineUtc) { throw 'Inventory phase exceeded its 30-minute deadline; no qualifying validation was produced.' }
        $dir = $stack.Pop()
        $dirBefore=Get-Item -LiteralPath $dir -Force
        foreach ($entry in [IO.Directory]::EnumerateFileSystemEntries($dir)) {
            if ([DateTime]::UtcNow -gt $script:InventoryDeadlineUtc) { throw 'Inventory phase exceeded its 30-minute deadline; no qualifying validation was produced.' }
            $item = Get-Item -LiteralPath $entry -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse point inside source tree: $entry" }
            $rel = $entry.Substring($Root.Length).TrimStart('\')
            if ($entry.Length -gt 240) { throw "Path exceeds conservative MAX_PATH qualification limit: $entry" }
            $projected = Join-Path $CanonicalRoot $rel
            if ($projected.Length -gt 240) { throw "Projected destination path exceeds conservative MAX_PATH qualification limit: $projected" }
            if ($item.PSIsContainer) {
                $rows.Add([ordered]@{ path=$rel; type='directory' })
                $stack.Push($entry)
            } else {
                $fileHandle = [WorkspaceConsolidation.NativeFileInfoApi]::CreateFile($entry,0x80,7,[IntPtr]::Zero,3,0x02000000,[IntPtr]::Zero)
                if ($fileHandle.IsInvalid) { throw "Cannot inspect file link count: $entry" }
                try {
                    $nativeInfo = [WorkspaceConsolidation.NativeFileInfo]::new()
                    if (-not [WorkspaceConsolidation.NativeFileInfoApi]::GetFileInformationByHandle($fileHandle,[ref]$nativeInfo)) { throw "Cannot inspect file link count: $entry" }
                    if ($nativeInfo.NumberOfLinks -gt 1) { throw "Hard-linked file is not qualified: $entry (links=$($nativeInfo.NumberOfLinks))" }
                } finally { $fileHandle.Dispose() }
                $before = Get-Item -LiteralPath $entry -Force
                $pointerKind=Get-GitPointerKind $Root $rel
                $pointer=($null -ne $pointerKind)
                $hashMode=Get-HashMode $rel
                $hash=$null
                if ($hashMode -eq 'sha256' -and -not $pointer) { $hash = (Get-FileHash -LiteralPath $entry -Algorithm SHA256).Hash.ToLowerInvariant() }
                $after = Get-Item -LiteralPath $entry -Force
                if ($before.Length -ne $after.Length -or $before.LastWriteTimeUtc -ne $after.LastWriteTimeUtc -or $before.CreationTimeUtc -ne $after.CreationTimeUtc) { throw "File changed during inventory: $entry" }
                $length = [long]$after.Length; $bytes += $length
                $fileHash = $hash
                $fingerprintLength = $length
                if ($pointer) {
                    $content = [IO.File]::ReadAllText($entry)
                    Assert-GitPointerIdentity $Root $rel $content
                    if ($pointerKind -eq 'linked') {
                        $normalized = "gitdir:<validated-worktree-admin-path>`n"
                    } else {
                        $normalized = "<validated-worktree-checkout-path>`n"
                    }
                    $fileHash = Get-Sha256Text $normalized
                    $fingerprintLength = [long]$script:Utf8.GetByteCount($normalized)
                }
                if ($hashMode -eq 'metadata-only') {
                    $metadataOnlyBytes += $length
                    $rows.Add([ordered]@{ path=$rel; type='file'; length=$length; lastWriteUtcTicks=$after.LastWriteTimeUtc.Ticks; creationUtcTicks=$after.CreationTimeUtc.Ticks; attributes=[int]$after.Attributes; hashMode='metadata-only'; sha256=$null; pointerPathNormalized=$false })
                } else {
                    $hashedBytes += $length
                    $rows.Add([ordered]@{ path=$rel; type='file'; length=$fingerprintLength; hashMode='sha256'; sha256=$fileHash; pointerPathNormalized=[bool]$pointer })
                }
            }
        }
        $dirAfter=Get-Item -LiteralPath $dir -Force
        if ([DateTime]::UtcNow -gt $script:InventoryDeadlineUtc) { throw 'Inventory phase exceeded its 30-minute deadline; no qualifying validation was produced.' }
        if ($dirBefore.LastWriteTimeUtc -ne $dirAfter.LastWriteTimeUtc -or $dirBefore.Attributes -ne $dirAfter.Attributes) { throw "Directory changed during inventory: $dir" }
    }
    $orderedRows = @($rows | Sort-Object { $_.path }, { $_.type })
    $canonical = ConvertTo-Json -InputObject $orderedRows -Depth 8 -Compress
    return [pscustomobject]@{ fingerprint=(Get-Sha256Text $canonical); bytes=$bytes; hashedBytes=$hashedBytes; metadataOnlyBytes=$metadataOnlyBytes; entries=$orderedRows }
}

function Get-ConfigFileAudit([string]$Candidate,[string]$Origin) {
    $found=[Collections.Generic.List[object]]::new()
    [void]$script:ConfigPathsChecked.Add($Candidate)
    if (-not [IO.File]::Exists($Candidate)) { return @($found) }
    [void]$script:ConfigFilesRead.Add($Candidate)
    $isCargo=$Candidate -match '[\\/]\.cargo[\\/]config(\.toml)?$'
    $gitSection=''
    $lineNo=0
    foreach ($line in [IO.File]::ReadLines($Candidate)) {
        $lineNo++
        if ($line -match '^\s*[#;]') { continue }
        if ($isCargo) {
            if ($line -match '^\s*(paths|target-dir|directory)\s*=') {
                $found.Add([ordered]@{ file=$Candidate; line=$lineNo; classification='cargo-path-setting'; origin=$Origin })
            }
            continue
        }
        if ($line -match '^\s*\[([^\]]+)\]') {
            $section=$Matches[1]
            if ($section -match '^includeIf\b') {
                $gitSection='includeIf'
                $found.Add([ordered]@{ file=$Candidate; line=$lineNo; classification='git-conditional-include'; origin=$Origin })
            } elseif ($section -match '^include\s*$') { $gitSection='include' }
            elseif ($section -match '^core\s*$') { $gitSection='core' }
            else { $gitSection='' }
            continue
        }
        if (($gitSection -eq 'include' -or $gitSection -eq 'includeIf') -and $line -match '^\s*path\s*=') {
            $found.Add([ordered]@{ file=$Candidate; line=$lineNo; classification='git-include-path'; origin=$Origin })
        } elseif ($gitSection -eq 'core' -and $line -match '^\s*worktree\s*=') {
            $found.Add([ordered]@{ file=$Candidate; line=$lineNo; classification='git-core-worktree'; origin=$Origin })
        }
    }
    return @($found)
}

function Get-PathAudit([string]$Path,[string]$Origin) {
    $full=[IO.Path]::GetFullPath($Path); $current=if ([IO.Directory]::Exists($full)) { $full } else { Split-Path -Parent $full }
    $ancestors=[Collections.Generic.List[string]]::new()
    while ($current) {
        $ancestors.Add($current)
        $parent=Split-Path -Parent $current
        if (-not $parent -or $parent -ieq $current) { break }
        $current=$parent
    }
    $found=[Collections.Generic.List[object]]::new()
    foreach ($dir in $ancestors) {
        foreach ($candidate in @((Join-Path $dir '.git\config'),(Join-Path $dir '.cargo\config.toml'),(Join-Path $dir '.cargo\config'))) {
            foreach ($observation in @(Get-ConfigFileAudit $candidate $Origin)) { $found.Add($observation) }
        }
    }
    return @($found)
}

function Get-UserGitConfigAudit {
    $candidates=[Collections.Generic.List[string]]::new()
    if ($env:USERPROFILE) { $candidates.Add((Join-Path $env:USERPROFILE '.gitconfig')) }
    if ($env:XDG_CONFIG_HOME) { $candidates.Add((Join-Path $env:XDG_CONFIG_HOME 'git\config')) }
    elseif ($env:USERPROFILE) { $candidates.Add((Join-Path $env:USERPROFILE '.config\git\config')) }
    if ($env:GIT_CONFIG_GLOBAL -and $env:GIT_CONFIG_GLOBAL -ne 'NUL') { $candidates.Add([IO.Path]::GetFullPath($env:GIT_CONFIG_GLOBAL)) }
    $found=[Collections.Generic.List[object]]::new()
    foreach ($candidate in @($candidates | Sort-Object -Unique)) {
        foreach ($observation in @(Get-ConfigFileAudit $candidate 'user-git')) { $found.Add($observation) }
    }
    return @($found)
}

function Assert-AclScreen([string]$Path,[bool]$RequireWrite) {
    $item=Get-Item -LiteralPath $Path -Force
    $acl=Get-Acl -LiteralPath $Path
    $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
    $sids=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [void]$sids.Add($identity.User.Value)
    foreach ($group in $identity.Groups) { [void]$sids.Add($group.Value) }
    $required=if ($RequireWrite) { [Security.AccessControl.FileSystemRights]::Modify } else { [Security.AccessControl.FileSystemRights]::ReadAndExecute }
    $allowed=[Security.AccessControl.FileSystemRights]0
    foreach ($rule in $acl.Access) {
        if (-not $sids.Contains($rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value)) { continue }
        if (($rule.FileSystemRights -band $required) -eq 0) { continue }
        if ($rule.AccessControlType -eq [Security.AccessControl.AccessControlType]::Deny) { throw "Applicable deny ACE prevents required access at $Path" }
        $allowed = $allowed -bor $rule.FileSystemRights
    }
    if (($allowed -band $required) -ne $required) { throw "ACL screen cannot establish required access at $Path (required=$required)" }
}

function Write-JsonAtomic([string]$Path, [object]$Value) {
    $tmp = "$Path.tmp-$([Guid]::NewGuid().ToString('N'))"
    $json = ConvertTo-Json -InputObject $Value -Depth 20
    [IO.File]::WriteAllText($tmp,$json,$script:Utf8)
    $stream = [IO.File]::Open($tmp,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    try { $stream.Flush($true) } finally { $stream.Dispose() }
    [IO.File]::Move($tmp,$Path)
}

function Add-Journal([string]$Path,[object]$Event) {
    $line = (ConvertTo-Json -InputObject $Event -Depth 12 -Compress) + "`n"
    $data = $script:Utf8.GetBytes($line)
    $stream = [IO.File]::Open($Path,[IO.FileMode]::Append,[IO.FileAccess]::Write,[IO.FileShare]::Read)
    try { $stream.Write($data,0,$data.Length); $stream.Flush($true) } finally { $stream.Dispose() }
}

function Read-Journal([string]$Path) {
    $content = [IO.File]::ReadAllText($Path)
    if (-not $content.EndsWith("`n")) { throw 'Journal has a torn final record; recovery is ambiguous and will not modify it.' }
    $events = [Collections.Generic.List[object]]::new()
    foreach ($line in @($content -split "`n" | Where-Object { $_ })) {
        try { $events.Add((ConvertFrom-Json -InputObject $line)) } catch { throw 'Journal contains a malformed record; recovery is ambiguous.' }
    }
    return @($events)
}

function Assert-Fingerprint([string]$Root,[string]$OtherRoot,[string]$Expected) {
    $actual = Get-TreeFingerprint $Root $OtherRoot
    if ($actual.fingerprint -cne $Expected) {
        $pairBaseline = @($script:CurrentBaseline.pairs | Where-Object { $_.sourceFingerprint -ceq $Expected } | Select-Object -First 1)
        $difference = ''
        if ($pairBaseline.Count -gt 0) {
            $old = @{}; foreach ($row in $pairBaseline[0].entries) { $old[$row.path] = ConvertTo-Json -InputObject $row -Compress -Depth 5 }
            $new = @{}; foreach ($row in $actual.entries) { $new[$row.path] = ConvertTo-Json -InputObject $row -Compress -Depth 5 }
            $keys = @($old.Keys + $new.Keys | Sort-Object -Unique)
            $difference = (@($keys | Where-Object { $old[$_] -cne $new[$_] } | Select-Object -First 5) -join ', ')
        }
        throw "Tree fingerprint changed or is not the recorded tree: $Root; changed entries: $difference"
    }
}

function Get-Baseline([object]$manifest,[string]$basePath) {
    if (-not (Test-Path -LiteralPath $basePath -PathType Leaf)) { throw "Missing preflight baseline: $basePath" }
    $baseline = Get-Content -LiteralPath $basePath -Raw | ConvertFrom-Json
    if ($baseline.status -cne 'ready' -or $baseline.manifestSha256 -cne $manifest.Sha256) { throw 'Preflight is not ready or manifest hash has changed.' }
    $expectedBaseline = Get-BaselineDigest $baseline.pairs $baseline.gitBindings
    if ($baseline.baselineSha256 -cne $expectedBaseline) { throw 'Preflight baseline checksum mismatch.' }
    if (@($baseline.pairs).Count -ne $manifest.Pairs.Count) { throw 'Preflight pair count differs from manifest.' }
    $script:CurrentBaseline = $baseline
    for ($i=0; $i -lt $manifest.Pairs.Count; $i++) {
        if ($baseline.pairs[$i].source -cne $manifest.Pairs[$i].source -or $baseline.pairs[$i].destination -cne $manifest.Pairs[$i].destination) { throw 'Preflight ordered pairs differ from manifest.' }
    }
    foreach ($binding in @($baseline.gitBindings)) {
        $main=@($manifest.Pairs | Where-Object { $_.source -ceq $binding.mainSource -and $_.destination -ceq $binding.mainDestination })
        $owner=@($manifest.Pairs | Where-Object { $_.source -ceq $binding.ownerSource -and $_.destination -ceq $binding.ownerDestination })
        if ($main.Count -ne 1 -or $owner.Count -ne 1) { throw 'Preflight Git binding does not match manifest roots.' }
        $relativeRoot=if ($binding.relativePointer -ceq '.git') { '' } else { Split-Path -Parent $binding.relativePointer }
        $expectedSource=if ($relativeRoot) { Join-Path $binding.ownerSource $relativeRoot } else { $binding.ownerSource }
        $expectedDestination=if ($relativeRoot) { Join-Path $binding.ownerDestination $relativeRoot } else { $binding.ownerDestination }
        if ($binding.linkedSource -cne $expectedSource -or $binding.linkedDestination -cne $expectedDestination -or
            $binding.adminName -cne [IO.Path]::GetFileName($binding.linkedSource)) { throw 'Preflight linked-worktree binding does not match its owner pair.' }
    }
    Assert-ApprovedLiveBindings @($baseline.gitBindings)
    $script:GitBindings=@($baseline.gitBindings)
    return $baseline
}

function Invoke-Preflight([object]$manifest) {
    $evidence = [IO.Path]::GetFullPath($EvidenceRoot)
    if (Test-Exists $evidence) { throw "Evidence root already exists; refusing overwrite: $evidence" }
    $pathSensitiveConfig=[Collections.Generic.List[object]]::new()
    $script:ConfigPathsChecked=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $script:ConfigFilesRead=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    for ($pairIndex=0; $pairIndex -lt $manifest.Pairs.Count; $pairIndex++) {
        $pair=$manifest.Pairs[$pairIndex]
        Assert-NoReparsePath $pair.source $true
        Assert-NoReparsePath $pair.destination $false
        if (-not [IO.Directory]::Exists($pair.source)) { throw "Source directory missing: $($pair.source)" }
        if (Test-Exists $pair.destination) { throw "Destination already exists: $($pair.destination)" }
        Assert-AclScreen $pair.source $false
        $existingDestinationParent=Split-Path -Parent $pair.destination
        while (-not [IO.Directory]::Exists($existingDestinationParent)) { $existingDestinationParent=Split-Path -Parent $existingDestinationParent }
        Assert-AclScreen $existingDestinationParent $true
        foreach ($observation in @(Get-PathAudit $pair.source 'source-ancestor') + @(Get-PathAudit $pair.destination 'destination-ancestor')) {
            $observation.pairIndex=$pairIndex
            $pathSensitiveConfig.Add($observation)
        }
        $sourceParent = [IO.Path]::GetPathRoot($pair.source)
        $destinationParent = [IO.Path]::GetPathRoot($pair.destination)
        if ($sourceParent -ine $destinationParent) { throw 'Pair is not on a single volume.' }
    }
    foreach ($observation in @(Get-UserGitConfigAudit)) { $pathSensitiveConfig.Add($observation) }
    $script:GitBindings=@(Get-GitBindings $manifest)
    foreach ($binding in $script:GitBindings) {
        $worktreeConfig=Join-Path $binding.mainSource ".git\worktrees\$($binding.adminName)\config.worktree"
        foreach ($observation in @(Get-ConfigFileAudit $worktreeConfig 'worktree-git-admin')) { $pathSensitiveConfig.Add($observation) }
    }
    [IO.Directory]::CreateDirectory($evidence) | Out-Null
    try {
        $script:InventoryDeadlineUtc=[DateTime]::UtcNow.AddMinutes(30)
        $baselinePairs = [Collections.Generic.List[object]]::new(); $capacity = [long]0
        for ($i=0; $i -lt $manifest.Pairs.Count; $i++) {
            $pair = $manifest.Pairs[$i]
            $rootIdentity=Get-DirectoryIdentity $pair.source
            $tree = Get-TreeFingerprint $pair.source $pair.destination
            Assert-DirectoryIdentity $pair.source $rootIdentity
            $capacity += $tree.bytes
            $baselinePairs.Add([ordered]@{ pairIndex=$i; source=$pair.source; destination=$pair.destination; sourceIdentity=$rootIdentity; sourceFingerprint=$tree.fingerprint; sourceBytes=$tree.bytes; hashedBytes=$tree.hashedBytes; metadataOnlyBytes=$tree.metadataOnlyBytes; entryCount=@($tree.entries).Count; entries=$tree.entries })
        }
        $volumeRoots=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $sourceVolumeRoots=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($pair in $manifest.Pairs) {
            $sourceVolume=[IO.Path]::GetPathRoot($pair.source)
            [void]$volumeRoots.Add($sourceVolume)
            [void]$sourceVolumeRoots.Add($sourceVolume)
        }
        $evidenceVolume=[IO.Path]::GetPathRoot($evidence)
        [void]$volumeRoots.Add($evidenceVolume)
        $volumes=[Collections.Generic.List[object]]::new()
        $estimatedNeed=[long]($capacity + 1048576)
        foreach ($volumeRoot in @($volumeRoots | Sort-Object)) {
            $drive=[IO.DriveInfo]::new($volumeRoot)
            $role=if ($volumeRoot -ieq $evidenceVolume -and $sourceVolumeRoots.Contains($volumeRoot)) { 'source-and-evidence' } elseif ($volumeRoot -ieq $evidenceVolume) { 'evidence' } else { 'source' }
            $volumes.Add([ordered]@{ volume=$volumeRoot; role=$role; availableBytes=$drive.AvailableFreeSpace; estimatedNeedBytes=$estimatedNeed; sufficientEstimate=($drive.AvailableFreeSpace -gt $estimatedNeed) })
        }
        $configByKey=@{}
        $uniqueConfig=[Collections.Generic.List[object]]::new()
        foreach ($observation in $pathSensitiveConfig) {
            $key="$($observation.file)|$($observation.line)|$($observation.classification)"
            if (-not $configByKey.ContainsKey($key)) {
                $configByKey[$key]=[pscustomobject]@{
                    file=$observation.file; line=$observation.line; classification=$observation.classification
                    old=[Collections.Generic.HashSet[int]]::new(); new=[Collections.Generic.HashSet[int]]::new(); userGit=$false; worktreeGitAdmin=$false
                }
            }
            $record=$configByKey[$key]
            if ($observation.origin -eq 'source-ancestor') { [void]$record.old.Add([int]$observation.pairIndex) }
            elseif ($observation.origin -eq 'destination-ancestor') { [void]$record.new.Add([int]$observation.pairIndex) }
            elseif ($observation.origin -eq 'user-git') { $record.userGit=$true }
            elseif ($observation.origin -eq 'worktree-git-admin') { $record.worktreeGitAdmin=$true }
        }
        foreach ($key in @($configByKey.Keys | Sort-Object)) {
            $record=$configByKey[$key]
            $uniqueConfig.Add([ordered]@{
                file=$record.file; line=$record.line; classification=$record.classification
                oldAncestorPairIndexes=@($record.old | Sort-Object); newAncestorPairIndexes=@($record.new | Sort-Object)
                userGitCandidate=[bool]$record.userGit; worktreeGitAdminCandidate=[bool]$record.worktreeGitAdmin
                gitConditionApplicability=if ($record.classification -eq 'git-conditional-include') { 'manual-review-required' } else { 'not-applicable-or-not-evaluated' }
            })
        }
        $metadataBytes=[long]0; $hashedBytesTotal=[long]0
        foreach ($pairReport in $baselinePairs) { $metadataBytes += [long]$pairReport.metadataOnlyBytes; $hashedBytesTotal += [long]$pairReport.hashedBytes }
        $inventoryPolicy=[ordered]@{ maxPreflightMinutes=30; maxMoveValidationMinutes=30; maxRecoveryValidationMinutes=30; fullTreePassSchedule='one preflight, one pre-journal Move, one pre-reconciliation Recover; no per-pair post-rename full pass'; postRenameValidation='source absence, destination presence, no reparse path, and unchanged root volume serial plus file ID'; externalQuiescenceRequired=$true; deadlineLimitation='deadline is checked between entries/directories, not inside a single file hash'; metadataOnlyPathSegments=@('cache','caches','.cache','node_modules','target','build','dist','bin','obj','tmp','temp'); highValueExceptions='Git admin, Cargo.lock, SQLite database and journal files are SHA-256 hashed even under generated directories'; fullyHashedBytes=$hashedBytesTotal; metadataOnlyBytes=$metadataBytes; metadataOnlyLimitation='Same-size cache byte changes with preserved timestamps and attributes can escape this inventory; the walk is not an atomic filesystem snapshot.' }
        $report = [ordered]@{ schemaVersion=1; status='ready'; runId=[Guid]::NewGuid().ToString('D'); createdUtc=[DateTime]::UtcNow.ToString('o'); manifestSha256=$manifest.Sha256; pathSensitiveConfig=@($uniqueConfig); configAudit=[ordered]@{ candidatePathsChecked=$script:ConfigPathsChecked.Count; existingFilesRead=$script:ConfigFilesRead.Count; pathSensitiveCandidateLines=$uniqueConfig.Count; scope='candidate locations and keys only; Git conditional matching and config values not evaluated' }; aclScreen='current-user-and-group-DACL-ACE-screen'; inventoryPolicy=$inventoryPolicy; capacityCheck=[ordered]@{ qualification='non-qualifying'; scope='source-and-evidence-volume-estimate-only'; sourceBytes=$capacity; estimatedNeedBytes=$estimatedNeed; requiresBackupReadbackGate=$true; volumes=@($volumes) }; backupReadbackQualification='Task-5-not-yet-implemented'; gitBindings=@($script:GitBindings); pairs=@($baselinePairs) }
        if (@($volumes | Where-Object { -not $_.sufficientEstimate }).Count -gt 0) { throw 'Estimated free space is insufficient on a source or evidence volume; full backup capacity remains unqualified.' }
        $report.baselineSha256 = Get-BaselineDigest $report.pairs $report.gitBindings
        Write-JsonAtomic (Join-Path $evidence 'preflight.json') $report
        Write-Output "Preflight ready: $evidence (pairs=$($report.pairs.Count), baseline=$($report.baselineSha256))"
    } catch {
        $failure = [ordered]@{ schemaVersion=1; status='failed'; createdUtc=[DateTime]::UtcNow.ToString('o'); error=$_.Exception.Message }
        try { Write-JsonAtomic (Join-Path $evidence 'preflight.failure.json') $failure } catch {}
        throw
    }
}

function Invoke-Move([object]$manifest) {
    if (-not $Fixture) { throw 'Live Move is blocked until Task 5 supplies an implemented, verified full backup and readback gate. Capacity estimates are not that gate.' }
    $evidence=[IO.Path]::GetFullPath($EvidenceRoot); $basePath=Join-Path $evidence 'preflight.json'; $journal=Join-Path $evidence 'journal.jsonl'
    if (Test-Exists $journal) { throw 'Journal already exists; refusing to replay Move.' }
    $baseline=Get-Baseline $manifest $basePath
    $script:InventoryDeadlineUtc=[DateTime]::UtcNow.AddMinutes(30)
    $created = [DateTime]$baseline.createdUtc
    if ($created.Kind -eq [DateTimeKind]::Unspecified) { $created = [DateTime]::SpecifyKind($created,[DateTimeKind]::Utc) }
    $baselineAge = [DateTime]::UtcNow - $created.ToUniversalTime()
    if ($baselineAge -lt [TimeSpan]::Zero -or $baselineAge -gt [TimeSpan]::FromMinutes(30)) { throw "Preflight timestamp is invalid or older than 30 minutes (age=$($baselineAge.TotalMinutes.ToString('F2'))); generate a new baseline." }
    for ($i=0; $i -lt $manifest.Pairs.Count; $i++) {
        $pair=$manifest.Pairs[$i]; Assert-Fingerprint $pair.source $pair.destination $baseline.pairs[$i].sourceFingerprint
        Assert-DirectoryIdentity $pair.source $baseline.pairs[$i].sourceIdentity
        Assert-NoReparsePath $pair.destination $false
        if (Test-Exists $pair.destination) { throw "Destination exists before move: $($pair.destination)" }
    }
    Assert-OriginalGitPointerTargets
    Add-Journal $journal ([ordered]@{ schemaVersion=1; event='session'; runId=$baseline.runId; manifestSha256=$manifest.Sha256; baselineSha256=$baseline.baselineSha256; pairCount=$manifest.Pairs.Count })
    for ($i=0; $i -lt $manifest.Pairs.Count; $i++) {
        $pair=$manifest.Pairs[$i]
        Assert-DirectoryIdentity $pair.source $baseline.pairs[$i].sourceIdentity
        Assert-NoReparsePath $pair.destination $false
        if (Test-Exists $pair.destination) { throw "Destination appeared after preflight: $($pair.destination)" }
        Add-Journal $journal ([ordered]@{ schemaVersion=1; event='move-intent'; pairIndex=$i; source=$pair.source; destination=$pair.destination; sourceFingerprint=$baseline.pairs[$i].sourceFingerprint })
        $parent=Split-Path -Parent $pair.destination
        [IO.Directory]::CreateDirectory($parent) | Out-Null
        Assert-NoReparsePath $pair.source $true
        Assert-DirectoryIdentity $pair.source $baseline.pairs[$i].sourceIdentity
        Assert-NoReparsePath $pair.destination $false
        if (Test-Exists $pair.destination) { throw "Destination appeared before rename: $($pair.destination)" }
        [IO.Directory]::Move($pair.source,$pair.destination)
        if (Test-Exists $pair.source) { throw "Source path remains after whole-root rename: $($pair.source)" }
        Assert-DirectoryIdentity $pair.destination $baseline.pairs[$i].sourceIdentity
        Add-Journal $journal ([ordered]@{ schemaVersion=1; event='move-complete'; pairIndex=$i; source=$pair.source; destination=$pair.destination; sourceFingerprint=$baseline.pairs[$i].sourceFingerprint })
    }
    Write-Output "Move complete: $($manifest.Pairs.Count) whole-root renames; run=$($baseline.runId)"
}

function Invoke-Recover([object]$manifest) {
    $evidence=[IO.Path]::GetFullPath($EvidenceRoot); $baseline=Get-Baseline $manifest (Join-Path $evidence 'preflight.json'); $journal=Join-Path $evidence 'journal.jsonl'
    $script:InventoryDeadlineUtc=[DateTime]::UtcNow.AddMinutes(30)
    if (-not (Test-Path -LiteralPath $journal -PathType Leaf)) { throw 'No journal exists; recovery cannot infer prior actions.' }
    $events=Read-Journal $journal
    if ($events.Count -eq 0 -or $events[0].event -cne 'session' -or $events[0].runId -cne $baseline.runId -or $events[0].manifestSha256 -cne $manifest.Sha256 -or $events[0].baselineSha256 -cne $baseline.baselineSha256 -or $events[0].pairCount -ne $manifest.Pairs.Count) { throw 'Journal session header does not match this baseline.' }
    $state=@{}; $order=[Collections.Generic.List[int]]::new()
    for ($i=1; $i -lt $events.Count; $i++) {
        $ev=$events[$i]; $idx=[int]$ev.pairIndex
        if ($idx -lt 0 -or $idx -ge $manifest.Pairs.Count) { throw 'Journal pair index is invalid.' }
        $pair=$manifest.Pairs[$idx]
        if ($ev.source -cne $pair.source -or $ev.destination -cne $pair.destination -or $ev.sourceFingerprint -cne $baseline.pairs[$idx].sourceFingerprint) { throw 'Journal pair details do not match baseline.' }
        if ($ev.event -ceq 'move-intent') {
            if ($state.ContainsKey($idx)) { throw 'Duplicate or out-of-order move intent.' }
            $state[$idx]='move-intent'; $order.Add($idx)
        } elseif ($ev.event -ceq 'move-complete') {
            if (-not $state.ContainsKey($idx) -or $state[$idx] -cne 'move-intent') { throw 'Completion without a matching intent.' }
            $state[$idx]='moved'
        } elseif ($ev.event -ceq 'recover-intent') {
            if (-not $state.ContainsKey($idx) -or $state[$idx] -cne 'moved') { throw 'Recovery intent without completed move.' }
            $state[$idx]='recover-intent'
        } elseif ($ev.event -ceq 'recovered') {
            if (-not $state.ContainsKey($idx) -or $state[$idx] -cne 'recover-intent') { throw 'Recovery completion without recovery intent.' }
            $state[$idx]='recovered'
        } elseif ($ev.event -ceq 'move-not-performed') {
            if (-not $state.ContainsKey($idx) -or $state[$idx] -cne 'move-intent') { throw 'Invalid move reconciliation record.' }
            $state[$idx]='not-moved'
        } else { throw "Unknown journal event: $($ev.event)" }
    }
    # Reconcile and validate every pair before writing or renaming anything.
    $reconcile=[Collections.Generic.List[object]]::new(); $toRecover=[Collections.Generic.List[int]]::new()
    for ($idx=0; $idx -lt $manifest.Pairs.Count; $idx++) {
        $pair=$manifest.Pairs[$idx]; $s=Test-Exists $pair.source; $d=Test-Exists $pair.destination; $st=$state[$idx]
        if ($null -eq $st -or $st -eq 'not-moved' -or $st -eq 'recovered') {
            if (-not $s -or $d) { throw "Unexpected source/destination state for untouched/recovered pair $idx." }
            Assert-Fingerprint $pair.source $pair.destination $baseline.pairs[$idx].sourceFingerprint
            Assert-DirectoryIdentity $pair.source $baseline.pairs[$idx].sourceIdentity
        } elseif ($st -eq 'move-intent') {
            if ($s -and -not $d) { Assert-Fingerprint $pair.source $pair.destination $baseline.pairs[$idx].sourceFingerprint; Assert-DirectoryIdentity $pair.source $baseline.pairs[$idx].sourceIdentity; $reconcile.Add([pscustomobject]@{ index=$idx; event='move-not-performed' }) }
            elseif (-not $s -and $d) { Assert-NoReparsePath $pair.source $false; Assert-Fingerprint $pair.destination $pair.source $baseline.pairs[$idx].sourceFingerprint; Assert-DirectoryIdentity $pair.destination $baseline.pairs[$idx].sourceIdentity; $reconcile.Add([pscustomobject]@{ index=$idx; event='move-complete' }); $toRecover.Add($idx) }
            else { throw "Ambiguous interrupted move state for pair $idx; no changes made." }
        } elseif ($st -eq 'moved') {
            if ($s -or -not $d) { throw "Unexpected completed-move state for pair $idx." }
            Assert-NoReparsePath $pair.source $false
            Assert-Fingerprint $pair.destination $pair.source $baseline.pairs[$idx].sourceFingerprint; Assert-DirectoryIdentity $pair.destination $baseline.pairs[$idx].sourceIdentity; $toRecover.Add($idx)
        } elseif ($st -eq 'recover-intent') {
            if ($s -and -not $d) { Assert-Fingerprint $pair.source $pair.destination $baseline.pairs[$idx].sourceFingerprint; Assert-DirectoryIdentity $pair.source $baseline.pairs[$idx].sourceIdentity; $reconcile.Add([pscustomobject]@{ index=$idx; event='recovered' }) }
            elseif (-not $s -and $d) { Assert-NoReparsePath $pair.source $false; Assert-Fingerprint $pair.destination $pair.source $baseline.pairs[$idx].sourceFingerprint; Assert-DirectoryIdentity $pair.destination $baseline.pairs[$idx].sourceIdentity; $toRecover.Add($idx) }
            else { throw "Ambiguous interrupted recovery state for pair $idx; no changes made." }
        } else { throw "Unsupported journal state for pair $idx." }
    }
    foreach ($item in $reconcile) {
        $idx=$item.index; $pair=$manifest.Pairs[$idx]
        Add-Journal $journal ([ordered]@{ schemaVersion=1; event=$item.event; pairIndex=$idx; source=$pair.source; destination=$pair.destination; sourceFingerprint=$baseline.pairs[$idx].sourceFingerprint })
        if ($item.event -eq 'move-complete') { $state[$idx]='moved' } else { $state[$idx]='not-moved' }
    }
    foreach ($idx in @($toRecover | Sort-Object -Descending)) {
        $pair=$manifest.Pairs[$idx]
        Assert-NoReparsePath $pair.source $false
        Assert-DirectoryIdentity $pair.destination $baseline.pairs[$idx].sourceIdentity
        Add-Journal $journal ([ordered]@{ schemaVersion=1; event='recover-intent'; pairIndex=$idx; source=$pair.source; destination=$pair.destination; sourceFingerprint=$baseline.pairs[$idx].sourceFingerprint })
        [IO.Directory]::CreateDirectory((Split-Path -Parent $pair.source)) | Out-Null
        Assert-NoReparsePath $pair.source $false
        Assert-NoReparsePath $pair.destination $true
        Assert-DirectoryIdentity $pair.destination $baseline.pairs[$idx].sourceIdentity
        if (Test-Exists $pair.source) { throw "Source path appeared before inverse rename: $($pair.source)" }
        [IO.Directory]::Move($pair.destination,$pair.source)
        if (Test-Exists $pair.destination) { throw "Destination path remains after inverse rename: $($pair.destination)" }
        Assert-DirectoryIdentity $pair.source $baseline.pairs[$idx].sourceIdentity
        Add-Journal $journal ([ordered]@{ schemaVersion=1; event='recovered'; pairIndex=$idx; source=$pair.source; destination=$pair.destination; sourceFingerprint=$baseline.pairs[$idx].sourceFingerprint })
    }
    Write-Output "Recovery complete: inversed $($toRecover.Count) whole-root moves; run=$($baseline.runId)"
}

try {
    Assert-InvocationScope
    $manifest=Read-Manifest
    Assert-ManifestScope $manifest
    switch ($Mode) {
        'Preflight' { Invoke-Preflight $manifest }
        'Move' { Invoke-Move $manifest }
        'Recover' { Invoke-Recover $manifest }
    }
} catch {
    [Console]::Error.WriteLine("WorkspaceConsolidation $Mode failed closed: $($_.Exception.Message)")
    exit 1
}
