# NeoBabylon status

## Public source checkpoint — 2026-10-04

Martin authorized public `SaintMagic/Project-NeoBabylon`, a reviewed initial
source commit and push after the current UI correction. The repository was
created and verified PUBLIC. The [publication boundary](../release/SOURCE_PUBLICATION.md)
excludes private app state, credentials, caches/builds and the two preserved
startup briefs containing personal instruction excerpts. The custom runtime's
patch/binary remain local; its digest is not public reconstruction material.
This is experimental product source, not an installable release or passed
product gate. First-party licensing, packaging and installation remain open/
deferred. Feature work remains paused at the checked popup correction.

The authorized restart retained the same popup-QA application/data root and
restored its saved chat. Fresh publication checks passed 162 UI tests,
TypeScript project checking and the focused host context/reasoning fixture;
they do not add live provider or broad acceptance qualification.

## Current implementation checkpoint — 2026-10-04

The remaining feature/UI source integration is implemented and reviewed. The
assembled development host executable is present under
`.local/App/Build/bin/NeoBabylon.Host/release/`; the app-private Node executable
matches `runtime/generated-tool-node-lock.json`. The reviewed App Server was
rebuilt and the product lock now points to the fresh versioned binary with
SHA-256 `a0c3ebdc8d1d9f5b56327f5ea6d1502ee5a0b50fa174d50743f8c7a17ff54386`.
The previous locked binary and manifest are retained.

New source includes retained candidate review bytes and bounded comparisons,
hash-linked activation/revocation/history, fixed named Host operations and UI,
and verified-backup recovery for Projects/ForkBookmarks. Unresolved Projects
startup state blocks execution and project writes while recovery remains
available. Registration and per-turn gates use the current App Server hash,
selected capability, effective policy, private Node pin, and fresh exact MCP
descriptors; unknown inventory does not authorize a turn.

Callable generated-tool activation remains deliberately closed:
`RouteAccepted=false` and the qualification allow-set is empty. The stdio MCP
route is a provisional implementation, not an accepted/qualified route. Review,
Prepare disabled, and Stage disabled do not grant callable access.

Martin requested feature/UI implementation before broad behavioral testing.
The later provider/conversation follow-up has focused fixture and native
evidence: the WPF host ran the locked App Server, OpenRouter Space Bunny
completed an ordinary `exec_command` round trip, right-click Rename confirmed,
and an explicit OpenRouter → NVIDIA GLM → OpenRouter switch retained the exact
chat ID, transcript and draft. The next OpenRouter reply recalled the previous
command's output from that history. NB-DEC-013 supersedes the former new-task
model-switch choice. Completion-cap options are progressively disclosed.

The supplied provider keys are stored only in application-private current-user
Windows DPAPI records. NVIDIA has an explicit app-private Responses-to-Chat
Completions adapter and three exact catalog-discovered selections. Deterministic
adapter/tool tests passed; live NVIDIA probes timed out and are not qualified.
Selection/resume with NVIDIA is not inference success. Generated-tool activation
remains closed. Broad acceptance, accessibility, performance, packaging,
containment and residual Phase 1B remain open/deferred as previously agreed.
See the [feature/source checkpoint](../history/2026-10-04-feature-ui-build-checkpoint.md)
and [provider/conversation checkpoint](../history/2026-10-04-provider-and-conversation-ui-checkpoint.md)
for the exact evidence, limits and changed-file record.

**Active model catalog update — 2026-10-04:** Martin replaced the NEX choice
with OpenRouter `inclusionai/ling-3.1-flash`. The authoritative Ling record
uses the observed free Novita route, advertised context 262,144 and completion
maximum 32,768; effective context, exact reasoning-effort mapping and live
tool qualification remain Unknown. Original NEX evidence and saved bindings
are retained, not rewritten or silently migrated. Other model choices and
the current conversation selection are unchanged. See the
[catalog replacement record](../history/2026-10-04-ling-model-catalog-replacement.md)
for implementation and verification scope.

**Context/metrics/reasoning follow-up — implemented, bounded checks passed 2026-10-04:** Martin
requested PiLOT-inspired context usage, compaction and live metrics in Task
details, plus reasoning selection in the composer. Fresh OpenRouter discovery
now establishes Space Bunny's mandatory Max/XHigh/High/Medium/Low control
(default Max), and Ling's optional On/Off control (default On). New versioned
records preserve the original records and saved-chat identities. Ling's
Boolean capability is Known; the pinned Codex Boolean-override path is not
implemented, and two bounded direct alias probes returned HTTP 429 without
qualifying effective reasoning. No native Ling effort list is invented.
The centered composer now exposes exact supported effort choices, while Ling's
Known On/Off control is disabled with the specific Boolean-transport limitation.
Task details includes Context, manual Compact context and source-attributed
token/cache/turn-average metrics; absent evidence remains Unknown. Saved chats
retain their exact capability snapshot until explicit same-thread adoption of
the current version. Focused Host checks, 162 UI tests, real TypeScript project
checking, development builds and isolated WPF/WebView2 rendered checks passed.
The rebuilt development app was reopened without resetting its Data. Provider
reasoning EFFECT, billing/throughput qualification and live manual compaction
are not passed. See the [observability follow-up](../history/2026-10-04-context-metrics-reasoning.md)
for the exact evidence, changed files and remaining transport limitation.

**Later popup correction and pause — 2026-10-04:** reasoning-menu viewport
overflow is corrected, including short-window/open-resize behavior. All 162
UI tests, 20 rendered regression cases and 11 native after-check geometry cases
passed; the actual development UI build is updated. Martin requested a pause
after this correction. Clearer throughput, visible/restored provider reasoning
and informative tool cards are recorded follow-ups, not implemented. The native
popup-QA window contains live activity and retained data under
`.local/Lab/Runs/Context-Reasoning-20261004-Popup/App`; it was not closed/reset
or migrated. No whole-window inference-free claim is made. See the
[paused checkpoint](../history/2026-10-04-popup-fix-paused-checkpoint.md).

## Historical verification snapshot — 2026-09-26

**Historical runtime source-only checkpoint — 2026-09-27 (unverified then):** the nested
runtime source now validates explicit and omitted completion caps against the
effective model before turn settings commit, forwards the review-model cap to
the one-shot request, clarifies function-form `apply_patch` secondary-environment
selection, and requires observed exit before confirming named command Stop.
No build, test, native run, or live request was performed for these changes.
At that checkpoint, the locked App Server binary was the older implementation; Task 4
source-to-launched identity and P2-11 native cap evidence remain open, as do
the full Phase 2 and Phase 4 gates. Guardian cap compatibility is a future
conditional gate only. See the dated source-only entry in
`../release/VERIFICATION.md`; prior machine evidence is not promoted to these
changes.

**Scope decision NB-DEC-010 — 2026-09-25:** local installation and packaging
(P5-03) are deferred until Martin requests them. This is not a passed gate and
does not resolve the separate data-root, migration, or runtime-provenance
questions.

**Requirement clarification NB-DEC-011 — 2026-09-25:** ordinary model-directed
tool output is displayed and forwarded without content redaction. An
unrestricted same-user command can therefore return sensitive user-accessible
data to the UI and provider; NeoBabylon does not promise that such data remains
confidential. Direct host credential serialization remains prohibited. P3-02
is still open for the per-operation denial, timeout/cancellation, and
partial-output evidence matrix.

**P3-02 exact command Stop slice — typed-failure retry extension passed at
bounded deterministic/native scope 2026-09-26:** the host exposes a separate,
identity-bound Stop command action while preserving Codex Stop-turn semantics.
The existing stable list/stop routes keep exact thread/turn/item/process
identity and App Server epoch host-owned, reject ambiguous or stale bindings,
and do not enable the global experimental API surface or expose process IDs to
React. New host coverage injects one typed App Server `failed` response only
after verifying the request targets the stored exact thread/item/process
tuple; the host exposes retry only while that same selected thread, binding,
and App Server epoch remain current. The controlled command stayed alive after
the typed failure, then a second request through the real pinned App Server
stopped it. The machine result is
`D:\CODING\NeoBabylon-Data\QA\P3-02-command-interruption-fca1366a31b64e0a857d0a9ec46ab2f5\result.json`.
The full ApprovalQA host harness exited 0, including this probe and the
delayed-list/item-completion race. UI tests passed **93/93**; typecheck and
production build passed (existing Astryx module-directive and bundle-size
advisories remain). Release and ApprovalQA WPF hosts built with zero warnings
or errors. A fresh native WPF/WebView2 fixture confirmed the visible,
keyboard-invokable command Stop, exact process exit, and absent scheduled
marker; its result and screenshots are under
`D:\CODING\NeoBabylon-Data\QA\P3-02-typed-failed-retry-native-approvalqa-20260926-01\P3-02-native-command-stop-1790373810728-74608\`.
Both test paths used pinned App Server `0.155.1` (SHA-256
`9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d`) and
deterministic loopback Responses fixtures; no live inference or fallback
occurred. Their application/Data roots were isolated outside the repository;
the ordinary Codex data root was unused. The broader P3-02 denial,
timeout/cancellation, and per-operation partial-output matrix remains open;
this is not overall P3-02 or Phase 3 acceptance.

**P3-02 late command-completion delivery — passed at bounded native scope
2026-09-26:** after an interrupted turn, the exact pinned App Server
`item/completed` notification is forwarded through the WPF bridge using the
original request ID and is correlated in React by exact thread, turn, item,
type, method, and source. The native activity card changed from running to
succeeded after the command's natural exit and removed its Stop action. The
same four-scenario run rechecked successful Stop, stale/duplicate Stop, and
transport-loss handling. Result:
`D:\CODING\NeoBabylon-Data\QA\P3-02-command-completion-native-green3-20260926-01\P3-02-native-command-stop-1790422749307-6972\result.json`.
It used pinned App Server `0.155.1`, revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d`, four
deterministic loopback Responses requests, and no live inference or fallback.
The app root and `Data` were isolated outside the source repository; ordinary
Codex-root use was false. No test-owned command or App Server process remained.
One intermediate retry showed an App Server EOF before its second fixture
request; the complete rerun passed without it, and the cause of that isolated
failure is unknown. P3-02's denial, timeout/cancellation, and broader
per-operation partial-output matrix remains open.

