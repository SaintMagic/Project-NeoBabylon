# NB-DEC-001 — Codex Phase 0 baseline

Status: **Accepted for Phase 0 evidence; proposed as the Phase 1 starting
baseline.** Date: 2026-09-22.

## Decision

Use Codex stable release `rust-v0.155.1`, resolved commit
`be2951ea34f0d295ed0becf97079f92fa5f6950e`, with the matching Windows x64
App Server and CLI assets as the reproducible reference baseline.

## Rationale

It is a stable release, has matching public source and Windows assets, and can
generate the exact App Server protocol contracts locally. Using a stable tag
reduces drift compared with unpinned `main` or a prerelease while preserving a
clear update point.

## Evidence

See [reference sources](../../reference/sources.json), [reference index](../../reference/INDEX.md),
and [verification](../release/VERIFICATION.md). The tag's GitHub API response
reported an unverified annotated tag; archive, asset, commit, version, and
hash records are therefore retained explicitly.

## Consequences

The baseline is not a claim that the runtime is provider-compatible, built
from source, or ready for distribution. A later update needs a new release
record, regenerated contracts, and the full qualification suite.

