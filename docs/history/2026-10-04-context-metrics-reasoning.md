# Conversation observability and reasoning controls — 2026-10-04

## Accepted user request

Martin requested the missing Context/Live metrics panel shown in his screenshot,
reasoning selection in the message bar, and PiLOT as inspiration. Existing
Codex-first architecture, exact model/capability authority, black/gray UI,
conversation/draft preservation, isolation, full-access/never tool authority
and deferred qualification/packaging decisions remain unchanged. PiLOT is
inspected read-only as a visual/interaction reference, not a code/config donor.

## Bounded implementation design

- Add context usage and live metrics at the top of existing collapsible Task
  details; add reasoning choice alongside the model in the centered composer.
- Use the selected authoritative capability record's exact supported reasoning
  controls only. The initial implementation design used the previous records
  (GLM/Kimi low/high/max, others Unknown). Fresh discovery below supersedes that
  stale observation for Space Bunny and Ling. Unknown controls remain visible
  but disabled, with no effort override. Advertised controls, requested settings,
  Codex-observed state and provider-observed state are distinct.
- Send validated explicit reasoning through the pinned `turn/start.effort`
  field. Its documented sticky semantics must not be misrepresented as a
  nullable reset-to-provider-default operation.
- Expose read-only, exact-thread usage through a typed bridge operation and
  bounded isolated Codex-journal/current-notification evidence. Current context
  uses latest request usage, not cumulative session spend. Runtime-observed
  windows take precedence; provider-advertised fallbacks are labelled as such.
- Show runtime-reported input/output/cache counters. Missing cost or timing is
  Unknown, not zero; free catalog pricing is not a billing receipt. Rates must
  identify whether they are measured turn averages including waits/tools or
  estimates, never present character counts as exact tokens. Keep history
  bounded to 90 seconds and reject stale/duplicate identity-mismatched events.
- `Compact context` uses the pinned App Server's existing `thread/compact/start`
  via a named, identity-bound host operation. Only idle, execution-eligible
  threads without continuing commands may request it. An empty queued RPC
  response is not completion; only a verified terminal event permits success.
  No local transcript deletion, generic RPC, shellCommand fallback, automatic
  compaction, experimental API opt-in, runtime patch or live test is authorized
  by this implementation check.

## Inspected evidence

- PiLOT `src/components/Inspector.tsx` (Context/metrics/compaction layout),
  read-only at `E:\pi-windows-x64\PROJECTS\PiLOT`.
- Pinned Codex source
  `.local/Runtime/NeoBabylon-Runtime/codex-rs/app-server-protocol/src/protocol/v2/thread.rs`:
  `ThreadCompactStartParams`, `ThreadTokenUsageUpdatedNotification`,
  `ThreadTokenUsage`, `TokenUsageBreakdown`. Usage cost metadata exists on an
  internal-only notification; this is not authority to enable it globally.
- The same protocol's `turn.rs`: `TurnStartParams.effort` applies to this and
  subsequent turns. Current authoritative capability JSON files supply exact
  known/unknown model controls; no GPT-family defaults are introduced.

### Fresh provider discovery and PiLOT interpretation

Martin supplied PiLOT's Space Bunny effort menu and said Ling reports On/Off.
Public OpenRouter model and endpoint discovery on 2026-10-04 at
08:59:53–54 UTC confirms:

- `stealth/space-bunny-alpha`: reasoning is mandatory; exact efforts in provider
  order are `max`, `xhigh`, `high`, `medium`, `low`, with default `max`. No Off
  control is appropriate. Its observed endpoint remains Stealth, context
  1,000,000, maximum completion 524,288. The listing reports expiration date
  2026-10-05; continued availability is not guaranteed.
- `inclusionai/ling-3.1-flash`: reasoning is optional and enabled by default;
  `supported_efforts` is omitted. This is an On/Off capability, not an advertised
  Medium/High/Max model. Novita is the sole observed route; context 262,144,
  maximum completion 32,768. Model/quant/server implementation remain as
  previously recorded; quantization and effective context remain Unknown.

