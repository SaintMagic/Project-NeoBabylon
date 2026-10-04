# S004 — Rich, chronological and navigable conversations

Priority: Important; individual cosmetics Moderate/Optional. Specification. Base: `f126612a00231a58626d72108aac6ee912431ee3`.

## Current implementation to complete

`App.tsx` renders all messages, followed by a separate activity stack, rather than interleaving agent text and tool execution. Message text is a plain div; copied text and width are addressed by patches 005/004. The separator is literally TODAY even for restored chats. `transcript.mjs`, `activity-history.mjs`, `restoration.mjs`, `history.mjs` and their tests already provide bounded display, identity and restoration logic. Preserve those semantics.

`ThreadTranscriptProjector`, `ThreadSavedOutputProjector`, `ThreadSavedChangeProjector`, `ThreadItemOutputRangeProjector` and `RuntimeSupervisor` provide saved content. `AppServerProtocol.BuildThreadTurnsListRequest` asks for a bounded recent turn set; it does not expose an older-turn cursor through the conversation UI. The saved-conversation list has its own pagination: do not misreport that as absent. Existing new task, rename, fork, model switch, project switch and tracked-change review are real features.

## Proposed model and ordering

Introduce a Core projected conversation envelope and UI discriminated `ConversationItem` union: user, assistant, tool, plan, reasoning, change-summary and status/omission. Identity is `(threadId, turnId, itemId, kind, sectionIndex?)`; ordering comes from authoritative turn/item sequence, not wall-clock guesses or message text. Live arrival sequence is provisional and reconciles against the completed journal. Patch 001's item matching and patch 008's lossless chunks must remain intact.

Reuse current bounded projection helpers; do not introduce a second conversation database. Keep the raw App Server source authoritative. UI item view state (expanded output, selected tab) can be cached by exact item ID. An update to a tool item changes that card in place; it never moves all tools below the final answer. Historical provider attribution remains attached to each turn across same-chat model switches.

## Rendering slices

First add safe Markdown for assistant text: paragraphs/lists/headings, fenced code with language label, tables and links. Disable raw HTML and dangerous URL schemes; no remote image fetching, embedded iframes or script execution. Code copy copies the exact source code, not highlighted DOM text. Preserve a plain-text fallback if parsing fails during an incomplete stream. The user's text remains distinguishable and is never interpreted as executable UI.

Next render tool cards with actual tool name, target/command where supplied, running/completed/failed/unknown status, duration, exit code, bounded output and inspect-more. Patch 003 provides data preservation, not the complete rich tool UI. MCP structured content gets type-specific rendering only after a schema is validated; unknown structured content stays inspectable JSON/text with its type label. Do not turn an unknown tool status into success or failure. The existing `TurnDiagnostics.Extract` unknown-status issue must be fixed and tested before unified terminal reconciliation can overwrite live cards.

Preserve existing file-change review as read-only and explicit about tracked versus shell-written files. Add plan progress only from runtime-supplied plan items. S002/S003 add attachment/reasoning variants later without blocking safe text/code rendering.

## Pagination and session affordances

Add named `loadEarlierTurns` with a host-owned cursor envelope bound to thread, workspace, capability/history identity and requested page size. Generate the exact runtime request type first. `ThreadHistoryCursor` is for the conversation list; do not accidentally reuse its provider/list scan cursor as a turns cursor. Merge overlapping turn pages by authoritative IDs, preserve the viewport anchor, and never start a model request to read history.

Use explicit 'Older messages not loaded' and 'Source unavailable' states. Loading a previous page must not imply all earlier content is in the model's active context. Add find-in-loaded-conversation with that scope in its label; a full-history search requires a separate bounded host operation. Dates use actual timestamps and local timezone, with 'Date unavailable' when missing, never fabricated Today.

Keep the composer usable for drafting while busy only after S007 separates draft state from submitted input. Add new-chat, focus composer, search-loaded-history and stop shortcuts in a small discoverable command list; test conflicts with native text editing and modal focus. Archive/unarchive can follow only if supported by the pinned runtime, clearly distinct from permanent deletion. Permanent chat/data deletion is not included in this first slice. Editing/retrying a past prompt must be an explicit branch/turn action, not rewriting journal history in place.

## Scrolling, performance and restoration

Follow streamed output only while the reader is near the bottom; scrolling upward suspends follow and exposes Jump to latest. Preserve this state across panel expansion and earlier-page loading. Bound mounted items or virtualize only after measuring the existing workload; do not hide arbitrary unrendered content without an accessible load path. Debounce live-region announcements. Restore the same item ordering, truncation notices and failure state from the journal after reload; uncertain partial output remains uncertain.

## Tests and acceptance

Fixture user → commentary → shell start/output/finish → commentary → MCP → final must have the same order live and after restart. Test identical assistant strings with different item IDs, late completion, unknown status, duplicate events, mixed-provider history, malformed Markdown, JavaScript/data links, massive code blocks, remote image syntax, missing timestamps, overlapping pages, stale cursors, scroll-anchor retention, keyboard copy, focus traps and narrow/wide windows.

Apply 001/003/008 first. Implement the item reducer and saved/live equivalence tests, then safe Markdown/tool cards, then pagination/navigation, then optional archive/search breadth. Keep current source bounds and inspect-more behavior. Acceptance requires native WPF plus browser keyboard tests; screenshot approval alone does not prove history integrity.
