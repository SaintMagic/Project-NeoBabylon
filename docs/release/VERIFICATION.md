# NeoBabylon verification record

## Latest source-publication review — 2026-10-05

Martin requested committing and pushing the latest update to the existing
public GitHub repository. Current session metadata verified `gpt-6.1-sol`.
The final review covers the supplied parity patches and subsequent bounded
conversation, reasoning, tool-output, throughput and turn-status corrections.
Private application data, credentials, session journals, build output, the
nested runtime checkout and reference cache remain excluded. No runtime source
or locked-runtime identity changed; the local App Server SHA-256 was rechecked
as `a0c3ebdc8d1d9f5b56327f5ea6d1502ee5a0b50fa174d50743f8c7a17ff54386`.

Fresh checks passed: **194 UI tests**, UI TypeScript and bundle build, **6 QA
run-root tests**, **12 focused C# parity checks**, the focused actual-journal
rate/identity checks, and isolated WPF Host Release and ApprovalQA harness
compilation with zero warnings/errors. The running user application was not
restarted; the previously recorded deterministic native evidence was not
rerun, and no live provider inference was requested.

The broad ApprovalQA host harness is **not green**. Before the test-contract
correction, current source returned 110 passes / 27 failures. The published
`f126612` baseline, built separately and executed from the same checkout CWD
with the same test environment, returned 109 passes / 26 failures. All 26
baseline failure names also occurred in the current run; this comparison does
not diagnose their causes or prove identical behavior. The extra current
failure was an obsolete assertion forbidding ordinary tool output, contrary
to accepted [NB-DEC-011](../decisions/0011-unrestricted-tool-output-visibility.md).
Only that test contract was corrected to require exact normal output,
arguments and retained output identity while keeping missing output Unknown.
The new focused regression passed, and the updated broad harness compiled;
the entire broad harness was not rerun after this test-only correction.
These are source-publication checks, not full-suite, live-provider, phase or
product-release acceptance. Private artifacts remain under
`.local/Lab/Runs/Push-20261005/`.

## Completed turns versus failed tools — 2026-10-04

Martin authorized correcting the misleading whole-turn failure state. The
saved missing-out.log command exited 1, but the same model turn continued and
finished normally. NeoBabylon had incorrectly classified the host's
`turnCompletedWithToolFailure` as failed generation and marked the assistant
answer failed. The UI now separates verified turn completion from individual
tool outcomes. A completed turn remains completed with a nonfatal warning;
failed tool cards retain their error/output, exit code and exact identity.
Actual provider/runtime failure, interruption and unconfirmed outcomes remain
distinct. A successful fork clears the source thread's live warning only after
identity validation; a failed fork leaves the source state intact.

Fresh final checks: **194 UI tests passed**, TypeScript passed, and the UI bundle
build passed. The existing WPF Host serves the rebuilt assets; host and runtime
source/binaries were not changed by this slice. The pinned App Server/native
WebView2 deterministic fixture passed: an actual command exited **23**, its
failed result reached the same model's single continuation, the model turn
completed, the nonfatal warning was visible, no fatal banner or failed answer
marker appeared, and there were exactly two loopback Responses requests with
no retry/fallback or page errors. The extended fixture then verified a distinct
forked thread, cleared warning and still only two requests. This is mock-path
evidence, not live inference/provider qualification. Two earlier fixture runs
failed on obsolete CSS-class selectors (duration shares exit-code styling);
their receipts are retained and the corrected semantic selector passed.

Scoped review and fork-reset re-review found no remaining defect in the final
fix. The turn-level warning summary is **live-only**; restored history still
shows authoritative completed state and failed individual tool cards. No new
durable warning/history API was introduced. A separate read-only trace found
the malformed spacing already in Codex's persisted AgentMessage before NB
rendering; provider versus Codex-internal origin remains Unknown. Existing
whitespace regressions passed; no saved answer was rewritten.

Final normal host PID 67948 was responsive at 18:55:51 UTC, with the preserved
popup-QA app/Data root and pinned App Server child PID 80076. Debugging was
disabled; old diagnostic port 60988 was not listening. Runtime SHA-256 remained
`a0c3ebdc8d1d9f5b56327f5ea6d1502ee5a0b50fa174d50743f8c7a17ff54386`.
No live inference, credential/configuration change, data reset, commit or push.
The previous full-host-harness and broader phase/product verification gaps are
not closed by this correction. Private exact paths and receipts:
`.local/Lab/Runs/Turn-Status-20261004/final-handoff.md` and the final native run
`.local/Lab/Runs/P3-02-native-command-failure-1791139974960-97908/result.json`.

## Throughput and retained tool output — 2026-10-04

Martin requested useful throughput/average values, meaningful tool cards and
scrollable retained output without silently discarded characters. The bounded
fix is implemented and uncommitted. Exact completed-turn journal evidence now
restores measured output rates; live measurements use monotonic elapsed time.
These are end-to-end rates, including waits/tools, not provider decode TPS.
Failed/interrupted/unconfirmed turns are excluded from the completed-turn
average. Missing provider provenance is not invented.

The controller's final native saved-chat check showed **18.9 tok/s** for both
Throughput and the one-turn, time-weighted selected-model average: 22,110 output
tokens over 1,169,440 ms. An earlier provisional 65.2 tok/s example was
withdrawn: that later turn actually failed with OpenRouter `openrouter:web_search`
upstream 502. Its terminal error is now captured separately from tool outcomes;
the restored UI did not display that specific cause in the native probe.

All 64 restored tool cards had meaningful command titles and inline retained
output controls. A representative card loaded all **4,726/4,726 retained
characters**, scrollable and keyboard-focusable. Exact host-returned output
identity and contiguous page offsets are checked. Post-turn merging preserves
arguments and retained output identity. Source-side truncation cannot be undone;
the existing newest-64 saved-card limit remains disclosed, not removed.

Final checks: **193 UI tests passed**; TypeScript and UI bundle builds passed;
the actual WPF Host Release build passed with zero warnings/errors; focused C#
fixtures included the actual journal and rejected mismatched thread identity.
Native WebView2 checks passed at desktop and 800x812, with no page errors,
framework overlay or horizontal overflow. Independent scoped re-reviews passed.
The earlier full-host-harness failures remain unclassified; this is not a full
suite, live-provider qualification or phase/product acceptance claim.

The exact idle diagnostic host closed gracefully. Normal host PID 82796 was
responsive at 17:43:10 UTC with the same popup-QA application Data root and
locked App Server child PID 95612. Temporary debugging port 60988 was no longer
listening. Runtime SHA-256 remained
`a0c3ebdc8d1d9f5b56327f5ea6d1502ee5a0b50fa174d50743f8c7a17ff54386`.
No inference, runtime edit, configuration reset, commit or push was performed.
Two read-only OpenRouter generation lookups returned zero native token counts
despite nonzero exact Codex counts; provider-native TPS remains unqualified and
is not substituted for the measured end-to-end rate. Private receipt and exact
task-owned paths: `.local/Lab/Runs/Throughput-20261004/final-handoff.md`;
tool evidence: `.local/Lab/Runs/Tool-Output-20261004/`.

## User bubbles and readable reasoning labels — 2026-10-04

Martin requested rounded user-message bubbles and clear Reasoning/Message
labels. The bounded display change is implemented, uncommitted: neutral gray
right-aligned user bubbles; flat assistant Message entries; explicit Reasoning
or Reasoning summary entries only from readable App Server reasoning data.
Live/restored identity, preview bounds, omission notices and the existing
saved-item range inspector are covered by focused regressions. The pinned
Codex runtime and provider settings were not modified.

The implementer reported 182 passing UI tests and 24/24 focused UI checks;
the focused C# reasoning range check passed. UI typecheck/bundle passed.
The controller built the actual WPF Host in isolated Release output with
zero warnings/errors. Synthetic renders passed at 1440x900 and 800x812,
including 10.19:1 dark bubble contrast. Independent task review found no
blocking issue. Native inspection of the rebuilt Host restored the existing
chat with 3 user bubbles, 65 readable reasoning sections and 72 Message
sections; at 1464x901 it showed matching gray colors, no framework overlay and
zero horizontal overflow. The same application Data root was preserved.
Temporary diagnostic remote debugging was closed before the final normal
launch; no live provider inference was requested by this task.

The full host harness is **not green**: 15 failures remain unclassified without
a comparable baseline run. This entry does not pass that suite, qualify a live
provider, close broader parity specifications, or close a product/phase gate.
Private task evidence and exact changed paths:
`.local/Lab/Runs/Message-Pills-20261004/task-report.md`, `task-review.md`, and
`progress.md`. Prior evidence and deferred scope remain unchanged.

## Source-publication and preserved-root restart — 2026-10-04

Current session metadata verified `gpt-6.1-sol`. The authenticated GitHub
account was `SaintMagic`; `Project-NeoBabylon` was created and API-verified as
PUBLIC. Martin separately authorized the initial commit and push. See the
[source boundary](SOURCE_PUBLICATION.md), not a product-release claim.

The exact QA host PID 77420 closed gracefully and reopened as PID 37452 at
11:07:41 UTC using the same explicitly selected popup-QA app root. Its existing
1,924,767-byte journal prefix was SHA-256 compared through shutdown without
change; read-only WebView2 inspection found the same saved thread/model/
workspace and message composer after restart. No reset or migration occurred.
The controller sent no inference request; this is not a whole-window inference
claim. Private receipt: `publication-restart-receipt.json` under
`.local/Lab/Runs/Context-Reasoning-20261004`.

Fresh source-publication checks: 162 UI tests passed with zero failures/skips;
`tsc -b --pretty false` exited 0; the focused Host context/reasoning fixture
returned `CONTEXT_REASONING_HOST=PASS`. All 225 implementation/test files in
the latest 10:54 UTC source snapshot remained byte-identical. Locked App Server
readback still matched `a0c3ebdc8d1d9f5b56327f5ea6d1502ee5a0b50fa174d50743f8c7a17ff54386`.
Private source/privacy/Sol review reports identify their actual scope and limits;
this is not an exhaustive semantic audit or full-product acceptance.

The two startup briefs were removed from the index only and explicitly ignored,
not deleted or edited. Both retained SHA-256
`7838b493af6be76e90f0cb905c8c3f2f3895b93b7b527ce6bb895d4def1754f1`.
No source commit includes the private app data, credentials, runtime executable,
nested runtime Git repository or reference cache. The unpublished custom patch
is a disclosed fresh-clone gap; first-party license selection remains open.

## Current source/build and bounded native checkpoint — 2026-10-04

The [feature/UI checkpoint](../history/2026-10-04-feature-ui-build-checkpoint.md)
records the new runtime build/cutover, source reviews, assembled desktop
artifacts and exact hash checks. Detailed receipts/reports are under
`.local/Lab/Runs/Finishing-20261003`. That checkpoint is compilation/source
evidence, not full-product acceptance. The later
[provider/conversation checkpoint](../history/2026-10-04-provider-and-conversation-ui-checkpoint.md)
records focused synthetic credential/adapter/model-switch checks, a fresh
native launch with the exact locked runtime hash, one live OpenRouter Space
Bunny ordinary tool round trip, confirmed Rename, and same-thread/draft/history
preservation across an explicit provider switch and live continuation.
NVIDIA live probes timed out; catalog discovery and adapter startup are not
inference qualification. Broad testing remains deferred; generated-tool
activation is still non-callable. The original Phase 0 and dated verification
evidence below is preserved.

The [context/metrics/reasoning follow-up](../history/2026-10-04-context-metrics-reasoning.md)
adds exact advertised controls and separate requested/runtime-observed state,
current-versus-lifetime usage, named manual compaction and frozen capability
version adoption. Focused Host protocol/journal/adoption fixtures, all 162 UI
tests, `tsc -b`, development Release/UI builds and an isolated assembled
WPF/WebView2 check passed. The rebuilt development host was reopened. Native
QA made no inference request; both bounded direct Ling compatibility probes
returned HTTP 429. Ling Boolean override, live effort effects, live compaction
and full-product acceptance remain unqualified. The App Server source and
locked binary were not changed in this increment.

The [popup correction/paused checkpoint](../history/2026-10-04-popup-fix-paused-checkpoint.md)
records the later viewport fix, 20 rendered and 11 native geometry cases, a
rebuilt development UI and the requested pause. It also corrects native QA
scope: the popup-QA root contains live model/tool activity, so script action
counts are not proof that its entire window was inference-free. Its active
thread/Data were preserved; no provider qualification is inferred. Throughput
clarity, reasoning display/recovery and tool-card usability remain follow-ups.

Date: 2026-09-22. Status: **Phase 0 evidence complete; one minimal live Phase
1A tool round trip passed; Phase 1B qualification remains open.** The initial
sections preserve the Phase 0 record; later dated sections record subsequent
implementation and qualification evidence.

## Checks performed

| Check | Result | Evidence |
| --- | --- | --- |
| Startup brief read completely | Pass | `NEOBABYLON_STARTUP.md` was read through line 625; exact copy preserved at `docs/history/NEOBABYLON_STARTUP.md` |
| Selected destination | Pass | Workspace is `D:\CODING\NeoBabylon`; no alternate destination was needed |
| CanonWell structural reference | Pass, read-only | Live `D:\CODING\Book Writing help\canonwell-studio` docs/instructions/categories were inspected; no writes made |
| Codex stable baseline | Pass | `rust-v0.155.1`, commit `be2951ea34f0d295ed0becf97079f92fa5f6950e`; metadata in `reference/cache/codex/rust-v0.155.1/metadata/` |
| Source archive | Pass | 7,688 files, 918 directories, 76,382,873 bytes; archive SHA recorded in `reference/sources.json` |
| App Server binary | Pass | `codex-app-server 0.155.1`; Windows x64 SHA recorded; `--help` inspected |
| CLI binary | Pass | `codex-cli 0.155.1`; `app-server --help` and generation help inspected |
| Official docs snapshot | Pass with one explicit gap | `llms.txt` plus 18 Markdown pages captured and hashed; optional harness article returned local HTTP 403/Cloudflare and is marked unavailable |
| Protocol generation | Pass | Stable and experimental TypeScript/JSON Schema outputs generated with matching CLI and isolated project-local data |
| Protocol output index | Pass | `reference/cache/codex/rust-v0.155.1/metadata/protocol-generated.sha256.json` records 2,331 generated files and their hashes |
| Offline reference verification | Pass on final rerun | `scripts/verify-reference.ps1` reported PASS for the manifest, baseline metadata, protocol counts, startup preservation, and matching 0.155.1 binary versions |
| Scope protection | Pass | No production source, runtime, shared Codex settings, system-wide dependency, remote repository, commit, or push was created |

## Upstream inspection performed

The pinned source and docs were inspected for:

- App Server `stdio://` transport, JSONL framing, initialize handshake,
  lifecycle events, approvals, cancellation, and recovery;
- generated stable/experimental protocol output paths;
- rollout/thread-store/history persistence and SQLite-backed queue metadata;
- Responses-only provider wire configuration and Ollama/LM Studio defaults;
- Windows sandbox/command execution and permission boundaries;
- `thread/shellCommand` full-host-access behavior and its explicit-human-use
  restriction;
- MCP, skills, hooks, plugins, and their trust/loading behavior;
- experimental dynamic tools, persisted thread metadata, and client callback
  flow.

The detailed path map is in [Reference index](../../reference/INDEX.md). The
primary local evidence includes `app-server.md`,
`codex-rs/app-server-transport/src/transport/stdio.rs`,
`codex-rs/app-server-protocol/src/precomputed_exports.rs`,
`codex-rs/model-provider-info/src/lib.rs`,
`codex-rs/app-server/src/dynamic_tools.rs`, and the corresponding upstream
tests.

## Not performed by design

- No Codex source build or complete upstream test suite.
- No App Server process was started for a live JSON-RPC session.
- No hosted or local provider/model was contacted.
- No live tool call, approval, continuation, cancellation, compaction, or
  persistence round trip.
- No UI/shell build, packaging, installation, performance soak, or security
  audit.

These are Phase 1+ acceptance gates, not hidden failures in the Phase 0
deliverables.

## 2026-09-22 plan-reconciliation checks

The external handoff
`C:\Users\Martin\Downloads\NEOBABYLON_DECISIONS_AND_PLAN_REVISION_FOR_LUNA.md`
was read completely in four ranged reads through line 652. Its observed
fingerprint was 57,920 bytes and SHA-256
`FC7A7B8025B83F125AC0DAEDBCFF1042C96971B3F5BF0DCE66AEA31D97FF6303`.

The reconciliation compared the handoff's eight settled choices against the
canonical decision, product, architecture, provider, tools, roadmap, status,
open-question, build-plan, licensing, instruction, verification, and history
documents. It changed documentation only. No source archive, binary, runtime,
provider, shared installation, repository, or live inference was touched.

The handoff remains an external input record rather than a second maintained
specification. The canonical decisions and build plan now distinguish settled
direction from implementation proposals and retain the prior Phase 0 evidence.

## Independent advisory review

On 2026-09-22, CODEX Helper reviewed the Phase 0 evidence and proposed build
plan. The direction was confirmed, with these material corrections applied to
the documents:

- model-controlled fallback may not use `thread/shellCommand`, which the
  pinned docs describe as outside the thread sandbox with full host access;
- generated candidates have separate generated, tested, Martin-approved
  immutable, and active states; editing invalidates approval;
- stable and experimental generated protocol outputs are separate compatibility
  domains, with `dynamicTools` outside the baseline until explicitly enabled;
- Phase 1 must verify runtime version and binary hash, enforce one owner for the
  NeoBabylon runtime data root, and use a narrow product-operation bridge
  rather than generic renderer RPC;
- provider qualification covers the exact runtime/provider/model/feature tuple,
  includes a real tool round trip, proves no silent provider fallback, and
  tests credential containment.
- OpenRouter route/fallback enforcement through stock 0.155.1 remains an
  explicit qualification blocker; it is not implied by selecting OpenRouter.
- The application root for `<NeoBabylon root>\Data` is distinct from a
  source-repository root, and Phase 1A uses a disposable application/data root.
- Phase 1A verifies normalized capability wiring into Codex's effective
  model/configuration state; actual budgeting/compaction and stale-value
  invalidation remain Phase 1B evidence.

This review is advisory and does not replace the pinned-source inspection,
machine verification of the remaining technical inputs, or decisions that
those results may leave open.

## Phase 1 gates made explicit

The build plan now treats these as required evidence before calling the stock
runtime path qualified: manifest/hash identity match; successful
`initialize`/`initialized`; stable API baseline with experimental capability
off; start, first turn, continuation, resume, fork, and interruption behavior;
crash/restart recovery; explicit approval handling with unknown cases denied or
failed visibly; workspace containment including Windows reparse-point tests;
mock-first and live provider/tool qualification; credential redaction;
single-owner data-root enforcement; malformed/unknown-message handling; and
API-only persistence access. It separately requires Phase 1A capability
wiring evidence and reserves actual request-budget/compaction behavior for
Phase 1B.

Final reconciliation checks after the last documentation edits:

- `scripts/index-protocol.ps1` indexed 2,331 generated protocol files.
- `scripts/verify-reference.ps1` passed with baseline
  `rust-v0.155.1 / be2951ea34f0d295ed0becf97079f92fa5f6950e`, App Server
  `codex-app-server 0.155.1`, and CLI `codex-cli 0.155.1`.
- An offline Markdown check resolved internal link targets in 38 maintained
  Markdown files.
- A required-document inventory found all 22 reconciliation/Phase 0
  deliverables, parsed `reference/sources.json` as 7 records, and found
  no production source/project/UI implementation files outside
  `reference/cache/`.

## 2026-09-22 — Authorized Phase 1A verification

Phase 1A was separately authorized after Phase 0 acceptance. The following
evidence is a bounded diagnostic implementation result, not a production
feature claim and not a pass of the ordinary-tool success gate.

- `D:\CODING\NeoBabylon-Runtime` is a local sibling checkout of the pinned
  upstream Codex source at `rust-v0.155.1` /
  `be2951ea34f0d295ed0becf97079f92fa5f6950e`. The product-side
  `runtime/runtime-lock.json` records App Server binary SHA-256
  `253C6D8424EA45BA9D36D1F665D9B7B4C917782D4CF5D56E61CD0664CC52BF0D`.
  The runtime source layout was preserved; no upstream source edit was made.
- Cached .NET/WPF and WebView2 prerequisites were available. The host uses
  the cached `Microsoft.Web.WebView2` package `1.0.4191.47`; no system-wide
  dependency was installed.
- The host/core qualification tests passed 9/9. The WPF host and qualification
  runner built with zero warnings and errors in the final verification run.
- The visible WPF diagnostic host proved named status, diagnostics, and
  thread-start operations. It reported the exact runtime identity, isolated
  application `Data` paths, `ordinaryCodexRootUsed=false`, and the canonical
  LM Studio capability record. The UI uses named operations and has no
  generic RPC or `thread/shellCommand` bridge.
- The deterministic Responses path ran before the live attempt and completed
  the App Server/fixture request and function-call-output exchange. The
  ordinary command itself was rejected by the current policy, so this is
  protocol evidence and an explicit failure, not a successful tool proof.
- LM Studio was inspected live and the exact selected tuple was recorded in
  `docs/release/PHASE1A_LMSTUDIO.md` and
  `docs/release/MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json`: server
  `0.4.25.0`, endpoint `http://127.0.0.1:1234/v1`, model
  `phase1a-qwen3-14b`, variant `qwen/qwen3-14b@q4_k_m`, and effective context
  `32768`. No provider or model fallback occurred.
- The live model emitted an ordinary `exec_command` call for
  `cmd.exe /d /c ver`; its isolated session journal recorded a
  `function_call_output` failure because the command was blocked by policy.
  Therefore `toolRoundTripCompleted=true` but `toolRoundTrip=false`.
- App Server `config/read` and the visible `thread/start` response agreed on
  provider, model, and context. The WPF live notification stream also exposed
  a material mismatch: App Server could not find native model metadata for the
  LM Studio identifier and announced fallback metadata. Reasoning and
  structured-output values remain `Unknown` in the authoritative record.
- The WPF host's application root under `%LOCALAPPDATA%\NeoBabylon` was
  distinct from `D:\CODING\NeoBabylon`; the disposable direct qualification
  root was `D:\CODING\NeoBabylon-Data\Phase1A-20260922T151744Z`. The ordinary
  `%USERPROFILE%\.codex` root was not used. Windows package `LocalCache\Local`
  path presentation was observed and is retained as a packaged-host
  recheck item.

At the end of that earlier Phase 1A attempt, the gate was **blocked** by the
ordinary command policy, the App Server model-metadata fallback warning, and
the unqualified Windows sandbox projection. Later Phase 1B checks addressed
those three findings; the subsequent live rerun is recorded below.

## 2026-09-22 — Phase 1B initial diagnostic slice

The earlier Phase 1A bullets above are retained as historical evidence. The
following later checks addressed the two requested Phase 1B concerns without
changing the pinned runtime source:

- `model_catalog_json` is now generated from the authoritative
  `MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json` record and points to an isolated
  exact-slug catalog entry. The entry carries context `32768`, empty
  reasoning levels, text-only input, `unified_exec`, and null compaction limit;
  Unknown provider capabilities were not filled with GPT-family defaults.
- A real App Server session using that catalog had no native-model fallback
  warning in its session/notification evidence. Its session metadata reported
  the selected model and an effective context of `31129`, which corresponds to
  the catalog's explicit 95% effective-context projection from `32768`.
- LM Studio's `parallel=4` remains in the authoritative capability record but
  is not mapped to a Codex catalog field or equated with parallel tool calls;
  its semantics relative to Codex concurrency are not verified.
  The `providerModelMetadataMismatch` flag covers identity, context, and
  fallback checks only; it does not mean every provider metadata field was
  mapped.
- A TOML ordering defect was found and fixed: `model_context_window` is now a
  top-level setting, before the `[windows]` table. Host tests assert this
  placement.
- The default/no-backend mock run observed `config/read.sandbox_mode` as
  `workspace-write` and effective `thread/start.sandbox.type` as `readOnly`;
  the new authority report labels this a Windows backend downgrade.
- The explicit `--windows-sandbox-unelevated` mock run produced effective
  `workspaceWrite`, network disabled, and a successful `cmd.exe /d /c ver`
  tool result. This is an explicit restricted-token qualification, not an
  unrestricted full-host workaround.
- Pinned source explains the split: `codex-rs/core/src/config/mod.rs` resolves
  `windows.sandbox = "unelevated"` to the restricted-token Windows sandbox
  level; `codex-rs/core/src/exec_policy.rs` notes there is no platform sandbox
  enforcement when that level is disabled; App Server's
  `codex-rs/app-server/src/request_processors/thread_processor.rs` checks the
  requested compatibility sandbox against the effective config-snapshot
  policy. Live command success under the selected mode corroborates which
  effective path was used. The UI reports configured and effective values
  separately; visible WPF rendering remains unverified.
- Tool results and host turn failures now have typed fields, and the UI
  renders the typed failure event. The timeout path retains partial
  notifications instead of throwing away the distinction between a tool
  result and a completed turn.
- Direct LM Studio Responses probes, using the same selected model, completed
  a bounded text request in about 1.4 seconds, a tool-call request in about
  2.5 seconds, and a tool-output continuation in about 9 seconds. These are
  provider-only probes, not App Server qualification.
- The real App Server live session emitted the exact tool call and recorded
  `cmd.exe /d /c ver` with exit code 0 under the explicit policy, but did not
  emit `turn/completed` within the ten-minute bound. The tool-call/output path
  is therefore observed; the completed-turn gate remains open.
- Product checks after the slice: host tests `16/16` passed; WPF host build
  passed with `0` warnings and `0` errors; qualification runner build passed
  with `0` warnings and `0` errors; explicit-`unelevated` deterministic mock
  path passed. The Windows UI automation helper was unavailable, so a fresh
  click-through of the WPF page was not claimed.

At the time of this initial slice, current blockers were live-turn
non-completion, visible WPF rerun, budgeting/compaction, stale-capability
evidence, and the broader Phase 1B authority/recovery matrix. The later rerun
below resolves the one-run live completion gap; the other items remain open.
OpenRouter remains unqualified. No runtime source, shared Codex root,
system-wide dependency, remote repository, commit, or push was changed.

## 2026-09-22 — LM Studio completed live round trip

The earlier ten-minute incomplete run above is preserved as historical
evidence. A later run completed the minimal live tool path:

- Fresh application root:
  `D:\CODING\NeoBabylon-Data\Phase1B-ServerLog-20260922T183103Z`; its
  `Live\Data\CodexHome` and fixture workspace were distinct from the source
  repository and ordinary Codex root.
- Mock Responses ran first and passed. The live process used the locked Codex
  App Server `0.155.1`, source revision
  `be2951ea34f0d295ed0becf97079f92fa5f6950e`, binary SHA-256
  `253C6D8424EA45BA9D36D1F665D9B7B4C917782D4CF5D56E61CD0664CC52BF0D`.
- LM Studio `0.4.25.0`, endpoint `http://127.0.0.1:1234/v1`, selected model
  `phase1a-qwen3-14b`, variant `qwen/qwen3-14b@q4_k_m`, context `32768`, and
  advertised `tool_use` matched the canonical capability record.
- Under explicit `unelevated` policy, raw `workspace-write` mapped to
  effective `workspaceWrite`, network off. `cmd.exe /d /c ver` completed with
  exit code 0 and returned Windows `10.0.26200.9457`; App Server emitted
  `turn/completed` after `275,918` ms.
- Codex effective model/provider matched. Catalog context `32768` mapped to
  session context `31129` at the explicit 95% adapter projection. No native
  metadata fallback warning or provider/model mismatch was recorded. This
  verifies effective context state, not request budgeting or compaction.
- LM Studio's server logs explicitly warned that `namespace` and
  `web_search` tool types, `prompt_cache_key`, and
  `reasoning.encrypted_content` were unsupported and ignored, and that the
  developer role was replaced with system. The tested `exec_command` path
  completed; broader tool semantics remain unqualified.
- The isolated session journal is
  `D:\CODING\NeoBabylon-Data\Phase1B-ServerLog-20260922T183103Z\Live\Data\CodexHome\sessions\2026\09\22\rollout-2026-09-22T20-31-25-01a0ca63-10d5-78b2-981c-f2ee40e42fc7.jsonl`.
  Its full qualification snapshot is preserved at
  `D:\CODING\NeoBabylon-Data\Phase1B-Repeat-20260922T185400Z\previous-qualified-run.json`;
  the raw LM Studio log capture remains outside the repository.

This is one successful run, not a repeatability result. The visible WPF
click-through, live budget/compaction behavior, stale-capability invalidation,
OpenRouter, and the broader Phase 1B authority/recovery matrix remain open.

## 2026-09-22 — Live repeat and qualification reader regression

A subsequent live repeat used the same locked runtime and selected LM Studio
tuple with explicit `unelevated` restricted-token policy. The deterministic
mock passed first. The session journal at
`D:\CODING\NeoBabylon-Data\Phase1B-Repeat-20260922T185400Z\Live\Data\CodexHome\sessions\2026\09\22\rollout-2026-09-22T20-54-34-01a0ca78-456e-79d2-8c0e-f0e57e09472d.jsonl`
contains no `function_call`, `function_call_output`, or completed-turn record.
The captured LM Studio server/model logs show generation continuing to the
ten-minute qualification bound. This repeat did not exercise the sandboxed
command path and is not a policy-rejection result.

After the bound, the runner's immediate journal read raised an `IOException`
because the file was temporarily unavailable for sharing. The exact OS handle
owner was not captured. A later exclusive read succeeded after the App Server
process exited. The pre-fix runner artifact is preserved at
`D:\CODING\NeoBabylon-Data\Phase1B-Repeat-20260922T185400Z\qualification-runner-sharing-failure.json`;
the earlier completed-run snapshot remains at
`D:\CODING\NeoBabylon-Data\Phase1B-Repeat-20260922T185400Z\previous-qualified-run.json`.

The qualification collector now opens append-only journals with read/write/delete
sharing, retries transient I/O failures for a bounded interval, and returns a
typed `sessionEvidenceReadFailure` without discarding the turn observation. A
deterministic test using an exclusively locked journal failed before the fix
and passes after it. Post-fix verification: `17/17` host/core/qualification
tests passed; WPF host and qualification runner builds passed with zero
warnings/errors; pinned reference verification passed; explicit-`unelevated`
mock-only qualification passed. The current workspace artifact
`artifacts/phase1a/lmstudio/qualification.json` is that mock-only run.

The remaining repeatability limitation is real: one live turn completed in
275.9 seconds, while two attempts reached ten minutes (one had a successful
tool result without final turn completion; this repeat had no tool call). The
cause of generation variance remains unknown. No runtime source, ordinary
Codex data/configuration, system-wide dependency, or remote repository was
changed; no commit or push was made.

## 2026-09-22 — UI reference and context-budget source trace

The supplied `Codex first UI Reference for NeoBabylon.md` was read completely
and reconciled into [the canonical UI/UX reference](../architecture/UI_UX_REFERENCE.md).
Its Codex-first conversation hierarchy, progressive disclosure, composer-local
decisions, contextual inspection panes, inline approvals, and distinct terminal
drawer are adopted as Phase 2 design guidance. Its proposed shell-first build
order does not supersede the reviewed Phase 1B-first project sequence. The
current WPF/WebView2 host still loads a minimal dependency-free diagnostic
page; it has no production Codex-style shell. The supplied report's citations
are opaque in the provided file, so exact current labels and styling were not
independently verified.

The pinned App Server source trace establishes, for the current catalog values:

- LM Studio context `32768` and adapter `effective_context_window_percent=95`
  produce Codex usable context `31129`; the successful live session reported
  that same effective context.
- A null catalog `auto_compact_token_limit` does not disable Codex's derived
  default. Pinned `ModelInfo::auto_compact_token_limit()` derives 90% of the
  resolved context, bounded by any explicit limit; the default scope is
  `Total`. The source-derived trigger is `29491` for this model, with a
  separate `31129` usable-context hard cap.
- The turn loop checks token state before and after sampling and routes a
  reached limit into compaction. No live request reached or recorded that
  event, so dynamic budgeting/compaction and stale-capability invalidation are
  still unqualified.

The successful live run's LM Studio model-output record reports two responses:
`717` output tokens at `4.1908 tok/s` (`10.88s` time to first token,
`181.73s` total request time) and `266` output tokens at `3.5673 tok/s`
(`3.48s` time to first token, `77.76s` total). This accounts for most of the
`275.918s` end-to-end turn and explains why that successful pass was slow. It
does not explain the later ten-minute attempt: that run has no completed model
output statistics; its engine log's `n_tokens=5402` is not a verified output
token count or generation rate. Slow/non-completing repeat behavior remains
open, and no further long live generation was started.

Verification limitation: `cargo test --offline -p codex-protocol
model_context_window_limits_preserve_their_distinct_meanings` failed before
test execution because the pinned `tungstenite` Git dependency was missing
from the local Cargo cache. No dependency was fetched. This prevents running
that upstream unit test here; the source behavior above is code-inspection
evidence only.

## 2026-09-22 — post-reconciliation verification

After the documentation reconciliation, a fresh local verification pass
passed all `17/17` host/core/qualification checks, built the WPF host and
qualification runner with zero warnings/errors, and passed
`scripts/verify-reference.ps1` against `rust-v0.155.1` /
`be2951ea34f0d295ed0becf97079f92fa5f6950e` and App Server `0.155.1`. A local
Markdown-link check also passed for `50` links across `27` project documents.

The visible WPF click-through remains unverified. The Windows automation
runtime failed during initialization with `failed to write kernel assets: The
system cannot find the path specified`; no UI action or inference was started.

The targeted upstream context-window Rust test was retried with `--locked` and
a NeoBabylon-specific Cargo home. Cargo attempted Git/registry resolution but
stopped before compilation because it would need to update the pinned checkout's
`Cargo.lock`. `--locked` prevented that change; the lockfile remained
unchanged. No lock relaxation or upstream source edit was made, so dynamic
context-budget/compaction behavior remains unqualified.

## 2026-09-22 — premature App Server stream closure

A deterministic child-process fixture completed `initialize`, accepted
`turn/start`, emitted `turn/started`, and exited with code `23` before a
terminal turn event. Before the fix, the new regression failed because
`WaitForTurnCompletionAsync` leaked `ChannelClosedException` and returned no
partial observation. It now returns an incomplete-turn failure with the
already received notifications retained; the host wraps that observation in
its typed `appServerTurn` failure shape attributed to Codex App Server.

Fresh verification passed all `18/18` host/core/qualification checks, including
the child-process regression. The WPF host and qualification runner built with
zero warnings/errors, and pinned-reference verification passed. This tests the
transport failure path, not visible WPF rendering or live-provider recovery.
No pinned runtime source was changed.

## 2026-09-22 — interruption protocol and typed terminal status

The pinned protocol defines `turn/start` as returning a `turn.id`,
`turn/interrupt` as requiring both `threadId` and `turnId`, and
`turn/completed` as carrying a status that can be `completed`, `interrupted`,
or `failed`. The initial interruption regression used a deterministic child
App Server. Before the fix it failed because a `turn/completed` event with
`turn.status = interrupted` was incorrectly reported as successful completion.

NeoBabylon now retains the App Server-issued turn ID, routes a named
`interruptTurn` operation through the host, sends the exact thread/turn pair,
and reports an interrupted terminal event as `turnInterrupted` with
`completed = false`, `interrupted = true`, and no synthetic failure. Unknown
terminal statuses, timeouts, and early protocol-stream closure remain visible
as failures/incomplete observations. Request IDs are allocated monotonically
for host-issued protocol calls, and overlapping thread/turn starts are refused.

The child-process regression verifies the exact interrupt identifiers and
that a returned `interrupted` status is terminal but not successful. This is
deterministic protocol evidence only: an actual pinned-server live interruption,
no-zombie behavior, WPF button click-through, and renderer recovery are still
unverified. This slice does not close Phase 1B.

## 2026-09-23 — OpenRouter visible live tool path and journal failure typing

The selected free OpenRouter target completed one bounded live run through the
visible WPF host. Before inference, the current public catalog/endpoints were
rechecked and the explicit deterministic Responses mock passed under the same
selected model and Windows policy. The test used:

