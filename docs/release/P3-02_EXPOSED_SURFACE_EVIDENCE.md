# P3-02 exposed-surface execution and failure evidence

Status: **Open. This is a current evidence/gap inventory, not acceptance or a
promise that every listed interaction is qualified.**

Date: 2026-09-26  
Runtime identity: App Server `0.155.1`, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, locked Windows binary SHA-256
`9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d` (see
[`runtime-lock.json`](../../runtime/runtime-lock.json)).

## Scope and authority

This matrix covers the operations and event surfaces currently bridged to the
React UI. An App Server event renderer is not automatically a separate
callable NeoBabylon capability. Upstream features absent from the named
bridge remain outside the exposed-surface set; see the
[versioned upstream inventory](UPSTREAM_CAPABILITY_INVENTORY.md).

Production tool policy requests `danger-full-access` with approval `never`.
The host verifies the App Server's effective projection at thread start, but
model-directed commands run as the NeoBabylon Windows account; workspace
selection is not a containment boundary. Approval qualification tests use a
separate QA-only read-only policy and do not change the production setting.

## Evidence matrix

| Surface | Permission and identity | Timeout and cancellation | Denial, partial output, and current evidence |
| --- | --- | --- | --- |
| Model-directed `commandExecution` / shell | Production policy above; Windows account authority. App Server event correlation plus thread/turn/item identity is projected. A separate `stopCommand` action carries only item ID from React; the host binds exact thread/turn/item/process and App Server instance epoch. | UI deadline is 650 seconds; host turn observation is 10 minutes. `interruptTurn` ends generation only; command Stop is a separate explicit operation. The WebView host dispatches with `CancellationToken.None`; there is no qualified per-command timeout. | Explicit failed/error outcomes render as failed; only explicit `completed`/`succeeded` without an error render as succeeded; absent/future statuses render as informational. Deterministic host and native WPF/WebView2 fixtures cover one pinned-App-Server `exec_command` failure and the separate command Stop path. Stop verifies survival after turn interruption, visible keyboard-invokable exact stop and attribution, confirmed process exit, and absence of a scheduled marker; it uses one loopback Responses request without fallback. This is not live-provider qualification. No production denial matrix, per-command timeout, or broad cancellation matrix is qualified. Bounded output is visible; it is not redacted. See [P2-08](VERIFICATION.md#2026-09-24--phase-2-p2-08-native-app-server-approval-qualification) and [P2-11](VERIFICATION.md#2026-09-24--phase-2-p2-11-bounded-native-large-output-cancellation-and-failure). |
| Patch / `fileChange` | Same production authority. Approval identity binds request, App Server instance, thread, turn, item, and the file-change preview fingerprint. | Same turn deadline/interrupt route; patch-specific timeout or cancellation is unqualified. | One live NEX function-form patch round trip and deterministic native accept/deny/fingerprint invalidation tests are recorded. Apply-time reread means atomic preview-to-write is not established. See [P2-09](VERIFICATION.md#2026-09-24--phase-2-p2-09-file-change-approval-preview-and-binding). |
| `functionCallOutput`, MCP, and dynamic-tool notifications | Rendered from App Server items/events; not independently callable bridge operations. Dynamic-tool registration is off in the stable selection; MCP configured state is Unknown. | No separate capability-level timeout/cancellation contract. | Output/event rendering is not proof of provider support or a callable tool. No selected-tuple MCP test or dynamic-tool registration test is recorded. |
| Approval response | Explicit human decision bound to exact pending request/instance/thread/turn; file-change acceptance additionally requires the current preview identity. Production tool policy is `never`; exercised approval flows are QA-only. | Five-minute default approval expiry is fail-closed. The renderer's 30-second request deadline does not cancel host work. | Native deterministic command accept/deny/timeout and file-change accept/deny tests exist. The all-request-family matrix is open. |
| Thread/history operations (`listThreads`, new/start/resume/fork) | Host uses the selected project and exact capability binding; resume checks saved workspace/model identity. | Renderer deadline is 30 seconds; host dispatch supplies `CancellationToken.None`. No rendered cancellation operation covers these requests. | Identity mismatch/recovery/read-only behavior has deterministic evidence; no complete operation-specific timeout/cancel/partial-result matrix. |
| Project selection | User folder picker or a saved project path; Windows account access. No model-directed arbitrary-CWD bridge operation. | Renderer deadline is 30 seconds; host dispatch is not cancelled when it expires. User cancellation of the native picker is represented as `cancelled`. | Project selection and named-operation validation are tested; timeout/partial-result coverage is open. |
| Saved output (`readOutputRange`) | Read-only App Server history operation restricted to the exact active thread, turn, and item. | Renderer deadline is 30 seconds; host dispatch supplies `CancellationToken.None`. Page cap is 32,768 characters with bounded history lookup. | Deterministic paging, bounds, wrong-identity, unavailable-output, and omission-state evidence exists. It is not arbitrary document ingestion and cannot recover bytes omitted before App Server persistence. |
| Diagnostics, capability selection, appearance | Local host/config operations. Capability selection requires a canonical provider/model record; diagnostics expose credential presence, not credential values. | Renderer deadline is 30 seconds; host dispatch supplies `CancellationToken.None`. | Exact-record selection/fail-closed behavior has tests. `HostOperationError` now preserves host error `type` and `attributedTo` and includes attribution in its message; a deterministic WebView-bridge unit test verifies the typed fields and formatted message. |

## Bounded command-failure execution — 2026-09-25

The isolated .NET host-test executable used the exact runtime-lock-verified
App Server `0.155.1` (SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`) and a
loopback Responses fixture. The ordinary `exec_command` ran a test-owned
`.cmd` file that emitted `NEOBABYLON_P3_02_PARTIAL_FAILURE_MARKER` and exited
23. Because the Windows default outer shell is PowerShell, the command
explicitly propagated `$LASTEXITCODE`; the terminal `commandExecution` item
retained the marker and exact exit code 23. `TurnDiagnostics` exposed a typed
`toolExecution` failure attributed to Codex App Server with the matching
thread/turn/item IDs. The fixture saw exactly two requests, including the
failed call's output; no retry or provider/model fallback occurred. The host
test executable exited 0 with 82 passing checks. Logs are under
`D:\CODING\NeoBabylon-Data\QA\P3-02-command-failure-host-build-20260925\`.
This was deterministic loopback test-host evidence, not live inference or a
rendered WPF test. Runtime binary identity was verified from the product
lock, but its depfile/source association remains unproven as noted in
`runtime/README.md`.

## Bounded native command-failure UI rendering — 2026-09-25

A separate real WPF/WebView2 run used a freshly built Release host and the
runtime-lock-verified Codex App Server `0.155.1` (source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, binary SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`). A
deterministic loopback Responses fixture selected the synthetic identifier
`p3-02-native-command-failure-fixture`; no live model or provider credential
was used. The permitted command ran a test-owned `.cmd` inside the isolated
application `Data\Workspace`, emitted
`NEOBABYLON_P3_02_NATIVE_FAILURE_MARKER`, and exited with code 23.

The App Server `item/completed` event and the host's final `toolDiagnostics`
projection contained the same item ID and exact exit code. The native failure
card showed `FAILED`, `Exit code 23`, the retained marker, and `Codex App
Server` attribution; its accessible label also included the exit code. Exactly
two fixture requests occurred (tool call and one continuation), with no retry
or fallback. The test-owned command also emitted the clearly synthetic value
`sk-nb-synthetic-canary-not-a-credential`; the loopback continuation received
it as ordinary tool output and the native activity card displayed it. No real
provider credential or unrelated user data was used. This demonstrates the
accepted unrestricted output path; it is not evidence of host credential
injection. The result records separate application and `Data` roots and
`ordinaryCodexRootUsed: false`; browser/page issues were empty.

Result and screenshot:
`D:\CODING\NeoBabylon-Data\QA\P3-03-tool-capabilities-20260925\P3-02-native-command-failure-1790346625113-13224\result.json` and
`command-failure-visible.png` in the same directory. The UI suite passed
88/88, the production UI build succeeded (existing Astryx/Lucide
`use client` and bundle-size advisories remain), and the isolated Release WPF
build had 0 warnings and 0 errors. This closes only the rendered ordinary
command-failure slice; it is neither live-provider evidence nor closure of
P3-02's broader acceptance matrix.

## Interactive-command Stop characterization (historical; superseded below) — 2026-09-25

A fresh run of the full isolated `ApprovalQA` host harness against the same
locked App Server captured the current interactive-command behavior. The
turn became `interrupted`, but the command process was still alive one second
after Stop. The late marker and partial output were absent from final turn
diagnostics; exactly one loopback Responses request occurred, with no
continuation or provider/model fallback. The isolated application root and
`Data` were outside the source repository, and the ordinary Codex data root
was not used. Machine result:
`D:\CODING\NeoBabylon-Data\QA\P3-02-command-interruption-6ffd15e3e03f4094a929d42ef46a251a\result.json`.

The fixture's `passed: true` means its characterization completed as expected;
it did **not** mean the command-stop product behavior passed at that time.
The characterized behavior motivated the separate command-stop implementation
and native evidence recorded below. This is deterministic loopback evidence,
not a live-provider test.

## Unresolved acceptance issues

- UI bridge deadlines are not host-operation cancellation. A timed-out
  renderer request can finish later, and current operations generally pass
  `CancellationToken.None`.
- The denial, timeout/cancellation, and partial-output matrix remains
  incomplete across exposed operations. The ordinary command-failure path now
  has both isolated host-level and native WPF/WebView2 loopback evidence, but
  not live-model evidence or broad per-operation qualification.
- Turn interruption leaves an interactive command alive and final turn
  diagnostics do not retain its partial output. The accepted Stop-turn
  behavior remains unchanged; the distinct command-stop action is now
  implemented and verified at bounded deterministic/native scope below.
- **Output handling is settled by [NB-DEC-011](../decisions/0011-unrestricted-tool-output-visibility.md):**
  normal App Server tool output is shown and forwarded without content
  redaction. A model-directed command under the accepted full same-user
  authority may read user-accessible data and return it this way. NeoBabylon
  must not directly serialize host-managed credentials/configuration into the
  renderer or add generic arbitrary RPC; it does not promise secrecy for data
  an unrestricted tool can independently access. The synthetic canary fixture
  used no real credential or user data.

## Remaining P3-02 evidence matrix (open; bounded command Stop verified below)

The UI correction and one pinned-App-Server partial-failure slice are
implemented and verified in both host-level and native WPF/WebView2 fixtures:
host error attribution survives the bridge, unknown/missing activity statuses
are not presented as success, and a failed ordinary command retains exact
partial output and exit code through the provider follow-up and visible
failure card. A test-only synthetic credential-shaped canary was also
forwarded as normal command output and rendered; it is not an actual key.
This matches the accepted output behavior without changing runtime policy.
The deterministic browser fixture confirms the unknown state is visible as
`info`. Continue with operation-specific denial and timeout/cancellation
qualification and partial-output coverage for other exposed paths. The
output-handling requirement is settled by NB-DEC-011; P3-02 remains open until
the remaining per-operation evidence matrix is complete.

No production policy, runtime, or provider configuration was changed for this
correction.

## Bounded identity-bound command Stop — 2026-09-25

The accepted contract is preserved: Stop turn ends model generation; a
surviving interactive command stays visible and has a separate explicit Stop
command action. NeoBabylon adds only named stable App Server list/stop routes
to the pinned `0.155.1` runtime; it does not set the global experimental API
opt-in, expose arbitrary RPC, use process-name killing or `thread/shellCommand`,
or send process IDs from React. The host binding contains exact
thread/turn/item/process identity plus the current App Server client epoch.
It rejects cross-thread duplicate item IDs and changed selection, retries an
ambiguous request only against the same live App Server instance, and
invalidates old bindings on replacement. Runtime termination checks item and
process identity under the process-store lock and does not touch a replacement
entry if the captured process slot changes after confirmed termination.

Fresh verification used App Server `0.155.1`, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d`, with
tracked source patch fingerprint
`ddbcd19e0c9424147174ad36bc4ae15144e0e4fcce6d0db9c7f01e7a36db053d`.
Four focused Rust termination tests passed, including exact identity, natural
exit, process-table replacement during termination, and initial-exec-owned
entry cleanup. The full ApprovalQA host harness passed **86 checks, 0
failures**, including collision rejection, same-instance retry, confirmed
stop, duplicate stop, and App Server replacement invalidation. A delayed-list
response arriving after `item/completed` is rejected, preventing a completed
command from being resurrected as running. The UI suite passed **92/92**;
typecheck and production build passed.

The native WPF/WebView2 result is
`D:\CODING\NeoBabylon-Data\QA\P3-02-native-command-stop-1790372549455-69872\result.json`.
It used synthetic model ID `p3-02-native-command-stop-fixture` and a
deterministic loopback Responses fixture, not live inference. The exact
test-owned command remained alive after turn interruption; the accessible
Stop command button was focused and activated with Enter; the UI showed
`stopped` with Codex App Server attribution and no remaining stop action; and
the exact PID/start-time was gone. The scheduled 5-second marker was absent
after a 5,250 ms observation. Exactly one fixture request occurred, with no
fallback, page issues, or fixture errors. The application root and its `Data`
directory were outside the source repository, and the ordinary Codex data
root was not used. Screenshots are retained alongside the result.

Not closed by this slice: typed `failed` Stop was UI-tested as retryable but
not returned by a host-level App Server fixture; broader
denial/timeout/cancellation and operation-wide partial-output coverage remain
open; and no live provider inference was performed. The delayed
list/completion race is now covered in the host harness. The first final-pass
native launch used the ordinary Release executable and correctly rejected the
isolated fixture capability path; the dedicated `ApprovalQA` executable then
passed. The old characterization paragraph above is historical, not the
current implementation state. These limits do not invalidate the successful
exact native round trip, but P3-02 and Phase 3 remain open.

## Typed `failed` Stop retry evidence — 2026-09-26

The host/UI retry boundary is now covered. In the `ApprovalQA` build only, a
one-shot test response can return App Server `failed`; before doing so, the
test seam verifies the outgoing stable stop request matches the exact
host-owned thread, item, and process identity. The host reports
`commandStopAvailable=true` only if the same selected thread, exact binding,
and App Server epoch remain current. The UI now treats a typed `failed` result
as retryable only when that explicit bit is true; a missing/false value fails
closed. The fixture observed the command still alive after `failed`, then
retried through the real pinned App Server and confirmed `stopped`. The seam
is excluded from Release builds and does not intercept production App Server
behavior. The UI also removes retry language from the visible explanation
when the current binding is not confirmed.

The complete ApprovalQA host harness exited 0. Its machine result is
`D:\CODING\NeoBabylon-Data\QA\P3-02-command-interruption-fca1366a31b64e0a857d0a9ec46ab2f5\result.json`.
The UI suite passed **93/93**, TypeScript typecheck and Vite production build
passed, and Release/ApprovalQA WPF hosts built with zero warnings/errors. The
native WPF/WebView2 Stop fixture was freshly rerun with an ApprovalQA host and
passed: one deterministic loopback Responses request, command alive after
turn interruption, visible keyboard-invokable Stop, exact process exit, no
late marker, and no page or fixture errors. Its result/screenshots are under
`D:\CODING\NeoBabylon-Data\QA\P3-02-typed-failed-retry-native-approvalqa-20260926-01\P3-02-native-command-stop-1790373810728-74608\`.
Neither fixture used live inference; both application roots and `Data`
directories are isolated, and the ordinary Codex root was not used. The
native test covers successful Stop, not a UI-driven synthetic failed Stop;
that cross-layer retry case is covered separately by the UI unit and host
integration tests.

This closes only the typed-failure retry evidence gap for the bounded Stop
action. Production denial, operation-specific timeout/cancellation, and the
broad per-operation partial-output matrix remain open. P3-02 and Phase 3 are
still open; this is not overall phase acceptance.

## Native late command-completion delivery — 2026-09-26

A fresh native WPF/WebView2 fixture interrupted a model turn while its ordinary
command continued running, then observed the command's natural exit. The host
forwarded the exact late App Server `item/completed` notification under the
original turn request ID. React accepted it only for the matching thread, turn,
item, type, method, and source; the visible activity changed to `succeeded` and
the Stop action disappeared. The same four-scenario run rechecked successful
Stop, stale/duplicate Stop rejection, and conservative `unknown` status after
the exact App Server transport was stopped.

The run used App Server `0.155.1`, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, binary SHA-256
`9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d`, and
source-patch fingerprint
`ddbcd19e0c9424147174ad36bc4ae15144e0e4fcce6d0db9c7f01e7a36db053d`. It made
four deterministic loopback Responses requests and no live inference or
fallback. Its isolated application root and `Data` directory were outside the
source repository; ordinary Codex-root use was false. The QA result and
screenshots are under
`D:\CODING\NeoBabylon-Data\QA\P3-02-command-completion-native-green3-20260926-01\P3-02-native-command-stop-1790422749307-6972\`.

An earlier retry ended with App Server JSONL EOF before the second fixture
request. The complete rerun passed, but the cause of that isolated failure is
unknown. This closes only the late natural-completion delivery path; production
denial, operation-specific timeout/cancellation, and the broad per-operation
partial-output matrix remain open. P3-02 and Phase 3 are not accepted.
