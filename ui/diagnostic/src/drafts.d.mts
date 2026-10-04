export function draftKey(workspace: string | undefined, threadId: string | null): string | null;
export function loadDraft(storage: Pick<Storage, "getItem">, key: string | null): string;
export function saveDraft(storage: Pick<Storage, "setItem" | "removeItem">, key: string | null, text: string): void;
export function moveDraft(storage: Pick<Storage, "getItem" | "setItem" | "removeItem">, fromKey: string | null, toKey: string | null): void;
export function pendingSubmissionStorageKey(workspace: string | undefined, threadId: string | null): string | null;
export function savePendingSubmission(storage: Pick<Storage, "setItem">, submission: {
  workspace: string;
  providerId: string;
  modelIdentifier: string;
  threadId: string;
  requestId: string;
  draftScope: string;
  sourceDraftScope: string;
  previousTurnId: string | null;
}): void;
export function clearPendingSubmission(storage: Pick<Storage, "removeItem">, workspace: string, threadId: string): void;
export function reconcilePendingSubmission(storage: Pick<Storage, "getItem" | "setItem" | "removeItem">, identity: {
  workspace: string;
  providerId: string;
  modelIdentifier: string;
  threadId: string;
  latestTurnId: string | null;
  latestUserText: string | null;
}): { status: "none" | "identityMismatch" | "accepted" | "notAccepted" | "unresolved" };
