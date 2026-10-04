# S006 — Durable exact NVIDIA continuation

Priority: Important. Specification, not a claim of live NVIDIA qualification. Base: `f126612a00231a58626d72108aac6ee912431ee3`.

## Confirmed source limitation

In `host/NeoBabylon.NvidiaAdapter/Program.cs`, `AdapterState.AssistantReplay` is an in-memory concurrent dictionary. `Cache` evicts by 128 assistant messages or 4 MiB; `TranslateInputItem` and `AppendReplay` require matching cached assistant/response/tool-call identities. Kimi (`moonshotai/kimi-k3`) explicitly requires exact assistant `reasoning_content` and `tool_calls`, and missing replay fails clearly. Reasoning input also requires its cached native entry; encrypted reasoning is rejected. `NvidiaAdapterProcess.StartAsync` bootstraps a new process with credentials/model/token/efforts but no durable replay location. A host/adapter restart loses that state. This is a confirmed persistence limitation, not evidence that a specific live model session failed in this review.

Do not 'fix' it by dropping reasoning, inventing assistant messages, sending display-truncated text, or silently falling back to another provider. Existing fail-closed behavior is preferable to invalid continuation.

## Proposed ownership and records

Add a narrowly scoped replay store shared with the adapter. `NeoBabylon.Core` is a plain net10.0 library; a deliberate adapter project reference can reuse its protected record/file validation without importing WPF. Proposed `ProviderReplayStore` owns immutable native assistant payloads and an atomic alias index under `Data/NeoBabylon/ProviderReplay/<provider-profile-or-capability-id>`. It is not a replacement conversation database.

`NvidiaAdapterProcess` supplies a host-derived canonical store root, capability identity, replay schema version and runtime/adapter contract identity in its private bootstrap. Never accept a replay root from model output or arbitrary renderer text. Validate it under the application's Data root, reject reparse escapes and keep session authentication unchanged. Credentials/session tokens must never be persisted in replay payloads.

An immutable entry contains native assistant message bytes (including reasoning and tool calls as received), provider/model/endpoint contract identity, thread ID, source response ID, assistant item IDs, call IDs, byte length and SHA-256. Alias keys resolve to one immutable payload. Duplicate aliases with different content fail; byte-identical repeated publication is idempotent. Thread and capability identities are mandatory even if call IDs happen to match.

## Commit and streaming state machine

States: receiving → complete native message → durable payload → committed alias index → completion acknowledged. Do not publish `response.completed` as durable success before replay required for the next request is committed. For streaming tool calls, provisional deltas may be sent, but if required persistence fails, terminate with an explicit adapter persistence error and do not represent the native message as a successfully replayable completion. The runtime may already have observed partial output: reconciliation must retain that uncertainty rather than retry a tool workflow automatically.

Write payload to a bounded temporary file, flush/readback/hash, atomically publish immutable content, then atomically update the alias index. Crash before index commit leaves an unreferenced blob; crash after commit leaves a verifiable entry. At startup validate the index, referenced hashes, schema and exact contract binding before admitting continuation. Corruption or a missing required payload returns a specific continuation-unavailable error without contacting the upstream provider.

Keep the existing in-memory limits as cache limits only. Durable entries cannot be evicted merely because the process cache reached 128 messages. Retention is reference-aware and explicit: protect active/saved/forked conversations and uncertain submissions. If storage is full, fail new durable admission clearly; do not discard replay and pretend restart is safe.

## Restart, switching and forks

Same exact thread/model/capability after restart resolves alias IDs from disk and reconstructs the original native message exactly once per request. `seenAssistant` deduplication remains meaningful. Switching away and back may reuse entries only for that exact compatible history. A different adapter contract or capability snapshot does not inherit replay merely because the model string matches.

Forking requires a host-authorized mapping from source thread to the new fork's inherited item identities and immutable entries; the adapter must not bypass its thread check globally. Add that mapping alongside the existing exact fork/binding path. A transcript imported from another provider or an old session without replay cannot be repaired from visible text. It remains readable; continuation on a requiring model is blocked with an explicit explanation and an optional user-selected new conversation, never an automatic fork/substitution.

Old installations have no durable replay store. Mark those sessions as legacy/non-restorable for affected continuations; do not claim migration can reconstruct missing reasoning. S003 reasoning visibility must not alter or truncate this store. Exporting logs does not include replay by default.

## Tests and qualification

Add source-only adapter fixtures separate from `tests/qa/nvidia-adapter.test.mjs`'s later exact private-runtime segment. Keep that full existing integration test intact; do not skip its runtime gate and call the whole suite green. Test native assistant/tool/reasoning bytes, alias collision, cross-thread replay rejection, duplicate key publication, restart after every commit boundary, corrupt blob/index, future schema, cache eviction followed by disk reload, disk full, model switch away/back, authorized fork and unauthorized thread reuse. Assert failure occurs before upstream request where replay is unavailable.

Then run the exact locked App Server loopback conversation: tool call → tool result → adapter restart → next turn, including one process restart after a committed native message. Capture adapter bundle hash, runtime hash, request count and byte-equivalence evidence. Only after deterministic success perform selected NVIDIA model live qualification with credentials. Preserve the current explicit unsupported encrypted-reasoning behavior unless a separate native contract supports it.

## Order and rollback

Implement/store-test the immutable format and index; add bootstrap binding; integrate cache fallback and commit ordering; add host fork mapping and recovery diagnostics; qualify deterministic and live paths. This can run independently of the Settings UI. A rollback retains replay files and refuses unknown schema rather than deleting them. No user decision is needed to preserve exact already-required continuation semantics.
