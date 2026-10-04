# NeoBabylon Phase 1A/1B Implementation Plan

> **Current execution note — 2026-09-25:** Martin explicitly deferred the
> remaining Phase 1B checks and authorized Phase 2 with unrestricted tools.
> Phase 2 is active; the deferral is not a Phase 1B pass. For current slice
> status and evidence, use [the Phase 2 execution map](PHASE2_VERTICAL_SLICES.md),
> [status](../product/STATUS.md), and [verification report](../release/VERIFICATION.md).
> Older checkbox notes below are historical plan detail and must not override
> those current evidence records.

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> `superpowers:executing-plans` or `superpowers:subagent-driven-development`
> to implement this plan task-by-task after Martin explicitly authorizes the
> next stage. The task descriptions below remain proposals; the separately
> authorized Phase 1A execution record later in this document records what was
> actually implemented and verified.

**Goal:** Prove the smallest honest WPF/WebView2-to-Codex path with one real
LM Studio or OpenRouter model/tool round trip, then complete foundational
qualification for both selected providers without silent fallback or hidden
authority expansion.

**Architecture:** A WPF/.NET host contains WebView2 and a small
React/TypeScript/Astryx diagnostic surface. The host supervises the pinned
Codex App Server over local `stdio://`, translates protocol objects through a
narrow validated product bridge, and keeps runtime data under the selected
application root's `Data` directory. Codex remains authoritative for agent
execution, approvals, thread state, rollout/history, and recovery.

**Tech Stack:** WPF/.NET, WebView2, React/TypeScript/Astryx, the pinned Codex
App Server and generated stable protocol contracts, deterministic mock
Responses fixtures, and the actual LM Studio/OpenRouter targets selected from
the available environment. Exact SDK/package versions remain qualification
inputs.

**Spec:** [PRODUCT_BRIEF.md](../product/PRODUCT_BRIEF.md),
[ARCHITECTURE.md](../architecture/ARCHITECTURE.md),
[MODEL_PROVIDERS.md](../architecture/MODEL_PROVIDERS.md),
[TOOLS_AND_SELF_SCAFFOLDING.md](../architecture/TOOLS_AND_SELF_SCAFFOLDING.md),
and accepted records [NB-DEC-002](../decisions/0002-runtime-source-ownership.md),
[NB-DEC-003](../decisions/0003-desktop-shell.md),
[NB-DEC-004](../decisions/0004-free-local-provider.md), and
[NB-DEC-005](../decisions/0005-application-data-boundary.md).

## Global Constraints

- “The host direction is WPF/.NET with WebView2 around React/TypeScript/Astryx.”
- “The initial provider targets are LM Studio and OpenRouter.”
- “Durable state belongs under `<NeoBabylon root>\Data`.”
- “Product source and Codex-derived runtime source are separate local sibling
  Git repositories; no remote NeoBabylon repository is required.”
- Stable protocol contracts are the default; experimental capabilities are
  separate, visible, and deliberately opted into.
- The host owns credential policy and delivery; credentials do not enter the
  renderer, prompts, source, candidate manifests, workspace files, ordinary
  child commands, logs, or diagnostics.
- React receives named validated product operations, not generic
  `sendRpc(method, params)` forwarding.
- `thread/shellCommand` is an explicit-human full-host surface and is never a
  model-controlled candidate fallback.
- Existing authorized model shell/tool execution remains valid for temporary
  candidate use; a candidate cannot grant itself new permissions.
- Runtime-owned persistence is accessed through App Server APIs, not direct
  SQLite mutation or a competing session database.
- No silent provider, model, endpoint, reasoning, context, permission, or
  paid/free-cost fallback.
- During the Phase 0 plan revision, no production implementation, live
  inference, system-wide dependency installation, shared configuration change,
  repository creation/rearrangement, commit, merge, tag, push, or remote
  publication occurred. Phase 1A was later authorized separately; its bounded
  diagnostic execution is recorded below. Each later action still needs the
  authorization applicable to that stage.

## Review Focus

1. **Provider identity and capability truth:** requested versus reported
   provider/model/route, context and output accounting, reasoning semantics,
   tool continuation, and stale metadata after reload or model changes.
2. **Windows data and authority boundaries:** app-root discovery, reparse-point
   containment, single-owner runtime data, approval handling, and failure when
   a path or root is unavailable.
3. **Credential handling under unrestricted authority:** the host must not
   directly inject or serialize provider secrets into renderer state, prompts,
   logs, candidate artifacts, or child-command environment. Full same-user tools
   may independently read user-accessible files; if returned as normal tool
   output, that content is visible and forwarded without redaction under
   [NB-DEC-011](../decisions/0011-unrestricted-tool-output-visibility.md).
4. **Protocol/bridge safety:** stable versus experimental contracts, malformed
   JSONL, unknown informational events, unknown authority-bearing requests,
   and broken-process/reconnect behavior.
5. **Mutation and candidate behavior:** no blind replay after renderer/server
   failure, no candidate permission expansion, no self-promotion, and no
   full-host escape through `thread/shellCommand`.

---

## Settled direction versus proposed mechanics

The following are accepted and must not be reopened without concrete
contradictory evidence: WPF/.NET + WebView2, the two-local-repository boundary,
LM Studio + OpenRouter as initial targets, app-root `Data`, private-local
scope, stable-first protocol policy, ordinary authorized temporary candidate
use, and manual promotion.

The following remain proposals to refine against the pinned source and actual
machine evidence: the runtime manifest filename/schema, sibling folder names,
host service/class names, exact bridge operation names, .NET/WebView2 package
versions, candidate manifest fields, drawer layout, migration mechanism, and
weekly-review scheduling surface.

## Phase 1A execution record — 2026-09-22

This is an evidence record, not a replacement for the accepted requirements
and not authorization to expand the scope. Martin authorized the smallest
Phase 1A path with local sibling repositories, the pinned Codex runtime, a
thin WPF/WebView2 host, React/TypeScript/Astryx UI direction, and LM Studio as
the first live target.

