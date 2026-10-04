import test from "node:test";
import assert from "node:assert/strict";
import {
  APPEARANCE_STORAGE_KEY,
  applyAppearance,
  readAppearance,
  saveAppearance,
} from "../src/appearance.mjs";

function memoryStorage(initial = {}) {
  const values = new Map(Object.entries(initial));
  return {
    getItem(key) { return values.get(key) ?? null; },
    setItem(key, value) { values.set(key, String(value)); },
  };
}

test("first-run and invalid stored preferences default to dark", () => {
  assert.equal(readAppearance(memoryStorage()), "dark");
  assert.equal(readAppearance(memoryStorage({ [APPEARANCE_STORAGE_KEY]: "system" })), "dark");
});

test("a valid saved appearance is restored", () => {
  assert.equal(readAppearance(memoryStorage({ [APPEARANCE_STORAGE_KEY]: "light" })), "light");
  assert.equal(readAppearance(memoryStorage({ [APPEARANCE_STORAGE_KEY]: "dark" })), "dark");
});

test("appearance storage failures safely retain the dark default", () => {
  const unavailable = { getItem() { throw new Error("storage blocked"); } };
  assert.equal(readAppearance(unavailable), "dark");
  assert.equal(saveAppearance("light", { setItem() { throw new Error("storage blocked"); } }), false);
});

test("selection is persisted under the versioned application key", () => {
  const storage = memoryStorage();
  assert.equal(saveAppearance("light", storage), true);
  assert.equal(storage.getItem(APPEARANCE_STORAGE_KEY), "light");
  assert.equal(saveAppearance("system", storage), false);
  assert.equal(saveAppearance("dark"), false, "missing browser storage is reported rather than claimed as persisted");
});

test("applying appearance updates the document theme before render", () => {
  let themeColor = null;
  const root = { dataset: {}, style: {}, ownerDocument: { querySelector: () => ({ setAttribute(name, value) { if (name === "content") themeColor = value; } }) } };
  applyAppearance(root, "dark");
  assert.equal(root.dataset.appearance, "dark");
  assert.equal(root.dataset.theme, "dark");
  assert.equal(root.style.colorScheme, "dark");
  assert.equal(themeColor, "#181818");
});
