export function canSelectCapability({ busy }) {
  return busy !== true;
}

function readField(record, name) {
  if (!record || typeof record !== "object" || Array.isArray(record)) return undefined;
  const value = record[name];
  return value === undefined ? record[`${name[0].toUpperCase()}${name.slice(1)}`] : value;
}

function canonicalRecord(value) {
  if (Array.isArray(value)) return value.map(canonicalRecord);
  if (value && typeof value === "object") return Object.fromEntries(Object.keys(value).sort().map((key) => [key, canonicalRecord(value[key])]));
  return value;
}

export function sameCapabilityRecord(left, right) {
  if (!left || !right || typeof left !== "object" || typeof right !== "object" || Array.isArray(left) || Array.isArray(right)) return false;
  return JSON.stringify(canonicalRecord(left)) === JSON.stringify(canonicalRecord(right));
}

export function confirmedResumeCapability(result, expected) {
  if (![expected.threadId, expected.providerId, expected.modelIdentifier].every((value) => typeof value === "string" && value.length > 0)) return null;
  const record = readField(result, "capabilityRecord");
  return readField(result, "threadId") === expected.threadId
    && readField(result, "modelProvider") === expected.providerId && readField(result, "model") === expected.modelIdentifier
    && readField(record, "providerId") === expected.providerId && readField(record, "modelIdentifier") === expected.modelIdentifier
    ? record : null;
}

export class CapabilitySelectionUnconfirmedError extends Error {
  constructor(message = "The host did not confirm the exact requested capability record; the current model selection was not changed.") {
    super(message);
    this.name = "CapabilitySelectionUnconfirmedError";
  }
}

export async function selectCapabilityForNextTurn(requestHost, state, onConfirmed) {
  const currentThreadId = typeof state.currentThreadId === "string" && state.currentThreadId ? state.currentThreadId : null;
  if (!canSelectCapability({ busy: state.busy })) {
    return { status: "blocked", activeThreadId: currentThreadId, preservedThreadId: currentThreadId };
  }

  const result = await requestHost("selectCapability", {
    providerId: state.providerId,
    modelIdentifier: state.modelIdentifier,
    threadId: currentThreadId,
  });
  if (readField(result, "threadId") !== currentThreadId) {
    throw new CapabilitySelectionUnconfirmedError("The host did not confirm preserving the current thread identity; the current model selection was not changed.");
  }
  const capabilityRecord = readField(result, "capabilityRecord");
  if (readField(capabilityRecord, "providerId") !== state.providerId
    || readField(capabilityRecord, "modelIdentifier") !== state.modelIdentifier
    || (state.capabilityRecord && !sameCapabilityRecord(capabilityRecord, state.capabilityRecord))) {
    throw new CapabilitySelectionUnconfirmedError();
  }

  const accepted = {
    status: "selected",
    result,
    capabilityRecord,
    activeThreadId: currentThreadId,
    preservedThreadId: currentThreadId,
  };
  onConfirmed(accepted);

  try {
    const [runtimeResult, diagnosticsResult] = await Promise.all([
      requestHost("getRuntimeStatus"),
      requestHost("getDiagnostics"),
    ]);
    return { ...accepted, runtimeResult, diagnosticsResult };
  } catch (error) {
    const detail = error instanceof Error ? error.message : "unknown host error";
    return {
      ...accepted,
      status: "diagnostics-warning",
      warning: `Could not refresh diagnostics after changing the model: ${detail}. The confirmed model remains selected for the next turn, and this conversation was preserved.`,
    };
  }
}
