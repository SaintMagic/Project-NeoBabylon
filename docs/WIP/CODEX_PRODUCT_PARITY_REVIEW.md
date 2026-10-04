# NeoBabylon: Codex product-completeness review

Reviewed: 2026-10-04. Source: `f126612a00231a58626d72108aac6ee912431ee3`.

This is a review and an unapplied implementation package, not a product release or a runtime/provider qualification. Product source on this review branch remains the reviewed source; proposed code is in `patches/codex-parity`. The implementation order, patch catalog, specifications and verification record are linked below.

- [Implementation order](CODEX_PARITY_IMPLEMENTATION_ORDER.md)
- [Patch catalog](../../patches/codex-parity/README.md)
- [Specification index](codex-parity/README.md)
- [Verification and limitations](codex-parity/VERIFICATION.md)

## Executive assessment

NeoBabylon is materially further along than a chat mock-up. It already has a supervised runtime, an intentionally explicit authority boundary, saved conversations, exact model identity, same-conversation model switching, recovery records, streamed output, compaction, command stopping, and substantial pure state-machine tests. Replacing this architecture would discard useful work.

The remaining distance to “Codex, but mine” is concentrated at the product boundaries: editable behavior, usable conversation content, attachments, provider capability administration, and reliable continuation across process lifetimes. Several capabilities are present below the UI but have no complete host-to-renderer path. The most obvious example is reasoning: an adapter can emit provider-native reasoning, but the application does not preserve and render a reasoning timeline. Reasoning effort and token counters are not a reasoning viewer.

The first implementation batch should apply the seven bounded corrections/enhancements in this package, then finish settings/instruction ownership and the conversation-content pipeline. Durable NVIDIA replay is a parallel correctness priority before promising restart-safe conversations for the affected provider/model. Do not start by implementing a universal desktop automation catalog or replacing the runtime.

No newly discovered Critical defect is claimed. Important findings below materially affect intended use; Moderate findings affect clarity, ergonomics or confidence in testing. Accepted unrestricted execution is high consequence, but it is an explicit existing decision, not a newly discovered hidden sandbox vulnerability.

## Scope, provenance and limits

The review used the complete public Git snapshot, including product source, tests, protocol artifacts, architecture, decision records, current WIP and relevant recent history. The 303 tracked source blobs were checked against the exact Git tree. A separate local snapshot repository and detached worktrees were used for patch construction. A synthetic local snapshot commit is not the upstream source revision. Windows CI fetched the real source commit and tested the patches against that commit directly.

There is no access in this review to an uncommitted working tree on Martin's computer. The private Codex-derived runtime repository, locked executable, Node deployment, real credentials, personal files and installed applications were not supplied by this public repository. None were substituted. The review did not operate Martin's desktop or call a live model.

`runtime/runtime-lock.json` identifies runtime version 0.155.1, upstream revision `be2951ea34f0d295ed0becf97079f92fa5f6950e`, and the private locked binary SHA-256 `a0c3ebdc8d1d9f5b56327f5ea6d1502ee5a0b50fa174d50743f8c7a17ff54386`. The binary path is `.local/Runtime/LockedBuild/20261003-a0c3ebdc/codex-app-server.exe`. Its absence limits runtime qualification; it does not prevent source-only builds and tests. Historical live evidence in the repository is not a live result from this review.

This was a product-oriented engineering review, not an exhaustive security audit, a complete line-by-line audit of every historical document, or independent certification of all accepted qualification claims.

## Decisions preserved

The WPF/.NET + WebView2 host, React/TypeScript/Astryx renderer, named bridge, separately owned pinned App Server and application-root Data boundary remain intact. `RuntimeSupervisor` is in `host/NeoBabylon.Host`, not Core; Core owns reusable records, protocol and projections.