**P5-01 complete 47-path tracked-source diff review at scan time — 2026-09-25
(not current-diff coverage):** sealed Codex Security scan
`7fa01596-17fa-41e7-95d5-c382f9475d76` covered the complete tracked
working-tree diff at that scan snapshot in `NeoBabylon-Runtime` at pinned source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e` (snapshot
`codex-security-snapshot/v1:sha256:2c93216bbdb28fbf5c3ab09292ebb59674dea1b9eeb66139eff86b2e1d70d9a7`).
All 47 paths in that snapshot were reconciled: 33 workbench review items plus 14
additional test/fixture/lock paths. The sealed report contains zero findings
across provider-route, function-form patch, Windows guard, and fixture/lock-test
surfaces. This was source-only: no runtime binary was built or exercised, no
provider request was made, and the scan could not establish the effective
`NEOBABYLON_REQUIRE_CONTAINED_TOOLS` value at host launch. The untracked
captured executable under `NeoBabylon-Runtime/bin` was excluded, and no second
independent security reviewer was available. P5-01 remains open for runtime
provenance, function-form patch denial/approval/hook/error parity, and the
previously documented suite/environment gaps. This does not qualify
containment. The runtime checkout has since grown to 57 changed tracked paths
with the P3-02 Stop slice, so this sealed scan does not cover the current diff;
P5-01 remains open for a refreshed review. Exact scope and limitations are
recorded in `VERIFICATION.md`.

**P5-01 earlier read-only runtime diff review — 2026-09-25 (superseded for current diff coverage):** sealed Codex Security
scan `76f65285-6b43-4fa3-ac08-e7531bdf03aa` reviewed all 46 tracked paths in
the existing `NeoBabylon-Runtime` patch against upstream
`be2951ea34f0d295ed0becf97079f92fa5f6950e` (snapshot
`codex-security-snapshot/v1:sha256:cfe3307966624544ef95eedae8a8ea0550ef84c5298bf5403f614a4b8bbdb3b4`).
The workbench first exposed 32 items; the other 14 were reconciled to the Git
diff and manually reviewed. The sealed result has zero reportable findings.
This was source-only; the untracked captured App Server executable under
`NeoBabylon-Runtime/bin` was outside scope. Follow-up checks now pass for the
three `function_patch` unit tests, the positive function-form App Server
integration path (1/1), and the marker-enabled rejection of both guarded
Windows legacy entrypoints (1/1). HTTP/WebSocket route-pin inspection and
targeted CODEX Helper review closed the request-reconstruction question; the
Helper reviewed selected source excerpts, not the full checkout. The complete
deterministic host harness also exited 0 on rerun, including the new
corrupt/unsupported project-registry preservation check. Its first run had a
transient `goals_1.sqlite` cleanup lock that was absent on immediate read and
did not recur. P5-01 remains open: full independent checkout-level review and
function-form denial/approval/hook/error parity are outstanding, as are the
previously documented full-suite environment failures. No runtime source or
lock was changed. Exact commands and limits are in `VERIFICATION.md`.

**P5-01 Guardian OpenRouter route correction — 2026-09-25:** source review
found the separate Guardian V2 Responses constructor omitted the hard
OpenRouter provider pin. This was a conditional route-contract defect if
Guardian were enabled with OpenRouter; the current NeoBabylon catalog has
`model_messages: null`, so the path is inactive in the present host
configuration. The source now uses the same exact-selector,
`allow_fallbacks: false` constructor as the ordinary Responses path, and
leaves the OpenRouter field absent for non-OpenRouter providers. The updated
Guardian crate suite passed 89/89 under test-process-only
`RUST_MIN_STACK=16777216`; an initial default-stack full-crate run
stack-overflowed, whose cause is not established. Focused ordinary HTTP,
direct WebSocket serialization, request construction, cache-identity, and
Guardian transport-fallback checks also passed. Workspace formatting and
`git diff --check` passed. CODEX Helper found no remaining concrete routing
concern from the exact source excerpts, but did not review the full checkout.
This is request-side/source-test evidence only: no live provider was called,
no new App Server binary was built, and no runtime lock/hash was changed.
P5-01 remains open for a second independent security review, remaining
function-form patch parity, and runtime source/binary provenance. See
`VERIFICATION.md`.

**P5-02 focused registry evidence — 2026-09-25:** the isolated host harness
rejects malformed JSON and an unsupported registry schema while preserving
the exact original bytes. This is a narrow non-destructive parsing check, not
backup/restore, interrupted migration, storage/permission, downgrade, or
production data-root qualification.

NeoBabylon remains **unaccepted for full Phase 2**. The earlier narrow native
P2-01 through P2-12 fixture results, UI builds, and visual checks remain
recorded below and in `VERIFICATION.md`; they do not substitute for the final
gate. P2-10's Windows screen-reader evaluation and P2-13 remain open. The
latest deterministic Edge check passed nine interaction flows and measured
714 visible text/placeholder observations across eight visual states, with
zero contrast exceptions and zero unknown backgrounds. Latest fixture evidence:
`D:\CODING\NeoBabylon-Data\QA\P2-10-ui-accessibility-1790328762772-51444\`.
The immediately preceding run exposed 16 below-AA occurrences across 14 unique
disabled/`aria-disabled` findings; opacity-based text fading was removed while
unavailable-state styling was retained. This browser check does not qualify
Windows Narrator or native screen-reader behavior; both remain open. A fresh
native keyboard subset passed model selection, dialog navigation, history
search/pagination, exact task reopen, and project switching, but it is not the
complete nine-flow native suite. A separate native composer keyboard check
also passed Enter-to-send, focused Stop plus Enter, interrupted-state restore
after renderer reload without replay, and one deliberate Enter-to-continue
fixture request. It exercises P2-06 with a deterministic loopback model, not a
live provider; Windows screen-reader and broader native-flow qualification
remain open. Evidence is recorded in `VERIFICATION.md` under the P2-10 native
composer keyboard journey.

**P2-10 native WebView2 accessibility-tree smoke — 2026-09-26:** the fresh
Release WPF/WebView2 run passed its visual/appearance flow and captured the
native Chromium accessibility tree in the primary shell and Runtime
diagnostics dialog. It exposed the expected searchbox, New task and Open
diagnostics buttons, composer textbox, dialog, and Done button; while the modal
was open, the background searchbox was absent from the exposed tree. This
supports role/name and modal-inertness checks, but is not Windows Narrator or
screen-reader qualification and does not complete the nine native flows. The
exact App Server `0.155.1` revision `be2951ea34f0d295ed0becf97079f92fa5f6950e`
matched SHA-256
`9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d`; there
were no page issues, provider inference, or ordinary Codex-root use. The
application root and `Data` were isolated under the QA run directory. Result
and screenshots are under
`D:\CODING\NeoBabylon-Data\QA\P2-10-native-ax-20260926-02\P2-12-native-visual-1790374903952-36916\`.

**P2-01/P2-10 fresh native keyboard/history rerun — 2026-09-26:** the
WPF/WebView2 harness passed against the current locked App Server: all 56
synthetic saved tasks, 50+6 pagination, loaded-history search messaging,
exact oldest-task resume, transcript-overflow disclosure at the 120,000
character projection bound, project isolation, diagnostics-modal focus return,
and 13 keyboard-reachable capability-category disclosures. Reopening saved
tasks caused no additional Responses request; the 56 requests were only the
fixture setup. Release host and ApprovalQA test-runner builds passed with zero
warnings/errors; the native script and `node --check` passed, with no page
issues. Runtime SHA-256 matched the lock at
`9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d`; the
isolated application root was outside the source repository. Evidence:
`D:\CODING\NeoBabylon-Data\QA\P2-10-native-keyboard-20260926-03\run6\P2-P01-native-1790431728685-59336\`.
This remains a native keyboard/history subset, not the complete nine-flow
native acceptance or Windows Narrator/screen-reader qualification; P2-10 stays
open.

**P2-13 ApprovalQA host-suite refresh — 2026-09-26:** two consecutive runs
of the complete NeoBabylon host harness exited 0; the counted repeat reported
**86 passed, 0 failed**. It rechecked runtime-lock identity, isolated
application roots, capability/effective-state mapping, project/history
bindings, output/failure paths, interruptions, and command Stop. No SQLite or
cleanup diagnostic appeared in either run. These were deterministic fixture
tests with no live provider inference. This is the NeoBabylon host harness,
not the separate upstream App Server source suite; its 1,170 passed / 25 failed
/ 11 ignored result remains separately classified. This session emitted no
consolidated aggregate result file. P2-13 and full Phase 2 remain open.

**P2-01 transcript-overflow native recheck — 2026-09-25:** the Release WPF/
WebView2 host reopened the exact saved task after 56 loopback fixture turns.
The 120,576-character assistant response rendered as a bounded 119,983-
character saved-history preview with the explicit omitted-history notice;
the live notification separately reported 576 omitted characters from its
120,000-character event budget. Pagination remained 50 + 6, task selection
was exact, and provider requests stayed at 56 (no inference on reopen). No
page errors. Evidence and screenshot are under
`D:\CODING\NeoBabylon-Data\QA\p2-01-history-overflow-20260925\P2-P01-native-1790324644555-38088\`.

**P2-10 fresh native keyboard subset — 2026-09-25:** a newly built isolated
Release host passed native model-picker focus/listbox behavior, diagnostics
dialog inertness and Tab/Shift+Tab/Escape/focus restoration, loaded-history
search and pagination, exact older-task reopen, transcript-overflow disclosure,
and project switching. It seeded 56 deterministic loopback turns; the provider
request count stayed at 56 after native history navigation (no inference on
reopen). The 120,576-character saved response reopened as a 119,983-character
preview with an explicit omission notice. The App Server hash matched the
runtime lock, page issues were empty, and application `Data` was isolated
outside the source root. This used a synthetic LM Studio record: it is not a
real-provider test, the full nine-flow native suite, or Windows Narrator
coverage. Result and screenshot are under
`D:\CODING\NeoBabylon-Data\QA\P2-10-native-keyboard-20260925\fresh-run\P2-P01-native-1790328085694-46796\`.

**P2-13 latest host verification — 2026-09-25:** the ApprovalQA build passed
with zero warnings/errors; the exact saved-task recovery probe passed four
runs across the process-identity and cleanup changes; and the latest complete
apphost-launched suite passed 82/82 (exit 0), with no SQLite cleanup exception.
The recovery test now terminates only the PID/start-time exposed by its own QA
supervisor, asserts that the final owned process exited, and uses the existing
bounded cleanup retry restricted to its GUID-scoped test root. The earlier
SQLite lock owner was not captured, so treat this as a mitigation and clean
rerun, not proof that the underlying lock source is eliminated. The broader
pinned-source integration suite remains 1,170 passed / 25 failed / 11 ignored,
with helper/environment and Guardian baseline gaps recorded below. The runtime
depfile's alternate worktree was independently compared with the canonical
sibling: both have the same patch fingerprint and normalized content. A
no-remap rebuild initially differed from the locked binary. A corrected,
isolated remap rebuild used the checkout's Windows stack/static-CRT flags plus
path remapping and produced the same executable-section bytes as the locked
image. The `.text`, `.data`, `.pdata`, `.fptable`, and `.reloc` sections match
exactly. The only `.rdata` differences are seven generated Cargo `OUT_DIR`
hash strings, three PE debug-directory timestamps, and the 16-byte CodeView
PDB GUID; the PE COFF timestamp also differs. Normalizing only those identified
build/debug fields yields byte-identical 258,022,400-byte images, with
normalized SHA-256
`69044cc43babe5b8777299c0cbd2a1a7af9aebf8ddbac87e22c5fc0fe3d9cab8`.
The raw executable hashes still differ, so this is normalized content parity,
not raw-hash identity or proof of every historical build input. The runtime
lock remains unchanged. P2-13 remains open for its other Phase 2 evidence and
acceptance gaps.

**Phase 4 preflight — 2026-09-25:** candidate creation under application
`Data\GeneratedTools`, use under existing task permissions, and later manual
review are accepted outcomes; no candidate store, drawer, envelope, or
promotion code/tests exist. Candidate schema, evidence layout, and review
storage remain proposals. Under accepted unrestricted tool authority, these
records cannot be described as tamper-proof or as technically preventing a
model-controlled command from changing candidate data or performing writes
elsewhere. The smallest safe next slice is P4-01A, a specification-only
contract review; defer implementation until the P3 permission/source evidence
and the operational-versus-enforced meaning of manual promotion are clear.

**Phase 4 source-only update — 2026-09-27 (unverified candidate):** later
authorized bounded implementation now includes the unapproved candidate
envelope/store, a React drawer with inert contract and source-file inspection,
and named host review, rejection, and history operations. The review record is
bound to the current candidate content identity; it records a product-mediated
local interaction, not independently verified reviewer identity or activation.
A separate Core prepared-disabled binding/revocation/logical-cleanup store is
present, with source-level named host history/current-read and disabled
lifecycle operations and corresponding drawer controls. None exposes a
callable route or Activate action. Focused test code is present; its tests,
builds, restart, native, and live qualification remain deferred. The
2026-09-25 preflight above is historical, not current source inventory.
Task 8/9 acceptance, Task 7 route qualification, relevant P2/P3 evidence,
and Phase 4 remain open. Same-user unrestricted tools are not contained or
made tamper-proof.

**Phase 4 manual activation decision — 2026-09-27:** Martin accepted
[NB-DEC-012](../decisions/0012-explicit-phase4-activation.md): a candidate
can become callable only after a separate explicit NeoBabylon Activate
confirmation showing its exact currently reviewed identity and requested
permissions. Review and Prepare disabled grant no callable access. This
decides the manual act, not the provisional MCP route, activation code, or
Phase 4 acceptance. The final-current-lock deterministic and exact selected
real provider/model tuple gates remain open. There is no Activate UI today;
selected authority remains full Windows-user access with containment deferred.

**Task 7–9 source reconciliation — 2026-09-27 (unverified implementation
candidate):** The product-owned stdio adapter source advertises no tools and
denies calls. An explicit Core config opt-in writes only a disabled app-private
MCP stanza with an empty tool allow-list; the default config remains unchanged.
The named host `stageGeneratedToolDisabledMcp` operation checks the exact
current prepared binding, chooses the fixed application-root `Adapters`
executable, and keeps one process-local selection. It rejects an active
client/turn and sends the same opt-in through both isolated-config write paths.
Typed MCP status after thread start/resume/fork can report
`ConfirmedDisabledEmpty` only for disabled status plus empty inventory;
otherwise the status is `Unknown`, or an observed nonempty inventory stops the
client. The React drawer has review/reject/history, prepared-disabled lifecycle,
a separate Stage confirmation, and candidate-independent binding recovery for
exact-hash Revoke/Cleanup only. NB-DEC-012 still requires a separate explicit
Activate; none exists. No candidate tool is callable through this groundwork.
Source and focused test code have not been built, run, or qualified in native
or live conditions for this candidate. Task 8/9 acceptance and Task 7's final
current-lock deterministic registration/call/disable/revoke/reload/restart and
exact Stealth `stealth/space-bunny-alpha` live provider/route gates remain open.
No native-request cost bound follows from advertised free pricing. Same-user
unrestricted tools remain uncontained. Installation/packaging remain deferred
under NB-DEC-010.

**P2-11 native request-cap audit — 2026-09-25:** the pinned App Server
`ResponsesApiRequest` has no `max_output_tokens` field, and its WebSocket
conversion only serializes fields from that request. NeoBabylon's advertised
`maxCompletionTokens` metadata is not an effective native request ceiling.
Prior direct-OpenRouter cap tests do not establish a cap on the native WPF /
App-Server path. Native generation cost and exact upstream endpoint attestation
also remain unverified. P2-11 stays open; see the bounded source audit and
implementation proposal in `PHASE2_VERTICAL_SLICES.md`.

**P2-11 OpenRouter request/attestation feasibility update — 2026-09-26:**
current official OpenRouter docs expose `max_output_tokens` for Responses and
document an `X-Generation-Id` response header plus a generation lookup with
provider/model/usage/cost metadata. Pinned Codex source has no output-cap field;
its HTTP SSE path drops `X-Generation-Id` and its WebSocket path does not retain
per-response headers. This provides a concrete focused runtime-patch route to
test, not proof of effective native enforcement or provider attestation. The
selected hard-cap value remains pending Martin's input, and P2-11 remains open.

**P2-12 fresh native visual rerun — 2026-09-25:** three WPF/WebView2 launches
passed at desktop and compact sizes in dark and light themes, including
reload/restart persistence, native chrome, visible focus, notice dismissal,
and no page issues. It used the exact locked App Server `0.155.1` and an
isolated application root/Data directory; ordinary Codex-root use and provider
inference were false. The native screenshots and machine result are under
`D:\CODING\NeoBabylon-Data\QA\P2-10-native-contrast-20260925\P2-12-native-visual-1790318060968-27424\`.

**P2-11 live native slice — 2026-09-25:** the actual WPF/WebView2 host and
locked Codex App Server `0.155.1`
(`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`)
completed a no-tools turn with `nex-agi/nex-n2.5-pro:free`, variant
`nex-agi/nex-n2.5-pro-20260907:free`, configured route `nex-agi/fp8`. The UI
rendered 18,403 characters, completed without tool activity or page errors,
and showed provider context 262,144, Codex task context 249,036, advertised
reasoning levels, and effective reasoning `Unknown`. The isolated
application `Data` root was distinct from source and ordinary Codex roots;
2,028 application-root text files contained no credential-like key pattern.
The configured route is request-side evidence; independent provider-side
route attribution and the native request's cost were not available. A separate
direct NEX request reported zero cost, but does not establish this request's
cost. The native UI/App Server call exposed no explicit output-token cap, so
this does not qualify arbitrary-size behavior or a hard spend bound. The
response text was not retained. Result and screenshot are under
`D:\CODING\NeoBabylon-Data\QA\P2-11-native-live-nex-1790313675806-41500\`.

**P3-02 ordinary-tool partial-failure slice — 2026-09-25:** the isolated host
harness used the exact runtime-lock-verified App Server `0.155.1`
(`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`) and a
loopback Responses fixture. One permitted `exec_command` emitted its marker,
exited with code 23, and retained both output and code on the terminal item;
host diagnostics reported a typed `toolExecution` failure attributed to Codex
App Server with exact thread/turn/item identity. Exactly two fixture requests
occurred and there was no retry/fallback. The full isolated host harness
exited 0 with 82 passing checks. Build/test logs are under
`D:\CODING\NeoBabylon-Data\QA\P3-02-command-failure-host-build-20260925\`.
This is a deterministic test-host/loopback result, not live inference or a UI
rendering test; it closes only this one failure path, not P3-02's broader
denial, timeout/cancellation, and per-operation evidence matrix. Normal tool
output behavior is settled separately in NB-DEC-011.

**P3-02 native failed-command rendering slice — passed at bounded deterministic
scope 2026-09-25:** a freshly built Release WPF/WebView2 host and the exact
locked App Server used a loopback Responses fixture with synthetic model ID
`p3-02-native-command-failure-fixture`. One isolated permitted command emitted
`NEOBABYLON_P3_02_NATIVE_FAILURE_MARKER` and exited 23. The final host
diagnostic retained the same item ID, exact exit code, failed outcome, and
Codex App Server attribution as the source event; the native card displayed
the partial output, `FAILED`, and `Exit code 23`, including the accessible
label. Exactly two provider-fixture requests occurred; no retries, fallback,
live inference, or page errors. A test-owned synthetic credential-shaped
canary was also forwarded as ordinary tool output and visibly rendered; no
real credential or user data was used, and no host credential was injected.
UI tests passed 88/88, the production UI build
succeeded with existing Astryx/Lucide directive and bundle-size advisories,
and the isolated Release WPF build had 0 warnings/errors. Result and screenshot
are under
`D:\CODING\NeoBabylon-Data\QA\P3-03-tool-capabilities-20260925\P3-02-native-command-failure-1790346625113-13224\`.
This closes only one visible ordinary-command failure path. The synthetic
canary follows accepted NB-DEC-011; it is not a real credential or user-data
test. P3-02 and Phase 3 remain open for the broader per-operation failure
matrix.

**P3-03 read-only Tools & capabilities catalog — passed at bounded deterministic
native scope 2026-09-25:** the Runtime diagnostics drawer now shows all 13
versioned upstream capability categories, with NeoBabylon exposure separated
from qualification for the selected tuple and permission/evidence provenance
shown per entry. Exact tool qualifications come only from the authoritative
model records; generic `tool_use` does not promote an operation. Unknown,
unsupported, and blocked rows remain informational and have no call controls.
The typed host model record preserves qualification evidence through the
existing diagnostics bridge; no bridge operation or App Server/runtime patch
was added. Fresh verification: diagnostic UI **88/88**; ApprovalQA host suite
**83/83**; Release WPF host build **0 warnings/errors**; native WPF/WebView2
fixture passed a graceful restart/reopen with the same selected test tuple,
all categories, explicit Unknown/Blocked states, zero Responses requests, and
no page/console issues. Runtime identity was App Server 0.155.1, revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`; isolated
application `Data` was outside the source repository. Evidence and screenshots
are under
`D:\CODING\NeoBabylon-Data\QA\P3-03-tool-capabilities-20260925\P3-03-native-tool-catalog-1790344190968-24492\`.
This closes only the reviewed P3-03 catalog slice, not Phase 3 or Phase 2, and
does not qualify new provider behavior.

**Current-source upstream test run — 2026-09-26:** the serial App Server
integration suite was rerun after the latest command-stop additions and
finished **1,170 passed, 25 failed, 11 ignored** (exit 101; 1,515.74 seconds).
The first-run helper absences accounted for 18 failures: nine code-mode cases
and nine stdio-helper cases. Both helpers were then built in isolated QA
targets and the affected focused groups rerun successfully; all 18 are now
verified passing in focused runs, not in a green broad run. The seven
remaining broad failures classify as five shell/environment cases and two
Guardian hook assertions. Four of five shell/environment cases previously
passed with process-local `-NoProfile` PowerShell and Python aliases. The
remaining `thread_shell_command_honors_optional_timeout` case sees its marker
in a stream delta but not in the terminal failed command item; it exercises
human-only `thread/shellCommand`, not NeoBabylon's model-controlled
`exec_command` path. The same two Guardian hook assertions failed in a
focused clean-HEAD comparison (one other Guardian test passed), so this is
evidence they are not introduced by NeoBabylon changes, not a complete
clean-baseline suite. Exact commands, helper identities, focused results, and
remaining caveats are in `VERIFICATION.md`. The broad suite remains non-green;
P2-13 and Phase 2 remain open.

A focused clean-HEAD worktree comparison ran three Guardian tests: one passed
and the same two hook assertions failed as in the current-source checkout.
The clean-HEAD run required Cargo offline workspace-version normalization
because the committed lock lists local workspace crates at `0.0.0`; only
local workspace package version labels changed (304 diff lines, no external
dependency versions/checksums). This is a focused comparison, not a complete
clean-baseline suite. The remaining test/runtime source-link evidence and
failure details are recorded in `VERIFICATION.md`. The current isolated
candidate executable is SHA-256
`E4DD43F3EEA024CB8C6CA1401386B20CA02CFFB4D749FA6DC9BBB8CE1B704CA2`; its
depfile points into `NeoBabylon-Runtime`. It is not accepted. The canonical
runtime lock and pinned binary remain unchanged at SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`, whose
depfile points to a separate `NBRT-RouteControl` checkout; its source
association remains unproven. Phase 1B residual checks remain
deferred; P2-10, the remaining P2-11 size/cost limits, and P2-13 remain open.

