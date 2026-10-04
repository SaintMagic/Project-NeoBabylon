# Finishing the Product — Implementation Plan

> **For agentic workers:** Use `superpowers:subagent-driven-development` or
> `superpowers:executing-plans` to execute this plan task by task. Do not
> create Git commits; none are authorized.

**Goal:** Finish the active, incomplete NeoBabylon product scope with evidence
that distinguishes verified behavior, known limitations, and deferred work.

**Current execution instruction — 2026-10-03:** Martin requested completion of
the remaining features and UI first, with behavioral tests and live
qualification afterward. Compilation and typechecking remain part of
implementation. The coordinator reviews and delegates implementation. This
changes execution order, not the acceptance criteria or the still-closed
generated-tool activation gate.

**Implementation checkpoint — 2026-10-04:** remaining Core/Host/React source
integration is implemented and source-reviewed; development desktop artifacts
and the private Node pin are present. The current runtime was rebuilt from
reviewed source and locked at `a0c3ebdc…54386`, preserving the prior binary.
Tasks 8/9's review/lifecycle/UI and Task 10's fixed-record recovery mechanism
are implemented, not behaviorally accepted. Tasks 1–6/7/10/11 retain their
remaining native/live/test/performance gates for the subsequent test pass.
There is no authority to enable callable generated tools by treating a build
as qualification. Final source capture/readback and content comparison are
the last handoff step; see the
[checkpoint](../history/2026-10-04-feature-ui-build-checkpoint.md).

**Architecture:** Keep the accepted thin WPF/.NET + WebView2 host around the
React/TypeScript/Astryx UI, with the pinned Codex App Server owning agent
execution and durable thread history. Finish the remaining client, manual
candidate workflow, and protected-record recovery implementation first. Then
qualify the remaining P2, P3, P4, and active P5 gates against that final build.

**Tech Stack:** C#/.NET WPF, WebView2, React, TypeScript, Astryx, Rust/Cargo
Codex App Server, PowerShell QA harnesses, Playwright, and deterministic
loopback Responses fixtures.

**Spec:** [Phase 2 vertical slices](PHASE2_VERTICAL_SLICES.md),
[Phase 3–5 vertical slices](PHASE3-5_VERTICAL_SLICES.md),
[initial build plan](INITIAL_BUILD_PLAN.md),
[accepted decisions](../decisions/README.md), and
[current status](../product/STATUS.md).

## Global Constraints

- This is an umbrella finish plan; the linked phase maps remain the detailed
  implementation specifications and acceptance authority.
- Execute only incomplete, non-deferred scope. Do not reschedule completed
  slices or change any accepted decision through implementation.
- The accepted execution path is `danger-full-access` / `never`; it is full
  Windows-user authority, not workspace containment. Do not qualify or imply
  containment.
- `<NeoBabylon root>\Data` means the application root, not the source root;
  keep runtime state isolated from ordinary Codex state.
- Codex App Server remains the execution/history authority. Keep the host
  bridge named and validated; do not add generic renderer RPC or use
  `thread/shellCommand` for model-controlled tools.
- Do not silently substitute a provider, model, endpoint, route, capability,
  permission, or unsupported transport. Preserve `Unknown` where evidence is
  unavailable.
- Ordinary output from unrestricted tools remains visible and forwarded under
  NB-DEC-011. Do not promise confidentiality for data an unrestricted tool can
  independently read.
- Keep QA data and build outputs in unique isolated locations. Do not modify
  shared Codex/provider configuration or expose credentials in artifacts.
- No system-wide dependency installation, remote repository, Git history
  mutation, commit, push, merge, tag, or package/install work is authorized by
  this plan.
- Label source inspection, deterministic fixtures, live-provider results, and
  unverified expectations separately. A build is not an acceptance result.
- Preserve full before/after source-content snapshots for both repositories;
  a list of hashes alone cannot support review of an all-untracked product
  tree. No snapshot retroactively proves work that preceded it.

## Review Focus

- Stale or mismatched source/runtime identity must remain non-executable and
  must never silently replace the locked App Server.
- Incomplete, failed, or interrupted output must remain visibly incomplete;
  output and retention bounds must be explicit rather than described as
  arbitrary-size support.
- Missing route, capability, token-usage, cost, or screen-reader evidence must
  stay `Unknown`, not be inferred from a successful HTTP status or UI render.
- Restart, delayed tool completion, approval, and concurrent workspace edits
  must preserve exact thread/turn/item identity and report limitations without
  implying transactional file protection.
- Candidate review must be invalidated when any reviewed implementation,
  manifest, permission, dependency, evidence, or runtime-interface input
  changes.

---

## What the linked review changes—and what it does not

The linked “Review findings” conversation is advisory source material, not a
new product decision. Its conclusions reconcile as follows:

