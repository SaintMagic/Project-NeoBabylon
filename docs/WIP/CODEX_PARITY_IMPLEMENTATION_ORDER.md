# Codex parity — implementation order

Product base: `f126612a00231a58626d72108aac6ee912431ee3`. Review date: 2026-10-04.

[Review](CODEX_PRODUCT_PARITY_REVIEW.md) · [Patch catalog](../../patches/codex-parity/README.md) · [Specifications](codex-parity/SPECIFICATIONS.md) · [Actual verification](codex-parity/VERIFICATION.md)

## 1. Apply the bounded correction batch

The patch files are unapplied product changes. Merely checking out or merging this documentation branch does not implement them. Do not apply to an unexamined dirty tree or assume the user's local checkout matches the public base.

| Patch | Capability | Prerequisites | Independence |
|---|---|---|---|
| 001 | Preserve assistant item identity | None | Can apply directly to base |
| 002 | Default goal ownership and boundaries | None | Can apply directly to base |
| 003 | Structured tool output and metadata | 002 | Dependency is the shared source-only C# test harness, not prompt behavior |
| 004 | Readable/Wide conversation layout | None | Can apply directly to base |
| 005 | Copy visible message/preview | 004 | Uses its UI integration and browser fixture |
| 006 | Canonical temporary roots in QA | None | Test-only; production path checks untouched |
| 007 | Current accessibility fixture contracts | None | Test-only; apply before the complete browser suite |
| 008 | Lossless assistant whitespace chunks | None | Can apply directly to base |

Recommended first batch is all eight in numeric order. The immediate correctness priority is 001/003/008; 002 is a useful default-policy improvement and 004/005 provide small visible gains. 006/007 restore trustworthy Windows QA. No large subsystem replacement is required.

The verifier checks each patch with only its declared prerequisites, then the cumulative series, including index application, whitespace checks and exact reverse restoration. The nested `patches/codex-parity/.gitattributes` is part of the package: unified patches must stay LF even though product files can check out as CRLF on Windows.

### Isolated application (PowerShell, from this review checkout)

```powershell
$ErrorActionPreference = 'Stop'
$base = 'f126612a00231a58626d72108aac6ee912431ee3'
$package = (Resolve-Path 'patches/codex-parity').Path
python docs/WIP/codex-parity/verify-patches.py --output .local/Lab/parity-patch-checks.json
if ($LASTEXITCODE -ne 0) { throw 'Patch verification failed' }
# This creates a separate branch/worktree; use an unused destination.
git worktree add -b implementation/codex-parity ../NeoBabylon-parity-apply $base
if ($LASTEXITCODE -ne 0) { throw 'Could not create isolated worktree' }
Push-Location ../NeoBabylon-parity-apply
try {
    foreach ($patch in Get-ChildItem $package -Filter '*.patch' | Sort-Object Name) {
        git apply --check --index $patch.FullName
        if ($LASTEXITCODE -ne 0) { throw "Check failed: $($patch.Name)" }
        git apply --index $patch.FullName
        if ($LASTEXITCODE -ne 0) { throw "Apply failed: $($patch.Name)" }
        git commit -m "Apply $($patch.BaseName)"
        if ($LASTEXITCODE -ne 0) { throw "Commit failed: $($patch.Name)" }
    }
} finally { Pop-Location }
```

If the exact base object is absent, fetch that explicit commit from origin before verification. If the real development branch has moved, apply/check in a separate worktree based on that branch and reconcile changed hunks manually; neither `--ignore-whitespace` nor silently dropping tests is acceptable qualification. Keep the reviewed base/result recorded.

### Batch verification (inside the implementation worktree)

```powershell
npm ci --prefix ui/diagnostic
npm test --prefix ui/diagnostic
npm run build --prefix ui/diagnostic
dotnet run --project tests/product-parity/NeoBabylon.ProductParity.Tests.csproj -c Release
dotnet build host/NeoBabylon.Host/NeoBabylon.Host.csproj -c Release
dotnet build tests/host/NeoBabylon.Phase1A.Tests.csproj -c ApprovalQA
dotnet run --no-build --project tests/host/NeoBabylon.Phase1A.Tests.csproj -c ApprovalQA -- --probe-rename-saved-thread
dotnet run --no-build --project tests/host/NeoBabylon.Phase1A.Tests.csproj -c ApprovalQA -- --probe-development-credential-records
node --test tests/qa/nvidia-adapter-bundle.test.mjs tests/qa/qa-run-root.test.mjs
# Install test tooling in a disposable sibling folder, not the product dependency graph.
npm install --prefix ../parity-review-tools --ignore-scripts --no-audit --no-fund playwright-core@1.56.0
New-Item -ItemType Directory -Force .local/Lab/Runs | Out-Null
node tests/qa/ui-accessibility.mjs ../parity-review-tools/node_modules/playwright-core/index.mjs --product-parity
```