- Task 1 evidence: `runtime/runtime-lock.json` ties the product to App Server
  `0.155.1`, source `rust-v0.155.1`, revision
  `be2951ea34f0d295ed0becf97079f92fa5f6950e`, and the verified binary hash.
  The disposable qualification root and WPF application root were distinct
  from the source repository and did not use the ordinary Codex root.
- Task 2 evidence: the WPF/WebView2 host, named bridge, isolated supervisor,
  and visible status/diagnostics/thread-start path were built and exercised.
  The bridge excludes generic RPC and `thread/shellCommand`.
- Task 3 evidence: the deterministic Responses path ran before LM Studio and
  completed the App Server/fixture function-call-output exchange. The command
  itself was rejected by policy, so the mock result is an explicit failure,
  not a successful ordinary-tool proof.
- Task 4 evidence: LM Studio server `0.4.25.0`, endpoint
  `http://127.0.0.1:1234/v1`, model `phase1a-qwen3-14b`, variant
  `qwen/qwen3-14b@q4_k_m`, and context `32768` were discovered and recorded.
  The live model emitted `exec_command`; the isolated session recorded its
  policy rejection. `toolRoundTripCompleted=true` but `toolRoundTrip=false`.

The Phase 1A acceptance gate remains **partial rather than a completed-turn
pass**. The explicit `unelevated` policy made the deterministic command pass,
and the real LM Studio session journal records the same command with exit code
0, but the App Server did not emit `turn/completed` within ten minutes. The
stock `model_catalog_json` seam removes the native metadata fallback for the
NeoBabylon path, and the raw/effective Windows sandbox layers are now
explained and reported separately. OpenRouter, approvals, recovery,
compaction, packaging, self-scaffolding, and broad UI remain out of scope.

### Later live rerun — 2026-09-22

The paragraph above records the first incomplete attempt. One later isolated
run completed the same LM Studio/App Server tool path in 275.9 seconds:
`exec_command` ran `cmd.exe /d /c ver`, returned exit code 0, and the App
Server emitted `turn/completed`. The selected provider/model and runtime
matched the lock and capability record; the effective context was `31129`,
and no metadata fallback warning was observed. The visible WPF click-through
is still unverified. A subsequent repeat reached the ten-minute bound while
generation was still active, without a function call or completed turn. Its
runner artifact was then masked by a transient session-journal sharing error;
the exact lock owner was not captured. Repeatability and slow-generation
behavior remain open, and the runner now preserves the turn observation while
reporting journal-read failure separately.

LM Studio's captured server log warned that `namespace` and `web_search` tool
types, `prompt_cache_key`, and `reasoning.encrypted_content` were unsupported;
it also converted the developer role to system. These warnings did not prevent
the tested `exec_command` round trip. They limit claims about the broader tool
set and request fields.

## Phase 1B initial execution slice — 2026-09-22

This is an evidence-backed implementation update, not a reopening of settled
choices and not a full Phase 1B gate. One live run completed; the later repeat
and the qualification-runner fix are recorded below as distinct evidence.

- Generate one exact Codex model-catalog entry from the authoritative LM Studio
  capability record through the supported `model_catalog_json` configuration
  seam. Keep reasoning levels, structured output, and auto-compaction unknown
  or disabled instead of copying GPT-family defaults.
- Keep `sandbox_mode = "workspace-write"` as the requested config value, but
  require an explicit `NEOBABYLON_WINDOWS_SANDBOX_MODE=unelevated` selector
  for the qualified restricted-token Windows backend. Report raw config and
  effective `thread/start` projection as separate fields.
- Deliver typed `toolOutcome` and host failure events to the diagnostic UI,
  including attribution, item type, status, exit code/output, and failure
  message. Preserve partial notifications when a turn times out.
- Keep the next assignment narrow: explain the slow-generation variance after
  one completed run and two ten-minute bounds, then qualify effective context
  budgeting/compaction. The reader regression test and deterministic mock path
  pass. Do not expand into
  OpenRouter, full approvals,
  recovery, packaging, or broad UI until this boundary is understood.

## Phase 1B follow-on evidence — 2026-09-22

Pinned-source inspection now distinguishes the LM Studio context value from
Codex's derived budgets. With context `32768`, the adapter's explicit
`effective_context_window_percent = 95` yields a Codex usable context/hard cap
of `31129`. In pinned App Server source, a null model `auto_compact_token_limit`
does not disable auto-compaction: `ModelInfo::auto_compact_token_limit()`
derives 90% of the resolved context, capped by any explicit limit. The default
scope is `Total`, so the derived threshold is `29491` for this tuple. The
turn loop checks the threshold before and after sampling and invokes its
compaction path when reached.

These are source-derived values, not LM Studio claims. The successful live run
observed the `31129` effective context, but did not reach or record a compaction
event. Dynamic budget/compaction and stale-capability behavior remain open. An
offline attempt to run the pinned Rust protocol unit test stopped because its
`tungstenite` Git dependency was not cached. A follow-up `--locked` attempt
used a NeoBabylon-specific Cargo home, attempted Git/registry resolution, then
stopped before compilation because Cargo reported the pinned checkout's lockfile
would need updating. The lockfile remained unchanged. The visible WPF test also
remains open because the Windows automation runtime failed before window
selection; no UI action or inference was started.

The completed live run's LM Studio request statistics show why that pass was
slow: its two responses generated `717` tokens at `4.19 tok/s` and `266` tokens
at `3.57 tok/s`, with measured total request times `181.73s` and `77.76s`.
Those measurements account for most of the `275.918s` turn. The later repeat
has no completed response statistics, so this explains the successful run's
duration but not the ten-minute non-completion. Repeatability remains open.

### 2026-09-23 same-tuple repeat and visible-host result

The deterministic mock-first App Server repeat completed in `289.8s` with
provider/model `lmstudio` / `phase1a-qwen3-14b`, configured context `32768`,
effective context `31129`, and no metadata fallback or provider/model
mismatch. A separate visible WPF/WebView2 run on the same tuple completed the
requested `cmd.exe /d /c ver` tool round trip in `212.674s`; the UI showed the
attributed success, completed turn, isolated application root, and exact
runtime identity. These two new completions do not explain the earlier two
ten-minute non-completions. The WPF screenshot and isolated App Server journal
are under `D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-WPF-20260923`.

