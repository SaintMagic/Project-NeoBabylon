import assert from "node:assert/strict";
import { spawn, spawnSync } from "node:child_process";
import { createServer as createHttpServer } from "node:http";
import { createServer as createTcpServer } from "node:net";
import { createHash, randomUUID } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, readdirSync, unlinkSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { assertQaRunsParent, newQaRun } from "./qa-run-root.mjs";

const [hostPathArg, playwrightModulePathArg, qaParentArg] = process.argv.slice(2);
assert.ok(hostPathArg && playwrightModulePathArg && qaParentArg,
  "usage: node native-cross-project-recovery.mjs <NeoBabylon.Host.exe> <playwright-core-entry.mjs> <exact .local\\Lab\\Runs parent>");

const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const hostPath = path.resolve(hostPathArg);
const playwrightModulePath = path.resolve(playwrightModulePathArg);
const qaParent = path.resolve(qaParentArg);
const releaseRoot = path.join(sourceRoot, "docs", "release");
assertQaRunsParent(sourceRoot, qaParent);
const within = (candidate, parent) => {
  const relative = path.relative(path.resolve(parent), path.resolve(candidate));
  return relative !== "" && relative !== ".." && !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative);
};

for (const [label, filePath] of [["WPF host", hostPath], ["Playwright module", playwrightModulePath]]) {
  assert.ok(existsSync(filePath), `${label} does not exist: ${filePath}`);
}

const runtimeLock = JSON.parse(readFileSync(path.join(sourceRoot, "runtime", "runtime-lock.json"), "utf8"));
const runtimePath = path.resolve(sourceRoot, runtimeLock.runtime.appServerBinaryRelativePath);
assert.ok(existsSync(runtimePath), `pinned App Server binary is missing: ${runtimePath}`);
const runtimeHash = createHash("sha256").update(readFileSync(runtimePath)).digest("hex");
assert.equal(runtimeHash, runtimeLock.runtime.sha256.toLowerCase(), "pinned App Server binary differs from runtime-lock.json");

const { runRoot, applicationRoot } = newQaRun(qaParent, `P2-P05-native-${Date.now()}-${process.pid}`);
const alphaWorkspace = path.join(runRoot, "Alpha");
const betaWorkspace = path.join(runRoot, "Beta");
const dataRoot = path.join(applicationRoot, "Data");
for (const directory of [alphaWorkspace, betaWorkspace, dataRoot]) mkdirSync(directory, { recursive: true });
assert.ok(!within(sourceRoot, applicationRoot), "source repository cannot be inside the QA application root");
assert.equal(path.resolve(dataRoot), path.resolve(applicationRoot, "Data"));

const modelIdentifier = "p2-05-cross-project-loopback-model";
const alphaPrompt = "P2-05 Alpha task remains attached to the Alpha project.";
const betaPendingPrompt = "P2-05 Beta turn is accepted once before WPF host restart.";
const betaContinuePrompt = "P2-05 deliberately continue the exact Beta task after restart.";
const alphaContinuePrompt = "P2-05 deliberately continue the exact Alpha task after returning.";
const plannedPrompts = [alphaPrompt, betaPendingPrompt, betaContinuePrompt, alphaContinuePrompt];
const capabilityPath = path.join(releaseRoot, `MODEL_CAPABILITY_P2_05_${Date.now()}_${process.pid}.json`);
assert.ok(!existsSync(capabilityPath), "refusing to overwrite an existing P2-05 synthetic capability record");
const projectRegistryPath = path.join(dataRoot, "NeoBabylon", "projects.json");
const screenshots = {
  betaPendingBeforeRestart: path.join(runRoot, "beta-pending-before-restart.png"),
  betaAfterRestart: path.join(runRoot, "beta-after-restart.png"),
  alphaAfterReturn: path.join(runRoot, "alpha-after-return.png"),
};