NB-DEC-005 places durable state under the application/install root's `Data`. NB-DEC-008 deliberately permits full Windows-user tool execution without containment. NB-DEC-012 makes persistent generated-tool activation a separate exact-identity host action; review, prepared-disabled binding and staging do not confer callability. Existing route/tuple qualification gates remain closed. NB-DEC-013 accepts switching the model in the same conversation and supersedes older next-task-only assumptions. NB-DEC-010 defers packaging. This package does not reopen those decisions.

Permission descriptions and prompt instructions cannot contain an arbitrary process running as the same unrestricted user. Completing scoped UX and agent policy is still useful, but must not be marketed as enforcement. No patch changes the authority policy, enables generated tools, updates the runtime lock, enables provider fallback, or writes to the ordinary Codex home.

## Confirmed current capabilities

| Area | What is actually present | Important limit |
| --- | --- | --- |
| Desktop shell | Native WPF/WebView2 shell, local renderer, theme integration, keyboard and modal support | Development assembly, not an installable release |
| Providers | LM Studio and OpenRouter capability paths; NVIDIA adapter; exact provider/model and route handling | No general in-app profile editor; compatibility is bounded, not universal |
| Conversation history | Saved-thread listing, loaded-history search, pagination, resume, rename and whole-thread fork | Search is explicitly over loaded conversations; no polished full-history management |
| Model switching | Host-confirmed exact same-thread selection, preserved draft/history, transition records | Requires idle boundary; cross-provider live repeatability remains to be qualified |
| Restoration | Drafts, active-task bookmark, pending-submission reconciliation and protected-record recovery | Does not imply every adapter's private continuation state is durable |
| Streaming | Assistant batching, bounded display projections, omission disclosures, saved output ranges | Visible content types are narrower than the underlying protocol |
| Tools | Activity cards, command failures, output inspection, saved diff review, exact command stopping | Structured outputs and metadata are lost on some paths; cards are not an ordered timeline |
| Context | Usage/context diagnostics, output cap controls and manual compaction tracking | Compaction acknowledgement is not completion; broader live qualification remains open |
| Reasoning controls | Provider-aware effort options and usage metadata where known | No complete reasoning content viewer or restored reasoning stream |
| Authority handling | Explicit full-access label, fail-closed unsupported requests and approval recovery | Not personal-file or system containment |
| Generated tools | Candidate/review/prepared binding/history UI and explicit activation groundwork | Callability intentionally disabled pending accepted qualification |
| Diagnostics | Runtime, capabilities, failures, protected records and usage evidence | Not a complete settings surface or end-to-end request trace |

Source anchors: `ui/diagnostic/src/App.tsx`; `bridge.ts`; `drafts.mjs`; `model-switch.mjs`; `restoration.mjs`; `transcript.mjs`; `context-compaction.mjs`; `host/NeoBabylon.Host/RuntimeSupervisor.cs`; `MainWindow.xaml.cs`; Core `ThreadCapabilityBindingStore`, `CapabilitySwitchSafety`, `ManualCompactionTracker`, `AppServerNotificationProjection`, `ProtectedDataRecordRecoveryService` and generated-tool classes. Some module names are best located through the source index rather than guessed from feature labels; use symbol search when implementing.

## Findings and disposition

