import assert from "node:assert/strict";
import { createHash, randomUUID } from "node:crypto";
import { spawn, spawnSync } from "node:child_process";
import { createServer as createHttpServer } from "node:http";
import { createServer as createTcpServer } from "node:net";
import { existsSync, mkdirSync, readFileSync, unlinkSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { assertQaRunsParent, newQaRun } from "./qa-run-root.mjs";

const [hostPathArg, playwrightModulePathArg, qaParentArg] = process.argv.slice(2);
assert.ok(hostPathArg && playwrightModulePathArg && qaParentArg,
  "usage: node native-command-stop.mjs <NeoBabylon.Host.exe> <playwright-core-entry.mjs> <exact .local\\Lab\\Runs parent>");

const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const hostPath = path.resolve(hostPathArg);
const playwrightModulePath = path.resolve(playwrightModulePathArg);
const qaParent = path.resolve(qaParentArg);
assertQaRunsParent(sourceRoot, qaParent);

for (const [label, filePath] of [["WPF host", hostPath], ["Playwright module", playwrightModulePath]]) {
  assert.ok(existsSync(filePath), `${label} does not exist: ${filePath}`);
}

const runtimeLock = JSON.parse(readFileSync(path.join(sourceRoot, "runtime", "runtime-lock.json"), "utf8"));
const runtime = runtimeLock.runtime;
const runtimePath = path.resolve(sourceRoot, runtime.appServerBinaryRelativePath);
assert.ok(existsSync(runtimePath), `pinned App Server binary is missing: ${runtimePath}`);
const runtimeHash = createHash("sha256").update(readFileSync(runtimePath)).digest("hex");
assert.equal(runtimeHash, runtime.sha256.toLowerCase(), "pinned App Server binary does not match runtime-lock.json");

const { runRoot, applicationRoot } = newQaRun(qaParent, `P3-02-native-command-stop-${Date.now()}-${process.pid}`);
const dataRoot = path.join(applicationRoot, "Data");
const workspace = path.join(dataRoot, "Workspace");
const fixturesRoot = path.join(dataRoot, "Fixtures");
mkdirSync(workspace, { recursive: true });
mkdirSync(fixturesRoot, { recursive: true });
const capabilityPath = path.join(fixturesRoot, "model-capability.json");
const resultPath = path.join(runRoot, "result.json");
const interruptedScreenshotPath = path.join(runRoot, "command-running-after-turn-stop.png");
const stoppedScreenshotPath = path.join(runRoot, "command-stopped-visible.png");
const modelIdentifier = "p3-02-native-command-stop-fixture";
const prompt = "Run the isolated long command exactly once; do not repeat it.";
const stalePrompt = "Run the stale-binding fixture once.";
const naturalPrompt = "Run the natural-exit fixture once.";
const transportPrompt = "Run the transport-loss fixture once.";

function createCommandScenario(name, scenarioPrompt, delaySeconds) {
  const prefix = name === "stop" ? "p3-02-command-stop" : `p3-02-${name}-command`;
  const scriptPath = path.join(workspace, `${prefix}.ps1`);
  const startedPath = path.join(workspace, `${prefix}-started.txt`);
  const identityPath = path.join(workspace, `${prefix}-identity.txt`);
  const latePath = path.join(workspace, `${prefix}-late.txt`);
  const startedMarker = name === "stop"
    ? "NEOBABYLON_P3_02_NATIVE_STOP_STARTED"
    : `NEOBABYLON_P3_02_${name.toUpperCase()}_STARTED`;
  const callId = name === "stop"
    ? "p3-02-native-command-stop-call"
    : `p3-02-native-${name}-call`;
  const scriptContent = [
    `[Console]::Out.WriteLine('${startedMarker}'); [Console]::Out.Flush();`,
    `$process = Get-Process -Id $PID;`,
    `[System.IO.File]::WriteAllText('${identityPath}', ([string]$PID + '|' + [string]$process.StartTime.ToUniversalTime().Ticks));`,
    `[System.IO.File]::WriteAllText('${startedPath}', 'started');`,
    `Start-Sleep -Seconds ${delaySeconds};`,
    `[System.IO.File]::WriteAllText('${latePath}', 'late');`,
  ].join(" ");
  return {
    name,
    prompt: scenarioPrompt,
    callId,
    scriptPath,
    scriptContent,
    command: `& '${scriptPath}'`,
    startedPath,
    identityPath,
    latePath,
    startedMarker,
    delaySeconds,
  };
}

const stopScenario = createCommandScenario("stop", prompt, 5);
const staleScenario = createCommandScenario("stale", stalePrompt, 15);
const naturalScenario = createCommandScenario("natural", naturalPrompt, 10);
const transportScenario = createCommandScenario("transport", transportPrompt, 30);
const commandScenarios = [stopScenario, staleScenario, naturalScenario, transportScenario];
const { callId, startedPath, identityPath, latePath } = stopScenario;
const lateMarkerDelaySeconds = stopScenario.delaySeconds;
const lateMarkerObservationMilliseconds = lateMarkerDelaySeconds * 1000 + 250;

for (const filePath of [capabilityPath, ...commandScenarios.flatMap((scenario) => [
  scenario.scriptPath,
  scenario.startedPath,
  scenario.identityPath,
  scenario.latePath,
])]) {
  assert.ok(!filePath.match(/\s|'/), `fixture path requires unsupported PowerShell escaping: ${filePath}`);
}
assert.equal(existsSync(capabilityPath), false, "refusing to overwrite a synthetic capability record");
for (const scenario of commandScenarios) {
  writeFileSync(scenario.scriptPath, scenario.scriptContent, { encoding: "utf8", flag: "wx" });
}

const unknown = (evidenceSource) => ({ state: "Unknown", value: null, evidenceSource });
const capability = {
  providerId: "lmstudio",
  providerDisplayName: "LM Studio · P3-02 deterministic command-stop fixture",
  providerServerVersion: "Test fixture; no LM Studio server",
  endpoint: "pending-loopback-endpoint",
  modelIdentifier,
  modelVariant: "synthetic Responses fixture; no model weights or live inference",
  architecture: "Unknown",
  parameterCount: "Unknown",
  modelSizeBytes: "Unknown",
  quantization: unknown("Synthetic P3-02 fixture; no model weights"),
  contextWindowAdvertised: unknown("Synthetic P3-02 fixture; no provider metadata"),
  contextWindowEffective: { state: "Known", value: 32768, evidenceSource: "Synthetic test catalog only; not model metadata" },
  reasoningControls: unknown("Synthetic P3-02 fixture; no model metadata"),
  toolFunctionCalling: { state: "Known", value: "tool_use", evidenceSource: "Synthetic fixture to exercise one command tool call" },
  applyPatchToolType: unknown("Synthetic P3-02 fixture; patch call not exercised"),
  structuredOutput: unknown("Synthetic P3-02 fixture; no provider metadata"),
  agentMetadata: {
    state: "Known",
    value: "type=llm; inputModalities=text; vision=False; capabilities=tool_use",
    evidenceSource: "Synthetic Codex test catalog only; not a provider/model claim",
  },
};

function sseEvent(event) {
  return `event: ${event.type}\ndata: ${JSON.stringify(event)}\n\n`;
}

function toolResponse(scenario) {
  const responseId = `p3-02-native-${scenario.name}-response`;
  return [
    { type: "response.created", response: { id: responseId } },
    {
      type: "response.output_item.done",
      item: {
        type: "function_call",
        call_id: scenario.callId,
        name: "exec_command",
        arguments: JSON.stringify({ cmd: scenario.command, workdir: workspace }),
      },
    },
    {
      type: "response.completed",
      response: {
        id: responseId,
        usage: { input_tokens: 0, input_tokens_details: null, output_tokens: 0, output_tokens_details: null, total_tokens: 0 },
      },
    },
  ].map(sseEvent).join("");
}

const providerRequests = [];
const fixtureErrors = [];
const fixtureServer = createHttpServer((request, response) => {
  if (request.method !== "POST" || request.url !== "/v1/responses") {
    fixtureErrors.push(`unexpected provider request ${request.method} ${request.url}`);
    response.writeHead(404).end();
    return;
  }
  let body = "";
  request.setEncoding("utf8");
  request.on("data", (chunk) => { body += chunk; });
  request.on("end", () => {
    let payload;
    try { payload = JSON.parse(body); }
    catch {
      fixtureErrors.push("Responses request body was not valid JSON");
      response.writeHead(400).end();
      return;
    }
    providerRequests.push(payload);
    const scenario = commandScenarios[providerRequests.length - 1];
    if (payload.model !== modelIdentifier) {
      fixtureErrors.push(`request used unexpected model ${payload.model ?? "<missing>"}`);
      response.writeHead(409).end();
      return;
    }
    if (!scenario) {
      fixtureErrors.push(`unexpected provider continuation/fallback request ${providerRequests.length}`);
      response.writeHead(409).end();
      return;
    }
    const inputText = JSON.stringify(payload.input ?? []);
    if (!inputText.includes(scenario.prompt)) {
      fixtureErrors.push(`the exact ${scenario.name} test prompt was absent from request ${providerRequests.length}`);
    }
    response.writeHead(200, { "content-type": "text/event-stream", "cache-control": "no-cache" });
    response.end(toolResponse(scenario));
  });
});

function listen(server) {
  return new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", () => {
      const address = server.address();
      if (!address || typeof address === "string") reject(new Error("loopback fixture did not bind a TCP port"));
      else resolve(address.port);
    });
  });
}

