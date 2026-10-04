# NeoBabylon workspace consolidation design

> **Superseded on 2026-09-26:** The 56-root Lab/ProbeSources/full-inventory
> cutover plan below was replaced by the simpler cleanup recorded in
> [`docs/history/2026-09-26-WORKSPACE_CLEANUP.md`](../history/2026-09-26-WORKSPACE_CLEANUP.md).
> Retained below as historical proposal; do not use it as the current layout or
> cutover procedure.

Status: **Layout approved by Martin for implementation; no migration or runtime qualification performed.**
Date: 2026-09-26.

## Accepted direction and scope

Martin chose to keep the product repository and source root at
`D:\CODING\NeoBabylon` and place the 56 identified NeoBabylon sibling directories
under its to-be-ignored `.local` workspace. The separate Codex-derived runtime must
remain a separate Git repository with its upstream ancestry and dirty worktrees.
Martin approved a **narrow** exception to the current application-root guard
for `.local\App` and Lab QA roots. This proposal restricts the latter to
`.local\Lab\Runs\<validated-run-id>\App`. Other source children and the
ordinary user Codex home remain rejected; there is no general `.local` or
`.local\Lab` allowance.

This document records the chosen paths and cutover checks below. It does not move
anything, change production code, certify the runtime, or relocate
`C:\Users\Martin\AppData\Local\NeoBabylon` or other unrelated user state.

## Approved exact layout

| Current path | Proposed path |
| --- | --- |
| `D:\CODING\NeoBabylon` | Stay at the same path, including its `.git` and uncommitted source. |
| `D:\CODING\NeoBabylon-Runtime` | `D:\CODING\NeoBabylon\.local\Runtime\NeoBabylon-Runtime` (move whole checkout, including `.git`). |
| `D:\CODING\NBRT-RouteControl` | `D:\CODING\NeoBabylon\.local\Runtime\NBRT-RouteControl` (linked worktree). |
| `D:\CODING\NeoBabylon-Data` | `D:\CODING\NeoBabylon\.local\Lab\NeoBabylon-Data` (intact; includes historical QA, builds, isolated Codex homes, and three linked worktrees). |
| Each of the 53 probe source directories identified below | `D:\CODING\NeoBabylon\.local\Lab\ProbeSources\<unchanged directory name>`. Preserve their paired Data directories in the intact Data tree. |
| No current sibling | `D:\CODING\NeoBabylon\.local\App\Data` for a future ordinary application root; do not populate it from `%LOCALAPPDATA%`. |
| No current sibling | `D:\CODING\NeoBabylon\.local\Lab\Runs\<validated-run-id>\App\Data` for disposable QA application state. `App` is the QA application root. |
| Existing `.local\QA\Source-Snapshot-20260926-155823-757f70c7` | Keep in place. Use `.local\QA` for migration snapshots and evidence, never as an application root. |

The frozen probe set is 38 `NeoBabylon-Qualification-Probe-<ID>`, 12
`NeoBabylon-429-Test-<ID>`, and these three exact names:
`NeoBabylon-OpenRouter-LiveMetadata-9e3c218bc5775813dcc860a606ab88cb`,
`NeoBabylon-OpenRouter-Preflight-297523ed055afe4e82a4f6287fc39b17`, and
`NeoBabylon-OpenRouter-RoutePreflight-c0961add19c368c16fccae98d89ecf15`.
The IDs are fixed, not a glob for future directories:

