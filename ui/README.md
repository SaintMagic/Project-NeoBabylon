# Diagnostic desktop UI

This is the first Codex-first NeoBabylon shell slice. The React/TypeScript UI is
served only inside the WPF/WebView2 host from the local `dist/` directory. The
host exposes named, validated product operations; React does not receive generic
App Server RPC or full-host shell access.

## Current behavior

- Renders the Codex-first conversation, task details, local workspace identity,
  provider/model selector, streamed assistant output, and attributed tool
  activity.
- Lists recent conversations from the isolated Codex App Server history,
  resumes only an exact provider/model/workspace match, and displays the latest
  20 turns. “New task” closes the current in-memory thread without deleting its
  saved session.
- Restores only user and assistant text into the presentation. The trusted host
  omits raw tool records and attachment payloads and caps the combined display
  text at 120,000 characters; the UI identifies a truncated preview. App Server
  remains the authoritative history; the renderer does not persist a second
  transcript database.
- Keeps credentials out of the renderer. Missing OpenRouter credentials fail
  before a provider turn is dispatched; no provider/model fallback is allowed.
- Shows unknown capability values as Unknown and keeps requested/effective
  authority distinct.

This is not yet the full Phase 2 client: workspace selection, approvals,
diff/review surfaces, reconnect/renderer-recovery behavior, pagination/search,
accessibility qualification, and long-output handling remain open. Phase 1B
also remains open; see [product status](../docs/product/STATUS.md), the
[roadmap](../docs/product/ROADMAP.md), and
[UI/UX reference](../docs/architecture/UI_UX_REFERENCE.md).

## Local build and tests

From this directory, with the repository-local Node dependencies available:

```powershell
npm test
npm run typecheck
npm run build
```

The host loads `dist/`, so rebuild after UI changes before launching WPF. The
UI package dependencies and bundled fonts are listed in
[`package.json`](diagnostic/package.json) and the lockfile. `node_modules/`
and generated `dist/` output are local build artifacts, not runtime state or
the authoritative provider/model capability record.
