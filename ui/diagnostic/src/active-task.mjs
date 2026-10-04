export const activeTaskStorageKey = "neobabylon.active-task:v1";

function validText(value) {
  return typeof value === "string" && value.trim() !== "";
}

function requirePointer(value) {
  if (!value || typeof value !== "object"
    || !validText(value.workspace)
    || !validText(value.providerId)
    || !validText(value.modelIdentifier)
    || !validText(value.threadId)) {
    throw new Error("The saved active-task hint has incomplete identity metadata.");
  }
  return {
    workspace: value.workspace,
    providerId: value.providerId,
    modelIdentifier: value.modelIdentifier,
    threadId: value.threadId,
  };
}

export function readActiveTask(storage) {
  const raw = storage.getItem(activeTaskStorageKey);
  if (raw === null) return null;
  let value;
  try { value = JSON.parse(raw); }
  catch { throw new Error("The saved active-task hint is not valid JSON."); }
  if (value?.schemaVersion !== 1) throw new Error("The saved active-task hint has an unsupported schema version.");
  return requirePointer(value);
}

export function saveActiveTask(storage, pointer) {
  const identity = requirePointer(pointer);
  storage.setItem(activeTaskStorageKey, JSON.stringify({ schemaVersion: 1, ...identity }));
}

export function clearActiveTask(storage) {
  storage.removeItem(activeTaskStorageKey);
}

function normalizedWorkspace(value) {
  return validText(value) ? value.replaceAll("\\", "/").replace(/\/+$/, "").toLowerCase() : null;
}

export function classifyActiveTask(pointer, selected, threads, executionEligible = true) {
  if (normalizedWorkspace(pointer?.workspace) !== normalizedWorkspace(selected?.workspace)) return "workspaceMismatch";
  if (pointer.providerId !== selected.providerId) return "providerMismatch";
  if (pointer.modelIdentifier !== selected.modelIdentifier) return "modelMismatch";
  const listed = Array.isArray(threads) ? threads.find((thread) => thread?.id === pointer.threadId) : null;
  if (!listed) return "threadMissing";
  if (normalizedWorkspace(listed.cwd) !== normalizedWorkspace(pointer.workspace)
    || listed.modelProvider !== pointer.providerId
    || listed.model !== pointer.modelIdentifier) return "threadMismatch";
  return executionEligible === false ? "readOnly" : "ready";
}

export function classifyActiveTaskListing(pointer, selected, threads) {
  if (normalizedWorkspace(pointer?.workspace) !== normalizedWorkspace(selected?.workspace)) return "workspaceMismatch";
  if (pointer?.providerId !== selected?.providerId) return "providerMismatch";
  if (pointer?.modelIdentifier !== selected?.modelIdentifier) return "modelMismatch";
  const listed = Array.isArray(threads) ? threads.find((thread) => thread?.id === pointer?.threadId) : null;
  if (!listed) return "threadMissing";
  if (normalizedWorkspace(listed.cwd) !== normalizedWorkspace(pointer.workspace)) return "threadMismatch";
  // App Server history rows preserve creation-time model metadata; resume confirms the current binding.
  return "ready";
}