**P2-09, “Resolve file-change approval without a mystery diff,” passed at a
bounded deterministic native scope on 2026-09-24.** Two isolated WPF/WebView2
sessions used the exact pinned App Server and a loopback Responses fixture:
one rendered and accepted the exact previewed patch, and one rendered and
denied it without changing the fixture file. Host-issued approval-instance
and request identity plus a fingerprint bind the one-shot choice; changed or
malformed post-preview data fails closed. No real model or live inference was
used. Upstream re-reads files while executing the accepted patch, so edits by
another actor while approval is pending can still change the outcome; no
transactional workspace guarantee is claimed. See the dated evidence in
`VERIFICATION.md`.

**P2-01, “Find and reopen an older task,” completed as a narrow native slice
on 2026-09-24.** A WPF/WebView2 run paged through 56 pinned-App-Server tasks
(50 + 6), searched honestly across loaded pages, reopened the oldest saved
task with its exact LM Studio/model attribution, reached cursor exhaustion,
and switched to a clean empty project. Fixture request count remained at 56,
so history browsing/resume caused no provider inference. A 2026-09-25 native
overflow follow-up additionally verified the 120,000-character transcript
budget, retained-source metadata, and visible omission notice on the saved
history path. The current diagnostic
UI suite passed 59/59; typecheck, Vite production build, isolated Release WPF
build, focused interruption probe, and a subsequent full host harness passed.
One immediately preceding full-harness attempt reported a transient pinned
App Server process-survival assertion; that PID was gone on later inspection,
the focused two-attempt probe passed, and the subsequent full run exited 0.
Treat that timing-sensitive observation as unresolved rather than silently
calling it fixed. P2-01 does not close full Phase 2; broader history
scale/concurrency and server-backed full-history search are not qualified.
See the dated evidence in `VERIFICATION.md`.

