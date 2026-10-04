# NB-DEC-003 — Windows desktop shell

Status: **Accepted direction; implementation qualification remains open.**
Date: 2026-09-22.

## Decision

Use a thin WPF/.NET Windows host containing WebView2. React, TypeScript, and
Astryx remain the interface direction. Codex App Server remains a separately
supervised process connected through a typed product bridge.

“Thin” describes responsibility boundaries, not an arbitrary line-count
limit. The host owns desktop lifecycle, App Server supervision, native
integration, configuration/credential policy and delivery, and validated
bridge operations. The renderer owns screens, interaction, drafts,
navigation, and derived view state. Codex owns agent execution, ordinary tool
orchestration, session semantics, and the persistence it already owns.

CanonWell may be inspected as a read-only structural reference for relevant
WPF/WebView2, bridge, lifecycle, and packaging patterns. CanonWell services,
domain data, storage, credentials, and product rules are not imported.

## Required qualification

The first host work is a small disposable WPF/WebView2 process fixture. It
must prove process supervision, typed message/bridge flow, shutdown, and
failure visibility around the pinned App Server before a polished desktop is
built. It should not implement competing Electron, Tauri, or other shells.

## Implementation details still open

- Exact .NET SDK, WebView2 SDK, runtime deployment, and package versions.
- The named product operations exposed to React and their validation rules.
- Accessibility, packaging, update, signing, and memory evidence from the
  real fixture.
- Development versus packaged process/data-root discovery.

No system-wide dependency installation is authorized by this record. Existing
machine capability must be inspected before proposing any additional setup.
