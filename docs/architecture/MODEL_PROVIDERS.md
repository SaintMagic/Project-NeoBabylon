# Model providers

Status: **LM Studio has one successful narrow App Server run; OpenRouter has
one successful bounded WPF/App Server tool round trip. Phase 1B's route,
reliability, budgeting, recovery, and authority matrix remains open.**

## Accepted targets

- **LM Studio** — local inference target.
- **OpenRouter** — free hosted route target.

They are separate qualification targets, not a promise that either current
installation, model, route, or configuration is compatible. Ollama remains
useful upstream comparison evidence but is not the second initial target.

Phase 1A inspected the running LM Studio installation and recorded the exact
tuple in [the canonical capability record](../release/MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json)
and [the narrative qualification report](../release/PHASE1A_LMSTUDIO.md):
server `0.4.25.0`, endpoint `http://127.0.0.1:1234/v1`, loaded model
`phase1a-qwen3-14b`, variant `qwen/qwen3-14b@q4_k_m`, and effective context
`32768`. Tool use is advertised; reasoning controls and structured output are
`Unknown`. The exact record now feeds a stock Codex `model_catalog_json` entry,
so NeoBabylon no longer relies on native metadata fallback in the qualified
tuple. One isolated App Server run completed the ordinary tool call, output
continuation, and `turn/completed` in 275.9 seconds. A subsequent repeat reached
the ten-minute bound before a tool call; the runner's journal read then hit a
transient sharing error, now covered by a deterministic regression test.
Repeatability and slow-generation behavior remain open. LM Studio warned that it ignored
`namespace` and `web_search` tool types and several request fields; only the
tested `exec_command` path is qualified.

Martin selected **Nex-N2.5-Pro (free)** for the first OpenRouter qualification.
The current public model catalog reports `nex-agi/nex-n2.5-pro:free`, canonical
slug `nex-agi/nex-n2.5-pro-20260907`, 262144 advertised context, up to 235929
completion tokens, Qwen3 tokenizer, FP8 quantization, text+image input, text
output, tool/function calling, JSON-schema structured outputs, and reasoning
efforts `high`, `medium`, and `none` with `high` as the catalog default. The
model and endpoint metadata currently show zero prompt/completion price and
exactly one endpoint, provider Nex AGI, tag `nex-agi/fp8`. The model catalog
reports expiry `2026-09-25`; endpoint-specific expiry is Unknown. A
route-pinned qualification turn completed one ordinary `exec_command`
call/output continuation through App Server `0.155.1` with a narrow local
source patch. Its corrected session reported effective context `249036` from
provider-effective/catalog context `262144` at the single 95% Codex adapter
projection. See the
[authoritative OpenRouter capability record](../release/MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json)
and [Phase 1B OpenRouter result](../release/PHASE1B_OPENROUTER.md). The live
session does not independently report effective reasoning effort; a
deterministic request fixture observed the mapped default `high`.

The Phase 0 inspection found that stock Codex `0.155.1` did not expose
OpenRouter's optional `provider` routing object, whose fallback default is
enabled unless constrained. Phase 1B added a narrow, separately pinned source
patch that maps the configured full endpoint slug to
`provider.only=["nex-agi/fp8"]` and `allow_fallbacks=false` on HTTP and WebSocket
Responses requests, preserves it through remote thread config, and includes it
in WebSocket reuse comparisons. Focused serialization, validation, and config
survival tests pass; a live turn using that binary completed. OpenRouter's
post-run generation record attests Nex AGI/model/cost/streaming but does not
echo the endpoint tag or routing object. The public endpoint snapshot had one
exact candidate at qualification time. The patch is recorded in
[NB-DEC-006](../decisions/0006-openrouter-route-pin.md), remains uncommitted,
and does not broaden into a Codex rewrite.

## Pinned evidence

The Codex `rust-v0.155.1` provider model defines `WireApi::Responses`, points
at the Responses endpoint, and rejects `wire_api = "chat"` with an explicit
configuration error. Its provider metadata also carries retry and stream-idle
timeout settings. The pinned defaults include Ollama and LM Studio in
Responses mode.

This means “OpenAI-compatible” is not sufficient as a compatibility claim.
Each target must implement the request/response, streaming, tool, continuation,
and error semantics the pinned runtime actually uses.

## Capability/configuration contract

The host/UI should use one normalized record for the requested and effective
configuration. `ModelCapabilities` is a useful working name, not a mandate to
invent a parallel model manager when upstream structures can satisfy it.

| Information | Required distinction |
| --- | --- |
| Identity | Provider, endpoint, requested model ID, and actual reported identity where available |
| Context | Advertised/model-supported limit versus selected, loaded, and effective request limit |
| Output | Output limit/reservation and its effect on the shared request budget |
| Reasoning | Unsupported, supported with known semantics, documented default, explicitly configured, or unresolved |
| Reasoning controls | Actual accepted control type and values; never an invented universal list |
| Agent capability | Tool calling, tool-result continuation, schemas, streaming, and required modalities |
| Provenance | Source, version, retrieval time, uncertainty, and qualification evidence |
| Runtime mapping | How effective values reach Codex request budgeting, compaction, and error handling |

