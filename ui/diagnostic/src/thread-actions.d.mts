export type ThreadRenameConfirmation = {
  attributedTo: string;
  threadId: string;
  name: string;
};

export function validateThreadName(value: unknown): { name: string; error: string | null };
export function isThreadContextMenuShortcut(event: { key?: string; shiftKey?: boolean } | null | undefined): boolean;
export function handleThreadContextMenuEvent(
  event: { type?: string; key?: string; shiftKey?: boolean; clientX?: number; clientY?: number; currentTarget?: unknown; preventDefault?: () => void } | null | undefined,
  openMenu: (input: { target: unknown; clientX: number; clientY: number }) => void,
): boolean;
export function renameSavedThread(
  requestHost: (operation: "renameSavedThread", payload: { threadId: string; name: string }) => Promise<Record<string, unknown>>,
  threadId: string,
  value: string,
): Promise<ThreadRenameConfirmation>;
