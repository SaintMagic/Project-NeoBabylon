# Phase 3–5 vertical-slice execution map

Status: **proposed decomposition of the accepted Phase 3–5 requirements; no
Phase 3, 4, or 5 acceptance is claimed.** The initial build plan remains the
authority; this map only divides its requirements into testable outcomes.

## Settled boundaries

- Private/local scope remains. Android, hosted, cross-platform, and public
  distribution are excluded unless Martin adds them.
- Stable protocol behavior is the default. Experimental capabilities remain
  separate and off unless deliberately opted into and qualified.
- Codex App Server retains ordinary tool execution, approval, thread, and
  runtime-persistence authority. React uses named validated host operations,
  not generic arbitrary RPC.
- thread/shellCommand and other full-host escape surfaces remain human-only.
- Generated candidates use existing authorized tools and current task
  permissions, cannot expand those permissions or promote themselves, and live
  under <application root>\Data\GeneratedTools until manual review.
- Phase 5 runtime divergence requires a concrete reproducible
  LM Studio/OpenRouter or protocol mismatch; no patch is presumed.
- Updates must preserve durable Data and must not modify a shared installation.

## Open implementation proposals

The exact toolbox record/exposure schema, weekly-report scheduler and delivery,
candidate artifact kinds and manifest/hash/review records, migration/backup/
downgrade mechanics, local package/update channel, signing strategy, and soak
thresholds remain proposals. Prefer read-only, opt-in, local behavior; review
these choices before building new subsystems around them.

## Verified Phase 5 blockers

- `MainWindow.xaml.cs` defaults the production application root to
  `%LOCALAPPDATA%\NeoBabylon`, while accepted decision NB-DEC-005 says durable
  state belongs beneath the application/install root, not a hidden LocalAppData
  store. QA runs inject isolated roots and therefore do not resolve this
  production-path contradiction. Do not migrate or change the default until
  root-selection/install semantics are settled.
- The current host finds its runtime lock and UI in the source tree; the locked
  App Server binary is external under sibling `NeoBabylon-Data`. There is no
  self-contained package/update layout yet.
- The current `runtime/README.md` describes the dirty sibling checkout and its
  mixed patch accurately. The locked binary's depfile points to the separate
  `NBRT-RouteControl` worktree. The canonical sibling and that worktree share
  upstream HEAD and the same tracked-patch fingerprint; their 28 raw text-file
  differences disappear after newline normalization. A corrected isolated
  remapped build, using the checkout's Windows stack/static-CRT flags, matches
  the locked image's `.text`, `.data`, `.pdata`, `.fptable`, and `.reloc`
  sections byte-for-byte. Its only `.rdata` differences are generated `OUT_DIR`
  hash strings and PE debug metadata; normalizing those plus the PE COFF
  timestamp makes the full image byte-identical. Raw executable SHA-256 still
  differs, and this does not attest to every historical build input. The
  current evidence supports source-to-binary content parity and gives no basis
  for runtime divergence; keep the lock and both checkouts unchanged. The newer
  sealed Codex Security diff scan completed 2026-09-25 for the exact 47-path
  tracked patch (scan `7fa01596-17fa-41e7-95d5-c382f9475d76`; snapshot
  `codex-security-snapshot/v1:sha256:2c93216bbdb28fbf5c3ab09292ebb59674dea1b9eeb66139eff86b2e1d70d9a7`).
  All 47 tracked changed paths were reconciled: 33 workbench review items and
  14 additional test/fixture/lock paths. The sealed report has zero findings
  across four reviewed surfaces. This completes coverage of the tracked source
  diff only; it does not provide a second independent security review or
  runtime/containment qualification. No runtime binary was built or exercised,
  and no provider request was made; the untracked captured executable under
  `NeoBabylon-Runtime/bin` was excluded. One independent architecture
  fact-sheet pass was completed, but no second independent security reviewer
  was available. The scan could not establish the effective launch-time value
  of `NEOBABYLON_REQUIRE_CONTAINED_TOOLS`. Separate P3-02 evidence proves one
  marker-enabled `dangerFullAccess/never` command path, and a focused negative
  test verifies both guarded legacy entrypoints reject before command
  execution. The positive function-form patch integration path and three unit
  tests pass, but denial/approval/hook/error parity remains unqualified.
  P5-01 remains open for those gaps and runtime provenance; the scan does not
  justify changing the runtime lock.