| ID | Severity | Finding | Evidence classification | Disposition |
| --- | --- | --- | --- | --- |
| F01 | Important | Equal assistant text can collapse distinct explicit item identities; fallback IDs can overwrite earlier turns | Source + failing/passing regression | Patch 001 |
| F02 | Important | Default agent instructions do not communicate task ownership or the requested three boundaries | Source + policy contract tests, not live model behavior | Patch 002; S001 makes behavior editable |
| F03 | Important | Structured tool results/errors and dynamic content are dropped or escape display limits | Source + failing/passing Core regressions | Patch 003 |
| F04 | Moderate | Large displays are held to a narrow fixed conversation width with no user preference | Source + layout preference tests; browser verification separately recorded | Patch 004 |
| F05 | Moderate | Messages have no direct copy action or explicit preview-copy semantics | Source + clipboard tests | Patch 005 |
| F06 | Moderate | Windows QA fixtures fail on short/long TEMP path aliases | Observed Windows test failures | Patch 006; production guards unchanged |
| F07 | Moderate | Accessibility fixture selectors and identity responses lag current UI/bridge contracts | Observed selector/focus failure + source contract tracing | Patch 007 |
| F08 | Important | No complete editable application settings/custom-instruction pipeline | Source confirmed | S001 |
| F09 | Important | Conversation input is text-only across composer, bridge and host | Source confirmed | S002 |
| F10 | Important | Provider reasoning is not preserved/rendered as conversation content | Source confirmed, provider data availability differs | S003 |
| F11 | Important | Conversation text, tools and results are not a rich, ordered turn timeline | Source confirmed | S004 |
| F12 | Important | Existing provider support lacks a first-class extensible profile/capability registry | Source confirmed | S005 |
| F13 | Important | NVIDIA exact replay depends on process-local state and can disappear after restart/eviction | Source-confirmed persistence gap; no live endpoint reproduction | S006 |
| F14 | Important | Long-running operation, transient failure, retry and steering behavior need a single user-facing lifecycle | Mixed: missing UX confirmed; specific event races require reproduction | S007 |
| F15 | Important | Personal-file/system/trust boundaries lack enforceable scope in the unrestricted runtime | Accepted architecture limit, not new containment finding | S008; no authority redesign in this patch set |
| F16 | Moderate | Diagnostics can report unfamiliar completed-item statuses as failure instead of unknown | Source-confirmed classification mismatch | S007 regression and typed outcomes |
| F17 | Moderate | Hardcoded TODAY grouping and stale feature documentation obscure restored/current state | Source confirmed | S004 and documentation synchronization |
| F18 | Optional | Request schema/token overhead and lazy tool discovery need measurement | Investigation item; no prompt-bloat defect asserted without private request evidence | S009 |

### F01: preserve identity, not text coincidence

`transcript.mjs` previously used the last streaming/global fallback ID and equal-text deduplication without consistently respecting explicit item IDs and turn boundaries. Two different assistant items containing the same text are not necessarily duplicate events. A completion without an ID after a new user turn must not replace an older assistant entry. Patch 001 preserves explicit identity and confines fallback matching to the current tail with compatible known thread/turn identity. Repeated completion of the same item remains idempotent. This changes the UI projection, not the runtime journal.

### F02/F08: customization must reach the runtime

`CodexModelCatalogBuilder` supplies a one-sentence base instruction. `CodexConfigBuilder` and `RuntimeSupervisor.EnsureClientAsync` generate runtime input; manually editing generated files is not a durable settings design. The gear entry opens diagnostics, not a complete editable settings interface.

Patch 002 gives the existing catalog a concrete default policy: own authorized goals, perform routine prerequisites, use helpers, retry reasonable approaches, verify outcomes, preserve exact provider choice, protect personal files and essential software, and install only reasonably trustworthy software. It does not claim that text prevents damage or guarantees that a particular model follows instructions. Its tests establish prompt inclusion and identity preservation only.

S001 adds a host-owned revisioned settings record, scoped effective settings, real save/apply status and instruction previews. This is deliberately a specification rather than a fake Settings dialog with no runtime propagation. The effective instruction composition must be inspected through the actual locked runtime before enabling system/developer overrides.

### F03: one bounded display representation for tools

The baseline live projection caps strings but allows nested result/error JSON to survive as objects; other readers ask for strings and silently lose that content. Saved output projection, range reads, UI live cards and terminal diagnostics use inconsistent field precedence. Dynamic tool `contentItems` and explicit `success:false` need handling too.

