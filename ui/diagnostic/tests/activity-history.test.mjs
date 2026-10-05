import assert from "node:assert/strict";
import test from "node:test";
import { boundActivityHistory } from "../src/activity-history.mjs";

const activity = (id, detail = "") => ({
  id,
  title: `Tool ${id}`,
  detail,
  status: "succeeded",
});

const retainedCharacters = (entries) => entries.reduce(
  (total, entry) => total + entry.title.length + (entry.detail?.length ?? 0),
  0,
);

test("activity history retains every tool invocation card", () => {
  const result = boundActivityHistory(Array.from({ length: 70 }, (_, index) => activity(`item-${index}`)));

  assert.equal(result.entries.length, 70);
  assert.equal(result.entries[0].id, "item-0");
  assert.equal(result.entries.at(-1).id, "item-69");
  assert.equal(result.truncated, false);
});

test("each activity detail has an explicit bounded preview without dropping its card", () => {
  const result = boundActivityHistory(Array.from(
    { length: 10 },
    (_, index) => activity(`large-${index}`, "x".repeat(50_000)),
  ));

  assert.equal(result.entries.length, 10);
  assert.equal(result.entries.at(-1).id, "large-9");
  assert.equal(retainedCharacters(result.entries), 10 * ("Tool large-0".length + 40_000));
  assert.equal(result.truncated, true);
});

test("individual activity text is bounded and preserves source omission evidence", () => {
  const result = boundActivityHistory([{
    ...activity("oversized", "z".repeat(50_000)),
    sourceRetained: true,
    upstreamTruncated: true,
  }]);
  const [entry] = result.entries;

  assert.equal(entry.detail.length, 40_000);
  assert.equal(entry.displayTruncated, true);
  assert.equal(entry.omittedCharacters, 10_000);
  assert.equal(entry.sourceRetained, true);
  assert.equal(entry.upstreamTruncated, true);
  assert.equal(result.truncated, true);
});

test("oversized activity titles are bounded and marked as truncated", () => {
  const result = boundActivityHistory([{ ...activity("title"), title: "t".repeat(5_000) }]);

  assert.equal(result.entries[0].title.length, 1_200);
  assert.equal(result.entries[0].displayTruncated, true);
  assert.equal(result.entries[0].omittedCharacters, 3_800);
  assert.equal(result.truncated, true);
});
