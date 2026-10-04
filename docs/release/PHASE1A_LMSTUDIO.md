# Phase 1A / 1B LM Studio qualification

Date: 2026-09-23. Status: **The minimal LM Studio/App Server live tool round
trip now passes under the explicit restricted-token Windows policy. The
Phase 1A visible WPF check was still open on 2026-09-22; a separate Phase 1B
visible WPF click-through passed on 2026-09-23. Phase 1B remains active.**

This is the canonical narrative record for the LM Studio tuple. The
machine-readable provider record is
[MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json](MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json).
The generated local trace is ignored by Git at
`artifacts/phase1a/lmstudio/qualification.json`; it is evidence, not a
runtime dependency.

## Accepted requirements used

These are authorized choices, not proposals: local sibling repositories
`NeoBabylon` and `NeoBabylon-Runtime`; upstream-preserving Codex ancestry; a
thin WPF/.NET + WebView2 host with React/TypeScript/Astryx as the UI direction;
LM Studio as the first live target; exact provider/model discovery; no
provider/model fallback; isolated application-root `Data`; and no remote
repository, system-wide installation, commit, or push.

The catalog adapter, `NEOBABYLON_WINDOWS_SANDBOX_MODE` selector, named
operation names, and diagnostic JSON fields are implementation choices inside
that scope. They do not settle packaging, the approval matrix, compaction
policy, OpenRouter behavior, or a future runtime fork.

## Exact tested tuple

| Field | Observed value | Evidence source |
| --- | --- | --- |
| Codex runtime | App Server `0.155.1`, `rust-v0.155.1` | `runtime/runtime-lock.json`, launch and `thread/start` evidence |
| Codex source revision | `be2951ea34f0d295ed0becf97079f92fa5f6950e` | pinned sibling checkout and runtime lock |
| App Server binary SHA-256 | `253C6D8424EA45BA9D36D1F665D9B7B4C917782D4CF5D56E61CD0664CC52BF0D` | runtime lock and launch verification |
| Runtime checkout | `D:\CODING\NeoBabylon-Runtime` | local sibling Git checkout; source unchanged |
| LM Studio application | ProductVersion `0.4.25.0`; FileVersion `0.4.25+1` | executable file metadata |
| LM Studio CLI | commit `69d945a` | `lms --version` |
| Server | `127.0.0.1:1234` | `lms server status` and live HTTP inspection |
| Provider endpoint | `http://127.0.0.1:1234/v1` | selected LM Studio Responses endpoint |
| Catalog model key | `qwen/qwen3-14b` | `/api/v0/models` and `lms ls --json` |
| Loaded model identifier | `phase1a-qwen3-14b` | `/api/v0/models` after load and `lms ps --json` |
| Selected variant | `qwen/qwen3-14b@q4_k_m` | `lms ls --json` / `lms ps --json` |

No other provider, model, endpoint, or route was substituted.

## Authoritative observed capability record

The canonical JSON record contains only observed provider/model facts:

- Architecture `qwen3`; parameters `14B`; model size `9001931568` bytes;
  GGUF; quantization `Q4_K_M` / 4 bits; publisher `qwen`.
- Advertised context: known `32768` from
  `/api/v0/models.max_context_length`.
- Effective loaded context: known `32768` from
  `/api/v0/models.loaded_context_length` and `lms ps --json.contextLength`.
- Tool/function calling: known `tool_use` from the advertised capabilities;
  `trainedForToolUse=true` was also reported.
- Agent metadata: model type `llm`, loaded state, `vision=false`, and
  `parallel=4` were reported by LM Studio metadata.
- Reasoning controls/levels: **Unknown**; they were not advertised.
- Structured output: **Unknown**; it was not advertised and was not inferred
  from tool use.
- Hardware/device identifier, output-token limit, and provider-specific
  reasoning semantics: **Unknown**.

Unknown values remain Unknown. They are not converted into GPT-family
defaults or silently substituted settings.

## Phase 1B metadata projection

Source inspection found a supported stock Codex seam: isolated
`config.toml` can point to a complete `model_catalog_json`. NeoBabylon now
generates one exact catalog entry from the authoritative capability record
before App Server startup. No Codex runtime source patch was needed.

The generated entry is an adapter projection, not a claim about LM Studio:

