# Upstream strategy

Status: **Accepted source relationship; update and patch mechanics proposed.**
The Phase 0 cache is inspection material only.

## Baseline

The selected reference is Codex stable `rust-v0.155.1` at commit
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, with matching Windows x64 App
Server and CLI binaries. Phase 1A qualifies the stock executable path first;
it does not require a source build.

## Selected repository relationship

NeoBabylon product source and Codex-derived runtime source are separate local
sibling Git repositories. No GitHub repository or hosted fork is required.
The product repository owns WPF/WebView2, React/Astryx, bridge, tests,
maintained tools, and docs. The runtime repository preserves upstream layout,
history, notices, and focused upstream-facing changes.

The exact sibling name, product-side manifest/lock format, and source/binary
build wiring remain implementation details. Do not create a nested runtime
repository, vendor the full runtime into the product tree, or add a submodule
merely to satisfy a generic repository pattern. Repository creation and any
history operation remain separately authorized actions.

## Why this boundary

It keeps product ownership separate from upstream implementation ownership,
makes runtime identity auditable, and permits a focused provider/runtime
adaptation without turning NeoBabylon into a rewritten agent platform. The
App Server boundary remains the preferred integration seam; internal Rust
crates are not product dependencies merely because they are present upstream.

## Update and divergence policy

1. Keep the accepted source/binary/protocol pins and generated-contract
   provenance coordinated in a checked product manifest.
2. Qualify stock App Server and both selected provider targets before local
   changes where the stock path is sufficient.
3. Keep NeoBabylon-specific changes product-side when configuration or a
   supported client adaptation is enough.
4. If LM Studio or OpenRouter semantics cannot be satisfied by configuration
   or the typed client boundary, propose the least invasive focused runtime
   change at the phase where the incompatibility is observed. Record the
   requirement, upstream gap, affected files/symbols, compatibility tests,
   merge risk, and removal condition.
5. Preserve the pristine pinned baseline for comparison and rollback. A
   modified build must be distinguishable from the pristine executable by
   manifest identity and evidence.
6. For an upstream update, regenerate stable and experimental contracts,
   classify changes, rerun protocol/provider/authority/recovery tests, inspect
   changed permission and persistence code, and update decision/evidence
   records before changing the runtime pin.

## Weekly read-only upstream review

Plan a read-only maintenance check for useful experimental API changes,
promotions to stable, removals, schema changes, provider/model metadata, tool
and extension changes, and relevant fixes. The report should record the
revision/evidence, relevance, whether it removes a local patch, and whether to
investigate, adopt, defer, or ignore.

This is discovery, not automatic deployment. It must not update the runtime
pin, rewrite source, merge/rebase, install dependencies, enable experimental
features, or launch paid model reviews. The schedule, notification surface,
and missed-run policy remain open implementation details and are not a
Phase 1A prerequisite.
