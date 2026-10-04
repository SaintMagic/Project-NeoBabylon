# Desktop host boundary

Phase 1A contains a bounded WPF/.NET + WebView2 diagnostic host and a named,
validated product bridge. The host owns process supervision, runtime identity
and isolated application data paths, native integration, and attributed
diagnostics. The pinned Codex App Server owns agent execution, approvals,
thread state, persistence, and ordinary tool semantics.

The host is intentionally thin: it exposes named operations such as runtime
status, diagnostics, thread start, and turn start; it does not expose generic
RPC or model-controlled `thread/shellCommand`. The React/TypeScript/Astryx
surface now contains the first conversation-first desktop shell slice, hosted
in this WPF window. It includes capability/runtime details, App Server-backed
saved-thread browsing, streamed activity, and bounded inline approvals. This
is an early Phase 2 implementation, not a complete product UI or live
approval/recovery qualification; workspace selection, full diff/review,
reconnection, pagination, accessibility, and long-output behavior remain
open. Packaging and broad native integration also remain deferred. The
current desktop host explicitly selects Codex `danger-full-access` /
`approvalPolicy=never` for user-authorized unrestricted tools, verifies the
effective App Server response, and labels the UI “Full access · no
containment.” The older `NEOBABYLON_WINDOWS_SANDBOX_MODE` setting is rejected
by this host; it is retained only by isolated historical qualification tools.
The application uses a dedicated `CODEX_HOME`, but unrestricted same-user
commands can reach other files and network resources. The Windows containment
gate remains deferred and unqualified.