### 2026-09-23 deterministic provider-error characterization

An offline-only local Responses fixture returned one HTTP 429 to App Server
`0.155.1` while the accepted LM Studio capability record remained selected.
App Server surfaced a structured `error` event with
`codexErrorInfo.responseTooManyFailedAttempts.httpStatusCode = 429`; the
effective provider/model stayed `lmstudio` / `phase1a-qwen3-14b`, with no tool
call and no provider/model fallback. App Server normalized the fixture body
message, so this test does not show end-user-facing provider prose. It used no
LM Studio process, external endpoint, API key, or inference, and is not
evidence for live rate-limit behavior. The exact artifact and test results are
in [VERIFICATION.md](../release/VERIFICATION.md).

## Phase 2 UI/UX reference

Use [UI_UX_REFERENCE.md](../architecture/UI_UX_REFERENCE.md) as the Codex-first
interaction baseline for the future functional desktop client. This is design
guidance, not authority to reorder phases or pull production UI, metrics,
tool-management, or developer surfaces into Phase 1B. Exact current chrome
must be checked against the installed client when Phase 2 reaches that work.

## Phase 1A — smallest honest functional path

### Objective and scope

Create one disposable WPF/WebView2 diagnostic host fixture, not the polished
desktop. It must supervise the pinned App Server, establish the actual typed
bridge, select one real LM Studio or OpenRouter configuration, and complete
one model/tool round trip. Build and qualify the other provider separately.

The first live path is:

```text
WPF/WebView2 diagnostic host
  → validated product bridge
  → pinned Codex App Server over stdio://
  → selected LM Studio or OpenRouter tuple
  → actual model request
  → permitted ordinary tool call
  → tool result returned to model
  → visible completion or explicit failure
```

Phase 1A must include runtime identity, isolated disposable workspace and
`Data` root, protected credential delivery, valid protocol framing, an explicit
authority boundary with one already-permitted ordinary tool operation,
requested/effective provider identity, and no
silent fallback from its first probe. “Harden later” does not mean running an
unrestricted agent first.

The product repository root is source-controlled project content. The
application root is the runtime/install root that owns `<NeoBabylon root>\Data`;
the two must not be conflated. Phase 1A uses a disposable application root and
disposable `Data`/Codex state, even if a later development layout deliberately
places that root near a checkout.

### Phase 1A preconditions and current status

The following were required before Phase 1A execution and are now evidenced
for the selected LM Studio attempt. They do not authorize the still-deferred
Phase 1B work:

- The selected two-repository local layout was established without a remote
  NeoBabylon repository or nested runtime copy.
- Cached .NET/WebView2 capability and exact package version were confirmed;
  no missing prerequisite was installed system-wide.
- A reachable LM Studio tuple was discovered from the live instance. No model
  slug or route was invented.
- The development application root is distinct from the product repository
  root, and the disposable `Data` root does not use ordinary shared Codex
  state.

### Task 1: Runtime identity and isolated data fixture

**Files:**

- Create (proposed): `runtime/runtime-lock.json` in the product repository,
  with the exact baseline/runtime executable, source revision, expected
  version, binary hash, generated-contract provenance, and fixture date.
- Create (proposed): `tests/fixtures/phase1a/workspace/` with safe input files,
  an instruction file, and one expected mutation.
- Create (proposed): `tests/fixtures/phase1a/data-root/` as disposable test
  data; never use real durable `Data` or an ordinary Codex home.
- Create (proposed): `tests/fixtures/phase1a/runtime-config/` with only the
  configuration needed for the selected fixture.

**Interfaces:**

- Consumes: the pinned binary/source records in `reference/sources.json` and
  the accepted runtime boundary in NB-DEC-002.
- Produces: a machine-readable runtime identity and fixture-root contract
  consumed by the host supervisor and tests.

- [ ] Verify the selected executable reports the expected version before a
  thread starts.
- [ ] Hash the executable and compare it with the runtime lock; fail visibly
  on mismatch instead of running an unknown engine.
- [ ] Resolve the fixture workspace, app-root `Data`, Codex runtime data, and
  logs to explicit absolute paths.
- [ ] Enforce one active owner for the fixture runtime data root; a second
  supervisor must fail clearly rather than share mutable state.
- [ ] Prove that normal product persistence uses App Server APIs and does not
  mutate Codex SQLite/rollout internals directly.
- [ ] Record path, version, hash, ownership, and shutdown evidence without
  storing credentials or uncontrolled workspace content.

**Test:**

- Run the identity/data-root fixture on Windows with a clean disposable root.
- Expected: matching version/hash starts; mismatch and second-owner cases
  stop before a thread; data is isolated from the user's ordinary Codex home.

### Task 2: Minimal WPF/WebView2 host and product bridge

**Files (proposed implementation paths):**

- Create: `host/NeoBabylon.Host/NeoBabylon.Host.csproj`.
- Create: `host/NeoBabylon.Host/App.xaml` and `App.xaml.cs`.
- Create: `host/NeoBabylon.Host/MainWindow.xaml` and a focused code-behind
  that delegates to services rather than owning agent logic.
- Create: `host/NeoBabylon.Host/Runtime/RuntimeSupervisor.cs`.
- Create: `host/NeoBabylon.Host/Runtime/AppServerBridge.cs`.
- Create: `host/NeoBabylon.Host/Runtime/CapabilityAdapter.cs`.
- Create: `host/NeoBabylon.Host/Runtime/CredentialDelivery.cs`.
- Create: `ui/diagnostic/` for the smallest React/Astryx status, prompt,
  streamed-event, tool-result, and explicit-failure view.
- Create: `tests/host/` for bridge and supervisor tests.

**Interfaces:**

- Consumes: Task 1 runtime lock, fixture roots, generated stable protocol
  types, and App Server stdio behavior.
- Produces: named product operations such as `getRuntimeStatus`,
  `startThread`, `startTurn`, `interruptTurn`, and
  `getDiagnostics`; the exact names are proposed but the boundary must
  remain product-level and argument-validated. Approval response operations
  are a Phase 1B addition.

