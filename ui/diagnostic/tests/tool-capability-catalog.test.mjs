import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { buildCapabilityCatalogView, filterCapabilityCatalog, selectedToolQualifications } from "../src/tool-capability-catalog.mjs";

const readJson = (relativePath) => JSON.parse(readFileSync(new URL(relativePath, import.meta.url), "utf8"));
const catalog = readJson("../../../docs/release/NEOBABYLON_TOOL_CAPABILITY_CATALOG.json");
const nex = readJson("../../../docs/release/MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json");
const lmStudio = readJson("../../../docs/release/MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json");
const nvidiaGlm = readJson("../../../docs/release/MODEL_CAPABILITY_NVIDIA_GLM_5_3.json");

function item(view, id) {
  for (const category of view) {
    const found = category.items.find((candidate) => candidate.id === id);
    if (found) return found;
  }
  assert.fail(`catalog item ${id} was not found`);
}

test("exact NEX operation evidence qualifies only the two tested tools", () => {
  const view = buildCapabilityCatalogView(catalog, nex);

  assert.equal(item(view, "exec-command").qualification.state, "qualified");
  assert.equal(item(view, "apply-patch").qualification.state, "qualified");
  assert.equal(item(view, "view-image").qualification.state, "advertised");
  assert.equal(item(view, "browser-computer-use").qualification.state, "unknown");
  assert.equal(item(view, "mcp-setup").exposure.state, "not-exposed");
});

test("LM Studio's generic tool-use metadata does not qualify apply_patch or vision", () => {
  const view = buildCapabilityCatalogView(catalog, lmStudio);

  assert.equal(item(view, "exec-command").qualification.state, "qualified");
  assert.equal(item(view, "apply-patch").qualification.state, "unknown");
  assert.equal(item(view, "view-image").qualification.state, "unsupported");
});

test("generic tool-use metadata is not mislabeled as an exact operation qualification", () => {
  const withoutTupleEvidence = {
    providerId: "test-provider",
    modelIdentifier: "test-model",
    toolFunctionCalling: { state: "Known", value: "tool_use", evidenceSource: "metadata only" },
  };
  const view = buildCapabilityCatalogView(catalog, withoutTupleEvidence);

  assert.equal(item(view, "exec-command").qualification.state, "advertised");
  assert.equal(item(view, "apply-patch").qualification.state, "unknown");
  assert.equal(item(buildCapabilityCatalogView(catalog, null), "exec-command").qualification.state, "unknown");
});

test("advertised NVIDIA tool calling has no selected-tuple qualification without operation evidence", () => {
  assert.equal(nvidiaGlm.toolFunctionCalling.state, "Known");
  assert.deepEqual(selectedToolQualifications(nvidiaGlm), []);
  assert.deepEqual(selectedToolQualifications({ toolFunctionCalling: { state: "Known", value: "advertised" } }), []);
  assert.deepEqual(selectedToolQualifications(null), []);
});

test("selected-tuple summary retains per-operation states and does not turn conflicts into support", () => {
  assert.deepEqual(selectedToolQualifications(nex).map(({ operationId, state }) => ({ operationId, state })), [
    { operationId: "exec_command", state: "qualified" },
    { operationId: "apply_patch", state: "qualified" },
    { operationId: "view_image", state: "advertised" },
  ]);
  const conflicting = {
    toolFunctionCalling: { state: "Known", value: "advertised" },
    toolQualifications: [
      { operationId: "exec_command", state: "qualified" },
      { operationId: "exec_command", state: "blocked" },
    ],
  };
  assert.equal(selectedToolQualifications(conflicting)[0].state, "unknown");
});

test("an explicit blocked operation stays blocked instead of inheriting generic support", () => {
  const blockedTuple = {
    ...nex,
    toolQualifications: [{ operationId: "exec_command", state: "blocked", evidenceSource: "deterministic denial fixture" }],
  };

  assert.equal(item(buildCapabilityCatalogView(catalog, blockedTuple), "exec-command").qualification.state, "blocked");
});

test("search is case-insensitive and retains every category when the query is empty", () => {
  const view = buildCapabilityCatalogView(catalog, null);

  assert.equal(filterCapabilityCatalog(view, "").length, 13);
  assert.deepEqual(filterCapabilityCatalog(view, "mcp").map((category) => category.id), ["built-in-tools", "mcp"]);
  assert.deepEqual(filterCapabilityCatalog(view, "APPLY_PATCH").map((category) => category.id), ["patch"]);
  assert.deepEqual(filterCapabilityCatalog(view, "no-such-category"), []);
});
