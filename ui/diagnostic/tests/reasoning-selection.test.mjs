import test from "node:test";
import assert from "node:assert/strict";
import { initialReasoningSelection, reasoningOptions, parseReasoningSelection, chooseReasoningEffort } from "../src/reasoning-selection.mjs";

const record = (model, efforts, defaultEffort, state = "Known") => ({
  providerId: "fixture-provider", modelIdentifier: model,
  reasoningControls: { state, value: { supported_efforts: efforts, default_effort: defaultEffort }, evidenceSource: "fixture capability" },
});
const glm = record("glm", ["low", "high", "max"], "max");
const kimi = record("kimi", ["low", "high", "max"], null);

test("known exact default is sent without inventing intermediate reasoning levels", () => {
  assert.deepEqual(reasoningOptions(glm).efforts, ["low", "high", "max"]);
  assert.deepEqual(parseReasoningSelection(glm, initialReasoningSelection(glm)).wire, { reasoningEffort: "max" });
  const selected = chooseReasoningEffort(glm, "low");
  assert.deepEqual(parseReasoningSelection(glm, selected).wire, { reasoningEffort: "low" });
  assert.throws(() => chooseReasoningEffort(glm, "medium"), /supported/);
});

test("Unknown default omits an override until an actual effort is selected; null is not a reset action", () => {
  assert.equal(reasoningOptions(kimi).defaultEffort, null);
  assert.deepEqual(parseReasoningSelection(kimi, initialReasoningSelection(kimi)).wire, {});
  assert.deepEqual(parseReasoningSelection(kimi, chooseReasoningEffort(kimi, "high")).wire, { reasoningEffort: "high" });
  for (const clearing of [null, undefined, "", "provider-default"]) assert.throws(() => chooseReasoningEffort(kimi, clearing), /supported/);
});

test("a capability change resets the selection to its exact default or no override", () => {
  const old = chooseReasoningEffort(glm, "low");
  assert.deepEqual(parseReasoningSelection(kimi, old).wire, {});
  assert.deepEqual(parseReasoningSelection(glm, chooseReasoningEffort(kimi, "low")).wire, { reasoningEffort: "max" });
});

test("a new same-tuple record exposes only its own advertised efforts after explicit record adoption", () => {
  const old = record("space-bunny", null, null, "Unknown");
  const current = record("space-bunny", ["max", "xhigh", "high", "medium", "low"], "max");
  current.reasoningControls.value.mandatory = true;
  assert.equal(reasoningOptions(old).known, false);
  assert.deepEqual(reasoningOptions(current).efforts, ["max", "xhigh", "high", "medium", "low"]);
  assert.deepEqual(parseReasoningSelection(current, initialReasoningSelection(old)).wire, { reasoningEffort: "max" });
  assert.deepEqual(parseReasoningSelection(old, chooseReasoningEffort(current, "xhigh")).wire, {});
  for (const unsupported of ["off", "none"]) assert.throws(() => chooseReasoningEffort(current, unsupported), /supported/);
});

test("advertised Boolean reasoning stays Known but cannot send an invented Codex effort or override", () => {
  const ling = { providerId: "fixture-provider", modelIdentifier: "ling", reasoningControls: {
    state: "Known", value: { control_type: "enabled", mandatory: false, default_enabled: true }, evidenceSource: "public provider metadata fixture",
  } };
  const options = reasoningOptions(ling);
  assert.equal(options.known, true);
  assert.equal(options.controlType, "enabled");
  assert.equal(options.defaultEnabled, true);
  assert.equal(options.canOverride, false);
  assert.equal(options.reason, "Provider supports On/Off; pinned Codex runtime has no Boolean reasoning override");
  assert.deepEqual(options.efforts, []);
  assert.deepEqual(parseReasoningSelection(ling, { ...initialReasoningSelection(ling), effort: "medium" }).wire, {});
  for (const invented of ["none", "medium", "on", "off", true, false]) assert.throws(() => chooseReasoningEffort(ling, invented), /supported/);
});

test("Unknown controls cannot transmit stale or numeric provider reasoning settings", () => {
  const unknown = record("ling", ["low", "high"], "high", "Unknown");
  const options = reasoningOptions(unknown);
  assert.equal(options.known, false);
  assert.deepEqual(options.efforts, []);
  assert.deepEqual(parseReasoningSelection(unknown, chooseReasoningEffort(glm, "high")).wire, {});
  for (const efforts of [[1, 100], ["100"], ["HIGH"], ["low", "low"], ["vendor-level"], []]) {
    const malformed = record("bad", efforts, null);
    assert.equal(reasoningOptions(malformed).known, false);
    assert.deepEqual(parseReasoningSelection(malformed, initialReasoningSelection(malformed)).wire, {});
  }
});

test("unsupported current settings fail closed rather than serializing a stale effort", () => {
  const selection = { ...initialReasoningSelection(glm), effort: "medium" };
  const parsed = parseReasoningSelection(glm, selection);
  assert.match(parsed.error, /supported/);
  assert.deepEqual(parsed.wire, {});
  assert.equal(reasoningOptions(record("bad-default", ["low"], "high")).known, false);
});

test("every offered pinned string is literal, not case-normalized or numerically mapped", () => {
  const all = record("pinned", ["none", "minimal", "low", "medium", "high", "xhigh", "max", "ultra", "persistent"], "none");
  for (const effort of all.reasoningControls.value.supported_efforts) {
    assert.deepEqual(parseReasoningSelection(all, chooseReasoningEffort(all, effort)).wire, { reasoningEffort: effort });
  }
});
