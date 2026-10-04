# NB-DEC-006 — Request-level OpenRouter endpoint pinning

Status: **Implemented for Phase 1B qualification; independent review and the
patched-runtime visible WPF click path remain open.**  
Date: 2026-09-23.

## Accepted requirement

An OpenRouter qualification must not silently change provider, endpoint,
model, or fallback behavior. A current one-endpoint catalog snapshot and
post-request provider attribution are evidence for a run, not a durable
request-level route guarantee. Unknown or changed route metadata must stop
before inference rather than select a substitute.

This requirement does not change the accepted initial target in
[NB-DEC-004](0004-free-local-provider.md), and does not authorize additional
providers, models, or paid routes.

## Implemented qualification choice

Stock Codex App Server `0.155.1` did not expose OpenRouter's request-level
provider routing object on its Responses path. The qualification runtime adds
an optional `openrouter_provider_endpoint` provider setting and maps the
observed full endpoint slug to:

```json
{
  "provider": {
    "only": ["nex-agi/fp8"],
    "allow_fallbacks": false
  }
}
```

The field is validated against the official OpenRouter base URL and the
Responses wire API, carried by HTTP and WebSocket request types, preserved
through remote thread configuration, and included in WebSocket reuse
comparisons so a route change cannot reuse stale connection properties.
Unknown routes fail closed in NeoBabylon's live config builder. The
deterministic local mock has an explicit test-only mode that accepts only an
HTTP loopback URL and omits the live provider route; it cannot masquerade as
the real endpoint.

This is a narrow runtime/source adaptation, not a new provider abstraction or
a general routing UI. The product capability record remains the authoritative
source for the selected endpoint. Model expiry and endpoint expiry are
separate: the current model catalog reports `2026-09-25`; the endpoint API
does not report endpoint-specific expiry, which remains **Unknown**.

## Evidence and identity

- Upstream source ref/revision: `rust-v0.155.1`,
  `be2951ea34f0d295ed0becf97079f92fa5f6950e`.
- Patched source checkout: `D:\CODING\NBRT-RouteControl`.
- App Server version: `0.155.1`.
- Binary SHA-256:
  `cb53784b951b42cb2925b71a14feaea61ae09154b69646fcf18e4903b1537187`.
- Runtime patch/worktree-diff SHA-256:
  `e38ea940308a61bd56c0315b08141f6bd801075a0d79f0f5ad6d12324c74c1f5`.
- Runtime-lock identity and executable path are recorded in
  [`runtime/runtime-lock.json`](../../runtime/runtime-lock.json).
- Focused tests cover configuration validation/round-trip, HTTP request JSON,
  WebSocket request JSON and route-change invalidation, and remote thread
  configuration preservation. The request test asserts the exact endpoint
  slug and disabled fallbacks.
- A deterministic Responses fixture passed before the corrected live run.
  The live session completed one ordinary `exec_command` call/output/assistant
  continuation with exit code 0; its isolated session reports effective
  context `249036`, with no native metadata fallback warning.
- OpenRouter generation metadata for both provider generations attests Nex
  AGI, model `nex-agi/nex-n2.5-pro-20260907:free`, zero cost, and streaming.
  It does not return the endpoint tag or the submitted routing object. The
  exact tag therefore rests on source/request tests plus the pre-request
  single-endpoint snapshot, not on an echoed server attestation.
- The OpenRouter generation API reported zero token counters while the Codex
  session journal reported nonzero usage. This accounting mismatch is open.
- The live route-pinned run used the qualification host/core path, not the
  visible WPF window. The earlier visible WPF OpenRouter run used the
  unpatched stock binary and does not prove the patched UI path.

The machine-readable capability record and detailed run data are in
[`MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json`](../release/MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json)
and [`PHASE1B_OPENROUTER.md`](../release/PHASE1B_OPENROUTER.md). OpenRouter's
[provider-routing documentation](https://openrouter.ai/docs/guides/routing/provider-selection)
describes full endpoint slugs and fallback controls.

## Alternatives not selected

- A catalog snapshot alone: does not prevent route drift or default fallback.
- A post-request generation lookup alone: validates the provider after the
  request but cannot constrain that request.
- An account preset or server-side default: not proven to constrain this
  request path and could change outside the locked application runtime.
- Direct provider API calls: bypass the pinned App Server and would not qualify
  the NeoBabylon execution path.
- Broad Codex routing redesign: unnecessary for the single evidenced
  incompatibility and higher maintenance risk.

## Failure, rollback, and maintenance

- Before each future OpenRouter run, refresh the exact model and endpoint
  records. Missing, expired, non-unique, nonzero-priced, or changed route
  metadata stops the run; there is no model/provider fallback.
- If the setting is unsupported by a later upstream runtime, NeoBabylon must
  not silently drop it. Requalify the upstream implementation or fail closed.
- Rollback means selecting an explicitly identified runtime that lacks route
  pinning only for non-OpenRouter work; OpenRouter live activation remains
  blocked until equivalent no-fallback enforcement is re-established.
- Keep the upstream source layout and notices. Rebase the patch against the
  next selected Codex revision, rerun route/config/protocol tests, rebuild, and
  update the runtime lock/hash before qualification.
- Remove this adaptation only when the selected upstream App Server exposes
  equivalent validated request-level exact-endpoint/no-fallback semantics.

## Review and remaining qualification

CODEX Helper review was attempted but unavailable because the computer-use
kernel failed to initialize and no separate browser-control tool was exposed.
The implementation received adversarial self-review; this is weaker than an
independent reviewer. Remaining checks include the patched-runtime visible
WPF path, provider token-usage reconciliation, repeated runs, live reasoning
telemetry, context-budget/compaction behavior, and the broader Phase 1B
authority/recovery matrix.
