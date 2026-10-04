// P5-04 preparation only: an observational, bounded native loopback profile.
// This file is intentionally independent of the other tests/qa modules.
import assert from "node:assert/strict";
import { spawn, spawnSync } from "node:child_process";
import { createHash, randomBytes } from "node:crypto";
import { createReadStream, existsSync, lstatSync, mkdirSync, readdirSync, readFileSync, realpathSync, writeFileSync } from "node:fs";
import { createServer as createHttpServer } from "node:http";
import { createServer as createTcpServer } from "node:net";
import os from "node:os";
import path from "node:path";
import { performance } from "node:perf_hooks";
import { fileURLToPath, pathToFileURL } from "node:url";

const runClock = performance.now();
const limits = Object.freeze({ targetMs: 180000, workloadMs: 300000, totalMs: 420000,
  setupMs: 90000, teardownMs: 30000, stepMs: 30000, cadenceMs: 12000,
  sampleIntervalMs: 5000, steps: 12, requestBytes: 65536, concurrentRequests: 1,
  fixtureRequests: 8, connections: 2, uiFiles: 128, uiFileBytes: 64 * 1024 * 1024,
  samples: 420, completeSteps: 6, cancelSteps: 2, historySteps: 2,
  diagnosticsSteps: 1, restartSteps: 1 });
const hardDeadline = runClock + limits.totalMs;
const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const profilePath = path.join(sourceRoot, "tests", "qa", "performance-profile.json");
assert.ok(lstatSync(profilePath).size <= 16384, "performance profile exceeds its size cap");
const profileBytes = readFileSync(profilePath);
const profile = JSON.parse(profileBytes.toString("utf8"));
const [hostArgument, playwrightArgument, parentArgument] = process.argv.slice(2);
assert.ok(hostArgument && playwrightArgument && parentArgument && process.argv.length === 5,
  "usage: node tests/qa/performance-soak.mjs <ApprovalQA NeoBabylon.Host.exe> <playwright-core entry.mjs> <exact .local\\Lab\\Runs parent>");
assert.equal(process.platform, "win32", "the native WPF profile requires Windows");
assert.equal(profile.mode, "deterministic-loopback-approvalqa");
assert.ok(Number.isInteger(profile.targetDurationMs) && profile.targetDurationMs > 0
  && profile.targetDurationMs <= limits.targetMs);
assert.ok(Number.isInteger(profile.maximumElapsedMs) && profile.maximumElapsedMs > profile.targetDurationMs
  && profile.maximumElapsedMs <= limits.workloadMs);
assert.ok(Number.isInteger(profile.stepCadenceMs) && profile.stepCadenceMs >= 2000
  && profile.stepCadenceMs <= limits.cadenceMs);
assert.ok(Number.isInteger(profile.sampleIntervalMs) && profile.sampleIntervalMs >= 1000
  && profile.sampleIntervalMs <= limits.sampleIntervalMs);
assert.ok(Array.isArray(profile.steps) && profile.steps.length > 0 && profile.steps.length <= limits.steps);
assert.ok(profile.steps.length * profile.stepCadenceMs <= profile.targetDurationMs);
assert.ok(profile.steps.every((step) => step && ["complete", "cancel", "history", "diagnostics", "restart"].includes(step.kind)
  && (step.id === undefined || Number.isInteger(step.id) && step.id >= 1 && step.id <= 8)));
assert.ok(profile.steps.filter((step) => step.kind === "complete" || step.kind === "cancel").length
  <= limits.fixtureRequests);
for (const [kind, maximum] of [["complete", limits.completeSteps], ["cancel", limits.cancelSteps],
  ["history", limits.historySteps], ["diagnostics", limits.diagnosticsSteps],
  ["restart", limits.restartSteps]]) {
  assert.ok(profile.steps.filter((step) => step.kind === kind).length <= maximum,
    `${kind} operation count exceeds its safety cap`);
}

const hostPath = path.resolve(hostArgument);
const playwrightPath = path.resolve(playwrightArgument);
const qaParent = path.resolve(parentArgument);
const expectedParent = path.join(sourceRoot, ".local", "Lab", "Runs");
const samePath = (left, right) => path.resolve(left).toLowerCase() === path.resolve(right).toLowerCase();
assert.ok(samePath(qaParent, expectedParent), `QA parent must be exactly ${expectedParent}`);
for (const component of [sourceRoot, path.join(sourceRoot, ".local"),
  path.join(sourceRoot, ".local", "Lab"), qaParent]) {
  const stat = lstatSync(component);
  assert.ok(stat.isDirectory() && !stat.isSymbolicLink() && samePath(realpathSync.native(component), component),
    `QA path component is not a direct directory: ${component}`);
}
assert.ok(existsSync(hostPath) && path.basename(hostPath).toLowerCase() === "neobabylon.host.exe",
  "an existing NeoBabylon.Host.exe is required");
assert.ok(hostPath.split(path.sep).some((segment) => segment.toLowerCase() === "approvalqa"),
  "the isolated capability record requires an ApprovalQA host build");
