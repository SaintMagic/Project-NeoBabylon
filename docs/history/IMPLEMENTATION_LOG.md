# Implementation log

## 2026-09-22 — Phase 0 foundation

Status: **Complete for review.**

- Read `NEOBABYLON_STARTUP.md` completely and preserved an exact copy in this
  history folder.
- Confirmed the selected workspace was `D:\CODING\NeoBabylon`; it was not a
  Git repository, so no repository was initialized.
- Inspected CanonWell only as a live, read-only documentation-structure
  reference.
- Created the categorized project, documentation, test, reference, artifact,
  and maintenance-script structure.
- Acquired Codex stable `rust-v0.155.1`, resolved commit
  `be2951ea34f0d295ed0becf97079f92fa5f6950e`, Windows x64 App Server and CLI
  binaries, official docs, and comparison captures.
- Generated stable and experimental App Server TypeScript/JSON Schema outputs
  with the matching CLI under isolated project-local data.
- Inspected upstream transport, protocol, lifecycle, persistence, providers,
  authority, toolbox, extension, and dynamic-tool implementation paths.
- Wrote the feature matrix, architecture/provider/tool strategy, source
  ownership recommendation, open questions, verification record, and phased
  build plan.
- Completed an independent advisory architecture review and folded its
  material corrections into the plan: full-host shell-command restriction,
  candidate promotion states, stable/experimental protocol separation, runtime
  identity/data-root gates, narrow renderer authority, and exact provider
  qualification.

No production feature, runtime change, system install, shared configuration
change, remote repository, commit, or push was performed.

## 2026-09-22 — Authorized Phase 1A diagnostic path

Status: **Evidence complete for review; ordinary-tool success gate blocked.**

- Created the authorized local sibling repositories `NeoBabylon` and
  `NeoBabylon-Runtime`; retained the pinned upstream Codex ancestry and did
  not create a remote NeoBabylon repository.
- Added the product-side runtime lock and exact App Server binary identity,
  with SHA-256 verification before launch.
- Added a thin WPF/.NET + WebView2 diagnostic host, a named product bridge,
  isolated application/Data roots, deterministic Responses fixtures, and
  host/qualification tests. React/TypeScript/Astryx remains the UI direction;
  no polished production UI was attempted.
- Inspected the running LM Studio instance and recorded the exact server,
  endpoint, loaded model, variant, quantization, context, tool capability,
  and Unknown reasoning/structured-output fields in the canonical capability
  record.
- Proved mock-first ordering, exact provider/model wiring into the effective
  Codex configuration, visible WPF attribution, and no silent fallback.
- The real model emitted an ordinary `exec_command` call, but the isolated
  environment rejected `cmd.exe /d /c ver` by policy. The session journal
  contains the attributed function-call output failure; no success claim is
  made.
- Recorded the App Server model-metadata fallback warning, the legacy
  read-only sandbox projection versus requested `workspace-write`, and the
  need for structured host-level tool-result reporting as concrete follow-up
  blockers.

No commit, push, system-wide installation, shared configuration change, or
production runtime feature was performed.

## 2026-09-22 — Decisions-and-plan handoff reconciliation

Status: **Documentation complete for review; implementation not authorized.**

- Read `C:\Users\Martin\Downloads\NEOBABYLON_DECISIONS_AND_PLAN_REVISION_FOR_LUNA.md`
  completely through line 652 in bounded ranges.
- Recorded its eight settled directions in NB-DEC-002 through NB-DEC-005,
  product/architecture/provider/tool documents, and the revised Phase 1A/1B
  plan. Illustrative folder names, manifest fields, class names, and scheduling
  mechanics remain labeled as proposals.
- Reconciled the prior candidate-executor restriction so ordinary authorized
  model shell/tool execution can temporarily use an unapproved candidate,
  while retaining the prohibition on model-controlled `thread/shellCommand`
  full-host escape and manual promotion boundary.
- Split the next work into a smallest WPF/WebView2 Phase 1A live path and
  Phase 1B foundational qualification. No live provider or inference test was
  run during reconciliation.
- Handoff fingerprint recorded in `docs/release/VERIFICATION.md`; the external
  handoff was not copied into the maintained specification tree.
