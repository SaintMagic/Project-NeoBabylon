# Atomic Codex parity patches

Exact product base: `f126612a00231a58626d72108aac6ee912431ee3` (2026-10-04). These files are proposals, not already-applied product changes. See the [implementation order](../../docs/WIP/CODEX_PARITY_IMPLEMENTATION_ORDER.md), [review](../../docs/WIP/CODEX_PRODUCT_PARITY_REVIEW.md), [specifications](../../docs/WIP/codex-parity/SPECIFICATIONS.md) and [verification record](../../docs/WIP/codex-parity/VERIFICATION.md).

All eight were mechanically checked against the exact base with declared prerequisites, then cumulatively, including `git apply --check --index`, application, whitespace checks and exact reverse restoration. Windows builds and automated tests passed. The complete browser accessibility gate remains failed on contrast measurements, although interaction/width/copy/reload checks completed; do not describe the package as fully accessibility-qualified.

## Common verification and rollback

From the review checkout run `python docs/WIP/codex-parity/verify-patches.py --output .local/Lab/parity-patch-checks.json`. Keep `.gitattributes` beside these patches so Windows does not convert their hunks to CRLF. The verifier emits source identity, patch SHA-256s, prerequisites and changed files. It never applies to the caller's working tree.

Apply patches in an isolated base worktree in numeric order. Commands in the implementation-order document run the complete UI build, C# regressions, Windows host builds, credential/capability probes and browser checks. Individual commands below assume repository root unless stated otherwise. No patch updates the runtime lock, provider routing, generated-tool activation or execution authority.

Rollback with ordinary reverse-order commit reverts; for staged/uncommitted application, use `git apply --reverse --check --index <patch>` before `git apply --reverse --index <patch>`, reversing dependants first. Do not reset unrelated work. No patch introduces a durable conversation/schema migration.

## 001 — Assistant item identity

File: [001-assistant-item-identity.patch](001-assistant-item-identity.patch). Prerequisites: none.

Purpose/visible result: distinct assistant items with identical text remain distinct, and a fallback completion cannot overwrite an incompatible earlier turn. Explicit item identity wins over content equality. This corrects display projection, not authoritative journal contents.

Files: `ui/diagnostic/src/transcript.mjs`; `ui/diagnostic/tests/transcript.test.mjs`.

Verification: `node --test ui/diagnostic/tests/transcript.test.mjs`, then the full UI suite/build. Native check: two equal assistant responses in different turns and streamed/final reconciliation retain both messages after reopening.

Risk/limits: legacy events without stable item IDs still need conservative fallback matching; tests cover the bounded projection contract, not every provider's event order. Reverting restores the old renderer matching and does not delete journal history.

## 002 — Default agent initiative

File: [002-default-agent-initiative.patch](002-default-agent-initiative.patch). Prerequisites: none.

Purpose/visible result: the compiled application instruction tells the agent to own an authorized goal, perform routine prerequisites, choose defaults, verify outcomes and avoid unnecessary permission questions, while respecting personal-file scope, essential system/work software and trusted downloads. Unknown side effects are not blindly retried.

Files: `host/NeoBabylon.Core/CodexModelCatalogBuilder.cs`; `tests/product-parity/AgentPolicyChecks.cs`, `NeoBabylon.ProductParity.Tests.csproj`, `Program.cs`.

Verification: `dotnet run --project tests/product-parity/NeoBabylon.ProductParity.Tests.csproj -c Release` (six policy/exact-model checks before 003, eleven with 003); Release host build. Later run the isolated goal-level fixture from S008 on each selected model.

Risk/limits: prompt compliance is not containment or live model qualification. Instructions remain compiled until S001 implements editing. Ordinary helper scripts can be used through authorized runtime tools; persistent generated-tool registration remains separately gated. Reversion changes future generated catalog policy through normal host regeneration, not past conversations or permissions.

## 003 — Structured tool output

File: [003-structured-tool-output.patch](003-structured-tool-output.patch). Prerequisites: 002 because it creates the source-only test harness.

Purpose/visible result: structured results/errors and dynamic content remain visible; saved inspection and range reads agree; unsuccessful results, exit codes and duration survive projection. Large structured display output receives the same bounded preview treatment rather than disappearing or bypassing the display cap.

Files in `host/NeoBabylon.Core/`: `AppServerNotificationProjection.cs`, `ThreadItemOutputRangeProjector.cs`, `ThreadSavedOutputProjector.cs`, new `ToolOutputText.cs`, `TurnDiagnostics.cs`. Tests: `tests/product-parity/Program.cs`, new `ToolOutputChecks.cs`. UI: `src/App.tsx`, `src/activity-history.d.mts`, `src/restoration.mjs`, `tests/structured-tool-restoration.test.mjs` under `ui/diagnostic/`.

Verification: the product-parity C# command above; `node --test ui/diagnostic/tests/structured-tool-restoration.test.mjs`; full UI and host builds. Test fixtures include structured success/failure, large output, dynamic content and saved command metadata. Native acceptance must compare live card, reopened card and inspect-more for the same item.

Risk/limits: normalization is display-only and may render unfamiliar structures as JSON, not a polished vendor widget. This does not create a pre-parse arbitrary-size memory guarantee, preserve missing upstream bytes, fix every unknown-status classification, or serialize exact provider replay. Revert 003 before 002; historical source remains in the runtime journal.

## 004 — Readable/Wide layout

File: [004-conversation-width-preference.patch](004-conversation-width-preference.patch). Prerequisites: none.

