# Versioned Codex capability inventory

Status: **P3-01 evidence inventory complete for the pinned source and
NeoBabylon surfaces inspected below. Explicit Unknowns and unqualified
behaviors are part of this result. This is not a capability promise, a
provider-support matrix, or approval to expose new tools.**

## Snapshot and interpretation

The source inspected here is the Phase 0 Codex archive
`rust-v0.155.1` / App Server `0.155.1`, revision
`be2951ea34f0d295ed0becf97079f92fa5f6950e`. Its source-archive SHA-256 is
`2DC56C1DB2CC3FB44FC8F132E1CB7064EE3188EE8F811580264BBC672AF0D442`.
The archive is retained under
[`reference/cache/codex/rust-v0.155.1/source`](../../reference/cache/codex/rust-v0.155.1/source/);
see the [reference index](../../reference/INDEX.md). The reference record
states that the archive has no Git history, the tag was not cryptographically
verified, and this captured source was inspected but not built.

“Present upstream” means only that the pinned source contains the named
implementation or contract. It does not mean the capability is enabled in
NeoBabylon, usable by a specific provider/model, or qualified end to end.
`Unknown` remains unknown unless the cited qualification record or a named
machine run proves otherwise. The LM Studio model was later unloaded; its
record below is historical qualification evidence, not current loaded-model
state.

Status wording is explicit per row: stable/default-enabled or experimental is
used only where the pinned feature metadata says so; otherwise source presence
does not imply a maturity stage. Product exposure is separately identified as
visible/verified, possible but unverified, absent from the named UI/bridge, or
Unknown.

## Category inventory

The read-only diagnostics catalog consumes the versioned
[`NEOBABYLON_TOOL_CAPABILITY_CATALOG.json`](NEOBABYLON_TOOL_CAPABILITY_CATALOG.json)
for product exposure and provenance. Selected-tuple operation evidence is
kept on each authoritative model record in `toolQualifications`; the UI does
not infer exact operation support from generic `tool_use` metadata.

