import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { test } from "node:test";

// Supply an explicitly built adapter path after the implementation test deferral ends.
const binary = process.env.NEOBABYLON_DISABLED_MCP_BINARY;
if (!binary) throw new Error("Set NEOBABYLON_DISABLED_MCP_BINARY to the built adapter path.");

async function exchange(input) {
  const environment = Object.fromEntries(
    ["SystemRoot", "WINDIR", "PATH", "DOTNET_ROOT", "TEMP", "TMP"]
      .filter((name) => process.env[name] !== undefined)
      .map((name) => [name, process.env[name]]),
  );
  const child = spawn(binary, [], {
    shell: false,
    stdio: ["pipe", "pipe", "pipe"],
    env: environment,
    windowsHide: true,
  });
  let stdout = "";
  let stderr = "";
  let captureFailure = null;
  const capture = (target, chunk) => {
    if (Buffer.byteLength(target, "utf8") + chunk.length > 32_768) {
      captureFailure = new Error("Adapter output exceeded QA capture bound.");
      child.kill();
      return target;
    }
    return target + chunk.toString("utf8");
  };
  child.stdout.on("data", (chunk) => { stdout = capture(stdout, chunk); });
  child.stderr.on("data", (chunk) => { stderr = capture(stderr, chunk); });
  child.stdin.on("error", () => {}); // Early fail-closed exit may close the pipe.
  let timedOut = false;
  const timeout = setTimeout(() => { timedOut = true; child.kill(); }, 5_000);
  try {
    child.stdin.end(input);
    const exitCode = await new Promise((resolve, reject) => {
      child.once("error", reject);
      child.once("close", resolve);
    });
    if (captureFailure) throw captureFailure;
    if (timedOut) throw new Error("Adapter did not close within the QA deadline.");
    return { exitCode, stdout, stderr,
      messages: stdout.trim() ? stdout.trim().split(/\r?\n/).map(JSON.parse) : [] };
  } finally {
    clearTimeout(timeout);
    if (child.exitCode === null) child.kill();
  }
}

const initialize = JSON.stringify({
  jsonrpc: "2.0", id: 1, method: "initialize",
  params: { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "qa", version: "1" } },
});

test("disabled adapter advertises no tools and denies direct calls with a typed result", async () => {
  const result = await exchange([
    initialize,
    JSON.stringify({ jsonrpc: "2.0", method: "notifications/initialized" }),
    JSON.stringify({ jsonrpc: "2.0", id: 2, method: "tools/list", params: {} }),
    JSON.stringify({ jsonrpc: "2.0", id: 3, method: "tools/call", params: { name: "candidate", arguments: {} } }),
    "",
  ].join("\n"));
  assert.equal(result.exitCode, 0);
  assert.equal(result.messages.length, 3);
  assert.equal(result.messages[0].result.protocolVersion, "2025-06-18");
  assert.deepEqual(result.messages[1].result.tools, []);
  assert.equal(result.messages[2].result.isError, true);
  assert.deepEqual(result.messages[2].result.structuredContent,
    { status: "disabled", code: "NEOBABYLON_TOOL_DISABLED" });
});

test("EOF processes a final complete JSON request without a trailing newline", async () => {
  const result = await exchange(`${initialize}\n${JSON.stringify({ jsonrpc: "2.0", id: "last", method: "tools/list" })}`);
  assert.equal(result.exitCode, 0);
  assert.equal(result.messages.length, 2);
  assert.equal(result.messages[1].id, "last");
  assert.deepEqual(result.messages[1].result.tools, []);
});

test("a direct call before initialization is still a disabled denial", async () => {
  const result = await exchange(`${JSON.stringify({
    jsonrpc: "2.0", id: 7, method: "tools/call",
    params: { name: "anything", arguments: { command: "never-run" } },
  })}\n`);
  assert.equal(result.exitCode, 0);
  assert.equal(result.messages.length, 1);
  assert.equal(result.messages[0].result.structuredContent.code, "NEOBABYLON_TOOL_DISABLED");
  assert.equal(result.messages[0].result.isError, true);
});

test("oversize input fails closed without responding with tool data", async () => {
  const result = await exchange("x".repeat(65_537));
  assert.notEqual(result.exitCode, 0);
  assert.deepEqual(result.messages, []);
});
