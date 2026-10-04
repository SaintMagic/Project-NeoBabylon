$ErrorActionPreference = 'Stop'

function Assert-Equal {
    param(
        [string]$Name,
        [AllowNull()][object]$Expected,
        [AllowNull()][object]$Actual
    )

    if ($Expected -cne $Actual) {
        throw "$Name`nExpected: [$Expected]`nActual:   [$Actual]"
    }
}

function Invoke-Git {
    param([string]$Repository, [string[]]$Arguments)
    $output = & git -C $Repository @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed in '$Repository': $($output -join [Environment]::NewLine)"
    }
    return (@($output) -join [Environment]::NewLine).Trim()
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'The upstream comparison fixture requires the locally installed Git executable.'
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$generatorPath = Join-Path $projectRoot 'scripts\compare-upstream.ps1'
$goldenPath = Join-Path $projectRoot 'tests\fixtures\upstream-comparison\expected-report.md'
$fixtureBase = Join-Path ([IO.Path]::GetTempPath()) ('NeoBabylon-UpstreamComparison-' + [Guid]::NewGuid().ToString('N'))
$fixtureProject = Join-Path $fixtureBase 'NeoBabylon'
$fixtureRuntime = Join-Path $fixtureBase 'NeoBabylon-Runtime'
$previousGitOptionalLocks = $env:GIT_OPTIONAL_LOCKS
$previousGitAuthorName = $env:GIT_AUTHOR_NAME
$previousGitAuthorEmail = $env:GIT_AUTHOR_EMAIL
$previousGitCommitterName = $env:GIT_COMMITTER_NAME
$previousGitCommitterEmail = $env:GIT_COMMITTER_EMAIL
$previousGitAuthorDate = $env:GIT_AUTHOR_DATE
$previousGitCommitterDate = $env:GIT_COMMITTER_DATE
$env:GIT_OPTIONAL_LOCKS = '0'
$env:GIT_AUTHOR_NAME = 'NeoBabylon fixture'
$env:GIT_AUTHOR_EMAIL = 'fixture@example.invalid'
$env:GIT_COMMITTER_NAME = 'NeoBabylon fixture'
$env:GIT_COMMITTER_EMAIL = 'fixture@example.invalid'
$env:GIT_AUTHOR_DATE = '2000-01-01T00:00:00Z'
$env:GIT_COMMITTER_DATE = '2000-01-01T00:00:00Z'

