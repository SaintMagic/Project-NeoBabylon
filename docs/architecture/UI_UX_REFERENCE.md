# Codex-first UI/UX reference

Status: **Adopted as the interaction reference for the future functional
desktop client; not a pixel specification or phase authorization.**

## Authority and phase boundary

Martin selected the supplied report as NeoBabylon's UI/UX reference: **Codex
first, NeoBabylon where necessary**. That direction sits inside the already
accepted WPF/.NET + WebView2 host, React/TypeScript/Astryx interface, thin-host
boundary, explicit provider identity, and runtime-owned session state.

The approved [build plan](../WIP/INITIAL_BUILD_PLAN.md) remains the acceptance
authority. Martin later explicitly authorized continuing through the later
phases, including UI work, while Phase 1B remains open. A limited first shell
slice has therefore been implemented early; that does not waive any Phase 1B
gate or mean the full Phase 2 acceptance gate has passed.

## Source and evidence limits

The source is Martin's supplied `Codex first UI Reference for NeoBabylon.md`,
research cutoff 2026-09-22, SHA-256
`148D8C6179342F151EC32167778129EB16023D570F7B009C325A359C2092566D`. The
report summarizes first-party documentation and screenshots and labels direct,
inferred, and unknown findings. Its citation markers are not stable URLs in the
provided file, and this reconciliation did not independently re-check the
current installed Codex/ChatGPT desktop. Treat its interaction patterns as a
secondary design reference, not fresh proof of exact September labels, layout,
or micro-interactions.

The report's statement that Codex is presented within the ChatGPT desktop app
is contextual evidence about the referenced product, not a requirement to make
NeoBabylon a ChatGPT extension or to change its accepted standalone WPF
architecture. That product-state claim was not independently re-verified here.

## Adopted interaction baseline

These are the design grammar to carry into Phase 2. They guide the experience;
they do not prescribe exact chrome or make every Codex feature a NeoBabylon
requirement.

- Start in dark mode by default. Offer an explicit light/dark toggle and persist
  the user's choice in the application-owned WebView2 profile; do not silently
  substitute the operating-system theme.
- Keep the task/conversation central. Prefer a human-oriented
  **Project → Chats** organization; execution environments such as Local or a
  worktree belong to a chat/task rather than becoming a competing navigation
  hierarchy.
- Keep the transcript as a view of App Server events; App Server remains the
  authority for thread history. Show
  user intent and the final result prominently; render reasoning summaries,
  commands, tool calls, approvals, and file changes as attributable activity
  that can be inspected without overwhelming the conversation.
- Put the composer and task decisions together. Provider/model identity,
  supported reasoning controls, permissions, and environment should be
  discoverable near the composer, with technical detail available on demand.
- Place deep inspection in contextual secondary panes: review/diffs, sources,
  files, provider/runtime diagnostics, and artifacts. Keep the interactive user
  terminal in a bottom drawer and visually distinct from agent-executed command
  activity in the transcript.
- Present approvals inline at the point where execution pauses, with enough
  command/diff context to make the decision. Keep approval authority in the
  host/App Server boundary; renderer presentation does not grant authority.
- Use progressive disclosure and restrained visual density: concise summary
  first, expandable details second. Preserve clear typed success, interruption,
  and failure states rather than inventing progress or disguising errors.
- Keep the composer discoverable through visible controls and, where supported,
  a command/mention path; do not make advanced actions settings-only.
- Adapt provider/model controls to actual discovered capabilities. Show exact
  requested and reported identity; expose only observed supported reasoning
  settings; keep Unknown unavailable/non-actionable; never silently substitute
  a provider, model, endpoint, or setting.

The latter capability and identity rules reinforce existing accepted product
requirements; the placement and visual treatments remain Phase 2 design
guidance.

## NeoBabylon-specific extensions remain proposals

| Report idea | Status in NeoBabylon |
| --- | --- |
| Compact context-usage indicator with detailed provenance | Proposed extension. Do not show a numeric value unless the effective runtime value and its source are known. Distinguish provider context, Codex usable context, and an auto-compaction threshold. |
| Optional live performance/provider metrics | Proposed and off/collapsed by default. Label values as runtime-measured, provider-reported, estimated, or unavailable; cost is never implied from an estimate. |
| Provider/runtime diagnostics in a contextual pane | Proposed Phase 2 presentation of already-required identity/capability truth; it is not a second provider or session authority. |
| Unapproved Tools drawer and candidate lifecycle | Phase 4 direction already recorded in project planning. Exact location and interaction are proposals; generation or successful execution never approves a candidate. |
| Upstream-feature tracker | Deferred developer-only surface; not ordinary navigation. |
| Exact sidebar labels/order, keyboard bindings, pixel dimensions, hover states, animation, and tool-card styling | Unknown until direct observation of the installed client and later usability checks. Do not copy old screenshot literals as current facts. |

## Current implementation truth

- `host/NeoBabylon.Host/MainWindow.xaml` is a WPF window containing a
  WebView2 surface.
- `ui/diagnostic/src/App.tsx` renders the React conversation-first shell in
  the local WPF/WebView2 host. It includes capability selection, streaming,
  attributed activity, diagnostics, saved-thread listing/resume, and a
  history-preserving “New task” operation.
- App Server remains authoritative for thread history. Resume is bounded to
  the latest 20 turns and requires exact saved provider/model/workspace
  identity. Before crossing the host-to-renderer bridge, the host projects
  only user/assistant text, omits raw tool records and attachment payloads,
  and caps aggregate display text at 120,000 characters; the renderer does
  not keep a separate durable transcript.
- One graceful WPF/App Server restart, WebView page reload, saved-thread
  listing, and same-thread resume was subsequently verified with the same
  isolated application/Data root and no inference; crash recovery remains open.
- The live WPF host and saved-thread restore/new-task/reopen cycle have been
  smoke-tested with an isolated OpenRouter session and no additional
  inference. Bounded inline approval cards now have synthetic-bridge and
  fake-App-Server coverage; no real approval prompt has been qualified, and
  file-change approval stays deny-only until its diff is reviewable. Workspace
  selection, full diffs/review, reconnection, pagination, accessibility, and
  long-output behavior remain unbuilt or unqualified.
- Basic saved-history search by preview/provider/model is implemented as a
  local filter; this does not provide server-side pagination or indexing.
- Dark mode is the first-run default. The accessible light/dark toggle persists
  its choice through WebView2 local storage, under the host's isolated
  application-owned WebView2 user-data directory. Browser checks cover both
  modes, reload persistence, and the 1440×900 and 960×720 desktop viewports.

Therefore the accurate status is: **the first Codex-first React/WPF shell slice
exists; the full Phase 2 product client does not yet pass its acceptance gate.**
See [current product status](../product/STATUS.md) for verified scope and open
gaps.

## Phase 2 use

Use this reference when the approved plan reaches the functional desktop
client. Before fixing exact current labels or small interactions, inspect the
installed contemporary client because the report itself identifies those
details as time-sensitive or unknown. Implement only the surfaces required by
the Phase 2 acceptance criteria; the reference does not pull Phase 3/4
management, metrics, or candidate UI forward.