- [ ] Start the App Server only after root and identity checks pass.
- [ ] Send one `initialize`, accept `initialized`, and preserve actual
  request/response/notification correlation rather than assuming strict
  generic JSON-RPC 2.0 behavior.
- [ ] For 1A, use one ordinary tool operation already permitted by the
  disposable profile; do not deliberately trigger an escalation. Keep
  unknown authority-bearing requests fail-closed. Full approval-category
  qualification is a Phase 1B task.
- [x] Preserve unknown informational events for diagnostics and fail closed on
  unknown authority-bearing requests. The host harness verifies preservation
  of unknown notifications and schema-valid denial of unknown server requests.
- [ ] Deliver a credential only to the authorized runtime mechanism; verify it
  is absent from renderer state, prompts, logs, workspace files, candidate
  artifacts, and ordinary child-command output.
- [ ] Exclude `thread/shellCommand` from every model-controlled operation.
- [ ] Shut down cleanly and surface server exit, broken pipe, timeout, and
  explicit failure without blind request replay.

**Test:**

- Run bridge tests with malformed messages, unknown event/request types,
  server exit, duplicate completion, and rejected authority responses.
- Expected: no renderer crash, no generic RPC escape, no auto-approval, and
  no duplicate mutation after a reconnect/reload boundary.

### Task 3: Deterministic mock Responses contract

**Files (proposed):**

- Create: `tests/fixtures/responses-mock/` with deterministic request,
  streamed event, tool-call, result, error, retry, timeout, and cancellation
  fixtures.
- Create: `tests/provider/` for provider request/response assertions and
  requested/effective capability records.
- Modify: `docs/architecture/MODEL_PROVIDERS.md` only when the observed mock
  contract reveals a concrete correction; do not convert a mock result into a
  live-provider claim.

**Interfaces:**

- Consumes: the stable generated protocol boundary and the provider contract
  in NB-DEC-004.
- Produces: repeatable evidence that the host/runtime path serializes and
  handles Responses streams, tool arguments/results, termination, malformed
  events, provider errors, retry, cancellation, and slow timeouts.

- [ ] Run the mock before contacting either live target.
- [ ] Assert tool-call arguments and result continuation, not only text output.
- [ ] Assert requested provider/model/configuration remain attributable.
- [ ] Assert unsupported critical reasoning/context/tool semantics stop the
  configuration instead of inventing defaults.
- [ ] Assert optional unknown capability remains visible without blocking an
  unrelated valid text/tool workflow.

**Test:**

- Run the deterministic mock suite from the product repository's documented
  test command once its test toolchain is selected.
- Expected: stable fixtures pass without live network access, credentials, or
  model files.

### Task 4: One live provider/tool round trip

**Files (proposed):**

- Modify: the Phase 1A provider fixture/configuration only; do not add a
  permanent provider fallback or invent a model catalog.
- Create: `artifacts/phase1a/<provider>/` with a redacted qualification record,
  exact endpoint/model/server/hardware identity, hashes/versions, timings,
  requested/effective capabilities, and limitations.

**Interfaces:**

- Consumes: Tasks 1–3, one actual LM Studio or OpenRouter tuple, and any
  credential supplied through the authorized host/runtime path.
- Produces: one attributable live qualification result, pass or explicit
  failure, without implying the other provider passed.

- [ ] Discover the actual available provider/model/route and record its source;
  do not infer from a model name or stale browser/cache record.
- [ ] Resolve context, output, reasoning, tool, streaming, and continuation
  semantics before activation where possible; verify the rest before the first
  agent request.
- [x] Prove that the normalized effective capability record reaches Codex's
  supported model/configuration state. Retained isolated catalogs and matching
  App Server `task_started.model_context_window` journals show LM Studio
  `32768 → 31129` and NEX/OpenRouter `262144 → 249036` at the 95% projection.
  This is wiring evidence only; it does not qualify actual request budgeting,
  compaction, or stale-capability invalidation. See the dated evidence in
  [VERIFICATION.md](../release/VERIFICATION.md).
- [ ] Run one real coding/tool fixture with a safe expected file mutation.
- [ ] Verify the requested and reported provider/model/route match and that no
  OpenAI, paid, or other hidden fallback occurred.
- [ ] Record provider errors, unsupported parameters, timeouts, cancellation,
  and rate-limit behavior when encountered; do not disguise them as success.
- [ ] Stop at the first unresolved critical capability or authority boundary.

**Phase 1A acceptance gate:**

Pass proves only that one selected provider tuple can be driven by the small
WPF/WebView2 fixture through the pinned App Server to one real permitted tool
round trip, with matching runtime identity, isolated data, protected
credentials, correct framing, visible completion/failure, and no silent
fallback. The tool operation uses authority already granted by the disposable
profile; this does not qualify the approval matrix. It also does not prove the
second provider, polished UI, recovery, packaging, broad toolbox,
self-scaffolding, or general model quality.

## Phase 1B — foundational qualification

Phase 1B begins only after a Phase 1A result is recorded. It completes the
minimum evidence needed before the path is called a dependable foundation.

### Task 5: Qualify the second selected provider

- [x] Repeat the live qualification sequence for the other target (LM Studio
  or OpenRouter) with its own exact provider/server/model/route/hardware and
  credential evidence. Unknown provider/hardware fields remain labeled Unknown
  in the separate records.
- [x] Keep records separate; a pass for one target is never copied to the
  other.
- [x] Characterize a deterministic loopback HTTP 429 response and verify the
  typed App Server error, selected model/provider retention, and absence of a
  tool call or fallback. This is a mock-only protocol test.
- [ ] Exercise live free/paid route boundaries and provider
  rate-limit/unavailability cases without automatic cost or endpoint
  substitution.

### Task 6: Authority, workspace, recovery, and capability regression