assert.ok(existsSync(playwrightPath), "an existing Playwright Core entry module is required");

const lockPath = path.join(sourceRoot, "runtime", "runtime-lock.json");
assert.ok(lstatSync(lockPath).size <= 16384, "runtime lock exceeds the parsing size cap");
const lock = JSON.parse(readFileSync(lockPath, "utf8")).runtime;
const runtimePath = path.resolve(sourceRoot, lock.appServerBinaryRelativePath);
const uiDistPath = path.join(sourceRoot, "ui", "diagnostic", "dist");
const uiEntryPath = path.join(uiDistPath, "index.html");
assert.ok(existsSync(runtimePath) && existsSync(uiEntryPath), "locked runtime and built UI entry are required");

async function sha256File(filePath) {
  const hash = createHash("sha256");
  for await (const chunk of createReadStream(filePath)) hash.update(chunk);
  return hash.digest("hex");
}

function regularFilesUnder(directory) {
  const files = [];
  let visitedDirectories = 0;
  const visit = (current) => {
    assert.ok(++visitedDirectories <= limits.uiFiles, "UI dist exceeds the traversal directory cap");
    for (const entry of readdirSync(current, { withFileTypes: true })) {
      remainingMs();
      const entryPath = path.join(current, entry.name);
      if (entry.isDirectory()) visit(entryPath);
      else if (entry.isFile()) {
        assert.ok(lstatSync(entryPath).size <= limits.uiFileBytes, "UI asset exceeds the hashing size cap");
        files.push(entryPath);
        assert.ok(files.length <= limits.uiFiles, "UI dist exceeds the hashing file cap");
      }
      else throw new Error(`UI dist contains a non-regular entry: ${entryPath}`);
    }
  };
  visit(directory);
  return files.sort((left, right) => left < right ? -1 : left > right ? 1 : 0);
}

function newRunRoot() {
  const runId = `P5-04-perf-${Date.now()}-${randomBytes(4).toString("hex")}`;
  const runRoot = path.join(qaParent, runId);
  assert.ok(!existsSync(runRoot), "refusing to reuse a QA run root");
  mkdirSync(runRoot, { recursive: false });
  return { runRoot, applicationRoot: path.join(runRoot, "App") };
}

const { runRoot, applicationRoot } = newRunRoot();
const dataRoot = path.join(applicationRoot, "Data");
mkdirSync(dataRoot, { recursive: true });
const capabilityPath = path.join(dataRoot, "performance-capability.json");
const evidencePath = path.join(runRoot, "observations.json");
writeFileSync(path.join(runRoot, "profile.json"), profileBytes, { flag: "wx" });

const model = "p5-04-local-loopback-fixture";
const unknown = (reason) => ({ state: "Unknown", value: null, evidenceSource: reason });
const capability = {
  providerId: "lmstudio",
  providerDisplayName: "P5-04 deterministic local fixture",
  providerServerVersion: "Unknown",
  endpoint: "pending-loopback-endpoint",
  modelIdentifier: model,
  modelVariant: "synthetic Responses events; no model weights",
  architecture: "Unknown",
  parameterCount: "Unknown",
  modelSizeBytes: "Unknown",
  quantization: unknown("P5-04 fixture has no model weights"),
  contextWindowAdvertised: unknown("P5-04 fixture has no provider catalog"),
  contextWindowEffective: { state: "Known", value: 32768, evidenceSource: "P5-04 synthetic catalog value only" },
  reasoningControls: unknown("P5-04 fixture does not qualify reasoning"),
  toolFunctionCalling: { state: "Known", value: "tool_use",
    evidenceSource: "P5-04 synthetic QA catalog declaration only; no real model capability or tool call" },
  applyPatchToolType: unknown("P5-04 fixture does not call apply_patch"),
  structuredOutput: unknown("P5-04 fixture does not qualify structured output"),
  agentMetadata: { state: "Known", value: "type=llm; inputModalities=text; vision=False",
    evidenceSource: "P5-04 synthetic local fixture only" },
};

const fixtureRequests = [];
const fixtureErrors = [];
const recordIssue = (list, value) => { if (list.length < 32) list.push(String(value).slice(0, 500)); };
const openStreams = new Set();
let concurrentRequests = 0;
let acceptedRequests = 0;
let totalRequests = 0;
const replyFor = (id) => `PERF_REPLY_${id} ${"r".repeat(256)}`;
const sse = (event) => `event: ${event.type}\ndata: ${JSON.stringify(event)}\n\n`;
const completed = (id) => ({ type: "response.completed", response: { id,
  usage: { input_tokens: 0, input_tokens_details: null, output_tokens: 0,
    output_tokens_details: null, total_tokens: 0 } } });

