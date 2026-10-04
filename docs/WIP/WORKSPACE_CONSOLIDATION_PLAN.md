# Workspace consolidation implementation plan

> **Superseded on 2026-09-26:** This 56-root Lab/ProbeSources/full-inventory
> cutover plan was replaced by the simpler cleanup recorded in
> [`docs/history/2026-09-26-WORKSPACE_CLEANUP.md`](../history/2026-09-26-WORKSPACE_CLEANUP.md).
> Retained below as historical proposal; do not use it as the current layout or
> cutover procedure.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Put the 56 identified NeoBabylon-owned sibling directories beneath the ignored product-local workspace without losing dirty Git state, runtime identity, or QA data.

**Architecture:** Prepare and test all path-producing code while the old paths remain active. After writers stop, capture a recovery baseline, rename each exact same-volume root with a durable journal, repair the four linked worktrees, activate the new runtime lock, and verify the binary the WPF host actually launches. A failure leaves the host stopped until recovery is verified.

**Tech Stack:** Windows PowerShell, .NET 8/C#, Node.js QA scripts, Git worktrees, WPF/WebView2, SQLite data files.

**Spec:** `docs/WIP/WORKSPACE_CONSOLIDATION_DESIGN.md`. Read that document completely; it owns the 56 exact names, target layout, invariants, and recovery policy. `docs/WIP/FINISHING_THE_PRODUCT.md` owns the wider phase gates. This plan does not certify the runtime build or packaging.

## Global Constraints

- Product source stays at `D:\CODING\NeoBabylon`; runtime remains its own upstream-ancestry Git repository, moved to `.local\Runtime\NeoBabylon-Runtime`.
- `.local\App` is the only ordinary in-repository application root; QA roots are only `.local\Lab\Runs\<validated-run-id>\App`. Archived Data and ProbeSources are never app roots.
- Preserve dirty/staged/untracked state and source bytes, historical evidence, SQLite state, and `%LOCALAPPDATA%\NeoBabylon`. No copy-delete fallback, merge, overwrite, destructive cleanup, commit, push, global configuration, or remote repository.
- `/.local/` must be ignored and excluded from source/archive/build/export inputs before credential-bearing Data enters it. Test against the actual workflows; Git ignore alone is insufficient.
- No live move until every old-path writer and relauncher, including Singer's current App Server test, has stopped and the final recovery snapshot is verified. Stop on first failed precondition.
- All QA launches set a process-scoped `NEOBABYLON_APPLICATION_ROOT` to a new Lab `Runs` root. Do not launch with the variable unset or migrate the existing LocalAppData root.
- Verify the exact locked binary hash before/after the move and the executable actually launched. A separately rebuilt binary is not proof of the locked binary's provenance.

## Review Focus

- A reparse point inside `.local` redirecting to another tree must be rejected **before** `Data` is created (Task 1 test).
- A deeper QA probe source must resolve a fixture lock relative to itself; blindly copying the product lock must fail the fixture test (Task 2 test).
- A QA runner given an old sibling parent must fail without creating it (Task 3 test).
- An interrupted move with an intent but no completion entry must reconcile on-disk source/destination, never retry blindly or merge (Task 4 test).
- After moving both main and linked worktrees, staged, unstaged, untracked, and ignored state must match the pre-move manifest; a HEAD-only check is insufficient (Task 5 verification).

---

### Task 1: Narrow application-root boundary and local ignore

**Files:** Modify `.gitignore`, `host/NeoBabylon.Core/ApplicationRootLayout.cs`, `tests/host/Program.cs`. Add only a focused helper file if the existing C# class becomes unwieldy.

**Interfaces:** `ApplicationRootLayout.Create(string sourceRepositoryRoot, string applicationRoot)` remains the public entrypoint. It must validate the app root and existing path components before any directory creation. Run ID grammar: 1–80 ASCII letters, digits, and hyphens, beginning and ending with an alphanumeric; compare Windows path components case-insensitively. Existing outside-repository behavior remains, except reject the ordinary Codex home itself or any path that would make `CodexHome` equal it.

- [ ] Add failing host checks for exact `.local\App` and `.local\Lab\Runs\qa-01\App` success; repo root, `Data`, `.local\Lab`, historical Data, ProbeSources, runtime, sibling-prefix, traversal-to-source, invalid run IDs, and ordinary Codex home fail without creating state. Include case variation.
- [ ] Add a Windows reparse/junction fixture under `.local` targeting an outside canary; `Create` must reject it before any `Data` child appears. If the fixture cannot be created, record the machine limitation and do not call this boundary qualified.
- [ ] Run `dotnet run --project tests/host/NeoBabylon.Phase1A.Tests.csproj --configuration ApprovalQA --no-restore`; confirm the new checks fail for the old guard and no unrelated failures are introduced by the fixture.
- [ ] Implement the minimal narrow exception, path-component and reparse validation; add `/.local/` to `.gitignore`. Do not permit arbitrary `.local` descendants.
- [ ] Rerun the host suite; `git check-ignore -v .local/QA/Source-Snapshot-20260926-155823-757f70c7/snapshot-metadata.json` must identify `/.local/`, and `git ls-files .local` must be empty. Audit the actual source snapshot, search, build, and export commands for recursive inclusion; fix only observed omissions in the owning scripts.