try {
    New-Item -ItemType Directory -Path (Join-Path $fixtureProject 'reference') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $fixtureProject 'runtime') -Force | Out-Null
    New-Item -ItemType Directory -Path $fixtureRuntime -Force | Out-Null

    $manifest = [ordered]@{
        schemaVersion = 1
        project = 'NeoBabylon'
        generatedAt = '2000-01-01T00:00:00Z'
        records = @(
            [ordered]@{
                id = 'CODEX-SOURCE-FIXTURE'
                kind = 'upstream-source'
                status = 'captured'
                role = 'selected Phase 0 baseline'
                release = 'rust-v9.8.7'
                commit = '0123456789abcdef0123456789abcdef01234567'
                localPath = 'reference/cache/codex/rust-v9.8.7/source'
                artifacts = @()
            }
        )
    }
    $lock = [ordered]@{
        schemaVersion = 1
        product = 'NeoBabylon'
        runtime = [ordered]@{
            kind = 'Codex App Server'
            version = '9.8.7'
            sourceRepository = 'https://github.com/openai/codex.git'
            sourceRef = 'rust-v9.8.7'
            sourceRevision = '0123456789abcdef0123456789abcdef01234567'
            sourceCheckoutRelativePath = '..\NeoBabylon-Runtime'
            appServerBinaryRelativePath = '..\NeoBabylon-Data\candidate\codex-app-server.exe'
            platform = 'x86_64-pc-windows-msvc'
            sha256 = ('a' * 64)
            protocol = 'stable'
            execution = 'fixture-only'
            sourcePatchSha256 = ('b' * 64)
        }
    }
    $manifestPath = Join-Path $fixtureProject 'reference\sources.json'
    $lockPath = Join-Path $fixtureProject 'runtime\runtime-lock.json'
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 20) + "`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($lockPath, ($lock | ConvertTo-Json -Depth 20) + "`n", [Text.UTF8Encoding]::new($false))

    Invoke-Git $fixtureRuntime @('init', '--quiet') | Out-Null
    Invoke-Git $fixtureRuntime @('config', 'core.autocrlf', 'false') | Out-Null
    $trackedPath = Join-Path $fixtureRuntime 'tracked.txt'
    [IO.File]::WriteAllText($trackedPath, "baseline`n", [Text.UTF8Encoding]::new($false))
    Invoke-Git $fixtureRuntime @('add', '--', 'tracked.txt') | Out-Null
    Invoke-Git $fixtureRuntime @('commit', '--quiet', '-m', 'fixture baseline') | Out-Null
    $actualRevision = Invoke-Git $fixtureRuntime @('rev-parse', 'HEAD')
    $manifest.records[0].commit = $actualRevision
    $lock.runtime.sourceRevision = $actualRevision
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 20) + "`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($lockPath, ($lock | ConvertTo-Json -Depth 20) + "`n", [Text.UTF8Encoding]::new($false))

    [IO.File]::WriteAllText($trackedPath, "baseline`ntracked change`n", [Text.UTF8Encoding]::new($false))
    $untrackedPath = Join-Path $fixtureRuntime 'untracked.txt'
    [IO.File]::WriteAllText($untrackedPath, "untracked fixture`n", [Text.UTF8Encoding]::new($false))

    $before = [ordered]@{
        manifest = (Get-FileHash -Algorithm SHA256 -LiteralPath $manifestPath).Hash
        lock = (Get-FileHash -Algorithm SHA256 -LiteralPath $lockPath).Hash
        tracked = (Get-FileHash -Algorithm SHA256 -LiteralPath $trackedPath).Hash
        untracked = (Get-FileHash -Algorithm SHA256 -LiteralPath $untrackedPath).Hash
        head = Invoke-Git $fixtureRuntime @('rev-parse', 'HEAD')
        status = Invoke-Git $fixtureRuntime @('status', '--porcelain=v1', '--untracked-files=all')
    }

    $first = @(& $generatorPath -Root $fixtureProject -RuntimeRoot $fixtureRuntime 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "First generator run failed: $($first -join [Environment]::NewLine)"
    }
    $second = @(& $generatorPath -Root $fixtureProject -RuntimeRoot $fixtureRuntime 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Second generator run failed: $($second -join [Environment]::NewLine)"
    }

    $firstReport = @($first | ForEach-Object { [string]$_ }) -join "`n"
    $secondReport = @($second | ForEach-Object { [string]$_ }) -join "`n"
    $expectedReport = [IO.File]::ReadAllText($goldenPath).TrimEnd("`r", "`n")
    Assert-Equal 'First run matches the hand-authored golden report' $expectedReport $firstReport
    Assert-Equal 'Repeated run is byte-for-byte deterministic' $firstReport $secondReport

    $invalidDateAccepted = $false
    try {
        & $generatorPath -Root $fixtureProject -RuntimeRoot $fixtureRuntime -ReportDate '2026-99-99' | Out-Null
        $invalidDateAccepted = $true
    }
    catch {
        $invalidDateAccepted = $false
    }
    Assert-Equal 'Impossible explicit report dates are rejected' $false $invalidDateAccepted

    $after = [ordered]@{
        manifest = (Get-FileHash -Algorithm SHA256 -LiteralPath $manifestPath).Hash
        lock = (Get-FileHash -Algorithm SHA256 -LiteralPath $lockPath).Hash
        tracked = (Get-FileHash -Algorithm SHA256 -LiteralPath $trackedPath).Hash
        untracked = (Get-FileHash -Algorithm SHA256 -LiteralPath $untrackedPath).Hash
        head = Invoke-Git $fixtureRuntime @('rev-parse', 'HEAD')
        status = Invoke-Git $fixtureRuntime @('status', '--porcelain=v1', '--untracked-files=all')
    }
    Assert-Equal 'Manifest, lock, and runtime inputs remain unchanged' ($before | ConvertTo-Json -Compress) ($after | ConvertTo-Json -Compress)

    Write-Output 'PASS deterministic upstream comparison fixture (2 runs, exact golden, unchanged inputs).'
}
finally {
    $env:GIT_OPTIONAL_LOCKS = $previousGitOptionalLocks
    $env:GIT_AUTHOR_NAME = $previousGitAuthorName
    $env:GIT_AUTHOR_EMAIL = $previousGitAuthorEmail
    $env:GIT_COMMITTER_NAME = $previousGitCommitterName
    $env:GIT_COMMITTER_EMAIL = $previousGitCommitterEmail
    $env:GIT_AUTHOR_DATE = $previousGitAuthorDate
    $env:GIT_COMMITTER_DATE = $previousGitCommitterDate
    if (Test-Path -LiteralPath $fixtureBase -PathType Container) {
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
        $resolvedFixture = (Resolve-Path -LiteralPath $fixtureBase).Path
        if (-not $resolvedFixture.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or
            (Split-Path -Leaf $resolvedFixture) -notmatch '^NeoBabylon-UpstreamComparison-[0-9a-f]{32}$') {
            throw "Refusing to remove unexpected fixture path '$resolvedFixture'."
        }
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
}