const fixture = createHttpServer((request, response) => {
  response.on("error", (error) => { recordIssue(fixtureErrors, `fixture response error: ${error.message}`); });
  request.on("error", (error) => { recordIssue(fixtureErrors, `fixture request error: ${error.message}`); });
  totalRequests++;
  if (totalRequests > limits.fixtureRequests + 4) {
    recordIssue(fixtureErrors, "fixture total request safety cap reached");
    response.writeHead(429, { connection: "close" }).end();
    return;
  }
  if (request.method !== "POST" || request.url !== "/v1/responses") {
    response.writeHead(404).end();
    return;
  }
  if (concurrentRequests >= limits.concurrentRequests || acceptedRequests >= limits.fixtureRequests) {
    recordIssue(fixtureErrors, "fixture request concurrency or count safety cap reached");
    response.writeHead(429, { connection: "close" }).end();
    return;
  }
  concurrentRequests++;
  acceptedRequests++;
  response.once("close", () => { concurrentRequests--; });
  if (Number(request.headers["content-length"]) > limits.requestBytes) {
    recordIssue(fixtureErrors, "Responses request content length exceeded the fixture byte cap");
    response.writeHead(413, { connection: "close" }).end();
    request.destroy();
    return;
  }
  const chunks = [];
  let bytes = 0;
  let rejected = false;
  request.on("data", (chunk) => {
    if (rejected) return;
    bytes += chunk.length;
    if (bytes > limits.requestBytes) {
      rejected = true;
      recordIssue(fixtureErrors, "Responses request exceeded the fixture byte cap");
      response.writeHead(413, { connection: "close" }).end();
      request.pause();
      response.once("finish", () => request.destroy());
    } else chunks.push(chunk);
  });
  request.on("end", () => {
    if (rejected) return;
    try {
      const body = JSON.parse(Buffer.concat(chunks).toString("utf8"));
      const markers = [...JSON.stringify(body.input ?? []).matchAll(/PERF_(COMPLETE|CANCEL)_(\d+)/g)];
      const marker = markers.at(-1);
      if (body.model !== model || !marker) throw new Error("unexpected model or missing current fixture marker");
      const kind = marker[1].toLowerCase();
      const id = Number(marker[2]);
      const responseId = `p5-04-${kind}-${id}-${fixtureRequests.length + 1}`;
      fixtureRequests.push({ index: fixtureRequests.length + 1, kind, id, bytes,
        elapsedMs: Math.round(performance.now() - runClock) });
      response.writeHead(200, { "content-type": "text/event-stream", "cache-control": "no-cache" });
      response.flushHeaders();
      response.write(sse({ type: "response.created", response: { id: responseId } }));
      if (kind === "cancel") {
        const itemId = `p5-04-partial-${id}`;
        response.write(sse({ type: "response.output_item.added",
          item: { type: "message", role: "assistant", id: itemId, content: [] } }));
        response.write(sse({ type: "response.output_text.delta", output_index: 0,
          item_id: itemId, content_index: 0, delta: `PERF_PARTIAL_${id}` }));
        openStreams.add(response);
        response.once("close", () => openStreams.delete(response));
        return;
      }
      response.write(sse({ type: "response.output_item.done", item: { type: "message",
        role: "assistant", id: `p5-04-message-${id}`, content: [{ type: "output_text", text: replyFor(id) }] } }));
      response.end(sse(completed(responseId)));
    } catch (error) {
      recordIssue(fixtureErrors, error.message);
      if (!response.headersSent) response.writeHead(409);
      response.end();
    }
  });
});
fixture.maxConnections = limits.connections;
fixture.requestTimeout = 10000;
fixture.headersTimeout = 10000;
fixture.maxRequestsPerSocket = limits.fixtureRequests;

async function listen(server) {
  return await withTimeout(new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", () => resolve(server.address().port));
  }), 5000, "loopback listener setup");
}

async function reservePort() {
  const server = createTcpServer();
  const port = await listen(server);
  await withTimeout(new Promise((resolve, reject) => server.close((error) => error ? reject(error) : resolve())),
    5000, "loopback port reservation close");
  return port;
}

const processScript = `$ErrorActionPreference = 'Stop'; Get-CimInstance Win32_Process |
  Select-Object ProcessId, ParentProcessId, Name, ExecutablePath, WorkingSetSize, PrivatePageCount,
    @{Name='StartedUtcTicks';Expression={if ($_.CreationDate) {$_.CreationDate.ToUniversalTime().Ticks.ToString()} else {''}}} |
  ConvertTo-Json -Compress -Depth 3`;
