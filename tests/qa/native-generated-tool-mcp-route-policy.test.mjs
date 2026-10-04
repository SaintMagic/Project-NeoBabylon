// Prepared-only focused contract tests. Do not execute until Task 7 test deferral lifts.
import test from "node:test";
import assert from "node:assert/strict";
import {
  assertThreadSelection, assertNamedStatus, assertDisabledDenial,
  assertPinnedOpenRouterConfig,
} from "./native-generated-tool-mcp-route-policy.mjs";

const server = "task7_private";
const model = "stealth/space-bunny-alpha";

test("effective thread model/provider mismatch rejects before a live turn", () => {
  const response = { thread: { id: "t1" }, model, modelProvider: "openrouter" };
  assert.equal(assertThreadSelection(response, model, "openrouter"), "t1");
  assert.throws(() => assertThreadSelection({ ...response, modelProvider: "other" }, model, "openrouter"));
  assert.throws(() => assertThreadSelection({ ...response, model: "fallback" }, model, "openrouter"));
  assert.throws(() => assertThreadSelection({ thread: { id: "t1" } }, model, "openrouter"));
});

test("status rejects missing, malformed, paginated, or ambiguously duplicated inventory", () => {
  const connected = { name: server, runtimeStatus: "connected", tools: { echo_reviewed: { name: "echo_reviewed" } }, toolsError: null };
  const disabled = { name: server, runtimeStatus: "disabled", tools: {}, toolsError: null };
  assertNamedStatus({ data: [connected], nextCursor: null }, server, true);
  assertNamedStatus({ data: [disabled], nextCursor: null }, server, false);
  assert.throws(() => assertNamedStatus({ data: [{ ...connected,
    tools: { ...connected.tools, unexpected: { name: "unexpected" } } }], nextCursor: null }, server, true));
  for (const invalid of [null, {}, { data: null }, { data: [], nextCursor: null },
    { data: [connected], nextCursor: "more" }, { data: [connected, connected], nextCursor: null },
    { data: [connected], nextCursor: null },
    { data: [{ ...disabled, runtimeStatus: null }], nextCursor: null },
    { data: [{ ...disabled, tools: { echo_reviewed: {} } }], nextCursor: null }]) {
    assert.throws(() => assertNamedStatus(invalid, server, false));
  }
});

test("only the exact disabled-server error is accepted as direct-call denial", () => {
  assertDisabledDenial({ error: { code: -32603, message: "unknown MCP server 'task7_private'" } }, server);
  for (const response of [{}, { error: { code: -32603, message: "thread not found" } },
    { error: { code: -32603, message: "unknown MCP server 'other'" } },
    { error: { code: -32602, message: "unknown MCP server 'task7_private'" } }]) {
    assert.throws(() => assertDisabledDenial(response, server));
  }
});

test("pinned route config rejects loopback endpoint and non-Responses wire API", () => {
  const selected = { baseUrl: "https://openrouter.ai/api/v1", wireApi: "responses", endpointTag: "stealth" };
  assert.deepEqual(assertPinnedOpenRouterConfig(selected), selected);
  assert.throws(() => assertPinnedOpenRouterConfig({ ...selected, baseUrl: "http://127.0.0.1:1234/v1" }));
  assert.throws(() => assertPinnedOpenRouterConfig({ ...selected, wireApi: "chat" }));
  assert.throws(() => assertPinnedOpenRouterConfig({ ...selected, endpointTag: "other" }));
});
