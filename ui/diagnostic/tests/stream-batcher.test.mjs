import assert from "node:assert/strict";
import test from "node:test";
import { createAssistantDeltaBatcher } from "../src/stream-batcher.mjs";

test("many assistant deltas produce one complete display update", () => {
  const timers = [];
  const batches = [];
  const batcher = createAssistantDeltaBatcher({
    schedule: (callback) => { timers.push(callback); return timers.length; },
    cancel: () => {},
    onBatch: (batch) => batches.push(batch),
  });

  for (let index = 0; index < 2500; index++) batcher.push("agent-1", "ab");
  assert.equal(timers.length, 1, "a timer was scheduled for each delta");
  assert.equal(batches.length, 0, "the display updated before the batch interval");
  timers[0]();
  assert.deepEqual(batches, [[{ id: "agent-1", delta: "ab".repeat(2500) }]]);
});

test("a completed authoritative message discards its pending stream text", () => {
  const timers = [];
  const cancelled = [];
  const batches = [];
  const batcher = createAssistantDeltaBatcher({
    schedule: (callback) => { timers.push(callback); return timers.length; },
    cancel: (id) => cancelled.push(id),
    onBatch: (batch) => batches.push(batch),
  });

  batcher.push("agent-1", "partial");
  batcher.discard("agent-1");
  timers[0]();
  assert.deepEqual(batches, []);
  assert.deepEqual(cancelled, [1]);
});

test("failure flushes partial assistant text before clearing the batch", () => {
  const batches = [];
  const batcher = createAssistantDeltaBatcher({
    schedule: () => 1,
    cancel: () => {},
    onBatch: (batch) => batches.push(batch),
  });

  batcher.push("agent-1", "before ");
  batcher.push("agent-1", "failure");
  batcher.flush();
  batcher.clear();
  assert.deepEqual(batches, [[{ id: "agent-1", delta: "before failure" }]]);
});

test("batching keeps the latest explicit truncation metadata with the visible text", () => {
  const timers = [];
  const batches = [];
  const batcher = createAssistantDeltaBatcher({
    schedule: (callback) => { timers.push(callback); return timers.length; },
    cancel: () => {},
    onBatch: (batch) => batches.push(batch),
  });
  batcher.push("agent-1", "visible", { displayTruncated: true, omittedCharacters: 4 });
  batcher.push("agent-1", " text", { displayTruncated: true, omittedCharacters: 9 });
  timers[0]();

  assert.deepEqual(batches, [[{
    id: "agent-1",
    delta: "visible text",
    displayMetadata: { displayTruncated: true, omittedCharacters: 9 },
  }]]);
});