| Effective Codex field | Value | Status/source |
| --- | --- | --- |
| Catalog slug | `phase1a-qwen3-14b` | exact provider identifier observed after load |
| `context_window` / `max_context_window` | `32768` | observed LM Studio context |
| `supported_reasoning_levels` | `[]` | unknown provider reasoning; no levels invented |
| `supports_reasoning_summary_parameter` | `false` | adapter refuses an unsupported/unknown control |
| `shell_type` | `unified_exec` | adapter mapping from observed `tool_use`; requires later tool qualification |
| `input_modalities` | `text` only | observed `vision=false` |
| `auto_compact_token_limit` | `null` | no provider compaction limit inferred |
| `effective_context_window_percent` | `95` | explicit Codex adapter/runtime accounting choice, not provider metadata |

The required `base_instructions` field is a small NeoBabylon adapter string
needed by the upstream catalog schema. It is not copied provider metadata.
The generated catalog is shown in qualification evidence and the host's
`startThread` result; `allowProviderModelFallback=false` remains explicit.

LM Studio's `parallel=4` metadata is retained in the authoritative record but
is not mapped into the Codex model catalog; the available evidence does not
establish its semantics relative to Codex concurrency, and this adapter does
not equate it with parallel tool-call support. The current
`providerModelMetadataMismatch` diagnostic checks provider/model identity,
configured/effective context, and native-metadata fallback only; it does not
claim every `agentMetadata` field is mapped. Reasoning and structured-output
values remain Unknown.

The corrected TOML ordering keeps `model_context_window = 32768` at the
top level rather than accidentally placing it inside `[windows]`. This was a
real implementation defect found by inspecting `config/read`, and the host
test now guards against its regression.

### Pinned Codex context-budget trace — 2026-09-22

The provider-reported/effective LM Studio context is `32768`. The catalog's
`effective_context_window_percent = 95` is an explicit NeoBabylon adapter
choice, not LM Studio metadata. In the pinned `rust-v0.155.1` source,
`ModelInfo::usable_context_window()` computes
`resolved_context_window * effective_context_window_percent / 100`, which
produces `31129`; this matches the session context observed in the successful
live run.

The catalog field `auto_compact_token_limit = null` means no provider-supplied
compaction limit was inferred; it does **not** mean Codex compaction is
disabled. The pinned `ModelInfo::auto_compact_token_limit()` derives a default
limit of 90% of the resolved context (capped by a non-null explicit limit).
`AutoCompactTokenLimitScope` defaults to `Total`. For this record that is a
source-derived trigger of `29491`, below the `31129` usable-context hard cap.
The turn loop checks token limits before and after sampling and enters the
runtime compaction path when the limit is reached.

This is source inspection plus arithmetic, not an observed compaction event.
The live run recorded effective context `31129`, but no request-budget
boundary, automatic compaction, post-compaction context, or stale-capability
invalidation was exercised. Therefore the actual LM Studio/App Server
budget/compaction qualification remains open. A targeted offline Cargo test
attempt did not start: Cargo could not find the pinned `tungstenite` Git
dependency in its local cache. No network fetch or dependency installation was
performed.

## Sandbox authority evidence

Pinned upstream source inspection establishes that the raw requested
`workspace-write` value and the Windows effective compatibility projection are
different layers. In pinned `codex-rs/core/src/config/mod.rs`,
`windows.sandbox = "unelevated"` resolves to the `RestrictedToken` Windows
sandbox level, while no selected Windows sandbox resolves to `Disabled`.
`codex-rs/core/src/exec_policy.rs` states that on Windows with the backend
disabled, managed filesystem restrictions are only policy shape and no platform
sandbox enforces them. App Server's
`codex-rs/app-server/src/request_processors/thread_processor.rs` compares a
requested legacy sandbox value against the active config snapshot's effective
sandbox policy. Thus `config/read`'s configured `sandbox_mode` alone is not the
execution authority; the effective permission profile and Windows sandbox
level govern the tested path. The default response projected to `readOnly` and
the ordinary command was rejected; the explicit restricted-token setting
projected to `workspaceWrite` and the exact command succeeded. This explains
the earlier report; it is not an unexplained UI mismatch.

| Configuration | `config/read` | `thread/start` effective projection | Observed command |
| --- | --- | --- | --- |
| default/no Windows backend | `workspace-write` | `readOnly`, network off | deterministic `cmd.exe /d /c ver` rejected |
| explicit `NEOBABYLON_WINDOWS_SANDBOX_MODE=unelevated` | `workspace-write` plus `[windows].sandbox="unelevated"` | `workspaceWrite`, network off | deterministic `cmd.exe /d /c ver` succeeded with exit code 0 |