- `THIRD_PARTY_NOTICES.md` now inventories the host's exact direct
  `Microsoft.Web.WebView2@1.0.4191.47` SDK dependency and the notices present
  in its local package. This resolves the missing direct-dependency entry,
  not release readiness: the resolved dependency closure, bundled WebView2
  Runtime/deployment mode and its terms, and complete distributable notice
  bundle remain unqualified.

## Phase 3 — toolbox inventory and qualification

### P3-01 — Versioned capability inventory
Inventory built-in tools, browser/computer, screenshots/vision, large
documents, search, patch, shell, compaction, skills, hooks, MCP, and plugins.
Record pinned source/version, stable or experimental status, actual
NeoBabylon exposure, applicable provider/model evidence, and limitations.
Source existence is not provider support or a product promise.

**Acceptance:** every category has a source/version and an honest
observed/experimental/unavailable status; Unknown stays explicit and nothing
is silently enabled or assigned provider defaults.

**Evidence status (2026-09-25):** the inventory is complete against this
acceptance for the pinned `rust-v0.155.1` snapshot and the inspected host/UI
surfaces. It explicitly records Unknowns and provider/runtime qualification
gaps; it does not claim capability support. Image-handler/attachment paths and
the exact experimental boundary for dynamic-tool registration versus its
callback are cross-checked. See
[`UPSTREAM_CAPABILITY_INVENTORY.md`](../release/UPSTREAM_CAPABILITY_INVENTORY.md).

### P3-02 — Per-category execution/failure evidence
For every actually exposed capability, record permission source and requested/
effective authority, identity, timeout, cancellation, denial, partial output,
and source/runtime version. Unexposed categories remain visibly unavailable
or planned, not callable stubs.

**Acceptance:** deterministic fixtures cover success, denial,
timeout/cancellation, partial failure, and attribution for each exposed
category. Host-managed credentials are not directly serialized to the renderer
or injected into tool processes. Under accepted unrestricted authority,
normal tool output is displayed and forwarded without content redaction, even
when a command returns sensitive user-accessible data; see
[NB-DEC-011](../decisions/0011-unrestricted-tool-output-visibility.md).

