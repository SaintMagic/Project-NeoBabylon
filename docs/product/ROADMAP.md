# NeoBabylon roadmap

Status: **Feature/UI implementation checkpoint 2026-10-04; phase acceptance remains open.**

Martin requested implementation before behavioral testing. The development
desktop artifacts now exist; Core/Host/React integration, protected-record
recovery and generated-tool review/lifecycle source are implemented. The new
runtime is source-built and locked; the later provider/conversation checkpoint
adds narrow native identity and live OpenRouter command evidence, not full
runtime/provider acceptance. Manual Activate
has a separate UI confirmation, but its route gate remains closed. See the
[current checkpoint](../history/2026-10-04-feature-ui-build-checkpoint.md).
The historical evidence below retains its original binary/model scope; it does
not certify the refreshed build or `stealth/space-bunny-alpha`. The
[provider/conversation follow-up](../history/2026-10-04-provider-and-conversation-ui-checkpoint.md)
implements accepted same-chat explicit model switching (NB-DEC-013), Rename,
compact completion-cap options and an explicit NVIDIA transport adapter.
NVIDIA live inference remains unqualified; broad deferred gates are unchanged.
Martin explicitly deferred the remaining Phase 1B checks until needed or
requested; they remain open, not passed. The selected providers have narrow
live App Server evidence. LM Studio has two completed same-tuple runs and two
earlier ten-minute non-completions, so repeatability remains open. OpenRouter
has request-pinned Nex AGI route evidence, including one live function-form
`apply_patch` round trip on its recorded historical runtime hash. Phase 2 includes a
substantial WPF/WebView2 shell with project/history, drafts, recovery, review,
and appearance slices; significant acceptance work remains. See
[`docs/WIP/INITIAL_BUILD_PLAN.md`](../WIP/INITIAL_BUILD_PLAN.md) and the
[OpenRouter qualification](../release/PHASE1B_OPENROUTER.md).

## Phase 0 — foundation and evidence

Completed for review on 2026-09-22:

- preserve the startup brief and establish the categorized documentation tree;
- acquire the pinned Codex source, matching Windows binaries, official docs,
  comparison captures, and generated protocol outputs;
- inspect transport, lifecycle, persistence, providers, permissions, tools,
  skills, hooks, MCP, plugins, and dynamic-tool paths;
- record capability status, limitations, settled direction, remaining
  technical gaps, and the initial build plan.

## Phase 1A — smallest honest functional path (runner and visible UI pass; repeat reliability open)

The isolated qualification runner starts the pinned App Server with runtime
identity and `<NeoBabylon root>\Data`, runs mock Responses first, and has
completed one real LM Studio tool round trip under explicit `unelevated`
policy. The App Server emitted `turn/completed`; the exact ordinary command
returned exit code 0. LM Studio now has two completed App Server qualification
runs and a separate visible WPF/WebView2 live run using the same selected
model and explicit policy. Two earlier ten-minute non-completions keep repeat
reliability open. The visible WPF OpenRouter path is also verified. Neither
result qualifies the full tool and approval surfaces. See
[PHASE1A_LMSTUDIO.md](../release/PHASE1A_LMSTUDIO.md).

## Phase 1B — foundational qualification (incomplete; remaining checks deferred)

Initial slice completed: exact capability-to-Codex catalog injection,
requested/effective Windows authority reporting, typed tool/turn diagnostics,
and deterministic policy qualification. The qualification runner retains
timeouts if session-journal reading fails; locked-file regression and mock
paths pass. Both selected provider tuples now have one successful narrow live
App Server tool round trip, including a visible WPF OpenRouter click-through.
Open evidence includes LM Studio repeat variance, actual context budgeting/
compaction and stale-capability behavior, broader interruption/process-tree and
recovery cases, live approval prompts, credential isolation, and single-owner
data protection. Windows containment is known to fail for legacy backends and
is explicitly deferred, not qualified. Martin directed that the remaining
Phase 1B checks stay deferred until needed or requested; Phase 2 proceeds under
the separately accepted unrestricted-tool authority.

## Phase 2 — first functional desktop client (active; full gate open)

Implemented slices include the Codex-first React/TypeScript UI in the thin
WPF/WebView2 host; explicit capability records and provider identity; streamed
conversation/tool activity and typed diagnostics; project selection; saved
history, pagination, search, task-scoped drafts and reconnect; interrupted-turn
recovery; read-only diff review; synthetic bounded approval UI; and the dark-
first neutral appearance. One route-pinned live NEX function-form patch call
also produced a saved App Server review. The App Server remains the single
history authority, and the host/renderer bridge remains named rather than
generic.

