# S001 — Settings and editable instructions

Priority: Important. Implementation specification, not an implemented patch. Base: `f126612a00231a58626d72108aac6ee912431ee3`.

## Current implementation

`ui/diagnostic/src/App.tsx` opens Runtime diagnostics from its settings-looking control. Appearance exists through `appearance.mjs`, `MainWindow.xaml.cs` and `Core/HostAppearance.cs`; width is added by patch 004. Context/output and reasoning-effort controls already exist. There is no general settings record or settings bridge operation. `Core/CodexModelCatalogBuilder.cs` places a compiled `NeoBabylonBaseInstructions` string in each generated model catalog and disables reasoning summaries. `Core/CodexConfigBuilder.cs` generates provider/runtime configuration. `Host/RuntimeSupervisor.cs:EnsureClientAsync` owns catalog/config generation and process reuse; this is the application point, not a renderer-only save button.

Also touch `Core/AppServerClient.cs:DiagnosticOperation.Parse`, `Core/AppServerProtocol.cs` named-operation admission, `ui/diagnostic/src/bridge.ts`, and the existing protected-record/recovery classes. Do not create an unrelated settings framework or overwrite normal Codex instructions.

## Proposed ownership and records

Add `Core/ApplicationSettings.cs`, `ApplicationSettingsStore.cs` and `EffectiveAgentConfiguration.cs`. Store one versioned, protected `Data/NeoBabylon/settings.json` with `schemaVersion`, monotonically increasing `revision`, `ui`, `agent`, `context`, `operations`, `diagnostics`, and default profile/workspace IDs. References are IDs, never credentials. Provider profile contents belong to S005.

`ui` includes theme, readable/wide conversation layout, font-size preference, reasoning visibility and follow-output behavior. `agent` includes an optional edited base instruction document and a separate personal developer-instruction document. `context` includes explicit inherit/default states rather than invented token limits. `operations` includes validated turn/idle/retry budgets from S007. `diagnostics` includes local retention and opt-in raw capture, not an automatic upload switch. Keep generated-tool activation outside general Settings.

Use `ProtectedDataRecordFile`'s locked replacement, validation and recovery mechanics. Add expected-revision comparison inside the same storage lock as commit (including the missing-record case); a UI read followed by an unrelated write is not optimistic concurrency control. Add a fixed Settings recovery key. A malformed existing record opens a recovery state, not a successful reset to defaults.

Defaults reproduce current behavior until a control has a qualified backing path. Missing file is a distinct first-run state. Existing local appearance/width may be imported once only when the host record is absent; save that migration before clearing the legacy key. Thereafter localStorage is only a render cache. Settings do not contain chat history or attachment bytes.

## Instruction composition and application

First generate and inspect the exact locked runtime schema/config behavior. Establish one injection point for each layer. The proposed composition is application base instructions (compiled default or explicitly edited replacement), personal developer instructions, and the runtime's existing workspace `AGENTS.md` handling. Task-specific scope is a separate turn-bound record from S008. Never insert the same instruction block into the catalog, generated TOML and user message simultaneously.

Expose an Effective instructions preview showing source/layer, text, hash, revision and application boundary. Label an edited application prompt as editable; do not imply it can override a remote model provider's policies or create an unsupported system role. The three user boundaries remain visible product requirements; prompts are not containment. An override that contradicts them must produce a clear validation conflict rather than silently disable the boundaries.

An effective configuration snapshot contains settings revision, instruction hash, workspace, capability identity and runtime-lock identity. In `EnsureClientAsync`, compare that snapshot alongside the current capability before reusing a process. Generate catalog and TOML from that single snapshot. Stage/readback the files and publish an applied-generation marker only after both are coherent; a partial update must not produce a mixed launch. Do not replace the binary lock.

UI-only preferences apply immediately. Instructions and runtime options are saved immediately but take effect at the next safe idle boundary. Show Saved / Pending for next turn / Applied revision N separately. Never terminate a live tool to apply Settings. If the pinned runtime cannot apply instructions to an existing thread without replacing it, block that transition with an explicit unsupported state and keep the saved edit; do not quietly fork or claim success. Qualify same-thread resume under the new configuration before enabling that path.

## Proposed bridge/UI

Add `getSettings`, `validateSettings`, `saveSettings(expectedRevision, patch)` and `getEffectiveConfiguration(threadId?)`. Return bounded structured validation errors, persisted revision, applied revision and pending fields. Host rejects duplicate/unknown fields, invalid enums, overlong instruction text, negative budgets and stale revisions. Suggested initial instruction limit: 64 KiB UTF-8 per document, with measured prompt size shown; this is a product bound, not a model context guarantee.

Use a real Settings dialog with General, Agent instructions, Models/providers, Execution, and Diagnostics sections; advanced provider controls appear only for the selected adapter's proven capabilities. Keep diagnostics accessible separately. Save/Cancel/Restore defaults are real transactions; Reset does not erase chats, credentials, projects or provider evidence. Unsaved-change navigation is handled locally without routine confirmation after every edit.

## Tests and acceptance

Test first-run defaults; revision conflicts including simultaneous creation; interrupted stage/journal/commit recovery; read-only/corrupt disk; future schemas; literal Unicode/newlines; migration only once; no secret fields; and workspace instructions not duplicated. Assert exact generated request/config fields for each admitted provider capability. Test saving during a turn, failed restart, stale apply acknowledgement and provider switching while a change is pending.

Acceptance: edit one instruction, see its exact effective source/hash, send a fresh turn, capture the model-visible request at a deterministic endpoint and prove the instruction occurs once; restart the host and prove the same saved revision is effective. Repeat with an existing thread and compare history/identity before and after. Verify UI-only changes do not restart the runtime. Native/live checks are required beyond mock UI. Rollback keeps the versioned settings record and uses explicit migration; older binaries must not overwrite unknown fields.

## Order

Implement store/CAS/recovery, effective composition tests, host propagation, then Settings UI. Ship the basic instruction editor before advanced retry/tool controls. S005 and S007 extend the schema only when their implementations are ready. No new product decision is required for these defaults.