Purpose/visible result: explicit Readable/Wide conversation preference expands the transcript/composer on large displays and survives reload; readable remains the default. Uses existing Astryx controls and responsive layout, not a new settings system.

Files: `ui/diagnostic/src/App.tsx`, `app.css`, new `conversation-layout.mjs` and `conversation-layout.d.mts`; new `ui/diagnostic/tests/conversation-layout.test.mjs`; `tests/qa/ui-accessibility.mjs` and new `product-parity-ui-checks.mjs`.

Verification: `node --test ui/diagnostic/tests/conversation-layout.test.mjs`; full UI tests/build; Windows browser command in implementation order with `--product-parity` after 007. It asserts width change, 2560/1440/960 layouts and preference persistence. Screenshots were inspected at wide/narrow widths.

Risk/limits: localStorage is proportionate for this presentation preference, not durable settings authority; S001 must migrate it once into host settings. Smaller-than-tested windows and native DPI/zoom need manual acceptance. The existing complete contrast gate remains open. Revert 005 before 004; the stored preference can harmlessly remain unused.

## 005 — Copy visible message

File: [005-copy-visible-message.patch](005-copy-visible-message.patch). Prerequisites: 004 for shared UI integration and browser fixture.

Purpose/visible result: Copy message or Copy visible preview writes the exact displayed string, reports Copied only after success and exposes clipboard denial rather than pretending it worked. Copy is disabled while streaming. No extra provider/file fetch occurs to retrieve hidden or omitted material.

Files: `ui/diagnostic/src/App.tsx`, new `MessageActions.tsx`, `app.css`, new `message-copy.mjs` and `message-copy.d.mts`; new `ui/diagnostic/tests/message-copy.test.mjs`; `tests/qa/product-parity-ui-checks.mjs` and `ui-accessibility.mjs`.

Verification: `node --test ui/diagnostic/tests/message-copy.test.mjs`; TS/Vite build; synthetic browser tests for exact copy and denied clipboard. The fixture records earlier task requests before its intentional reload so persistence testing does not erase other flow evidence. Console/external-request assertions remain after the reload checks.

Risk/limits: native WebView2 clipboard policy still needs manual qualification. This copies displayed text only; it is not a code-block-specific copy feature or full-source export. Async completion is guarded against text changes/unmount. Reverting removes the action without altering messages or clipboard history.

## 006 — Canonical QA temporary roots

File: [006-canonical-qa-temp-roots.patch](006-canonical-qa-temp-roots.patch). Prerequisites: none.

Purpose/visible result: Windows QA no longer confuses short/long aliases of its own temporary root. No end-user feature or production guard change.

File: `tests/qa/qa-run-root.test.mjs` only.

Verification: `node --test tests/qa/qa-run-root.test.mjs` on Windows with the expected .NET tools. Existing tests still reject redirected roots, missing parent folders and reused IDs. Linux without .NET is not a substitute for this Windows test.

Risk/limits: fixes the fixture's path identity, not all Windows filesystem behavior. Reversion only reinstates the brittle assertion behavior; no data migration or authority impact.

## 007 — Current accessibility fixture contracts

File: [007-current-accessibility-selectors.patch](007-current-accessibility-selectors.patch). Prerequisites: none.

Purpose/visible result: restores meaningful browser coverage by updating exact capability/thread responses, selecting the intended saved-chat button, matching ActivityCard's group role and the approval button's full accessible name, and checking the diff's actual separate keyboard stop. No product UI is changed or test assertion removed.

File: `tests/qa/ui-accessibility.mjs` only.

Verification: build UI then run the Windows Edge fixture. Without 004 omit `--product-parity`; with 004/005 include it. The nine interaction flows complete on the final tested series. The fixture still fails its contrast gate—this patch deliberately does not hide that result.

Risk/limits: these are deterministic browser mocks, not native WPF, Narrator or live App Server tests. Future UI contract changes require explicit fixture updates. Revert independently, unless later commits have modified the same test hunks.

## 008 — Lossless whitespace in assistant streams

File: [008-lossless-assistant-whitespace.patch](008-lossless-assistant-whitespace.patch). Prerequisites: none.

Purpose/visible result: spaces, newlines and indentation arriving as their own assistant chunks reach the batcher unchanged. Interrupted output no longer depends on a final completed message to restore those characters. Empty/non-string values are still rejected.

Files: `ui/diagnostic/src/App.tsx`, `stream-batcher.mjs`, `stream-batcher.d.mts`; new `ui/diagnostic/tests/stream-whitespace.test.mjs`.

Verification: `node --test ui/diagnostic/tests/stream-whitespace.test.mjs ui/diagnostic/tests/stream-batcher.test.mjs`; full UI suite/build. Three new tests failed before the correction; seven tests passed with the existing batcher tests afterward. The new suite includes actual UI-handler wiring and both assistant notification aliases.

Risk/limits: it cannot recover characters dropped before the host received them and does not change labels/IDs that intentionally use trimmed validation. Native acceptance streams a split-space sentence and indented code, interrupts, and checks the retained preview. Reversion affects future renderer admission only.

## Artifact-only supporting changes

Nested `.gitattributes`, this catalog, docs and the patch verifier are package infrastructure. Branch-scoped GitHub Actions fetch exact source into disposable worktrees with read-only repository permission. They do not implement product features or grant activation authority. Current CI evidence and SHA-256 bindings are in [VERIFICATION.md](../../docs/WIP/codex-parity/VERIFICATION.md).
