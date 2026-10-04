import test from "node:test";
import assert from "node:assert/strict";
import { canCompactContext, requestContextCompaction } from "../src/context-compaction.mjs";

const state = { threadId: "thread-a", busy: false, executionEligible: true };
const result = { eventType: "compactionOutcome", attributedTo: "NeoBabylon.Host", threadId: "thread-a", turnId: "compact-turn", outcome: "completed", succeeded: true };

test("compaction is available only for an idle executable existing chat", async () => {
  assert.equal(canCompactContext(state), true);
  for (const patch of [{ threadId: null }, { busy: true }, { executionEligible: false }]) {
    assert.equal(canCompactContext({ ...state, ...patch }), false);
    const outcome = await requestContextCompaction(() => assert.fail("blocked operation must not reach host"), { ...state, ...patch });
    assert.equal(outcome.status, "blocked");
  }
});

test("only a final same-thread attributed outcome confirms compaction; the request is typed and thread scoped", async () => {
  const outcome = await requestContextCompaction(async (operation, payload) => {
    assert.equal(operation, "compactContext");
    assert.deepEqual(payload, { threadId: "thread-a" });
    return result;
  }, state);
  assert.equal(outcome.status, "completed");
  for (const bad of [{}, { ...result, eventType: "queued" }, { ...result, threadId: "other" }, { ...result, threadId: null }, { ...result, attributedTo: "provider" }, { ...result, succeeded: null }]) {
    await assert.rejects(requestContextCompaction(async () => bad, state), /confirm/);
  }
});

test("failed and unknown outcomes remain explicit, and typed host errors are not replaced with success", async () => {
  const failure = await requestContextCompaction(async () => ({ ...result, outcome: "failed", succeeded: false, failure: { attributedTo: "Codex App Server", type: "providerFailure", message: "fixture failure" } }), state);
  assert.equal(failure.status, "failed");
  assert.match(failure.message, /Codex App Server.*providerFailure.*fixture failure/);
  const unknown = await requestContextCompaction(async () => ({ ...result, outcome: "unknown", succeeded: null }), state);
  assert.equal(unknown.status, "unknown");
  await assert.rejects(requestContextCompaction(async () => { throw new Error("NeoBabylon.Host: fixture timeout"); }, state), /fixture timeout/);
});

test("the bridge uses the long turn timeout for compaction and waits for the matching final reply", async () => {
  let listener; const sent = []; const timers = [];
  globalThis.window = { setTimeout: (_callback, ms) => { timers.push(ms); return 1; }, clearTimeout: () => {}, chrome: { webview: {
    addEventListener: (_name, callback) => { listener = callback; }, postMessage: (message) => sent.push(message),
  } } };
  const { requestHost } = await import("../src/bridge.ts");
  let resolved = false;
  const pending = requestContextCompaction((operation, payload) => requestHost(operation, payload, "compact-fixture"), state).then((outcome) => { resolved = true; return outcome; });
  assert.deepEqual(timers, [650000]);
  assert.deepEqual(sent, [{ operation: "compactContext", requestId: "compact-fixture", threadId: "thread-a" }]);
  listener({ data: { stream: true, requestId: "compact-fixture", method: "turn/started", params: { threadId: "thread-a", turn: { id: "compact-turn" } } } });
  listener({ data: { requestId: "wrong-request", ok: true, result } });
  await Promise.resolve();
  assert.equal(resolved, false);
  listener({ data: { requestId: "compact-fixture", ok: true, result } });
  assert.equal((await pending).status, "completed");
});
