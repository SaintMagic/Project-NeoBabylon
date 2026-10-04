param([switch] $OpenRouterCredentialOnly, [switch] $LegacyUnsafeResearch, [switch] $PathCheckOnly, [switch] $RunRootJunctionCheckOnly)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$runId = [Guid]::NewGuid().ToString('N')
$runName = 'NeoBabylon-Qualification-Probe-' + $runId
$probeSourcesRoot = Join-Path $repositoryRoot '.local\Lab\ProbeSources'
$runRoot = Join-Path $probeSourcesRoot $runName
$sourceRoot = $runRoot
$applicationRoot = Join-Path (Join-Path (Join-Path $repositoryRoot '.local\Lab\Runs') $runId) 'App'
$artifactPath = Join-Path $sourceRoot 'artifacts\phase1a\lmstudio\qualification.json'
$openRouterArtifactPath = Join-Path $sourceRoot 'artifacts\phase1b\openrouter\qualification.json'
$runtimeDirectory = Join-Path $sourceRoot 'runtime'

if ($RunRootJunctionCheckOnly) {
    $runnerProject = Join-Path $repositoryRoot 'tools\Phase1AQualification\NeoBabylon.Phase1AQualification.csproj'
    & dotnet build $runnerProject --configuration Release --no-restore | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The offline qualification runner build failed.' }
    $runner = Join-Path $repositoryRoot 'tools\Phase1AQualification\bin\Release\net10.0\NeoBabylon.Phase1AQualification.exe'
    $fixtureRoot = Join-Path $repositoryRoot ('.local\QA\Workspace-Consolidation-Execution\task-2-junction-' + $runId)
    $fixtureSource = Join-Path $fixtureRoot 'source'
    $outsideRoot = Join-Path $fixtureRoot 'outside'
    $junction = Join-Path $fixtureSource '.local\Lab\Runs'
    New-Item -ItemType Directory -Path (Split-Path $junction -Parent) -Force | Out-Null
    New-Item -ItemType Directory -Path $outsideRoot -Force | Out-Null
    $canary = Join-Path $outsideRoot 'outside-canary.txt'
    Set-Content -LiteralPath $canary -Value 'UNCHANGED_OUTSIDE_CANARY' -NoNewline
    New-Item -ItemType Junction -Path $junction -Target $outsideRoot | Out-Null
    try {
        $start = [Diagnostics.ProcessStartInfo]::new($runner)
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $start.Environment['NEOBABYLON_SOURCE_ROOT'] = $fixtureSource
        $start.Environment['NEOBABYLON_APPLICATION_ROOT'] = '   '
        $start.ArgumentList.Add('--mock-only')
        $child = [Diagnostics.Process]::Start($start)
        try {
            if (-not $child.WaitForExit(10000)) {
                $child.Kill()
                throw 'The offline junction fixture runner did not exit promptly.'
            }
            $diagnostic = $child.StandardOutput.ReadToEnd() + $child.StandardError.ReadToEnd()
            $outsideEntries = @(Get-ChildItem -LiteralPath $outsideRoot -Force | ForEach-Object Name)
            if ($outsideEntries.Count -ne 1 -or $outsideEntries[0] -ne 'outside-canary.txt' -or
                (Get-Content -LiteralPath $canary -Raw) -ne 'UNCHANGED_OUTSIDE_CANARY') {
                throw "Junction redirected a run root or claim outside the fixture source: $($outsideEntries -join ', ')"
            }
            if ($child.ExitCode -eq 0 -or $diagnostic -notmatch '(?i)reparse|junction') {
                throw "The runner did not reject the junction before reservation: $diagnostic"
            }
            Write-Output 'PASS junction rejected before any outside run root or claim; canary unchanged'
        }
        finally { $child.Dispose() }

        $explicitRoot = Join-Path $fixtureRoot 'explicit-App'
        $start = [Diagnostics.ProcessStartInfo]::new($runner)
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $start.Environment['NEOBABYLON_SOURCE_ROOT'] = $fixtureSource
        $start.Environment['NEOBABYLON_APPLICATION_ROOT'] = $explicitRoot
        $start.ArgumentList.Add('--mock-only')
        $child = [Diagnostics.Process]::Start($start)
        try {
            if (-not $child.WaitForExit(10000)) { $child.Kill(); throw 'Explicit-root offline runner timed out.' }
            $null = $child.StandardOutput.ReadToEnd()
            $null = $child.StandardError.ReadToEnd()
            $explicitArtifact = Join-Path $fixtureSource 'artifacts\phase1a\lmstudio\qualification.json'
            $explicitEvidence = Get-Content -LiteralPath $explicitArtifact -Raw | ConvertFrom-Json
            if ($child.ExitCode -ne 1 -or $explicitEvidence.applicationRoot -ne [IO.Path]::GetFullPath($explicitRoot) -or
                @(Get-ChildItem -LiteralPath $outsideRoot -Force).Count -ne 1) {
                throw 'Explicit application-root selection changed or wrote through the junction.'
            }
            Write-Output 'PASS explicit application root unchanged while default Runs parent is a junction'
        }
        finally { $child.Dispose() }
    }
    finally {
        $link = Get-Item -LiteralPath $junction -Force -ErrorAction SilentlyContinue
        if ($link -and ($link.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            Remove-Item -LiteralPath $junction -Force
        }
    }

    New-Item -ItemType Directory -Path $junction | Out-Null
    $seenRoots = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $lmArtifact = Join-Path $fixtureSource 'artifacts\phase1a\lmstudio\qualification.json'
    foreach ($attempt in 1..3) {
        $start = [Diagnostics.ProcessStartInfo]::new($runner)
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $start.Environment['NEOBABYLON_SOURCE_ROOT'] = $fixtureSource
        $start.Environment['NEOBABYLON_APPLICATION_ROOT'] = '   '
        $start.ArgumentList.Add('--mock-only')
        $child = [Diagnostics.Process]::Start($start)
        try {
            if (-not $child.WaitForExit(10000)) { $child.Kill(); throw 'Repeated offline runner timed out.' }
            $null = $child.StandardOutput.ReadToEnd()
            $null = $child.StandardError.ReadToEnd()
            if ($child.ExitCode -ne 1) { throw "Repeated offline runner exited $($child.ExitCode), expected missing-lock preflight." }
        }
        finally { $child.Dispose() }
        $evidence = Get-Content -LiteralPath $lmArtifact -Raw | ConvertFrom-Json
        $selectedRoot = [IO.Path]::GetFullPath($evidence.applicationRoot)
        $runRoot = Split-Path $selectedRoot -Parent
        $selectedId = Split-Path $runRoot -Leaf
        if ((Split-Path $runRoot -Parent) -ne [IO.Path]::GetFullPath($junction) -or
            (Split-Path $selectedRoot -Leaf) -ne 'App' -or
            $selectedId -cnotmatch '^\d{8}T\d{6}Z-[0-9a-f]{32}$' -or
            -not (Test-Path -LiteralPath (Join-Path $runRoot '.qualification-root-claim') -PathType Leaf) -or
            -not $seenRoots.Add($selectedRoot)) {
            throw 'Repeated offline runner did not reserve a distinct validated App root.'
        }
    }
    Write-Output 'PASS three repeated offline starts reserved distinct validated App roots'

    $children = @()
    try {
        foreach ($phase in @('lmstudio', 'openrouter')) {
            $start = [Diagnostics.ProcessStartInfo]::new($runner)
            $start.UseShellExecute = $false
            $start.CreateNoWindow = $true
            $start.RedirectStandardOutput = $true
            $start.RedirectStandardError = $true
            $start.Environment['NEOBABYLON_SOURCE_ROOT'] = $fixtureSource
            $start.Environment['NEOBABYLON_APPLICATION_ROOT'] = '   '
            if ($phase -eq 'openrouter') { $start.ArgumentList.Add('--openrouter') }
            $start.ArgumentList.Add('--mock-only')
            $children += [Diagnostics.Process]::Start($start)
        }
        foreach ($child in $children) {
            if (-not $child.WaitForExit(10000)) { $child.Kill(); throw 'Concurrent offline runner timed out.' }
            $null = $child.StandardOutput.ReadToEnd()
            $null = $child.StandardError.ReadToEnd()
            if ($child.ExitCode -ne 1) { throw "Concurrent offline runner exited $($child.ExitCode), expected missing-lock preflight." }
        }
        $lm = Get-Content -LiteralPath $lmArtifact -Raw | ConvertFrom-Json
        $orArtifact = Join-Path $fixtureSource 'artifacts\phase1b\openrouter\qualification.json'
        $or = Get-Content -LiteralPath $orArtifact -Raw | ConvertFrom-Json
        if ($lm.applicationRoot -eq $or.applicationRoot -or
            -not $seenRoots.Add([IO.Path]::GetFullPath($lm.applicationRoot)) -or
            -not $seenRoots.Add([IO.Path]::GetFullPath($or.applicationRoot))) {
            throw 'Concurrent offline runners reused an App root.'
        }
        foreach ($selectedRoot in @($lm.applicationRoot, $or.applicationRoot)) {
            $runRoot = Split-Path $selectedRoot -Parent
            if ((Split-Path $runRoot -Parent) -ne [IO.Path]::GetFullPath($junction) -or
                -not (Test-Path -LiteralPath (Join-Path $runRoot '.qualification-root-claim') -PathType Leaf)) {
                throw 'Concurrent offline runner lacked an exact Lab Runs root or exclusive claim.'
            }
        }
        Write-Output 'PASS two concurrent offline starts reserved distinct App roots and claims'
    }
    finally { foreach ($child in $children) { $child.Dispose() } }
    return
}

function Test-ExactFixtureRoots([string] $CandidateSourceRoot, [string] $CandidateApplicationRoot) {
    $expectedSourceRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot ('.local\Lab\ProbeSources\NeoBabylon-Qualification-Probe-' + $runId)))
    $expectedApplicationRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot ('.local\Lab\Runs\' + $runId + '\App')))
    return ($runId -cmatch '^[0-9a-f]{32}$' -and
        [IO.Path]::GetFullPath($CandidateSourceRoot) -eq $expectedSourceRoot -and
        [IO.Path]::GetFullPath($CandidateApplicationRoot) -eq $expectedApplicationRoot)
}

