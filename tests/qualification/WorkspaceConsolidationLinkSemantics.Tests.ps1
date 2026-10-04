$ErrorActionPreference = 'Stop'

if (-not ('WorkspaceConsolidationLinkSemantics.Native' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace WorkspaceConsolidationLinkSemantics {
  [StructLayout(LayoutKind.Sequential)] public struct FileTime { public uint Low; public uint High; }
  [StructLayout(LayoutKind.Sequential)] public struct FileInfo {
    public uint Attributes; public FileTime Creation; public FileTime Access; public FileTime Write;
    public uint VolumeSerial; public uint SizeHigh; public uint SizeLow; public uint Links;
    public uint IndexHigh; public uint IndexLow;
  }
  public static class Native {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    public static extern bool CreateHardLink(string newName, string existingName, IntPtr security);
    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInfo info);
    public static string Error() { return new Win32Exception(Marshal.GetLastWin32Error()).Message; }
  }
}
'@
}

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('NeoBabylon-WorkspaceConsolidationLinks-' + [Guid]::NewGuid().ToString('N'))
$root = [IO.Path]::GetFullPath($fixtureRoot)
$temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$passed = 0
function Assert-That([string]$Name, [bool]$Condition, [string]$Detail = '') {
    if (-not $Condition) { throw "FAIL: $Name $Detail" }
    $script:passed++
}
function Get-FileIdentity([string]$Path) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
    try {
        $info = New-Object WorkspaceConsolidationLinkSemantics.FileInfo
        if (-not [WorkspaceConsolidationLinkSemantics.Native]::GetFileInformationByHandle($stream.SafeFileHandle, [ref]$info)) {
            throw "GetFileInformationByHandle failed for '$Path': $([WorkspaceConsolidationLinkSemantics.Native]::Error())"
        }
        [pscustomobject]@{ Volume = $info.VolumeSerial.ToString('x8'); FileId = ($info.IndexHigh.ToString('x8') + $info.IndexLow.ToString('x8')); Links = [int]$info.Links }
    } finally { $stream.Dispose() }
}
function Get-ReparseMetadata([string]$Path) {
    $item = Get-Item -LiteralPath $Path -Force
    [pscustomobject]@{ Type = [string]$item.LinkType; Attributes = [string]$item.Attributes; RawTarget = [string]$item.LinkTarget }
}