| Finding | Current evidence | Plan treatment |
|---|---|---|
| Product source has no usable Git baseline | Confirmed: the product directory is a Git worktree but `HEAD` does not resolve; current files are untracked. | Preserve complete source-content snapshots and sorted hash manifests before further implementation and after code freeze. The first snapshot is post-P2-10 delegated work, not a retroactive pre-P2-10 baseline. Do not create Git history without authorization. |
| Current runtime source-to-binary provenance is incomplete | The earlier `dev` comparison concerned the former lock (`9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d`) and a different QA build (`6a0eff65e59e89968e264f633725c4c71a2e1c5bced3b7582281cf1ccf28b019`). The 2026-09-26 cutover now locks the source-linked candidate at `5a48fe628c0654971da58542b65e33ac64cc7767e889d580fddb34c527fa807b`; final host-launch provenance remains open. | Certify after all runtime-changing work: trace reviewed source/build inputs through the lock to the final host-launched executable. Preserve the earlier comparison as historical evidence. |
| Broad upstream App Server test result needs a baseline comparison | Confirmed. The current-source run remains 1,170 passed / 25 failed / 11 ignored. The 18 helper-dependent cases passed focused retries, but the broad run was not repeated with helpers present; five shell/environment and two Guardian cases remain classified. | Repeat the broad run with helpers, compare remaining cases to the exact clean pinned upstream revision under the same environment, and report regressions separately from baseline/environment failures. Do not call the suite green by subtracting focused results. |
| Application `Data` root is contradictory | NB-DEC-005 already accepts `<application root>\Data`; P5-02 wording is corrected. The source-linked host default and packaged-root discovery still need separate alignment/qualification. | Do not reopen the root choice. Qualify development/packaged root discovery only where in active scope, backup, migration, and recovery. |
| LM Studio needs a 5–10 run repeatability campaign | The old ten-minute non-completions are real evidence, but the remaining Phase 1B repeatability/usage/recovery qualifications are explicitly deferred. | Do not add that campaign to the active finish queue or describe the provider as reliability-qualified. |
| P2-11 should use bounded rather than arbitrary-size claims | Valid. Existing native stress is finite; pinned Codex has no output-token field today; native cost and provider-served route evidence remain unavailable for some runs. | Define enforceable request/output/retention bounds and truthful overflow behavior. A numeric completion cap is not settled and must not be guessed. Distinguish request-side route pinning from provider-side attribution and provider-reported cost from guaranteed cost. |
| Phase 4 approval should cover more than source bytes | Useful implementation proposal consistent with the existing requirement to retain permissions, dependencies, provenance, and evidence. | Include a content identity over those inputs as a proposed implementation detail; edits invalidate the prior review. Do not present it as a previously accepted ADR. |
| Phase 4 has no qualified approved-tool invocation path | Confirmed in current product scope: `thread/start.dynamicTools` is experimental and not exposed by the stable NeoBabylon bridge; MCP management and selected-tuple qualification are absent. Ordinary authorized tools can use temporary unapproved helpers, but that does not establish a named approved-tool registration lifecycle. | Implement non-callable candidate/review groundwork first; qualify one exact callable path, including activation and revocation, before enabling activation. Neither dynamicTools nor MCP is accepted merely because pinned source contains it. |
| Add a machine-readable status ledger | Potentially useful, but not an existing requirement or acceptance gate. | Not included in this finish plan. Revisit only if ongoing evidence reconciliation continues to drift materially. |

The review does not call for an architecture redesign. The accepted decisions
remain authoritative; implementation proposals below are labeled separately.

## Completion sequence

### Task 0 — Preserve the current source baseline before further implementation

**State:** Initial snapshot captured and fully read back on 2026-09-26;
post-implementation snapshot and content comparison remain open. P2-10
delegated implementation preceded this baseline, so its label is
**post-P2-10 / pre-further implementation**.

**Files/areas:** Both separate local repositories (runtime now nested under `.local/Runtime`), a unique isolated QA output
root, and a read-only inventory of generated-output exclusions.

- [x] Preserve actual contents of both product and runtime source trees,
  including relevant untracked files, in independently readable archives or
  copies outside the source trees. A hash list alone is insufficient.
- [x] Produce sorted per-file relative path, byte length, and SHA-256 manifests
  using the same inclusion rules for both snapshots. Exclude only recorded
  generated caches/build outputs and Git administrative data; record the exact
  exclusions and separately identify binary/lock artifacts.
- [x] Hash the snapshots themselves and verify extraction/readback against
  their manifests. Record the snapshot time and any files changing during
  capture; retry a non-atomic capture rather than claiming a stable baseline.
- [ ] Repeat with identical rules after the last implementation edit and
  before final review. Compare changed/added/removed contents, not only path
  names, and inspect the complete relevant source difference. Preserve the
  earlier P2-10 agent report as its own boundary; do not claim this snapshot
  proves its pre-edit state.

