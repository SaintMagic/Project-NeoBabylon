# Codex parity implementation package

Reviewed source: `f126612a00231a58626d72108aac6ee912431ee3`, 2026-10-04.

Start with the [review report](../CODEX_PRODUCT_PARITY_REVIEW.md) and [implementation order](../CODEX_PARITY_IMPLEMENTATION_ORDER.md). The [patch catalog](../../../patches/codex-parity/README.md) describes eight unapplied atomic patches. [SPECIFICATIONS.md](SPECIFICATIONS.md) indexes the nine larger, implementation-ready specifications. [VERIFICATION.md](VERIFICATION.md) distinguishes actual checks from native/live gates. [CODEX_SOURCES.md](CODEX_SOURCES.md) records the current official comparison sources and their limits.

`verify-patches.py` validates declared prerequisites, cumulative application, patch hashes and exact reverse restoration in disposable worktrees. It does not run a model or qualify a runtime/provider. Product source on the review branch remains unchanged.