**P2-02, “Switch model, then return to an exact saved task,” passed as a narrow
native slice on 2026-09-24.** Two test-only model records used loopback-only
Responses fixtures; one task per record survived host restart. Changing only
Model B's synthetic effective context invalidated execution without hiding
its transcript or changing its saved binding. New task remained available,
and the unchanged Model A task reopened under its exact original binding. No
live provider/model inference occurred. The native test caught and verified a
fix for a UI race: model switching now remains busy until the selected model's
saved-history refresh finishes. The separate full-host-runner cleanup failure
and remaining Phase 1B/Phase 2 gaps are recorded in `VERIFICATION.md`.

**P2-03, “Submit, reload, and recover the exact pending send,” passed as a
narrow native slice on 2026-09-24.** With a loopback-only mock Responses
fixture, an unacknowledged send remained in its scoped draft through a real
WebView2 renderer reload and was submitted once only after a deliberate retry.
An acknowledged prompt was journaled once and its pending response was not
replayed after a real host restart; the UI showed the interrupted outcome.
The native run also exposed and verified a fix for a race where a thread
refresh was still active after the UI became sendable. No live provider
inference occurred. This does not qualify general crash/concurrency or
storage-failure recovery, nor close Phase 1B or Phase 2. See the dated evidence
in `VERIFICATION.md`.

**P2-04, “Recover a draft when local storage fails,” passed as a bounded
native fault-injection slice on 2026-09-24.** A rejected draft write remained
visible with a warning until WPF restart, but could not be recovered after
restart because it had never persisted. When both local clear and its empty-
value fallback failed after an accepted send, the prompt remained visible
exactly once in saved history alongside the stale draft and a do-not-resend
warning; no request was replayed. With all Web Storage methods unavailable,
saved App Server history remained inspectable but new unsaved text was
necessarily lost at restart. Since the pending-send marker was unreadable too,
the conservative unresolved-pending warning was also shown. Four explicitly
authored requests reached only a loopback fixture; there was no live
model/provider inference or unsolicited request. This confirms safe, honest
failure behavior, not draft durability when storage is unavailable. No
production source changed for P2-04; Phase 1B and the full Phase 2 gate remain
open. See the dated evidence in `VERIFICATION.md`.

**P2-05, “Keep projects distinct during active and interrupted work,” passed
as a bounded host/native slice on 2026-09-24.** The focused host fixture
rejected a project switch and history request during an active Alpha turn,
rejected a competing turn during an Alpha history scan, kept Alpha/Beta task
history bound to exact folders, and rejected cross-project resume. The native
WPF/WebView2 run delayed Beta history refresh and confirmed the UI stayed
busy; after an accepted Beta turn was interrupted by graceful host restart,
the exact Beta project/task returned with a visible no-replay warning and no
additional provider request. A host-bridge attempt to resume Alpha under
selected Beta failed on exact workspace identity without changing the
selected task. Deliberate Beta and Alpha continuations each made one request;
the four authored prompts yielded four loopback fixture requests and one
journal entry apiece. Runtime version/hash matched the lock. The LM Studio
fixture was synthetic and loopback-only, with no live inference. No
production source change was needed. Broad concurrent stress, crash recovery,
live-provider behavior, Phase 1B, and full Phase 2 remain open. See the dated
evidence in `VERIFICATION.md`.

**P2-06, “Stop or lose a live turn, then continue safely,” passed its
deterministic/native portion on 2026-09-24.** Pinned-runtime protocol tests
preserved a partial assistant delta before both user interruption and fake
App Server child exit; cancellation was terminal `interrupted`, while child
exit remained an unconfirmed outcome with typed Codex App Server attribution.
In native WPF/WebView2, a synthetic loopback Responses stream rendered partial
text, stopped on user action, settled the bubble, and showed the attributed
interruption. Renderer reload restored the persisted interrupted status and
no-replay warning with no extra request; a separately authored continuation
made exactly one new request. The two prompts appeared once apiece in the
isolated App Server journal, and graceful WPF shutdown cleaned up its pinned
App Server child. Runtime `0.155.1` and SHA-256 matched the lock; the native
diagnostics confirmed application `Data` isolation and no ordinary Codex root.

The deterministic test does not qualify a real provider/model. A follow-up
native WPF/WebView2 run on 2026-09-24 used the user-supplied credential via a
masked prompt and tested the exact NEX free route. Real streamed text appeared;
Stop produced the attributed interrupted state, reload restored it without
replaying the prompt (journal count 1), and the owned host exited cleanly with
no new pinned App Server process. Runtime-lock identity/hash, exact model/route
configuration, isolated application `Data` root, and no ordinary Codex root
were verified. The partial assistant text did not survive renderer reload, so
durable partial-text restoration remains open. The earlier missing-key message
was a test-process handoff error, not a lack of user authorization. Phase 1B
and the full Phase 2 gate remain open. See the dated evidence in
`VERIFICATION.md`.

**P2-07, “Review a supported file change end to end,” passed as a bounded
native deterministic slice on 2026-09-24.** The exact pinned App Server runtime
returned an attributed patch diff during a WPF/WebView2 turn; the read-only
review remained available after renderer reload and graceful host restart.
The loopback Responses fixture received exactly two requests for one tool
round trip. Application `Data` stayed under an application root outside the
source repository, and diagnostics confirmed the ordinary Codex root was not
used. The capability was synthetic and the endpoint loopback-only; no live
provider request or API key was needed. The drawer shows an absolute saved
path, which can cause horizontal scrolling; this is a presentation follow-up,
not a review-integrity failure. Phase 1B and full Phase 2 remain open. See the
dated evidence in `VERIFICATION.md`.

