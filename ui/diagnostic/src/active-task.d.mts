export type ActiveTaskPointer = {
  workspace: string;
  providerId: string;
  modelIdentifier: string;
  threadId: string;
};

export type ActiveTaskSelection = {
  workspace: string | undefined;
  providerId: string | undefined;
  modelIdentifier: string | undefined;
};

export type ActiveTaskListedThread = {
  id: string;
  cwd?: string;
  modelProvider: string;
  model: string;
};

export const activeTaskStorageKey: "neobabylon.active-task:v1";
export function readActiveTask(storage: Pick<Storage, "getItem">): ActiveTaskPointer | null;
export function saveActiveTask(storage: Pick<Storage, "setItem">, pointer: ActiveTaskPointer): void;
export function clearActiveTask(storage: Pick<Storage, "removeItem">): void;
export function classifyActiveTask(pointer: ActiveTaskPointer, selected: ActiveTaskSelection, threads: ActiveTaskListedThread[] | null, executionEligible?: boolean): "ready" | "readOnly" | "workspaceMismatch" | "providerMismatch" | "modelMismatch" | "threadMissing" | "threadMismatch";
export function classifyActiveTaskListing(pointer: ActiveTaskPointer, selected: ActiveTaskSelection, threads: Array<Pick<ActiveTaskListedThread, "id" | "cwd">> | null): "ready" | "workspaceMismatch" | "providerMismatch" | "modelMismatch" | "threadMissing" | "threadMismatch";