mkdirSync(path.dirname(projectRegistryPath), { recursive: true });
writeFileSync(projectRegistryPath, `${JSON.stringify({
  schemaVersion: 1,
  product: "NeoBabylon",
  selectedWorkspace: alphaWorkspace,
  projects: [
    { workspacePath: alphaWorkspace, name: "Alpha" },
    { workspacePath: betaWorkspace, name: "Beta" },
  ],
}, null, 2)}\n`, { encoding: "utf8", flag: "wx" });

const unknown = (evidenceSource) => ({ state: "Unknown", value: null, evidenceSource });
const capability = {
  providerId: "lmstudio",
  providerDisplayName: "LM Studio · P2-05 loopback fixture",
  providerServerVersion: "Unknown",
  endpoint: "pending-loopback-endpoint",
  modelIdentifier,
  modelVariant: "test-only local Responses fixture; no model weights",
  architecture: "Unknown",
  parameterCount: "Unknown",
  modelSizeBytes: "Unknown",
  quantization: unknown("P2-05 fixture; no model weights or quantization metadata"),
  contextWindowAdvertised: unknown("P2-05 fixture; provider metadata not queried"),
  contextWindowEffective: {
    state: "Known",
    value: 32768,
    evidenceSource: "P2-05 synthetic test-catalog declaration only; not observed model metadata",
  },
  reasoningControls: unknown("P2-05 fixture; no model loaded"),
  toolFunctionCalling: {
    state: "Known",
    value: "tool_use",
    evidenceSource: "P2-05 synthetic test-catalog prerequisite only; no real model capability claim",
  },
  applyPatchToolType: unknown("P2-05 fixture; no patch format qualified"),
  structuredOutput: unknown("P2-05 fixture; provider metadata not queried"),
  agentMetadata: {
    state: "Known",
    value: "type=llm; inputModalities=text; vision=False",
    evidenceSource: "P2-05 synthetic test-catalog declaration only; no model weights or live model metadata",
  },
};

const providerRequests = [];
const fixtureErrors = [];
const openResponses = new Set();
function assistantResponse(responseId, itemId, text) {
  const events = [
    { type: "response.created", response: { id: responseId } },
    { type: "response.output_item.done", item: { type: "message", role: "assistant", id: itemId,
      content: [{ type: "output_text", text }] } },
    { type: "response.completed", response: { id: responseId,
      usage: { input_tokens: 0, input_tokens_details: null, output_tokens: 0,
        output_tokens_details: null, total_tokens: 0 } } },
  ];
  return events.map((event) => `event: ${event.type}\ndata: ${JSON.stringify(event)}\n\n`).join("");
}

const fixtureServer = createHttpServer((request, response) => {
  if (request.method !== "POST" || request.url !== "/v1/responses") {
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
      fixtureErrors.push("Responses request body was not JSON");
      response.writeHead(400).end();
      return;
    }
    const number = providerRequests.length + 1;
    const expectedPrompt = plannedPrompts[number - 1];
    const serializedInput = JSON.stringify(payload.input ?? []);
    const promptMatched = Boolean(expectedPrompt && serializedInput.includes(expectedPrompt));
    providerRequests.push({ number, model: payload.model ?? null, expectedPrompt: expectedPrompt ?? null, promptMatched });
    if (payload.model !== modelIdentifier) {
      fixtureErrors.push(`request #${number} used unexpected model ${payload.model ?? "<missing>"}`);
      response.writeHead(409).end();
      return;
    }
    if (!promptMatched) {
      fixtureErrors.push(`request #${number} did not include its planned user-authored prompt`);
      response.writeHead(409).end();
      return;
    }
    response.writeHead(200, { "content-type": "text/event-stream", "cache-control": "no-cache" });
    response.flushHeaders();
    if (expectedPrompt === betaPendingPrompt) {
      response.write(": P2-05 holds the accepted Beta turn across the host restart boundary\n\n");
      openResponses.add(response);
      response.once("close", () => openResponses.delete(response));
      return;
    }
    response.end(assistantResponse(`p2-05-response-${number}`, `p2-05-item-${number}`,
      `Deterministic fixture reply for request ${number}.`));
  });
});