function powershell(script, timeoutMs) {
  const call = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", script],
    { encoding: "utf8", timeout: Math.max(1, Math.min(5000, timeoutMs)),
      windowsHide: true, maxBuffer: 4 * 1024 * 1024 });
  if (call.error || call.status !== 0) throw new Error(`PowerShell process inspection failed: ${call.error?.message ?? call.stderr}`);
  return call.stdout.trim();
}
function processList(timeoutMs = remainingMs()) {
  const rows = JSON.parse(powershell(processScript, timeoutMs));
  return Array.isArray(rows) ? rows : [rows];
}
function descendants(rows, parentPid) {
  const selected = new Set([parentPid]);
  let size;
  do {
    size = selected.size;
    for (const row of rows) if (selected.has(Number(row.ParentProcessId))) selected.add(Number(row.ProcessId));
  } while (selected.size !== size);
  return rows.filter((row) => selected.has(Number(row.ProcessId)));
}
const identityKey = (row) => `${row.ProcessId}:${row.StartedUtcTicks}:${row.ExecutablePath ?? ""}`;
function sameProcessStart(left, right) {
  if (!/^\d+$/.test(String(left)) || !/^\d+$/.test(String(right))) return false;
  const difference = BigInt(left) - BigInt(right);
  return difference >= -100000n && difference <= 100000n; // Allow at most 10 ms representation difference.
}
let observedDescendants = new Map();
const samples = [];
const samplingErrors = [];
const cleanup = [];
const teardownErrors = [];
const operations = [];
const pageIssues = [];
let host = null;
let browser = null;
let page = null;
let launchIdentity = null;
let currentGeneration = null;
const generations = [];
const identityErrors = [];
let browserVersion = null;
let sampleBusy = false;
let sampleTimer = null;
let phaseDeadline = Infinity;
let stepDeadline = Infinity;
let abortSignal = null;
let identityEvidence = null;
for (const signal of ["SIGINT", "SIGTERM"]) {
  process.on(signal, () => { abortSignal = signal; phaseDeadline = 0; });
}

function observeTree(rows, { allowMissingRoot = false } = {}) {
  if (!host?.pid) return [];
  const tree = descendants(rows, host.pid);
  const root = tree.find((row) => Number(row.ProcessId) === host.pid);
  if (!root) {
    if (!allowMissingRoot && host.exitCode === null)
      throw new Error("QA host process was not visible during process inspection");
    return [];
  }
  if (root && !samePath(root.ExecutablePath ?? "", hostPath))
    throw new Error("QA host PID resolved to a different executable");
  if (root && launchIdentity && identityKey(root) !== launchIdentity)
    throw new Error("QA host PID identity changed during the run");
  if (root && !launchIdentity) launchIdentity = identityKey(root);
  for (const row of tree) if (row.StartedUtcTicks && row.ExecutablePath)
    observedDescendants.set(identityKey(row), row);
  return tree;
}

function sample() {
  if (!host) return;
  if (samples.length >= limits.samples) throw new Error("process sample safety cap reached");
  const tree = observeTree(processList());
  const members = tree.map((row) => ({ pid: Number(row.ProcessId), parentPid: Number(row.ParentProcessId),
    startedUtcTicks: row.StartedUtcTicks, name: row.Name, workingSetBytes: Number(row.WorkingSetSize ?? 0),
    privateBytes: Number(row.PrivatePageCount ?? 0) }));
  samples.push({ elapsedMs: Math.round(performance.now() - runClock), members,
    totalWorkingSetBytes: members.reduce((sum, item) => sum + item.workingSetBytes, 0),
    totalPrivateBytes: members.reduce((sum, item) => sum + item.privateBytes, 0) });
}

function startSampling() {
  sample();
  sampleTimer = setInterval(() => {
    if (sampleBusy) return;
    sampleBusy = true;
    try { sample(); } catch (error) { recordIssue(samplingErrors, error.message); }
    finally { sampleBusy = false; }
  }, profile.sampleIntervalMs);
}

function remainingMs() {
  if (abortSignal) throw new Error(`profile interrupted by ${abortSignal}`);
  const remaining = Math.min(phaseDeadline, stepDeadline, hardDeadline) - performance.now();
  if (remaining <= 0) throw new Error("maximum profile elapsed time reached");
  return Math.min(30000, Math.max(1, Math.floor(remaining)));
}
function teardownRemaining(deadline) {
  const remaining = Math.min(deadline, hardDeadline) - performance.now();
  if (remaining <= 0) throw new Error("teardown safety deadline reached");
  return Math.max(1, Math.floor(remaining));
}
async function withTimeout(promise, timeoutMs, label) {
  let timer;
  try {
    return await Promise.race([promise, new Promise((_, reject) => {
      timer = setTimeout(() => reject(new Error(`${label} exceeded its safety deadline`)), timeoutMs);
    })]);
  } finally { clearTimeout(timer); }
}
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

async function waitForExit(child, timeoutMs) {
  if (child.exitCode !== null) return { code: child.exitCode, signal: child.signalCode };
  return await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`owned WPF child ${child.pid} did not exit`)), timeoutMs);
    child.once("exit", (code, signal) => { clearTimeout(timer); resolve({ code, signal }); });
  });
}

async function waitForCdp(port) {
  const limit = performance.now() + Math.min(45000, remainingMs());
  while (performance.now() < limit) {
    if (host.exitCode !== null) throw new Error(`QA host exited before WebView2 CDP opened (${host.exitCode})`);
    try {
      const response = await fetch(`http://127.0.0.1:${port}/json/version`, { signal: AbortSignal.timeout(1000) });
      if (response.ok) return await response.json();
    } catch { /* The owned WebView2 process is still starting. */ }
    await sleep(250);
  }
  throw new Error("owned WebView2 CDP endpoint did not open");
}