- [x] Repeat active-generation interruption and verify terminal state,
  process-tree absence, and no blind replay. One live LM Studio Stop pass
  reached `interrupted` after a streamed delta and the App Server thread
  returned to `idle`. Two additional isolated loopback attempts exercised the
  pinned App Server's active interrupt path, confirmed typed `interrupted`
  terminal outcomes, verified one provider request per turn and no replay on
  saved-thread resume, and confirmed the exact App Server process exited after
  supervisor disposal. The repeatability/process-cleanup test is deterministic;
  repeated live-provider cancellation and WPF Stop repetition remain untested.
- [x] Test local saved-thread listing and `thread/resume` using runtime-owned
  identifiers/history; exact provider/model/workspace mismatch is rejected.
- [x] Test one live `thread/fork`, restart, resume, and text-only continuation.
  A bookmark was revalidated through metadata-only `thread/read`; App Server
  remains the sole conversation-history authority. Crash/forced-termination
  recovery and repeated/cross-provider fork behavior remain unverified.
- [ ] Qualify live App Server approval prompts and every stable category
  exposed by the fixture. The host/UI now handles command, file-change, and
  permission requests through explicit named decisions, with fake-server and
  synthetic-bridge coverage for one-shot command acceptance, denial, exact
  turn-scoped permissions, and unknown requests. File-change requests are
  deny-only because the pinned request schema provides no diff to review. A
  command accept is withheld when the optional/experimental
  `availableDecisions` list is absent or does not include `accept`. No real App
  Server approval prompt has yet exercised this path.
- [ ] Test allowed writes, parent/sibling/outside paths, and Windows junction/
  reparse-point escapes.
  A protected-DACL deterministic App Server probe on 2026-09-23 denied an
  ordinary sibling write but twice allowed `cmd.exe del` to remove a sibling
  canary outside the thread workspace under `workspace-write` / `unelevated`.
  The focused pinned-source test also allowed outside-file and protected `.git`
  deletion. This demonstrates an effective-authority gap: the reported
  `workspaceWrite` sandbox allowed a forbidden delete. It is distinct from the
  earlier raw `config/read` versus legacy `thread/start` display mismatch; the
  direct source-level cause remains to be independently reviewed. Do not
  re-enable this backend or claim the containment item passes. Martin's later
  [NB-DEC-008](../decisions/0008-phase2-unrestricted-tool-authority.md)
  decision permits Phase 2 through explicitly unrestricted tools; this is a
  different authority choice, not evidence that Windows containment is safe.
  See the dated evidence in [VERIFICATION.md](../release/VERIFICATION.md).
- [x] Test direct host credential delivery through renderer, logs, workspace,
  environment, shell/profile behavior, and ordinary child-command output.
  Synthetic-canary checks passed on 2026-09-24 for the exact NEX capability
  under the accepted unrestricted authority: the deterministic App Server
  child reported the key absent, the effective config excluded it and disabled
  login/profile loading, and no canary appeared in isolated source/data
  artifacts. A separate native WPF/WebView2 launch with that canary captured
  by the host found none in rendered text/DOM, browser storage, bridge messages,
  renderer request URLs/headers, or the full application root; the only
  renderer requests were same-origin UI assets. No live provider request or
  inference was made. This qualifies the tested synthetic-canary paths, not an
  arbitrary provider, host process memory, or an actual user credential.
  Normal tool output is intentionally not redacted; see accepted
  [NB-DEC-011](../decisions/0011-unrestricted-tool-output-visibility.md) and
  [VERIFICATION.md](../release/VERIFICATION.md).
- [ ] Test malformed/unknown messages, broken pipe, server exit, and recovery
  diagnostics. The fake-server harness now covers one malformed-JSON active
  turn (partial events retained, `JsonReaderException` identified), one
  unrecognized informational notification (preserved through normal turn
  completion), EOF failure for a pending request, an unexpected server exit,
  and interruption. Broader crash/recovery cases remain open. See
  [VERIFICATION.md](../release/VERIFICATION.md).
- [ ] Test model switches, reloads, context changes, and stale capability
  invalidation. Prove the effective values reach request budgeting and
  compaction rather than remaining display-only metadata. A deterministic
  loopback integration now proves this narrow runtime path using a test-only
  2,048-token capability: the Codex catalog/config receive 2,048, its session
  journal reports 1,945 usable tokens after the 95% reserve, and reported
  usage 1,900 crosses the derived 1,843 auto-compaction threshold. The pinned
  App Server emits matched compaction lifecycle items and makes exactly four
  fixture requests. This is not live-provider budget/usage evidence. A
  deterministic App Server A→B→A model-switch probe now verifies that explicit
  host-side capability reset closes the prior binding, rejects a turn until a
  new thread is started, gives each selected model a distinct thread and exact
  request identity, and restores Model A's runtime-owned transcript only after
  an exact-record resume; only the three user-authored turns reached the
  fixture.
  This does not qualify model-switch behavior across renderer/host reloads or
  live-provider budget behavior. The cross-restart capability gap is guarded
  by a per-thread identity
  sidecar under isolated application `Data`: resume compares the complete
  serialized capability record and normalized workspace before executable
  resume. A mismatch, missing file, or corrupt file retains readable history
  but is non-executable; NeoBabylon does not backfill or overwrite it. The
  host harness covers same-provider/model changed-endpoint rejection, blocked
  turns, missing/corrupt history-only behavior, no provider inference, and no
  binding repair. A rendered shell check confirms the UI presentation, while
  the read-only controls are code/build tested rather than exercised through a
  connected WPF host in this run. The sidecar is a stale-state guard, not a
  tamper-proof boundary under explicitly unrestricted tools. Post-reload
  capability invalidation and live-provider budget behavior remain open; the
  broader checklist item stays unchecked.
- [x] Verify stable baseline behavior with experimental capability off and
  prove a representative experimental request is unavailable without opt-in.
  `AppServerProtocol.BuildInitializeRequest` sends
  `capabilities.experimentalApi=false`; the current locked App Server passed
  stable thread/turn behavior in the deterministic host harness and the live
  NEX qualification. Against the same `be2951ea34f0d295ed0becf97079f92fa5f6950e`
  source checkout, `cargo test --locked -p codex-app-server --test all
  suite::v2::experimental_api -- --nocapture` passed 9/9 integration tests,
  including the expected `-32600` rejection for
  `thread/memoryMode/set` without the capability. The gating source and test
  file are unchanged in this checkout. This closes the bounded stable/API-off
  boundary only; it does not enable experimental APIs or qualify other
  experimental methods.