- `429-Test`: `0dbdf8a3bb90417bb53d418937fd41a1`, `574a8df0d4ed456db14535fbcddee57d`, `664867fa1d39478d9d03a3f2882a5235`, `777f9dcde006484baaba0b71a689ba5a`, `8837716c7b5842cd8f4ebb97e4e942c8`, `89cabcceb0d9448ba972f066e6f12cde`, `a0ad5fc3cc5249bbab9f4088d8588abd`, `a1211c24534a48bbac762b952b7c7594`, `a8a17cdd409c4c53a9a908eccefedd4a`, `ad91c830f4594c55b21ed02d45084dc1`, `cef4cfa09a6d452ba2144dda3f269e5c`, `dcee6a4f59834f85b77166542c67250b`.
- `Qualification-Probe`: `0b33e60d328646638f85412a0b93bbb1`, `0b596d723c594b889251cc544dd14bd1`, `16cd945fdbd54c90b57de7bd5091ced2`, `190bb1f3c24f4466be764292a87c25c6`, `1cc5e5b733544c55a6518748f14bf24c`, `2d9f3bb28f2642fab2f2633c0622b1b3`, `3908dad53084409cbcd80a31fe8dd86c`, `45182dd57a704775bca28bab18d821b7`, `4f31743b6f2a41daa69c7458fad61e90`, `50a589888f434298909a90e83197e8a3`, `527b5c48831a459cb3aca6bed04735e9`, `55bdb9e81ae04209ad423b345daf1422`, `640dae678249468798dc0f298e7915d7`, `728432b3d2034c4eaed2ee4637e757e5`, `742a44f8c37546c58c4142760e039b23`, `7534b6c5a20c44b881c05355b7fd1ba7`, `85e615b67ed84c5dab763ac34bbcedb3`, `872274ba2a214f858e4e172454d26a51`, `8903a86effae44a1b1bfc9e42ba13de5`, `944b8e39da2e4e7ea221751a963d28a2`, `9e6561fd7fbf4e5b92f054f7839b1d48`, `a3001e008a1e443d87c03e3254d4fae1`, `a8e3f5f4f58148eb81ec65b8eaf56aba`, `b636933305c2459db7a940141e5ce43f`, `ba9c10c962924462a03dca7d22b22ba9`, `cbb387a69c054761ad480a6d74743f36`, `d3d61a65cc5e4565b2bea35f6d8168cd`, `d853b087cfcd4fa48d5d2050fe1fe982`, `d914c1ae49014570b89c17391dde8dc5`, `db2df6e1f19e4ae7b8d27498f6258717`, `db34e1bd15ba45c49d9afe4aff8c337a`, `dcbc28cfd53b458f87e42268e5636035`, `e40797bd80e447318e1705b99f7ec6ae`, `e5c78dcd591549d899ea10d5c78ea161`, `e5cff6a9ed7744ab9117c8b4eb1615f6`, `e94f846e0946402890c1a020374a2c40`, `f52bbcdee9954464ac3a14f7ce46a322`, `fcade15a631747cb9feb50cb3dfc7316`.

The `a3001e...` and `e40797...` probe source roots have no matching Data
directory; retain them as partial evidence. Do not sweep other `D:\CODING`
directories into this move set merely because their names look similar.

## Path and authority changes required before cutover

- Before sensitive Data enters a product descendant, add `/.local/` to the
  product ignore rules; verify it is ignored and the product Git index has no
  entries beneath it. Explicitly verify product source snapshots, search,
  build, and export inputs exclude `.local`; an ignored folder alone does not
  stop a recursive filesystem copier. Keep the existing `reference/cache`
  snapshot separate; never copy the product root recursively into its own
  `.local\QA` child.
- Keep `ApplicationRootLayout`'s outside-source behavior and ordinary Codex
  home rejection. Within the source root, permit only the exact `.local\App`
  ordinary application root or `.local\Lab\Runs\<validated-run-id>\App` for a
  trusted disposable QA run. Define and validate the run ID; do not accept an
  arbitrary Lab descendant. Archived `.local\Lab\NeoBabylon-Data` and
  `.local\Lab\ProbeSources` are **not** allowed application roots. Reject
  traversal, sibling-prefix tricks, reparse-point redirects, and other source
  children before creating `Data`, `CodexHome`, or `Workspace`. Test these
  boundaries, including case and ordinary-Codex-home cases.
- Prepare a new `runtime/runtime-lock.json` candidate with paths relative to
  the unchanged product root: `.local/Runtime/NeoBabylon-Runtime` and
  `.local/Lab/NeoBabylon-Data/Phase2-FunctionPatch-Target/debug/codex-app-server.exe`.
  Keep the old active lock and a recovery copy; do not expose the candidate
  while paths are mixed. Activate it only after all moves, byte checks, and
  Git repair succeed, before any relaunch. A path edit or hash match alone
  does **not** pass source-to-binary provenance or Phase 5.
- Update qualification defaults, current probe-lock generation logic and
  current fixtures, native QA path guards, host tests, launch configuration,
  and current path guidance to the exact Lab Runs pattern. A product-root
  relative lock copied unchanged into a deeper probe source root resolves to
  the wrong place; generate fixture-relative paths explicitly. Preserve
  historical probe locks and dated evidence verbatim with an old-to-new
  relocation map. Verify no test or launch path recreates an old sibling.

## Snapshot, move, and verification sequence

1. Complete and check the path-code, harness, and ignore-rule edits, while
   keeping the new lock candidate inactive. Freeze the exact 56 source and
   destination pairs. Preflight ownership, destination nonexistence, resolved
   paths, same D: volume, ACLs, deeper reparse points/hard links, prospective
   Windows path lengths, applicable Git `gitdir`/`includeIf` conditions, and
   Cargo configuration inherited from old versus new ancestors. Change only
   observed path-dependent settings. Before sensitive Data enters `.local`,
   prove `/.local/` is ignored, has no product-index entries, and is excluded
   by actual product source snapshot/search/build/export workflows. The prior
   audit checked reparse points only at sibling roots and immediate children.
