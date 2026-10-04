# NeoBabylon architecture

Status: **Accepted boundaries and settled direction plus proposed
implementation details.** This document records what the product must
preserve, what Martin selected, and what still needs qualification.

Phase 1A established the bounded WPF/WebView2 host, named bridge, isolated
runtime supervisor, and LM Studio qualification path. Subsequent explicit
authorization advanced work to the first Codex-first React/TypeScript/Astryx
client shell and bounded Phase 2 feature slices. Those implementation results
do not mean the full Phase 2 gate passed or authorize the later broad toolbox,
self-scaffolding, or packaging scopes.

Phase 1B produced the LM Studio capability projection, Windows policy
diagnostics, typed tool/turn events, and a focused runtime route-control patch.
Martin later deferred the remaining Phase 1B checks and explicitly authorized
Phase 2 with unrestricted tools; those deferred checks remain open, not passed.
The `thread/shellCommand` host escape hatch remains prohibited for
model-controlled tools.

## Accepted direction

- NeoBabylon is a Windows desktop client for a structured coding-agent
  runtime.
- The selected host direction is WPF/.NET with WebView2.
- The interface direction is React/TypeScript/Astryx.
- Martin selected a Codex-first interaction reference for the future
  functional client. Its adopted baseline and still-proposed extensions are
  recorded in [UI_UX_REFERENCE.md](UI_UX_REFERENCE.md). Phase 2 is the current
  execution phase under Martin's explicit unrestricted-tools authorization;
  this does not imply that any deferred Phase 1B gate passed.
- The starting runtime is Codex App Server, connected through a typed protocol
  rather than terminal scraping.
- Product source and Codex-derived runtime source are separate local sibling
  Git repositories; no remote NeoBabylon repository is required.
- LM Studio and OpenRouter are the initial provider qualification targets.
- Durable state belongs under `<NeoBabylon root>\Data`; ordinary Codex user
  state is not shared automatically.
- Free hosted or local inference is required; paid OpenAI use is not a product
  prerequisite.
- Runtime execution and session persistence are not duplicated in the UI.
- Authority, permissions, approvals, provider choice, and failures remain
  visible and explicit.
- NeoBabylon is private local software for the current scope; upstream and
  third-party notices remain applicable.
- Android, hosted deployment, multi-platform expansion, and public
  distribution are outside the initial target.

## Observed upstream facts

The pinned App Server defaults to `stdio://`, uses newline-delimited JSON over
stdin/stdout, requires an `initialize`/`initialized` handshake, and streams
thread/item/turn events. The source has durable rollout/thread-store/history
paths, SQLite-backed queue metadata, recovery support, provider capability
checks, Windows sandbox code, and server-initiated approval requests. Its
`thread/shellCommand` endpoint is explicitly documented as full-host-access,
outside the thread sandbox, and for explicit user-initiated commands only.
These facts are recorded in [Reference](../../reference/INDEX.md) and the
[verification report](../release/VERIFICATION.md).

## Selected logical topology

```text
WPF/.NET desktop host
    ├── WebView2
    │     └── React / TypeScript / Astryx UI
    ├── typed product bridge and native integration
    └── supervised Codex App Server process
              └── selected LM Studio or OpenRouter connection

<NeoBabylon root>\Data\
    ├── isolated Codex runtime state
    ├── configuration, logs, diagnostics, backups
    └── retained generated-tool candidates
```

Here `<NeoBabylon root>` uses the application/install-root meaning defined in
[NB-DEC-005](../decisions/0005-application-data-boundary.md), not an automatic
alias for the source repository root.

The initial transport recommendation remains a supervised local `stdio://`
process. WebSocket is documented as experimental/unsupported and a local
network listener introduces an avoidable authentication and lifecycle surface.

## Responsibility boundaries

