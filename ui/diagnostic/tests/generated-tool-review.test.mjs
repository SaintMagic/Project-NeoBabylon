import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { bindingActionContext, candidateContract, confirmBindingTransition, confirmDisabledStageResponse, createSerialRequestQueue, currentReviewRecord, parseCandidateFilePage, parsePreparedBindingHistory, parsePreparedBindingToolIds, parseReviewHistory, reviewNoteError, stageBindingContext, verifyStageRecovery } from "../src/generated-tool-review.mjs";

const request = { toolId: "sample-tool", contentIdentity: "candidate-v1:sha256:abc", path: "tool.js", offset: 0 };

test("file page requires exact host attribution, identity, path, and UTF-16 positions", () => {
  const response = { attributedTo: "NeoBabylon.Host", ...request, text: "a😀b", total: 4, next: 4, hasMore: false };
  assert.deepEqual(parseCandidateFilePage(response, request), { path: "tool.js", offset: 0, text: "a😀b", total: 4, next: 4, hasMore: false });
  assert.throws(() => parseCandidateFilePage({ ...response, contentIdentity: "stale" }, request), /identity/i);
  assert.throws(() => parseCandidateFilePage({ ...response, next: 3 }, request), /range/i);
  assert.throws(() => parseCandidateFilePage({ ...response, hasMore: true }, request), /range/i);
  assert.throws(() => parseCandidateFilePage({ ...response, attributedTo: "Codex App Server" }, request), /attribution/i);
});

test("review history is tied to the requested tool and current content identity", () => {
  const record = { toolId: "sample-tool", candidateContentIdentity: request.contentIdentity, decision: "reviewed", reviewIdentity: "review-1", note: "Inspected locally", recordedAtUtc: "2026-09-27T00:00:00Z", sequence: 1, recordSha256: "a".repeat(64) };
  const history = parseReviewHistory({ attributedTo: "NeoBabylon.Host", toolId: "sample-tool", history: [record] }, "sample-tool");
  assert.deepEqual(currentReviewRecord(history, request.contentIdentity), record);
  assert.equal(currentReviewRecord(history, "changed"), null);
  assert.throws(() => parseReviewHistory({ attributedTo: "NeoBabylon.Host", toolId: "another-tool", history: [record] }, "sample-tool"), /identity/i);
});

test("review notes must be explicit, bounded, and single-line", () => {
  assert.equal(reviewNoteError("Inspected the candidate."), null);
  assert.match(reviewNoteError("  "), /note/i);
  assert.match(reviewNoteError("line one\nline two"), /single line/i);
  assert.match(reviewNoteError("line one\u0085line two"), /single line/i);
  assert.match(reviewNoteError("a".repeat(2049)), /2,048/);
});

test("complete declared invocation and schemas are required before a review decision", () => {
  const contract = { invocation: "manual only", inputSchema: "{\"type\":\"object\"}", outputSchema: "{\"type\":\"string\"}" };
  assert.deepEqual(candidateContract({ contract }), contract);
  assert.equal(candidateContract({ contract: { ...contract, outputSchema: "" } }), null);
  assert.equal(candidateContract({}), null);
});

test("candidate host requests serialize and a rejected request does not poison the queue", async () => {
  const run = createSerialRequestQueue();
  const order = [];
  let releaseFirst;
  const first = run(() => new Promise((resolve) => { order.push("list-start"); releaseFirst = resolve; }));
  const second = run(async () => { order.push("file-start"); throw new Error("read failed"); });
  const third = run(async () => { order.push("history-start"); return "history"; });
  await Promise.resolve();
  assert.deepEqual(order, ["list-start"]);
  releaseFirst("listed");
  assert.equal(await first, "listed");
  await assert.rejects(second, /read failed/);
  assert.equal(await third, "history");
  assert.deepEqual(order, ["list-start", "file-start", "history-start"]);
});

