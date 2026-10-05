import test from "node:test";
import assert from "node:assert/strict";
import { createTaskTelemetry, reduceTaskTelemetry, contextMetrics, rateHistory, createTelemetryFramePublisher, normalizeTurnMeasurementOutcome } from "../src/task-telemetry.mjs";

const identity = { workspace: "D:/fixture", threadId: "thread-a", providerId: "provider-a", modelIdentifier: "model-a" };
const counts = (totalTokens, outputTokens) => ({ totalTokens, inputTokens: totalTokens - outputTokens, cachedInputTokens: 20, cacheWriteInputTokens: null, outputTokens, reasoningOutputTokens: 5 });
const usage = (totalTokens = 1000, outputTokens = 100, lastTokens = 200, window = 1000) => ({ total: counts(totalTokens, outputTokens), last: counts(lastTokens, 10), modelContextWindow: window });
const snapshot = (state, tokenUsage, extra = {}) => ({ type: "snapshot", epoch: state.epoch, revision: state.revision, result: {
  attributedTo: "NeoBabylon.Host", threadId: "thread-a", tokenUsage,
  evidenceSource: "Codex journal fixture", observedAtUtc: "2026-10-04T09:00:00Z", reasoningEffort: null, reasoningEvidenceSource: null,
  usageModelProvider: "provider-a", usageModelIdentifier: "model-a", usageTurnId: null, ...extra,
} });
const hydrate = () => {
  const state = createTaskTelemetry(identity);
  return reduceTaskTelemetry(state, snapshot(state, usage()));
};
const begin = (state, requestId = "request-1", nowMs = 1000) => reduceTaskTelemetry(state, { type: "begin", requestId, nowMs });
const started = (state, requestId = "request-1", turnId = "turn-1") => reduceTaskTelemetry(state, { type: "notification", event: {
  requestId, method: "turn/started", params: { threadId: "thread-a", turn: { id: turnId } },
} });
const live = (state, tokenUsage, extra = {}) => reduceTaskTelemetry(state, { type: "notification", event: {
  requestId: "request-1", method: "thread/tokenUsage/updated", params: { threadId: "thread-a", turnId: "turn-1", tokenUsage }, ...extra,
} });

test("context uses last.totalTokens, never cumulative spend or reasoning twice", () => {
  const state = hydrate();
  assert.deepEqual(contextMetrics(state, {}).usedTokens, 200);
  assert.equal(contextMetrics(state, {}).percent, 20);
  assert.equal(state.usage.total.totalTokens, 1000);
  assert.equal(state.usage.total.outputTokens, 100);
  assert.equal(state.usage.total.cacheWriteInputTokens, null);
  assert.equal(state.providerCost, null);
});

test("missing context/window/cost/cache remain Unknown, with explicit sourced window fallback", () => {
  const state = createTaskTelemetry(identity);
  assert.equal(contextMetrics(state, {}).percent, null);
  const noWindow = reduceTaskTelemetry(state, snapshot(state, usage(1000, 100, 200, null)));
  assert.equal(contextMetrics(noWindow, {}).windowTokens, null);
  const capability = { contextWindowEffective: { state: "Unknown", value: 999 }, contextWindowAdvertised: { state: "Known", value: 400, evidenceSource: "model card" }, pricing: 0 };
  assert.equal(contextMetrics(noWindow, capability).percent, 50);
  assert.match(contextMetrics(noWindow, capability).windowSource, /advertised/);
  assert.equal(noWindow.providerCost, null);
  capability.contextWindowEffective = { state: "Known", value: 500, evidenceSource: "effective fixture" };
  assert.equal(contextMetrics(noWindow, capability).windowTokens, 500);
  assert.match(contextMetrics(noWindow, capability).windowSource, /effective/);
  assert.equal(contextMetrics(hydrate(), capability).windowTokens, 1000);
  assert.match(contextMetrics(hydrate(), capability).windowSource, /Codex/);
});