async function launchHost(chromium) {
  const port = await reservePort();
  const environment = {};
  for (const name of ["PATH", "Path", "SystemRoot", "WINDIR", "COMSPEC", "PATHEXT", "TEMP", "TMP",
    "APPDATA", "LOCALAPPDATA", "USERPROFILE", "HOMEDRIVE", "HOMEPATH", "ProgramFiles", "ProgramFiles(x86)"]) {
    if (process.env[name]) environment[name] = process.env[name];
  }
  Object.assign(environment, {
    NEOBABYLON_SOURCE_ROOT: sourceRoot,
    NEOBABYLON_APPLICATION_ROOT: applicationRoot,
    NEOBABYLON_MODEL_CAPABILITY_PATH: capabilityPath,
    NEOBABYLON_ENABLE_WEBVIEW2_REMOTE_DEBUG: "1",
    NEOBABYLON_WEBVIEW2_DEBUG_PORT: String(port),
  });
  const start = performance.now();
  observedDescendants = new Map();
  host = spawn(hostPath, [], { cwd: sourceRoot, env: environment, stdio: "ignore", windowsHide: false });
  host.once("error", (error) => recordIssue(pageIssues, `host spawn: ${error.message}`));
  currentGeneration = { host: { status: "Unknown", pid: host.pid ?? null,
    reason: "launched executable has not been observed" },
  appServer: { status: "Unknown", reason: "not observed yet" } };
  generations.push(currentGeneration);
  if (!host.pid) throw new Error("QA host spawn did not return a process ID");
  launchIdentity = null;
  const hostRow = observeTree(processList()).find((row) => Number(row.ProcessId) === host.pid);
  assert.ok(hostRow?.StartedUtcTicks && samePath(hostRow.ExecutablePath, hostPath),
    "the launched WPF host executable identity could not be established");
  const launchedPathHash = await withTimeout(sha256File(hostRow.ExecutablePath), remainingMs(),
    "launched host path hashing");
  assert.equal(launchedPathHash, identityEvidence.prelaunchHostSha256,
    "launched host path changed after prelaunch hashing");
  currentGeneration.host = { status: "Observed", pid: host.pid,
    startedUtcTicks: hostRow.StartedUtcTicks, executablePath: hostRow.ExecutablePath,
    onDiskSha256AtObservation: launchedPathHash, inMemoryImageSha256: "Unknown" };
  browserVersion = await waitForCdp(port);
  browser = await chromium.connectOverCDP(`http://127.0.0.1:${port}`, { timeout: remainingMs() });
  const until = performance.now() + remainingMs();
  while (performance.now() < until) {
    page = browser.contexts().flatMap((context) => context.pages())
      .find((candidate) => candidate.url().startsWith("https://neobabylon.local/"));
    if (page) break;
    await sleep(250);
  }
  if (!page) throw new Error("owned native NeoBabylon page did not appear");
  page.setDefaultTimeout(remainingMs());
  page.on("pageerror", (error) => recordIssue(pageIssues, error.message));
  page.on("console", (message) => {
    if (message.type() === "error") recordIssue(pageIssues, message.text());
  });
  await page.getByText("Desktop host ready").waitFor({ timeout: remainingMs() });
  await page.getByRole("textbox", { name: "Message NeoBabylon" }).waitFor({ timeout: remainingMs() });
  const readyMs = Math.round(performance.now() - start);
  await page.getByRole("button", { name: "Open diagnostics" }).click();
  const dialog = page.getByRole("dialog", { name: "Runtime diagnostics" });
  await dialog.waitFor({ timeout: remainingMs() });
  const runtimeDetails = await dialog.innerText();
  assert.ok(runtimeDetails.toLowerCase().includes(String(lock.sha256).toLowerCase()),
    "native runtime diagnostics did not show the locked App Server hash");
  assert.ok(runtimeDetails.includes(String(lock.version)),
    "native runtime diagnostics did not show the locked App Server version");
  assert.ok(runtimeDetails.includes(applicationRoot),
    "native runtime diagnostics did not show the isolated application root");
  assert.ok(runtimeDetails.includes(model),
    "native runtime diagnostics did not show the synthetic selected capability");
  assert.match(runtimeDetails, /Ordinary Codex root used\s+false/i,
    "native runtime diagnostics did not confirm Codex home isolation");
  await dialog.getByRole("button", { name: "Done" }).click();
  await dialog.waitFor({ state: "detached", timeout: remainingMs() });
  return readyMs;
}

