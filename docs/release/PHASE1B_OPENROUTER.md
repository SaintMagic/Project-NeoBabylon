# Phase 1B OpenRouter qualification

Status: **The pinned OpenRouter route/no-fallback path and corrected context
projection now pass a deterministic mock followed by a live App Server tool
round trip. Phase 1B remains open for usage-accounting reconciliation, repeated
qualification, budgeting/compaction, recovery, authority, and host/UI checks.**

## Exact selected tuple

- Provider requested: OpenRouter, `https://openrouter.ai/api/v1`.
- Model requested and reported by the isolated Codex session:
  `nex-agi/nex-n2.5-pro:free`.
- OpenRouter public canonical slug: `nex-agi/nex-n2.5-pro-20260907`.
- Public route snapshot at `2026-09-23T00:44:15Z`: exactly one endpoint,
  provider Nex AGI, full tag `nex-agi/fp8`, FP8 quantization, and zero prompt /
  completion price. The model catalog reports expiry `2026-09-25`; the
  endpoint response has no endpoint-specific expiry, so endpoint expiry is
  **Unknown**.
- The route-pinned live turn used a focused local Codex source patch. Its
  Responses request carries `provider.only=["nex-agi/fp8"]` and
  `allow_fallbacks=false`; request-body and configuration-survival tests passed.
  OpenRouter generation metadata for both generations attests provider
  **Nex AGI**, model `nex-agi/nex-n2.5-pro-20260907:free`, zero cost,
  `is_byok=false`, streaming, and `api_type=completions`. The generation API
  does not echo the endpoint tag or request routing object, so the full tag is
  established by the pinned request implementation plus the one-endpoint
  preflight snapshot, not by post-hoc metadata alone.
- OpenRouter server version: **Unknown**; the public model/endpoint endpoints
  do not report a server build. Provider hardware is also **Unknown**.
- Martin states that the selected API key is restricted to free models. That
  restriction was not independently inspectable. The key was read from the
  local Pi auth store at launch, held only in host/App Server process memory,
  never written to the project or app data, and never printed.

The machine-readable source of provider metadata is the
[authoritative OpenRouter capability record](MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json).
Unknown values remain explicit. Provider claims, Codex effective values, and
single-run observations are kept distinct; the current live usage-counter
disagreement is recorded rather than normalized away.

## Runtime and isolated roots

- Pinned Codex App Server: `0.155.1`, upstream source revision
  `be2951ea34f0d295ed0becf97079f92fa5f6950e`, with the narrow route patch
  identified by SHA-256
  `e38ea940308a61bd56c0315b08141f6bd801075a0d79f0f5ad6d12324c74c1f5`.
- Binary SHA-256:
  `cb53784b951b42cb2925b71a14feaea61ae09154b69646fcf18e4903b1537187`.
- Product source root: `D:\CODING\NeoBabylon`; patched runtime source worktree:
  `D:\CODING\NBRT-RouteControl`.
- Corrected live application root:
  `D:\CODING\NeoBabylon-Data\Phase1B-OpenRouter-ContextCorrected-Live-20260923\Live`.
  Runtime state, isolated CodexHome, and fixture workspace are under this
  application root and outside the product source repository. The ordinary
  `%USERPROFILE%\.codex` root was not selected.
- Requested Windows sandbox: `unelevated`; the thread response reported
  `workspaceWrite` with network access disabled. This is a restricted-token
  result, not an unrestricted-host workaround.
- OpenRouter config used the exact selected endpoint/model, Responses wire
  protocol, the explicit `OPENROUTER_API_KEY` environment-key name, zero
  request/stream retries, and the exact provider-only / no-fallback route
  setting. The key was read from the local Pi auth store into the qualification
  process only; the value was never printed or persisted. The user's
  free-only key restriction is not independently inspectable.

## Test sequence and result

1. The current public model/endpoint metadata was fetched and validated before
   the live request. The exact `:free` model remained available with one listed
   endpoint, zero prompt/completion price, and a model expiry of 2026-09-25.
   Endpoint-specific expiry was absent and remains Unknown.
2. The deterministic loopback Responses fixture ran first and passed the
   `cmd.exe /d /c ver` function-call/output exchange. Because the fixture is
   not OpenRouter, its mock config intentionally omitted the live route field;
   it did not pretend a local HTTP server was Nex AGI. Its data is preserved
   under `D:\CODING\NeoBabylon-Data\Phase1B-OpenRouter-Context-MockRetry-20260923\Mock\Data`.
