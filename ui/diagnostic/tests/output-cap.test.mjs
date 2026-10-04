import test from "node:test";
import assert from "node:assert/strict";
import { completionLimit, parseOutputOverride } from "../src/output-cap.mjs";

test("known advertised completion max is the default and bounds one-turn overrides", () => {
  const limit = completionLimit({ state: "Known", value: 524288 });
  assert.deepEqual(limit, { state: "Known", defaultTokens: 524288, maximumTokens: 524288 });
  assert.deepEqual(parseOutputOverride("", limit), { value: null, error: null });
  assert.deepEqual(parseOutputOverride("8192", limit), { value: 8192, error: null });
  assert.match(parseOutputOverride("524289", limit).error, /advertised maximum/i);
});

test("unknown advertised maximum keeps the explicit 32768 default distinct from an override", () => {
  const limit = completionLimit({ state: "Unknown", value: null });
  assert.deepEqual(limit, { state: "Unknown", defaultTokens: 32768, maximumTokens: null });
  assert.deepEqual(parseOutputOverride("", limit), { value: null, error: null });
  assert.deepEqual(parseOutputOverride("40000", limit), { value: 40000, error: null });
});

test("invalid or noncanonical overrides fail instead of being clamped or replaced by the default", () => {
  const limit = completionLimit({ state: "Known", value: 524288 });
  for (const input of ["0", "-1", "1.5", " 5", "5 ", "03", "NaN", "2147483648"]) {
    assert.notEqual(parseOutputOverride(input, limit).error, null, input);
  }
  const invalidCapability = completionLimit({ state: "Known", value: "524288" });
  assert.equal(invalidCapability.state, "Invalid");
  assert.notEqual(parseOutputOverride("", invalidCapability).error, null);
});
