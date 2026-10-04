import test from "node:test";
import assert from "node:assert/strict";
import { nextListboxIndex } from "../src/model-picker.mjs";

test("capability list supports wrapped arrows and Home/End navigation", () => {
  assert.equal(nextListboxIndex("ArrowDown", 0, 3), 1);
  assert.equal(nextListboxIndex("ArrowDown", 2, 3), 0);
  assert.equal(nextListboxIndex("ArrowUp", 0, 3), 2);
  assert.equal(nextListboxIndex("ArrowUp", 1, 3), 0);
  assert.equal(nextListboxIndex("Home", 2, 3), 0);
  assert.equal(nextListboxIndex("End", 0, 3), 2);
});

test("capability list ignores unsupported keys and empty lists", () => {
  assert.equal(nextListboxIndex("Enter", 0, 3), null);
  assert.equal(nextListboxIndex("ArrowDown", 0, 0), -1);
  assert.equal(nextListboxIndex("Home", 0, 0), -1);
});
