# NeoBabylon

NeoBabylon is a Windows desktop coding-agent environment with a thin WPF/.NET
host using WebView2, a React/TypeScript client, a named desktop bridge, and a
supervised pinned Codex App Server. Its initial provider targets are LM Studio
and OpenRouter. Both have recorded historical narrow live tool round trips;
the OpenRouter route-pinned path was also exercised through a visible WPF
host. Those results do not qualify the refreshed build. The Codex-first desktop shell includes capability selection, streamed
conversation state, saved-thread listing/resume, and isolated runtime
diagnostics. Phase 1B qualification and the complete desktop-client gate are
still open; see [current status](docs/product/STATUS.md).

## Current status

As of 2026-10-04, the implementation has an assembled development desktop,
saved-chat/model switching, protected-record recovery, context/usage diagnostics,
manual compaction controls and generated-tool review/activation groundwork.
Recent bounded UI/host checks are in the
[context/reasoning checkpoint](docs/history/2026-10-04-context-metrics-reasoning.md)
and [popup correction](docs/history/2026-10-04-popup-fix-paused-checkpoint.md).
Broader behavioral/live qualification remains deferred. This is experimental
source, not a passed product acceptance gate or installable release. Callable
generated-tool activation remains disabled pending route/selected-tuple
qualification. Feature work is paused; clearer throughput, visible/restored
provider reasoning and useful tool-card details remain follow-ups.

Phase 0 foundation is complete. The workspace contains the documentation
structure, provenance-tracked Codex reference corpus, generated protocol
artifacts, upstream source inspection notes, and the reconciled build plan.
Phase 1A added isolated runtime supervision and qualification evidence.
Phase 1B adds capability mapping, authority/failure diagnostics, and targeted
route control. The first UI slice is an early authorized implementation, not a
claim that the remaining Phase 1B or Phase 2 acceptance gates have passed.
Martin subsequently deferred Windows containment and authorized Phase 2 with
explicit unrestricted tools. The UI labels that authority “Full access · no
containment”; see [NB-DEC-008](docs/decisions/0008-phase2-unrestricted-tool-authority.md).

The working reference baseline is Codex stable release `rust-v0.155.1`,
commit `be2951ea34f0d295ed0becf97079f92fa5f6950e`, inspected on Windows x64.
See [Reference](reference/README.md), [Status](docs/product/STATUS.md), and
the [initial build plan](docs/WIP/INITIAL_BUILD_PLAN.md).

The accepted local source boundary is two separate Git repositories: this
product repository and the nested, ignored
`.local/Runtime/NeoBabylon-Runtime` Codex-derived runtime repository. The runtime
preserves the pinned upstream ancestry and is
bound to the product by `runtime/runtime-lock.json`. Martin subsequently
authorized a public source repository, initial commit and push. This does not
publish private runtime binaries or application data; see the
[source-publication boundary](docs/release/SOURCE_PUBLICATION.md).

Phase 1A/1B evidence and current blockers are in
[PHASE1A_LMSTUDIO.md](docs/release/PHASE1A_LMSTUDIO.md) and
[PHASE1B_OPENROUTER.md](docs/release/PHASE1B_OPENROUTER.md). Broad
approval/recovery/compaction qualification, packaging and callable
self-scaffolding remain outside verified completion.

## Development preview

From this workspace in PowerShell, use `./tools/Launch-Development.ps1` to
launch the assembled development host. Add `-PromptForApiKey` if a process-only
OpenRouter credential is not already available, or `-BuildOnly` to compile and
assemble without launching or requesting a credential. The explicit default
tuple is OpenRouter / `stealth/space-bunny-alpha`; it is not live-qualified by
this launcher. No provider/model fallback is provided.

A fresh source clone does not contain the app-private runtime binary, Node
runtime, dependency caches, protected credentials or assembled host. The launcher
is not an installer and does not download missing prerequisites. Read the
[runtime boundary](runtime/README.md) and
[source-publication limitations](docs/release/SOURCE_PUBLICATION.md) first;
do not substitute an upstream binary for the locked custom runtime.

The development application root is `.local/App`, distinct from the source
root; durable state stays in `.local/App/Data`, not the ordinary Codex home.
Tools run with full Windows-user authority, not workspace containment. Packaging
and installation remain deferred.

## Read first

- [Project instructions](AGENTS.md)
- [Product brief](docs/product/PRODUCT_BRIEF.md)
- [Architecture](docs/architecture/ARCHITECTURE.md)
- [Codex-first UI/UX reference](docs/architecture/UI_UX_REFERENCE.md)
- [Feature matrix](docs/product/FEATURE_MATRIX.md)
- [Reference index](reference/INDEX.md)
- [Open questions](docs/WIP/OPEN_QUESTIONS.md)
- [Initial build plan](docs/WIP/INITIAL_BUILD_PLAN.md)

## Phase 0 boundaries

The Phase 0 workspace and reference corpus were established without changing
CanonWell, global Codex settings, or unrelated projects. The downloaded
reference cache is inert and ignored by Git policy. Subsequent work has been
limited to this product workspace and its separately authorized local runtime
checkout. No remote repository, commit, or push was created during Phase 0;
later source-publication authorization is recorded separately.

Further work follows the reviewed plan with Martin's explicit Phase 2
sequencing revision. The old Windows sandbox paths remain unqualified; a
deterministic pinned App Server → ordinary command tool round trip now passes
under unrestricted authority without live provider inference. Live provider
repeatability, request budgeting/compaction, broader recovery and authority
evidence, and the complete Phase 2 client remain open.
