param([switch] $PathCheckOnly)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$runId = [Guid]::NewGuid().ToString('N')
$sourceRoot = Join-Path (Join-Path $repositoryRoot '.local\Lab\ProbeSources') ('NeoBabylon-429-Test-' + $runId)
$applicationRoot = Join-Path (Join-Path (Join-Path $repositoryRoot '.local\Lab\Runs') $runId) 'App'
$artifactPath = Join-Path $sourceRoot 'artifacts\phase1a\lmstudio\qualification.json'

function Test-ExactFixtureRoots([string] $CandidateSourceRoot, [string] $CandidateApplicationRoot) {
    $expectedSourceRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot ('.local\Lab\ProbeSources\NeoBabylon-429-Test-' + $runId)))
    $expectedApplicationRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot ('.local\Lab\Runs\' + $runId + '\App')))
    return ($runId -cmatch '^[0-9a-f]{32}$' -and
        [IO.Path]::GetFullPath($CandidateSourceRoot) -eq $expectedSourceRoot -and
        [IO.Path]::GetFullPath($CandidateApplicationRoot) -eq $expectedApplicationRoot)
}

function Protect-TestRoot([string] $Path) {
    $acl = Get-Acl -LiteralPath $Path
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($existingRule in @($acl.Access)) {
        $acl.RemoveAccessRuleAll($existingRule)
    }

    $inheritance = [System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor
        [System.Security.AccessControl.InheritanceFlags]::ObjectInherit
    $principals = @(
        [System.Security.Principal.WindowsIdentity]::GetCurrent().User,
        [System.Security.Principal.SecurityIdentifier]::new('S-1-5-18'),
        [System.Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
    )
    foreach ($principal in $principals) {
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $principal,
            [System.Security.AccessControl.FileSystemRights]::FullControl,
            $inheritance,
            [System.Security.AccessControl.PropagationFlags]::None,
            [System.Security.AccessControl.AccessControlType]::Allow
        )
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $Path -AclObject $acl
}

if (-not (Test-ExactFixtureRoots $sourceRoot $applicationRoot)) {
    throw '429 fixtures must use the approved ProbeSources and Lab Runs paths.'
}
if ($PathCheckOnly) {
    $nestedSource = Join-Path $sourceRoot 'nested'
    $nestedApplication = Join-Path (Split-Path $applicationRoot -Parent) 'nested\App'
    if (Test-ExactFixtureRoots $nestedSource $nestedApplication) {
        throw 'Nested probe or application roots were accepted.'
    }
    Write-Output 'PASS 429 fixture root components and nested-path rejection'
    return
}

New-Item -ItemType Directory -Path $sourceRoot -Force | Out-Null
New-Item -ItemType Directory -Path $applicationRoot -Force | Out-Null
Protect-TestRoot $sourceRoot
Protect-TestRoot $applicationRoot

$runtimeDirectory = Join-Path $sourceRoot 'runtime'
$capabilityDirectory = Join-Path $sourceRoot 'docs\release'
New-Item -ItemType Directory -Path $runtimeDirectory | Out-Null
New-Item -ItemType Directory -Path $capabilityDirectory | Out-Null
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
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\release\MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json') -Destination (Join-Path $capabilityDirectory 'MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json')

$oldSourceRoot = $env:NEOBABYLON_SOURCE_ROOT
$oldApplicationRoot = $env:NEOBABYLON_APPLICATION_ROOT
$env:NEOBABYLON_SOURCE_ROOT = $sourceRoot
$env:NEOBABYLON_APPLICATION_ROOT = $applicationRoot

try {
    $runnerProject = Join-Path $repositoryRoot 'tools\Phase1AQualification\NeoBabylon.Phase1AQualification.csproj'
    $runnerOutput = & dotnet run --project $runnerProject --configuration Release --no-restore -- --mock-only --mock-provider-error 429 2>&1
    $runnerExitCode = $LASTEXITCODE
    if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
        throw "The 429 qualification artifact was not written. Runner output: $($runnerOutput -join [Environment]::NewLine)"
    }

    $evidence = Get-Content -LiteralPath $artifactPath -Raw | ConvertFrom-Json
    $mock = $evidence.mock
    $failures = [System.Collections.Generic.List[string]]::new()
    $diagnostic = @(
        ($mock.failure | ConvertTo-Json -Depth 16 -Compress)
        ($mock.interestingNotifications | ConvertTo-Json -Depth 16 -Compress)
    ) -join ' '
    $runnerTranscript = $runnerOutput -join [Environment]::NewLine
    $expectedModel = 'phase1a-qwen3-14b'

    if ($runnerExitCode -ne 0 -or $mock.expectedProviderErrorObserved -ne $true) {
        $failures.Add("The isolated 429 case was not qualified as an expected provider failure (exit=$runnerExitCode). Runner output: $($runnerOutput -join [Environment]::NewLine)")
    }
    if ($runnerTranscript -notmatch 'Mock Responses 429 qualification: PASS') {
        $failures.Add("The runner did not clearly label the expected 429 result as a qualification pass. Runner output: $runnerTranscript")
    }
    if ($evidence.providerInspection.liveInspectionPerformed -ne $false) {
        $failures.Add('The deterministic 429 run did not prove that live provider inspection was skipped.')
    }
    if ($mock.providerFailure.statusCode -ne 429 -or $mock.responsesRequestCount -lt 1) {
        $failures.Add("The App Server did not observe an HTTP 429 from the local fixture (status=$($mock.providerFailure.statusCode), requests=$($mock.responsesRequestCount)).")
    }
    if ($mock.providerFailure.httpStatusCode -ne 429 -or
        $mock.providerFailure.appServerErrorType -ne 'responseTooManyFailedAttempts' -or
        $mock.providerFailure.appServerErrorVisible -ne $true -or
        $diagnostic -notmatch '429 Too Many Requests') {
        $failures.Add("The App Server did not expose the typed fixture error details. Evidence: $diagnostic")
    }
    if ($mock.toolRoundTrip -ne $false -or $mock.toolOutputRequestSeen -ne $false) {
        $failures.Add('The failed provider request unexpectedly produced a tool round trip or tool output.')
    }
    if ($mock.noToolCallObserved -ne $true) {
        $failures.Add('The qualification evidence did not explicitly confirm that no tool call was observed.')
    }
    if ($mock.attributedTo -ne 'Codex App Server → deterministic mock Responses') {
        $failures.Add("The failure was not attributed to the App Server/local Responses boundary (attributedTo='$($mock.attributedTo)').")
    }
    if ($mock.responsesEndpoint -notmatch '^http://127\.0\.0\.1:\d+/v1$') {
        $failures.Add("The Responses test endpoint was not loopback-local ('$($mock.responsesEndpoint)').")
    }
    if ($mock.effectiveConfig.config.model -ne $expectedModel -or $mock.effectiveConfig.config.model_provider -ne 'lmstudio') {
        $failures.Add("The effective model/provider changed during the 429 case (model='$($mock.effectiveConfig.config.model)', provider='$($mock.effectiveConfig.config.model_provider)').")
    }
    $requestModels = @($mock.responsesRequestSummaries | ForEach-Object { $_.model })
    if ($requestModels.Count -lt 1 -or @($requestModels | Where-Object { $_ -ne $expectedModel }).Count -gt 0) {
        $failures.Add("The Responses fixture saw a missing or fallback model ID: $($requestModels -join ', ')")
    }
    if (@($mock.responsesHttpStatusCodes | Where-Object { $_ -ne 429 }).Count -gt 0 -or
        @($mock.responsesHttpStatusCodes).Count -ne $mock.responsesRequestCount) {
        $failures.Add("The local fixture status evidence does not show only the requested 429 response (statuses=$($mock.responsesHttpStatusCodes -join ', '), requests=$($mock.responsesRequestCount)).")
    }

    if ($failures.Count -gt 0) {
        throw ($failures -join [Environment]::NewLine)
    }

    Write-Output "PASS local HTTP 429 is visible, attributed, has no tool call or model fallback; evidence: $artifactPath"
}
finally {
    $env:NEOBABYLON_SOURCE_ROOT = $oldSourceRoot
    $env:NEOBABYLON_APPLICATION_ROOT = $oldApplicationRoot
}