P2-01's bounded native history path is now verified: 56 tasks loaded over two
pages (50 + 6), honest loaded-page search messaging, exact older-task resume,
cursor exhaustion, and an empty-project switch, without a provider request on
reopen. A 2026-09-25 native follow-up also reopened a saved 120,576-character
assistant response, enforced the combined 120,000-character transcript preview
budget, and displayed the explicit saved-history omission notice; provider
requests remained at 56. This does not establish full-history search or broader
history-scale and concurrency behavior.

P2-02 is also verified as a narrow native slice: explicit switching between
two synthetic loopback capability records, retained tasks across host restart,
history-only display for a context-mismatched record, and exact re-opening of
the unchanged original task. The test surfaced and fixed a model-switch/history
refresh race. No live model/provider behavior is claimed; full Phase 1B and
Phase 2 gates remain open.

P2-03 is now verified as a narrow native mock-provider slice: an unacknowledged
send stayed retryable through WebView2 renderer reload without auto-replay; an
acknowledged prompt was journaled once and not replayed across host restart.
The native test also found and fixed a send/history-refresh serialization
race. This does not qualify general crash, storage-failure, or concurrency
recovery; the full Phase 2 gate remains open.

P2-04 is verified as a bounded native WebView storage-failure slice. Injected
draft-write rejection retained visible text and a warning only for the live
window; restart correctly could not reconstruct text that was never stored.
Injected clear and fallback failure left the accepted prompt once in history
beside the stale draft and a warning to inspect history before retrying. Total
storage failure preserved readable App Server history but could not preserve
new unsaved text; it also showed the conservative unresolved-pending warning
because that marker could not be read. Only four authored prompts reached the
loopback fixture and there was no replay or live inference. The production
UI/runtime was unchanged.
This is not broad crash/storage recovery; Phase 1B and full Phase 2 remain open.