test("wrong thread, request, turn, stale snapshot epoch/revision or unattributed host data cannot update usage", () => {
  let state = started(begin(hydrate()));
  for (const patch of [
    { requestId: "wrong" }, { params: { threadId: "wrong", turnId: "turn-1", tokenUsage: usage(2000) } },
    { params: { threadId: "thread-a", turnId: "wrong", tokenUsage: usage(2000) } },
  ]) assert.strictEqual(live(state, usage(2000), patch), state);
  for (const extra of [{ attributedTo: "provider" }, { threadId: "other" }]) assert.strictEqual(reduceTaskTelemetry(state, snapshot(state, usage(2000), extra)), state);
  assert.strictEqual(reduceTaskTelemetry(state, { ...snapshot(state, usage(2000)), epoch: -1 }), state);
  const stale = snapshot(state, usage(1100, 150));
  state = live(state, usage(1200, 160));
  assert.strictEqual(reduceTaskTelemetry(state, stale), state);
});

test("repeated cumulative events replace counters without adding them or rolling back newer output", () => {
  let state = started(begin(hydrate()));
  state = live(state, usage(1200, 160));
  state = live(state, usage(1200, 160));
  assert.equal(state.usage.total.totalTokens, 1200);
  assert.equal(state.usage.total.outputTokens, 160);
  assert.strictEqual(live(state, usage(1100, 150)), state);
});

test("rates require measured completed turns and count actual output deltas, including waits/tools", () => {
  let state = started(begin(hydrate()));
  state = live(state, usage(1200, 160));
  state = reduceTaskTelemetry(state, { type: "complete", requestId: "request-1", threadId: "thread-a", turnId: "turn-1", outcome: "completed", nowMs: 11000 });
  assert.equal(state.turnAverage, null);
  state = reduceTaskTelemetry(state, { ...snapshot(state, usage(1200, 160), { usageTurnId: "turn-1" }), finalForRequestId: "request-1" });
  assert.equal(state.turnAverage, 6);
  assert.equal(state.weightedAverage, 6);
  assert.equal(state.measuredTurns, 1);
  state = reduceTaskTelemetry(state, { ...snapshot(state, usage(1200, 160), { usageTurnId: "turn-1" }), finalForRequestId: "request-1" });
  assert.equal(state.measuredTurns, 1);
  state = started(begin(state, "request-2", 12000), "request-2", "turn-2");
  state = reduceTaskTelemetry(state, { type: "complete", requestId: "request-2", threadId: "thread-a", turnId: "turn-2", outcome: "completed", nowMs: 32000 });
  state = reduceTaskTelemetry(state, { ...snapshot(state, usage(1600, 400), { usageTurnId: "turn-2" }), finalForRequestId: "request-2" });
  assert.equal(state.turnAverage, 12);
  assert.equal(state.weightedAverage, 10);
});

test("a captured completed live turn is measurable without a race-prone follow-up snapshot", () => {
  let state = started(begin(hydrate(), "request-1", 1000));
  state = reduceTaskTelemetry(state, { type: "notification", nowMs: 5000, event: {
    requestId: "request-1", method: "thread/tokenUsage/updated", params: { threadId: "thread-a", turnId: "turn-1", tokenUsage: usage(1200, 160) },
  } });
  assert.equal(state.turnAverage, 15);
  state = reduceTaskTelemetry(state, { type: "complete", requestId: "request-1", threadId: "thread-a", turnId: "turn-1", outcome: "completed", nowMs: 11000 });
  assert.equal(state.turnAverage, 6);
  assert.equal(state.weightedAverage, 6);
  assert.equal(state.measuredTurns, 1);
  assert.equal(state.samples.at(-1).rate, 6);
});