**Initial artifact:**
`.local/QA/Source-Snapshot-20260926-155823-757f70c7` contains verified
product (201 files) and runtime (7,631 files) ZIPs, sorted path/size/SHA-256
manifests, and exact exclusion metadata. The snapshot excludes `.local`, Git
administration, generated outputs, and credential-pattern files; no concurrent
source changes were detected. This is a source baseline, not a snapshot of
the historical Data/QA tree.

**Acceptance:** two complete, readable source-content snapshots and verified
manifests enable a meaningful review of every change made after the first
capture, without Git commits or invented pre-P2-10 history.

### Task 1 — Diagnose P2-13 runtime provenance and baseline evidence

**State:** In progress; the isolated compile completed, but provenance and
suite acceptance remain open. This task gathers evidence; it does not certify
the final runtime before Task 3 or later runtime-changing work.

**Files/areas:** `runtime/runtime-lock.json` (read-only unless an independently
reviewed replacement is justified), current `NeoBabylon-Runtime` source,
`docs/release/VERIFICATION.md`, `docs/product/STATUS.md`,
`docs/WIP/PHASE2_VERTICAL_SLICES.md`, and isolated QA output under
`NeoBabylon-Data\QA`.

- [ ] Use Task 0's source-content snapshot and manifest as the starting
  baseline; record any runtime-tree changes after it.
- [ ] Record upstream revision, complete runtime-tree digest, Cargo lockfile
  digest, Rust/Cargo versions, target and relevant build environment, exact
  command, target directory, build outcome, and output executable hash.
- [ ] Review the exact current runtime diff against the accepted upstream
  revision. Confirm every patch is tied to an accepted requirement and test;
  identify unrelated or unexplained paths without discarding or reverting
  user changes.
- [ ] Investigate the build-input/flag difference between the isolated QA
  output and the currently locked executable. Do not treat a rebuilt Binary A
  as verification of the host-launched Binary B, and do not replace the lock
  merely because a different current-source build succeeded.
- [ ] Build all required integration-test helper binaries into isolated test
  targets, rerun the complete current-source App Server suite, and preserve
  its raw log and exact counts.
- [ ] Run the five remaining shell/environment cases and two Guardian
  assertions against pristine revision
  `be2951ea34f0d295ed0becf97079f92fa5f6950e` with the same relevant Windows
  environment and helper setup. If a complete baseline suite is needed to
  resolve attribution, run it rather than extrapolating from focused cases.
- [ ] Reconcile the comparison using “no NeoBabylon-only regressions” as a
  proposed acceptance rule; retain baseline failures as failures, not passes.
  Record any residual uncertainty and leave the P2-13 gate open if attribution
  is still incomplete.
- [ ] Refresh the UI suite, typecheck/build, and affected ApprovalQA host suite
  only where this work changes their inputs; keep current two-run 86/86 host
  evidence as dated evidence rather than relabeling it as an upstream suite.

**Acceptance:** the build/lock mismatch and broad-suite failures are
classified with retained evidence. Final source-to-launched-binary
certification is deliberately reserved for Task 4 after runtime edits.

### Task 2 — Finish P2-10 native accessibility acceptance

**State:** Partially verified; native subsets and nine synthetic browser flows
pass, but the full native flow set does not. On 2026-09-26 Martin explicitly
removed Narrator evaluation from the requested acceptance scope; this is a
scope decision, not a passing screen-reader result.

**Files/areas:** `tests/qa/ui-accessibility.mjs`,
`tests/qa/native-visual-acceptance.mjs`, focused native QA fixtures, and UI
components only if a failing assertion demonstrates a product defect.

- [ ] Map each of the nine existing scripted journeys to a native WPF/WebView2
  test and identify which are already covered by
  `native-history-pagination.mjs`, `native-visual-acceptance.mjs`,
  `native-turn-interruption.mjs`, or the native approval/review fixtures.
- [ ] Add only the missing native checks, preserving the existing Codex-first
  layout and neutral charcoal/black appearance.
- [ ] Run all nine journeys on the native host at the existing supported
  desktop/compact layouts. Assert named roles, keyboard-only reachability,
  visible focus, modal inertness/return, correct task/model attribution, and
  no replay or duplicate request.
- [ ] Fix only verified accessibility defects and rerun the affected native
  flows plus UI tests/build.

**Acceptance:** all nine flows pass in native WPF/WebView2. Do not claim a
Narrator or broader screen-reader qualification from keyboard or AX-tree
evidence; Narrator work is explicitly outside this accepted gate.

### Task 3 — Close P2-11 with bounded output and truthful provider metadata

**State:** Deterministic large-output and one native live NEX render pass;
hard output ceiling, native request cost, and independent served-route evidence
remain open.