**Phase 1B acceptance gate:**

Pass requires both provider records, the Phase 1A gate, and the authority,
recovery, Windows containment, credential, capability-mapping, approval, and
stable/experimental-boundary evidence above. It still does not authorize
dynamic tools, candidate promotion, packaging, public distribution, or a
modified runtime.

**2026-09-23 sequencing revision:** Martin later explicitly deferred Windows
containment and authorized Phase 2 to proceed with unrestricted tools. The
other open Phase 1B checks are also deferred until needed or requested; their
boxes remain unchecked and the full Phase 1B gate remains open. The legacy backend fails a direct
DELETE regression, and a native MXC/PSEC candidate fails an isolated hard-link
write regression. The elevated separate-account candidate also fails both
pre-existing and child-created hard-link and junction mutation regressions.
None is an approved
model-directed tool path. See
[NB-DEC-007](../decisions/0007-windows-legacy-containment-gate.md).
The interim authority and its limits are recorded in
[NB-DEC-008](../decisions/0008-phase2-unrestricted-tool-authority.md).

## Phase 2 — functional desktop client

For bounded agent handoffs, the remaining work is divided into independently
testable journeys in [PHASE2_VERTICAL_SLICES.md](PHASE2_VERTICAL_SLICES.md).
That execution map does not alter this plan's acceptance authority.

The already implemented bounded first-shell work below remains valid. Further
Phase 2 implementation proceeds under Martin's explicit unrestricted-tools
decision, without claiming that Windows containment or the full Phase 1B gate
passed. The other Phase 1B checks remain deferred until needed or requested.

The UI direction is documented in
[UI_UX_REFERENCE.md](../architecture/UI_UX_REFERENCE.md). It guides the shell,
conversation, composer, contextual panes, and progressive disclosure; it does
not override the phase boundary or make proposed NeoBabylon extensions
requirements.

### 2026-09-23 early authorized implementation note

Implemented: a local React/TypeScript Codex-first shell hosted by WPF/WebView2;
named capability selection; App Server streamed conversation/tool events;
runtime/capability diagnostics; saved-thread listing; exact provider/model and
fixture-workspace checks before resume; bounded latest-20-turn transcript
restoration through a host projection containing only user/assistant text,
omitting tool records and attachment payloads, with a 120,000-character
aggregate display limit; and “New task” that preserves the old App Server
session. WPF
smoke evidence covers restore → new task → restore, model mismatch rejection,
keyless local-history access, and provider-turn rejection before App Server
dispatch when the OpenRouter key is absent. No inference was sent by these UI
smokes.

Dark mode is the first-run default. An accessible light/dark toggle persists
the explicit choice in the WebView2 profile under the isolated application
data root. The original light palette is retained; both modes and persistence
were browser-tested at the WPF-supported `1440×900` and `960×720` sizes. This
small appearance feature does not close the Phase 1B or full Phase 2 gate.

On the tested Windows 11 build, the native WPF title bar now follows that
persisted UI choice; the dark palette is neutral charcoal/gray, with light
appearance retained. Appearance sync is a narrow host operation, not arbitrary
renderer access to desktop APIs.

The unrestricted-tool warning is dismissible per session, with a separate
versioned local UI preference for “Don’t show again.” The authority chip is
never hidden by that preference. The large welcome-screen orbit decorations
were removed at Martin's request; no authority or runtime setting changed.

Not yet fully qualified: cross-project history/recovery and live approval request
generation and the complete authority matrix; diffs/review, reconnect and
renderer recovery, history pagination, full draft recovery, accessibility, and
long-output behavior. The bounded approval cards and host responder have
synthetic-bridge and fake-App-Server tests, but no real approval prompt has
been generated. File-change requests remain deny-only until a reviewable diff
is available. Basic saved-history search by preview/provider/model is
implemented locally and WPF-smoke-tested; it is not pagination or indexed
full-history search. The live shell is a partial Phase 2 slice, not the full
Phase 2 gate.

Pinned source inspection narrows the diff/review work: stable
`turn/diff/updated` and `item/fileChange/patchUpdated` events exist, but the
file-change approval request itself has no diff. The current generated
local-model catalog uses `apply_patch_tool_type: null`, and the upstream
turn-diff tracker is populated by patch-tool events rather than arbitrary
shell writes. A proposed next slice is to project only an explicitly
attributed, exact-thread/turn diff with stale-content invalidation, then test
whether that is sufficient for any approval path. Until that evidence exists,
retain deny-only file-change requests. This is an implementation proposal,
not an accepted requirement to change the runtime or tool authority.

The first read-only display slice is implemented: the renderer accepts only
exact thread/turn `turn/diff/updated` events after `turn/started`, replaces
prior aggregate revisions, and labels empty/oversized diffs unavailable. An
inline review action opens a contextual drawer with no approval action. A
synthetic rendered bridge and unit tests pass. A host projection now retrieves
bounded saved `fileChange` item diffs from App Server turn history for
resume/fork, and a synthetic UI reopen passes. A real WPF/App Server task
without patch items correctly offers no review. An isolated mock Responses
fixture plus test-only `apply_patch_tool_type: freeform` catalog has now driven
the exact pinned App Server through a real patch, positive turn-diff events,
and saved patch-item history. The same task's positive saved review now also
renders in native WPF/WebView2 across renderer reload and QA-host restart.
The production local-model catalog remains `null`; real-provider patch
support, arbitrary shell edits, pre-approval content, and changed-content
invalidation remain separate open qualifications.

The first bounded real-provider patch probe (OpenRouter NEX free, exact route)
failed the patch-specific qualification: Codex advertised test-only freeform
`apply_patch` in a captured deterministic provider request, but the live NEX
turn produced no tool item or diff and explicitly reported the tool unavailable.
The isolated file remained unchanged. Do not infer the cause from that output:
inspect Responses/tool translation and the effective tool inventory before
proposing a focused runtime adaptation. Do not silently convert this into an
untracked shell write or mark the read-only review path as live-qualified.

