# Phase 2 vertical-slice execution map

Status: **execution map; P2-01 through P2-09, the deterministic/native portion
of P2-11 (including a fresh isolated Release-host rerun on 2026-09-25), and the
visual/interaction portion of P2-12 have verified narrow slices. Two direct
bounded live NEX Responses outputs completed, and a separate native WPF +
pinned-App-Server NEX response rendered successfully on 2026-09-25. The native
turn completed 18,403 visible characters with no tool activity; its selected
model and configured route are recorded, but provider-side route attestation
and a hard output-token/cost cap are unknown. P2-10 Windows screen-reader and
full native nine-flow qualification, arbitrary-size/output-cap limits for
P2-11, and P2-13 remain open. The latest eight-state rendered-text/placeholder
audit measured 714
visible text/placeholder observations, with zero active-text failures, zero
below-AA disabled/`aria-disabled` exceptions, and zero unknown backgrounds.
The immediately preceding run exposed 16 low-contrast occurrences in 14
disabled-control findings; the CSS was corrected and the runner now fails on
such exceptions. A fresh native P2-10 subset passed keyboard model selection,
diagnostics dialog navigation/focus restoration, loaded-history search and
pagination, exact task reopen, and project switching. This does not run the
nine browser flows in WPF/WebView2 or qualify Windows screen readers. An
additional native composer check passed Enter-to-send, focused Stop plus
Enter, interrupted status after renderer reload without replay, and a single
deliberate Enter-to-continue request using a deterministic P2-06 loopback
fixture. It closes only that keyboard subset; Narrator and broader native
flows remain open. A 2026-09-26 native WebView2 accessibility-tree smoke also
verified the expected primary-shell controls, Runtime diagnostics dialog and
Done button, and modal inertness (background Search is absent while the dialog
is open). Narrator and the full nine-flow native suite remain open.
None of this closes the full Phase 2 gate.**
Authority: [the accepted Phase 2 build plan](INITIAL_BUILD_PLAN.md),
[current status](../product/STATUS.md), [roadmap](../product/ROADMAP.md), and
[Codex-first UI reference](../architecture/UI_UX_REFERENCE.md). This map divides
the remaining Phase 2 work; it does not add product requirements or declare
the deferred Phase 1B gate passed.

This is a coordination map, not thirteen simultaneous code-level plans. The
implementer should turn only the selected slice into a short test-first edit
plan after reading its actual interfaces, then finish and review that slice
before loading the next. This avoids inventing signatures or tests from a
stale snapshot of a moving checkout.

## Goal and boundaries

Complete the first functional WPF/WebView2 + React/TypeScript/Astryx desktop
client in small, independently reviewable end-to-end paths. Keep App Server
authoritative for turns and history, the WPF host thin and limited to named
desktop/process/native/bridge operations, and the renderer free of generic
arbitrary RPC. Preserve the exact runtime/capability binding and isolated
`<application root>\Data`; the application root is not the source root.

Phase 2 uses the accepted, visibly labelled `danger-full-access` / `never`
path. Windows containment is deferred and **must not** be represented as
passing. The default unrestricted path is not silently changed to provoke
approval requests. In an approval flow, file-change consent is offered only
for an exact, frozen, reviewable App Server change; missing or invalidated
previews remain deny-only. Upstream re-reads target files when applying an
accepted patch, so concurrent workspace edits are a documented limitation,
not a transactional guarantee. Do not substitute a provider/model, loosen a
fail-closed condition, overwrite unrelated work, install into shared
configuration, or commit/push without separate authorization. Phase 3 toolbox,
Phase 4 self-scaffolding, and Phase 5 packaging are not pulled into this plan.

Use the existing tests and isolated QA roots. A slice is one visible user
journey, its host/runtime path, and the focused regression that proves it.
Keep one slice per Luna turn where practical; if one grows, split at the next
independently testable user outcome. Do not carry all Phase 2 source and logs
into every turn. Read this map, the authority docs, and only that slice's
source/tests. End each slice with exact files changed, commands/results,
mock-versus-live distinctions, remaining gaps, and a status/verification
update. No slice may be marked complete from a build alone.

Across all slices, give special review attention to five user-visible failure
classes: a stale model record on resume (P2-02), Send acknowledged during a
renderer/host death (P2-03), interleaved cross-project history and turn start
(P2-05), a missing or changed approval diff (P2-09), and partial long output
followed by provider failure (P2-11). The owning slice must test each case.

## Baseline to preserve, not redo

The Codex-first dark-default shell, neutral charcoal/title-bar theme, tower
icon, dismissible unrestricted notice, project picker, named bridge, saved
task listing/resume, local search, page cursors, streaming, task drafts,
read-only patch review, and bounded approval cards already exist. Recorded
tests include a native WPF saved-patch reopen, a two-project mock flow,
mock interruption/restart, one route-pinned live NEX function-form patch,
55/55 UI tests, and a Release host build. Those observations are narrow;
see `STATUS.md` and `VERIFICATION.md` for their exact dates and conditions.
The broad App Server run is not green (1,523 pass, 26 fail in the recorded
run). The host harness previously passed 69/69, later hit a transient SQLite
cleanup lock, and a fresh 2026-09-24 `ApprovalQA` rerun subsequently exited 0
with all emitted checks passing. Preserve the earlier cleanup failure as an
intermittent, unexplained verification risk; see `VERIFICATION.md`.

## File and test map

| Area | Primary implementation/test anchors |
| --- | --- |
| Shell, projects, task lifecycle | `ui/diagnostic/src/App.tsx`, `ui/diagnostic/src/bridge.ts`, `host/NeoBabylon.Host/MainWindow.xaml.cs`, `host/NeoBabylon.Host/RuntimeSupervisor.cs`, `host/NeoBabylon.Core/WorkspaceProjectRegistry.cs` |
| History and exact identity | `ui/diagnostic/src/history.mjs`, `ui/diagnostic/src/active-task.mjs`, `host/NeoBabylon.Core/ThreadHistoryCursor.cs`, `ThreadListProjector.cs`, `ThreadResumeIdentity.cs`, `ThreadCapabilityBindingStore.cs` in the same directory |
| Drafts and recovery | `ui/diagnostic/src/drafts.mjs`, `restoration.mjs`, `host/NeoBabylon.Host/RuntimeSupervisor.cs`, `tests/host/Program.cs`, `tests/qa/draft-accepted-turn-recovery.mjs` |
| Review and approval | `ui/diagnostic/src/turn-review.mjs`, `approvals.mjs`, `host/NeoBabylon.Core/ThreadSavedChangeProjector.cs`, `AppServerClient.cs`, `tests/qa/native-patch-review.mjs` |
| Accessibility and scale | `ui/diagnostic/src/App.tsx`, `app.css`, `modal-focus.mjs`, `stream-batcher.mjs`, their UI tests, native WPF QA |