| Boundary | Owns | Must not own |
| --- | --- | --- |
| React/WebView2 UI | Presentation, drafts, navigation, derived view state, approval display, diagnostics views, Unapproved Tools drawer | Codex rollout persistence, provider credentials, hidden retries, generic RPC forwarding, authority policy, arbitrary approval responses |
| WPF/.NET host | Desktop lifecycle, App Server supervision, runtime identity checks, workspace/root validation, app-data ownership, single-owner enforcement, credential policy and delivery, native integration, validated product operations, trusted interaction channel for approvals | Model reasoning, duplicate session history, silent provider fallback, renderer access to secrets, direct SQLite mutation |
| Codex App Server | Agent loop, thread/turn lifecycle, ordinary tool execution, provider interaction, rollout/history, approval semantics, sandbox policy, interruption and recovery | UI layout, NeoBabylon-specific candidate promotion policy |
| Provider boundary | Endpoint/model configuration and compatibility evidence | Rendering concerns, unannounced model changes, bypassing runtime authority |
| Maintained tools | Explicitly reviewed domain actions, schemas, permissions, diagnostics | Arbitrary self-installation or implicit trust |
| Unapproved candidates | Generated/testable proposals and temporary task helpers within existing authority | Permission expansion, self-promotion, full-host escape, silent installation |

App Server owns approval semantics and emits approval requests. The host owns
the trusted channel through which Martin's decision is obtained and returned;
the renderer displays context but cannot manufacture arbitrary authority
responses. Unknown authority-bearing requests fail closed. Unknown
informational events may be preserved as inspectable diagnostics.

## Lifecycle shape

1. The host validates the selected workspace and `<NeoBabylon root>\Data`,
   checks the expected runtime version/hash, and starts the pinned App Server
   with isolated runtime configuration.
2. The bridge opens the local stream, sends exactly one `initialize`, and
   acknowledges `initialized` before forwarding validated operations.
3. The UI requests `thread/start` or `thread/resume`; the runtime returns a
   durable thread identity and streams lifecycle/item events.
4. Server requests for approvals, user input, or deliberately enabled
   experimental tools are routed through the host with correlation IDs and
   explicit timeout/error behavior.
5. The UI can request `turn/interrupt`; the host keeps process and thread
   state observable until the runtime reports completion, interruption, or
   failure.
6. Reconnect/resume uses the runtime's durable identity and history. The UI
   rebuilds its view from runtime responses rather than maintaining a second
   authoritative transcript.

## Data and trust boundaries

- Source code and workspace data remain in the selected workspace and are
  accessed through runtime-authorized tools.
- Runtime rollout/history and SQLite state live under the isolated Codex area
  inside `<NeoBabylon root>\Data`. One supervised App Server owns that root at
  a time; normal product workflows use App Server APIs, not direct SQLite
  mutation.
- Provider credentials use host-owned policy and delivery. The host must not
  directly inject or serialize credential values into renderer state, prompts,
  source, candidate manifests, workspace files, child-command environment,
  logs, or diagnostics. Under the accepted unrestricted policy, a tool may
  independently read user-accessible files and return their contents; normal
  tool output is displayed and forwarded without redaction under
  [NB-DEC-011](../decisions/0011-unrestricted-tool-output-visibility.md).
  NeoBabylon does not claim those files remain confidential from unrestricted
  tools.
- The bridge exposes validated product operations rather than a generic
  `sendRpc(method, params)` escape hatch. Generated stable types are an input
  to the adapter, not permission to expose every upstream method directly.
- NeoBabylon must never route model-controlled fallback tooling through
  `thread/shellCommand`; that full-host surface is explicit-human-only.
- Reference source and downloaded docs remain inert evidence under
  `reference/cache/`.
- Generated protocol contracts are derived artifacts tied to the baseline, not
  hand-authored application state.

## Host qualification remains required

WPF/.NET + WebView2 is selected; Electron/Tauri is no longer an open product
choice. The first functional Codex-first shell and bounded Phase 2 paths are
implemented and tested, but accessibility, full Phase 2 acceptance, process/
data-root discovery, packaging, updates, and signing are not thereby qualified.
The native host remains thin and limited to desktop/process/native/bridge
responsibilities; broad toolbox and self-scaffolding work remain in later
phases.
