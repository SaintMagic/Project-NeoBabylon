const FOCUSABLE_SELECTOR = [
  "a[href]",
  "area[href]",
  "button:not([disabled])",
  "input:not([disabled]):not([type='hidden'])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  "iframe",
  "object",
  "embed",
  "[contenteditable='true']",
  "[tabindex]:not([tabindex='-1'])",
].join(",");

function isTabbable(element) {
  if (element.disabled || element.hidden || element.tabIndex < 0
    || element.getAttribute?.("aria-hidden") === "true"
    || element.closest?.("[inert], [aria-hidden='true']")) return false;
  if (typeof element.getClientRects === "function" && element.getClientRects().length === 0) return false;
  if (typeof globalThis.getComputedStyle === "function") {
    const style = globalThis.getComputedStyle(element);
    if (style.display === "none" || style.visibility === "hidden" || style.visibility === "collapse") return false;
  }
  return true;
}

export function handleDialogKeyDown(event, dialog, onClose) {
  if (event.key === "Escape") {
    event.preventDefault();
    onClose();
  } else if (event.key === "Tab" && dialog) {
    const focusable = [...dialog.querySelectorAll(FOCUSABLE_SELECTOR)].filter(isTabbable);
    if (!focusable.length) {
      event.preventDefault();
      dialog.focus();
      return;
    }
    const first = focusable[0];
    const last = focusable.at(-1);
    const targetInside = dialog.contains(event.target);
    if (event.shiftKey && (event.target === first || !targetInside)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && (event.target === last || !targetInside)) {
      event.preventDefault();
      first.focus();
    }
  }
}
