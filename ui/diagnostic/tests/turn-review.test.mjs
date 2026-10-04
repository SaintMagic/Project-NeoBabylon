import test from "node:test";
import assert from "node:assert/strict";

let review;
try { review = await import("../src/turn-review.mjs"); }
catch (error) { if (error?.code !== "ERR_MODULE_NOT_FOUND") throw error; review = {}; }

const started = { threadId: "thread-A", turn: { id: "turn-1" } };

test("turn review accepts only an exact App Server turn diff and replaces prior revisions", () => {
  assert.equal(typeof review.reduceTurnReview, "function", "turn review reducer is missing");
  let state = review.reduceTurnReview(null, "turn/started", started, "thread-A");
  assert.equal(state?.status, "awaiting");
  state = review.reduceTurnReview(state, "turn/diff/updated", { threadId: "thread-B", turnId: "turn-1", diff: "wrong thread" }, "thread-A");
  state = review.reduceTurnReview(state, "turn/diff/updated", { threadId: "thread-A", turnId: "turn-2", diff: "wrong turn" }, "thread-A");
  assert.equal(state?.status, "awaiting");
  state = review.reduceTurnReview(state, "turn/diff/updated", { threadId: "thread-A", turnId: "turn-1", diff: "-old\n+new" }, "thread-A");
  assert.deepEqual({ status: state.status, diff: state.diff }, { status: "available", diff: "-old\n+new" });
  state = review.reduceTurnReview(state, "turn/diff/updated", { threadId: "thread-A", turnId: "turn-1", diff: "-later\n+latest" }, "thread-A");
  assert.equal(state.diff, "-later\n+latest", "latest aggregate diff must replace, not append to, the earlier revision");
});

test("turn review never labels an invalidated or oversized diff as complete", () => {
  let state = review.reduceTurnReview(null, "turn/started", started, "thread-A");
  state = review.reduceTurnReview(state, "turn/diff/updated", { threadId: "thread-A", turnId: "turn-1", diff: "ok" }, "thread-A", 3);
  state = review.reduceTurnReview(state, "turn/diff/updated", { threadId: "thread-A", turnId: "turn-1", diff: "too long" }, "thread-A", 3);
  assert.deepEqual({ status: state.status, diff: state.diff }, { status: "oversized", diff: null });
  state = review.reduceTurnReview(state, "turn/diff/updated", { threadId: "thread-A", turnId: "turn-1", diff: "" }, "thread-A", 3);
  assert.deepEqual({ status: state.status, diff: state.diff }, { status: "invalidated", diff: null });
});

test("turn review ignores an unstarted or malformed diff and resets on the next exact turn", () => {
  assert.equal(review.reduceTurnReview(null, "turn/diff/updated", { threadId: "thread-A", turnId: "turn-1", diff: "forged" }, "thread-A"), null);
  let state = review.reduceTurnReview(null, "turn/started", started, "thread-A");
  state = review.reduceTurnReview(state, "turn/diff/updated", { threadId: "thread-A", turnId: "turn-1", diff: "ok" }, "thread-A");
  state = review.reduceTurnReview(state, "turn/diff/updated", { threadId: "thread-A", turnId: "turn-1", diff: 123 }, "thread-A");
  assert.equal(state.diff, "ok");
  state = review.reduceTurnReview(state, "turn/started", { threadId: "thread-A", turn: { id: "turn-2" } }, "thread-A");
  assert.deepEqual({ turnId: state.turnId, status: state.status, diff: state.diff }, { turnId: "turn-2", status: "awaiting", diff: null });
});

test("saved patch review reconstructs only the matching runtime item diffs", () => {
  assert.equal(typeof review.restoreSavedTurnReview, "function", "saved patch review restoration is missing");
  const result = review.restoreSavedTurnReview([
    { turnId: "turn-2", source: "savedFileChangeItems", status: "available", changes: [
      { path: "note.txt", kind: "update", diff: "-old\n+new", movePath: null },
    ] },
    { turnId: "turn-1", source: "savedFileChangeItems", status: "available", changes: [
      { path: "older.txt", kind: "add", diff: "+older" },
    ] },
  ], "thread-A");
  assert.equal(result.threadId, "thread-A");
  assert.equal(result.turnId, "turn-2");
  assert.equal(result.source, "savedFileChangeItems");
  assert.match(result.diff, /note\.txt/);
  assert.match(result.diff, /-old\n\+new/);
  assert.doesNotMatch(result.diff, /older/);
});

test("saved patch review never reconstructs a malformed or oversized partial diff", () => {
  const malformed = review.restoreSavedTurnReview([
    { turnId: "turn-2", source: "savedFileChangeItems", status: "available", changes: [{ path: "note.txt", kind: "update", diff: 123 }] },
  ], "thread-A");
  assert.equal(malformed.status, "unavailable");
  assert.equal(malformed.diff, null);
  const oversized = review.restoreSavedTurnReview([
    { turnId: "turn-2", source: "savedFileChangeItems", status: "oversized", changes: null },
  ], "thread-A");
  assert.equal(oversized.status, "oversized");
  assert.equal(oversized.diff, null);
  assert.equal(review.restoreSavedTurnReview([], "thread-A"), null);
  assert.equal(review.restoreSavedTurnReview([{ turnId: "turn-1", source: "unknown", status: "available", changes: [] }], "thread-A"), null);
});
