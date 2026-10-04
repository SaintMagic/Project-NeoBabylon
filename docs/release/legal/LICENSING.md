# Licensing and distribution notes

Status: **Evidence captured; public source publication authorized 2026-10-04.** This is not
legal advice and is not a final distribution notice.

## Codex

The pinned Codex source workspace declares Apache-2.0 licensing and includes
upstream `LICENSE`, `NOTICE`, and third-party materials. If NeoBabylon later
incorporates or distributes the runtime or derived source, those notices and
the applicable license text must remain with the accepted distribution.

The Phase 0 source remains an ignored local reference cache and is not being
packaged or published.

## Astryx

The diagnostic UI uses exact-pinned `@astryxdesign/core@0.6.2`,
`@astryxdesign/theme-neutral@0.6.2`, and `@stylexjs/stylex@0.19.0`; the
development-only CLI is `@astryxdesign/cli@0.6.2`. The packages report MIT
licenses, and core's peer requirements are React/React-DOM `>=19.0.0` plus
StyleX `^0.19.0`. The UI package's `package-lock.json` is the exact dependency
graph; the captured Astryx license remains in the reference cache. Astryx is
currently upstream-labeled beta, so its compatibility must be rechecked at
future dependency updates. This is not a final distribution inventory.

## NeoBabylon current direction

The initial direction was private local software. On 2026-10-04 Martin
authorized a public source repository, reviewed initial source commit and push;
see [NB-DEC-014](../../decisions/0014-public-source-publication.md). This is not
an installable release, marketplace, licensing portal or packaging decision.
It does not select a first-party NeoBabylon license. The Phase 0 reference
cache and private app state remain unpublished. Included third-party/derived
source must retain its applicable licenses and notices.

## Future distribution details still open

- first-party NeoBabylon license;
- whether and how the runtime is distributed with a Windows application;
- binary/source update and signing policy;
- notices for the selected UI, shell, provider, and future tool dependencies;
- whether private local use is later expanded to another supported
  distribution mode.

Use the exact accepted manifests and runtime ownership decision for any future
license inventory. Do not infer a final distribution policy from this Phase 0
record.