Patch 003 introduces `ToolOutputText` for display-only serialization and consistent precedence, preserves explicit failure, carries exit code and duration, and makes the live/saved/range paths agree. It retains bounded text and omission disclosures without changing provider input or source journal bytes. JSON is deliberately displayed as text, not executable markup. The patch does not eliminate allocation of the entire incoming JSON object or promise an arbitrary-size RPC memory bound. Rich result blocks remain S004.

### F04/F05: immediately usable conversation improvements

The baseline transcript/composer are constrained to approximately 820/850 px even on very large displays. Patch 004 offers an explicit Readable/Wide control using the existing design system, persists only a UI preference, and keeps readable layout as the default. Patch 005 adds message copying with truthful failure feedback. Truncated messages say Copy visible preview; they never imply that omitted journal content was copied. These are small working features, not substitutes for the settings or rich-content specifications.

### F06/F07: protect the value of qualification tests

`qa-run-root.test.mjs` compares a temp directory spelling with a canonical real path. Windows can supply an 8.3 TEMP alias. Canonicalizing the fixture's own newly created root fixes the tests without changing `qa-run-root.mjs` or weakening its traversal/junction guards.

`ui-accessibility.mjs` selects both a saved-chat row and its overflow button with one accessible-name regex, expects activity cards to have an obsolete role, and omits fields now required by exact resume/model-selection contracts. The test should return valid synthetic identities and use the current semantic controls. It should not relax the application to accept a missing frozen capability. Patch 007 repairs the fixture and preserves the keyboard, modal and failure assertions.

### F09: attachments require a durable input contract

The model catalog can describe input modalities, but `App.tsx` submits text, `bridge.ts` exposes no attachment lifecycle, and `RuntimeSupervisor.StartTurnCoreAsync` builds text input. The transcript projector omits non-text content rather than reconstructing usable attachment descriptors. A file-picker button alone would be misleading.

S002 covers explicit file selection/paste/drop, opaque host-owned attachment IDs, immutable app-managed copies, preview/error states, provider admission, draft/turn references, restart behavior and safe cleanup. Unsupported images must fail before send rather than silently becoming filenames or text-only prompts. User attachment selection grants the selected file, not its whole parent directory.

### F10: reasoning is a capability and content problem

`AppServerNotificationProjection` excludes reasoning items from the main preserved content path; the UI handles assistant and tool events, not a reasoning transcript; saved transcript reconstruction keeps user/assistant text. The catalog's reasoning-summary defaults also do not enable a generic content stream. Meanwhile `NeoBabylon.NvidiaAdapter/Program.cs` already translates exposed `reasoning_content` into reasoning events. Wiring must address all these layers, not only add an Analysis accordion.

S003 separates summaries, explicitly exposed native text, metadata-only usage, opaque continuation material, no-channel providers and providers that must not expose content. Never generate a hidden reasoning transcript from latency, token counters or ordinary answer text. OpenAI reasoning summaries and opaque encrypted continuation are not interchangeable with provider-native text.

### F11/F17: conversation readability and chronology

`App.tsx` renders message bodies as plain text and appends the activity section after the message list, rather than interleaving tool activity with the appropriate turn. It lacks a full Markdown/code/result artifact presentation layer. TODAY is a literal header rather than reliable date grouping. Whole-thread fork and loaded-history search exist; editing a particular earlier prompt, retrying from a known turn and full-history search should not be advertised as present.

S004 completes a stable item timeline with source IDs, bounded paging, user-controlled follow-scroll, safe Markdown, code copying, typed tool blocks, correct dates and journal-backed conversation commands. It does not establish a second authoritative history store in the renderer.

### F12: preserve heterogeneous providers while making them configurable

`RuntimeSupervisor.EnsureClientAsync` has explicit provider-ID branches; capability records originate through bounded existing record paths including release evidence. This is an important foundation but not a general profile workflow. S005 adds user-owned profiles alongside shipped qualification fixtures, with transport type, exact endpoint/model/route, credential references and evidence-backed capabilities. Unknown is distinct from unsupported and supported. “OpenAI-compatible” does not prove multimodal input, reasoning, tool schema, cancellation or replay compatibility.