**Files/areas:** `codex-rs` Responses request/HTTP/WebSocket transport and
tests if the focused adaptation is justified; NeoBabylon capability/config
record and request projection; `tests/qa/native-large-output.mjs` and
`tests/qa/native-live-nex-output.mjs`; P2-11 status and verification records.

- [x] Martin selected the advertised provider maximum completion tokens as
  the default, or 32,768 when that value is unavailable, with a user override.
  Keep this separate from context size. Enforcement source is present;
  native verification is deferred to the final test pass.
- [ ] **Proposed implementation choice:** carry an explicit completion limit
  separately from model context and tool-output retention. Serialize it on
  each supported native Responses transport; if a transport cannot enforce the
  selected contract, disable it for that configuration or fail before
  inference rather than silently dropping the limit.
- [ ] Preserve stream cancellation, typed partial failure, transcript paging,
  saved-history authority, and explicit omitted-range/unavailable states.
  State the request, retained UI/history, and overflow limits separately.
- [ ] Preserve and surface provider metadata only when returned. Test exact
  configured NEX model/route with no fallback; capture generation/route/usage
  evidence if supplied. Report request-side route pin separately from
  provider-side serving attribution. `Unknown` is an acceptable honest result
  where the provider does not return evidence.
- [ ] Report provider-reported usage/cost as such. Do not claim a guaranteed
  financial ceiling unless the exact request path enforces it and reliable
  pricing/usage evidence exists; a token ceiling alone is not a billing
  guarantee.
- [ ] Run deterministic/native cap, overflow, cancellation, partial-failure,
  history-reconstruction, and no-fallback tests. Run one bounded live exact
  tuple test only if its current model/route remains available; never substitute
  a different tuple.

**Acceptance:** the selected request bound is actually sent/enforced on the
qualified path, all overflow/failure states are visible and attributed, and
route, usage, and cost claims match evidence. The cap value is settled; source
compilation and final behavioral qualification remain separate steps. This
task is not passed.

### Task 4 — Certify the final locked and launched App Server

**State:** Reviewed source/build receipts and versioned lock cutover now exist;
final native source-to-launched identity and behavior remain unqualified. Run
those checks after every runtime-changing edit, not before.
If Phase 4 later changes the runtime, repeat this gate at final code freeze.

**Files/areas:** Reviewed runtime source snapshot, exact build inputs and
receipt, `runtime/runtime-lock.json`, host launch/diagnostics evidence, and
isolated App Server/native QA results.

- [ ] Review the complete runtime change set since Task 0, including relevant
  untracked files. Build from that reviewed source with recorded lockfile,
  toolchain, flags, environment, and target; retain its output hash.
- [ ] Establish a verifiable source/build chain for the **exact executable in
  the product lock**. Hash the locked path on disk and independently observe
  the executable path of the App Server process the WPF host actually starts;
  hash that path while the host run is attributable to this lock. All three
  identities must agree. A test of a separate QA binary does not qualify the
  locked/launched binary.
- [ ] If the currently locked binary cannot be traced to reviewed source and
  build inputs, keep the gate open until a reviewed build deliberately replaces
  the lock/launch target. Record the old/new hashes, update the lock and any
  affected evidence together, then restart the host and rerun affected native
  tests against the newly locked executable. Never silently overwrite the
  existing binary or equate source similarity with binary identity.
- [ ] Re-run the broad App Server suite and affected host/UI/native checks
  against the final source and locked/launched binary as applicable. Preserve
  upstream baseline failures and explicit environment gaps rather than
  relabeling them as passes.

**Acceptance:** reviewed source and build receipt lead to the exact lock hash,
the WPF-launched App Server path/hash matches that lock, and affected tests use
that identity. Re-run this gate after any later runtime or lock change.

### Task 5 — Reconcile and close remaining active Phase 2 slice rows

**State:** Narrow slices P2-01 through P2-09 and P2-12 have substantial passing
evidence; their broad residuals must not be confused with the completed narrow
journeys.

**Files/areas:** the individual P2 slices, their owning tests, and the final
P2 checklist in `docs/WIP/PHASE2_VERTICAL_SLICES.md`.

- [ ] Re-audit each P2-01 through P2-09 row against its stated user outcome
  and the latest native/host results. Carry forward only a concrete unmet
  Phase 2 outcome, not a request for indefinite stress testing.
- [ ] Recheck exact capability-binding mismatch behavior, pending-send no
  replay, project-bound history/list-start ordering, turn interruption and
  partial-output presentation, and saved review/approval identity on the
  Task 4-certified locked runtime where the owning test or runtime changed.
- [ ] For command and file-change approvals, finish only deterministic
  Phase 2 outcomes within the accepted `danger-full-access` / `never` policy.
  Keep the deferred live-provider approval qualification and containment work
  out of this queue.
