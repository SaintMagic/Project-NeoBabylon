# NeoBabylon — Codex product-completeness review

Reviewed 2026-10-04 against `f126612a00231a58626d72108aac6ee912431ee3`.

This is an unapplied implementation package, not a release or live runtime qualification. Product source and `main` remain unchanged. The review branch contains eight atomic patches, nine larger implementation specifications, and reproducible verification tooling.

[Implementation order](CODEX_PARITY_IMPLEMENTATION_ORDER.md) · [Patch catalog](../../patches/codex-parity/README.md) · [Specifications](codex-parity/SPECIFICATIONS.md) · [Verification](codex-parity/VERIFICATION.md) · [Current Codex sources](codex-parity/CODEX_SOURCES.md)

## Assessment

The repository is substantially more capable than a chat mock-up. It already has supervised local execution, exact provider/model selection, same-conversation model switching, saved conversations, restoration, compaction, command stopping, protected records and a considerable state-machine test suite. Its main shortcoming is product integration: the settings surface, input/content pipeline and provider lifecycle do not yet expose the capabilities needed for a complete everyday desktop agent.

The right first move is to finish this architecture, not replace it. Apply the bounded projection/streaming/UI fixes, establish host-owned editable settings and instructions, and make conversation content survive the complete send/stream/save/reopen path. Exact NVIDIA replay persistence is a parallel correctness priority. Do not begin with a universal desktop tool catalog or a new agent framework.

No newly discovered Critical issue is claimed. Important means a material gap or defect affecting the requested product. Moderate means meaningful ergonomics or test reliability. Optional means useful later work, not a release blocker invented from personal preference.

## Scope and provenance

The full published snapshot was recovered through GitHub and all 303 source blobs checked against the pinned Git tree. Source, tests, architecture, decisions, protocol instructions and current WIP were examined. Review artifacts from an interrupted earlier attempt on the same review branch were recovered and completed rather than duplicated. Detached worktrees were used for patch validation; the exact upstream base commit was also reconstructed and checked locally. Windows CI independently fetched that actual commit.

This is not a claim of line-by-line certification of every historical document. There was no access to an uncommitted checkout on Martin's computer, personal files, work applications, real credentials or the private runtime checkout. Historical qualification reports are historical evidence, not tests rerun here.

`runtime/runtime-lock.json` pins version `0.155.1`, upstream revision `be2951ea34f0d295ed0becf97079f92fa5f6950e`, and private executable SHA-256 `a0c3ebdc8d1d9f5b56327f5ea6d1502ee5a0b50fa174d50743f8c7a17ff54386` at `.local/Runtime/LockedBuild/20261003-a0c3ebdc/codex-app-server.exe`. That executable, the private runtime source and its generated schema cache are not supplied by the public checkpoint. No upstream executable was substituted. Builds and synthetic tests are possible without it; exact native/runtime/provider qualification is not established by those tests.

## Decisions preserved

Keep WPF/.NET + WebView2, React/TypeScript/Astryx, the named host bridge, the separately maintained pinned App Server and application-root Data ownership. `RuntimeSupervisor` belongs to Host; pure records and projections belong to Core.

NB-DEC-005 defines durable application Data. NB-DEC-008 deliberately uses full logged-in Windows-user execution without containment. NB-DEC-012 keeps generated-tool review/preparation separate from explicit exact-identity activation; current route and qualification gates remain closed. NB-DEC-013 permits idle model switching in the same conversation. NB-DEC-010 defers packaging. This package does not reopen these choices, update the binary lock, enable fallback or touch the ordinary Codex home.

The user's three boundaries remain product requirements, but instructions cannot technically contain arbitrary same-user shell execution. The proposed scope/initiative work improves behavior and host-owned operations; it is not advertised as a security sandbox.

## Confirmed capabilities

