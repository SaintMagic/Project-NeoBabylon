import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { spawn, spawnSync } from "node:child_process";
import { createServer as createHttpServer } from "node:http";
import { createServer as createTcpServer } from "node:net";
import { existsSync, mkdirSync, readFileSync, unlinkSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { assertQaRunsParent, newQaRun } from "./qa-run-root.mjs";

const [hostPathArg, playwrightModulePathArg, qaParentArg] = process.argv.slice(2);
assert.ok(hostPathArg && playwrightModulePathArg && qaParentArg,
  "usage: node native-approval-qualification.mjs <ApprovalQA NeoBabylon.Host.exe> <playwright-core-entry.mjs> <exact .local\\Lab\\Runs parent>");

const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const hostPath = path.resolve(hostPathArg);
const playwrightModulePath = path.resolve(playwrightModulePathArg);
const qaParent = path.resolve(qaParentArg);
const releaseRoot = path.join(sourceRoot, "docs", "release");
assertQaRunsParent(sourceRoot, qaParent);
for (const [label, filePath] of [["ApprovalQA WPF host", hostPath], ["Playwright module", playwrightModulePath]]) {
  assert.ok(existsSync(filePath), `${label} does not exist: ${filePath}`);
}

const runtimeLock = JSON.parse(readFileSync(path.join(sourceRoot, "runtime", "runtime-lock.json"), "utf8"));
const runtime = runtimeLock.runtime;
const runtimeBinaryPath = path.resolve(sourceRoot, runtime.appServerBinaryRelativePath);
assert.ok(existsSync(runtimeBinaryPath), `pinned App Server binary is missing: ${runtimeBinaryPath}`);
const runtimeSha256 = createHash("sha256").update(readFileSync(runtimeBinaryPath)).digest("hex");
assert.equal(runtimeSha256, runtime.sha256.toLowerCase(), "pinned App Server binary does not match runtime-lock.json");

const { runRoot } = newQaRun(qaParent, `P2-08-09-native-approvals-${Date.now()}-${process.pid}`);

const modelIdentifier = "p2-08-native-loopback-approval-fixture";
const commandPrompt = "P2-08: issue exactly the harmless Windows version check using exec_command with sandbox_permissions=require_escalated. Do not run any other command or tool.";
const patchPrompt = "P2-09: use only apply_patch to change tracked.txt from before to after. Do not run another tool or edit another path.";
const assistantReply = "The isolated approval qualification request is complete.";
const command = "ver";
const patchText = "*** Begin Patch\n*** Update File: tracked.txt\n@@\n-before\n+after <img src=x onerror=window.__p2_09Injected=true>\n*** End Patch";
const capability = {
  providerId: "lmstudio",
  providerDisplayName: "LM Studio · P2-08/P2-09 deterministic loopback fixture",
  providerServerVersion: "Unknown",
  endpoint: "pending-loopback-endpoint",
  modelIdentifier,
  modelVariant: "test-only Responses fixture; no model weights",
  architecture: "Unknown",
  parameterCount: "Unknown",
  modelSizeBytes: "Unknown",
  quantization: { state: "Unknown", value: null, evidenceSource: "Synthetic approval fixture; no model weights." },
  contextWindowAdvertised: { state: "Unknown", value: null, evidenceSource: "Synthetic approval fixture; no provider metadata." },
  contextWindowEffective: { state: "Known", value: 32768, evidenceSource: "Synthetic test catalog only; not model metadata." },
  reasoningControls: { state: "Unknown", value: null, evidenceSource: "Synthetic approval fixture; no model loaded." },
  toolFunctionCalling: { state: "Known", value: "tool_use", evidenceSource: "Synthetic test-catalog declaration only." },
  applyPatchToolType: {
    state: "Known",
    value: "freeform",
    evidenceSource: "Synthetic catalog declaration required to exercise pinned App Server apply_patch approval; not a provider/model capability claim.",
  },
  structuredOutput: { state: "Unknown", value: null, evidenceSource: "Synthetic approval fixture; no provider metadata." },
  agentMetadata: {
    state: "Known",
    value: "type=llm; inputModalities=text; vision=False",
    evidenceSource: "Required synthetic Codex catalog metadata for this test fixture; not observed model metadata.",
  },
};
const suffix = `${Date.now()}_${process.pid}`;
const capabilityPath = path.join(releaseRoot, `MODEL_CAPABILITY_P2_08_09_NATIVE_${suffix}.json`);
assert.ok(!existsSync(capabilityPath), "refusing to overwrite an existing test capability record");
let capabilityWritten = false;
let capabilityFileHash = null;

function deferred() {
  let resolve;
  const promise = new Promise((complete) => { resolve = complete; });
  return { promise, resolve };
}

function sseEvent(event) {
  return `event: ${event.type}\ndata: ${JSON.stringify(event)}\n\n`;
}

function responseUsage(responseId) {
  return {
    type: "response.completed",
    response: {
      id: responseId,
      usage: { input_tokens: 0, input_tokens_details: null, output_tokens: 0, output_tokens_details: null, total_tokens: 0 },
    },
  };
}

function approvalToolResponse(scenarioId) {
  const responseId = `${scenarioId}-tool-response`;
  if (scenarioId.startsWith("file-change-")) {
    return [
      { type: "response.created", response: { id: responseId } },
      {
        type: "response.output_item.done",
        item: { type: "custom_tool_call", call_id: `${scenarioId}-patch-call`, name: "apply_patch", input: patchText },
      },
      responseUsage(responseId),
    ].map(sseEvent).join("");
  }

  const callId = `${scenarioId}-exec-call`;
  const argumentsText = JSON.stringify({
    cmd: command,
    shell: "cmd.exe",
    workdir: path.join(activeScenario.applicationRoot, "Data", "Workspace"),
    sandbox_permissions: "require_escalated",
    justification: "P2-08 QA only: read the Windows version; no files or external resources are changed.",
  });
  return [
    { type: "response.created", response: { id: responseId } },
    {
      type: "response.output_item.done",
      item: { type: "function_call", call_id: callId, name: "exec_command", arguments: argumentsText },
    },
    responseUsage(responseId),
  ].map(sseEvent).join("");
}

function finalResponse(scenarioId) {
  const responseId = `${scenarioId}-final-response`;
  return [
    { type: "response.created", response: { id: responseId } },
    {
      type: "response.output_item.done",
      item: {
        type: "message", role: "assistant", id: `${scenarioId}-final-message`,
        content: [{ type: "output_text", text: assistantReply }],
      },
    },
    responseUsage(responseId),
  ].map(sseEvent).join("");
}

let activeScenario = null;
let providerRequests = [];
const fixtureErrors = [];
const fixtureServer = createHttpServer((request, response) => {
  if (request.method !== "POST" || request.url !== "/v1/responses") {
    fixtureErrors.push(`unexpected fixture request ${request.method} ${request.url}`);
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
    if (!activeScenario) {
      fixtureErrors.push("Responses request arrived outside an active QA scenario");
      response.writeHead(409).end();
      return;
    }
    const number = providerRequests.length + 1;
    providerRequests.push(payload);
    if (payload.model !== modelIdentifier) {
      fixtureErrors.push(`request ${number} used unexpected model ${payload.model ?? "<missing>"}`);
      response.writeHead(409).end();
      return;
    }
    if (number === 1) {
      activeScenario.firstRequest.resolve(payload);
      response.writeHead(200, { "content-type": "text/event-stream", "cache-control": "no-cache" });
      response.end(approvalToolResponse(activeScenario.id));
      return;
    }
    if (number === 2) {
      activeScenario.secondRequest.resolve(payload);
      response.writeHead(200, { "content-type": "text/event-stream", "cache-control": "no-cache" });
      response.end(finalResponse(activeScenario.id));
      return;
    }
    fixtureErrors.push(`unexpected extra Responses request ${number} in ${activeScenario.id}`);
    response.writeHead(409).end();
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
      server.close((error) => error ? reject(error) : resolve(address.port));
    });
  });
}

