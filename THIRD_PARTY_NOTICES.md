# Third-party notices

This file records third-party material currently referenced or used by
NeoBabylon. It is not a distribution notice for a product release;
distribution and the first-party NeoBabylon license remain open decisions.

## OpenAI Codex

The Phase 0 source snapshot and matching Windows binaries are from the
OpenAI Codex repository and release `rust-v0.155.1`. The upstream source
declares Apache-2.0 licensing and includes its own `LICENSE`, `NOTICE`, and
third-party notices. Those files remain in the source snapshot under
`reference/cache/codex/rust-v0.155.1/source/` and must be preserved if the
runtime is later incorporated.

Source: <https://github.com/openai/codex>

## Astryx

The diagnostic UI now uses the exact-pinned Astryx packages recorded in
`ui/diagnostic/package-lock.json`: `@astryxdesign/core@0.6.2`,
`@astryxdesign/theme-neutral@0.6.2`, and `@stylexjs/stylex@0.19.0`; the
development-only component/docs tool is `@astryxdesign/cli@0.6.2`. Their
package metadata declares MIT licensing; core requires React/React DOM 19+
and StyleX. The npm lockfile is the authoritative complete dependency graph.
The captured upstream license is retained under
`reference/cache/supplemental/astryx-LICENSE`; any distribution must inventory
the full locked graph and include applicable notices.

Source: <https://github.com/facebook/astryx>

## Diagnostic UI dependencies and fonts

The local diagnostic UI currently uses these direct packages, locked in
`ui/diagnostic/package-lock.json`:

- React `19.3.0` and React DOM `19.3.0` — MIT.
- Vite `8.3.0` — MIT; build-time bundler.
- TypeScript `7.0.2` — Apache-2.0; build-time compiler.
- `@types/react` and `@types/react-dom` `19.3.0` — MIT; build-time type
  declarations.
- `@fontsource-variable/dm-sans` and `@fontsource-variable/manrope` `5.3.0` —
  SIL Open Font License 1.1. Their local WOFF2 assets are bundled with the
  diagnostic UI build; the package license texts remain in the local package
  installs and must be included in any distribution bundle.

This is the current development stack, not an approved release dependency
manifest. The lockfile contains additional transitive build dependencies; a
distribution review must inventory the full locked graph and ship all
applicable notices and font licenses. No system-wide dependency was installed.

## Microsoft WebView2 SDK

The WPF host directly references `Microsoft.Web.WebView2@1.0.4191.47` in
`host/NeoBabylon.Host/NeoBabylon.Host.csproj`. The package's own `LICENSE.txt`
contains BSD-3-Clause-style terms; its `NOTICE.txt` also names
`Antlr3.Runtime 3.5.2-rc1` and `StringTemplate4 4.0.9-rc1`. These package files
are present in the local NuGet package cache, but this development inventory
is not yet a release notice bundle. Preserve the exact package license and
notice with any later distributable, and include other dependencies' required
notices after auditing the full resolved package graph.

The SDK package is distinct from the WebView2 Runtime used to render the UI.
The host currently relies on an installed runtime; NeoBabylon has not selected
or qualified an Evergreen/Fixed Version deployment mode, and this SDK entry
does not establish the Runtime's redistribution terms. See Microsoft's
[WebView2 SDK release notes](https://learn.microsoft.com/en-us/microsoft-edge/webview2/release-notes/sdk/1-0-4191-47),
[package page](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4191.47),
[package license](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4191.47/License),
and [Runtime distribution guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution).

## Other upstream dependencies

The Codex source archive contains its own dependency manifests and third-party
materials. Preserve those upstream notices and the source archive's license
materials. Distribution and the first-party NeoBabylon license remain open
decisions.
