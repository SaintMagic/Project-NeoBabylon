export async function copyVisibleMessageText(text, clipboard = globalThis.navigator?.clipboard) {
  if (!clipboard || typeof clipboard.writeText !== "function") throw new Error("Clipboard is unavailable.");
  await clipboard.writeText(text);
}