| Area | Already present | Limit that matters |
|---|---|---|
| Desktop | Native host, local renderer, appearance integration, keyboard/modal behavior | Source-development checkpoint, not an installer |
| Providers | LM Studio/OpenRouter capability paths, NVIDIA adapter, exact model/route handling | Bounded compatibility; no general profile administration UI |
| Conversations | Saved-thread list and pagination, search over loaded chats, resume, rename, whole-thread fork, new task | Older-turn navigation and comprehensive management remain limited |
| Model changes | Host-confirmed idle same-chat switching and exact binding records | No proof here of every live cross-provider combination |
| Recovery | Drafts, active-task bookmark, pending-send reconciliation and protected-record recovery | Does not make provider-private replay durable |
| Streaming | Batching, bounded projections, omission notices and saved output ranges | Two concrete text/identity defects; narrow content types |
| Tools | Activity cards, command failure/stop, output inspection and tracked-change review | Structured fields can be lost; cards are not chronologically interleaved |
| Context | Usage display, output caps, reasoning effort and tracked manual compaction | Effort/tokens are not a reasoning-content viewer |
| Generated tools | Candidate/review/prepared-disabled/history and activation groundwork | Callability intentionally unavailable pending accepted qualification |
| Diagnostics | Runtime, capability, failure, recovery and usage evidence | Not application settings or a complete operation trace |

Source anchors: `ui/diagnostic/src/App.tsx`, `bridge.ts`, `drafts.mjs`, `history.mjs`, `restoration.mjs`, `transcript.mjs`; `host/NeoBabylon.Host/RuntimeSupervisor.cs` and `MainWindow.xaml.cs`; Core `ThreadCapabilityBindingStore`, `CapabilitySwitchSafety`, `ManualCompactionTracker`, `AppServerNotificationProjection`, `ProtectedDataRecordFile` and generated-tool stores. The specifications name the exact files and proposed interfaces for each change.

## Findings and disposition

| ID | Severity | Finding | Evidence / action |
|---|---|---|---|
| F01 | Important | Assistant projection can collapse distinct explicit item IDs with equal text or replace an earlier turn through fallback matching | Source and regression coverage; patch 001 |
| F02 | Important | Compiled default instructions do not express ownership of authorized goals, routine prerequisites or the requested boundaries | Source and prompt-contract tests; patch 002, then S001 |
| F03 | Important | Structured tool results/errors and dynamic content are lost or bypass text projection limits on different paths | Source and Core/UI regression coverage; patch 003 |
| F04 | Moderate | Fixed narrow conversation/composer width wastes large displays; no explicit width preference | Source and layout tests; patch 004 |
| F05 | Moderate | No direct message-copy action or truthful copy-preview semantics | Source and clipboard tests; patch 005 |
| F06 | Moderate | QA temporary-path comparisons depend on Windows short/long path spelling | Test-fixture correction; patch 006, production guards unchanged |
| F07 | Moderate | Accessibility fixtures lag exact identity, role, accessible-name and focus-order contracts | Reproduced Windows browser failures; patch 007 |
| F08 | Important | No complete editable settings/instruction persistence-to-runtime pipeline | Source confirmed; S001 |
| F09 | Important | Attachments are absent across composer, bridge, turn construction and saved transcript projection | Source confirmed; S002 |
| F10 | Important | Exposed provider reasoning has no complete preserved/rendered conversation path | Source confirmed; availability differs by provider; S003 |
| F11 | Important | Plain text plus a separate activity stack is not a rich chronological conversation | Source confirmed; S004 |
| F12 | Important | Existing provider implementations lack first-class extensible profile/capability administration | Source confirmed; S005 |
| F13 | Important | NVIDIA exact assistant replay is process-local and bounded; restart/eviction can remove required continuation data | Source-confirmed persistence limitation, not a live endpoint reproduction; S006 |
| F14 | Important | Long-operation budgets, uncertain outcomes and busy-composer behavior need an integrated user-facing lifecycle | Missing UX/configuration confirmed; particular races require tests; S007 |
| F15 | Important | Personal-file/system boundaries are not technically enforced against unrestricted model tools | Accepted architecture limit, not a newly discovered containment bypass; S008 |
| F16 | Moderate | Unfamiliar completed-item statuses can become failure in terminal diagnostics rather than Unknown | Source-confirmed classification mismatch; S007 |
| F17 | Moderate | Literal TODAY grouping and limited older-turn navigation misrepresent or constrain restored conversations | Source confirmed; S004 |
| F18 | Optional | Actual tool-schema overhead and discovery efficiency are unmeasured in the private request builder | Investigation item, not an asserted prompt-bloat defect; S009 |
| F19 | Important | Whitespace-only assistant deltas are discarded before batching | Reproduced red/green tests and actual handler wiring; patch 008 |

