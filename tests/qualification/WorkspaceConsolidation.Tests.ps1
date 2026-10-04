$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$scriptPath = Join-Path $repositoryRoot 'scripts\WorkspaceConsolidation.ps1'
$manifestPath = Join-Path $repositoryRoot 'scripts\workspace-consolidation-pairs.json'
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('NeoBabylon-WorkspaceConsolidation-' + [Guid]::NewGuid().ToString('N'))
$fixtureSentinel = Join-Path $fixtureRoot 'outside-move-sentinel.txt'
$oldGitConfigGlobal = $env:GIT_CONFIG_GLOBAL
$oldGitConfigSystem = $env:GIT_CONFIG_NOSYSTEM
$oldGitOptionalLocks = $env:GIT_OPTIONAL_LOCKS
$env:GIT_CONFIG_GLOBAL = 'NUL'
$env:GIT_CONFIG_NOSYSTEM = '1'
$env:GIT_OPTIONAL_LOCKS = '0'

function Assert-Equal([string]$Name, [object]$Expected, [object]$Actual) {
    if ($Expected -cne $Actual) {
        throw "$Name`nExpected: [$Expected]`nActual:   [$Actual]"
    }
}

function Invoke-Git([string]$Repository, [string[]]$Arguments) {
    $output = & git -C $Repository @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed in '$Repository': $($output -join [Environment]::NewLine)"
    }
    return (@($output | ForEach-Object { [string]$_ }) -join "`n").TrimEnd("`r", "`n")
}

function Get-GitState([string]$Repository) {
    return [ordered]@{
        head=(Invoke-Git $Repository @('rev-parse','HEAD'))
        status=(Invoke-Git $Repository @('status','--porcelain=v2','--branch','--untracked-files=all','--ignored=matching'))
        staged=(Invoke-Git $Repository @('diff','--cached','--binary'))
        unstaged=(Invoke-Git $Repository @('diff','--binary'))
        untracked=(Invoke-Git $Repository @('ls-files','--others','--exclude-standard'))
        ignored=(Invoke-Git $Repository @('ls-files','--others','--ignored','--exclude-standard'))
        trackedSha256=(Get-FileHash -LiteralPath (Join-Path $Repository 'tracked.txt') -Algorithm SHA256).Hash
        untrackedSha256=(Get-FileHash -LiteralPath (Join-Path $Repository 'untracked.txt') -Algorithm SHA256).Hash
        ignoredSha256=(Get-FileHash -LiteralPath (Join-Path $Repository 'ignored.txt') -Algorithm SHA256).Hash
    }
}

function Invoke-Mechanism([string]$Mode, [string]$Manifest, [string]$Evidence, [switch]$WithoutFixture) {
    $arguments = @('-NoLogo','-NoProfile','-File',$scriptPath,'-Mode',$Mode,'-ManifestPath',$Manifest,'-EvidenceRoot',$Evidence)
    if (-not $WithoutFixture) { $arguments += @('-Fixture','-FixtureRoot',$fixtureRoot) }
    $output = & pwsh @arguments 2>&1
    return [pscustomobject]@{ ExitCode=[int]$LASTEXITCODE; Output=@($output | ForEach-Object { [string]$_ }) }
}

function Write-FixtureManifest([string]$Path, [object[]]$Pairs) {
    [IO.File]::WriteAllText($Path, (ConvertTo-Json -InputObject @($Pairs) -Depth 8) + "`n", [Text.UTF8Encoding]::new($false))
}

function New-OnePairFixture([string]$Name) {
    $base = Join-Path $fixtureRoot $Name
    $source = Join-Path $base 'source'
    $destination = Join-Path $base 'destination\source'
    $evidence = Join-Path $base 'evidence'
    New-Item -ItemType Directory -Path $source -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $source 'payload.txt'), 'fixture-payload', [Text.UTF8Encoding]::new($false))
    $manifest = Join-Path $base 'pairs.json'
    Write-FixtureManifest $manifest @([ordered]@{ source=$source; destination=$destination })
    return [pscustomobject]@{ Base=$base; Source=$source; Destination=$destination; Evidence=$evidence; Manifest=$manifest }
}

function Assert-Expected([string]$Name, [bool]$Condition, [string]$Detail) {
    if (-not $Condition) { throw "$Name failed: $Detail" }
    Write-Output "PASS $Name"
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'The fixture requires the locally installed Git executable.' }
if (-not (Get-Command pwsh -ErrorAction SilentlyContinue)) { throw 'The fixture requires PowerShell 7 (pwsh).'}
if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf) -or -not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'The workspace-consolidation script and exact pair manifest must exist before this test can run.'
}