function reservePort() {
  const server = createTcpServer();
  return new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", () => {
      const address = server.address();
      if (!address || typeof address === "string") reject(new Error("could not reserve a local WebView2 debug port"));
      else server.close((error) => error ? reject(error) : resolve(address.port));
    });
  });
}

function waitForExit(child, timeoutMs) {
  if (child.exitCode !== null) return Promise.resolve({ code: child.exitCode, signal: child.signalCode });
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`QA host PID ${child.pid} did not exit within ${timeoutMs}ms`)), timeoutMs);
    child.once("exit", (code, signal) => { clearTimeout(timer); resolve({ code, signal }); });
  });
}

function readProcessStartTicks(pid) {
  const commandText = `$process = Get-Process -Id ${pid} -ErrorAction Stop; [string]$process.StartTime.ToUniversalTime().Ticks`;
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", commandText], {
    encoding: "utf8", timeout: 10000,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`could not capture QA host PID ${pid} identity: ${result.stderr || result.stdout}`);
  const startTicks = result.stdout.trim();
  if (!/^\d{15,}$/.test(startTicks)) throw new Error(`QA host PID ${pid} returned invalid start-time identity`);
  return startTicks;
}

function requestHostClose(pid, expectedStartTicks) {
  assert.ok(Number.isSafeInteger(pid) && pid > 0 && /^\d{15,}$/.test(expectedStartTicks),
    "refusing PID close without a validated exact process identity");
  const commandText = `$process = Get-Process -Id ${pid} -ErrorAction SilentlyContinue; if (-not $process -or [int64]$process.StartTime.ToUniversalTime().Ticks -ne [int64]${expectedStartTicks}) { exit 3 }; if (-not $process.CloseMainWindow()) { exit 2 }; exit 0`;
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", commandText], {
    encoding: "utf8", timeout: 10000,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`could not gracefully close exact QA host PID ${pid}: ${result.stderr || result.stdout}`);
}

