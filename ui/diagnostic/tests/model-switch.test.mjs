import test from "node:test";
import assert from "node:assert/strict";
import { canSelectCapability, confirmedResumeCapability, sameCapabilityRecord, selectCapabilityForNextTurn } from "../src/model-switch.mjs";
import { draftKey } from "../src/drafts.mjs";
import { readActiveTask, saveActiveTask } from "../src/active-task.mjs";

const selectedRecord = { providerId: "openrouter", modelIdentifier: "vendor/model-b" };

test("resume uses the host-confirmed frozen record, never the current catalog's reasoning metadata", () => {
  const frozen = { ...selectedRecord, reasoningControls: { state: "Unknown", value: null } };
  const expected = { ...selectedRecord, threadId: "saved-thread-1" };
  const result = { threadId: "saved-thread-1", modelProvider: selectedRecord.providerId, model: selectedRecord.modelIdentifier, capabilityRecord: frozen };
  assert.strictEqual(confirmedResumeCapability(result, expected), frozen);
  for (const patch of [{ capabilityRecord: undefined }, { threadId: "other" }, { model: "other" }, { capabilityRecord: { ...frozen, modelIdentifier: "other" } }]) {
    assert.equal(confirmedResumeCapability({ ...result, ...patch }, expected), null);
  }
});

test("same provider/model capability revisions require exact confirmed record adoption", async () => {
  const original = { ...selectedRecord, reasoningControls: { state: "Unknown", value: null } };
  const updated = { ...selectedRecord, reasoningControls: { state: "Known", value: { supported_efforts: ["low", "high", "max"], default_effort: "max" } } };
  assert.equal(sameCapabilityRecord(original, updated), false);
  assert.equal(sameCapabilityRecord(updated, { reasoningControls: updated.reasoningControls, modelIdentifier: "vendor/model-b", providerId: "openrouter" }), true);
  let accepted = 0;
  const state = { ...selectedRecord, currentThreadId: "saved-thread-1", busy: false, capabilityRecord: updated };
  await assert.rejects(selectCapabilityForNextTurn(async () => ({ threadId: "saved-thread-1", capabilityRecord: original }), state, () => accepted++), /capability record/);
  assert.equal(accepted, 0);
  const selected = await selectCapabilityForNextTurn(async (operation) => operation === "selectCapability"
    ? { threadId: "saved-thread-1", capabilityRecord: updated } : {}, state, () => accepted++);
  assert.equal(selected.preservedThreadId, "saved-thread-1");
  assert.equal(accepted, 1);
});

test("a missing or mismatched exact host capability confirmation never accepts the catalog entry", async () => {
  for (const response of [
    { threadId: "saved-thread-1" },
    { threadId: "saved-thread-1", capabilityRecord: { providerId: "openrouter", modelIdentifier: "vendor/other" } },
    { threadId: "saved-thread-1", capabilityRecord: { providerId: "other-provider", modelIdentifier: "vendor/model-b" } },
  ]) {
    let accepted = 0;
    await assert.rejects(selectCapabilityForNextTurn(async () => response, {
      providerId: selectedRecord.providerId,
      modelIdentifier: selectedRecord.modelIdentifier,
      currentThreadId: "saved-thread-1",
      busy: false,
    }, () => { accepted += 1; }), /exact requested capability/);
    assert.equal(accepted, 0);
  }
});

test("selection requires an exact top-level thread identity echo before accepting the capability", async () => {
  for (const returnedThreadId of [undefined, null, "different-thread"]) {
    let accepted = 0;
    const result = returnedThreadId === undefined
      ? { capabilityRecord: selectedRecord }
      : { threadId: returnedThreadId, capabilityRecord: selectedRecord };
    await assert.rejects(selectCapabilityForNextTurn(async (operation, payload) => {
      if (operation === "selectCapability") {
        assert.deepEqual(payload, {
          providerId: selectedRecord.providerId,
          modelIdentifier: selectedRecord.modelIdentifier,
          threadId: "saved-thread-1",
        });
        return result;
      }
      assert.fail("diagnostics must not run before thread identity confirmation");
    }, {
      providerId: selectedRecord.providerId,
      modelIdentifier: selectedRecord.modelIdentifier,
      currentThreadId: "saved-thread-1",
      busy: false,
    }, () => { accepted += 1; }), /thread identity/);
    assert.equal(accepted, 0);
  }
});

