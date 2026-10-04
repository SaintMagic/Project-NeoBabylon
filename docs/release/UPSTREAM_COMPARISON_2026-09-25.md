# NeoBabylon upstream comparison — 2026-09-25

Report schema: `neo-babylon-upstream-comparison/v1`  
Status: **read-only local snapshot; not an upstream refresh, diff review, or
runtime qualification.**

## Expected baseline

- Reference corpus: `reference/sources.json` record
  `CODEX-SOURCE-RUST-0.155.1`.
- Release/tag: `rust-v0.155.1`.
- Pinned upstream revision: `be2951ea34f0d295ed0becf97079f92fa5f6950e`.
- Source archive SHA-256:
  `2dc56c1db2cc3fb44fc8f132e1cb7064ee3188ee8f811580264bbc672af0d442`.
- Runtime binary identity in `runtime/runtime-lock.json`: App Server
  `0.155.1`, SHA-256
  `636f221667c00499e4ea4bffd9a18bf0f02412e71594700c0d55d72e2709f318`.
- Tracked runtime patch SHA-256 in the lock:
  `a0a7cd319a31f2125195e7087be72a2867be4779edf00c686ec85f42c58cf520`.

## Observed local comparison

The sibling `D:\CODING\NeoBabylon-Runtime` Git HEAD is exactly the pinned
revision above. Its tracked working-tree diff contains 46 files: `Cargo.lock`
(1), `app-server` (2), `codex-api` (4), `config` (4), `core` (18), `ext` (1),
`login` (1), `model-provider` (1), `model-provider-info` (2), `mxc-sandbox`
(1), `protocol` (1), `tui` (6), and `windows-sandbox-rs` (4). The SHA-256 of
`git diff --binary HEAD` is exactly the `sourcePatchSha256` recorded in the
NeoBabylon runtime lock. This confirms patch-byte identity with the lock; it is
not a review of the patch's behavior or necessity.

Git also reports one untracked runtime-root file:
`bin/codex-app-server-x86_64-pc-windows-msvc.exe` (236,072,752 bytes). Its
SHA-256 is
`253c6d8424ea45ba9d36d1f665d9b7b4c917782d4cf5d56e61cd0664cc52bf0d`, exactly
matching the captured upstream release binary under
`reference/cache/codex/rust-v0.155.1/binary-x86_64-pc-windows-msvc/`. It is a
duplicate of that captured binary and is not included in the tracked patch
fingerprint. It was left untouched.

## Review actions

1. Review the 46-file tracked diff against the accepted Phase 1B scope and
   retain the existing lock fingerprint unless new evidence requires a patch
   change.
2. Preserve the untracked binary until its owner confirms whether it should
   remain in the runtime checkout; do not delete it automatically.
3. Do not treat this local comparison as a fetch of newer upstream, test-suite
   acceptance, or a claim that the runtime patch is minimal.

No remote refs were fetched. No source, lock, binary, shared configuration,
or runtime state was modified. No recurring schedule was created. A future
deterministic report generator and any report delivery schedule remain
separate from this snapshot.
