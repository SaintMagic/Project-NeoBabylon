import test from "node:test";
import assert from "node:assert/strict";
import {
  handleThreadContextMenuEvent,
  isThreadContextMenuShortcut,
  renameSavedThread,
  validateThreadName,
} from "../src/thread-actions.mjs";

test("thread rename trims and accepts names from one through 160 characters", () => {
  assert.deepEqual(validateThreadName("  Sprint notes  "), { name: "Sprint notes", error: null });
  assert.equal(validateThreadName("   ").error, "Enter a conversation name.");
  assert.equal(validateThreadName("x".repeat(161)).error, "Conversation names must be 160 characters or fewer.");
  assert.equal(validateThreadName(` ${"x".repeat(160)} `).name.length, 160);
});

test("thread rename rejects control characters before calling the host", async () => {
  let requests = 0;
  await assert.rejects(
    renameSavedThread(async () => { requests += 1; }, "thread-1", "bad\nname"),
    /control characters/,
  );
  assert.equal(requests, 0);
});

test("rename sends the captured thread identity and accepts only an attributed matching host response", async () => {
  const calls = [];
  const result = await renameSavedThread(async (operation, payload) => {
    calls.push({ operation, payload });
    return { attributedTo: "NeoBabylon.Host", threadId: "thread-1", name: "Planning" };
  }, "thread-1", "  Planning  ");

  assert.deepEqual(calls, [{ operation: "renameSavedThread", payload: { threadId: "thread-1", name: "Planning" } }]);
  assert.deepEqual(result, { attributedTo: "NeoBabylon.Host", threadId: "thread-1", name: "Planning" });
});

test("rename failure and stale or incomplete host responses do not confirm success", async () => {
  await assert.rejects(renameSavedThread(async () => { throw new Error("host unavailable"); }, "thread-1", "Planning"), /host unavailable/);
  await assert.rejects(renameSavedThread(async () => ({ attributedTo: "NeoBabylon.Host", threadId: "thread-2", name: "Planning" }), "thread-1", "Planning"), /different conversation/);
  await assert.rejects(renameSavedThread(async () => ({ threadId: "thread-1", name: "Planning" }), "thread-1", "Planning"), /did not confirm/);
});

test("chat context menu supports ContextMenu and Shift+F10 only", () => {
  assert.equal(isThreadContextMenuShortcut({ key: "ContextMenu" }), true);
  assert.equal(isThreadContextMenuShortcut({ key: "F10", shiftKey: true }), true);
  assert.equal(isThreadContextMenuShortcut({ key: "F10", shiftKey: false }), false);
  assert.equal(isThreadContextMenuShortcut({ key: "Enter", shiftKey: true }), false);
});

test("right-click and keyboard context-menu events open the same captured row menu", () => {
  const calls = [];
  const target = { getBoundingClientRect: () => ({ right: 180, top: 20, height: 44 }) };
  const event = (values) => ({ ...values, currentTarget: target, prevented: false, preventDefault() { this.prevented = true; } });

  const rightClick = event({ type: "contextmenu", clientX: 80, clientY: 33 });
  assert.equal(handleThreadContextMenuEvent(rightClick, (position) => calls.push(position)), true);
  assert.equal(rightClick.prevented, true);
  assert.deepEqual(calls[0], { target, clientX: 80, clientY: 33 });

  const keyboard = event({ type: "keydown", key: "F10", shiftKey: true });
  assert.equal(handleThreadContextMenuEvent(keyboard, (position) => calls.push(position)), true);
  assert.equal(keyboard.prevented, true);
  assert.deepEqual(calls[1], { target, clientX: 172, clientY: 42 });

  const unrelatedKey = event({ type: "keydown", key: "Enter" });
  assert.equal(handleThreadContextMenuEvent(unrelatedKey, () => calls.push("unexpected")), false);
  assert.equal(unrelatedKey.prevented, false);
});
