# NB-DEC-007 — NeoBabylon Windows legacy-command containment gate

Status: **Fail-closed mitigation implemented; contained tool execution remains open.**  
Date: 2026-09-23.

## Accepted requirement

The selected workspace-write policy must not permit a model-controlled command
to delete or mutate files outside its writable roots, including protected
`.git` content. A tool failure must be visible and attributed. A permissive
mode that merely makes a diagnostic command succeed does not satisfy this
requirement.

## Observed failure

The pinned Codex `0.155.1` unelevated legacy Windows sandbox denied an outside
write but allowed `cmd.exe del` to remove a sibling canary. The isolated App
Server mock path reproduced this four times before this decision, and the
direct runtime regression reproduced outside and `.git` deletion while
workspace/TEMP/TMP deletion succeeded. The legacy token uses
`WRITE_RESTRICTED`; removing that flag in a local experiment blocked the
outside deletion but also broke full-disk read and permitted writable-root
deletions. A token-flag change alone is therefore not a qualified repair.

## Phase 1B implementation choice

Until a contained backend is qualified, every NeoBabylon App Server launch
sets the process-scoped `NEOBABYLON_REQUIRE_CONTAINED_TOOLS` marker. The pinned
runtime rejects both legacy Windows sandbox command entrypoints before token,
ACL, or child-process setup when that marker is present. There is no automatic
fallback to an uncontained backend. The marker does not change standalone
upstream Codex behavior when NeoBabylon is not the caller.

This is a **tool-execution stop**, not a claim that the legacy backend has been
fixed. Text-only and metadata operations can continue, but ordinary command
tools cannot pass through the marked legacy path. The application remains
unable to claim Phase 1B contained-tool acceptance.

## Deferred qualification

- Select and qualify a Windows backend that preserves outside reads and
  workspace/TEMP/TMP writes/deletes while denying outside and `.git` mutations.
- Cover delete-child rights, rename/move, reparse points, hard links, and
  process descendants before enabling model-directed command tools.
- Verify an actual App Server failure reaches the WPF/React activity surface
  as an attributed typed tool failure, not only as a mock or journal string.
- Revalidate restart/relaunch marker propagation and exact runtime lock after
  any backend change.

No shared Codex service, ordinary Codex home, or system account is selected by
this decision. A dedicated elevated/service backend or AppContainer approach
requires separate design and evidence before it becomes an accepted path.

## 2026-09-23 native-backend hard-link probe

An isolated `codex-mxc-sandbox` native/PSEC test established that the Windows
build can instantiate the native process security environment. With an explicit
read-root/workspace-write policy, a real `cmd.exe` child read an outside canary,
deleted a workspace canary, and received OS denial for direct outside and
protected `.git` deletion. The next test added a *trusted pre-created hard
link inside the workspace* to the outside canary. The same child wrote through
that alias: the outside canary changed from `outside` to `MUTATED\r\n`.
The focused ignored regression in the runtime source reproduces this failure
when run explicitly with `--ignored`. It is not an App Server/tool success.

The exploratory NeoBabylon-marker routing code was removed; the existing
legacy fail-closed guard and pinned executable remain the active behavior.
Native MXC/PSEC is **not qualified** for model-directed tools. An elevated
separate-account backend is a candidate, not an accepted fix: this machine has
enabled `CodexSandboxOffline` and `CodexSandboxOnline` local users, but the
current process is medium integrity and the pinned isolated-home setup path
invokes `ShellExecuteExW` with `runas`, then refreshes ACLs. No such setup was
run for this investigation. Its hard-link, junction, delete/rename, descendant,
and exact App Server behavior must be demonstrated before this gate clears.

Martin's 2026-09-23 priority is to clear this gate before further Phase 2
implementation. Other unfinished Phase 1B qualifications are deferred until
needed or requested, without being marked passed.

## 2026-09-23 elevated-backend qualification result

Martin authorized isolated elevated setup and the account/ACL changes it
requires. The pinned runtime's separate-account elevated backend was tested
under fresh homes within `D:\CODING\NeoBabylon-Data`, using a real
`cmd.exe` child and outside canaries. Both pre-existing and child-created
workspace hard links and junctions let that child change an outside canary.
The first child-created-alias probes used malformed absolute targets and gave
false passes; after correcting them to valid relative targets and verifying
the junction destination, both failed containment. The focused
manual tests are retained as ignored-by-default qualification probes in
`codex-rs/windows-sandbox-rs/src/unified_exec/tests.rs`.

Do not enable this backend. A startup-only scan cannot close a child-created
alias escape. The observed ACL propagation to a pre-existing hard-linked
outside file explains one path; the dynamic alias failures also implicate
path-based authorization, but the exact kernel/ACL sequence remains to be
traced. A copy under the same accessible host filesystem is **not** an
isolation boundary by itself. A genuinely isolated guest or another mechanism
must prevent aliases from resolving back to the original host files. Such a
design would change the workspace/edit authority model and needs review.
The fail-closed NeoBabylon guard remains active for the unqualified Windows
sandbox paths. Martin subsequently changed the Phase 2 sequencing and chose
explicit unrestricted tools; see [NB-DEC-008](0008-phase2-unrestricted-tool-authority.md).
This does not clear the containment gate or make these backends safe.