**Evidence status (2026-09-26):** the exposed-surface audit is recorded in
[`P3-02_EXPOSED_SURFACE_EVIDENCE.md`](../release/P3-02_EXPOSED_SURFACE_EVIDENCE.md).
A bounded UI correction is implemented: host error type/attribution survive the
bridge, and missing/future tool statuses render as informational rather than
success. The separate identity-bound command Stop path now also has a
host-level typed-`failed` retry fixture: retry is enabled only when the exact
thread/item/process binding and App Server epoch remain current; the following
retry is sent through the real pinned App Server. UI tests fail closed if a
typed failure lacks explicit retry availability. Deterministic unit tests, the
nine-flow browser fixture, and the native WPF/WebView2 failure-rendering
fixture pass; the latest full UI suite is 94/94 and the production
build/typecheck succeeds with existing dependency/bundle advisories. The native fixture used the exact
locked App Server and a
synthetic selected model plus loopback Responses server. It proves one
ordinary command failure retains marker and exit code 23 across the App Server
event and final host diagnostic, then displays the failure, exact exit code,
partial output, and attribution in the UI. Exactly two fixture requests
occurred with no fallback. A synthetic credential-shaped canary in that
output was also visible in the native UI and forwarded to the loopback model;
it was test-only, not a real key or user data. Evidence is under
`D:\CODING\NeoBabylon-Data\QA\P3-03-tool-capabilities-20260925\P3-02-native-command-failure-1790346625113-13224\`.
The typed-stop host result is under
`D:\CODING\NeoBabylon-Data\QA\P3-02-command-interruption-fca1366a31b64e0a857d0a9ec46ab2f5\result.json`.
**Native late-completion follow-on — passed 2026-09-26:** a fresh four-scenario
WPF/WebView2 run interrupted an ordinary command, left that command running,
then observed its natural exit. The host forwarded the exact late
`item/completed` event under the original turn request ID; the UI changed the
activity card to `succeeded` and removed Stop. The run also verified successful
Stop, stale/duplicate Stop rejection, and conservative `unknown` status after
the exact App Server process was stopped. It made four loopback Responses
requests with no fallback or provider inference, and had no fixture/page
issues. Exact runtime/hash and QA result path are recorded in
[`VERIFICATION.md`](../release/VERIFICATION.md). One earlier retry failed
before its second fixture request with App Server EOF; the cause is unknown
and it did not recur in the complete passing run. This closes only the late
natural-completion delivery path, not P3-02's broader failure matrix.
No live model was used. Martin accepted the normal-output behavior in
[NB-DEC-011](../decisions/0011-unrestricted-tool-output-visibility.md): normal
tool output is shown and forwarded without content redaction. This resolves
the requirement ambiguity, not the operation-specific denial,
timeout/cancellation, or broader partial-output matrix. P3-02 remains open.

### P3-03 — Categorized toolbox UI
Add discoverable categories and capability detail through the existing named
bridge and App Server authority. Each entry states available, experimental/off,
unsupported for the selected tuple, or blocked, with permission/source
provenance. Do not add generic RPC.

**Acceptance:** native WPF/WebView2 reflects effective runtime/model state
after restart; unknown and denied capabilities remain non-callable.

**Status — passed at bounded deterministic native scope 2026-09-25:** the
versioned read-only catalog presents all 13 categories and separates
NeoBabylon exposure from exact selected-tuple qualification, with permission
and evidence provenance. Exact operation evidence is read from the canonical
model capability records; generic tool-use metadata does not qualify an
operation. The typed host record carries the data through the existing named
diagnostics bridge; no bridge operation, generic RPC, or App Server/runtime
patch was added. UI tests prove exact LM Studio/NEX operation mapping, unknown
and explicit blocked behavior, and search. A native WPF/WebView2 fixture
reopened the same isolated application root and selected tuple after graceful
host restart, displayed all 13 categories, kept Unknown `apply_patch` and
Blocked `exec_command` non-callable, and made zero Responses requests. See the
dated P3-03 entry in [`VERIFICATION.md`](../release/VERIFICATION.md) and the
retained result/screenshots under
`D:\CODING\NeoBabylon-Data\QA\P3-03-tool-capabilities-20260925\P3-03-native-tool-catalog-1790344190968-24492\`.
This is not live-provider qualification or Phase 3 acceptance; P3-02 and other
phase gates remain open.

### P3-04 — Large-document truncation and omitted-range proof
Exercise the pinned runtime's actual document/search/output path. Force
truncation, expose exact omissions, and inspect omitted ranges where supported;
otherwise show explicit unavailability.

**Acceptance:** deterministic native tests prove bounds, identity, ordering,
and unavailable/error behavior. Advertised context length is not a substitute.

**Status — passed at deterministic native scope 2026-09-25:** the isolated
WPF/WebView2 harness read a generated 1,100,000-character document through the
locked App Server. The saved item exposed 895,200 retained characters in 28
ordered pages (maximum 32,768 each); the document start and end were retained,
but its middle was not. App Server returned no recognized omission marker, so
the inspector now explicitly says completeness cannot be confirmed and omitted
source text cannot be recovered or counted. The test also verified exact page
ranges, item attribution, bounded preview, and a clean native run. This is
deterministic/mock evidence, not live-model-size qualification. Evidence:
`D:\CODING\NeoBabylon-Data\QA\P3-04-native-document-read-20260925\P2-P211-native-1790310358597-23700`.

### P3-05 — Read-only upstream report
Generate a versioned comparison against the pinned source/reference snapshot.
Report changes and review actions only; never update locks, install
dependencies, edit shared configuration, or enable experimental features.

**Acceptance:** fixture proves deterministic, read-only output. Until report
scheduling/delivery is reviewed, do not create an OS task or external recurring
automation.

**Status — generator slice passed 2026-09-25; delivery deferred:**
`scripts/compare-upstream.ps1` emits stable Markdown to stdout, fingerprints the
tracked diff from raw Git bytes, hashes tracked changes and untracked files,
compares the actual patch fingerprint with the lock, and never updates inputs.
The fixture test ran twice against a temporary local Git checkout, matched an
exact golden, and verified unchanged manifest, lock, HEAD, and worktree. A live
read-only comparison confirmed the manifest/lock/runtime HEAD revision and the
46-file tracked patch fingerprint match. No remote refs were fetched. A weekly
schedule/delivery design remains unreviewed and was not created.

## Phase 4 — candidate creation and manual promotion

### P4-01 — Candidate envelope and standard template
Validate candidates under application Data\GeneratedTools, created only after
a genuine gap is recorded. Capture provenance, proposed behavior,
permissions/data flow, dependencies, evidence, and actual paths. Creation is
explicit; candidate contents are never host instructions.

**Acceptance:** tests reject missing authority data, outside-root paths, and
credentials; templates and candidates do not enter global Codex configuration.

### P4-02 — Durable unapproved drawer
Retain candidates across restart and display source, status, permissions,
dependencies, evidence, path, and current content identity. Validate/use them
only through existing authorized model/tool execution and current permissions;
add no bespoke full-host executor or fallback.

**Acceptance:** native test reopens an unapproved candidate and confirms
authority is unchanged. Under accepted unrestricted execution, these files
are operational records, not a tamper-proof security boundary.

### P4-03 — Changed-content invalidation and manual review
Bind each review to exact bytes and provenance. Any change invalidates it and
requires a fresh explicit review. Display the diff/evidence; testing, model use,
and App Server approval do not equal promotion. Under accepted
[NB-DEC-012](../decisions/0012-explicit-phase4-activation.md), Review and
Prepare disabled grant no callable access; no Activate UI exists yet.

**Acceptance:** exact review remains visible; changed/replaced content
invalidates it; nothing auto-reviews or activates.

### P4-04 — Integrate, activate, reject, revoke, clean up
Implement separate explicit transitions and durable evidence for each action.
The product UI cannot use a candidate to promote itself, broaden permissions,
erase evidence, or invoke thread/shellCommand. Callable activation additionally
requires a separate explicit NeoBabylon Activate confirmation displaying the
exact reviewed identity and requested permissions. Current authority is full
Windows-user access, not containment. The route and its qualification remain
open; this is not permission to expose an activation action yet.

**Acceptance:** restart-safe tests cover every transition, duplicates, stale
review, denied permissions, failed integration, revocation, and cleanup
without deleting unrelated data.

## Phase 5 — justified divergence and local delivery readiness

### P5-01 — Evidence-gated runtime adaptation
Keep the upstream pin unless a reproducible mismatch prevents an accepted
requirement. For a proposed divergence, record affected upstream files/symbols,
reproducer, compatibility tests, merge risk, removal condition, and rollback
before changing runtime code.

**Acceptance:** with no mismatch, the pin and ancestry remain unchanged; any
future patch has a focused regression test and rollback instructions.

**Focused justified source patch — Guardian OpenRouter route pin (2026-09-25):**
the Guardian V2 scorer is a second production `ResponsesApiRequest`
constructor. It previously omitted the provider route; if Guardian were enabled
through OpenRouter, that would violate the accepted hard route pin. The
current NeoBabylon host keeps `model_messages` null, so the path is presently
inactive. A shared `OpenRouterProviderRouting::for_endpoint` now emits exactly
the configured selector with `allow_fallbacks: false`; both the ordinary
client and Guardian use the validated configured endpoint, while non-OpenRouter
providers omit the OpenRouter field. Regression evidence covers Guardian
WebSocket serialization, Guardian WebSocket-to-HTTP transport fallback,
non-OpenRouter omission, ordinary HTTP and WebSocket serialization, and
provider-only WebSocket cache invalidation. The 89-test Guardian library suite
passed with `RUST_MIN_STACK=16777216` scoped to the test process; the initial
default-stack full-crate attempt overflowed, and its cause is not established.
Formatting and diff checks passed. Helper review was limited to exact excerpts,
not the complete checkout. No provider request, binary build, runtime-lock
change, or claim of provider-side serving occurred. Full P5-01 acceptance
remains open; see [`VERIFICATION.md`](../release/VERIFICATION.md).

### P5-02 — Protected migration, backup, and downgrade
Separate replaceable application files from durable Data. Validate schema/
version, verify backup, preserve records on failure, and define compatible
downgrade or explicit non-destructive recovery. Never reset user data to start.

The durable root is already decided by NB-DEC-005:
`<application root>\Data`. Discovering a packaged application root is a
separate deferred packaging concern, not a choice of another Data location.
The current source-linked host default still needs a separate alignment check;
this record-level work does not change host/root selection.

**Acceptance:** fixture-root integration tests cover restore, interrupted
migration, corrupt/unknown versions, insufficient storage/permission, and
downgrade.

**Focused progress — 2026-09-25:** the isolated host harness now rejects
malformed project-registry JSON and an unsupported schema without rewriting
the original bytes. This does not qualify backup/restore, interrupted
migration, insufficient storage/permission, downgrade, or development/package
root discovery. The durable `<application root>\Data` boundary is already
settled by NB-DEC-005; it is not an open product choice.

**Implementation candidate — 2026-09-26, untested by request:** mutable
NeoBabylon-owned `Data\NeoBabylon\projects.json` and
`Data\NeoBabylon\fork-bookmarks.json` use the Core protected-record writer.
Before replacing existing bytes it validates the current schema, writes an
immutable SHA-256-addressed copy under `Data\Backups\NeoBabylon`, reads that
copy back, stages and flushes the replacement, then publishes a small journal
before same-directory replacement. On the next access a journal resolves only
when the live bytes match the recorded old/new digest; if the record is missing,
the verified old backup can be restored. A divergent/corrupt state stops without
overwriting it. A missing record with an older backup but no journal also
stops rather than defaulting to fresh data; explicit restore requires a missing
target. The existing
schema remains v1: this does **not** invent a v2 migration or accept unknown
future versions. The explicit transition API has an isolated v1-to-v2 fixture
test only; no production record is automatically promoted. Immutable
task-capability bindings remain create-once and
already reject unsupported schema versions.

Only NeoBabylon-authored versioned records are in this boundary. No code here
copies, migrates, or restores live `Data\CodexHome` SQLite, credentials,
WebView2 state, generated tools, or arbitrary Data. A separate quiescence,
consistent runtime backup, access-control, and recovery design remains open.
Fixture-root test code was added but no tests, builds, or live requests were
run; backup/restore, interruption, storage/permission, downgrade, crash
durability, and packaged-root acceptance are **not qualified**. Backups are
retained; pruning/retention, access restrictions, and recovery UI remain open.
SHA-256 readback detects accidental divergence, not malicious same-user
replacement of both record and backup. No diagnostic records
or log output includes record bytes or direct credentials. Unrestricted
same-user tools can still read accessible files independently.

### P5-03 — Local package/install/recovery rehearsal — deferred

**Status — deferred per accepted decision NB-DEC-010 (2026-09-25):** do not
build or rehearse a local package/installer, install NeoBabylon, or qualify
package update/recovery until Martin asks to resume this work. This slice is
deferred, not passed. Its prerequisites and the broader Phase 5 gate remain
open.

Build a local unsigned QA package only after runtime identity, notices,
writable app root, WebView2 prerequisite, update layout, and recovery are
verified. No shared/system-wide install, signing, distribution, or public repo
is part of this local gate.

**Acceptance:** fresh isolated app root runs with exact runtime hash; Data
remains outside replaceable files; interrupted update recovers without
overwriting records. Signing requires suitable credentials and authorization.

### P5-04 — Reproducible bounded performance/soak
Record exact app/runtime, model/provider or fixture, hardware, operation mix,
duration, memory method, latency percentiles, cancellation/restart outcomes,
and thresholds. Separate fixture from live inference; claim no arbitrary-size
or service-level guarantee.

**Acceptance:** repeatable bounded local soak writes evidence to isolated QA
Data and meets reviewed thresholds; no remote load or shared service is used.

## Phase gates

Run Phase 3 inventory before exposing categories; qualify each exposed surface
and large-document range behavior before closing it. Run Phase 4 against a
reviewed candidate contract and after permission/source evidence is
authoritative. Run Phase 5 after migration/package boundaries are reviewed.
Close each phase only when every active corresponding row in
INITIAL_BUILD_PLAN.md is verified. P5-03 is explicitly deferred, not passed,
per NB-DEC-010. Phase 2 screen-reader/full-contrast and release-evidence gates
remain separate; this map neither marks them passed nor treats Windows
containment as qualified.
