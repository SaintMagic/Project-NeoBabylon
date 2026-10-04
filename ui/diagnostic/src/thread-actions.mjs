const CONTROL_CHARACTERS = /[\u0000-\u001f\u007f-\u009f]/u;

export function validateThreadName(value) {
  const name = typeof value === "string" ? value.trim() : "";
  if (!name) return { name, error: "Enter a conversation name." };
  if (CONTROL_CHARACTERS.test(name)) return { name, error: "Conversation names cannot contain control characters." };
  if (name.length > 160) return { name, error: "Conversation names must be 160 characters or fewer." };
  return { name, error: null };
}

export function isThreadContextMenuShortcut(event) {
  return event?.key === "ContextMenu" || (event?.key === "F10" && event.shiftKey === true);
}

export function handleThreadContextMenuEvent(event, openMenu) {
  const isPointerMenu = event?.type === "contextmenu";
  if (!isPointerMenu && !isThreadContextMenuShortcut(event)) return false;
  event.preventDefault?.();
  const target = event.currentTarget;
  const bounds = target?.getBoundingClientRect?.();
  const clientX = isPointerMenu ? event.clientX : (bounds?.right ?? 0) - 8;
  const clientY = isPointerMenu ? event.clientY : (bounds?.top ?? 0) + (bounds?.height ?? 0) / 2;
  openMenu?.({ target, clientX, clientY });
  return true;
}

export async function renameSavedThread(requestHost, threadId, value) {
  const validated = validateThreadName(value);
  if (validated.error) throw new Error(validated.error);
  if (typeof threadId !== "string" || !threadId.trim()) throw new Error("A saved conversation identity is required.");

  const result = await requestHost("renameSavedThread", { threadId, name: validated.name });
  const attributedTo = typeof result?.attributedTo === "string" ? result.attributedTo.trim() : "";
  if (result?.threadId !== threadId) {
    throw new Error("The host confirmed a different conversation; this rename was not applied in the UI.");
  }
  if (!attributedTo || result?.name !== validated.name) {
    throw new Error("The host did not confirm renaming this conversation with the requested name.");
  }
  return { attributedTo, threadId, name: validated.name };
}