- [ ] Preserve the explicit limitations already accepted for loaded-page
  search and upstream patch-apply races. Do not turn optional server-wide
  search, transactional workspace snapshots, or containment into silent new
  acceptance criteria.
- [ ] Record each open item as passed with evidence, a documented limitation,
  or a still-open blocker. Do not erase historical failed or partial runs.

**Acceptance:** every active P2 row is either satisfied at its written scope or
explicitly represented as an evidence-backed limitation/blocker, with all
  deferred Phase 1B rows still visibly deferred. Proceed to Phase 3 only after
  P2-10, P2-11, and P2-13 (including Task 4) are genuinely closed or Martin
  changes the bar.

### Task 6 — Complete P3-02 per-operation failure and attribution evidence

**State:** In progress; catalog, normal output, typed tool failure, partial
output, and identity-bound command Stop have bounded evidence. The general
denial/timeout/cancellation/partial-output matrix remains open.

**Files/areas:** `docs/release/P3-02_EXPOSED_SURFACE_EVIDENCE.md`, host
operation/bridge result records, tool activity UI, and owning host/UI/native
fixtures.

- [ ] Derive the matrix from the exact operations actually callable in the
  pinned product. Keep catalog-only, experimental, unknown, or unsupported
  categories non-callable; do not expand the bridge.
- [ ] For each callable operation, test success, denial, timeout or
  cancellation where supported, partial failure/output, terminal status,
  attribution, and stale-identity rejection. Reuse existing command Stop
  cases; add no redundant success-only run.
- [ ] Verify normal output is inertly rendered and forwarded, while direct
  host credential/config serialization remains absent. Do not claim that
  unrestricted commands cannot read user-accessible secrets.
- [ ] Verify command Stop is bound to the exact turn/item/process and App
  Server epoch, and distinguishes turn Stop from command Stop, natural exit,
  stale binding, duplicate action, and lost transport.
- [ ] Run the complete affected UI/ApprovalQA host suites and native fixtures;
  record any operation lacking a faithful deterministic failure surface as
  `Unknown`/unqualified, not passed.

**Acceptance:** every exposed callable operation has evidence for its supported
failure contract and exact attribution; unqualified operations are not exposed
as callable, with no blanket Phase 3 pass based on the catalog alone.

### Task 7 — Decide and qualify how an approved tool becomes callable

**Accepted manual-act boundary:** [NB-DEC-012](../decisions/0012-explicit-phase4-activation.md)
requires a separate explicit NeoBabylon Activate confirmation showing the
exact currently reviewed identity and requested permissions before a candidate
can become callable. Review and Prepare disabled grant no callable access.
This settles the manual act only: the app-private stdio MCP route is still
provisional and the route/activation behavior is not accepted. A separate
activation UI is implemented but remains disabled by the current source gate.
Current selected authority is full Windows
user access; containment remains deferred.

**State:** Deterministic app-private stdio MCP call/reload/revocation probe
passed against the then-locked App Server on 2026-09-26. An earlier offline named-tool
round trip also passed for its recorded older binary, but its default-path probe
checked discovery only. App-private stdio MCP remains provisional; the product
route decision, current-lock revocation qualification, and exact selected real
provider/model round trip remain open. Implement the bounded mechanism first;
keep callable activation disabled until final qualification and decision.

**2026-09-27 source-only implementation candidate (unverified):** Core now has
a standalone product-owned stdio adapter that advertises an empty tool list and
rejects direct calls. The isolated config builder can opt in to one validated,
app-root `Adapters` executable with `enabled = false` and an empty enabled-tool
allow-list; the default config is unchanged. The host's exact named
`stageGeneratedToolDisabledMcp` operation accepts request ID, tool ID, candidate
content identity, review identity, and current prepared-binding record hash
only. It re-reads the current prepared-disabled binding, chooses the fixed
adapter path itself, and keeps one process-local selection; active client/turn
or stale binding fails closed. Both App Server config-write sites receive that
opt-in. A later thread start/resume/fork requests typed MCP status: only an
explicit disabled status with empty inventory is reported as
`ConfirmedDisabledEmpty`; inconclusive status is `Unknown`, and observed tools
stop the client. Staging itself reports runtime confirmation `Unknown`. There
is no runtime enable or reload invoked by Stage, callable registration,
model-visible generated tool, or Activate operation. This candidate has focused
unrun test code, not a build, native run, live request, or Task 7
qualification. Config and model catalog publication are not one transaction.
The final **current-lock**
deterministic call/reload/revocation/restart gate and exact selected Stealth
`stealth/space-bunny-alpha` live provider/route round trip remain open; neither
anonymous endpoint metadata nor a zero-price listing bounds native request
cost.

**Files/areas:** Pinned App Server protocol/implementation, stable NeoBabylon
bridge, selected provider/model capability record, Phase 4 architecture and
decision ledger, and isolated deterministic/native qualification fixtures.