The first unrestricted-policy slice requests `danger-full-access` / `never`
from pinned App Server for each thread and turn, verifies the effective thread
state, and labels the WPF UI “Full access · no containment.” An isolated mock
Responses request completed an actual ordinary Windows command through App
Server; no live provider inference was used for this new policy. The later
project-folder selection slice below does not close broader Phase 2 checks.

A subsequent bounded draft slice stores exact unsent composer text in the
isolated WebView2 profile under a versioned project/task key. A WPF reload,
graceful host restart, and one abrupt QA-host relaunch restore a new-task
draft; a synthetic bridge proves acknowledged-send clearing, failed-start
retention, and draft restoration on reopening a saved task after renderer
reload. It does not yet qualify real App Server saved-thread restore, active-turn
crash recovery, or live-provider interruption. A later actual saved-task WPF
probe passed task-scoped draft restore through New task, reopen, renderer
reload, and host restart, followed by explicit clear. It did not exercise an
active-turn crash or storage failure. The combined Phase 2 checkbox below
therefore remains open.

A fresh 2026-09-24 Edge/Vite synthetic-bridge test simulates a renderer reload
after `startTurn` is posted but before the host response arrives. If the fake
App Server persisted the prompt as an interrupted turn, resume restores the
message once, clears only its exact stale draft, and does not replay. If there
is no accepted turn, the prompt remains in that task's composer for retry, also
without replay. This is UI reconciliation against synthetic App Server state;
it does not qualify actual App Server or WPF-process restart behavior, native
WebView2, live inference, or total-storage-failure recovery. The broader Phase
2 recovery/draft checkbox remains open.

A further narrow reconnect slice stores only a versioned active-task
navigation hint in isolated WebView2 storage. Startup requires exact selected
project/provider/model and a matching listed App Server thread before
`thread/resume`; it never replays a turn. A rendered synthetic bridge passes
matching restore, mismatch/missing-history refusal, and New task clearing.
The first isolated live WPF QA profile had no saved turns, and an empty thread
did not appear in App Server history. A separate isolated root with two real
saved LM Studio threads later passed same-thread transcript restore after
renderer reload and abrupt QA-host restart without inference. A deterministic
paused-Responses/App Server-exit probe now observes a persisted `interrupted`
turn and no provider replay on read/resume. The host projects that status and
the rendered UI attributes it visibly. A subsequent isolated WPF-host
termination/restart during the paused mock request restored the same task as
interrupted without replay and completed one separate mock follow-up. A
separate between-turn App Server child exit now passes while WPF remains
open: the exact saved task is revalidated before a new user-authored
continuation, with no provider replay. An in-flight mock request now also
shows a typed failure after only App Server exits while WPF stays open; a
new prompt resumes the same saved task without replay. Inline status from
App Server history survives a later successful turn and renderer reload.
Live-provider interruption and broader cross-project recovery remain open. The
earlier white-title/blue-body screenshot predates
the verified current neutral-dark WPF build; it does not introduce a new
palette requirement beyond the accepted black/gray dark direction.

An additional isolated recovery slice tests repeated pinned-child exits and a
failed replacement initialization without dropping the selected task. The
supervisor binds a live task to the complete capability record, not merely its
provider/model labels; saved-history listing now reserves the same host
operation slot as turn start. A rendered mock-provider WPF exit shows an
`UNKNOWN` live outcome after stream loss and the authoritative `INTERRUPTED`
saved outcome after reload, without replay. This narrows but does not close
the active-turn recovery checkbox: reverse list/start interleaving, broader
concurrency and live-provider interruption remain unqualified.

A two-project pinned-App-Server test and rendered WPF probe now pass one
bounded cross-project history/recovery path against a loopback Responses
fixture. Distinct project tasks remain separately listed, a cross-project
resume fails closed, switching back restores the original task, and an actual
WPF host restart preserves the selected project and permits an explicit
continuation in the other project without replay. This does not qualify live
providers, concurrent switches, larger project sets, or the full gate.

- [x] Render the first React conversation shell in the selected WPF/WebView2
  host without replacing the named bridge.
- [x] Default to dark appearance, with an accessible, locally persisted
  light/dark choice synchronized with Astryx and browser color-scheme state.
- [x] Add bounded local project-folder selection with a native WPF picker,
  isolated Data registry, project-scoped saved threads, and exact selected cwd
  for App Server start/resume/fork. Missing folders fail closed; the selected
  project is never silently replaced. Provider/model selection from canonical
  capability records and requested/effective identity diagnostics are present.
  One two-project mock-provider history/recovery path now passes; broader
multi-project and larger workspace UX remain open.

The diagnostics panel has one verified keyboard-modal slice in the rendered
WPF host: initial/return focus, boundary Tab wrapping, Escape, and inert
background. This does not close the broad accessibility task; audit remaining
dialogs, screen-reader semantics, contrast, and keyboard-only task flows.
- [x] Add saved-thread listing/resume, bounded transcript restore, streamed
  events, tool progress, errors, and history-preserving new-task behavior.
- [x] Add and WPF-smoke-test local saved-history filtering by preview,
  provider, and model; this does not satisfy App Server pagination or
  full-history indexing.
- [x] Add bounded saved-history page loading with separate project-bound
  App Server cursors and no duplicate sidebar rows. The bounded native
  WPF/WebView2 page, project isolation, and exact older-task reopen path passed
  under P2-01; broader concurrency and full-history indexing remain open.
- [x] Coalesce high-frequency assistant stream deltas in the renderer without
  truncating the authoritative response. The synthetic 5 MB reply/2 MB tool
  output case and a 1.1-million-character native deterministic case passed; a
  separate native WPF/App-Server NEX turn rendered 18,403 characters. A hard
  output cap, native-request cost, independent route attestation, and
  arbitrary-size behavior remain unqualified.
