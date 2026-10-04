const prefix = "neobabylon.draft:v1:";
const pendingSubmissionPrefix = "neobabylon.pending-submission:v1:";

export function draftKey(workspace, threadId) {
  if (typeof workspace !== "string" || !workspace.trim()) return null;
  const normalized = workspace.replaceAll("\\", "/").replace(/\/+$/, "").toLowerCase();
  const task = threadId ? `thread:${threadId}` : "new";
  return `${prefix}${encodeURIComponent(normalized)}:${encodeURIComponent(task)}`;
}

export function loadDraft(storage, key) {
  return key ? storage.getItem(key) ?? "" : "";
}

export function saveDraft(storage, key, text) {
  if (!key) return;
  if (text !== "") {
    storage.setItem(key, text);
    return;
  }
  try {
    storage.removeItem(key);
  } catch (removeError) {
    try { storage.setItem(key, ""); }
    catch { throw removeError; }
  }
}

export function moveDraft(storage, fromKey, toKey) {
  if (!fromKey || !toKey || fromKey === toKey) return;
  const text = loadDraft(storage, fromKey);
  if (text !== "") saveDraft(storage, toKey, text);
  saveDraft(storage, fromKey, "");
}

export function pendingSubmissionStorageKey(workspace, threadId) {
  const targetKey = draftKey(workspace, threadId);
  return targetKey ? `${pendingSubmissionPrefix}${encodeURIComponent(targetKey)}` : null;
}

export function savePendingSubmission(storage, submission) {
  const { workspace, providerId, modelIdentifier, threadId, requestId, draftScope, sourceDraftScope, previousTurnId } = submission ?? {};
  const key = pendingSubmissionStorageKey(workspace, threadId);
  const expectedDraftScope = draftKey(workspace, threadId);
  const expectedSourceScope = draftKey(workspace, null);
  if (!key || ![providerId, modelIdentifier, threadId, requestId].every((value) => typeof value === "string" && value.trim())
    || draftScope !== expectedDraftScope
    || (sourceDraftScope !== expectedDraftScope && sourceDraftScope !== expectedSourceScope)
    || !(previousTurnId === null || typeof previousTurnId === "string")) {
    throw new Error("The pending submission identity is incomplete or inconsistent.");
  }

  storage.setItem(key, JSON.stringify({
    schemaVersion: 1,
    workspace,
    providerId,
    modelIdentifier,
    threadId,
    requestId,
    draftScope,
    sourceDraftScope,
    previousTurnId,
  }));
}

export function clearPendingSubmission(storage, workspace, threadId) {
  const key = pendingSubmissionStorageKey(workspace, threadId);
  if (key) storage.removeItem(key);
}

export function reconcilePendingSubmission(storage, identity) {
  const { workspace, providerId, modelIdentifier, threadId, latestTurnId, latestUserText } = identity ?? {};
  const key = pendingSubmissionStorageKey(workspace, threadId);
  if (!key) return { status: "none" };
  const raw = storage.getItem(key);
  if (raw === null) return { status: "none" };

  let marker;
  try { marker = JSON.parse(raw); }
  catch { return { status: "unresolved" }; }
  const targetScope = draftKey(workspace, threadId);
  const newTaskScope = draftKey(workspace, null);
  if (marker?.schemaVersion !== 1
    || marker.draftScope !== targetScope
    || (marker.sourceDraftScope !== targetScope && marker.sourceDraftScope !== newTaskScope)
    || typeof marker.requestId !== "string"
    || typeof marker.previousTurnId !== "string" && marker.previousTurnId !== null) {
    return { status: "unresolved" };
  }
  if (marker.workspace.replaceAll("\\", "/").replace(/\/+$/, "").toLowerCase()
      !== workspace.replaceAll("\\", "/").replace(/\/+$/, "").toLowerCase()
    || marker.providerId !== providerId
    || marker.modelIdentifier !== modelIdentifier
    || marker.threadId !== threadId) {
    return { status: "identityMismatch" };
  }

  const targetText = loadDraft(storage, marker.draftScope);
  const sourceText = marker.sourceDraftScope === marker.draftScope
    ? ""
    : loadDraft(storage, marker.sourceDraftScope);
  if (targetText && sourceText && targetText !== sourceText) return { status: "unresolved" };
  const pendingText = targetText || sourceText;

  if (latestTurnId === marker.previousTurnId) {
    if (!latestTurnId && latestUserText && latestUserText === pendingText) return { status: "unresolved" };
    if (!targetText && sourceText) saveDraft(storage, marker.draftScope, sourceText);
    if (sourceText && marker.sourceDraftScope !== marker.draftScope) saveDraft(storage, marker.sourceDraftScope, "");
    clearPendingSubmission(storage, workspace, threadId);
    return { status: "notAccepted" };
  }

  if (!pendingText) {
    clearPendingSubmission(storage, workspace, threadId);
    return { status: "accepted" };
  }
  if (latestTurnId && latestUserText === pendingText) {
    saveDraft(storage, marker.draftScope, "");
    if (sourceText && marker.sourceDraftScope !== marker.draftScope) saveDraft(storage, marker.sourceDraftScope, "");
    clearPendingSubmission(storage, workspace, threadId);
    return { status: "accepted" };
  }
  return { status: "unresolved" };
}