- [ ] Compare the exact feasible paths: a local MCP server, experimental
  `thread/start.dynamicTools` plus `item/tool/call`, and any ordinary-tool
  handoff that could meet the intended *approved callable tool* contract.
  Distinguish temporary unapproved helper use from registering a named,
  approved tool. No generic arbitrary RPC or model-controlled
  `thread/shellCommand` is an acceptable shortcut.
- [ ] **Provisional implementation path, not yet an accepted choice:** a local
  app-private stdio MCP server. Pinned source has named MCP configuration and
  a stable reload operation, while dynamic-tool registration is experimental
  and not exposed by the current stable product bridge. Implement only the
  named, host-controlled path and fail-closed activation boundary for now.
  This is not Windows containment; current tools retain full user authority.
- [ ] Choose one path with a concrete invocation contract: who registers the
  tool, what the model sees, the schema and version, when activation takes
  effect, how an exact call reaches the reviewed implementation, and how
  disable/revocation survives restart and fails closed. Record permission,
  credential, isolation, and failure/attribution boundaries. The separate
  human Activate confirmation and its identity/permission display are settled
  by NB-DEC-012; the callable mechanism and enforcement remain open.
- [ ] After implementation is finished, prove registration, exact
  call/response, disabled/revoked reload, restart, and controlled subprocess
  environment with the **final current-locked App Server** in a deterministic
  fixture. Then qualify the exact selected real provider/model tuple with one
  bounded named-tool round trip, without substitution. Until both gates pass,
  activation stays disabled; a failed route remains unqualified. Experimental
  protocol use requires an explicit reviewed product decision.
- [ ] Record the chosen and rejected mechanisms, evidence, limitations, and
  accepted decision in the project ledger. If meeting the contract would
  materially change an accepted authority boundary, seek Martin's decision
  before implementation. Do not enable or claim approved callable activation
  around an unproven mechanism.

**Deterministic probe evidence (2026-09-26):** an isolated QA-only stdio MCP
server was configured under an isolated Codex home and exercised with the
then-locked App Server SHA-256 `9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d`.
The first loopback Responses request advertised the named tool; the synthetic
call reached the server and its result appeared in the second request. After
`enabled = false` and stable `config/mcpServer/reload`, the same thread omitted
the tool and direct calls were rejected. A restarted App Server kept it
disabled on resumed and new threads. A fresh local rerun exited 0 with five
fixture requests and no recorded probe errors. The throwaway fixture and
trace are under `.local/Lab/Runs/P4-Route-Probe-5a362283d7f6/`; see the
dated verification record. This tests neither in-flight-call revocation nor
the exact real provider/model tuple, and it does not establish Windows
containment. No product registration/activation operation was added.

**Acceptance:** one exact callable route is chosen, demonstrated against the
final current-locked runtime in both deterministic and selected-real-tuple
tests, and has a documented activation/revocation contract. Unknown provider
support remains Unknown. Implementation may precede these deferred tests, but
no callable activation or Phase 4 acceptance may be claimed while this gate
is open.

### Task 8 — Deliver the Phase 4 candidate envelope and durable drawer

**State:** Candidate envelope/drawer source is implemented and compiled;
retained review and activation controls are integrated. Native/live behavior
and full Phase 4 acceptance remain unqualified.
Non-executing candidate envelope and drawer implementation may precede Task 7
qualification; final acceptance still requires the relevant Phase 2/P3
permission/source evidence and Task 7's qualified route. No candidate becomes
callable through this groundwork.

**Files/areas:** existing `Data\GeneratedTools` boundaries, candidate record
and validation in `host/NeoBabylon.Core`, named host operations, React
Unapproved Tools drawer, and isolated restart tests.

**2026-09-27 source-only reconciliation:** `GeneratedToolCandidate` and its
store now define a bounded, versioned unapproved envelope under application
`Data\GeneratedTools`, including declared origin, invocation/input/output
contract, authority, dependencies, evidence, file hashes, and content identity.
The named host listing projects that validated contract as inert strings; the
React drawer shows the envelope and uses a bounded, identity-bound source-file
read operation. Focused test code exists, but the deferred tests, builds,
restart, native, and live checks have not been run for this candidate. This
does not qualify permissions, durability, or the Task 8 acceptance gate.

- [ ] Define the versioned candidate manifest from the existing Phase 4
  requirements: provenance, purpose, permissions/data flow, dependencies,
  evidence, actual path, and content identity. Candidate text is data, never
  host instruction.
- [ ] Reject missing/invalid authority data, credentials, and paths outside
  the application `Data\GeneratedTools` root. Do not add a bespoke executor
  or global Codex configuration.
