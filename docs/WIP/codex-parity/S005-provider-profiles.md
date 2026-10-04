# S005 — Provider and model profile management

Priority: Important. Specification. Base: `f126612a00231a58626d72108aac6ee912431ee3`.

## Existing source and constraints

`Core/CapabilityRecordPathResolver.cs` restricts Release capability records to `docs/release/MODEL_CAPABILITY_*.json`; its ApprovalQA exception is deliberately limited to isolated Data. `ModelCapabilityRecord`, `ActiveCapabilitySelection`, `CapabilitySwitchSafety`, and `ThreadCapabilityBindingStore` retain exact model/capability identity. `RuntimeSupervisor.EnsureClientAsync` recognizes only `lmstudio`, `openrouter` and `nvidia`; it does not already support every arbitrary compatible provider. `CodexConfigBuilder` has provider-specific transport/route rules, and `NvidiaAdapterProcess` additionally admits three explicit NVIDIA model IDs. Credentials are delivered through `ProviderCredentialAccess` and `AppServerLaunchEnvironment`, not React state. Locate those definitions in `AppServerClient.cs` when extending them.

The existing idle same-chat model switch is accepted by NB-DEC-013. Preserve the thread/history, historical attribution and exact fail-closed selection; do not replace it with mandatory new-chat switching.

## Proposed storage and data model

Add `Core/ProviderProfileStore.cs` and immutable `ModelCapabilitySnapshotStore.cs`. Host-owned profiles live under `Data/NeoBabylon/Providers`; capability snapshots are immutable, content-addressed records beneath that namespace. Use protected replacement for mutable indexes. Import bundled release records as seed evidence, retaining their original bytes/hash/source and qualification label. Do not edit historical release evidence when a user changes an endpoint.

A profile contains ID, revision, display name, transport adapter ID, endpoint, non-secret auth reference, model discovery mode, allowed advanced options and immutable capability snapshot references. A model choice contains the literal model ID and capability identity, not merely a human label. A discovery response is advertised metadata, not proof that tools/images/reasoning/resume work. Track advertised, observed, manually asserted and unknown values separately. Manual context input remains labeled an assertion until observed; never relabel it verified automatically.

Secrets stay in the host credential mechanism. The renderer gets `present/missing/unavailable`, not the secret value. Add a native credential-entry surface or an explicitly secret-handled bridge route with no logging; the ordinary settings object must never contain keys. Persist only protected secrets already supported by the application, preserving the documented DPAPI/OS-reinstall limitation. Export profiles excludes secrets by default and says so.

## Host APIs and application state

Proposed named operations: `listProviderProfiles`, `saveProviderProfile(expectedRevision, profile)`, `discoverModels(profileId, expectedRevision)`, `probeCapability(profileId, modelId, requestedCapabilities)`, and `selectCapability` extended to an immutable snapshot reference. Keep the old selection shape working for bundled records during migration.

Validate endpoint URI and transport before any credential is attached. Local HTTP is an explicit local/private endpoint configuration, not a reason to allow insecure remote credential delivery. Remote credentialed endpoints require HTTPS; reject userinfo/fragments and unapproved redirects to other origins. Discovery must not forward a canonical provider key to a user-edited unrelated host. Profile edits invalidate pending discovery/probe results and do not mutate a currently running turn.

States: draft → valid configuration → discovered metadata → capability snapshot available → selected/applied; with failed, stale and credential-unavailable states. Show selection versus execution eligibility separately. On idle switch, use the existing exact binding path and record the transition; a failed application keeps the old binding and does not send a turn under an unexpected provider. Reload restores exact snapshot IDs. Missing snapshots allow history viewing but disable continuation until exact evidence is recovered.

## Provider-specific behavior

Keep OpenRouter's route pin and `allow_fallbacks=false`; a model label is not a serving-route guarantee. Keep NVIDIA's Chat Completions adapter and model-specific reasoning/replay rules rather than pretending its `/responses` endpoint exists. LM Studio uses observed local capabilities and can have unknown metadata. A new OpenAI-compatible endpoint requires an explicit adapter capability table and deterministic request/stream/tool fixtures; transport compatibility does not imply model-level tool competence or provider policy equivalence.

Expose only options implemented by the selected adapter: endpoint/model, observed context, supported reasoning efforts, output cap, route pin and qualified transport controls. Unsupported advanced fields are absent or explicitly unavailable, not silently ignored. Do not add one unrestricted JSON box whose values bypass validation or alter tool authority.

## UI and migration

Settings gains profile list, add/edit/duplicate, credential status, discovery/probe results and exact selected model/route details. Search models by display name but confirm literal ID. A connection check is labeled transport reachability; it is not a qualification badge. Save profiles without requiring a live network, then display unavailable evidence honestly. Removing a profile used by saved chats is logical retirement; retain immutable snapshots and historical references.

Existing threads continue referencing their exact old capability hashes. Migrate record lookup to search trusted seed evidence plus the new host store, not arbitrary filesystem paths. Test legacy capability serialization with the existing `CapabilityVersionChecks`. No silent reseeding, model substitution or expansion of accepted live claims.

## Tests, acceptance and order

Test duplicate display names across providers; edited endpoint while discovery is pending; malformed metadata; wrong model in response; auth failure; redirect credential leakage; missing credential after OS reinstall; stale snapshot; deleted/retired profile; same-chat switch during busy/idle; rollback after failed apply; and old record identity remaining byte-for-byte stable. Test each advanced field reaches exactly one adapter or is rejected.

Implement storage/legacy lookup and credential handling, then discovery/probes, then UI and exact selection. S001 supplies Settings infrastructure; S006 is required before promising restart-safe affected NVIDIA continuation. Acceptance is create/edit/restart/select a profile and prove the actual request uses its exact endpoint/model/route, while an intentionally mismatched response fails visibly. No new provider is called reliability-qualified solely because HTTP returned 200.
