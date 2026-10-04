# Test layout

For native and browser QA, `<source>` is the NeoBabylon repository root.
Evidence belongs under `<source>\.local\Lab\Runs\<validated-run-id>`, beside
that run's `App` child, which is the isolated application root. A native
runner's `<QA-parent>` argument must resolve exactly to
`<source>\.local\Lab\Runs`; archived Data, an old sibling, an ancestor, and a
nested directory are invalid QA parents. Every WPF launch and restart sets
process-scoped `NEOBABYLON_APPLICATION_ROOT` to the selected `App` path.
The existing `%LOCALAPPDATA%\NeoBabylon` state is not a QA target.

- `qualification/MockProbeQualification.Tests.ps1` and
  `qualification/MockProviderError.Tests.ps1` create fresh probe source roots
  under `.local\Lab\ProbeSources` and application roots at
  `.local\Lab\Runs\<run-id>\App`. Each fixture writes its own probe-relative
  `runtime-lock.json`; the product lock remains unchanged, and historical
  probe locks are not rewritten. The `Phase1AQualification` runner uses the
  same Lab Runs application-root layout by default, reserves a fresh run
  directory with an exclusive claim, and sets
  `NEOBABYLON_APPLICATION_ROOT` in its own process when no explicit root is
  supplied. These mock fixtures do not perform live inference.
- `qa/no-profile-pwsh-shim.rs` is a test-only launcher for isolated Windows
  Codex App Server integration tests whose login-shell assertions are
  contaminated by the workstation PowerShell profile. Compile it outside the
  source tree as `pwsh.exe`, prepend that directory only to the test process
  `PATH`, and set `NEOBABYLON_QA_PWSH_REAL` to the actual PowerShell executable.
  It adds `-NoProfile` and changes no global shell policy. Tests that explicitly
  require `python3` may also need a process-local alias to the installed Python
  executable; do not change system or user PATH/configuration for the test.
- `qualification/UpstreamComparison.Tests.ps1` is a dependency-free native
  PowerShell fixture test for `scripts/compare-upstream.ps1`. It creates a
  temporary local Git checkout with one tracked edit and one untracked file,
  checks two exact stdout reports against `fixtures/upstream-comparison/`, and
  proves the manifest, lock, and Git worktree inputs remain unchanged. It uses
  PowerShell assertions rather than Pester.
