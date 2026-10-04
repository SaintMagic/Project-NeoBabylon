# S008 — High initiative with explicit task scope

Priority: Important product foundation; technical containment remains deferred. Specification. Base: `f126612a00231a58626d72108aac6ee912431ee3`.

## Existing execution and the actual gap

`CodexConfigBuilder`, `AppServerProtocol`, `RuntimeSupervisor` and `SandboxAuthorityDiagnostics` deliberately use full logged-in Windows-user authority (`danger-full-access` / `never`) under NB-DEC-008. Ordinary model-controlled shell/tools already provide substantial general agency. The application does not need a bespoke tool for every installer or helper script. Patch 002 improves the compiled initiative policy but is neither a behavioral qualification nor filesystem/system protection.

The gap is a durable, inspectable task-scope and workflow-result contract, plus practical browser/desktop adapters where shell alone is insufficient. Preserve the existing model/runtime loop. Do not add generic renderer RPC, route model actions through the human `thread/shellCommand` escape surface, or require approval for each package/environment/helper.

## Proposed task context

Add a host-owned `TaskScope` record referenced by the thread/turn configuration: user goal, explicit workspace/attachment resources, individually granted external paths, operation categories already authorized, protected system/work software constraints, and unresolved consent requirements. Record the user's grant source and scope revision. A user selecting a project or attaching a file grants that resource for the stated task, not all Documents/Desktop/mail/browser profiles. Model output cannot expand scope or turn an untrusted document instruction into a user grant.

S001 composes a bounded scope summary once at the appropriate runtime instruction boundary. The user can inspect and edit scope through a concise native/Settings surface; routine execution uses the existing grant without repeated prompts. Carry scope revision with queued sends and recheck before host-mediated reads/writes. Do not scan personal directories to discover which files might be sensitive.

Technical truth must remain explicit: arbitrary same-user shell can bypass advisory prompts and host file-picker checks. This design improves agent behavior and the host's own operations; it cannot guarantee that unrestricted tools will not access personal files or break software. A future enforcement claim requires an explicit authority/containment decision and qualification. Do not market prompt text as a sandbox or silently reactivate the failed Windows containment mode.

## Workflow behavior contract

An authorized goal implies ordinary dependency installation, task-local configuration, helper scripts/virtual environments, reasonable defaults, application launch/restart, retry of safely repeatable failures, cleanup of owned temporary files and verification. Temporary helpers remain ordinary task tools; creating or reviewing a persistent GeneratedTools candidate does not activate a callable registration. Keep NB-DEC-012's separate manual activation and exact qualification gates.

Before an unusually high-consequence step, determine whether existing authority actually covers it. Ask only for missing private information, materially divergent choices, credentials/external consent, or destructive/irreversible work outside the grant. A routine prerequisite is not one of those conditions. Browser login, purchases, external sharing and elevated system changes remain distinct from installing a project dependency.

For software installation prefer the publisher or a trusted package source; resolve package identity, requested version and source before executing. Verify signatures/hashes when supplied by a trusted origin and record that provenance. A hash copied from the same untrusted page is not independent trust. Avoid download-and-execute from model-invented URLs. Do not disable security software, remove work-login/VDI components, broadly change services or replace essential PATH entries as a speculative fix. Prefer reversible local environment changes; preserve previous values for necessary wider changes.

## Process, browser and verification foundations

Track processes launched for the task by owned handles, creation time and executable identity, not name-only matching. Waiting for a process and polling a generated result are normal workflow steps. A launcher exiting zero does not prove its child installer completed or that the requested settings/import took effect. Define a postcondition per task step: installed version/path, configuration readback, expected output file/hash, application-visible import/result, or a targeted functional probe.

Browser automation should attach to a deliberately scoped profile/session and expose credentials/consent boundaries. Do not reuse the user's personal browser profile by default or claim computer-control support from a shell button. Existing shell can install ordinary trusted local tooling under the task grant; a new browser/desktop adapter still needs exact action/result/error/cancel schemas, bounded screenshots and target-window identity. Do not build every desktop-control feature in this first batch.

Maintain a bounded task execution ledger of intended step, attempted approach, observed result, verification and remaining uncertainty. This is operational evidence, not fabricated hidden reasoning. A retry after uncertain side effects uses S007 reconciliation, not blind re-execution. Cleanup only app/task-owned temporary resources recorded in the ledger; never delete a user folder because its name resembles a temp path.

## Tests and acceptance

Use isolated Windows fixtures, never the real personal/work environment: a dummy package installer, disposable PATH/config, owned test process and a local browser page requiring an explicit consent action. Test normal prerequisites proceed without an approval cascade; missing credential pauses once; unrelated personal path stays out of host access; a document prompt injection cannot expand scope; untrusted download provenance is rejected; named VDI/work components are not removed; a misleading zero-exit installer fails postcondition verification; and cancellation kills only the owned test process.

Run a complete goal-level task (find trusted fixture package, install locally, configure, launch, import fixture, verify output) without manually spelling out each tool call. Capture actual action counts, requests for input and postconditions. Model-level success is empirical per exact provider/model, not proved by checking that prompt strings exist.

Implement task-scope record and instruction composition, then result-ledger/verification fixtures, then selected browser/desktop integration. No decision blocks these advisory/host-owned improvements. Only a promise of technical enforcement against unrestricted model shell requires reopening authority, which this review does not recommend doing in the first batch.