function sameCommandProcessIsRunning(processId, startTicks) {
  assert.ok(Number.isSafeInteger(processId) && processId > 0 && /^\d{15,}$/.test(startTicks),
    "refusing process inspection without a validated exact identity");
  const commandText = `$process = Get-Process -Id ${processId} -ErrorAction SilentlyContinue; if ($process -and [int64]$process.StartTime.ToUniversalTime().Ticks -eq [int64]${startTicks}) { 'same-process' }; exit 0`;
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", commandText], {
    encoding: "utf8", timeout: 10000,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`could not inspect exact command PID ${processId}: ${result.stderr || result.stdout}`);
  return result.stdout.trim() === "same-process";
}

function stopExactAppServerProcess(processId, startTicks, expectedBinaryPath) {
  assert.ok(Number.isSafeInteger(processId) && processId > 0 && /^\d{15,}$/.test(startTicks),
    "refusing App Server termination without a validated exact process identity");
  assert.ok(typeof expectedBinaryPath === "string" && path.isAbsolute(expectedBinaryPath) && !expectedBinaryPath.includes("'"),
    "refusing App Server termination without the locked absolute binary path");
  const commandText = `$process = Get-Process -Id ${processId} -ErrorAction SilentlyContinue; if (-not $process -or [int64]$process.StartTime.ToUniversalTime().Ticks -ne [int64]${startTicks}) { exit 3 }; if (-not [string]::Equals([System.IO.Path]::GetFullPath($process.Path), [System.IO.Path]::GetFullPath('${expectedBinaryPath}'), [System.StringComparison]::OrdinalIgnoreCase)) { exit 4 }; $process.Kill(); if (-not $process.WaitForExit(10000)) { exit 5 }; 'stopped-exact-app-server'`;
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", commandText], {
    encoding: "utf8", timeout: 15000,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`could not stop exact pinned App Server PID ${processId}: ${result.stderr || result.stdout}`);
  assert.equal(result.stdout.trim(), "stopped-exact-app-server", "the exact pinned App Server was not stopped");
}

function stopExactCommandIfStillRunning(processId, startTicks) {
  assert.ok(Number.isSafeInteger(processId) && processId > 0 && /^\d{15,}$/.test(startTicks),
    "refusing process cleanup without a validated exact identity");
  const commandText = `$process = Get-Process -Id ${processId} -ErrorAction SilentlyContinue; if ($process -and [int64]$process.StartTime.ToUniversalTime().Ticks -eq [int64]${startTicks}) { $process.Kill(); $process.WaitForExit(); 'stopped-exact-process' }; exit 0`;
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", commandText], {
    encoding: "utf8", timeout: 10000,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`could not clean up exact command PID ${processId}: ${result.stderr || result.stdout}`);
  return result.stdout.trim() === "stopped-exact-process";
}

async function waitFor(predicate, message, timeoutMs = 30000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (await predicate()) return;
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  throw new Error(message);
}

async function requestHostOperation(operation, payload = {}) {
  const requestId = `qa-${randomUUID()}`;
  const response = await page.evaluate(({ operationName, operationPayload, id }) => new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      window.chrome.webview.removeEventListener("message", onMessage);
      reject(new Error(`ApprovalQA host operation ${operationName} timed out`));
    }, 30000);
    function onMessage(event) {
      if (event.data?.requestId !== id || event.data?.stream === true) return;
      clearTimeout(timer);
      window.chrome.webview.removeEventListener("message", onMessage);
      resolve(event.data);
    }
    window.chrome.webview.addEventListener("message", onMessage);
    window.chrome.webview.postMessage({ operation: operationName, requestId: id, ...operationPayload });
  }), { operationName: operation, operationPayload: payload, id: requestId });
  assert.equal(response.requestId, requestId, "the native host response lost its request identity");
  return response;
}

const { chromium } = await import(pathToFileURL(playwrightModulePath).href);
let host;
let hostStartTicks;
let browser;
let page;
let capabilityBytes = null;
let capabilityCreated = false;
const commandIdentities = [];
const appServerIdentities = [];
let runError = null;
const pageIssues = [];