The host harness is `dotnet run --project tests/host/NeoBabylon.Phase1A.Tests.csproj --configuration Release --no-restore` from the product root, with focused `--probe-*` modes documented in `tests/README.md` and `docs/release/VERIFICATION.md`. The UI suite/build are `npm test`, `npm run typecheck`, and `npm run build` from `ui/diagnostic`. Use an isolated output path if running windows hold normal Release assemblies. Run focused checks per slice; run the complete affected suites at the final gate and whenever shared supervisor/protocol code changes materially. Qualify rendered behavior in native WPF/WebView2 when the slice claims native behavior; a synthetic Edge bridge is not equivalent.

## Slice queue

### P2-01 — Find and reopen an older task

**User outcome:** In the native WPF app, create enough saved tasks to exceed
the first App Server page, find an older task, open it, and see the correct
runtime-owned transcript and exact selected project/provider/model. No
duplicates, cross-page reordering, or turn replay.

**Work boundary:** `history.mjs`, `App.tsx`, `ThreadHistoryCursor.cs`,
`ThreadListProjector.cs`, and their focused tests. Exercise both upstream
cursor forms. Keep local preview/provider/model filtering honest: if a search
only covers loaded pages, visibly say so; full-history search needs a bounded
server-backed traversal or another explicitly scoped design, not a false
claim of indexing.

**Proof:** deterministic pinned-App-Server multi-page fixture; native WPF
find/reopen across page boundary; UI tests for overlap, cursor exhaustion,
project switch, empty/unavailable history, and search coverage. Record the
number of pages/tasks and that provider request count did not increase on
reopen. Preserve the currently documented bounded latest-20-turn/text-only
transcript projection unless the accepted Phase 2 authority requires more;
make older/omitted material explicit rather than presenting a partial view as
the full task history.