| Category | Pinned source and upstream status | NeoBabylon exposure and evidence | Provider/model qualification and limitations |
| --- | --- | --- | --- |
| Built-in tools | Registry and handlers are present in `codex-rs/core/src/tools/registry.rs` and `codex-rs/core/src/tools/handlers/`; source maturity varies by tool and is not generalized here. | App Server-backed turns can emit tool activity; selected command, patch, MCP, dynamic-tool, and function-output events are rendered. No general catalog or per-tool enablement surface exists. See [`bridge.ts`](../../ui/diagnostic/src/bridge.ts) and [`App.tsx`](../../ui/diagnostic/src/App.tsx). | LM Studio: exact selected-model `exec_command` succeeded. OpenRouter: exact NEX route function call and function-form `apply_patch` succeeded. No other built-in or route is qualified; see [LM Studio](PHASE1A_LMSTUDIO.md) and [OpenRouter](PHASE1B_OPENROUTER.md). |
| Browser/computer use | Requirements/configuration are present in `codex-rs/config/src/browser_computer_use_requirements.rs`, `config/src/browser_use.rs`, and `config/src/computer_use.rs`; executor availability/maturity is **Unknown** from the inspected evidence. | No browser/computer product operation or named host-bridge surface is exposed. WebView2 is the product UI renderer, not evidence of a model browser tool. | Selected provider/model support, permissions, and execution status: **Unknown / unqualified**. |
| Screenshots and vision | `Feature::ViewImage` is **Stable / default-enabled** in `codex-rs/features/src/lib.rs`; registration is in `codex-rs/core/src/tools/spec_plan.rs`, handler in `codex-rs/core/src/tools/handlers/view_image.rs`, and thread-attachment handling in `codex-rs/app-server/src/request_processors/thread_attachments.rs`. | No image picker or screenshot-capture UI exists. NEX metadata says `text,image`; the host maps that into Codex `input_modalities` and does not explicitly disable `view_image`. A model-callable local-image path is **possible but unverified**; the actual advertised tool list/call was not inspected. LM Studio's `vision=False` maps to text-only modalities. | NEX image input is provider-advertised, not live image-qualified. LM Studio Qwen3-14B was recorded `vision=False`. Image dimensions, path handling, route behavior, and failures remain **unqualified**. |
| Large documents | Upstream file-read and file-oriented command handlers are present, including `exec-server/src/file_read.rs`; no dedicated “large-document” maturity/availability contract was established. | `readOutputRange` pages an existing App Server item with a 32,768-character limit; it is not a distinct arbitrary-document reader. Ordinary model-directed shell access is a separate indirect path under the accepted unrestricted policy, not a NeoBabylon large-document UI. | P2-11 verified bounded saved tool-output paging for a deterministic fixture. Arbitrary document ingestion, source truncation, and omitted-range retrieval remain unqualified. See [P2-11 evidence](VERIFICATION.md#2026-09-24--phase-2-p2-11-bounded-native-large-output-cancellation-and-failure). |
| Search | File search and fuzzy-search session code are present in `codex-rs/file-search/src/lib.rs`, `codex-rs/app-server/src/request_processors/search.rs`, and `codex-rs/core/src/tools/handlers/tool_search.rs`; provider availability is separate. | The UI filters loaded conversation-history pages; no workspace-search UI is exposed. The host model catalog sets `supports_search_tool=false`. | Web-search support is **unqualified**. LM Studio qualification logs say the provider ignored a web-search tool type; `web_search_tool_type="text"` is a mapping value, not provider evidence. |
| Patch | `apply_patch` and its specification are present in `codex-rs/core/src/tools/handlers/apply_patch.rs` and `apply_patch_spec.rs`; source presence is not universal tuple support. | Patch review and selected activity are exposed through App Server-backed turns. | One function-form `apply_patch` round trip is recorded for the exact OpenRouter/NEX route on the patched runtime. LM Studio patch support and general failure/authority behavior remain Unknown or unqualified. |
| Shell | Unified execution and command handlers are present in `codex-rs/core/src/tools/handlers/unified_exec.rs` and `unified_exec/exec_command.rs`; `Feature::ShellTool` is **Stable / default-enabled** in `codex-rs/features/src/lib.rs`. | Model-directed `exec_command` is exposed through normal App Server turns. There is no shell panel or `thread/shellCommand` bridge operation; `thread/shellCommand` remains human-only and is not a model-controlled fallback. | One ordinary command round trip is recorded for each selected provider tuple. The accepted unrestricted setting is `danger-full-access`/`never`, not containment. Broader support, denial, timeout, cancellation, and partial-output cases remain open. |
| Compaction and context | Automatic/manual compaction and App Server compact requests are present in `codex-rs/core/src/compact.rs` and `codex-rs/app-server/src/request_processors/thread_processor.rs`; no separate feature stage is inferred. | Effective context is shown. The host projects compaction completion items, but the renderer does not display them as activity; there is no explicit compact control. | Deterministic loopback evidence verifies model metadata → effective context → automatic compaction for a synthetic context. Live provider usage/budget triggering and cross-model behavior remain unqualified. |
| Skills | Filesystem discovery/loading and result metadata are present in `codex-rs/skills/src/loading.rs`; actual activation state is **Unknown**. | No skill catalog or skill-specific operation is exposed. The model catalog sets `include_skills_usage_instructions=false`. Skill-file text remains untrusted content, not product instructions. | Per-model loaded state, execution, permissions, and failure behavior are not qualified. |
| Hooks | Hook registry/configuration/dispatch code is present in `codex-rs/hooks/src/registry.rs`; current runtime activation is **Unknown**. | No hook catalog, configuration, or control operation is exposed; no product evidence establishes whether hooks are loaded. | Trust, configuration provenance, timeout, denial, and outcomes are not qualified. Hooks are not a complete policy boundary. |
| MCP | Connection management and App Server request processing are present in `codex-rs/codex-mcp/src/connection_manager.rs` and `codex-rs/app-server/src/request_processors/mcp_processor.rs`; configured state is **Unknown**. | MCP call activity can be rendered, but server discovery, setup, status, and authentication are not exposed. | No selected LM Studio/NEX MCP server/tool qualification is recorded; identity, transport/auth, permission, timeout, and denial remain Unknown. |
| Plugins | Plugin management and App Server request handlers are present in `codex-rs/core-plugins/src/manager.rs` and `codex-rs/app-server/src/request_processors/plugins.rs`; installed state is **Unknown**. | No plugin catalog/install/enable operation is exposed. The model catalog sets `include_plugin_usage_instructions=false`. | No model/provider qualification or NeoBabylon plugin-install evidence is recorded. Packaging and install side effects remain deferred. |
| Dynamic tools | The `thread/start.dynamicTools` registration field is **Experimental** in `codex-rs/app-server-protocol/src/protocol/v2/thread.rs`; server-side registration is implemented in `codex-rs/app-server/src/dynamic_tools.rs`. The `item/tool/call` callback declaration in `codex-rs/app-server-protocol/src/protocol/common.rs` and its parameter/response types in `protocol/v2/item.rs` are not themselves marked experimental there. | Stable protocol selected; `experimental_supported_tools` is empty. A rendered `dynamicToolCall` event does not enable registration. No registration/hot-reload operation is exposed. | Dynamic-tool registration support for LM Studio and NEX is **Unknown / unqualified**. The experimental registration capability remains off unless separately opted into and qualified. |

## Qualification boundaries

- The exact local tuple records live for one LM Studio command and a narrow
  set of NEX/OpenRouter function calls; see the linked reports above. These
  are not capability-wide certifications.
- The current NEX alias is `nex-agi/nex-n2.5-pro:free`, request-pinned to
  `nex-agi/fp8` with fallback disabled in the recorded live tests. The recent
  direct Responses output test did not traverse App Server/WPF and its
  generation lookup returned 404; it is not evidence of route attestation.
- The NEX capability snapshot records model-catalog expiration `2026-09-25`
  and endpoint-specific expiration as Unknown. It was not refreshed in this
  inventory; refresh volatile metadata before another live qualification.
- Existing WebView UI surfaces report only what the host/runtime supplies.
  Unexposed categories remain unavailable or unqualified, not callable
  placeholders.
- This inventory alone does not close any P3 or phase gate. The bounded P3-03
  read-only diagnostics catalog and its UI/native evidence are tracked
  separately in `VERIFICATION.md`. No category may be enabled based only on
  this source scan, and all deferred Phase 1B gates remain deferred.