2. Preserve the existing Task0 QA source snapshot. Its metadata reports
   filtered archives of 201 product and 7,631 runtime source files. It is
   provenance for that earlier source state, not cutover recovery evidence.
3. Quiesce **all path-owning writers and relaunchers**, including WPF/WebView2,
   App Server, Node, Cargo, tests, PowerShell jobs, SQLite writers, IDE/background
   watchers, and Git maintenance. Re-enumerate processes and confirm stable
   trees. The audit observed active Runtime/Data users; Windows `openfiles`
   could not certify locks because local object tracking was disabled. Abort
   before moving if writers or relaunchers cannot be stopped. Do not run Git
   prune/gc during cutover.
4. After both path edits and confirmed quiescence, capture the **final pre-move
   recovery baseline** under access-restricted `.local\QA`: complete verified
   product/runtime source-content hash snapshots using Task0 filters and
   archive readback; a separate opaque local backup of both repositories'
   Git administration and four linked-worktree pointers; and a restricted
   backup of non-reconstructible Data. Capture each SQLite DB with its extant
   WAL, SHM, and rollback journal as one consistent set, without displaying
   or sharing credential contents. Freeze Data/probe path, type, count, and
   length inventories plus hashes of durable/high-value records; full hashes
   of huge generated caches are optional. Verify backup readback and stable
   manifests before any rename. An inventory or backup made while writers
   were active is not recovery evidence. Retain the old active lock for abort.
5. Use a persistent per-pair intent/completion journal under `.local\QA` for
   all 56 same-volume whole-root renames. Require each destination not to
   exist; never merge, overwrite, or use copy-delete. Rename Data and the 53
   probe roots intact, then linked `NBRT-RouteControl`, then main Runtime.
   Flush each journal transition and verify source absence/destination
   presence before the next pair; reconcile an interrupted intent from disk.
   Stop on the first failure. Do not activate the new lock or relaunch while
   old and new paths coexist.
6. **Before Git repair**, compare moved source bytes and all required Data/
   probe hashes against the pre-move baseline; compare every tree's path/type/
   count/length and link inventory. Hash the complete locked App Server binary
   before and after its move and compare both with the active lock. Only after
   byte checks pass, run `git worktree repair` from the moved main runtime
   checkout with all four explicit linked paths: `NBRT-RouteControl`, Data's
   `NeoBabylon-Runtime-RouteControl-20260923`, Data's
   `qa\P2-13-classification-20260926-01\baseline\runtime`, and Data's
   `qa\P5-01-clean-baseline-20260925\codex-rs`. Three links live inside the
   moved Data tree. After repair, permit byte changes only in identified Git
   linkage files; use read-only Git checks that avoid incidental index writes.
   For all five worktrees verify common-dir, HEAD, index/staged entries,
   unstaged diff, and untracked/ignored path manifests and relevant bytes,
   not just counts. Preserve the main's 57 tracked changes and untracked
   `bin/`, NBRT's 55 changes, and P5-01's one change as rechecked at cutover.
   Do not prune/gc until repair and state reconciliation are complete.
7. Only after all paths and Git links verify, activate the prepared new lock
   and take final verified source-content hash snapshots with Task0 filters;
   reconcile the intentional lock/path-code edits separately from unintended
   changes. Keep historical docs and probe locks verbatim. Build the runtime
   for QA into a separate target, without replacing the locked executable.
   For every WPF smoke launch and restart, set process-scoped
   `NEOBABYLON_APPLICATION_ROOT` to a fresh disposable
   `.local\Lab\Runs\<validated-run-id>\App` and verify it before state init;
   never use an unset variable that would touch `%LOCALAPPDATA%`. Verify the
   identity, path, and full hash of the executable the host actually launches
   against the active lock. Run focused mock/native checks and restart/resume;
   verify no old `D:\CODING` sibling was recreated. Treat `.local\App\Data`
   only as a future ordinary root and leave existing `%LOCALAPPDATA%` state
   untouched. These checks do not certify runtime provenance, containment,
   provider behavior, or release readiness.

If any precondition fails, stop before the first rename. After a partial
failure, stop writers, preserve the journal and backups, and either inverse
only verified completed renames in reverse order to empty original paths or
pause for manual recovery. After inverse moves, reverse Git repair from the
restored main checkout using the old linked paths, restore the old lock, and
verify old-path identity **before any restart**. If any inverse step cannot be
verified, leave both host and tests stopped. Never discard dirty work, delete
an occupied path, silently rebuild Data, or create an outside compatibility
junction. Completion requires all 56 journal entries reconciled, Git links and
Data/source integrity checked, and the explicit-root launch/restart recorded.