Run each command with exit-code checking in automation; the branch workflow contains that handling. Browser fixture expects Windows Edge. Synthetic browser success is not a native WebView2/runtime qualification. Native acceptance: stream spaces/newlines/code, interrupt before completion, reopen, inspect structured tool results and preview omissions, toggle widths, copy/fail clipboard and exercise keyboard controls. Use the exact private runtime and an isolated fixture workspace, not personal files.

Rollback applied commits in reverse dependency order using normal `git revert`, or reverse unapplied/staged patch changes with `git apply --reverse --check --index` followed by `--reverse --index`. Do not force-reset unrelated work. These patches introduce no durable history migration. Width preference may remain as harmless UI state; source rollback does not erase user data. Restore the previous default catalog policy by regenerating through the ordinary host path, not editing historical runtime evidence.

## 2. Settings and durable continuation foundations

Implement [S001](codex-parity/S001-settings-and-instructions.md) as store/revision/recovery → effective configuration → actual runtime propagation → real Settings UI. Exit gate: a saved instruction appears exactly once in the captured model-visible request, survives restart and reports its applied revision. UI-only changes must not restart a turn. Do not build a broad Settings dialog before that vertical path works.

In parallel implement [S006](codex-parity/S006-nvidia-replay.md): immutable native replay and alias index → bootstrap identity → commit-before-completion → restart/fork/recovery → exact-runtime/provider qualification. Exit gate: a tool/reasoning continuation survives an adapter restart without reconstructing hidden material or making an extra side-effecting request. This does not depend on the Settings dialog.

## 3. Complete the conversation content pipeline

Implement [S002](codex-parity/S002-attachments.md) persistence/admission receipts before picker/paste UI. Then qualify one exact image-capable provider tuple, followed by restart/fork/GC and additional adapters. No attachment restoration claim before stored bytes and receipt reconciliation work.

Implement [S003](codex-parity/S003-reasoning.md) capability evidence and recorded event fixtures before rendering panels. Keep readable summaries/provider text separate from metadata and opaque replay. Exit gates cover supported, unsupported and metadata-only providers, not only one happy-path OpenAI-shaped response.

Start [S004](codex-parity/S004-conversation-timeline.md) item ordering and safe Markdown/code independently after 001/003/008. Add attachment and reasoning item renderers when S002/S003 provide their contracts. Older-turn paging and archive/steer-related commands require exact pinned protocol verification. Preserve the App Server journal as authority.

## 4. Provider administration and long operations

[S005](codex-parity/S005-provider-profiles.md) extends S001 with host-owned profiles, immutable capability snapshots, credential status and exact endpoint/model/route administration. Retain legacy snapshot identities. Do not enable a generic provider because it merely advertises an OpenAI-compatible URL.

[S007](codex-parity/S007-operation-lifecycle.md) begins with tri-state outcomes and operation snapshots, then coherent budgets/recovery, next-draft/queue, and qualified steering/output deltas. Add tests proving uncertain accepted work is not replayed. No broad automatic retry until side-effect reconciliation is demonstrated.

## 5. Goal-level autonomy, then measured orchestration

[S008](codex-parity/S008-autonomous-workflows.md) scope and postcondition tests can progress alongside S001. A complete synthetic installation/configuration/import task should require a goal, not a user-authored list of tool calls. Host scope checks remain distinct from technical containment of unrestricted shell.

[S009](codex-parity/S009-tool-orchestration.md) first produces actual tool/request byte and outcome measurements. Only then choose a bounded discovery optimization. Persistent generated-tool activation remains governed by its existing separate decision and qualification work.

## Integration checkpoints

For each slice retain source revision, configuration/capability identity, deterministic request evidence, build/test commands, restart behavior and known gaps. Sync current WIP with the completed slice without rewriting historical qualification artifacts. No new product decision blocks this order. The omitted runtime/credentials and a future containment promise are the genuine external gates, not routine UI choices.
