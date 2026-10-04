[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$RuntimeRoot,
    [string]$ReportDate
)

$ErrorActionPreference = 'Stop'

function Invoke-ReadOnlyGit {
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $output = & git --no-optional-locks -C $Repository @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Local Git command failed in '$Repository': git $($Arguments -join ' ')`n$($output -join [Environment]::NewLine)"
    }

    return [string]::Join("`n", @($output | ForEach-Object { [string]$_ }))
}

function Get-GitDiffSha256 {
    param([Parameter(Mandatory = $true)][string]$Repository)

    $gitPath = (Get-Command git -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $gitPath
    $startInfo.Arguments = '--no-optional-locks diff --no-ext-diff --binary --no-renames HEAD'
    $startInfo.WorkingDirectory = $Repository
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    $process = [Diagnostics.Process]::new()
    $buffer = [IO.MemoryStream]::new()
    try {
        $process.StartInfo = $startInfo
        if (-not $process.Start()) {
            throw 'Could not start local Git to fingerprint the tracked diff.'
        }
        $errorTask = $process.StandardError.ReadToEndAsync()
        $process.StandardOutput.BaseStream.CopyTo($buffer)
        $process.WaitForExit()
        $errorText = $errorTask.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            throw "Local Git diff fingerprint failed: $errorText"
        }

        $sha256 = [Security.Cryptography.SHA256]::Create()
        try {
            $hashBytes = $sha256.ComputeHash($buffer.ToArray())
        }
        finally {
            $sha256.Dispose()
        }
        return [BitConverter]::ToString($hashBytes).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $buffer.Dispose()
        $process.Dispose()
    }
}

function Get-SortedUniquePaths {
    param([string[]]$Paths)

    $normalized = @(
        $Paths |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            ForEach-Object { $_ -replace '\\', '/' } |
            Sort-Object -Unique -CaseSensitive
    )
    [Array]::Sort($normalized, [StringComparer]::Ordinal)
    return ,$normalized
}

function Get-PathHash {
    param([string]$Repository, [string]$RelativePath)

    $fullPath = Join-Path $Repository ($RelativePath -replace '/', '\')
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        return 'deleted'
    }
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $fullPath).Hash.ToLowerInvariant()
}

if ($ReportDate) {
    $parsedReportDate = [DateTime]::MinValue
    $validReportDate = [DateTime]::TryParseExact(
        $ReportDate,
        'yyyy-MM-dd',
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::None,
        [ref]$parsedReportDate)
    if (-not $validReportDate) {
        throw "ReportDate must be a valid calendar date in yyyy-MM-dd format; received '$ReportDate'."
    }
}

$rootPath = (Resolve-Path -LiteralPath $Root).Path
$manifestPath = Join-Path $rootPath 'reference\sources.json'
$lockPath = Join-Path $rootPath 'runtime\runtime-lock.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Reference manifest not found: $manifestPath"
}
if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
    throw "Runtime lock not found: $lockPath"
}

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$runtimeLock = Get-Content -Raw -LiteralPath $lockPath | ConvertFrom-Json
$baselineRecords = @($manifest.records | Where-Object {
    $_.kind -eq 'upstream-source' -and $_.role -eq 'selected Phase 0 baseline'
})
if ($baselineRecords.Count -ne 1) {
    throw "Expected exactly one selected Phase 0 upstream-source record; found $($baselineRecords.Count)."
}
$baseline = $baselineRecords[0]
if (-not $runtimeLock.runtime.sourceCheckoutRelativePath) {
    throw 'Runtime lock has no sourceCheckoutRelativePath.'
}

if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $runtimePath = Join-Path $rootPath ($runtimeLock.runtime.sourceCheckoutRelativePath -replace '/', '\')
}
else {
    $runtimePath = $RuntimeRoot
}
if (-not (Test-Path -LiteralPath $runtimePath -PathType Container)) {
    throw "Local runtime source checkout not found: $runtimePath"
}
$runtimePath = (Resolve-Path -LiteralPath $runtimePath).Path

$gitRoot = Invoke-ReadOnlyGit -Repository $runtimePath -Arguments @('rev-parse', '--show-toplevel')
$gitRoot = (Resolve-Path -LiteralPath $gitRoot.Trim()).Path
if (-not [string]::Equals($gitRoot, $runtimePath, [StringComparison]::OrdinalIgnoreCase)) {
    throw "RuntimeRoot must be the Git worktree root. Git reports '$gitRoot' for '$runtimePath'."
}