function withTimeout(promise, timeoutMs, message) {
  let timer;
  return Promise.race([
    promise,
    new Promise((_, reject) => { timer = setTimeout(() => reject(new Error(message)), timeoutMs); }),
  ]).finally(() => clearTimeout(timer));
}

function requestHostClose(pid) {
  const commandText = `$process = Get-Process -Id ${pid} -ErrorAction Stop; if (-not $process.CloseMainWindow()) { exit 2 }; exit 0`;
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", commandText], {
    encoding: "utf8", timeout: 10000,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`could not request graceful QA host close for PID ${pid}: ${result.stderr || result.stdout}`);
}

function waitForExit(child, timeoutMs) {
  if (child.exitCode !== null) return Promise.resolve({ code: child.exitCode, signal: child.signalCode });
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`process ${child.pid} did not exit within ${timeoutMs}ms`)), timeoutMs);
    child.once("exit", (code, signal) => {
      clearTimeout(timer);
      resolve({ code, signal });
    });
  });
}

function pinnedServerPids() {
  const expected = runtimeBinaryPath.replaceAll("'", "''");
  const commandText = `$expected = [IO.Path]::GetFullPath('${expected}'); Get-CimInstance Win32_Process -Filter \"Name = 'codex-app-server.exe'\" | Where-Object { $_.ExecutablePath -and [IO.Path]::GetFullPath($_.ExecutablePath) -ieq $expected } | ForEach-Object { $_.ProcessId }`;
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", commandText], {
    encoding: "utf8", timeout: 10000,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`could not inspect exact App Server processes: ${result.stderr || result.stdout}`);
  return new Set(result.stdout.split(/\r?\n/).map((line) => Number(line.trim())).filter(Number.isInteger));
}

