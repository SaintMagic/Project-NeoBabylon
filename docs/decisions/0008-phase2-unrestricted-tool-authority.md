# NB-DEC-008 — Explicit unrestricted tool authority for Phase 2

Status: **Accepted sequencing and authority choice; focused implementation in progress.**  
Date: 2026-09-23.

## Accepted decision

Martin explicitly directed: “Defer containment and proceed with unrestricted
tools.” This supersedes the earlier *sequencing gate* that required Windows
containment before any further Phase 2 work. It does **not** reverse the
finding in [NB-DEC-007](0007-windows-legacy-containment-gate.md), qualify a
Windows sandbox, or mark the full Phase 1B acceptance gate passed. Its
remaining checks stay deferred until needed or requested.

For the current desktop client, model-controlled tools may run with the
logged-in Windows user's full filesystem and network authority. That includes
the ability to affect files outside the selected workspace. The product must
show this plainly rather than describe the session as contained or
workspace-write. No new provider/model, remote repository, shared Codex
configuration, commit, or push is authorized by this choice.

## Focused implementation choice

The WPF host selects Codex's existing `danger-full-access` sandbox mode and
`never` approval policy in its own isolated App Server configuration and
explicit `thread/start`, `thread/resume`, `thread/fork`, and `turn/start`
requests. It requires App Server's reported effective `dangerFullAccess` /
`never` before binding a thread. An absent or different response is an error,
not an automatic switch to a Windows sandbox or another provider. The pinned
runtime's NeoBabylon guard still rejects the unqualified legacy Windows
sandbox entrypoints; no Windows backend is enabled as a contained path.

`CODEX_HOME` remains under the distinct NeoBabylon application root's `Data`
directory and is checked against App Server's `initialize` response. This is
**state/configuration isolation, not a security boundary** against full-access
same-user tools. The UI must say “Full access · no containment” and warn that
model-controlled commands can read, modify, or delete files outside the
workspace and use the network.

## Evidence and limits

The 2026-09-23 isolated deterministic Responses fixture exercised the pinned
App Server through a real `cmd.exe /d /c ver` tool round trip without live
provider inference. Its full-access thread start, post-turn resume, and fork
echoed `dangerFullAccess` / `never`; the fixture observed the command output
returned to the mock provider. The WPF/WebView2 shell rendered the warning and
opened its runtime diagnostics from the policy chip. Exact commands and paths
are in [VERIFICATION.md](../release/VERIFICATION.md).

A separate credentialed live NEX/OpenRouter qualification then exercised one
function-form `apply_patch` turn under this unrestricted policy, on the exact
route-pinned model and currently locked App Server binary. The isolated test
file changed, a matching function output was returned, and App Server exposed
a saved file-change review. This qualifies only that model/route/tool path;
it is not a general live-provider or native-WPF acceptance result. See
[VERIFICATION.md](../release/VERIFICATION.md) and the canonical model
capability record for the evidence.

Live LM Studio under this policy, general filesystem mutations, crash
recovery, direct host-managed credential handling, and the full Phase 2 gate
remain unqualified. Normal tool output visibility/forwarding is settled by
[NB-DEC-011](0011-unrestricted-tool-output-visibility.md); this does not
provide secrecy for data a full-access tool can independently read.
Unrestricted same-user commands may reach personal files, NeoBabylon state,
and potentially credentials by means beyond the child-shell environment.
Sanitizing credential variables and separating application data reduce
accidental coupling; neither proves secrets inaccessible to the model. Those
risks are accepted consequences of this interim authority, not silently
closed verification boxes.