### Projection and streaming correctness: F01, F03, F19

`transcript.mjs` uses explicit and fallback assistant identities. Equal text is not sufficient evidence of duplicate events: two different assistant items may legitimately say the same thing. Patch 001 retains explicit identity and confines fallback matching to the compatible current turn/tail. It changes a display projection, not the App Server journal.

The baseline tool paths disagree about output precedence and types. A result/error object may escape a string-only cap in `AppServerNotificationProjection`, then disappear when another reader asks for a string. Patch 003 introduces display-only `ToolOutputText`, preserves structured/dynamic outputs, explicit unsuccessful results, exit code and duration, and aligns live, saved and range-read views. It does not promise an arbitrary-size RPC memory bound or use bounded display text for provider replay.

`App.tsx` passes stream deltas through `textValue`, which rejects strings whose trimmed length is zero. Splitting `Hello`, ` `, `world` therefore produces `Helloworld`; newline/indent-only chunks are affected too. A later authoritative completion might repair a completed message, but an interrupted stream cannot rely on that. Patch 008 adds a lossless nonempty-string admission helper, wires both assistant event aliases and tests interruption plus actual handler usage.

### Customization and provider administration: F02, F08, F12

The settings-looking control opens diagnostics. `CodexModelCatalogBuilder` supplies a compiled base instruction; `CodexConfigBuilder` and `RuntimeSupervisor.EnsureClientAsync` generate runtime inputs. Editing generated TOML or putting text into localStorage alone would not be durable customization.

Patch 002 adds a useful default goal-ownership policy now. S001 defines host-owned revisioned settings, atomic recovery, effective instruction composition, precise save/pending/applied states and idle-boundary runtime propagation. It explicitly avoids duplicating the same prompt in catalog, config and user messages. S005 adds user-owned provider profiles and immutable capability snapshots without rewriting historical release evidence or weakening exact selection.

`CapabilityRecordPathResolver` currently restricts ordinary Release records to bundled release paths; the host explicitly recognizes LM Studio, OpenRouter and NVIDIA. Those are real supported paths, not an already universal provider plug-in system. New compatible endpoints need adapter/request evidence. Preserve OpenRouter routing without fallback and NVIDIA's deliberate adapter/model rules.

### Content and conversation UX: F04, F05, F09–F11, F17

The composer and host `startTurn` path accept text, output limits and reasoning effort, not attachment descriptors. `TurnStartOptions` constructs text input; `ThreadTranscriptProjector` omits non-text content. S002 therefore starts with durable payloads, manifests, exact submission receipts and provider admission, before adding a picker. It includes image-only sends, explicit file scope, restart/fork behavior and reference-aware cleanup. Unsupported images must not silently become filenames.

Reasoning effort and token usage already exist, but `AppServerNotificationProjection` and saved transcript reconstruction do not provide a reasoning timeline. NVIDIA can translate exposed `reasoning_content`; that alone does not make it visible or durable in the app. S003 separates readable summaries, provider-native text, metadata-only, ordinary-text-only, opaque continuation and restricted channels. No fabricated hidden reasoning or decoding private continuation for display.