### F13: saved history is not sufficient for NVIDIA continuation

In `NeoBabylon.NvidiaAdapter/Program.cs`, `TranslateInput`, `AppendReplay` and `AdapterState` require exact cached assistant reasoning/tool-call material for some replay paths. `RequiresReasoningReplay` explicitly includes `moonshotai/kimi-k3`. `AssistantReplay` is an in-memory dictionary with bounded FIFO eviction. A process restart or eviction removes data that a subsequent continuation may require; `MissingReplay` correctly refuses reconstruction rather than inventing equivalent history.

This is a source-proven durability gap, not a claim that a live NVIDIA run failed during this review. S006 retains exact provider-native replay in a protected, versioned sidecar, binds it to the exact provider/thread/item identity and treats the cache as an acceleration layer. Unknown/incompatible legacy history stays readable but cannot silently execute with substituted replay. This deserves priority alongside settings, not after decorative UI improvements.

### F14/F16: distinguish waiting, retrying, interrupted and unknown

Existing interrupt and exact command-stop implementations should be retained. The busy composer, missing steer bridge and distributed request/turn state still make lengthy jobs feel less capable than the intended baseline. Completion waits and bridge deadlines are not proof that execution stopped.

`AppServerClient.WaitForTurnCompletionAsync` and `MatchesExpectedTurn` deserve deterministic tests for generic error notifications, transient retry notifications, stale epochs and unrelated turn IDs. A generic error branch is broader than some of the item/turn filters; actual runtime event ordering and retry semantics must be established before changing it. This is a concrete investigation target, not a claimed reproduced race.

Separately, `TurnDiagnostics.Extract` maps an unfamiliar completed-item status to `succeeded:false` and an outcome of failed, while the live renderer can represent unknown as informational. S007 makes unknown outcomes explicit without ever representing unknown as success. Automatic re-execution of a potentially side-effecting operation is not an acceptable retry policy.

### F15/F18: autonomous goals without an enormous prompt catalog

The accepted shell/runtime foundation is compatible with creating helpers, installing prerequisites and executing multi-step tasks. This public-source review cannot demonstrate that every requested desktop/browser workflow completes, or measure the private runtime's actual per-turn tool prompt. There is insufficient evidence to label the implementation a giant always-on tool dump.

S008 defines scoped inputs, trusted installation and result-verification contracts around existing tools. S009 specifies measurement and bounded lazy discovery experiments before making registry changes. Feature flags and generated-tool registration must not be mistaken for proof of efficient model-visible prompting. Normal temporary helper scripts do not need the persistent generated-tool activation path.

## Current Codex comparison: verified expectations, not invented parity

Primary sources were consulted on 2026-10-04:

- https://developers.openai.com/codex/app/features — redirects in the retrieved documentation to https://learn.chatgpt.com/docs/features
- https://developers.openai.com/codex/app/settings — redirects to https://learn.chatgpt.com/docs/reference/settings
- https://developers.openai.com/codex/app-server — redirects to https://learn.chatgpt.com/docs/app-server
- https://developers.openai.com/api/docs/guides/reasoning

The current documentation establishes useful behavioral targets: project conversations with richer input, configurable instructions/preferences, typed streamed items and explicit interruption/steering semantics. The App Server documentation describes user input beyond text, separate reasoning items, command execution metadata, MCP and dynamic tool results, and turn lifecycle events. Those are sound reasons to complete the corresponding NeoBabylon paths.

The reasoning guidance distinguishes supported summaries from inaccessible raw hidden reasoning. It also describes opaque continuation information; that is not content to decode for display. Provider-native reasoning outside that API requires its own validated adapter contract.