- [x] Add an attributed, read-only review drawer for exact live App Server
  turn-diff events, with explicit invalidated/oversized states and no approval
  authority. Saved patch item diffs have a separate bounded host projection
  and synthetic reopen check. Positive pinned-runtime patch emission and saved
  item projection pass with a deterministic mock provider. In addition, a
  retained live NEX/OpenRouter run qualifies function-form `apply_patch` for
  that exact model/route and runtime, with a completed saved review. Native WPF
  saved-review rendering after renderer reload and graceful host restart is
  verified for the current function-patch runtime hash. Broader provider/tool
  coverage and approval qualification remain open.
- [x] Add bounded inline command/file-change/permission approval cards and a
  named host response operation. Fake-App-Server and Playwright synthetic-bridge
  tests cover explicit one-shot command choices, file-change denial without a
  diff preview, exact turn-only grants, and fail-closed unknown/missing
  metadata; live prompt generation and full approval qualification remain open
  in Phase 1B.
- [ ] Finish broader active-turn/cross-project recovery, Windows screen-reader
  evaluation, arbitrary-size/live-output limits,
  and remaining Phase 2 acceptance evidence. P2-03 through P2-06 have bounded
  native pending-send, draft-storage failure, cross-project restart, and
  interruption/no-replay cases; broader crash/concurrency behavior and durable
  partial assistant-text restoration remain open. P2-10's nine-flow browser
  accessibility fixture passed in synthetic Edge. An earlier eight-state
  rendered-text/placeholder audit measured 713 observations; a fresh
  2026-09-25 audit measured 714, with zero active/inactive contrast exceptions
  or unknown backgrounds. A separate native keyboard/history subset passed;
  full native nine-flow and screen-reader qualification remain open. A
  deliberate red run first reproduced 16 below-AA occurrences across 14
  disabled-control findings; opacity-based text fading was corrected and is
  now a failing regression condition. The separate native P2-12 smoke test
  checked visible focus after Tab; it did not rerun the nine flows natively.
  Windows Narrator remains open. P2-12 verifies dark/light mode,
  matching native chrome, persistence, notice dismissal, focus, and selected
  contrast samples. The App Server remains authoritative for saved history;
  no second durable partial-transcript store has been added.
- [x] Keep generated stable types separate from experimental modules; unknown
  informational items remain inspectable and unknown authority-bearing requests
  fail closed. The versioned reference corpus keeps stable TypeScript/JSON
  Schema and experimental TypeScript/JSON Schema in separate generated trees;
  the host harness verifies notification preservation and fail-closed request
  handling.
- [x] Prove the displayed transcript reconstructs from runtime-owned history
  and does not become a second authoritative history store.

## Phase 3 — broad toolbox and quality of life

- [ ] Inventory upstream capabilities before rebuilding them.
- [ ] Keep browser/computer interaction, screenshots/vision where supported,
  large-document access, search, patching, shell, compaction, skills, hooks,
  MCP, and plugins visible in the feature matrix with actual provider/runtime
  evidence.
- [ ] Add permissions, timeout/cancellation, denial, partial-output, and
  source/version records for each exposed category.
- [ ] Test large-document truncation and omitted-range access instead of
  inferring success from advertised context length.
- [ ] Add the weekly read-only upstream report; it must not auto-update pins,
  install dependencies, or enable experimental capabilities.

## Phase 4 — useful self-scaffolding and manual promotion

- [ ] Create candidates from the standard template under durable
  `Data\GeneratedTools` only after a genuine gap remains.
- [ ] Validate/test/use candidates through existing authorized model/tool
  execution under the current permissions; do not require a bespoke executor
  merely because code was generated.
- [ ] Retain unapproved candidates across restarts and expose their provenance,
  permissions, dependencies, evidence, and actual paths in an Unapproved Tools
  drawer.
- [ ] Test changed-content review invalidation, manual review, integration,
  activation, rejection, revocation, cleanup, and safe evidence retention.
- [ ] Keep `thread/shellCommand` and other full-host surfaces human-only.

## Phase 5 — targeted divergence, migration, and packaging

- [ ] Propose a focused runtime/client adaptation only when a concrete
  LM Studio/OpenRouter or pinned-protocol incompatibility requires it.
- [ ] Record affected upstream files/symbols, compatibility tests, merge risk,
  removal condition, and rollback path.
- [ ] Define application-file update, protected Data migration, backup,
  downgrade, signing, notices, and install/recovery behavior.
- [ ] Run bounded performance/soak evaluations with reproducible provider,
  model, hardware, and thresholds.
- [ ] Revisit Android, hosted, multi-platform, or public distribution only if
  Martin explicitly adds it to product scope.

**Current scope decision:** local installation and packaging/recovery work is
deferred under [NB-DEC-010](../decisions/0010-defer-local-installation-packaging.md).
This is not a completed Phase 5 item; do not resume it until Martin asks.

## Cross-phase evidence map

| Theme | Phase 1A | Phase 1B and later |
| --- | --- | --- |
| Runtime | version/hash, stdio process, initialize, shutdown | restart/recovery, source-build/adaptation when needed |
| Provider | mock then one live tuple/tool call | second provider, capability mapping, limits, compaction, rate/error cases |
| Authority | named bridge, approval channel, no full-host model escape | Full same-user authority is accepted under NB-DEC-008; Windows containment/reparse-point qualification is deferred. Direct host-managed credential handling remains a separate check; unrestricted tool output is visible and forwarded under NB-DEC-011. |
| Persistence | isolated app-root Data and single owner | resume/fork/recovery, migration/backup, downgrade behavior |
| UI | diagnostic status/prompt/event/tool/error view | functional desktop, accessibility, drafts, long output, renderer recovery |
| Tools | ordinary permitted built-in tool round trip | toolbox catalog, MCP/skills/hooks/plugins, candidates and promotion |
| Evidence | redacted trace and exact tuple | repeatable qualification bundle and upstream maintenance records |

## Stop conditions

Stop and return to review if execution would alter the accepted provider/cost
boundary, broaden platform scope, bypass runtime authority, share ordinary
Codex state, duplicate durable history, expose credentials, make an
experimental API a hidden requirement, use `thread/shellCommand` as a model
tool, initialize/rearrange repositories without authorization, install a
system-wide dependency, or publish/share the project. Report the exact
evidence and decision needed instead of silently expanding the plan.
