import test from "node:test";
import assert from "node:assert/strict";

const modal = await import("../src/modal-focus.mjs");

function makeControl(name, options = {}, focused = []) {
  return {
    name,
    disabled: options.disabled ?? false,
    hidden: options.hidden ?? false,
    tabIndex: options.tabIndex ?? 0,
    focus: () => focused.push(name),
    getAttribute: (attribute) => attribute === "aria-hidden" ? options.ariaHidden ?? null : null,
    closest: (selector) => options.inertAncestor && selector.includes("inert")
      || options.hiddenAncestor && selector.includes("aria-hidden") ? {} : null,
    getClientRects: () => options.visible === false ? [] : [{}],
  };
}

function makeDialog(controls, focused = []) {
  return {
    querySelectorAll: () => controls,
    contains: (target) => controls.includes(target),
    focus: () => focused.push("dialog"),
  };
}

function keyEvent(key, target, shiftKey = false) {
  return { key, target, shiftKey, prevented: false, preventDefault() { this.prevented = true; } };
}

test("dialog traps Tab using only visible, enabled focusable controls and closes on Escape", () => {
  const focused = [];
  const first = makeControl("first", {}, focused);
  const middle = makeControl("middle", {}, focused);
  const last = makeControl("last", {}, focused);
  const controls = [first, makeControl("disabled", { disabled: true }, focused),
    makeControl("hidden", { hidden: true }, focused), makeControl("aria-hidden", { ariaHidden: "true" }, focused),
    makeControl("inert-parent", { inertAncestor: true }, focused), makeControl("offscreen", { visible: false }, focused),
    makeControl("tabindex-minus-one", { tabIndex: -1 }, focused), middle, last];
  const dialog = makeDialog(controls);
  let closeCount = 0;
  const forward = keyEvent("Tab", last);
  const backward = keyEvent("Tab", first, true);
  const inside = keyEvent("Tab", middle);
  const escape = keyEvent("Escape", middle);

  modal.handleDialogKeyDown(forward, dialog, () => { closeCount++; });
  modal.handleDialogKeyDown(backward, dialog, () => { closeCount++; });
  modal.handleDialogKeyDown(inside, dialog, () => { closeCount++; });
  modal.handleDialogKeyDown(escape, dialog, () => { closeCount++; });

  assert.deepEqual(focused, ["first", "last"]);
  assert.equal(forward.prevented, true);
  assert.equal(backward.prevented, true);
  assert.equal(inside.prevented, false);
  assert.equal(escape.prevented, true);
  assert.equal(closeCount, 1);
});

test("dialog recovers Tab when focus is outside and handles a dialog with no controls", () => {
  const focused = [];
  const first = makeControl("first", {}, focused);
  const last = makeControl("last", {}, focused);
  const dialog = makeDialog([first, last], focused);
  const outsideForward = keyEvent("Tab", {});
  const outsideBackward = keyEvent("Tab", {}, true);

  modal.handleDialogKeyDown(outsideForward, dialog, () => {});
  modal.handleDialogKeyDown(outsideBackward, dialog, () => {});
  assert.deepEqual(focused, ["first", "last"]);

  const emptyDialog = makeDialog([], focused);
  const emptyTab = keyEvent("Tab", emptyDialog);
  modal.handleDialogKeyDown(emptyTab, emptyDialog, () => {});
  assert.equal(emptyTab.prevented, true);
  assert.equal(focused.at(-1), "dialog");
});
