import test from "node:test";
import assert from "node:assert/strict";
import { readConversationLayout, saveConversationLayout, CONVERSATION_LAYOUT_KEY } from "../src/conversation-layout.mjs";

test("readable is the default; only known persisted modes are accepted", () => {
  for (const saved of [null, "invalid", "true", ""]) assert.equal(readConversationLayout({ getItem: () => saved }), "readable");
  assert.equal(readConversationLayout({ getItem: () => "wide" }), "wide");
});
test("storage exceptions degrade to readable without breaking startup", () => {
  assert.equal(readConversationLayout({ getItem: () => { throw new Error("denied"); } }), "readable");
});
test("saving reports success, rejection and persistence failure truthfully", () => {
  const calls = [];
  const storage = { setItem: (...args) => calls.push(args) };
  assert.equal(saveConversationLayout("wide", storage), true);
  assert.deepEqual(calls, [[CONVERSATION_LAYOUT_KEY, "wide"]]);
  assert.equal(saveConversationLayout("arbitrary", storage), false);
  assert.equal(calls.length, 1);
  assert.equal(saveConversationLayout("readable", { setItem: () => { throw new Error("quota"); } }), false);
});