$runtimeHead = (Invoke-ReadOnlyGit -Repository $runtimePath -Arguments @('rev-parse', 'HEAD')).Trim().ToLowerInvariant()
$trackedOutput = Invoke-ReadOnlyGit -Repository $runtimePath -Arguments @('diff', '--no-ext-diff', '--name-only', '--no-renames', 'HEAD')
$trackedPaths = Get-SortedUniquePaths -Paths @($trackedOutput -split "`n")
$untrackedOutput = Invoke-ReadOnlyGit -Repository $runtimePath -Arguments @('ls-files', '--others', '--exclude-standard')
$untrackedPaths = Get-SortedUniquePaths -Paths @($untrackedOutput -split "`n")
$diffHash = Get-GitDiffSha256 -Repository $runtimePath

$revisionAgreement = [string]::Equals(
    [string]$baseline.commit,
    [string]$runtimeLock.runtime.sourceRevision,
    [StringComparison]::OrdinalIgnoreCase
)
$expectedPatchHash = [string]$runtimeLock.runtime.sourcePatchSha256
$patchFingerprintStatus = if ([string]::IsNullOrWhiteSpace($expectedPatchHash)) {
    'not recorded'
}
elseif ([string]::Equals($expectedPatchHash, $diffHash, [StringComparison]::OrdinalIgnoreCase)) {
    'yes'
}
else {
    'no'
}
$expectedPatchFingerprintDisplay = if ([string]::IsNullOrWhiteSpace($expectedPatchHash)) {
    'not recorded'
}
else {
    "``$expectedPatchHash``"
}
$report = [System.Collections.Generic.List[string]]::new()
$report.Add('# Upstream comparison')
$report.Add('')
if ($ReportDate) {
    $report.Add("Report date: $ReportDate")
    $report.Add('')
}
$report.Add('## Pinned source')
$report.Add('')
$report.Add("- Manifest record: ``$($baseline.id)`` ($($baseline.release))")
$report.Add("- Expected revision (manifest): ``$($baseline.commit)``")
$report.Add("- Runtime-lock revision: ``$($runtimeLock.runtime.sourceRevision)``")
$report.Add("- Runtime HEAD: ``$runtimeHead``")
$report.Add("- Manifest/runtime-lock revision agreement: $(if ($revisionAgreement) { 'yes' } else { 'no' })")
$report.Add("- Locked runtime version: ``$($runtimeLock.runtime.version)``")
$report.Add('')
$report.Add('## Local runtime changes')
$report.Add('')
$report.Add("- Tracked changed files: $($trackedPaths.Count)")
if ($trackedPaths.Count -eq 0) {
    $report.Add('  - none')
}
else {
    foreach ($path in $trackedPaths) {
        $report.Add("  - ``$path`` (sha256 ``$(Get-PathHash -Repository $runtimePath -RelativePath $path)``)")
    }
}
$report.Add("- Untracked, non-ignored files: $($untrackedPaths.Count)")
if ($untrackedPaths.Count -eq 0) {
    $report.Add('  - none')
}
else {
    foreach ($path in $untrackedPaths) {
        $report.Add("  - ``$path`` (sha256 ``$(Get-PathHash -Repository $runtimePath -RelativePath $path)``)")
    }
}
$report.Add("- Expected tracked diff fingerprint (runtime lock): $expectedPatchFingerprintDisplay")
$report.Add("- Tracked diff fingerprint matches runtime lock: $patchFingerprintStatus")
$report.Add("- Actual tracked diff fingerprint (sha256): ``$diffHash``")
$report.Add('')
$report.Add('## Fixed review actions')
$report.Add('')
$report.Add('- Review every tracked change against the pinned upstream revision; this report is not approval.')
$report.Add('- Review untracked files and hashes before deciding whether they belong in the runtime source tree.')
$report.Add('- Resolve any manifest/runtime-lock revision mismatch manually; this generator never updates either record.')
$report.Add('- Qualify built binaries and runtime behavior separately; source comparison does not establish either.')

Write-Output ([string]::Join("`n", $report))