async function captureAppServerIdentity() {
  if (!currentGeneration || currentGeneration.appServer.status === "Observed") return;
  const requestId = `p5-04-identity-${randomBytes(8).toString("hex")}`;
  const diagnostics = await withTimeout(page.evaluate(({ requestId, timeoutMs }) => new Promise((resolve, reject) => {
    const bridge = window.chrome?.webview;
    if (!bridge) { reject(new Error("native host bridge is unavailable")); return; }
    const finish = (error, result) => {
      clearTimeout(timer);
      bridge.removeEventListener("message", onMessage);
      if (error) reject(error); else resolve(result);
    };
    const onMessage = (event) => {
      if (event.data?.requestId !== requestId) return;
      if (!event.data.ok) finish(new Error("native host diagnostics request failed"));
      else finish(null, event.data.result);
    };
    const timer = setTimeout(() => finish(new Error("native host diagnostics timed out")), timeoutMs);
    bridge.addEventListener("message", onMessage);
    bridge.postMessage({ operation: "getDiagnostics", requestId });
  }), { requestId, timeoutMs: remainingMs() }), remainingMs(), "native identity diagnostics");
  const app = diagnostics?.approvalQaAppServerIdentity;
  const row = app && processList().find((item) => Number(item.ProcessId) === Number(app.processId));
  if (!app || !row || !sameProcessStart(row.StartedUtcTicks, app.startTimeUtcTicks)
      || !samePath(row.ExecutablePath ?? "", runtimePath)
      || !samePath(diagnostics.runtime?.binaryPath ?? "", runtimePath)
      || String(diagnostics.runtime?.sha256).toLowerCase() !== String(lock.sha256).toLowerCase()) {
    currentGeneration.appServer = { status: "Unknown", reason: "QA diagnostics and OS process identity did not agree" };
    identityErrors.push("actual launched App Server executable identity was not established");
    return;
  }
  const onDiskSha256AtObservation = await withTimeout(sha256File(row.ExecutablePath), remainingMs(),
    "launched App Server path hashing");
  if (onDiskSha256AtObservation !== String(lock.sha256).toLowerCase()) {
    currentGeneration.appServer = { status: "Unknown", reason: "observed App Server path hash differed from lock" };
    identityErrors.push("observed App Server path did not match the runtime lock hash");
    return;
  }
  currentGeneration.appServer = { status: "Observed", pid: Number(row.ProcessId),
    startedUtcTicks: row.StartedUtcTicks, qaStartTimeUtcTicks: String(app.startTimeUtcTicks),
    executablePath: row.ExecutablePath,
    onDiskSha256AtObservation, inMemoryImageSha256: "Unknown" };
}

async function closeHost() {
  if (!host) return;
  const closeDeadline = Math.min(hardDeadline, phaseDeadline, stepDeadline,
    performance.now() + 20000);
  const issues = [];
  if (browser) {
    try {
      await withTimeout(browser.close(), Math.min(5000, teardownRemaining(closeDeadline)), "CDP close");
    } catch (error) { issues.push(error.message); }
  }
  browser = null;
  page = null;
  const child = host;
  if (!child.pid) {
    host = null;
    return;
  }
  const observed = new Map(observedDescendants);
  try { observeTree(processList(teardownRemaining(closeDeadline)), { allowMissingRoot: true }); }
  catch (error) { issues.push(`final process observation: ${error.message}`); }
  for (const [key, row] of observedDescendants) observed.set(key, row);
  if (child.exitCode === null) {
    try {
      if (!child.kill()) throw new Error("owned host process handle refused termination");
      await waitForExit(child, Math.min(5000, teardownRemaining(closeDeadline)));
    } catch (error) { issues.push(`owned host handle: ${error.message}`); }
  }
  await sleep(Math.min(1000, Math.max(0, closeDeadline - performance.now())));
  let remainingRows = [];
  try { remainingRows = processList(teardownRemaining(closeDeadline)); }
  catch (error) { issues.push(`orphan inspection: ${error.message}`); }
  const after = new Map(remainingRows.map((row) => [identityKey(row), row]));
  const possibleOrphans = [...observed.entries()]
    .filter(([key, row]) => Number(row.ProcessId) !== child.pid && after.has(key))
    .map(([, row]) => ({ pid: Number(row.ProcessId), startedUtcTicks: row.StartedUtcTicks,
      executablePath: row.ExecutablePath }));
  if (possibleOrphans.length) issues.push("observed descendants remained; not terminated by PID");
  if (child.exitCode === null) issues.push("owned host handle did not report exit");
  cleanup.push({ hostPid: child.pid, hostExited: child.exitCode !== null,
    observedDescendantCount: Math.max(0, observed.size - 1), possibleOrphans,
    orphanDetectionScope: "previously observed descendants only; late or unobserved children may be missed",
    issues });
  host = null;
  launchIdentity = null;
  observedDescendants = new Map();
  currentGeneration = null;
  if (issues.length) throw new Error(`QA cleanup incomplete: ${issues.join("; ")}`);
}

function percentile(values, fraction) {
  if (!values.length) return null;
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.ceil(sorted.length * fraction) - 1];
}
function summary(values) {
  return { count: values.length, p50: percentile(values, 0.5), p95: percentile(values, 0.95),
    max: values.length ? Math.max(...values) : null };
}