**P2-08, “Exercise real command and permission approval requests,” passed at a
bounded deterministic native scope on 2026-09-24.** Three fresh WPF/WebView2
application roots used the pinned App Server and a loopback-only Responses
fixture. The native UI accepted one advertised, one-shot approval for the exact
read-only Windows `ver` command and showed its exit-0 output; explicit denial
and a five-second no-choice timeout were resolved fail-closed without running
the command. Each scenario completed its two-request fixture round trip, and
the result record confirms isolated application `Data`, no ordinary Codex root,
and App Server child exit. Runtime 0.155.1, source revision, and binary hash
matched `runtime/runtime-lock.json`. The test used only synthetic capability
metadata and made no live provider request or inference, so it does not qualify
live-provider approval generation. Its explicit QA read-only/on-request policy
does not change the accepted unrestricted production policy or qualify Windows
containment. Phase 1B and full Phase 2 remain open; see `VERIFICATION.md`.

**P2-09 file-change approval preview and binding passed as a bounded native
deterministic slice on 2026-09-24.** A positive request displayed the exact
App-Server-projected path, change kind, and diff in WPF and applied only the
expected isolated fixture patch after a one-shot accept. A second exact
preview was explicitly denied and left its file unchanged. Approval is bound
to the host-issued instance, exact request identity, and schema-versioned
fingerprint; changed/malformed post-start snapshots, stale identity, and
mismatched fingerprints are rejected by host regressions. The UI rendered a
synthetic markup payload as inert text. The exact `0.155.1` runtime/hash,
separate per-run application roots and `Data`, unused ordinary Codex root, and
child-process exit were verified. All five command/file-change scenarios
used a loopback-only synthetic capability and ten total fixture Responses
requests; no model weights, credential, live provider request, or inference
were involved. This does not close the upstream concurrent-file-edit race,
full approval matrix, Windows containment, Phase 1B, or full Phase 2. See
`VERIFICATION.md`.

The complete NeoBabylon host harness was rerun after a transient SQLite
cleanup lock and exited 0; the prior cleanup failure did not reproduce and
its cause is unresolved. The current public NEX free-route preflight still
reports one zero-priced endpoint but raw endpoint `status=-2`, whose semantics
are undocumented. No live request was sent and no credential or route was
substituted at that preflight checkpoint; the later bounded live attempt is
recorded below.

The offline NEX credential-exclusion probe was corrected to match the accepted
unrestricted authority across Codex config, `thread/start`, and `turn/start`.
It confirmed the synthetic credential absent from the ordinary child and the
isolated source/data artifacts, with the exact NEX model still selected. The
loopback-only mock made no provider request or inference. A separate native
WPF/WebView2 launch with a synthetic startup canary found none in rendered
content, browser storage, bridge messages, renderer requests, or the isolated
application root. This closes the planned synthetic-canary path only; it is not
a claim about arbitrary credentials or untested providers. See the current
dated verification entry.

After that preflight checkpoint, one bounded test used the user-supplied
free-only credential through a hidden one-shot prompt and completed the exact
NEX free-model function-form patch round trip on the current locked App Server.
The isolated file change and saved review passed; OpenRouter reported two
zero-cost Nex AGI generations. Its usage counters remain inconsistent with
the Codex journal, endpoint status semantics remain Unknown, and no paid route
or alternate model was tried. This does not close the Phase 1B gate.

A fresh unauthenticated public-route preflight at `2026-09-24T04:32:31Z` again
matched the exact NEX free tuple, one `nex-agi/fp8` endpoint, and zero prompt/
completion prices; model expiration is `2026-09-25`, while raw endpoint
`status=-2` semantics remain Unknown. The unrestricted local mock tool path
passed. This process had no `OPENROUTER_API_KEY`, so no live repeat was sent.

As of 2026-09-23, **Phase 0 is complete; Martin deferred the Windows
containment gate and authorized Phase 2 work with explicitly unrestricted
model-controlled tools. Other unfinished Phase 1B checks are deferred until
needed or requested, not passed. Phase 1B has not
passed its full acceptance gate. LM Studio now has two successful narrow live
tool runs plus two earlier ten-minute non-completions; OpenRouter has a
separate successful live tool run. Those successful tool runs predate the
current fail-closed Windows legacy-sandbox guard. A later isolated deterministic
Responses run completed a real ordinary command under `danger-full-access` /
`never`, then verified resume and fork authority without live inference. The
exact request-level OpenRouter route pin was
also exercised through the visible WPF host. The first Codex-first React/WPF
shell slice now supports saved-thread restore and a history-preserving new
task. One graceful WPF/App Server restart and WebView renderer reload followed
by same-thread resume now pass on the isolated LM Studio root. A bookmarked
fork was revalidated after restart and completed one text-only continuation;
the new branch is visible and attributed in the sidebar. Repeatability,
effective budget/compaction, authority, containment, crash/recovery cases, and
the broader Phase 2 client remain open. Basic saved-history search by
preview/provider/model is implemented and WPF-smoke-tested without inference.
Bounded inline approvals have named host handling and rendered synthetic-bridge
coverage. P2-08 now exercises actual pinned-App-Server command approvals in a
native host using a deterministic local Responses fixture; live-provider
approval generation and the full authority matrix remain unqualified. The
subsequent bounded P2-09 file-change preview/decision binding result is recorded
above; neither it nor P2-08 qualifies containment or the full approval matrix.**

The Phase 2 local-project slice now lets the WPF host choose an existing folder
through a native picker, persist a named project list under isolated application
`Data`, and scope saved-thread listing and App Server cwd to the selected
project. The React shell reflects the selected project and blocks switching
while a draft is unsent or an operation is active. An unavailable selected
folder is shown as unavailable and cannot silently fall back to the fixture.
That initial workspace-selection test alone did not qualify multi-project
history recovery or the full Phase 2 gate. No live provider inference was used.

A subsequent isolated two-project qualification used the pinned App Server and
the rendered WPF shell with a loopback Responses fixture. Alpha and Beta kept
distinct task IDs and selected-folder histories; attempting to resume Alpha
while Beta was selected was rejected. Both tasks continued under their exact
folders after switching, and a real WPF host restart restored the selected
Alpha task before a separately authored Beta continuation. The fixture saw
three requests for three WPF-authored prompts, with no replay. This qualifies
one bounded mock-provider cross-project path, not live-provider, larger
project-set, concurrent, or full Phase 2 recovery.

An unsent composer draft now lives in the isolated WebView2 profile, keyed by
selected project and task, rather than in App Server conversation history.
The new-task draft survived a real WPF renderer reload, one graceful host
restart, and one abrupt QA-host termination/relaunch; clearing it stayed
cleared. A synthetic bridge verified that an acknowledged turn clears its
draft while a rejected turn start retains it in the created task's scope.
That rejected draft also reappeared only on reopening its saved task in the
synthetic bridge, including after renderer reload. A later real WPF/App Server
saved-task check confirmed an unsent Beta draft stayed with that task across
New task, reopen, renderer reload, and host restart, then stayed cleared after
explicit clearing; no turn was sent. Native WPF/real-App-Server active-turn
crash recovery, total-storage-failure recovery across restart, and
live-provider send/interruption remain unqualified.
Draft text is local plaintext application data, not a credential vault.

A fresh 2026-09-24 Edge/Vite synthetic-bridge check confirms the narrow
acceptance boundary: the draft persisted before submit, survived a `turn/started`
event for the same request but a different thread, then cleared only after the
matching thread's start event while the turn request was still open. The
composer cleared, and browser console errors/warnings were empty. At 760px the
desktop layout had no horizontal overflow; at 390px it expands to the explicit
640px CSS minimum and is not a phone-width layout. This renderer/fake-host
evidence does not qualify native WPF, provider inference, or crash/restart
recovery.

A further rendered synthetic-bridge check injected Web Storage failures during
the accepted-turn clear. When removal failed but an empty replacement could be
saved, the draft stayed logically cleared through New task. When both remove
and replacement writes failed, the composer cleared only in memory and showed
the explicit “check history before resending” warning while preserving the
stored text. This does not establish recovery after restart under total storage
failure.

