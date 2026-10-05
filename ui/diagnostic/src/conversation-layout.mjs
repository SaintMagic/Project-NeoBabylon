export const CONVERSATION_LAYOUT_KEY = "neobabylon.conversation-layout:v1";
const valid = (mode) => mode === "readable" || mode === "wide";

export function readConversationLayout(storage) {
  try {
    const saved = (storage ?? globalThis.localStorage)?.getItem(CONVERSATION_LAYOUT_KEY);
    return valid(saved) ? saved : "readable";
  } catch { return "readable"; }
}

export function saveConversationLayout(mode, storage) {
  if (!valid(mode)) return false;
  try {
    const target = storage ?? globalThis.localStorage;
    if (!target) return false;
    target.setItem(CONVERSATION_LAYOUT_KEY, mode);
    return true;
  } catch { return false; }
}