test("candidate card presents all contract fields as inert text and gates decisions on completeness", () => {
  const source = readFileSync(new URL("../src/App.tsx", import.meta.url), "utf8");
  const card = source.slice(source.indexOf("function CandidateCard("), source.indexOf("function UnapprovedToolsDrawer("));
  assert.match(card, /const contract = candidateContract\(candidate\)/);
  assert.match(card, /<pre tabIndex=\{0\}>\{contract\.invocation\}<\/pre>/);
  assert.match(card, /<pre tabIndex=\{0\}>\{contract\.inputSchema\}<\/pre>/);
  assert.match(card, /<pre tabIndex=\{0\}>\{contract\.outputSchema\}<\/pre>/);
  assert.match(card, /const canDecide = Boolean\(toolId && contentIdentity && contract && history/);
  assert.match(card, /if \(!toolId \|\| !contentIdentity \|\| !contract \|\| !pendingDecision/);
  assert.doesNotMatch(card, /dangerouslySetInnerHTML/);
});

test("all candidate host operations use the drawer-wide serialized request", () => {
  const source = readFileSync(new URL("../src/App.tsx", import.meta.url), "utf8");
  const drawer = source.slice(source.indexOf("const requestCandidateHost: CandidateHostRequest"), source.indexOf("function DiagnosticsDrawer("));
  for (const operation of ["listGeneratedToolCandidates", "readGeneratedToolCandidateFileRange", "listGeneratedToolReviewHistory", "recordGeneratedToolReview", "rejectGeneratedToolCandidate"]) {
    assert.match(drawer, new RegExp(`requestCandidateHost\\([^;]*["']${operation}["']`));
  }
  assert.match(drawer, /candidateRequestQueueRef\.current!\(\(\) => requestHost<JsonRecord>\(operation, payload\)\)/);
});

const reviewed = { toolId: "sample-tool", candidateContentIdentity: request.contentIdentity, decision: "reviewed", reviewIdentity: "review-v1:sha256:abc", recordSha256: "a".repeat(64) };
const prepared = { schemaVersion: 1, toolId: "sample-tool", candidateContentIdentity: request.contentIdentity, reviewIdentity: reviewed.reviewIdentity, reviewInterfaceVersion: "1", reviewRecordSha256: reviewed.recordSha256, state: "prepared-disabled", activationState: "disabled", callableRoute: "none", note: "Prepared locally", recordedAtUtc: "2026-09-27T00:00:00Z", sequence: 1, previousRecordSha256: "", recordSha256: "b".repeat(64) };
const revoked = { ...prepared, state: "revoked", sequence: 2, previousRecordSha256: prepared.recordSha256, recordSha256: "c".repeat(64) };
const cleaned = { ...revoked, state: "cleaned", sequence: 3, previousRecordSha256: revoked.recordSha256, recordSha256: "d".repeat(64) };

test("prepared history requires exact host attribution and disabled non-callable linked records", () => {
  const response = { attributedTo: "NeoBabylon.Host", toolId: "sample-tool", history: [prepared, revoked, cleaned] };
  assert.deepEqual(parsePreparedBindingHistory(response, "sample-tool"), response.history);
  assert.equal(parsePreparedBindingHistory({ ...response, history: [{ ...prepared, note: "Host\u2028accepted" }] }, "sample-tool")[0].state, "prepared-disabled");
  assert.throws(() => parsePreparedBindingHistory({ ...response, attributedTo: "other" }, "sample-tool"), /attribution/i);
  assert.throws(() => parsePreparedBindingHistory({ ...response, history: [{ ...prepared, callableRoute: "mcp" }] }, "sample-tool"), /disabled|callable/i);
  assert.throws(() => parsePreparedBindingHistory({ ...response, history: [prepared, { ...revoked, previousRecordSha256: "wrong" }] }, "sample-tool"), /linked|history/i);
});

test("recovery tool IDs are host-attributed, bounded, unique, and valid independently of candidates", () => {
  assert.deepEqual(parsePreparedBindingToolIds({ attributedTo: "NeoBabylon.Host", toolIds: ["alpha", "old-binding"] }), ["alpha", "old-binding"]);
  assert.throws(() => parsePreparedBindingToolIds({ attributedTo: "other", toolIds: [] }), /attribution/i);
  assert.throws(() => parsePreparedBindingToolIds({ attributedTo: "NeoBabylon.Host", toolIds: ["alpha", "alpha"] }), /duplicate/i);
  assert.throws(() => parsePreparedBindingToolIds({ attributedTo: "NeoBabylon.Host", toolIds: ["../escape"] }), /invalid/i);
  assert.throws(() => parsePreparedBindingToolIds({ attributedTo: "NeoBabylon.Host", toolIds: ["con"] }), /invalid/i);
  assert.throws(() => parsePreparedBindingToolIds({ attributedTo: "NeoBabylon.Host", toolIds: Array.from({ length: 513 }, (_, index) => `tool-${index}`) }), /bounded/i);
});

test("prepare requires current review; revoke and cleanup bind to the latest record even after content changes", () => {
  assert.deepEqual(bindingActionContext("prepare", reviewed, [], request.contentIdentity), { contentIdentity: request.contentIdentity, reviewIdentity: reviewed.reviewIdentity });
  assert.equal(bindingActionContext("prepare", { ...reviewed, decision: "rejected" }, [], request.contentIdentity), null);
  assert.equal(bindingActionContext("prepare", reviewed, [prepared], request.contentIdentity), null);
  assert.deepEqual(bindingActionContext("revoke", null, [prepared], "changed-content"), { contentIdentity: prepared.candidateContentIdentity, reviewIdentity: prepared.reviewIdentity, currentRecordSha256: prepared.recordSha256 });
  assert.deepEqual(bindingActionContext("cleanup", null, [prepared, revoked], "changed-content"), { contentIdentity: revoked.candidateContentIdentity, reviewIdentity: revoked.reviewIdentity, currentRecordSha256: revoked.recordSha256 });
  assert.equal(bindingActionContext("cleanup", null, [prepared], request.contentIdentity), null);
  assert.equal(bindingActionContext("revoke", null, [prepared, revoked], request.contentIdentity), null);
});

test("transition outcome needs the exact returned record as current history", () => {
  const response = { attributedTo: "NeoBabylon.Host", record: revoked };
  const context = bindingActionContext("revoke", null, [prepared], request.contentIdentity);
  assert.equal(confirmBindingTransition(response, [prepared, revoked], "revoke", "sample-tool", context, prepared), revoked);
  assert.throws(() => confirmBindingTransition(response, [prepared], "revoke", "sample-tool", context, prepared), /confirm/i);
  assert.throws(() => confirmBindingTransition({ ...response, record: { ...revoked, callableRoute: "mcp" } }, [prepared, revoked], "revoke", "sample-tool", context, prepared), /disabled|callable/i);
});

test("Stage disabled targets only the displayed reviewed content and latest prepared-disabled hash", () => {
  const expected = { contentIdentity: request.contentIdentity, reviewIdentity: reviewed.reviewIdentity, currentRecordSha256: prepared.recordSha256 };
  assert.deepEqual(stageBindingContext(reviewed, [prepared], request.contentIdentity), expected);
  assert.equal(stageBindingContext(reviewed, [prepared], "changed-content"), null);
  assert.equal(stageBindingContext({ ...reviewed, reviewIdentity: "other" }, [prepared], request.contentIdentity), null);
  assert.equal(stageBindingContext({ ...reviewed, recordSha256: "e".repeat(64) }, [prepared], request.contentIdentity), null);
  assert.equal(stageBindingContext({ ...reviewed, decision: "rejected" }, [prepared], request.contentIdentity), null);
  assert.equal(stageBindingContext(reviewed, [prepared, revoked], request.contentIdentity), null);
  assert.equal(stageBindingContext(reviewed, null, request.contentIdentity), null);
});

test("Stage disabled response retains Unknown runtime confirmation and exact hash", () => {
  const context = stageBindingContext(reviewed, [prepared], request.contentIdentity);
  const response = { attributedTo: "NeoBabylon.Host", state: "staged-disabled", toolId: "sample-tool", currentRecordSha256: prepared.recordSha256, runtimeConfirmation: "Unknown" };
  assert.deepEqual(confirmDisabledStageResponse(response, "sample-tool", context), { state: "staged-disabled", currentRecordSha256: prepared.recordSha256, runtimeConfirmation: "Unknown" });
  assert.throws(() => confirmDisabledStageResponse({ ...response, currentRecordSha256: "c".repeat(64) }, "sample-tool", context), /identity|hash/i);
  assert.throws(() => confirmDisabledStageResponse({ ...response, runtimeConfirmation: "Confirmed" }, "sample-tool", context), /runtime|confirmation/i);
  assert.throws(() => confirmDisabledStageResponse({ ...response, state: "active" }, "sample-tool", context), /stage|disabled/i);
  assert.throws(() => confirmDisabledStageResponse({ ...response, attributedTo: "other" }, "sample-tool", context), /attribution/i);
});

test("Stage recovery requires fresh attributed candidate and exact reviewed prepared identity", () => {
  const target = { toolId: "sample-tool", contentIdentity: request.contentIdentity, reviewIdentity: reviewed.reviewIdentity, currentRecordSha256: prepared.recordSha256 };
  const candidate = { toolId: target.toolId, contentIdentity: target.contentIdentity, state: "unapproved", contract: { invocation: "echo_reviewed", inputSchema: "{}", outputSchema: "{}" } };
  const listing = { attributedTo: "NeoBabylon.Host", candidates: [candidate] };
  const reviews = { attributedTo: "NeoBabylon.Host", toolId: target.toolId, history: [{ ...reviewed, note: "Reviewed", recordedAtUtc: "2026-09-27T00:00:00Z", sequence: 1 }] };
  const bindings = { attributedTo: "NeoBabylon.Host", toolId: target.toolId, history: [prepared] };
  assert.deepEqual(verifyStageRecovery(listing, reviews, bindings, target), { reviewHistory: reviews.history, bindingHistory: bindings.history });
  const other = { ...candidate, toolId: "other-tool", contentIdentity: "candidate-v1:sha256:other" };
  assert.deepEqual(verifyStageRecovery({ ...listing, candidates: [other, candidate] }, reviews, bindings, target), { reviewHistory: reviews.history, bindingHistory: bindings.history });
  assert.deepEqual(verifyStageRecovery({ ...listing, candidates: [candidate, other] }, reviews, bindings, target), { reviewHistory: reviews.history, bindingHistory: bindings.history });
  assert.throws(() => verifyStageRecovery({ ...listing, attributedTo: "other" }, reviews, bindings, target), /attribution/i);
  assert.throws(() => verifyStageRecovery({ ...listing, candidates: [{ ...candidate, contentIdentity: "changed" }] }, reviews, bindings, target), /candidate|identity/i);
  assert.throws(() => verifyStageRecovery({ ...listing, candidates: [candidate, candidate] }, reviews, bindings, target), /candidate|duplicate/i);
  assert.throws(() => verifyStageRecovery({ ...listing, candidates: Array.from({ length: 65 }, (_, index) => ({ ...candidate, toolId: `tool-${index}` })) }, reviews, bindings, target), /candidate|malformed/i);
  assert.throws(() => verifyStageRecovery(listing, { ...reviews, history: [{ ...reviews.history[0], decision: "rejected" }] }, bindings, target), /review|identity/i);
  assert.throws(() => verifyStageRecovery(listing, reviews, { ...bindings, history: [prepared, revoked] }, target), /binding|identity/i);
  assert.throws(() => verifyStageRecovery(listing, reviews, bindings, { ...target, currentRecordSha256: "f".repeat(64) }), /binding|identity/i);
});

test("Stage failure recovery UI revalidates three named reads before clearing the parent-owned block", () => {
  const source = readFileSync(new URL("../src/App.tsx", import.meta.url), "utf8");
  const drawer = source.slice(source.indexOf("function UnapprovedToolsDrawer("), source.indexOf("function DiagnosticsDrawer("));
  const recovery = drawer.slice(drawer.indexOf("async function revalidateAfterStageFailure()"), drawer.indexOf("return <div className=\"drawer-backdrop\""));
  assert.match(recovery, /requestCandidateHost\("listGeneratedToolCandidates"\)/);
  assert.match(recovery, /requestCandidateHost\("listGeneratedToolReviewHistory", \{ toolId: target\.toolId \}\)/);
  assert.match(recovery, /requestCandidateHost\("listGeneratedToolPreparedBindingHistory", \{ toolId: target\.toolId \}\)/);
  assert.match(recovery, /verifyStageRecovery\(listing, reviews, bindings, target\)/);
  assert.ok(recovery.indexOf("verifyStageRecovery(listing, reviews, bindings, target)") < recovery.indexOf("onStageRecovered()"));
  assert.match(recovery, /setStageRecoveryError\(/);
  assert.match(source, /Previous Stage outcome remains Unknown/);
});

test("Stage Unknown survives candidate reorder, card remount, drawer reload, and close", () => {
  const source = readFileSync(new URL("../src/App.tsx", import.meta.url), "utf8");
  const owner = source.slice(source.indexOf("export default function App("), source.indexOf("function CandidateCard("));
  const card = source.slice(source.indexOf("function CandidateCard("), source.indexOf("function BindingRecoverySection("));
  const drawer = source.slice(source.indexOf("function UnapprovedToolsDrawer("), source.indexOf("function DiagnosticsDrawer("));
  assert.match(owner, /const \[unresolvedStage, setUnresolvedStage\] = useState/);
  assert.match(card, /onStageFailure\(\{ toolId, \.\.\.context \}/);
  assert.doesNotMatch(card, /useState<.*StageBindingContext/);
  assert.match(drawer, /key=\{`\$\{textValue\(field\(candidate, "toolId"\)\)\}-\$\{textValue\(field\(candidate, "contentIdentity"\)\)\}`\}/);
  assert.doesNotMatch(drawer, /candidates\.map\(\(candidate, index\)/);
  assert.match(drawer, /unresolvedStage=\{Boolean\(unresolvedStage\)\}/);
  assert.match(drawer, /\{unresolvedStage && <section/);
  assert.ok(drawer.indexOf("{unresolvedStage && <section") < drawer.indexOf("{error &&"), "Unknown warning remains visible when listing fails");
  assert.match(drawer, /!stageRecoveryInFlightRef\.current\) onReload\(\)/);
  assert.match(card, /const canStage = Boolean\([^;]*!unresolvedStage/);
  assert.match(card, /const bindingActionReady = Boolean\([^;]*!unresolvedStage/);
  const reload = owner.slice(owner.indexOf("async function loadUnapprovedTools()"), owner.indexOf("function toggleAppearance()"));
  assert.doesNotMatch(reload, /setUnresolvedStage\(/);
  assert.match(owner, /onStageRecovered=\{\(\) => \{ setUnresolvedStage\(null\)/);
});

test("Stage disabled UI uses the queued named operation with separate confirmation and no activation control", () => {
  const source = readFileSync(new URL("../src/App.tsx", import.meta.url), "utf8");
  const card = source.slice(source.indexOf("function CandidateCard("), source.indexOf("function BindingRecoverySection("));
  assert.match(card, /requestCandidateHost\("stageGeneratedToolDisabledMcp", \{ toolId, \.\.\.context \}\)/);
  assert.match(card, /stageBindingContext\(currentDecision, bindingHistory, contentIdentity\)/);
  assert.match(card, /confirmDisabledStageResponse\(response, toolId, context\)/);
  assert.match(card, /app-root Adapters binary/i);
  assert.match(card, /no active App Server task/i);
  assert.match(card, /Runtime confirmation:.*stageResult\.runtimeConfirmation/);
  assert.doesNotMatch(card, /activateGeneratedTool|Activate this tool/);
});

test("binding lifecycle source uses only named queued operations and has no activation control", () => {
  const source = readFileSync(new URL("../src/App.tsx", import.meta.url), "utf8");
  const card = source.slice(source.indexOf("function CandidateCard("), source.indexOf("function UnapprovedToolsDrawer("));
  assert.match(card, /requestCandidateHost\("listGeneratedToolPreparedBindingHistory", \{ toolId \}\)/);
  for (const operation of ["prepareGeneratedToolDisabledBinding", "revokeGeneratedToolPreparedBinding", "cleanupGeneratedToolPreparedBinding"]) assert.ok(card.includes(`"${operation}"`));
  assert.match(card, /requestCandidateHost\(operation, \{ toolId, \.\.\.context, note: bindingNote \}\)/);
  assert.match(card, /bindingActionContext\("prepare", currentDecision, bindingHistory, contentIdentity\)/);
  assert.match(card, /confirmBindingTransition\(response, records, action, toolId, context, previous\)/);
  assert.match(card, /disabled=\{!canPrepare\}/);
  assert.match(card, /disabled=\{!canRevoke\}/);
  assert.match(card, /disabled=\{!canCleanup\}/);
  assert.doesNotMatch(card, /activateGeneratedTool|Activate this tool/);
});

test("recovery section stays outside candidate listing and exposes only hash-bound denial actions", () => {
  const source = readFileSync(new URL("../src/App.tsx", import.meta.url), "utf8");
  const recovery = source.slice(source.indexOf("function BindingRecoverySection("), source.indexOf("function UnapprovedToolsDrawer("));
  const drawer = source.slice(source.indexOf("function UnapprovedToolsDrawer("), source.indexOf("function DiagnosticsDrawer("));
  assert.match(drawer, /<BindingRecoverySection /);
  assert.ok(drawer.indexOf("<BindingRecoverySection ") > drawer.indexOf("candidates.map("));
  assert.match(recovery, /requestCandidateHost\("listGeneratedToolPreparedBindingToolIds"\)/);
  assert.match(recovery, /requestCandidateHost\("listGeneratedToolPreparedBindingHistory", \{ toolId \}\)/);
  assert.match(recovery, /currentRecordSha256/);
  assert.match(recovery, /confirmBindingTransition\(response, records, action, toolId, context, previous\)/);
  assert.doesNotMatch(recovery, /prepareGeneratedToolDisabledBinding|activateGeneratedTool|Prepare disabled binding|Activate this tool/);
});
