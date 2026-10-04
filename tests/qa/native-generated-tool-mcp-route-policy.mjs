// Pure QA assertions shared by the prepared fixture and its focused tests.
function requireTrue(value, message) { if (!value) throw new Error(message); }

export function assertThreadSelection(result, expectedModel, expectedProvider) {
  requireTrue(typeof result?.thread?.id === "string" && result.thread.id.length > 0, "missing effective thread ID");
  requireTrue(result.model === expectedModel, "effective App Server model differs from selected tuple");
  requireTrue(result.modelProvider === expectedProvider, "effective App Server model provider differs from selected tuple");
  return result.thread.id;
}

export function assertNamedStatus(status, server, enabled) {
  requireTrue(Array.isArray(status?.data) && status.nextCursor === null, "malformed or paginated MCP status");
  const matches = status.data.filter((item) => item?.name === server);
  requireTrue(matches.length === 1, "named MCP server missing or duplicated in status");
  const item = matches[0];
  requireTrue(item.tools && typeof item.tools === "object" && !Array.isArray(item.tools), "malformed MCP tool inventory");
  requireTrue(item.toolsError === null, "MCP tool inventory error");
  if (enabled) {
    requireTrue(item.runtimeStatus === "connected" &&
      item.tools.echo_reviewed?.name === "echo_reviewed" && Object.keys(item.tools).length === 1,
    "named MCP tool not exclusively connected");
  } else {
    requireTrue(item.runtimeStatus === "disabled" && Object.keys(item.tools).length === 0,
      "named MCP server is not demonstrably disabled");
  }
  return item;
}

export function assertDisabledDenial(response, server) {
  requireTrue(response?.error?.code === -32603 &&
    response.error.message === `unknown MCP server '${server}'` && response.result === undefined,
  "direct call failed for another reason or unexpectedly succeeded");
  return response.error;
}

// Pinned model-provider-info/src/lib.rs:230 rejects an endpoint tag on any
// base URL other than the canonical direct OpenRouter Responses endpoint.
export function assertPinnedOpenRouterConfig(config) {
  requireTrue(config?.baseUrl === "https://openrouter.ai/api/v1" &&
    config.wireApi === "responses" && config.endpointTag === "stealth",
  "pinned runtime rejects the selected OpenRouter route on this provider config");
  return config;
}
