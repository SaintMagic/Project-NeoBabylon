import assert from "node:assert/strict";
import test from "node:test";
import { hideFullAccessNotice, isFullAccessNoticeHidden, fullAccessNoticeStorageKey } from "../src/full-access-notice.mjs";

function storage(initial = {}) {
  const values = new Map(Object.entries(initial));
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
  };
}

test("full-access notice is visible until explicitly hidden", () => {
  assert.equal(isFullAccessNoticeHidden(storage()), false);
  assert.equal(isFullAccessNoticeHidden(storage({ [fullAccessNoticeStorageKey]: "0" })), false);
  assert.equal(isFullAccessNoticeHidden(storage({ [fullAccessNoticeStorageKey]: "1" })), true);
});

test("don't show again persists only the notice preference", () => {
  const local = storage();
  assert.equal(hideFullAccessNotice(local), true);
  assert.equal(isFullAccessNoticeHidden(local), true);
  assert.equal(isFullAccessNoticeHidden(storage()), false);
});

test("blocked storage fails visible and never claims persistence", () => {
  const blocked = {
    getItem: () => { throw new Error("storage unavailable"); },
    setItem: () => { throw new Error("storage unavailable"); },
  };
  assert.equal(isFullAccessNoticeHidden(blocked), false);
  assert.equal(hideFullAccessNotice(blocked), false);
});