test("a first turn without a cumulative baseline needs two timed observations", () => {
  let state = started(begin(createTaskTelemetry(identity), "request-1", 1000));
  state = reduceTaskTelemetry(state, { type: "notification", nowMs: 3000, event: {
    requestId: "request-1", method: "thread/tokenUsage/updated", params: { threadId: "thread-a", turnId: "turn-1", tokenUsage: usage(1100, 100) },
  } });
  assert.equal(state.turnAverage, null);
  state = reduceTaskTelemetry(state, { type: "notification", nowMs: 5000, event: {
    requestId: "request-1", method: "thread/tokenUsage/updated", params: { threadId: "thread-a", turnId: "turn-1", tokenUsage: usage(1200, 160) },
  } });
  assert.equal(state.turnAverage, 30);
  state = reduceTaskTelemetry(state, { type: "complete", requestId: "request-1", threadId: "thread-a", turnId: "turn-1", outcome: "completed", nowMs: 6000 });
  assert.equal(state.measuredTurns, 1);
  assert.equal(state.turnAverage, 20);
});

test("completed model output remains measurable when a tool failed after turn completion", () => {
  const result = { terminal: true, turnStatus: "completed", eventType: "turnCompletedWithToolFailure" };
  assert.equal(normalizeTurnMeasurementOutcome(result), "completed");
  let state = started(begin(hydrate()));
  state = reduceTaskTelemetry(state, { type: "complete", requestId: "request-1", threadId: "thread-a", turnId: "turn-1",
    outcome: normalizeTurnMeasurementOutcome(result), nowMs: 11000 });
  state = reduceTaskTelemetry(state, { ...snapshot(state, usage(1200, 160), {
    usageTurnId: "turn-1", rateEvidenceVersion: 1, rateEvidenceSource: "journal v1",
    rateMeasurements: [{ turnId: "turn-1", modelIdentifier: "model-a", providerId: "provider-a", outputTokens: 60, durationMs: 10000 }],
  }), finalForRequestId: "request-1" });
  assert.equal(state.measuredTurns, 1);
  assert.equal(state.turnAverage, 6);
});

test("actual model failures and unconfirmed turns remain excluded regardless of UI workflow outcome", () => {
  for (const result of [
    { terminal: true, turnStatus: "failed", eventType: "turnFailure" },
    { terminal: true, turnStatus: "interrupted", eventType: "turnInterrupted" },
    { terminal: false, turnStatus: null, eventType: "turnFailure" },
  ]) {
    assert.notEqual(normalizeTurnMeasurementOutcome(result), "completed");
  }
  const uiWorkflowSucceeded = { terminal: true, turnStatus: "failed", eventType: "turnFailure" };
  assert.equal(normalizeTurnMeasurementOutcome(uiWorkflowSucceeded), "failed");
});

test("versioned journal measurements restore, deduplicate repeated snapshots, and stay model-attributed", () => {
  const first = { turnId: "restored-1", modelIdentifier: "model-a", providerId: null, outputTokens: 18376, durationMs: 281900, source: "Codex completed-turn journal v1" };
  const second = { turnId: "restored-2", modelIdentifier: "model-a", providerId: "provider-a", outputTokens: 100, durationMs: 10000, source: "Codex completed-turn journal v1" };
  const wrongModel = { turnId: "other-model", modelIdentifier: "model-b", providerId: "provider-a", outputTokens: 900, durationMs: 1000, source: "Codex completed-turn journal v1" };
  let state = createTaskTelemetry(identity);
  const restored = () => snapshot(state, usage(98471, 18376), { rateEvidenceVersion: 1, rateEvidenceSource: "Codex completed-turn journal v1", rateMeasurements: [first, second, wrongModel] });
  state = reduceTaskTelemetry(state, restored());
  state = reduceTaskTelemetry(state, restored());
  assert.equal(state.turnAverage, 10);
  assert.equal(state.weightedAverage, 18476 * 1000 / 291900);
  assert.equal(state.measuredTurns, 2);
  assert.equal(state.measurements.filter((measurement) => measurement.turnId === "restored-1").length, 1);
  assert.equal(state.measurements.find((measurement) => measurement.turnId === "restored-1").providerId, null);
  const switched = createTaskTelemetry({ ...identity, modelIdentifier: "model-b" }, { epoch: state.epoch + 1 });
  const modelB = reduceTaskTelemetry(switched, { ...restored(), epoch: switched.epoch, revision: switched.revision });
  assert.equal(modelB.measuredTurns, 1);
  assert.equal(modelB.weightedAverage, 900);
});

