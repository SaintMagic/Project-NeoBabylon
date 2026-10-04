export type HostOperation =
  | "getRuntimeStatus"
  | "getDiagnostics"
  | "listGeneratedToolCandidates"
  | "readGeneratedToolCandidateFileRange"
  | "listGeneratedToolReviewHistory"
  | "recordGeneratedToolReview"
  | "rejectGeneratedToolCandidate"
  | "listGeneratedToolPreparedBindingHistory"
  | "listGeneratedToolPreparedBindingToolIds"
  | "readGeneratedToolPreparedBindingCurrent"
  | "prepareGeneratedToolDisabledBinding"
  | "revokeGeneratedToolPreparedBinding"
  | "cleanupGeneratedToolPreparedBinding"
  | "stageGeneratedToolDisabledMcp"
  | "getGeneratedToolActivationStatus"
  | "activateGeneratedTool"
  | "revokeGeneratedToolActivation"
  | "listGeneratedToolActivationHistory"
  | "readGeneratedToolReviewComparison"
  | "listProtectedRecordBackups"
  | "restoreProtectedRecordBackup"
  | "listCapabilities"
  | "selectCapability"
  | "listThreads"
  | "renameSavedThread"
  | "resumeThread"
  | "getThreadUsage"
  | "compactContext"
  | "forkThread"
  | "readOutputRange"
  | "newTask"
  | "startThread"
  | "startTurn"
  | "interruptTurn"
  | "stopCommand"
  | "respondToApproval"
  | "setAppearance"
  | "addProject"
  | "selectProject";

export type HostResponse<T = Record<string, unknown>> = {
  requestId: string;
  ok: boolean;
  result?: T;
  error?: { attributedTo?: string; type?: string; code?: string; status?: string; recordKey?: string; refreshAction?: string; message?: string };
  stream?: boolean;
  method?: string;
  params?: unknown;
};

export class HostOperationError extends Error {
  readonly attributedTo?: string;
  readonly type?: string;
  readonly code?: string;
  readonly status?: string;
  readonly recordKey?: string;
  readonly refreshAction?: string;

  constructor(error?: HostResponse["error"]) {
    const attributedTo = typeof error?.attributedTo === "string" && error.attributedTo.trim()
      ? error.attributedTo.trim()
      : undefined;
    const type = typeof error?.type === "string" && error.type.trim()
      ? error.type.trim()
      : undefined;
    const code = typeof error?.code === "string" && error.code.trim() ? error.code.trim() : undefined;
    const status = typeof error?.status === "string" && error.status.trim() ? error.status.trim() : undefined;
    const recordKey = typeof error?.recordKey === "string" && error.recordKey.trim() ? error.recordKey.trim() : undefined;
    const refreshAction = typeof error?.refreshAction === "string" && error.refreshAction.trim() ? error.refreshAction.trim() : undefined;
    const message = typeof error?.message === "string" && error.message.trim()
      ? error.message
      : "The local host operation failed without a detail.";
    const attribution = [attributedTo, type].filter((value): value is string => Boolean(value)).join(" / ");
    super(attribution ? `${attribution}: ${message}` : message);
    this.name = "HostOperationError";
    this.attributedTo = attributedTo;
    this.type = type;
    this.code = code;
    this.status = status;
    this.recordKey = recordKey;
    this.refreshAction = refreshAction;
  }
}

export type HostNotification = HostResponse & { stream: true; method: string; params: unknown };
type WebViewBridge = {
  postMessage(message: unknown): void;
  addEventListener(type: "message", listener: (event: MessageEvent<HostResponse>) => void): void;
};

declare global {
  interface Window { chrome?: { webview?: WebViewBridge } }
}

const pending = new Map<string, {
  resolve: (value: Record<string, unknown>) => void;
  reject: (reason: Error) => void;
  timer: number;
}>();
const subscribers = new Set<(message: HostResponse) => void>();
let listening = false;
let sequence = 0;

function attachListener() {
  const bridge = window.chrome?.webview;
  if (!bridge || listening) return bridge;
  listening = true;
  bridge.addEventListener("message", (event) => {
    const message = event.data;
    for (const subscriber of subscribers) {
      try { subscriber(message); }
      catch (error) { console.error("A NeoBabylon stream renderer failed; the host response remains active.", error); }
    }
    if (message.stream) return;
    const request = pending.get(message.requestId);
    if (!request) return;
    window.clearTimeout(request.timer);
    pending.delete(message.requestId);
    if (message.ok && message.result && typeof message.result === "object") request.resolve(message.result as Record<string, unknown>);
    else request.reject(new HostOperationError(message.error));
  });
  return bridge;
}

export function requestHost<T extends Record<string, unknown>>(
  operation: HostOperation,
  payload: Record<string, unknown> = {},
  requestId = `ui-${++sequence}-${crypto.randomUUID()}`,
): Promise<T> {
  const bridge = attachListener();
  if (!bridge) return Promise.reject(new Error("The NeoBabylon desktop bridge is unavailable."));
  return new Promise<T>((resolve, reject) => {
    const timeoutMs = operation === "startTurn" || operation === "compactContext" ? 650_000 : 30_000;
    const timer = window.setTimeout(() => {
      pending.delete(requestId);
      reject(new Error(`The ${operation} operation did not return within ${Math.round(timeoutMs / 1000)} seconds.`));
    }, timeoutMs);
    pending.set(requestId, { resolve: (result) => resolve(result as T), reject, timer });
    try { bridge.postMessage({ operation, requestId, ...payload }); }
    catch (error) {
      window.clearTimeout(timer);
      pending.delete(requestId);
      reject(error instanceof Error ? error : new Error("The host bridge rejected the operation."));
    }
  });
}

export function listenToHost(listener: (message: HostNotification) => void): () => void {
  attachListener();
  const wrapped = (message: HostResponse) => {
    if (message.stream && message.method && message.params !== undefined) listener(message as HostNotification);
  };
  subscribers.add(wrapped);
  return () => subscribers.delete(wrapped);
}