3. The qualification runner then used the patched pinned App Server and exact
   OpenRouter model. It completed one ordinary `exec_command` call for
   `cmd.exe /d /c ver`; the isolated journal pairs the call and output by
   `call_id`, records process exit code 0 and Windows version `10.0.26200.9457`,
   and App Server emitted `turn/completed`. This latest route-pinned run used
   the qualification host/core path, not the visible WPF window.
4. The isolated session journal is
   `D:\CODING\NeoBabylon-Data\Phase1B-OpenRouter-ContextCorrected-Live-20260923\Live\Data\CodexHome\sessions\2026\09\23\rollout-2026-09-23T02-57-39-01a0cbc4-ad5c-71b2-88ca-a74ded23dcc7.jsonl`.
   It reports App Server `0.155.1`, `model_provider=openrouter`, the selected
   model, `model_context_window=249036`, the exact function call/output pair,
   and no metadata fallback warning.
5. Read-only OpenRouter generation lookups for
   `gen-1790125061-aFqRacmN8snPhT26jA1y` and
   `gen-1790125070-lGuHWtIpRUTH82J3vEPr` both attest Nex AGI,
   `nex-agi/nex-n2.5-pro-20260907:free`, zero total cost, `is_byok=false`,
   streaming, and `api_type=completions`. They do not return the full endpoint
   tag or request routing object. The official [provider-routing guide](https://openrouter.ai/docs/guides/routing/provider-selection)
   documents exact endpoint slugs and fallback control; the local pinned
   runtime tests verify the request serialization. The read-only generation
   endpoint is documented at [Get generation metadata](https://openrouter.ai/docs/api/api-reference/generations/get-generation).
6. The OpenRouter generation API returned zero token/usage counters for both
   generations, while the Codex session journal recorded 12,565 input tokens,
   230 output tokens, and 69 reasoning-output tokens across them. This usage
   accounting mismatch remains unresolved; the provider values are not treated
   as verified token counts. Both sources report zero cost for the turn.

## 2026-09-23 pre-containment-guard WPF run and UI history verification

A later credentialed run used the product's then-current runtime lock and the
visible WPF/WebView2 shell. It is distinct from the earlier qualification
runner record above. It does not qualify command execution on the later
fail-closed binary pinned in `runtime/runtime-lock.json`:

- App Server `0.155.1`, source revision
  `be2951ea34f0d295ed0becf97079f92fa5f6950e`, binary SHA-256
  `cb53784b951b42cb2925b71a14feaea61ae09154b69646fcf18e4903b1537187`, and
  source patch SHA-256
  `e38ea940308a61bd56c0315b08141f6bd801075a0d79f0f5ad6d12324c74c1f5`.
- Application root:
  `D:\CODING\NeoBabylon-Data\Phase2-UI-Live-20260923\App`; Data,
  `CodexHome`, and fixture workspace remained under this app root, separate
  from `D:\CODING\NeoBabylon` and the ordinary Codex home. The runtime
  snapshot records `ordinaryCodexRootUsed=false`.
- Selected tuple: OpenRouter / `nex-agi/nex-n2.5-pro:free` / requested route
  `nex-agi/fp8`. The WPF host completed exactly `cmd.exe /d /c ver`; App Server
  journal evidence records exit code 0 and Windows
  `10.0.26200.9457`. The UI showed attributed success and 92 streamed App
  Server events.
- Codex session context was `249036` against provider capability context
  `262144`; the UI deliberately keeps effective reasoning **Unknown** without
  live telemetry. Two read-only generation records,
  `gen-1790130176-ByDpeKxpIWRxxpazyGyM` and
  `gen-1790130192-Dwfs4kEFfxP4Aa6Mt8bn`, report provider Nex AGI, canonical
  model `nex-agi/nex-n2.5-pro-20260907:free`, and zero cost. They do not return
  the endpoint tag. The Codex journal reports 12,596 input, 94 output, and 47
  reasoning-output tokens while these generation records report zero token
  counters; do not reconcile or present this as verified provider usage.
- The route-control runtime serialization regression verifies
  `provider.only=["nex-agi/fp8"]` and `allow_fallbacks=false`. The generation
  response does not independently attest the selected endpoint, so route
  intent/control and post-hoc provider/model attribution remain distinct
  evidence.

The later UI-only smoke sent no inference. It listed the saved OpenRouter
thread, restored its two user/assistant text messages, used “New task” without
deleting history, and restored the same thread again. It rejected a mismatched
LM Studio selection, allowed local resume without a provider key, then failed a
new turn locally before any App Server turn event because the key was absent.
At 1464×901 the UI had no viewport overflow, external browser-resource
requests, or console errors. Machine-readable UI evidence is in
`D:\CODING\NeoBabylon-Data\Phase2-UI-Live-20260923\thread-resume-ui-smoke.json`
and `thread-history-ui-smoke.json`.

## Effective capability mapping

- Advertised context: `262144` from the OpenRouter public catalog.
- Provider-effective context input: `262144` from the exact free endpoint's
  `context_length`; this is the authoritative capability record value.
- Codex catalog context: `262144`; the generated entry carries
  `effective_context_window_percent=95`.
- Effective session context: `249036`, observed in the corrected live App
  Server session (`262144 × 95%`, truncated by the pinned runtime).
- A first route-pinned diagnostic run exposed an input-recording mistake: the
  capability record had fed its prior Codex-derived `249036` back as the model
  context, so Codex applied 95% again and reported `236584`. The authoritative
  provider record now holds `262144`; the later live run confirms a single
  projection to `249036`. The earlier run is retained as diagnostic evidence,
  not counted as the corrected effective-context pass.
- Reasoning: provider metadata advertises `high`, `medium`, and `none`, with
  `high` default. Those values reached the generated Codex catalog. The
  deterministic Responses fixture observed `reasoning.effort=high`; the live
  session journal does not independently record the effective reasoning
  request, so live effort remains **not independently observed**.
- Tools and continuation: one live ordinary function call, successful output,
  and assistant continuation were observed. A separate bounded live
  function-form `apply_patch` test on the exact NEX free-model route changed
  only the isolated `tracked.txt`, returned the matching function output, and
  retained an App Server file-change review. This qualifies that patch tool
  for this exact model and route only; other tool types are not qualified.
- Streaming: App Server text/reasoning deltas and terminal completion were
  visible in the WPF event stream for this run.
- Structured output: provider metadata advertises JSON-schema structured
  output; no live structured-output request was sent, and Codex effective
  structured-output support is not established.
- Advertised maximum completion: `235929`. It is captured in the capability
  record but is not currently mapped as an enforced Codex output-token cap.

The corrected run has no mismatch among the provider-effective context input,
Codex catalog context, and App Server session context, and no model metadata
fallback warning. The generation responses attest the provider and model, but
do not independently echo the exact endpoint tag. Live reasoning-effort
telemetry, effective structured-output support, enforcement of the advertised
output-token cap, and reconciliation of provider token counters remain open.

## Typed failure evidence

The C: `LocalAppData\NeoBabylon` root still fails closed under the current
Codex-hosted Windows execution context. A fresh deterministic mock run on
2026-09-23 again failed before the OS command launched with the pinned
restricted-token error that split writable root sets could not be enforced;
the result is preserved at
`artifacts/phase1b/openrouter/qualification-cdrive-current-context-fail.json`.
The returned session/config provenance used the packaged
`C:\Users\Martin\AppData\Local\Packages\OpenAI.Codex_...\LocalCache\Local\NeoBabylon`
name, while the host supplied the corresponding
`C:\Users\Martin\AppData\Local\NeoBabylon` path. `fsutil` reported the same
NTFS file IDs for corresponding data paths and no reparse point at either
`NeoBabylon` directory. This supports (but does not prove) a path-alias mismatch
between legacy and split policy root sets; the pinned error does not expose the
compared sets. The result is specific to this execution context; behavior in a
standalone installed host remains unverified.

The earlier preserved C: sandbox denial also exposed the host's
notification-only diagnostic blind spot: App Server did not emit a typed
`item/completed` failure. The new bounded reader was run against that exact
isolated journal and produced a typed `toolExecution` failure attributed to
Codex App Server, with raw tool output omitted. A deterministic journal
regression verifies success, failure, Unknown, call/output pairing, and no
raw-output leakage.

The D: application root passed both the deterministic mock and the one live
host tool call; the fresh D: mock pass is in the canonical qualification
artifact. No broader permission change was made to mask the C: mismatch.

## Remaining Phase 1B blockers

- The exact endpoint/no-fallback behavior is implemented by the local patched
  runtime, covered by source serialization/config-survival tests, and has
  passed in a visible WPF run. OpenRouter's generation record does not echo the
  endpoint tag, so post-hoc endpoint attribution is still absent. The model
  catalog expiry is `2026-09-25`; endpoint expiry is Unknown. Recheck before
  any future request and stop rather than substitute.
- Reconcile the OpenRouter generation API's zero token counters with the
  nonzero Codex session counters. Cost is zero in both generation records,
  while token-usage attribution remains Unknown/inconsistent.
- The C: `LocalAppData` root produces a Windows restricted-token root-set
  mismatch in the current Codex-hosted execution context, whereas the D:
  application data root passes. A packaged LocalCache path alias is a
  source-supported hypothesis, not proven root-set telemetry. Verify the
  intended standalone install context; do not mask this with broader access.
- Two small OpenRouter live turns do not establish repeatability, slow-model
  behavior, cancellation, actual request-budget enforcement, auto-compaction,
  stale-capability invalidation, or the provider's effective reasoning level.
- The UI exercised `thread/resume` with the isolated local history; fork and
  restart/renderer-crash recovery are still open. The trusted host now sends a
  text-only user/assistant projection to React, strips tool records and
  attachment payloads, and applies a tested 120,000-character aggregate cap.
  The latest `1464×901` WPF/WebView2 smoke confirmed no tool records in the
  bridge response, no console errors, no external browser requests, and no
  viewport overflow. The rendered oversized-history notice has not yet been
  exercised. The remaining Phase 1B plan matrix includes live
  interruption/no-zombie behavior, approval variants, Windows
  parent/sibling/outside/junction containment, malformed/unknown-message
  recovery. The stable/experimental-off boundary passed on 2026-09-24 using
  the locked binary's stable path plus nine matching-source integration tests;
  see [VERIFICATION.md](VERIFICATION.md).
- LM Studio has two same-tuple completed App Server runs (`275.9s` and
  `289.8s`) plus two earlier ten-minute non-completions. Its visible WPF live
  click-through also passed on 2026-09-23 in `212.674s`; repeat variance,
  broader tool semantics, and live budget/compaction remain unresolved. The
  second-provider result does not close those gaps.

This record does not authorize packaging, self-scaffolding, or publication.
The first Phase 2 shell slice is now implemented under Martin's explicit
continuation authorization; the complete Phase 2 acceptance gate remains open.

## 2026-09-24 current public preflight

The exact public model alias remains `nex-agi/nex-n2.5-pro:free` and returned
one listed Nex AGI endpoint, `nex-agi/fp8`, with context `262144` and zero
prompt/completion prices. Its raw endpoint API `status` value was `-2`. The
official list-endpoints reference does not define the meaning of that value;
availability is therefore **Unknown**, not a pass. The model page continues
to label the alias Free, but that does not establish health for this exact
provider endpoint. Catalog expiry is `2026-09-25`; endpoint-specific expiry is
still Unknown.

At the read-only preflight checkpoint no inference had yet been sent. The
process environment had no key, and the older Pi-stored credential was not
substituted. A subsequent bounded live qualification using Martin's supplied
free-only key is recorded below. Sources: [OpenRouter Nex-N2.5-Pro (free)
model page](https://openrouter.ai/nex-agi/nex-n2.5-pro%3Afree) and
[OpenRouter model-endpoints API reference](https://openrouter.ai/docs/api/api-reference/endpoints/list-endpoints).

### 2026-09-24 exact free-route function-tool retry

The product host harness made one bounded live function-form
`apply_patch` round trip on the selected free alias after its deterministic
mock and fresh route preflight passed. It ran under the same locked App Server
identity and a new isolated application root at
`D:\CODING\NeoBabylon-Data\Phase1B-NEX-Free-Retry-20260924`. The effective
Codex context was `249036` from provider-effective context `262144` and the
locked runtime's 95% projection. Only isolated `Workspace/tracked.txt`
changed (`before` → `after`); the turn completed, produced three attributed
diff events, and saved one review.

OpenRouter generation IDs
`gen-1790219773-7Yp5QQTDULNiZI3a0it9` and
`gen-1790219786-pq4zy200aoSHTlFHJB3E` both report Nex AGI,
`nex-agi/nex-n2.5-pro-20260907:free`, streaming, `is_byok=false`, and zero
total cost. Their prompt/completion usage counters are zero; the Codex journal
records 12,117 input and 133 output tokens, so usage attribution remains
inconsistent. The generation responses do not echo the provider endpoint tag;
the request-side endpoint pin/no-fallback settings and one-endpoint preflight
remain the evidence for `nex-agi/fp8`. The preflight's raw `status=-2` remains
semantically Unknown; the successful turn proves reachability for this one
request only. The user's free-only key restriction is not independently
inspectable.

The machine capability record's newest attestation is in
[MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json](MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json).
No WPF interaction was part of this run. It does not qualify a paid route,
live 429/unavailability handling, repeatability, effective reasoning effort,
structured-output execution, context-triggered compaction, or other tool
types.