test("out-of-order and duplicate timed notifications cannot roll back a live rate", () => {
  let state = started(begin(hydrate(), "request-1", 1000));
  const notify = (atMs, output) => reduceTaskTelemetry(state, { type: "notification", nowMs: atMs, event: {
    requestId: "request-1", method: "thread/tokenUsage/updated", params: { threadId: "thread-a", turnId: "turn-1", tokenUsage: usage(1000 + output - 100, output) },
  } });
  state = notify(5000, 160);
  const measured = state.turnAverage;
  assert.strictEqual(notify(4000, 150), state);
  assert.strictEqual(notify(6000, 160), state);
  assert.equal(state.turnAverage, measured);
});

test("missing baseline, invalid elapsed time, unconfirmed or failed outcomes produce gaps, not synthetic zero rates", () => {
  for (const fixture of [
    { initial: createTaskTelemetry(identity), end: 11000, outcome: "completed" },
    { initial: hydrate(), end: 1000, outcome: "completed" },
    { initial: hydrate(), end: 11000, outcome: "failed" },
  ]) {
    let state = started(begin(fixture.initial));
    state = reduceTaskTelemetry(state, { type: "complete", requestId: "request-1", threadId: "thread-a", turnId: "turn-1", outcome: fixture.outcome, nowMs: fixture.end });
    state = reduceTaskTelemetry(state, { ...snapshot(state, usage(1200, 160)), finalForRequestId: "request-1" });
    assert.equal(state.turnAverage, null);
    assert.equal(state.weightedAverage, null);
    assert.equal(state.samples[0].rate, null);
  }
  const state = started(begin(hydrate()));
  assert.strictEqual(reduceTaskTelemetry(state, { type: "complete", requestId: "wrong", threadId: "thread-a", turnId: "turn-1", outcome: "completed", nowMs: 11000 }), state);
});

test("a same-model but uncorrelated final journal snapshot cannot invent a completed-turn rate", () => {
  for (const usageTurnId of [null, "old-turn"]) {
    let state = started(begin(hydrate()));
    state = reduceTaskTelemetry(state, { type: "complete", requestId: "request-1", threadId: "thread-a", turnId: "turn-1", outcome: "completed", nowMs: 11000 });
    state = reduceTaskTelemetry(state, { ...snapshot(state, usage(), { usageTurnId }), finalForRequestId: "request-1" });
    assert.equal(state.turnAverage, null);
    assert.equal(state.weightedAverage, null);
  }
});

test("compaction preserves spend but identical old journal observations never refill current context", () => {
  let state = hydrate();
  state = reduceTaskTelemetry(state, { type: "invalidateContext", observedAfterUtcMs: Date.parse("2026-10-04T09:01:00Z") });
  assert.equal(state.usage.total.totalTokens, 1000);
  assert.equal(contextMetrics(state, {}).usedTokens, null);
  state = reduceTaskTelemetry(state, snapshot(state, usage()));
  assert.equal(contextMetrics(state, {}).usedTokens, null);
  state = reduceTaskTelemetry(state, snapshot(state, usage(), { observedAtUtc: "2026-10-04T09:01:01Z" }));
  assert.equal(contextMetrics(state, {}).usedTokens, 200);
  state = reduceTaskTelemetry(state, { type: "invalidateContext" });
  state = reduceTaskTelemetry(state, snapshot(state, usage(1200, 160, 50), { observedAtUtc: "2026-10-04T09:01:01Z" }));
  assert.equal(contextMetrics(state, {}).usedTokens, 50);
});

test("a fresh correlated usage event can restore context after compaction; unknown snapshots cannot", () => {
  let state = reduceTaskTelemetry(hydrate(), { type: "invalidateContext" });
  state = reduceTaskTelemetry(state, snapshot(state, null));
  assert.equal(state.usage.total.totalTokens, 1000);
  assert.equal(contextMetrics(state, {}).usedTokens, null);
  state = started(begin(state));
  state = live(state, usage());
  assert.equal(contextMetrics(state, {}).usedTokens, 200);
});