A fresh Edge/Vite synthetic-bridge check covers one pending-send
renderer-restart boundary. When App Server history contains the exact prompt
as an interrupted turn, resume restores it once, clears the matching stale
draft, and issues no replay; New task opens with a blank composer. When no turn
was accepted, the pinned App Server integration check shows the empty thread is
omitted from saved history and `thread/read` after host restart fails with
`thread not loaded`. The UI attempts exact resume, surfaces that failure, does
not replay, and keeps the original new-task draft visible for a deliberate
retry. Both rendered cases passed at 1440×900 with no page or console errors or
warnings. The UI scenarios use synthetic bridge state; the empty-thread
behavior was separately confirmed against the pinned App Server without
provider inference. This does not prove native WPF/WebView2 restart or broad
recovery. Screenshots are retained outside the repository under
`D:\CODING\NeoBabylon-Data\Phase2-EmptyThreadFallback-20260924-final\`.

A bounded active-task reconnect slice now stores only an exact project,
provider, model, and thread navigation hint in the isolated WebView2 profile.
At startup, the React shell checks that hint against current selection and
App Server saved history before requesting `thread/resume`; a mismatch stays
closed with a visible warning, and no turn is replayed. Synthetic rendered
checks pass for exact restoration, model mismatch, missing history, and
clearing the hint with New task. The first isolated WPF QA root had no saved
turns; an empty App Server thread was not listed as saved history. A separate
isolated LM Studio qualification root did contain two real saved threads:
the current-build WPF host opened one, reconstructed six visible messages
after renderer reload, and reopened the same active task after an abrupt
QA-host termination/relaunch. That proves one saved-task reconnect path without
new inference. A separate deterministic paused-Responses test subsequently
terminated the pinned App Server during its first provider request. The stored
turn was `interrupted` before and after resume, with exactly one provider
request and no replay. The host now projects that outcome even when the turn
has no displayable items; the React shell shows an interrupted badge and
non-replay warning. A rendered synthetic bridge passed this UI path. A later
isolated **full WPF-host termination** while the loopback Responses request
was pending restored the same saved task as interrupted, made no replayed
provider request, and completed one separate deterministic mock follow-up.
This qualifies that bounded mock-provider crash/restart path, not a live-model
interruption or cross-project recovery. A separate between-turn App Server
child-exit probe passed with WPF remaining open: a new user prompt resumed
the same saved task after exact identity/authority checks, with one mock
request per prompt and no replay. A further in-flight child-exit probe kept
WPF open, showed a typed stream-closure failure without a successful assistant
message, made no replay request, and completed a separately authored follow-up
in the same task. The prior interruption remains visible inline from saved
App Server status after renderer reload and after a later successful turn.
These are mock-provider cases, not live-model interruption qualification.

A follow-up recovery regression preserved the selected task across a failed
replacement App Server initialization, rejected a same-provider/model record
with a different endpoint, and prevented saved-history listing from overlapping
an active turn. A rendered WPF child-exit probe now distinguishes a host stream
loss (**unknown outcome**) from the persisted App Server status
(**interrupted** after reload). The details badge and inline message agree;
neither view replays the turn. These are isolated mock-provider observations,
not live-provider or complete concurrent-recovery qualification.

Saved-history pagination now carries separate scan-and-repair and state-DB
cursors in a project/provider/model-bound host token. The React sidebar can
load older pages, preserve previously loaded rows, and deduplicate overlap;
an active-task hint may reconnect directly when its saved thread lies beyond
the first page, with exact identity checked by the host before resume. The
pinned App Server advanced both upstream cursors in an isolated two-thread
fixture; rendered synthetic checks passed pagination and older-thread
reconnect without turn replay. A real multi-page WPF history and cross-page
ordering remain unqualified, so the combined Phase 2 gate remains open.

The rendered shell now coalesces frequent assistant text deltas before React
updates the transcript. A synthetic 5-million-character assistant reply sent
as 2,500 deltas plus 2 million characters of tool output completed in 7.4
seconds, retaining the full displayed text and a scrollable tool output area;
the same case previously timed out after 60 seconds. Partial streamed text
remained visible after a simulated provider failure. This is browser-rendered
stress evidence, not a live-provider, App Server, memory-bound, or arbitrary-size
qualification. The combined Phase 2 gate remains open.

The P2-10 keyboard/accessibility slice progressed beyond the earlier
diagnostics-only check on 2026-09-24. The Edge fixture covers keyboard model
selection, project/task navigation, loaded-history search and resume,
send/stop, failure attribution, approval denial, modal inertness, named
scroll-region Tab stops, focus wrapping, Escape, and focus return. A fresh
deterministic Edge run passed all nine scripted flows with no console errors
or external requests; screenshots were inspected at 1440x900 dark and
1024x768 light. Separately, the native P2-12 smoke test confirmed visible
focus after Tab; it did not rerun the nine flows natively. No Windows screen
reader was exercised. The earlier eight-state rendered-text/placeholder audit
measured 713 observations; the fresh follow-up recorded above measured 714.
Both have zero active/inactive contrast exceptions or unknown backgrounds. A deliberately failing regression run first exposed 16
low-contrast occurrences across 14 disabled-control findings; opacity-based
text fading was removed while unavailable-state backgrounds/cursors were
retained. The nine flows remain synthetic Edge, not native WPF. Windows
screen-reader qualification remains open, so P2-10 is not accepted.

The bounded native/deterministic portion of P2-11 passed on 2026-09-24 and was
repeated in a fresh isolated Release-host run on 2026-09-25 against the exact
locked App Server runtime, using a loopback Responses fixture rather than a
live model. The native harness exercised 1.1 million assistant characters,
paged saved tool output, a captured `exec_command` round trip, renderer reload,
cancellation, and typed partial-output failure. It records sampled process
memory and explicitly discloses that the pinned App Server does not persist
cancelled streamed deltas. The fresh rerun evidence is under
`D:\CODING\NeoBabylon-Data\QA\P2-11-native-output-release-rerun-20260925\P2-P211-native-1790302010892-70740\`.
A separate direct OpenRouter Responses
qualification on 2026-09-25 completed 19,233 streamed characters with the exact
NEX free alias and reported zero cost; its generation lookup returned 404 and
it did not use App Server/WPF. A later native WPF/App-Server turn rendered
18,403 live NEX characters with zero tool activity. That proves one live
rendering path only: native-request cost and independent route attestation were
not captured, and the UI/App Server exposes no explicit output-token cap.
Arbitrary-size and hard-spend limits remain open; P2-11 and the combined Phase
2 gate are not closed. See the dated P2-11 entries in `VERIFICATION.md`, the
capability record, and the native `result.json` under
`D:\CODING\NeoBabylon-Data\QA\P2-11\P2-P211-native-1790283016868-62532\`.
A later Pi-key repeat completed 18,704 streamed characters / 3,877 output
tokens at provider-reported zero cost. It also lacked post-hoc route metadata
and its generation lookup returned 404; it was direct Responses API evidence,
not a native App Server/WPF run. The Pi key was used only in the isolated test
process and neither the secret nor response text was persisted. See the dated
repeat entry and `liveLargeOutputRepeat` in the authoritative capability
record.

Pinned `0.155.1` source has stable `turn/diff/updated` and
`item/fileChange/patchUpdated` notifications, but
`item/fileChange/requestApproval` does not carry the proposed diff. The current
NeoBabylon local-model catalog also selects `apply_patch_tool_type: null`;
the source's turn-diff tracker is updated by the patch tool path, not by every
shell write. Therefore a generic reviewable file-change approval cannot be
claimed from the current event stream. File-change requests remain deny-only
pending a tested, trustworthy diff/authority design; no policy was loosened.

A first read-only review drawer now renders an exact thread/turn-matched
`turn/diff/updated` aggregate after App Server emits one. Later aggregate
updates replace earlier ones; empty invalidation and oversized diffs show
explicitly unavailable states, never a partial diff passed off as complete.
The rendered Edge synthetic-bridge test covers these cases and keyboard
return focus at desktop and compact widths. A bounded host projection now
exposes saved `fileChange` item diffs from App Server turn history on exact
task resume/fork; malformed or oversized items are marked unavailable. A
synthetic reopen renders one saved patch diff, while an actual WPF/App Server
reopen of a task without patch items shows no invented review. A newer
test-only catalog (`apply_patch_tool_type: freeform`) and deterministic mock
Responses fixture drove the exact pinned App Server through one actual patch:
it changed an isolated tracked file, emitted three `turn/diff/updated` events,
and returned a saved patch-item review for the matching thread/turn. This
establishes the positive protocol path. The same saved task was then reopened
in the native WPF/WebView2 host: its attributed patch review rendered after
initial load, renderer reload, and a full QA-host restart, with no provider
inference or console/page errors. This does not establish that the real
local-model capability record (still `null`) supports patch tools or that
shell writes are covered. File-change approval remains deny-only.

A subsequent bounded live OpenRouter/NEX probe exposed the remaining gap:
the test-only catalog advertised `apply_patch` to the deterministic fixture,
but the real NEX turn answered that this tool was unavailable. The turn
completed with no tool item or diff event, and `tracked.txt` remained
unchanged. Public route preflight matched the selected unique free Nex AGI
endpoint. Whether the Responses gateway dropped the custom/freeform tool or
the model failed to recognize it is not yet established. This is a failed
patch-capability qualification, not a failed ordinary-tool qualification or
permission denial; no production metadata was promoted and no fallback was
used.

The previously supplied blue-body/white-title screenshot was captured before
the current neutral-dark update. A current WPF capture and DWM dark-mode
attribute show a charcoal title bar; live WebView2 computed backgrounds are
gray/black on the main surfaces. Light-to-dark switching also passed. No
additional palette change was required for that screenshot report.

The Windows containment gate has a concrete reproduced failure: the legacy
`workspace-write` / `unelevated` backend allowed a model-controlled command to
delete a sibling canary outside the workspace. The current NeoBabylon runtime
now **fails closed** at both legacy command entrypoints when launched by the
product. A rebuilt, re-pinned App Server rejected the same isolated mock
parent-delete probe, returned a tool-failure result, and preserved the canary.
This does not repair or qualify the legacy backend: model-directed command
tools remain unavailable on that sandbox path until a genuinely contained
replacement is qualified. The desktop host now selects Codex's separate
unrestricted path and labels it as uncontained. A separately tested native
MXC/PSEC candidate denied direct outside
and `.git` deletion but allowed a workspace hard link to overwrite an outside
file. It was not connected to App Server; its experimental routing was removed
and the fail-closed guard remains active. The separate-account elevated backend
was tested with isolated homes and let a real sandboxed child change outside
canaries through both pre-existing and child-created hard links and junctions.
This backend remains
unqualified and is not selected by NeoBabylon. The `WRITE_RESTRICTED` token
explanation, rejected token-only
experiment, and remaining tests are in [NB-DEC-007](../decisions/0007-windows-legacy-containment-gate.md)
and [VERIFICATION.md](../release/VERIFICATION.md).

## Current Phase 2 tool authority

The WPF host requests Codex `danger-full-access` with `approvalPolicy=never`
in its isolated configuration and explicit start/resume/fork/turn protocol
requests. It checks the App Server's effective thread response before binding
the thread. The UI says “Full access · no containment” and warns about
outside-workspace files and network access. The notice can be dismissed for
one UI session or hidden across app launches with “Don’t show again”; the
policy chip and task details remain visible either way. The welcome screen's
large decorative orbit rings have been removed. This is a user-authorized policy
change, **not** a containment repair; the failed Windows backends remain
unqualified. [NB-DEC-008](../decisions/0008-phase2-unrestricted-tool-authority.md)
owns the decision and [VERIFICATION.md](../release/VERIFICATION.md) owns the
test evidence. A deterministic pinned App Server tool round trip passes under
this policy. A retained credentialed live NEX/OpenRouter run also completed
one function-form `apply_patch` turn on the exact locked runtime, with a
matching tool output and saved review. That is limited to this model, route,
and tool; it does not qualify general live tool behavior or close Phase 2.
Fresh native WPF startup/render/reload/restart, exact runtime identity, and
saved-patch-review rendering are now verified against the exact function-patch
runtime; the full Phase 2 gate remains open.

The dark-first shell now uses neutral charcoal/gray rather than blue-tinted
surfaces and text. The WPF native caption uses the same charcoal as the React
top bar and follows the persisted light/dark UI choice through a validated
named host operation. This is a visual/native-host change; no runtime tool
policy or containment claim changed. The sidebar now uses the existing
NeoBabylon tower SVG instead of its former CSS-drawn overlapping-circle mark;
the native WebView verifies the loaded decorative image at 29×29.

The 2026-09-24 offline OpenRouter mock and native WPF/WebView2 canary checks
cover the planned synthetic credential surfaces for the exact NEX record and
accepted unrestricted policy. The ordinary child reported the key absent; the
isolated config excluded it and disabled login/profile loading; source, Data,
workspace, WebView profile, renderer DOM/storage/bridge, and renderer request
surfaces contained no canary. The first offline probe also exposed that the
user's PowerShell profile was still being loaded: `use_profile = false` was
not the pinned config key, while `allow_login_shell` remained at its `true`
default. The isolated config now sets `allow_login_shell = false` and
`experimental_use_profile = false` for both initial providers. The LM Studio
profile settings have a passing config-generation check but no separate tool-
child observation. This evidence uses a synthetic marker and does not claim
that arbitrary real credentials or every provider implementation were tested.

## Completed and verified

- Selected workspace: `D:\CODING\NeoBabylon`; no destination change was
  needed.
- Phase 0 documentation, reference corpus, upstream inspection, generated
  protocol outputs, decision reconciliation, and evidence records are present.
- Local sibling runtime: `D:\CODING\NeoBabylon-Runtime`, preserved upstream
  Codex ancestry at `rust-v0.155.1` /
  `be2951ea34f0d295ed0becf97079f92fa5f6950e`.
- The active product lock verifies App Server `0.155.1`, source revision
  `be2951ea34f0d295ed0becf97079f92fa5f6950e`, binary SHA-256
  `9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d`, and
  source-patch fingerprint
  `ddbcd19e0c9424147174ad36bc4ae15144e0e4fcce6d0db9c7f01e7a36db053d`.
  The active binary was freshly built from the canonical `NeoBabylon-Runtime`
  sibling; its depfile and tracked-patch fingerprint match that checkout. The
  separate `NBRT-RouteControl` worktree is only a comparison copy. Its tracked
  patch fingerprint and normalized content match, though raw line endings
  differ in 28 files.
- `NeoBabylon-Runtime` currently has 57 tracked changes and one untracked
  captured upstream binary under `bin/`. The mixed patch includes OpenRouter
  routing, function-form `apply_patch`, a conditional Windows legacy-sandbox
  guard, identity-bound command list/stop routes, and related dependency/test
  changes. The 2026-09-26 broad App Server source suite rerun after these
  additions remains at 1,170 passed / 25 failed / 11 ignored; focused helper
  retries now pass all 18 helper-dependent cases. The older `636f...` binary and `a0a7...` patch
  fingerprint describe a historical lock/build comparison, not the active
  product lock; its investigation remains in `VERIFICATION.md`.
- Thin WPF/.NET + WebView2 diagnostic host builds with cached
  `Microsoft.Web.WebView2 1.0.4191.47`; no system-wide dependency was
  installed.
- The host uses named product operations, isolated application-root `Data`, a
  single-owner runtime-data lease, fail-closed authority responses, and typed
  tool/turn diagnostics. It does not expose generic RPC or
  `thread/shellCommand` to the model.
- Unexpected App Server output closure during a turn now returns an incomplete
  observation with partial notifications retained; an interprocess regression
  test covers exit before the terminal event. Host source maps it to a typed
  `appServerTurn` failure.
- LM Studio was inspected live: server `0.4.25.0`, endpoint
  `http://127.0.0.1:1234/v1`, model `phase1a-qwen3-14b`, variant
  `qwen/qwen3-14b@q4_k_m`, effective context `32768`, tool capability
  `tool_use`; reasoning and structured output remain Unknown.