The explicit mode selects Codex's restricted-token Windows backend. NeoBabylon
does not make the policy silently more permissive: the host reports the
requested selector, raw config, effective sandbox, and whether the Windows
backend downgraded it. The WPF host reads the selector only from the explicit
`NEOBABYLON_WINDOWS_SANDBOX_MODE` environment setting; absent selection remains
visible as the default/downgraded path.

## Qualification results

| Stage | Result | Evidence and limitation |
| --- | --- | --- |
| Deterministic mock, default Windows policy | **Expected failure** | App Server protocol completed, but the ordinary command was rejected by effective `readOnly` policy. |
| Deterministic mock, explicit `unelevated` policy | **Pass** | App Server completed the function-call/output exchange; `cmd.exe /d /c ver` returned the Windows version; effective sandbox was `workspaceWrite`; catalog fallback warning was absent. |
| WPF/WebView2 build | **Pass** | Cached WebView2 package `1.0.4191.47`; host build had zero warnings/errors. A fresh UI click regression could not run because the Windows automation helper was unavailable. |
| Live LM Studio through App Server, first incomplete attempt | **Partial** | The real model emitted `exec_command` and the isolated session recorded exit code 0, but no `turn/completed` arrived within ten minutes. |
| Live LM Studio through App Server, completed attempt | **Pass for the minimal tool round trip** | App Server emitted `turn/completed`; the exact `exec_command` ran with exit code 0 and returned the Windows version. Runtime/model/provider identity matched, effective context was `31129`, the effective sandbox was `workspaceWrite`, and no metadata fallback warning appeared. The run took 275.9 seconds. |
| Subsequent live repeat | **Timed out before tool execution** | The same selected tuple reached the ten-minute bound with no `function_call` or `turn/completed` in the session evidence. LM Studio logs show generation continuing until the bound. The runner then hit a transient journal-sharing `IOException`, so its original timeout observation was not included in that run's JSON; the raw artifact was preserved and the collector is now hardened/tested. The specific OS handle owner was not identified. |
| Same-tuple App Server repeat, 2026-09-23 | **Pass for the minimal tool round trip** | Deterministic mock passed before live inference. The exact `cmd.exe /d /c ver` call completed in 289.8 seconds with the selected model/provider, configured context `32768`, effective context `31129`, no metadata fallback, and no provider/model mismatch. Tool output included the Windows version and exit code 0, plus unrelated PowerShell profile warnings. This was the isolated qualification harness, not a WPF live click-through. |
| Visible WPF/WebView2 live path, 2026-09-23 | **Pass for the minimal tool round trip** | The UI showed LM Studio `phase1a-qwen3-14b`, the exact `Q4_K_M` variant, and explicit `unelevated` policy. It displayed the model's exact `exec_command` arguments (`cmd.exe /d /c ver`, shell `cmd.exe`), attributed successful Windows-version output to Codex App Server, and reached terminal `COMPLETED`. The isolated journal records a `212.674s` turn and context `31129`; the thread subsequently listed `idle`. At `1464×901`, there was no document overflow, console error, or external resource request. |
| Direct LM Studio Responses probes | **Pass as provider-only probes** | The same selected model completed a bounded text request in about 1.4 s, a tool-call request in about 2.5 s, and a tool-output continuation in about 9 s. These probes do not replace the App Server qualification. |

The earlier partial session is preserved at
`D:\CODING\NeoBabylon-Data\Phase1A-20260922T174057Z\Live\Data\CodexHome\sessions\2026\09\22\rollout-2026-09-22T19-41-13-01a0ca35-1bb1-79f2-b40e-2febc6d00f9d.jsonl`.
The successful session is
`D:\CODING\NeoBabylon-Data\Phase1B-ServerLog-20260922T183103Z\Live\Data\CodexHome\sessions\2026\09\22\rollout-2026-09-22T20-31-25-01a0ca63-10d5-78b2-981c-f2ee40e42fc7.jsonl`;
its full qualification snapshot is
`D:\CODING\NeoBabylon-Data\Phase1B-Repeat-20260922T185400Z\previous-qualified-run.json`.
The repeat's session is
`D:\CODING\NeoBabylon-Data\Phase1B-Repeat-20260922T185400Z\Live\Data\CodexHome\sessions\2026\09\22\rollout-2026-09-22T20-54-34-01a0ca78-456e-79d2-8c0e-f0e57e09472d.jsonl`;
the preserved pre-fix run artifact is
`D:\CODING\NeoBabylon-Data\Phase1B-Repeat-20260922T185400Z\qualification-runner-sharing-failure.json`.
The workspace artifact `artifacts/phase1a/lmstudio/qualification.json` now
contains the post-fix deterministic mock-only run. The successful session records
the exact `exec_command` arguments, a successful `function_call_output`, the
Windows version `10.0.26200.9457`, and `turn/completed`.