Documentation redirects and evolving product naming mean this review does not claim to have run the latest closed-source Codex desktop UI, establish every platform's precise controls, or verify that every described feature ships in Martin's installed version. No requirement here depends on guessing a particular current Codex release number. Newer protocol documentation is a behavioral reference; it does not authorize sending unsupported methods to the pinned runtime. Each protocol addition must be checked against the repository's generated schema and exact locked runtime.

## Architecture observations

The existing boundary between Core projections/records, host supervision and renderer state is appropriate. The biggest maintainability problem is the amount of orchestration concentrated in `RuntimeSupervisor.cs` and `App.tsx`, not the framework choice. Extract cohesive controllers while implementing specific features; avoid a prerequisite wholesale refactor.

Use one host-owned effective configuration pipeline and one journal-derived timeline. Avoid parallel settings state in localStorage, arbitrary generated TOML edits and provider adapters making unrecorded identity substitutions. Keep display serialization separate from replay serialization: bounded readable text is good for cards but cannot stand in for exact native provider history.

UI localStorage is proportionate for a theme or width preference, not the only durable home for instructions, provider definitions, attached files or irreversible-operation consent. Host-owned records should use the repository's protected-record, recovery and application-root conventions. DPAPI-protected secrets may not survive an OS reinstall merely because Data survives; that limitation must remain explicit.

## Priority and dependencies

1. Apply and qualify the bounded patch batch. It fixes confirmed projection defects and provides useful visible improvements without a runtime replacement.
2. Implement S001 settings/instructions and S006 exact NVIDIA continuation in parallel; their host ownership must be agreed through the supplied contracts, not new routine product questions.
3. Implement S002 attachments and S003 reasoning, sharing journal content identity but keeping private replay separate from display.
4. Implement S004 ordered rich transcript and conversation commands; stage safe text/code rendering before provider-specific rich outputs.
5. Implement S005 profile management and S007 operation lifecycle, with selected-tuple qualification before enabling broader combinations.
6. Implement scoped workflow improvements from S008. Measure S009 before attempting a larger tool-orchestration redesign.

See the implementation-order document for the precise patch graph, vertical slices and acceptance commands. Settings is not a prerequisite for fixing tool output, identity or tests. Attachment persistence is a prerequisite for promising attachment restoration. A durable replay store is a prerequisite for promising affected NVIDIA conversations survive a process restart.

## Decisions actually needed

No new product decision blocks this patch set. Default readable width, explicit Wide mode, preview-only copying, conservative capability admission, next-safe-boundary application of settings and no provider substitution follow the supplied intent.

A later decision is required only if the product is to claim technical enforcement of personal-file/system boundaries while arbitrary tools run with full logged-in-user access. That is a real containment/authority tradeoff, already deferred in repository decisions. This review neither quietly solves it with prompts nor forces it into the first batch. Provider-specific live qualification may require credentials or a selected runtime tuple; that is an execution prerequisite, not a reason to redesign providers.

## Intentionally not recommended

Do not rewrite the desktop shell, replace Astryx, consolidate the private runtime into the product repository, silently update the binary lock, or treat an upstream public binary as equivalent. Do not replace provider flexibility with an OpenAI-only path. Do not reopen the accepted same-chat switching decision. Do not enable generated tools just because a review exists. Do not add approval prompts for routine authorized prerequisites. Do not use ordinary model output as authority to expand personal-file scope. Do not claim unexposed hidden reasoning exists or decode opaque continuation for display. Do not implement automatic replay of unknown side effects. Do not create a universal tool-discovery subsystem before measuring the actual request path.

## Completion and verification

The patch/spec package is the deliverable. It does not implement all nine larger specifications. Detailed verification results, revisions, commands, failures corrected during this review and remaining native/live/manual gates are recorded separately in [VERIFICATION.md](codex-parity/VERIFICATION.md). Implementation agents must use that record rather than infer a passed product gate from a successful compiler or from this report's breadth.
