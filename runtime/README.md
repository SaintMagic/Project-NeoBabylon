# Runtime boundary

Codex App Server owns execution, thread/history lifecycle, approvals and provider
behavior. React receives named validated desktop operations, not arbitrary RPC.

## Current identity — 2026-10-04

`runtime-lock.json` is authoritative, not historical build/comparison notes:

- Upstream `https://github.com/openai/codex.git`, tag `rust-v0.155.1`, revision
  `be2951ea34f0d295ed0becf97079f92fa5f6950e`.
- Separate checkout `.local/Runtime/NeoBabylon-Runtime`, preserving ancestry.
- Locked binary `.local/Runtime/LockedBuild/20261003-a0c3ebdc/codex-app-server.exe`.
- App Server `0.155.1`, target `x86_64-pc-windows-msvc`.
- Binary SHA-256
  `a0c3ebdc8d1d9f5b56327f5ea6d1502ee5a0b50fa174d50743f8c7a17ff54386`.
- Tracked-source patch SHA-256
  `cce8de430fd6bd1d8f4842ff3768bef9d185fce02ad83d62812e653595584dde`.

The supervisor checks the locked executable's hash and requires the checkout.
It does not substitute stock Codex or the ordinary user's installation. Public
product source does not authorize committing/pushing the nested runtime repo.

The private source build receipt is
`.local/Lab/Runs/Finishing-20261003/runtime/build-receipt.json`: Cargo/rustc 1.97.0,
serial locked offline dev build, upstream revision, tracked-patch/Cargo.lock
hashes and matching output hash, using project Windows stack/static-CRT flags.
This is local source-to-binary evidence, not a portable byte-identical rebuild.

The accumulated changes are not route-only: they include routing, function-form
patch tools, context/compaction metadata, command-stop identity, Windows guards
and related contracts/dependencies/tests. Historical locks/comparisons/tests
remain in [VERIFICATION.md](../docs/release/VERIFICATION.md); they are not current
binary identities. The Phase 0 cache remains inert inspection material.

## Public source and development prerequisites

Read [SOURCE_PUBLICATION.md](../docs/release/SOURCE_PUBLICATION.md) for public
source scope and reconstruction gaps. The ignored runtime executable/builds,
reference cache, app Data and credentials are not included. A source clone is
not an assembled app; no stock-binary fallback or automatic lock rewrite is
permitted merely to make the launcher run on another machine.
The full custom downstream patch payload is not currently public. A digest
alone cannot reconstruct it; stock upstream source does not build this custom
runtime. The recorded build used Rust 1.97.0, whereas the retained upstream
toolchain file pins 1.95.0. Fresh reconstruction/toolchain selection and any
public patch/artifact publication require their own qualified source chain.

`generated-tool-node-lock.json` separately pins an app-private Node executable.
That binary is excluded too. The launcher checks an available local Node's
version/hash before copying it. Callable generated-tool activation remains
closed until route/selected-tuple qualification succeeds.

State belongs under the selected application root's `Data`, never the ordinary
Codex home. Tools use the accepted full-access/no-containment policy: Windows-user
authority, not a qualified workspace sandbox. Source publication does not imply
new runtime changes, whole-product acceptance, packaging or installation.