- Before the containment guard, the deterministic mock path passed when the
  explicit restricted-token Windows policy was selected. The current runtime
  deliberately rejects that legacy command path; its parent-delete mock
  returns a tool failure with the canary preserved.
- The stock Codex `model_catalog_json` seam now receives an exact generated
  entry from the authoritative capability record. No runtime-source patch or
  GPT-family metadata fallback is used by the NeoBabylon path.
- A deterministic local HTTP 429 characterization now passes using the
  pinned LM Studio capability record in offline mock mode. App Server emitted
  a typed `responseTooManyFailedAttempts` error with status 429; the selected
  provider/model remained effective and no tool call or fallback occurred.
  A fake-App-Server regression now also verifies that the host exposes the
  readable error, source attribution, typed category, and native 429 details;
  code inspection confirms the renderer places the message in an alert region.
  This is not live provider-limit evidence or a rendered WPF error-state test;
  see [VERIFICATION.md](../release/VERIFICATION.md).
- One isolated LM Studio/App Server run completed the exact `exec_command`
  round trip and `turn/completed` in 275.9 seconds. It reported
  effective model context `31129` from catalog context `32768` at the explicit
  95% adapter setting, with no provider/model mismatch or metadata fallback.
- A fresh deterministic-mock-first LM Studio repeat completed the same tool
  path in 289.8 seconds, and a separate visible WPF/WebView2 run completed it
  in 212.674 seconds. The WPF screen showed the selected model and policy,
  attributed command success to Codex App Server, and reached `COMPLETED`; its
  isolated journal recorded context `31129` and the thread returned to `idle`.
  Both new runs preserved the exact provider/model and no metadata fallback.
  Two older ten-minute non-completions remain unexplained, so reliability is
  not yet qualified. WPF evidence is under
  `D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-WPF-20260923`.
- The existing WPF Stop control was exercised during active LM Studio text
  generation after an assistant message delta. App Server returned an
  interrupted terminal state, the UI showed `INTERRUPTED` and re-enabled the
  composer, the thread listed `idle`, and the interrupted turn recorded no
  function call. This is one LM Studio cancellation pass, not a cross-provider
  or full no-zombie/recovery matrix.
- The selected free OpenRouter model
  `nex-agi/nex-n2.5-pro:free` completed one WPF→pinned App Server→provider→real
  model→`exec_command` output-continuation round trip under explicit
  `unelevated` policy. The session recorded process exit code 0 and
  `turn/completed`; its Codex-effective context was `249036` from advertised
  `262144` at the 95% adapter projection. A read-only post-run OpenRouter
  generation lookup attested Nex AGI, the returned canonical model, and zero
  cost for this response only; it does not guarantee future routing or disable
  fallback.
- The visible WPF host showed attributed live completion and successful tool
  status. A prior sandbox-denial journal was parsed into a typed
  `toolExecution` failure without exposing raw journal output. A post-run scan
  of 962 text/config/session files under the isolated OpenRouter application
  root found no exact credential match.
- The React/TypeScript shell builds locally with React 19.3.0, Vite 8.3.0,
  TypeScript 7.0.2, and bundled Manrope/DM Sans. Its tests now pass `23/23`,
  including a Vite-served local favicon regression test;
  the host/core protocol harness passes `44/44`, and the WPF host builds with
  zero warnings or errors.
- After local dependency acquisition was authorized, two focused tests in the
  pinned Codex source checkout passed with `--locked`: the model-context-limit
  distinction test (`1/1`) and WebSocket request serialization test (`1/1`).
  The latter preserves OpenRouter `provider.only` and `allow_fallbacks=false`.
  These do not qualify dynamic live budgeting/compaction or the complete
  upstream suite. The existing runtime checkout remains dirty and was not
  cleaned or rewritten.
- A follow-up ran focused upstream context/compaction checks: the protocol
  context-limit test passed (`1/1`), Codex-core clamp and smaller-context model
  switch tests passed (`1/1` each), and App Server compaction notifications
  passed (`2/2`). The core clamp test overflowed the default Rust test-thread
  stack, then passed with a process-local 8 MiB `RUST_MIN_STACK`; no source or
  machine setting was changed. These validate upstream mechanics, not
  NeoBabylon's live request-budget/compaction wiring. The runtime checkout was
  already dirty; the relevant compaction implementation/tests were unchanged.
  See [VERIFICATION.md](../release/VERIFICATION.md) for commands and scope.
- A fresh full host qualification run exited `0`; the diagnostic UI test,
  typecheck, and production build also exited `0`. The build emitted upstream
  Astryx `use client` directive notices. An isolated native WPF/WebView2 host
  passed Playwright smoke at `1464×901` and `960×720`: the tower image loaded
  at `29×29`, diagnostics opened/closed, the fresh profile defaulted to
  charcoal dark (`#181818`), dark/light switching returned without bridge
  errors, the notice's session-dismiss and persistent “Don't show again” paths
  behaved as intended across reloads, and the narrow viewport had no
  horizontal overflow. No provider request was sent; the temporary QA host
  closed gracefully. Captures and the scope limitation (WebView viewport, not
  native title-bar screenshot) are recorded in [VERIFICATION.md](../release/VERIFICATION.md).
- A WPF/WebView2 smoke at `1464×901` verified recent App Server history,
  transcript restore, “New task” preservation, and a second successful
  restore. It rejected a saved OpenRouter thread while LM Studio was selected.
  The trusted host response contained only user/assistant text (no command or
  tool output) and the display projection caps aggregate transcript text at
  120,000 characters with a visible truncation note.
  With no OpenRouter key, resume still worked locally, but a new turn failed
  before any App Server turn event. UI smoke recorded no external browser
  resource requests, console errors, or viewport overflow; no inference was
  sent during the history tests. Evidence is under
  `D:\CODING\NeoBabylon-Data\Phase2-UI-Live-20260923`.
- A separate recovery smoke gracefully stopped the visible WPF host and its
  App Server, relaunched both with the same isolated application root, reloaded
  the WebView page, listed the saved thread, and resumed that exact LM Studio
  thread without inference. The resumed view showed four projected messages
  and an `idle` turn state; diagnostics showed the same application/Data root
  and `Ordinary Codex root used=false`. The favicon request returned HTTP 200;
  the reload had no console errors, failed requests, or external requests.
  Evidence is `D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-WPF-20260923\post-restart-resume.png`.

- Dark is now the first-run UI appearance; users can switch to light and back,
  with the preference persisted under the isolated WebView2 profile. React
  tests pass `22/22`; TypeScript typecheck, production build, and Edge browser
  checks for preference persistence and `1440×900`/`960×720` layouts pass.
  The appearance browser test used Vite without WPF, so it does not add new
  host/provider evidence.

- A WPF-created fork is persisted only as an app-owned identity bookmark and
  revalidated through metadata-only App Server `thread/read` after restart.
  The fork resumed as the same runtime thread and completed one text-only
  LM Studio continuation with zero function calls. The sidebar shows the
  branch label and parent; the list returned one verified and zero unresolved
  bookmarks. Screenshot: `D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-WPF-20260923\fork-branch-label-verified.png`.