## Latest LM Studio request observations

The bounded LM Studio server/model log capture is outside the repository at
`D:\CODING\NeoBabylon-Data\Phase1B-ServerLog-20260922T183103Z`. It records two
Responses requests from the same App Server turn: the initial tool request
and the continuation with the tool output. The model log reports
`stopReason=toolCalls`; the App Server session records a native
`function_call`, executes it, sends `function_call_output`, and completes the
turn. This is evidence for the tested path, not a general claim about every
LM Studio tool or model.

LM Studio explicitly warned that it ignored `namespace` and `web_search` tool
types, `prompt_cache_key`, and `reasoning.encrypted_content`, and that it
replaced the developer role with a system role. The tested `exec_command`
round trip still succeeded. Therefore advertised `tool_use` does not qualify
the full Codex tool inventory or these unsupported request fields. The command
exited 0 and returned the Windows version, although the PowerShell wrapper
also emitted profile access/initialization warnings before that output.

## Repeat attempt and qualification evidence handling

The subsequent repeat reached the ten-minute limit while the selected model was
still generating. Its isolated journal contains no `function_call`, tool output,
or completed turn. During cleanup, the runner's immediate journal read received
an `IOException` stating that another process was using the file. A later
exclusive read succeeded after the App Server process had exited; the exact
lock owner was not captured, so the underlying Windows handle cause remains
unknown. This is a qualification-runner evidence-collection defect, not proof
that the provider or sandbox rejected a command on this attempt.

The runner now opens append-only session journals with concurrent-read sharing,
retries transient I/O failures for a bounded interval, and reports any remaining
read failure as a typed `sessionEvidenceReadFailure` while preserving the turn
observation. A deterministic test reproduced the exclusive-lock case (red
before the change, green after); the post-fix explicit-`unelevated` mock path
also passed. No Codex runtime source was changed.

## Structured diagnostic result

Completed tool items now arrive as typed `toolOutcome` objects with
`itemType`, `status`, `succeeded`, output/exit code, and a typed `failure`
object when applicable. A host-level turn failure is also typed with
`eventType`, `failure.type`, `failure.attributedTo`, and `failure.message`.
The diagnostic page renders that event directly instead of requiring log
reverse-engineering.

## Remaining blockers and decisions

Settled choices are not reopened. The concrete remaining blockers are:

1. Explain live-turn variance before claiming repeatability: two same-tuple
   runs completed in `275.9s` and `289.8s`, while two other attempts reached
   ten minutes (one recorded a successful tool result without final completion;
   the other recorded no tool call). The latest successful repeat confirms
   the narrow path can complete again, but does not explain the non-completions.
2. The visible WPF/WebView2 LM Studio live path now passes with the explicit
   sandbox selector. The `212.674s` result closes that specific click-through
   gap; it does not explain the two earlier ten-minute non-completions.
3. Qualify real Codex request budgeting/compaction and stale-capability invalidation;
   the `95%` effective context projection is now visible but is not a
   compaction qualification.
4. Perform the broader Phase 1B approval, containment/reparse-point,
   credential, restart/recovery, and cancellation matrix.
5. OpenRouter has a separate successful request-pinned qualification; its
   remaining limitations are recorded in
   [PHASE1B_OPENROUTER.md](PHASE1B_OPENROUTER.md).

The 2026-09-23 repeat is preserved in the current ignored
`artifacts/phase1a/lmstudio/qualification.json`. The prior file was preserved
as `D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-Repeat-20260923T051010\previous-qualified-run.json`;
the isolated repeat application root is
`D:\CODING\NeoBabylon-Data\Phase1B-LMStudio-Repeat-20260923T051010\App`.

No production UI expansion, packaging, self-scaffolding, remote repository,
runtime-source edit, system-wide installation, commit, or push was performed.