async function waitFor(predicate, message, timeoutMs = 30000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (predicate()) return;
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  throw new Error(message);
}

async function waitForCdp(port, child) {
  const deadline = Date.now() + 45000;
  while (Date.now() < deadline) {
    if (child.exitCode !== null) throw new Error(`ApprovalQA host exited before CDP opened (code ${child.exitCode})`);
    try {
      const result = await fetch(`http://127.0.0.1:${port}/json/version`, { signal: AbortSignal.timeout(1000) });
      if (result.ok) return await result.json();
    } catch { /* WebView2 is still starting. */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`WebView2 CDP did not open on port ${port}`);
}

async function waitForNativePage(browser, port, child) {
  const deadline = Date.now() + 30000;
  let targets = [];
  while (Date.now() < deadline) {
    if (child.exitCode !== null) throw new Error(`ApprovalQA host exited before its page appeared (code ${child.exitCode})`);
    const page = browser.contexts().flatMap((context) => context.pages())
      .find((candidate) => candidate.url().startsWith("https://neobabylon.local/"));
    if (page) return page;
    try {
      const result = await fetch(`http://127.0.0.1:${port}/json/list`, { signal: AbortSignal.timeout(1000) });
      if (result.ok) targets = await result.json();
    } catch { /* The native target is not ready yet. */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`native NeoBabylon page was not found; CDP targets=${JSON.stringify(targets)}`);
}

function installBridgeCapture() {
  if (window.__p2_08Installed) return;
  window.__p2_08Installed = true;
  window.__p2_08BridgeResponses = [];
  window.__p2_08AppServerEvents = [];
  window.chrome.webview.addEventListener("message", ({ data }) => {
    window.__p2_08BridgeResponses.push({
      requestId: data?.requestId, ok: data?.ok, method: data?.method,
      attributedTo: data?.attributedTo, params: data?.params, data: data?.data,
      error: data?.error?.message,
    });
    if (data?.attributedTo === "Codex App Server" && typeof data.method === "string") {
      window.__p2_08AppServerEvents.push({ method: data.method, params: data.params });
    }
  });
}

const { chromium } = await import(pathToFileURL(playwrightModulePath).href);
const scenarios = [
  { id: "accept", decision: "accept" },
  { id: "deny", decision: "decline" },
  { id: "timeout", decision: "timeout" },
  { id: "file-change-accept", decision: "accept" },
  { id: "file-change-deny", decision: "decline" },
];
const scenarioResults = [];
let host = null;
let browser = null;
let page = null;
const pageIssues = [];
let activeChildPids = new Set();
let runError = null;
const baselineServerPids = pinnedServerPids();
const resultPath = path.join(runRoot, "result.json");

async function launchHost(applicationRoot) {
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
  await waitForCdp(debugPort, host);
  browser = await chromium.connectOverCDP(`http://127.0.0.1:${debugPort}`);
  page = await waitForNativePage(browser, debugPort, host);
  page.on("pageerror", (error) => pageIssues.push(error.message));
  page.on("console", (message) => { if (message.type() === "error") pageIssues.push(message.text()); });
  await page.addInitScript(installBridgeCapture);
  await page.evaluate(installBridgeCapture);
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  await page.getByRole("textbox", { name: "Message NeoBabylon" }).waitFor();
  await page.waitForFunction(() => {
    const composer = document.querySelector('textarea[aria-label="Message NeoBabylon"]');
    return composer && !composer.disabled && !document.querySelector(".chat-list-error");
  }, null, { timeout: 45000 });
  return debugPort;
}

async function closeHost() {
  if (browser) await browser.close().catch(() => {});
  browser = null;
  if (host && host.exitCode === null) {
    requestHostClose(host.pid);
    const result = await waitForExit(host, 20000);
    assert.equal(result.code, 0, `ApprovalQA WPF host exited unexpectedly: ${JSON.stringify(result)}`);
  }
  host = null;
  page = null;
}

function inspectFailure() {
  if (!page) return null;
  return page.evaluate(() => ({
    title: document.title,
    turnState: document.querySelector("section.conversation")?.getAttribute("data-turn-state"),
    alerts: [...document.querySelectorAll('[role="alert"], [role="status"]')].map((element) => element.textContent?.trim()),
    bridgeResponses: window.__p2_08BridgeResponses ?? [],
    appServerEvents: window.__p2_08AppServerEvents ?? [],
    body: document.body.innerText.slice(0, 3000),
  })).catch((error) => ({ error: String(error) }));
}

async function runScenario(scenario, index) {
  const scenarioRoot = path.join(runRoot, scenario.id);
  const { applicationRoot } = newQaRun(qaParent, `${scenario.id}-${Date.now()}-${process.pid}`);
  const dataRoot = path.join(applicationRoot, "Data");
  const workspace = path.join(dataRoot, "Workspace");
  mkdirSync(workspace, { recursive: true });
  const trackedFile = path.join(workspace, "tracked.txt");
  if (scenario.id.startsWith("file-change-")) writeFileSync(trackedFile, "before\n", { encoding: "utf8", flag: "wx" });
  const scenarioPrompt = scenario.id.startsWith("file-change-") ? patchPrompt : commandPrompt;
  providerRequests = [];
  const active = {
    ...scenario,
    applicationRoot,
    firstRequest: deferred(),
    secondRequest: deferred(),
  };
  activeScenario = active;
  const hostBefore = pinnedServerPids();
  const screenshotPending = path.join(runRoot, `${scenario.id}-approval-pending.png`);
  const screenshotDone = path.join(runRoot, `${scenario.id}-result.png`);
  await launchHost(applicationRoot);
  await page.getByRole("button", { name: "See runtime details" }).click();
  const diagnostics = page.getByRole("dialog", { name: "Runtime diagnostics" });
  await diagnostics.waitFor();
  const diagnosticsText = await diagnostics.innerText();
  assert.ok(diagnosticsText.includes(applicationRoot), "runtime diagnostics omitted the isolated scenario application root");
  assert.ok(diagnosticsText.includes(dataRoot), "runtime diagnostics omitted the isolated scenario Data root");
  assert.match(diagnosticsText, /Ordinary Codex root used\s+false/i, "ordinary Codex root use was not explicitly false");
  await diagnostics.getByRole("button", { name: "Done" }).click();

  await page.getByRole("textbox", { name: "Message NeoBabylon" }).fill(scenarioPrompt);
  await page.getByRole("button", { name: "Send message" }).click();
  await page.getByRole("region", { name: "Conversation" }).locator("article.message-user .message-text")
    .getByText(scenarioPrompt, { exact: true }).waitFor({ timeout: 20000 });
  const firstPayload = await withTimeout(active.firstRequest.promise, 45000,
    `${scenario.id}: first request did not reach the deterministic Responses fixture`);
  assert.equal(firstPayload.model, modelIdentifier, "first Responses request changed the exact selected synthetic model");
  assert.ok(!firstPayload.allow_fallback, "Responses request unexpectedly enabled provider/model fallback");
  const isFileChange = scenario.id.startsWith("file-change-");
  const pendingCard = page.getByRole("region", { name: "Pending App Server approvals" }).locator("section.approval-card");
  await pendingCard.waitFor({ timeout: 30000 });
  const pendingText = await pendingCard.innerText();
  if (isFileChange) {
    assert.match(pendingText, /File changes approval/);
    assert.match(pendingText, /Exact proposed changes/);
    assert.match(pendingText, /tracked\.txt/);
    assert.match(pendingText, /-before/);
    assert.match(pendingText, /\+after <img src=x onerror=window\.__p2_09Injected=true>/);
    assert.equal(await pendingCard.locator(".approval-file-review").count(), 1,
      "native file-change approval card did not render the exact App Server preview");
    assert.equal(await pendingCard.locator(".approval-file-review img").count(), 0,
      "model-controlled file diff was interpreted as an HTML element instead of inert text");
    assert.equal(await page.evaluate(() => window.__p2_09Injected ?? false), false,
      "model-controlled file diff executed markup in the native renderer");
  } else {
    assert.match(pendingText, /Command approval/);
    assert.match(pendingText, /cmd\.exe.*\bver\b/i);
    assert.match(pendingText, /No persistent session policy is offered here/);
  }
  if (scenario.id === "timeout") {
    await page.waitForFunction(() => document.querySelectorAll(".approval-card").length === 0, null, { timeout: 15000 });
  } else {
    await pendingCard.getByRole("button", { name: "Approve once" }).waitFor();
    await page.screenshot({ path: screenshotPending });
  }

  const appServerEventsBeforeChoice = await page.evaluate(() => window.__p2_08AppServerEvents ?? []);
  const started = appServerEventsBeforeChoice.find((event) => event.method === "turn/started"
    && event.params?.threadId && event.params?.turn?.id);
  assert.ok(started, "native UI did not receive an attributed App Server turn/started event");
  const approvalEvent = appServerEventsBeforeChoice.find((event) => event.method === "neobabylon/approvalRequested"
    && event.params?.params?.threadId === started.params.threadId
    && event.params?.params?.turnId === started.params.turn.id
    && event.params?.method === (isFileChange ? "item/fileChange/requestApproval" : "item/commandExecution/requestApproval"));
  assert.ok(approvalEvent, "approval was not tied to the active exact thread and turn");
  const approval = approvalEvent.params;
  assert.ok(Number.isSafeInteger(approval.requestId), "approval request ID is not numeric");
  assert.ok(typeof approval.params?.itemId === "string" && approval.params.itemId.length > 0,
    "native App Server approval omitted its item identity");
  assert.ok(Number.isSafeInteger(approval.params?.startedAtMs), "native approval omitted its start timestamp");
  assert.ok(typeof approval.approvalInstanceId === "string" && approval.approvalInstanceId.length >= 32,
    "native approval omitted the host-issued exact-instance binding");
  if (isFileChange) {
    assert.ok(approval.reviewPreview, "file-change approval event omitted its pre-execution preview");
    assert.equal(approval.reviewPreview.threadId, started.params.threadId);
    assert.equal(approval.reviewPreview.turnId, started.params.turn.id);
    assert.equal(approval.reviewPreview.itemId, approval.params.itemId);
    assert.match(approval.reviewPreview.fingerprint, /^[a-f0-9]{64}$/i);
    assert.ok(approval.reviewPreview.changes.some((change) => change.path.includes("tracked.txt")
      && change.diff.includes("-before") && change.diff.includes("+after <img src=x onerror=window.__p2_09Injected=true>")),
    "host-held preview was not the exact proposed tracked.txt change");
  } else {
    assert.ok(approval.params?.availableDecisions?.includes("accept"),
      "pinned App Server did not advertise the one-shot accept decision");
    assert.match(approval.params?.command ?? "", /cmd\.exe.*\bver\b/i,
      "approval did not show the exact harmless command after Codex shell normalization");
    assert.equal(approval.params?.cwd, workspace, "approval did not show the isolated test workspace");
  }

  if (scenario.decision === "accept") {
    await pendingCard.getByRole("button", { name: "Approve once" }).click();
  } else if (scenario.decision === "decline") {
    const denyButton = pendingCard.getByRole("button", { name: "Deny", exact: true });
    await denyButton.focus();
    assert.equal(await denyButton.evaluate((element) => document.activeElement === element), true,
      `${scenario.id}: native denial should be keyboard-focusable before Enter activation`);
    await page.keyboard.press("Enter");
  }

  if (scenario.decision === "timeout") {
    await page.locator(".activity-stack").getByText("Approval timed out · denied", { exact: true }).waitFor({ timeout: 15000 });
  } else {
    await page.locator(".activity-stack")
      .getByText(scenario.decision === "accept" ? "Approval granted" : "Approval denied", { exact: true })
      .waitFor({ timeout: 10000 });
  }

  const secondPayload = await withTimeout(active.secondRequest.promise, 30000,
    `${scenario.id}: App Server did not return the tool result through a second Responses request`);
  assert.equal(providerRequests.length, 2, `${scenario.id}: expected exactly one tool call and one final Responses request`);
  const secondPayloadText = JSON.stringify(secondPayload.input);
  assert.match(secondPayloadText, isFileChange ? /custom_tool_call_output/ : /function_call_output/,
    `${scenario.id}: tool output was not returned to the provider`);
  const expectedCallId = `${scenario.id}-${isFileChange ? "patch" : "exec"}-call`;
  assert.ok(secondPayloadText.includes(expectedCallId), `${scenario.id}: tool output lost the exact call id`);
  const activity = await page.locator(".activity-stack").innerText();
  if (scenario.id === "accept") {
    assert.match(secondPayloadText, /Microsoft Windows/i, "accepted harmless command did not return Windows version output");
    assert.match(secondPayloadText, /Process exited with code 0/i, "accepted version command did not exit successfully");
  } else if (isFileChange && scenario.decision === "accept") {
    assert.match(secondPayloadText, /applied patch|success/i, "accepted exact file-change tool result was not returned to the provider");
    assert.equal(readFileSync(trackedFile, "utf8"), "after <img src=x onerror=window.__p2_09Injected=true>\n",
      "approved patch did not apply the exact fixture change");
    assert.equal(await page.evaluate(() => window.__p2_09Injected ?? false), false,
      "approved model-controlled patch text executed markup in the native renderer");
  } else if (isFileChange) {
    assert.match(secondPayloadText, /denied|declined|rejected/i, "denied file-change result was not returned to the provider");
    assert.equal(readFileSync(trackedFile, "utf8"), "before\n", "denied patch changed the isolated fixture file");
  } else {
    assert.match(secondPayloadText, /denied|declined|rejected/i, `${scenario.id}: denial was not represented in the tool result`);
  }
  await page.getByRole("region", { name: "Conversation" }).locator("article.message-assistant .message-text")
    .getByText(assistantReply, { exact: true }).waitFor({ timeout: 30000 });
  const expectedUiTurnState = scenario.decision === "accept" ? "completed" : "failed";
  await page.waitForFunction((expected) =>
    document.querySelector("section.conversation")?.getAttribute("data-turn-state") === expected,
  expectedUiTurnState, { timeout: 30000 });
  await page.screenshot({ path: screenshotDone });

  const appServerEvents = await page.evaluate(() => window.__p2_08AppServerEvents ?? []);
  const resolved = appServerEvents.find((event) => event.method === "serverRequest/resolved"
    && event.params?.requestId === approval.requestId);
  assert.ok(resolved, `${scenario.id}: serverRequest/resolved did not retire the exact pending request`);
  const timeoutEvent = appServerEvents.find((event) => event.method === "neobabylon/approvalTimedOut"
    && event.params?.requestId === approval.requestId);
  assert.equal(Boolean(timeoutEvent), scenario.id === "timeout", `${scenario.id}: timeout lifecycle event mismatch`);
  if (timeoutEvent) {
    assert.equal(timeoutEvent.params?.response?.result?.decision, "decline", "timeout did not send schema-valid fail-closed denial");
  }
  const terminal = appServerEvents.find((event) => event.method === "turn/completed"
    && event.params?.threadId === started.params.threadId
    && event.params?.turn?.id === started.params.turn.id);
  assert.ok(terminal, `${scenario.id}: native UI did not receive terminal status for the exact turn`);
  assert.equal(terminal.params.turn.status, "completed", `${scenario.id}: tool-result turn did not complete`);
  assert.deepEqual(fixtureErrors, [], fixtureErrors.join("; "));
  assert.deepEqual(pageIssues, [], "the native renderer reported a console/page error");

  const childPids = [...pinnedServerPids()].filter((pid) => !hostBefore.has(pid));
  assert.ok(childPids.length > 0, "native QA host did not start a process at the exact locked App Server path");
  activeChildPids = new Set([...activeChildPids, ...childPids]);
  const result = {
    id: scenario.id,
    decision: scenario.decision,
    runtime: { version: runtime.version, sourceRevision: runtime.sourceRevision, path: runtimeBinaryPath, sha256: runtimeSha256 },
    capability: { providerId: capability.providerId, modelIdentifier, endpoint: capability.endpoint,
      evidence: "synthetic loopback Responses fixture; no provider/model was loaded and no live inference occurred" },
    applicationRoot,
    dataRoot,
    workspace,
    ordinaryCodexRootUsed: false,
    threadId: started.params.threadId,
    turnId: started.params.turn.id,
    approvalRequestId: approval.requestId,
    approvalItemId: approval.params.itemId,
    approvalStartedAtMs: approval.params.startedAtMs,
    approvalInstanceId: approval.approvalInstanceId,
    reviewPreviewFingerprint: approval.reviewPreview?.fingerprint ?? null,
    renderedExactFilePreview: isFileChange,
    advertisedDecisions: approval.params.availableDecisions,
    command,
    providerRequestCount: providerRequests.length,
    returnedToolOutput: isFileChange
      ? scenario.decision === "accept" ? "exact file patch applied; rendered markup remained inert" : "file patch denied; fixture file unchanged"
      : secondPayloadText.match(/Microsoft Windows/i) ? "Windows version; exit code 0" : "schema-denied command result",
    timeoutFailClosed: Boolean(timeoutEvent),
    resolvedRequestObserved: true,
    keyboardActions: scenario.decision === "decline" ? ["focused Deny", "Enter denied the exact pending approval"] : [],
    pinnedServerPids: childPids,
    screenshots: { pending: scenario.decision === "timeout" ? null : screenshotPending, result: screenshotDone },
    activity,
  };
  scenarioResults.push(result);
  await closeHost();
  activeScenario = null;
  return result;
}

let capabilityPort = null;
try {
  capabilityPort = await listen(fixtureServer);
  capability.endpoint = `http://127.0.0.1:${capabilityPort}/v1`;
  const capabilityBytes = `${JSON.stringify(capability, null, 2)}\n`;
  writeFileSync(capabilityPath, capabilityBytes, { encoding: "utf8", flag: "wx" });
  capabilityWritten = true;
  capabilityFileHash = createHash("sha256").update(capabilityBytes).digest("hex");

  for (let index = 0; index < scenarios.length; index++) {
    await runScenario(scenarios[index], index);
  }

  const afterClose = pinnedServerPids();
  const stillRunning = [...afterClose].filter((pid) => activeChildPids.has(pid));
  assert.deepEqual(stillRunning, [], "one or more QA-owned pinned App Server processes remained after host shutdown");
  writeFileSync(resultPath, `${JSON.stringify({
    passed: true,
    slice: "P2-08 and P2-09",
    evidenceKind: "deterministic loopback Responses fixture; no live inference",
    runtime: { version: runtime.version, sourceRevision: runtime.sourceRevision, path: runtimeBinaryPath, sha256: runtimeSha256 },
    endpoint: capability.endpoint,
    modelIdentifier,
    providerRequestsPerScenario: 2,
    applicationRootIsolatedPerScenario: true,
    scenarios: scenarioResults,
    appServerExitedAfterEachHost: true,
    fixtureErrors,
    pageIssues,
    capabilityRecordPath: capabilityPath,
    capabilityRecordSha256: capabilityFileHash,
  }, null, 2)}\n`, "utf8");
  const totalProviderRequests = scenarioResults.reduce((total, scenario) => total + scenario.providerRequestCount, 0);
  console.log(`P2_08_09_NATIVE result=pass runtime=${runtime.version} sha256=${runtimeSha256} model=${modelIdentifier} scenarios=${scenarioResults.map((scenario) => scenario.id).join(",")} providerRequests=${totalProviderRequests} noLiveInference=true appServerExited=true root=${runRoot}`);
} catch (error) {
  runError = error;
  const diagnostics = await inspectFailure();
  writeFileSync(resultPath, `${JSON.stringify({
    passed: false,
    slice: "P2-08 and P2-09",
    error: error instanceof Error ? `${error.name}: ${error.message}` : String(error),
    runtime: { version: runtime.version, sourceRevision: runtime.sourceRevision, sha256: runtimeSha256 },
    endpoint: capability.endpoint,
    modelIdentifier,
    scenarioResults,
    activeScenario: activeScenario?.id ?? null,
    providerRequests,
    fixtureErrors,
    diagnostics,
    applicationRoot: activeScenario ? activeScenario.applicationRoot ?? null : null,
    runRoot,
  }, null, 2)}\n`, "utf8");
  console.error(`P2_08_NATIVE_DIAGNOSTIC ${JSON.stringify({ error: error instanceof Error ? error.message : String(error), activeScenario: activeScenario?.id ?? null, providerRequestCount: providerRequests.length, fixtureErrors, diagnostics, runRoot })}`);
  throw error;
} finally {
  await closeHost().catch((closeError) => {
    if (!runError) throw closeError;
    console.error(`P2_08_NATIVE_HOST_CLEANUP ${closeError.message}`);
  });
  if (fixtureServer.listening) {
    fixtureServer.close();
    fixtureServer.closeAllConnections();
  }
  if (capabilityWritten && existsSync(capabilityPath)) {
    const currentHash = createHash("sha256").update(readFileSync(capabilityPath)).digest("hex");
    if (currentHash === capabilityFileHash) unlinkSync(capabilityPath);
    else console.error(`P2_08_CAPABILITY_RETAINED path=${capabilityPath} reason=content-changed-after-test-write`);
  }
}