function listen(server) {
  return new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", () => {
      const address = server.address();
      if (!address || typeof address === "string") reject(new Error("fixture did not bind a loopback TCP port"));
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

function waitForExit(child, timeoutMs) {
  if (child.exitCode !== null) return Promise.resolve({ code: child.exitCode, signal: child.signalCode });
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`QA host PID ${child.pid} did not exit within ${timeoutMs}ms`)), timeoutMs);
    child.once("exit", (code, signal) => { clearTimeout(timer); resolve({ code, signal }); });
  });
}

function requestHostClose(pid) {
  const command = `$process = Get-Process -Id ${pid} -ErrorAction Stop; if (-not $process.CloseMainWindow()) { exit 2 }; exit 0`;
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", command], {
    encoding: "utf8", timeout: 10000,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`could not request graceful close for QA host PID ${pid}: ${result.stderr || result.stdout}`);
}

function findFiles(directory, suffix) {
  if (!existsSync(directory)) return [];
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const child = path.join(directory, entry.name);
    return entry.isDirectory() ? findFiles(child, suffix) : entry.name.endsWith(suffix) ? [child] : [];
  });
}

function journalUserPrompts() {
  const sessionRoot = path.join(dataRoot, "CodexHome", "sessions");
  const prompts = [];
  for (const filePath of findFiles(sessionRoot, ".jsonl")) {
    for (const line of readFileSync(filePath, "utf8").split(/\r?\n/)) {
      if (!line) continue;
      let record;
      try { record = JSON.parse(line); } catch { continue; }
      if (record.type !== "event_msg" || record.payload?.type !== "item_completed"
        || record.payload?.item?.type !== "UserMessage") continue;
      const text = (record.payload.item.content ?? []).filter((part) => part.type === "text")
        .map((part) => part.text ?? "").join("");
      if (text) prompts.push(text);
    }
  }
  return prompts;
}

async function waitFor(predicate, message, timeoutMs = 30000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (predicate()) return;
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  throw new Error(message);
}

const { chromium } = await import(pathToFileURL(playwrightModulePath).href);
let browser;
let host;
let page;
const pageIssues = [];

