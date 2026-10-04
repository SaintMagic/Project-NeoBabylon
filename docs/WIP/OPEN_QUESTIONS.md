# Remaining implementation questions

Status: **Settled product direction with technical evidence gaps.** The eight
original decision categories are retained below for traceability. They no
longer reopen Martin's selected direction; each lists only the implementation
detail or evidence needed before the relevant phase.

## Phase 1A execution reconciliation — 2026-09-22

Phase 1A was authorized after the documentation revision. The smallest
diagnostic path now exists and was exercised with the exact LM Studio tuple
recorded in [the qualification report](../release/PHASE1A_LMSTUDIO.md). Runtime
identity, isolated application/Data roots, mock-first ordering, named WPF
operations, and effective provider/model/context wiring are evidenced. The
default Windows policy deliberately downgrades to `readOnly`, while the
explicitly selected `unelevated` restricted-token policy passes the
deterministic ordinary command and the real session journal records the same
command with exit code 0 and `turn/completed` in 275.9 seconds. A fresh
same-tuple repeat on 2026-09-23 also completed in 289.8 seconds after the
deterministic mock path. Two other attempts reached the ten-minute bound: one
recorded a successful tool result without final completion; the other recorded
no function call or completed turn. The latter's journal read also hit a
transient sharing error; the runner now preserves the timeout and reports
journal-read failure separately, covered by a locked-file regression test.
Two completions are now observed, but repeatability and the cause of the
non-completions remain open.

## 1. Desktop shell — selected, Phase 1A diagnostic evidence partial

WPF/.NET with WebView2 is selected around React/TypeScript/Astryx. Phase 1A
verified cached prerequisites, process supervision, named bridge operations,
visible diagnostics, and a development application root without installing
system-wide dependencies. Accessibility depth and packaged-root behavior
remain open for Phase 1B. See [NB-DEC-003](../decisions/0003-desktop-shell.md).

## 2. Runtime source ownership — selected, identity evidence complete

Use two separate local sibling Git repositories: NeoBabylon product and
Codex-derived runtime. No GitHub hosting is required. Phase 1A created and
verified the exact sibling path/name, product-side runtime manifest/lock,
source/binary hash relationship, and launch/build wiring at
`D:\CODING\NeoBabylon` and
`D:\CODING\NeoBabylon-Runtime`; the runtime source remains upstream-shaped and
unmodified. Source-build parity and later runtime update policy remain open.
See
[NB-DEC-002](../decisions/0002-runtime-source-ownership.md).

## 3. Initial providers — narrow tuples qualified; broader coverage open

LM Studio and OpenRouter remain the two accepted initial targets. Phase 1A
resolved the local Qwen3-14B tuple and recorded its model, quantization, and
context; hardware, reasoning controls, and structured-output behavior remain
`Unknown`. Two narrow live LM Studio completions are recorded alongside two
ten-minute non-completions, so repeatability is not established. The normalized
record reaches Codex without native-metadata fallback.

OpenRouter is now narrowly qualified for the selected NEX free model and exact
Nex AGI `nex-agi/fp8` route. The request pin disables fallback; live evidence
covers an ordinary function call/output path and one function-form
`apply_patch` path under the currently locked runtime. This does not qualify
other models, routes, or tool types. Provider token accounting remains
inconsistent with Codex's journal, and reasoning telemetry, live structured
output, and budgeting/compaction remain open. Recheck volatile model/route
metadata before any new live call. Do not invent a model slug or silently
substitute another provider. See [NB-DEC-004](../decisions/0004-free-local-provider.md),
[NB-DEC-006](../decisions/0006-openrouter-route-pin.md), and the
[OpenRouter qualification](../release/PHASE1B_OPENROUTER.md).

## 4. Application data — selected root, Phase 1A isolation evidenced

Durable state belongs under `<NeoBabylon root>\Data`, where that root means an
application/install root rather than the source-repository root, with isolated
Codex state and one owner at a time. Define development and packaged root discovery,
pre-migration backup/snapshot, version checks, interruption/recovery,
downgrade limitations, log redaction, and protected credential delivery. No
automatic shared `CODEX_HOME` or history import is implied. Phase 1A proved a
source-repository-distinct application root, isolated `Data\CodexHome`, and no
use of the ordinary `%USERPROFILE%\.codex`; packaged-host path presentation,
single-owner enforcement depth, migration, and recovery remain open. See
[NB-DEC-005](../decisions/0005-application-data-boundary.md).

## 5. Distribution and licensing — private-local direction accepted

NeoBabylon remains private local software for the current scope. There is no
Phase 1 licensing or public distribution decision to make. Continue preserving
Codex/Astryx and future third-party notices; revisit first-party distribution
terms only if distribution becomes an explicit requirement. See
[LICENSING.md](../release/legal/LICENSING.md).

## 6. Protocol policy — stable baseline selected

Use stable generated contracts by default; keep experimental contracts and
capabilities separate and deliberately opted into. The baseline qualification
must run with experimental capability off. A weekly read-only upstream review
is planned discovery, not an automatic update. The remaining details are the
report/scheduling surface and the first experimental feature worth qualifying;
neither blocks Phase 1A. See [UPSTREAM_STRATEGY.md](../architecture/UPSTREAM_STRATEGY.md).