test("fresh request/thread/turn-correlated compaction usage survives final invalidation without entering rate averages", () => {
  let state = reduceTaskTelemetry(hydrate(), { type: "beginCompaction", requestId: "compact-1" });
  const event = { requestId: "compact-1", method: "turn/started", params: { threadId: "thread-a", turn: { id: "compact-turn" } } };
  state = reduceTaskTelemetry(state, { type: "notification", event });
  const updated = { requestId: "compact-1", method: "thread/tokenUsage/updated", params: { threadId: "thread-a", turnId: "compact-turn", tokenUsage: usage(1200, 160, 40) } };
  assert.strictEqual(reduceTaskTelemetry(state, { type: "notification", event: { ...updated, requestId: "wrong" } }), state);
  assert.strictEqual(reduceTaskTelemetry(state, { type: "notification", event: { ...updated, params: { ...updated.params, turnId: "old-turn" } } }), state);
  state = reduceTaskTelemetry(state, { type: "notification", event: updated });
  state = reduceTaskTelemetry(state, { type: "finishCompaction", requestId: "compact-1", confirmed: true, turnId: "compact-turn", observedAfterUtcMs: Date.parse("2026-10-04T09:01:00Z") });
  assert.equal(state.currentUsage.last.totalTokens, 40);
  assert.equal(state.usage.total.totalTokens, 1200);
  assert.equal(state.measuredTurns, 0);
  assert.equal(state.turnAverage, null);
  assert.deepEqual(state.samples, []);
  const refreshed = reduceTaskTelemetry(state, snapshot(state, usage(1200, 160, 40), { usageModelProvider: null, usageTurnId: "compact-turn" }));
  assert.equal(refreshed.currentUsage.last.totalTokens, 40);
  const withoutLive = reduceTaskTelemetry(reduceTaskTelemetry(hydrate(), { type: "beginCompaction", requestId: "compact-2" }), {
    type: "finishCompaction", requestId: "compact-2", confirmed: true, turnId: null,
  });
  assert.equal(reduceTaskTelemetry(withoutLive, snapshot(withoutLive, usage())).currentUsage, null);
});

test("identity/resume resets measured rates and observed reasoning; a model switch fences old context", () => {
  const old = hydrate();
  const state = createTaskTelemetry({ ...identity, modelIdentifier: "model-b" }, { epoch: old.epoch + 1, fenceFrom: old });
  assert.equal(state.usage.total.totalTokens, old.usage.total.totalTokens);
  assert.equal(state.weightedAverage, null);
  assert.deepEqual(state.samples, []);
  assert.equal(state.observedReasoning, null);
  const refreshed = reduceTaskTelemetry(state, snapshot(state, usage()));
  assert.equal(refreshed.usage.total.totalTokens, 1000);
  assert.equal(contextMetrics(refreshed, {}).usedTokens, null);
});

test("nullable journal provider metadata needs a captured matching turn, never tuple inference", () => {
  let state = hydrate();
  state = reduceTaskTelemetry(state, snapshot(state, usage(), { usageModelProvider: null }));
  assert.equal(contextMetrics(state, {}).usedTokens, null);
  state = started(begin(state));
  state = reduceTaskTelemetry(state, { type: "complete", requestId: "request-1", threadId: "thread-a", turnId: "turn-1", outcome: "completed", nowMs: 11000 });
  for (const extra of [{ usageTurnId: "old-turn" }, { usageTurnId: "turn-1", usageModelIdentifier: "wrong-model" }]) {
    const rejected = reduceTaskTelemetry(state, { ...snapshot(state, usage(1200, 160), { usageModelProvider: null, ...extra }), finalForRequestId: "request-1" });
    assert.equal(rejected.currentUsage, null);
    assert.equal(rejected.turnAverage, null);
  }
  const accepted = reduceTaskTelemetry(state, { ...snapshot(state, usage(1200, 160), {
    usageModelProvider: null, usageTurnId: "turn-1", reasoningEffort: "high", reasoningEvidenceSource: "Codex turn journal",
  }), finalForRequestId: "request-1" });
  assert.equal(accepted.currentUsage.last.totalTokens, 200);
  assert.equal(accepted.turnAverage, 6);
  assert.equal(accepted.observedReasoning, "high");
});

