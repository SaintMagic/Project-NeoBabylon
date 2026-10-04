# NB-DEC-005 — Application data and credentials

Status: **Accepted data boundary; path and migration mechanics remain open.**
Date: 2026-09-22.

## Decision

For clarity, the source-repository root, the Codex-derived runtime-repository
root, and the application root are different concepts. The product and
runtime repositories are source-controlled siblings. In this decision,
`<NeoBabylon root>` means the application/install root that owns a running
NeoBabylon instance; it does not silently redefine the product repository as
runtime state. A development fixture may choose a deliberate disposable
application root, but its `Data` remains runtime-owned and isolated rather
than ordinary source-controlled project content.

Durable NeoBabylon state belongs under:

```text
<NeoBabylon root>\Data\
```

This is the primary durable location, not a hidden LocalAppData store and not
the process current directory. It should survive replacement of application
files and an OS reinstall when the application/data folder itself survives.
Updates may perform an intentional compatible migration with a protected
pre-migration recovery path; they must never casually delete, reset, replace,
or clean durable state.

Codex runtime persistence remains authoritative inside its isolated data area.
NeoBabylon must not create a competing Sessions/History database or share the
ordinary user Codex home. One supervised App Server owns a NeoBabylon runtime
data root at a time. Model files remain wherever LM Studio manages them;
NeoBabylon stores configuration and references rather than copying weights.

An illustrative layout is:

```text
<NeoBabylon root>\Data\
├── Config\
├── CodexHome\
├── GeneratedTools\
├── Logs\
├── Diagnostics\
└── Backups\
```

The names are proposed organization, not a frozen schema.

## Credentials and disposable state

The host owns credential policy and delivery. The selected Codex mechanism may
physically consume or store a credential in the supervised runtime, but the
renderer, prompts, source tree, candidate manifests, workspace files, normal
child commands, and routine logs must not receive it. The working direction is
host-managed Windows-protected storage; the exact mechanism and re-entry
behavior after OS reinstall remain implementation details.

This prohibits direct host/runtime credential delivery through those channels;
it is not a confidentiality guarantee against unrestricted same-user tools.
Such a tool may independently read user-accessible files and return their
contents as ordinary output. Under [NB-DEC-011](0011-unrestricted-tool-output-visibility.md),
normal tool output is displayed and forwarded without content redaction.

WebView2 caches and temporary files may use an OS-appropriate disposable
location. They must not become the only copy of durable settings, history,
candidate records, or diagnostics Martin expects to retain.

## Remaining implementation details

- How the development and packaged application roots are discovered and
  validated.
- Backup/snapshot, migration, interruption, downgrade, and recovery behavior.
- Permission and log-redaction policy.
- Whether a deliberate existing-Codex-history import is ever added; no
  automatic import or shared ownership is implied.
- Narrow access from authorized candidate execution to `Data\GeneratedTools`
  without exposing credentials or unrelated runtime history.