New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
[IO.File]::WriteAllText($fixtureSentinel, 'must-remain-outside-move-roots', [Text.UTF8Encoding]::new($false))
try {
    # This literal, design-derived list is independent of the generated manifest.
    $probe429 = @(
        '0dbdf8a3bb90417bb53d418937fd41a1','574a8df0d4ed456db14535fbcddee57d','664867fa1d39478d9d03a3f2882a5235',
        '777f9dcde006484baaba0b71a689ba5a','8837716c7b5842cd8f4ebb97e4e942c8','89cabcceb0d9448ba972f066e6f12cde',
        'a0ad5fc3cc5249bbab9f4088d8588abd','a1211c24534a48bbac762b952b7c7594','a8a17cdd409c4c53a9a908eccefedd4a',
        'ad91c830f4594c55b21ed02d45084dc1','cef4cfa09a6d452ba2144dda3f269e5c','dcee6a4f59834f85b77166542c67250b'
    )
    $probeQualification = @(
        '0b33e60d328646638f85412a0b93bbb1','0b596d723c594b889251cc544dd14bd1','16cd945fdbd54c90b57de7bd5091ced2',
        '190bb1f3c24f4466be764292a87c25c6','1cc5e5b733544c55a6518748f14bf24c','2d9f3bb28f2642fab2f2633c0622b1b3',
        '3908dad53084409cbcd80a31fe8dd86c','45182dd57a704775bca28bab18d821b7','4f31743b6f2a41daa69c7458fad61e90',
        '50a589888f434298909a90e83197e8a3','527b5c48831a459cb3aca6bed04735e9','55bdb9e81ae04209ad423b345daf1422',
        '640dae678249468798dc0f298e7915d7','728432b3d2034c4eaed2ee4637e757e5','742a44f8c37546c58c4142760e039b23',
        '7534b6c5a20c44b881c05355b7fd1ba7','85e615b67ed84c5dab763ac34bbcedb3','872274ba2a214f858e4e172454d26a51',
        '8903a86effae44a1b1bfc9e42ba13de5','944b8e39da2e4e7ea221751a963d28a2','9e6561fd7fbf4e5b92f054f7839b1d48',
        'a3001e008a1e443d87c03e3254d4fae1','a8e3f5f4f58148eb81ec65b8eaf56aba','b636933305c2459db7a940141e5ce43f',
        'ba9c10c962924462a03dca7d22b22ba9','cbb387a69c054761ad480a6d74743f36','d3d61a65cc5e4565b2bea35f6d8168cd',
        'd853b087cfcd4fa48d5d2050fe1fe982','d914c1ae49014570b89c17391dde8dc5','db2df6e1f19e4ae7b8d27498f6258717',
        'db34e1bd15ba45c49d9afe4aff8c337a','dcbc28cfd53b458f87e42268e5636035','e40797bd80e447318e1705b99f7ec6ae',
        'e5c78dcd591549d899ea10d5c78ea161','e5cff6a9ed7744ab9117c8b4eb1615f6','e94f846e0946402890c1a020374a2c40',
        'f52bbcdee9954464ac3a14f7ce46a322','fcade15a631747cb9feb50cb3dfc7316'
    )
    $expected = [Collections.Generic.List[object]]::new()
    $nb = 'D:\CODING\NeoBabylon'
    $expected.Add([ordered]@{ source='D:\CODING\NeoBabylon-Data'; destination="$nb\.local\Lab\NeoBabylon-Data" })
    foreach ($id in $probe429) { $name="NeoBabylon-429-Test-$id"; $expected.Add([ordered]@{ source="D:\CODING\$name"; destination="$nb\.local\Lab\ProbeSources\$name" }) }
    foreach ($id in $probeQualification) { $name="NeoBabylon-Qualification-Probe-$id"; $expected.Add([ordered]@{ source="D:\CODING\$name"; destination="$nb\.local\Lab\ProbeSources\$name" }) }
    foreach ($name in @(
        'NeoBabylon-OpenRouter-LiveMetadata-9e3c218bc5775813dcc860a606ab88cb',
        'NeoBabylon-OpenRouter-Preflight-297523ed055afe4e82a4f6287fc39b17',
        'NeoBabylon-OpenRouter-RoutePreflight-c0961add19c368c16fccae98d89ecf15'
    )) { $expected.Add([ordered]@{ source="D:\CODING\$name"; destination="$nb\.local\Lab\ProbeSources\$name" }) }
    $expected.Add([ordered]@{ source='D:\CODING\NBRT-RouteControl'; destination="$nb\.local\Runtime\NBRT-RouteControl" })
    $expected.Add([ordered]@{ source='D:\CODING\NeoBabylon-Runtime'; destination="$nb\.local\Runtime\NeoBabylon-Runtime" })
    $actual = @(Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json)
    Assert-Equal 'manifest has exactly 56 pairs' 56 $actual.Count
    Assert-Equal 'approved manifest byte hash stays pinned' 'C5B6AF1D297EC9B03544192834C29C8DC3F9496C12EC7768D9AD70E8467088E9' (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    foreach ($pair in $actual) { Assert-Equal 'manifest pair has only source/destination keys' 'destination,source' ((@($pair.PSObject.Properties.Name) | Sort-Object) -join ',') }
    $actualText = @($actual | ForEach-Object { ([IO.Path]::GetFullPath($_.source)) + ' -> ' + ([IO.Path]::GetFullPath($_.destination)) }) -join "`n"
    $expectedText = @($expected | ForEach-Object { ([IO.Path]::GetFullPath($_.source)) + ' -> ' + ([IO.Path]::GetFullPath($_.destination)) }) -join "`n"
    Assert-Equal 'manifest source/destination pairs match approved layout and fixed probe IDs' $expectedText $actualText
    Write-Output 'PASS exact 56-pair manifest mapping'

    # A temporary arbitrary manifest must never be accepted by the live interface.
    $liveGate = New-OnePairFixture 'live-gate'
    $liveGateResult = Invoke-Mechanism 'Preflight' $liveGate.Manifest $liveGate.Evidence -WithoutFixture
    Assert-Expected 'live interface rejects arbitrary manifest path' ($liveGateResult.ExitCode -ne 0) ($liveGateResult.Output -join "`n")
    Assert-Expected 'rejected live interface created no evidence' (-not (Test-Path $liveGate.Evidence)) 'evidence was created'

    # Fixture mode must reject a manifest whose source escapes its unique temp root.
    $escape = New-OnePairFixture 'fixture-escape'
    Write-FixtureManifest $escape.Manifest @([ordered]@{ source=[IO.Path]::GetTempPath(); destination=$escape.Destination })
    $escapeResult = Invoke-Mechanism 'Preflight' $escape.Manifest $escape.Evidence
    Assert-Expected 'fixture mode rejects source outside fixture root' ($escapeResult.ExitCode -ne 0) ($escapeResult.Output -join "`n")
    Assert-Expected 'fixture escape created no evidence' (-not (Test-Path $escape.Evidence)) 'evidence was created'
    $outsideEvidence = Join-Path ([IO.Path]::GetTempPath()) ('NeoBabylon-OutsideEvidence-' + [Guid]::NewGuid().ToString('N'))
    $evidenceEscape = Invoke-Mechanism 'Preflight' $liveGate.Manifest $outsideEvidence
    Assert-Expected 'fixture mode rejects evidence outside fixture root' ($evidenceEscape.ExitCode -ne 0) ($evidenceEscape.Output -join "`n")
    Assert-Expected 'fixture evidence escape writes nothing' (-not (Test-Path $outsideEvidence)) 'outside evidence created'

    # Build a real temporary Git repository with a linked worktree and dirty states.
    $gitBase = Join-Path $fixtureRoot 'git-rehearsal'
    $main = Join-Path $gitBase 'main'
    $linked = Join-Path $gitBase 'linked'
    $movedMain = Join-Path $gitBase 'destination\main'
    $movedLinked = Join-Path $gitBase 'destination\linked'
    New-Item -ItemType Directory -Path $main -Force | Out-Null
    Invoke-Git $main @('init','--quiet','-b','main') | Out-Null
    Invoke-Git $main @('config','user.name','Workspace fixture') | Out-Null
    Invoke-Git $main @('config','user.email','fixture@example.invalid') | Out-Null
    [IO.File]::WriteAllText((Join-Path $main '.gitignore'), "ignored.txt`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $main 'tracked.txt'), "base`n", [Text.UTF8Encoding]::new($false))
    Invoke-Git $main @('add','--','.gitignore','tracked.txt') | Out-Null
    Invoke-Git $main @('commit','--quiet','-m','fixture baseline') | Out-Null
    Invoke-Git $main @('worktree','add','--quiet','-b','fixture-linked',$linked) | Out-Null
    foreach ($repo in @($main,$linked)) {
        [IO.File]::WriteAllText((Join-Path $repo 'tracked.txt'), "staged`n", [Text.UTF8Encoding]::new($false))
        Invoke-Git $repo @('add','--','tracked.txt') | Out-Null
        [IO.File]::WriteAllText((Join-Path $repo 'tracked.txt'), "unstaged`n", [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $repo 'untracked.txt'), 'untracked', [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $repo 'ignored.txt'), 'ignored', [Text.UTF8Encoding]::new($false))
    }
    $gitPairsPath = Join-Path $gitBase 'pairs.json'
    Write-FixtureManifest $gitPairsPath @(
        [ordered]@{ source=$main; destination=$movedMain },
        [ordered]@{ source=$linked; destination=$movedLinked }
    )
    $gitEvidence = Join-Path $gitBase 'evidence'
    $gitInitial = [ordered]@{}
    foreach ($repo in @($main,$linked)) {
        $gitInitial[$repo] = [ordered]@{
            head=(Invoke-Git $repo @('rev-parse','HEAD'))
            status=(Invoke-Git $repo @('status','--porcelain=v2','--branch','--untracked-files=all','--ignored=matching'))
            staged=(Invoke-Git $repo @('diff','--cached','--binary'))
            unstaged=(Invoke-Git $repo @('diff','--binary'))
            untracked=(Invoke-Git $repo @('ls-files','--others','--exclude-standard'))
            ignored=(Invoke-Git $repo @('ls-files','--others','--ignored','--exclude-standard'))
        }
    }
    $outsideHash = (Get-FileHash -LiteralPath $fixtureSentinel -Algorithm SHA256).Hash
    $preflight = Invoke-Mechanism 'Preflight' $gitPairsPath $gitEvidence
    Assert-Expected 'fixture preflight succeeds' ($preflight.ExitCode -eq 0) ($preflight.Output -join "`n")
    $gitPreflightReport = Get-Content -LiteralPath (Join-Path $gitEvidence 'preflight.json') -Raw | ConvertFrom-Json
    Assert-Equal 'capacity report is explicitly non-qualifying' 'non-qualifying' $gitPreflightReport.capacityCheck.qualification
    Assert-Expected 'capacity report requires backup readback gate' ([bool]$gitPreflightReport.capacityCheck.requiresBackupReadbackGate) 'gate field missing'
    Assert-Expected 'capacity report includes source and evidence volumes' (@($gitPreflightReport.capacityCheck.volumes).Count -ge 1) 'volume list missing'
    $move = Invoke-Mechanism 'Move' $gitPairsPath $gitEvidence
    Assert-Expected 'fixture whole-root move succeeds' ($move.ExitCode -eq 0) ($move.Output -join "`n")
    Assert-Expected 'both original roots are absent after move' (-not (Test-Path $main) -and -not (Test-Path $linked)) 'source roots remain'
    Assert-Expected 'both destinations exist after move' ((Test-Path $movedMain) -and (Test-Path $movedLinked)) 'destination roots missing'
    Invoke-Git $movedMain @('worktree','repair',$movedLinked) | Out-Null
    foreach ($pair in @(@($movedMain,$main),@($movedLinked,$linked))) {
        $repo=$pair[0]; $original=$pair[1]; $after=[ordered]@{
            head=(Invoke-Git $repo @('rev-parse','HEAD'))
            status=(Invoke-Git $repo @('status','--porcelain=v2','--branch','--untracked-files=all','--ignored=matching'))
            staged=(Invoke-Git $repo @('diff','--cached','--binary'))
            unstaged=(Invoke-Git $repo @('diff','--binary'))
            untracked=(Invoke-Git $repo @('ls-files','--others','--exclude-standard'))
            ignored=(Invoke-Git $repo @('ls-files','--others','--ignored','--exclude-standard'))
        }
        Assert-Equal "moved Git state preserved for $original" ($gitInitial[$original] | ConvertTo-Json -Compress) ($after | ConvertTo-Json -Compress)
    }
    $movedWorktrees = Invoke-Git $movedMain @('worktree','list','--porcelain')
    $normalizedWorktreeList = $movedWorktrees.Replace('/','\')
    Assert-Expected 'moved main checkout recognizes repaired linked worktree' ($normalizedWorktreeList.Contains($movedLinked,[StringComparison]::OrdinalIgnoreCase)) $movedWorktrees
    $journalPath = Join-Path $gitEvidence 'journal.jsonl'
    $journalBeforeTamper = (Get-FileHash -LiteralPath $journalPath -Algorithm SHA256).Hash
    $linkedPointerPath = Join-Path $movedLinked '.git'
    $linkedPointerOriginal = [IO.File]::ReadAllText($linkedPointerPath)
    $linkedPointerAttributes = [IO.File]::GetAttributes($linkedPointerPath)
    [IO.File]::SetAttributes($linkedPointerPath,[IO.FileAttributes]::Normal)
    [IO.File]::WriteAllText($linkedPointerPath, "gitdir: $gitBase\foreign\.git\worktrees\linked`n", [Text.UTF8Encoding]::new($false))
    $linkedPointerRecovery = Invoke-Mechanism 'Recover' $gitPairsPath $gitEvidence
    Assert-Expected 'foreign linked-worktree pointer is refused before inverse' ($linkedPointerRecovery.ExitCode -ne 0) ($linkedPointerRecovery.Output -join "`n")
    Assert-Equal 'foreign pointer refusal leaves journal unchanged' $journalBeforeTamper (Get-FileHash -LiteralPath $journalPath -Algorithm SHA256).Hash
    Assert-Expected 'foreign pointer refusal leaves moved roots in place' ((Test-Path $movedMain) -and (Test-Path $movedLinked) -and -not (Test-Path $main)) 'inverse occurred'
    [IO.File]::WriteAllText($linkedPointerPath, $linkedPointerOriginal, [Text.UTF8Encoding]::new($false))
    [IO.File]::SetAttributes($linkedPointerPath,$linkedPointerAttributes)
    $adminPointerPath = Join-Path $movedMain '.git\worktrees\linked\gitdir'
    $adminPointerOriginal = [IO.File]::ReadAllText($adminPointerPath)
    $adminPointerAttributes = [IO.File]::GetAttributes($adminPointerPath)
    [IO.File]::SetAttributes($adminPointerPath,[IO.FileAttributes]::Normal)
    [IO.File]::WriteAllText($adminPointerPath, "$gitBase\foreign-linked\.git`n", [Text.UTF8Encoding]::new($false))
    $adminPointerRecovery = Invoke-Mechanism 'Recover' $gitPairsPath $gitEvidence
    Assert-Expected 'foreign Git admin backpointer is refused before inverse' ($adminPointerRecovery.ExitCode -ne 0) ($adminPointerRecovery.Output -join "`n")
    Assert-Equal 'foreign admin refusal leaves journal unchanged' $journalBeforeTamper (Get-FileHash -LiteralPath $journalPath -Algorithm SHA256).Hash
    [IO.File]::WriteAllText($adminPointerPath, $adminPointerOriginal, [Text.UTF8Encoding]::new($false))
    [IO.File]::SetAttributes($adminPointerPath,$adminPointerAttributes)
    $recover = Invoke-Mechanism 'Recover' $gitPairsPath $gitEvidence
    Assert-Expected 'fixture inverse recovery succeeds' ($recover.ExitCode -eq 0) ($recover.Output -join "`n")
    Assert-Expected 'inverse restores both original roots' ((Test-Path $main) -and (Test-Path $linked) -and -not (Test-Path $movedMain) -and -not (Test-Path $movedLinked)) 'source/destination state mismatched'
    Invoke-Git $main @('worktree','repair',$linked) | Out-Null
    foreach ($repo in @($main,$linked)) {
        $after=[ordered]@{
            head=(Invoke-Git $repo @('rev-parse','HEAD'))
            status=(Invoke-Git $repo @('status','--porcelain=v2','--branch','--untracked-files=all','--ignored=matching'))
            staged=(Invoke-Git $repo @('diff','--cached','--binary'))
            unstaged=(Invoke-Git $repo @('diff','--binary'))
            untracked=(Invoke-Git $repo @('ls-files','--others','--exclude-standard'))
            ignored=(Invoke-Git $repo @('ls-files','--others','--ignored','--exclude-standard'))
        }
        Assert-Equal "recovered Git state preserved for $repo" ($gitInitial[$repo] | ConvertTo-Json -Compress) ($after | ConvertTo-Json -Compress)
    }
    Assert-Equal 'no outside-move fixture sentinel was changed' $outsideHash (Get-FileHash -LiteralPath $fixtureSentinel -Algorithm SHA256).Hash
    Write-Output 'PASS moved and inverse-recovered dirty main Git checkout and linked worktree; HEAD/index/staged/unstaged/untracked/ignored state verified after worktree repair'

    # Rehearse the approved ownership shape: Data moves as one root but contains three linked worktrees.
    $fourBase = Join-Path $fixtureRoot 'four-linked-worktrees'
    $fourMain = Join-Path $fourBase 'NeoBabylon-Runtime'
    $fourDirect = Join-Path $fourBase 'NBRT-RouteControl'
    $fourData = Join-Path $fourBase 'NeoBabylon-Data'
    $fourMovedMain = Join-Path $fourBase 'destination\NeoBabylon-Runtime'
    $fourMovedDirect = Join-Path $fourBase 'destination\NBRT-RouteControl'
    $fourMovedData = Join-Path $fourBase 'destination\NeoBabylon-Data'
    $nestedRelative = @(
        'NeoBabylon-Runtime-RouteControl-20260923',
        'qa\P2-13-classification-20260926-01\baseline\runtime',
        'qa\P5-01-clean-baseline-20260925\codex-rs'
    )
    $fourNested = @($nestedRelative | ForEach-Object { Join-Path $fourData $_ })
    $fourMovedNested = @($nestedRelative | ForEach-Object { Join-Path $fourMovedData $_ })
    New-Item -ItemType Directory -Path $fourMain,$fourData -Force | Out-Null
    Invoke-Git $fourMain @('init','--quiet','-b','main') | Out-Null
    Invoke-Git $fourMain @('config','user.name','Workspace fixture') | Out-Null
    Invoke-Git $fourMain @('config','user.email','fixture@example.invalid') | Out-Null
    [IO.File]::WriteAllText((Join-Path $fourMain '.gitignore'), "ignored.txt`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $fourMain 'tracked.txt'), "base`n", [Text.UTF8Encoding]::new($false))
    Invoke-Git $fourMain @('add','--','.gitignore','tracked.txt') | Out-Null
    Invoke-Git $fourMain @('commit','--quiet','-m','four-link fixture baseline') | Out-Null
    $fourLinks = @($fourDirect) + $fourNested
    for ($i=0; $i -lt $fourLinks.Count; $i++) {
        New-Item -ItemType Directory -Path (Split-Path $fourLinks[$i] -Parent) -Force | Out-Null
        Invoke-Git $fourMain @('worktree','add','--quiet','-b',"fixture-link-$i",$fourLinks[$i]) | Out-Null
    }
    $fourWorktreeConfig = Join-Path $fourMain '.git\worktrees\NBRT-RouteControl\config.worktree'
    [IO.File]::WriteAllText($fourWorktreeConfig, '[includeIf "gitdir:fixture-only/"]' + "`n", [Text.UTF8Encoding]::new($false))
    $fourOriginalRepos = @($fourMain) + $fourLinks
    $fourMovedRepos = @($fourMovedMain,$fourMovedDirect) + $fourMovedNested
    for ($i=0; $i -lt $fourOriginalRepos.Count; $i++) {
        $repo = $fourOriginalRepos[$i]
        [IO.File]::WriteAllText((Join-Path $repo 'tracked.txt'), "staged-$i`n", [Text.UTF8Encoding]::new($false))
        Invoke-Git $repo @('add','--','tracked.txt') | Out-Null
        [IO.File]::WriteAllText((Join-Path $repo 'tracked.txt'), "unstaged-$i`n", [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $repo 'untracked.txt'), "untracked-$i", [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $repo 'ignored.txt'), "ignored-$i", [Text.UTF8Encoding]::new($false))
    }
    $fourInitial = [ordered]@{}
    foreach ($repo in $fourOriginalRepos) { $fourInitial[$repo] = Get-GitState $repo }
    $fourManifest = Join-Path $fourBase 'pairs.json'
    $fourEvidence = Join-Path $fourBase 'evidence'
    Write-FixtureManifest $fourManifest @(
        [ordered]@{ source=$fourData; destination=$fourMovedData },
        [ordered]@{ source=$fourDirect; destination=$fourMovedDirect },
        [ordered]@{ source=$fourMain; destination=$fourMovedMain }
    )
    $fourPreflight = Invoke-Mechanism 'Preflight' $fourManifest $fourEvidence
    Assert-Expected 'four-link Data/main fixture preflight succeeds' ($fourPreflight.ExitCode -eq 0) ($fourPreflight.Output -join "`n")
    $fourReport = Get-Content -LiteralPath (Join-Path $fourEvidence 'preflight.json') -Raw | ConvertFrom-Json
    Assert-Equal 'four exact Git bindings are recorded' 4 @($fourReport.gitBindings).Count
    Assert-Expected 'linked-worktree admin config candidate is audited' (@($fourReport.pathSensitiveConfig | Where-Object {
        $_.file -eq $fourWorktreeConfig -and $_.classification -eq 'git-conditional-include' -and $_.worktreeGitAdminCandidate
    }).Count -eq 1) 'config.worktree candidate missing'
    $nestedPointerRows = @($fourReport.pairs[0].entries | Where-Object { $_.path -match '\.git$' -and $_.pointerPathNormalized })
    Assert-Equal 'three nested Data Git pointers are normalized' 3 $nestedPointerRows.Count
    $fourJournal = Join-Path $fourEvidence 'journal.jsonl'
    $earlyNestedPointer = Join-Path $fourNested[1] '.git'
    $earlyNestedOriginal = [IO.File]::ReadAllText($earlyNestedPointer)
    $earlyNestedAttributes = [IO.File]::GetAttributes($earlyNestedPointer)
    [IO.File]::SetAttributes($earlyNestedPointer,[IO.FileAttributes]::Normal)
    [IO.File]::WriteAllText($earlyNestedPointer, "gitdir: $(Join-Path $fourMovedMain '.git\worktrees\runtime')`n", [Text.UTF8Encoding]::new($false))
    $earlyNestedMove = Invoke-Mechanism 'Move' $fourManifest $fourEvidence
    Assert-Expected 'pre-journal Move refuses planned-new nested Git pointer target' ($earlyNestedMove.ExitCode -ne 0) ($earlyNestedMove.Output -join "`n")
    Assert-Expected 'nested planned-new pointer writes no journal or moves roots' ((-not (Test-Path $fourJournal)) -and
        (Test-Path $fourData) -and (Test-Path $fourDirect) -and (Test-Path $fourMain) -and
        (-not (Test-Path $fourMovedData)) -and (-not (Test-Path $fourMovedDirect)) -and (-not (Test-Path $fourMovedMain))) 'journal or rename occurred'
    [IO.File]::WriteAllText($earlyNestedPointer,$earlyNestedOriginal,[Text.UTF8Encoding]::new($false))
    [IO.File]::SetAttributes($earlyNestedPointer,$earlyNestedAttributes)
    $earlyAdminPointer = Join-Path $fourMain '.git\worktrees\runtime\gitdir'
    $earlyAdminOriginal = [IO.File]::ReadAllText($earlyAdminPointer)
    $earlyAdminAttributes = [IO.File]::GetAttributes($earlyAdminPointer)
    [IO.File]::SetAttributes($earlyAdminPointer,[IO.FileAttributes]::Normal)
    [IO.File]::WriteAllText($earlyAdminPointer, "$(Join-Path $fourMovedNested[1] '.git')`n", [Text.UTF8Encoding]::new($false))
    $earlyAdminMove = Invoke-Mechanism 'Move' $fourManifest $fourEvidence
    Assert-Expected 'pre-journal Move refuses planned-new Git admin backpointer target' ($earlyAdminMove.ExitCode -ne 0) ($earlyAdminMove.Output -join "`n")
    Assert-Expected 'planned-new admin backpointer writes no journal or moves roots' ((-not (Test-Path $fourJournal)) -and
        (Test-Path $fourData) -and (Test-Path $fourDirect) -and (Test-Path $fourMain) -and
        (-not (Test-Path $fourMovedData)) -and (-not (Test-Path $fourMovedDirect)) -and (-not (Test-Path $fourMovedMain))) 'journal or rename occurred'
    [IO.File]::WriteAllText($earlyAdminPointer,$earlyAdminOriginal,[Text.UTF8Encoding]::new($false))
    [IO.File]::SetAttributes($earlyAdminPointer,$earlyAdminAttributes)
    $fourMove = Invoke-Mechanism 'Move' $fourManifest $fourEvidence
    Assert-Expected 'Data, NBRT-like link, and main root move in fixture' ($fourMove.ExitCode -eq 0) ($fourMove.Output -join "`n")
    Assert-Expected 'all three original roots are absent' ((-not (Test-Path $fourData)) -and (-not (Test-Path $fourDirect)) -and (-not (Test-Path $fourMain))) 'a source remains'
    Invoke-Git $fourMovedMain (@('worktree','repair',$fourMovedDirect) + $fourMovedNested) | Out-Null
    for ($i=0; $i -lt $fourOriginalRepos.Count; $i++) {
        $repo = $fourMovedRepos[$i]
        Assert-Equal "moved five-worktree state $i" ($fourInitial[$fourOriginalRepos[$i]] | ConvertTo-Json -Compress -Depth 8) ((Get-GitState $repo) | ConvertTo-Json -Compress -Depth 8)
        $common = [IO.Path]::GetFullPath((Invoke-Git $repo @('rev-parse','--path-format=absolute','--git-common-dir')))
        Assert-Expected "moved five-worktree common-dir $i" ($common -ieq (Join-Path $fourMovedMain '.git')) $common
    }
    $fourList = (Invoke-Git $fourMovedMain @('worktree','list','--porcelain')).Replace('/','\')
    foreach ($repo in $fourMovedRepos) { Assert-Expected "moved worktree list contains $repo" ($fourList.Contains($repo,[StringComparison]::OrdinalIgnoreCase)) $fourList }
    $fourJournalHash = (Get-FileHash -LiteralPath $fourJournal -Algorithm SHA256).Hash
    $nestedPointer = Join-Path $fourMovedNested[1] '.git'
    $nestedPointerOriginal = [IO.File]::ReadAllText($nestedPointer)
    $nestedPointerAttributes = [IO.File]::GetAttributes($nestedPointer)
    [IO.File]::SetAttributes($nestedPointer,[IO.FileAttributes]::Normal)
    [IO.File]::WriteAllText($nestedPointer, "gitdir: $fourBase\foreign\.git\worktrees\runtime`n", [Text.UTF8Encoding]::new($false))
    $nestedTamper = Invoke-Mechanism 'Recover' $fourManifest $fourEvidence
    Assert-Expected 'foreign nested Data worktree pointer is refused before inverse' ($nestedTamper.ExitCode -ne 0) ($nestedTamper.Output -join "`n")
    Assert-Equal 'nested pointer refusal leaves journal unchanged' $fourJournalHash (Get-FileHash -LiteralPath $fourJournal -Algorithm SHA256).Hash
    Assert-Expected 'nested pointer refusal leaves Data moved' (Test-Path $fourMovedData) 'Data was inversed'
    [IO.File]::WriteAllText($nestedPointer,$nestedPointerOriginal,[Text.UTF8Encoding]::new($false))
    [IO.File]::SetAttributes($nestedPointer,$nestedPointerAttributes)
    $fourRecover = Invoke-Mechanism 'Recover' $fourManifest $fourEvidence
    Assert-Expected 'four-link inverse recovery succeeds after repair' ($fourRecover.ExitCode -eq 0) ($fourRecover.Output -join "`n")
    Assert-Expected 'Data, NBRT-like link, and main original roots restored' ((Test-Path $fourData) -and (Test-Path $fourDirect) -and (Test-Path $fourMain) -and -not (Test-Path $fourMovedData)) 'inverse paths mismatch'
    Invoke-Git $fourMain (@('worktree','repair',$fourDirect) + $fourNested) | Out-Null
    for ($i=0; $i -lt $fourOriginalRepos.Count; $i++) {
        $repo = $fourOriginalRepos[$i]
        Assert-Equal "recovered five-worktree state $i" ($fourInitial[$repo] | ConvertTo-Json -Compress -Depth 8) ((Get-GitState $repo) | ConvertTo-Json -Compress -Depth 8)
        $common = [IO.Path]::GetFullPath((Invoke-Git $repo @('rev-parse','--path-format=absolute','--git-common-dir')))
        Assert-Expected "recovered five-worktree common-dir $i" ($common -ieq (Join-Path $fourMain '.git')) $common
    }
    Assert-Equal 'five-worktree rehearsal keeps sentinel bytes' $outsideHash (Get-FileHash -LiteralPath $fixtureSentinel -Algorithm SHA256).Hash
    Write-Output 'PASS main + four linked worktrees, including three nested under the moved Data root, repaired and inverse-recovered with dirty state intact'

    # Existing destination collision must be rejected without overwriting.
    $collision = New-OnePairFixture 'collision'
    New-Item -ItemType Directory -Path $collision.Destination -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $collision.Destination 'keep.txt'), 'do-not-overwrite', [Text.UTF8Encoding]::new($false))
    $collisionResult = Invoke-Mechanism 'Preflight' $collision.Manifest $collision.Evidence
    Assert-Expected 'destination collision fails closed' ($collisionResult.ExitCode -ne 0) ($collisionResult.Output -join "`n")
    Assert-Equal 'collision content is untouched' 'do-not-overwrite' ([IO.File]::ReadAllText((Join-Path $collision.Destination 'keep.txt')))

    $projected = New-OnePairFixture 'projected-path-length'
    $projectedDestination = Join-Path $projected.Base ('destination\' + ('p' * 100) + '\' + ('q' * 50) + '\source')
    Write-FixtureManifest $projected.Manifest @([ordered]@{ source=$projected.Source; destination=$projectedDestination })
    $projectedResult = Invoke-Mechanism 'Preflight' $projected.Manifest $projected.Evidence
    Assert-Expected 'projected destination file length is refused' ($projectedResult.ExitCode -ne 0) ($projectedResult.Output -join "`n")
    Assert-Expected 'projected length leaves source in place' (Test-Path (Join-Path $projected.Source 'payload.txt')) 'source missing'

    $highValue = New-OnePairFixture 'high-value-change'
    $lockFile = Join-Path $highValue.Source 'Cargo.lock'
    [IO.File]::WriteAllText($lockFile, 'lock-state-A')
    $highValuePreflight = Invoke-Mechanism 'Preflight' $highValue.Manifest $highValue.Evidence
    Assert-Expected 'high-value baseline preflight succeeds' ($highValuePreflight.ExitCode -eq 0) ($highValuePreflight.Output -join "`n")
    $highValueBaseline = Get-Content -LiteralPath (Join-Path $highValue.Evidence 'preflight.json') -Raw | ConvertFrom-Json
    $lockRow = @($highValueBaseline.pairs[0].entries | Where-Object { $_.path -eq 'Cargo.lock' })[0]
    Assert-Equal 'Cargo.lock is fully byte-hashed' 'sha256' $lockRow.hashMode
    $lockWriteTime = [IO.File]::GetLastWriteTimeUtc($lockFile)
    [IO.File]::WriteAllText($lockFile, 'lock-state-B')
    [IO.File]::SetLastWriteTimeUtc($lockFile,$lockWriteTime)
    $highValueMove = Invoke-Mechanism 'Move' $highValue.Manifest $highValue.Evidence
    Assert-Expected 'same-length and restored-timestamp high-value change fails Move' ($highValueMove.ExitCode -ne 0) ($highValueMove.Output -join "`n")
    Assert-Expected 'high-value change leaves source in place' (Test-Path $lockFile) 'source moved'

    $cache = New-OnePairFixture 'cache-metadata-change'
    $cacheDir = Join-Path $cache.Source 'cache'
    New-Item -ItemType Directory -Path $cacheDir | Out-Null
    $cacheFile = Join-Path $cacheDir 'generated.bin'
    [IO.File]::WriteAllText($cacheFile, 'generated-bytes')
    $cachePreflight = Invoke-Mechanism 'Preflight' $cache.Manifest $cache.Evidence
    Assert-Expected 'cache metadata baseline preflight succeeds' ($cachePreflight.ExitCode -eq 0) ($cachePreflight.Output -join "`n")
    $cacheBaseline = Get-Content -LiteralPath (Join-Path $cache.Evidence 'preflight.json') -Raw | ConvertFrom-Json
    $cacheRow = @($cacheBaseline.pairs[0].entries | Where-Object { $_.path -eq 'cache\generated.bin' })[0]
    Assert-Equal 'generated cache uses metadata-only inventory' 'metadata-only' $cacheRow.hashMode
    Assert-Expected 'generated cache has no content hash' ([string]::IsNullOrEmpty([string]$cacheRow.sha256)) 'content hash was recorded'
    Assert-Equal 'inventory has bounded preflight duration' 30 $cacheBaseline.inventoryPolicy.maxPreflightMinutes
    Assert-Expected 'inventory policy declares one full pass per phase and external quiescence' ($cacheBaseline.inventoryPolicy.fullTreePassSchedule -match 'one preflight, one pre-journal Move, one pre-reconciliation Recover' -and
        $cacheBaseline.inventoryPolicy.externalQuiescenceRequired) 'scan schedule or quiescence boundary missing'
    Assert-Expected 'inventory reports metadata-only bytes' ($cacheBaseline.inventoryPolicy.metadataOnlyBytes -ge [IO.File]::ReadAllBytes($cacheFile).Length) 'metadata count missing'
    [IO.File]::AppendAllText($cacheFile, '-changed')
    $cacheMove = Invoke-Mechanism 'Move' $cache.Manifest $cache.Evidence
    Assert-Expected 'generated cache metadata change fails Move' ($cacheMove.ExitCode -ne 0) ($cacheMove.Output -join "`n")
    Assert-Expected 'cache metadata change leaves source in place' (Test-Path $cacheFile) 'source moved'

    $config = New-OnePairFixture 'config-audit'
    $configProfile = Join-Path $config.Base 'profile'
    $configXdg = Join-Path $config.Base 'xdg'
    $configGitGlobal = Join-Path $config.Base 'specified-global.gitconfig'
    $configCargo = Join-Path $config.Base '.cargo\config.toml'
    $configDestinationCargo = Join-Path $config.Base 'destination\.cargo\config.toml'
    New-Item -ItemType Directory -Path $configProfile,(Join-Path $configXdg 'git'),(Split-Path $configCargo -Parent),(Split-Path $configDestinationCargo -Parent) -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $configProfile '.gitconfig'), ('[includeIf "gitdir:source/"]' + "`n" + 'path = private-profile-value' + "`n"))
    [IO.File]::WriteAllText((Join-Path $configXdg 'git\config'), ('[includeIf "gitdir:source/"]' + "`n" + 'path = private-xdg-value' + "`n"))
    [IO.File]::WriteAllText($configGitGlobal, ('[includeIf "gitdir:source/"]' + "`n" + 'path = private-global-value' + "`n"))
    [IO.File]::WriteAllText($configCargo, "paths = ['private-cargo-value']`n")
    [IO.File]::WriteAllText($configDestinationCargo, "target-dir = 'private-destination-value'`n")
    $savedUserProfile = $env:USERPROFILE; $savedXdg = $env:XDG_CONFIG_HOME; $savedGitGlobal = $env:GIT_CONFIG_GLOBAL
    try {
        $env:USERPROFILE = $configProfile; $env:XDG_CONFIG_HOME = $configXdg; $env:GIT_CONFIG_GLOBAL = $configGitGlobal
        $inheritedPaths = @(& pwsh -NoProfile -Command '$env:USERPROFILE; $env:XDG_CONFIG_HOME; $env:GIT_CONFIG_GLOBAL')
        Assert-Equal 'fixture child inherited USERPROFILE' $configProfile $inheritedPaths[0]
        Assert-Equal 'fixture child inherited XDG_CONFIG_HOME' $configXdg $inheritedPaths[1]
        Assert-Equal 'fixture child inherited GIT_CONFIG_GLOBAL' $configGitGlobal $inheritedPaths[2]
        $configResult = Invoke-Mechanism 'Preflight' $config.Manifest $config.Evidence
    } finally {
        $env:USERPROFILE = $savedUserProfile; $env:XDG_CONFIG_HOME = $savedXdg; $env:GIT_CONFIG_GLOBAL = $savedGitGlobal
    }
    Assert-Expected 'fixture config audit preflight succeeds' ($configResult.ExitCode -eq 0) ($configResult.Output -join "`n")
    $configReportText = Get-Content -LiteralPath (Join-Path $config.Evidence 'preflight.json') -Raw
    $configReport = $configReportText | ConvertFrom-Json
    $observedConfigFiles = @($configReport.pathSensitiveConfig | ForEach-Object { $_.file })
    foreach ($needed in @((Join-Path $configProfile '.gitconfig'),(Join-Path $configXdg 'git\config'),$configGitGlobal,$configCargo,$configDestinationCargo)) {
        Assert-Expected "config audit records $needed" ($observedConfigFiles -contains $needed) ("Observed: " + ($observedConfigFiles -join ', '))
    }
    Assert-Expected 'config audit excludes config values' (-not $configReportText.Contains('private-')) 'configuration value leaked'
    Assert-Expected 'config audit has no presence-only rows' (@($configReport.pathSensitiveConfig | Where-Object { $_.classification -eq 'config-present' }).Count -eq 0) 'presence-only row was reported'
    $destinationCargoObservation = @($configReport.pathSensitiveConfig | Where-Object { $_.file -eq $configDestinationCargo -and $_.classification -eq 'cargo-path-setting' })
    Assert-Expected 'destination-only Cargo config has old/new inherited comparison' ($destinationCargoObservation.Count -eq 1 -and
        @($destinationCargoObservation[0].oldAncestorPairIndexes).Count -eq 0 -and
        @($destinationCargoObservation[0].newAncestorPairIndexes).Count -eq 1 -and
        $destinationCargoObservation[0].newAncestorPairIndexes[0] -eq 0) ($destinationCargoObservation | ConvertTo-Json -Compress)

    $defaultXdg = New-OnePairFixture 'config-default-xdg'
    $defaultXdgPath = Join-Path $configProfile '.config\git\config'
    New-Item -ItemType Directory -Path (Split-Path $defaultXdgPath -Parent) -Force | Out-Null
    [IO.File]::WriteAllText($defaultXdgPath, '[includeIf "gitdir:fixture-only/"]' + "`n", [Text.UTF8Encoding]::new($false))
    try {
        $env:USERPROFILE=$configProfile; $env:XDG_CONFIG_HOME=$null; $env:GIT_CONFIG_GLOBAL='NUL'
        $defaultXdgResult = Invoke-Mechanism 'Preflight' $defaultXdg.Manifest $defaultXdg.Evidence
    } finally {
        $env:USERPROFILE=$savedUserProfile; $env:XDG_CONFIG_HOME=$savedXdg; $env:GIT_CONFIG_GLOBAL=$savedGitGlobal
    }
    Assert-Expected 'default XDG config fixture preflight succeeds' ($defaultXdgResult.ExitCode -eq 0) ($defaultXdgResult.Output -join "`n")
    $defaultXdgReport=Get-Content -LiteralPath (Join-Path $defaultXdg.Evidence 'preflight.json') -Raw | ConvertFrom-Json
    Assert-Expected 'default user-profile XDG Git config candidate is audited' (@($defaultXdgReport.pathSensitiveConfig | Where-Object {
        $_.file -eq $defaultXdgPath -and $_.classification -eq 'git-conditional-include' -and $_.userGitCandidate
    }).Count -eq 1) 'default XDG config candidate missing'

    # Hard-linked source content must not be silently treated as an independent tree.
    $hardlink = New-OnePairFixture 'hardlink'
    $hardlinkOutside = Join-Path $hardlink.Base 'outside-hardlink.txt'
    New-Item -ItemType HardLink -Path $hardlinkOutside -Target (Join-Path $hardlink.Source 'payload.txt') | Out-Null
    $hardlinkResult = Invoke-Mechanism 'Preflight' $hardlink.Manifest (Join-Path $hardlink.Base 'evidence')
    Assert-Expected 'hard-linked source fails closed' ($hardlinkResult.ExitCode -ne 0) ($hardlinkResult.Output -join "`n")
    Assert-Equal 'hard-linked source bytes remain intact' 'fixture-payload' ([IO.File]::ReadAllText((Join-Path $hardlink.Source 'payload.txt')))

    # Reparse point in the destination path must be rejected before a move.
    $reparse = New-OnePairFixture 'reparse'
    $outside = Join-Path $reparse.Base 'outside'
    New-Item -ItemType Directory -Path $outside -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $outside 'sentinel.txt'), 'outside-target', [Text.UTF8Encoding]::new($false))
    $linkPath = Join-Path $reparse.Base 'destination'
    New-Item -ItemType Junction -Path $linkPath -Target $outside | Out-Null
    $reparseResult = Invoke-Mechanism 'Preflight' $reparse.Manifest (Join-Path $reparse.Base 'evidence')
    Assert-Expected 'destination reparse point fails closed' ($reparseResult.ExitCode -ne 0) ($reparseResult.Output -join "`n")
    Assert-Equal 'reparse target content is untouched' 'outside-target' ([IO.File]::ReadAllText((Join-Path $outside 'sentinel.txt')))
    Assert-Expected 'reparse source remains in place' (Test-Path (Join-Path $reparse.Source 'payload.txt')) 'source was altered'

    $lateDestination = New-OnePairFixture 'late-destination-reparse'
    $lateDestinationPreflight = Invoke-Mechanism 'Preflight' $lateDestination.Manifest $lateDestination.Evidence
    Assert-Expected 'late destination baseline preflight succeeds' ($lateDestinationPreflight.ExitCode -eq 0) ($lateDestinationPreflight.Output -join "`n")
    $lateDestinationTarget = Join-Path $lateDestination.Base 'outside-target'
    New-Item -ItemType Directory -Path $lateDestinationTarget -Force | Out-Null
    New-Item -ItemType Junction -Path (Join-Path $lateDestination.Base 'destination') -Target $lateDestinationTarget | Out-Null
    $lateDestinationMove = Invoke-Mechanism 'Move' $lateDestination.Manifest $lateDestination.Evidence
    Assert-Expected 'destination parent reparse introduced after preflight is refused' ($lateDestinationMove.ExitCode -ne 0) ($lateDestinationMove.Output -join "`n")
    Assert-Expected 'late destination reparse leaves source unmoved' (Test-Path (Join-Path $lateDestination.Source 'payload.txt')) 'source moved'
    Assert-Expected 'late destination reparse writes no journal' (-not (Test-Path (Join-Path $lateDestination.Evidence 'journal.jsonl'))) 'journal was written'

    $sourceRootReparse = New-OnePairFixture 'source-root-reparse'
    Assert-Expected 'source root reparse baseline preflight succeeds' ((Invoke-Mechanism 'Preflight' $sourceRootReparse.Manifest $sourceRootReparse.Evidence).ExitCode -eq 0) 'preflight failed'
    $sourceRootPreserved = Join-Path $sourceRootReparse.Base 'source-preserved'
    [IO.Directory]::Move($sourceRootReparse.Source,$sourceRootPreserved)
    New-Item -ItemType Junction -Path $sourceRootReparse.Source -Target $sourceRootPreserved | Out-Null
    $sourceRootMove = Invoke-Mechanism 'Move' $sourceRootReparse.Manifest $sourceRootReparse.Evidence
    Assert-Expected 'source root reparse introduced before forward rename is refused' ($sourceRootMove.ExitCode -ne 0) ($sourceRootMove.Output -join "`n")
    Assert-Expected 'source root reparse writes no journal' (-not (Test-Path (Join-Path $sourceRootReparse.Evidence 'journal.jsonl'))) 'journal was written'
    Assert-Equal 'source root reparse keeps target bytes' 'fixture-payload' ([IO.File]::ReadAllText((Join-Path $sourceRootPreserved 'payload.txt')))
    [IO.Directory]::Delete($sourceRootReparse.Source)
    [IO.Directory]::Move($sourceRootPreserved,$sourceRootReparse.Source)

    $inverseRootReparse = New-OnePairFixture 'inverse-root-reparse'
    Assert-Expected 'inverse root reparse baseline preflight succeeds' ((Invoke-Mechanism 'Preflight' $inverseRootReparse.Manifest $inverseRootReparse.Evidence).ExitCode -eq 0) 'preflight failed'
    Assert-Expected 'inverse root reparse fixture move succeeds' ((Invoke-Mechanism 'Move' $inverseRootReparse.Manifest $inverseRootReparse.Evidence).ExitCode -eq 0) 'fixture move failed'
    $inverseRootPreserved = Join-Path $inverseRootReparse.Base 'destination-preserved'
    [IO.Directory]::Move($inverseRootReparse.Destination,$inverseRootPreserved)
    New-Item -ItemType Junction -Path $inverseRootReparse.Destination -Target $inverseRootPreserved | Out-Null
    $inverseRootJournal = Join-Path $inverseRootReparse.Evidence 'journal.jsonl'
    $inverseRootJournalHash = (Get-FileHash -LiteralPath $inverseRootJournal -Algorithm SHA256).Hash
    $inverseRootRecover = Invoke-Mechanism 'Recover' $inverseRootReparse.Manifest $inverseRootReparse.Evidence
    Assert-Expected 'destination root reparse introduced before inverse rename is refused' ($inverseRootRecover.ExitCode -ne 0) ($inverseRootRecover.Output -join "`n")
    Assert-Equal 'inverse root reparse refusal leaves journal unchanged' $inverseRootJournalHash (Get-FileHash -LiteralPath $inverseRootJournal -Algorithm SHA256).Hash
    Assert-Equal 'inverse root reparse keeps target bytes' 'fixture-payload' ([IO.File]::ReadAllText((Join-Path $inverseRootPreserved 'payload.txt')))
    [IO.Directory]::Delete($inverseRootReparse.Destination)
    [IO.Directory]::Move($inverseRootPreserved,$inverseRootReparse.Destination)
    Assert-Expected 'inverse root reparse fixture then recovers' ((Invoke-Mechanism 'Recover' $inverseRootReparse.Manifest $inverseRootReparse.Evidence).ExitCode -eq 0) 'recovery failed'

    $lateSourceBase = Join-Path $fixtureRoot 'late-source-reparse'
    $lateSource = Join-Path $lateSourceBase 'source-parent\source'
    $lateSourceDestination = Join-Path $lateSourceBase 'destination\source'
    $lateSourceManifest = Join-Path $lateSourceBase 'pairs.json'
    $lateSourceEvidence = Join-Path $lateSourceBase 'evidence'
    New-Item -ItemType Directory -Path $lateSource -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $lateSource 'payload.txt'), 'inverse-parent-fixture')
    Write-FixtureManifest $lateSourceManifest @([ordered]@{ source=$lateSource; destination=$lateSourceDestination })
    Assert-Expected 'inverse parent baseline preflight succeeds' ((Invoke-Mechanism 'Preflight' $lateSourceManifest $lateSourceEvidence).ExitCode -eq 0) 'preflight failed'
    Assert-Expected 'inverse parent fixture move succeeds' ((Invoke-Mechanism 'Move' $lateSourceManifest $lateSourceEvidence).ExitCode -eq 0) 'fixture move failed'
    [IO.Directory]::Delete((Split-Path $lateSource -Parent))
    $lateSourceOutside = Join-Path $lateSourceBase 'outside-target'
    New-Item -ItemType Directory -Path $lateSourceOutside -Force | Out-Null
    New-Item -ItemType Junction -Path (Split-Path $lateSource -Parent) -Target $lateSourceOutside | Out-Null
    $lateSourceJournal = Join-Path $lateSourceEvidence 'journal.jsonl'
    $lateSourceJournalHash = (Get-FileHash -LiteralPath $lateSourceJournal -Algorithm SHA256).Hash
    $lateSourceRecover = Invoke-Mechanism 'Recover' $lateSourceManifest $lateSourceEvidence
    Assert-Expected 'source parent reparse introduced before inverse is refused' ($lateSourceRecover.ExitCode -ne 0) ($lateSourceRecover.Output -join "`n")
    Assert-Equal 'inverse parent refusal leaves journal unchanged' $lateSourceJournalHash (Get-FileHash -LiteralPath $lateSourceJournal -Algorithm SHA256).Hash
    Assert-Expected 'inverse parent refusal leaves destination intact' (Test-Path (Join-Path $lateSourceDestination 'payload.txt')) 'destination moved'

    # A changed tree after Preflight must prevent the first rename.
    $changed = New-OnePairFixture 'changed-source'
    $changedEvidence = Join-Path $changed.Base 'evidence'
    $changedPreflight = Invoke-Mechanism 'Preflight' $changed.Manifest $changedEvidence
    Assert-Expected 'changed-source baseline preflight succeeds' ($changedPreflight.ExitCode -eq 0) ($changedPreflight.Output -join "`n")
    [IO.File]::AppendAllText((Join-Path $changed.Source 'payload.txt'), '-changed')
    $changedMove = Invoke-Mechanism 'Move' $changed.Manifest $changedEvidence
    Assert-Expected 'changed source fails closed before move' ($changedMove.ExitCode -ne 0) ($changedMove.Output -join "`n")
    Assert-Expected 'changed source stays at original path' (Test-Path (Join-Path $changed.Source 'payload.txt')) 'source missing'
    Assert-Expected 'changed source destination remains absent' (-not (Test-Path $changed.Destination)) 'destination unexpectedly exists'

    $rootSwap = New-OnePairFixture 'root-identity-swap'
    Assert-Expected 'root identity swap baseline preflight succeeds' ((Invoke-Mechanism 'Preflight' $rootSwap.Manifest $rootSwap.Evidence).ExitCode -eq 0) 'preflight failed'
    $rootSwapOriginal = Join-Path $rootSwap.Base 'source-preserved'
    [IO.Directory]::Move($rootSwap.Source,$rootSwapOriginal)
    New-Item -ItemType Directory -Path $rootSwap.Source | Out-Null
    [IO.File]::Copy((Join-Path $rootSwapOriginal 'payload.txt'),(Join-Path $rootSwap.Source 'payload.txt'))
    $rootSwapMove = Invoke-Mechanism 'Move' $rootSwap.Manifest $rootSwap.Evidence
    Assert-Expected 'byte-identical replacement root is refused by file ID' ($rootSwapMove.ExitCode -ne 0 -and
        (($rootSwapMove.Output -join "`n") -match 'file ID or volume serial changed')) ($rootSwapMove.Output -join "`n")
    Assert-Expected 'identity swap leaves both fixture roots in place' ((Test-Path (Join-Path $rootSwapOriginal 'payload.txt')) -and
        (Test-Path (Join-Path $rootSwap.Source 'payload.txt')) -and -not (Test-Path $rootSwap.Destination)) 'fixture roots changed'
    Assert-Expected 'identity swap writes no journal' (-not (Test-Path (Join-Path $rootSwap.Evidence 'journal.jsonl'))) 'journal was written'

    # Reconcile a crash after rename but before its completion record, then inverse it.
    $interrupted = New-OnePairFixture 'interrupted-intent'
    $interruptedEvidence = Join-Path $interrupted.Base 'evidence'
    $interruptedPreflight = Invoke-Mechanism 'Preflight' $interrupted.Manifest $interruptedEvidence
    Assert-Expected 'interrupted-intent baseline preflight succeeds' ($interruptedPreflight.ExitCode -eq 0) ($interruptedPreflight.Output -join "`n")
    $baseline = Get-Content -Raw -LiteralPath (Join-Path $interruptedEvidence 'preflight.json') | ConvertFrom-Json
    $journal = Join-Path $interruptedEvidence 'journal.jsonl'
    $session = [ordered]@{ schemaVersion=1; event='session'; runId=$baseline.runId; manifestSha256=$baseline.manifestSha256; baselineSha256=$baseline.baselineSha256; pairCount=$baseline.pairs.Count }
    $intent = [ordered]@{ schemaVersion=1; event='move-intent'; pairIndex=0; source=$baseline.pairs[0].source; destination=$baseline.pairs[0].destination; sourceFingerprint=$baseline.pairs[0].sourceFingerprint }
    [IO.File]::WriteAllText($journal, (($session | ConvertTo-Json -Compress) + "`n" + ($intent | ConvertTo-Json -Compress) + "`n"), [Text.UTF8Encoding]::new($false))
    [IO.Directory]::CreateDirectory((Split-Path $interrupted.Destination -Parent)) | Out-Null
    [IO.Directory]::Move($interrupted.Source,$interrupted.Destination)
    $reconcile = Invoke-Mechanism 'Recover' $interrupted.Manifest $interruptedEvidence
    Assert-Expected 'interrupted moved intent is reconciled and inversed' ($reconcile.ExitCode -eq 0) ($reconcile.Output -join "`n")
    Assert-Expected 'reconciled interruption restores original tree' ((Test-Path (Join-Path $interrupted.Source 'payload.txt')) -and -not (Test-Path $interrupted.Destination)) 'recovery state mismatched'

    # Both names existing for an unmatched intent is ambiguous and must write nothing to either tree.
    $ambiguous = New-OnePairFixture 'ambiguous-intent'
    $ambiguousEvidence = Join-Path $ambiguous.Base 'evidence'
    $ambiguousPreflight = Invoke-Mechanism 'Preflight' $ambiguous.Manifest $ambiguousEvidence
    Assert-Expected 'ambiguous-intent baseline preflight succeeds' ($ambiguousPreflight.ExitCode -eq 0) ($ambiguousPreflight.Output -join "`n")
    $ambiguousBaseline = Get-Content -Raw -LiteralPath (Join-Path $ambiguousEvidence 'preflight.json') | ConvertFrom-Json
    $ambiguousJournal = Join-Path $ambiguousEvidence 'journal.jsonl'
    $ambiguousSession = [ordered]@{ schemaVersion=1; event='session'; runId=$ambiguousBaseline.runId; manifestSha256=$ambiguousBaseline.manifestSha256; baselineSha256=$ambiguousBaseline.baselineSha256; pairCount=$ambiguousBaseline.pairs.Count }
    $ambiguousIntent = [ordered]@{ schemaVersion=1; event='move-intent'; pairIndex=0; source=$ambiguousBaseline.pairs[0].source; destination=$ambiguousBaseline.pairs[0].destination; sourceFingerprint=$ambiguousBaseline.pairs[0].sourceFingerprint }
    [IO.File]::WriteAllText($ambiguousJournal, (($ambiguousSession | ConvertTo-Json -Compress) + "`n" + ($ambiguousIntent | ConvertTo-Json -Compress) + "`n"), [Text.UTF8Encoding]::new($false))
    New-Item -ItemType Directory -Path $ambiguous.Destination -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $ambiguous.Destination 'foreign.txt'), 'foreign', [Text.UTF8Encoding]::new($false))
    $ambiguousSourceHash = (Get-FileHash -LiteralPath (Join-Path $ambiguous.Source 'payload.txt') -Algorithm SHA256).Hash
    $ambiguousDestHash = (Get-FileHash -LiteralPath (Join-Path $ambiguous.Destination 'foreign.txt') -Algorithm SHA256).Hash
    $ambiguousRecover = Invoke-Mechanism 'Recover' $ambiguous.Manifest $ambiguousEvidence
    Assert-Expected 'ambiguous intent is refused' ($ambiguousRecover.ExitCode -ne 0) ($ambiguousRecover.Output -join "`n")
    Assert-Equal 'ambiguous source remains unchanged' $ambiguousSourceHash (Get-FileHash -LiteralPath (Join-Path $ambiguous.Source 'payload.txt') -Algorithm SHA256).Hash
    Assert-Equal 'ambiguous destination remains unchanged' $ambiguousDestHash (Get-FileHash -LiteralPath (Join-Path $ambiguous.Destination 'foreign.txt') -Algorithm SHA256).Hash

    Write-Output 'PASS fixture-only workspace-consolidation recovery suite'
}
finally {
    $env:GIT_CONFIG_GLOBAL = $oldGitConfigGlobal
    $env:GIT_CONFIG_NOSYSTEM = $oldGitConfigSystem
    $env:GIT_OPTIONAL_LOCKS = $oldGitOptionalLocks
    if (Test-Path -LiteralPath $fixtureRoot) {
        $resolved = [IO.Path]::GetFullPath($fixtureRoot)
        $expectedPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
        if (-not $resolved.StartsWith($expectedPrefix,[StringComparison]::OrdinalIgnoreCase) -or
            (Split-Path -Leaf $resolved) -notmatch '^NeoBabylon-WorkspaceConsolidation-[0-9a-f]{32}$') {
            throw "Refusing to remove an unexpected fixture path '$resolved'."
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