test("a prior observation of the newly selected model cannot roll back lifetime spend or bypass the model-switch fence", () => {
  const old = hydrate();
  const switched = createTaskTelemetry({ ...identity, modelIdentifier: "model-b" }, { epoch: 2, fenceFrom: old });
  const stale = reduceTaskTelemetry(switched, snapshot(switched, usage(900, 90), {
    usageModelIdentifier: "model-b", observedAtUtc: "2026-10-04T08:00:00Z",
  }));
  assert.equal(stale.usage.total.totalTokens, 1000);
  assert.equal(stale.currentUsage, null);
});

test("observed reasoning requires its own runtime source and is never inferred from requested effort", () => {
  const state = createTaskTelemetry(identity);
  assert.equal(reduceTaskTelemetry(state, snapshot(state, usage(), { reasoningEffort: "high" })).observedReasoning, null);
  const observed = reduceTaskTelemetry(state, snapshot(state, usage(), { reasoningEffort: "high", reasoningEvidenceSource: "Codex turn journal" }));
  assert.equal(observed.observedReasoning, "high");
  assert.equal(observed.reasoningSource, "Codex turn journal");
});

test("same-thread old-model journal evidence cannot acquire the new tuple's context or observed reasoning", () => {
  const old = hydrate();
  let state = createTaskTelemetry({ ...identity, modelIdentifier: "model-b" }, { epoch: 2, fenceFrom: old });
  state = reduceTaskTelemetry(state, snapshot(state, usage(1200, 160), {
    observedAtUtc: "2026-10-04T09:00:01Z", reasoningEffort: "high", reasoningEvidenceSource: "old-model turn",
  }));
  assert.equal(state.usage.total.totalTokens, 1200);
  assert.equal(contextMetrics(state, {}).usedTokens, null);
  assert.equal(state.observedReasoning, null);
  state = reduceTaskTelemetry(state, snapshot(state, usage(1300, 170), {
    usageModelIdentifier: null, usageModelProvider: null, observedAtUtc: "2026-10-04T09:00:02Z",
  }));
  assert.equal(contextMetrics(state, {}).usedTokens, null);
  state = reduceTaskTelemetry(state, snapshot(state, usage(1400, 180, 100), {
    usageModelIdentifier: "model-b", usageModelProvider: "provider-a", usageTurnId: "new-model-turn", observedAtUtc: "2026-10-04T09:00:03Z",
  }));
  assert.equal(contextMetrics(state, {}).usedTokens, 100);
});

test("90-second history is bounded and keeps unavailable samples as gaps", () => {
  const samples = [{ atMs: 9999, rate: 5 }, { atMs: 10000, rate: null }, { atMs: 100000, rate: 8 }, { atMs: 100001, rate: 9 }];
  assert.deepEqual(rateHistory(samples, 100000), [{ atMs: 10000, rate: null }, { atMs: 100000, rate: 8 }]);
  assert.equal(rateHistory(Array.from({ length: 300 }, (_, atMs) => ({ atMs, rate: 1 })), 300).length, 90);
});

test("telemetry frame publishing coalesces frequent updates and cancels pending work", () => {
  let callback; let scheduled = 0; let cancelled = 0; const values = [];
  const publisher = createTelemetryFramePublisher({ schedule: (fn) => { scheduled++; callback = fn; return 1; }, cancel: () => cancelled++, onValue: (value) => values.push(value) });
  for (let index = 0; index < 100; index++) publisher.push(index);
  assert.equal(scheduled, 1);
  callback();
  assert.deepEqual(values, [99]);
  publisher.push(100);
  publisher.clear();
  assert.equal(cancelled, 1);
  assert.deepEqual(values, [99]);
});