### Task 2: Qualification runner and PowerShell probe paths

**Files:** Modify `tools/Phase1AQualification/Program.cs`, `tests/qualification/MockProbeQualification.Tests.ps1`, `tests/qualification/MockProviderError.Tests.ps1`, and `tests/README.md` path guidance. Do not edit dated probe evidence or their historical locks.

**Interfaces:** For each new qualification, create its source under `.local\Lab\ProbeSources\NeoBabylon-<kind>-<id>` and app root under `.local\Lab\Runs\<validated-id>\App`. A generated probe lock resolves its `sourceCheckoutRelativePath` and `appServerBinaryRelativePath` from the probe source root to the exact approved nested checkout and locked binary; it does not copy the product lock unchanged. `NEOBABYLON_APPLICATION_ROOT` is set process-locally for the runner.

- [ ] Add failing fixture assertions that both scripts choose only the new paths, and that a copied unchanged product lock resolves incorrectly from a probe root.
- [ ] Run both PowerShell fixture tests in their mock/offline modes; record the expected path/lock failures, without invoking live inference or containment research.
- [ ] Update path generation and fixture-relative lock writing, preserving restrictive ACLs, bounded cleanup, and environment restoration. Default `Phase1AQualification` output path must follow the same Lab Runs pattern.
- [ ] Rerun the same mock tests and host suite. Assert no new `D:\CODING\NeoBabylon-Qualification-Probe-*` or `NeoBabylon-429-Test-*` sibling appears.

### Task 3: Native/browser QA path contract

**Files:** Create `tests/qa/qa-run-root.mjs` and `tests/qa/qa-run-root.test.mjs`; modify `tests/qa/ui-accessibility.mjs`, `tests/qa/p2-11-live-nex-output.mjs`, and each `tests/qa/native-*.mjs` file that currently uses sibling `NeoBabylon-Data\QA` (list with `rg -l 'NeoBabylon-Data.*QA|authorizedQaRoot' tests/qa` before editing). Update `tests/README.md` once with Task 2's agent coordinating that shared file.

**Interfaces:** `assertQaRunsParent(sourceRoot, candidate)` requires the exact `<sourceRoot>\.local\Lab\Runs` directory and rejects an old sibling or descendant/ancestor; `newQaRun(qaRunsParent, runId)` creates only `<parent>\<validated-run-id>` and returns `{ runRoot, applicationRoot: <runRoot>\App }`. Callers continue writing evidence beside `App`, not into source or archived Data. Keep existing unique-run semantics and each runner's locked-binary check.

- [ ] Add failing `node --test tests/qa/qa-run-root.test.mjs` cases for the exact parent, old sibling, prefix sibling, traversal, reused run ID, and a pre-existing application root. Assert rejects create nothing outside the approved parent.
- [ ] Implement the helper and replace duplicated sibling guards/path generation in the affected QA scripts. Every host spawn/restart must receive the returned `applicationRoot`; an unset variable is a test failure.
- [ ] Run the helper test, `node --check` on all edited `.mjs`, the deterministic browser accessibility flow, and one mock native WPF flow using a fresh QA host build and a new `Runs` parent. Record any UI/host test whose credentials or installed prerequisites make it unavailable; do not silently skip it.

### Task 4: Rehearsed recovery and journaled move mechanism

**Files:** Create `scripts/WorkspaceConsolidation.ps1`, `tests/qualification/WorkspaceConsolidation.Tests.ps1`, and an exact pair manifest under `scripts/workspace-consolidation-pairs.json`. The manifest contains only the 56 source/destination pairs from the spec. Do not invoke the live `-Move` operation in this task.

**Interfaces:** `WorkspaceConsolidation.ps1 -Mode Preflight|Move|Recover -ManifestPath <exact-file> -EvidenceRoot <.local\QA\unique-id>`; `Preflight` is read-only except its own evidence directory, emits a machine-readable report, and refuses existing destinations, cross-volume targets, foreign/reparsed or hard-linked surprises, unsafe length/ACL or insufficient recovery capacity. It inspects path-sensitive Git `gitdir`/`includeIf` and Cargo ancestor configuration and flags only observed changes. `Move` accepts only a verified fresh preflight/recovery baseline and records each pair's intent, then completed state with durable flush. `Recover` inverses only verified completed pairs into empty original paths, then stops for manual action on ambiguity. Both modes fail closed if the manifest or tree changed.

- [ ] Test the exact manifest count/names and destination mapping; test a temporary same-volume fixture with a dirty main Git repo plus a linked worktree and staged/unstaged/untracked files. Simulate collision, reparse target, changed source, and interrupted journal intent. Assert no overwrite or copy-delete and no writes outside the fixture.
- [ ] Run `pwsh -NoProfile -File tests/qualification/WorkspaceConsolidation.Tests.ps1`; confirm the tests fail before implementation and pass afterward. No live NeoBabylon path is moved by this command.
- [ ] Implement minimal preflight/journal/recover behavior using literal resolved paths and same-volume whole-root rename. Journal writes must be durable (`FileStream.Flush(true)` or equivalent) before/after each rename. Do not use globs or source-prefix discovery to choose targets.
- [ ] Rehearse the full fixture move and inverse, including `git worktree repair`, before live use. Verify fixture bytes, Git index/staged, unstaged, untracked, and ignored manifests; collect the report without secrets.

