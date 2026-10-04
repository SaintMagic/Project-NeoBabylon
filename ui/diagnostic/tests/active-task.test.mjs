import assert from "node:assert/strict";
import test from "node:test";
import { activeTaskStorageKey, classifyActiveTask, classifyActiveTaskListing, clearActiveTask, readActiveTask, saveActiveTask } from "../src/active-task.mjs";

function storage() {
  const values = new Map();
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
    removeItem: (key) => values.delete(key),
  };
}

const pointer = { workspace: "D:\\QA\\DraftProject", providerId: "lmstudio", modelIdentifier: "phase1a-qwen3-14b", threadId: "saved-thread-1" };
const selected = { workspace: "d:/qa/draftproject/", providerId: "lmstudio", modelIdentifier: "phase1a-qwen3-14b" };
const threads = [{ id: "saved-thread-1", cwd: "D:\\QA\\DraftProject", modelProvider: "lmstudio", model: "phase1a-qwen3-14b" }];

test("active task hint persists exact navigation identity and clears only on explicit close", () => {
  const store = storage();
  assert.equal(readActiveTask(store), null);
  saveActiveTask(store, pointer);
  assert.deepEqual(readActiveTask(store), pointer);
  clearActiveTask(store);
  assert.equal(readActiveTask(store), null);
});

test("corrupt or incomplete active task hints are rejected instead of guessed", () => {
  const store = storage();
  store.setItem(activeTaskStorageKey, "not-json");
  assert.throws(() => readActiveTask(store));
  store.setItem(activeTaskStorageKey, JSON.stringify({ schemaVersion: 1, workspace: pointer.workspace, threadId: pointer.threadId }));
  assert.throws(() => readActiveTask(store));
});

test("reconnect eligibility requires exact selected and listed thread identities", () => {
  assert.equal(classifyActiveTask(pointer, selected, threads), "ready");
  assert.equal(classifyActiveTask(pointer, { ...selected, workspace: "D:\\QA\\Other" }, threads), "workspaceMismatch");
  assert.equal(classifyActiveTask(pointer, { ...selected, providerId: "openrouter" }, threads), "providerMismatch");
  assert.equal(classifyActiveTask(pointer, { ...selected, modelIdentifier: "other-model" }, threads), "modelMismatch");
  assert.equal(classifyActiveTask(pointer, selected, []), "threadMissing");
  assert.equal(classifyActiveTask(pointer, selected, [{ ...threads[0], cwd: "D:\\QA\\Other" }]), "threadMismatch");
  assert.equal(classifyActiveTask(pointer, selected, [{ ...threads[0], model: "other-model" }]), "threadMismatch");
});

test("saved-history creation model metadata does not block resume before host binding confirmation", () => {
  const switchedSelection = { ...selected, providerId: "openrouter", modelIdentifier: "new-selected-model" };
  const switchedPointer = { ...pointer, providerId: "openrouter", modelIdentifier: "new-selected-model" };
  const originalCreationMetadata = [{ ...threads[0], modelProvider: "lmstudio", model: "original-model" }];
  assert.equal(classifyActiveTaskListing(switchedPointer, switchedSelection, originalCreationMetadata), "ready");
  assert.equal(classifyActiveTask(switchedPointer, switchedSelection, originalCreationMetadata), "threadMismatch");
});

test("host-level execution denial keeps an identity-matched saved task read-only", () => {
  assert.equal(classifyActiveTask(pointer, selected, threads, false), "readOnly");
});