try {
    if ([IO.Path]::GetDirectoryName($root) -ine $temp -or [IO.Path]::GetFileName($root) -notmatch '^NeoBabylon-WorkspaceConsolidationLinks-[0-9a-f]{32}$') {
        throw "Refusing fixture path outside a unique direct temp child: '$root'."
    }
    New-Item -ItemType Directory -Path $root | Out-Null
    $source = Join-Path $root 'source'
    $moved = Join-Path $root 'renamed'
    $insideTarget = Join-Path $source 'inside-target'
    $externalTarget = Join-Path $root 'external-target'
    New-Item -ItemType Directory -Path $insideTarget,$externalTarget | Out-Null

    $primary = Join-Path $source 'hardlink-primary.bin'
    $insideAlias = Join-Path $source 'hardlink-inside-alias.bin'
    $outsideAlias = Join-Path $root 'hardlink-outside-alias.bin'
    $ordinary = Join-Path $source 'ordinary.txt'
    $sentinel = Join-Path $externalTarget 'sentinel.txt'
    [IO.File]::WriteAllText($primary, 'shared inode payload')
    [IO.File]::WriteAllText($ordinary, 'ordinary fixture payload')
    [IO.File]::WriteAllText((Join-Path $insideTarget 'target.txt'), 'internal absolute target')
    [IO.File]::WriteAllText($sentinel, 'external sentinel unchanged')
    foreach ($alias in @($insideAlias,$outsideAlias)) {
        if (-not [WorkspaceConsolidationLinkSemantics.Native]::CreateHardLink($alias,$primary,[IntPtr]::Zero)) {
            throw "FAIL-CLOSED: could not create hardlink '$alias': $([WorkspaceConsolidationLinkSemantics.Native]::Error())"
        }
    }

    $internalLink = Join-Path $source 'absolute-internal-junction'
    $externalLink = Join-Path $source 'absolute-external-junction'
    foreach ($spec in @(@($internalLink,$insideTarget),@($externalLink,$externalTarget))) {
        $output = & $env:ComSpec /d /c mklink /J "`"$($spec[0])`"" "`"$($spec[1])`"" 2>&1
        if ($LASTEXITCODE -ne 0) { throw "FAIL-CLOSED: could not create junction '$($spec[0])' -> '$($spec[1])': $($output -join ' ')" }
    }
    $internalMetadata = Get-ReparseMetadata $internalLink
    $externalMetadata = Get-ReparseMetadata $externalLink
    Assert-That 'internal junction stores absolute target' ([IO.Path]::IsPathRooted($internalMetadata.RawTarget)) "raw='$($internalMetadata.RawTarget)'"
    Assert-That 'external junction stores absolute target' ([IO.Path]::IsPathRooted($externalMetadata.RawTarget)) "raw='$($externalMetadata.RawTarget)'"
    $externalBefore = (Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash

    $files = @($primary,$insideAlias,$ordinary)
    $baseline = @{}
    foreach ($path in $files) {
        $baseline[[IO.Path]::GetFileName($path)] = [pscustomobject]@{
            Hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
            Identity = Get-FileIdentity $path
        }
    }
    $baselineOutside = [pscustomobject]@{ Hash = (Get-FileHash -LiteralPath $outsideAlias -Algorithm SHA256).Hash; Identity = Get-FileIdentity $outsideAlias }
    $observations = [System.Collections.Generic.List[object]]::new()
    $reparseObservations = [System.Collections.Generic.List[object]]::new()
    foreach ($entry in @(@('before','absolute-internal-junction',$internalMetadata),@('before','absolute-external-junction',$externalMetadata))) {
        $reparseObservations.Add([pscustomobject]@{ Stage = $entry[0]; Name = $entry[1]; Type = $entry[2].Type; Attributes = $entry[2].Attributes; RawTarget = $entry[2].RawTarget })
    }
    Assert-That 'fixture hardlink names share identity and link count three' ($baseline['hardlink-primary.bin'].Identity.FileId -eq $baseline['hardlink-inside-alias.bin'].Identity.FileId -and $baseline['hardlink-primary.bin'].Identity.Volume -eq $baselineOutside.Identity.Volume -and $baseline['hardlink-primary.bin'].Identity.Links -eq 3 -and $baselineOutside.Identity.Links -eq 3)
    Assert-That 'fixture lives on one volume' ((Get-FileIdentity $primary).Volume -eq (Get-FileIdentity $sentinel).Volume)

    foreach ($destination in @($moved,$source)) {
        if ($destination -eq $moved) { Move-Item -LiteralPath $source -Destination $moved }
        else { Move-Item -LiteralPath $moved -Destination $source }
        $currentRoot = $destination
        foreach ($name in $baseline.Keys) {
            $path = Join-Path $currentRoot $name
            $now = Get-FileIdentity $path
            $before = $baseline[$name]
            Assert-That "$destination preserves $name content hash" ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $before.Hash)
            Assert-That "$destination preserves $name volume and file ID" ($now.Volume -eq $before.Identity.Volume -and $now.FileId -eq $before.Identity.FileId)
            Assert-That "$destination preserves $name link count" ($now.Links -eq $before.Identity.Links)
            $observations.Add([pscustomobject]@{ Stage = if ($destination -eq $moved) { 'after-forward-rename' } else { 'after-inverse-rename' }; Name = $name; Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash; VolumeSerial = $now.Volume; FileId = $now.FileId; LinkCount = $now.Links })
        }
        $internalNow = Get-ReparseMetadata (Join-Path $currentRoot 'absolute-internal-junction')
        $externalNow = Get-ReparseMetadata (Join-Path $currentRoot 'absolute-external-junction')
        Assert-That "$destination preserves internal junction reparse metadata" ($internalNow.Type -ceq $internalMetadata.Type -and $internalNow.Attributes -ceq $internalMetadata.Attributes -and $internalNow.RawTarget -ceq $internalMetadata.RawTarget) "before='$($internalMetadata | ConvertTo-Json -Compress)' after='$($internalNow | ConvertTo-Json -Compress)'"
        Assert-That "$destination preserves external junction reparse metadata" ($externalNow.Type -ceq $externalMetadata.Type -and $externalNow.Attributes -ceq $externalMetadata.Attributes -and $externalNow.RawTarget -ceq $externalMetadata.RawTarget) "before='$($externalMetadata | ConvertTo-Json -Compress)' after='$($externalNow | ConvertTo-Json -Compress)'"
        foreach ($entry in @(@('absolute-internal-junction',$internalNow),@('absolute-external-junction',$externalNow))) {
            $reparseObservations.Add([pscustomobject]@{ Stage = if ($destination -eq $moved) { 'after-forward-rename' } else { 'after-inverse-rename' }; Name = $entry[0]; Type = $entry[1].Type; Attributes = $entry[1].Attributes; RawTarget = $entry[1].RawTarget })
        }
        Assert-That "$destination external junction still reaches sentinel" ((Get-Content -LiteralPath (Join-Path (Join-Path $currentRoot 'absolute-external-junction') 'sentinel.txt') -Raw) -eq 'external sentinel unchanged')
        Assert-That "$destination internal absolute target resolves only after inverse rename" ((Test-Path -LiteralPath (Join-Path (Join-Path $currentRoot 'absolute-internal-junction') 'target.txt')) -eq ($destination -eq $source))
        Assert-That "$destination preserves outside hardlink alias identity and count" ((Get-FileIdentity $outsideAlias).FileId -eq $baselineOutside.Identity.FileId -and (Get-FileIdentity $outsideAlias).Links -eq 3 -and (Get-FileHash -LiteralPath $outsideAlias -Algorithm SHA256).Hash -eq $baselineOutside.Hash)
        Assert-That "$destination preserves external sentinel" ((Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash -eq $externalBefore)
    }
    Assert-That 'inverse rename restores source and removes renamed path' ((Test-Path -LiteralPath $source -PathType Container) -and -not (Test-Path -LiteralPath $moved))
    $identityEvidence = foreach ($name in $baseline.Keys) { [pscustomobject]@{ Stage = 'before'; Name = $name; Sha256 = $baseline[$name].Hash; VolumeSerial = $baseline[$name].Identity.Volume; FileId = $baseline[$name].Identity.FileId; LinkCount = $baseline[$name].Identity.Links } }
    $identityEvidence += $observations.ToArray()
    Write-Output ('IDENTITY_EVIDENCE=' + ($identityEvidence | ConvertTo-Json -Compress -Depth 4))
    Write-Output ('REPARSE_EVIDENCE=' + ($reparseObservations.ToArray() | ConvertTo-Json -Compress -Depth 4))
    Write-Output "PASS fixture-only Windows workspace link rename semantics ($passed assertions)"
} catch {
    Write-Error $_
    exit 1
} finally {
    if (Test-Path -LiteralPath $root) {
        if ([IO.Path]::GetDirectoryName($root) -ine $temp -or [IO.Path]::GetFileName($root) -notmatch '^NeoBabylon-WorkspaceConsolidationLinks-[0-9a-f]{32}$') {
            throw "Refusing to remove unexpected fixture path '$root'."
        }
        Remove-Item -LiteralPath $root -Recurse -Force
    }
}