- Final advisory reconciliation kept all eight choices settled and added only
  evidence-backed blockers: OpenRouter route/fallback enforcement through stock
  0.155.1 remains unqualified; application root is distinct from a source
  repository root; and Phase 1A verifies capability wiring into Codex's
  effective model state while Phase 1B verifies budgeting/compaction behavior.

## 2026-09-22 — Phase 1B initial diagnostic slice

Status: **Initial metadata/authority/diagnostics work verified; live App Server
turn-completion remains open.**

- Inspected the pinned source's stock `model_catalog_json` seam and generated
  an exact Codex catalog entry from the authoritative LM Studio capability
  record. No runtime-source patch was needed; no fallback metadata warning was
  observed in the catalog-backed live session.
- Fixed the capability wiring so `model_context_window = 32768` remains a
  top-level TOML setting rather than entering the `[windows]` table.
- Explained and surfaced the Windows authority layers: raw requested
  `workspace-write`, optional `[windows].sandbox="unelevated"`, and effective
  `thread/start` sandbox projection. The default path reports an explicit
  downgrade to `readOnly`; the restricted-token path reports `workspaceWrite`.
- Added typed `toolOutcome` and host failure fields, partial-notification
  retention on timeout, and direct diagnostic-page rendering of failures.
- Deterministic qualification passed with explicit `unelevated` policy and
  failed as expected under the default/no-backend policy. The real LM Studio
  session executed `cmd.exe /d /c ver` successfully under the explicit policy,
  but App Server did not emit `turn/completed` within ten minutes. Direct
  provider-only probes completed; they do not replace App Server evidence.
- Host/core tests passed `16/16`; WPF host and qualification runner builds
  passed with zero warnings/errors. The Windows UI automation helper was not
  available for a fresh click-through, so that check remains unclaimed.

No remote repository, runtime-source edit, system-wide installation, commit,
or push was performed.

## 2026-09-22 — LM Studio live round-trip rerun

Status: **Minimal App Server tool round trip passed once; broader Phase 1A/1B
qualification remains in progress.**

- Repeated the live qualification using the pinned App Server, exact
  catalog-backed LM Studio model, a fresh application `Data` root, and the
  explicit `unelevated` restricted-token Windows policy. Mock Responses passed
  first.
- The live model called `exec_command` with `cmd.exe /d /c ver`. The session
  journal records exit code 0, the Windows version, the tool output
  continuation, and `turn/completed`. Total turn duration was 275,918 ms.
- Effective provider/model matched the capability record; catalog context
  `32768` produced effective context `31129`, matching the configured 95%
  projection. No native metadata fallback warning or provider/model mismatch
  was recorded.
- Captured LM Studio request warnings: `namespace` and `web_search` tool types,
  `prompt_cache_key`, and encrypted reasoning content were ignored; the
  developer role was converted to system. The tested command succeeded, but
  broader Codex tool coverage remains unqualified.
- An earlier attempt exceeded ten minutes without `turn/completed`; that
  completed rerun established one success, not repeatability. Visible WPF click-through,
  budgeting/compaction, OpenRouter, and the broad authority/recovery matrix
  remain open.
- Machine evidence is at
  `D:\CODING\NeoBabylon-Data\Phase1B-ServerLog-20260922T183103Z`, with the
  concise qualification artifact at `artifacts/phase1a/lmstudio/qualification.json`.

No runtime-source change, shared Codex data/configuration change, system-wide
installation, remote repository, commit, or push was made.

## 2026-09-22 — Phase 1B repeat timeout and evidence-reader hardening

Status: **One live tool round trip passed; the repeat did not reach tool
execution. The runner now preserves timeout evidence across transient journal
locks.**

- The repeat used the same pinned App Server and selected LM Studio model with
  the explicit `unelevated` restricted-token policy. Mock Responses passed
  first. LM Studio logs show generation continuing to the ten-minute bound;
  the isolated session journal contains no `function_call`, tool output, or
  completed turn. This is a slow/incomplete generation, not an observed policy
  rejection.
