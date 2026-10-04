# Project scripts

Scripts in this directory are maintenance and verification tools, not agent
runtime features. The Phase 0 scripts acquire the pinned public reference
corpus and verify its recorded hashes. They do not alter shared Codex
configuration or install dependencies.

`compare-upstream.ps1` prints a deterministic Markdown comparison of the
selected upstream source record, runtime lock, and local sibling runtime Git
worktree. It uses local Git and SHA-256 file hashes only; it does not fetch,
run runtime binaries, or modify its inputs. Output goes to stdout and has no
date unless `-ReportDate yyyy-MM-dd` is supplied. `-Root` selects a product
checkout; `-RuntimeRoot` is an optional override intended for isolated fixtures
and alternate local checkouts. Run the deterministic fixture test with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/qualification/UpstreamComparison.Tests.ps1
```