For the recorded LM Studio tuple, the first runtime mapping is deliberately
small and visible: `model_catalog_json` carries the exact slug/context and
explicitly empty reasoning levels, text-only modalities, `unified_exec`, and a
null auto-compaction limit. These are adapter choices derived from the
authoritative record, not provider claims. The catalog's 95% effective-context
projection is visible in the session state. Pinned Codex source inspection
shows that a null catalog `auto_compact_token_limit` does not disable Codex
auto-compaction: `ModelInfo::auto_compact_token_limit()` derives 90% of the
resolved context, capped by an explicit limit, and the default scope is
`Total`. For this tuple, that gives a source-derived threshold of `29491`,
separate from the `31129` usable-context cap. The live run confirmed the
effective context but did not exercise the budget trigger or compaction;
dynamic behavior remains unqualified. LM Studio's reported `parallel=4` is retained
in the authoritative record but not mapped to Codex; its semantics relative to
Codex concurrency are unverified, so it is not treated as a parallel tool-call
capability. Identity/context/fallback checks do not assert that every provider
metadata field has a Codex equivalent.

Discovery, load, and activation should follow this sequence:

```text
select provider
  → discover available models/catalog
  → identify exact target
  → resolve limits and reasoning semantics
  → choose and validate configuration
  → load/connect where applicable
  → verify effective runtime values
  → activate agent work
```

If a value becomes knowable only after loading, the final check occurs before
the first agent request. Do not guess from model names, family defaults,
self-identification prose, stale caches, or a successful tiny prompt. Unknown
optional capability can remain visible; unresolved critical context, reasoning,
tool, or accounting semantics block that configuration.

## Qualification matrix

| Target | Phase 0 conclusion | Required live proof |
| --- | --- | --- |
| LM Studio | Exact tuple and capability record observed; one catalog-backed tool call/output/turn completed under explicit Windows policy; two attempts reached ten minutes, including one repeat with no tool call; unsupported tool/field warnings and hardware/reasoning/structured-output gaps remain | Explain slow-generation variance and repeat reliability; qualify request budgeting/compaction, broader tool support, reasoning behavior, cancellation, errors, timeouts, and no fallback |
| OpenRouter | Public metadata observed; one Nex AGI FP8 endpoint (`nex-agi/fp8`); model catalog expiry 2026-09-25 and endpoint expiry Unknown; local runtime patch sends exact endpoint-only/no-fallback policy; two small live tool turns completed through the qualification path; corrected Codex context `249036`; generation metadata attests Nex AGI/model/zero cost/streaming | Patched-runtime visible WPF click path, provider token-counter reconciliation, repeatability, effective reasoning telemetry, request-budget/compaction behavior, recovery, broader tools, and full authority matrix; recheck metadata before any future request and stop rather than substitute |
| OpenAI paid endpoint | Not required by product direction | Only an explicit later user choice; never an implicit rescue path |

## Qualification sequence

1. Run a deterministic mock Responses server for request serialization,
   streamed text/reasoning events, tool-call arguments/results, termination,
   malformed responses, provider errors, retries, cancellation, and timeout
   behavior.
2. Select one real LM Studio or OpenRouter tuple for Phase 1A and run a
   disposable model/tool round trip. Prefer the target with the smaller
   unresolved integration surface after inspection; qualify the other target
   separately, and do not turn one pass into certification of both.
3. Measure first-token latency, steady token rate, context/compaction,
   cancellation, repeated-turn reliability, and slow-generation timeout
   behavior. The startup brief's 5–7 tokens/second guidance is a planning
   baseline, not a current measurement.
4. Record exact provider/server/model/route, requested versus reported values,
   configuration, failures, limits, and evidence before exposing the target as
   supported.

No provider/model/endpoint/reasoning/context/cost fallback is silent. Retries
must not duplicate side effects invisibly. A provider pass is a tuple:
`Codex runtime + provider build/route + model + configuration + hardware +
enabled feature set`, not a provider name.

## Open technical inputs

The LM Studio version, model/quantization, endpoint, advertised/effective
context, and advertised tool capability are verified for the recorded tuple.
Hardware/device identity, output-token limit, reasoning controls/semantics,
structured output, and provider-specific compaction semantics remain Unknown.
The stock catalog injection removes the NeoBabylon path's native metadata
fallback, but stale-capability invalidation and effective budgeting remain
unverified. OpenRouter server version and provider-hardware identity remain
Unknown. Its public model metadata is recorded, Codex effective context and
streamed function-call/output/continuation are observed, and request-level
exact-route/no-fallback behavior is implemented and tested in the local source
build. OpenRouter generation metadata attests the provider but not the endpoint
tag; its token counters disagree with the nonzero Codex journal counts.
Independently observed live reasoning effort, structured-output integration,
output-cap enforcement, and dynamic budget/compaction remain unverified. These
are evidence gaps, not reopened product decisions.
