// PREPARED ONLY. Do not run before Task 7 implementation/test deferral is lifted.
// QA transport probe, not product approval or a generated-candidate activation route.
// Replays .local/Lab/Runs/P4-Route-Probe-5a362283d7f6/probe.py against the
// CURRENT lock, then offers a separately opt-in exact-tuple live probe.
import { createHash, randomUUID } from "node:crypto";
import { createReadStream, mkdirSync, readFileSync, writeFileSync, openSync, closeSync } from "node:fs";
import { createServer } from "node:http";
import { spawn } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { newQaRun } from "./qa-run-root.mjs";
import { assertThreadSelection, assertNamedStatus, assertDisabledDenial,
  assertPinnedOpenRouterConfig } from "./native-generated-tool-mcp-route-policy.mjs";

const SOURCE = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const LOCK = path.join(SOURCE, "runtime/runtime-lock.json");
const BINARY = path.join(SOURCE, ".local/Runtime/LockedBuild/codex-app-server.exe");
const MCP = path.join(SOURCE, "tests/qa/native-generated-tool-mcp-echo.mjs");
const EXPECTED_SHA = "5a48fe628c0654971da58542b65e33ac64cc7767e889d580fddb34c527fa807b";
const MODEL = "stealth/space-bunny-alpha";
const PROVIDER = "stealth";
const LIVE_PROVIDER = Object.freeze({ baseUrl: "https://openrouter.ai/api/v1", wireApi: "responses", endpointTag: PROVIDER });
const SERVER = "task7_private";
const TOOL = `mcp__${SERVER}`;
const MARKER = "TASK7_NAMED_MCP_ECHO_OK";
const TOKEN_CAP = 512;
const DEADLINE_MS = 300000;
const RPC_MS = 15000;
const TURN_MS = 60000;
const MAX_EVIDENCE = 4 * 1024 * 1024;
const flags = new Set(process.argv.slice(2));
const mode = flags.has("--deterministic") ? "deterministic" : flags.has("--live") ? "live" : null;
if (flags.size !== process.argv.length - 2 ||
    (mode === "deterministic" && (flags.size !== 1)) ||
    (mode === "live" && (flags.size !== 2 || !flags.has("--ack-external-request"))) || !mode) {
  process.stderr.write("Usage (later, only when authorized): --deterministic | --live --ack-external-request\n");
  process.exitCode = 2;
} else {
  await main();
}