### Task 5: Quiescent recovery baseline and live cutover

**Files:** Live filesystem roots listed in the spec, `runtime/runtime-lock.json`, and an access-restricted `.local\QA\Workspace-Consolidation-<id>` evidence directory. Keep the old lock in that restricted directory. No historical probe file is rewritten.

**Interfaces:** The cutover operator consumes Task 4's exact manifest and verified preflight; the output is 56 completed journal entries, repaired links in all five worktrees, an activated lock with relative paths `.local/Runtime/NeoBabylon-Runtime` and `.local/Lab/NeoBabylon-Data/Phase2-FunctionPatch-Target/debug/codex-app-server.exe`, and readback evidence. This task is serialized, not parallelized.

- [ ] Confirm Singer's old-path App Server test is finished. Identify and quiesce only exact path-owning WPF/WebView2/App Server/Node/Cargo/PowerShell/SQLite/Git processes and relaunchers; re-enumerate and verify stable trees. If not possible, stop before any move.
- [ ] Prepare a candidate lock with the new relative source/binary paths and unchanged version, revision, source-patch digest, and full binary hash. Keep it inactive and byte-preserve the old active lock for rollback; test candidate path resolution against the planned destination without launching it.
- [ ] Run the exact manifest preflight and test actual source snapshot/search/build/export exclusions. Capture **post-edit, post-quiescence** verified source-content snapshots using Task 0 filters, Git-admin and four link-pointer backups, and a restricted backup of non-reconstructible Data. SQLite DB plus extant WAL/SHM/journal must be captured consistently. Record path/type/count/length/link inventories and durable/high-value hashes, read back archives/backups, check free space, and freeze the old lock and full locked-binary SHA. This is a recovery baseline, unlike the earlier Task 0 archive.
- [ ] Run the journaled `-Move` once. On any failure, keep host/tests stopped; reconcile the journal, use only verified inverse renames or manual recovery, and restore old lock/linkage before relaunch.
- [ ] **Before Git repair**, compare moved bytes and required Data/probe inventories/hashes with the recovery baseline and compare the locked binary to its old hash and active lock. Then run `git worktree repair` from moved main with all four exact linked paths in the spec; verify all five worktrees' common-dir, HEAD, staged/index, unstaged, untracked, and ignored manifests/bytes. Permit only recorded Git linkage-file changes after repair.
- [ ] Activate the candidate lock only after all 56 moves and links verify. `RuntimeIdentity.LoadVerified` must resolve the nested source and exact hash. Do not claim source-to-binary provenance from this move.

### Task 6: Reopen and post-cutover evidence

**Files:** `.local\QA\Workspace-Consolidation-<id>` machine reports; update current path guidance in `runtime/README.md`, `tests/README.md`, and the canonical status/release documents only for observed results. Preserve dated records verbatim.

**Interfaces:** A new native QA run lives at `.local\Lab\Runs\<validated-id>\App` and launches the host with `NEOBABYLON_APPLICATION_ROOT` explicitly set. The host's supervisor reports the actual spawned App Server executable path; its full SHA equals the active lock. Evidence distinguishes source archives, locked binary, separately built QA binary, host launch, and full runtime certification.

- [ ] Take a final verified product/runtime source-content snapshot and compare with the final pre-move baseline; explain only intentional path/lock/code changes. Recheck that `/.local/` is ignored and unindexed.
- [ ] Build a new QA host into a unique Lab Runs artifact directory, run focused host/mock tests, and run one native WPF/WebView2 open/restart with fresh explicit app root. Record the actual launched App Server path/hash and `Data\CodexHome`; confirm neither `%USERPROFILE%\.codex` nor `%LOCALAPPDATA%\NeoBabylon` was used.
- [ ] Confirm no 56 old source siblings reappeared and no test created another NeoBabylon sibling under `D:\CODING`; compare old/new path inventories and all 56 journal entries. If an unrelated folder remains, leave it untouched.
- [ ] Update current documentation with exact verified results and remaining runtime/Phase 2 gates. Review all file changes against the Task 0 source manifest and post-cutover snapshot. No Git commit or push.

## Execution assignment

Use Luna agents for Tasks 1–4 with disjoint file ownership. Task 2 and Task 3 share `tests/README.md`: Task 2 owns that file and Task 3 sends its required path edits to Task 2, or the coordinator makes one documentation-only reconciliation after both finish. Review each task's tests and diff before proceeding. Task 5 is a single Luna executor plus coordinator read-only review/checkpoints; Task 6 is independently reviewed. Do not create a product Git worktree: the selected product repo has no resolvable `HEAD`, and moving the dirty existing runtime/worktrees is the task itself. Work in the selected checkout and preserve all unrelated changes.