- `qa/ui-accessibility.mjs <playwright-core-entry.mjs>` runs nine deterministic
  browser interaction flows and audits rendered text/placeholder contrast
  across eight UI states in Edge. Its evidence labels the run as a synthetic
  host, with no live provider or native WPF; it does not exercise Windows
  Narrator. Any below-threshold disabled/`aria-disabled` text or indeterminate
  background now fails the audit, separately counted from active-text failures.
  Native WPF/WebView2 scripts must use a freshly built host executable in a
  unique QA output directory; the pre-existing `bin` host can be older than
  current source and produce misleading failures. Build with
  `dotnet build host/NeoBabylon.Host/NeoBabylon.Host.csproj --configuration Release --no-restore -p:OutDir=<unique-QA-directory>\`
  and pass that directory's `NeoBabylon.Host.exe` explicitly to the runner.
- `qa/native-visual-acceptance.mjs` runs the bounded native WPF/WebView2 visual
  acceptance flow and records Chromium accessibility-tree snapshots for the
  primary shell and Runtime diagnostics modal. The snapshots assert key role/
  name exposure and modal inertness; they are not Windows Narrator or full
  screen-reader qualification. The machine result and screenshots are written
  under a validated run in `.local\Lab\Runs`, beside its `App` child.
- `fixtures/` holds deterministic protocol, provider, filesystem, and
  permission fixtures.
- `host/` exercises the pinned App Server and host protocol against disposable
  application roots and deterministic Responses fixtures. The normal suite
  attempts to delete its own test roots. Run the framework-dependent suite with
  `dotnet run --project tests/host/NeoBabylon.Phase1A.Tests.csproj --configuration
  ApprovalQA --no-restore` or launch its generated apphost executable; do not
  invoke the test `.dll` directly through `dotnet`, because fake-server tests
  relaunch `Environment.ProcessPath` as their protocol child.
- The `--probe-exited-supervisor-client-recovery` host mode isolates the saved-
  task recovery-after-App-Server-exit path. Its process termination targets
  come from the QA supervisor's exact PID/start-time identity, not a
  machine-wide process-name scan.
- The host suite includes a P3-02 failed-`exec_command` fixture that checks
  exact partial output/exit-code retention, typed thread/turn/item attribution,
  one provider follow-up, and no retry or provider/model fallback.
- `qa/native-command-failure-rendering.mjs <NeoBabylon.Host.exe>
  <playwright-core-entry.mjs> <QA-parent>` runs the same bounded
  failure through a real WPF/WebView2 host and the pinned App Server with a
  synthetic capability record and loopback Responses fixture. Build the UI
  and a Release host into a unique QA run directory first. The fixture
  command emits a marker and exits 23; the test verifies the exact item ID and
  exit code in both the App Server event and final host diagnostic, visible
  and accessible `FAILED`/`Exit code 23`/attribution, the partial output, an
  isolated application `Data` root, exactly one continuation, and no fallback.
  A synthetic credential-shaped canary in the test command output must be
  forwarded only to the loopback fixture and rendered as ordinary tool output;
  it is not a real credential. The test uses no live inference or API key. The synthetic catalog record is
  removed only if its bytes remain unchanged; results and screenshots stay in
  the selected QA run directory beside `App`.
- `qa/native-command-stop.mjs <NeoBabylon.Host.exe>
  <playwright-core-entry.mjs> <QA-parent>` runs
  the P3-02 command-stop path through a real WPF/WebView2 host, the pinned App
  Server, and a loopback Responses fixture. Build the diagnostic UI and host
  first. The fixture starts one isolated command whose completion side effect
  is scheduled after five seconds, verifies turn Stop leaves it running, then
  activates the separate keyboard-accessible command Stop. It checks the
  interrupted and stopped UI states, Codex attribution, exact process exit,
  and absence of the scheduled marker after a 5.25-second observation window.
  It uses no live inference or API key; the
  application `Data` root and screenshots/result remain under the unique QA
  run directory. Cleanup targets only the fixture command's recorded PID and
  start-time identity; the synthetic capability record is removed only if its
  bytes remain unchanged.
- `host/` also has an explicit `--prepare-patch-review-qa <new-app-root>` mode
  that retains one isolated mock-generated patch task for a native WPF reopen
  check. Use a new application-root path outside the source repository; this
  mode refuses an existing path. `--probe-patch-review-resume <app-root>
  <thread-id>` and `--probe-patch-supervisor <app-root> <thread-id>` inspect
  that saved task without provider inference.
- `host/` has a bounded `--probe-live-openrouter-patch <new-app-root>` mode for
  the exact canonical NEX free-model route. It requires `OPENROUTER_API_KEY`
  in the calling process, refuses an existing application root, uses an
  isolated root outside the source repository, and enables function-form patch
  metadata only in a test-owned generated catalog. An optional third argument
  names a candidate runtime lock for testing before the canonical lock changes.
  A passing terminal turn
  alone is insufficient: the probe requires a changed file and saved patch
  review. The first live run failed those assertions and its retained root is
  evidence for the earlier freeform mode, not a successful qualification.
- `host/` has `--probe-function-patch <candidate-runtime-lock>` for the same
  tracked-patch fixture through a separately built and hashed App Server. It
  does not change the canonical runtime lock.
- `qa/native-patch-review.mjs` checks a running WPF/WebView2 QA host's saved
  patch review through its explicit test-only CDP port. Pass the port, an
  installed Playwright Core module path, and an output screenshot path. The
  script does not start or stop the host and does not call a live provider.
- `qa/native-brand-mark.mjs` checks that the running WPF/WebView2 shell loads
  the accessible decorative tower SVG in the sidebar at its intended size.
  Pass the test-only CDP port, installed Playwright Core module path, and an
  output screenshot path. It does not start or stop the host or call a provider.
- `qa/native-history-pagination.mjs` runs P2-01 in a fresh isolated QA root:
  it seeds 56 saved conversations through a deterministic local Responses
  fixture, launches the WPF host against that isolated application root, then
  checks newest-first pagination, loaded-page search messaging, exact provider/
  model attribution, cursor exhaustion, empty-project isolation, and reopening
  the oldest transcript without another inference. It requires the test-runner
  apphost executable, WPF host executable, Playwright Core entry module, and a
  the exact `<QA-parent>` directory. The QA host window is closed by the harness;
  the isolated data, screenshot, and `result.json` are retained for inspection.
- `qa/native-model-capability-rebind.mjs` runs P2-02 with two unique, synthetic
  capability records and loopback-only Responses endpoints. It creates one
  task per record, restarts the native host, changes only Model B's synthetic
  effective context to verify history-only invalidation, then reopens Model A
  under its original binding. It requires the WPF host executable, installed
  Playwright Core entry module, and the exact `<QA-parent>` directory. The
  harness creates and removes only its
  uniquely named test capability records; its isolated application data and
  screenshots are retained. It does not call a live provider.
- `qa/native-pending-send-recovery.mjs` runs P2-03 with a synthetic LM Studio
  capability record and a loopback-only Responses fixture. It checks the
  pre-`turn/started` retry boundary across WebView2 reload, and the
  post-acknowledgement no-replay boundary across WPF host restart, comparing
  provider request counts, App Server journal entries, and draft/UI state. Pass
  a freshly built WPF host executable, the installed Playwright Core entry
  module, and the exact `<QA-parent>` directory.
  The test creates and removes only its uniquely named synthetic capability
  record; isolated application data and screenshots are retained. It performs
  no live provider inference.
- `qa/native-draft-storage-failure.mjs` runs P2-04 against three isolated
  native WPF/WebView2 application roots using a synthetic LM Studio catalog
  record and a loopback-only Responses fixture. It injects draft write failure,
  failure of both remove and empty-value clear fallback after an accepted
  send, and total Web Storage API failure. It verifies visible warning/draft/
  saved-history state across real host restarts and asserts exact fixture
  request and App Server journal counts, with no automatic replay. Run it as
  `node tests/qa/native-draft-storage-failure.mjs <NeoBabylon.Host.exe>
  <playwright-core-entry.mjs> <QA-parent>` after building the
  current WPF host to an isolated output directory. The final QA run and
  screenshots are retained under `.local\Lab\Runs`. No live model,
  provider inference, or production capability claim is involved; text that
  could not be persisted is expected to be unavailable after restart.
- `qa/native-cross-project-recovery.mjs` runs P2-05 in a native WPF/WebView2
  host with two seeded local projects and a loopback-only deterministic
  Responses fixture. It delays a project history refresh, verifies the busy
  UI state, restarts the host with an accepted Beta turn pending, rejects an
  Alpha resume while Beta is selected, and deliberately continues the exact
  Beta and Alpha tasks. Run it as
  `node tests/qa/native-cross-project-recovery.mjs <NeoBabylon.Host.exe>
  <playwright-core-entry.mjs> <QA-parent>` after building the
  current host to an isolated output directory. It temporarily creates a
  uniquely named synthetic catalog record under `docs/release/`, then removes
  it only if its content still matches the test-created bytes. QA application
  state, result JSON, and screenshots are retained beside `App` in the QA run.
  It uses no model weights or live inference.
- `qa/native-turn-interruption.mjs` runs P2-06 and its native keyboard subset
  against a synthetic LM Studio capability and a loopback Responses fixture.
  It submits, stops, and deliberately continues with Enter; streams one
  partial delta; verifies the saved interrupted status after renderer reload
  without replay; and checks request/journal counts and pinned App Server
  cleanup. The retained result records each Enter-activated action.
  Run it as `node tests/qa/native-turn-interruption.mjs <NeoBabylon.Host.exe>
  <playwright-core-entry.mjs> <QA-parent>` after running
  `npm run build` in `ui/diagnostic` and building the WPF host. It creates a
  uniquely named temporary catalog record under
  `docs/release/` and removes it only if its content hash is unchanged. The
  app root, `result.json`, and screenshots stay in the selected QA run;
  no real model, provider inference, or durable partial-output claim is made.
- `qa/native-live-patch-review.mjs` runs P2-07 through native WPF/WebView2 and
  the pinned App Server with a deterministic loopback Responses fixture. It
  verifies exact-thread/turn live diff attribution, the saved read-only review
  after renderer reload and graceful host restart, and isolated application
  `Data`. Its live review path also verifies keyboard opening, modal inertness,
  Tab/Shift+Tab reachability, Escape close, and focus return. Run
  `node tests/qa/native-live-patch-review.mjs <NeoBabylon.Host.exe>
  <playwright-core-entry.mjs> <QA-parent>` after building the
  current WPF host into an isolated output directory and building the UI. It
  temporarily creates a unique synthetic capability record under
  `docs/release/`, deleting it only if its contents still match the test bytes.
  The loopback server accepts exactly the expected fixture requests; no API
  key or live inference is used. The app root, `result.json`, and screenshots
  are retained in the selected QA run beside `App`.
- `qa/native-approval-qualification.mjs` runs the bounded P2-08 approval path
  through native WPF/WebView2 and the exact pinned App Server, using a
  deterministic loopback Responses fixture. Run
  `node tests/qa/native-approval-qualification.mjs <ApprovalQA NeoBabylon.Host.exe>
  <playwright-core-entry.mjs> <QA-parent>` after building the
  test-only `ApprovalQA` host. It creates three fresh application roots and
  verifies native one-shot accept, explicit deny, and fail-closed timeout for
  an App-Server-generated `exec_command` approval. The only accepted command is
  the read-only Windows version query `ver`; accepting a host command is not
  containment-qualified. This QA profile is read-only/on-request and does not
  change the product's unrestricted default. Runtime identity/hash, per-root
  `Data`, provider request counts, approval-resolution identity, process exit,
  and screenshots are retained in the selected run under `.local\Lab\Runs`.
  It uses synthetic capability metadata and no live
  provider/model inference or API key.
- `qa/native-large-output.mjs` runs the P2-11 deterministic native-output path
  and the P3-04 large-document proof through WPF/WebView2 and the pinned App
  Server. Run it with the test-runner executable, a freshly built host
  executable, Playwright Core's `index.mjs`, and the exact `<QA-parent>`
  directory. It uses a loopback Responses fixture and a
  generated 1.1-million-character document in the isolated workspace; it
  checks bounded previews, saved-output identity and ordered 32,768-character
  pages, explicit unknown-completeness disclosure when upstream omits content
  without a marker, cancellation/reload behavior, and typed late failure. The
  result and screenshots remain in QA output; the temporary capability record
  under `docs/release/` is removed only if its bytes are unchanged. No live
  model, provider inference, or API key is used.
- `qa/native-tool-capability-catalog.mjs` verifies the P3-03 read-only
  capability catalog in native WPF/WebView2. After building the diagnostic UI
  and a fresh Release host, run
  `node tests/qa/native-tool-capability-catalog.mjs <NeoBabylon.Host.exe>
  <playwright-core-entry.mjs> <QA-parent>`. It uses a uniquely
  named synthetic capability record, a loopback fixture with a fail-if-
  Responses guard, and a fresh `App` root in a validated QA run;
  verifies all 13
  categories, filtering, explicit Unknown and Blocked states without call
  controls, and the same tuple after graceful restart/reopen. It fails if a
  Responses inference request occurs and retains `result.json` plus before/
  after screenshots in the selected QA run beside `App`. The temporary synthetic
  capability record under `docs/release/` is removed only if its bytes remain
  unchanged. No live model/provider, API key, or production capability claim
  is involved.
- `qa/native-live-nex-output.mjs` is a bounded live qualification attempt for
  the exact OpenRouter `nex-agi/nex-n2.5-pro:free` alias with request-side
  `nex-agi/fp8` pinning and provider fallbacks disabled. Run it with the
  Phase 1A test-runner executable, a freshly built WPF host executable,
  Playwright Core's `index.mjs`, and the exact `<QA-parent>` directory.
  Supply `OPENROUTER_API_KEY` through a hidden, process-only prompt; the runner
  clears its environment copy immediately after starting the host. It uses a
  fresh isolated app root and a no-tools prompt, checks the effective Codex
  model/route config, visible native output, runtime hash, and credential
  persistence scan, and retains only output length/hash, timing, process-memory
  samples, UI screenshot, and protocol metadata. The native UI/App Server call
  does not expose an explicit output-token cap, so the prompt-size bound is not
  a hard generation-token bound. Provider-side post-hoc route attribution is
  recorded as unknown if the generation lookup is unavailable; do not treat a
  request-side route pin as independent provider attribution. No response
  text or credential value is written to the QA result.
- `performance/` is reserved for measured latency, throughput, cancellation,
  and soak checks; no broad performance gate is claimed yet.
