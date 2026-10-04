import assert from "node:assert/strict";
import test from "node:test";
import {
  clearPendingSubmission,
  draftKey,
  loadDraft,
  moveDraft,
  pendingSubmissionStorageKey,
  reconcilePendingSubmission,
  saveDraft,
  savePendingSubmission,
} from "../src/drafts.mjs";

function storage() {
  const values = new Map();
  return {
    values,
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
    removeItem: (key) => values.delete(key),
  };
}

test("unsent text survives storage round trip only in its project and task", () => {
  const store = storage();
  const firstNew = draftKey("D:\\Projects\\First", null);
  const firstTask = draftKey("D:\\Projects\\First", "thread-1");
  const secondNew = draftKey("D:\\Projects\\Second", null);
  assert.ok(firstNew && firstTask && secondNew);
  saveDraft(store, firstNew, "  Keep my exact draft.  ");
  assert.equal(loadDraft(store, firstNew), "  Keep my exact draft.  ");
  assert.equal(loadDraft(store, firstTask), "");
  assert.equal(loadDraft(store, secondNew), "");
  assert.equal(draftKey("d:/projects/first/", null), firstNew);
});

test("clearing a sent draft removes only its matching scope", () => {
  const store = storage();
  const first = draftKey("D:\\Projects\\First", "thread-1");
  const second = draftKey("D:\\Projects\\First", "thread-2");
  saveDraft(store, first, "sent text");
  saveDraft(store, second, "still unsent");
  saveDraft(store, first, "");
  assert.equal(loadDraft(store, first), "");
  assert.equal(loadDraft(store, second), "still unsent");
  assert.equal(store.values.has(first), false);
});

test("failed first turn can move its draft from new-task scope to its created thread", () => {
  const store = storage();
  const newTask = draftKey("D:\\Projects\\First", null);
  const createdThread = draftKey("D:\\Projects\\First", "thread-1");
  saveDraft(store, newTask, "retry after failed start");
  moveDraft(store, newTask, createdThread);
  assert.equal(loadDraft(store, newTask), "");
  assert.equal(loadDraft(store, createdThread), "retry after failed start");
});

test("clearing a sent draft persists an empty value when storage denies removal", () => {
  const store = storage();
  const key = draftKey("D:\\Projects\\First", "thread-1");
  saveDraft(store, key, "already accepted by App Server");
  store.removeItem = () => { throw new Error("remove denied"); };

  saveDraft(store, key, "");

  assert.equal(loadDraft(store, key), "");
  assert.equal(store.values.get(key), "");
});

test("accepted in-flight submission clears its exact thread and source drafts after history proves acceptance", () => {
  const store = storage();
  const workspace = "D:\\Projects\\First";
  const sourceDraftScope = draftKey(workspace, null);
  const draftScope = draftKey(workspace, "thread-1");
  saveDraft(store, sourceDraftScope, "accepted prompt");
  saveDraft(store, draftScope, "accepted prompt");
  savePendingSubmission(store, {
    workspace, providerId: "lmstudio", modelIdentifier: "model-1", threadId: "thread-1",
    requestId: "turn-request-1", draftScope, sourceDraftScope, previousTurnId: null,
  });

  const outcome = reconcilePendingSubmission(store, {
    workspace, providerId: "lmstudio", modelIdentifier: "model-1", threadId: "thread-1",
    latestTurnId: "turn-1", latestUserText: "accepted prompt",
  });

  assert.equal(outcome.status, "accepted");
  assert.equal(loadDraft(store, draftScope), "");
  assert.equal(loadDraft(store, sourceDraftScope), "");
  assert.equal(store.getItem?.(pendingSubmissionStorageKey(workspace, "thread-1")) ?? store.values.get(pendingSubmissionStorageKey(workspace, "thread-1")) ?? null, null);
});

test("unaccepted new-task draft is moved to its recovered thread scope and remains retryable", () => {
  const store = storage();
  const workspace = "D:\\Projects\\First";
  const sourceDraftScope = draftKey(workspace, null);
  const draftScope = draftKey(workspace, "thread-1");
  saveDraft(store, sourceDraftScope, "unaccepted prompt");
  savePendingSubmission(store, {
    workspace, providerId: "lmstudio", modelIdentifier: "model-1", threadId: "thread-1",
    requestId: "turn-request-2", draftScope, sourceDraftScope, previousTurnId: null,
  });

  const outcome = reconcilePendingSubmission(store, {
    workspace, providerId: "lmstudio", modelIdentifier: "model-1", threadId: "thread-1",
    latestTurnId: null, latestUserText: null,
  });

  assert.equal(outcome.status, "notAccepted");
  assert.equal(loadDraft(store, draftScope), "unaccepted prompt");
  assert.equal(loadDraft(store, sourceDraftScope), "");
});

test("matching prior history does not prove acceptance without a new turn identity", () => {
  const store = storage();
  const workspace = "D:\\Projects\\First";
  const draftScope = draftKey(workspace, "thread-1");
  saveDraft(store, draftScope, "repeat the previous prompt");
  savePendingSubmission(store, {
    workspace, providerId: "lmstudio", modelIdentifier: "model-1", threadId: "thread-1",
    requestId: "turn-request-3", draftScope, sourceDraftScope: draftScope, previousTurnId: "turn-0",
  });

  const outcome = reconcilePendingSubmission(store, {
    workspace, providerId: "lmstudio", modelIdentifier: "model-1", threadId: "thread-1",
    latestTurnId: "turn-0", latestUserText: "repeat the previous prompt",
  });

  assert.equal(outcome.status, "notAccepted");
  assert.equal(loadDraft(store, draftScope), "repeat the previous prompt");
});

test("different last-turn content leaves an uncertain pending draft untouched", () => {
  const store = storage();
  const workspace = "D:\\Projects\\First";
  const draftScope = draftKey(workspace, "thread-1");
  saveDraft(store, draftScope, "do not discard me");
  savePendingSubmission(store, {
    workspace, providerId: "lmstudio", modelIdentifier: "model-1", threadId: "thread-1",
    requestId: "turn-request-4", draftScope, sourceDraftScope: draftScope, previousTurnId: "turn-0",
  });

  const outcome = reconcilePendingSubmission(store, {
    workspace, providerId: "lmstudio", modelIdentifier: "model-1", threadId: "thread-1",
    latestTurnId: "turn-1", latestUserText: "a different accepted message",
  });

  assert.equal(outcome.status, "unresolved");
  assert.equal(loadDraft(store, draftScope), "do not discard me");
});

test("pending-send identity mismatch never clears the draft", () => {
  const store = storage();
  const workspace = "D:\\Projects\\First";
  const draftScope = draftKey(workspace, "thread-1");
  saveDraft(store, draftScope, "preserve on mismatch");
  savePendingSubmission(store, {
    workspace, providerId: "lmstudio", modelIdentifier: "model-1", threadId: "thread-1",
    requestId: "turn-request-5", draftScope, sourceDraftScope: draftScope, previousTurnId: null,
  });

  const outcome = reconcilePendingSubmission(store, {
    workspace, providerId: "openrouter", modelIdentifier: "model-2", threadId: "thread-1",
    latestTurnId: "turn-1", latestUserText: "preserve on mismatch",
  });

  assert.equal(outcome.status, "identityMismatch");
  assert.equal(loadDraft(store, draftScope), "preserve on mismatch");
  clearPendingSubmission(store, workspace, "thread-1");
});