**Verified 2026-09-24:** the native QA harness seeded 56 saved tasks against
the pinned App Server and a loopback Responses fixture. WPF/WebView2 loaded
50 tasks, then the remaining 6 with no duplicates and newest-first ordering;
an unloaded-page search explicitly offered “Load older conversations to
search more.” The oldest task reopened with its saved user/assistant text and
exact `LM Studio · phase1a-qwen3-14b` attribution. Provider-fixture request
count stayed at 56 through native browsing and resume. Cursor exhaustion
removed the older-page action, and switching to a separate empty project
showed its empty state without leaking tasks or a stale cursor. No live
inference was used. The retained screenshot and detailed run record are in
[`VERIFICATION.md`](../release/VERIFICATION.md#2026-09-24--phase-2-p2-01-native-history-pagination-and-reopen).

This closes the P2-01 pagination/search/reopen user journey only. Search is
intentionally described as covering loaded pages, not the whole server-side
history.

**Verified 2026-09-25 — native transcript-overflow disclosure:** a second
WPF/WebView2 run used 56 isolated saved tasks against the exact pinned App
Server and deterministic loopback Responses fixture. One assistant fixture
contained 120,576 characters. The bounded live-notification projection showed
120,000 and recorded 576 omitted characters with `sourceRetained=true`; after
the Release host restarted, the saved-history projection showed 119,983
assistant characters within its combined 120,000-character transcript budget
(including the user prompt) and displayed the explicit “Full history remains
in Codex App Server” notice. Exact selected task identity was retained, all
56 tasks were loaded newest-first, provider requests stayed at 56 through
reopen, and no browser errors occurred. Result and screenshot are in
`D:\CODING\NeoBabylon-Data\QA\p2-01-history-overflow-20260925\P2-P01-native-1790324644555-38088\`.

This closes the bounded native P2-01 pagination/search/reopen and transcript-
overflow-disclosure paths only. Search still covers loaded pages, not the
entire server-side history; broader history scale and concurrency remain open.
No live inference was used.

### P2-02 — Switch model, then return to an exact saved task

**User outcome:** Switch explicitly between two capability records, create a
task under each, restart the host, and return to the original task without
changing its effective provider/model/endpoint/context or silently rebinding
it. A stale/missing/corrupt capability binding leaves history readable but
execution disabled, with a visible reason and a usable New task action.

**Work boundary:** `ThreadCapabilityBindingStore.cs`,
`ThreadResumeIdentity.cs`, supervisor binding, `active-task.mjs`, and the
capability UI. Existing same-process model-switch and stale-binding probes
are the starting evidence; do not duplicate them for this slice.

**Proof:** pinned-App-Server + native WPF restart with two test-only exact
records; focused mismatch/context-change cases and zero inference on
switch/resume; native renderer checks that the effective identity and
history-only state agree. Do not use an invented live model to fill a gap.

**Verified 2026-09-24:** the native WPF/WebView2 harness used two unique,
test-only LM Studio identifiers backed only by local loopback Responses
fixtures. It created one task under each, restarted the host with Model A
selected, then selected Model B and reopened its retained transcript after
changing only B's synthetic effective context from 16,384 to 8,192. The B
task became history-only with a visible capability-record mismatch; its
composer and Fork action were disabled, New task remained enabled, and its
binding bytes were unchanged. Returning to Model A reopened the same thread
and pointer with its original binding unchanged. Request counts stayed at one
per explicitly authored fixture task throughout switching, restart, and
resume. The application `Data` root was outside and distinct from the source
repository; the diagnostics panel reported that the ordinary Codex data root
was not used.

The fixture records keep real provider/model observations Unknown. Their
effective contexts and the minimum `tool_use` plus text-input catalog
prerequisites are explicitly synthetic test declarations; no real provider
metadata, model weights, function call, or live inference is qualified. This
native reproduction also exposed a real UI race: model selection launched
saved-history refresh without waiting, so Send could collide with the host's
active-operation guard. The UI now keeps model switching busy until that
refresh completes. Exact machine evidence and screenshots are in
[`VERIFICATION.md`](../release/VERIFICATION.md#2026-09-24--phase-2-p2-02-model-capability-switch-and-exact-task-rebind).

This closes the tested model-switch/restart/rebind journey only. It does not
close Phase 1B or the full Phase 2 acceptance gate; live provider context
accounting and broader recovery remain open.

### P2-03 — Submit, reload, and recover the exact pending send

**User outcome:** If WPF/WebView2 reloads or exits between Send and the
matching App Server `turn/started`, the composer and saved history reconcile
to the one authoritative outcome: accepted prompt appears once and its draft
clears; unaccepted prompt remains available for deliberate retry. Never
auto-replay an uncertain turn.

**Work boundary:** `drafts.mjs`, `restoration.mjs`, `active-task.mjs`, named
bridge lifecycle, and focused host tests. Preserve task/project-scoped plain
text drafts and the existing unknown-outcome warning.

**Proof:** native WPF/WebView2 + pinned mock Responses run at both sides of
the acknowledgement boundary, including a real renderer reload and host
restart; compare journal/request counts and draft state. Keep the existing
synthetic tests as regressions, not as the sole acceptance evidence.

**Result (2026-09-24):** passed as a narrow native slice. A loopback-only
Responses fixture verified that a send interrupted before `turn/started`
remains in the scoped draft after renderer reload, is not replayed, and is
sent once only after deliberate retry. A send acknowledged by `turn/started`
was journaled once, its draft cleared, and its pending response was not
replayed after host restart; the UI showed the interrupted/unknown-outcome
state. Native reproduction exposed a race between saved-history refresh and
the host's active-operation guard; the UI now remains busy until all thread
refreshes finish. See the dated machine evidence in
[`VERIFICATION.md`](../release/VERIFICATION.md#2026-09-24--phase-2-p2-03-pending-send-reconciliation).

This closes only the tested mock-provider acknowledgement/reload/restart
journey. It is not full crash, storage-failure, live-provider, or general
concurrency qualification and does not close Phase 1B or Phase 2.

### P2-04 — Recover a draft when local storage fails

**User outcome:** A blocked/failed WebView2 storage write or clear must not
silently erase an unsent prompt or falsely imply it was sent. The user sees
the correct warning and can inspect history before manually retrying.

**Work boundary:** `drafts.mjs`, `App.tsx`, storage failure injection in UI
and native QA. Do not create a second authoritative transcript or a new
credential store.

**Proof:** injected remove failure, write failure, and total storage failure
across a real WPF restart. Assert exact visible draft/warning/history state
and zero unsolicited provider requests. If a fully unavailable profile makes
durable recovery impossible, report that limit explicitly and keep the UI
safe; do not claim recovery from data that cannot exist.

**Result (2026-09-24):** passed as a bounded native fault-injection slice.
The current-source WPF/WebView2 host was restarted for each isolated scenario,
with a runtime-lock-verified App Server and deterministic loopback Responses
fixture. A rejected draft write kept the unsent text and warning visible in
the live window, but—as expected for a rejected persistence operation—the
text was unavailable after restart; saved history remained inspectable and
there was no send or replay. When both draft deletion and the empty-value
fallback failed after an accepted send, the prompt appeared once in saved
history and as the stale composer draft after restart, with a warning to check
history before resending; no replay occurred. With all Web Storage methods
unavailable, the warning and text were visible before restart, and after
restart the saved baseline history remained readable while the unsaved text
was necessarily unrecoverable. Because the pending-send marker was also
unreadable, the UI additionally showed its conservative unresolved-pending
warning. Across all three scenarios, only four
explicitly authored fixture prompts reached the loopback provider; neither
unsent prompt did. This verifies UI honesty and no-unsolicited-send behavior,
not durable recovery when storage rejects writes. The production UI/runtime
was not changed for this slice. See the dated evidence in
[`VERIFICATION.md`](../release/VERIFICATION.md#2026-09-24--phase-2-p2-04-local-storage-failure-behavior).

### P2-05 — Keep projects distinct during active and interrupted work

**User outcome:** With two or more local projects, each task remains bound to
its exact folder and capability. A history scan, attempted project switch,
and turn start that interleave cannot send a turn to the wrong project or
lose the selected task. After an interrupted turn, the user can deliberately
continue the right task; nothing is replayed.

**Work boundary:** project registry, supervisor operation ordering,
`ThreadHistoryCursor.cs`, `active-task.mjs`, and UI switch controls. Preserve
the current block on switching with an unsent draft or active operation unless
a separately reviewed interaction is required.

**Proof:** deterministic pinned-App-Server concurrency fixture for both
list-before-start and start-before-list orderings, multiple project/task
identities, plus native WPF recovery after child/host restart. Assert exact
cwd, one request per authored turn, and fail-closed cross-project resume.

**Verified 2026-09-24:** the focused host fixture exercised both operation
orders. While an Alpha turn was active, history access and a Beta switch were
rejected without changing the selected workspace or issuing another request;
when an Alpha history scan began first, a competing turn was rejected. Alpha
and Beta histories/tasks retained their exact workspace and capability
binding, and an Alpha resume under selected Beta failed closed. After a
supervisor restart, Beta's exact task resumed and a separately authored
continuation succeeded; switching back restored Alpha's original task.

The native WPF/WebView2 harness verified the rendered project busy state
during a delayed Beta history refresh, accepted a Beta prompt once, then
gracefully restarted the host while the deterministic loopback response was
held. Restart restored Beta's selected folder and exact task pointer, showed
the no-replay interruption warning, and produced no extra provider request.
A bridge-level attempt to resume Alpha while Beta was selected was rejected
on the workspace mismatch and did not replace Beta's pointer. Deliberate
Beta and Alpha continuations each produced exactly one request. Four authored
turns yielded four loopback requests; all matching user-message journal
entries occurred once, and no WebView errors were observed. The runtime-lock-
verified App Server was `0.155.1`, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`.
Provider/model were a synthetic LM Studio catalog identity and loopback
Responses fixture only; no model weights or live inference were involved.
No production source change was needed. The dated run, exact paths, commands,
and screenshots are recorded in [`VERIFICATION.md`](../release/VERIFICATION.md#2026-09-24--phase-2-p2-05-cross-project-operation-ordering-and-recovery).

This closes only the tested serialized operation orders and two-project
restart/continuation journey. Broader concurrent stress, crash recovery,
live-provider behavior, and Phase 1B remain open/deferred; full Phase 2 is
not passed.

### P2-06 — Stop or lose a live turn, then continue safely

**User outcome:** A selected real provider/model emits partial output, then
the user cancels or the connection/child fails. The UI distinguishes the
immediate unknown stream outcome from the persisted App Server status, retains
partial activity, and allows a separately authored continuation without
duplicating the original request.

**Work boundary:** supervisor stream/error projection, `TurnDiagnostics.cs`,
`restoration.mjs`, transcript UI. First make deterministic tests cover
cancel-after-delta and child exit; then run one bounded live-provider case
only for the exact selected route with credential in the test process.

**Proof:** request counts, saved turn status before/after reload, visible
attribution, no automatic replay, and child cleanup for the owned App Server.
If live provider access is unavailable, retain the deterministic pass and
mark live qualification open rather than switching provider/model.

**Verified 2026-09-24 — deterministic/native portion:** the host protocol
fixtures now emit a real partial `item/agentMessage/delta` before both an
interruption and an unexpected child exit. The interrupted observation remains
terminal `interrupted`, not success; the child-exit observation remains
nonterminal/unknown and carries a typed Codex App Server failure. A native
WPF/WebView2 run stopped a loopback Responses stream after one visible delta.
The partial bubble remained visible but stopped showing active-stream chrome;
the UI attributed interruption to Codex App Server. After renderer reload,
the saved interrupted status and no-replay warning were restored without a
provider request. One separately authored continuation produced exactly the
second request, and each user prompt appeared once in the isolated session
journal. Host shutdown left no newly owned pinned App Server process.

This run used only the synthetic LM Studio catalog identity
`p2-06-loopback-interruption-model` and a loopback endpoint; no weights or live
inference were involved. Runtime `0.155.1`, revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318` matched
the lock. The diagnostic panel confirmed the isolated application `Data`
root and `Ordinary Codex root used: false`. The exact run, screenshots, paths,
and commands are in [`VERIFICATION.md`](../release/VERIFICATION.md#2026-09-24--phase-2-p2-06-stop-or-lose-a-live-turn-then-continue-safely).

**Observed limitation / remaining qualification:** the incomplete assistant
delta is retained in the live renderer through interruption, but App Server
does not include that unfinished message in the saved turn transcript. After
renderer reload, the prompt and `interrupted` status remain while the partial
assistant text is absent. This slice did not add a second durable transcript
store. A bounded native live-provider cancellation check passed on 2026-09-24
using the exact accepted NEX free route and the user-supplied key via a masked
prompt: real streaming text appeared, Stop yielded an attributed interrupted
state, reload did not replay the prompt (journal count 1), and the owned host
exited cleanly. An earlier attempt omitted the credential from its test process;
that was a harness handoff error, corrected for this run. Durable partial-text
restoration and broad crash/concurrency recovery remain open. Exact evidence is
in [`VERIFICATION.md`](../release/VERIFICATION.md#2026-09-24--phase-2-p2-06-stop-or-lose-a-live-turn-then-continue-safely).

### P2-07 — Review a supported file change end to end

**User outcome:** A supported patch turn shows its attributed live diff and
saved item review, survives renderer reload and WPF restart, and accurately
signals invalidated, malformed, oversized, and unavailable diffs. A shell
write without a trustworthy App Server diff is not fabricated as reviewed.

**Work boundary:** `ThreadSavedChangeProjector.cs`, App Server diff-event
projection, `turn-review.mjs`, and the review drawer. Keep review read-only;
do not bind it to permission authority in this slice.

**Proof:** pinned-runtime deterministic patch fixture and native WPF reopen;
focused malformed/oversized/invalidation tests; one narrow live exact-route
check only if the route remains available. Preserve the existing live NEX
evidence without treating it as general patch support.

**Status — passed 2026-09-24:** a deterministic loopback Responses fixture
completed one native WPF/WebView2 patch turn. The exact App Server thread/turn
diff appeared while the turn was active; its saved review remained readable
after renderer reload and graceful host restart. The host suite and focused UI
tests cover malformed, oversized, invalidated, and unsupported saved diffs.
This verifies the bounded read-only review path, not general provider support
or approval authority. The drawer currently presents absolute paths, which can
cause horizontal scrolling; relative-path presentation is a non-blocking UI
follow-up.

### P2-08 — Exercise real command and permission approval requests

**User outcome:** In an isolated QA configuration that can actually emit
approval requests, an ordinary command or permission request displays the
correct bounded card, an explicit supported choice reaches App Server once,
and denial/timeout/unknown metadata fail closed. Production's accepted
unrestricted default remains unchanged.

**Work boundary:** named host responder, `approvals.mjs`, card UI, and focused
protocol tests. Validate exact thread/turn/request identity and one-shot
decisions; do not route model-controlled work through `thread/shellCommand`.

**Status (2026-09-24): passed at bounded deterministic native scope.** Three
fresh WPF/WebView2 roots drove actual approval requests from the pinned App
Server using a loopback Responses fixture. Native accept, deny, and five-second
no-response timeout cases resolved the exact request once; only accept ran the
requested `ver` command. The pinned binary identity/hash and isolated roots
were verified. This is not live-provider/model qualification and does not close
the Phase 1B live-provider approval or authority-matrix gates. See the dated
record in `VERIFICATION.md`.

The existing focused UI tests also verify fail-closed behavior for omitted
decision metadata, unknown permission fields, and unknown request methods;
those are regression tests, not additional native App Server scenarios.

**Proof:** real pinned-App-Server approval generation and response under an
isolated nondefault QA policy, native WPF render/action, and App Server
terminal evidence for accept, deny, and no-response/invalid request. The
synthetic bridge/fake server tests remain regressions, not proof of live
approval generation. If current runtime/policy cannot emit a given request,
record the precise protocol blocker rather than widening default authority.

### P2-09 — Resolve file-change approval without a mystery diff

**Accepted requirement:** Never ask the user to approve a file change whose
contents NeoBabylon cannot reliably show. When a trustworthy reviewable change
is available, show it and bind consent to the exact request; otherwise remain
deny-only and report the acceptance gap.

**Observed upstream behavior (pinned 0.155.1):** `item/started` is emitted
from the verified `apply.action` and its projected `changes` are carried into
the approval request. `item/fileChange/patchUpdated` is a cumulative
model-argument/parser snapshot, not execution authority. At execution,
`ApplyPatchRuntime` reparses the original patch and derives updated contents
from the then-current filesystem. There is no source-level preimage/version
check tying that later filesystem read to the preview. An external edit while
approval is pending can therefore make execution fail or produce a result
different from the preview. This is a verified upstream limitation, not a
NeoBabylon transactional guarantee.

**P2-09 implementation choice (not a new product requirement):** Freeze the
review at matching `item/started`, fingerprint its schema-versioned exact
identity and changes, and require the host-issued approval-instance ID plus
request/thread/turn/item identity and fingerprint before accepting. The host
constructs only the one-shot `{decision:"accept"}` response. Ignore
pre-start parser snapshots; tolerate an identical post-start snapshot; revoke
accept and fail closed on a changed/malformed snapshot. Keep deny available
without a preview. Serialize approval commitment against invalidation.

**Work boundary:** source/protocol inspection for
`item/fileChange/requestApproval`, host review projection, approval card,
and focused tests. A new runtime patch is justified only by concrete
evidence, narrow scope, and the existing divergence decision process. Do not
use a post-hoc turn diff as pre-execution approval evidence.

**Verified bounded outcome (2026-09-24):** Isolated native WPF/WebView2 QA
produced one accepted and one denied pinned-App-Server file-change approval.
The exact preview was visible, inertly rendered, and bound to its request;
the accepted fixture patch changed only the expected test file, while denial
left it unchanged. Host regressions cover stale request/turn/instance,
fingerprint mismatch, exact response shape, duplicate response, identical
snapshot, changed/malformed snapshot invalidation, and resolved lifecycle.
This closes the preview-to-request binding slice, not the concurrent-file
edit race above, the full approval matrix, or Windows containment. See
[`VERIFICATION.md`](../release/VERIFICATION.md#2026-09-24--phase-2-p2-09-file-change-approval-preview-and-binding).

### P2-10 — Complete keyboard and screen-reader task flows

**User outcome:** A keyboard-only and screen-reader user can select a project
and model, start/resume/search a task, send/stop, inspect tool failure/review,
use dialogs/approval controls where present, and return focus predictably.
Dark and light contrast remains readable at supported desktop sizes.

**Work boundary:** `App.tsx`, `app.css`, `modal-focus.mjs`, Astryx controls,
and focused UI tests. Preserve the existing Codex-first hierarchy and neutral
theme; this is not a wholesale redesign.

**Proof:** automated role/name/focus/inert/escape/tab tests plus a native
WPF/WebView2 keyboard and Windows screen-reader pass. Record the reader,
Windows version, resolutions, observed failures, and fixes. One diagnostics
modal test does not qualify the rest of the app.

**Progress (2026-09-25; not accepted):** The diagnostic UI has keyboard model
selection, explicit focus behavior for task resume and dialogs, named
status/approval regions, visible focus indicators, and an explicit named Tab
stop for scrollable drawer content. The nine scripted keyboard/focus flows
were exercised in a synthetic Edge fixture. A fresh browser-only Edge run
passed all nine scripted flows at 1440x900 dark and 1024x768 light, with no
console errors or external requests. Its eight-state rendered-text/placeholder
contrast audit observed 714 text/placeholder runs and reported zero active or
disabled-state contrast exceptions and zero unknown backgrounds. The run was
preceded by a deliberate red test that reproduced all 16 below-AA occurrences
across 14 disabled-control findings. Removing opacity-based text fading and
retaining disabled styling through neutral backgrounds/cursors cleared the
findings. A fresh current-source native WPF/WebView2 run passed the keyboard
model picker, diagnostics dialog focus/inertness/Tab loop, loaded-history
search and page loading, exact saved-task resume, transcript-overflow
disclosure, and project switching. The native run covers a subset, not all
nine flows; Windows screen-reader interaction and the remaining native flows
remain untested. A separate native P2-06 fixture run now also passes the
composer keyboard journey: Enter sends the original prompt, Enter activates
the focused Stop button, the interrupted status survives renderer reload
without automatic replay, and Enter sends one deliberate continuation. The
fixture received exactly two Responses requests; the runtime identity/hash
matched the lock, application `Data` remained isolated, and the host/App Server
exited cleanly. Neither this focused flow nor the earlier subset covers the
complete nine-flow native suite or Windows screen-reader output, so P2-10
remains open.

**Native WebView2 accessibility-tree smoke — 2026-09-26:** the current-source
Release WPF/WebView2 visual harness captured the native Chromium AX tree in two
states. The primary shell exposed 251 nodes and included the named Search
loaded conversations searchbox, New task and Open diagnostics buttons, and
Message NeoBabylon textbox. Runtime diagnostics exposed 273 nodes, including
the named dialog and Done button; the background searchbox was absent while
the modal was open. The run passed with three host launches, no page issues,
no provider inference, and no ordinary Codex data-root use. App Server
`0.155.1`, source revision `be2951ea34f0d295ed0becf97079f92fa5f6950e`, and
SHA-256 `9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d`
matched the runtime lock. Source root was `D:\CODING\NeoBabylon`; application
root and `Data` were isolated under
`D:\CODING\NeoBabylon-Data\QA\P2-10-native-ax-20260926-02\P2-12-native-visual-1790374903952-36916\`.
The result JSON records the accessibility snapshots. This is native role/name
and modal-inertness smoke evidence only; it does not qualify Windows Narrator,
broader screen-reader behavior, or the complete nine-flow native suite, and
does not close P2-10.

### P2-11 — Stream a genuinely large live response without losing truth

**User outcome:** Long assistant output and tool output remain responsive,
scrollable, attributable, and complete or explicitly truncated with a way
to inspect omitted material where the pinned App Server supports it; otherwise
the limitation is explicit. A late provider failure leaves partial text
visible and typed as failure, not success.

**Work boundary:** `stream-batcher.mjs`, transcript projection/rendering,
bounded host payloads, and performance tests. Keep the App Server journal
authoritative; do not keep an unbounded second copy in React or silently
discard content.

**Proof:** retain the 5 MB/2 MB synthetic stress case, then run a bounded
native WPF + pinned-App-Server case and one selected live model/route where
available. Measure elapsed time and process memory; test cancellation,
renderer reload, partial-output failure, truncation/omitted-range access, and
saved-history reconstruction. Label synthetic, mock, and live results
separately; do not promise arbitrary-size support.

**Verified 2026-09-24 — bounded native/deterministic portion only:** the
isolated WPF/WebView2 harness passed against Codex App Server `0.155.1`, source
revision `be2951ea34f0d295ed0becf97079f92fa5f6950e`, binary SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`. Its
Responses endpoint was a loopback fixture labeled
`p2-11-loopback-large-output-fixture`; no live model or weights were used.
Four fixture requests used that exact model identifier without fallback.
The native path displayed 120,000 of 1,100,000 assistant characters and
marked 980,000 omitted; five saved-output pages reached the inspected middle
marker at 32,768 characters per page. A deterministic `exec_command` tool
round trip returned captured output with the upstream omission marker
preserved. Completed history reconstructed after WebView reload. A cancelled
partial response was visible before reload but the pinned App Server did not
persist its streamed deltas; the reopened task now discloses that unsaved
partial text may be unavailable. Partial text also remained visible and
typed as failure after a non-retryable provider error.

The run sampled host and App Server memory 16 times; observed maxima were
275,525,632 / 247,861,248 bytes working-set/private for WPF and
301,408,256 / 213,209,088 bytes for App Server. These are sampled observations,
not peak guarantees. Isolated application root and `Data` were under the QA
run directory, distinct from source root; ordinary Codex root use was false.
`pageIssues` and fixture errors were empty.

**Fresh isolated Release-host rerun — 2026-09-25:** the same deterministic
native path passed again using a newly built Release WPF host and the locked
App Server identity above. The loopback fixture remained
`p2-11-loopback-large-output-fixture` at `http://127.0.0.1:63098/v1`; no
weights, live provider, or credential were used. A successful `exec_command`
requested 1,100,031 output characters; the upstream omission marker reached
the model continuation. The harness verified 1,100,000 assistant characters,
120,000 displayed and 980,000 explicitly omitted, five 32,768-character saved
output inspection pages, history/inspection reconstruction after WebView
reload, truthful disclosure that cancelled deltas are not persisted, and
typed/visible partial output after a late provider failure. Exactly four
fixture requests used the selected model, with no fallback. The isolated
application root and `Data` are under
`D:\CODING\NeoBabylon-Data\QA\P2-11-native-output-release-rerun-20260925\P2-P211-native-1790302010892-70740\application`;
the source root is `D:\CODING\NeoBabylon`, and
`ordinaryCodexRootUsed=false`. The run sampled each process 18 times; maximum
observed working-set/private bytes were 254,013,440 / 212,791,296 for WPF and
159,911,936 / 66,699,264 for App Server. These are observations, not peak or
arbitrary-size guarantees. Completion became visible at 5,926 ms; the fixture
stream took 215 ms; provider cancellation closed after 1,634 ms. Full evidence
and six screenshots are in the run directory above. This rerun strengthens
only deterministic native evidence; native live-model-size qualification
remains open.

**Additional direct live-provider evidence — 2026-09-25:** a first exact-route
request capped at 4,096 output tokens ended incomplete at that cap. One bounded
follow-up through the direct OpenRouter Responses API (not App Server/WPF)
completed 19,233 streamed characters from the exact `nex-agi/nex-n2.5-pro:free`
alias, reporting 4,056 output tokens and zero cost. The generation lookup
returned 404, and request-pinned endpoint routing was not post-hoc attested.
Generated text was not retained. This qualifies a direct live response only;
native live rendering, saved reconstruction, memory limits, and arbitrary-size
behavior remain open. A later bounded repeat completed 18,704 characters /
3,877 output tokens, also at provider-reported zero cost with no post-hoc route
attestation and a generation lookup of 404. Neither request traversed the
native App Server/WPF path. Full details are in the dated verification record
and the authoritative model-capability JSON.

**Native live-model rendering — 2026-09-25:**
`tests/qa/native-live-nex-output.mjs` completed a real no-tools turn through
the WPF/WebView2 host and the locked Codex App Server `0.155.1`, SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`, using
`nex-agi/nex-n2.5-pro:free` (variant
`nex-agi/nex-n2.5-pro-20260907:free`). The effective config referenced
`OPENROUTER_API_KEY` by environment-variable name and selected
`nex-agi/fp8`; the UI showed the same model and route. The turn completed with
18,403 visible characters / 18,413 UTF-8 bytes, 0 tool-activity cards, no
page issues, first visible text at 23,383 ms, and UI completion at 310,223 ms.
The response text was not retained; its SHA-256 is
`1e95c1b456aa82d02e558c917d67969e736ec7745e74682c3b397c203ca9bf45`. The
capability card showed provider context 262,144, Codex task context 249,036,
advertised reasoning levels, and effective reasoning `Unknown`. The host and
App Server were sampled 59 times; observed maximum working-set/private bytes
were 155,930,624 / 104,722,432 for WPF and 151,293,952 / 60,502,016 for App
Server. These are sampled observations, not peak guarantees.

The isolated application `Data` root was outside the source root and the
ordinary Codex root was not used. A scan of 2,028 application-root text files
found no credential-like OpenRouter key; no key value was written to the
result. The configured route is request-side evidence only: this native run
did not produce independent provider route attribution. The separate direct
request's reported zero cost is not evidence of this App-Server request's
cost, which was not surfaced in the native result. The UI/App-Server request
also exposes no explicit output-token cap; the prompt shape is bounded but
does not hard-cap generation or cost. Result and screenshot:
`D:\CODING\NeoBabylon-Data\QA\P2-11-native-live-nex-1790313675806-41500\`.
This verifies one live native rendering path, not arbitrary-size behavior or
the complete P2-11 acceptance.

**Native output-cap and route-attestation source audit — 2026-09-25:** the
versioned `0.155.1` source defines `ResponsesApiRequest` without
`max_output_tokens`; its WebSocket request conversion likewise maps only the
fields present on that request. The host capability/catalog/config path does
not transform advertised `maxCompletionTokens` into a generation ceiling, and
`tool_output_token_limit` is for tool-result storage, not model completion.
Earlier direct OpenRouter requests did accept a cap, including a run stopped
at its requested 4,096 tokens, but that does not qualify the native
App-Server/WPF path. No live request was needed or sent for this source audit.

**New provider/API evidence — 2026-09-26:** OpenRouter's current official
Responses reference documents both the `max_output_tokens` request field and
the opt-in `X-OpenRouter-Metadata: enabled` response metadata; its streaming
reference says `X-Generation-Id` is returned for Responses and the other API
endpoints. The official generation lookup returns the selected model,
`provider_name`, token counts, and `total_cost` when the supplied generation ID
is valid ([Responses API](https://openrouter.ai/docs/api/api-reference/responses/create-responses),
[streaming reference](https://github.com/OpenRouterTeam/docs/blob/main/api_reference/streaming.mdx),
[generation metadata API](https://openrouter.ai/docs/api/api-reference/generations/get-request-&-usage-metadata-for-a-generation)).
This narrows the question from provider API support to preserving and testing
the metadata through NeoBabylon's pinned transport; it does not prove current
native behavior or retroactively attest the prior NEX runs.

Read-only inspection of the pinned source confirms the application gap:
`ResponsesApiRequest` and its WebSocket conversion have no output-token field.
The HTTP SSE path receives response headers and currently retains
`x-request-id` in `ResponseStream`, but does not retain `X-Generation-Id`; the
WebSocket path currently sets `upstream_request_id=None`. This is a feasible,
focused runtime patch candidate for the HTTP path. WebSocket route attestation
still requires qualification; if it cannot carry equivalent per-response
evidence, the design must disable that transport for a route that requires
attestation rather than silently downgrade the claim.

OpenRouter's generation metadata endpoint can report provider/model and usage
when a generation lookup succeeds, but the recorded NEX lookups returned 404
and do not attest `nex-agi/fp8`; the configured route remains request-side
evidence only. See the [OpenRouter generation metadata API](https://openrouter.ai/docs/api/api-reference/generations/get-request-&-usage-metadata-for-a-generation).

**Implementation proposal, not a settled choice:** add an explicit
per-capability completion ceiling distinct from advertised model maximums,
then serialize it on every supported native request transport (or formally
disable a transport that cannot carry it). The API supports the necessary cap
field and has an official generation-metadata mechanism, but neither has been
tested through the pinned native runtime. The exact cap/default, enforcement
behavior, cost ceiling, WebSocket parity, and whether route metadata is
required are unresolved; do not silently choose them. P2-11 remains open.

### P2-12 — Native visual and interaction acceptance

**User outcome:** The actual desktop window looks coherent and behaves
consistently at the supported desktop sizes in default dark and chosen light
mode: charcoal title bar/body, tower branding, discoverable project/task
navigation, readable conversation/tool/review states, persistent appearance,
and a dismissible unrestricted notice while the authority chip remains.

**Work boundary:** only UI/native-chrome defects found in this pass. Use the
adopted Codex-first reference, current screenshots, and existing Astryx
components; do not expand to Phase 3 toolbox or redesign accepted flows.

**Proof:** native WPF screenshots and interaction checks at the documented
desktop sizes, renderer reload and app restart, keyboard focus and contrast
smoke, no console/page errors. Record any remaining visual choices as
proposals, not settled requirements.

**Verified bounded native slice — 2026-09-24:**
`tests/qa/native-visual-acceptance.mjs` passed against the locked App Server
0.155.1 (SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`). It
captured the complete WPF window at 1440×900 and 960×720 in charcoal dark and
light mode, exercised the actual native window resize, renderer reload, theme
toggle, and host restart. The fresh default was dark; light preference and
then dark preference survived reload/restart. The unrestricted notice could
be dismissed for the session or suppressed persistently, while the authority
chip and diagnostics remained available. Tower branding, primary navigation,
composer, and warning stayed within the viewport; keyboard focus was visible;
the runner recorded no page issues. Sampled primary-text contrast ranged from
6.76:1 to 13.71:1 in dark mode and 5.16:1 to 12.32:1 in light mode. An earlier
light permission-chip sample measured 3.44:1; its foreground was darkened and
the passing rerun measured 5.16:1. These are named-element smoke samples, not
a full WCAG audit or a Windows screen-reader evaluation.

Evidence and whole-window screenshots are under
`D:\CODING\NeoBabylon-Data\QA\P2-12-native-visual\P2-12-native-visual-1790284917857-36324\`.
The isolated root and `Data` were outside the repository; ordinary Codex root
use and provider inference were false. P2-12's bounded native visual pass is
verified, but at that checkpoint P2-10 screen-reader/full contrast work,
P2-11's hard output-size and native request cost/route-attestation limits, and
P2-13 remained open; full Phase 2 was not accepted.

**Fresh native visual rerun — 2026-09-25:** the WPF/WebView2 test passed three
host launches at 1440×900 and 960×720, in charcoal dark and light themes. It
verified matching native chrome, dark default, theme persistence through
renderer reload and host restart, visible focus, notice dismissal, and zero
page issues. The exact locked App Server identity/hash matched; the isolated
application root and `Data` were outside the source tree, and ordinary Codex
root use and provider inference were false. Screenshots and `result.json` are
under `D:\CODING\NeoBabylon-Data\QA\P2-10-native-contrast-20260925\P2-12-native-visual-1790318060968-27424\`.
The new browser contrast audit is recorded under P2-10; Windows screen-reader,
P2-11 size/cost/route bounds, and P2-13 remain open.

### P2-13 — Close the Phase 2 acceptance evidence

**User outcome:** A reviewer can tell exactly which Phase 2 paths work on
the pinned runtime and selected provider tuples, which remain qualified only
by deterministic fixtures, and which are blocked. The app must not advertise
full Phase 2 acceptance if any required slice remains open.

**Work boundary:** `docs/release/VERIFICATION.md`, `docs/product/STATUS.md`,
`ROADMAP.md`, `FEATURE_MATRIX.md`, and the Phase 2 checklist. Reconcile the
plan's stale statement that native saved-patch rendering is open with the
newer recorded native test; do not erase the test's limited scope.

**Proof:** rerun the complete UI tests/typecheck/build and affected host
harness against the exact runtime lock; repeat/diagnose the intermittent
SQLite cleanup case; classify the current-source broad App Server result and
reconcile the earlier 26-failure run separately. Do not require an unexplained
blanket green or attribute candidate failures to upstream without a clean
baseline.
Verify native startup, runtime identity, isolated Data, app-root/source-root
separation, exact provider/model/capability reporting, and no silent
fallback. Link dated screenshots and live/fixture logs without secrets.
Mark the gate passed only if every required P2 outcome is demonstrated or
Martin explicitly revises its acceptance bar.

**Current evidence — 2026-09-25:** the serial current-source integration run
finished 1,170 passed, 25 failed, and 11 ignored. The 25 failures classify as
nine code-mode-helper dependent cases (missing
`codex-code-mode-host.exe`), nine stdio-helper dependent cases (the initial
run lacked `test_stdio_server.exe`), five shell/environment cases, and two
Guardian hook assertions. Focused retries passed 14 stdio-helper cases after
building the helper and four of five shell/environment cases with process-
local shell/Python shims. The remaining
`thread_shell_command_honors_optional_timeout` case sees its marker in the
stream delta but not the terminal failed command item; this is the explicit
human-only `thread/shellCommand` path, not NeoBabylon's model-controlled
`exec_command` path. The nine code-mode cases fail because the V8 `150.4.0`
sandbox prebuilt returned 404 and required local source/build inputs are
missing; they are environment-blocked, not passed.

A focused clean-HEAD worktree comparison ran three Guardian tests: one passed,
and the same two hook assertions failed as in the current-source checkout.
Cargo offline resolution required local workspace-version normalization
because the committed lock labels workspace packages `0.0.0`; only local
workspace package version labels changed (304 diff lines, no external
dependency versions/checksums). This focused comparison does not establish a
full clean-baseline suite. The locked binary's depfile points to the separate
`NBRT-RouteControl` checkout rather than the source checkout named in the
product lock. A fresh comparison found that its tracked patch fingerprint and
newline-normalized source content match the canonical sibling. The initial
no-remap isolated rebuild differed. A corrected offline rebuild restored the
checkout's Windows stack/static-CRT flags and remapped the QA target path; all
executable/data/relocation sections then matched the locked image byte-for-byte.
The remaining PE differences are limited to seven generated `OUT_DIR` hash
strings, three debug-directory timestamps, the CodeView PDB GUID, and the PE
COFF timestamp. Whole-image comparison after normalizing exactly those fields
found zero other differing bytes. The raw executable hashes still differ, so
record this as normalized content parity, not raw-hash identity or proof of all
historical build inputs. The canonical runtime lock is unchanged and no runtime
patch is justified by this evidence. See `VERIFICATION.md` for exact commands,
hashes, artifact paths, and limitations.

The current NeoBabylon host harness was rebuilt with the QA-only exact child
process identity probe and passed **82/82** checks (exit code 0) against the
unchanged runtime lock. A focused repeated-interruption probe also passed both
attempts: typed interruption, one fixture request, resume without replay, and
the same PID/start-time identity absent after supervisor disposal. The older
global PID-snapshot assertion could include another simultaneous instance and
did not identify who launched a reported PID; those earlier failures therefore
remain unattributed rather than proven false positives or product leaks. The
`logs_2.sqlite` cleanup exception did not recur in that specific 82/82 run; a
later run reproduced a `goals_1.sqlite` cleanup lock in the saved-task recovery
test. See the later P2-13 recheck below; the lock owner was not captured.

**Latest P2-13 host recheck — 2026-09-25:** the saved-task recovery test now
uses the supervisor-exposed PID/start-time tuple for every process it
deliberately terminates, checks the final owned process exited, and applies
the existing bounded exact-parent cleanup retry to its GUID-scoped root. This
removes the prior machine-wide process scan from this test. The focused
`--probe-exited-supervisor-client-recovery` passed three consecutive times
before the cleanup change and once after rebuilding it. The complete
ApprovalQA apphost suite then passed **82/82** (exit 0), with no SQLite cleanup
exception. Machine output is under
`D:\CODING\NeoBabylon-Data\QA\P2-13-host-harness-post-retry-20260925\`.
The previous lock's owner remains unknown, so this is a clean rerun plus
bounded cleanup mitigation, not proof the lock source is eliminated.

One intermediate suite launch invoked the framework-dependent test DLL via
`dotnet` instead of the test apphost; ten fake-server protocol tests then read
the .NET host's “Possible reasons…” output as JSON and failed. That run is
excluded as an invalid harness invocation, not product evidence. The supported
`dotnet run` / generated-apphost invocation is documented in `tests/README.md`.

**ApprovalQA suite refresh — 2026-09-26:** two consecutive runs of the complete
NeoBabylon host harness exited 0. The counted repeat reported **86 passed, 0
failed**, with no SQLite/cleanup diagnostic. The command was
`dotnet run --project tests/host/NeoBabylon.Phase1A.Tests.csproj
--configuration ApprovalQA --no-restore`; all provider paths were deterministic
fixtures. This does not change the separately reported pinned-source App Server
suite result (1,170 passed / 25 failed / 11 ignored), and is not the Phase 2
acceptance gate.

Full Phase 2 stays open because P2-10 screen-reader/native-flow coverage,
P2-11 hard output/cost/route bounds, broad runtime-suite/environment
classification, and other slice-specific gaps remain.

## Coverage and sequencing

| Open Phase 2 area | Slice(s) |
| --- | --- |
| Broader history scale/concurrency and any server-backed full-history search | P2-01 (bounded native page/reopen plus transcript-overflow disclosure verified; broader coverage open) |
| Model/capability identity through restart and stale bindings | P2-02 |
| Native pending-send acknowledgement/reload/restart path (bounded mock case passed); broader active-turn recovery | P2-03 |
| Local storage write/clear/total-failure behavior (bounded native cases passed; unsaved text cannot survive rejected persistence) | P2-04 |
| Focused list/start interleavings and two-project restart recovery passed; broader concurrent stress and crash recovery remain open | P2-05 |
| Live stop/child/provider failure, partial output, no replay | P2-06 |
| Supported diffs, saved review, invalidated/unsupported change paths | P2-07 |
| Deterministic native command approval accept/deny/timeout passed; live-provider generation and the full approval matrix remain open | P2-08 |
| Exact file-change approval preview/request binding passed in isolated native QA; concurrent edits between preview and execution, broader approval matrix and containment remain open | P2-09 |
| Nine-flow synthetic Edge keyboard/focus coverage passed; native subsets passed model picker, diagnostics, search/pagination, exact task reopen, overflow disclosure, project switch, and composer Enter send/stop/continue with no replay; eight-state audit measured 714 observations with zero contrast failures/unknowns; Windows screen-reader and remaining native flows remain open | P2-10 |
| Native deterministic fixture stream/cancel/failure, saved-output paging, and memory sampling passed in isolated Release-host QA; one native live NEX turn rendered 18,403 characters with no tool activity. A hard output-token cap, native request cost, provider-side route attestation, and arbitrary-size limits remain open | P2-11 |
| Bounded whole-shell dark/light/native visual and interaction pass rerun at desktop/compact sizes; screen-reader qualification remains separate | P2-12; P2-10 |
| Full gate, test flake, broad-suite classification, release evidence | P2-13 |

P2-01, P2-02, P2-07, P2-10, and P2-12 can be pursued independently after
the baseline check. P2-03 precedes P2-04; P2-05 and P2-06 use the same
recovery vocabulary but need separate acceptance. P2-07 precedes P2-09.
P2-08 and P2-09 are the Phase 1B/Phase 2 approval overlap: because the
remaining Phase 1B checks were deferred, their absence must stay visible;
doing them for Phase 2 does not silently reopen Windows containment or claim
all Phase 1B checks passed. P2-13 is last.

Live-provider availability and exact model/route must be checked at run
time. If access is unavailable or a protocol blocker remains, finish all
independent deterministic/native slices, preserve the failed/blocked evidence,
and request only the specific missing decision. No alternative provider/model
may be substituted silently. The current free NEX route's published expiry
was 2026-09-25 in the recorded preflight, so prior availability is not a
promise of future availability.