- [ ] Persist explicit candidates and their review state under isolated
  application Data; after implementation, prove restart/reopen and unchanged
  authority.
- [ ] Show provenance, state, permission, dependencies, evidence, path, and
  content identity through the existing named bridge only.

**Acceptance:** malformed/out-of-root/credential-bearing records fail closed;
an unapproved candidate survives restart and remains non-active, with no new
execution authority.

### Task 9 — Bind manual review to the complete candidate and lifecycle

**Accepted transition boundary:** [NB-DEC-012](../decisions/0012-explicit-phase4-activation.md)
requires a later, separate explicit NeoBabylon Activate confirmation showing
the exact reviewed identity and requested permissions. Review, Reject, and
Prepare disabled are not callable activation. Separate Activate confirmation
and lifecycle source are implemented; callable registration remains disabled
until Task 7's route and exact tuple qualification/acceptance.

**State:** Retained review snapshots, bounded comparison, exact-identity manual
activation/revocation/history and Host/UI integration are implemented and
source-reviewed. Behavioral acceptance remains open. Task 7 remains provisional. Callable activation remains
disabled until Task 7's final current-lock and exact selected-real-tuple
qualification passes and its route is accepted.

**Files/areas:** candidate review record, content comparison, explicit
integration/activation/rejection/revocation/cleanup operations, UI, and
restart-safe tests.

**2026-09-27 source-only reconciliation (unverified candidate):**
`GeneratedToolReviewStore` records content-identity-bound Review or Reject
decisions and retains hash-linked
history; the named host bridge returns host-attributed results and the React
drawer exposes separate review, rejection, history, and source-inspection
paths. The UI requires a displayed contract and explicit note for a
product-mediated local decision. These records
are not independent proof of human identity or a tool authority grant. A
separate `GeneratedToolIntegrationStore` source candidate now provides
prepared-disabled binding, revocation, and logical cleanup records bound to a
current review. Named host history/current-read and disabled lifecycle
operations, plus corresponding React drawer controls, are source-present but
unverified. A separate recovery drawer can list binding tool IDs and history
without candidate validation and offers only exact-hash Revoke/Cleanup, not
Prepare or Activate. The normal candidate drawer also presents a distinct
confirmation for the named Stage-disabled operation using the current content,
review, and binding identities. Stage may write only a disabled, empty-inventory
app-private MCP configuration; it is not callable activation. There is no
callable route, Activate action, or generated-tool execution method. Focused
test code exists; Task 9 tests/builds/native/live checks remain deferred and
unverified. None of this satisfies the remaining
lifecycle or Task 9 gate.

- [ ] Display a reviewable change and evidence, and require a distinct manual
  action for review and for any later lifecycle transition.
- [ ] **Proposed implementation choice:** compute review identity over the
  implementation bytes plus manifest/tool schema, declared permissions and
  data flow, dependency lock/material, evidence identity, and runtime
  interface version. Any changed/replaced input invalidates approval.
- [ ] Implement integration, review-state, rejection, revocation, and cleanup
  as separate explicit transitions with durable evidence. Activation code may
  be prepared behind a disabled gate pending Task 7 acceptance; only a later
  separate NeoBabylon Activate confirmation of exact reviewed identity and
  requested permissions may authorize callable activation. Reject duplicate,
  stale, and invalid transitions.
- [ ] Prove candidates cannot self-promote, widen their permissions, erase
  their review trail, or invoke `thread/shellCommand`. Cleanup must target only
  the selected candidate, never unrelated user data.
- [ ] After implementation is finished, run restart-safe native tests for each
  transition and failure case, including the activation gate.

**Acceptance:** every transition is explicit, review is bound to the complete
content/authority envelope, edits invalidate it, and no candidate bypasses
the existing App Server permission path. Review or prepared-disabled binding
cannot substitute for Activate. Coding review-state groundwork is not verified
acceptance or authorization to activate a callable tool.

### Task 10 — Close P5-02 protected Data migration and downgrade behavior

**State:** Protected replacement/journal preservation and fixed-key verified
backup listing/missing-only restore are implemented through Core/Host/UI.
Compilation/source review do not close backup/restore, interrupted migration,
storage/permission failure or downgrade behavioral acceptance.

**2026-09-26 implementation candidate (not verified):** Core now protects
replacement of mutable NeoBabylon-owned versioned records with read-back
backup, staged replacement, and digest-journal recovery. The current schema
remains v1; unknown/future versions fail closed. Isolated test code was added
but execution is deferred by Martin, so no P5-02 acceptance claim follows.
`Data\CodexHome`/credentials and other runtime-owned Data are deliberately
excluded; their consistent backup requires a separate quiescence design.

**Files/areas:** isolated application Data layout, project/config registry
versioning, migration and backup code, and fixture-root host tests.