P2-05 is verified as a bounded two-project operation-ordering and native
recovery slice. The host regression covered active-turn/history/project-switch
conflicts, list-before-turn, exact workspace-bound task resume, and fail-closed
cross-project resume. Native WPF/WebView2 delayed a project history refresh,
then restored the selected Beta task after a graceful host restart without
replaying the accepted turn; an attempted Alpha resume under Beta was
rejected, and deliberate continuations in both projects each produced one
loopback fixture request. No production source change or live inference was
needed. Larger concurrent stress and crash recovery remain open; this does
not close the Phase 1B or full Phase 2 gates. See the dated evidence in
[`VERIFICATION.md`](../release/VERIFICATION.md#2026-09-24--phase-2-p2-05-cross-project-operation-ordering-and-recovery).

P2-06 is verified for deterministic partial-delta interruption, typed child
exit, and native WPF stop/reload/continue against a loopback fixture. A bounded
native live NEX run also emitted real text, stopped with the attributed
interrupted state, restored that status after reload without replay (one prompt
in the journal), and exited cleanly. The user-supplied key was passed through a
masked prompt to the isolated test process; an earlier no-key result was a
test-process handoff error, not missing authorization. The partial assistant
text did not survive renderer reload, so durable partial-output restoration
remains open. Phase 1B and the full Phase 2 gate remain open. See the dated evidence in
[`VERIFICATION.md`](../release/VERIFICATION.md#2026-09-24--phase-2-p2-06-stop-or-lose-a-live-turn-then-continue-safely).

P2-07 is verified as a bounded native read-only diff-review slice: an
App-Server-attributed patch was visible during the exact active turn, and the
saved review reopened after renderer reload and graceful WPF host restart.
Malformed/oversized/invalidation cases remain covered by focused UI and host
tests. The fixture was deterministic and loopback-only; no live provider
request was made. Absolute-path presentation can cause horizontal scrolling
and is a non-blocking follow-up. See the dated evidence in
[`VERIFICATION.md`](../release/VERIFICATION.md#2026-09-24--phase-2-p2-07-native-live-and-saved-diff-review).

Full Phase 2 acceptance remains open. Native WPF startup/render/reload/restart,
runtime identity, deterministic saved-patch-review rendering, the bounded
P2-01 native history path, and P2-08 deterministic native command-approval
accept/deny/timeout are verified against the exact pinned runtime. The P2-08
Responses endpoint was a local fixture, not a live provider. Other gaps include
live-provider approval generation and the complete approval matrix,
broader active-turn and concurrent cross-project recovery, draft behavior
across active-turn and interruption boundaries, broader history-scale/
concurrency behavior, the broad keyboard/screen-reader pass, and live/large-
output qualification.
Existing mock, synthetic, and narrow live results must not be extrapolated
beyond their recorded scope. Use the adopted
[Codex-first UI/UX reference](../architecture/UI_UX_REFERENCE.md) while
preserving the thin host and avoiding a second runtime/history authority.

P2-10's nine scripted keyboard/focus flows ran in a synthetic Edge fixture,
including history search/pagination and focus restoration. Across eight
states, the earlier audit measured 713 text/placeholder observations; a fresh
2026-09-25 audit measured 714, with zero active or disabled-state contrast
exceptions and zero unknown backgrounds. A separate fresh native subset passed
keyboard model selection, diagnostics-dialog navigation/focus restoration,
loaded-history search/pagination, exact older-task reopen, and project
switching; it did not rerun all nine flows natively.
The immediately preceding audit exposed 16 below-AA occurrences across 14
disabled/`aria-disabled` findings; opacity-based text fading was removed while
disabled styling was retained. Separately, native P2-12 checked visible focus
after Tab but did not run the nine flows natively. Windows screen-reader
behavior remains untested, so P2-10 remains open. A 2026-09-26 native
WebView2 accessibility-tree smoke additionally verified primary-shell roles
and names and that the background searchbox is absent from the exposed tree
while Runtime diagnostics is modal. This still does not exercise Narrator or
complete the nine native flows.

P2-11 passed its bounded native deterministic large-output, paging,
cancellation, and typed-failure slice with the exact locked runtime. A separate
direct OpenRouter Responses request to the exact NEX free alias completed
19,233 streamed characters at a provider-reported zero cost, but generation
lookup returned 404. A later native WPF/App-Server turn rendered 18,403 live
characters with no tool activity. Native request cost, independent route
attestation, a hard output-token cap, and arbitrary-size limits remain open.

P2-12 passed a bounded native visual/interaction run at 1440×900 and 960×720
in charcoal dark and light themes. The WPF title bar matches the theme, dark
is the fresh default, preference survives renderer reload and host restart,
and the dismissible unrestricted notice leaves the authority chip available.
The fresh native rerun also passed three launches at desktop and compact
sizes with no page issues. The separate rendered-text/placeholder browser
audit now has no active/inactive contrast exceptions or unknown backgrounds;
its preceding disabled-control findings were corrected. These results do not
close the Windows screen-reader gate or overall Phase 2 gate.
See the dated entries in [`VERIFICATION.md`](../release/VERIFICATION.md).

## Phase 3 — toolbox and hardening

Proposed gate: categorized built-in, MCP, skill, hook, and plugin surfaces have
discoverable metadata, permission boundaries, failure handling, and tests.
Windows authority, workspace/Git behavior, diagnostics, and packaging inputs
are qualified before broadening the catalog.

Evidence status (2026-09-26): the versioned upstream capability inventory
(P3-01), bounded large-document proof (P3-04), and deterministic upstream
comparison generator fixture/report (P3-05) are recorded. One P3-02 ordinary
`exec_command` failure now passes through the exact locked App Server with
partial output, exit-code retention, typed attribution, and no retry/fallback;
the identity-bound command Stop slice additionally covers a host-verified
typed-`failed` result and same-instance retry, with a separate successful
native WPF/WebView2 stop. These bounded fixtures do not close the wider
per-operation denial, timeout/cancellation, and partial-output matrix. Output
visibility and forwarding without redaction is now settled by
[NB-DEC-011](../decisions/0011-unrestricted-tool-output-visibility.md). The
toolbox schema (P3-03) and
report schedule/delivery remain proposals, not implemented decisions. See
[`PHASE3-5_VERTICAL_SLICES.md`](../WIP/PHASE3-5_VERTICAL_SLICES.md) and the
dated evidence in [`VERIFICATION.md`](../release/VERIFICATION.md).

## Phase 4 — self-scaffolding and promotion

Implementation checkpoint: candidate envelope/drawer, retained review
snapshots and comparison, inert `.mjs` template, linked activation/revocation
history, named bridge and recovery controls are present. The app-private
`stdio-mcp-node-v1` route remains a proposal. Its source-controlled route gate
is false and the exact runtime/model allow-set is empty. The separate Activate
confirmation implements NB-DEC-012; it does not waive route qualification.
No current-lock live call/revocation/restart result is claimed. Behavioral
testing follows implementation under Martin's revised order.

Proposed gate: a genuine missing capability can produce a candidate record and
helper implementation from the standard template in durable
`Data\GeneratedTools` through the existing authorized execution path. It may
be tested and used while unapproved, appears in an Unapproved Tools drawer,
and is manually reviewed before promotion. Candidate tests, denial paths,
review invalidation, and activation are visible and auditable;
`thread/shellCommand` remains human-only.

## Phase 5 — targeted divergence and qualification

If the stock path cannot satisfy a selected LM Studio/OpenRouter requirement,
a focused runtime/client adaptation may be proposed at the phase where the
incompatibility is observed. Each divergence needs a decision record, upstream
update strategy, compatibility tests, security review, removal condition, and
rollback path. It must not become a broad rewrite of Codex.

Local installation and packaging are deferred per accepted decision
[`NB-DEC-010`](../decisions/0010-defer-local-installation-packaging.md): P5-03
is inactive and not passed until Martin requests it. This does not close Phase
5 or settle the application-root, migration, backup, or runtime-provenance
questions.