- Pinned App Server `0.155.1`, source revision
  `be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
  `253C6D8424EA45BA9D36D1F665D9B7B4C917782D4CF5D56E61CD0664CC52BF0D`.
- OpenRouter model `nex-agi/nex-n2.5-pro:free`; public canonical slug
  `nex-agi/nex-n2.5-pro-20260907`; one current public Nex AGI FP8 candidate,
  zero listed prompt/completion prices, and listed expiry `2026-09-25`.
- Application root
  `D:\CODING\NeoBabylon-Data\Phase1B-OpenRouter-Host-Live-20260923`, with
  `Data\CodexHome` and the fixture workspace isolated under that root, separate
  from `D:\CODING\NeoBabylon` and ordinary `%USERPROFILE%\.codex`.
- Explicit Windows `unelevated` restricted-token policy. The thread response
  reported `workspaceWrite`; network access was disabled.

The visible WPF host started the exact OpenRouter thread and made one
`exec_command` call for `cmd.exe /d /c ver`. The isolated journal pairs the
function call/output by `call_id`, records exit code 0 and Windows
`10.0.26200.9457`, and reports `task_started.model_context_window=249036`.
The generated catalog carried context `262144` with the 95% adapter setting.
App Server emitted `turn/completed`; the visible host showed
`toolOutcomeStatus=succeeded`, `toolEvidenceReadStatus=read`, and attributed the
turn to the requested model while labeling the actual endpoint provider
unverified at display time. After the run, a read-only GET to OpenRouter's
generation metadata endpoint using this response ID attested provider Nex AGI,
model `nex-agi/nex-n2.5-pro-20260907:free`, `total_cost=0`, `is_byok=false`,
`streamed=true`, and `api_type=completions`. This is evidence for one response,
not request-level provider pinning or a guarantee for future routing; no second
inference was sent. See [OpenRouter generation metadata](https://openrouter.ai/docs/api/api-reference/generations/get-generation).
A post-run scan of 962 text/config/session files in that app root
found no exact occurrence of the Pi-stored key. The key itself was not printed
or written to any project/app file.

The earlier C: LocalAppData test remains a separate preserved failure. Its
ordinary command was denied before process start because the pinned Windows
restricted-token backend could not enforce the split writable-root sets; the
command itself did not run. A new regression first failed because journal
function call/output records were not converted to typed diagnostics, then
passed after the host gained a bounded reader restricted to the exact
`thread.path` under isolated `CodexHome\sessions`. The new reader was also run
against that preserved failure journal and produced `outcome=failed`,
`failure.type=toolExecution`, and no raw journal output. The current WPF live
run exercised the success notification path; the failure projection itself is
covered by the deterministic regression and the preserved-journal parser run,
not by replaying the failed turn through the visible host.

Fresh local verification at this checkpoint: `26/26` host/core/qualification
checks passed; WPF host build completed with zero warnings/errors; the D:
OpenRouter mock-first qualification passed. A subsequent C: LocalAppData
mock-only rerun under the current parent environment reproduced the same
fail-closed split-root error without launching the command. Corresponding
session/config paths under `AppData\Local\NeoBabylon` and the Codex package's
`LocalCache\Local\NeoBabylon` returned the same NTFS file IDs; no reparse
point was reported for either `NeoBabylon` directory. This supports a path
alias explanation, but the exact compared writable-root sets remain unavailable.
The C: failure artifact is preserved separately. The host-only WPF capture is preserved at
`artifacts/phase1b/openrouter/host-live-after-turn.png`; the live session is
under the application root above.

At that earlier checkpoint, the run closed per-response provider attribution,
but not request-level route pinning or no-fallback behavior, nor all of Phase
1B. Live reasoning telemetry, structured
output, enforced output limits, repeatability, effective request budgeting and
compaction, stale-capability invalidation, recovery, approval variants, broad
Windows containment, and live interruption remain open. LM Studio's repeat
variance and other tool semantics also remain open. No dependency was
installed, no Codex runtime source/shared config was changed, and no commit or
push was created.

## 2026-09-23 — WPF history flow and first Codex-first shell slice

The local React/TypeScript UI builds with no external runtime resources. The
WPF/WebView2 smoke at `1464×901` verified a real saved OpenRouter conversation
can be listed, resumed, restored from App Server-owned history, closed from the
current view with “New task” while retaining its session, and reopened again.
The saved thread's provider/model/workspace identity is checked against the
canonical capability and isolated fixture workspace before `thread/resume`;
the resume and turns-list requests use only named pinned App Server methods,
and the renderer displays a bounded latest-20-turn transcript without creating
a second durable history store.

Negative-path checks verified that a mismatched selected model is rejected
before resume and that, without an OpenRouter key, a new provider turn fails
before App Server receives any turn event. These history and failure-path UI
smokes did not send inference. They recorded no external browser-resource
requests, console errors, or viewport overflow. The UI message reducer test
covers streaming delta → completed item → final-text fallback and confirms one
assistant bubble; `2/2` reducer tests pass.

Fresh checks at the original UI checkpoint: `npm test` passed `2/2`;
`npm run build` passed with TypeScript and Vite; `dotnet run --project
tests/host/NeoBabylon.Phase1A.Tests.csproj --no-restore` passed `31/31`; and
the WPF host build completed with zero warnings and errors. WPF evidence,
screenshots, and isolated runtime snapshots
are under
`D:\CODING\NeoBabylon-Data\Phase2-UI-Live-20260923`.

At that checkpoint, the runtime identity was App Server `0.155.1`, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, binary SHA-256
`cb53784b951b42cb2925b71a14feaea61ae09154b69646fcf18e4903b1537187`, and
route-patch SHA-256
`e38ea940308a61bd56c0315b08141f6bd801075a0d79f0f5ad6d12324c74c1f5`.
That OpenRouter WPF live run completed `cmd.exe /d /c ver` with exit
code 0 and displayed attributed success. Its Codex journal context was
`249036` versus provider capability context `262144`; effective live reasoning
remains Unknown. OpenRouter generation IDs
`gen-1790130176-ByDpeKxpIWRxxpazyGyM` and
`gen-1790130192-Dwfs4kEFfxP4Aa6Mt8bn` report Nex AGI, canonical model
`nex-agi/nex-n2.5-pro-20260907:free`, and zero cost, but zero generation token
counters disagree with Codex's 12,596 input / 94 output / 47 reasoning-output
counters. The generation response does not echo the exact route tag. Do not
claim token-counter reconciliation or provider-side endpoint attestation.

This progress does not close Phase 1B or the full Phase 2 gate. Remaining
material gaps include LM Studio repeatability/visible-host qualification,
effective request budgeting and compaction, provider usage-counter
reconciliation, live interruption, approval variants, broad Windows
containment, C: standalone-host app-root behavior, restart/fork/renderer
recovery, and UI workspace selection, approvals, diffs/review, reconnect,
history pagination/search, accessibility, and long-output handling. No commit,
push, remote repository, system-wide dependency, or shared configuration
change was made.

### 2026-09-23 — Transcript bridge projection hardening

The host now projects saved history to user/assistant text before returning
it to React. Raw command/function records, tool outputs, and attachment
payloads are omitted; non-text user content is marked as omitted. The
aggregate display text is limited to 120,000 characters and the UI exposes a
truncation notice. This is a bounded presentation preview only; App Server
history remains authoritative.

Fresh verification after the projection change: host/core tests passed
`32/32`; `npm test` passed `2/2`; `npm run build` passed TypeScript and Vite;
and the WPF host build completed with zero warnings and errors. The actual
WPF/WebView2 run at `1464×901` restored the isolated conversation twice,
confirmed “New task” preserves the saved session, and verified the host
response contains only the projected user/assistant text, with no tool-call
records. The smoke had no console errors, external browser requests, or
viewport overflow. The projector's 120,000-character cap is unit-tested;
a deliberately oversized transcript has not yet been exercised in the
rendered host. Screenshots and smoke evidence remain under
`D:\CODING\NeoBabylon-Data\Phase2-UI-Live-20260923`.

At the time of this hardening pass, Astryx remained the accepted UI direction
but the shell had not yet adopted a package. The later dated adoption and
verification record below supersedes that current-state note; it does not
close Phase 1B or the full Phase 2 gate.

### 2026-09-23 — LM Studio same-tuple live repeat

A fresh read of LM Studio's local model API showed the already-loaded
`phase1a-qwen3-14b` (`qwen/qwen3-14b@q4_k_m`, quantization `Q4_K_M`) at
32,768 context and advertising `tool_use`; server version remained `0.4.25.0`.
No model was substituted. The deterministic mock Responses round trip passed
before the live request. The pinned App Server then completed the same
`cmd.exe /d /c ver` tool path in 289.8 seconds, returning Windows version
`10.0.26200.9457` and exit code 0. Codex effective provider/model matched
`lmstudio` / `phase1a-qwen3-14b`; configured context `32768` projected to
effective `31129`; metadata fallback and provider/model mismatch were both
false. Reasoning and structured-output support remain Unknown.

The raw tool output also contained PowerShell profile/terminal-initialization
warnings before the successful command result. This run did not exercise the
WPF live click path or context-budget/compaction behavior. Two older
same-tuple attempts still reached the ten-minute bound, so repeatability is
not qualified. The current ignored run evidence is
`artifacts/phase1a/lmstudio/qualification.json`; the previous JSON is preserved
outside the repository at
`D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-Repeat-20260923T051010\previous-qualified-run.json`.

### 2026-09-23 — Visible WPF/WebView2 LM Studio live path

The diagnostic host was launched against a new isolated application root
`D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-WPF-20260923\App`, the canonical
LM Studio capability record, and explicit `unelevated` Windows policy. The
visible screen showed provider `lmstudio`, model `phase1a-qwen3-14b`, variant
`qwen/qwen3-14b@q4_k_m`, and the expected local endpoint. The session journal
records the model's exact function-call arguments as
`{"cmd":"cmd.exe /d /c ver","shell":"cmd.exe"}`, context `31129`, and
`task_complete` after `212.674s`. The App Server thread list reported the
same thread `idle`; the rendered shell reached terminal `COMPLETED` and
displayed attributed success with the Windows version.

Playwright over the WPF WebView2 debugging port verified page identity
(`https://neobabylon.local/index.html`, title `NeoBabylon`), visible
provider/model/policy, the completed conversation and success card, `1464×901`
viewport/document dimensions, no console errors, and no external resource
requests. The screenshot is
`D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-WPF-20260923\live-turn-completed.png`.
The Browser plugin was unavailable, so local Playwright was used. This does
not qualify the two earlier ten-minute non-completions, real context
budgeting/compaction, interruption, or the broader approval/sandbox matrix.

### 2026-09-23 — Live LM Studio interruption through the WPF Stop control

In the same isolated WPF host and selected LM Studio model, a long text-only
turn produced an `item/agentMessage/delta`; only then was the existing Stop
button clicked. The UI reached `interrupted`, removed the Stop control, and
re-enabled the composer. A named `listThreads` operation attributed to Codex
App Server returned the matching `lmstudio` / `phase1a-qwen3-14b` thread with
status `idle`. The isolated journal showed zero function calls for the
interrupted turn. Playwright recorded no console errors. The screenshot is
`D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-WPF-20260923\interrupted-after-token-delta.png`.

This proves one active-generation cancellation through this host and provider;
it does not prove process-tree absence, repeated cancellation reliability,
OpenRouter interruption, server restart, or renderer recovery. The first
smoke harness had a post-disconnect assertion error; a fresh read-only browser
check and journal/thread evidence confirmed the resulting terminal state.

### 2026-09-23 — focused pinned-source tests and desktop-width UI smoke

After authorization to acquire local build dependencies, the two previously
blocked focused Rust tests passed from the existing dirty runtime checkout
`D:\CODING\NBRT-RouteControl\codex-rs`, both with `--locked`:

- `cargo test --locked -p codex-protocol model_context_window_limits_preserve_their_distinct_meanings --lib`
  passed `1/1`. This checks static context-limit semantics; it is not evidence
  of a live request crossing the budget threshold or triggering compaction.
- `cargo test --locked -p codex-api direct_serialization_preserves_websocket_request_payload --lib`
  passed `1/1`. The test asserts that WebSocket request serialization retains
  the OpenRouter `provider.only=["nex-agi/fp8"]` route and
  `allow_fallbacks=false`, along with the rest of the request payload.

No source cleanup, reset, or lockfile relaxation was performed. These focused
tests do not replace the upstream workspace suite or runtime integration
qualification.

The visible WPF/WebView2 page was smoke-tested through the existing local
Playwright fallback (the Browser plugin was unavailable). At `1464×901`, the
page identity was `https://neobabylon.local/index.html` / `NeoBabylon`, the
diagnostics dialog opened and closed, and no console errors or horizontal
overflow were observed. Emulated viewport checks at `640`, `768`, `960`,
`961`, and `1464` pixels had no horizontal overflow. A `390`-pixel emulation
does overflow because the page intentionally retains a `640px` minimum below
the tablet breakpoint; the WPF window's declared `MinWidth` is `960px`, so
390px is outside this desktop host's supported size, not a qualified mobile
layout. Evidence screenshots are outside the repository under
`D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-WPF-20260923\ui-*.png`.

### 2026-09-23 — WPF/App Server restart, renderer recovery, and favicon

The WPF host was closed through its normal window shutdown. Process inspection
confirmed its App Server process exited; no NeoBabylon App Server process
remained for that isolated root. The host was relaunched with the exact same
application root, LM Studio capability record, and explicit `unelevated`
policy, then the WebView page was reloaded. The saved-thread list returned one
thread; resuming it rendered four user/assistant messages and left the thread
`idle`. No provider request was sent. Runtime details still showed the same
`D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-WPF-20260923\App` root and stated
that the ordinary Codex data root was not used.

The first post-restart browser check exposed that the HTML had no declared icon
and WebView2 requested missing `/favicon.ico`. A Vite-serving regression test
was written first and observed failing because the served page declared no
icon; after adding a local SVG asset and explicit link, the test passed. Fresh
UI verification passed `npm test` (`3/3`) and `npm run build`; the built
`dist/favicon.svg` was served at `/favicon.svg` with HTTP 200. On the actual
WPF/WebView2 page, renderer reload followed by saved-thread resume again
reached `idle`; no console errors, failed requests, external requests, or
viewport overflow were observed at `1464×901`. Screenshot:
`D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-WPF-20260923\post-restart-resume.png`.

This is one clean host/App Server restart and renderer reload/resume pass, not
crash/forced-termination recovery, continuation/fork, or no-zombie proof after
interruption. Browser-plugin absence remains; local Playwright was used.

### 2026-09-23 — Local saved-history search in WPF/WebView2

The UI regression suite passed `5/5`; focused tests cover trimmed,
case-insensitive matching across preview/provider/model, preservation of the
App Server list order for an empty query, and the no-match result. TypeScript
type-check plus Vite production build passed. On the actual WPF/WebView2 page
(`https://neobabylon.local/index.html`, `1464×901`), the toolbar search button
focused the search field; searching the selected `phase1a-qwen3-14b` model
showed the saved thread; a unique miss produced a visible empty state; clearing
the search and selecting the result resumed the same App Server thread with
four projected messages and returned it to `idle`. There were no console
errors, external requests, or horizontal overflow. This was a non-inference
history/UI check under the same isolated application root; it did not touch
provider settings or ordinary Codex data. Screenshot:
`D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-WPF-20260923\search-ui-verified.png`.

This closes only basic local history filtering and its visible click path.
App Server pagination, full-history search, large-list performance, and
accessibility review remain open.

### 2026-09-23 — App-owned fork bookmark, restart, and live continuation

The WPF host created fork `01a0ccba-8697-7de2-8a03-d95eeb689e11` from
`01a0cc49-9aa4-7aa1-9e9c-ced89ac98354` using the selected LM Studio capability
under
`D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-WPF-20260923\App`. Upstream
`thread/list` omitted the previewless fork, and `thread/read(includeTurns=false)`
returns only metadata, not transcript history. NeoBabylon therefore persists
only a bookmark tuple (thread id, provider, model, workspace, parent, timestamp)
under isolated app `Data`; after restart it asks App Server to revalidate that
identity before projecting the fork into the list. The merged list reported
two threads, one verified bookmark, and zero unresolved bookmarks. Once the
fork had a preview, the App Server scan still omitted `forkedFromId`; the
metadata-only validation enriches that relation without copying turns.

The visible WPF/WebView2 host was gracefully restarted against the same
application root. The bookmarked branch was listed and resumed as the same
runtime thread, then one local text-only Qwen continuation returned the exact
requested text. The request explicitly prohibited tools; the isolated journal
recorded zero function calls. The UI showed the branch icon/`BRANCH` label,
parent identity, selected provider/model, and successful completion. Local
Playwright verified two visible list entries, one verified bookmark, zero
unresolved bookmarks, no console errors, no failed or external requests, and
no viewport overflow at `1464×901`. Screenshot:
`D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-WPF-20260923\fork-branch-label-verified.png`.

This closes only one same-provider fork→restart→resume→continuation path. It
does not prove forced-crash recovery, repeated or cross-provider fork behavior,
bookmark corruption/recovery, nor process-tree absence after interruption.

### 2026-09-23 — Inline approval UI and fail-closed decision metadata

The pinned App Server protocol source at
`be2951ea34f0d295ed0becf97079f92fa5f6950e` marks command
`availableDecisions` optional and experimental in
`app-server-protocol/src/protocol/v2/item.rs`. A new regression first showed
that the UI offered `Approve once` when this field was absent; the matching
host-validator regression showed that the same response was accepted. Both
tests failed for the intended reason before the fix. NeoBabylon now presents
command acceptance only when App Server explicitly includes `accept`; absence
or an unadvertised choice stays fail-closed. Denial remains available. Command
grants are one-shot only. File-change requests are deny-only because the pinned
request schema contains no diff or path context that can be reviewed; the UI
explains this and withholds accept, and the host independently rejects accept.
`grantRoot` cannot expand persistent write scope, permission grants copy only a
recognized requested profile and apply for the current turn, and unknown
authority request methods return a typed error response.

Fresh automated verification:

- `npm test` passed `17/17` approval, theme/asset, history, and transcript
  tests.
- `npm run build` passed TypeScript and Vite production compilation. Rolldown
  emitted non-fatal upstream `use client` directive warnings from dependencies.
- `dotnet run --project tests/host/NeoBabylon.Phase1A.Tests.csproj
  --no-restore` passed `39/39`. The fake App Server interprocess test exercised
  explicit command acceptance, file-change denial, exact turn-scoped
  permission grant, unknown-request denial, and host waiting for the decision.
- Release WPF host publish passed to
  `D:\CODING\NeoBabylon-Data\Phase1B-Approval-UI-20260923\publish-final` and
  was relaunched against the existing isolated LM Studio application root. A
  live process inspection confirmed host PID `56020` owns WebView2 PID `2684`
  and pinned App Server PID `44860` (`--listen stdio://`). The server binary
  SHA-256 was
  `cb53784b951b42cb2925b71a14feaea61ae09154b69646fcf18e4903b1537187`.
  No provider inference was sent during this approval UI test.

Rendered UI verification used local Playwright with installed Microsoft Edge
because the Browser plugin was unavailable. At `http://127.0.0.1:5173/`, the
page identity was `NeoBabylon`, the page rendered meaningful content with no
framework overlay, and browser console errors/warnings were empty. At
`1440×900`, an advertised one-shot request displayed its command, working
directory, and inline actions; clicking `Approve once` sent exactly
`respondToApproval { approvalRequestId: 101, decision: "accept" }`, removed the
pending card, and showed attributed approval and completion. A second request
without `availableDecisions` showed no accept option; clicking `Deny` sent
`{ approvalRequestId: 102, decision: "decline" }` and showed a visible denied
activity. A file-change prompt lacking a diff rendered the explicit withheld-
approval explanation, offered no accept action, and sent only the safe decline
when selected. The WPF-supported `960×720` viewport had no horizontal overflow. A
`390px` emulation overflowed to the CSS `640px` minimum; this is below the
WPF window's declared `960px` minimum and is not mobile-layout qualification.

Screenshots are outside the repository under
`D:\CODING\NeoBabylon-Data\Phase1B-Approval-UI-20260923\qa`:
`approval-pending.png`, `approval-unknown-decisions.png`,
`approval-denied.png`, `approval-file-change-no-diff.png`,
`minimum-desktop-width.png`, and the out-of-range `approval-narrow.png`.

This verifies the UI bridge and fake App Server response machinery, not a live
approval prompt from the pinned App Server or LM Studio. Phase 1B still needs
real prompt generation for supported stable categories and broader authority
coverage; Phase 2 still needs workspace selection, diff/review, reconnect and
draft recovery, accessibility, and long-output qualification.

CODEX Helper review was attempted twice but its in-app browser kernel failed
to initialize (`failed to write kernel assets: The system cannot find the path
specified`). Under the authorized unattended-work rule, an adversarial
self-review was used instead; no independent external review is claimed.

## 2026-09-23 — Dark-first appearance preference

Implemented a dark first-run appearance with an accessible light/dark toggle.
The selected mode is stored under the versioned WebView2 local-storage key
`neobabylon.appearance:v1`; WebView2's user-data folder is under the isolated
application `Data` root. The toggle updates the document's `data-theme` and
the Astryx `Theme` provider mode together, so native controls and the app
palette use the same color scheme. A missing, invalid, or inaccessible saved
preference defaults to dark; storage failure does not prevent changing the
current session's appearance.

Verification: the React suite passed `22/22`; TypeScript typecheck and
production Vite build passed. Playwright using installed Microsoft Edge
verified initial dark mode, switching to light, exact original light shell
color, local persistence through reload in both directions, no browser console
errors/warnings, and no horizontal overflow at `1440×900` or `960×720`. The
dark shell rendered as `rgb(29, 34, 42)` and the restored light shell as
`rgb(251, 252, 254)`. These checks used Vite without the WPF host, so they
verify renderer appearance and preference behavior, not host startup or a
provider turn. Screenshots are under
`D:\CODING\NeoBabylon-Data\Phase2-dark-mode-20260923\qa-tools`.

A separate framework-dependent WPF publish at
`D:\CODING\NeoBabylon-Data\Phase2-dark-mode-20260923\publish` launched with
source root `D:\CODING\NeoBabylon` and a distinct application root
`D:\CODING\NeoBabylon-Data\Phase2-dark-mode-20260923\AppRoot`. Its actual
WebView2 runtime was Edge `153.0.4234.48`; the process command line placed its
profile at `AppRoot\Data\WebView2\EBWebView`. With no model turn sent, the
visible host reported App Server `0.155.1`, rendered the dark appearance, and
reported `Ordinary Codex root used=false`. The UI preference switched to light,
survived a WPF WebView reload, switched back to dark, and survived a second
reload. Browser console errors/warnings were empty. Screenshot:
`D:\CODING\NeoBabylon-Data\Phase2-dark-mode-20260923\qa-tools\webview2-host-dark.png`.

## 2026-09-23 — Offline Windows DELETE boundary probe

LM Studio was unloaded. The explicit named mock-probe mode loads the accepted
LM Studio capability record without querying or loading a provider; successful
checks used the deterministic local Responses fixture and sent no LLM request.
NEX was not needed. The qualification fixture creates separate source and
application roots with protected DACLs, grants only the current user, SYSTEM,
and Administrators, and rejects an Everyone ACE.

Fresh App Server integration evidence:

- The workspace write succeeded and created its expected marker.
- A sibling parent write was attempted by the tool and denied; the target
  remained absent. This corrects the earlier invalid broad-ACL fixture result.
- The first PowerShell delete attempt was rejected before process creation and
  is not counted as sandbox evidence. The probe was tightened to require an
  execution marker and a real OS denial, then used `cmd.exe /d /c del` against
  one disposable canary in `Mock\Data`, outside the
  `Mock\Data\Workspace` thread cwd. On two fresh protected application roots,
  the command ran, emitted `PROBE_OUTSIDE_DELETE_OK`, returned exit code 0, and
  removed the canary. App Server reported `workspace-write`, Windows mode
  `unelevated`, and `toolSucceeded=true` / `toolRoundTrip=true`. This is a
  **containment failure**, not a passing denial test. The updated qualification
  test exits nonzero with `SECURITY FAILURE` when it observes this outcome.
- Exact runtime on both runs: App Server `0.155.1`, source revision
  `be2951ea34f0d295ed0becf97079f92fa5f6950e`, binary SHA-256
  `cb53784b951b42cb2925b71a14feaea61ae09154b69646fcf18e4903b1537187`,
  source-patch SHA-256
  `e38ea940308a61bd56c0315b08141f6bd801075a0d79f0f5ad6d12324c74c1f5`.
  Effective-authority evidence names the Codex Windows restricted-token
  sandbox; no provider inspection, model load, or inference occurred.
- Preserved evidence:
  `D:\CODING\NeoBabylon-Qualification-Probe-742a44f8c37546c58c4142760e039b23\parent-delete-evidence.json`
  and repeat
  `D:\CODING\NeoBabylon-Qualification-Probe-4f31743b6f2a41daa69c7458fad61e90\parent-delete-evidence.json`.
  The full qualification invocation returned exit code 1 with `SECURITY
  FAILURE`; its final canonical artifact is overwritten by the later
  unsupported-probe check, so the per-probe copies above are the evidence for
  deletion.