S004 replaces the all-messages-then-all-tools arrangement with a journal-derived item timeline, safe Markdown/code, typed tool presentation, actual dates, older-turn paging and user-controlled follow-scroll. It preserves bounded history and existing conversation controls. Patches 004/005 provide narrow immediate improvements: Readable/Wide and Copy message/Copy visible preview with honest failure feedback. They are not substitutes for S001 or S004.

### Continuation and task ownership: F13–F16

NVIDIA `Program.cs` stores exact native assistant messages in `AdapterState.AssistantReplay`, with 128-message/4 MiB cache limits. Some replay paths require that state; Kimi explicitly needs exact reasoning/tool-call material. Host restart starts a new adapter with no durable replay root. Saved visible messages are insufficient to reconstruct that private transport state. S006 defines immutable exact replay, alias identity, durable completion ordering, fork mapping and restart/corruption tests. Existing fail-closed behavior remains preferable to inventing continuation content.

Turns and compaction use fixed ten-minute host budgets; the renderer uses a corresponding but separate long timeout, and selected provider retry counts are fixed at zero. The busy composer cannot even prepare the next draft. S007 establishes operation identity, acknowledgement versus completion, honest unknown outcomes, coherent budgets and safe drafting/queue/qualified steering. It does not blindly replay an accepted installer or file mutation after a lost response.

Full-user shell already supports much routine installation, helper creation and configuration. The missing product foundation is consistent task ownership, scoped grants and observed postconditions, not a dedicated API for every action. S008 defines that contract and preserves the essential warning that prompts/host checks do not constrain arbitrary unrestricted shell. S009 measures actual request overhead before proposing discovery changes; no giant-tool-prompt finding is fabricated from a candidate registry that is not callable.

## Architecture and integration guidance

Keep Core's reusable record/projection role, Host's native/lifecycle ownership and React's rendering role. The amount of orchestration in `App.tsx` and `RuntimeSupervisor.cs` warrants extracting cohesive feature controllers during implementation, not a mandatory rewrite before useful work.

Use one effective configuration pipeline, one journal-derived conversation model and exact provider-native replay where required. A UI cache is not a settings authority. Display truncation is not replay serialization. DPAPI-protected credentials may require re-entry after OS reinstall even if Data survives. New durable stores must join existing recovery conventions explicitly, including fixed record keys and future-schema handling.

For current Codex comparisons, consult [CODEX_SOURCES.md](codex-parity/CODEX_SOURCES.md). The documented baseline supports typed input, lifecycle events and configuration concepts that justify the proposed product work. It does not prove those newer methods exist in the pinned runtime or establish every platform's closed-source desktop UI. Generate and verify schemas from the exact locked executable before adding protocol operations.

## Priority, genuine decisions and non-recommendations

First apply the eight bounded patches with their declared dependencies and run the supplied checks. Then implement S001 settings/instructions and S006 replay in parallel. Build attachment persistence and reasoning projection next; stage S004's safe text/code/timeline work independently where possible. Extend provider administration and operation lifecycle only with exact capability evidence. Measure tool overhead before redesigning orchestration.

No product decision blocks the patch batch or the proposed conservative defaults. Credentials and the omitted locked runtime are execution prerequisites for later qualification, not reasons to ask routine design questions. A new decision is required only before promising technical containment under unrestricted execution; that tradeoff is deliberately deferred.

Do not replace WPF/WebView2/Astryx, change to an OpenAI-only product, silently update the runtime, enable generated tools because a review exists, substitute providers, force new chats for accepted same-chat switching, add approval spam, auto-read personal browser/files, implement fake settings, display invented reasoning, or automatically repeat unknown side effects. Packaging, broad computer control and universal discovery are not prerequisites for this first batch.

The eight patches are implementation proposals; the nine specifications are not claimed as implemented. Builds, synthetic tests, browser checks and native/live gaps are recorded separately in [VERIFICATION.md](codex-parity/VERIFICATION.md).
