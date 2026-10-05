import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import * as streaming from "../src/stream-batcher.mjs";

test("assistant delta admission retains whitespace, but rejects empty and non-text values", () => {
  assert.equal(typeof streaming.assistantDeltaText, "function", "stream payloads need a lossless text admission function");
  for (const value of [" ", "\n", "\r\n", "\t", "  indented", "answer"]) {
    assert.equal(streaming.assistantDeltaText(value), value);
  }
  for (const value of ["", null, undefined, 0, false, {}, ["text"]]) {
    assert.equal(streaming.assistantDeltaText(value), undefined);
  }
});

test("an interrupted stream preserves word separators, newlines and indentation", () => {
  assert.equal(typeof streaming.assistantDeltaText, "function");
  const batches = [];
  const batcher = streaming.createAssistantDeltaBatcher({
    schedule: () => 1, cancel: () => {}, onBatch: (batch) => batches.push(batch),
  });
  for (const raw of ["Hello", " ", "world", "\n", "\t", "return", " ", "42;"]) {
    const delta = streaming.assistantDeltaText(raw);
    if (delta !== undefined) batcher.push("assistant-1", delta);
  }
  // A failed/interrupted turn has no authoritative completed message to repair it.
  batcher.flush();
  batcher.clear();
  assert.deepEqual(batches, [[{ id: "assistant-1", delta: "Hello world\n\treturn 42;" }]]);
});

test("both assistant notification aliases use lossless admission in the actual UI handler", () => {
  const source = readFileSync(new URL("../src/App.tsx", import.meta.url), "utf8");
  const start = source.indexOf('if (method === "item/agentMessage/delta" || method === "item/assistantMessage/delta")');
  assert.ok(start >= 0, "assistant event branch must remain present");
  const branch = source.slice(start, source.indexOf("return;", start));
  assert.match(branch, /const delta = assistantDeltaText\(field\(params, "delta"\)\)/);
  assert.doesNotMatch(branch, /textValue\(field\(params, "delta"\)\)/);
  assert.match(branch, /\.push\(id, delta, displayMetadata\)/);
});