The focused pinned-source test
`legacy_workspace_write_delete_is_limited_to_writable_roots` had also allowed
outside-file and protected `.git` deletion under its protected fixture. This
is consistent with, but does not alone prove the cause: the source creates a
`WRITE_RESTRICTED` token; Microsoft's documentation says that flag considers
restricting SIDs only when evaluating write access, while Windows defines
`DELETE` and `FILE_DELETE_CHILD` as distinct standard/file rights. See
[CreateRestrictedToken](https://learn.microsoft.com/en-us/windows/win32/api/securitybaseapi/nf-securitybaseapi-createrestrictedtoken),
[File Access Rights Constants](https://learn.microsoft.com/en-us/windows/win32/fileio/file-access-rights-constants),
and [File Security and Access Rights](https://learn.microsoft.com/en-us/windows/win32/fileio/file-security-and-access-rights).
The denied-data-write / successful-delete mismatch is a strong,
source-consistent inference, not a completed root-cause review.

`scripts/verify-reference.ps1` passed the manifest, baseline metadata,
protocol outputs, startup preservation, and pinned binary-version checks. The
qualification runner Release build passed with 0 warnings and 0 errors;
the host suite passed 39/39. The React suite passed 23/23; TypeScript checking
and the production Vite build passed with non-fatal upstream `use client`
directive warnings. The deterministic mock qualification suite now correctly
fails at the deletion acceptance check while its workspace-write,
parent-write-denial, and unsupported-probe checks pass. CODEX Helper review
could not be reached because the in-app browser helper failed to initialize
(`failed to write kernel assets: The system cannot find the path specified`).
No runtime source patch was made. A focused, independently reviewed runtime
correction and direct DELETE/FILE_DELETE_CHILD regression are required before
Windows containment can pass; do not broaden policy or mark Phase 1B complete.

The same `MockProbeQualification.Tests.ps1` suite was rerun on 2026-09-23. Its
workspace-write marker succeeded, the sibling write remained denied, and the
unsupported probe failed closed; the isolated sibling deletion canary was
again removed by `cmd.exe /d /c del`. Fresh evidence is at
`D:\CODING\NeoBabylon-Qualification-Probe-190bb1f3c24f4466be764292a87c25c6\parent-delete-evidence.json`.
The suite returned exit code 1 with `SECURITY FAILURE`. The command's
PowerShell profile also attempted writes to four global Terminal-Icons XML
files, all denied by the sandbox; no global configuration was changed. This
confirms that the tested model-tool shell can touch host profile startup even
when those writes are denied, so do not “fix” the test by changing Martin's
global profile.

## 2026-09-23 — deterministic local HTTP 429 characterization

`tests/qualification/MockProviderError.Tests.ps1` passed against the pinned
App Server with `--mock-only --mock-provider-error 429`. The qualification
runner loaded the existing accepted LM Studio capability record offline and
did not inspect, load, or infer with LM Studio. It sent one Responses request
to the ephemeral loopback fixture
`http://127.0.0.1:55198/v1`; the fixture returned HTTP 429 with a typed
`rate_limit_error` body. No external provider request or tool execution
occurred.

Observed identity stayed `lmstudio` / `phase1a-qwen3-14b` (the accepted
`qwen/qwen3-14b@q4_k_m` / `Q4_K_M` record). The App Server emitted an `error`
notification with
`codexErrorInfo.responseTooManyFailedAttempts.httpStatusCode = 429`; the
request body used the selected model, and the effective model/provider
remained unchanged. There was no `function_call_output`, tool round trip, or
provider/model fallback. The App Server normalized the body message to
`exceeded retry limit, last status: 429 Too Many Requests`; its notification
did not preserve the fixture's original `mock rate limit` message. This
characterizes App Server error handling only: it is not a live LM Studio or
OpenRouter rate-limit test and does not qualify host UI presentation.
The runner labels this `Mock Responses 429 qualification: PASS`; the separate
`toolRoundTrip` evidence remains `false`, and the evidence explicitly records
`noToolCallObserved = true`.

The source and application roots were distinct protected test roots; the
application data root was
`D:\CODING\NeoBabylon-Data\429-Test-dcee6a4f59834f85b77166542c67250b\Data`.
Evidence is preserved at
`D:\CODING\NeoBabylon-429-Test-dcee6a4f59834f85b77166542c67250b\artifacts\phase1a\lmstudio\qualification.json`.
The exact runtime was App Server `0.155.1`, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, binary SHA-256
`cb53784b951b42cb2925b71a14feaea61ae09154b69646fcf18e4903b1537187`.

After the qualification change, the runner Release build passed with 0
warnings and 0 errors; the host suite passed 39/39; the React suite passed
23/23; and TypeScript checking plus the production Vite build passed. Vite
reported non-fatal upstream `use client` directive warnings. Reference
verification passed. The provider-error mock does not close any Phase 1B
acceptance gate, and the Windows deletion containment failure remains open.

## 2026-09-23 — typed host provider-failure propagation

The focused failure-path check found that `AppServerClient` handled an
`error` notification by assigning the complete `params` JSON string to the
turn's failure message. `RuntimeSupervisor` then wrapped that string in its
`appServerTurn` object, so the renderer's alert showed serialized JSON rather
than a readable provider message, and the host field had no separate native
error details.

The parser now extracts the App Server error message and retains the native
error object separately. The host failure payload preserves the
`appServerTurn` type and `Codex App Server` attribution and carries the native
details under `details`. A fake App Server regression emitted
`429 Too Many Requests` with
`codexErrorInfo.responseTooManyFailedAttempts.httpStatusCode = 429`; assertions
verified the readable message and retained 429 details in both the observation
and the host payload. This fixture made no provider request and ran no model
tool. Source inspection confirms the renderer displays turn failures in a
`role="alert"` region; a rendered WPF/browser failure-state check has not yet
been captured.

Verification after the change:

- `dotnet run --project tests\host\NeoBabylon.Phase1A.Tests.csproj --no-restore`:
  40/40 checks passed, including the red/green provider-error regression.
- `dotnet build host\NeoBabylon.Host\NeoBabylon.Host.csproj --no-restore`:
  succeeded with 0 warnings and 0 errors.
- `npm test` in `ui\diagnostic`: 23/23 passed.
- `npm run typecheck` in `ui\diagnostic`: succeeded.

The existing `MockProviderError.Tests.ps1` was also rerun against the pinned
App Server and the loopback-only 429 fixture; it passed and made no external
provider request. Its fresh evidence is
`D:\CODING\NeoBabylon-429-Test-777f9dcde006484baaba0b71a689ba5a\artifacts\phase1a\lmstudio\qualification.json`.
Windows PowerShell 5.1 could not parse this UTF-8 script, so it was run using
the already-installed bundled PowerShell 7 executable with a process-scoped
execution-policy bypass; no machine or user policy was changed.

This closes only the deterministic host failure-payload gap. Live provider
429/unavailability, rendered failure-state behavior, Windows containment, and
the remaining Phase 1B acceptance items are still open.

## 2026-09-23 — malformed App Server JSON during a turn

A fake App Server completed `initialize` and `turn/start`, emitted
`turn/started`, then wrote malformed JSON and exited. The host returned a
non-completed turn observation, preserved the partial notification, and
reported `JsonReaderException` in its protocol-stream-closure diagnostic.
The adjacent fake-server case emitted an unrecognized but valid
`future/progress` notification before `turn/completed`; the host preserved its
trace ID and completed the turn normally. Existing protocol tests also verify
that unsupported authority-bearing requests are denied. A separate fake
server exited before replying to a pending request; the host surfaced
`EndOfStreamException` rather than success or timeout. Broader crash/recovery
qualification remains open. These fixtures used isolated temporary Codex
homes and made no provider request or model/tool call.

`dotnet run --project tests\host\NeoBabylon.Phase1A.Tests.csproj --no-restore`
passed all 43 checks, including unexpected server exit and interruption
regressions. No production runtime or shared configuration changed.

## 2026-09-23 — pinned Windows sandbox DELETE regression

To turn the live mock-probe escape into a reproducible upstream regression,
the prescribed focused test was run from the clean pinned runtime worktree at
revision `be2951ea34f0d295ed0becf97079f92fa5f6950e`:

```text
just test -p codex-windows-sandbox legacy_workspace_write_delete_is_limited_to_writable_roots
```

The test failed on both configured attempts. It ran one matching test across
five binaries (193 tests skipped); the actual result tuple was
`(0, false, false, false, None, false)`, while the assertion expected
`(0, false, false, false, Some("outside"), true)`. In this fixture the
outside canary and protected `.git` entry were deleted. The result matches the
separate protected-root App Server reproduction above and confirms the
containment regression in the pinned source; no runtime source was changed.

Source inspection found the legacy backend creates a token with
`WRITE_RESTRICTED`; its own comments limit restricting-SID checks to writes.
Microsoft documents that behavior and separately defines `DELETE` and
`FILE_DELETE_CHILD`. This is a source-supported explanation for why data
writes can be denied while deletion succeeds, not an independently reviewed
remediation. A focused correction and direct delete-right regression remain
necessary. Until then, live model-directed tool execution through this
backend remains prohibited; deterministic mock tests that do not rely on
untrusted tools remain in scope.

## 2026-09-23 — offline OpenRouter child credential and shell-profile probe

The deterministic qualification runner now has one exact OpenRouter-only
offline probe. It loads the accepted NEX capability record, routes the mock
Responses request to a loopback fixture, supplies a synthetic credential only
to the isolated App Server, and asks the ordinary command tool to report only
whether `OPENROUTER_API_KEY` is present. The observed child reported it absent;
the effective model matched `nex-agi/nex-n2.5-pro:free`; the effective config
excluded the variable; the evidence artifact contained no synthetic credential;
and `providerInspection.liveInspectionPerformed` was false. No live model,
provider request, or user credential was used. The check does not close the
renderer, log, workspace, or full credential-leakage matrix.

The first mock execution exposed a separate profile mismatch: despite the
existing `use_profile = false`, the child loaded the user's PowerShell profile
and attempted `Export-Clixml` writes under the ordinary user Roaming tree. The
OS denied those writes; no shared file or configuration was changed. Pinned
source inspection shows `allow_login_shell` defaults to `true`, and the
PowerShell command builder adds `-NoProfile` only for a non-login command. The
mock `exec_command` omitted a `login` override, so the configured default was
used. The pinned TOML parser names the profile field
`experimental_use_profile`; the old unprefixed key was not effective here.
NeoBabylon's isolated OpenRouter config now sets both
`allow_login_shell = false` and `experimental_use_profile = false`. The
red/green test confirms the effective values and that the rerun emitted no
profile diagnostics.

Verification:

- `tests\qualification\MockProbeQualification.Tests.ps1 -OpenRouterCredentialOnly`:
  passed after the red run identified the unsupported probe, then exposed the
  login-shell/profile mismatch. The final targeted run passed the local
  ordinary-tool round trip and all credential/profile assertions.
- `dotnet run --project tests\host\NeoBabylon.Phase1A.Tests.csproj --no-restore`:
  43/43 checks passed, including OpenRouter config generation and the existing
  host/runtime-boundary tests.
- The combined qualification script was also run once; it correctly failed
  its parent-delete assertion because the pinned App Server deleted the
  sibling canary in its isolated fixture. This is the fourth deterministic
  App Server reproduction of the DELETE escape. No repository or ordinary
  application data was targeted. The existing full containment failure remains
  a hard blocker, so no live model-directed tool call was attempted.

## 2026-09-23 — independent profile-policy parity check

A focused review found that the no-profile settings above were emitted only
for OpenRouter, although the same config builder also serves LM Studio. A new
LM Studio config-generation check failed against the prior builder because it
still allowed the pinned runtime's default login-shell behavior. The builder
now emits `allow_login_shell = false` and
`shell_environment_policy.experimental_use_profile = false` for both initial
providers; OpenRouter alone retains the `OPENROUTER_API_KEY` child-environment
exclusion. The host suite passed 44/44 checks after the change. The isolated
OpenRouter child probe passed again, reporting the synthetic credential absent
and no profile diagnostics. No LM Studio tool-child run was made for this
specific parity check. The Windows parent-delete containment failure remains
open, so no live model-directed tool request was attempted.

## 2026-09-23 — NeoBabylon fail-closed legacy Windows command guard

This entry supersedes the *current-runtime* status of the previous paragraph,
not its historical evidence. The known legacy `workspace-write` / `unelevated`
DELETE escape remains unfixed. A process-scoped NeoBabylon launch marker now
causes the pinned source runtime to reject both legacy command entrypoints
before token/ACL preparation or child-process creation. Standalone upstream
behavior without the marker is unchanged. This is a temporary execution stop,
not Phase 1B contained-tool acceptance; see
[NB-DEC-007](../decisions/0007-windows-legacy-containment-gate.md).

Source revision remains `be2951ea34f0d295ed0becf97079f92fa5f6950e`.
The active, uncommitted route-control worktree is `D:\CODING\NBRT-RouteControl`.
The rebuilt App Server reports `codex-app-server 0.155.1`; binary SHA-256 is
`dfa1b18d255f82c7fcaac0b7b83729ac1286a86a7d3783694983426ce08d5ab7`.
The complete active worktree diff SHA-256 is
`10d35018384e630abd65876fe31c3ec0004c3f84026842216189d8b302ece728`.
Both values are pinned in `runtime/runtime-lock.json`; the worktree diff also
includes the pre-existing OpenRouter route-control patch, not only this guard.

Red/green evidence:

- `dotnet run --project tests\host\NeoBabylon.Phase1A.Tests.csproj --no-restore`
  first failed the newly added launch-marker assertion; after the host change,
  it passed all 44 checks. The fake App Server child also confirmed the marker
  reached the actual spawned process.
- `just test -p codex-windows-sandbox
  neobabylon_contained_mode_rejects_both_legacy_entrypoints` failed twice
  before the guard (legacy session spawn was not rejected), then passed `1/1`
  after the guard.
- The default `tests\qualification\MockProbeQualification.Tests.ps1` ran
  against the old binary first: it returned exit 0, deleted the isolated
  sibling canary, and failed its guard assertion. Against the rebuilt locked
  binary it returned the expected tool-failure result (runner exit 2),
  `toolRoundTrip=false`, `toolSucceeded=false`, and an App Server
  `function_call_output` containing “NeoBabylon contained-tools mode rejects
  the unqualified legacy Windows sandbox”. A second red/green run added an
  assertion on the actual isolated journal projection: it first failed for
  a missing structured field, then passed with `typedToolFailure.type` set to
  `toolExecution`, attributed to `Codex App Server`, and
  `toolEvidenceReadStatus=read`. The sibling canary retained its original
  contents. Final evidence is under
  `D:\CODING\NeoBabylon-Qualification-Probe-e94f846e0946402890c1a020374a2c40`.
  This was a deterministic local Responses fixture, not live inference.
- `cargo fmt --package codex-windows-sandbox --check` passed with stable-toolchain
  warnings about an upstream nightly-only formatting option. `just fmt-check`
  could not complete its Bazel/Starlark step because its `buildifier` executable
  was absent; no broad formatter was run.
- `just test -p codex-windows-sandbox` ran 191 tests: 189 passed, 2 failed,
  4 skipped. The two failures reproduced individually: the setup binary's
  `lock_sandbox_dir_blocks_inherited_write_for_runner_files` could not create
  a new runner after ACL locking (`Access is denied`), and
  `elevated_non_tty_cmd_forwards_env_output_and_exit` could not start its
  elevated runner (`CreateProcessWithLogonW failed: 2`). These tests do not
  exercise the patched legacy entrypoints, but the suite is **not green**.
  The original outside/`.git` DELETE contract is intentionally ignored in
  ordinary runs with a visible reason: it must pass before enabling legacy
  commands, and the guard does not make that backend safe.
- `dotnet build host\NeoBabylon.Host\NeoBabylon.Host.csproj --configuration
  Release --no-restore` succeeded with zero warnings/errors.
  `tests\qualification\MockProviderError.Tests.ps1` still passed its
  deterministic 429/no-tool/no-fallback path after the qualification artifact
  change. React tests passed 23/23; `npm run build` succeeded with existing
  third-party `use client` bundling warnings, not a warning-free build.

The actual guarded failure has now passed through NeoBabylon's journal
diagnostic projection as a typed, attributed `toolExecution` failure. It has
not yet been rendered through WPF, so visible UI attribution remains an
integration verification gap. No live
provider request, shared Codex data/configuration change, commit, or push was
made for this correction.

## 2026-09-23 — native Windows containment candidate rejected on hard links

Martin directed clearing the Windows containment gate before further Phase 2
implementation and deferring the other unfinished Phase 1B checks until needed
or requested. This investigation did **not** clear the gate.

The pinned `codex-mxc-sandbox` source includes a native MXC/PSEC adapter. A
focused Windows test confirmed `is_process_security_environment_usable()`,
then used an isolated real `cmd.exe` child. An initial one-variable child
environment failed before spawn with Windows error 203; the SDK's own test
notes require a complete Windows environment block. With a credential-free
allowlist of ordinary OS variables and test-local TEMP/TMP, direct outside
and `.git` deletes were denied, outside reading and workspace deletion worked.
The next red test created a hard link *within the workspace* to an outside
canary before launch. The native child wrote through that link, changing the
outside canary from `outside` to `MUTATED\r\n`. The test failed again after
the exploratory Codex routing code was removed, using an explicit equivalent
read-root/workspace-write policy. The durable ignored regression is
`codex-rs/mxc-sandbox/src/lib.rs::neobabylon_native_rejects_hardlink_escape`.
It must fail when run with `--ignored` until a genuine containment fix exists.

Observed checks:

- `cargo test -p codex-mxc-sandbox neobabylon_native --lib -- --nocapture`:
  native availability 1 passed, hard-link regression 1 intentionally ignored.
- The focused hard-link regression with `--ignored --nocapture`: 0 passed,
  1 failed; direct outside and `.git` operations printed `Access is denied`,
  but the outside canary assertion observed `MUTATED\r\n`.
- An exploratory streamed `SpawnedProcess` adapter passed its initial simple
  canary test and compiled in `codex-sandboxing`, but that evidence was
  invalidated by the hard-link regression. Its entire routing/adapter branch
  was removed. The existing NeoBabylon launch marker still rejects legacy
  commands; no MXC path is reachable from the product.

The same source revision remains
`be2951ea34f0d295ed0becf97079f92fa5f6950e`. The locked App Server
binary remains byte-identical at SHA-256
`dfa1b18d255f82c7fcaac0b7b83729ac1286a86a7d3783694983426ce08d5ab7`.
Only the ignored regression/availability tests were retained in the runtime
source; the complete worktree diff digest changed to
`9a8f0e1ca6da4b69d72576c5cf6aa0eabfc58daa4d95504cb3135176c285b907`
and `runtime/runtime-lock.json` was updated to match. Excluding that test file
from the diff reproduces the previous lock digest exactly. No App Server
rebuild, live model request, shared sandbox setup, account/ACL change, commit,
or push was performed in this investigation. The separate-account elevated
backend is unqualified and its isolated-home provisioning would trigger an
interactive `runas` setup; current process integrity is medium.

Final non-inference integrity checks passed: the host suite reported 44/44
checks; `MockProbeQualification.Tests.ps1` again returned the attributed
legacy-tool rejection and preserved its isolated outside canary; `cargo fmt
--package codex-mxc-sandbox --check` passed with an upstream stable-toolchain
warning; and direct hash checks matched both the locked App Server executable
and complete runtime source diff. These checks verify the fail-closed state,
not contained ordinary-tool success or Phase 2 readiness.

## 2026-09-23 — elevated Windows backend isolated alias probes

With Martin's authorization for the elevated setup, four manual runtime tests
launched real `cmd.exe` children through the pinned separate-account elevated
backend. Each created a fresh workspace, outside canary, and isolated Codex
home under `D:\CODING\NeoBabylon-Data`; the test runner found byte-identical
setup and command-runner helpers in its build directory. The current product
App Server was **not** switched to this backend, and no provider inference was
sent. The four tests are ignored by default and require the explicit
`NEOBABYLON_SANDBOX_PROBE_ROOT` environment variable when run manually.

| Isolated probe | Observed child/result | Outside canary | Qualification |
| --- | --- | --- | --- |
| Pre-existing workspace hard link | Child exit 0 | Changed to `MUTATED\r\n` | Failed |
| Newly created hard link | Valid relative target; link created, child exit 0 | Changed to `MUTATED\r\n` | Failed |
| Pre-existing workspace junction | Child exit 0 | Changed to `MUTATED\r\n` | Failed |
| Newly created junction | Valid relative target; junction resolves to outside, child exit 0 | Changed to `MUTATED\r\n` | Failed |

These results are direct child/fixture evidence, not an App Server end-to-end
pass. The first two child-created-alias probes used malformed absolute targets
and produced false passes. Corrected relative-target tests verified that a
new hard link was created and that a new junction resolved to the outside
fixture; both then changed its canary. An `icacls` read after the pre-existing
hard-link run showed the outside canary and its workspace alias shared inherited
`CodexSandboxUsers` and capability-SID modify entries; the no-alias outside
canary did not have those entries. That supports setup-time ACL propagation
for one path. Dynamic alias failures show a startup scan alone is insufficient;
the exact authorization sequence and other alias classes remain untested. The ordinary NeoBabylon
legacy guard remains in effect, so contained ordinary-tool success is still
**not proved** and Phase 2 remains gated.

Official Windows documentation corroborates the object-identity hazard:
[hard links share the same underlying file and mutations are visible through
all names](https://learn.microsoft.com/en-us/windows/win32/fileio/hard-links-and-junctions),
while [file ACLs and inheritance are object-level security
mechanisms](https://learn.microsoft.com/en-us/windows/win32/fileio/file-security-and-access-rights).
Microsoft's [Windows Sandbox CLI](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-cli)
does not currently provide process I/O, so it is not a direct replacement for
the App Server's ordinary streamed tool path. The documented
[CreateProcessInSandbox API](https://learn.microsoft.com/en-us/windows/win32/secauthz/createprocessinsandbox)
is experimental and has not been qualified here. These are research leads,
not accepted implementation decisions.

The source revision is unchanged. The retained manual tests changed only the
runtime worktree diff digest to
`924d62510ff2b3a29c7ab889d0a27228e52d673fb54b5ce57756ef9a86f1dc03`;
`runtime/runtime-lock.json` was updated accordingly. The App Server binary
remains SHA-256
`dfa1b18d255f82c7fcaac0b7b83729ac1286a86a7d3783694983426ce08d5ab7`.
No commit or push was made. At least three `.tmp*` isolated probe directories remained
under the test data root after `TempDir` cleanup encountered the setup-owned
state; they were not recursively removed or pointed at user workspaces.

### 2026-09-23 — Explicit unrestricted Phase 2 authority slice

Martin explicitly deferred Windows containment and authorized Phase 2 with
unrestricted tools. This changes sequencing and selected authority, **not** the
failed containment result above. The legacy, native MXC/PSEC, and elevated
Windows sandbox backends remain unqualified; the NeoBabylon legacy guard is
unchanged. [NB-DEC-008](../decisions/0008-phase2-unrestricted-tool-authority.md)
records the accepted policy and its security limits.

Fresh verification on the current product workspace:

- `dotnet run --project tests/host/NeoBabylon.Phase1A.Tests.csproj
  --no-restore` passed all **49** host/core checks. The new real pinned App
  Server test used a disposable application root and the accepted LM Studio
  capability record redirected to a local deterministic Responses fixture.
  `initialize.codexHome` matched the isolated root; `config/read` and
  `thread/start` agreed on `danger-full-access` / `never`. A real
  `cmd.exe /d /c ver` tool call returned `Microsoft Windows` through a
  `function_call_output` request to the mock provider, then the turn completed.
  `thread/resume` and `thread/fork` each echoed effective
  `dangerFullAccess` / `never`. No LM Studio or OpenRouter inference was sent.
- `npm test` in `ui/diagnostic` passed **29/29** checks, including the
  unrestricted-authority presentation and mismatch-label regressions, plus
  persistent notice preference and unavailable-storage behavior.
- `npm run build` completed TypeScript/Vite production build. Rolldown emitted
  non-fatal third-party `use client` directive warnings; there were no
  TypeScript errors. `dotnet build host/NeoBabylon.Host/NeoBabylon.Host.csproj
  --no-restore` and a Release publish to the isolated test directory passed
  with zero .NET warnings or errors.
- A fresh hidden WPF/WebView2 host at
  `D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\AppRoot` exposed its
  page to local Playwright via WebView2 debugging. At a measured `1464×901`
  viewport, `https://neobabylon.local/index.html` had title `NeoBabylon`, a
  nonblank dark shell, no framework overlay or browser console errors/warnings,
  and a visible “Full access · no containment” warning. Clicking the policy
  chip opened real host diagnostics showing `danger-full-access`, containment
  qualified `No`, the separate application root, and ordinary Codex root used
  `false`; closing the drawer worked. Screenshot:
  `D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\qa\full-access-wpf.png`.
  The Browser plugin was unavailable; local Playwright Core attached to the
  running WPF WebView2 instance. The first UI assertion expected exactly two
  copies of the authority label but found three legitimate surfaces (warning,
  chip, context pane); it was corrected to assert the chip and at least two
  visible instances, then passed.
- After the UI adjustment, the real WPF WebView2 smoke was rerun at
  `1464×901` and `960×720`: the two welcome orbit elements were absent;
  both notice actions were visible; Dismiss hid the notice for the current
  session but it returned after reload; “Don’t show again” hid it through
  reload and a fresh WPF host process. The “Full access · no containment”
  policy chip remained visible and opened matching host diagnostics. Browser
  console issues remained empty. Screenshots are
  `D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\qa\full-access-wpf.png`
  and `D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\qa\full-access-wpf-compact.png`.
- The runtime binary SHA-256 was rechecked as
  `dfa1b18d255f82c7fcaac0b7b83729ac1286a86a7d3783694983426ce08d5ab7`.
  The unchanged runtime worktree patch digest was rechecked as
  `924d62510ff2b3a29c7ab889d0a27228e52d673fb54b5ce57756ef9a86f1dc03`.

The first thread-start-only resume test correctly failed with “no rollout
found”: pinned App Server does not persist a resumable rollout until a turn
has occurred. The deterministic tool turn was added to the fixture before
the resume assertion; the full host suite then passed. This is not evidence
for crash recovery or live provider repeatability. Unrestricted same-user
tools may access personal files, NeoBabylon state, network resources, and
potentially credentials through means other than inherited shell environment;
application-root separation is not security containment. No general outside-
file mutation test, credential-secrecy proof, or live-provider turn under the
new policy is claimed.

## 2026-09-23 — Native caption and neutral dark palette

The supplied screenshot showed the default white Windows caption above a
blue-gray dark shell. Before the change, the running host reported DWM dark
attribute `0`, and WebView2 computed `.topbar` as `rgb(29, 34, 42)`; a
neutral-palette smoke assertion failed on that blue tint. The host now uses
the Windows DWM caption APIs on startup and when the named `setAppearance`
operation receives exactly `dark` or `light`. The renderer retains its
versioned appearance preference and synchronizes it with the native caption.
The dark CSS colors were made neutral without altering the light-mode colors.

Fresh checks: the host/core suite passed `50/50`, including dark/light payload
validation and rejection of unknown values; `dotnet build` passed with zero
warnings/errors, and the isolated Release publish succeeded. UI tests passed
`29/29`; the TypeScript/Vite build passed with nonfatal third-party
`use client` directive warnings. The actual WPF window was captured with
`PrintWindow`: the dark caption and React top bar visually matched, and the
light caption matched light mode. DWM attribute `20` read `1` in dark mode,
`0` after switching to light, `0` after a fresh host launch with the saved
light choice, and `1` after switching back to dark. WebView2 computed dark
surfaces: top bar/sidebar/details `rgb(28, 28, 28)`, conversation
`rgb(24, 24, 24)`, composer `rgb(36, 36, 36)`. The dark shell was checked
at `1464×901` and `960×720`; the light/dark toggle, nonblank page, absence
of framework overlay, and browser-console health passed. Captures:
`D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\qa\native-window-capture.png`,
`native-window-light.png`, and `neutral-dark-wpf-compact.png` in that QA
directory. This qualifies the appearance change on the tested Windows build
`26200`, not every Windows version or the full Phase 2 gate.

## 2026-09-23 — Phase 2 local project selection slice

The host now maintains a schema-versioned project registry at
`<application root>\Data\NeoBabylon\projects.json`. On first run it previews
the isolated fixture workspace without writing a registry. A native WPF folder
picker is the only `addProject` path; the renderer can select only a registered
project by named bridge operation. The host validates an existing absolute
folder, does not accept application `Data` as a newly added project, rejects
unregistered selection, and does not silently replace a missing selected
folder. App Server launch/start/resume/fork use the selected cwd; saved-thread
list projection omits records without an exact selected cwd. The registry does
not alter the isolated Codex home or ordinary Codex configuration.

Test-first host checks verified first-run preview, registry persistence,
duplicate selection, missing/unregistered/Data rejection, project-only thread
projection (including a trailing cwd separator), and named bridge operations.
The final full host run returned
`53/53` passes. One earlier run had an intermittent `goals_1.sqlite` lock
during teardown of the pinned mock App Server test; two immediate reruns were
green. This remains a test-harness flake to diagnose, not a claimed runtime
qualification failure. `dotnet publish` and the React type-check/Vite build
passed; the latter emitted existing Astryx `use client` directive warnings.

The published WPF host was restarted against isolated
`D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\AppRoot`. Local
Playwright/CDP and Windows UI Automation verified the enabled Add project
button opened the real native folder dialog, selected an isolated
`qa\SampleProject` folder, and persisted it alongside the fixture in the
application Data registry. The rendered UI then switched between both
projects, updated selected row/breadcrumb/workspace details, prevented a
switch with an unsent draft, and switched after clearing the draft. A renderer
request-generation guard now discards older saved-thread refresh responses
after a project switch. The UI test suite passed `29/29`; a deliberately
delayed cross-project history response was not separately injected. The same
rendered smoke passed again after the final host publish/restart. Page
identity, nonblank content, absent Vite overlay, and page/console error checks
passed. The captured WebView screenshot is
`D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\qa\workspace-selection-wpf.png`.
This test used no live inference. Cross-project saved-thread recovery,
pagination, missing-folder UI on startup, broader accessibility, and the full
Phase 2 acceptance gate remain unqualified.

## 2026-09-23 — Isolated composer draft recovery slice

The React composer now stores exact unsent text under a versioned key scoped
to the selected project path and task identity in the isolated WebView2
profile. Empty text removes its key. The draft is not a second conversation
history store. A turn's draft is cleared only after an App Server
`turn/started` event for the matching request/thread or a terminal host result
that implies `turn/start` was accepted. If a new App Server thread is created
but `turn/start` is rejected, the draft moves from the new-task scope to that
thread's scope; storage failures are shown in the composer rather than claimed
as saved. Local plaintext draft content must be treated as application data.

The rendered WPF/WebView2 red check first reproduced loss of unsent composer
text after reload (`actual=''`). After implementation, the same check passed:
the exact prompt returned after reload, clearing it remained cleared through
another reload, and page identity, overlay, and console checks passed. The
screenshot is
`D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\qa\draft-recovered-wpf.png`.
The same isolated root then retained an unsent draft across one graceful host
close/relaunch and one abrupt termination/relaunch of the verified QA-host PID;
the restarted WPF composer showed the exact text and it was cleared after
each probe. Neither probe involved an active App Server turn or live inference.
The UI suite passed `32/32`; React type-check and Vite build passed, with the
pre-existing Astryx `use client` bundler warnings. A separate isolated
Playwright/Edge synthetic bridge verified that an acknowledged turn cleared
the stored draft and a rejected `turn/start` retained it in the newly created
task scope. Navigating to a new task did not show that draft; reopening the
saved failed task restored it, including after a renderer reload. The fixture
initially omitted the existing `setAppearance` host
operation and produced console errors; after completing that fixture response,
the final run had no page/console errors. This is deterministic UI/bridge
evidence, not live provider inference or App Server acceptance evidence.

Real App Server saved-thread restoration of drafts, active-turn crash behavior, local-storage
failure handling in the rendered host, and live turn interruption are not yet
qualified. The combined Phase 2 draft/reconnection gate remains open.

## 2026-09-23 — Active-task navigation reconnect and neutral-dark WPF check

The React shell now persists a versioned navigation hint containing only the
selected project, exact provider/model identifiers, and App Server thread id
in the isolated WebView2 profile. On startup it checks the selected identity
and listed saved-thread metadata before calling the existing `resumeThread`
host operation. The host revalidates identity against the isolated App Server
thread before restoring a bounded transcript. No `startTurn` is issued on
reconnect. New task, project change, and capability change clear the hint.

The active-task unit tests passed `3/3`; the complete UI suite passed `35/35`,
TypeScript type-check passed, and Vite production build exited 0 with the
existing Astryx `use client` and chunk-size warnings. The full host test
runner also exited 0. A Playwright/Edge
synthetic host test first failed before the reconnect implementation, then
passed exact saved-task restoration after reload with no `startTurn`. The
extended rendered check also showed a visible warning and no resume for a
wrong model or missing saved history; New task removed the hint and a later
reload did not resume. The Vite overlay and page/console errors were absent.
This is deterministic renderer/bridge evidence, not live inference.

The current isolated WPF QA profile returned no saved threads. A no-inference
`startThread` produced an empty test thread, but `listThreads` did not return
it, so that thread could not qualify real saved-history reconnect. The test
thread was closed with `newTask`; no live provider request was made. A real
saved App Server thread, active-turn host crash, and multi-project recovery
still require separate qualification. The combined Phase 2 gate remains open.

The user screenshot `codex-clipboard-4a334f9c-36f5-4508-9252-be7538bfbceb.png`
was timestamped before the existing title-bar and neutral-palette source
changes. Fresh inspection of the running WPF host showed DWM immersive dark
mode attribute `20 = 1`; `PrintWindow` captured a charcoal draggable title
bar matching the gray/black body at
`D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\qa\current-native-printwindow.png`.
WebView2 computed backgrounds were topbar/sidebar `rgb(28, 28, 28)`, main
conversation `rgb(24, 24, 24)`, composer `rgb(36, 36, 36)`. The appearance
control switched light and back to dark without console errors. No new WPF
chrome or palette code was needed for this screenshot report.

## 2026-09-23 — Saved-history pagination slice

The host now sends the selected project cwd with `thread/list`, preserves
independent scan-and-repair and state-DB cursors in a token bound to the
selected project/provider/model, and rejects a cursor for another selection.
The renderer appends older rows without duplicates or erasing the first page.
For a saved task beyond the first page, reconnect uses an exact `thread/read`
identity check before resume and never replays a turn.

The isolated pinned App Server fixture returned two distinct pages with
`limit: 1` for each listing path. The host suite passed twice after test
cleanup was corrected to clear a read-only Git temporary pack file within its
validated temporary root. The UI suite passed 36/36; `npm run build` exited 0
with existing Astryx `use client` and chunk-size warnings. Synthetic rendered
checks passed two-page loading, deduplication, search, and older-task reconnect
without page/console errors. The live WPF appearance check still showed
neutral dark surfaces, and the light-to-dark toggle passed. The current WPF
QA profile has no multi-page saved history, so end-to-end host pagination,
cross-page ordering, and active-turn recovery are not qualified.

## 2026-09-23 — Real saved-task reconnect after QA-host restart

The current WPF host build was launched on the existing isolated
`D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-WPF-20260923\App` application root,
separate from the source repository. Its pinned App Server listed two saved
LM Studio tasks without a loaded LM Studio model. Playwright over that host's
WebView2 debugging port opened one task and observed six visible messages;
renderer reload restored the same selected task and six messages without a
page or console error.

The exact QA host PID `53392` was verified against its executable path and
terminated abruptly. Its App Server child exited; the old WebView2 debugging
port closed. A new host PID `8668` was launched on the same isolated root.
The saved active-task hint's SHA-256 remained
`8b64a10849f187db490b82b0a77eef2985f3d7b8edf161436278023f7746b799`;
the restarted UI selected the same task, listed two saved rows, and rendered
six messages with no page/console errors. Evidence screenshots are under
`D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\qa\real-history-*.png`.
No `startTurn` or live inference was requested by the test. This qualifies
one real saved-task host-restart/reconnect path, not active-turn crash recovery,
provider interruption, multi-page history, or cross-project recovery.

## 2026-09-23 — Rendered long-output stress and stream coalescing

The synthetic bridge rendered a 300,000-character assistant reply from 150
stream deltas and 200,000 characters of tool output in 562 ms before the
change. A 5,000,000-character reply from 2,500 deltas plus 2,000,000
characters of tool output then timed out at 60 seconds. The same full final
reply and tool output without those frequent deltas completed in 933 ms;
50 deltas also completed in 947 ms. This isolated high-frequency React
transcript updates as the practical slowdown, rather than the final text size.

The renderer now buffers assistant deltas and applies them in display batches;
an authoritative completed message supersedes pending deltas, while a failed
turn flushes partial text. The unit suite passed 39/39 and the rebuilt UI
bundle completed with existing Astryx `use client` and chunk-size warnings.
The formerly timed-out 5 MB/2 MB, 2,500-delta case completed in 7.4 seconds
and 8.0 seconds on repeat, with all 5,000,000 assistant characters and
2,000,000 tool characters present and one assistant message. The repeat also
confirmed scrollable tool output; neither run had page/console errors.
A 300,000/200,000-character case completed in 426 ms after the fix; a
25-delta provider-failure case preserved all 50,000 partial characters.
The pinned `item/commandExecution/outputDelta` path was also probed with
2,500 synthetic notifications after a 5 MB reply and completed in 2.8 seconds.
After rebuilding, a separate isolated WPF host reopened the same saved task
with six messages and no browser errors. The stress bridge was not connected
to a live provider or the App Server, and no inference was requested.

## 2026-09-23 — Paused-turn persistence and truthful saved-task outcome

An isolated loopback Responses fixture held its first request while the exact
locked App Server ran a turn. After the fixture received that request, the test
disposed the App Server client, terminating its process tree; it did **not**
crash the entire WPF host. A fresh App Server on the same isolated application
root returned the saved turn as `interrupted` from `thread/turns/list` before
and after `thread/resume`. The fixture counted exactly one provider request:
neither `thread/read` nor resume replayed inference. No live provider was
contacted and no model-controlled tool ran in this probe.

The host transcript projection now carries an allowlisted saved-turn status,
including an itemless latest turn, without copying raw tool data or error
payloads into the renderer. The React restore path shows that status instead
of unconditionally showing `READY`; interrupted/failed/unknown outcomes get a
visible non-replay warning. The host suite passed after a red test proved the
previous itemless-turn omission, and the UI helper test and rendered synthetic
bridge each failed before the corresponding fix, then passed. Final checks:
host suite passed, UI suite 40/40, UI build exit 0 with existing Astryx
`use client`/chunk warnings, and WPF host build 0 warnings/errors. The rendered
interrupted-state capture is
`D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\qa\interrupted-turn-recovered.png`.
This narrows active-turn recovery risk but does not qualify abrupt WPF-host
termination during a live provider request, cross-project recovery, or the
combined Phase 2 gate.

## 2026-09-23 — Isolated WPF-host active-turn crash and continuation

For a stronger end-to-end recovery probe, a disposable QA source mirror under
`D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\qa\CrashHostSource`
copied the current built UI and capability record, changing only the mirror's
LM Studio endpoint to `http://127.0.0.1:19242/v1`. Its test-only runtime lock
resolved to the same accepted App Server `0.155.1` binary, SHA-256
`dfa1b18d255f82c7fcaac0b7b83729ac1286a86a7d3783694983426ce08d5ab7`.
Neither the canonical capability record nor shared Codex/provider state was
changed. The application root was a separate
`...\qa\CrashHostApp2` directory with its own `Data\CodexHome` and WebView2
profile. The only model identifier on the wire was `phase1a-qwen3-14b`; no
real model was loaded or contacted.

The visible WPF/WebView2 host entered `RUNNING` and the loopback fixture held
one `/v1/responses` request for that exact model. The active-task hint named
the exact workspace/provider/model/thread. Test-owned host PID `64940` was
terminated abruptly after verifying its executable path. Its App Server child
PID `42380` then exited and the fixture connection closed. A fresh WPF host
PID `49884` on the *same* isolated application root reopened that exact task:
WebView2 reported `interrupted`, displayed the non-replay warning, and had no
page/console errors. The paused fixture's request count remained **one**.
After replacing only the isolated loopback fixture with a deterministic
assistant-message fixture, a user-authored follow-up in that same thread
produced one new request for the exact model, rendered the expected reply,
and reached `completed`. The two phases are evidenced by
`qa\full-host-crash-before.png`, `qa\full-host-crash-recovered.png`, and
`qa\full-host-crash-followup.png` under the Phase 2 data root.

An earlier QA launch observed App Server JSONL EOF before any provider
request. Its precise external termination cause was not established, but it
exposed a host defect: `EnsureClientAsync` reused a confirmed-exited child.
An isolated host test killed only the exact newly spawned locked binary and
first failed with `The pipe is being closed`; after a liveness check and
relaunch in `RuntimeSupervisor`, the full host suite passed and a marker in
isolated `Data` survived. This fixes reuse of a *confirmed-dead* client for
the tested listing path; it does not yet prove automatic rebinding of an
already-active UI thread after only its App Server child exits.

The WPF crash result is a bounded mock-provider qualification. Live-provider
interruption, cross-project recovery, and the combined Phase 2 gate remain
open. No production provider inference or model-controlled tool was run in
this crash probe.

## 2026-09-23 — Standalone App Server child exit between WPF turns

The pinned runtime remained `0.155.1`, SHA-256
`dfa1b18d255f82c7fcaac0b7b83729ac1286a86a7d3783694983426ce08d5ab7`.
An isolated source mirror reused only the accepted binary and a loopback
Responses endpoint (`http://127.0.0.1:19242/v1`); the application root was
`D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\qa\ChildExitApp`,
not the product source repository or ordinary Codex root. The fixture returned
deterministic assistant text for `phase1a-qwen3-14b`; no real model or live
provider was used.

First, a host integration test with the real App Server completed a saved
turn, killed its exact verified child, and refreshed history. The test failed
at the next user turn with `Start a pinned thread before starting a turn`:
dead-client disposal had erased `_threadId`. The supervisor now retains the
exact thread ID only for a matching provider/model when a confirmed-dead
child is relaunched, marks it as requiring resume, and routes the next
user-authored prompt through existing `thread/read` identity, effective
unrestricted authority, `thread/resume`, and transcript checks. It does not
replay an earlier turn. The same test passed with one original tool round
trip and one new mock continuation; a second child termination **without**
a history refresh first failed with a closed pipe and then passed after the
direct-exit path relaunched and revalidated the same task. The full host
suite passed, and `dotnet build host/NeoBabylon.Host/NeoBabylon.Host.csproj
--no-restore` exited zero with no warnings/errors.

The visible WPF/WebView2 probe stayed in host PID `46348`. Its first prompt
completed in saved task `01a0cf6e-dfb1-7f00-8f1c-7fd3776e00dd`; the
verified exact child PID `63592` was then terminated between turns. A second
user-authored prompt completed in the **same** task, rendered a second mock
reply, and had no page/console errors. The replacement App Server PID was
`5828`; the fixture counted exactly **two** `/v1/responses` requests total,
one per prompt, with the same model identifier. The retained captures are
`qa\child-exit-wpf-before.png` and `qa\child-exit-wpf-after.png` under the
Phase 2 data root. The test-owned host and fixture were subsequently stopped.

That probe proves only a between-turn child exit on the tested Windows build.
The following section separately covers an in-flight child exit. Live-provider
interruption, cross-project recovery, and the full Phase 2 gate remain open.

## 2026-09-23 — In-flight App Server exit with WPF still open

A second isolated application root, `...\qa\ActiveChildExitApp`, ran the
current WPF host against the same exact locked App Server binary and a
loopback Responses fixture. Host PID `31076` stayed open. Its saved task ID
was `01a0cf72-223f-7d00-8564-74e2324318a5`. While the first mock provider
request was pending (`responseRequests=1`, `pending=1`, model
`phase1a-qwen3-14b`), only the verified App Server child PID `65692` was
terminated. The WPF conversation changed from `running` to `failed`, displayed
`Pinned Codex App Server protocol stream closed before a terminal turn event
(EndOfStreamException).`, rendered no assistant success, and retained the
same task identity. The paused fixture's request count stayed **one** and its
pending connection closed. After replacing only that local fixture with a
deterministic completion fixture on the same endpoint, a *new user-authored*
prompt completed in the same task with exactly one new provider request and
no page/console errors. Captures: `qa\active-child-exit-start.png`,
`qa\active-child-exit-after-exit.png`, and
`qa\active-child-exit-followup.png` under the Phase 2 data root. No live
provider or real model was contacted.

That first UI run exposed a truthful-history gap: the current-turn error card
was cleared by a later successful prompt, leaving no visible marker for the
prior interruption. A UI regression test first failed because
`restoreVisibleTranscript` omitted a prior turn's `interrupted` status; it
then passed after projecting allowlisted non-completed status as a separate,
attributed inline status item, not an invented assistant reply or a second
history store. The live result path also adds a host-attributed inline failure
marker, while saved history after reload uses the App Server's authoritative
`interrupted` status. The renderer gained neutral-dark-compatible status
styling. UI tests passed **41/41**; the TypeScript/Vite build exited zero
with existing third-party `use client` and chunk-size warnings.

After copying the rebuilt UI only into the isolated QA source mirror, a
renderer reload of the first saved task displayed its earlier interrupted
marker between the failed prompt and later successful prompt; the fixture
count stayed one, proving no replay on reload. A second in-flight mock request
was held and its verified child PID `51884` was terminated. The **current**
WPF screen showed both the prior saved interruption and a new
`NeoBabylon host: Turn failed. No automatic replay.` marker, with one paused
provider request and no second success. A separate new prompt then completed
through the replacement fixture, with both status markers still visible in
the same UI session. Finally, another renderer reload reconstructed both
turns as `Codex App Server: Turn interrupted. No automatic replay.` from
runtime-owned saved status, alongside four user prompts and two successful
assistant replies; it made no additional provider request and showed no
page/console errors. Captures: `qa\active-child-exit-repeat-after.png`,
`qa\active-child-exit-repeat-followup.png`, and
`qa\past-failure-restored.png`. The final replacement child was PID `56260`;
the binary hash remained the accepted SHA-256 above.

This qualifies a bounded *mock-provider* in-flight child-exit and same-task
continuation path on the tested WPF build. Live-provider interruption,
cross-project recovery, broader no-zombie/recovery cases, and the combined
Phase 2 gate remain unqualified.

## 2026-09-23 — Recovery ownership and live-versus-persisted outcome

The host regression first reproduced loss of a selected saved task after a
third exact pinned App Server child exit and a cancelled replacement
initialization; a separately authored continuation then failed with `Start a
pinned thread before starting a turn`. The supervisor now retains the exact
selected task and capability snapshot through that failed replacement, while
explicit New task/model/project changes still clear them. A second regression
reproduced reuse of a live App Server client when the incoming capability had
the same provider/model labels but a different endpoint; the host now rejects
that full-record mismatch. A paused turn fixture also reproduced saved-history
listing during a running turn; listing now holds the existing host operation
reservation and reports busy. The host suite passed after these changes via
`dotnet run --project tests/host/NeoBabylon.Phase1A.Tests.csproj --no-restore`
(exit 0). These tests do not establish every reverse/concurrent interleaving.

The live UI formerly called a stream-closure `failed` even though the pinned
App Server had not sent a terminal turn status. A new UI regression first
failed, then passed with an `unknown` turn state and host-attributed
`Turn outcome unconfirmed. No automatic replay.` marker. Terminal failed or
interrupted status remains App Server-attributed. UI tests passed **42/42**;
`npm run build` exited zero with the pre-existing third-party `use client` and
large-chunk warnings.

For rendered verification, a disposable WPF host (PID `11736`) used isolated
application root `D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\qa\UnconfirmedApp`
and source mirror `...\qa\CrashHostSource`. The paused loopback Responses
fixture was `http://127.0.0.1:19242/v1` with mock model
`phase1a-qwen3-14b`; **no live model/provider was contacted**. One new,
explicitly authored prompt reached the fixture (`responseRequests=2` total,
including the earlier separate prompt; `pending=1`). The exact child PID
`65748` had parent `11736`, executable
`D:\CODING\NeoBabylon-Data\Phase1B-RoutePatch-Target\debug\codex-app-server.exe`,
and SHA-256
`dfa1b18d255f82c7fcaac0b7b83729ac1286a86a7d3783694983426ce08d5ab7`
before only that child was terminated. The WPF conversation and details badge
both showed `UNKNOWN`, with the host-attributed unconfirmed marker, typed EOF
error, no successful assistant, and no WebView console/page errors. On renderer
reload, App Server history showed both saved turns as `INTERRUPTED` with two
runtime-attributed markers; the fixture still reported only two total
requests. Captures are `qa\unconfirmed-badge-after-exit.png` and
`qa\unconfirmed-badge-reload.png` under the Phase 2 data root.

This is a bounded mock-provider recovery and truthful-attribution result, not
a live-provider interruption, a complete concurrency matrix, or the combined
Phase 2 gate.

## 2026-09-23 — Two-project saved-task recovery, host and rendered WPF

The host suite added a pinned-App-Server test with separate temporary Alpha
and Beta folders outside the isolated application `Data`. It started and
completed a mock Responses turn in each project, listed only the selected
project's saved task, rejected Alpha resume while Beta was selected, and
confirmed that rejection left Beta's active task usable. Switching back
resumed and continued Alpha. After disposing/recreating the host supervisor
with the same application root, the selected Alpha project persisted; Beta
history, exact task resume, and a separate Beta continuation passed. Six mock
provider requests matched six authored host-test prompts. The full host suite
passed with `dotnet run --project tests/host/NeoBabylon.Phase1A.Tests.csproj
--no-restore` (exit 0).

After tightening the cross-project rejection assertion to require the exact
workspace-mismatch diagnostic, one full-suite run reported a transient
Windows directory-in-use error while the pre-existing fake-App-Server approval
test deleted its empty temporary folder. The test had already observed its
terminal approval result; the empty folder could be removed after the process
ended. A fresh full-suite rerun passed (exit 0), including both the approval
test and the stricter cross-project test. This intermittent teardown result is
recorded rather than counted as product approval qualification.

For the rendered check, a disposable WPF host used
`D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\qa\CrossProjectApp`
with `Data\NeoBabylon\projects.json` preseeded to select sibling QA folders
`CrossProjectAlpha` and `CrossProjectBeta`. The QA source mirror's LM Studio
capability record pointed to a **loopback Responses fixture** at
`http://127.0.0.1:19242/v1`; the displayed provider/model labels were test
metadata, **not a live LM Studio call**. WPF PID `49288` created distinct
Alpha `01a0cf9e-27ea-7512-84e3-5b2474fc886f` and Beta
`01a0cf9e-32f9-7382-8e52-f13d5dd2080f` tasks. After switching back to
Alpha, the sidebar contained only its saved task, and opening it restored its
prompt. The host was closed gracefully; its App Server child exited. WPF PID
`66076` reopened the same application root and automatically restored Alpha.
Selecting Beta displayed only its saved task; opening it restored its prompt
and an explicitly authored continuation completed in the same task. The
loopback fixture counted **three requests for three WPF-authored prompts**;
no extra request appeared on project selection, resume, or restart. No
WebView page/console errors were observed. Captures are
`qa\cross-project-before-restart.png` and `qa\cross-project-after-restart.png`
under the Phase 2 data root. Both test hosts, their owned App Server children,
and the loopback fixture were stopped afterward.

This qualifies one two-folder mock-provider path through the actual WPF
shell, not live-provider routing, concurrent project switching, a larger
project catalog, or the full Phase 2 gate.

## 2026-09-23 — Real saved-task draft restore without inference

The same isolated `CrossProjectApp` root was reopened in WPF with its real
saved Beta App Server task
`01a0cf9e-32f9-7382-8e52-f13d5dd2080f`; the loopback provider was no
longer running. An unsent 54-character draft was entered in Beta. Choosing
New task left the new-task composer empty; reopening Beta from saved history
restored the exact draft. A WebView renderer reload and a graceful host
close/relaunch (PIDs `65212` then `41112`) preserved the same task and draft.
Explicitly clearing the draft and reloading kept the composer empty. The
saved transcript remained two user turns and two assistant replies throughout;
there was no provider request or WebView page/console error in this probe.
Screenshots: `qa\saved-task-draft-before-restart.png` and
`qa\saved-task-draft-after-restart.png` under the Phase 2 data root. The QA
hosts and their App Server children were stopped after the test.

This qualifies one real WPF/App Server saved-task draft restoration path, not
active-turn crash recovery, storage-denial behavior, or a live-provider turn.

## 2026-09-23 — Diagnostics keyboard-modal slice and diff-source inspection

A new `ui/diagnostic/tests/modal-focus.test.mjs` first failed because the
dialog keyboard handler was absent. After adding focus management and
`inert` background state, the UI suite passed **43/43** and `npm run build`
exited zero, with the existing third-party `use client` and large-chunk
warnings. The isolated WPF host (PID `53044`) used application root
`D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\qa\CrossProjectApp`
and the QA source mirror; no model turn or provider request was started. The
rendered WebView2 probe connected to that host's CDP endpoint, reloaded the
page, opened Runtime diagnostics, and checked focus entry, both boundary Tab
directions, inert sidebar/conversation, Escape close, restored background
interactivity, and focus return. It passed with no page/console errors. The
probe and capture are `qa\diagnostics-keyboard-probe.mjs` and
`qa\diagnostics-keyboard-open.png` under that data root. This is a bounded
keyboard result, not a screen-reader or whole-application accessibility pass.

Source inspection of pinned `codex-rs/app-server-protocol/src/protocol/v2`
found `TurnDiffUpdatedNotification` with thread, turn, and diff fields, and
`FileChangePatchUpdatedNotification` with item-scoped changes. The stable
`FileChangeRequestApprovalParams` contains thread, turn, item, time, reason,
and an unstable optional grant root, but no diff. In
`codex-rs/core/src/tools/events.rs`, `emit_patch_end` emits the file-change
item and updates the turn-diff tracker. The isolated local-model catalog used
in this WPF fixture records `apply_patch_tool_type: null`. These observations
do not prove that arbitrary shell edits are captured as a reviewable diff or
that a diff is available before a file-change approval decision. No approval
policy was changed; file-change approval remains deny-only. A matching
review/authority test is still required before declaring this gate passed.

## 2026-09-23 — Read-only App Server turn-diff review slice

`ui/diagnostic/tests/turn-review.test.mjs` first failed because no turn-review
reducer existed. The new reducer then passed tests for exact thread/turn
correlation, latest-aggregate replacement, malformed/mismatched rejection,
empty invalidation, oversized withholding, and next-turn reset. A rendered
Edge synthetic-bridge probe first failed waiting for a Review changes action.
After UI integration it exposed a fast-response ordering bug: React applied
the diff state after the pending-turn reference had been cleared. Capturing
the exact expected thread before scheduling the state update fixed that case.
The probe then exposed lost return focus when the opener became inert; an
explicit opener reference and post-close focus restoration fixed it.

The final synthetic probe at `http://127.0.0.1:19255/` passed at `1464×901`
and `960×720`: one exact `turn/started` plus `turn/diff/updated` yielded a
read-only, App Server-attributed drawer; oversized and invalidated subsequent
turn diffs displayed unavailable states without stale/partial content; Escape
closed the drawer, restored background interactivity and opener focus; there
were no page/console errors or compact-width page overflow. The test uses a
stub WebView bridge in Edge, **not the native WPF host or pinned App Server**.
Evidence is `qa\turn-review-rendered-probe.mjs`,
`qa\turn-review-rendered.png`, and `qa\turn-review-compact.png` under
`D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923`. UI tests passed
**46/46** and `npm run build` exited zero, with existing third-party
`use client` and large-chunk warnings.

The review drawer grants no tool authority. The current selected local-model
catalog uses `apply_patch_tool_type: null`; a real pinned-App-Server patch
event, shell-write completeness, restored-history diff, and pre-approval
review/invalidation are **not qualified**. File-change approval remains
deny-only and the full Phase 2 diff/review gate remains open.

## 2026-09-23 — Saved patch-item review projection and reopen checks

The host test for `ThreadSavedChangeProjector` first failed because the
projector did not exist. It now accepts only saved `fileChange` items with
completed status and well-formed path/kind/diff fields, preserves exact turn
identity, omits command-output payloads, and withholds the entire review for
malformed or oversized content. A second regression first reproduced a
numeric move destination being silently omitted; the projector now rejects
that malformed destination. The full host suite passed **60/60**, exit 0.
The WPF host build passed with zero warnings and errors.

`RuntimeSupervisor` now includes this bounded `savedReviews` projection from
the same App Server `thread/turns/list` result already used for transcript
resume/fork. The renderer reconstructs only the latest matching saved patch
review and marks its source distinctly from the live aggregate turn diff. Two
new UI tests first failed before restoration code existed, then passed;
the full UI suite passed **48/48** and `npm run build` exited zero with the
pre-existing third-party directive and chunk warnings. The Edge synthetic
bridge reopened a saved task and rendered its patch item diff, with no
console/page errors. Evidence: `qa\turn-review-rendered-probe.mjs` and
`qa\turn-review-restored.png` under the Phase 2 data root.

A separate native WPF/WebView2 run used the isolated
`qa\CrossProjectApp` application root and QA source mirror with the exact
locked App Server. It reopened the existing Beta task containing **no** saved
patch items and displayed no Review changes action. Page identity, transcript,
and console/page checks passed without provider inference. Evidence:
`qa\saved-review-wpf-smoke.mjs` and `qa\saved-review-wpf-no-patch.png`.
The test-owned WPF host PID `8796` and its App Server child PID `44376` exited
gracefully afterward. This is a negative real-runtime and positive synthetic
reopen result, **not** positive pinned-runtime patch-history qualification.
File-change approval remains deny-only.

## 2026-09-23 — Positive pinned-runtime patch event and saved-review fixture

The earlier negative WPF/App Server reopen check above remains valid. A new
isolated host integration test now supplies a deterministic loopback Responses
fixture that requests one `apply_patch` tool call against `tracked.txt` in a
disposable workspace. Only the test-owned generated model catalog is changed
from `apply_patch_tool_type: null` to `freeform`; the canonical capability
record and production catalog builder are unchanged. The App Server executable
was verified against the runtime lock: Codex `0.155.1`, SHA-256
`dfa1b18d255f82c7fcaac0b7b83729ac1286a86a7d3783694983426ce08d5ab7`.

The integration assertion observed `before` become `after` in the isolated
file, three `turn/diff/updated` notifications for the exact thread/turn, a
completed saved `fileChange` item with patch text projected from
`thread/turns/list`, and a `custom_tool_call_output` on the second and final
fixture provider request. The full host suite passed **61/61**, exit 0, with
the sanitized line `PATCH_EVIDENCE runtimeSha=dfa1b18d255f82c7fcaac0b7b83729ac1286a86a7d3783694983426ce08d5ab7 turnDiffEvents=3 savedReview=available providerRequests=2 testCatalog=freeform-only`.
The test did not call LM Studio or OpenRouter, qualify a real model's patch
capability, render this positive review in native WPF, or validate review
survival across an actual host restart. Approval policy remains deny-only.

## 2026-09-23 — Positive native WPF saved-patch review and host restart

The host test's `--prepare-patch-review-qa` mode retained one disposable
application root at
`D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\qa\PositivePatchApp-20260923-1`
after the same verified pinned-runtime patch fixture. It reported thread
`01a0cfe7-c6de-74d3-9303-c7c40c6187cc`; the provider fixture saw exactly
two requests and was shut down before native WPF launched. Direct fresh
App Server and `RuntimeSupervisor` resume probes each returned one available
saved patch review from isolated task history using the canonical LM Studio
capability record without making a provider request. The host response also
serialized successfully (2,584 UTF-8 bytes).

The native WPF/WebView2 host then opened this isolated root with the exact
locked App Server. The Browser plugin was unavailable in this session, so
`tests/qa/native-patch-review.mjs` used Playwright Core against the test-only
WebView2 CDP port. It checked the NeoBabylon page title, meaningful content,
absence of a framework error overlay, and the saved task's user/assistant
transcript and read-only review drawer containing `tracked.txt`, `-before`,
and `+after`, attributed to App Server-owned patch item history. It found no
approval action and no console/page errors. The same result passed after a
renderer reload, then again after the QA WPF process was closed gracefully
and restarted with the same root. Both runs reported `passed:true`,
`reloadPassed:true`, `taskCount:1`, `reviewCharacters:435`, `issues:[]`.
Screenshots:
`qa/native-positive-patch-review.png` and
`qa/native-positive-patch-review-after-host-restart.png` under the Phase 2
data root. The test-owned host and App Server child exited afterward.

The first native probe falsely appeared stalled because its Playwright script
clicked the saved row during automatic reopen; the row was disabled while
loading and later became the already-current task. The corrected probe waits
for that state and does not issue a duplicate click. The positive path is
verified for **mock-generated saved patch history**, not live LM Studio or
OpenRouter patch capability, arbitrary shell writes, or pre-approval content.
The production catalog still has `apply_patch_tool_type: null`; file-change
approval remains deny-only. After the test-only QA harness changes, the full
host suite passed **61/61**, the UI suite passed **48/48**, and the WPF host
build passed with zero warnings and errors.

## 2026-09-23 — Live NEX freeform patch-tool mismatch

A bounded, test-only live probe used the canonical
`MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json`, a fresh isolated
`qa/LiveNexPatchApp-20260923-1` application root under the Phase 2 data
directory, and the exact locked App Server binary (SHA-256
`dfa1b18d255f82c7fcaac0b7b83729ac1286a86a7d3783694983426ce08d5ab7`).
The OpenRouter public catalog/endpoint preflight still identified the exact
`nex-agi/nex-n2.5-pro:free` model, one Nex AGI `nex-agi/fp8` endpoint,
262,144-token context, zero prompt/completion price, and a 2026-09-25 model
expiration date. No provider/model fallback was configured. The supplied
free-only key was held only in the probe process and passed to the isolated
App Server child; it was not recorded in the evidence output.

Only this test-owned generated catalog set `apply_patch_tool_type: freeform`;
the canonical record and production catalog builder still leave it `null`.
The deterministic pinned-runtime fixture now additionally verifies the
outgoing Responses request actually lists `apply_patch`, not merely that the
runtime accepts a synthetic unsolicited patch call. The live NEX turn
`01a0cff8-b4a4-7c83-acfb-8880e9a4b41a` in thread
`01a0cff8-b471-7840-96c0-1fd6f70494e2` reached terminal `completed`,
but emitted only user, reasoning, and assistant-message items: zero tool
items, zero `turn/diff/updated` events, and `tracked.txt` stayed `before`.
The assistant's recorded answer was: “Unable to make the change: the
`apply_patch` tool is not available in this session. No files were modified.”
The probe exited nonzero because the requested patch did not occur. Evidence
remains in that isolated application's Codex session journal; no retry or
alternate provider/model was used.

This proves **the real NEX/freeform patch path did not work in this run**. It
does not yet distinguish an OpenRouter Responses translation/filtering issue
from a model tool-recognition issue. OpenRouter's published client-defined
tool examples use function tools; that is a lead, not a confirmed cause for
this exact request. The earlier ordinary `exec_command` live tool round trip
remains valid. File-change approval remains deny-only, and shell-written edits
are not represented as tracked patch diffs by this result.

A separate, bounded direct OpenRouter Responses request tested the **same
exact free model** with a client-defined `type: function` tool named
`apply_patch`, JSON arguments containing a `patch` string, and
`tool_choice: required`. The response completed and contained a
`function_call` named `apply_patch` with arguments. Its generation lookup
reported provider `Nex AGI`, model
`nex-agi/nex-n2.5-pro-20260907:free`, `total_cost: 0`, and `is_byok:
false`. This provider-only request did **not** execute a local patch or
exercise the Codex App Server. It establishes that the exact live model can
emit a function-form patch request through OpenRouter, while the current
Codex runtime exposes its patch tool as custom/freeform. A function-form
runtime adaptation is a proposal pending review and end-to-end qualification,
not an accepted capability claim. The model capability record and production
catalog remain unchanged.

## 2026-09-24 — Function-form patch candidate regression checks

The runtime lock now identifies the function-form patch candidate: Codex App
Server `0.155.1`, upstream revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, executable SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`, and
source-patch SHA-256
`a0a7cd319a31f2125195e7087be72a2867be4779edf00c686ec85f42c58cf520`.
The protocol suite passed **302/302** with **1 skipped**. Focused route and
function-patch regressions passed **7/7**, and the function-patch execution
integration test passed **1/1**. The deterministic NeoBabylon host harness
passed **66/66**; its function-patch probe reported the locked runtime hash,
three turn-diff events, an available saved review, two fixture provider
requests, and a test-only `Function` catalog. No live provider inference was
used for these checks.

Separate retained live evidence qualifies the function-form patch path on this
same runtime. The isolated application root is
`D:\CODING\NeoBabylon-Data\Phase2-Unrestricted-20260923\qa\LiveNexFunctionPatchApp-20260923-1`;
its Codex config pins OpenRouter `nex-agi/nex-n2.5-pro:free`, endpoint
`nex-agi/fp8`, and Responses. The live run used the verified App Server hash
above and enabled function-form patch metadata only in a test-owned catalog.
Its retained session
`01a0d031-e130-7ca2-b35c-a70346c40fac` contains an `apply_patch`
`function_call`, its matching `function_call_output`, a completed `FileChange`
item, and a completed turn. The isolated `tracked.txt` contains `after`; the
live harness also required an available saved patch review from
`thread/turns/list`. The two OpenRouter generation attestations recorded for
this turn report Nex AGI, the returned free NEX model, streaming, zero cost,
and `is_byok=false`; provider token counters remain inconsistent with the
Codex session journal as described in the capability record. This qualifies
function-form `apply_patch` for this exact model and route only. It was a
host/App Server test, not a native WPF rendering or restart test.

The diagnostic UI passed **48/48** tests, `npm run typecheck` passed, and
`npm run build` succeeded with existing third-party module-directive and
bundle-size warnings. `dotnet build host\NeoBabylon.Host\NeoBabylon.Host.csproj
-c Release --no-restore` succeeded with zero warnings and errors. A separate
broad `codex-app-server` plus `codex-model-provider-info` test attempt was not
green: **1,523 passed and 26 failed**. The failures included unavailable
runtime test helpers (`test_stdio_server.exe` and code-mode-host), timing
assertions, and shell/sandbox environment cases; this must not be represented
as a passing full runtime suite.

A fresh native WPF/WebView2 session against the newly locked function-form
candidate was not completed in this run: the command runner rejected the
per-process environment-bound launch needed to select the isolated QA
application root. The pre-existing visible WPF instance was left untouched.
Earlier native saved-patch-review evidence used runtime hash
`dfa1b18d255f82c7fcaac0b7b83729ac1286a86a7d3783694983426ce08d5ab7` and does
not substitute for a native test of this candidate. The live NEX function-form
patch path is qualified by the retained test above; a fresh native WPF render,
renderer reload, and host restart against this exact function-patch binary
remain unverified.

## 2026-09-24 — Broad runtime-suite follow-up

The test-only `test_stdio_server.exe` helper was built locally with
`cargo build -p codex-rmcp-client --bin test_stdio_server`; no system-wide
install was used. A follow-up run of the 1,549-test App Server/provider suite
was manually stopped after **689/1,549** tests because the runtime test
environment still lacked `codex-code-mode-host.exe` and deadline-sensitive
Guardian tests were failing. The run was incomplete and has no aggregate pass
or failure count. The preceding complete run (1,523 passed, 26 failed) remains
the latest completed broad-suite result; the helper build does not convert
that result to a pass.

## 2026-09-24 — Turn-start draft acceptance in the rendered UI

The diagnostic UI unit suite passed **49/49**, `npm run typecheck` passed, and
`npm run build` succeeded with the existing Astryx `use client` bundle notices
and the 500 KB chunk-size warning. Microsoft Edge through local Playwright
(Browser plugin unavailable) loaded `http://127.0.0.1:4173/` with title
`NeoBabylon`, meaningful content, dark appearance by default, and no browser
console errors or warnings.

With a synthetic WebView host, a project-scoped draft was saved before submit.
The test held `startTurn` open, sent a `turn/started` notification carrying
the active request ID but a mismatched thread ID, and confirmed the draft
remained. A second notification for the exact thread removed the stored draft
and cleared the composer before any final turn response was delivered. The
760×850 desktop-width layout had no horizontal overflow. A 390-pixel viewport
expanded to the stylesheet's declared 640-pixel minimum and is therefore not
qualified as a mobile layout. Screenshots are retained outside the repository
under the current Codex visualization workspace.

The same rendered host fixture exercised two storage failures. If
`removeItem` failed but an empty-value `setItem` succeeded, the draft remained
cleared across New task without an alert. If both operations failed, the
composer cleared in memory, the original stored value remained, and the UI
showed “Check history before resending after a restart.” A unit regression
first failed on remove denial, then passed after adding the empty-value
fallback; the full suite now passes **49/49**.

This remains a React/bridge-fixture check. It does not qualify native
WPF/WebView2 startup, a live App Server/provider, or recovery after restart
when all Web Storage mutations fail. The Phase 2 active-turn/draft gate remains
open.

## 2026-09-24 — Protocol boundary and current focused suite rerun

Fresh reruns passed the diagnostic UI suite (**49/49**) and the host harness
(**66/66**). The host results include preservation of unknown informational
notifications and schema-valid fail-closed responses to unknown authority
requests. Inspection confirmed the accepted versioned protocol corpus keeps
stable TypeScript, stable JSON Schema, experimental TypeScript, and
experimental JSON Schema in separate generated directories under
`reference/cache/codex/rust-v0.155.1/protocol-generated/`; the hash index is
`reference/cache/codex/rust-v0.155.1/metadata/protocol-generated.sha256.json`.
This closes that bounded protocol-boundary checklist item. It does not close
the broader Phase 2 recovery, draft, accessibility, live long-output, or
native-WPF acceptance gates. No live-provider inference was used in these
suite reruns.

## 2026-09-24 — Pending-send draft recovery after renderer reload

The focused diagnostic UI checks passed **54/54**. `npm run typecheck` passed,
and `npm run build` exited successfully with Astryx module-level `use client`
notices and a 519.69 KB JavaScript chunk-size warning. Microsoft Edge was
driven with local Playwright Core because the Browser plugin was unavailable.
At `http://127.0.0.1:4174/`, title `NeoBabylon`, rendered content, and the
1440×900 viewport were confirmed; no page errors or console
errors/warnings were observed. The complete host integration suite passed
after the mock Responses fixture was corrected to treat listener-abort 995
during explicit disposal as expected test teardown.

The synthetic bridge exercised: start a new task, create a thread, submit one
prompt, suppress the host response to model a renderer restart, then reload.
The accepted case had the exact user prompt in persisted interrupted history:
reconnect rendered it once, cleared the matching stale drafts, left the turn
request count at one, and showed an empty composer on New task. In the
unaccepted case, the fake App Server omitted the empty thread and returned
`thread not loaded` to the exact resume attempt. The UI showed that failure,
kept the transcript empty, retained the original new-task-scoped draft in the
composer, and did not replay the turn (request count remained one).

The empty-thread boundary was also tested against the exact pinned App Server,
without inference: an empty `thread/start` result was absent from `thread/list`
and `thread/read` after restarting the isolated supervisor returned
`thread not loaded`. This is runtime integration evidence for empty-thread
non-persistence, distinct from the synthetic rendered UI check. Draft handling
now copies new-task text into the provisional thread scope; the source is only
cleared when acceptance is confirmed or exact saved history proves the outcome.

This does not qualify native WPF/WebView2 restart, live-provider inference,
total Web Storage failure, or broad concurrent/cross-project recovery. Those
checks remain open. The accepted and unaccepted screenshots are outside the
repository at
`D:\CODING\NeoBabylon-Data\Phase2-EmptyThreadFallback-20260924-final\`:
`draft-recovery-accepted.png` and `draft-recovery-unaccepted.png`.

## 2026-09-24 — Native WPF startup and exact runtime identity

A fresh visible WPF/WebView2 host run used source root
`D:\CODING\NeoBabylon`, application root
`D:\CODING\NeoBabylon-Data\Phase1B-WpfFunctionPatch-20260924\App`, and the
separate `App\Data` runtime state. Diagnostics reported that the ordinary
Codex data/configuration root was not used. The selected App Server binary was
`D:\CODING\NeoBabylon-Data\Phase2-FunctionPatch-Target\debug\codex-app-server.exe`,
version **0.155.1**, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`,
matching `runtime/runtime-lock.json`.

The native page rendered as `NeoBabylon`; its runtime diagnostics showed the
locked identity and separate source/application/data roots. The dark
appearance and dismissed full-access note survived a WebView renderer reload
and a graceful WPF host restart. No model inference or provider request was
made. Captures are under
`D:\CODING\NeoBabylon-Data\Phase1B-WpfFunctionPatch-20260924\qa\`:
`native-wpf-startup.png`, `native-wpf-runtime-details.png`, and
`native-wpf-host-restart-settled.png`.

This verifies native startup and preference persistence only; saved
patch-review rendering through this exact native runtime remains open.

## 2026-09-24 — Native WPF tower brand mark

The focused WPF/WebView2 regression first failed against the old rendered
sidebar: `.brand-symbol` was a `DIV` with the CSS-drawn overlapping circles.
The sidebar now loads the existing `/favicon.svg` tower artwork as a
decorative image, preserving its 29×29 footprint and relying on the adjacent
brand text for the accessible name. The browser favicon and Windows program
icon are unchanged.

The React production build succeeded; diagnostic UI tests passed **54/54**;
TypeScript typecheck passed. Build output retains the known Astryx
module-level `use client` notices and the 500 KB chunk-size warning. Microsoft
Playwright Core was used because the Browser plugin is unavailable. Against
the actual native WPF/WebView2 host at 1464×901, the page title was `NeoBabylon`,
the image loaded from `/favicon.svg`, was marked decorative, measured 29×29,
and there were no framework overlays, page errors, or console errors. No model
inference or provider request occurred. Screenshot:
`D:\CODING\NeoBabylon-Data\Phase1B-WpfFunctionPatch-20260924\qa\native-brand-mark-after.png`.

This is a bounded visual verification only; it does not change or close any
Phase 1B or Phase 2 runtime acceptance gate.

## 2026-09-24 — Native saved-patch review on the current function-patch runtime

The host qualification harness created a new isolated application root at
`D:\CODING\NeoBabylon-Data\Phase2-FunctionPatch-NativeReview-20260924` using
the deterministic loopback Responses fixture. The fixture used the exact
binary named by `runtime/runtime-lock.json` (Codex App Server **0.155.1**,
source revision `be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`). Its
ordinary `apply_patch` round trip produced three attributed diff events and an
available saved review; the deterministic provider received exactly two
fixture requests. No external provider or credential was used.

The native WPF/WebView2 host opened the saved task using that same locked
runtime and application root. The UI rendered the user/assistant exchange and
read-only patch review (`tracked.txt`, `-before`, `+after`) with no approval
action. Playwright Core reported `passed:true`, `reloadPassed:true`, one saved
task, a 418-character review, and no page or console errors. The QA host was
then closed gracefully and restarted with the same isolated root; the same
native review check passed again. Runtime diagnostics in the restarted native
UI displayed the exact source revision and binary hash, application root and
`Data` root, and `Ordinary Codex root used: false`. No provider request was
made while opening or reviewing the saved task. Screenshots:
`D:\CODING\NeoBabylon-Data\Phase2-FunctionPatch-NativeReview-20260924\native-patch-review-current-runtime.png`
and
`D:\CODING\NeoBabylon-Data\Phase2-FunctionPatch-NativeReview-20260924\native-patch-review-after-host-restart.png`.

This closes the specific native rendering/reopen gap for deterministic saved
patch history on this exact runtime. It does not qualify live-provider patch
generation, approvals, general tool behavior, or the full Phase 2 gate.

## 2026-09-24 — Fresh host/UI validation and native appearance interaction

The current NeoBabylon host qualification runner
`dotnet run --project tests/host/NeoBabylon.Phase1A.Tests.csproj --configuration Release`
completed with exit code **0** and no failed checks. In the UI project,
`npm test`, `npm run typecheck`, and `npm run build` each completed with exit
code **0**. The build emitted upstream Astryx module-level `use client`
directive notices; no dependency or system configuration was changed.

A fresh WPF/WebView2 host was launched with no provider credential, a new
isolated application root
`D:\CODING\NeoBabylon-Data\Phase2-CurrentVisualQA-20260924`, and test-only
loopback CDP port `9317`. No provider inference was requested. Using the
already-installed Playwright Core `1.63.0` (the Browser plugin was unavailable),
the native-page check passed at `1464×901`: page title `NeoBabylon`, local
tower SVG loaded as an accessible decorative `29×29` image, no framework
overlay/page/console error, and the diagnostics drawer opened/closed at `510`
pixels wide with a neutral `rgb(28, 28, 28)` surface.

A second native-page interaction verified a fresh profile starts in dark mode
with document background `rgb(24, 24, 24)` and no saved appearance setting;
switching to light produced `rgb(247, 248, 250)`, and switching back restored
dark. The host accepted both appearance bridge operations without errors. The
notice close button hid the full-access note for the current page session and
the note returned after reload; “Don't show again” remained hidden after
reload. At a `960×720` WebView viewport, document scroll width equaled the
viewport width (`960`) and no console/page errors occurred. Screenshots:
`D:\CODING\NeoBabylon-Data\Phase2-CurrentVisualQA-20260924\qa\native-ui-main.png`
and
`D:\CODING\NeoBabylon-Data\Phase2-CurrentVisualQA-20260924\qa\native-ui-960x720.png`.
The screenshot captures WebView content, not the native WPF title-bar chrome;
that chrome was not visually captured in this run. The isolated QA host was
closed gracefully.

Code inspection also found a remaining stale-capability edge. The live
supervisor compares the full serialized capability record and explicit model
selection clears the active thread binding while preserving saved App Server
history. That snapshot is in memory only. After a host restart, the saved-thread
identity guard checks provider/model/workspace, not a durable capability
snapshot, so the same identity with a changed context record can reach
`thread/resume`. This is source-inspection evidence, not a reproduction of the
resume result; stale-record behavior across restart remains unverified.

## 2026-09-24 — Host-harness cleanup rerun and current NEX route preflight

After an earlier host-harness run reported two `Directory.Delete` failures
because `goals_1.sqlite` was in use, the current test-created SQLite files
could be opened exclusively and no test App Server process remained. A fresh
complete run of
`dotnet run --project tests/host/NeoBabylon.Phase1A.Tests.csproj --configuration Release`
exited **0**; both previously failing cleanup cases and the other host checks
passed. The prior lock failure did not reproduce, so its cause remains
unresolved; no retry or cleanup workaround was added.

A read-only OpenRouter preflight on the exact selected model
`nex-agi/nex-n2.5-pro:free` returned one endpoint, Nex AGI `nex-agi/fp8`,
context `262144`, and zero prompt/completion prices. The endpoint response
reported raw `status=-2`. The current official endpoint API reference
documents the endpoint-list operation but does not define that status value;
NeoBabylon therefore retains its semantics as **Unknown**. The official model
page still labels the model Free and lists context `262K`, but does not prove
this particular endpoint is presently healthy. The model catalog expiration
remains `2026-09-25`.

At this read-only preflight checkpoint, no inference had yet been sent. The
current process environment lacked `OPENROUTER_API_KEY`, and the Pi auth file
predated Martin's later stated free-only test key; the older stored credential
was not substituted. A subsequent bounded test using Martin's supplied key is
recorded below. Sources: [OpenRouter Nex-N2.5-Pro (free) model
page](https://openrouter.ai/nex-agi/nex-n2.5-pro%3Afree) and [OpenRouter
model-endpoints API reference](https://openrouter.ai/docs/api/api-reference/endpoints/list-endpoints).

## 2026-09-24 — Exact NEX free-route live function-tool round trip

After deterministic mock-first coverage and a fresh public route preflight, a
bounded live test used the user-supplied free-only key through a hidden,
one-shot secure prompt. The key was held in process memory, not written to
configuration or the application root. The application root was
`D:\CODING\NeoBabylon-Data\Phase1B-NEX-Free-Retry-20260924`; runtime state and
workspace were under its `Data` directory, separate from the source repository
and ordinary Codex root. A credential-prefix scan found no API-key pattern in
the application root.

The test used locked Codex App Server **0.155.1**, upstream revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, binary SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`, and
source-patch SHA-256
`a0a7cd319a31f2125195e7087be72a2867be4779edf00c686ec85f42c58cf520`.
The fresh preflight returned exactly one zero-priced endpoint,
`nex-agi/fp8`, context `262144`, with raw endpoint status `-2` (semantics
Unknown). The request was pinned to that provider endpoint with fallback
disabled. The Codex session recorded the requested model alias
`nex-agi/nex-n2.5-pro:free` and effective context `249036`.

The turn completed with a function-form `apply_patch` call changing only the
isolated `Workspace/tracked.txt` from `before` to `after`; App Server emitted
three attributed diff events and persisted one available review. The session
journal records no effective reasoning effort (`null`); provider-advertised
reasoning defaults are not substituted for that missing observation. No WPF
interaction was part of this particular live run.

Read-only OpenRouter generation lookups for
`gen-1790219773-7Yp5QQTDULNiZI3a0it9` and
`gen-1790219786-pq4zy200aoSHTlFHJB3E` both reported provider Nex AGI, returned
model `nex-agi/nex-n2.5-pro-20260907:free`, zero total cost, `is_byok=false`,
streaming, and `api_type=completions`. Both reported zero prompt/completion
token counters while the isolated Codex journal recorded 12,117 input and 133
output tokens. Usage attribution therefore remains inconsistent. OpenRouter
does not echo the exact endpoint tag in these generation records; endpoint
identity is established by the one-endpoint preflight and request-side pin/no-
fallback controls, not post-hoc endpoint attribution. This proves one
successful request on the selected free model path, not paid-route behavior,
live 429/unavailability handling, repeatability, or broader tool support.

## 2026-09-24 — Stable API with experimental capability disabled

`AppServerProtocol.BuildInitializeRequest` sends
`capabilities.experimentalApi=false`. The current locked App Server completed
stable thread/turn behavior in the deterministic host harness and the live
NEX qualification. On the matching upstream source checkout at
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, the focused runtime integration
command
`cargo test --locked -p codex-app-server --test all suite::v2::experimental_api -- --nocapture`
passed **9/9** tests (1,197 filtered out). This includes a valid
`thread/memoryMode/set` request rejected with JSON-RPC code `-32600` and
`requires experimentalApi capability`, plus stable `thread/start` success
without opt-in. The experimental gate implementation and test file are
unchanged in the source checkout. This closes the plan's bounded
stable/experimental-off item; it does not opt NeoBabylon into experimental
APIs or qualify every experimental method.

## 2026-09-24 — Focused upstream context and compaction tests

Against the runtime checkout whose `HEAD` is
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, these targeted tests passed:

- `cargo test --locked -q -p codex-protocol --lib model_context_window_limits_preserve_their_distinct_meanings` — **1/1**.
- `cargo test --locked -q -p codex-core --test all auto_compact_clamps_config_limit_to_context_window` — **1/1** with process-local `$env:RUST_MIN_STACK='8388608'`.
- `cargo test --locked -q -p codex-core --test all pre_sampling_compact_runs_on_switch_to_smaller_context_model` — **1/1** with the same process-local stack setting.
- `cargo test --locked -q -p codex-app-server --test all auto_compaction_emits_started_and_completed_items` — **2/2** with the same process-local stack setting.

The core clamp test first overflowed the default Rust test-thread stack
(`STATUS_STACK_OVERFLOW`); rerunning with the larger process-local test stack
passed. This was a test invocation setting only; no repository or system
setting was changed. The runtime checkout is dirty, but the context-limit
method/test and the compaction implementation and test files exercised here
were unchanged; unrelated pre-existing edits remain untouched. These tests
support the upstream context/compaction mechanics. They do not prove that
NeoBabylon's normalized capability value reaches a live request budget,
triggers NeoBabylon-observed compaction, or invalidates a stale capability
record. Those Phase 1B checks remain open.

## 2026-09-24 — Re-read of both effective capability projections

The retained application-root artifacts were re-read directly. The LM Studio
authoritative record (`lmstudio` / `phase1a-qwen3-14b`, effective provider
context `32768`) generated
`D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-WPF-20260923\App\Data\CodexHome\model-catalog.json`
with `context_window=max_context_window=32768` and
`effective_context_window_percent=95`. Its completed tool session
`D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-WPF-20260923\App\Data\CodexHome\sessions\2026\09\23\rollout-2026-09-23T05-22-50-01a0cc49-9aa4-7aa1-9e9c-ced89ac98354.jsonl`
records effective session context `31129`, one function call and its output,
and `task_complete`.

The OpenRouter authoritative record (`openrouter` /
`nex-agi/nex-n2.5-pro:free`, effective provider context `262144`) generated
`D:\CODING\NeoBabylon-Data\Phase1B-NEX-Free-Retry-20260924\Data\CodexHome\model-catalog.json`
with `context_window=max_context_window=262144`, the reported reasoning
levels, and `effective_context_window_percent=95`. Its completed live tool
session
`D:\CODING\NeoBabylon-Data\Phase1B-NEX-Free-Retry-20260924\Data\CodexHome\sessions\2026\09\24\rollout-2026-09-24T05-16-11-01a0d169-de35-7ca1-a465-3f72148c89ff.jsonl`
records context `249036`, one function call and its output, and
`task_complete`.

This closes the specific capability-record-to-Codex-effective-model wiring
check for these two qualified tuples. It does not show that a live request
budget uses the value correctly, trigger/observe compaction, enforce the
advertised output limit, independently observe effective reasoning, or reject
a stale same-provider/model record after a host restart.

## 2026-09-24 — Offline credential probe aligned to unrestricted authority

The old OpenRouter credential probe still selected `workspace-write` plus the
legacy `unelevated` Windows backend. After the fail-closed command guard was
added, that stale setup correctly produced an attributed tool failure before
the ordinary child ran. The first test update made the required unrestricted
configuration/effective-authority assertions fail, confirming that the
qualification runner ignored the host's accepted authority choice.

The qualification runner now accepts an explicit `--unrestricted-tools`
selection and carries it consistently into isolated Codex config,
`thread/start`, and `turn/start`. It verifies the effective App Server response
as `dangerFullAccess` / `Codex unrestricted execution` before proceeding.
The old sandbox option remains available for the separate fail-closed guard
probe; combining it with unrestricted policy is rejected by the config builder.

`tests\qualification\MockProbeQualification.Tests.ps1 -OpenRouterCredentialOnly`
then passed against App Server **0.155.1**, upstream revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, binary SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`, and
the selected `openrouter` / `nex-agi/nex-n2.5-pro:free` record. The Responses
endpoint was a loopback deterministic fixture, not OpenRouter. The ordinary
`exec_command` round trip returned `NB_OPENROUTER_KEY_ABSENT` and the Windows
version output; the effective config excluded `OPENROUTER_API_KEY`, disabled
login shells and PowerShell profile loading, and retained the exact model.
The synthetic canary was absent from the saved evidence and a binary-safe scan
found no canary in either isolated source or application/data root. The test
made two local fixture requests and performed no provider inspection, model
load, live inference, or external request. Evidence:

- Qualification artifact:
  `D:\CODING\NeoBabylon-Qualification-Probe-2d9f3bb28f2642fab2f2633c0622b1b3\artifacts\phase1b\openrouter\qualification.json`
- Isolated application root:
  `D:\CODING\NeoBabylon-Data\Qualification-Probe-2d9f3bb28f2642fab2f2633c0622b1b3`

This is partial credential evidence, not the full leakage matrix: no WPF host
or renderer was launched in this test, so renderer visibility remains open.
The host harness initially hit a transient `goals_1.sqlite` cleanup lock; the
immediate full rerun exited 0. Its cause remains unresolved. The deterministic
`tests\qualification\MockProviderError.Tests.ps1` rerun also passed its local
HTTP 429/no-tool/no-fallback assertions.

## 2026-09-24 — Native WPF/WebView2 synthetic credential visibility check

A fresh hidden Release WPF host was launched with a test-only synthetic
`OPENROUTER_API_KEY`, the accepted NEX capability record, a new application
root under `D:\CODING\NeoBabylon-Data`, and test-only WebView2 CDP port 9321.
Other inherited environment names containing `KEY`, `SECRET`, or `TOKEN` were
removed from that host launch. No App Server turn, live provider request, or
inference was initiated. The runtime-details panel displayed only the status
“Credential captured at startup — Yes · held by host”; it did not display the
credential value.

The flow `native WPF startup → local WebView2 page → diagnostics drawer` passed
at 1464×901. Playwright inspected visible text and DOM before and after opening
diagnostics, local/session storage, cookies, captured host-to-renderer bridge
messages, renderer resource URLs, and renderer request URLs/headers. It found
zero canary occurrences, zero page/console errors, and six renderer requests,
all to `https://neobabylon.local`. After graceful close of only the test host,
a binary-safe scan of the entire isolated application root found no canary and
the CDP port was closed. The screenshot is
`D:\CODING\NeoBabylon-Data\CredentialVisibility-99f4a3f601095ded06b127aa740837b7\qa\credential-visibility-settled.png`.

Together with the preceding deterministic App Server child-environment probe,
this closes the planned **synthetic-canary surface check** for the tested
OpenRouter/NEX configuration: child environment/output, shell/profile policy,
isolated artifacts/workspace, and native renderer/browser storage/bridge/network
surfaces. It does not prove safety for arbitrary providers, an actual user key,
or host-process memory. The WPF binary was rebuilt for this run with zero
warnings and errors; no UI/runtime source changed during the visibility test.

## 2026-09-24 — Fresh NEX free-route preflight with unrestricted mock path

At `2026-09-24T04:32:31Z`, the qualification runner performed fresh public
OpenRouter model and endpoint reads, then completed its local deterministic
mock `exec_command` round trip using the explicitly unrestricted policy. The
exact record still matched `nex-agi/nex-n2.5-pro:free` / canonical slug
`nex-agi/nex-n2.5-pro-20260907`, context `262144`, provider Nex AGI, endpoint
`nex-agi/fp8`, and zero prompt/completion prices. Exactly one endpoint was
listed. The public model expiration is `2026-09-25`; endpoint-specific expiry
remains Unknown. Raw endpoint `status=-2` remains semantically Unknown. Route
pinning was retained with provider fallback disabled.

The local mock path passed with effective model/provider unchanged and
`dangerFullAccess` effective authority; it made two loopback fixture requests
and no authenticated provider request, model inference, or model load. The
task process had no `OPENROUTER_API_KEY`, so no live repeat was attempted and
the older Pi credential was not substituted. Evidence:

- Qualification artifact:
  `D:\CODING\NeoBabylon-OpenRouter-LiveMetadata-9e3c218bc5775813dcc860a606ab88cb\artifacts\phase1b\openrouter\qualification.json`
- Isolated application root:
  `D:\CODING\NeoBabylon-Data\OpenRouter-LiveMetadata-9e3c218bc5775813dcc860a606ab88cb`

## 2026-09-24 — Deterministic effective-context and compaction qualification

Added a loopback-only host integration probe using the pinned App Server
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318` and the
existing LM Studio capability record as its source shape. The probe overrides
context to **2,048 tokens in test-only metadata**; this is not a claim about the
provider's actual context. It writes isolated Codex config/catalog under a
temporary application root, selects the explicitly unrestricted execution
policy, and sends three short turns to a deterministic local Responses fixture.
The fixture reports 500 and then 1,900 total tokens; the third turn triggers a
local compaction-summary request and a final reply. No provider credential,
provider inspection, live inference, or external request was used.

Observed results:

- Codex model catalog and isolated `config.toml`: context `2048`, maximum
  context `2048`, `effective_context_window_percent=95`, and no invented
  explicit `auto_compact_token_limit`.
- App Server session journal: usable model context `1945` (`2048 * 95%`,
  integer-rounded down).
- Pinned Codex model metadata derives auto-compaction at `1843` (`2048 * 90%`,
  integer-rounded down); fixture usage `1900` crossed it.
- The third user turn emitted `item/started` and `item/completed` for the same
  context-compaction item. Exactly four loopback `/v1/responses` requests
  covered the two initial replies, one compaction-summary response, and the
  post-compaction final reply for that third turn.
- Targeted probe
  `dotnet run --project tests\host\NeoBabylon.Phase1A.Tests.csproj --configuration Release --no-restore -- --probe-effective-context-compaction`
  passed. The complete host harness rerun with this probe included exited 0.
- `tests\qualification\MockProbeQualification.Tests.ps1 -OpenRouterCredentialOnly`
  passed; `tests\qualification\MockProviderError.Tests.ps1` passed its local
  HTTP 429/no-tool/no-fallback case.
- One immediate rerun hit a transient Windows lock while deleting the exact
  temporary root's `goals_1.sqlite`; the App Server child had exited and an
  exclusive read became available shortly afterward. Test cleanup now retries
  deletion only inside its GUID-scoped temporary parent with bounded backoff.
  The focused probe and complete host harness both passed after that change.
- Current renderer verification: `npm test` passed **54/54** and
  `npm run build` (including TypeScript build) exited 0. Vite reported
  non-fatal dependency `use client` directive notices and a chunk-size advisory
  above 500 kB; generated `dist/` output is ignored by Git. These are build
  warnings, not compilation failures.

This closes only deterministic capability-to-effective-context and automatic
compaction wiring. It does not establish live provider token-accounting or
budget behavior, live compaction, model switching/reload invalidation, stale
capability rejection after restart, or the full Phase 1B acceptance gate. The
test application root was removed after the probe; its console evidence
reported `codexUsableContext=1945`, `derivedThreshold=1843`, and four provider
requests, with the runtime hash above.

## 2026-09-24 — Durable per-thread capability binding and history-only UI

The earlier source review identified that an active supervisor guarded the
complete serialized capability record only in memory. After restart, a saved
thread with unchanged provider/model/workspace but a changed endpoint or
context could pass the older resume identity check. This was fixed with a
versioned per-thread sidecar under isolated application `Data`, as described
in [NB-DEC-009](../decisions/0009-thread-capability-binding.md). The sidecar
stores a SHA-256 identity of the complete capability record plus exact thread,
workspace, provider, and model identity; it does not copy the raw endpoint,
credentials, or conversation text. Workspace normalization now preserves
filesystem roots rather than turning `C:\` into drive-relative `C:`.

The test-only host harness built to a GUID-scoped temporary output directory
because the normal Release host assemblies were locked by two existing
NeoBabylon processes; neither process was stopped. The build completed with
zero warnings/errors, and the generated test apphost exited **0**. The locked
Codex App Server exercised by the deterministic fixtures was **0.155.1**,
SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`; the
accepted LM Studio capability record was redirected only to loopback fixtures,
so no live model inference occurred. A separate Release WPF host build to an
isolated temporary output also passed with zero warnings/errors. It verified:

- The same-provider/model thread resumes normally after a host restart only
  when the complete capability record still matches.
- A changed endpoint on that same provider/model returns transcript history
  with `executionEligible=false`; a new turn is rejected, original history
  remains listed, and the changed loopback endpoint receives zero inference
  requests.
- Deleting the sidecar yields `capabilityBindingMissing`, preserves history,
  and does not recreate the sidecar. Corrupt JSON yields
  `capabilityBindingUnavailable`, preserves history, and is not rewritten.
  Neither path issues a provider request.
- The binding store rejects a conflicting rewrite, detects an endpoint change,
  keeps the filesystem-root workspace exact, and stores identity metadata
  without the raw endpoint or credential-shaped field.

The React client now treats only `executionEligible=true` as executable for a
resumed task. History-only tasks preserve their transcript, skip pending-send
reconciliation, show the host's explicit block reason, and disable the composer
and fork control; “New task” remains available. The diagnostic UI passed
**55/55** tests, and `npm run build` (TypeScript plus Vite) exited 0. Vite still
prints non-fatal dependency `use client` notices and a chunk-size advisory
above 500 kB. A local in-app browser render confirmed the charcoal shell and
composer layout, but the browser had no WPF host bridge; it did not exercise a
host-projected history-only screen. No live provider inference, shared config
change, commit, or push was performed.

This closes the cross-restart stale-record mismatch case at the tested host
boundary, not all model-switch/reload behavior or the Phase 1B/Phase 2 gates.
Because tools are explicitly unrestricted, the unkeyed local sidecar is not
tamper-proof against model-controlled host-file changes; hostile local mutation
remains an explicit limitation rather than a security guarantee.

## 2026-09-24 — Repeated pinned-runtime interruption and no-replay recovery

Added a deterministic acceptance probe around the exact pinned Codex App
Server (`0.155.1`, SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`). It
uses the LM Studio capability record only as the model-metadata source; its
endpoint is replaced in the isolated Codex config with a loopback Responses
fixture. No live provider, API credential, or external request was used.

Two fresh isolated roots each started a real pinned App Server and thread,
held one Responses request in progress, issued the named host interruption,
and then released the fixture. Both attempts returned a terminal
`turnInterrupted` / `interrupted` result with exactly one provider-fixture
request. After supervised shutdown, no new process for the exact locked App
Server binary remained. A newly created supervisor resumed each same saved
thread under its unchanged capability binding; the fixture request count
remained one, proving that history resume did not replay the cancelled turn.
The test invokes no tools, so the process-cleanup result covers the isolated
App Server child, not arbitrary subprocess trees from tool execution.

The focused probe exited **0** for both attempts, and the complete host harness
later exited **0** with **69/69** checks reporting `PASS`. Build output was
isolated at
`D:\CODING\NeoBabylon-Data\qa\NeoBabylon-RepeatedInterruption-0dc13af953e74a7daf04bdd76b884f8a\build`
because two existing NeoBabylon windows held the normal Release assemblies;
neither window was stopped or modified. Test application roots were removed
after each attempt.

This closes deterministic repeated interruption, terminal-state attribution,
App Server process cleanup, and saved-thread no-replay at the tested pinned
runtime boundary. Together with the separately recorded one live LM Studio
Stop-after-delta run, it does not prove repeated live-provider/WPF cancellation
or cancel behavior after partial streamed output on the OpenRouter route. The
other open Phase 1B checks and the overall Phase 1B gate remain open.

## 2026-09-24 — Explicit model switching and exact task rebinding

A new deterministic integration probe used the pinned App Server and a
loopback Responses fixture with the LM Studio capability record as its base.
The alternate model identifier `fixture/alternate-model` is deliberately
test-only; no live model was selected and no external request or credential was
used. The scenario completed a user-authored Model A turn, explicitly reset
the active binding, verified a turn was rejected before a new thread existed,
started Model B in a distinct thread, then reset again and resumed Model A by
its exact saved capability binding. Model A's transcript remained available,
and an explicit new Model A continuation completed.

The three captured Responses requests contained the exact sequence
`phase1a-qwen3-14b` → `fixture/alternate-model` → `phase1a-qwen3-14b` and each
contained its matching user-authored prompt. There were no provider requests
during either model reset or the saved-thread resume. The focused
`--probe-model-switch-rebind` run exited **0**. After both new probes were
included, the full host harness completed with **69/69** checks passing.

One preceding full-harness run had **68 passes and one transient cleanup
failure**: the patch-diff fixture's immediate recursive deletion encountered
a locked `goals_1.sqlite`. Inspection found that this fixture did not use the
same bounded, exact-parent-validated cleanup retry already used by the context
compaction fixture. It now reuses that helper for its GUID-scoped temporary
root. The final full harness run after this and both new probes' cleanup-helper
updates exited **0**, with **69/69** checks passing. This change does not widen
cleanup beyond the precise test-owned roots.

This qualifies one same-process model-switch/rebind path. Model switches across
renderer/host restart, live provider context accounting, live compaction, and
the full Phase 1B gate remain open.

## 2026-09-24 — Phase 2 P2-01 native history pagination and reopen

The P2-01 acceptance script seeded 56 saved conversations through the exact
pinned Codex App Server `0.155.1` (binary SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`) and a
deterministic loopback Responses fixture. The native WPF/WebView2 client
loaded the first page of 50 and the older page of 6, with unique IDs and
newest-first ordering. Searching for the unloaded oldest task said that only
loaded conversations were searched and offered to load older history. After
loading the second page, the oldest task was found and reopened with the exact
saved user prompt and fixture assistant response, and its sidebar attribution
was `lmstudio · phase1a-qwen3-14b`. Provider request count was 56 before native
browsing and remained 56 after resume: reopening history did not infer or
replay. Cursor exhaustion removed the older-page control. Switching to a
second, empty project showed the empty state with zero loaded tasks and no
stale cursor or cross-project history. Browser page/console issue collection
was empty.

The runner was
`D:\CODING\NeoBabylon-Data\QA\Phase2-P201\prepare-final\bin\Release\net10.0-windows\NeoBabylon.Phase1A.Tests.exe`;
the WPF host was
`D:\CODING\NeoBabylon-Data\QA\Phase2-P201\wpf-build\bin\Release\net10.0-windows\NeoBabylon.Host.exe`.
The retained native screenshot is
`D:\CODING\NeoBabylon-Data\QA\Phase2-P201\P2-P01-native-1790237419128-31528\native-history-pagination.png`.
The isolated application root was
`D:\CODING\NeoBabylon-Data\QA\Phase2-P201\P2-P01-native-1790237419128-31528\application`,
separate from source root `D:\CODING\NeoBabylon`; the WPF diagnostic panel
reported the isolated application `Data` root and that the ordinary Codex
root was not used. This was fixture-backed local execution, not a live-model
test.

Verification on the same code state also included the diagnostic UI suite
(59/59), TypeScript typecheck, TypeScript/Vite production build, and isolated
Release WPF host build (zero warnings/errors). The Release host test apphost
completed the full harness with exit code 0 and all emitted checks passing.
An immediately preceding full-harness attempt had reported that exact pinned
App Server PID `60664` survived supervisor disposal during the repeated
interruption probe; a later process query found no such PID. The focused
`--probe-repeated-interruption-recovery` probe then passed twice, each time
confirming terminal interruption, one fixture request, saved-thread resume
without replay, and no exact pinned App Server child after disposal. A
subsequent full harness exited 0. The earlier process assertion was not
reproduced, but its cause remains unresolved and should be rechecked during
P2-13 rather than treated as fixed.

The native fixture did not exceed the host transcript projection limit, so
the `historyTruncated` omission notice is code-inspected but its overflow
branch was not exercised in this run. Search remains explicitly limited to
loaded pages; no server-backed full-history search was qualified. These
limitations, broader history scale/concurrency, the other P2 slices, and the
overall Phase 2 gate remain open. No live inference, commit, or push occurred.

## 2026-09-24 — Phase 2 P2-02 model capability switch and exact task rebind

The native acceptance harness passed against Codex App Server `0.155.1`,
source revision `be2951ea34f0d295ed0becf97079f92fa5f6950e`, binary SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`. The
host's runtime identity check and a separate SHA-256 read confirmed the
manifest's exact binary. WPF/WebView2 used two unique, synthetic
`lmstudio`-routed records and local endpoints only:

- `p2-02-fixture-model-a`, effective context **32,768**;
- `p2-02-fixture-model-b`, effective context **16,384**, changed before the
  second launch to **8,192**.

Those context values are fixture values, not provider observations. All
provider/model metadata remains Unknown except the explicitly synthetic
catalog prerequisites (`tool_use` and text-only input) needed to start a
Codex thread; no tool/function call was made. Each deliberately authored task
produced exactly one deterministic loopback Responses request. There were no
provider requests on model switch, host restart, stale-thread inspection, or
exact task resume.

The test application root was
`D:\CODING\NeoBabylon-Data\QA\Phase2-P202\P2-P02-native-1790240447407-40036\application`,
distinct from source root `D:\CODING\NeoBabylon`. The native diagnostics panel
showed state under that application's `Data` directory and explicitly
reported that the ordinary Codex data root was not used. After restart, the
original Model A task was visible under Model A. Selecting Model B displayed
its saved transcript and a visible different-capability warning while
disabling its composer and Fork action; New task remained enabled. The
Model B capability-binding sidecar stayed byte-identical. Selecting Model A
again reopened the original thread and active-task pointer, kept its binding
byte-identical, and caused no additional Responses request. Browser console
and page errors were empty.

The native reproduction exposed one actual UI sequencing defect: model
selection started `refreshThreads()` without awaiting it, releasing the busy
state while the host still held its App Server operation guard. A fast Send
then failed with “The current App Server operation must finish before starting
a new thread.” `App.tsx` now awaits that refresh before clearing the switching
state; the same native flow passed after the fix.

The retained screenshots are:

- Model B task history-only after restart:
  `D:\CODING\NeoBabylon-Data\QA\Phase2-P202\P2-P02-native-1790240447407-40036\model-b-history-only.png`
- Unchanged Model A task reopened:
  `D:\CODING\NeoBabylon-Data\QA\Phase2-P202\P2-P02-native-1790240447407-40036\model-a-reopened.png`
- Model B task before restart:
  `D:\CODING\NeoBabylon-Data\QA\Phase2-P202\P2-P02-native-1790240447407-40036\model-b-created.png`

Verification: diagnostic UI tests **59/59 passed**; the TypeScript/Vite
production build passed with upstream `use client` directive and >500-kB
chunk warnings; the isolated Release WPF host build passed with zero
warnings/errors (WPF host sources were unchanged in this UI slice). Focused
host probes `--probe-model-switch-rebind` and
`--probe-context-change-invalidation` exited 0. A full-host test build in the
normal Release output was blocked because two pre-existing WPF host processes
(PIDs `62356` and `30660`) held `NeoBabylon.Core.dll`; they were left running.
The already-built Phase2-P201 test runner then ran the full host harness but
exited **1** on an unrelated test-cleanup failure: the empty-thread-resume
check reached its expected unavailable-thread result, but its `finally`
directory deletion found `logs_2.sqlite` locked. All other emitted checks
passed. The failed cleanup left its test-generated root at
`C:\Users\Martin\AppData\Local\Temp\NeoBabylon-EmptyThreadResume-Tests\52850fffa7f64fdfbe62a44e81f16f87`.
A current-source full-host rebuild/run remains a verification gap;
this does not negate the P2-02 native acceptance pass or the two focused
passing probes.

P2-02 closes only the recorded synthetic model-switch/restart/rebind path.
There was no live inference, no provider metadata qualification, and no claim
that this proves live context accounting, compaction, broader recovery, Phase
1B, or the overall Phase 2 gate. No commit or push occurred.

## 2026-09-24 — Phase 2 P2-03 pending-send reconciliation

**Accepted requirement:** if the renderer reloads or the host exits around the
App Server `turn/started` acknowledgement, an accepted prompt must appear once
and must not be automatically replayed; a send not accepted by App Server must
remain available for an explicit retry. Preserve scoped drafts and disclose an
unknown outcome.

**Implementation choice/test finding:** keep the UI busy while one or more
saved-thread refreshes are outstanding. A delayed native `listThreads`
reproduction showed the renderer could otherwise re-enable Send while the WPF
host's serialized App Server operation guard was still held. The UI now tracks
overlapping refreshes and releases the busy state only when all finish.

The new native harness is
`tests/qa/native-pending-send-recovery.mjs`. It launched the current-source
WPF/WebView2 host against a loopback-only deterministic Responses fixture,
using a uniquely named synthetic LM Studio capability record. The synthetic
record declares only what the App Server test path requires; model weights,
provider metadata, reasoning support, and structured output are not claimed.
No live provider request or model inference occurred.

The first boundary intercepted Send before the matching `turn/started` event.
The request count did not increase beyond the baseline; the exact prompt
remained in its project/task-scoped draft after a real renderer reload; no
App Server user-message journal entry existed for it; and the harness observed
no automatic replay. An explicit retry then produced one additional fixture
request and exactly one matching journal user message.

The second boundary allowed `turn/started` to be acknowledged, then held the
fixture response open while the WPF host restarted. The accepted prompt was
present exactly once in the App Server journal before and after restart; the
draft had cleared; and the reopened UI showed the interrupted/unknown-outcome
warning without sending another request. The three total fixture requests
were baseline, explicit retry, and accepted prompt. The harness reported
`passed=true`, no page errors, and no fallback to the ordinary Codex data
root.

The pinned App Server binary was version `0.155.1`, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`, matching
`runtime/runtime-lock.json`. The isolated QA application root was outside the
source repository and its `Data` directory held runtime state; diagnostics
reported that ordinary Codex data was not used. No provider/shared configuration
was changed. The retained run used
`D:\CODING\NeoBabylon-Data\qa\Phase2-P203\P2-P03-native-1790243304564-68040\application`
as its application root and `D:\CODING\NeoBabylon` as its source root.

The normal Release output directory could not be rebuilt because two
pre-existing WPF hosts (PIDs `62356` and `30660`) held the output assemblies;
they were left running. A fresh current-source Release host was instead built
to `D:\CODING\NeoBabylon-Data\QA\Phase2-P203-build\bin\` with `dotnet build
host/NeoBabylon.Host/NeoBabylon.Host.csproj -c Release --no-restore
'-p:OutDir=D:\CODING\NeoBabylon-Data\QA\Phase2-P203-build\bin\'` and passed
with zero warnings/errors. The diagnostic UI passed **59/59 tests** and
`npm run build` passed; Vite/Rolldown emitted the existing module-level
`use client` and large-chunk advisories. `npm run typecheck` and the harness
syntax check also passed.

The native race regression first failed before the UI change because the
composer was enabled during a delayed thread refresh. After the change,
`refreshingThreads` contributes to the busy state and a refresh counter handles
overlapping requests; the same native recovery harness passed. The only
production source change for this slice is in `ui/diagnostic/src/App.tsx`.

Retained native screenshots are under
`D:\CODING\NeoBabylon-Data\QA\Phase2-P203\P2-P03-native-1790243304564-68040\`:

- `before-acknowledgement.png`
- `unaccepted-after-renderer-reload.png`
- `composer-disabled-during-history-refresh.png`
- `accepted-before-host-restart.png`
- `accepted-after-host-restart.png`

P2-03 closes only this bounded mock acknowledgement/reload/restart journey.
It does not qualify live-provider behavior, general crash recovery, total
storage failure, or broad concurrency; Phase 1B remains deferred/incomplete
and full Phase 2 remains open. No commit or push occurred.

## 2026-09-24 — Phase 2 P2-04 local-storage failure behavior

**Accepted requirement:** injected WebView2 draft-write/clear failure must not
silently imply a prompt was sent or silently discard text while the window is
open. The UI must show the state and let the user inspect authoritative App
Server history before manually retrying. If storage is fully unavailable,
NeoBabylon must state the durability limit rather than claim it can recover
data it could not persist. No second transcript store is authorized.

**Implementation choice/test scope:** no product-code change was needed. A
test-only Playwright Core harness injects `Storage` method failures into the
native WebView2 page and exercises the existing warning and task-history
behavior with isolated application roots. It tests (1) rejecting writes to
the task draft key, (2) rejecting both `removeItem` and the empty-string
`setItem` fallback after the prompt was accepted, and (3) throwing from every
Web Storage method. This verifies current UI behavior; it does not add an
independent durable draft mechanism.

The retained successful run is
`D:\CODING\NeoBabylon-Data\QA\Phase2-P204\P2-P04-storage-1790246050288-30396\`.
Its three application roots are `write-failure\application`,
`clear-failure\application`, and `total-storage-failure\application`; each
root is separate from source `D:\CODING\NeoBabylon` and stores runtime state
under its own `Data` directory. The App Server executable passed the runtime
lock hash check: version `0.155.1`, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`, matching
`runtime/runtime-lock.json`. A fresh WPF Release host was built to
`D:\CODING\NeoBabylon-Data\QA\Phase2-P204-build\bin\` with zero warnings and
errors.

The model tuple was synthetic only: provider id `lmstudio`, model identifier
`p2-04-storage-failure-fixture`, ephemeral loopback endpoint under
`http://127.0.0.1:60380/v1` for the final successful run, and no model weights.
The endpoint port is ephemeral, not a durable service address. The capability record
explicitly labels provider version, advertised context, reasoning controls,
structured output, and quantization Unknown; its test-only effective context,
tool-use, and agent metadata are fixture prerequisites, not real provider
observations. No LM Studio process or live model was used.

Observed results:

- Draft-write rejection kept the exact unsent text visible with “This draft
  could not be saved locally. Keep this window open until you can send or copy
  it.” After host restart, that never-persisted text was absent, the baseline
  App Server history remained inspectable, and no extra request or journal
  entry appeared. This is an unavoidable persistence limit, not a recovery
  regression hidden by the test.
- The clear-failure case exercised both deletion and empty-value fallback
  failures. The accepted prompt was journaled and displayed once in history;
  after restart it also remained in the composer as a stale draft, with “A
  prior send could not be matched safely to saved task history. Check this
  conversation before resending.” The failed-clear warning was visible before
  restart. No request was replayed.
- With all Web Storage APIs faulted, the unsent text and local-write warning
  were visible before restart. After restart, saved baseline history remained
  readable and the UI warned “Saved drafts are unavailable in this WebView
  profile. Unsent text may not survive a restart.” Because the pending-send
  marker read also failed, the UI showed the conservative unresolved-pending
  warning (“A prior send could not be matched safely to saved task history.
  Check this conversation before resending.”). The unpersisted text could not
  be reconstructed and no request was made. The harness asserts both warnings.
- Exactly four authored prompts reached the loopback fixture: one history
  baseline in each case and the single explicitly accepted clear-failure
  prompt. The two unsent drafts caused zero requests. The harness asserted
  request count, journal entries, and zero WebView page errors; it reported
  `passed=true`.

During harness development, initial attempts exposed test-only assumptions:
the already-selected saved-history row was disabled, fixture prompt matching
could match an earlier request, and a post-restart prompt selector could match
both transcript and composer text. The harness now accepts the selected row,
validates the next expected fixture prompt, and scopes the transcript locator.
These were corrected in test code; no production defect or production source
change resulted. The native scenarios passed before and after the final
additional assertion for the unresolved-pending warning.

Retained screenshots in the run directory:
`write-failure\write-failure-before-host-restart.png`,
`write-failure\write-failure-after-host-restart.png`,
`clear-failure\clear-failure-before-host-restart.png`,
`clear-failure\clear-failure-after-host-restart.png`,
`total-storage-failure\total-failure-before-host-restart.png`, and
`total-storage-failure\total-failure-after-host-restart.png`.

The final checks were:

- `npm test` in `ui/diagnostic`: **59 passed, 0 failed**.
- `npm run typecheck` in `ui/diagnostic`: passed.
- `npm run build` in `ui/diagnostic`: passed. Vite/Rolldown emitted existing
  module-level `use client` advisories and a 522.02 kB minified JavaScript
  chunk-size advisory; these are not acceptance failures.
- `dotnet build host/NeoBabylon.Host/NeoBabylon.Host.csproj -c Release
  --no-restore '-p:OutDir=D:\CODING\NeoBabylon-Data\QA\Phase2-P204-build\bin\'`:
  passed with zero warnings/errors.
- `node --check tests/qa/native-draft-storage-failure.mjs`: passed. The full
  native harness passed twice; its final rerun also asserted the unresolved
  pending-state warning.

The native harness used Playwright Core over the WPF host's test-only CDP
port; the Browser plugin was not available in this environment. This closes
only P2-04's injected Web Storage cases; it does not
qualify arbitrary profile corruption, disk-full behavior, OS-level profile
loss, broad crash/concurrency recovery, live provider behavior, Phase 1B, or
the overall Phase 2 gate. No production source, shared provider/Codex
configuration, Git history, commit, or remote was changed.

## 2026-09-24 — Phase 2 P2-05 cross-project operation ordering and recovery

**Accepted requirement:** each task stays bound to its exact project folder
and capability. Interleaved history, project-switch, and turn-start attempts
must neither route work into the wrong project nor lose the selected task.
After an interrupted turn, the user can deliberately continue the right task;
the runtime must not replay it automatically.

**Implementation choice and scope:** no production behavior needed changing.
The host regression uses two local workspaces and one identical synthetic
capability record so the cross-project resume case reaches the workspace
identity check rather than the separate model-change guard. A loopback-only
Responses fixture makes the first Alpha request and later continuations
deterministic, and holds one accepted Beta response open during restart. The
native harness delays the Beta history request at the WebView2 bridge to
observe the renderer's busy state. Its temporary, uniquely named
`MODEL_CAPABILITY_P2_05_*.json` record was removed after the run only after
the exact test-created content hash matched. No model weights, live provider,
or live inference were used.

The Release test/host build passed with zero warnings and errors to
`D:\CODING\NeoBabylon-Data\QA\Phase2-P205-host-tests\bin\`. The focused host
probe passed both operation orderings: while an Alpha turn was active, both
history and a Beta switch were rejected and no extra provider request was
issued; after an Alpha history scan started first, a competing turn was
rejected. Alpha/Beta histories were workspace-filtered, cross-project Alpha
resume while Beta was selected failed closed on workspace mismatch, and the
selected project/task survived supervisor restart. Separate explicit
continuations in Beta and Alpha completed without replay or duplicate
requests.

The native WPF/WebView2 harness passed in
`D:\CODING\NeoBabylon-Data\QA\Phase2-P205\P2-P05-native-1790251422143-68928\`.
It delayed Beta's history response for 1.8 seconds; the composer and competing
project button remained disabled and the fixture request count did not
increase. After accepting one Beta prompt, the host restarted while its local
fixture response was pending. The reopened UI selected the exact Beta folder,
restored the same workspace/provider/model/thread pointer, displayed “No
automatic replay,” and retained one journal entry for that prompt without
increasing the two-request provider count. A native bridge attempt to resume
Alpha under Beta returned an error specifically stating that the saved thread
workspace did not match; it did not mutate Beta's pointer or contact the
provider. The user-authored Beta continuation and the later Alpha continuation
each added one request. Final count was four requests for four authored turns;
each exact prompt appeared once in the isolated App Server journal. The final
Alpha selection and original Alpha task identity were restored. No WebView
page errors were observed.

The selected tuple was deliberately synthetic: provider id `lmstudio`, model
`p2-05-cross-project-loopback-model`, and an ephemeral loopback endpoint
`http://127.0.0.1:60843/v1` for this run. Provider server version, actual
model metadata, weights, reasoning, and structured-output claims are not
qualified by this fixture. The actual pinned App Server was version
`0.155.1`, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, and SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`, matching
`runtime/runtime-lock.json`. Source root was `D:\CODING\NeoBabylon`; isolated
application root was
`D:\CODING\NeoBabylon-Data\QA\Phase2-P205\P2-P05-native-1790251422143-68928\application`
and runtime state was under its `Data` child. Diagnostics reported that the
ordinary Codex root was not used.

Checks performed:

- `dotnet build tests/host/NeoBabylon.Phase1A.Tests.csproj -c Release
  --no-restore '-p:OutDir=D:\CODING\NeoBabylon-Data\QA\Phase2-P205-host-tests\bin\'`:
  passed, zero warnings/errors.
- `dotnet D:\CODING\NeoBabylon-Data\QA\Phase2-P205-host-tests\bin\NeoBabylon.Phase1A.Tests.dll
  --probe-cross-project-operation-ordering`: passed.
- Full host harness via
  `& 'D:\CODING\NeoBabylon-Data\QA\Phase2-P205-host-tests\bin\NeoBabylon.Phase1A.Tests.exe'`:
  exited 0, including the newly registered P2-05 check. An earlier manual
  invocation through `dotnet NeoBabylon.Phase1A.Tests.dll` failed eight
  self-hosted fake-App-Server cases because `Environment.ProcessPath` then
  named `dotnet.exe`, whose CLI help text (beginning “Possible reasons…”) was
  misread as JSON protocol output. Running `dotnet --listen stdio://`
  reproduced that CLI output; rerunning the harness apphost executable
  correctly re-entered the fake server and all checks passed. This was a test
  invocation issue, not a runtime source failure.
- `node --check tests/qa/native-cross-project-recovery.mjs`: passed.
- `node tests/qa/native-cross-project-recovery.mjs <host.exe>
  <Playwright-Core-index.mjs> D:\CODING\NeoBabylon-Data\QA\Phase2-P205`:
  passed; Playwright Core was installed only under the sibling QA directory
  because no reusable local package was available. The Browser plugin was
  unavailable. No global/system-wide dependency or UI package manifest was
  changed.
- `npm test` in `ui/diagnostic`: 59 passed, 0 failed; `npm run typecheck`:
  passed; `npm run build`: passed. The existing Astryx/Lucide module-level
  `use client` notices and 522.02 kB minified chunk-size advisory remain.

Machine result is retained as `result.json`. Screenshots are
`beta-history-refresh-busy.png`, `beta-pending-before-restart.png`,
`beta-after-restart.png`, and `alpha-after-return.png`. This closes the
focused two-project serialized-operation and graceful host-restart journey
only. It does not qualify forced process crash, broad concurrency/stress,
cross-provider behavior, live inference, or Phase 1B; Phase 1B remains
deferred/incomplete and the full Phase 2 gate remains open. No production
source, shared Codex/provider configuration, Git history, commit, or remote
was changed.

## 2026-09-24 — Phase 2 P2-06 stop or lose a live turn, then continue safely

**Accepted requirement:** partial assistant output must remain visible through
an interrupted or failed turn; the UI must distinguish an unconfirmed stream
loss from an App Server-persisted terminal status, never replay the original
request automatically, and permit a separate user-authored continuation.

**Implementation choice and scope:** the UI now settles streamed assistant
bubbles when the host returns a terminal or failed turn result. It preserves
the received text and removes the stale “working” indicator without inventing
a final answer. The test suite extends fake App Server interruption and child
exit messages with a partial `item/agentMessage/delta`. A new native harness
uses a local loopback Responses fixture that emits one partial text delta and
holds the response open until the user presses Stop. The temporary capability
record is removed only when its content hash still matches the test-created
bytes. No production Codex runtime, shared configuration, or provider
credential was changed.

**Deterministic protocol evidence:** the host test assertions were first run
against the old fixture and failed specifically because no partial delta was
retained for the interrupted observation or child-exit observation. After the
fake App Server emitted those deltas, both focused cases passed in the full
host harness. Cancellation retained the terminal `interrupted` status, did
not report success, and preserved the App Server notifications in order.
Child exit retained the partial delta, returned a nonterminal observation, and
produced a typed `appServerTurn` failure attributed to Codex App Server. These
cases use the test runner as a fake App Server; they do not simulate a real
provider or a pinned-runtime process crash.

**Native WPF/WebView2 evidence:** run root
`D:\CODING\NeoBabylon-Data\QA\Phase2-P206\P2-P206-native-1790253640420-28092\`;
isolated application root
`D:\CODING\NeoBabylon-Data\QA\Phase2-P206\P2-P206-native-1790253640420-28092\application`;
runtime state was under its `Data` child. The runtime-lock-verified App Server
was `0.155.1`, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`.
The diagnostic UI reported the selected test capability and application root,
and confirmed `Ordinary Codex root used: false`.

The exact tuple was synthetic provider id `lmstudio`, model
`p2-06-loopback-interruption-model`, endpoint
`http://127.0.0.1:62758/v1`, and a deterministic local Responses fixture. The
test first saw the original prompt once in request 1, then held the stream
after `Partial answer before stop.`. The WPF Stop action produced a visible
`Codex App Server: Turn interrupted. No automatic replay.` marker; partial text
remained visible and no longer had streaming chrome. Before reload there was
one provider-fixture request. After renderer reload, the App Server-saved
`interrupted` status and no-replay warning were visible, still with one
request. The incomplete assistant text was absent from the restored transcript
and isolated session journal: the partial output is currently live-window
state only, not durable App Server history. One deliberate continuation added
request 2. Each authored prompt appeared exactly once in the saved journal,
and the original request was not replayed. Graceful WPF shutdown left no newly
owned pinned App Server process. The result JSON and screenshots are retained
under the run root: `result.json`, `partial-stream.png`, `interrupted.png`,
`restored-interrupted.png`, and `continued.png`.

**Live-provider follow-up:** the first attempt at this check did not pass the
user-supplied credential into its isolated test process. That was a harness
handoff error, not a missing key or permission. It was corrected with a masked
credential prompt; no key was written to project files or test artifacts. The
bounded native WPF/WebView2 run passed on the exact selected OpenRouter/NEX
free route. No provider or model fallback occurred.

- Observed UTC: `2026-09-24T13:27:02.175Z`; public route preflight at
  `2026-09-24T13:24:12.295Z`.
- Exact tuple: OpenRouter model `nex-agi/nex-n2.5-pro:free`; endpoint provider
  Nex AGI; endpoint tag/route `nex-agi/fp8`; model variant
  `nex-agi/nex-n2.5-pro-20260907`; public preflight listed one endpoint,
  context 262144, and zero prompt/completion prices. Those are preflight
  metadata, not a per-request billing attestation.
- Exact runtime: App Server `0.155.1`, source revision
  `be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
  `636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`.
- Isolated run root:
  `D:\CODING\NeoBabylon-Data\QA\P2-06-live-cancellation-1790256252295-41464\`;
  application root `...\application`, runtime `Data` root
  `...\application\Data`, and selected workspace
  `...\application\Data\Workspace`. Result reported ordinary Codex root
  unused, exact model/route configuration present, and selected control
  `OpenRouter / nex-n2.5-pro:free`.
- Live evidence: streamed text was visible after 156871 ms; pressing Stop
  produced visible interrupted status. After renderer reload the partial text
  was not restored, while the interrupted status remained and the original
  prompt was journaled once (`noReplayPromptJournalCount: 1`). The credential
  prefix was absent from text artifacts, page issues were empty, graceful host
  exit code was 0, and no newly owned pinned App Server process remained.
- The two earlier live-cancellation attempts failed in temporary test-harness
  checks before provider contact (a broken CDP readiness poll, then an
  overstrict workspace-display assertion). They were not provider failures.

The live-provider result qualifies only this exact tuple and this stop/reload
journey. It does not make the partial assistant text durable, qualify other
models/routes, establish per-request cost, or close broad crash recovery.

**Checks performed:**

- `node --test ui/diagnostic/tests/transcript.test.mjs`: first failed because
  partial assistant streams had no terminal settlement behavior; after the
  helper/UI change, 3 passed, 0 failed.
- `dotnet build tests/host/NeoBabylon.Phase1A.Tests.csproj -c Release
  --no-restore '-p:OutDir=D:\CODING\NeoBabylon-Data\QA\Phase2-P206-host-tests\bin\'`:
  passed, zero warnings/errors. Full apphost harness passed, including the
  interruption and child-exit partial-delta regressions.
- `node --check tests/qa/native-turn-interruption.mjs`: passed.
- `dotnet build host/NeoBabylon.Host/NeoBabylon.Host.csproj -c Release
  --no-restore '-p:OutDir=D:\CODING\NeoBabylon-Data\QA\Phase2-P206-native-host\'`:
  passed, zero warnings/errors.
- `node tests/qa/native-turn-interruption.mjs <NeoBabylon.Host.exe>
  <playwright-core-entry.mjs> D:\CODING\NeoBabylon-Data\QA\Phase2-P206`:
  passed using the isolated Playwright Core installation and the native
  WebView2 test-only CDP port. A first native attempt loaded stale UI `dist`
  assets (the bundle timestamp preceded the edited `App.tsx`); rebuilding the
  UI bundle refreshed the source projection, after which the same harness
  passed. The Browser plugin was not available; no system-wide dependency was
  installed.
- `npm run build` in `ui/diagnostic`: passed. Existing Astryx/Lucide
  module-level `use client` advisories and the approximately 522 kB chunk
  advisory remain.

This closes deterministic/native cancel-after-delta, protocol child-exit
projection, and the bounded live NEX stop/reload journey. It does not qualify
pinned-runtime crash recovery or durable partial text across reload. Phase 1B
remains deferred/incomplete and full Phase 2 remains open. No commit or push
was made.

## 2026-09-24 — Phase 2 P2-07 native live and saved diff review

The native WPF/WebView2 diagnostic host completed a deterministic patch turn
through the exact runtime recorded in `runtime/lock.json`:

- App Server `0.155.1`, source revision
  `be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
  `636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`.
- Synthetic capability/model identifier
  `p2-07-native-loopback-patch-fixture`; ephemeral loopback endpoint
  `http://127.0.0.1:60503/v1`. This is a deterministic local fixture, not a
  provider/model qualification. Exactly two Responses requests were observed
  for the tool-result/completion round trip; no API key or live inference was
  used.
- The completed tool round trip changed isolated `tracked.txt`. While the
  turn remained active, the UI displayed the App-Server-attributed diff for
  thread `01a0d3d2-413f-7842-a747-4c8f296149eb` and turn
  `01a0d3d2-417f-7620-90e1-8f199bd462f2`. The read-only saved review was
  restored after both a WebView renderer reload and a graceful WPF host
  restart. No approval action was exposed from the review drawer.
- Application root:
  `D:\CODING\NeoBabylon-Data\QA\P2-07-native-live-run-20260924\P2-07-live-native-1790260158163-66996\application`;
  runtime `Data` was inside that application root and outside the source
  repository. Runtime diagnostics reported `ordinaryCodexRootUsed=false`.
  The test capability file was removed after the run; no matching temporary
  `MODEL_CAPABILITY_P2_07_NATIVE_*.json` remains in `docs/release/`.
- Retained evidence: `result.json`, `native-live-diff.png`,
  `native-saved-review-after-renderer-reload.png`, and
  `native-saved-review-after-host-restart.png` in the run directory above.
  The final browser issue list was empty.

`npm test` in `ui/diagnostic` passed 60/60, including live exact-turn diff and
malformed, oversized, invalidated, and saved-diff projection cases. The host
test project built successfully to a fresh sibling QA output directory with
zero warnings/errors; its apphost exited 0, including the pinned-runtime
tracked-patch tests and exact capability-binding-after-restart regression.
`node --check tests/qa/native-live-patch-review.mjs` passed. The isolated
Release WPF host build and UI production build passed; existing Astryx/Lucide
module advisories and the large-chunk advisory remain. No production host or UI
source was changed for P2-07.

The first harness iterations exposed only test-harness issues: reloading the
renderer merely to attach an observer duplicated an initial history request,
and a later assertion expected `idle` although the product correctly reported
`completed`. The final harness now instruments the current page without an
extra reload and checks the observed terminal state. The shared Release build
was also blocked by two pre-existing WPF host processes locking shared output;
neither was stopped. A no-build run using those shared outputs reported one
capability-binding assertion failure, but the fresh isolated host build and
full apphost run passed the same assertion. The shared-output result is
therefore retained as an inconclusive stale-output attempt, not a reproduced
source failure.

Visual inspection found the saved-review drawer displays an absolute file path;
on this long QA path it causes horizontal scrolling. Diff contents and
attribution were intact. Relative-path presentation is a non-blocking UI
follow-up; no permission/approval behavior is inferred from this read-only
review.

P2-07 is complete at this bounded scope. Phase 1B remains deferred/incomplete
and the full Phase 2 gate remains open. No commit or push was made.

## 2026-09-24 — Phase 2 P2-08 native App Server approval qualification

Three fresh native WPF/WebView2 sessions drove actual approval requests from
the exact pinned Codex App Server. The model endpoint was a deterministic
loopback Responses fixture; the selected test-only catalog identifier was
`p2-08-native-loopback-approval-fixture`, with no loaded model or real provider
request. The run endpoint was `http://127.0.0.1:52028/v1`. Each scenario made
exactly two fixture requests: one tool call and one follow-up carrying the App
Server result. No API key or live inference was used.

- Runtime: App Server `0.155.1`, source revision
  `be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
  `636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`.
  The observed binary hash matched `runtime/runtime-lock.json`.
- QA application roots were separate from the source repository, each had
  runtime `Data` below its application root, and all three reported
  `ordinaryCodexRootUsed=false`. Each owned App Server process exited when its
  WPF host closed.
- **Accept:** the WPF card showed the exact request. One advertised one-shot
  accept resolved App Server request `0` for item `accept-exec-call` on thread
  `01a0d41c-8976-7562-a953-1d1a53bcb2f7`, turn
  `01a0d41c-89bd-7af3-ae5a-a577a572cb99`. The server normalized the command to
  `C:\WINDOWS\system32\cmd.exe /c ver`; it returned Windows version
  `10.0.26200.9457` and exit code 0. The UI showed the attributed successful
  result and one-shot approval notice.
- **Deny:** the UI's explicit Deny resolved the exact request for item
  `deny-exec-call`; the command did not run. The denial result was returned in
  the second fixture request and visibly attributed as denied.
- **No response:** after five seconds with no choice, the host sent App Server
  the schema-valid `decline` result, emitted its typed timeout event, and
  resolved the exact `timeout-exec-call` request. The command did not run; the
  resulting denial was returned to the fixture and shown as timed out/denied.
- All three requests advertised `accept`,
  `acceptWithExecpolicyAmendment`, and `cancel`; NeoBabylon exposed only the
  supported one-shot accept/deny choices and did not apply a session grant.
  The exact thread, turn, request, and item correlation was retained in each
  scenario record.
- Result and visual artifacts are in
  `D:\CODING\NeoBabylon-Data\qa\P2-08-run\P2-08-native-approvals-1790265024940-6944`:
  `result.json`, `accept-approval-pending.png`, `accept-result.png`,
  `deny-approval-pending.png`, `deny-result.png`, and `timeout-result.png`.
  The result records the three isolated roots, request counts, runtime hash,
  process ids/exits, decisions, activity, and screenshot paths.

The initial QA profile requested `workspace-write`, but the pinned App Server
reported effective `readOnly` because no supported Windows sandbox backend was
selected; the fail-closed authority check stopped before any provider request.
The QA-only profile was narrowed to explicit `read-only` plus
`on-request` approval, matching the actual effective authority and the pinned
App Server's documented approval path. No sandbox backend was enabled and no
production policy was changed. The UI warns that an accepted shell command can
run with the current Windows account because containment is unqualified; the
only command this harness permits is the harmless `ver` query. This adjustment
is a test-profile choice, not a new product requirement or a claim of Windows
containment.

`dotnet build host/NeoBabylon.Host/NeoBabylon.Host.csproj -c ApprovalQA
--no-restore` passed with zero warnings/errors. Focused approval/policy tests
passed 13/13, including missing decision metadata and unknown permission fields
or request methods failing closed; `npm test` passed 62/62; `npm run build` and
`node --check tests/qa/native-approval-qualification.mjs` passed. Existing
frontend module/chunk advisories remain. The host test harness's P2-08-specific
policy assertions passed, but that broader harness run exited nonzero on an
unrelated `System.IO.IOException` because `logs_2.sqlite` remained in use in the
test cleanup path. A fresh full `ApprovalQA` host-harness rerun then exited 0;
all emitted checks passed, including the P2-08 policy assertions and broader
supervisor/protocol coverage. The prior cleanup exception did not recur, but
its cause is unresolved and remains an intermittent Phase 2/P2-13 verification
risk rather than a diagnosed or fixed defect.

This closes P2-08's bounded native deterministic command-approval journey only.
Live-provider approval generation, other permission/request families, full
authority/approval matrix, Windows containment, and Phase 1B remain open or
deferred; file-change approval remains deny-only because the pinned request
does not include a reviewable diff. No commit or push was made.

## 2026-09-24 — Phase 2 P2-09 file-change approval preview and binding

The combined P2-08/P2-09 native harness completed five fresh WPF/WebView2
sessions against the locked Codex App Server and a deterministic loopback
Responses fixture. P2-08's command accept/deny/timeout scenarios remained
green; P2-09 added file-change accept and deny:

- Exact App Server runtime: version `0.155.1`, source revision
  `be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
  `636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`,
  matching `runtime/runtime-lock.json`.
- Test-only tuple: synthetic provider id `lmstudio`, model identifier
  `p2-08-native-loopback-approval-fixture`, endpoint
  `http://127.0.0.1:52309/v1`. This is a loopback fixture, not an LM Studio
  instance or loaded model. Every scenario made two fixture Responses requests
  (ten total); no key, model weights, external provider request, or live
  inference was used.
- Every scenario had a distinct application root with runtime state under its
  `Data` child, outside the source repository. The native result reports
  `ordinaryCodexRootUsed=false` for all five and that each owned App Server
  exited after its host. Fixture errors and browser/page issues were empty.
- **Accept:** App Server request `0`, item
  `file-change-accept-patch-call`, thread
  `01a0d470-b81b-7df3-9e76-c4b047a62777`, turn
  `01a0d470-b86c-7050-a956-a06caffbee13`; host approval-instance ID
  `65eb8d774a24450b8eabfe0988e2c2eb`; preview fingerprint
  `8a522c419c648f2c08287486aaec3ecdd218de6140148068c1e0c0043f2dfcf7`.
  WPF displayed the exact isolated file path/change/diff; one accept applied
  only the expected fixture patch and the UI showed the attributed result.
- **Deny:** item `file-change-deny-patch-call`, thread
  `01a0d470-f74a-70e1-a660-93d9208b51c2`, turn
  `01a0d470-f7a7-77d3-85db-1c7ee75f6475`; host approval-instance ID
  `154b6deed6db483aa950219f5d39ce44`; preview fingerprint
  `2b06d5ee67591a6412562b6eaa9db43355391a1bb6c526b93829dcd9c8a2962a`.
  WPF displayed the exact preview; denial left the fixture file unchanged.
- The acceptance screenshot included a synthetic HTML-like payload; it was
  rendered as inert text, not active markup. Host protocol tests additionally
  reject stale request/turn/instance identity, fingerprint mismatch, duplicate
  decision, malformed/changed post-start preview data, and any response shape
  beyond the one-shot decision. An identical cumulative post-start snapshot
  is tolerated. Denial remains available without a preview.
- Native result and screenshots:
  `D:\CODING\NeoBabylon-Data\QA\P2-09-run\P2-08-09-native-approvals-1790270502029-17436\result.json`,
  `file-change-accept-approval-pending.png`,
  `file-change-accept-result.png`,
  `file-change-deny-approval-pending.png`, and
  `file-change-deny-result.png` in the same directory.

**Upstream concurrency limitation found by source inspection:** in pinned
`codex-rs/core/src/tools/handlers/apply_patch.rs`, `execute_verified_patch`
projects changes from the verified `apply.action` and passes them into the
approval request. However,
`codex-rs/core/src/tools/runtimes/apply_patch.rs` later calls
`apply_patch_with_options` with `req.action.patch`; pinned
`codex-rs/apply-patch/src/lib.rs` reparses that patch and
`file_update.rs::derive_new_contents_from_chunks` reads the target's current
contents when execution runs. There is no preimage/version comparison tying
that read to the frozen preview. Inference from this call path: an external
edit while approval is pending can make the accepted apply fail or produce a
result different from the preview. This check was source inspection, not a
concurrent-mutation runtime test. P2-09 verifies exact preview-to-request
binding, but does not claim an atomic filesystem transaction. A narrow runtime
stale-file precondition, or an explicit product decision to accept patch rebasing, may
be considered during later acceptance review; no such patch was made here.

The first native harness attempt stopped before a useful file-approval result
because its synthetic catalog marked the fixture's `apply_patch` tool type
Unknown and the pinned server rejected the custom tool call. The fixture-only
catalog was corrected to advertise a known freeform tool with synthetic
evidence; no production capability record, provider, model, runtime, or policy
was altered to manufacture a pass. The final run passed all five scenarios.

**Checks:**

- `npm test` in `ui/diagnostic`: 67 passed, 0 failed.
- `npm run build` in `ui/diagnostic`: passed. Existing Astryx/Lucide
  `use client` advisories and the large-chunk advisory remain.
- `node --check tests/qa/native-approval-qualification.mjs`: passed.
- `dotnet run --project tests/host/NeoBabylon.Phase1A.Tests.csproj
  --configuration ApprovalQA --no-restore`: exited 0, including exact
  preview/fingerprint, stale identity, changed-snapshot invalidation, and
  one-shot response regressions.
- Isolated `ApprovalQA` WPF host build: passed with zero warnings/errors.
- Native harness command `node tests/qa/native-approval-qualification.mjs
  <NeoBabylon.Host.exe> <playwright-core-entry.mjs>
  D:\CODING\NeoBabylon-Data\QA\P2-09-run`: passed. Native result records
  `passed=true`, all five isolated roots, ten fixture requests, and no live
  inference.
- A trailing-space/tab scan of the four edited documentation files: passed.

This closes P2-09's bounded exact-preview, decision-binding, accept/deny, and
invalidation path. It does not qualify concurrent filesystem mutation,
live-provider approval generation, the broad approval matrix, Windows
containment, Phase 1B, or the full Phase 2 gate. No pinned Codex App Server
runtime source or shared installation was changed; NeoBabylon host and UI
source was changed as listed in this slice. No commit or push was made.

## 2026-09-24 — Phase 2 P2-10 deterministic keyboard accessibility

The diagnostic React UI now gives the capability picker listbox keyboard
navigation (Arrow keys, Home/End, Enter/Space, Escape), moves focus to a
resumed task's composer or read-only conversation, exposes named status and
approval regions, and applies visible focus styling including forced-colors
support. Dialog focus handling filters hidden/disabled controls, recovers focus
that escapes the dialog, restores focus to the opener, and handles the named
scrollable content region as an explicit Tab stop. Opener capture now happens
before the background is made inert so the restoration target survives the
dialog lifecycle.

Native WebView2 testing found that its scrollable drawer region participates in
Tab order even where the separately launched Edge run did not. The drawers now
expose that scroll region as a named, focusable region, and both UI and native
tests assert the same Close → content → Done → wrapped Close sequence. The
updated native sequence passed.

**Deterministic browser evidence:** `node tests/qa/ui-accessibility.mjs
<playwright-core-entry.mjs>` passed with Microsoft Edge `153.0.4234.48` across
eight fixture flows. The fake host covered project selection/focus return,
saved-history search/resume, exact provider/model selection, a mocked send and
attributed tool failure, approval denial, review/diagnostics dialog behavior,
and stop-turn activation. It recorded no browser errors and no external
requests. It used no provider credentials or live inference. Screenshots are
`D:\CODING\NeoBabylon-Data\QA\P2-10-ui-accessibility-1790274557251-58608\dark-1440x900.png`
and
`D:\CODING\NeoBabylon-Data\QA\P2-10-ui-accessibility-1790274557251-58608\light-1024x768.png`;
both were visually inspected. This is a visual
readability check, not a computed contrast-ratio audit.

**Native evidence:** `tests/qa/native-history-pagination.mjs` passed against
the pinned App Server and deterministic loopback Responses fixture. It covered
the capability listbox, diagnostics dialog inertness and named scroll stop,
Tab/Shift+Tab wrap, Escape/focus return, loaded-history search, 50+6 pagination,
keyboard load-more, exact older-task resume, and project selection. The result
reports `passed=true`, 56 fixture requests, `noInferenceOnReopen=true`, and no
page issues. The `lmstudio` / `phase1a-qwen3-14b` capability in this run is a
synthetic fixture record; no LM Studio model was loaded or invoked. The pinned
Codex App Server was version `0.155.1`; its observed SHA-256 was
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`.
Evidence and screenshot are under
`D:\CODING\NeoBabylon-Data\QA\P2-10-native-accessibility\P2-P01-native-1790274574152-30072\`;
the isolated application root is outside the source repository.

**Checks:** `npm test` passed 70/70; `npm run build` passed. The existing
Astryx/Lucide `use client` and large-bundle warnings remain. No host runtime or
provider configuration was changed for this slice.

**Acceptance gap:** No Windows screen reader (including Narrator) was operated
or evaluated, so speech, reading order, and announcement quality remain
unverified. Numerical contrast ratios were not measured. P2-10 remains open;
these browser/native keyboard results do not satisfy its screen-reader
acceptance criterion or the combined Phase 2 gate. No commit or push was made.

## 2026-09-24 — Phase 2 P2-11 bounded native large-output, cancellation, and failure

The isolated native WPF/WebView2 harness passed its deterministic large-output
scenarios against pinned Codex App Server `0.155.1`, revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`, matching
`runtime/runtime-lock.json`. The Responses endpoint was a loopback fixture
`http://127.0.0.1:60556/v1`, model identifier
`p2-11-loopback-large-output-fixture`; it used no model weights, credential,
external provider, or live inference. Four fixture requests all named this
exact model and no fallback occurred. This is deterministic App Server/native
evidence, not live-provider qualification.

The isolated application root was
`D:\CODING\NeoBabylon-Data\QA\P2-11\P2-P211-native-1790283016868-62532\application`
with runtime state under its `Data` child. It was distinct from source root
`D:\CODING\NeoBabylon`; the run reports `ordinaryCodexRootUsed=false`.
`pageIssues` and `fixtureErrors` are empty.

- A 1,100,000-character completed assistant response rendered 120,000
  characters with 980,000 explicitly omitted. Saved-output inspection advanced
  five 32,768-character pages and found `P2_11_INSPECT_MIDDLE`.
- A deterministic `exec_command` tool round trip requested 1,100,031 output
  characters. The result was captured and its upstream omission marker was
  carried into the continuation; the output projection reports 1,008,605
  omitted characters.
- Completed history and output inspection reconstructed after WebView reload.
  Cancellation made the partial text visible before reload. The pinned App
  Server does not persist `AgentMessageContentDelta`, so the cancelled partial
  was absent after reopening. The UI now explicitly discloses that unsaved
  streamed response text may be unavailable after reopening; it does not
  silently imply that the partial was saved.
- A non-retryable fixture provider failure retained partial assistant text and
  showed it as failed, not completed. The harness checked exact model identity
  and observed one request for this failure scenario.
- The run sampled each process 16 times. Observed working-set/private-byte
  maxima were 275,525,632 / 247,861,248 for WPF and 301,408,256 / 213,209,088
  for App Server. These are sampled values, not guaranteed process peaks.

Latency observations in milliseconds: completed large response visible 5,733;
fixture provider large stream 226; cancellation interruption visible 1,487;
provider-failure typed state visible 247; cancelled provider request closed
1,446 after cancellation. These are bounded fixture-run timings, not service
SLOs.

Evidence directory:
`D:\CODING\NeoBabylon-Data\QA\P2-11\P2-P211-native-1790283016868-62532\`.
It contains `result.json` and the screenshots `large-output-completed.png`,
`assistant-output-inspected.png`, `large-output-restored.png`,
`partial-output-cancelled.png`, `partial-output-cancellation-restored.png`,
and `partial-output-failed.png`.

Checks for this bounded slice: `npm test` passed 81/81; `npm run typecheck` and
`npm run build` passed (existing Astryx/Lucide `use client` and large-chunk
advisories remain); `node --check tests/qa/native-large-output.mjs` passed;
the isolated Release host harness build passed with zero warnings/errors and
the resulting `NeoBabylon.Phase1A.Tests.exe` exited 0; the native harness
reported `P2-11_NATIVE result=pass`. No live model-size test was attempted in
this run. P2-11's live-model/route qualification, broader memory guarantees,
and arbitrary-size support remain unverified. The App Server cancellation
persistence limitation above is an observed upstream behavior, not a NeoBabylon
claim that the partial text can be recovered. No commit or push was made.

## 2026-09-24 — Phase 2 P2-12 native visual and interaction pass

`tests/qa/native-visual-acceptance.mjs` passed against the exact locked Codex
App Server `0.155.1`, revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`.
This was a native WPF/WebView2 UI-only pass: no provider request or live
inference. The QA application root and `Data` were under
`D:\CODING\NeoBabylon-Data\QA\P2-12-native-visual\P2-12-native-visual-1790284917857-36324\`;
source was `D:\CODING\NeoBabylon`, and `ordinaryCodexRootUsed=false`.

The runner exercised the actual WPF window and WebView at 1440×900 and
960×720 in dark and light modes. It verified dark as the fresh default,
matching charcoal title bar/body, toggle/reload/host-restart preference
persistence, session-only notice dismissal and persistent “Don't show again,”
authority-chip/diagnostics availability, visible keyboard focus, and primary
layout bounds. All four theme/size observations reported no horizontal
overflow; `issues=[]`. Sampled text contrast ratios were 6.76:1–13.71:1 in dark
and 5.16:1–12.32:1 in light. The first light permission-chip sample was
3.44:1; the UI foreground was darkened and the passing rerun measured 5.16:1.
These are representative named-element smoke measurements, not a comprehensive
contrast/WCAG audit. No Windows screen reader was tested; that P2-10 criterion
remains open.

Native screenshots include `dark-desktop-window.png`,
`dark-compact-window.png`, `light-compact-window.png`, and
`light-desktop-window.png`, plus theme-persistence captures, in the run
directory above. Whole-window screenshots were inspected. The browser plugin
was unavailable, so the runner connected to the local WebView via the
available Playwright Core package; the separate browser-only page was not
treated as native evidence.

`npm run build` passed after the contrast correction (existing dependency
`use client` and bundle-size advisories remain). The final native QA runner
reported `passed=true` and no page issues. Early runner attempts exposed a
selector mismatch, a test-induced premature history reload, and native-window
inset calibration; those were corrected in the test harness, not in product
startup/history behavior. The final run passed. P2-12's bounded visual pass is
verified. P2-10's Windows screen-reader/full contrast acceptance, P2-11's
live-model-size behavior, and the P2-13 release-evidence gate remain open; this
does not pass full Phase 2. No runtime/source binary or shared installation
was changed for this slice. No commit or push was made.

## 2026-09-25 — Phase 2 P2-11 bounded live NEX Responses output

Two bounded, direct OpenRouter Responses API requests were made to the exact
user-selected free alias `nex-agi/nex-n2.5-pro:free`, with the exact advertised
Nex AGI `nex-agi/fp8` endpoint selected in request routing, fallbacks disabled,
and no tools. The credential came from the user's existing Pi auth store and
was present only in the isolated test process; it was not printed or saved.
The user-stated free-only restriction could not be independently inspected.
Both requests were preflighted against the model/endpoint catalog; on this
local date the model catalog listed expiration date 2026-09-25, while endpoint
expiration and raw status semantics were Unknown.

The first request was intentionally capped at 4,096 output tokens and ended
`incomplete` with `max_output_tokens`, 19,622 text characters, HTTP 200, and
zero reported cost. It is not counted as a successful complete-output test.
The bounded follow-up used an 8,192-token output cap. Its Responses body
reported `completed`, exact resolved alias, 19,233 text characters (19,251
UTF-8 bytes), 4,056 output tokens, 706 reported reasoning tokens, 4,163 total
tokens, zero reported cost, and `is_byok=false`. It streamed 3,377 text
deltas; first text arrived at 24,724 ms and completion at 140,581 ms. Only the
output length and SHA-256 were retained; generated text was neither logged nor
saved. No tools were offered or called.

The second run's read-only OpenRouter generation lookup returned HTTP 404, so
no independent generation-record attestation was available. In particular,
the endpoint tag remains request-pinned but not post-hoc attested. The
qualification wrapper therefore reported `partial-or-failed`, despite the
Responses body itself being complete. These observations establish that this
exact live alias emitted a bounded streamed response larger than the earlier
4,096-token cap; they do not establish native WPF/WebView2 plus App Server
rendering, provider-side route attestation, memory limits, cancellation,
history reconstruction, or arbitrary-size behavior. P2-11 and P2-13 remain
open, and the Phase 2 gate does not pass.

Evidence: first run
`D:\CODING\NeoBabylon-Data\QA\P2-11-live-nex\P2-11-live-nex-20260925-d07fcdeb7c9c4d26af13b1f2549869fb\result.json`;
bounded completed follow-up
`D:\CODING\NeoBabylon-Data\QA\P2-11-live-nex\P2-11-live-nex-20260925-d79fdb1b8743481299ffd2b3b7902247\result.json`.
The authoritative capability record is
`docs/release/MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json`.

## 2026-09-25 — Phase 2 acceptance regression rerun

The current React UI suite passed **81/81** (`npm test`), `npm run typecheck`
passed, and `npm run build` completed. The production bundle still emits
third-party `use client` and large-chunk advisories; none were build errors.
The updated OpenRouter capability JSON parsed successfully, and
`node --check tests/qa/p2-11-live-nex-output.mjs` passed.

The full isolated host harness
`dotnet run --project tests/host/NeoBabylon.Phase1A.Tests.csproj --configuration ApprovalQA --no-restore`
exited **0**. It rechecked exact runtime binary identity, isolated roots and
single-owner behavior, capability mapping, provider route fail-closed cases,
context compaction, tracked/function patching, interruption/no replay,
project recovery, output bounds, approvals, diagnostics, named bridge, and the
empty-thread-after-restart case. The previously intermittent SQLite cleanup
did not fail in this rerun; no first-failure instrumentation or handle-owner
evidence was produced, so its cause remains Unknown rather than fixed.

This does not replace the recorded broad upstream App Server/provider result
of **1,523 passed / 26 failed**. Twelve missing-helper failures are supported
as test-environment helper absence; five timeout failures remain root-cause
unknown; nine assertion/panic failures remain unclassified because the full
failure context and comparable baseline are unavailable. No conclusion that
those remaining cases are NeoBabylon regressions or unrelated upstream
failures is supported. The upstream checkout remains user-dirty; it was not
modified by this rerun.

A read-only search of the retained `NeoBabylon-Data\QA` artifacts found no
raw output or structured result matching the aggregate, helper names, or
P2-13 marker. The recorded aggregate also has no unique artifact path or exact
command, so per-test failure mapping cannot be reconstructed from the current
evidence. The 12/5/9 classification remains summary-level; the five timeouts
and nine assertion/panic cases are not closed.

P2-10 remains open: deterministic keyboard/focus coverage and prior named
contrast samples exist, but Windows screen-reader behavior and a complete
contrast audit were not run. P2-11 has one direct live bounded response in
addition to deterministic native evidence, but no single native live App
Server/WPF output run. P2-13 and the overall Phase 2 gate remain open; the
application must not claim full acceptance.

## 2026-09-25 — P2-10 deterministic browser accessibility recheck

The existing `tests/qa/ui-accessibility.mjs` fixture passed again with
Microsoft Edge `153.0.4234.48` and the isolated Playwright Core module. All
eight scripted keyboard, project/history/model selection, approval, dialog,
focus-return, and stop-turn flows passed; the runner reported no browser
errors and no external requests. Screenshots and JSON evidence are under
`D:\CODING\NeoBabylon-Data\QA\P2-10-ui-accessibility-1790289904167-41704\`.
This is a browser-only synthetic-host run (`nativeWpf=false`); Narrator and
Windows screen-reader speech were not run, and this is not a complete contrast
audit. P2-10 and the Phase 2 gate remain open.

## 2026-09-25 — P3-01 versioned capability inventory

Completed the evidence inventory for the pinned Codex `rust-v0.155.1` /
App Server `0.155.1` source snapshot (`be2951ea34f0d295ed0becf97079f92fa5f6950e`,
source archive SHA-256
`2DC56C1DB2CC3FB44FC8F132E1CB7064EE3188EE8F811580264BBC672AF0D442`). The
inventory covers built-ins, browser/computer, screenshots and vision, large
documents, search, patch, shell, compaction, skills, hooks, MCP, plugins, and
dynamic tools, separating upstream source presence, current NeoBabylon
exposure, and tuple-specific provider evidence. Every unqualified behavior
remains explicitly Unknown or open.

Cross-check corrections distinguish the experimental
`thread/start.dynamicTools` registration field from the `item/tool/call`
callback schema, and identify the exact `view_image` and thread-attachment
handler paths. NEX image modalities are metadata evidence only; no advertised
tool list or successful image call was verified. No new capability was
enabled, no runtime/UI code was changed, and the pinned archive remains
unbuilt; this closes the inventory/documentation slice only, not P3-02 or the
Phase 3 gate.

## 2026-09-25 — P3-02 exposed-surface audit

Recorded the current bridge/UI execution and failure evidence in
[`P3-02_EXPOSED_SURFACE_EVIDENCE.md`](P3-02_EXPOSED_SURFACE_EVIDENCE.md).
The audit references the locked App Server `0.155.1` identity and separates
callable named operations from rendered App Server events. It confirms that
renderer deadlines do not cancel most host operations, unknown/missing tool
statuses are rendered as success by current UI code, and host `type` /
`attributedTo` fields are dropped when `bridge.ts` creates a plain `Error`.

Existing deterministic evidence is uneven by operation; no new P3-02 fixture
or production behavior was added in this audit. P3-02 remains open. Its
renderer-confidentiality wording also needs interpretation: an absolute ban
on unrelated workspace data in visible model/tool output conflicts with the
accepted unrestricted execution policy. No authority change or narrower
interpretation is adopted here.

## 2026-09-25 — P3-02 conservative activity outcomes and typed host errors

Implemented the narrow corrective UI slice in `ui/diagnostic`: App Server
tool activity is reported as succeeded only for an explicit completed/succeeded
status without an error; explicit failure/error stays failed, and unknown or
missing status stays informational. The WebView bridge now rejects host errors
as `HostOperationError`, retaining `type` and `attributedTo` and including
attribution in the message consumed by the existing UI error surface.

Verification passed:

- `npm test`: **83/83**.
- `npm run typecheck`: passed.
- `npm run build`: passed; existing third-party `use client` bundler
  advisories remain.
- `node tests/qa/ui-accessibility.mjs <Playwright Core 1.63.0 index.mjs>`:
  passed **9** scripted flows in Microsoft Edge `153.0.4234.48`, including an
  unknown future tool status rendered as `info`. The fixture also exercises
  visible tool failure and keyboard/focus flows. Evidence:
  `D:\CODING\NeoBabylon-Data\QA\P2-10-ui-accessibility-1790298759301-72284\accessibility-evidence.json`.

The browser test uses a synthetic WebView host (`nativeWpf=false`), no provider
request, and no external request. A separate deterministic bridge unit test
checks the actual `requestHost` rejection path, retained error fields, and
formatted message. This does not establish native WPF host-failure rendering,
operation cancellation after a renderer timeout, or the complete
denial/timeout/partial-output matrix. P3-02 and Phase 3 remain open. No runtime,
provider configuration, execution policy, or shared Codex settings changed.

## 2026-09-25 — P2-11 fresh native deterministic Release-host rerun

`tests/qa/native-large-output.mjs` passed its bounded native WPF/WebView2
large-output, saved-output paging/reload, cancellation disclosure, and typed
partial-failure scenarios against Codex App Server `0.155.1`, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, binary SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`, matching
`runtime/runtime-lock.json`. This is a fresh isolated Release-host rerun; no
runtime source or shared installation was changed.

The selected capability was provider `lmstudio`, model
`p2-11-loopback-large-output-fixture`, endpoint `http://127.0.0.1:63098/v1`.
It used no model weights, credential, live provider, or live inference. All
four fixture requests used the exact model identifier and no fallback. The
`exec_command` round trip requested 1,100,031 output characters; the upstream
omission marker reached the continuation. The WPF UI rendered 1,100,000
assistant characters, displaying 120,000 and explicitly omitting 980,000.
Five saved-output pages of 32,768 characters reached the middle marker.
History and inspection reconstructed after WebView reload. The UI disclosed
that cancelled streamed text is not persisted by this pinned App Server; a
non-retryable provider failure retained partial assistant text as a typed,
visible failure. No page issues or fixture errors occurred.

The isolated application root was
`D:\CODING\NeoBabylon-Data\QA\P2-11-native-output-release-rerun-20260925\P2-P211-native-1790302010892-70740\application`,
with runtime state beneath `Data`; the source root was
`D:\CODING\NeoBabylon`, and `ordinaryCodexRootUsed=false`. The WPF process
sampled 18 times reached 254,013,440 working-set / 212,791,296 private bytes;
App Server sampled 18 times reached 159,911,936 / 66,699,264 bytes. These are
sample maxima, not process-peak guarantees. Completion became visible at
5,926 ms, fixture streaming took 215 ms, and cancellation closed the provider
request after 1,634 ms.

The harness result is
`D:\CODING\NeoBabylon-Data\QA\P2-11-native-output-release-rerun-20260925\P2-P211-native-1790302010892-70740\result.json`;
the same directory contains the six completion, inspection, restoration,
cancellation, and partial-failure screenshots. The Release host compiled with
zero warnings/errors and the native harness exited 0 with
`P2_11_NATIVE result=pass`. This establishes deterministic native behavior
only; a native App Server/WPF run with the selected live model remains open.

## 2026-09-25 — P2-11 repeat bounded live NEX response using Pi-local credential

The existing `tests/qa/p2-11-live-nex-output.mjs` harness was run once using
the OpenRouter credential already stored by Pi, held only in the test
PowerShell process environment. The key was not written to NeoBabylon,
serialized into the result, or echoed. The synthetic prompt contained no
project source or user data; the harness persists only request metadata,
response lengths, timing, token/cost fields, status, and a response hash.

Preflight matched exactly one model record for
`nex-agi/nex-n2.5-pro:free` and one endpoint for provider `Nex AGI`, route tag
`nex-agi/fp8`, quantization `fp8`, 262,144 context, 235,929 maximum completion
tokens, and zero prompt/completion prices. The user-stated free-only key
restriction was not independently inspectable. The request pinned that route,
set `allow_fallbacks=false`, zero `max_price`, no tools, no automatic retries,
and an 8,192-token output limit. OpenRouter returned the exact accepted alias,
a completed response of 18,704 text characters / 3,877 output tokens, and
reported total cost `$0` (`is_byok=false`). Time to first text was 68,402 ms;
total response time was 258,192 ms.

This is direct OpenRouter Responses evidence only—not App Server/WPF output.
The response had no OpenRouter route metadata, so exact route post-hoc
attestation failed; the generation lookup returned HTTP 404. The harness
therefore recorded `partial-or-failed` despite the completed, zero-cost
response. Endpoint `rawStatus=-5` was observed but its semantics remain
Unknown. No additional repeat request was made in that run. OpenRouter's model page currently states
the free alias is going away on 2026-09-25, so future availability is not
assumed. The route, output, cost, and missing-attestation observations do not
close P2-11. The [current NEX free-model listing](https://openrouter.ai/nex-agi/nex-n2.5-pro:free)
also displayed this scheduled removal when checked on 2026-09-25.

A later bounded Pi-key request completed 18,704 characters / 3,877 output
tokens at provider-reported zero cost. It likewise lacked post-hoc route
metadata and its generation lookup returned 404; the exact result is captured
in `liveLargeOutputRepeat` in the authoritative capability record. No
credential or response text was persisted.

After adding this observation to the authoritative capability record, the
current host test sources built into an isolated QA artifacts directory and
passed `81/81` checks, including exact capability-catalog mapping and JSON
deserialization. Command: `dotnet run --project
tests/host/NeoBabylon.Phase1A.Tests.csproj --configuration Release
--artifacts-path D:\CODING\NeoBabylon-Data\P2-13-CleanBaseline-20260925\host-tests-artifacts`.
The isolated outputs avoided the locked shared Release assemblies used by
already-running NeoBabylon hosts.

Result: `D:\CODING\NeoBabylon-Data\QA\P2-11-live-nex-pi-auth-20260925\result.json`.
No API key or generated response text is present in that artifact.

## 2026-09-25 — P3-05 upstream comparison snapshot / P5-01 pin identity

Created the read-only local comparison
[`UPSTREAM_COMPARISON_2026-09-25.md`](UPSTREAM_COMPARISON_2026-09-25.md).
The NeoBabylon-Runtime Git HEAD equals the locked upstream revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`. The 46-file tracked diff hashes
to `a0a7cd319a31f2125195e7087be72a2867be4779edf00c686ec85f42c58cf520`,
exactly matching `runtime/runtime-lock.json`. Its one untracked
`bin/codex-app-server-x86_64-pc-windows-msvc.exe` is byte-identical to the
captured upstream release binary and was left untouched. No runtime divergence
was added. This verifies local identity and the existing patch fingerprint,
not the patch's behavior or minimality.

This satisfies the one-time report snapshot portion of P3-05 and the narrow
P5-01 pin-preservation check. A deterministic report generator, its fixture
test, a reviewed report schedule/delivery design, and a complete code review
of the 46-file patch are still open. No remote refs were fetched; no lock was
updated and no scheduled task was created.

## 2026-09-25 — P2-10 browser-only accessibility rerun

`tests/qa/ui-accessibility.mjs` passed all nine scripted flows in Microsoft
Edge `153.0.4234.48` at 1440×900 dark and 1024×768 light. Coverage included
keyboard project selection and focus return, saved-history search/resume,
model-picker navigation and exact capability binding, attributed tool
failure, unknown status, approval denial, review/diagnostics dialog focus and
Escape handling, and stop-turn keyboard activation. The run recorded no
console errors or external requests. It used a synthetic host (`nativeWpf=false`)
and no provider request. Evidence, screenshots, and request summary are under
`D:\CODING\NeoBabylon-Data\QA\P2-10-ui-accessibility-1790305900342-66652`.
This rerun does not exercise Narrator and does not measure every rendered
text/placeholder contrast pair; P2-10 remains open.

## 2026-09-25 — P3-04 native large-document proof and P3-05 generator

`tests/qa/native-large-output.mjs` passed through a freshly built Release
WPF/WebView2 host and the locked Codex App Server 0.155.1 (SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`). A
deterministic loopback Responses fixture requested an ordinary `exec_command`
read of a 1,100,000-character isolated document. The saved App Server item
exposed 895,200 characters in 28 contiguous pages of at most 32,768
characters; the start and end markers were retained and the middle marker was
not. No recognized upstream omission marker or exact omitted-source count was
returned. The inspector now says that the absence of a marker does not prove
completeness and that omitted text cannot be recovered or counted. The run
reported zero fixture errors and zero page issues; the result and seven
screenshots are under
`D:\CODING\NeoBabylon-Data\QA\P3-04-native-document-read-20260925\P2-P211-native-1790310358597-23700`.
This is deterministic/mock evidence, not live-model-size qualification.

The same native harness retained P2-11 deterministic checks: 1,100,000
assistant characters, a bounded 120,000-character preview with the exact
980,000-character omission, renderer reload/history reconstruction, late
typed failure, and cancellation disclosure for partial output not persisted
by App Server. It used no live model, provider inference, or credential.

The P3-05 comparison generator is
[`scripts/compare-upstream.ps1`](../../scripts/compare-upstream.ps1). Its
PowerShell fixture ran twice, matched the exact golden report, and verified
that manifest, lock, Git HEAD, and tracked/untracked fixture files were
unchanged; it also rejects impossible explicit report dates. The live
read-only run confirmed runtime HEAD and manifest/lock revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, 46 tracked changed files, one
untracked captured upstream executable, and raw tracked-diff SHA-256
`a0a7cd319a31f2125195e7087be72a2867be4779edf00c686ec85f42c58cf520`, matching
`runtime/runtime-lock.json`. It fetched no refs and wrote no report file or
lock. Report scheduling/delivery remains deferred for review.

## 2026-09-25 — P5-01 source-linked candidate build (not accepted runtime)

A serial offline debug build succeeded from
`D:\CODING\NeoBabylon-Runtime\codex-rs` into isolated target
`D:\CODING\NeoBabylon-Data\P5-01-SourceLinked-20260925\target` using
`cargo build --locked --offline --jobs 1 --package codex-app-server --bin
codex-app-server --target-dir <isolated target>`. The source remained at HEAD
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, with the tracked-diff fingerprint
above, 46 tracked modifications, one pre-existing untracked upstream binary,
and Cargo.lock SHA-256
`ba7f4391706e7f772f6be58913553776a5a7f40ccc319f2810e26fa027884bf1`.
Rust/Cargo were 1.95.0; the project Cargo config supplied an 8 MiB stack and
static CRT. The candidate reports App Server 0.155.1 and has SHA-256
`2e698f4fd01228e26dc84916e56e116ae84826e07fc0dc6b11d9a2c89a9754aa`. Its
depfile references 2,046 paths under `NeoBabylon-Runtime`, none under
`NBRT-RouteControl`, and only two under the isolated `NeoBabylon-Data` target.

This supports the claim that the candidate was freshly built from the current
source checkout; it does not establish behavior or accept the candidate. Its
binary hash differs from the locked executable (`636f2216…9f318`), whose old
depfile points to the separate `NBRT-RouteControl` worktree. The canonical
runtime lock remains unchanged pending current-source behavioral results and
independent review of the mixed runtime patch scope. The full serial App Server
integration suite against the current runtime checkout is in progress; no
suite result is claimed here.

## 2026-09-25 — P2-10 deterministic browser accessibility rerun

`node tests/qa/ui-accessibility.mjs <Playwright Core 1.63.0 index.mjs>` passed
all nine scripted flows in Microsoft Edge `153.0.4234.48`: project selection
and focus return; loaded-history search/resume; model picker keyboard
navigation and exact provider binding; send and attributed tool failure;
informational handling for an unknown tool status; approval denial; review and
diagnostics dialog focus/Tab/Escape behavior; and stop-turn keyboard
activation. It recorded zero console errors and zero external requests. The
run used a deterministic browser fixture (`nativeWpf=false`), not a live App
Server or provider. The Browser plugin was not available; Playwright was the
browser-test path. Evidence and screenshots are under
`D:\CODING\NeoBabylon-Data\QA\P2-10-ui-accessibility-1790315997138-43184`.

On the restored lockfile dependency tree, `npm test` passed `83/83`,
`npm run typecheck` passed, and `npm run build` passed. Logs are under
`D:\CODING\NeoBabylon-Data\QA\P2-10-ui-verification-lockfile-20260925`. This
does not establish a Windows Narrator pass or a complete text/placeholder
contrast audit. P2-10 remains open.

## 2026-09-25 — P2-11 native live NEX response render

The isolated WPF/WebView2 host completed one ordinary no-tools response using
the locked Codex App Server `0.155.1`, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`. The
provider tuple was OpenRouter endpoint `https://openrouter.ai/api/v1`, alias
`nex-agi/nex-n2.5-pro:free`, variant
`nex-agi/nex-n2.5-pro-20260907:free`, configured route `nex-agi/fp8`, and
request fallbacks disabled. The provider-side route attestation remained
`Unknown`; native-request cost and an explicit output-token cap were not
available. No model/tool activity occurred, as requested by the bounded
prompt, and no provider/model fallback was observed.

The UI displayed a completed 18,403-character response (18,413 UTF-8 bytes,
SHA-256
`1e95c1b456aa82d02e558c917d67969e736ec7745e74682c3b397c203ca9bf45`), with
first visible text at 23,383 ms and total elapsed time 310,223 ms. It displayed
provider context 262,144, Codex task context 249,036, advertised reasoning
levels, and effective reasoning `Unknown`. The isolated application `Data`
root was distinct from the source repository, and the ordinary Codex data root
was not used. The host exited with code 0; page issues were empty. Result JSON
and the WPF screenshot are under
`D:\CODING\NeoBabylon-Data\QA\P2-11-native-live-nex-1790313675806-41500`;
the screenshot SHA-256 is
`2045E182EA3D962DF6FC239A2324B7BB90CCC4D0EC246B49C3F1E1474CBC566B`. This
qualifies one bounded native live render only, not arbitrary-size output,
request cost, or independent route attribution.

## 2026-09-25 — P5-01 current-source App Server suite and focused retries

From `D:\CODING\NeoBabylon-Runtime\codex-rs`, the serial command
`cargo test --locked --offline --jobs 1 -p codex-app-server --test all
--target-dir D:\CODING\NeoBabylon-Data\P5-01-SourceLinked-20260925\target --
--test-threads=1` ran with `RUST_MIN_STACK=16777216`. The full log is
`D:\CODING\NeoBabylon-Data\P5-01-SourceLinked-20260925\cargo-app-server-integration-serial-current-runtime-rust-min-stack-16m-20260925.log`.
Result: **1,170 passed, 25 failed, 11 ignored**. The source-linked candidate
App Server binary in that target currently hashes to
`E4DD43F3EEA024CB8C6CA1401386B20CA02CFFB4D749FA6DC9BBB8CE1B704CA2`; this is
not the locked executable and is not accepted.

The 25 failures classify as nine code-mode-helper cases (the
`codex-code-mode-host.exe` helper was unavailable), nine stdio-helper cases
(the initial run lacked `test_stdio_server.exe`), five shell/environment
cases, and two Guardian hook assertions. Focused retries ran 19 affected shell
and stdio cases against that isolated target. Fourteen stdio-helper cases
passed after building `test_stdio_server.exe` (SHA-256
`2BBBBD027846052F984A1F4AF5226909C54B8B6CFFFF019B878C054473D80A1F`). Four
of five shell/environment cases passed with a test-only `-NoProfile`
PowerShell shim and process-local Python alias. No global PATH, PowerShell
profile, or Codex configuration was changed. The remaining shell failure is
`thread_shell_command_honors_optional_timeout`: its marker arrived in
`item/commandExecution/outputDelta`, but after timeout the terminal failed
command item had no marker and the test assertion failed. Inspection traced
this explicit `thread/shellCommand` path through `core/src/tasks/user_shell.rs`:
the execution error branch constructs an empty stdout/aggregate record. That
endpoint is documented as a human-invoked local-host escape hatch and remains
prohibited for NeoBabylon model-controlled tools; no runtime fix was made from
this test alone. The nine code-mode failures occurred because the V8
`150.4.0` sandbox prebuilt archive returned HTTP 404; the locally cached crate
lacks `v8/third_party/disarm/src/parse.py` and ICU `icudtl.dat`, and GN,
depot_tools, and Clang are absent. The official
[Rusty V8 build documentation](https://github.com/denoland/rusty_v8) describes
the experimental sandbox as having no published prebuilt; issues
[#2070](https://github.com/denoland/rusty_v8/issues/2070) and
[#2035](https://github.com/denoland/rusty_v8/issues/2035) document source-input
and Windows build constraints. These are environment-blocked tests, not
passed cases. The focused logs and isolated shim are under
`D:\CODING\NeoBabylon-Data\QA\P5-01-focused-followups-20260925` and
`D:\CODING\NeoBabylon-Data\QA\P5-01-SourceLinked-20260925`.

The other two failures are the Guardian assertions
`blocking_hook_cannot_erase_answer` and `hook_feedback_cannot_hide_answer`.
A focused clean-HEAD worktree at
`D:\CODING\NeoBabylon-Data\QA\P5-01-clean-baseline-20260925\codex-rs`
ran three Guardian tests: one passed and the same two hook assertions failed,
with `the configured hook must replace or reject the visible tool output` at
`app-server/tests/suite/v2/guardian_v2.rs:1260:17`. Its HEAD was
`be2951ea34f0d295ed0becf97079f92fa5f6950e`. The committed Cargo lock labels
local workspace packages `0.0.0`, so `--locked --offline` could not resolve
that clean checkout; the focused offline run normalized local workspace
package versions to `0.155.1`. The resulting Cargo.lock diff was 304 lines,
all local workspace-version substitutions, with no external dependency
version/checksum change. This is a focused clean-HEAD comparison, not a full
baseline run; it shows those two Guardian failures also occur at the pinned
source revision. The canonical runtime lock is unchanged, but complete
source-to-binary provenance and independent review of the mixed runtime patch
remain open. The full suite's failure categories are accounted for above; this
does not qualify or accept the candidate runtime.

## 2026-09-25 — P2-10 rendered-text contrast and interaction audit

The latest run of `tests/qa/ui-accessibility.mjs` passed all nine scripted
keyboard/interaction flows in Microsoft Edge `153.0.4234.48` and measured
rendered text and placeholders in eight states: dark home, model picker,
failure/approval, review dialog, diagnostics dialog, and light model picker,
home, and active turn. The audit covered 714 visible-text runs: zero
active-text contrast failures and zero unknown backgrounds; it recorded 16
below-AA occurrences grouped into 14 unique disabled/`aria-disabled` control
findings. The runner recorded zero browser-console errors and zero external
requests. Evidence and screenshots are under
`D:\CODING\NeoBabylon-Data\QA\P2-10-ui-accessibility-1790318388435-69940\`.
The nine-flow suite ran in synthetic Edge only, not native WPF. Separately,
the P2-12 native visual run checked visible focus after Tab but did not run
these nine flows. This is not screen-reader evidence; Windows Narrator was not
exercised.

After the contrast CSS changes, the diagnostic UI's `npm test` passed 83/83,
`npm run typecheck` passed, and `npm run build` passed. The production build
still reports the existing ignored `use client` directive and large-chunk
warnings; they did not fail the build.

## 2026-09-25 — P2-12 fresh native visual rerun

`tests/qa/native-visual-acceptance.mjs` passed against the exact locked Codex
App Server `0.155.1`, revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`.
Three WPF/WebView2 launches verified dark and light appearances at 1440×900
and 960×720, default-dark behavior, theme persistence through renderer reload
and host restart, matching native title-bar colors, visible keyboard focus,
dismissible/suppressible notice, and in-viewport primary controls. The result
contains zero issues. Application root and `Data` were isolated beneath
`D:\CODING\NeoBabylon-Data\QA\P2-10-native-contrast-20260925\P2-12-native-visual-1790318060968-27424\application`; source root remained
`D:\CODING\NeoBabylon`, ordinary Codex root use was false, and no provider
inference occurred. Whole-window and WebView screenshots plus `result.json`
are in that run directory. This validates the bounded visual slice only; it
does not close P2-10 screen-reader coverage or the Phase 2 gate.

## 2026-09-25 — P3-02 ordinary-tool partial failure and attribution

The isolated .NET host-test executable used the exact product-lock-verified
Codex App Server `0.155.1` (source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, binary SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`) and a
loopback Responses fixture. One ordinary `exec_command` ran a test-owned
`.cmd` file that emitted `NEOBABYLON_P3_02_PARTIAL_FAILURE_MARKER` and exited
23. The Windows default outer shell is PowerShell, so the command explicitly
propagated `$LASTEXITCODE`. The terminal `commandExecution` item retained the
marker and exact exit code; host diagnostics retained typed `toolExecution`
failure attribution and the matching thread, turn, and item IDs. The provider
fixture received exactly two requests, including the failed tool result; no
retry or provider/model fallback occurred. The full host-test executable
exited 0 with 82 passing checks. Build and test logs are under
`D:\CODING\NeoBabylon-Data\QA\P3-02-command-failure-host-build-20260925\`.

This was deterministic loopback/test-host evidence: no live provider request,
no production runtime or policy change, and no rendered WPF assertion. It
closes only this one partial-failure path. P3-02's wider denial,
timeout/cancellation, confidentiality, and per-operation matrix remains open.

## 2026-09-25 — P3-02 native command-failure rendering

A fresh Release WPF/WebView2 host exercised the exact product-lock-verified
Codex App Server `0.155.1` (source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, binary SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`). A
deterministic loopback Responses fixture used synthetic selected model
`p3-02-native-command-failure-fixture`; no live provider/model or API key was
used. An ordinary permitted command ran a test-owned `.cmd` under the isolated
application `Data\Workspace`, emitted
`NEOBABYLON_P3_02_NATIVE_FAILURE_MARKER`, and exited 23.

The native bridge captured the terminal App Server item and final `startTurn`
tool diagnostic with the same item ID, exact code 23, failed outcome, and
`Codex App Server` attribution. The rendered card showed `FAILED`, `Exit code
23`, and the retained partial output; its accessible name included the code.
The provider fixture received exactly two requests (one tool call and one
continuation), with no retry/fallback. The isolated command also emitted the
test-only value `sk-nb-synthetic-canary-not-a-credential`; the loopback
continuation received it as ordinary tool output and the native activity card
displayed it. No real credential or user data was used, and the host did not
inject a credential. The run verified distinct source and
application roots, isolated `<application root>\Data`, ordinary Codex root
use false, and zero page/console issues.

Fresh verification: UI tests **88/88**; production UI build passed with
existing Astryx/Lucide `use client` and bundle-size advisories; isolated
Release WPF build **0 warnings/errors**; native interaction passed. Result and
screenshot:
`D:\CODING\NeoBabylon-Data\QA\P3-03-tool-capabilities-20260925\P3-02-native-command-failure-1790346625113-13224\`.
This closes one visible ordinary-command failure path only. P3-02's broader
denial, timeout/cancellation, partial-output, and confidentiality matrix
remains open; no production policy/runtime or provider configuration changed.

## 2026-09-25 — P2-10 disabled-state contrast regression

The Edge contrast harness was tightened so below-threshold text on disabled or
`aria-disabled` controls and indeterminate text backgrounds fail the run,
rather than being informational exceptions. The first red run reproduced 16
below-AA occurrences across 14 unique findings. They came from opacity-based
fading on disabled model options, project rows, and the focusable
`aria-disabled` New task button. The CSS now retains disabled affordance using
neutral surfaces and not-allowed cursors without fading text; the selected
model label uses a darker light-theme accent. No interaction, provider, or
runtime behavior was changed.

The passing rerun of `tests/qa/ui-accessibility.mjs` used Microsoft Edge
`153.0.4234.48` and passed all nine scripted flows across eight audits: 713
visible text/placeholder observations, zero active contrast failures, zero
inactive-control contrast exceptions, zero indeterminate backgrounds, zero
browser-console errors, and zero external requests. The audit states covered
dark home/model-picker/failure-and-approval/review/diagnostics and light
model-picker/home/active-turn. Evidence and screenshots are under
`D:\CODING\NeoBabylon-Data\QA\P2-10-ui-accessibility-1790322726820-9644\`;
the temporary Playwright Core `1.63.0` installation used for the run is under
the sibling QA root at `D:\CODING\NeoBabylon-Data\QA\p2-10-playwright-1.63.0\`.

The diagnostic UI suite passed 83/83, `npm run typecheck` passed, and
`npm run build` passed. The build still emitted the existing upstream
`use client` directive and large-chunk advisories. This remains synthetic Edge
evidence (`nativeWpf=false`), with no provider request; Windows Narrator and a
native WPF rerun of all nine flows were not performed. P2-10 and the full
Phase 2 gate remain open.

## 2026-09-25 — P2-01 native transcript-overflow disclosure

The ApprovalQA fixture runner seeded 56 saved tasks through the exact locked
Codex App Server `0.155.1` (source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`) and a
loopback Responses fixture. The WPF/WebView2 UI was launched from a separately
built Release host so the QA-only fresh-thread restriction did not disable the
saved-thread resume path. The fixture used the selected test record
`lmstudio / phase1a-qwen3-14b`; no real model or live inference was used.

The newest assistant fixture contained 120,576 characters. The live event
projection retained the bounded 120,000-character preview and recorded exactly
576 omitted characters with `sourceRetained=true`. The native UI then reopened
the exact saved task after the host restart. Saved-history projection showed
119,983 assistant characters (the combined transcript budget also includes the
user prompt), visibly displayed “Some earlier or longer messages are omitted
from this preview. Full history remains in Codex App Server,” and retained the
selected task identity. Pagination loaded 50 + 6 tasks newest-first; older-page
search disclosed its loaded-page scope; the isolated fixture request count
remained exactly 56 through the native reopen; and the browser recorded no
page issues. Result JSON and screenshot are under
`D:\CODING\NeoBabylon-Data\QA\p2-01-history-overflow-20260925\P2-P01-native-1790324644555-38088\`.

The first harness attempt used the `ApprovalQA` WPF host, which correctly
rejects saved-task resume by design; diagnostic output identified that mode.
The final passing run used the Release host built to an isolated QA output
directory because two long-running Release hosts held the normal output files.
Those user processes were inspected read-only and left running. This closes
the bounded transcript-overflow disclosure path, not broad history scale,
concurrency, or server-backed full-history search.

## 2026-09-25 — P2-13 host-harness and App Server process identity recheck

After two earlier `AssertNoNewPinnedAppServer` failures reported PIDs without
capturing who launched them, read-only source review found that the assertion
compared a machine-wide PID snapshot with every later process using the pinned
image. It could include another concurrent NeoBabylon instance and could not
attribute either reported PID; the failures are therefore unresolved, not
proven false positives or product leaks. The ApprovalQA-only test hook now
captures the exact supervisor-owned child PID plus OS start-time before
disposal and asserts that same identity is gone afterward. The unrelated
machine-wide scan was removed from pass/fail.

`dotnet build tests/host/NeoBabylon.Phase1A.Tests.csproj --configuration
ApprovalQA --no-restore` passed with zero warnings/errors. The focused
`--probe-repeated-interruption-recovery` run passed both attempts: each ended
as a typed App Server interruption, used one loopback fixture request, resumed
without replay, and proved the same process identity exited. A full fresh
ApprovalQA host-harness run passed **82/82** checks with exit code 0. The
previously recorded `logs_2.sqlite` cleanup exception did not recur, but no
deterministic reproduction or root cause was established. The complete broad
App Server source-suite classification and source-to-binary provenance gap
remain as recorded above. This does not pass P2-13 or the overall Phase 2 gate.

## 2026-09-25 — P2-11 native completion-cap and route-attestation audit

Source inspection of the versioned Codex App Server `0.155.1` found no
`max_output_tokens` member in `reference/cache/codex/rust-v0.155.1/source/codex-rs/codex-api/src/common.rs:260-285` (`ResponsesApiRequest`), and its
WebSocket conversion at `common.rs:287-308` only copies members from that
request. The builder in `codex-rs/core/src/client.rs:871-888` does not set a
completion cap. The NeoBabylon capability record advertises
`maxCompletionTokens=235929` from model/endpoint metadata, but the host
catalog/config path does not turn that fact into a native generation limit;
the catalog's `truncation_policy` and runtime
`tool_output_token_limit` concern tool-result handling instead. This is
source-level evidence, not a captured byte-for-byte native request body.

The direct OpenRouter Responses test harness separately sends a
`max_output_tokens` request field, and its recorded 4,096-token run ended at
that limit. That direct API result is not native App-Server/WPF evidence. No
live request was sent for this source audit. OpenRouter's [generation metadata
API](https://openrouter.ai/docs/api/api-reference/generations/get-request-&-usage-metadata-for-a-generation)
documents provider/model and usage fields when a lookup is available. The
recorded NEX generation lookups returned 404, and no available result attests
the exact `nex-agi/fp8` endpoint; route remains request-side evidence only.

**Implementation proposal, not accepted:** add an explicit per-capability
completion ceiling distinct from provider-advertised maximums, then serialize
it in each supported native request transport or formally constrain transport
selection. The exact cap/default, enforcement behavior, cost ceiling, and
required route-attestation strength remain unresolved. P2-11 is open.

## 2026-09-25 — P2-13 recovery-harness identity and cleanup recheck

The saved-task recovery test previously found and killed App Server children
by comparing a machine-wide process list against a PID snapshot. It now uses
the QA-only PID/start-time identity exposed by the specific `RuntimeSupervisor`
under test, verifies the locked binary before deliberate termination, and
asserts that the final supervisor-owned process exited. This removes a
concurrent-process attribution hazard from the test; it changes no production
runtime behavior.

The test's GUID-scoped application root now uses the existing bounded cleanup
retry after clearing read-only attributes. That helper validates the exact
temporary parent. The earlier `goals_1.sqlite` lock owner could not be
identified after the failed run; the latest valid rerun did not reproduce the
exception, so cleanup is mitigated but the lock's initiating process/source is
not proven.

`dotnet build tests/host/NeoBabylon.Phase1A.Tests.csproj --configuration
ApprovalQA --no-restore` passed with zero warnings/errors. The focused
`--probe-exited-supervisor-client-recovery` passed four runs across the
identity/cleanup changes (three before the cleanup-helper change and one after
the rebuilt change). The complete ApprovalQA suite, launched with the generated
test apphost, passed **82/82** checks with exit code 0. It produced no SQLite
cleanup error. The log is
`D:\CODING\NeoBabylon-Data\QA\P2-13-host-harness-post-retry-20260925\approvalqa-host-harness.log`.

An intermediate attempted run invoked the framework-dependent test DLL as
`dotnet <dll>`. Ten fake-server protocol checks then parsed the .NET host's
“Possible reasons…” text as JSON because `Environment.ProcessPath` resolved to
`dotnet.exe`; that 72/82 run is excluded as an invalid test invocation, not a
product result. `tests/README.md` now gives the supported project/apphost
invocation and documents the focused recovery probe. The broader pinned-source
suite classification and source-to-binary provenance gaps remain open, as do
P2-10, P2-11, and full Phase 2 acceptance.

## 2026-09-25 — P2-10 fresh contrast audit and native keyboard/history subset

The deterministic Edge accessibility runner completed all nine scripted
keyboard/focus flows on browser `153.0.4234.48`. Its eight rendered-state
contrast audits counted 714 text/placeholder observations, with zero below-AA
runs, zero indeterminate backgrounds, no console errors, and no external
requests. The evidence and dark/light screenshots are under
`D:\CODING\NeoBabylon-Data\QA\P2-10-ui-accessibility-1790327765182-73452\`.
This was a synthetic host; `nativeWpf` is false and Windows Narrator was not
exercised.

The accessibility runner was repeated after the native subset record was
prepared. The repeat exited 0 with the same browser version, nine flows, eight
audits, 714 observations, zero below-AA runs, zero indeterminate backgrounds,
and no console errors or external requests. Latest repeat evidence is under
`D:\CODING\NeoBabylon-Data\QA\P2-10-ui-accessibility-1790328762772-51444\`;
it remains synthetic-only and does not change the open native/screen-reader
qualification.

A separately freshly built Release WPF host passed the bounded native keyboard
and history run: model-picker focus/listbox, diagnostics dialog inertness and
Tab/Shift+Tab/Escape/focus restoration, loaded-history search and pagination,
exact older-task reopen, transcript-overflow disclosure, and project switching.
The run used 56 synthetic loopback LM Studio fixture turns, retained exactly
56 provider requests after reopening the oldest saved task, and recorded no
browser page issues. The 120,576-character saved assistant response reopened
with a bounded preview and explicit omission notice. The isolated application
root and `Data` were outside source root `D:\CODING\NeoBabylon`; no live
inference occurred. The evidence manifest records runtime SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700d55d72e2709f318` and is at
`D:\CODING\NeoBabylon-Data\QA\P2-10-native-keyboard-20260925\fresh-run\P2-P01-native-1790328085694-46796\`.

The first native attempt used an old pre-existing Release executable whose
timestamp preceded the current host and UI sources. It failed saved-task reopen
and is excluded as stale-build evidence, not treated as a product regression.
Building to a unique QA output directory and passing that host explicitly
produced the valid run above. This native subset does not equal the nine-flow
synthetic suite, does not qualify Windows Narrator, and does not close P2-10 or
the overall Phase 2 gate. The earlier audit's 713 observations remain
historical; the fresh audit counted 714.

## 2026-09-25 — P2-13 runtime worktree equivalence and rebuild comparison

The locked App Server depfile names `D:\CODING\NBRT-RouteControl\codex-rs`,
while `runtime/runtime-lock.json` names the sibling
`D:\CODING\NeoBabylon-Runtime`. Both repositories are at HEAD
`be2951ea34f0d295ed0becf97079f92fa5f6950e` and each has 46 modified tracked
paths. A read-only per-file comparison found 28 raw text-file differences but
zero content differences after normalizing CRLF/CR to LF. Independently,
`git diff --binary HEAD` from `NBRT-RouteControl` hashed to
`a0a7cd319a31f2125195e7087be72a2867be4779edf00c686ec85f42c58cf520`, exactly
the lock's `sourcePatchSha256`. This corrects the earlier implication that 28
paths contained substantive code differences; the observed differences are
line endings.

To test actual rebuild reproducibility without touching either source checkout
or the locked executable, the depfile-named workspace was built with
`cargo build --locked --offline --jobs 1 --package codex-app-server --bin
codex-app-server --target-dir D:\CODING\NeoBabylon-Data\QA\P2-13-routecontrol-repro-20260925\target`.
Cargo/Rust were `1.95.0`; the build completed in 24m12s and the output reported
`codex-app-server 0.155.1`. Its SHA-256 was
`8e5acfaf7f4e59dc0f679009f40235ff5eac45502034d5c62342fc9f1b4bfd38`, not the
locked binary's
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`. Both files
were 258,022,400 bytes. PE `.pdata` and `.fptable` sections matched; `.text`,
`.rdata`, `.data`, and `.reloc` differed. The cause is not established. The
fresh build and comparison output live under
`D:\CODING\NeoBabylon-Data\QA\P2-13-routecontrol-repro-20260925\`.

The current tracked-patch identity and normalized source equivalence strengthen
the link between the canonical and depfile-named source trees; current modified
RouteControl source timestamps also do not postdate the locked executable. Neither proves
that the bytes consumed by the historical build exactly equal today's source,
and the isolated executable hash mismatch remains unexplained. Keep the binary
locked as-is and leave P2-13/P5-01 provenance/reproducibility unaccepted until
the original build inputs/configuration are recovered or a reproducible build
is demonstrated. No runtime source, lock, or executable was changed.

**P2-13 Cargo fingerprint follow-up — 2026-09-25:** read-only comparison of
the old locked build target
`D:\CODING\NeoBabylon-Data\Phase2-FunctionPatch-Target\debug` and the
isolated rebuild target
`D:\CODING\NeoBabylon-Data\QA\P2-13-routecontrol-repro-20260925\target\debug`
found byte-identical `bin-codex-app-server`, its JSON fingerprint record, and
`dep-bin-codex-app-server` files under Cargo's
`.fingerprint\codex-app-server-c34a3a99ebf35fac` directory. The binary
fingerprint string is `0a2a99a77925ff01` in both. This confirms Cargo's
recorded fingerprint metadata agrees for this target; it is not a cryptographic
hash of every historical input and does not attest to the exact contents
consumed by the old build. Despite the matching record, the executable SHA-256
values differ (`636f2216...f318` locked; `8e5acfaf...bfd38` rebuild), and the
PE `.text`, `.rdata`, `.data`, and `.reloc` sections differ. The cause remains
unknown. This narrows the mismatch but does not establish byte-for-byte
reproducibility, explain the old artifact, authorize a runtime-lock update, or
close P2-13/P5-01. No source, lock, or executable was changed.

A targeted read-only artifact comparison then found the mismatch already
present in Rust link-input archives, not only in the final executable: the
`codex_app_server_protocol`, `codex_app_server_transport`, and
`codex_app_server` `.rlib` files have different SHA-256 values and lengths in
the two targets (each isolated-rebuild archive is 4,096 bytes larger). The
`codex_app_server_protocol_noop_macros` DLL is the same length but has a
different hash; its PDB and the App Server PDB also differ. This demonstrates
that a final-linker-only explanation is insufficient. The `.rlib` archive
difference is now directly explained by build-output paths embedded in their
CodeView debug metadata, as detailed below; the separate DLL/PDB and final PE
differences are not yet explained.

**Further artifact narrowing:** for each of those three `.rlib` files, all 256
Rust codegen-unit object members matched after normalizing the differing
seven-character object-name suffix. In every object, the sole changed
`.debug$S` subsection grew by exactly 16 bytes; the only changed printable
string was that object's full output path under the old target versus the
isolated QA target. Across all 256 objects per archive, non-debug section
payloads and relocation/line tables were byte-identical; `.debug$T` was also
unchanged. The three archive-size increases are exactly 256 × 16 bytes. This
directly explains the `.rlib` hash/size mismatch as target-path-sensitive
CodeView output, not different code/data section bytes in these three
App-Server-owned crates. Their COFF symbol-table records and archive object
name suffixes also differ. This does not explain the final PE's `.text` and
other section differences, the DLL/PDB differences, or the full transitive
link closure. P2-13/P5-01 remain open; do not treat the old executable as
reproduced or update the runtime lock from this evidence.

## 2026-09-25 — P2-13 corrected remapped-build content comparison

The first remap experiment used process-local `RUSTFLAGS` containing only
`--remap-path-prefix`. Cargo gives the environment variable precedence over
matching target flags in `.cargo/config.toml` ([Cargo configuration reference](https://doc.rust-lang.org/cargo/reference/config.html#buildrustflags)); this accidentally omitted the checkout's
`-C link-arg=/STACK:8388608` and `-C target-feature=+crt-static` flags. That
invalid candidate (`D:\CODING\NeoBabylon-Data\QA\P2-13-remap-repro-20260925\target\debug\codex-app-server.exe`, SHA-256
`851dd6dc8f5e637b1cd7865e0ee166f9fa3544df5ab1a427d71ab6a99b687781`,
257,751,040 bytes) had a different image layout and is excluded from the
controlled comparison.

A second isolated offline build from `D:\CODING\NBRT-RouteControl\codex-rs`
used Rust/Cargo `1.95.0`, the same package/locked/offline/jobs settings as the
normal rebuild, the original Windows flags above, and an additional
`--remap-path-prefix` from its fresh QA target root to
`D:\CODING\NeoBabylon-Data\Phase2-FunctionPatch-Target`. It completed in
23m22s at
`D:\CODING\NeoBabylon-Data\QA\P2-13-remap-configured-repro-20260925\target\debug\codex-app-server.exe`.
The file is 258,022,400 bytes with SHA-256
`0d4208b050bde60e485b1383b8f1eb52aab0cd2bc1c58b111818e8c8b320e205`; the
locked image remains SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`.

PE comparison found `.text`, `.data`, `.pdata`, `.fptable`, and `.reloc` to be
byte-identical. `.rdata` has exactly 123 differing bytes: seven 16-character
generated `codex-code-mode-protocol` `OUT_DIR` fingerprint strings (the new
Cargo fingerprint changes the generated directory suffix), three
`IMAGE_DEBUG_DIRECTORY.TimeDateStamp` values (types 2, 12, and 13), and the
16-byte CodeView RSDS PDB GUID. The PE COFF timestamp also differs. The PDB
basename and generated source path otherwise match; no QA target path remains
embedded after remapping.

A bytewise whole-image comparison normalized only the PE COFF timestamp, those
three debug timestamps, the RSDS GUID, and the seven known generated-directory
hash strings. It found zero remaining byte differences; both normalized full
image SHA-256 values are
`69044cc43babe5b8777299c0cbd2a1a7af9aebf8ddbac87e22c5fc0fe3d9cab8`.
This is strong current source-to-binary content parity after explicit
build/debug-metadata normalization, not raw-hash identity or proof of every
historical input. It provides no evidence requiring a runtime patch; the
runtime lock and accepted executable remain unchanged. P2-13 and P5-01 are not
marked passed solely by this comparison; full Phase 2 still has independent
screen-reader, output/cost/route, broad-suite, and other slice gates open.

## 2026-09-25 — P2-10 screen-reader preflight and regression recheck

A read-only machine preflight confirmed Windows build `10.0.26200`, Narrator
`10.0.26100.8972` at `C:\WINDOWS\system32\Narrator.exe`,
`UIAutomationCore.dll` `7.2.26100.1`, and Windows SDK `Inspect.exe` `7.2.0.0`
at `C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\inspect.exe`.
No screen reader was launched and no accessibility settings were changed.
These prerequisites permit a native UI Automation inspection, but neither UIA
properties nor the existing Edge/WebView2 automation establish that Narrator
speaks the correct content, order, and timing. P2-10 remains open for an
actual listening/interaction pass; the minimum useful pilot is model-list
navigation, a changing status/failure with approval, and opening/closing a
dialog with focus returned to its trigger. Record reader version, Windows
build, resolution, observations, and fixes. A pilot alone does not satisfy
the full task-flow acceptance.

Fresh non-live regression checks on the same date passed: `npm test` reported
83/83; `npm run typecheck` exited 0; the UI production build exited 0 to
`D:\CODING\NeoBabylon-Data\QA\P3-03-ui-build-20260925\dist`; and
`dotnet run --project tests/host/NeoBabylon.Phase1A.Tests.csproj --configuration
ApprovalQA --no-restore` exited 0 with all emitted host checks passing. The
Vite build retained existing third-party `use client` directive warnings and
the >500 kB entry-chunk warning. The host suite used deterministic fixtures;
no live provider or model inference occurred. These checks do not close the
P2-10, P2-11, P2-13, Phase 3, or combined Phase 2 acceptance gates.

## 2026-09-25 — P2-10 native composer keyboard journey

A fresh Release WPF/WebView2 host exercised the P2-06 interruption flow using
keyboard activation throughout: Enter submitted the original prompt, Enter on
the focused Stop button interrupted the streamed partial response, and Enter
submitted one deliberate continuation after renderer reload. The interrupted
status remained visibly attributed to Codex App Server; the partial output was
not restored and no request was replayed. Exactly two Responses requests
reached the deterministic loopback fixture: the initial turn and explicit
continuation. There was no real model, provider traffic, API key, or live
inference.

- Runtime identity matched `runtime/lock.json`: App Server `0.155.1`, source
  revision `be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
  `636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`.
- The synthetic capability identified provider `lmstudio`, test model
  `p2-06-loopback-interruption-model`, and an ephemeral `127.0.0.1` endpoint.
  Runtime diagnostics reported the ordinary Codex root unused and placed
  application `Data` under the isolated QA application root, outside the
  source repository. The synthetic capability file was removed after the
  test; the QA result and four screenshots were retained.
- `node --check tests/qa/native-turn-interruption.mjs` passed;
  `npm run build` in `ui/diagnostic` passed with existing Astryx/Lucide
  `use client` advisories and the >500 kB entry-chunk advisory; the isolated
  WPF host `dotnet build ... --no-restore` passed with zero warnings/errors.
  The native harness then passed (`result=pass`, two provider requests,
  interrupted status restored, no replay, explicit continuation, App Server
  exited). Evidence is under
  `D:\CODING\NeoBabylon-Data\QA\P2-10-native-keyboard-controls-20260925\P2-P206-native-1790340779292-7044\`;
  `result.json` records all three Enter-activated actions.
- The first two harness attempts reached the interrupted state but failed on
  an obsolete exact-text assertion for the shorter status copy. Runtime
  evidence showed Enter had activated Stop and the UI reached `interrupted`;
  the current UI also includes a longer explanatory suffix. The harness was
  corrected to check the stable App-Server-attributed no-replay prefix, then
  the complete flow passed. No product/runtime code changed for this text
  mismatch.

This qualifies only the bounded keyboard composer journey. P2-10's full native
flows and actual Windows Narrator/screen-reader interaction remain open; the
pass does not close Phase 2. Installation and packaging remain deferred by
NB-DEC-010 and were not attempted.

## 2026-09-25 — P3-03 read-only Tools & capabilities catalog

The Runtime diagnostics drawer now consumes the versioned
[`NEOBABYLON_TOOL_CAPABILITY_CATALOG.json`](NEOBABYLON_TOOL_CAPABILITY_CATALOG.json)
for product exposure and provenance, plus the exact selected tuple's
`toolQualifications` from its authoritative model capability record. It shows
13 searchable categories and keeps product exposure separate from tuple
qualification. Generic `tool_use` metadata cannot promote an individual tool;
Unknown, unsupported, advertised-only, and explicitly blocked evidence remain
non-callable. The Core model record preserves this typed evidence through the
existing diagnostics bridge. No bridge operation, generic RPC, or Codex App
Server/runtime patch was added.

Fresh checks on the final catalog typography and harness:

- `npm test` in `ui/diagnostic`: **88 passed, 0 failed**.
- `npm run build` in `ui/diagnostic`: exited 0. Existing Astryx/Lucide
  module-level `use client` advisories and the >500 kB bundle advisory remain.
- `dotnet run --project tests/host/NeoBabylon.Phase1A.Tests.csproj
  --configuration ApprovalQA --no-restore`: exited 0, **83/83 checks passed**;
  this includes serialization of the operation qualification records.
- Isolated Release WPF host build: exited 0, zero warnings/errors.
- `node --check tests/qa/native-tool-capability-catalog.mjs`: exited 0.
- Native WPF/WebView2 QA passed graceful host close/reopen using one isolated
  application root and the same synthetic selected tuple. It displayed all 13
  categories, preserved explicit Blocked `exec_command` and Unknown
  `apply_patch` states, exposed no call buttons, made zero Responses requests,
  and reported no page or console errors. The app's `Data` directory was under
  the sibling QA application root, outside the source repository. Automation
  used Playwright Core over the WebView2 CDP endpoint; no live model, provider,
  API key, or inference was used.

The pinned runtime verified by the fixture is App Server **0.155.1**, source
revision `be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`. Result
and screenshots are under
`D:\CODING\NeoBabylon-Data\QA\P3-03-tool-capabilities-20260925\P3-03-native-tool-catalog-1790344190968-24492\`.

A fresh native confirmation after the P3-02 final-diagnostic rendering change
also passed: Release WPF host build had 0 warnings/errors; graceful restart and
reopen retained the exact synthetic tuple; all 13 categories rendered;
Unknown `apply_patch` and Blocked `exec_command` remained non-callable; there
were zero Responses requests and zero page/console issues. The exact result and
screenshots are under
`D:\CODING\NeoBabylon-Data\QA\P3-03-approval-native-check-20260925\results\P3-03-native-tool-catalog-1790347539347-38356\`.

This passes only the bounded P3-03 catalog slice; it is not live-provider
qualification, full Phase 3 acceptance, or full Phase 2 acceptance. P3-02's
broader per-operation failure matrix, P2-10 screen-reader evaluation, P2-13
source/test provenance, and other recorded gates remain open. Packaging and
installation remain deferred by NB-DEC-010.

## 2026-09-25 — P5-01 read-only runtime diff review

The Codex Security workbench sealed scan
`76f65285-6b43-4fa3-ac08-e7531bdf03aa` against the dirty working-tree diff in
`D:\CODING\NeoBabylon-Runtime`, base/head
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, snapshot
`codex-security-snapshot/v1:sha256:cfe3307966624544ef95eedae8a8ea0550ef84c5298bf5403f614a4b8bbdb3b4`.
All 46 tracked changed paths were source-reviewed. The workbench's first
displayed inventory had 32 items; the remaining 14 were reconciled directly
against the Git diff and reviewed rather than counted implicitly. The sealed
report contains **zero findings** and four reviewed surface summaries:
OpenRouter request-route pinning, function-form `apply_patch`, the Windows
legacy guard under accepted unrestricted authority, and upstream/config/lock
compatibility changes. The inspected Cargo.lock edits changed internal
workspace package versions only; no third-party source/version/checksum
expansion was observed.

This result is a source review, not a security certification or runtime
qualification. No tests, build, provider request, or tool execution ran in
this scan. An independent checkout-level reviewer was unavailable; CODEX
Helper feedback was advisory and based on the supplied review summary. The
untracked captured executable
`bin/codex-app-server-x86_64-pc-windows-msvc.exe` was explicitly outside the
tracked diff and is not attested here.

Subsequent cross-check (separate from the sealed scan): the existing P3-02
native WPF/WebView2 fixture result at
`D:\CODING\NeoBabylon-Data\QA\P3-03-tool-capabilities-20260925\P3-02-native-command-failure-1790346625113-13224\result.json`
records the locked App Server 0.155.1 binary (`636f2216…9f318`, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`), ordinary `exec_command`, exit code
23, and final failure attributed to Codex App Server. The host supplied
`NEOBABYLON_REQUIRE_CONTAINED_TOOLS`; the request used the explicitly supported
`dangerFullAccess/never` policy, had no retry/fallback, and did not use the
ordinary Codex root. This closes the narrow supported-command smoke question:
one ordinary marker-enabled command ran. It does not directly invoke either
guarded legacy entrypoint or qualify containment.

Source inspection also confirms the implemented HTTP and WebSocket Responses
paths share `ResponsesApiRequest` construction and carry its provider pin into
the WebSocket payload; configured-route, WebSocket serialization, and cache
property tests exist. The distinct Guardian V2 scorer constructor sets
`provider: None`; the current host catalog supplies `model_messages: null`, so
this conditional path remains outside the active provider route unless its
feature/model metadata is enabled. This is path inspection and targeted test
evidence, not provider-side proof of the physical endpoint that served a
response. CODEX Helper reviewed selected verified source excerpts, found no
concrete defect in those excerpts, and closed the HTTP/WebSocket reconstruction
concern. Its review does not cover all 46 changed paths and is not full
independent checkout-level review.

Remaining evidence gaps: exercise function-form patch denial/approval/error/
hook parity against freeform where applicable; directly exercise both guarded
legacy entrypoints with the marker and verify rejection occurs before sandbox
startup; and revisit Guardian V2 route pinning only if its feature or guardian
model metadata becomes enabled. These are follow-up qualifications, not
findings established by the scan. The runtime source, lock, and provider
configuration were not changed. P5-01 and Phase 5 remain open.

## 2026-09-25 — P5-01 focused runtime follow-ups and P5-02 registry parsing

The source-linked target remained isolated at
`D:\CODING\NeoBabylon-Data\P5-01-SourceLinked-20260925\target`; the locked
runtime source revision and hash remained
`be2951ea34f0d295ed0becf97079f92fa5f6950e` and
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`.
No runtime source or runtime lock was changed and no live provider was used.

Focused current-source checks passed:

- `cargo test --locked --offline --jobs 1 -p codex-core --lib function_patch
  --target-dir D:\CODING\NeoBabylon-Data\P5-01-SourceLinked-20260925\target --
  --nocapture`: **3 passed, 0 failed** (function argument validation,
  advertised schema, and hook rewrite/output).
- `cargo test --locked --offline --jobs 1 -p codex-windows-sandbox --lib
  neobabylon_contained_mode_rejects_both_legacy_entrypoints --target-dir
  D:\CODING\NeoBabylon-Data\P5-01-SourceLinked-20260925\target -- --nocapture`:
  **1 passed, 0 failed**. The test set
  `NEOBABYLON_REQUIRE_CONTAINED_TOOLS=1` and verified both legacy entrypoints
  reject before the canary command runs. This is a negative guard check, not
  containment qualification.
- With PowerShell `$env:RUST_MIN_STACK='16777216'`,
  `cargo test --locked --offline --jobs 1 -p codex-core --test all
  --target-dir D:\CODING\NeoBabylon-Data\P5-01-SourceLinked-20260925\target
  apply_patch_function_call_executes_once_and_returns_function_output --
  --nocapture` passed **1 test, 0 failed**. The deterministic test verified
  one function call and its output. An earlier `--exact` invocation matched
  zero tests; a run without the stack-size setting overflowed the test thread.
  Neither attempt is counted as a pass.

The full deterministic host harness command
`dotnet run --project tests/host/NeoBabylon.Phase1A.Tests.csproj
--configuration ApprovalQA --no-restore` exited 0 on the second run. It
included the new project-registry test: malformed JSON and schema version 999
were rejected without changing the original file bytes. The first run had a
single cleanup error because `goals_1.sqlite` was briefly in use; an immediate
exclusive-read probe found the file available, no App Server child remained,
and the full rerun passed. This transient fixture cleanup result is retained
as a test-harness observation; no data was manually deleted.

These checks do not close P5-01: full independent checkout-level review and
function-form denial/approval/hook/error parity remain outstanding, and the
broader App Server suite retains its previously classified failures. They do
not close P5-02: migration, backup/restore, permission/storage failure,
downgrade, and production data-root semantics are not qualified. P5-03
installation and packaging remain deferred by NB-DEC-010.

## 2026-09-25 — P3-03 current UI regression pass

From `D:\CODING\NeoBabylon\ui\diagnostic`, `npm test` passed **88/88**,
`npm run typecheck` exited 0, and
`npm run build -- --outDir
D:\CODING\NeoBabylon-Data\QA\P3-03-ui-build-20260925-1817-fd41a9\dist`
exited 0 after transforming 2,002 modules. Build output was sent to the
fresh, previously absent QA path outside the source repository. Existing
warnings remain: Astryx/Lucide module-level `use client` notices and the
minified JavaScript chunk exceeds Vite's 500 kB advisory threshold. No UI
source changed during this pass. This is current UI regression/build evidence;
the separate prior native WPF restart/reopen fixture remains the evidence for
host-bound catalog persistence and renderer/runtime isolation.

## 2026-09-25 — P3-02 normal tool-output clarification (NB-DEC-011)

Martin accepted that ordinary model-directed App Server tool output is shown
in the conversation and forwarded in the normal provider continuation without
content redaction. Under the already accepted unrestricted same-user authority,
a command may read user-accessible files and return sensitive data; NeoBabylon
does not promise confidentiality for content an unrestricted tool can access.
The host must still avoid directly serializing its credential/configuration
values to renderer state, diagnostics, or bridge responses, and no generic
arbitrary RPC is authorized.

No new inference or test was made for this clarification. The relevant existing
deterministic WPF/WebView2 evidence is the synthetic canary run recorded above
in [P3-02 EXPOSED SURFACE EVIDENCE](P3-02_EXPOSED_SURFACE_EVIDENCE.md), which
used no real credential, real user data, or live provider. This settles output
handling only; P3-02's remaining denial, timeout/cancellation,
partial-output, and attribution matrix remains open.

## 2026-09-25 — P5-01 Guardian OpenRouter route-control correction

Source inspection found two production `ResponsesApiRequest` constructors:
the ordinary Codex client already carried the configured OpenRouter route,
while the conditional Guardian V2 sampler set `provider: None`. If Guardian
V2 were enabled with an OpenRouter provider, its request would omit the
accepted exact-selector / no-provider-fallback control. The NeoBabylon host
catalog currently supplies `model_messages: null`; this is not an active
Guardian inference path in the present host configuration.

The source patch adds the shared
`OpenRouterProviderRouting::for_endpoint` constructor, which yields one
`only` selector and `allow_fallbacks: false`. The ordinary client and Guardian
sampler both derive the routing object from their configured
`openrouter_provider_endpoint`; non-OpenRouter configuration leaves the
serialized field absent. HTTP and WebSocket serialization both retain this
request-level value, and the route remains part of WebSocket request-property
equality/cache invalidation. This is source-only evidence; it does not prove
which provider ultimately served a live request.

Focused checks against the current source passed:

- `cargo test --locked --offline --jobs 1 -p codex-guardian-v2 --lib
  --target-dir D:\CODING\NeoBabylon-Data\P5-01-SourceLinked-20260925\target`
  with PowerShell `$env:RUST_MIN_STACK='16777216'`: **89 passed, 0 failed**.
  The suite includes exact Guardian WebSocket pin serialization, the existing
  WebSocket-to-HTTP transport fallback test asserting the same route on both
  transports, and the new assertion that non-OpenRouter requests omit the
  OpenRouter field. An initial full-crate run without the stack override
  overflowed a test thread. Its cause is not established; do not describe the
  whole crate as passing under its default test-thread stack.
- `cargo test --locked --offline --jobs 1 -p codex-api --test clients
  responses_client_stream_request_preserves_item_ids --target-dir
  D:\CODING\NeoBabylon-Data\P5-01-SourceLinked-20260925\target -- --nocapture`:
  **1 passed**.
- `cargo test --locked --offline --jobs 1 -p codex-api --lib
  direct_serialization_preserves_websocket_request_payload --target-dir
  D:\CODING\NeoBabylon-Data\P5-01-SourceLinked-20260925\target -- --nocapture`:
  **1 passed**.
- `cargo test --locked --offline --jobs 1 -p codex-core --lib
  openrouter_provider_endpoint_is_pinned_in_the_responses_request --target-dir
  D:\CODING\NeoBabylon-Data\P5-01-SourceLinked-20260925\target -- --nocapture`:
  **1 passed**.
- `cargo test --locked --offline --jobs 1 -p codex-core --lib
  response_request_route_changes_invalidate_cached_websocket_properties
  --target-dir D:\CODING\NeoBabylon-Data\P5-01-SourceLinked-20260925\target
  -- --nocapture`: **1 passed**.
- `cargo fmt --all --check` and `git diff --check`: **passed**. Stable
  rustfmt printed its existing warnings that `imports_granularity = Item` is
  nightly-only; no formatting differences remained.

CODEX Helper reviewed the exact changed-source excerpts and these results. It
found no remaining concrete routing defect; its review was excerpt-limited and
not a full checkout review. The current runtime-lock identity and captured
App Server binary were not changed or rebuilt, and no live provider was
contacted. The source tests establish request construction/serialization,
not binary qualification or provider-side route attribution. P5-01 remains
open for complete checkout-level independent review, function-form
`apply_patch` denial/approval/hook/error parity, and source-to-binary
provenance. The conditional Guardian route should be kept under this focused
regression if Guardian metadata is later enabled.

## 2026-09-25 — P5-01 complete tracked-source diff review

The Codex Security workbench sealed scan
`7fa01596-17fa-41e7-95d5-c382f9475d76` against the dirty working-tree diff in
`D:\CODING\NeoBabylon-Runtime`, base/head
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, snapshot
`codex-security-snapshot/v1:sha256:2c93216bbdb28fbf5c3ab09292ebb59674dea1b9eeb66139eff86b2e1d70d9a7`.
The sealed manifest reports complete coverage of 47 tracked paths: 33
workbench review items plus 14 additional changed test/fixture/lock paths
reconciled against the Git diff and manually reviewed. The findings document
contains **zero findings**. Coverage records four reviewed surfaces:
OpenRouter route construction/serialization, function-form `apply_patch`, the
Windows legacy guard and candidate backend probes, and remaining fixtures,
tests, schema, and Cargo.lock changes. The supplemental source-review artifact
contains the complete path reconciliation.

This closes review coverage of the tracked source diff; it is not a security
certification, a second independent security review, a binary/runtime
qualification, or containment evidence. One independent architecture
fact-sheet pass was completed, but no second independent security reviewer was
available. The scan did not build or exercise a runtime binary, make a
provider request, or establish whether the NeoBabylon launch environment sets
`NEOBABYLON_REQUIRE_CONTAINED_TOOLS`. The untracked captured executable under
`D:\CODING\NeoBabylon-Runtime\bin` was explicitly excluded. Prior
hard-link/junction escape failures for candidate MXC/PSEC and elevated
backends remain unchanged; this scan does not endorse those backends.

P5-01 remains open for runtime source-to-binary provenance and function-form
patch denial/approval/hook/error parity. The separate focused tests recorded
above establish only the positive function-form execution path, three unit
tests, and the marker-enabled rejection of both guarded legacy entrypoints.
The scan neither changes the runtime lock nor expands the accepted execution
authority. It supersedes the earlier 46-path scan as the current tracked-diff
review record.

## 2026-09-25 — Current UI/host regression pass and P3-02 Stop characterization

The following current-source checks passed:

- `npm.cmd test --prefix ui/diagnostic`: **88/88** tests passed.
- `npm.cmd run typecheck --prefix ui/diagnostic`: passed.
- `npm.cmd run build --prefix ui/diagnostic -- --outDir D:\CODING\NeoBabylon-Data\QA\P2-P3-current-ui-build-20260925-2041`: passed; Vite transformed 2,002 modules. Existing Astryx/Lucide module-directive and >500 kB chunk advisories remain. Build output is outside the repository.
- `dotnet run --project tests/host/NeoBabylon.Phase1A.Tests.csproj --configuration ApprovalQA --no-restore`: full host harness exited 0; every emitted check passed.

That host run included the P3-02 interactive-command interruption
characterization against App Server `0.155.1`, revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`.
The turn was interrupted, but the command was still alive one second later;
partial output and the late marker were absent from final turn diagnostics.
There was exactly one loopback Responses request and no continuation/fallback.
The isolated application/Data roots were outside the source tree and
`ordinaryCodexRootUsed` was false. Result:
`D:\CODING\NeoBabylon-Data\QA\P3-02-command-interruption-6ffd15e3e03f4094a929d42ef46a251a\result.json`.

The probe's `passed: true` denotes successful characterization, not successful
command cancellation. The emitted test title says “stops an active ordinary
tool, preserves its partial output,” but the machine result contradicts that
wording; do not cite that line as feature acceptance. The distinct command-stop
operation and native UI outcome remain open. No live provider, runtime binary,
shared configuration, or credential was used or changed by these checks.

## 2026-09-25 — P3-02 identity-bound command Stop slice

This entry supersedes the earlier same-day characterization's statement that
the separate command-stop operation was unimplemented. It records a bounded
slice only; the complete P3-02 operation/failure matrix and Phase 3 gate remain
open.

The product now distinguishes Stop turn from Stop command. A focused stable
App Server list route discovers only command items observed on the interrupted
turn; a separate stable stop route requires exact item/process identity. The
trusted host binds thread, turn, item, process ID, and App Server client epoch;
React sends only item ID. Cross-thread duplicate IDs and stale selected-thread
actions fail closed. A request may be retried only while its original server
instance remains current. Runtime termination checks the exact item under the
process-store lock before capturing the process; after awaiting confirmed
termination it leaves any replacement slot untouched. The App Server
connection's `experimentalApi` opt-in remains false.

Current runtime: Codex App Server `0.155.1`, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, fresh canonical-sibling Windows
binary SHA-256
`9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d`; tracked
runtime patch SHA-256
`ddbcd19e0c9424147174ad36bc4ae15144e0e4fcce6d0db9c7f01e7a36db053d`. The
offline `--locked` build succeeded. Focused `codex-core` termination tests
passed **4/4** and `cargo fmt --all --check` passed. The complete isolated
ApprovalQA host harness exited 0 with **86 passes, 0 failures**. A new
regression delays the active-command list response until after the exact
`item/completed` notification and confirms the stale response cannot publish
a running Stop binding; completion also removes an already-published binding.
The final CODEX Helper adversarial follow-up found no remaining Critical or
Important issue in this correction and emphasized keeping observer updates and
binding publication in the same synchronization domain. It classified the
typed-`failed` host retry test as useful follow-up coverage, not a blocker.
UI tests
passed **92/92**, TypeScript typecheck passed, and the production Vite build
succeeded with pre-existing `use client` and >500 kB chunk advisories.

Native WPF/WebView2 result:
`D:\CODING\NeoBabylon-Data\QA\P3-02-native-command-stop-1790372549455-69872\result.json`.
The synthetic loopback fixture used model ID
`p3-02-native-command-stop-fixture`; no live inference or API credential was
used. The exact test command stayed alive after generation interruption, then
the accessible Stop command button was keyboard-focused and activated with
Enter. The UI visibly showed stopped state and Codex App Server attribution,
and removed the stop action. The exact process identity exited; a marker
scheduled for five seconds remained absent after a 5,250 ms observation. There
was one fixture request, no fallback, no page issues, and no fixture errors.
The isolated application and `Data` roots were outside the source repository;
the ordinary Codex data root was unused. Three screenshots and full result
JSON are retained in the result directory.

Host-level result:
`D:\CODING\NeoBabylon-Data\QA\P3-02-command-interruption-1ba9126bbf564c8e852ee570ea1fb14d\result.json`.
The controlled two-turn harness verified same-server retry, exact stop,
cross-thread colliding-item rejection, duplicate Stop as `not_found`, exact
App Server process replacement and epoch change, stale-binding rejection,
two fixture requests total, and no continuation/fallback. It is deterministic
host evidence, not a second native provider request.

Remaining gaps: a host fixture has not returned a typed `failed` Stop status
(the UI unit test does verify failed => unknown/retryable); the broader
denial/timeout/cancellation and per-operation partial-output matrix remains
open. The native probe is one exact command, one turn Stop, and one command
Stop; it does not qualify concurrent commands or full recovery behavior. The
first final-pass native launch used the ordinary Release host, which correctly
rejected the synthetic capability record outside `docs/release`; rerunning
with the dedicated `ApprovalQA` build passed. This was a test-configuration
correction, not a product-code change.

## 2026-09-26 — P3-02 typed command-Stop failure and retry

Closed the remaining host-level typed-`failed` retry evidence gap for the
bounded P3-02 command Stop action. A deterministic UI test first demonstrated
that a `failed` response with `commandStopAvailable=false` was incorrectly
left retryable. The UI now permits that retry only when the host explicitly
confirms the exact binding is still current; missing or false availability is
fail-closed and the explanation no longer invites an unavailable retry. The UI
suite passed **93/93**, including this distinction;
`npm run build` (TypeScript typecheck plus production Vite build) passed with
the existing Astryx module-directive and large-chunk advisories.

The ApprovalQA-only host seam supplies one typed App Server `failed` result
only after checking the outgoing stable `thread/commandExecution/stop`
request against the host-owned thread, item, and process identity. Production
Release behavior has no response interception. The host reports retry
availability only while the selected thread, exact identity object, and
App Server client epoch remain current. The full ApprovalQA host harness
exited 0. The machine result records `typedFailedStopStatus=failed`,
`typedFailedStopRetryAvailable=true`, and the command still alive; a subsequent
request through the real pinned App Server returned `stopped`. It also
rechecked cross-thread collision rejection, cancelled-request same-instance
retry, duplicate Stop, App Server replacement, and stale identity rejection.
There were two deterministic loopback Responses requests and no provider/model
fallback. Result:
`D:\CODING\NeoBabylon-Data\QA\P3-02-command-interruption-fca1366a31b64e0a857d0a9ec46ab2f5\result.json`.

Release and ApprovalQA WPF hosts built with zero warnings/errors. The first
native fixture attempt with the ordinary Release host correctly refused the
synthetic capability record; rerunning with the fresh ApprovalQA host passed.
The successful WPF/WebView2 result is
`D:\CODING\NeoBabylon-Data\QA\P3-02-typed-failed-retry-native-approvalqa-20260926-01\P3-02-native-command-stop-1790373810728-74608\result.json`.
It confirms the test command remained alive after turn interruption, the
separate keyboard-invokable Stop action visibly reached `stopped`, the exact
process exited, and no scheduled marker appeared after 5,250 ms. Exactly one
synthetic loopback request occurred, with no page or fixture errors. This
native fixture tests successful Stop, not a UI-driven synthetic `failed`
response; typed-failure retry is covered by the UI unit and host-level fixture
separately.

Both probes used locked App Server `0.155.1`, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d`, and
isolated application/Data roots outside the source repository. The ordinary
Codex data root was unused. No live inference, system-wide dependency
installation, provider configuration change, or runtime-source change was
performed for this extension. Production denial, operation-specific
timeout/cancellation, and broad per-operation partial-output coverage remain
open. P3-02 and Phase 3 are not accepted.

## 2026-09-26 — P2-10 native WebView2 accessibility-tree smoke

The freshly built Release WPF host completed the native visual/appearance
harness and captured Chromium accessibility trees from the actual WebView2.
In the primary shell, the snapshot contained the named Search loaded
conversations searchbox, New task and Open diagnostics buttons, and Message
NeoBabylon textbox (251 exposed nodes). With Runtime diagnostics open, the
273-node snapshot contained the named dialog and Done button and did not expose
the background searchbox, confirming modal inertness in this tree. The same
run passed three host launches at desktop/compact viewports and dark/light
appearance states, with visible keyboard focus and no page issues.

App Server identity matched the lock: version `0.155.1`, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d`. Source
root was `D:\CODING\NeoBabylon`; the isolated application root and `Data` were
under
`D:\CODING\NeoBabylon-Data\QA\P2-10-native-ax-20260926-02\P2-12-native-visual-1790374903952-36916\`.
`ordinaryCodexRootUsed=false`, `providerInference=false`, and `issues=[]` in
`result.json`. The run used no provider request or API key.

Build: `dotnet build host/NeoBabylon.Host/NeoBabylon.Host.csproj
--configuration Release --no-restore` with a unique QA `OutDir`; zero warnings
and errors. Runner: `node tests/qa/native-visual-acceptance.mjs` with the fresh
host, Playwright Core entry module, and isolated QA parent. Result:
`D:\CODING\NeoBabylon-Data\QA\P2-10-native-ax-20260926-02\P2-12-native-visual-1790374903952-36916\result.json`.
Native window screenshots and WebView screenshots are in the same directory.

This is accessibility-tree role/name and modal-inertness evidence, not a
Windows Narrator/screen-reader pass. It does not cover the complete nine-flow
native suite; P2-10 and the full Phase 2 gate remain open.

## 2026-09-26 — P2-13 ApprovalQA host-suite refresh

Ran the complete NeoBabylon host harness twice consecutively with
`dotnet run --project tests/host/NeoBabylon.Phase1A.Tests.csproj
--configuration ApprovalQA --no-restore`. Both runs exited 0. The counted
repeat reported **86 passed, 0 failed**. It included runtime-lock identity,
application-root/Codex-home isolation, capability and effective-context
mapping, project/history binding, bounded-output and tool-failure outcomes,
interruption/recovery, and the P3-02 identity-bound command Stop probe. The
command-Stop probe used loopback Responses fixtures; no provider inference or
fallback occurred. The repeat emitted no SQLite/cleanup diagnostic.

This is the NeoBabylon host harness against the locked App Server, not the
separate broad pinned-source App Server integration suite. The latter remains
at 1,170 passed / 25 failed / 11 ignored with the helper, shell/environment,
and Guardian baseline classifications recorded in the preceding P2-13 entry.
No consolidated aggregate result file was emitted for this run. This refresh
does not close P2-10, P2-11, P2-13, or overall Phase 2 acceptance.

## 2026-09-26 — P3-02 native late command-completion delivery

The native WPF/WebView2 P3-02 command-stop fixture now verifies the lifecycle
after model generation has been interrupted but an approved command continues
to run. After the controlled command exited naturally, the host forwarded the
exact late App Server `item/completed` notification under the original
`startTurn` bridge request ID. React accepted it only for the stored exact
thread/turn/command-item binding, and the visible activity card changed to
`succeeded` with no Stop control. The assertion also checks the App Server
terminal item status. This closes the previously unverified late-event path;
it does not qualify other interruption, denial, timeout, or partial-output
cases.

The same four-scenario native run checked confirmed command Stop, stale and
duplicate Stop rejection, natural exit, and a lost App Server transport. The
visible states were `stopped`, `not_found`, `succeeded`, and conservative
`unknown` respectively. The controlled command's scheduled marker was absent
after confirmed Stop and present after natural exit. There were exactly four
loopback Responses requests, no continuation/fallback, no fixture/page issues,
and no live model inference. The QA result and screenshots are under
`D:\CODING\NeoBabylon-Data\QA\P3-02-command-completion-native-green3-20260926-01\P3-02-native-command-stop-1790422749307-6972\`.

App Server identity matched the lock: version `0.155.1`, source revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, SHA-256
`9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d`, and
source-patch fingerprint
`ddbcd19e0c9424147174ad36bc4ae15144e0e4fcce6d0db9c7f01e7a36db053d`. The
application root and `Data` were isolated under the QA directory, distinct
from source; ordinary Codex-root use was false. Exact command/App Server
process identities were checked after the run and none remained alive.

During test repair, one native attempt reached the late-event wait and then
failed because the harness treated its `waitFor` helper as returning the
matched value. The helper returns `undefined`; the test now retrieves the
observed event separately. One subsequent attempt stopped earlier: the second
fixture prompt did not reach the loopback server, and the isolated UI showed
App Server JSONL EOF. The next complete four-scenario run passed; the cause of
that single EOF attempt is unknown and remains a verification note, not a
claimed product fix.

The contemporaneous full UI suite passed **94/94**. `npm run build --prefix
ui/diagnostic` passed TypeScript and production bundling; existing Astryx
module-directive and >500 kB chunk advisories remain. The full ApprovalQA host
harness initially hit a transient `goals_1.sqlite` lock during cleanup of its
exact temporary unrestricted-policy root. After confirming the test-owned
App Server had exited and the file was no longer exclusively held, that
cleanup was changed to use the existing bounded, exact-parent deletion retry.
The complete rerun exited 0 with all checks passing (86 passed, 0 failed).
This is test-cleanup hardening, not a production storage change. The separate
pinned-source App Server suite remains classified at 1,170 passed, 25 failed,
and 11 ignored; it is not converted to a green result by these checks.

## 2026-09-26 — P2-13 current-source broad suite and helper retries

Reran the pinned current-source App Server integration suite serially and
offline after the latest command-stop additions:

```text
cargo test --locked --offline --jobs 1 -p codex-app-server --test all \
  --target-dir D:\CODING\NeoBabylon-Data\QA\P2-13-current-runtime-20260926\target \
  -- --test-threads=1
```

The run exited 101 after **1,170 passed, 25 failed, 11 ignored** in 1,515.74
seconds. Full output:
`D:\CODING\NeoBabylon-Data\QA\P2-13-current-runtime-20260926\cargo-app-server-integration-current-20260926.log`.
Afterward, the two absent test helpers were built and the previously failing
helper-dependent cases were rerun in focused groups:

- `codex-rmcp-client`'s `test_stdio_server` built offline into the isolated
  D-drive QA target. Focused `daybreak_access` (5/5), `mcp_server_status`
  (18/18), `plugin_install_makes_bundled_mcp_servers_available_to_followup_requests`
  (1/1), and
  `thread_start_with_managed_read_only_does_not_trust_or_load_project_mcp`
  (1/1) filters passed. These include all nine stdio-helper failures from the
  broad run plus passing controls.
- `codex-code-mode-host` built offline into
  `C:\Users\Martin\AppData\Local\Temp\NeoBabylon-P2-13-code-mode-target`.
  The source tree is on C: while the primary QA target is on D:; the upstream
  `v8` build script requires a same-drive `gn_root` symlink, so the separate
  temporary target avoided a privilege change. The build used OpenAI's
  published Rusty V8 150.4.0 archive and source-binding asset. The archive
  SHA-256 matched the exact value in the upstream
  [Codex MODULE.bazel](https://github.com/openai/codex/blob/main/MODULE.bazel#L3461-L3472);
  the binding path and process-local environment-variable mechanism were
  checked against the upstream
  [Rusty V8 release workflow](https://github.com/openai/codex/blob/main/.github/workflows/rusty-v8-release.yml#L1625-L1637).
  The resulting host helper SHA-256 was
  `03A615E2A78B726C8CD17D80E32FEA510ACFE7491D55A1813C13695254C37F2A`.
  It was copied only to the absent helper path in the isolated D-drive test
  target after confirming that destination did not exist; source and
  destination hashes matched. Focused `code_mode_host` (7/7),
  `model_guardian_policy_scores_code_mode_cells` (3/3), and
  `code_mode_exec_emits_correlated_production_analytics` (1/1) filters passed.
  These include all nine code-mode-helper failures from the broad run plus
  passing controls.

Therefore all 18 failures attributable to absent helper binaries now pass in
focused reruns; the broad suite itself remains **1,170/25/11**, not green.
The current broad-run remainder is five shell/environment-sensitive cases and
two Guardian hook assertions. Prior focused evidence passed four of five
shell/environment cases using process-local aliases; the optional-timeout
case is on the human-only `thread/shellCommand` route and remains unresolved.
The same two Guardian hook assertions failed in a focused clean-HEAD
comparison, which is not a complete upstream baseline run. Do not attribute
all broad failures to either the NeoBabylon patch or upstream without that
qualification.

No NeoBabylon runtime source or runtime-lock identity changed. The test
helpers and downloaded V8 assets reside only in QA/temp targets; no global
configuration, credentials, or live provider inference were used. The
separate pre-existing `codex-code-mode-host` process was observed with start
time 2026-09-25 and was not modified or stopped. P2-13 and Phase 2 acceptance
remain open.

## 2026-09-26 — P2-01/P2-10 fresh native keyboard and history rerun

Built a fresh Release WPF host into
`D:\CODING\NeoBabylon-Data\QA\P2-10-native-keyboard-20260926-03\host\`;
the build passed with zero warnings and errors. The native fixture runner
requires test-only `ApprovalQA` hooks: a first runner build mistakenly used
Release and failed at compile time because those hooks are configuration-gated.
Rebuilding the runner with `--configuration ApprovalQA` passed with zero
warnings and errors. No product source change was required for this build
configuration correction.

Ran `node --check tests/qa/native-history-pagination.mjs` and then
`tests/qa/native-history-pagination.mjs` with the fresh host, ApprovalQA
apphost runner, cached Playwright Core, and a new QA child directory. The
native WPF/WebView2 run passed (`passed: true`) with no page issues. It seeded
56 deterministic loopback Responses tasks, verified 50+6 newest-first
pagination, loaded-history search disclosure, exact provider/model attribution,
keyboard load-more, exact oldest-task resume, and project-isolated empty
history. Reopening saved tasks issued no additional provider request. The
oversized assistant transcript reopened with an explicit App Server-history
notice; its 119,983-character preview remained under the 120,000-character
projection limit, versus 120,576 characters in the complete fixture response.
The diagnostics dialog test verifies initial focus, the named scroll region,
the P3-03 capability search, all 13 category disclosure tab stops, Done,
tab/shift-tab wrapping, Escape, background inertness, and focus return.

The run uses synthetic provider/model identity `lmstudio /
phase1a-qwen3-14b` and a loopback endpoint. The fixture made exactly 56 setup
requests before opening the native UI and zero inference requests while
reopening saved tasks. App Server SHA-256 matched the current lock:
`9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d`; source
revision is `be2951ea34f0d295ed0becf97079f92fa5f6950e`. The application root
was separate from source. Screenshot and manifest:
`D:\CODING\NeoBabylon-Data\QA\P2-10-native-keyboard-20260926-03\run6\P2-P01-native-1790431728685-59336\native-history-pagination.png`
and `history-manifest.json` in the same directory.

Two stale/racy assumptions in the old fixture were corrected based on native
evidence: P3-03 added a search input and 13 keyboard-focusable category
disclosures to Runtime diagnostics, and saved task/project buttons are disabled
while the asynchronous resume/history refresh is active. The test now asserts
the current focus order and waits for each target button to become enabled
before keyboard activation. A final full native rerun passed. This is not the
complete nine-flow native suite or a Windows Narrator/screen-reader evaluation;
P2-10 and overall Phase 2 acceptance remain open.
## 2026-09-26 — workspace cleanup

The actual workspace layout now keeps 53 probe source directories in
`.local/Obsolete/ProbeSources`, the `NBRT-RouteControl` and `NeoBabylon-Data`
repositories/data under `.local/Obsolete`, and the active runtime checkout
under `.local/Runtime/NeoBabylon-Runtime`. The pinned binary is at
`.local/Runtime/LockedBuild/codex-app-server.exe`; its SHA-256 matches
`runtime/runtime-lock.json`, which points to both paths. Read-only inspection
confirmed the 53 directories and runtime checkout revision. The supplied
cleanup handoff reports Git worktree repair and `RuntimeIdentity.LoadVerified`
passed; these were not rerun for this record. After the moves,
`MockProviderError.Tests.ps1` and `MockProbeQualification.Tests.ps1` each exited
0 after their active binary fixture path was updated. An initial stale
2026-09-25 Release host rejected the isolated QA application root. A
current-source Release build on 2026-09-26 exited 0 with zero warnings and
errors. Its WPF window titled `NeoBabylon` appeared and closed cleanly using
`.local/Lab/Runs/post-cleanup-current-host-20260926T195646Z-92120d3805c2/App`
with isolated `Data` and `CodexHome` paths.

The native brand-mark fixture passed on its single rerun after a bounded,
test-only CDP page lookup wait was added. It observed the `NeoBabylon` title,
the loaded 29x29 tower mark, successful diagnostics drawer open/close, and no
page or console errors. Evidence is in
`.local/Lab/Runs/native-brand-mark-fixed-20260926T200323Z-d40f8a8bc63c/`
(`smoke-evidence.json`, `fixture-output.txt`, and `native-brand-mark.png`). No
model request or inference occurred. The host smoke checked the locked App
Server path's expected and observed SHA-256 before launch, but recorded no App
Server child process. Thus the current-layout host UI smoke passed, while the
executable path actually used by a host-spawned App Server, restart, and
broader QA qualification remain open. The default `%LOCALAPPDATA%\NeoBabylon`
root was not moved or modified. See
[`2026-09-26 workspace cleanup`](../history/2026-09-26-WORKSPACE_CLEANUP.md).

## 2026-09-26 — P4 callable-route deterministic qualification

An isolated test-only App Server 0.155.1 run used the locked binary (SHA-256
`9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d`),
an isolated `CODEX_HOME`, a loopback Responses fixture, and a private stdio MCP
server with one `echo_reviewed` tool. The provider request advertised
`mcp__p4_private.echo_reviewed`; a synthetic function call reached the MCP
process and the `P4_MCP_LOOPBACK_OK` result reached the next provider request.
Disabling the server and calling `config/mcpServer/reload` removed it from that
thread. A direct call to the disabled server was rejected, including after an
App Server restart on resumed and new threads. The independent rerun returned
`passed: true`, five fixture provider requests, one MCP call, and no errors.
Evidence: `.local/Lab/Runs/P4-Route-Probe-5a362283d7f6/result.json` and
`trace.json` (test-only generated fixtures). This establishes a candidate
callable route, not product approval/promotion, live-model tool use, in-flight
revocation, or Phase 4 acceptance.

The user explicitly removed Narrator evaluation from the current requested
gate. Keyboard/native fixture evidence must not be labeled a Narrator pass.

## 2026-09-26 — Nemotron free-route test preflight (not App Server qualification)

Martin selected `nvidia/nemotron-3-ultra-550b-a55b:free` for tests; this did
not change the product default. A fresh OpenRouter models/endpoints API read
found the exact alias, advertised 1,000,000-token context, `tools` and
`tool_choice` parameters, and one currently listed endpoint: Nvidia, tag
`nvidia`, served-model name `nvidia/nemotron-3-ultra-550b-a55b-20260604:free`,
max completion 65,536. Quantization and structured output are Unknown. The
models API advertises `high`/`medium` reasoning efforts and default `high`,
but the endpoint-specific effective levels/default remain unqualified; the
capability record therefore maps no Codex reasoning level.

One bounded direct Responses request with the user-provided key, exact alias,
`provider.only=["nvidia"]`, `allow_fallbacks=false`, and 64 requested output
tokens returned HTTP 200 and the requested marker. Generation lookup for
`gen-1790454988-h44QIOQnWFmhWgeLVknq` returned HTTP 200, provider Nvidia,
served model `nvidia/nemotron-3-ultra-550b-a55b-20260604:free`, and cost 0.
The response and generation lookup reported different token totals, so token
accounting is not reconciled. No key was saved in project artifacts. This was
direct-provider evidence, not an App Server or named-tool round trip.

The first isolated App Server QA attempt with `openrouter_provider_endpoint =
"nvidia"` failed during config validation at `thread/start`: the locked binary
requires a two-part `provider/tag` value. It sent zero provider requests and
did not read the key. Evidence:
`.local/Lab/Runs/P4-Route-Probe-Nemotron-c512858c7698/result.json`.
[OpenRouter's provider-routing documentation](https://openrouter.ai/docs/guides/routing/provider-selection)
confirms that a base provider slug is valid but matches that provider's
current and future endpoint variants. Thus a `nvidia`-only selector with
fallbacks disabled assures the provider, not an immutable endpoint variant.
The narrow source/host validation correction and locked-binary rebuild remain
in progress; neither live MCP qualification nor the product route decision is
accepted on this evidence alone.

## 2026-09-26 — bounded runtime lock cutover

The active runtime source checkout at `.local/Runtime/NeoBabylon-Runtime` was
read at HEAD `be2951ea34f0d295ed0becf97079f92fa5f6950e`. Its tracked diff,
hashed from the raw `git --no-optional-locks diff --no-ext-diff --binary
--no-renames HEAD` output, was
`76454c604e0a375d925e1d9dcc4f3ce544c7776539ba651acd61a29f6a273a1f`.
No locked App Server process was running before replacement.

The previous `.local/Runtime/LockedBuild/codex-app-server.exe` was copied to
`.local/Runtime/LockedBuild/Archive/codex-app-server-2026-09-26-9a3e88bf2513d835.exe`;
the archive retains SHA-256
`9a3e88bf2513d83581718f3e52055b9231351ec7d50da9fe6051a057659e859d`.
The candidate at
`.local/Runtime/NeoBabylon-Runtime/codex-rs/target/debug/codex-app-server.exe`
was copied to the locked path. Both files now hash to
`5a48fe628c0654971da58542b65e33ac64cc7767e889d580fddb34c527fa807b`;
the locked executable reports `codex-app-server 0.155.1`.
`runtime/runtime-lock.json` records this binary hash and the tracked source
diff hash. `RuntimeIdentity.LoadVerified` returned the expected source path,
revision, diff hash, binary path, version, and binary hash.

The focused probe at
`.local/Lab/Runs/P4-Route-Probe-Nemotron-c512858c7698/probe.py` was rerun
offline with its default locked-path selection. Its `result.json` reports
`syntax_verified_only`, `locked_manifest_default`, private MCP tool discovery,
no MCP key, and zero provider requests; this mode does not execute a turn. The
prior `candidate-result.json` records a two-request loopback named-MCP round
trip for the identical binary SHA-256. The earlier `result.json` was preserved
as `result-before-lock-cutover-2026-09-26.json` in the same run directory.
No live provider request, key read, or Phase 2/Phase 4 acceptance is claimed.

## 2026-09-27 — Task 7–9 source-only reconciliation (unverified candidate)

Source inspection only; **no test, build, native run, live request, or runtime
qualification was performed for this entry**. Core source contains the
non-executing candidate envelope/review/prepared-disabled records, a
product-owned stdio MCP adapter that lists no tools and rejects calls, and an
explicit isolated-config opt-in for one app-root adapter with `enabled = false`
and `enabled_tools = []`. The default config path does not select it. The named
host `stageGeneratedToolDisabledMcp` parser accepts only request ID, tool ID,
content identity, review identity, and current binding hash; supervisor source
revalidates the current prepared-disabled record, selects the fixed
`Adapters\NeoBabylon.GeneratedToolMcp.exe` path, rejects an active client/turn,
and carries one process-local opt-in through both config writes. Its typed MCP
status path reports confirmation only for disabled plus empty inventory,
reports inconclusive status as `Unknown`, and stops a client if tools are
observed. The React drawer source exposes candidate inspection, review/reject,
prepared-disabled transitions, separate Stage confirmation, and an independent
binding-history recovery path restricted to exact-hash Revoke/Cleanup. Focused
test code is present but unrun; no installed adapter or end-to-end staging
behavior is verified by this entry. Config and model catalog writes are not a
single transaction.

NB-DEC-012 accepts a **separate future Activate confirmation**, not activation
by Review, Prepare, or Stage. No Activate UI or operation exists. Stage neither
enables nor reloads runtime MCP servers; this candidate exposes no callable
generated-tool route or model-visible generated tool. The historical
2026-09-26 MCP probes above
remain historical evidence only. Final current-locked-binary deterministic
registration/call/disabled-revoked reload/restart and the exact selected
Stealth `stealth/space-bunny-alpha` live provider/route qualification remain
open; anonymous endpoint metadata and advertised free pricing do not prove a
native-request cost bound. Task 8/9 and Phase 4 acceptance remain open.
Installation and packaging remain deferred under NB-DEC-010.

## 2026-09-27 — runtime cap, patch guidance, and command Stop source checkpoint

Source inspection only for this entry: **no build, test, native run, or live
request was performed**. The active nested runtime source now resolves the
effective model's `max_completion_tokens` (or the 32,768 Unknown fallback) and
validates an explicit or omitted `max_output_tokens` before committing turn
settings; it rejects zero and above-known-maximum values rather than clamping.
Review source resolves the selected review model's cap and forwards that value
through the one-shot delegate's `TurnStartOptions` to request construction. A
fresh review context does not inherit the preceding turn's per-request cap.
The function-form `apply_patch` spec now tells the model that omission targets
the primary environment only and how to name a secondary environment by exact
ID. The named command-Stop source now waits for observed child exit (or returns
failure on timeout/read error) before reporting a confirmed Stop for the exec
server path. Focused test code exists but was not run for this checkpoint.

At this historical checkpoint, these changes postdated the **then-locked App Server binary**,
which was the older implementation; no source-to-launched-binary identity was established
for them. Task 4's source-to-launched identity, P2-11 effective native cap and
the full Phase 2/Phase 4 gates remain open. Guardian cap compatibility is a
future conditional gate if that path is enabled, not a current qualification
or a passed check. Prior dated machine evidence above is unchanged and does
not verify these new source changes.