- During cleanup, session-journal reading raised a sharing `IOException` and
  the old top-level catch obscured the turn observation. A later exclusive
  journal read succeeded after App Server exit. The specific Windows handle
  owner was not captured; the exact underlying lock cause remains unknown.
- Added an append-only journal reader with shared-read open, bounded retries,
  and a typed non-fatal read-failure result. The qualification result retains
  turn failure/timeout separately from `sessionEvidenceReadFailure`.
- Added a deterministic locked-journal regression: it failed before the fix and
  passes after; the post-fix explicit-`unelevated` mock qualification also
  passed. All `17/17` host/core/qualification tests pass. WPF host and runner
  builds pass with zero warnings/errors; pinned reference verification passes.
- The successful live snapshot remains at
  `D:\CODING\NeoBabylon-Data\Phase1B-Repeat-20260922T185400Z\previous-qualified-run.json`;
  the repeat's pre-fix run artifact is preserved beside it as
  `qualification-runner-sharing-failure.json`. The current workspace
  qualification artifact is the post-fix mock-only result.

Live repeatability, the source of slow-generation variance, visible WPF
click-through, request budgeting/compaction, and the broader provider/authority
gates remain open. No runtime source, shared Codex configuration, or global
install was changed; no commit or push was made.

## 2026-09-22 — Codex-first UI reference and context budget trace

- Read all 948 lines of Martin's supplied UI reference (SHA-256
  `148D8C6179342F151EC32167778129EB16023D570F7B009C325A359C2092566D`) and
  added `docs/architecture/UI_UX_REFERENCE.md` as the single canonical
  interaction reference. It preserves the reviewed Phase 1B-first sequence;
  the report's shell-first order remains a proposal.
- Reconciled the stale phase marker: Phase 0 is complete and Phase 1B
  qualification is current. Confirmed implementation state: WebView2 host plus
  a dependency-free diagnostic HTML/JS surface; `App.tsx` contains bridge
  types/helper only, not the product React UI.
- Source-traced the pinned Codex context rules: `32768 * 95% = 31129` usable
  context; null catalog compaction metadata still derives a 90% (`29491`)
  threshold with total-context default scope. No live compaction event was
  observed.
- Read the completed LM Studio response statistics: 717 tokens at 4.1908
  tok/s and 266 at 3.5673 tok/s explain most of the 275.918-second successful
  turn. The timed-out repeat has no completed output statistics; root cause is
  still unresolved.
- The targeted offline upstream Rust test could not start because the pinned
  `tungstenite` Git dependency is absent from Cargo's cache. No fetch was
  attempted. No production UI, runtime source, shared installation/config,
  commit, or push was changed.

## 2026-09-22 — post-reconciliation verification

- Re-ran the local host/core/qualification harness: all `17/17` checks passed.
  The WPF host and qualification runner built with zero warnings/errors, and
  pinned-reference verification passed. The local Markdown-link check passed
  for `50` links across `27` project documents.
- Tried the targeted upstream protocol test with `--locked` and a
  NeoBabylon-specific Cargo home. Cargo attempted dependency resolution but
  stopped before compilation because it would need to update the runtime
  checkout's `Cargo.lock`; the lock remained unchanged.
- Tried to initialize the Windows desktop automation runtime for the visible
  WPF click-through. It failed with `failed to write kernel assets: The system
  cannot find the path specified`; no window action or live inference was
  started.
- No runtime source, shared configuration, system-wide installation, commit,
  or push was changed.

## 2026-09-22 — premature App Server stream closure

- Added a child-process regression that completes initialization, emits
  `turn/started`, and exits before a terminal event. It failed before the fix
  with `The channel has been closed.`
- `AppServerClient.WaitForTurnCompletionAsync` now converts channel closure to
  an incomplete-turn failure and preserves notifications already received.
  The existing host mapping gives the failure type `appServerTurn` and
  attributes it to Codex App Server.
- The full host/core/qualification harness passes `18/18`; WPF host and
  qualification builds pass with zero warnings/errors; pinned-reference
  verification passes. This does not verify the visible WPF click path or a
  live-provider recovery.
- The fix changes NeoBabylon Core only; the pinned Codex runtime, shared
  configuration, and global installations were not changed. No commit/push.