- [x] Correct the stale P5-02 statement that calls the production Data root
  unresolved. NB-DEC-005 is settled: durable state belongs under
  `<application root>\Data`; package/install discovery is a separate concern.
- [ ] Keep replaceable application files separate from durable Data. Define
  version validation, protected pre-migration backup, restore verification,
  interruption recovery, and non-destructive downgrade behavior for the
  current development/fixture roots.
- [ ] Add isolated integration tests for successful backup/restore,
  interruption, corrupt/unknown schema, insufficient storage/permission, and
  downgrade. Preserve original bytes/data when migration fails.
- [ ] Keep logs and diagnostics free of direct credentials; do not claim
  unrestricted same-user tools cannot independently read accessible files.

**Acceptance:** failures never reset/overwrite user Data; successful and
interrupted transitions recover deterministically, and the application-root
decision is reflected consistently in canonical docs.

### Task 11 — Establish a reproducible bounded performance/soak result

**State:** Not started; existing stream/memory samples are observations, not a
repeatable performance qualification.

**Files/areas:** isolated QA harness, `docs/release/VERIFICATION.md`, and the
product status/feature qualification records.

- [ ] Specify one bounded local profile with exact app/runtime, fixture or
  exact provider/model tuple, hardware, operation mix, duration, sampling
  method, latency/memory metrics, and restart/cancellation cases. Capture a
  reproducible baseline without calling it pass/fail.
- [ ] Propose concrete numeric latency/memory/stability thresholds with the
  measured baseline, intended hardware/profile, and tradeoffs. Obtain
  **Martin's explicit approval** of the thresholds before issuing a pass
  verdict; agents must not invent or self-approve what is good enough.
- [ ] Run repeatable loopback tests first. Add a live-provider profile only
  when the exact selected tuple is available and the request is within the
  accepted cost/bound contract.
- [ ] Retain raw metrics and distinguish fixture from live inference. Verify
  cancellation and cleanup of only the QA-owned host/App Server processes.

**Acceptance:** repeated bounded runs meet Martin-approved thresholds with
complete environment and runtime identity recorded. Until approval, report
measurements only, not a pass; make no arbitrary-size or general service-level
claim.

## Final product gate

- [ ] Task 0's before/after source-content snapshots are readable and their
  manifests verified. Review every changed/added/removed source file.
- [ ] Tasks 1–5 close the active Phase 2 gate without changing deferred Phase
  1B status; Task 4 follows all Phase 2 runtime edits.
- [ ] Task 6 closes Phase 3 only for the exact exposed operations and their
  evidence.
- [ ] Tasks 7–9 close Phase 4 with one qualified callable route and a manual,
  restart-safe, non-self-promoting candidate lifecycle.
- [ ] Tasks 10–11 close the active Phase 5 data-recovery and bounded-performance
  scope; packaging/install is not a prerequisite in this plan.
- [ ] After **any** runtime/lock change in Tasks 6–9 or other late work,
  repeat Task 4 against the final host-launched executable, not a parallel QA
  build. Capture the final Task 0 snapshot only after that code freeze.
- [ ] Refresh `STATUS.md`, `ROADMAP.md`, `FEATURE_MATRIX.md`, and
  `VERIFICATION.md` once from the accepted task evidence. Preserve the
  distinction between complete, partial, unknown, and deferred; do not
  duplicate stale prose across documents.
- [ ] Adversarially review the complete Task 0 source-snapshot difference and
  current untracked additions, confirm no unrelated edits were included, and
  run the full affected UI and host checks. Leave all changes uncommitted.

## Execution notes

- Preserve the existing Task 0 baseline and capture the final source snapshot
  after implementation. Task 1's broad-suite comparison and Task 2's native
  evaluation belong to the final test pass. Task 3's cap/default is settled
  as described above.
- Task 4 is the post-runtime-edit identity/verification gate; Task 5 is the
  P2 reconciliation step, not permission to reopen deferred Phase 1B work.
  Formal Phase 3 acceptance still follows the active P2 gate. Martin's
  2026-10-03 instruction permits remaining source implementation before those
  delayed qualification runs.
- Complete the bounded Task 7–9 implementation first; defer new tests until
  afterward. Task 8's non-executing candidate envelope/drawer and Task 9's
  review-state groundwork may proceed before Task 7 qualification, using
  `Unknown` where Task 6 permission/source evidence is still open. Task 9
  follows Task 8's envelope. Keep activation disabled until the final
  current-locked-binary deterministic and exact selected-real-tuple checks
  pass and the callable route is accepted. Phase 5 data and measurement work
  can proceed independently after their input contracts are recorded, but
  Task 11 cannot pass without Martin's threshold approval.
- Implementation agents must use unique QA output roots, preserve all dirty
  work, make no commits, and report exact files, commands, results, and
  remaining gaps. The coordinator reviews and delegates; it does not make
  implementation edits.
