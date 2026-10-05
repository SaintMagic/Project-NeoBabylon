import test from "node:test";
import assert from "node:assert/strict";
import { restoreSavedActivities } from "../src/restoration.mjs";
import { mergeActivityHistoryEntry, mergeOutputRange, projectOutputRangePage } from "../src/tool-activity.mjs";

test("restores actionable command title, exact arguments, and retained output", () => {
  const call = {
    type: "function_call",
    id: "call-1",
    call_id: "call-id-1",
    name: "exec_command",
    arguments: '{"cmd":"Get-ChildItem -Force ui/diagnostic"}',
  };
  const output = {
    type: "function_call_output",
    call_id: "call-id-1",
    output: "3 entries",
  };
  assert.equal(call.call_id, output.call_id);
  const [activity] = restoreSavedActivities([{
    itemId: call.id,
    turnId: "turn-1",
    itemType: "functionCallOutput",
    outputItemId: "output-item-1",
    title: call.name,
    toolName: call.name,
    arguments: call.arguments,
    output: output.output,
    outcome: "unknown",
  }], "thread-1");

  assert.equal(activity.title, "Get-ChildItem -Force ui/diagnostic");
  assert.equal(activity.argumentsText, call.arguments);
  assert.equal(activity.detail, "3 entries");
  assert.equal(activity.outputItemId, "output-item-1");
  assert.equal(activity.sourceRetained, true);
  assert.equal(activity.status, "info");
});

test("appends contiguous retained output pages without omitting earlier characters", () => {
  const first = {
    threadId: "thread-1", turnId: "turn-1", itemId: "item-1", itemType: "functionCallOutput",
    offset: 0, totalCharacters: 5, text: "abc", nextOffset: 3, hasMore: true,
    upstreamTruncated: false, omittedParts: false,
  };
  const second = { ...first, offset: 3, text: "de", nextOffset: 5, hasMore: false };

  const loaded = mergeOutputRange(first, second);

  assert.equal(loaded.text, "abcde");
  assert.equal(loaded.offset, 0);
  assert.equal(loaded.nextOffset, 5);
  assert.equal(loaded.hasMore, false);
});

test("refuses to append a page from another item or a noncontiguous offset", () => {
  const first = {
    threadId: "thread-1", turnId: "turn-1", itemId: "item-1", itemType: "functionCallOutput",
    offset: 0, totalCharacters: 5, text: "abc", nextOffset: 3, hasMore: true,
    upstreamTruncated: false, omittedParts: false,
  };
  assert.throws(() => mergeOutputRange(first, { ...first, itemId: "item-2", offset: 3 }), /same item/);
  assert.throws(() => mergeOutputRange(first, { ...first, offset: 4 }), /contiguous/);
});

test("rejects missing or mismatched host-returned range identity", () => {
  const location = { threadId: "thread-1", turnId: "turn-1", itemId: "item-1", itemType: "functionCallOutput", title: "command" };
  const response = { threadId: "thread-1", turnId: "turn-1", itemId: "item-1", itemType: "functionCallOutput", offset: 0, totalCharacters: 3, text: "abc", nextOffset: 3, hasMore: false };
  assert.equal(projectOutputRangePage(response, location, 0).text, "abc");
  assert.throws(() => projectOutputRangePage({ ...response, itemId: "other" }, location, 0), /identity/);
  assert.throws(() => projectOutputRangePage({ ...response, threadId: undefined }, location, 0), /identity/);
  assert.throws(() => projectOutputRangePage({ ...response, itemType: "agentMessage" }, location, 0), /identity/);
});

test("post-turn card updates keep richer live retained identity and arguments", () => {
  const live = { id: "call-1", threadId: "thread-1", turnId: "turn-1", itemType: "functionCallOutput", outputItemId: "output-1", sourceRetained: true, argumentsText: '{"cmd":"git status"}', command: "git status" };
  const diagnostic = { id: "call-1", threadId: "thread-1", turnId: "turn-1", itemType: undefined, outputItemId: undefined, sourceRetained: false, status: "succeeded" };
  const merged = mergeActivityHistoryEntry(live, diagnostic);
  assert.equal(merged.argumentsText, live.argumentsText);
  assert.equal(merged.outputItemId, "output-1");
  assert.equal(merged.sourceRetained, true);
  assert.equal(merged.status, "succeeded");
  assert.equal(mergeActivityHistoryEntry(live, { ...diagnostic, turnId: "turn-2" }).sourceRetained, false);
});