test("an exact idle model selection preserves the same conversation, transcript, draft, and navigation if diagnostics fail", async () => {
  const calls = [];
  const activeTaskStorage = new Map();
  const storage = {
    getItem: (key) => activeTaskStorage.get(key) ?? null,
    setItem: (key, value) => activeTaskStorage.set(key, value),
  };
  const ui = {
    capability: { providerId: "lmstudio", modelIdentifier: "old-model" },
    threadId: "saved-thread-1",
    messages: [{ text: "old transcript", attributedProvider: "lmstudio", attributedModel: "old-model" }],
    draft: "continue this chat",
    navigation: { threadId: "saved-thread-1", providerId: "lmstudio", modelIdentifier: "old-model" },
  };
  const transition = await selectCapabilityForNextTurn(async (operation, payload) => {
    calls.push(operation);
    if (operation === "selectCapability") return { threadId: "saved-thread-1", capabilityRecord: selectedRecord };
    throw new Error(`${operation} unavailable`);
  }, {
    providerId: selectedRecord.providerId,
    modelIdentifier: selectedRecord.modelIdentifier,
    currentThreadId: ui.threadId,
    busy: false,
  }, (accepted) => {
    ui.capability = accepted.capabilityRecord;
    ui.threadId = accepted.activeThreadId;
    ui.navigation = { ...ui.navigation, providerId: selectedRecord.providerId, modelIdentifier: selectedRecord.modelIdentifier };
    saveActiveTask(storage, {
      workspace: "D:\\QA\\DraftProject",
      providerId: accepted.capabilityRecord.providerId,
      modelIdentifier: accepted.capabilityRecord.modelIdentifier,
      threadId: accepted.activeThreadId,
    });
  });

  assert.deepEqual(calls, ["selectCapability", "getRuntimeStatus", "getDiagnostics"]);
  assert.deepEqual(ui, {
    capability: selectedRecord,
    threadId: "saved-thread-1",
    messages: [{ text: "old transcript", attributedProvider: "lmstudio", attributedModel: "old-model" }],
    draft: "continue this chat",
    navigation: { threadId: "saved-thread-1", providerId: "openrouter", modelIdentifier: "vendor/model-b" },
  });
  assert.equal(transition.status, "diagnostics-warning");
  assert.equal(transition.preservedThreadId, "saved-thread-1");
  assert.match(transition.warning, /getRuntimeStatus unavailable|GetRuntimeStatus unavailable/);
  assert.deepEqual(readActiveTask(storage), {
    workspace: "D:\\QA\\DraftProject",
    providerId: "openrouter",
    modelIdentifier: "vendor/model-b",
    threadId: "saved-thread-1",
  });
  assert.equal(draftKey("D:\\QA\\DraftProject", ui.threadId), draftKey("D:\\QA\\DraftProject", "saved-thread-1"));
});

test("blank-task selection sends and requires a null thread identity", async () => {
  let accepted = false;
  const result = await selectCapabilityForNextTurn(async (operation, payload) => {
    if (operation === "selectCapability") {
      assert.deepEqual(payload, {
        providerId: selectedRecord.providerId,
        modelIdentifier: selectedRecord.modelIdentifier,
        threadId: null,
      });
      return { threadId: null, capabilityRecord: selectedRecord };
    }
    return {};
  }, {
    providerId: selectedRecord.providerId,
    modelIdentifier: selectedRecord.modelIdentifier,
    currentThreadId: null,
    busy: false,
  }, () => { accepted = true; });
  assert.equal(result.activeThreadId, null);
  assert.equal(accepted, true);
});

test("a draft stays selectable when idle, while busy or pending-turn state blocks capability selection", async () => {
  assert.equal(canSelectCapability({ busy: false }), true);
  assert.equal(canSelectCapability({ busy: true }), false);
  let calls = 0;
  const idleWithDraft = await selectCapabilityForNextTurn(async (operation) => {
    if (operation === "selectCapability") calls += 1;
    return { threadId: "saved-thread-1", capabilityRecord: selectedRecord };
  }, {
    providerId: selectedRecord.providerId,
    modelIdentifier: selectedRecord.modelIdentifier,
    currentThreadId: "saved-thread-1",
    busy: false,
    draft: "existing unsent text",
  }, () => {});
  assert.equal(idleWithDraft.activeThreadId, "saved-thread-1");
  assert.equal(calls, 1);

  const blocked = await selectCapabilityForNextTurn(async () => { calls += 1; }, {
    providerId: selectedRecord.providerId,
    modelIdentifier: selectedRecord.modelIdentifier,
    currentThreadId: "saved-thread-1",
    busy: true,
  }, () => assert.fail("a pending-turn guard must keep the current task untouched"));
  assert.equal(calls, 1);
  assert.equal(blocked.status, "blocked");
  assert.equal(blocked.activeThreadId, "saved-thread-1");
});
