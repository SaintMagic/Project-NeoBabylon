import test from "node:test";
import assert from "node:assert/strict";
import { copyVisibleMessageText } from "../src/message-copy.mjs";

test("copy preserves the exact visible source text, including code and Unicode", async () => {
  const original = "```js\nconst value = '<script> Žluťoučký 🦊';\n```\n";
  const copies = [];
  await copyVisibleMessageText(original, { writeText: async (text) => copies.push(text) });
  assert.deepEqual(copies, [original]);
});
test("missing clipboard is an explicit failure, not a fake success", async () => {
  await assert.rejects(copyVisibleMessageText("text", {}), /Clipboard is unavailable/);
});
test("denied writes remain failures", async () => {
  await assert.rejects(copyVisibleMessageText("text", { writeText: async () => { throw new Error("denied"); } }), /denied/);
});
