# Feature/UI development checkpoint — 2026-10-04

Martin authorized feature/UI implementation before behavioral/live testing.
The coordinator reviewed and delegated production code; changes remain
uncommitted. Packaging/install, containment and residual Phase 1B are deferred.
The source/build work started on October 3; its evidence root remains
`.local/Lab/Runs/Finishing-20261003`.

## Implementation and source review

- Candidate review captures exact manifest/listed-file bytes, retains linked
  snapshots, and provides bounded prior/current comparisons without inventing
  bytes for historical records that lack snapshots.
- Exact manual activation, append-only history, candidate-independent revoke,
  inert template and private-Node stdio MCP route source are integrated through
  fixed validated Host operations and React controls.
- Route acceptance is still false and the compiled qualification allow-set is
  empty. Review/Prepare/Stage are not callable activation. Runtime executable
  hash, selected capability/policy, private Node and fresh exact MCP schemas
  are separate qualification inputs; unknown/mismatched inventories deny a turn.
- Protected Projects/ForkBookmarks backup metadata and missing-only hash-bound
  restore are exposed. First-write journal evidence is preserved; unresolved
  project startup state blocks execution/project writes but permits recovery.
- Dark-default neutral UI, Codex-first hierarchy and existing icon/authority
  notice remain. No broad redesign was introduced.

Bounded independent source reviews closed two UI-state findings, protected
record recovery findings, and two Host integration blockers. The reviewed
runtime diff covers 108 tracked paths plus the six focused compiler/behavior
corrections; no remaining material source-review blocker was reported. See
`runtime-review/`, `ui-source-review.md`, `product-data-review.md` and
`product-feature-review.md` under the evidence root. Reviews do not establish
runtime behavior or tamper-proof containment.

## Runtime and assembled development artifacts

The upstream revision remains `be2951ea34f0d295ed0becf97079f92fa5f6950e`
(`rust-v0.155.1`, App Server `0.155.1`). Reviewed local source was compiled and
cut over to a fresh versioned path; the prior binary/manifest are retained.

| Artifact | Independently checked SHA-256 |
|---|---|
| `.local/Runtime/LockedBuild/20261003-a0c3ebdc/codex-app-server.exe` | `a0c3ebdc8d1d9f5b56327f5ea6d1502ee5a0b50fa174d50743f8c7a17ff54386` |
| `.local/App/Build/bin/NeoBabylon.Host/release/NeoBabylon.Host.exe` | `24c64a973bdbe8aacc1d530e0c3236d9715f38bef4b6aff4d346673326a2c607` |
| `.local/App/Build/bin/NeoBabylon.Host/release/NeoBabylon.Host.dll` | `1d43367bb38b26589ef697cc051dcb23c8f024546642bbd3b2783c698356f369` |
| `.local/App/Runtimes/Node/v24.18.0/node.exe` | `9a4eb5f1c29c6a2e93852ead46b999e284a6a5ca8bab4d4e241d587d025a52de` |

The runtime and private Node hashes match their product locks. Runtime build
and cutover receipts are in `runtime/`. Core Release and the full development
BuildOnly command both exited 0; Host/adapter/Core reported zero compiler
warnings/errors, while the UI bundle emitted 39 module-directive warnings and
one chunk-size advisory. `build-final/` retains raw stdout/stderr, exit-code
files and a 48-artifact receipt with stable second hash readbacks. The final
source-snapshot readback/content comparison remains the handoff check at this
document's first publication; its result is owned by the generated capture
receipt under `data/captures/`. Do not infer native launch success from builds.

Use `tools/Launch-Development.ps1` for the assembled development host,
`-PromptForApiKey` for optional secure process-only credential entry, and
`-BuildOnly` for compilation without launch. Its explicit default is
OpenRouter / `stealth/space-bunny-alpha`, not a live-qualified tuple. Application
root is `.local/App`; durable state remains `.local/App/Data`, not ordinary
Codex state. The launcher does not install dependencies or substitute models.

## Remaining qualification

No refreshed native launch, live provider request, behavioral test suite,
generated-tool callable acceptance, accessibility pass or performance verdict
was performed in this implementation pass. Previous dated native/live results
remain historical evidence for their exact binaries and tuples. Completion of
source features is not full Phase 2–5 acceptance. Performance thresholds still
require Martin's input before a pass verdict. No shared installation, unrelated
project, global instructions/configuration, remote repository or Git history
was changed by this pass.