## Current qualification state

The latest credentialed WPF live tool test **predated the containment guard**.
It used App Server `0.155.1`, source
revision `be2951ea34f0d295ed0becf97079f92fa5f6950e`, binary SHA-256
`cb53784b951b42cb2925b71a14feaea61ae09154b69646fcf18e4903b1537187`, and
the OpenRouter capability record for `nex-agi/nex-n2.5-pro:free`. It completed
`cmd.exe /d /c ver` with exit code 0 and showed one attributed success in the
WPF transcript/activity. The request route was pinned to `nex-agi/fp8` with
fallback disabled by the runtime patch; a source serialization test verifies
`provider.only` and `allow_fallbacks=false`. Read-only OpenRouter generation
records for `gen-1790130176-ByDpeKxpIWRxxpazyGyM` and
`gen-1790130192-Dwfs4kEFfxP4Aa6Mt8bn` report Nex AGI, canonical model
`nex-agi/nex-n2.5-pro-20260907:free`, and zero cost for those responses. They
do not independently echo the endpoint tag. The isolated Codex journal records
context `249036` and nonzero token use (`12,596` input, `94` output, `47`
reasoning output), while OpenRouter reports zero token counters. Usage
attribution therefore remains inconsistent; do not treat provider usage
counters as verified token counts. The UI evidence and runtime/isolation
snapshot are `wpf-ui-live-evidence.json` and `runtime-and-isolation.json` in
the evidence directory above.

The earlier parent-delete canary used a then-locked fail-closed binary
(SHA-256 `dfa1b18d255f82c7fcaac0b7b83729ac1286a86a7d3783694983426ce08d5ab7`),
not the current product lock. Its offline mock parent-delete test reported
`toolRoundTrip=false`,
`toolSucceeded=false`, a `function_call_output` containing the explicit
NeoBabylon guard rejection, a typed Codex-attributed `toolExecution` failure
from the isolated journal, and an unchanged sibling canary. That result
applies only to its tested binary; older live LM Studio/OpenRouter tool
successes and newer locked-runtime evidence are separate runs and hashes.

The earlier LM Studio sessions included a `275.9s` completed round trip, one
ten-minute non-completion after a successful tool result, and one ten-minute
attempt with no function call. A fresh same-tuple App Server repeat on
2026-09-23 passed after the deterministic mock gate and completed in `289.8s`.
It used the loaded `phase1a-qwen3-14b` / `Q4_K_M` model, configured context
`32768`, and effective context `31129`; the effective model/provider matched,
and both the metadata-fallback and provider/model-mismatch diagnostics were
false. The tool returned Windows version output and exit code 0, alongside
PowerShell profile warnings. This repeat used the isolated qualification
harness, not a WPF live click-through. Two successful runs improve evidence but
do not establish repeatability while the two earlier non-completions remain
unexplained. See the current artifact at
`artifacts/phase1a/lmstudio/qualification.json`; the previous artifact is
preserved under `D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-Repeat-20260923T051010`.

The previous metadata warning is addressed by the exact custom catalog entry.
The previous sandbox report is explained by pinned source behavior: raw
`config/read` can retain `workspace-write` while the Windows backend projects
effective `readOnly` when no sandbox backend is selected. The explicit
`unelevated` selector produces effective `workspaceWrite` and is reported
separately from the raw request.

The Codex-first UI/UX report remains the design reference in
[UI_UX_REFERENCE.md](../architecture/UI_UX_REFERENCE.md). The first React/WPF
shell and App Server-backed history flow are now implemented under Martin's
explicit continuation authorization. This early UI slice does not change the
open Phase 1B gate or assert full Phase 2 completion.

Pinned source inspection shows the current `32768` model context and adapter
95% setting yield a `31129` usable context; Codex derives a 90% auto-compaction
threshold of `29491` even though the provider catalog field is null. A new
deterministic loopback integration probe now verifies that capability-to-Codex
context wiring and automatic compaction end to end: test-only context 2048,
Codex-reported usable context 1945, derived threshold 1843, provider-reported
usage 1900, matched compaction lifecycle events, and exactly four local
Responses requests. This does not establish live-provider usage accounting or
budget behavior. The successful live turn's measured generation rate explains
most of its 275.9 seconds, but the incomplete repeat has no completed response
statistics, so live repeat variance remains unresolved.

The full isolated host harness passed after adding this deterministic check.
The diagnostic UI suite now passes 55/55, and its TypeScript/production build
completes; Vite still reports non-fatal dependency `use client` directive
notices and a >500 kB chunk advisory. The default charcoal dark mode, matching
WPF caption, tower branding, dismissible unrestricted notice, and removal of
the welcome orbit decorations remain implemented. The new saved-history path
also treats a host-returned `executionEligible: false` task as history-only:
send controls and forking are disabled while “New task” remains available.
A local browser render verified the shell/theme but had no desktop bridge, so it
did not exercise the host-projected read-only state. Exact cross-restart
capability binding and missing/corrupt-sidecar behavior are covered by the
isolated host harness below.

## Not yet implemented or qualified

The first usable Codex-first UI shell exists, but the complete desktop client
is not qualified. Phase 1B remains open for repeatability, provider-usage
accounting and live budget behavior, live
provider rate-limit/unavailability behavior (the local 429 case is mock-only),
  live-provider approval generation/response qualification, Windows containment,
crash/failure recovery beyond one graceful restart/reload/resume pass,
cross-provider/repeated
cancellation, and malformed-protocol cases. One same-provider fork and
text-only continuation now pass, but crash recovery and repeated/cross-provider
fork behavior remain open. Full approval-matrix behavior, diffs/review, full
reconnect/recovery, real multi-page history, broad accessibility, and live
long-output behavior remain unqualified. Local project selection and narrow navigation-
hint reconnect are implemented but not the broader multi-project/recovery gate.
Basic search is a local
filter, not server-side pagination or indexed full-history search. Broad toolbox
and self-scaffolding remain later planned work. Installation/packaging are
explicitly deferred per NB-DEC-010, not treated as passed or merely queued.

## Open qualifications and risks

- Explain slow-generation variance before claiming live repeatability: the
  successful 275.9-second run is largely accounted for by measured output rate
  and volume, but two other attempts reached ten minutes. The later repeat had
  no tool call; its journal-access failure is fixed and regression tested, but
  the timeout/non-completion cause is not established.
- Account for LM Studio's explicit warnings that it ignored `namespace` and
  `web_search` tool types, `prompt_cache_key`, and `reasoning.encrypted_content`,
  and converted the developer role to system. The tested `exec_command` path
  passed; broader tool coverage is not established.
- The visible WPF OpenRouter and LM Studio live paths and the non-inference
  history UI cycle now pass. The C:
  `LocalAppData\NeoBabylon` mock failed closed again under the current
  Codex-hosted context; corresponding paths report the same
  NTFS file IDs under a packaged LocalCache alias, a plausible but unproven
  cause of the split-root mismatch. D: mock/live runs passed; standalone-host
  C: behavior is unverified.
- A new deterministic loopback App Server qualification now proves the
  capability-to-effective-context and automatic-compaction path with test-only
  context 2048: Codex's catalog/config use 2048, the session journal reports
  1945 usable after the 95% reserve, and provider-reported usage 1900 crosses
  the derived 1843 compaction threshold. The pinned App Server emits matching
  compaction start/completion items and makes exactly four fixture requests.
  This is not live-provider usage/budget evidence. Focused upstream static
  context-limit and compaction tests also pass (one requires a process-local
  8 MiB Rust test-thread stack on this machine). Live-provider accounting and
  model switch/reload behavior remain open. A newly added per-thread sidecar
  under isolated application `Data` binds the complete serialized capability
  record and workspace across host restarts. A changed record with the same
  provider/model, or a missing/corrupt binding, now returns readable history
  as explicitly non-executable; it is not rebound or backfilled. The complete
  host harness verifies changed-endpoint rejection, transcript retention,
  blocked turns, no provider inference, and no overwrite/backfill. The UI maps
  that host result to disabled send/fork controls and preserves “New task.”
  This plain local sidecar is a stale-state guard, not tamper-proof storage:
  explicitly unrestricted tools can modify local files. Live provider budget,
  model reload/switch, and malicious local tampering remain unqualified.
 - Deferred Phase 1B work includes live-provider approval prompts/responses,
   Windows containment, credential and crash/recovery tests. Fake App Server
   coverage now includes one malformed-JSON active-turn failure, preservation of one
  unknown informational notification, EOF for a pending request, unexpected
  server exit, and interruption.
  A deterministic
  interrupted-turn protocol regression and one live LM Studio active-generation
  cancellation pass; one graceful host/App Server restart and renderer
  reload/resume plus one revalidated fork and text-only continuation also pass.
  Other-provider/repeated cancellation, process-tree absence, crash recovery,
  live-provider approval generation, and the full approval matrix remain open.
  A deterministic native P2-08 fixture now drives actual pinned-App-Server
  accept, deny, and no-response timeout decisions through WPF; it is not live
  provider approval evidence. The earlier bounded UI/protocol slice is also
  tested with a synthetic bridge and fake App Server. The
  file-change approval remains deny-only because the pinned request omits its
  diff.
- Contained Windows command execution remains deferred and unqualified, not a
  prerequisite for the explicitly unrestricted Phase 2 path. Legacy deletion,
  native hard-link mutation, and
  elevated pre-existing and child-created hard-link/junction mutation are
  reproduced; none is a
  passing backend. The current guard prevents the known unsafe legacy sandbox path;
  the earlier full sandbox package run had 189 passes and 2 setup/elevated
  failures on this machine, not a green suite.
- Reconcile OpenRouter's zero generation token counters with the nonzero
  isolated Codex journal counters. Cost was zero for the observed responses,
  but token attribution remains Unknown/inconsistent.
- Verify the C: LocalAppData sandbox path in a standalone host. The current
  Codex-hosted context reports two lexical names for the same filesystem IDs;
  the D: app root is a tested non-broadening workaround.

See [PHASE1A_LMSTUDIO.md](../release/PHASE1A_LMSTUDIO.md) for LM Studio's exact
tuple and [PHASE1B_OPENROUTER.md](../release/PHASE1B_OPENROUTER.md) for the
OpenRouter tuple, evidence paths, capability projection, and limitations.
