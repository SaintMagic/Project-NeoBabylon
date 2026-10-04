# S009 — Tool prompt measurement and bounded discovery

Priority: Optional first-pass instrumentation; Important before a broad orchestration change. Specification. Base: `f126612a00231a58626d72108aac6ee912431ee3`.

## What source establishes, and what it does not

`CodexConfigBuilder` explicitly enables/disables generated MCP tools from qualified bindings. The route gate remains closed; a large candidate store therefore does not prove that every candidate schema is sent every turn. `CodexModelCatalogBuilder` disables several unsupported app/plugin/skill prompt features. NVIDIA `Program.cs:TranslateTools` normalizes function tools, limits the tool count and maintains name/namespace mapping. These are real compatibility paths worth measuring, not evidence that the prompt is already efficient or already bloated.

The runtime owns the actual model request assembly and resides in the separately maintained, ignored private runtime checkout. That source/binary is not in this public checkpoint. This review cannot truthfully report current per-turn schema tokens, repeated tool bytes, supported runtime discovery internals or a live model stall rate. No broad rewrite is justified from the public host code alone.

## First deliverable: measurement without behavior changes

Instrument a deterministic provider endpoint and, where appropriate, the existing NVIDIA adapter request boundary. Record per request: exact runtime/adapter/model/capability identity, sequence/turn ID, serialized tool count/bytes, stable schema fingerprints, instruction bytes, transcript bytes, duplicated schema hashes, discovery calls, call/result IDs, retry count, latency and completion outcome. Prefer byte counts when the actual model tokenizer is unavailable; label token counts as provider-reported or estimator-specific, never exact by assumption.

Default records contain counts/hashes and field names, not argument values, credentials, prompt contents or provider replay. Raw request capture is opt-in, bounded, task-scoped and redacted, with a local retention policy. It must not transmit data to a telemetry service. Retain provider-native schema form alongside the normalized form only in an authorized fixture capture so differences can be explained.

Compare zero tools, the minimal routine shell/file set, a small MCP set and a deliberately large synthetic registry. Measure first turn and continuation separately. Identify what is actually repeated, what is cached by the transport/provider and what the model must reprocess. No numerical improvement target is claimed before this baseline exists.

## Conditional design if measurements show material waste

Prefer the pinned runtime's already supported lazy/discovery mechanism if its exact schema and behavior are qualified. Otherwise propose a small stable discovery entry point returning short, paginated descriptions and a narrow request for the exact full schema. Never make a tool callable merely because it was discoverable. Discovery is read-only; invocation still resolves the exact current registered identity, activation state, permissions and selected capability.

Keep a small core set sufficient to make progress without recursive discovery. A model should not need to discover the discovery tool. Return stable opaque tool IDs and registry revision, not a huge natural-language catalog. Discovery results are bounded and cached against schema/registry identity; edits/revocation invalidate caches and pending invocations. Avoid packing every schema into system instructions as a workaround for a provider lacking deferred tool support.

Provider differences remain explicit. NVIDIA normalization must preserve tool-name/namespace round trips and its length/field limits. A provider without tool search may receive a bounded eagerly selected subset, with the selection and omitted capability visible; do not silently remove a tool the current task requires. A model returning malformed arguments receives a precise bounded tool error, not a fabricated success or an infinite retry loop.

## Loop/error behavior and state

Preserve call ID to result ID matching across retries and concurrent calls. Track repeated identical call/error pairs and failed schema-discovery loops. After a bounded number of non-progressing attempts, return a specific corrective result to the agent and expose stalled state to the user; do not abruptly change providers or disable ordinary tools. The task may choose another safe approach within scope. Empty results, cancelled calls and unsupported schemas must be distinguishable from successful empty output.

Cache state can be disposable on restart if it is recreated from authoritative registry/schema identity. Persisted tool activation/revocation remains owned by existing generated-tool stores. Discovery entries never confer execution authority or upgrade a prepared-disabled candidate. S006's native replay remains separate from tool-discovery cache.

## Tests and acceptance

Test duplicate schema definitions, long/non-ASCII names, namespace collisions, parallel call/result ordering, invalid JSON arguments, unsupported schema keywords, missing required fields, huge results, stale registry revision, revoked tool after discovery, zero matching tools, pagination boundaries, provider-specific normalization and repeated error loops. Add an explicit assertion that unactivated candidates remain uncallable even when their descriptions can be inspected manually.

Acceptance for the first slice is a reproducible request-byte/token-evidence report and exact fixture captures, not a new framework. A later optimization must demonstrate lower measured overhead on representative task suites without lowering completion rate, increasing unauthorized exposure or changing provider identity. Measure both latency and total model/tool round trips: lazy discovery can save prompt bytes while costing extra turns.

## Implementation order and deferrals

Add counters and fixture capture; establish baseline; identify one narrow optimization; qualify request serialization and task outcomes on each affected adapter; then consider discovery/caching. Defer a universal tool broker, automatic generated-tool activation, schema-rewriting DSL, opaque cross-provider prompt translator and replacing Codex's tool loop. No product decision is needed to measure. A materially different callable-route design must pass the existing separate acceptance process.
