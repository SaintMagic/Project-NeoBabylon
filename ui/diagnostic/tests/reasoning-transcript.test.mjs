import assert from "node:assert/strict";
import test from "node:test";
import { upsertReasoningDelta, upsertReasoningItem } from "../src/reasoning-transcript.mjs";
import { transcriptMessageKey } from "../src/transcript.mjs";

test("indexed reasoning deltas retain identity, order, whitespace, and are replaced by completion", () => {
  let messages = [];
  messages = upsertReasoningDelta(messages, "item/reasoning/summaryTextDelta", {
    threadId: "thread-a", turnId: "turn-a", itemId: "reasoning-1", summaryIndex: 1, delta: " second\n",
  });
  messages = upsertReasoningDelta(messages, "item/reasoning/summaryTextDelta", {
    threadId: "thread-a", turnId: "turn-a", itemId: "reasoning-1", summaryIndex: 0, delta: "first ",
  });
  assert.equal(messages.length, 1);
  assert.equal(messages[0].text, "first  second\n");
  assert.equal(messages[0].reasoningLabel, "Reasoning summary");
  assert.equal(messages[0].threadId, "thread-a");
  assert.equal(messages[0].turnId, "turn-a");

  messages = upsertReasoningDelta(messages, "item/reasoning/textDelta", {
    threadId: "thread-a", turnId: "turn-a", itemId: "reasoning-1", contentIndex: 0, delta: "content",
  });
  assert.equal(messages[0].text, "content");
  assert.equal(messages[0].reasoningLabel, "Reasoning");

  messages = upsertReasoningItem(messages, {
    id: "reasoning-1", type: "reasoning", summary: ["summary"], content: ["authoritative ", "payload"],
  }, { threadId: "thread-a", turnId: "turn-a" });
  assert.equal(messages.length, 1);
  assert.equal(messages[0].text, "authoritative payload");
  assert.equal(messages[0].streaming, false);
});

test("does not create reasoning entries from empty, unindexed, or mismatched events", () => {
  const event = { threadId: "thread-a", turnId: "turn-a", itemId: "r-1", summaryIndex: 0, delta: " " };
  assert.deepEqual(upsertReasoningDelta([], "item/reasoning/summaryTextDelta", { ...event, delta: "" }), []);
  assert.deepEqual(upsertReasoningDelta([], "item/reasoning/summaryTextDelta", { ...event, summaryIndex: -1 }), []);
  assert.deepEqual(upsertReasoningItem([], { id: "r-2", type: "reasoning", summary: [], content: [] }, event), []);
});

test("bounded part previews report omissions without creating empty reasoning sections", () => {
  const parts = Array.from({ length: 1_000 }, () => "");
  parts[999] = "unshown source";
  const restored = upsertReasoningItem([], {
    id: "many-parts",
    type: "reasoning",
    summary: [],
    content: parts,
    neoBabylonDisplay: {
      displayTruncated: true,
      omittedCharacters: 0,
      omittedParts: true,
      sourceRetained: true,
    },
  }, { threadId: "thread-bounded", turnId: "turn-bounded" });
  assert.equal(restored.length, 1);
  assert.equal(restored[0].role, "status");
  assert.match(restored[0].text, /Additional reasoning parts were omitted/);
  assert.equal(restored[0].sourceRetained, true);
  assert.equal(restored[0].itemId, "many-parts");
});

test("omission evidence updates its exact streamed item and keeps other turns separate", () => {
  let messages = upsertReasoningDelta([], "item/reasoning/textDelta", {
    threadId: "thread-a", turnId: "turn-a", itemId: "r-1", contentIndex: 0, delta: "visible",
  });
  messages = upsertReasoningItem(messages, {
    id: "r-1", type: "reasoning", summary: [], content: [],
    neoBabylonDisplay: { displayTruncated: true, omittedCharacters: 9, sourceRetained: false },
  }, { threadId: "thread-a", turnId: "turn-a" });
  assert.equal(messages.length, 1);
  assert.equal(messages[0].text, "visible");
  assert.equal(messages[0].omittedCharacters, 9);
  assert.equal(messages[0].sourceRetained, false);

  const otherTurn = upsertReasoningItem(messages, {
    id: "r-1", type: "reasoning", summary: ["separate turn"], content: [],
  }, { threadId: "thread-a", turnId: "turn-b" });
  assert.equal(otherTurn.length, 2);
  assert.equal(otherTurn[1].turnId, "turn-b");
});

test("authoritative reasoning replaces or removes its same-item omission note", () => {
  const identity = { threadId: "t", turnId: "u" };
  let messages = upsertReasoningItem([], {
    id: "r", type: "reasoning", summary: [], content: [],
    neoBabylonDisplay: { displayTruncated: true, omittedCharacters: 9, sourceRetained: false },
  }, identity);
  assert.equal(messages.length, 1);
  assert.equal(messages[0].reasoningOmission, true);
  assert.equal(messages[0].itemId, "r");

  messages = upsertReasoningItem(messages, {
    id: "r", type: "reasoning", summary: ["available summary"], content: [],
  }, identity);
  assert.equal(messages.length, 1);
  assert.equal(messages[0].role, "reasoning");
  assert.equal(messages[0].text, "available summary");

  messages = upsertReasoningItem(messages, {
    id: "r", type: "reasoning", summary: [], content: [],
  }, identity);
  assert.deepEqual(messages, []);
});

test("reasoning React keys bind role, thread, turn, and logical item identity", () => {
  const first = upsertReasoningItem([], {
    id: "r", type: "reasoning", summary: ["first turn"], content: [],
  }, { threadId: "t", turnId: "u1" })[0];
  const second = upsertReasoningItem([first], {
    id: "r", type: "reasoning", summary: ["second turn"], content: [],
  }, { threadId: "t", turnId: "u2" })[1];
  assert.notEqual(transcriptMessageKey(first), transcriptMessageKey(second));

  const omission = upsertReasoningItem([], {
    id: "r", type: "reasoning", summary: [], content: [],
    neoBabylonDisplay: { displayTruncated: true, omittedCharacters: 9 },
  }, { threadId: "t", turnId: "u1" })[0];
  assert.equal(JSON.parse(transcriptMessageKey(omission)).at(-1), "r");
  assert.notEqual(transcriptMessageKey(omission), transcriptMessageKey(first));
});