async function launchHost() {
  const debugPort = await reservePort();
  const hostEnvironment = { ...process.env };
  for (const name of ["OPENROUTER_API_KEY", "OPENAI_API_KEY", "LM_STUDIO_API_KEY", "NEOBABYLON_WINDOWS_SANDBOX_MODE"]) {
    delete hostEnvironment[name];
  }
  Object.assign(hostEnvironment, {
    NEOBABYLON_SOURCE_ROOT: sourceRoot,
    NEOBABYLON_APPLICATION_ROOT: applicationRoot,
    NEOBABYLON_MODEL_CAPABILITY_PATH: capabilityPath,
    NEOBABYLON_ENABLE_WEBVIEW2_REMOTE_DEBUG: "1",
    NEOBABYLON_WEBVIEW2_DEBUG_PORT: String(debugPort),
  });
  host = spawn(hostPath, [], { cwd: sourceRoot, env: hostEnvironment, stdio: "ignore", windowsHide: false });
  hostStartTicks = readProcessStartTicks(host.pid);
  const cdpDeadline = Date.now() + 45000;
  while (Date.now() < cdpDeadline) {
    if (host.exitCode !== null) throw new Error(`WPF host exited before CDP opened (code ${host.exitCode})`);
    try {
      const result = await fetch(`http://127.0.0.1:${debugPort}/json/version`, { signal: AbortSignal.timeout(1000) });
      if (result.ok) break;
    } catch { /* WebView2 is starting. */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  browser = await chromium.connectOverCDP(`http://127.0.0.1:${debugPort}`);
  const pageDeadline = Date.now() + 30000;
  while (Date.now() < pageDeadline) {
    page = browser.contexts().flatMap((context) => context.pages())
      .find((candidate) => candidate.url().startsWith("https://neobabylon.local/"));
    if (page) break;
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  assert.ok(page, "native NeoBabylon WebView page did not appear");
  page.on("pageerror", (error) => pageIssues.push(error.message));
  page.on("console", (message) => { if (message.type() === "error") pageIssues.push(message.text()); });
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  assert.equal(await page.title(), "NeoBabylon");
  await page.getByRole("textbox", { name: "Message NeoBabylon" }).waitFor();
}

async function closeHost() {
  if (browser) await browser.close().catch(() => {});
  browser = null;
  if (host && host.exitCode === null) {
    assert.ok(typeof hostStartTicks === "string" && /^\d{15,}$/.test(hostStartTicks),
      "QA host has no captured process identity; refusing PID-only close");
    requestHostClose(host.pid, hostStartTicks);
    const result = await waitForExit(host, 20000);
    assert.equal(result.code, 0, `WPF host exited unexpectedly: ${JSON.stringify(result)}`);
  }
  host = null;
  page = null;
}

async function readApprovalQaAppServerIdentity() {
  const response = await requestHostOperation("getDiagnostics");
  assert.equal(response.ok, true, `ApprovalQA diagnostics failed: ${JSON.stringify(response.error)}`);
  const identity = response.result?.approvalQaAppServerIdentity;
  assert.ok(Number.isSafeInteger(identity?.processId) && identity.processId > 0
    && typeof identity?.startTimeUtcTicks === "string"
    && /^\d{15,}$/.test(identity.startTimeUtcTicks),
    "ApprovalQA returned an invalid App Server process identity");
  return identity;
}

async function currentRuntimeThreadLabel() {
  return page.locator(".execution-row").filter({ hasText: "Runtime thread" }).locator("strong").innerText();
}

async function runInterruptedScenario(scenario, expectedRequestCount) {
  const eventOffset = await page.evaluate(() => window.__p302StopEvents.length);
  const composer = page.getByRole("textbox", { name: "Message NeoBabylon" });
  await composer.fill(scenario.prompt);
  await page.getByRole("button", { name: "Send message" }).click();
  await waitFor(() => providerRequests.length === expectedRequestCount,
    `the ${scenario.name} request did not reach the loopback Responses fixture`, 45000);
  const request = providerRequests[expectedRequestCount - 1];
  assert.equal(request.model, modelIdentifier, `${scenario.name} used a different fixture model`);
  assert.ok(JSON.stringify(request.input ?? []).includes(scenario.prompt),
    `the exact ${scenario.name} prompt was absent from its provider request`);
  assert.ok(!request.allow_fallback, `${scenario.name} unexpectedly enabled provider/model fallback`);

  const approvalCard = page.locator("section.approval-card");
  await waitFor(async () => await approvalCard.count() === 1,
    `the ${scenario.name} command did not receive its one-shot approval`, 4000);
  const approvalText = await approvalCard.innerText();
  assert.ok(approvalText.toLowerCase().includes(path.basename(scenario.scriptPath).toLowerCase()),
    `${scenario.name} approval omitted its exact QA command`);
  assert.ok(approvalText.includes(workspace), `${scenario.name} approval omitted the isolated workspace path`);
  await approvalCard.getByRole("button", { name: "Approve once", exact: true }).click();

  await waitFor(() => existsSync(scenario.startedPath) && existsSync(scenario.identityPath),
    `the ${scenario.name} command did not enter its controlled wait`, 30000);
  const identityParts = readFileSync(scenario.identityPath, "utf8").split("|");
  assert.equal(identityParts.length, 2, `${scenario.name} command did not record its exact process identity`);
  const identity = { processId: Number(identityParts[0]), startTicks: identityParts[1] };
  assert.ok(Number.isSafeInteger(identity.processId) && identity.processId > 0 && /^\d{15,}$/.test(identity.startTicks),
    `${scenario.name} command recorded a malformed process identity`);
  commandIdentities.push(identity);
  assert.ok(sameCommandProcessIsRunning(identity.processId, identity.startTicks),
    `${scenario.name} command was not alive before turn interruption`);

  await waitFor(async () => {
    const events = await page.evaluate((offset) => window.__p302StopEvents.slice(offset), eventOffset);
    return events.some((event) => event.method === "turn/started" && event.params?.turn?.id)
      && events.some((event) => event.method === "item/started"
        && event.params?.item?.type === "commandExecution" && event.params?.item?.id === scenario.callId);
  }, `native WPF did not observe the ${scenario.name} App Server turn and command start`);
  const events = await page.evaluate((offset) => window.__p302StopEvents.slice(offset), eventOffset);
  const turnStarted = events.find((event) => event.method === "turn/started" && event.params?.turn?.id);
  const commandStarted = events.find((event) => event.method === "item/started"
    && event.params?.item?.type === "commandExecution" && event.params?.item?.id === scenario.callId);
  assert.ok(turnStarted && commandStarted, `${scenario.name} stream omitted its exact turn or item identity`);

  await page.getByRole("button", { name: "Stop turn" }).click();
  await page.waitForFunction(() => document.querySelector("section.conversation")?.getAttribute("data-turn-state") === "interrupted",
    null, { timeout: 30000 });
  const commandCard = page.locator(".activity-card").filter({ hasText: path.basename(scenario.scriptPath) });
  await commandCard.waitFor({ timeout: 30000 });
  assert.equal((await commandCard.locator(".activity-status-label").innerText()).trim().toLowerCase(), "running",
    `${scenario.name} turn interruption did not visibly leave its command running`);
  assert.match(await commandCard.innerText(), /still running after the turn was interrupted/i,
    `${scenario.name} UI did not explain that Stop ends generation only`);
  assert.ok(sameCommandProcessIsRunning(identity.processId, identity.startTicks),
    `${scenario.name} turn interruption unexpectedly stopped the command`);
  assert.equal(providerRequests.length, expectedRequestCount,
    `${scenario.name} interruption caused a provider continuation or fallback`);
  const screenshotPath = path.join(runRoot, `${scenario.name}-command-running-after-turn-stop.png`);
  await page.screenshot({ path: screenshotPath });
  return {
    scenario,
    commandIdentity: identity,
    commandCard,
    itemId: commandStarted.params.item.id,
    threadId: turnStarted.params.threadId,
    turnId: turnStarted.params.turn.id,
    screenshotPath,
  };
}

const fixturePort = await listen(fixtureServer);
capability.endpoint = `http://127.0.0.1:${fixturePort}/v1`;

try {
  capabilityBytes = `${JSON.stringify(capability, null, 2)}\n`;
  writeFileSync(capabilityPath, capabilityBytes, { encoding: "utf8", flag: "wx" });
  capabilityCreated = true;
  await launchHost();

  await page.getByRole("button", { name: "Open diagnostics" }).click();
  const diagnostics = page.getByRole("dialog", { name: "Runtime diagnostics" });
  await diagnostics.waitFor();
  const diagnosticsText = await diagnostics.innerText();
  assert.ok(diagnosticsText.includes(applicationRoot), "diagnostics omitted the isolated application root");
  assert.ok(diagnosticsText.includes(dataRoot), "diagnostics omitted the isolated Data root");
  assert.match(diagnosticsText, /Ordinary Codex root used\s+false/i, "ordinary Codex root use was not false");
  await diagnostics.getByRole("button", { name: "Done" }).click();
  await page.evaluate(() => {
    window.__p302StopEvents = [];
    window.__p302StopResults = [];
    window.chrome.webview.addEventListener("message", ({ data }) => {
      if (data?.attributedTo === "Codex App Server" && typeof data.method === "string") {
        window.__p302StopEvents.push({ method: data.method, params: data.params });
      }
      if (data?.result?.eventType === "commandExecutionStopResult") {
        window.__p302StopResults.push(data.result);
      }
    });
  });

  const composer = page.getByRole("textbox", { name: "Message NeoBabylon" });
  await composer.fill(prompt);
  await page.getByRole("button", { name: "Send message" }).click();
  await waitFor(() => providerRequests.length === 1, "the synthetic request did not reach the loopback Responses fixture", 45000);
  assert.equal(providerRequests[0].model, modelIdentifier, "the exact selected fixture model changed");
  assert.ok(!providerRequests[0].allow_fallback, "provider/model fallback was unexpectedly enabled");
  const firstAppServerIdentity = await readApprovalQaAppServerIdentity();
  appServerIdentities.push(firstAppServerIdentity);

  const approvalCard = page.locator("section.approval-card");
  await waitFor(async () => await approvalCard.count() === 1,
    "the controlled command did not receive its one-shot host approval", 4000);
  const approvalText = await approvalCard.innerText();
  assert.match(approvalText, /p3-02-command-stop\.ps1/i, "one-shot approval omitted the exact test command");
  assert.ok(approvalText.includes(workspace), "one-shot approval omitted the isolated workspace path");
  await approvalCard.getByRole("button", { name: "Approve once", exact: true }).click();

  await waitFor(() => existsSync(startedPath) && existsSync(identityPath),
    "the exact permitted command did not enter its controlled wait", 30000);
  const identityParts = readFileSync(identityPath, "utf8").split("|");
  assert.equal(identityParts.length, 2, "the fixture command did not record exact process identity");
  const processId = Number(identityParts[0]);
  const startTicks = identityParts[1];
  assert.ok(Number.isSafeInteger(processId) && processId > 0 && /^\d{15,}$/.test(startTicks),
    "the fixture command recorded malformed process identity");
  commandIdentities.push({ processId, startTicks });
  assert.ok(sameCommandProcessIsRunning(processId, startTicks), "the command was not still running before turn interruption");

  await waitFor(async () => {
    const events = await page.evaluate(() => window.__p302StopEvents ?? []);
    return events.some((event) => event.method === "turn/started" && event.params?.turn?.id)
      && events.some((event) => event.method === "item/started"
        && event.params?.item?.type === "commandExecution" && event.params?.item?.id === callId);
  }, "native UI did not observe the exact App Server turn and command start");
  const turnEvents = await page.evaluate(() => window.__p302StopEvents ?? []);
  const started = turnEvents.find((event) => event.method === "turn/started" && event.params?.turn?.id);
  assert.ok(started, "native UI did not receive the exact App Server turn-start identity");
  const commandStarted = turnEvents.find((event) => event.method === "item/started"
    && event.params?.item?.type === "commandExecution" && event.params?.item?.id === callId);
  assert.ok(commandStarted, "native UI did not observe the exact command item start");

  await page.screenshot({ path: path.join(runRoot, "command-running-before-turn-stop.png") });
  await page.getByRole("button", { name: "Stop turn" }).click();
  await page.waitForFunction(() => document.querySelector("section.conversation")?.getAttribute("data-turn-state") === "interrupted",
    null, { timeout: 30000 });
  const commandCard = page.locator(".activity-card").filter({ hasText: /p3-02-command-stop\.ps1/i });
  await commandCard.waitFor({ timeout: 30000 });
  assert.equal((await commandCard.locator(".activity-status-label").innerText()).trim().toLowerCase(), "running",
    "turn interruption did not visibly leave the command marked running");
  assert.match(await commandCard.innerText(), /still running after the turn was interrupted/i,
    "the UI did not explain that turn Stop leaves the command running");
  assert.equal((await commandCard.locator(".activity-attribution").innerText()).trim(), "Codex App Server",
    "the continuing command lost its App Server attribution");
  assert.ok(sameCommandProcessIsRunning(processId, startTicks), "turn interruption unexpectedly stopped the command");
  assert.equal(providerRequests.length, 1, "turn interruption caused a provider continuation or fallback");
  await page.screenshot({ path: interruptedScreenshotPath });

  const stopButton = commandCard.getByRole("button", { name: "Stop command" });
  await stopButton.focus();
  assert.equal(await stopButton.evaluate((element) => document.activeElement === element), true,
    "the separate command-stop action is not keyboard-focusable");
  await stopButton.press("Enter");
  await waitFor(async () => (await commandCard.locator(".activity-status-label").innerText()).trim().toLowerCase() === "stopped",
    "the separate Stop action did not reach the visibly confirmed stopped state", 10000);
  assert.equal(await commandCard.getByRole("button", { name: "Stop command" }).count(), 0,
    "a confirmed command stop remained actionable");
  assert.match(await commandCard.innerText(), /confirmed that this exact command was stopped/i,
    "the UI did not explain the exact App Server stop result");
  assert.equal((await commandCard.locator(".activity-attribution").innerText()).trim(), "Codex App Server",
    "the successful Stop result lost its App Server attribution");
  await page.screenshot({ path: stoppedScreenshotPath });

  const stopDeadline = Date.now() + 5000;
  while (sameCommandProcessIsRunning(processId, startTicks) && Date.now() < stopDeadline) {
    await new Promise((resolve) => setTimeout(resolve, 50));
  }
  assert.equal(sameCommandProcessIsRunning(processId, startTicks), false,
    "the exact command process remained alive after the UI displayed confirmed Stop");
  await new Promise((resolve) => setTimeout(resolve, lateMarkerObservationMilliseconds));
  assert.equal(existsSync(latePath), false,
    `the command's late marker appeared after the scheduled ${lateMarkerDelaySeconds}-second completion time`);
  const duplicateStopResponse = await requestHostOperation("stopCommand", { itemId: commandStarted.params.item.id });
  assert.equal(duplicateStopResponse.ok, true, `duplicate Stop bridge request failed: ${JSON.stringify(duplicateStopResponse.error)}`);
  const duplicateStop = duplicateStopResponse.result;
  assert.equal(duplicateStop?.attributedTo, "NeoBabylon.Host", "duplicate Stop was not reported by the trusted host");
  assert.equal(duplicateStop?.status, "not_found", "duplicate Stop did not fail closed after confirmed completion");
  assert.equal(duplicateStop?.commandStopAvailable, false, "duplicate Stop incorrectly remained actionable");

  assert.equal(providerRequests.length, 1, "the tool stop caused a provider continuation or model fallback");

  await page.getByRole("button", { name: "New task" }).click();
  await waitFor(async () => await page.locator(".execution-row").filter({ hasText: "Runtime thread" }).count() === 0,
    "New task did not clear the selected runtime thread before the stale-identity case");
  appServerIdentities.push(await readApprovalQaAppServerIdentity());
  const staleRun = await runInterruptedScenario(staleScenario, 2);
  const staleItemId = staleRun.itemId;
  await page.getByRole("button", { name: "New task" }).click();
  await waitFor(async () => await page.locator(".execution-row").filter({ hasText: "Runtime thread" }).count() === 0,
    "New task did not invalidate the previous runtime selection before the stale-identity check");
  const naturalRun = await runInterruptedScenario(naturalScenario, 3);
  const staleStopResponse = await requestHostOperation("stopCommand", { itemId: staleItemId });
  assert.equal(staleStopResponse.ok, true, `stale Stop bridge request failed: ${JSON.stringify(staleStopResponse.error)}`);
  const staleStop = staleStopResponse.result;
  assert.equal(staleStop?.attributedTo, "NeoBabylon.Host", "stale Stop was not rejected by the trusted host");
  assert.equal(staleStop?.status, "not_found", "an old thread's cleared Stop identity remained usable in the new thread");
  assert.equal(staleStop?.commandStopAvailable, false, "a stale thread identity remained actionable");
  assert.ok(sameCommandProcessIsRunning(naturalRun.commandIdentity.processId, naturalRun.commandIdentity.startTicks),
    "a stale Stop request from the previous thread touched the currently selected command");
  if (sameCommandProcessIsRunning(staleRun.commandIdentity.processId, staleRun.commandIdentity.startTicks)) {
    stopExactCommandIfStillRunning(staleRun.commandIdentity.processId, staleRun.commandIdentity.startTicks);
  }
  const naturalExitDeadline = Date.now() + 15000;
  while (sameCommandProcessIsRunning(naturalRun.commandIdentity.processId, naturalRun.commandIdentity.startTicks)
      && Date.now() < naturalExitDeadline) {
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  assert.equal(sameCommandProcessIsRunning(naturalRun.commandIdentity.processId, naturalRun.commandIdentity.startTicks), false,
    "the natural-exit fixture process did not exit by itself");
  assert.equal(existsSync(naturalScenario.latePath), true,
    "the natural-exit fixture did not reach its scheduled completion side effect");
  await waitFor(async () => {
    const events = await page.evaluate(() => window.__p302StopEvents ?? []);
    return events.some((event) => event.method === "item/completed"
      && event.params?.threadId === naturalRun.threadId
      && event.params?.turnId === naturalRun.turnId
      && event.params?.item?.type === "commandExecution"
      && event.params?.item?.id === naturalRun.itemId);
  }, "the native host did not forward the exact late command-completion event after turn interruption", 5000);
  const naturalCompletionEvents = await page.evaluate(() => window.__p302StopEvents ?? []);
  const naturalCompletion = naturalCompletionEvents.find((event) => event.method === "item/completed"
    && event.params?.threadId === naturalRun.threadId
    && event.params?.turnId === naturalRun.turnId
    && event.params?.item?.type === "commandExecution"
    && event.params?.item?.id === naturalRun.itemId);
  assert.ok(naturalCompletion, "the exact late command-completion event disappeared after observation");
  assert.equal(naturalCompletion.params.item.status, "completed",
    "the late command-completion event did not preserve App Server's terminal item status");
  await waitFor(async () => {
    const state = (await naturalRun.commandCard.locator(".activity-status-label").innerText()).trim().toLowerCase();
    return state !== "running" && state !== "stopping";
  }, "the native activity card did not leave running state after natural command exit", 5000);
  const naturalVisibleState = (await naturalRun.commandCard.locator(".activity-status-label").innerText()).trim().toLowerCase();
  assert.equal(await naturalRun.commandCard.getByRole("button", { name: /Stop command/ }).count(), 0,
    "a naturally exited command still exposed an actionable Stop control");
  await page.screenshot({ path: path.join(runRoot, "natural-command-exit-visible.png") });
  const naturalDuplicateResponse = await requestHostOperation("stopCommand", { itemId: naturalRun.itemId });
  assert.equal(naturalDuplicateResponse.ok, true,
    `natural-exit Stop status request failed: ${JSON.stringify(naturalDuplicateResponse.error)}`);
  assert.ok(["not_found", "already_exited"].includes(naturalDuplicateResponse.result?.status),
    "a natural exit was reported as an active or newly stopped process");
  assert.equal(providerRequests.length, 3, "natural command completion triggered a provider continuation or fallback");

  await page.getByRole("button", { name: "New task" }).click();
  await waitFor(async () => await page.locator(".execution-row").filter({ hasText: "Runtime thread" }).count() === 0,
    "New task did not clear the selected runtime thread before the transport-failure case");
  const transportRun = await runInterruptedScenario(transportScenario, 4);
  const transportServerIdentity = await readApprovalQaAppServerIdentity();
  appServerIdentities.push(transportServerIdentity);
  stopExactAppServerProcess(
    transportServerIdentity.processId,
    transportServerIdentity.startTimeUtcTicks,
    runtimePath);
  const transportStopButton = transportRun.commandCard.getByRole("button", { name: "Stop command" });
  await transportStopButton.click();
  await waitFor(async () => (await transportRun.commandCard.locator(".activity-status-label").innerText()).trim().toLowerCase() === "unknown",
    "transport failure did not produce a visible conservative unknown command state", 10000);
  assert.equal(await transportRun.commandCard.getByRole("button", { name: /Retry stop/ }).count(), 0,
    "Stop against a lost App Server instance incorrectly remained retryable");
  assert.equal((await transportRun.commandCard.locator(".activity-attribution").innerText()).trim(), "NeoBabylon.Host",
    "transport failure was not attributed to the trusted host");
  const transportStopResults = await page.evaluate(() => window.__p302StopResults ?? []);
  const transportStop = transportStopResults.at(-1);
  assert.equal(transportStop?.status, "unknown", "the host did not return a typed unknown transport result");
  assert.equal(transportStop?.commandStopAvailable, false, "transport failure allowed retry against a replaced App Server");
  await page.screenshot({ path: path.join(runRoot, "transport-failure-visible.png") });

  assert.equal(providerRequests.length, 4, "P3-02 command Stop fixtures caused provider continuation or fallback");
  assert.deepEqual(fixtureErrors, [], fixtureErrors.join("; "));
  assert.deepEqual(pageIssues, [], "native WebView reported page or console errors");

  const result = {
    passed: true,
    slice: "P3-02 native identity-bound command Stop",
    evidenceKind: "pinned App Server plus deterministic loopback Responses; no live inference",
    runtime: {
      version: runtime.version,
      sourceRevision: runtime.sourceRevision,
      path: runtimePath,
      sha256: runtimeHash,
      sourcePatchSha256: runtime.sourcePatchSha256,
    },
    selectedModel: modelIdentifier,
    applicationRoot,
    dataRoot,
    sourceRoot,
    ordinaryCodexRootUsed: false,
    approvalQaAppServerIdentities: appServerIdentities,
    threadId: started.params.threadId,
    turnId: started.params.turn.id,
    commandItemId: commandStarted.params.item.id,
    turnInterruptionState: "interrupted",
    commandWasStillRunningAfterTurnInterruption: true,
    visibleCommandStopState: "stopped",
    visibleCommandStopAttribution: "Codex App Server",
    commandProcessAliveAfterConfirmedStop: false,
    commandLateMarkerObserved: existsSync(latePath),
    lateMarkerDelaySeconds,
    lateMarkerObservationMilliseconds,
    duplicateStopStatus: duplicateStop.status,
    staleThreadStopStatus: staleStop.status,
    naturalExit: {
      processExitedWithoutStop: true,
      scheduledMarkerObserved: existsSync(naturalScenario.latePath),
      visibleState: naturalVisibleState,
      staleStopStatus: naturalDuplicateResponse.result.status,
    },
    transportFailure: {
      exactAppServerProcessStopped: true,
      status: transportStop.status,
      retryAvailable: transportStop.commandStopAvailable,
      attribution: transportStop.attributedTo,
    },
    providerRequestCount: providerRequests.length,
    approvedExactCommandOneShot: true,
    retryOrFallback: false,
    pageIssues,
    fixtureErrors,
    screenshots: [
      path.join(runRoot, "command-running-before-turn-stop.png"),
      interruptedScreenshotPath,
      stoppedScreenshotPath,
      staleRun.screenshotPath,
      naturalRun.screenshotPath,
      path.join(runRoot, "natural-command-exit-visible.png"),
      transportRun.screenshotPath,
      path.join(runRoot, "transport-failure-visible.png"),
    ],
    runRoot,
  };
  writeFileSync(resultPath, `${JSON.stringify(result, null, 2)}\n`, "utf8");
  console.log(JSON.stringify(result));
} catch (error) {
  runError = error;
  const diagnostics = page ? await page.evaluate(() => ({
    turnState: document.querySelector("section.conversation")?.getAttribute("data-turn-state"),
    activity: document.querySelector(".activity-stack")?.innerText ?? null,
    body: document.body.innerText.slice(0, 2500),
  })).catch((inspectionError) => ({ inspectionError: String(inspectionError) })) : null;
  const hostDiagnostics = page
    ? await requestHostOperation("getDiagnostics").catch((inspectionError) => ({
      ok: false,
      error: { message: String(inspectionError) },
    }))
    : null;
  writeFileSync(resultPath, `${JSON.stringify({
    passed: false,
    slice: "P3-02 native identity-bound command Stop",
    error: error instanceof Error ? `${error.name}: ${error.message}` : String(error),
    runtime: { version: runtime.version, sourceRevision: runtime.sourceRevision, sha256: runtimeHash },
    selectedModel: modelIdentifier,
    approvalQaAppServerIdentities: appServerIdentities,
    approvalQaAppServerIdentityAtFailure: hostDiagnostics?.result?.approvalQaAppServerIdentity ?? null,
    hostDiagnosticsFailure: hostDiagnostics?.ok === false ? hostDiagnostics.error : null,
    providerRequestCount: providerRequests.length,
    fixtureErrors,
    pageIssues,
    diagnostics,
    runRoot,
  }, null, 2)}\n`, "utf8");
  throw error;
} finally {
  for (const scenario of commandScenarios) {
    if (!existsSync(scenario.identityPath)) continue;
    const [rawProcessId, startTicks] = readFileSync(scenario.identityPath, "utf8").split("|");
    const processId = Number(rawProcessId);
    if (Number.isSafeInteger(processId) && processId > 0 && /^\d{15,}$/.test(startTicks)
        && !commandIdentities.some((identity) => identity.processId === processId && identity.startTicks === startTicks)) {
      commandIdentities.push({ processId, startTicks });
    }
  }
  for (const identity of commandIdentities) {
    if (sameCommandProcessIsRunning(identity.processId, identity.startTicks)) {
      stopExactCommandIfStillRunning(identity.processId, identity.startTicks);
    }
  }
  await closeHost().catch((closeError) => {
    if (!runError) throw closeError;
  });
  if (fixtureServer.listening) {
    fixtureServer.close();
    fixtureServer.closeAllConnections();
  }
  if (capabilityCreated && capabilityBytes !== null && existsSync(capabilityPath)) {
    if (readFileSync(capabilityPath, "utf8") === capabilityBytes) unlinkSync(capabilityPath);
    else console.error(`Synthetic capability record changed externally and was preserved: ${capabilityPath}`);
  }
}
