import test from "node:test";
import assert from "node:assert/strict";
import { restoreSavedActivities } from "../src/restoration.mjs";

test("restored failed tool preserves structured display text and nonzero exit code", () => {
  const [activity] = restoreSavedActivities([{ itemId: "i", turnId: "t", itemType: "mcpToolCall", outcome: "failed", text: '{"message":"denied"}', exitCode: 7, durationMs: 123 }], "thread");
  assert.equal(activity.status, "failed");
  assert.equal(activity.detail, '{"message":"denied"}');
  assert.equal(activity.exitCode, 7);
  assert.equal(activity.durationMs, 123);
  assert.equal(activity.threadId, "thread");
  assert.equal(activity.turnId, "t");
});