async function measure(kind, id, action) {
  const started = performance.now();
  const before = fixtureRequests.length;
  stepDeadline = Math.min(hardDeadline, started + limits.stepMs);
  try {
    if (page) page.setDefaultTimeout(remainingMs());
    const detail = await withTimeout(action(), remainingMs(), `${kind} step`);
    const added = fixtureRequests.length - before;
    assert.equal(added, kind === "complete" || kind === "cancel" ? 1 : 0,
      `${kind} caused an unexpected number of fixture requests`);
    operations.push({ kind, id: id ?? null, elapsedMs: Math.round(performance.now() - started),
      fixtureRequestsAdded: added, outcome: "observed", ...detail });
  } catch (error) {
    operations.push({ kind, id: id ?? null, elapsedMs: Math.round(performance.now() - started),
      fixtureRequestsAdded: fixtureRequests.length - before, outcome: "incomplete", error: error.message });
    throw error;
  } finally { stepDeadline = Infinity; }
}

async function main() {
  phaseDeadline = Math.min(hardDeadline, performance.now() + limits.setupMs);
  const runtimeHash = await withTimeout(sha256File(runtimePath), remainingMs(), "prelaunch runtime hashing");
  assert.equal(runtimeHash, String(lock.sha256).toLowerCase(), "locked App Server SHA-256 mismatch");
  const fixturePort = await listen(fixture);
  capability.endpoint = `http://127.0.0.1:${fixturePort}/v1`;
  writeFileSync(capabilityPath, `${JSON.stringify(capability, null, 2)}\n`, { flag: "wx" });
  const { chromium } = await withTimeout(import(pathToFileURL(playwrightPath).href), remainingMs(),
    "Playwright module loading");
  const uiDistFiles = [];
  for (const file of regularFilesUnder(uiDistPath)) {
    uiDistFiles.push({ path: path.relative(uiDistPath, file).replaceAll(path.sep, "/"),
      sha256: await withTimeout(sha256File(file), remainingMs(), "UI dist hashing") });
  }
  identityEvidence = { prelaunchHostPath: hostPath,
    prelaunchHostSha256: await withTimeout(sha256File(hostPath), remainingMs(), "prelaunch host hashing"),
    prelaunchRuntimePath: runtimePath, prelaunchRuntimeSha256: runtimeHash, runtimeVersion: lock.version,
    sourceRevision: lock.sourceRevision, sourcePatchSha256: lock.sourcePatchSha256,
    uiDistFiles, playwrightEntrySha256: await withTimeout(sha256File(playwrightPath), remainingMs(),
      "Playwright entry hashing"),
    profileSha256: createHash("sha256").update(profileBytes).digest("hex"),
    nodeVersion: process.version, platform: os.platform(), osRelease: os.release(), arch: os.arch(),
    cpuModel: os.cpus()[0]?.model ?? "Unknown", logicalCpuCount: os.cpus().length,
    installedRamBytes: os.totalmem(), fixture: "local HTTP Responses; no weights or external provider" };
  const startupMs = await launchHost(chromium);
  const workloadStart = performance.now();
  phaseDeadline = Math.min(hardDeadline, workloadStart + profile.maximumElapsedMs);
  startSampling();
  let lastCompleted = null;
  for (const [index, step] of profile.steps.entries()) {
    const target = workloadStart + index * profile.stepCadenceMs;
    if (performance.now() < target) await sleep(Math.min(target - performance.now(), remainingMs()));
    remainingMs();
    if (page) page.setDefaultTimeout(remainingMs());
    if (step.kind === "complete") {
      await measure("complete", step.id, async () => {
        const prompt = `PERF_COMPLETE_${step.id}`;
        const reply = replyFor(step.id);
        const composer = page.getByRole("textbox", { name: "Message NeoBabylon" });
        await composer.fill(prompt);
        await page.getByRole("button", { name: "Send message" }).click();
        await page.getByRole("region", { name: "Conversation" })
          .locator("article.message-assistant .message-text").getByText(reply, { exact: true })
          .waitFor({ timeout: remainingMs() });
        await page.waitForFunction(() => !["starting", "running"].includes(
          document.querySelector("section.conversation")?.getAttribute("data-turn-state")),
        null, { timeout: remainingMs() });
        lastCompleted = { prompt, reply };
        return {};
      });
      await captureAppServerIdentity();
    } else if (step.kind === "cancel") {
      await measure("cancel", step.id, async () => {
        const prompt = `PERF_CANCEL_${step.id}`;
        await page.getByRole("textbox", { name: "Message NeoBabylon" }).fill(prompt);
        await page.getByRole("button", { name: "Send message" }).click();
        await page.getByRole("region", { name: "Conversation" })
          .locator("article.message-assistant .message-text")
          .getByText(`PERF_PARTIAL_${step.id}`, { exact: true }).waitFor({ timeout: remainingMs() });
        const stopAt = performance.now();
        await page.getByRole("button", { name: "Stop turn" }).click();
        await page.waitForFunction(() => document.querySelector("section.conversation")
          ?.getAttribute("data-turn-state") === "interrupted", null, { timeout: remainingMs() });
        return { cancellationMs: Math.round(performance.now() - stopAt) };
      });
    } else if (step.kind === "history") {
      assert.equal(step.id, Number(lastCompleted?.prompt.split("_").at(-1)), "history target must be the last completed turn");
      await measure("history", step.id, async () => {
        await page.getByRole("button", { name: "New task" }).click();
        const saved = page.locator("button.saved-chat").filter({ hasText: lastCompleted.prompt });
        await saved.waitFor({ timeout: remainingMs() });
        const openedAt = performance.now();
        await saved.click();
        await page.getByRole("region", { name: "Conversation" })
          .locator("article.message-assistant .message-text")
          .getByText(lastCompleted.reply, { exact: true }).waitFor({ timeout: remainingMs() });
        return { reopenMs: Math.round(performance.now() - openedAt) };
      });
    } else if (step.kind === "diagnostics") {
      await measure("diagnostics", null, async () => {
        await page.getByRole("button", { name: "Open diagnostics" }).click();
        const dialog = page.getByRole("dialog", { name: "Runtime diagnostics" });
        await dialog.waitFor({ timeout: remainingMs() });
        await dialog.getByRole("button", { name: "Done" }).click();
        await dialog.waitFor({ state: "detached", timeout: remainingMs() });
        return {};
      });
    } else if (step.kind === "restart") {
      await measure("restart", null, async () => {
        await closeHost();
        const restartReadyMs = await launchHost(chromium);
        const saved = page.locator("button.saved-chat").filter({ hasText: lastCompleted.prompt });
        await saved.waitFor({ timeout: remainingMs() });
        const resumeAt = performance.now();
        await saved.click();
        await page.getByRole("region", { name: "Conversation" })
          .locator("article.message-assistant .message-text")
          .getByText(lastCompleted.reply, { exact: true }).waitFor({ timeout: remainingMs() });
        return { restartReadyMs, resumeMs: Math.round(performance.now() - resumeAt) };
      });
    } else throw new Error(`unknown performance profile step: ${step.kind}`);
  }
  const elapsed = performance.now() - workloadStart;
  if (elapsed < profile.targetDurationMs) await sleep(Math.min(profile.targetDurationMs - elapsed, remainingMs()));
  remainingMs();
  assert.ok(observeTree(processList()).length > 0, "QA host exited before the bounded workload ended");
  return { startupMs, workloadElapsedMs: Math.round(performance.now() - workloadStart) };
}

