# S003 — Provider-aware reasoning presentation

Priority: Important. Specification, not an implemented patch. Base: `f126612a00231a58626d72108aac6ee912431ee3`.

## Existing source

`Core/CodexModelCatalogBuilder.cs` hard-codes `supports_reasoning_summary_parameter=false` and `default_reasoning_summary=none`. Effort controls exist but are not a reasoning viewer. `AppServerNotificationProjection.cs` discards completed reasoning items; `App.tsx` has no dedicated reasoning event path and shows a generic thinking indicator. `ThreadTranscriptProjector.cs` restores user/assistant text only. `ThreadUsageEvidence.cs` owns usage evidence. NVIDIA `Program.cs` translates provider reasoning fields and retains exact assistant replay in `AdapterState`; that private continuation material must not be conflated with a display transcript.

Touch those projections, `ModelCapabilityRecord.cs`/capability identity handling, host snapshots, `bridge.ts`, and a new UI reasoning item component/reducer. Do not disable or change the existing effort control while adding visibility.

## Capability contract

Add a separately versioned capability extension, preserving existing capability-record serialization/identity compatibility. Proposed fields: readable channel kind (`none`, `summary`, `providerText`, `metadataOnly`, `unknown`), supported event families, request parameter support, provenance/evidence, disclosure restriction and opaque continuation requirement. Capability absence is Unknown, not an assumption that every OpenAI-compatible provider supports Responses reasoning fields.

Model the following independently:

| Provider outcome | UI behavior | Persistence |
|---|---|---|
| Explicit readable summary | Collapsible 'Reasoning summary' with provider/model attribution | Journal-derived summary and bounded display projection |
| Explicit vendor-readable analysis text | Collapsible 'Provider reasoning', preserving supplied channel name | Native display text, not synthesized commentary |
| Tokens/timing only | Usage/elapsed metadata; no empty fake transcript | Reported counters and measurement provenance |
| Ordinary answer text only | Normal answer; generic working status may remain | No invented reasoning item |
| Opaque encrypted/private continuation | No decoding or display control for that payload | Exact transport replay only, separated from display |
| Disclosure prohibited/unsupported | Visibility unavailable with a specific explanation | Preserve only allowed operational metadata |

No extraction of alleged hidden reasoning from ordinary assistant prose or guessed `<think>` delimiters unless the exact provider contract explicitly declares those delimiters as its output format. Never label elapsed time or a reasoning-token count as readable reasoning.

## Event/data model

Propose a `ReasoningDisplayItem` with thread/turn/item ID, section/index, channel kind, raw provider channel label, capability identity, status, bounded text and omission/source-retention metadata. Use item identity plus summary/content index, not one global 'current thinking' buffer. Preserve whitespace and order; reuse the batching principle fixed by patch 008. Interleaved answer/tool/reasoning events must not overwrite one another.

Generate exact runtime event schemas before wiring `item/reasoning/summaryTextDelta`, summary boundaries or vendor equivalents. The current public docs identify summary events; they do not prove every event exists in the locked build. Normalize only validated known fields at the provider adapter boundary. Retain a bounded unknown-event diagnostic containing field names and identity, not secrets or full opaque payloads.

Apply an initial 40,000-character visible budget per reasoning item and a bounded aggregate turn budget. On overflow show a clear partial badge and inspect-source action only when the authoritative source was retained. An authoritative completed item replaces its own pending deltas, never an answer item or another reasoning section. Interrupted items remain explicitly partial. Metadata-only usage updates must not create text items.

## Request configuration and UI

S001 owns global visibility preference (collapsed by default) and optional per-conversation override. Visibility and provider generation are separate: hiding a panel must not imply reasoning was disabled or tokens were saved. Enable a provider summary request only when the exact capability says that parameter is supported. Never switch the catalog's summary flag to true globally.

Show a compact, keyboard-expandable panel in the chronological timeline (S004) with source label, partial/completed state and truthful copy-preview action. Expanded state is UI state, not conversational context. Screen readers get throttled status updates rather than every token. A provider that exposes nothing readable gets no fake expandable empty panel. Do not send displayed reasoning back as an ordinary user/assistant message.

## Restart, failures and compatibility

Restore allowed readable reasoning from the App Server journal into the same item IDs. If the locked journal omits it, show 'not retained' after restart; adding a host sidecar is a separate explicitly labeled display cache, never an authoritative replay source. S006 owns exact NVIDIA replay and cannot reconstruct it from this UI's truncated text.

Old capability records remain byte/identity-compatible through an extension file or a tested version migration. Missing, malformed or stale extension evidence disables the new channel without disabling ordinary text conversations. A failed panel render must not interrupt the agent turn. Changing models retains historical channel attribution rather than repainting previous items as the new model.

## Tests and implementation order

First implement capability admission and recorded event fixtures for each of the six outcomes above. Then host projection, reducer/batching, UI, and journal restoration. Test split UTF-8/Unicode, whitespace-only chunks, two simultaneous items, section boundaries, duplicate terminal events, stale epochs, final answer before/after reasoning, empty summaries, over-budget text, metadata without text, hidden preference changes mid-stream and opaque data never entering clipboard/DOM/log export.

Acceptance: a synthetic supported endpoint produces exactly its supplied text in the appropriate panel and final answer remains unchanged; a text-only endpoint produces no reasoning panel; opaque replay survives its transport test without disclosure; restart restores only genuinely retained readable items. Repeat native tests and exact selected-provider live checks before asserting provider support. No decision to fabricate or expose hidden reasoning is part of this work.