async function waitForCdp(port, child) {
  const deadline = Date.now() + 45000;
  while (Date.now() < deadline) {
    if (child.exitCode !== null) throw new Error(`WPF host exited before CDP opened (code ${child.exitCode})`);
    try {
      const result = await fetch(`http://127.0.0.1:${port}/json/version`, { signal: AbortSignal.timeout(1000) });
      if (result.ok) return await result.json();
    } catch { /* WebView2 startup is still in progress. */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`WebView2 CDP did not open on port ${port}`);
}

async function waitForNativePage(port, child) {
  const deadline = Date.now() + 30000;
  let targets = [];
  while (Date.now() < deadline) {
    if (child.exitCode !== null) throw new Error(`WPF host exited before its page appeared (code ${child.exitCode})`);
    const found = browser.contexts().flatMap((context) => context.pages())
      .find((candidate) => candidate.url().startsWith("https://neobabylon.local/"));
    if (found) return found;
    try {
      const result = await fetch(`http://127.0.0.1:${port}/json/list`, { signal: AbortSignal.timeout(1000) });
      if (result.ok) targets = await result.json();
    } catch { /* The native WebView target is not ready yet. */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`native page was not found; CDP targets=${JSON.stringify(targets)}`);
}

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
  await waitForCdp(debugPort, host);
  browser = await chromium.connectOverCDP(`http://127.0.0.1:${debugPort}`);
  page = await waitForNativePage(debugPort, host);
  page.on("pageerror", (error) => pageIssues.push(error.message));
  page.on("console", (message) => { if (message.type() === "error") pageIssues.push(message.text()); });
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  assert.equal(await page.title(), "NeoBabylon");
  assert.equal(await page.locator("vite-error-overlay").count(), 0);
  await page.getByRole("textbox", { name: "Message NeoBabylon" }).waitFor();
  return debugPort;
}

async function closeHost() {
  if (browser) await browser.close().catch(() => {});
  browser = null;
  if (host && host.exitCode === null) {
    requestHostClose(host.pid);
    const result = await waitForExit(host, 20000);
    assert.equal(result.code, 0, `WPF host exited unexpectedly: ${JSON.stringify(result)}`);
  }
  host = null;
  page = null;
}

async function sendPrompt(prompt, expectedReply) {
  const conversation = page.getByRole("region", { name: "Conversation" });
  await page.getByRole("textbox", { name: "Message NeoBabylon" }).fill(prompt);
  await page.getByRole("button", { name: "Send message" }).click();
  await conversation.locator("article.message-user .message-text").getByText(prompt, { exact: true })
    .waitFor({ timeout: 20000 });
  if (expectedReply) {
    await conversation.locator("article.message-assistant .message-text")
      .getByText(expectedReply, { exact: true }).waitFor({ timeout: 30000 });
    await page.waitForFunction(() => {
      const state = document.querySelector("section.conversation")?.getAttribute("data-turn-state");
      return state !== "starting" && state !== "running";
    }, null, { timeout: 30000 });
  }
}

async function runtimeEvidence() {
  await page.getByRole("button", { name: "Open diagnostics" }).click();
  const dialog = page.getByRole("dialog", { name: "Runtime diagnostics" });
  await dialog.waitFor();
  const text = await dialog.innerText();
  assert.ok(text.includes(modelIdentifier), "runtime diagnostics did not expose the selected synthetic model identity");
  assert.ok(text.includes(applicationRoot), "runtime diagnostics did not expose the isolated application root");
  assert.match(text, /Ordinary Codex root used\s+false/i, "diagnostics did not confirm ordinary Codex root isolation");
  assert.ok(text.includes(String(runtimeLock.runtime.version)), "diagnostics did not expose the pinned App Server version");
  await dialog.getByRole("button", { name: "Done" }).click();
  return text;
}

async function captureHostResponse(operation, payload) {
  const requestId = `p2-05-${randomUUID()}`;
  return page.evaluate(({ requestId, operation, payload }) => new Promise((resolve, reject) => {
    const bridge = window.chrome?.webview;
    if (!bridge) return reject(new Error("native bridge is unavailable"));
    const timer = window.setTimeout(() => reject(new Error(`host response timed out for ${operation}`)), 15000);
    const listener = (event) => {
      if (event.data?.requestId !== requestId || event.data?.stream) return;
      window.clearTimeout(timer);
      bridge.removeEventListener("message", listener);
      resolve(event.data);
    };
    bridge.addEventListener("message", listener);
    bridge.postMessage({ requestId, operation, ...payload });
  }), { requestId, operation, payload });
}

async function messageCount(prompt) {
  return page.locator(".transcript article.message-user .message-text").getByText(prompt, { exact: true }).count();
}

const fixturePort = await listen(fixtureServer);
capability.endpoint = `http://127.0.0.1:${fixturePort}/v1`;
let capabilityBytes;

try {
  capabilityBytes = `${JSON.stringify(capability, null, 2)}\n`;
  writeFileSync(capabilityPath, capabilityBytes, { encoding: "utf8", flag: "wx" });
  await launchHost();
  const firstRuntimeEvidence = await runtimeEvidence();
  const firstRuntimeStatus = await captureHostResponse("getRuntimeStatus", {});
  assert.equal(firstRuntimeStatus.ok, true, "the trusted host did not return its initial runtime status");
  assert.equal(firstRuntimeStatus.result?.selectedWorkspace, alphaWorkspace,
    "the initial runtime status did not identify the exact Alpha workspace");
  await sendPrompt(alphaPrompt, "Deterministic fixture reply for request 1.");
  assert.equal(providerRequests.length, 1, "the Alpha authored turn must cause exactly one fixture request");
  const alphaPointer = await page.evaluate(() => JSON.parse(localStorage.getItem("neobabylon.active-task:v1")));
  assert.ok(alphaPointer?.threadId && alphaPointer.workspace === alphaWorkspace,
    "Alpha's active-task pointer did not preserve its exact workspace/thread identity");

  await page.evaluate(() => {
    const bridge = window.chrome?.webview;
    if (!bridge) throw new Error("native WebView2 bridge is unavailable");
    const originalPostMessage = bridge.postMessage.bind(bridge);
    window.__neoBabylonP205ListLatch = { delayed: false, released: false };
    bridge.postMessage = (request) => {
      const latch = window.__neoBabylonP205ListLatch;
      if (request?.operation === "listThreads" && latch && !latch.delayed) {
        latch.delayed = true;
        window.setTimeout(() => { originalPostMessage(request); latch.released = true; }, 1800);
        return;
      }
      originalPostMessage(request);
    };
  });
  await page.locator("button.project-row").filter({ hasText: "Beta" }).click();
  await page.waitForFunction(() => window.__neoBabylonP205ListLatch?.delayed === true, null, { timeout: 15000 });
  assert.equal(await page.locator("button.project-row[aria-current='page']").innerText(), "Beta",
    "project selection was not applied before the Beta history refresh");
  assert.equal(await page.getByRole("textbox", { name: "Message NeoBabylon" }).isDisabled(), true,
    "the composer must remain unavailable while project history refresh owns the UI operation");
  assert.equal(await page.locator("button.project-row").filter({ hasText: "Alpha" }).isDisabled(), true,
    "a competing project switch must remain unavailable while the history refresh is active");
  assert.equal(providerRequests.length, 1, "project selection/history refresh must not start a provider request");
  await page.screenshot({ path: path.join(runRoot, "beta-history-refresh-busy.png") });
  await page.waitForFunction(() => window.__neoBabylonP205ListLatch?.released === true
    && document.querySelector('textarea[aria-label="Message NeoBabylon"]')?.disabled === false,
  null, { timeout: 20000 });
  const betaHistoryDuringSwitch = await page.locator("button.saved-chat").allInnerTexts();
  assert.ok(betaHistoryDuringSwitch.every((row) => !row.includes(alphaPrompt)),
    "the Beta project history exposed Alpha's saved task after switching");

  await sendPrompt(betaPendingPrompt, null);
  await waitFor(() => providerRequests.length === 2, "the accepted Beta turn did not reach the loopback fixture");
  await page.waitForFunction((prompt) => {
    const composer = document.querySelector('textarea[aria-label="Message NeoBabylon"]');
    const pendingKey = Object.keys(localStorage).find((key) => key.startsWith("neobabylon.pending-submission:v1:"));
    const marker = pendingKey ? JSON.parse(localStorage.getItem(pendingKey)) : null;
    return composer?.value === "" && (!marker || localStorage.getItem(marker.draftScope) === "");
  }, betaPendingPrompt, { timeout: 30000 });
  await waitFor(() => journalUserPrompts().filter((prompt) => prompt === betaPendingPrompt).length === 1,
    "the accepted Beta prompt was not durably journaled exactly once before restart");
  assert.equal(await messageCount(betaPendingPrompt), 1, "the accepted Beta prompt must appear once before restart");
  assert.equal(providerRequests.length, 2, "the held Beta turn must have exactly one request before restart");
  const betaPointer = await page.evaluate(() => JSON.parse(localStorage.getItem("neobabylon.active-task:v1")));
  assert.ok(betaPointer?.threadId && betaPointer.workspace === betaWorkspace,
    "Beta's active-task pointer did not preserve its exact workspace/thread identity");
  await page.screenshot({ path: screenshots.betaPendingBeforeRestart });
  await closeHost();

  await launchHost();
  await page.getByRole("region", { name: "Conversation" }).getByText(betaPendingPrompt, { exact: true })
    .waitFor({ timeout: 45000 });
  const selectedProject = page.locator("button.project-row[aria-current='page']");
  assert.equal((await selectedProject.innerText()).trim(), "Beta", "host restart did not restore the selected Beta project");
  assert.equal(await selectedProject.getAttribute("title"), betaWorkspace,
    "the selected project row did not expose the exact Beta workspace path after restart");
  assert.deepEqual(await page.evaluate(() => JSON.parse(localStorage.getItem("neobabylon.active-task:v1"))), betaPointer,
    "host restart changed or lost the exact active Beta task pointer");
  assert.equal(await messageCount(betaPendingPrompt), 1, "the held Beta turn must not duplicate in the restored transcript");
  assert.equal(journalUserPrompts().filter((prompt) => prompt === betaPendingPrompt).length, 1,
    "the accepted Beta prompt must remain journaled once after restart");
  assert.equal(providerRequests.length, 2, "host restart must not replay the uncertain accepted Beta turn");
  const restartAlerts = await page.locator('[role="alert"]').allInnerTexts();
  assert.ok(restartAlerts.some((text) => /not replay/i.test(text)),
    `the UI must explain that the uncertain turn was not replayed; alerts=${JSON.stringify(restartAlerts)}`);
  await page.screenshot({ path: screenshots.betaAfterRestart });

  const crossProjectResume = await captureHostResponse("resumeThread", { threadId: alphaPointer.threadId });
  assert.equal(crossProjectResume.ok, false, "the native host bridge accepted Alpha's thread while Beta was selected");
  assert.match(crossProjectResume.error?.message ?? "", /workspace does not match/i,
    "cross-project resume should fail specifically on the selected-workspace identity");
  assert.deepEqual(await page.evaluate(() => JSON.parse(localStorage.getItem("neobabylon.active-task:v1"))), betaPointer,
    "a rejected cross-project resume must not replace the selected Beta task pointer");
  assert.equal(providerRequests.length, 2, "cross-project resume must not contact the provider");

  await sendPrompt(betaContinuePrompt, "Deterministic fixture reply for request 3.");
  assert.equal(providerRequests.length, 3, "the user-authored Beta continuation must issue exactly one new request");
  assert.equal(await messageCount(betaContinuePrompt), 1, "the deliberate Beta continuation must appear exactly once");

  await page.locator("button.project-row").filter({ hasText: "Alpha" }).click();
  await page.waitForFunction(() => document.querySelector("button.project-row[aria-current='page']")
    ?.textContent?.includes("Alpha") && document.querySelector('textarea[aria-label="Message NeoBabylon"]')?.disabled === false,
  null, { timeout: 30000 });
  const alphaRows = await page.locator("button.saved-chat").allInnerTexts();
  assert.ok(alphaRows.some((row) => row.includes(alphaPrompt)), "returning to Alpha did not restore Alpha's distinct history");
  assert.ok(alphaRows.every((row) => !row.includes(betaPendingPrompt) && !row.includes(betaContinuePrompt)),
    "returning to Alpha exposed Beta's saved task history");
  await page.locator("button.saved-chat").filter({ hasText: alphaPrompt }).click();
  const conversation = page.getByRole("region", { name: "Conversation" });
  await conversation.getByText(alphaPrompt, { exact: true }).waitFor({ timeout: 20000 });
  assert.deepEqual(await page.evaluate(() => JSON.parse(localStorage.getItem("neobabylon.active-task:v1"))), alphaPointer,
    "reopening Alpha did not restore its exact saved project/task pointer");
  assert.equal(providerRequests.length, 3, "reading or reopening Alpha must not replay an earlier provider request");
  await sendPrompt(alphaContinuePrompt, "Deterministic fixture reply for request 4.");
  assert.equal(providerRequests.length, 4, "the authored Alpha continuation must issue exactly one request");
  assert.equal(await messageCount(alphaContinuePrompt), 1, "the deliberate Alpha continuation must appear exactly once");
  await page.screenshot({ path: screenshots.alphaAfterReturn });

  const finalPrompts = journalUserPrompts();
  for (const prompt of plannedPrompts) {
    assert.equal(finalPrompts.filter((entry) => entry === prompt).length, 1,
      `the isolated App Server journal must contain authored prompt once: ${prompt}`);
  }
  assert.deepEqual(providerRequests.map((entry) => entry.expectedPrompt), plannedPrompts,
    "fixture request order must follow only the four explicit authored turns");
  assert.ok(providerRequests.every((entry) => entry.model === modelIdentifier && entry.promptMatched),
    "each provider request must retain the exact synthetic model and planned prompt");
  assert.deepEqual(fixtureErrors, [], "the loopback fixture received an unplanned provider request");
  assert.deepEqual(pageIssues, [], "the native project recovery path should not produce WebView errors");
  assert.equal(readFileSync(capabilityPath, "utf8"), capabilityBytes, "the synthetic capability record changed during QA");
  const finalProjectRegistry = JSON.parse(readFileSync(projectRegistryPath, "utf8"));
  assert.equal(finalProjectRegistry.selectedWorkspace, alphaWorkspace,
    "returning to Alpha did not persist the exact selected workspace in the registry");
  assert.deepEqual(finalProjectRegistry.projects.map((entry) => [entry.name, entry.workspacePath]),
    [["Alpha", alphaWorkspace], ["Beta", betaWorkspace]],
    "project switching changed, reordered, or lost the saved local project identities");

  const result = {
    passed: true,
    scenario: "P2-05 native WPF project interleaving and exact task recovery",
    source: "native WPF + WebView2 + runtime-lock-verified Codex App Server + loopback-only deterministic Responses fixture",
    runtime: { version: runtimeLock.runtime.version, sourceRevision: runtimeLock.runtime.sourceRevision,
      sha256: runtimeHash, binaryPath: runtimePath },
    syntheticProvider: { providerId: capability.providerId, modelIdentifier, endpoint: capability.endpoint,
      realModelWeights: false, liveInference: false },
    projects: { alphaWorkspace, betaWorkspace, selectedAfterRestart: betaWorkspace,
      alphaThreadId: alphaPointer.threadId, betaThreadId: betaPointer.threadId,
      crossProjectResumeRejected: true, selectedProjectHistoryIsolated: true },
    evidence: { busyDuringBetaHistoryRefresh: true, betaAcceptedPromptJournaledOnceBeforeRestart: true,
      betaRestartDidNotReplay: true, noReplayWarningVisible: true, rejectedCrossProjectResumeDidNotMutatePointer: true,
      deliberateContinuations: [betaContinuePrompt, alphaContinuePrompt], providerRequestCount: providerRequests.length,
      providerRequests, journalUserPrompts: finalPrompts },
    applicationRoot, dataRoot, sourceRoot, ordinaryCodexDataRootUsed: false,
    projectRegistryPath, capabilityPath, screenshots, pageIssues,
  };
  writeFileSync(path.join(runRoot, "result.json"), `${JSON.stringify(result, null, 2)}\n`, { encoding: "utf8", flag: "wx" });
  console.log(JSON.stringify(result));
} finally {
  if (browser) await browser.close().catch(() => {});
  if (host && host.exitCode === null) {
    try {
      requestHostClose(host.pid);
      await waitForExit(host, 20000);
    } catch (error) {
      host.kill();
      await waitForExit(host, 5000).catch(() => {});
      console.error(`QA-owned WPF process needed targeted cleanup after graceful-close attempt: ${error.message}`);
    }
  }
  for (const response of openResponses) response.destroy();
  await new Promise((resolve) => fixtureServer.close(() => resolve()));
  if (capabilityBytes && existsSync(capabilityPath)) {
    const currentHash = createHash("sha256").update(readFileSync(capabilityPath)).digest("hex");
    const createdHash = createHash("sha256").update(capabilityBytes).digest("hex");
    if (currentHash === createdHash
      && within(capabilityPath, releaseRoot)
      && path.basename(capabilityPath).startsWith("MODEL_CAPABILITY_P2_05_")) {
      unlinkSync(capabilityPath);
    } else {
      console.error(`Refused to remove a missing, changed, or non-test capability record: ${capabilityPath}`);
    }
  }
}
