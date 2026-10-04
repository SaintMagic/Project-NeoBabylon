# NB-DEC-002 — Runtime source ownership

Status: **Accepted product boundary; implementation details remain open.**
Date: 2026-09-22.

## Decision

NeoBabylon product source and the Codex-derived runtime source are separate
local sibling Git repositories. NeoBabylon does not require GitHub hosting,
public repository creation, or a hosted fork.

The runtime is pinned explicitly by upstream baseline, source revision,
expected executable/version, artifact hashes, and generated-contract
provenance. The product repository owns the WPF host, React/Astryx UI,
integration boundary, maintained product tools, tests, and documentation. The
runtime repository preserves Codex's upstream layout, history, notices, and
focused runtime-facing changes.

This records the intended local arrangement; it does not create, initialize,
rearrange, or publish either repository as part of documentation
reconciliation.

## Boundaries

- The reference cache remains read-only evidence and is not the modified
  runtime repository.
- The product must not vendor the full runtime into its source tree merely to
  make the dependency convenient.
- A submodule, clean vendor import, or other cross-repository mechanism is not
  selected by this record. Introducing one later requires a concrete
  compatibility or maintenance reason and a separate review.
- Product and runtime revisions may need coordinated changes, but a checked
  runtime manifest must make the compatibility relationship explicit.
- Git commits, merges, tags, pushes, and remote creation retain Martin's
  existing authorization gates.

## Implementation details still open

- The exact sibling folder/name convention.
- The product-side manifest/lock format and how it records source, binary, and
  generated-contract hashes.
- The launch/build ownership mechanism once the WPF fixture exists.
- The source-build and focused-patch procedure required before a modified
  runtime is carried or distributed.

## Alternatives considered

- A Codex-derived product repository preserves ancestry but couples product and
  upstream concerns.
- A binary-only dependency is small but weakens source traceability and
  deliberate divergence support.
- A submodule or vendor import may be useful later, but neither is required by
  the selected local sibling-repository boundary now.