## 2026-09-22 — interrupted turn protocol and host operation

- Rechecked the pinned stable protocol: `turn/start` returns the runtime turn
  ID; `turn/interrupt` requires thread and turn IDs; terminal status is part of
  `turn/completed`.
- Added a deterministic child-process regression. Before the fix it failed
  because the host treated `turn.status = interrupted` as successful turn
  completion. The fake server now accepts an interrupt only for the expected
  thread/turn pair and returns an interrupted terminal event.
- The client distinguishes terminal/completed/interrupted status and retains
  failures for failed or unknown statuses. The host routes named
  `interruptTurn`, retains the current turn ID, prevents overlapping starts,
  and attributes interruption separately from success and failure.
- Deterministic protocol coverage does not prove interruption against the
  pinned live server, no-zombie behavior, or visible WPF behavior. Those remain
  Phase 1B evidence gaps. OpenRouter, recovery, containment, and the other
  broader qualification gates are still open.

## 2026-09-23 — OpenRouter WPF round trip and typed journal outcomes

- Refreshed the OpenRouter public metadata and ran the deterministic mock
  Responses path before the live test. The selected `nex-agi/nex-n2.5-pro:free`
  route showed one free Nex AGI FP8 candidate. A read-only post-run GET to
  OpenRouter generation metadata attested Nex AGI, the returned canonical
  model, and zero cost for this response; future route pinning remains absent.
- Started the visible WPF host with its app root under
  `D:\CODING\NeoBabylon-Data`, distinct from the product repository, and the
  exact selected capability record. One live `exec_command` call completed
  `cmd.exe /d /c ver` with exit code 0 and `turn/completed`. The Codex session
  recorded effective context `249036` from catalog context `262144` at 95%.
- Added a bounded App Server journal reader keyed to the path returned by
  `thread/start`, restricted to the isolated CodexHome sessions directory and
  to records appended during the active turn. It pairs function call/output
  by `call_id`, supplements protocol notification diagnostics, leaves unknown
  outcomes Unknown, and omits raw journal output from structured diagnostics.
- A new regression failed before the reader was connected to qualification
  evidence and then passed; it covers success, sandbox failure, an unmatched
  output, item/call pairing, and raw-output non-disclosure. The production
  reader also classified the preserved C: sandbox-denial journal as a typed
  `toolExecution` failure without re-running its command.
- Fresh checks at this point: `26/26` tests, WPF host build with zero warnings
  and errors, and OpenRouter mock-only qualification passed. Visible host
  screenshots and the isolated run journal remain in ignored local artifacts.
- Remaining: OpenRouter request-level route pinning/no-fallback enforcement;
  C: default-root sandbox compatibility; live reasoning telemetry; output cap,
  structured-output integration, budget/compaction and stale-capability
  behavior; repeatability; and the broader Phase 1B authority/recovery matrix.
  No system/global dependency, runtime-source edit, shared configuration
  change, remote repo, commit, or push was made.

### 2026-09-23 follow-up evidence

- The read-only OpenRouter generation lookup for the same live response
  attested provider `Nex AGI`, model
  `nex-agi/nex-n2.5-pro-20260907:free`, `total_cost=0`, `is_byok=false`,
  `streamed=true`, and `api_type=completions`. The authoritative capability
  record now keeps this as a typed, one-response attestation; it does not claim
  request-level route pinning or future no-fallback behavior.
- A fresh deterministic C: LocalAppData mock rerun failed closed before command
  launch. App Server session/config provenance used the packaged
  `LocalCache\Local\NeoBabylon` path; `fsutil` returned matching file IDs
  for that path and the supplied `Local\NeoBabylon` path. This supports, but
  does not prove, a lexical path-alias explanation for the sandbox root-set
  mismatch. The failure artifact is preserved separately; a fresh D: mock
  run passed and restored the canonical qualification artifact.
- OpenRouter's official Responses documentation exposes optional provider
  preferences and opt-in routing metadata; pinned Codex 0.155.1 does not expose
  `provider.only` or `allow_fallbacks=false`. This makes a small runtime
  extension technically feasible, subject to source-level design and tests.
