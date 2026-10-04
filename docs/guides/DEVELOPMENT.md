# Development guide

Status: **Current workspace guidance, reconciled on 2026-09-23.** This is a
project-specific guide and does not replace Martin's global instructions.

## Current workspace

The selected product workspace is `D:\CODING\NeoBabylon`; the separate
upstream-preserving runtime checkout is `D:\CODING\NeoBabylon-Runtime`. These
are local sibling repositories; no remote NeoBabylon repository is required.
The product repository's `main` branch has no commits, so its current project
files appear untracked. Preserve that work; do not commit or push without
separate authorization. Phase 0 is complete and Phase 1B qualification remains
open. The first Codex-first UI shell slice is also implemented under Martin's
explicit broad continuation authorization; see `docs/product/STATUS.md` and
`docs/WIP/INITIAL_BUILD_PLAN.md`.

At the Phase 0 check, observed tools included Git 2.55.0.windows.2,
Node.js 24.18.0, npm 11.16.0, Rust/Cargo 1.97.0, .NET 10.0.400, and Codex
CLI 0.155.0-alpha.9.2. These observations do not prove the selected
WPF/WebView2 package/runtime versions or provider availability; recheck
version-sensitive prerequisites before using them.

## Reference maintenance commands

From the project root:

```powershell
pwsh -File scripts/acquire-reference.ps1 -DownloadAppServer -DownloadCodexCli
pwsh -File scripts/verify-reference.ps1
```

The acquisition script downloads public files into the ignored cache and
regenerates its local hash index. The verifier is offline and checks the
manifest, hashes, baseline metadata, protocol output counts, preserved
startup brief, and matching binary versions.

To refresh the derived protocol-file index after generation:

```powershell
pwsh -File scripts/index-protocol.ps1
```

Protocol outputs were generated with the matching CLI and an isolated,
project-local `CODEX_HOME` process environment. Do not point a fixture command
at shared user Codex data unless an explicit test requires it and records the
boundary.

## Current implementation sequence

Continue the remaining Phase 1B qualification in the reviewed build plan. The
current open gates include effective context-budget/compaction evidence,
provider repeatability, authority, containment, recovery, and credential
checks. Do not restart Phase 1A or mark Phase 1B complete based on the first
shell slice or the narrow live provider runs.

The first Phase 2 shell slice now expands the diagnostic host into a
conversation-first desktop client: selected capability records, streamed
assistant/tool events, recent App Server history, exact-identity resume, and
preserving “New task” behavior. This partial implementation is explicitly
authorized but does not declare the Phase 1B or Phase 2 gates passed. Use
[`UI_UX_REFERENCE.md`](../architecture/UI_UX_REFERENCE.md) as the Codex-first
interaction baseline. Keep the host thin and App Server authoritative for
session state. Workspace selection, approvals, diffs/review, reconnection,
accessibility, and long-output behavior remain open. Toolbox expansion and
self-scaffolding remain later planned phases; temporary use and manual
promotion stay distinct.

Do not duplicate existing projects, generated contracts, or runtime state to
make the layout appear implemented. Use the existing phase plan and evidence
before proposing new files or dependencies.

## Evidence discipline

Record exact release/commit, endpoint/model, command, platform, timestamps,
hashes, and result for any behavior claim. Distinguish source inspection from
binary behavior, deterministic fixtures from live-provider tests, and a
successful build from a successful end-to-end run.
