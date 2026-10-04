// QA-only, inert stdio MCP peer. It echoes a fixed marker; it never executes a candidate.
import { appendFileSync } from "node:fs";

const marker = "TASK7_NAMED_MCP_ECHO_OK";
const logPath = process.env.NB_TASK7_MCP_LOG;
if (!logPath) process.exit(2);
let buffer = "";
let count = 0;

function record(value) {
  // Never record environment values, request bodies, or credentials.
  appendFileSync(logPath, `${JSON.stringify(value)}\n`, { encoding: "utf8" });
}

function reply(id, value) {
  const line = JSON.stringify({ jsonrpc: "2.0", id, ...value });
  if (Buffer.byteLength(line) > 16384) throw new Error("MCP output bound exceeded");
  process.stdout.write(`${line}\n`);
}

record({ event: "start", pid: process.pid, keyPresent: Boolean(process.env.OPENROUTER_API_KEY) });
process.stdin.setEncoding("utf8");
process.stdin.on("data", (chunk) => {
  buffer += chunk;
  if (Buffer.byteLength(buffer) > 65536) throw new Error("MCP input bound exceeded");
  for (;;) {
    const end = buffer.indexOf("\n");
    if (end < 0) break;
    const line = buffer.slice(0, end);
    buffer = buffer.slice(end + 1);
    if (++count > 32) throw new Error("MCP request count bound exceeded");
    const request = JSON.parse(line);
    const method = request.method;
    if (method === "notifications/initialized" || method === "initialized") continue;
    record({ event: "request", method, keyPresent: Boolean(process.env.OPENROUTER_API_KEY) });
    if (request.id === undefined) continue;
    if (method === "initialize") {
      reply(request.id, { result: { protocolVersion: "2024-11-05", capabilities: { tools: {} }, serverInfo: { name: "task7-qa-echo", version: "1" } } });
    } else if (method === "tools/list") {
      reply(request.id, { result: { tools: [{ name: "echo_reviewed", description: "QA transport marker only", inputSchema: { type: "object", properties: { value: { type: "string", const: "probe" } }, required: ["value"], additionalProperties: false } }] } });
    } else if (method === "tools/call" && request.params?.name === "echo_reviewed" && request.params?.arguments?.value === "probe") {
      record({ event: "call", name: "echo_reviewed" });
      reply(request.id, { result: { content: [{ type: "text", text: marker }], structuredContent: { marker }, isError: false } });
    } else {
      reply(request.id, { error: { code: -32601, message: "QA fixture method or arguments denied" } });
    }
  }
});
process.stdin.on("end", () => { record({ event: "eof" }); process.exit(0); });