The official [OpenRouter reasoning guide](https://openrouter.ai/docs/guides/best-practices/reasoning-tokens)
distinguishes an omitted effort list from `null`, explains mandatory reasoning,
and identifies `default_enabled` as the Boolean default. This discovery is
provider metadata, not proof of a live request's effective behavior.

Read-only inspection of PiLOT's
`electron/services/model-capabilities.ts` confirms its live model-list lookup.
It presents omitted effort lists as On/Off, then maps On to its `medium` RPC
setting and Off to `off`. That compatibility mapping is not a native Ling effort
list or provider observation. NeoBabylon must not relabel it as such.

The locked Codex `turn/start.effort` and Responses `Reasoning` type expose effort,
not Boolean `reasoning.enabled`. This increment will retain Known Ling Boolean
metadata without inventing a callable effort mapping; the UI must identify this
specific transport limitation. Exact, versioned records must preserve older
saved-chat capability identities and require an explicit adoption/switch for
new evidence, not rewrite original bindings or historical attribution.

Two bounded direct Responses compatibility probes, using the existing protected
app-private OpenRouter credential, exact Ling/Novita route, disabled fallback,
128 output-token cap and an ordinary `Return exactly OK.` prompt, tested proposed
`none`/`medium` aliases. Both returned HTTP 429 at 09:18:34–35 UTC. No response,
reasoning effect, served route, tool call or Codex round trip was qualified.
No retries or substitute models were used. This does not establish that either
alias is supported or unsupported; native effort support remains unadvertised.
Full raw public discovery and redacted probe receipts are in the local evidence
directory below. No credential was printed or embedded in this record.

## Implementation and verified scope

Two Luna workers implemented non-overlapping Host and UI scopes; the controller
reviewed source/integration paths and verified the assembled app. PiLOT source,
shared configuration and the runtime checkout were not changed. The old Space
Bunny, Ling and NEX records and capability identity serialization are unchanged.
New `_20261004.json` records retain the newer reasoning evidence without
rewriting historical identities.

Current catalog selection offers one current version per exact tuple. Passive
saved-chat resume resolves the latest explicitly adopted hash, or the original
hash if no adoption exists. A missing latest hash cannot fall back to the old
original. Explicit adoption of a newer same-tuple record uses the existing
verified same-thread restart/resume path and preserves creation identity,
thread ID, transcript and draft. The returned frozen record, not today's picker
metadata, controls resumed reasoning choices.

The context and metrics panel uses last request totals for current occupancy;
cumulative totals are never relabelled as context usage. Session-journal reads
are bounded to an identity-validated 256 KiB initial header and 4 MiB recent
tail, with absolute byte provenance. Large valid journals remain readable;
missing recent model/turn context leaves window/effort Unknown while attributable
lifetime counts can survive. Missing cache-write input is null, not invented
zero. Cost remains Unknown without a billing receipt. Measured throughput is
explicitly a completed-turn average including waiting/tools, not provider
generation speed. The 90-second history is bounded and has honest gaps.

Compaction reserves the existing exclusive operation guard and verifies exact
selected thread/client/workspace/capability, credentials and no continuing
commands. Only stable `thread/compact/start` is sent. Queued reply or item
completion alone cannot establish success. Matching item plus terminal turn
are required; failure and uncertainty are typed. An unverified residual
notification backlog fails before dispatch, reports Host attribution and
`requestSent:false`, settles waiting ready promises and releases the unsent
reservation. Sent-but-Unknown retains its guard until an exact late terminal
event. No generic RPC, experimental opt-in, runtime patch or authority widening.

### Checks actually performed

- Host red/green fixtures: `--probe-context-reasoning-host` and
  `--probe-capability-versions`; focused Ling selection and saved-thread Rename
  regressions also passed. The adoption fixture used the actual locked App
  Server and one deterministic localhost assistant response solely to seed
  persistence; no real provider inference or tool execution.
- Controller review caught and workers fixed absent cache-write counters being
  projected as zero, long journals being rejected solely by size, residual
  compaction notifications and a pre-start rejection leaving a ready waiter
  unresolved. Final affected fixture and Host Release build reruns passed.
- All 162 UI tests passed, with zero failures/skips. Actual TypeScript project
  checking (`tsc -b --pretty false`) passed. The UI worker separately passed 43
  focused checks and a 15-check rendered synthetic fixture for stale usage,
  same-tuple adoption, reasoning controls, compaction fencing and preservation.
- The controller built the actual development Host under `.local/App/Build`
  and the source-mapped UI under `ui/diagnostic/dist`. Host Release build had
  zero warnings/errors. UI build passed with existing dependency directive and
  large-chunk warnings; no dependencies were installed.
- Isolated assembled WPF/WebView2 QA ran that exact development binary with
  application root `.local/Lab/Runs/Context-Reasoning-20261004/App`, not the
  normal app Data or ordinary Codex root. Playwright CDP was the fallback for
  unavailable browser control. Native checks confirmed one current entry per
  tuple, Space Bunny's exact five efforts/default Max/no Off, Ling's disabled
  On/Off and precise explanation, blank-task Unknown usage/cost, and disabled
  compaction for a blank task. No turn/inference request was sent.
- Five native desktop/narrow/details geometry cases had 0 px composer center
  error, no document/toolbar overflow and no console/page errors. Details were
  genuinely visible at 1440 and 1120 px; existing CSS hides the panel at 820 px.
  Native screenshots were visually inspected.
- The normal development app was verified idle with no continuing command
  child, gracefully closed, rebuilt and reopened (PID 100596 at
  2026-10-04T10:16:34 UTC). Its `.local/App/Data` was not reset. QA's exact owned
  host was gracefully closed separately. No forced process-name termination.

Development Host executable SHA-256:
`24c64a973bdbe8aacc1d530e0c3236d9715f38bef4b6aff4d346673326a2c607`.
Host DLL: `18fadc0ffe36d246c5060f3c1c9c3a3bddcbf0d51f4c2fb2170edac055e3b42d`.
Core DLL: `4fa929420728583d0a497a3e54db88c29f624b91739a67aba1b56fc91216b72f`.
The unchanged App Server is `0.155.1`, source HEAD
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, locked binary SHA-256
`a0c3ebdc8d1d9f5b56327f5ea6d1502ee5a0b50fa174d50743f8c7a17ff54386`.
These are development-artifact checks, not packaging or release qualification.

### Remaining gaps

Ling's actual advertised Boolean capability is Known, but no qualified Boolean
override exists in the pinned Codex wire path; its control remains disabled.
The two 429 probes do not prove aliases equivalent or incompatible. Space
Bunny effort effects, actual provider generation rates/cost and live manual
compaction were not qualified. A resumed chat may have attributable lifetime
counts but Unknown current context/effort until fresh matching evidence; neither
the selected setting nor old-model evidence is presented as observed state.
The advertised Space Bunny expiry remains a provider-availability concern,
not permission to substitute another model. Broad acceptance, packaging,
containment and generated-tool callable qualification remain deferred/open.

Evidence is retained under `.local/Lab/Runs/Context-Reasoning-20261004/`:
`host-report.md`, `ui-report.md` (worker-scope pre-native report),
`ui/fixture-receipt.json`, `native-context-receipt.json`, native screenshots,
public discovery and the direct Ling probe receipts. Controller receipt and
snapshot delta supply the final artifact identities and exact changed files.

## Exact product/source files changed in this increment

Documentation/capability records:

- `docs/product/STATUS.md`
- `docs/release/VERIFICATION.md`
- `docs/history/2026-10-04-context-metrics-reasoning.md` (new)
- `docs/release/MODEL_CAPABILITY_OPENROUTER_STEALTH_SPACE_BUNNY_ALPHA_20261004.json` (new)
- `docs/release/MODEL_CAPABILITY_OPENROUTER_LING_3_1_FLASH_20261004.json` (new)

Host, tests and exact default-path updates:

- `host/NeoBabylon.Core/ActiveCapabilitySelection.cs`
- `host/NeoBabylon.Core/AppServerProtocol.cs`
- `host/NeoBabylon.Core/AppServerClient.cs`
- `host/NeoBabylon.Core/CapabilityRecordPathResolver.cs`
- `host/NeoBabylon.Core/CapabilitySwitchSafety.cs`
- `host/NeoBabylon.Core/CodexModelCatalogBuilder.cs`
- `host/NeoBabylon.Core/ManualCompactionTracker.cs` (new)
- `host/NeoBabylon.Core/SessionJournalToolEvidenceReader.cs`
- `host/NeoBabylon.Core/ThreadCapabilityBindingStore.cs`
- `host/NeoBabylon.Core/ThreadUsageEvidence.cs` (new)
- `host/NeoBabylon.Host/MainWindow.xaml.cs`
- `host/NeoBabylon.Host/RuntimeSupervisor.cs`
- `tests/host/Program.cs`
- `tests/host/ContextReasoningChecks.cs` (new)
- `tests/host/CapabilityVersionChecks.cs` (new)
- `tools/Launch-Development.ps1` (current Space Bunny record default only)
- `tools/Phase1AQualification/Program.cs` (current Ling record default only)

UI:

- `ui/diagnostic/src/App.tsx`
- `ui/diagnostic/src/app.css`
- `ui/diagnostic/src/bridge.ts`
- `ui/diagnostic/src/model-switch.mjs`
- `ui/diagnostic/src/model-switch.d.mts`
- `ui/diagnostic/tests/model-switch.test.mjs`
- `ui/diagnostic/src/context-compaction.mjs` (new)
- `ui/diagnostic/src/context-compaction.d.mts` (new)
- `ui/diagnostic/src/reasoning-selection.mjs` (new)
- `ui/diagnostic/src/reasoning-selection.d.mts` (new)
- `ui/diagnostic/src/reasoning-selector.tsx` (new)
- `ui/diagnostic/src/task-telemetry.mjs` (new)
- `ui/diagnostic/src/task-telemetry.d.mts` (new)
- `ui/diagnostic/src/use-task-telemetry.ts` (new)
- `ui/diagnostic/src/task-telemetry-panel.tsx` (new)
- `ui/diagnostic/tests/context-compaction.test.mjs` (new)
- `ui/diagnostic/tests/reasoning-selection.test.mjs` (new)
- `ui/diagnostic/tests/task-telemetry.test.mjs` (new)

This list excludes generated build output, local-only test harnesses/receipts,
isolated synthetic App Data and snapshots, all retained under ignored `.local`.
Earlier Ling catalog replacement/composer centering has its own separate dated
record; its overlapping paths are not treated as a Git baseline.

The verified source snapshot immediately before this increment is
`.local/Lab/Runs/Finishing-20261003/data/captures/20261004T083907Z-d223f5a17cac4f63a840628230dbfcd5`.