## 7. Candidate promotion — workflow selected, storage details open

Use a standard-template candidate under `Data\GeneratedTools`. An unapproved
candidate may be tested and used through existing authorized model/tool
execution under the current task permissions, then retained in an Unapproved
Tools drawer for manual review. Promotion, integration, activation, rejection,
revocation, and Git commit authorization remain separate. The candidate
manifest schema, narrow candidate-root access, evidence layout, and review
record storage are Phase 4 implementation details.

## 8. Phase 1 authority/provider contract — Phase 1A evidence with blockers

Phase 1A defined the smallest named UI-to-host operations, initial diagnostic
profile, runtime identity manifest, mismatch behavior, and provider
qualification record. It preserved the following settled boundaries:

- App Server owns ordinary agent execution, approval semantics, thread state,
  and runtime persistence; the host owns the trusted interaction channel and
  credential policy/delivery.
- React receives validated product operations, not generic arbitrary RPC.
- `thread/shellCommand` and other full-host escape surfaces are never
  model-controlled.
- Requested and effective provider/model/capability values remain attributable;
  no silent cost, endpoint, model, reasoning, context, or permission fallback.
- In 1A, prove normalized capability wiring into Codex's effective
  model/configuration state; defer real budgeting/compaction and stale-value
  invalidation to 1B.

The remaining blockers are live-run repeatability despite two successful
completions and two ten-minute non-completions,
real budgeting/compaction and stale-capability evidence, and the broader
approval/containment/recovery matrix. The evidence does not authorize
`thread/shellCommand` as a workaround or reopen the settled direction.

Pinned-source inspection now clarifies the static context mapping: the LM
Studio context `32768` with the adapter's 95% setting yields Codex usable
context `31129`; Codex derives a 90% auto-compaction threshold (`29491`) even
when the catalog field is null. The successful live session reported `31129`,
but no live budget trigger or compaction event was observed. The formerly
blocked `model_context_window_limits_preserve_their_distinct_meanings` test
now passes `1/1` from the pinned runtime checkout with `--locked`. A separate
`direct_serialization_preserves_websocket_request_payload` test also passes
`1/1`, preserving the exact OpenRouter route pin and disabled-fallback fields
in the serialized request. These static checks do not establish dynamic live
budgeting/compaction or stale-value invalidation, which remain open. See the
[current verification record](../release/VERIFICATION.md).

The 2026-09-23 visible WPF/WebView2 LM Studio click-through is now verified in
the isolated D: application root. It reached terminal `COMPLETED`, showed the
attributed tool success, and recorded the expected effective model context.
The older C: `LocalAppData` root remains unverified. A later same-session live
LM Studio cancellation after an assistant text delta also reached terminal
`INTERRUPTED`; the thread was `idle`, the composer re-enabled, and the journal
showed no function call for that turn. A later non-inference restart smoke
gracefully stopped/relaunched the WPF host and App Server on the same isolated
root, reloaded WebView, listed and resumed the saved thread, and reached `idle`.
This is not crash recovery; further provider/repeat/no-zombie coverage remains
open. The earlier Windows automation failure is retained as historical
evidence, not the current UI status.

## Phase 1B initial slice — implemented, not a full gate

- The authoritative LM Studio record generates one exact Codex model catalog
  entry. Unknown reasoning and structured-output values remain Unknown;
  unsupported Codex controls are not enabled.
- The raw requested `workspace-write` value and the effective Windows sandbox
  projection are reported separately. No Windows backend means effective
  `readOnly`; explicit `NEOBABYLON_WINDOWS_SANDBOX_MODE=unelevated` selects
  the restricted-token backend and reports effective `workspaceWrite`.
- Tool results and host turn failures now have typed diagnostic event/failure
  fields, and the diagnostic UI renderer consumes that shape. Visible LM Studio
  and OpenRouter WPF click paths are verified; the broad approval/error matrix
  remains open.
- One run completed with the exact catalog and explicit policy. LM
  Studio logs show warnings for ignored `namespace`/`web_search` tools,
  `prompt_cache_key`, and encrypted reasoning content, plus developer-to-system
  role conversion. The exact tested `exec_command` succeeded; broader tool
  support remains unqualified. A subsequent repeat did not reach tool
  execution, and the runner's journal-read failure no longer masks timeout
  evidence. The next bounded assignment is to explain slow-generation variance
  and verify effective context budgeting before broader approval/recovery work.
- The canonical LM Studio record also retains `parallel=4`; the current Codex
  catalog adapter has no verified mapping for that provider value and does not
  infer Codex parallel tool-call support from it. If runtime concurrency
  controls are required, qualify their semantics separately.

## Later details intentionally deferred

Candidate drawer interaction, dynamic native registration, packaging/update
signing, migration implementation, weekly-check notification mechanics,
Android/hosted/multi-platform expansion, and public distribution remain later
work. They must not be used to expand Phase 1A or to reopen settled choices
without concrete contradictory evidence.
