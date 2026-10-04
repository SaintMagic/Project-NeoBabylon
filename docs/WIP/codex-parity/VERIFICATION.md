# Verification record — 2026-10-04

Product source: `f126612a00231a58626d72108aac6ee912431ee3`.

Final tested patch/spec commit: `d2831949f424c3643eb57c143aa5a075662cc805`. Subsequent report/catalog/evidence-only commits do not change those patch bytes. Local copies of all eight patches were compared to the Windows verifier's SHA-256 manifest and match.

Authoritative CI: [run 37205696436](https://github.com/SaintMagic/Project-NeoBabylon/actions/runs/37205696436), workflow `.github/workflows/codex-product-parity-check.yml`. Its overall conclusion is FAILURE, specifically the retained browser contrast gate. Do not turn the successful individual checks below into a claim that the entire run passed.

## Actual results

| Layer | Command/evidence | Result |
|---|---|---|
| Source identity | 303 canonical blobs and Git tree/commit verification; Windows fetch of exact base | Pass |
| Mechanical patch validation | `verify-patches.py`; each patch with only declared prerequisites plus cumulative series | 9 combinations pass apply/index/whitespace/exact reverse checks |
| UI automated tests | `npm test` in `ui/diagnostic` | 175/175 pass; 0 failed/skipped |
| UI production build | `npm run build` (TypeScript + Vite) | Pass; dependency bundler `use client` directive warnings remain |
| C# source-only regressions | `dotnet run --project tests/product-parity/NeoBabylon.ProductParity.Tests.csproj -c Release` | 11/11 pass |
| Native host compilation | Release build of `host/NeoBabylon.Host/NeoBabylon.Host.csproj` | Pass; 0 warnings, 0 errors |
| Host fixture compilation | ApprovalQA build of `tests/host/NeoBabylon.Phase1A.Tests.csproj` | Pass; 0 warnings, 0 errors |
| Existing identity probe | `--probe-rename-saved-thread` | `RENAME_AND_CAPABILITY_SWITCH_CHECKS=PASS` |
| Existing credential probe | `--probe-development-credential-records` | Pass: missing/invalid/synthetic DPAPI records and child-process exclusion |
| Adapter bundle / QA roots | `node --test tests/qa/nvidia-adapter-bundle.test.mjs tests/qa/qa-run-root.test.mjs` | 7/7 pass |
| Synthetic browser interactions | Windows Edge 153.0.4234.48; `ui-accessibility.mjs ... --product-parity` | All 9 recorded interaction flows complete; copy success/denial, 2560/1440/960 width checks and reload preference checks complete |
| Browser console/network | Fixture capture | 0 console errors; 0 external requests |
| Browser contrast | 9 audited states | FAIL: 43 active below-threshold readings, 9 inactive exceptions, 0 indeterminate readings; see F20 addendum |
| Visual inspection | Generated narrow/wide screenshots opened during review | Layout/control placement inspected; not comprehensive accessibility/native visual approval |
| Native WPF/WebView2 execution | Actual locked App Server + native window | Not run |
| Live providers / model task outcomes | Real selected provider/model/credentials | Not run |
| Narrator / native clipboard / DPI | Windows desktop manual tests | Not run |

The C# tests are a console regression harness, not an assertion that `dotnet test` discovered a full test framework. The identity/credential probes are two named synthetic checks, not the full host integration suite. The NVIDIA bundle check compiles/verifies the adapter bundle; it does not prove live NVIDIA conversation or restart support. `tests/qa/nvidia-adapter.test.mjs` includes a later private-runtime integration gate and was not represented as fully passed.

## Browser evidence and open finding F20

The recorded flows are project selection/focus return, loaded-history search/resume, model keyboard/exact binding, send/attributed failure, informational unknown tool status, approval denial, review dialog keyboard/focus, diagnostics keyboard/focus and stop-turn activation. The parity helper additionally checked exact displayed-text copying, denied clipboard handling, readable/wide changes, tested viewport containment and width persistence after reload.

The completed contrast audit reports 43 active failures across repeated UI states, not 43 distinct bugs; there are 15 distinct text/class combinations. Examples: light telemetry text `rgb(121,131,152)` on white at 3.81:1; enabled Compact context label at 3.33:1; reasoning field label at 1.99:1 dark / 3.15:1 light. These are measured fixture results, not an independent WCAG certification. The disabled-control exceptions remained separate; nothing was relabeled disabled to make the gate pass.

The implicated telemetry/reasoning selectors already exist in base source, and 004/005 do not change those color declarations. However, a complete clean unpatched browser comparison was not run, so the record does not assert that every measurement is a proved pre-existing defect. The selector/theme integration and audit visibility rules require the bounded follow-up in [VALIDATION_FINDINGS.md](VALIDATION_FINDINGS.md). Full accessibility acceptance remains blocked. No speculative global CSS override was bundled merely to force green CI.

## Regression and failure history

The recovered interrupted package was not assumed trustworthy. Its early Windows run failed `git apply` because repository text attributes converted unified patch files to CRLF. LF versus CRLF patch behavior was reproduced locally. A nested `.gitattributes` now preserves patch LF; Windows then passed every exact apply/reverse combination without whitespace-ignore flags.

Run [37204635426](https://github.com/SaintMagic/Project-NeoBabylon/actions/runs/37204635426) passed mechanical/build/unit checks but stopped at an obsolete approval selector. Patch 007 was corrected to the real accessible name and the actual diff tab stop. Run [37204959545](https://github.com/SaintMagic/Project-NeoBabylon/actions/runs/37204959545) completed the interactions and parity helper, then exposed a fixture evidence-lifetime error: the helper's intentional reload reset its mock request log. Patch 005 now captures earlier flow evidence before reload and retains post-check console/external-request assertions. Final run 37205696436 reaches and honestly fails the existing contrast gate.

For patch 008, three new regression tests failed before the change; those three plus four existing batcher tests passed afterward. The full Windows UI suite also passes. The test preserves whitespace-only chunks, rejects empty/non-text values, simulates interrupted output and verifies the actual App handler uses the helper for both aliases.

Local Linux testing additionally passed a 172-test dependency-independent UI subset. The first raw baseline attempt had environment failures from absent installed UI packages and Node 22 TypeScript handling; it was not counted as a clean baseline or used to invent product bugs. Windows CI installed the repository's locked UI dependencies and ran the complete suite on Node 24.21.0. Local .NET was unavailable; successful .NET results above came from Windows CI, not this container.

## Patch identities

| Patch | SHA-256 | Prerequisites |
|---|---|---|
| 001 | `c17c5e2b60fb9cdb69a292f2f8bc544995c5da890ade022908c233b2c8776c31` | None |
| 002 | `47634702aa22af8ee9bf7de0ba8a4357483cd8341ec9cba56560967d21f3b1c0` | None |
| 003 | `4fb387eb70c746e3f31e83dc8fdaead05815cdf3b5debe3e6ebe6b55b0d6ef1a` | 002 |
| 004 | `f2f1c44b752cc3ec3565fbaac5cac74ae586e1b0079dd8b582c2e456b57e68fc` | None |
| 005 | `2d3b6f7563ad5ad39064b83fcaad4d8ed547118d728675c63399aaf7dd205900` | 004 |
| 006 | `44a0690813bff767946556666d1cf224a18e0ac45bcc91086547c234b81b80ba` | None |
| 007 | `2e2e4b77d51af175fd97210533e9560d79e0cdbbf7cdf989300489129f8b235e` | None |
| 008 | `1c05cb4624090da20dfc26098fa45605270b839d9c3e4d9a55f1191b31cae3e6` | None |

CI artifact `neobabylon-patch-verification`, ID `11304910310`, ZIP SHA-256 `6f9f38356c04a2a74a7b8f88ad16b467017958d2330d4827a62b27d75dfb305c`, contains the patch manifest, command logs, environment record, screenshots and `accessibility-evidence.json`. The archive was downloaded and its digest checked. Actions artifacts expire after 14 days; the durable facts/hashes/commands are recorded here, and a downloadable handoff can retain raw evidence independently.

## Remaining qualification requirements

The public checkpoint omits the exact locked App Server executable/private source and generated schema cache. No private credentials or desktop environment were used. A successful WPF build is not native execution; a browser mock is not provider qualification; prompt-contract tests are not proof that a model will complete a real installation autonomously.

Before shipping, run native Windows scenarios for interrupted whitespace, duplicate assistant text, structured output/reopen/range paging, copy failure, wide/narrow/DPI, reasoning effort and identity preservation. Run the current exact-runtime command-stop/turn-cancel/compaction/recovery suites in isolated authorized workspaces. Fix and rerun the contrast gate. Each provider-specific new feature in S001–S009 needs deterministic transport fixtures and exact selected-tuple live qualification. Generated-tool activation remains disabled until its separate accepted gates pass.

No qualification or runtime-lock file was updated by this review.
