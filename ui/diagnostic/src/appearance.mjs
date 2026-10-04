export const APPEARANCE_STORAGE_KEY = "neobabylon.appearance:v1";

const isAppearance = (value) => value === "dark" || value === "light";

function resolveStorage(storage) {
  if (storage) return storage;
  return globalThis.localStorage;
}

export function readAppearance(storage) {
  try {
    const saved = resolveStorage(storage)?.getItem(APPEARANCE_STORAGE_KEY);
    return isAppearance(saved) ? saved : "dark";
  } catch {
    return "dark";
  }
}

export function saveAppearance(appearance, storage) {
  if (!isAppearance(appearance)) return false;
  try {
    const target = resolveStorage(storage);
    if (!target) return false;
    target.setItem(APPEARANCE_STORAGE_KEY, appearance);
    return true;
  } catch {
    return false;
  }
}

export function applyAppearance(root, appearance) {
  const mode = isAppearance(appearance) ? appearance : "dark";
  root.dataset.appearance = mode;
  root.dataset.theme = mode;
  root.style.colorScheme = mode;
  const themeColor = root.ownerDocument?.querySelector('meta[name="theme-color"]');
  if (themeColor) themeColor.setAttribute("content", mode === "dark" ? "#181818" : "#f7f8fa");
}