function requireTrue(value, message) { if (!value) throw new Error(message); }
function toml(value) { return JSON.stringify(String(value).replaceAll("\\", "/")); }
function bounded(promise, ms, label, signal) {
  let timer; let rejectBound;
  const boundary = new Promise((_, reject) => { rejectBound = reject; timer = setTimeout(() => reject(new Error(`${label} timeout`)), ms); });
  const onAbort = () => rejectBound(signal.reason ?? new Error(`${label} aborted`));
  if (signal?.aborted) onAbort();
  else signal?.addEventListener("abort", onAbort, { once: true });
  return Promise.race([promise, boundary]).finally(() => {
    clearTimeout(timer); signal?.removeEventListener("abort", onAbort);
  });
}
function scrub(value, secret) {
  let text = JSON.stringify(value);
  if (secret) text = text.replaceAll(secret, "[REDACTED]");
  return text.replace(/Bearer\s+[^\s"\\]+/gi, "Bearer [REDACTED]");
}
function evidence(root, name, data, secret = null) {
  const text = scrub(data, secret);
  requireTrue(Buffer.byteLength(text) <= MAX_EVIDENCE, `${name} evidence bound exceeded`);
  writeFileSync(path.join(root, name), `${text}\n`, { encoding: "utf8", flag: "wx" });
}
async function fileSha(file) {
  const hash = createHash("sha256");
  for await (const chunk of createReadStream(file)) hash.update(chunk);
  return hash.digest("hex");
}
function lockIdentity() {
  const lock = JSON.parse(readFileSync(LOCK, "utf8"));
  requireTrue(lock.runtime?.sha256 === EXPECTED_SHA, "runtime lock hash drifted; update fixture only after review");
  requireTrue(path.resolve(SOURCE, lock.runtime?.appServerBinaryRelativePath ?? "") === BINARY, "runtime lock path drifted");
  requireTrue(lock.runtime?.version === "0.155.1", "runtime lock version drifted");
  return lock;
}
function catalog(home) {
  // Same one-model catalog shape as the documented isolated probe; no ignored artifact dependency.
  const template = {
    slug: MODEL, display_name: `OpenRouter ${MODEL}`, description: "Task7 QA tuple only",
    default_reasoning_level: null, supported_reasoning_levels: [], shell_type: "unified_exec",
    visibility: "none", supported_in_api: true, priority: 99,
    additional_speed_tiers: [], service_tiers: [], default_service_tier: null,
    availability_nux: null, upgrade: null, model_messages: null,
    base_instructions: "Use only presented tools. Report the exact QA marker returned by the named echo tool.",
    include_skills_usage_instructions: false, include_plugin_usage_instructions: false,
    include_apps_usage_instructions: false, supports_reasoning_summary_parameter: false,
    default_reasoning_summary: "none", support_verbosity: false, default_verbosity: null,
    apply_patch_tool_type: null, web_search_tool_type: "text",
    truncation_policy: { mode: "bytes", limit: 10000 },
    supports_image_detail_original: false, context_window: 1000000, max_context_window: 1000000,
    auto_compact_token_limit: null, comp_hash: null, effective_context_window_percent: 95,
    experimental_supported_tools: [], input_modalities: ["text"], supports_search_tool: false,
    supports_experimental_context: false, use_responses_lite: false,
    node_repl_auto_review_required: false, node_repl_disabled: true, auto_review_model_override: null,
    model_specialty: null, tool_mode: null, multi_agent_version: null, multi_agent_reasoning_effort: null,
  };
  const file = path.join(home, "model-catalog.json");
  writeFileSync(file, JSON.stringify({ models: [template] }), { flag: "wx" });
  return file;
}
function writeConfig(state, enabled) {
  if (enabled) requireTrue(!state.abort.signal.aborted, "qualification sequence aborted");
  const { home, work, mcpLog, port, live } = state;
  if (live) assertPinnedOpenRouterConfig(LIVE_PROVIDER);
  const model = live ? MODEL : "mock-model";
  const provider = live ? "openrouter" : "task7_loopback";
  const catalogLine = live ? `model_catalog_json = ${toml(state.catalog)}\nmodel_context_window = 1000000\n` : "";
  const providerConfig = live ? `
[model_providers.openrouter]
name = "OpenRouter"
base_url = ${toml(LIVE_PROVIDER.baseUrl)}
wire_api = ${toml(LIVE_PROVIDER.wireApi)}
env_key = "OPENROUTER_API_KEY"
requires_openai_auth = false
request_max_retries = 0
stream_max_retries = 0
openrouter_provider_endpoint = ${toml(LIVE_PROVIDER.endpointTag)}
http_headers = { "X-OpenRouter-Metadata" = "enabled" }

[shell_environment_policy]
exclude = ["OPENROUTER_API_KEY"]
experimental_use_profile = false
` : `
[model_providers.task7_loopback]
name = "Task7 deterministic loopback"
base_url = "http://127.0.0.1:${port}/v1"
wire_api = "responses"
requires_openai_auth = false
request_max_retries = 0
stream_max_retries = 0
`;
  const config = `model = ${toml(model)}
model_provider = ${toml(provider)}
approval_policy = "never"
default_permissions = ":danger-full-access"
sandbox_mode = "danger-full-access"
allow_login_shell = false
${catalogLine}${providerConfig}
[mcp_servers.${SERVER}]
command = ${toml(process.execPath)}
args = [${toml(MCP)}]
enabled = ${enabled}
enabled_tools = ["echo_reviewed"]
startup_timeout_sec = 10

[mcp_servers.${SERVER}.env]
NB_TASK7_MCP_LOG = ${toml(mcpLog)}

[projects.${toml(work)}]
trust_level = "trusted"
`;
  if (live) {
    // Keep this in step with pinned model-provider-info/src/lib.rs:230.
    requireTrue(config.includes('base_url = "https://openrouter.ai/api/v1"')
      && config.includes('wire_api = "responses"')
      && config.includes('openrouter_provider_endpoint = "stealth"'),
    "live config violates the pinned OpenRouter endpoint rule");
  }
  writeFileSync(path.join(home, "config.toml"), config, "utf8");
  state.configHistory.push({ enabled, sha256: createHash("sha256").update(config).digest("hex") });
}
function appEnvironment(state, secret) {
  const env = {};
  for (const name of ["SystemRoot", "WINDIR", "COMSPEC", "PATHEXT", "PATH"]) {
    if (process.env[name]) env[name] = process.env[name];
  }
  Object.assign(env, { CODEX_HOME: state.home, TEMP: state.temp, TMP: state.temp,
    USERPROFILE: state.profile, APPDATA: state.roaming, LOCALAPPDATA: state.localApp,
    HOMEDRIVE: path.parse(state.profile).root.replace(/[\\/]$/, ""),
    HOMEPATH: state.profile.slice(path.parse(state.profile).root.length - 1) });
  if (secret) env.OPENROUTER_API_KEY = secret;
  return env;
}
class AppClient {
  constructor(state, secret) {
    requireTrue(!state.abort.signal.aborted, "qualification sequence aborted before App Server launch");
    this.state = state; this.secret = secret; this.pending = new Map(); this.messages = [];
    this.waiters = []; this.buffer = ""; this.outputBytes = 0; this.nextId = 0;
    this.child = spawn(BINARY, [], { cwd: state.work, env: appEnvironment(state, secret),
      stdio: ["pipe", "pipe", "pipe"], windowsHide: true });
    state.ownedAppPids.push(this.child.pid ?? null);
    this.child.stdout.on("data", (chunk) => this.onData(chunk));
    this.child.stdin.on("error", (error) => this.failAll(error));
    // Do not retain stderr chunks: a key can straddle chunk boundaries.
    this.child.stderr.on("data", (chunk) => {
      this.state.stderrBytes += chunk.length;
      if (this.state.stderrBytes > MAX_EVIDENCE) this.abort(new Error("App Server stderr bound exceeded"));
    });
    state.abort.signal.addEventListener("abort", () => this.abort(new Error("qualification deadline aborted")), { once: true });
    this.child.on("error", (error) => this.failAll(error));
    this.child.on("exit", (code, signal) => {
      state.exits.push({ pid: this.child.pid ?? null, code, signal });
      this.failAll(new Error(`owned App Server exited: ${code}/${signal}`));
    });
  }
  record(value) {
    const line = scrub(value, this.secret);
    this.outputBytes += Buffer.byteLength(line);
    if (this.outputBytes > MAX_EVIDENCE) { this.abort(new Error("App Server output evidence bound exceeded")); return; }
    this.messages.push(JSON.parse(line));
    this.state.appTrace.push(JSON.parse(line));
    if (this.messages.length > 512) this.abort(new Error("App Server message count bound exceeded"));
  }
  failAll(error) {
    if (this.failure) return;
    this.failure = error;
    for (const pending of this.pending.values()) pending.reject(error);
    for (const waiter of this.waiters) waiter.reject(error);
    this.pending.clear(); this.waiters = [];
  }
  abort(error) {
    this.failAll(error);
    if (this.child.exitCode === null && this.child.signalCode === null) this.child.kill();
  }
  onData(chunk) {
    this.buffer += String(chunk);
    if (Buffer.byteLength(this.buffer) > 262144) { this.abort(new Error("App Server line bound exceeded")); return; }
    for (;;) {
      const end = this.buffer.indexOf("\n"); if (end < 0) break;
      const line = this.buffer.slice(0, end); this.buffer = this.buffer.slice(end + 1);
      let message;
      try { message = JSON.parse(line); } catch { this.abort(new Error("invalid App Server JSON")); return; }
      this.record(message);
      if (this.failure) return;
      const pending = this.pending.get(message.id);
      if (pending) { this.pending.delete(message.id); pending.resolve(message); }
      for (const waiter of [...this.waiters]) {
        if (waiter.match(message)) { this.waiters.splice(this.waiters.indexOf(waiter), 1); waiter.resolve(message); }
      }
    }
  }
  send(method, params) {
    requireTrue(!this.state.abort.signal.aborted && !this.failure, this.failure?.message ?? "qualification sequence aborted");
    const request = { jsonrpc: "2.0", id: ++this.nextId, method, ...(params === undefined ? {} : { params }) };
    const wire = `${JSON.stringify(request)}\n`;
    requireTrue(Buffer.byteLength(wire) < 65536, "App Server request bound exceeded");
    this.record({ channel: "request", ...request });
    requireTrue(!this.failure, this.failure?.message ?? "App Server evidence bound exceeded");
    return bounded(new Promise((resolve, reject) => {
      this.pending.set(request.id, { resolve, reject });
      this.child.stdin.write(wire, (error) => { if (error) { this.pending.delete(request.id); reject(error); } });
    }), RPC_MS, method, this.state.abort.signal).finally(() => this.pending.delete(request.id));
  }
  async checked(method, params) {
    const answer = await this.send(method, params);
    requireTrue(!answer.error && answer.result !== undefined, `${method} failed: ${scrub(answer.error ?? answer, this.secret)}`);
    return answer.result;
  }
  async start() {
    const init = await this.checked("initialize", { clientInfo: { name: "task7-route-qa", version: "1" }, capabilities: { experimentalApi: false } });
    this.state.initializations.push({ userAgent: init.userAgent ?? null });
    this.child.stdin.write('{"jsonrpc":"2.0","method":"initialized"}\n');
  }
  waitTurn(turnId) {
    const match = (m) => ["turn/completed", "turn/failed"].includes(m.method) && m.params?.turn?.id === turnId;
    const already = this.messages.find(match);
    if (already) return Promise.resolve(already);
    return bounded(new Promise((resolve, reject) => this.waiters.push({ match, resolve, reject })), TURN_MS, "turn", this.state.abort.signal);
  }
  async stop() {
    if (this.child.exitCode !== null || this.child.signalCode !== null) return;
    this.child.stdin.end();
    const exited = new Promise((resolve) => this.child.once("exit", resolve));
    try { await bounded(exited, 3000, "App Server EOF shutdown"); }
    catch {
      this.child.kill(); // Only this ChildProcess handle; never discover/kill by PID or parent PID.
      await bounded(exited, 3000, "owned App Server termination");
    }
  }
}
function sse(value) { return `event: ${value.type}\ndata: ${JSON.stringify(value)}\n\n`; }
function providerReply(index) {
  const id = `task7-fixture-${index}`;
  const item = index === 1
    ? { type: "function_call", call_id: "task7-call-1", namespace: TOOL, name: "echo_reviewed", arguments: JSON.stringify({ value: "probe" }) }
    : { type: "message", role: "assistant", id: `task7-message-${index}`, content: [{ type: "output_text", text: "QA fixture done" }] };
  return [
    { type: "response.created", response: { id } },
    { type: "response.output_item.done", item },
    { type: "response.completed", response: { id, usage: { input_tokens: 0, input_tokens_details: null, output_tokens: 0, output_tokens_details: null, total_tokens: 0 } } },
  ].map(sse).join("");
}
async function loopback(state) {
  const sockets = new Set();
  const server = createServer((req, res) => {
    if (req.method !== "POST" || req.url !== "/v1/responses" || state.requests.length >= 5 ||
        Number(req.headers["content-length"]) > 131072) { res.writeHead(400).end(); return; }
    let size = 0; const chunks = [];
    req.on("data", (chunk) => { size += chunk.length; if (size > 131072) req.destroy(); else chunks.push(chunk); });
    req.on("end", () => {
      try {
        const body = JSON.parse(Buffer.concat(chunks).toString("utf8"));
        state.requests.push(body); // Body only: never request headers or authorization.
        const response = providerReply(state.requests.length);
        res.writeHead(200, { "content-type": "text/event-stream", "content-length": Buffer.byteLength(response) });
        res.end(response);
      } catch { res.writeHead(400).end(); }
    });
  });
  server.on("connection", (socket) => { sockets.add(socket); socket.on("close", () => sockets.delete(socket)); });
  try {
    await bounded(new Promise((resolve, reject) => { server.once("error", reject); server.listen(0, "127.0.0.1", resolve); }),
      3000, "loopback listen", state.abort.signal);
  } catch (error) {
    if (server.listening) server.close(); else server.once("listening", () => server.close());
    throw error;
  }
  state.port = server.address().port;
  return async () => {
    const closed = new Promise((resolve) => server.close(resolve));
    for (const socket of sockets) socket.destroy(); // Only this fixture's accepted sockets.
    await bounded(closed, 3000, "loopback close");
  };
}
function noToolRequest(body) { return !body.tools?.some((tool) => tool.name === TOOL); }
async function thread(client, state) {
  const result = await client.checked("thread/start", { cwd: state.work, model: state.live ? MODEL : "mock-model",
    approvalPolicy: "never", sandbox: "danger-full-access", allowProviderModelFallback: false });
  return assertThreadSelection(result, state.live ? MODEL : "mock-model", state.live ? "openrouter" : "task7_loopback");
}
async function turn(client, threadId, prompt) {
  const result = await client.checked("turn/start", { threadId, input: [{ type: "text", text: prompt }],
    approvalPolicy: "never", sandboxPolicy: { type: "dangerFullAccess" }, maxOutputTokens: TOKEN_CAP });
  const done = await client.waitTurn(result.turn.id);
  requireTrue(done.method === "turn/completed", `turn failed: ${scrub(done)}`);
  return done;
}
async function denied(client, threadId) {
  const response = await client.send("mcpServer/tool/call", { threadId, server: SERVER, tool: "echo_reviewed", arguments: { value: "probe" } });
  return assertDisabledDenial(response, SERVER);
}
function mcpEvents(state) {
  const raw = readFileSync(state.mcpLog, "utf8");
  requireTrue(Buffer.byteLength(raw) <= 65536, "MCP log bound exceeded");
  return raw.trim().split("\n").map((line) => JSON.parse(line));
}
async function deterministic(state) {
  let closeLoopback;
  try {
    closeLoopback = await loopback(state);
    writeConfig(state, true);
    let app = state.app = new AppClient(state, null);
    await app.start();
    const id = await thread(app, state);
    assertNamedStatus(await app.checked("mcpServerStatus/list", { threadId: id, detail: "full" }), SERVER, true);
    await turn(app, id, "Call only the named QA echo tool with value probe; report its marker.");
    requireTrue(state.requests.length === 2, "expected function call plus continuation");
    requireTrue(state.requests[0].tools?.some((tool) => tool.name === TOOL), "named tool not advertised");
    requireTrue(JSON.stringify(state.requests[1].input).includes(MARKER), "MCP marker not bound into continuation");
    requireTrue(state.requests.every((request) => request.max_output_tokens === TOKEN_CAP), "request max_output_tokens mismatch");
    const events = mcpEvents(state);
    requireTrue(events.filter((event) => event.event === "call" && event.name === "echo_reviewed").length === 1, "expected exactly one MCP call");
    requireTrue(events.every((event) => !event.keyPresent), "credential reached QA MCP fixture");
    state.checks.push("named registration/call/response observed on current lock");

    writeConfig(state, false);
    await app.checked("config/mcpServer/reload");
    assertNamedStatus(await app.checked("mcpServerStatus/list", { threadId: id, detail: "full" }), SERVER, false);
    await denied(app, id);
    await turn(app, id, "QA after disable; do not call tools.");
    requireTrue(state.requests.length === 3 && noToolRequest(state.requests[2]), "existing-thread tool advertisement survived disable");
    state.checks.push("existing-thread reload disabled and direct call denied");
    await app.stop(); state.app = null;

    app = state.app = new AppClient(state, null);
    await app.start();
    const resumed = await app.checked("thread/resume", { threadId: id, excludeTurns: true,
      sandbox: "danger-full-access", approvalPolicy: "never" });
    requireTrue(resumed.thread.id === id, "resumed wrong thread");
    assertNamedStatus(await app.checked("mcpServerStatus/list", { threadId: id, detail: "full" }), SERVER, false);
    await denied(app, id);
    await turn(app, id, "QA resumed after revoke; do not call tools.");
    requireTrue(state.requests.length === 4 && noToolRequest(state.requests[3]), "resumed-thread tool advertisement survived");
    const fresh = await thread(app, state);
    assertNamedStatus(await app.checked("mcpServerStatus/list", { threadId: fresh, detail: "full" }), SERVER, false);
    await denied(app, fresh);
    await turn(app, fresh, "QA new thread after revoke; do not call tools.");
    requireTrue(state.requests.length === 5 && noToolRequest(state.requests[4]), "new-thread tool advertisement survived");
    requireTrue(state.requests.every((request) => request.max_output_tokens === TOKEN_CAP), "post-revoke max_output_tokens mismatch");
    state.checks.push("restart/resume/new-thread remained disabled");
  } finally { if (closeLoopback) await closeLoopback(); }
}
async function live(state, secret) {
  // Exact selected tuple only. This is an opt-in transport probe, not product activation.
  state.catalog = catalog(state.home);
  writeConfig(state, true);
  const app = state.app = new AppClient(state, secret);
  await app.start();
  const id = await thread(app, state);
  assertNamedStatus(await app.checked("mcpServerStatus/list", { threadId: id, detail: "full" }), SERVER, true);
  await turn(app, id, "For this bounded QA check, call only the named echo_reviewed tool with value probe, then report its exact returned marker. Do not use shell or other tools.");
  const events = mcpEvents(state);
  requireTrue(events.filter((event) => event.event === "call" && event.name === "echo_reviewed").length === 1,
    "live model did not make exactly one named call");
  requireTrue(events.every((event) => !event.keyPresent), "credential reached QA MCP fixture");
  requireTrue(JSON.stringify(state.appTrace).includes(MARKER), "live MCP marker not observed in App Server response");
  state.checks.push("live named MCP call observed; provider wire, request count, cost, and served endpoint not captured");
  writeConfig(state, false);
  await app.checked("config/mcpServer/reload");
  assertNamedStatus(await app.checked("mcpServerStatus/list", { threadId: id, detail: "full" }), SERVER, false);
  await denied(app, id);
  await turn(app, id, "QA post-disable model turn: do not call any tool; state only that no named tool is available.");
  requireTrue(mcpEvents(state).filter((event) => event.event === "call").length === 1,
    "live post-disable turn reached the MCP fixture");
  state.checks.push("live post-disable model turn completed, MCP call count unchanged, direct call denied; provider advertisement not captured");
  await app.stop(); state.app = null;
  const restarted = state.app = new AppClient(state, secret);
  await restarted.start();
  const resumed = await restarted.checked("thread/resume", { threadId: id, excludeTurns: true,
    sandbox: "danger-full-access", approvalPolicy: "never" });
  requireTrue(resumed.thread.id === id, "live resumed wrong thread");
  assertNamedStatus(await restarted.checked("mcpServerStatus/list", { threadId: id, detail: "full" }), SERVER, false);
  await denied(restarted, id);
  const fresh = await thread(restarted, state);
  assertNamedStatus(await restarted.checked("mcpServerStatus/list", { threadId: fresh, detail: "full" }), SERVER, false);
  await denied(restarted, fresh);
  state.checks.push("live restart/resume/new-thread direct calls denied; no post-restart live turn sent");
}
async function main() {
  let state; let secret = null; let fatal = null; let overallTimer;
  try {
    const lock = lockIdentity();
    const sha = await fileSha(BINARY);
    requireTrue(sha === EXPECTED_SHA, "locked binary bytes do not match the exact current lock");
    // The secret is read only after explicit live opt-in; it is never printed or persisted.
    if (mode === "live") {
      secret = process.env.OPENROUTER_API_KEY;
      requireTrue(Boolean(secret), "OPENROUTER_API_KEY missing; no live request sent");
    }
    const { runRoot, applicationRoot } = newQaRun(path.join(SOURCE, ".local/Lab/Runs"), `Task7-NamedMcp-${mode}-${randomUUID()}`);
    state = { runRoot, applicationRoot, home: path.join(applicationRoot, "Data/Codex"),
      work: path.join(runRoot, "fixture-work"), temp: path.join(runRoot, "temp"),
      profile: path.join(runRoot, "profile"), roaming: path.join(runRoot, "profile/Roaming"),
      localApp: path.join(runRoot, "profile/Local"), mcpLog: path.join(runRoot, "mcp.jsonl"),
      live: mode === "live", lock, sha, requests: [], checks: [], configHistory: [],
      initializations: [], ownedAppPids: [], exits: [], appTrace: [], app: null,
      abort: new AbortController(), secret, stderrBytes: 0 };
    for (const dir of [applicationRoot, state.home, state.work, state.temp, state.profile, state.roaming, state.localApp]) mkdirSync(dir, { recursive: true });
    // The MCP peer appends only to this run-owned file.
    closeSync(openSync(state.mcpLog, "wx"));
    // Abort and terminate the owned App Server before awaiting sequence settlement.
    // No detached Promise.race continuation can launch another turn after the deadline.
    overallTimer = setTimeout(() => state.abort.abort(new Error("overall qualification deadline")), DEADLINE_MS);
    await (mode === "live" ? live(state, secret) : deterministic(state));
  } catch (error) { fatal = error; }
  finally {
    clearTimeout(overallTimer);
    if (state) {
      const cleanupErrors = [];
      try { writeConfig(state, false); } // Fail closed on disk even if the sequence failed.
      catch (error) { cleanupErrors.push(`disabled config: ${String(error)}`); }
      try { if (state.app) await state.app.stop(); } // Independent of config-write outcome.
      catch (error) { cleanupErrors.push(`owned App Server stop: ${String(error)}`); }
      if (cleanupErrors.length) {
        state.cleanupFailure = cleanupErrors.join("; ").replaceAll(secret ?? "\u0000", "[REDACTED]");
        fatal ??= new Error(state.cleanupFailure);
      }
      try { evidence(state.runRoot, "app-server-messages.json", state.appTrace, secret); }
      catch (error) { fatal ??= error; }
      try { if (!state.live) evidence(state.runRoot, "loopback-requests.json", state.requests, null); }
      catch (error) { fatal ??= error; }
      const result = {
        verdict: fatal ? "INCOMPLETE_OR_FAILED" : "FIXTURE_OBSERVATIONS_ONLY",
        routeAccepted: false, mode, error: fatal ? String(fatal).replaceAll(secret ?? "\u0000", "[REDACTED]") : null,
        lockedBinary: BINARY, lockedSha256: state.sha, lockManifest: LOCK,
        applicationRoot: state.applicationRoot, codexHome: state.home, fixtureRoot: state.runRoot,
        lockedVersion: state.lock.runtime.version, lockedSourceRevision: state.lock.runtime.sourceRevision,
        selectedTuple: state.live ? { provider: "OpenRouter", model: MODEL, endpointSelection: PROVIDER,
          allowProviderModelFallback: false, maxOutputTokensRequestedPerTurn: TOKEN_CAP,
          directBaseUrl: LIVE_PROVIDER.baseUrl } : null,
        checks: state.checks, configHistory: state.configHistory, initializations: state.initializations,
        providerRequestsObserved: state.live ? "Unknown: direct provider wire is not captured" : state.requests.length,
        liveWireOutputCapObserved: state.live ? "Unknown: 512 is requested and pinned source maps the override to Responses requests" : null,
        liveKeyScope: state.live ? "Operator-designated free-only; the fixture does not verify key policy" : null,
        liveRequestCountBound: state.live ? "None; deadline and per-request token cap do not bound total requests or cost" : null,
        stderrBytesDiscarded: state.stderrBytes,
        servedEndpoint: "Unknown: response does not attest concrete endpoint",
        productGeneratedCandidateRoute: "Not exercised; QA echo only",
        mcpChildExit: (() => {
          try {
            const events = mcpEvents(state);
            const starts = events.filter((event) => event.event === "start").length;
            const eofs = events.filter((event) => event.event === "eof").length;
            return starts > 0 && starts === eofs ? "EOF observed for each QA MCP child" :
              `Unknown: ${starts} starts, ${eofs} EOFs; no PID-based cleanup attempted`;
          } catch { return "Unknown: bounded MCP log unavailable; no PID-based cleanup attempted"; }
        })(),
        ownedAppPids: state.ownedAppPids, appExits: state.exits, cleanupFailure: state.cleanupFailure ?? null,
      };
      try { evidence(state.runRoot, "result.json", result, secret); }
      catch (error) { process.stderr.write(`Could not write bounded result: ${String(error)}\n`); }
      process.stdout.write(`Task7 QA evidence: ${state.runRoot}\n`);
    }
    if (fatal) { process.stderr.write(`Task7 QA incomplete/failed: ${String(fatal).replaceAll(secret ?? "\u0000", "[REDACTED]")}\n`); process.exitCode = 1; }
  }
}
