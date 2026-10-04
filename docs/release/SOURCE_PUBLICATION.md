# Experimental source-publication boundary — 2026-10-04

Martin authorized public `SaintMagic/Project-NeoBabylon`, a reviewed initial
source commit and push. This is not product-release certification; see
[NB-DEC-014](../decisions/0014-public-source-publication.md).

## Intended public material

Product host/UI source, source tests/development tools, protocol contracts,
product-owned icons, dependency manifests/locks, reference provenance indexes,
accepted decisions and evidence-labelled project documentation. Historical
records are not current completion claims. Local evidence paths refer to
retained private artifacts, not files promised to exist in a fresh clone.

## Excluded private/local material

The entire `.local` tree, nested runtime Git repository, Data/CodexHome,
WebView2 profile, provider credentials, session journals/conversations, private
QA/snapshot/build receipts, reference cache, compiled binaries, dependency
installs and build output. Global instructions and unrelated projects/config
are not included. `AGENTS.md` is the project-only file, not Martin's global file.
App-private Windows current-user DPAPI credential records are excluded too.
The original `NEOBABYLON_STARTUP.md` and its history copy retain personal/global
instruction excerpts. They remain byte-preserved locally but are excluded from
this initial publication; project requirements/decisions remain in product docs.

## Fresh-clone limitations

This is not a turnkey installer or clean-machine qualification. The launcher
expects UI packages, .NET/WPF/WebView2 prerequisites, the exact app-private Node
executable and locked custom App Server/source checkout. It does not install
missing prerequisites or substitute a runtime/provider/model. See
[`runtime/runtime-lock.json`](../../runtime/runtime-lock.json) and
[`runtime/README.md`](../../runtime/README.md) for exact identity, local build
evidence and runtime-source reconstruction limitations.

A rebuild's hash must be checked independently; do not silently relax/rewrite
the lock because another build differs. No byte-identical clean rebuild is
claimed. Private runtime binaries and application data are not distributed.
The custom runtime's full downstream patch payload is not published in this
product repository. Its digest identifies the retained local changes but cannot
reconstruct them. Stock upstream source alone is insufficient to build this
locked runtime. Public runtime reconstruction is an explicit remaining gap,
not an implied clean-clone feature or an automatically selected fallback.

## Verification and open work

Bounded context/reasoning and popup checks have dated machine-produced evidence.
They do not qualify the full product, live reasoning controls/compaction,
generation throughput, all recovery/approval paths or current provider routes.
The native window contains a saved chat, not an empty disposable QA fixture.

Feature work is paused. Throughput clarity, visible/restored provider reasoning
and useful tool-card details remain unimplemented follow-ups. Callable generated
tools, deferred qualifications, performance acceptance thresholds, installation
and packaging remain outside verified completion.
Review also found an inspection-only limitation with a nondefault
`Launch-Development.ps1 -BuildOutputRoot`: NVIDIA bundle resolution still uses
the default app build root. The default layout is aligned; custom-root NVIDIA
launch attribution is not qualified. No implementation fix was made while paused.

No first-party NeoBabylon license has been selected. Third-party/derived source
retains applicable licensing; see [`THIRD_PARTY_NOTICES.md`](../../THIRD_PARTY_NOTICES.md)
and [licensing notes](legal/LICENSING.md). Do not label this a licensed,
installable or fully qualified product release.
