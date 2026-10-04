# NeoBabylon product brief

Status: **Accepted product direction; implementation details remain
proposed.** Source: Martin's Phase 0 startup brief, preserved at
`docs/history/NEOBABYLON_STARTUP.md`.

## Product intent

NeoBabylon is a serious desktop coding-agent environment. It should make a
capable agent runtime feel like a dependable development workspace: inspect a
project, understand its instructions, plan work, request authority when
needed, execute tools, show changes, and recover a session without hiding
important state.

The initial target is Windows desktop. NeoBabylon is private project work;
publication, hosted service operation, and remote-repository creation are not
part of this startup phase.

## Accepted requirements

- The product name is **NeoBabylon** and the slug is `neobabylon`.
- The starting engine is the open-source Codex runtime/App Server.
- The client/server boundary is structured and typed; terminal scraping is not
  the integration model.
- The client is React/TypeScript with Astryx as the accepted UI direction.
- The product must support free hosted or local inference and must not require
  a paid GPT/OpenAI path.
- The toolbox should be broad, reliable, and discoverable. NeoBabylon may
  self-scaffold genuine gaps, but Martin explicitly promotes candidates before
  they become trusted capabilities.
- The documentation should use conventional categorized folders and preserve
  the separation between accepted requirements, proposals, evidence, and WIP.
- CanonWell is a read-only structural reference. CanonWell, PiLOT, Pi, and
  launchers are not to be modified as part of NeoBabylon.
- The initial platform target is Windows. Android, hosted deployment, and
  multi-platform expansion require a later decision.
- The name “Bricks” is not an approved schema. Use tools, generated tools,
  candidates, and plugins unless a future decision introduces another term.

## Settled Phase 0 decisions

The following choices are now selected product direction. Their exact
implementation versions, environment tuples, and qualification evidence remain
proposed or open where the decision records say so:

- The desktop host is WPF/.NET with WebView2 around the React/TypeScript/Astryx
  interface.
- Product source and Codex-derived runtime source are separate local sibling
  Git repositories; no GitHub repository is required.
- LM Studio and OpenRouter are the initial provider qualification targets.
- Durable state is under `<NeoBabylon root>\Data`, with protected compatible
  migrations rather than casual reset/delete behavior.
- NeoBabylon is private local software for the current scope; upstream and
  third-party licenses/notices remain applicable.
- Stable protocol behavior is the default. Experimental capabilities require
  deliberate opt-in and qualification; a weekly upstream check is read-only
  discovery, not automatic update.
- A genuine capability gap may produce a standard-template candidate under
  `Data\GeneratedTools`. An unapproved candidate may be tested and used under
  existing task permissions, retained in an Unapproved Tools surface, and
  manually reviewed/promoted later.

## Intended user workflow

The first useful product should let Martin:

1. Select a workspace and see its trusted instructions and repository state.
2. Select a provider and model without hidden fallback to a paid or unrelated
   endpoint.
3. Start, observe, pause/cancel, resume, and reopen a Codex thread.
4. See streamed messages, tool progress, approvals, diffs, failures, and
   persistent thread identity as distinct states.
5. Approve or deny authority requests with enough context to make a decision.
6. Use a categorized toolbox whose permissions and failure behavior are
   inspectable.
7. Use a temporary self-scaffolded candidate when the existing authorized
   toolbox is insufficient, retain it for later inspection, and review it
   before promotion into a trusted catalog.

These are product outcomes. Exact .NET/WebView2 package versions, provider
model/server/route tuples, bridge operations, migration mechanism, and
packaging details remain proposals or technical evidence gaps.

## Non-goals for the startup phase

- No production UI, host, runtime fork, provider adapter, or tool catalog.
- No system-wide dependency installation or shared Codex configuration.
- No automatic model/provider fallback that could change cost, authority, or
  data handling.
- No direct copying of CanonWell implementation or domain-specific rules.
- No Android, hosted, multi-platform, or public distribution surface.
- No claim that a local model is compatible until its Responses API behavior,
  streaming, tools, context handling, and recovery are tested.

## Product acceptance bar after Phase 0

Phase 0 is ready for review when the reference corpus is reproducible, the
protocol boundary is evidenced from the pinned binary/source, the selected
direction is recorded in decision documents, the capability matrix
distinguishes observed from proposed behavior, and the build plan names gates
for provider capability accuracy, permissions, persistence, and temporary
self-scaffolding. The current documentation reconciliation preserves that
evidence and divides the next work into Phase 1A and 1B.