let observation = null;
let runError = null;
try { observation = await main(); }
catch (error) { runError = error; }
finally {
  if (sampleTimer) clearInterval(sampleTimer);
  stepDeadline = Infinity;
  phaseDeadline = Math.min(hardDeadline, performance.now() + limits.teardownMs);
  try { await closeHost(); } catch (error) { teardownErrors.push(error.message); }
  for (const response of openStreams) response.destroy();
  if (fixture.listening) {
    try {
      await withTimeout(new Promise((resolve, reject) => fixture.close((error) => error ? reject(error) : resolve())),
        Math.min(5000, teardownRemaining(phaseDeadline)), "fixture listener close");
    } catch (error) {
      teardownErrors.push(error.message);
      fixture.closeAllConnections();
    }
  }
  const launchedIdentityEstablished = generations.length > 0
    && generations.every((generation) => generation.host?.status === "Observed"
      && generation.appServer?.status === "Observed");
  if (!launchedIdentityEstablished) identityErrors.push("launched host/App Server identity is incomplete or Unknown");
  const operationKinds = [...new Set(operations.map((item) => item.kind))];
  const evidence = {
    schemaVersion: 1, profileId: profile.profileId,
    status: runError || fixtureErrors.length || samplingErrors.length || pageIssues.length
      || teardownErrors.length || identityErrors.length
      ? "incomplete" : "observations_recorded",
    qualification: "unqualified; no threshold or pass verdict",
    limits,
    runRoot, applicationRoot, dataRoot,
    startedAtUtc: new Date(Date.now() - (performance.now() - runClock)).toISOString(),
    completedAtUtc: new Date().toISOString(),
    identity: identityEvidence, launchedGenerations: generations, identityErrors,
    ...(observation ?? {}), browserVersion,
    operations, fixtureRequests, fixtureErrors, pageIssues, samplingErrors,
    samples, cleanup, teardownErrors,
    summaries: { operationMs: Object.fromEntries(operationKinds.map((kind) =>
      [kind, summary(operations.filter((item) => item.kind === kind && item.outcome === "observed")
        .map((item) => item.elapsedMs))])),
      workingSetBytes: summary(samples.map((item) => item.totalWorkingSetBytes)),
      privateBytes: summary(samples.map((item) => item.totalPrivateBytes)),
      cancellationMs: summary(operations.filter((item) => item.kind === "cancel" && item.outcome === "observed")
        .map((item) => item.cancellationMs)) },
    error: runError?.message ?? null,
  };
  writeFileSync(evidencePath, `${JSON.stringify(evidence, null, 2)}\n`, { flag: "wx" });
  console.log(JSON.stringify({ status: evidence.status, qualification: evidence.qualification,
    evidencePath, operationCount: operations.length, sampleCount: samples.length }));
  process.exit(evidence.status === "incomplete" ? 1 : 0);
}
