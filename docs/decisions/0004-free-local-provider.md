# NB-DEC-004 — Initial provider qualification targets

Status: **Accepted targets; exact qualification tuples remain open.**
Date: 2026-09-22.

## Decision

The initial provider targets are:

1. **LM Studio** for local inference.
2. **OpenRouter** for a selected free hosted route.

Both must be qualified independently. Ollama remains observed upstream
comparison evidence but is not the second required initial target. No model
slug, server version, route, quantization, hardware assumption, credential,
or endpoint is invented by this record.

OpenRouter route and fallback control is an unresolved stock-runtime question.
Phase 0 inspection records no proof that the pinned 0.155.1 App Server path
can enforce a requested per-request provider route/fallback policy. The
accepted no-silent-fallback rule therefore remains a qualification gate, not
a claim that stock Codex already satisfies it. An account/preset policy or a
focused runtime/client adaptation is a proposal to evaluate only if the
qualification exposes that incompatibility; no slug, route, or workaround is
to be invented during documentation reconciliation.

## Required capability contract

Before activation, NeoBabylon must resolve and record the effective provider,
endpoint, model ID, context/request budget, output allowance, reasoning
semantics, tool-calling and continuation support, streaming behavior, timeout
policy, cancellation, retry/error behavior, and relevant modalities. Each
value must identify its source, version/retrieval time, uncertainty, and how it
maps into the pinned Codex request/budgeting/compaction path.

Unknown optional capability may remain visible without blocking an otherwise
valid text/tool workflow. Missing or contradictory critical context, reasoning,
tool, or accounting semantics blocks that configuration. Unsupported,
unknown, documented-default, and explicitly configured reasoning states remain
distinct.

## Qualification rules

- “OpenAI-compatible” or a successful greeting is not qualification.
- Run deterministic mock Responses serialization/stream/tool/error tests first.
- Run a real disposable model/tool round trip for LM Studio and OpenRouter
  separately, recording requested versus reported identity.
- No silent provider, model, endpoint, reasoning, context, or paid/free-cost
  fallback. Retries may preserve accepted semantics but must not duplicate
  side effects invisibly.
- Phase 1A verifies capability/configuration wiring into the pinned Codex
  effective model state; actual context-budget and auto-compaction behavior,
  stale-capability invalidation, and model-switch regressions are Phase 1B
  evidence.
- Start local qualification without credentials when the selected endpoint
  permits it; OpenRouter credentials remain host/runtime-delivered and never
  enter renderer state, prompts, source, candidate manifests, or routine logs.

## Phase 1B implementation evidence — 2026-09-23

These accepted provider and no-fallback requirements are unchanged. The stock
Codex `0.155.1` request path lacked an OpenRouter endpoint-routing field, so the
qualification work added the focused source adaptation recorded in
[NB-DEC-006](0006-openrouter-route-pin.md): the selected full endpoint slug is
carried as `provider.only`, with `allow_fallbacks=false`, through HTTP and
WebSocket Responses requests and remote thread config. It remains based on
upstream revision `be2951ea34f0d295ed0becf97079f92fa5f6950e`; runtime source and
binary identity are in `runtime/runtime-lock.json`.

Focused source serialization/config-survival checks passed, and two small live
tool turns using that build completed. OpenRouter generation metadata attests
Nex AGI, the selected free model, zero cost, and streaming, but does not echo
the endpoint tag or routing object. The model catalog reports expiry
`2026-09-25`; endpoint-specific expiry is Unknown. The visible WPF path has not
yet been exercised against the patched binary, and provider-generation token
counters conflict with nonzero Codex session counters. These remain
qualification gaps, not changes to the accepted provider decision.

## Remaining selection inputs

Phase 1A must select one real LM Studio or OpenRouter tuple from the available
machine/environment before its live gate can pass. The second provider is a
separate qualification target. The exact server versions, model/route,
hardware, endpoint, credentials, and data-handling terms remain technical
inputs, not reopened product choices.