function Protect-QualificationRoot([string] $Path) {
    $acl = Get-Acl -LiteralPath $Path
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($existingRule in @($acl.Access)) {
        $acl.RemoveAccessRuleAll($existingRule)
    }

    $inheritance = [System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor
        [System.Security.AccessControl.InheritanceFlags]::ObjectInherit
    $propagation = [System.Security.AccessControl.PropagationFlags]::None
    $rights = [System.Security.AccessControl.FileSystemRights]::FullControl
    $principals = @(
        [System.Security.Principal.WindowsIdentity]::GetCurrent().User,
        [System.Security.Principal.SecurityIdentifier]::new('S-1-5-18'),
        [System.Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
    )
    foreach ($principal in $principals) {
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $principal,
            $rights,
            $inheritance,
            $propagation,
            [System.Security.AccessControl.AccessControlType]::Allow
        )
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $Path -AclObject $acl
}

if (-not (Test-ExactFixtureRoots $sourceRoot $applicationRoot)) {
    throw 'Qualification fixtures must use the approved ProbeSources and Lab Runs paths.'
}
if ($PathCheckOnly) {
    $nestedSource = Join-Path $sourceRoot 'nested'
    $nestedApplication = Join-Path (Split-Path $applicationRoot -Parent) 'nested\App'
    if (Test-ExactFixtureRoots $nestedSource $nestedApplication) {
        throw 'Nested probe or application roots were accepted.'
    }
    Write-Output 'PASS qualification fixture root components and nested-path rejection'
    return
}

New-Item -ItemType Directory -Path $sourceRoot -Force | Out-Null
New-Item -ItemType Directory -Path $applicationRoot -Force | Out-Null
foreach ($fixtureRoot in @($sourceRoot, $applicationRoot)) {
    Protect-QualificationRoot $fixtureRoot
    $fixtureAcl = Get-Acl -LiteralPath $fixtureRoot
    $hasEveryoneAce = $false
    foreach ($rule in $fixtureAcl.Access) {
        if ($rule.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value -eq 'S-1-1-0') {
            $hasEveryoneAce = $true
            break
        }
    }
    if ($hasEveryoneAce) {
        throw "Qualification root '$fixtureRoot' is writable through an Everyone ACE; the containment fixture is invalid."
    }
}

New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null
$probeLockPath = Join-Path $runtimeDirectory 'runtime-lock.json'
$probeLock = Get-Content -LiteralPath (Join-Path $repositoryRoot 'runtime\runtime-lock.json') -Raw | ConvertFrom-Json
$copiedLockRuntime = [IO.Path]::GetFullPath((Join-Path $sourceRoot $probeLock.runtime.sourceCheckoutRelativePath))
$copiedLockBinary = [IO.Path]::GetFullPath((Join-Path $sourceRoot $probeLock.runtime.appServerBinaryRelativePath))
$approvedRuntime = [IO.Path]::GetFullPath((Join-Path $repositoryRoot '.local\Runtime\NeoBabylon-Runtime'))
$approvedBinary = [IO.Path]::GetFullPath((Join-Path $repositoryRoot '.local\Runtime\LockedBuild\codex-app-server.exe'))
if ($copiedLockRuntime -eq $approvedRuntime -or $copiedLockBinary -eq $approvedBinary) {
    throw 'The unchanged product lock unexpectedly resolves correctly from the deeper probe source.'
}
$probeLock.runtime.sourceCheckoutRelativePath = '../../../Runtime/NeoBabylon-Runtime'
$probeLock.runtime.appServerBinaryRelativePath = '../../../Runtime/LockedBuild/codex-app-server.exe'
$generatedRuntime = [IO.Path]::GetFullPath((Join-Path $sourceRoot $probeLock.runtime.sourceCheckoutRelativePath))
$generatedBinary = [IO.Path]::GetFullPath((Join-Path $sourceRoot $probeLock.runtime.appServerBinaryRelativePath))
if ($generatedRuntime -ne $approvedRuntime -or $generatedBinary -ne $approvedBinary) {
    throw "Generated fixture lock paths do not resolve to approved runtime targets (runtime='$generatedRuntime', binary='$generatedBinary')."
}
$probeLock | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $probeLockPath -Encoding utf8
$capabilityDirectory = Join-Path $sourceRoot 'docs\release'
New-Item -ItemType Directory -Path $capabilityDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\release\MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json') -Destination (Join-Path $capabilityDirectory 'MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\release\MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json') -Destination (Join-Path $capabilityDirectory 'MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json')

$oldSourceRoot = $env:NEOBABYLON_SOURCE_ROOT
$oldApplicationRoot = $env:NEOBABYLON_APPLICATION_ROOT
$oldOpenRouterApiKey = $env:OPENROUTER_API_KEY
$env:NEOBABYLON_SOURCE_ROOT = $sourceRoot
$env:NEOBABYLON_APPLICATION_ROOT = $applicationRoot

try {
    $runnerProject = Join-Path $repositoryRoot 'tools\Phase1AQualification\NeoBabylon.Phase1AQualification.csproj'
    $failures = [System.Collections.Generic.List[string]]::new()

    if (-not $OpenRouterCredentialOnly -and -not $LegacyUnsafeResearch) {
        $deleteTarget = Join-Path (Join-Path $applicationRoot 'Mock\Data') 'delete-canary.txt'
        New-Item -ItemType Directory -Path (Split-Path $deleteTarget -Parent) -Force | Out-Null
        Set-Content -LiteralPath $deleteTarget -Value 'NEOBABYLON_DELETE_CANARY' -NoNewline
        $runnerOutput = & dotnet run --project $runnerProject --configuration Release --no-restore -- --mock-only --windows-sandbox-unelevated --mock-probe parent-delete 2>&1
        $runnerExitCode = $LASTEXITCODE
        if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
            throw "Containment guard probe did not write its isolated evidence artifact. Runner output: $($runnerOutput -join [Environment]::NewLine)"
        }

        $evidence = Get-Content -LiteralPath $artifactPath -Raw | ConvertFrom-Json
        $denialText = @(
            $evidence.mock.toolOutputEvidence
            ($evidence.mock.interestingNotifications | ConvertTo-Json -Depth 12 -Compress)
            $evidence.mock.failure
        ) -join ' '
        $canaryPreserved = (Test-Path -LiteralPath $deleteTarget -PathType Leaf) -and
            (Get-Content -LiteralPath $deleteTarget -Raw) -eq 'NEOBABYLON_DELETE_CANARY'
        if ($runnerExitCode -ne 2 -or
            $evidence.mock.toolCommand -notmatch 'delete-canary\.txt' -or
            $evidence.mock.toolSucceeded -ne $false -or
            $evidence.mock.toolRoundTrip -ne $false -or
            $evidence.mock.toolOutputRequestSeen -ne $true -or
            $evidence.mock.typedToolFailure.type -ne 'toolExecution' -or
            $evidence.mock.typedToolFailure.attributedTo -ne 'Codex App Server' -or
            $denialText -notmatch 'NeoBabylon contained-tools mode' -or
            -not $canaryPreserved) {
            $failures.Add("NeoBabylon did not attribute a guarded legacy-tool rejection while preserving the outside canary (exit=$runnerExitCode, canaryPreserved=$canaryPreserved, evidence='$denialText'). Runner output: $($runnerOutput -join [Environment]::NewLine)")
        }
        else {
            Write-Output "PASS NeoBabylon legacy-tool guard denied the parent-delete probe and preserved the outside canary; evidence: $artifactPath"
        }
    }

    if ($LegacyUnsafeResearch) {
    $runnerOutput = & dotnet run --project $runnerProject --configuration Release --no-restore -- --mock-only --windows-sandbox-unelevated --mock-probe workspace-write 2>&1
    $runnerExitCode = $LASTEXITCODE
    if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
        throw "Qualification artifact was not written under the shadow source root. Runner output: $($runnerOutput -join [Environment]::NewLine)"
    }

    $evidence = Get-Content -LiteralPath $artifactPath -Raw | ConvertFrom-Json
    Copy-Item -LiteralPath $artifactPath -Destination (Join-Path $sourceRoot 'workspace-write-evidence.json') -Force
    if ($evidence.mock.toolCommand -notmatch '\.neobabylon-sandbox-probe\.txt') {
        $failures.Add("The named workspace-write probe was ignored; recorded command was '$($evidence.mock.toolCommand)'.")
    }
    elseif ($runnerExitCode -ne 0 -or $evidence.mock.toolRoundTrip -ne $true) {
        $failures.Add("The deterministic workspace-write probe did not complete successfully (exit=$runnerExitCode, toolSucceeded=$($evidence.mock.toolSucceeded), output='$($evidence.mock.toolOutputEvidence)'). Runner output: $($runnerOutput -join [Environment]::NewLine)")
    }
    else {
        $probePath = Join-Path $evidence.mock.threadStart.cwd '.neobabylon-sandbox-probe.txt'
        if (-not (Test-Path -LiteralPath $probePath -PathType Leaf)) {
            $failures.Add("App Server reported success but did not create the expected fixture file '$probePath'.")
        }
        elseif ((Get-Content -LiteralPath $probePath -Raw).Trim() -ne 'NEOBABYLON_SANDBOX_PROBE') {
            $failures.Add("The fixture file contents were unexpected at '$probePath'.")
        }
        else {
            Write-Output "PASS deterministic workspace-write probe; evidence: $artifactPath; file: $probePath"
        }
    }

    $runnerOutput = & dotnet run --project $runnerProject --configuration Release --no-restore -- --mock-only --windows-sandbox-unelevated --mock-probe parent-write 2>&1
    $runnerExitCode = $LASTEXITCODE
    $evidence = Get-Content -LiteralPath $artifactPath -Raw | ConvertFrom-Json
    Copy-Item -LiteralPath $artifactPath -Destination (Join-Path $sourceRoot 'parent-write-evidence.json') -Force
    $outsideTarget = Join-Path (Join-Path $applicationRoot 'Mock\Data') 'outside-write.txt'
    $denialText = @(
        $evidence.mock.toolOutputEvidence
        ($evidence.mock.interestingNotifications | ConvertTo-Json -Depth 12 -Compress)
        $evidence.mock.failure.message
    ) -join ' '
    $parentWriteWasBlocked = $runnerExitCode -eq 2 -and
        $evidence.mock.toolCommand -match 'outside-write\.txt' -and
        $evidence.mock.completed -eq $true -and
        $evidence.mock.toolSucceeded -eq $false -and
        $evidence.mock.toolOutputRequestSeen -eq $true -and
        $denialText -match '(?i)denied|blocked|sandbox|unauthorized' -and
        -not (Test-Path -LiteralPath $outsideTarget)
    if (-not $parentWriteWasBlocked) {
        $failures.Add("The parent-write probe did not show an attributed sandbox denial (exit=$runnerExitCode, targetExists=$(Test-Path -LiteralPath $outsideTarget), evidence='$denialText'). Runner output: $($runnerOutput -join [Environment]::NewLine)")
    }
    else {
        Write-Output "PASS parent write blocked by the effective Windows sandbox; target absent: $outsideTarget"
    }

    $deleteTarget = Join-Path (Join-Path $applicationRoot 'Mock\Data') 'delete-canary.txt'
    Set-Content -LiteralPath $deleteTarget -Value 'NEOBABYLON_DELETE_CANARY' -NoNewline
    $runnerOutput = & dotnet run --project $runnerProject --configuration Release --no-restore -- --mock-only --windows-sandbox-unelevated --mock-probe parent-delete 2>&1
    $runnerExitCode = $LASTEXITCODE
    $evidence = Get-Content -LiteralPath $artifactPath -Raw | ConvertFrom-Json
    Copy-Item -LiteralPath $artifactPath -Destination (Join-Path $sourceRoot 'parent-delete-evidence.json') -Force
    $deletionDenialText = @(
        $evidence.mock.toolOutputEvidence
        ($evidence.mock.interestingNotifications | ConvertTo-Json -Depth 12 -Compress)
        $evidence.mock.failure.message
    ) -join ' '
    $deleteAttemptExecuted = $deletionDenialText -match 'PROBE_DELETE_ATTEMPTED'
    $deleteSucceeded = $deleteAttemptExecuted -and
        $deletionDenialText -match 'PROBE_OUTSIDE_DELETE_OK' -and
        -not (Test-Path -LiteralPath $deleteTarget -PathType Leaf)
    $operatingSystemDeniedDelete = $deletionDenialText -match 'PROBE_DELETE_DENIED' -and
        $deletionDenialText -match '(?i)access is denied|access to the path.+denied'
    $parentDeleteWasBlocked = $runnerExitCode -eq 2 -and
        $evidence.mock.toolCommand -match 'delete-canary\.txt' -and
        $evidence.mock.completed -eq $true -and
        $evidence.mock.toolSucceeded -eq $false -and
        $evidence.mock.toolRoundTrip -eq $false -and
        $evidence.mock.toolOutputRequestSeen -eq $true -and
        $deleteAttemptExecuted -and
        $operatingSystemDeniedDelete -and
        (Test-Path -LiteralPath $deleteTarget -PathType Leaf)
    if (-not $parentDeleteWasBlocked) {
        if ($deleteSucceeded) {
            $failures.Add("SECURITY FAILURE: the pinned App Server completed cmd.exe deletion of a sibling canary outside the workspace under workspace-write/unelevated (exit=$runnerExitCode, canaryExists=False, evidence='$deletionDenialText').")
        }
        else {
            $failures.Add("The parent-delete probe did not execute and return an OS access denial with its canary preserved (exit=$runnerExitCode, attemptMarker=$deleteAttemptExecuted, osDenied=$operatingSystemDeniedDelete, canaryExists=$(Test-Path -LiteralPath $deleteTarget), evidence='$deletionDenialText'). Runner output: $($runnerOutput -join [Environment]::NewLine)")
        }
    }
    else {
        Write-Output "PASS parent delete blocked by the effective Windows sandbox; canary preserved: $deleteTarget"
    }

    $runnerOutput = & dotnet run --project $runnerProject --configuration Release --no-restore -- --mock-only --windows-sandbox-unelevated --mock-probe unsupported 2>&1
    $runnerExitCode = $LASTEXITCODE
    $evidence = Get-Content -LiteralPath $artifactPath -Raw | ConvertFrom-Json
    if ($runnerExitCode -eq 0 -or $evidence.failure.message -notmatch 'unsupported' -or $null -ne $evidence.mock) {
        $failures.Add("An unknown mock probe was not rejected before execution (exit=$runnerExitCode). Runner output: $($runnerOutput -join [Environment]::NewLine)")
    }
    else {
        Write-Output 'PASS unsupported mock probes fail closed'
    }
    }

    if ($OpenRouterCredentialOnly -or $LegacyUnsafeResearch) {
    $env:OPENROUTER_API_KEY = 'phase1b-mock-secret'
    $runnerOutput = & dotnet run --project $runnerProject --configuration Release --no-restore -- --openrouter --mock-only --unrestricted-tools --mock-probe openrouter-key-exclusion 2>&1
    $runnerExitCode = $LASTEXITCODE
    if (-not (Test-Path -LiteralPath $openRouterArtifactPath -PathType Leaf)) {
        throw "OpenRouter credential-exclusion probe did not write its isolated evidence artifact. Runner output: $($runnerOutput -join [Environment]::NewLine)"
    }

    $openRouterEvidenceText = Get-Content -LiteralPath $openRouterArtifactPath -Raw
    $openRouterEvidence = $openRouterEvidenceText | ConvertFrom-Json
    $openRouterConfig = $openRouterEvidence.mock.effectiveConfig
    if ($null -ne $openRouterConfig.config) {
        $openRouterConfig = $openRouterConfig.config
    }
    $hasOpenRouterEnvironmentExclusion = @($openRouterConfig.shell_environment_policy.exclude) -contains 'OPENROUTER_API_KEY'
    $unrestrictedConfigSelected = $openRouterConfig.sandbox_mode -eq 'danger-full-access'
    $unrestrictedAuthorityEffective = $openRouterEvidence.mock.executionAuthority.effectiveSandboxType -eq 'dangerFullAccess' -and
        $openRouterEvidence.mock.executionAuthority.controllingAuthority -eq 'Codex unrestricted execution'
    $profileUseDisabled = $openRouterConfig.shell_environment_policy.experimental_use_profile -eq $false
    $loginShellDisabled = $openRouterConfig.allow_login_shell -eq $false
    $toolOutputEvidence = [string]$openRouterEvidence.mock.toolOutputEvidence
    $credentialWasAbsent = $toolOutputEvidence -match 'NB_OPENROUTER_KEY_ABSENT' -and $toolOutputEvidence -notmatch 'NB_OPENROUTER_KEY_PRESENT'
    $profileWasLoaded = $toolOutputEvidence -match '(?i)PowerShell_profile\.ps1|Terminal-Icons|Export-Clixml'
    $inspectionWasOffline = $openRouterEvidence.providerInspection.mode -eq 'offline deterministic mock probe' -and
        $openRouterEvidence.providerInspection.liveInspectionPerformed -eq $false
    if ($runnerExitCode -ne 0 -or
        $openRouterEvidence.mock.toolRoundTrip -ne $true -or
        -not $hasOpenRouterEnvironmentExclusion -or
        -not $unrestrictedConfigSelected -or
        -not $unrestrictedAuthorityEffective -or
        $openRouterEvidence.mock.providerCredentialExcludedByEffectiveConfig -ne $true -or
        $openRouterEvidence.mock.toolEnvironmentCredentialObservation -ne 'absent' -or
        -not $profileUseDisabled -or
        -not $loginShellDisabled -or
        $profileWasLoaded -or
        -not $credentialWasAbsent -or
        -not $inspectionWasOffline -or
        $openRouterEvidenceText.Contains('phase1b-mock-secret')) {
        $failures.Add("The deterministic OpenRouter child-environment probe did not prove unrestricted offline credential exclusion (exit=$runnerExitCode, toolRoundTrip=$($openRouterEvidence.mock.toolRoundTrip), configExclusion=$hasOpenRouterEnvironmentExclusion, unrestrictedConfig=$unrestrictedConfigSelected, unrestrictedEffective=$unrestrictedAuthorityEffective, profileUseDisabled=$profileUseDisabled, loginShellDisabled=$loginShellDisabled, profileWasLoaded=$profileWasLoaded, observed=$($openRouterEvidence.mock.toolEnvironmentCredentialObservation), output='$toolOutputEvidence', offlineInspection=$inspectionWasOffline, secretRecorded=$($openRouterEvidenceText.Contains('phase1b-mock-secret')). Runner output: $($runnerOutput -join [Environment]::NewLine)")
    }
    else {
        Write-Output 'PASS offline OpenRouter probe observes the synthetic credential absent from the ordinary tool environment'
    }
    }

    if ($failures.Count -gt 0) {
        throw ($failures -join [Environment]::NewLine)
    }
}
finally {
    $env:NEOBABYLON_SOURCE_ROOT = $oldSourceRoot
    $env:NEOBABYLON_APPLICATION_ROOT = $oldApplicationRoot
    $env:OPENROUTER_API_KEY = $oldOpenRouterApiKey
}
