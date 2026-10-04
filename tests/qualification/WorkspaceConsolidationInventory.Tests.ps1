$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$project = Join-Path $PSScriptRoot '..\..\tools\WorkspaceConsolidationInventory\WorkspaceConsolidationInventory.csproj'
$project = [IO.Path]::GetFullPath($project)
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('NeoBabylon-Inventory-A1-' + [guid]::NewGuid().ToString('N'))
$sourceRoot = Join-Path $fixtureRoot 'source'
$externalRoot = Join-Path $fixtureRoot 'outside'
$outputRoot = Join-Path $fixtureRoot 'inventory'

function Assert([bool] $condition, [string] $message) {
    if (-not $condition) { throw "ASSERTION FAILED: $message" }
}

function Invoke-Inventory([string[]] $arguments) {
    $buildRoot = Join-Path $fixtureRoot 'build'
    $captured = @(& dotnet run --project $project --no-launch-profile --artifacts-path $buildRoot -- @arguments 2>&1)
    $exitCode = $LASTEXITCODE
    [pscustomobject]@{ ExitCode = $exitCode; Output = ($captured -join [Environment]::NewLine) }
}

try {
    New-Item -ItemType Directory -Path $fixtureRoot -ErrorAction Stop | Out-Null
    New-Item -ItemType Directory -Path $sourceRoot -ErrorAction Stop | Out-Null
    New-Item -ItemType Directory -Path $externalRoot -ErrorAction Stop | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $sourceRoot 'nested') -ErrorAction Stop | Out-Null

    $targetFile = Join-Path $sourceRoot 'nested\hardlink-source.bin'
    [IO.File]::WriteAllBytes($targetFile, [byte[]](0, 1, 2, 3, 250, 255))
    New-Item -ItemType HardLink -Path (Join-Path $sourceRoot 'nested\hardlink-alias.bin') -Target $targetFile -ErrorAction Stop | Out-Null
    New-Item -ItemType HardLink -Path (Join-Path $fixtureRoot 'external-alias.bin') -Target $targetFile -ErrorAction Stop | Out-Null

    $hidden = Join-Path $sourceRoot 'nested\hidden-system.txt'
    [IO.File]::WriteAllText($hidden, 'included')
    [IO.File]::SetAttributes($hidden, [IO.FileAttributes]::Hidden -bor [IO.FileAttributes]::System)

    $junctionTarget = Join-Path $externalRoot 'target'
    New-Item -ItemType Directory -Path $junctionTarget -ErrorAction Stop | Out-Null
    [IO.File]::WriteAllText((Join-Path $junctionTarget 'must-not-be-enumerated.txt'), 'outside canary')
    New-Item -ItemType Junction -Path (Join-Path $sourceRoot 'external-junction') -Target $junctionTarget -ErrorAction Stop | Out-Null

    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class InventoryA1Native {
  [StructLayout(LayoutKind.Sequential)] public struct FILETIME { public uint Low; public uint High; }
  [StructLayout(LayoutKind.Sequential)] public struct BY_HANDLE_FILE_INFORMATION {
    public uint Attributes; public FILETIME Creation; public FILETIME Access; public FILETIME Write;
    public uint Volume; public uint SizeHigh; public uint SizeLow; public uint Links; public uint IndexHigh; public uint IndexLow;
  }
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  public static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
  [DllImport("kernel32.dll", SetLastError=true)] public static extern bool GetFileInformationByHandle(SafeFileHandle h, out BY_HANDLE_FILE_INFORMATION info);
  [DllImport("kernel32.dll", SetLastError=true)] public static extern bool DeviceIoControl(SafeFileHandle h, uint code, IntPtr input, uint inputLength, byte[] output, uint outputLength, out uint returned, IntPtr overlapped);
  public static byte[] ReadReparse(string path) {
    using (var h=CreateFile(path,0,7,IntPtr.Zero,3,0x02200000,IntPtr.Zero)) {
      if (h.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
      byte[] b=new byte[16384]; uint n;
      if (!DeviceIoControl(h,0x000900A8,IntPtr.Zero,0,b,(uint)b.Length,out n,IntPtr.Zero)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
      Array.Resize(ref b,(int)n); return b;
    }
  }
  public static BY_HANDLE_FILE_INFORMATION Info(string path) {
    using (var h=CreateFile(path,0,7,IntPtr.Zero,3,0x02200000,IntPtr.Zero)) {
      if (h.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
      BY_HANDLE_FILE_INFORMATION i; if (!GetFileInformationByHandle(h,out i)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); return i;
    }
  }
}
'@

    $baseArgs = @('--source-root', $sourceRoot, '--output-dir', $outputRoot)
    $normal = Invoke-Inventory $baseArgs
    Assert ($normal.ExitCode -eq 0) "normal fixture scan succeeds; exit=$($normal.ExitCode) output=$($normal.Output)"
    $manifestPath = Join-Path $outputRoot 'completion.json'
    $streamPath = Join-Path $outputRoot 'inventory.ndjson'
    Assert (Test-Path -LiteralPath $manifestPath -PathType Leaf) 'completed scan has completion.json'
    Assert (Test-Path -LiteralPath $streamPath -PathType Leaf) 'completed scan has inventory.ndjson'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $rows = @(Get-Content -LiteralPath $streamPath | ForEach-Object { $_ | ConvertFrom-Json })
    Assert ($manifest.complete -eq $true) 'manifest marks only complete inventory'
    Assert ($manifest.entryCount -eq $rows.Count) 'manifest entry count matches streamed rows'
    $actualStreamHash = (Get-FileHash -LiteralPath $streamPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert ($manifest.outputSha256 -eq $actualStreamHash) 'manifest digest matches exact NDJSON bytes'

    $hardRows = @($rows | Where-Object { $_.relativePath -like '*hardlink-*.bin' })
    Assert ($hardRows.Count -eq 2) 'both in-root hardlink names are inventoried'
    Assert (@($hardRows | Select-Object -ExpandProperty fileId -Unique).Count -eq 1) 'hardlink aliases share lossless file ID'
    Assert (@($hardRows | Select-Object -ExpandProperty volumeSerial -Unique).Count -eq 1) 'hardlink aliases share volume serial'
    Assert (@($hardRows | Where-Object { $_.hardLinkCount -eq 3 }).Count -eq 2) 'hardlink count includes the external fixture alias'
    Assert (($hardRows[0].fileId -match '^[0-9a-f]{32}$') -and ($hardRows[0].volumeSerial -match '^[0-9A-F]{16}$')) 'regular-file identity fields retain full native widths'
    $hiddenRow = $rows | Where-Object { $_.relativePath -like '*hidden-system.txt' }
    Assert ($null -ne $hiddenRow) 'hidden/system file is inventoried'
    Assert (($hiddenRow.attributes -band 6) -eq 6) 'hidden and system attribute bits are preserved'
    Assert (@($hardRows | Where-Object { $_.kind -eq 'file' }).Count -eq 2) 'hardlink names are classified as regular files'

    $junctionRow = $rows | Where-Object { $_.relativePath -eq 'external-junction' }
    Assert ($null -ne $junctionRow) 'junction itself is inventoried'
    Assert ($junctionRow.reparseTag -eq '0xA0000003') 'junction tag is captured'
    Assert ($junctionRow.kind -eq 'directory-reparse-point') 'junction remains a directory reparse entry'
    Assert (($junctionRow.fileId -match '^[0-9a-f]{32}$') -and ($junctionRow.volumeSerial -match '^[0-9A-F]{16}$')) 'reparse entry retains full native identity fields'
    Assert ($junctionRow.hardLinkCount -ge 1) 'reparse entry records native hard-link count'
    $rawReparse = [InventoryA1Native]::ReadReparse((Join-Path $sourceRoot 'external-junction'))
    $rawHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($rawReparse)).ToLowerInvariant()
    Assert ($junctionRow.reparseDataLength -eq $rawReparse.Length) 'reparse output records the exact returned byte length'
    Assert ($junctionRow.reparsePayloadSha256 -eq $rawHash) 'reparse output digest matches exact raw FSCTL bytes'
    Assert (@($rows | Where-Object { $_.relativePath -like 'external-junction\*must-not-be-enumerated.txt' }).Count -eq 0) 'junction target is never traversed'
    Assert (@($rows | Where-Object { $_.relativePath -eq 'nested\must-not-be-enumerated.txt' }).Count -eq 0) 'external canary is absent from source inventory'

    $collision = Invoke-Inventory $baseArgs
    Assert ($collision.ExitCode -ne 0) 'existing output collision is refused'

    $insideSourceOutput = Join-Path $sourceRoot 'out'
    $overlap = Invoke-Inventory @('--source-root', $sourceRoot, '--output-dir', $insideSourceOutput)
    Assert ($overlap.ExitCode -ne 0) 'output nested under source is refused'
    Assert (-not (Test-Path -LiteralPath $insideSourceOutput)) 'overlap refusal creates no output directory'

    $sourceInsideOutput = Join-Path $fixtureRoot 'enclosing-output'
    New-Item -ItemType Directory -Path $sourceInsideOutput | Out-Null
    $nestedSource = Join-Path $sourceInsideOutput 'source'
    New-Item -ItemType Directory -Path $nestedSource | Out-Null
    $reverseOverlap = Invoke-Inventory @('--source-root', $nestedSource, '--output-dir', $sourceInsideOutput)
    Assert ($reverseOverlap.ExitCode -ne 0) 'output enclosing source is refused'

    $projectedOut = Join-Path $fixtureRoot 'projected'
    $longPath = Invoke-Inventory @('--source-root', $sourceRoot, '--output-dir', $projectedOut, '--projected-destination-root', 'C:\projected-root', '--max-path-length', '8')
    Assert ($longPath.ExitCode -ne 0) 'projected destination path limit is enforced'
    Assert (-not (Test-Path -LiteralPath (Join-Path $projectedOut 'completion.json'))) 'path-limit refusal has no success manifest'

    $timeoutOut = Join-Path $fixtureRoot 'timeout'
    $timeout = Invoke-Inventory @('--source-root', $sourceRoot, '--output-dir', $timeoutOut, '--max-duration-seconds', '0.000001')
    Assert ($timeout.ExitCode -ne 0) 'expired duration fails the scan'
    Assert (-not (Test-Path -LiteralPath (Join-Path $timeoutOut 'completion.json'))) 'timeout has no success manifest'

    $incompleteOut = Join-Path $fixtureRoot 'incomplete'
    $incomplete = Invoke-Inventory @('--source-root', $sourceRoot, '--output-dir', $incompleteOut, '--test-mode', '--test-fail-after-rows', '2')
    Assert ($incomplete.ExitCode -ne 0) 'injected enumeration interruption fails the scan'
    Assert (-not (Test-Path -LiteralPath (Join-Path $incompleteOut 'completion.json'))) 'incomplete scan has no success manifest'

    $stressOut = Join-Path $fixtureRoot 'stress'
    $stress = Invoke-Inventory @('--source-root', $sourceRoot, '--output-dir', $stressOut, '--test-mode', '--test-synthetic-rows', '100000')
    Assert ($stress.ExitCode -eq 0) "100k-row synthetic stream succeeds; exit=$($stress.ExitCode) output=$($stress.Output)"
    $stressManifest = Get-Content -LiteralPath (Join-Path $stressOut 'completion.json') -Raw | ConvertFrom-Json
    Assert ($stressManifest.entryCount -ge 100000) 'synthetic stress run streams at least 100,000 rows'
    "STRESS RESULT: $($stress.Output)"

    $releaseBuild = @(& dotnet build $project --configuration Release --artifacts-path (Join-Path $fixtureRoot 'release-artifacts') 2>&1)
    $releaseExitCode = $LASTEXITCODE
    Assert ($releaseExitCode -eq 0) "Release build succeeds; exit=$releaseExitCode output=$($releaseBuild -join [Environment]::NewLine)"
    $releaseSummary = $releaseBuild | Where-Object { $_ -match 'Build succeeded|Warning\(s\)|Error\(s\)' }
    "RELEASE BUILD: $($releaseSummary -join '; ')"

    'PASS fixture-only WorkspaceConsolidationInventory A1 qualification'
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        $resolvedFixture = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $fixtureRoot).Path)
        $tempPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if (-not $resolvedFixture.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing cleanup outside temp root: $resolvedFixture"
        }
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
}
