$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot '..\..\scripts\WorkspaceConsolidationBackup.ps1'
$scriptPath = [IO.Path]::GetFullPath($scriptPath)
$fixtureName = 'NeoBabylon-WorkspaceConsolidationBackup-' + [Guid]::NewGuid().ToString('N')
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) $fixtureName
$sentinelPath = Join-Path $fixtureRoot 'outside-move-sentinel.txt'
$passed = 0

if (-not ('WorkspaceConsolidationBackupFixture.HardLinkApi' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace WorkspaceConsolidationBackupFixture {
  public static class HardLinkApi {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    public static extern bool CreateHardLink(string linkName, string existingName, IntPtr securityAttributes);
  }
}
'@
}

function Assert-That([string]$Name, [bool]$Condition, [string]$Detail = '') {
    if (-not $Condition) { throw "FAIL $Name $Detail" }
    $script:passed++
    Write-Output "PASS $Name"
}

function Write-FixtureManifest([string]$Name, [string[]]$Sources, [string[]]$Destinations) {
    $rows = for ($i = 0; $i -lt $Sources.Count; $i++) {
        [ordered]@{ source=$Sources[$i]; destination=$Destinations[$i] }
    }
    $path = Join-Path $fixtureRoot "$Name.json"
    [IO.File]::WriteAllText($path, (ConvertTo-Json -InputObject @($rows) -Depth 4), [Text.UTF8Encoding]::new($false))
    return $path
}

function Invoke-Backup([string]$Manifest, [string]$Evidence, [string[]]$Extra = @()) {
    $args = @('-NoProfile','-File',$scriptPath,'-Mode','Capture','-ManifestPath',$Manifest,
        '-EvidenceRoot',$Evidence,'-Fixture','-FixtureRoot',$fixtureRoot) + $Extra
    $output = @(& pwsh @args 2>&1 | ForEach-Object { [string]$_ })
    return [pscustomobject]@{ ExitCode=$LASTEXITCODE; Output=$output }
}

if (-not (Get-Command pwsh -ErrorAction SilentlyContinue)) { throw 'The fixture requires PowerShell 7 (pwsh).' }
if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) { throw "Backup script not found: $scriptPath" }
$backupScriptText = Get-Content -LiteralPath $scriptPath -Raw
if ([IO.Path]::GetDirectoryName($fixtureRoot) -ine [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')) {
    throw 'Fixture root must be a direct child of the system temp directory.'
}

New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
[IO.File]::WriteAllText($sentinelPath, 'outside fixture sentinel', [Text.UTF8Encoding]::new($false))
$outsideHashBefore = (Get-FileHash -LiteralPath $sentinelPath -Algorithm SHA256).Hash
try {
    $scope = Join-Path $fixtureRoot 'scope'
    $product = Join-Path $scope 'NeoBabylon'
    $runtime = Join-Path $scope 'NeoBabylon-Runtime'
    $data = Join-Path $scope 'NeoBabylon-Data'
    $probe = Join-Path $scope 'NeoBabylon-Probe-1'
    foreach ($root in @($product,$runtime,$data,$probe)) { New-Item -ItemType Directory -Path $root -Force | Out-Null }

    [IO.File]::WriteAllText((Join-Path $product 'README.md'), 'fixture source content')
    [IO.File]::WriteAllText((Join-Path $product 'credential.local.json'), 'must be excluded from source snapshot')
    [IO.File]::WriteAllText((Join-Path $product '.env'), 'must be excluded')
    [IO.File]::WriteAllText((Join-Path $product '.env.example'), 'safe documented example')
    New-Item -ItemType Directory -Path (Join-Path $product '.LOCAL') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $product '.LOCAL\must-exclude.txt'), 'case-insensitive local exclusion')
    New-Item -ItemType Directory -Path (Join-Path $product 'data'),(Join-Path $product 'src\data') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $product 'data\root-generated.txt'), 'exact root generated path')
    [IO.File]::WriteAllText((Join-Path $product 'src\data\ordinary-source.txt'), 'nested data is ordinary source')
    New-Item -ItemType Directory -Path (Join-Path $product '.local\QA\Source-Snapshot-fixture') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $product '.local\QA\Source-Snapshot-fixture\must-not-archive.txt'), 'existing local snapshot')
    New-Item -ItemType Directory -Path (Join-Path $product 'empty-directory') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $runtime 'Cargo.lock'), 'fixture cargo lock')
    [IO.File]::WriteAllText((Join-Path $runtime 'src.rs'), 'fixture runtime source')
    [IO.File]::WriteAllText((Join-Path $data 'state.db'), 'fixture database bytes')
    [IO.File]::WriteAllText((Join-Path $data 'state.db-wal'), 'fixture WAL bytes')
    [IO.File]::WriteAllText((Join-Path $data 'state.db-shm'), 'fixture SHM bytes')
    [IO.File]::WriteAllText((Join-Path $data 'state.db-journal'), 'fixture journal bytes')
    [IO.File]::WriteAllText((Join-Path $data 'durable-record.json'), '{"fixture":true}')
    New-Item -ItemType Directory -Path (Join-Path $runtime '.git\worktrees\link1') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $runtime '.git\worktrees\link1\gitdir'), (Join-Path $probe '.git'))
    [IO.File]::WriteAllText((Join-Path $probe '.git'), "gitdir: $(Join-Path $runtime '.git\worktrees\link1')`n")
    [IO.File]::WriteAllText((Join-Path $probe 'history.txt'), 'preserve historical probe content')
    $linkedFixtureRoots = @($probe)
    for ($i = 2; $i -le 4; $i++) {
        $linkedRoot = Join-Path $data "linked-$i"
        New-Item -ItemType Directory -Path $linkedRoot -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $runtime ".git\worktrees\link$i") -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $linkedRoot '.git'), "gitdir: $(Join-Path $runtime ".git\worktrees\link$i")`n")
        [IO.File]::WriteAllText((Join-Path $runtime ".git\worktrees\link$i\gitdir"), (Join-Path $linkedRoot '.git'))
        $linkedFixtureRoots += $linkedRoot
    }

    $destinations = @(
        (Join-Path $fixtureRoot 'dest\NeoBabylon'),
        (Join-Path $fixtureRoot 'dest\NeoBabylon-Runtime'),
        (Join-Path $fixtureRoot 'dest\NeoBabylon-Data'),
        (Join-Path $fixtureRoot 'dest\NeoBabylon-Probe-1')
    )
    $manifest = Write-FixtureManifest 'valid-scope' @($product,$runtime,$data,$probe) $destinations
    $evidence = Join-Path $fixtureRoot 'evidence\valid'
    $captured = Invoke-Backup $manifest $evidence
    Assert-That 'fixture backup captures and reads back its declared scope' ($captured.ExitCode -eq 0) ($captured.Output -join "`n")
    $evidenceAcl = Get-Acl -LiteralPath $evidence
    $evidenceSids = @($evidenceAcl.Access | ForEach-Object { $_.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value })
    $trustedSids = @([Security.Principal.WindowsIdentity]::GetCurrent().User.Value,'S-1-5-18','S-1-5-32-544')
    Assert-That 'evidence DACL is protected from parent inheritance' ([bool]$evidenceAcl.AreAccessRulesProtected)
    Assert-That 'evidence DACL grants only current user, SYSTEM, and Administrators' (@($evidenceSids | Where-Object { $_ -notin $trustedSids }).Count -eq 0 -and @($trustedSids | Where-Object { $_ -notin $evidenceSids }).Count -eq 0)
    Assert-That 'evidence DACL contains no inherited ACEs' (@($evidenceAcl.Access | Where-Object IsInherited).Count -eq 0)
    $markerPath = Join-Path $evidence 'backup-ready.json'
    Assert-That 'capture emits the machine-readable marker' (Test-Path -LiteralPath $markerPath -PathType Leaf)
    $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
    Assert-That 'fixture marker is clearly fixture-only' ($marker.scope.kind -eq 'fixture' -and $marker.status -eq 'fixture-verified')
    Assert-That 'marker hashes the exact manifest' ($marker.manifest.sha256 -match '^[0-9a-f]{64}$')
    Assert-That 'marker records external quiescence assertion without claiming proof' ($marker.quiescence.operatorAssertion -and -not $marker.quiescence.scriptProvesQuiescence)
    Assert-That 'marker contains no fixture secret value' (-not ((Get-Content -LiteralPath $markerPath -Raw) -match 'must be excluded from source snapshot'))
    Assert-That 'Task0 local-file exclusion is recorded' ([regex]::IsMatch(($marker.scope.exclusions.files -join "`n"), '\*\.local\.\*'))
    Assert-That 'all four extant SQLite members are recorded' (@($marker.sqlite.groups[0].members).Count -eq 4)
    Assert-That 'linked worktree pointer and admin metadata are recorded' (@($marker.gitArtifacts).Count -ge 2)
    Assert-That 'all four linked-worktree pointers are separately inventoried' (@($marker.gitArtifacts | Where-Object kind -eq 'linked-worktree-pointer').Count -eq 4)
    Assert-That 'all four Git admin backpointers are separately inventoried' (@($marker.gitArtifacts | Where-Object kind -eq 'git-admin-backpointer').Count -eq 4)
    Assert-That 'live data qualification remains explicitly non-ready' ($marker.liveQualification.unclassifiedLiveDataBlocksReady -and $marker.liveQualification.status -eq 'not-ready')
    Assert-That 'marker explicitly describes non-flattening link handling' ($marker.linkPolicy.hardLinks -match 'does not recreate topology' -and $marker.linkPolicy.reparse -match 'without traversal' -and -not $marker.linkPolicy.broaderAllowance)
    Assert-That 'artifact hashes are present for independent checking' (@($marker.artifacts | Where-Object { $_.sha256 -match '^[0-9a-f]{64}$' }).Count -gt 0)
    $productScope = $marker.scope.roots | Where-Object label -eq 'NeoBabylon' | Select-Object -First 1
    $inventoryCount = @($productScope.inventory).Count
    $inventoryFileCount = @($productScope.inventory | Where-Object type -eq 'file').Count
    $inventoryBytes = [long](($productScope.inventory | Where-Object type -eq 'file' | Measure-Object length -Sum).Sum)
    $nonSingleLinks = @($productScope.inventory | Where-Object { $_.type -eq 'file' -and $_.linkCount -ne 1 }).Count
    $inventoryIsSound = $productScope.entryCount -eq $inventoryCount -and $productScope.fileCount -eq $inventoryFileCount -and $productScope.byteLength -eq $inventoryBytes -and $nonSingleLinks -eq 0
    Assert-That 'inventory records path, type, count, length, and file link count' $inventoryIsSound "entries=$($productScope.entryCount)/$inventoryCount files=$($productScope.fileCount)/$inventoryFileCount bytes=$($productScope.byteLength)/$inventoryBytes nonSingleLinks=$nonSingleLinks"
    Assert-That 'source archive preserves empty directories' (Test-Path -LiteralPath (Join-Path $evidence 'artifacts\NeoBabylon\empty-directory') -PathType Container)
    $localLeaks = @($marker.artifacts | Where-Object { $_.relativePath -clike '.local\QA\*\must-not-archive.txt' })
    Assert-That 'existing product .local snapshot is excluded from source archive' ($localLeaks.Count -eq 0) ($localLeaks.relativePath -join ',')
    Assert-That 'Task0 excludes .env and retains .env.example' (@($marker.artifacts | Where-Object relativePath -eq '.env').Count -eq 0 -and @($marker.artifacts | Where-Object relativePath -eq '.env.example').Count -eq 1)
    $upperLocalArtifact = @($marker.artifacts | Where-Object { $_.relativePath.Replace('\','/').ToLowerInvariant() -ceq '.local/must-exclude.txt' })
    Assert-That 'Task0 excludes .LOCAL case-insensitively' ($upperLocalArtifact.Count -eq 0)
    $dataRootArtifact = @($marker.artifacts | Where-Object { $_.relativePath.Replace('\','/') -ceq 'data/root-generated.txt' })
    $nestedDataArtifact = @($marker.artifacts | Where-Object { $_.relativePath.Replace('\','/') -ceq 'src/data/ordinary-source.txt' })
    Assert-That 'Task0 exact root data path is excluded but nested src/data is included' ($dataRootArtifact.Count -eq 0 -and $nestedDataArtifact.Count -eq 1)

    $changedArtifact = $marker.artifacts | Where-Object { $_.relativePath -like '*durable-record.json' } | Select-Object -First 1
    $changedPath = Join-Path $evidence $changedArtifact.backupPath
    [IO.File]::AppendAllText($changedPath, 'tamper')
    $verifyChanged = @(& pwsh -NoProfile -File $scriptPath -Mode Verify -ManifestPath $manifest -EvidenceRoot $evidence -Fixture -FixtureRoot $fixtureRoot 2>&1 | ForEach-Object { [string]$_ })
    Assert-That 'verification rejects a changed backup artifact' ($LASTEXITCODE -ne 0) ($verifyChanged -join "`n")

    $badSidecarEvidence = Join-Path $fixtureRoot 'evidence\missing-sidecar'
    $badSidecar = Invoke-Backup $manifest $badSidecarEvidence @('-TestRemoveSidecarDuringCapture','state.db-wal')
    Assert-That 'capture fails when an extant SQLite sidecar disappears' ($badSidecar.ExitCode -ne 0) ($badSidecar.Output -join "`n")
    Assert-That 'missing sidecar cannot produce a ready marker' (-not (Test-Path (Join-Path $badSidecarEvidence 'backup-ready.json')))

    $overlap = Write-FixtureManifest 'ambiguous-scope' @($data,(Join-Path $data 'nested')) @(
        (Join-Path $fixtureRoot 'dest\one'),(Join-Path $fixtureRoot 'dest\two'))
    New-Item -ItemType Directory -Path (Join-Path $data 'nested') -Force | Out-Null
    $ambiguous = Invoke-Backup $overlap (Join-Path $fixtureRoot 'evidence\ambiguous')
    Assert-That 'capture rejects overlapping ambiguous root scopes' ($ambiguous.ExitCode -ne 0) ($ambiguous.Output -join "`n")

    $readbackEvidence = Join-Path $fixtureRoot 'evidence\readback-failure'
    $firstCapture = Invoke-Backup $manifest $readbackEvidence
    Assert-That 'separate fixture is captured before readback corruption' ($firstCapture.ExitCode -eq 0) ($firstCapture.Output -join "`n")
    $readbackMarker = Get-Content (Join-Path $readbackEvidence 'backup-ready.json') -Raw | ConvertFrom-Json
    $artifact = $readbackMarker.artifacts | Where-Object { $_.relativePath -like '*history.txt' } | Select-Object -First 1
    [IO.File]::WriteAllText((Join-Path $readbackEvidence $artifact.backupPath), 'corrupt archive payload')
    $verifyReadback = @(& pwsh -NoProfile -File $scriptPath -Mode Verify -ManifestPath $manifest -EvidenceRoot $readbackEvidence -Fixture -FixtureRoot $fixtureRoot 2>&1 | ForEach-Object { [string]$_ })
    Assert-That 'readback rejects payload corruption' ($LASTEXITCODE -ne 0) ($verifyReadback -join "`n")

    $missingEvidence = Join-Path $fixtureRoot 'evidence\missing-readback'
    $missingCapture = Invoke-Backup $manifest $missingEvidence
    Assert-That 'separate fixture is captured before missing-member check' ($missingCapture.ExitCode -eq 0) ($missingCapture.Output -join "`n")
    $missingMarker = Get-Content (Join-Path $missingEvidence 'backup-ready.json') -Raw | ConvertFrom-Json
    $missingArtifact = $missingMarker.artifacts | Where-Object { $_.relativePath -like '*history.txt' } | Select-Object -First 1
    Remove-Item -LiteralPath (Join-Path $missingEvidence $missingArtifact.backupPath) -Force
    $verifyMissing = @(& pwsh -NoProfile -File $scriptPath -Mode Verify -ManifestPath $manifest -EvidenceRoot $missingEvidence -Fixture -FixtureRoot $fixtureRoot 2>&1 | ForEach-Object { [string]$_ })
    Assert-That 'readback rejects a missing artifact' ($LASTEXITCODE -ne 0) ($verifyMissing -join "`n")

    $removedEntryEvidence = Join-Path $fixtureRoot 'evidence\removed-entry'
    $removedEntryCapture = Invoke-Backup $manifest $removedEntryEvidence
    Assert-That 'separate fixture captured before coordinated artifact/marker deletion' ($removedEntryCapture.ExitCode -eq 0) ($removedEntryCapture.Output -join "`n")
    $removedEntryMarkerPath = Join-Path $removedEntryEvidence 'backup-ready.json'
    $removedEntryMarker = Get-Content $removedEntryMarkerPath -Raw | ConvertFrom-Json
    $removeRecord = $removedEntryMarker.artifacts | Where-Object relativePath -eq 'history.txt' | Select-Object -First 1
    Remove-Item -LiteralPath (Join-Path $removedEntryEvidence $removeRecord.backupPath) -Force
    $removedEntryMarker.artifacts = @($removedEntryMarker.artifacts | Where-Object { $_.backupPath -cne $removeRecord.backupPath })
    [IO.File]::WriteAllText($removedEntryMarkerPath,($removedEntryMarker | ConvertTo-Json -Depth 24),[Text.UTF8Encoding]::new($false))
    $verifyRemovedEntry = @(& pwsh -NoProfile -File $scriptPath -Mode Verify -ManifestPath $manifest -EvidenceRoot $removedEntryEvidence -Fixture -FixtureRoot $fixtureRoot 2>&1 | ForEach-Object { [string]$_ })
    Assert-That 'verification rejects artifact removed from both archive and marker' ($LASTEXITCODE -ne 0) ($verifyRemovedEntry -join "`n")

    $readyFlipEvidence = Join-Path $fixtureRoot 'evidence\ready-flip'
    $readyFlipCapture = Invoke-Backup $manifest $readyFlipEvidence
    Assert-That 'separate fixture captured before ready-claim mutation' ($readyFlipCapture.ExitCode -eq 0) ($readyFlipCapture.Output -join "`n")
    $readyFlipPath = Join-Path $readyFlipEvidence 'backup-ready.json'
    $readyFlipMarker = Get-Content $readyFlipPath -Raw | ConvertFrom-Json
    $readyFlipMarker.ready = $true
    $readyFlipMarker.status = 'ready'
    [IO.File]::WriteAllText($readyFlipPath,($readyFlipMarker | ConvertTo-Json -Depth 24),[Text.UTF8Encoding]::new($false))
    $verifyReadyFlip = @(& pwsh -NoProfile -File $scriptPath -Mode Verify -ManifestPath $manifest -EvidenceRoot $readyFlipEvidence -Fixture -FixtureRoot $fixtureRoot 2>&1 | ForEach-Object { [string]$_ })
    Assert-That 'fixture verifier rejects a tampered ready claim' ($LASTEXITCODE -ne 0) ($verifyReadyFlip -join "`n")

    $outsideHash = (Get-FileHash -LiteralPath $sentinelPath -Algorithm SHA256).Hash
    Assert-That 'fixture operations preserve the outside sentinel' ($outsideHashBefore -eq $outsideHash)

    $linkSource = Join-Path $fixtureRoot 'scope\NeoBabylon-Probe-LinkTest'
    $linkTarget = Join-Path $fixtureRoot 'reparse-target'
    New-Item -ItemType Directory -Path $linkSource,$linkTarget -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $linkTarget 'canary.txt'), 'must not be traversed')
    New-Item -ItemType Junction -Path (Join-Path $linkSource 'outside-junction') -Target $linkTarget | Out-Null
    $linkManifest = Write-FixtureManifest 'reparse-link-scope' @($linkSource) @((Join-Path $fixtureRoot 'dest\NeoBabylon-Probe-LinkTest'))
    $linkEvidence = Join-Path $fixtureRoot 'evidence\reparse-link'
    $linkResult = Invoke-Backup $linkManifest $linkEvidence
    Assert-That 'capture fails closed when a reparse link identity cannot be preserved' ($linkResult.ExitCode -ne 0) ($linkResult.Output -join "`n")
    $failureBaseline = Get-Content (Join-Path $linkEvidence 'backup-baseline.json') -Raw | ConvertFrom-Json
    $linkRow = $failureBaseline.scopes[0].entries | Where-Object type -eq 'link' | Select-Object -First 1
    Assert-That 'reparse node is recorded as a link without traversal' ($null -ne $linkRow -and $linkRow.followed -eq $false -and $linkRow.linkType)
    Assert-That 'reparse target canary is not copied into the backup' (@(Get-ChildItem (Join-Path $linkEvidence 'artifacts') -Recurse -File -ErrorAction SilentlyContinue | Where-Object Name -eq 'canary.txt').Count -eq 0)

    $hardLinkSource = Join-Path $fixtureRoot 'scope\NeoBabylon-Probe-HardLinkTest'
    New-Item -ItemType Directory -Path $hardLinkSource -Force | Out-Null
    $original = Join-Path $hardLinkSource 'original.bin'
    $alias = Join-Path $hardLinkSource 'alias.bin'
    [IO.File]::WriteAllBytes($original,[byte[]](1,2,3,4))
    if (-not [WorkspaceConsolidationBackupFixture.HardLinkApi]::CreateHardLink($alias,$original,[IntPtr]::Zero)) {
        throw "Could not create fixture hard link: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
    }
    $hardManifest = Write-FixtureManifest 'hard-link-scope' @($hardLinkSource) @((Join-Path $fixtureRoot 'dest\NeoBabylon-Probe-HardLinkTest'))
    $hardEvidence = Join-Path $fixtureRoot 'evidence\hard-link'
    $hardResult = Invoke-Backup $hardManifest $hardEvidence
    Assert-That 'capture fails closed when hard-link topology cannot be reconstructed' ($hardResult.ExitCode -ne 0) ($hardResult.Output -join "`n")
    Assert-That 'hard-link failure cannot produce a ready marker' (-not (Test-Path (Join-Path $hardEvidence 'backup-ready.json')))
    $hardBaseline = Get-Content (Join-Path $hardEvidence 'backup-baseline.json') -Raw | ConvertFrom-Json
    $hardRows = @($hardBaseline.scopes[0].entries | Where-Object type -eq 'file')
    Assert-That 'baseline records both hard-link names with shared file identity and count' ($hardRows.Count -eq 2 -and @($hardRows | Where-Object linkCount -eq 2).Count -eq 2 -and $hardRows[0].volumeSerial -eq $hardRows[1].volumeSerial -and $hardRows[0].fileIndex -eq $hardRows[1].fileIndex)

    $redirectTarget = Join-Path ([IO.Path]::GetTempPath()) ('NeoBabylon-BackupRedirect-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $redirectTarget | Out-Null
    $redirectLink = Join-Path $fixtureRoot 'evidence-redirect'
    New-Item -ItemType Junction -Path $redirectLink -Target $redirectTarget | Out-Null
    $redirectEvidence = Join-Path $redirectLink 'redirected-evidence'
    $redirectResult = Invoke-Backup $manifest $redirectEvidence
    Assert-That 'fixture capture rejects a reparse component in evidence path before writing' ($redirectResult.ExitCode -ne 0) ($redirectResult.Output -join "`n")
    Assert-That 'reparse evidence-path rejection creates nothing at redirected target' (-not (Test-Path -LiteralPath $redirectEvidence))
    Assert-That 'live verifier rejects ready markers and reads completeness from hashed baseline JSON' ($backupScriptText -match 'Live-ready backup marker verification is not supported by Task5A' -and $backupScriptText -match '\$baseline\.coverage\.complete -ne \$false')
    Assert-That 'live capture validates QA parent and evidence path reparse ancestry before directory creation' ($backupScriptText -match 'Assert-NoReparseComponents \$script:ApprovedQaRoot \$true\s+Assert-NoReparseComponents \$evidenceFull \$true')
    Remove-Item -LiteralPath $redirectLink -Force
    Remove-Item -LiteralPath $redirectTarget -Force -Recurse
    Write-Output "PASS fixture-only consolidation backup suite ($passed assertions)"
} finally {
    $resolved = [IO.Path]::GetFullPath($fixtureRoot)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
    if ([IO.Path]::GetDirectoryName($resolved) -ine $temp -or [IO.Path]::GetFileName($resolved) -notmatch '^NeoBabylon-WorkspaceConsolidationBackup-[0-9a-f]{32}$') {
        throw "Refusing to remove unexpected fixture path '$resolved'."
    }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
    if (Test-Path -LiteralPath $sentinelPath) { Remove-Item -LiteralPath $sentinelPath -Force }
}
