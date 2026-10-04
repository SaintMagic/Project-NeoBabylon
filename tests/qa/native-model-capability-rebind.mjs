import assert from "node:assert/strict";
import { spawn, spawnSync } from "node:child_process";
import { createServer as createHttpServer } from "node:http";
import { createServer as createTcpServer } from "node:net";
import { createHash } from "node:crypto";
import {
  existsSync,
  mkdirSync,
  readFileSync,
  unlinkSync,
  writeFileSync,
} from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { assertQaRunsParent, newQaRun } from "./qa-run-root.mjs";

const [hostPathArg, playwrightModulePathArg, qaParentArg] = process.argv.slice(2);
assert.ok(hostPathArg && playwrightModulePathArg && qaParentArg,
  "usage: node native-model-capability-rebind.mjs <NeoBabylon.Host.exe> <playwright-core-entry.mjs> <exact .local\\Lab\\Runs parent>");

const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const hostPath = path.resolve(hostPathArg);
const playwrightModulePath = path.resolve(playwrightModulePathArg);
const qaParent = path.resolve(qaParentArg);
const releaseRoot = path.join(sourceRoot, "docs", "release");
assertQaRunsParent(sourceRoot, qaParent);
const within = (candidate, parent) => path.resolve(candidate).toLowerCase()
  .startsWith(`${path.resolve(parent).toLowerCase()}${path.sep.toLowerCase()}`);
for (const [label, filePath] of [["WPF host", hostPath], ["Playwright module", playwrightModulePath]]) {
  assert.ok(existsSync(filePath), `${label} does not exist: ${filePath}`);
}

const { runRoot, applicationRoot } = newQaRun(qaParent, `P2-P02-native-${Date.now()}-${process.pid}`);
const screenshotModelBPath = path.join(runRoot, "model-b-created.png");
const screenshotReadOnlyPath = path.join(runRoot, "model-b-history-only.png");
const screenshotModelAPath = path.join(runRoot, "model-a-reopened.png");
const suffix = `${Date.now()}_${process.pid}`;
const capabilityAPath = path.join(releaseRoot, `MODEL_CAPABILITY_P2_02_A_${suffix}.json`);
const capabilityBPath = path.join(releaseRoot, `MODEL_CAPABILITY_P2_02_B_${suffix}.json`);
assert.ok(!existsSync(capabilityAPath) && !existsSync(capabilityBPath),
  "refusing to overwrite an existing test capability record");

const modelAId = "p2-02-fixture-model-a";
const modelBId = "p2-02-fixture-model-b";
const modelAPrompt = "P2-02 Model A saved task before restart.";
const modelBPrompt = "P2-02 Model B saved task before restart.";
const modelAReply = "Deterministic Model A fixture response.";
const modelBReply = "Deterministic Model B fixture response.";
const providerRequests = { modelA: 0, modelB: 0 };
const fixtureErrors = [];
const unknownObservation = (evidenceSource) => ({
  state: "Unknown",
  value: null,
  evidenceSource,
});
function fixtureCapabilityRecord(modelIdentifier, contextWindow) {
  return {
    providerId: "lmstudio",
    providerDisplayName: "LM Studio · P2-02 fixture",
    providerServerVersion: "Unknown",
    endpoint: "pending-loopback-endpoint",
    modelIdentifier,
    modelVariant: "test-only loopback Responses fixture; no model weights",
    architecture: "Unknown",
    parameterCount: "Unknown",
    modelSizeBytes: "Unknown",
    quantization: unknownObservation("P2-02 fixture; no model weights or quantization metadata"),
    contextWindowAdvertised: unknownObservation("P2-02 fixture; provider metadata not queried"),
    contextWindowEffective: {
      state: "Known",
      value: contextWindow,
      evidenceSource: "P2-02 deterministic fixture value only; not observed model metadata",
    },
    reasoningControls: unknownObservation("P2-02 fixture; no model loaded"),
    toolFunctionCalling: {
      state: "Known",
      value: "tool_use",
      evidenceSource: "P2-02 synthetic test-catalog declaration only; no real model capability claim or tool call qualification",
    },
    applyPatchToolType: unknownObservation("P2-02 fixture; no patch format qualified"),
    structuredOutput: unknownObservation("P2-02 fixture; provider metadata not queried"),
    agentMetadata: {
      state: "Known",
      value: "type=llm; inputModalities=text; vision=False",
      evidenceSource: "P2-02 synthetic test-catalog declaration only; no model weights or real model metadata",
    },
  };
}
const modelARecord = fixtureCapabilityRecord(modelAId, 32768);
const modelBRecord = fixtureCapabilityRecord(modelBId, 16384);

function assistantResponse(responseId, itemId, text) {
  const events = [
    { type: "response.created", response: { id: responseId } },
    {
      type: "response.output_item.done",
      item: {
        type: "message",
        role: "assistant",
        id: itemId,
        content: [{ type: "output_text", text }],
      },
    },
    {
      type: "response.completed",
      response: {
        id: responseId,
        usage: {
          input_tokens: 0,
          input_tokens_details: null,
          output_tokens: 0,
          output_tokens_details: null,
          total_tokens: 0,
        },
      },
    },
  ];
  return events.map((event) => `event: ${event.type}\ndata: ${JSON.stringify(event)}\n\n`).join("");
}

const fixtureServer = createHttpServer((request, response) => {
  const route = request.url;
  const isModelA = route === "/a/v1/responses";
  const isModelB = route === "/b/v1/responses";
  if (request.method !== "POST" || (!isModelA && !isModelB)) {
    response.writeHead(404).end();
    return;
  }
  const selected = isModelA ? "modelA" : "modelB";
  const expectedModel = isModelA ? modelAId : modelBId;
  let body = "";
  request.setEncoding("utf8");
  request.on("data", (chunk) => { body += chunk; });
  request.on("end", () => {
    let payload;
    try { payload = JSON.parse(body); }
    catch {
      fixtureErrors.push(`${selected}: request body was not JSON`);
      response.writeHead(400).end();
      return;
    }
    providerRequests[selected] += 1;
    if (payload.model !== expectedModel) {
      fixtureErrors.push(`${selected}: expected ${expectedModel}, got ${payload.model ?? "<missing>"}`);
      response.writeHead(409).end();
      return;
    }
    const number = providerRequests[selected];
    const text = isModelA ? modelAReply : modelBReply;
    response.writeHead(200, { "content-type": "text/event-stream", "cache-control": "no-cache" });
    response.end(assistantResponse(`p2-02-${selected}-${number}`, `p2-02-message-${selected}-${number}`, text));
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

function requestHostClose(pid) {
  const command = `$process = Get-Process -Id ${pid} -ErrorAction Stop; if (-not $process.CloseMainWindow()) { exit 2 }; exit 0`;
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", command], {
    encoding: "utf8",
    timeout: 10000,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`could not request a graceful close for QA host PID ${pid}: ${result.stderr || result.stdout}`);
}

async function waitForCdp(port, child) {
  const deadline = Date.now() + 45000;
  while (Date.now() < deadline) {
    if (child.exitCode !== null) throw new Error(`WPF host exited before CDP opened (code ${child.exitCode})`);
    try {
      const result = await fetch(`http://127.0.0.1:${port}/json/version`, { signal: AbortSignal.timeout(1000) });
      if (result.ok) return await result.json();
    } catch { /* WebView2 is starting. */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`WebView2 CDP did not open on port ${port}`);
}

async function waitForNativePage(browser, port, child) {
  const deadline = Date.now() + 30000;
  let targets = [];
  while (Date.now() < deadline) {
    if (child.exitCode !== null) throw new Error(`WPF host exited before its page appeared (code ${child.exitCode})`);
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

const { chromium } = await import(pathToFileURL(playwrightModulePath).href);
let browser;
let host;
let page;
let hostClosed = false;
const pageIssues = [];

async function launchHost() {
  const debugPort = await reservePort();
  const hostEnv = { ...process.env };
  for (const name of ["OPENROUTER_API_KEY", "OPENAI_API_KEY", "NEOBABYLON_WINDOWS_SANDBOX_MODE"]) delete hostEnv[name];
  Object.assign(hostEnv, {
    NEOBABYLON_SOURCE_ROOT: sourceRoot,
    NEOBABYLON_APPLICATION_ROOT: applicationRoot,
    NEOBABYLON_MODEL_CAPABILITY_PATH: capabilityAPath,
    NEOBABYLON_ENABLE_WEBVIEW2_REMOTE_DEBUG: "1",
    NEOBABYLON_WEBVIEW2_DEBUG_PORT: String(debugPort),
  });
  host = spawn(hostPath, [], { cwd: sourceRoot, env: hostEnv, stdio: "ignore", windowsHide: false });
  await waitForCdp(debugPort, host);
  browser = await chromium.connectOverCDP(`http://127.0.0.1:${debugPort}`);
  page = await waitForNativePage(browser, debugPort, host);
  page.on("pageerror", (error) => pageIssues.push(error.message));
  page.on("console", (message) => {
    if (message.type() === "error") pageIssues.push(message.text());
  });
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  assert.equal(await page.title(), "NeoBabylon");
  assert.equal(await page.locator("vite-error-overlay").count(), 0);
  await page.getByRole("textbox", { name: "Message NeoBabylon" }).waitFor();
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
  hostClosed = true;
}

async function showCapabilityDetails(expectedRecord) {
  if (await page.locator(".details-panel").count() === 0) {
    await page.getByRole("button", { name: "Toggle details" }).click();
  }
  const panel = page.locator(".details-panel");
  await panel.waitFor();
  const text = await panel.innerText();
  assert.ok(text.includes(expectedRecord.modelIdentifier), `selected model was not visible: ${text}`);
  assert.ok(text.includes(expectedRecord.endpoint), `selected endpoint was not visible: ${text}`);
  const context = expectedRecord.contextWindowEffective.value;
  if (typeof context === "number") {
    assert.ok(text.includes(`${context.toLocaleString("en-US")} tokens`),
      `selected capability context was not visible: ${text}`);
  }
  return text;
}

async function selectModel(modelIdentifier) {
  await page.locator("button.model-select").click();
  const option = page.getByRole("option", { name: new RegExp(modelIdentifier.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")) });
  await option.waitFor({ timeout: 10000 });
  await option.click();
  await page.waitForFunction((expected) =>
    document.querySelector(".model-select .model-short-name")?.textContent === expected,
  modelIdentifier, { timeout: 15000 });
}

async function sendPrompt(prompt, reply) {
  const conversation = page.getByRole("region", { name: "Conversation" });
  await page.getByRole("textbox", { name: "Message NeoBabylon" }).fill(prompt);
  await page.getByRole("button", { name: "Send message" }).click();
  await conversation.locator("article.message-user .message-text")
    .getByText(prompt, { exact: true }).waitFor({ timeout: 20000 });
  try {
    await conversation.locator("article.message-assistant .message-text")
      .getByText(reply, { exact: true }).waitFor({ timeout: 30000 });
  } catch (error) {
    const state = await page.evaluate(() => ({
      turnState: document.querySelector("section.conversation")?.getAttribute("data-turn-state"),
      model: document.querySelector("button.model-select .model-short-name")?.textContent,
      alerts: [...document.querySelectorAll('[role="alert"]')].map((element) => element.textContent?.trim()),
      activeTask: localStorage.getItem("neobabylon.active-task:v1"),
    }));
    const screenshot = path.join(runRoot, `failed-send-${Date.now()}.png`);
    await page.screenshot({ path: screenshot }).catch(() => {});
    console.error(`NATIVE_MODEL_TURN_DIAGNOSTIC ${JSON.stringify({ prompt, providerRequests, fixtureErrors, state, screenshot })}`);
    throw error;
  }
  await page.waitForFunction(() => {
    const state = document.querySelector("section.conversation")?.getAttribute("data-turn-state");
    return state !== "starting" && state !== "running";
  }, null, { timeout: 30000 });
  await page.locator("button.saved-chat").filter({ hasText: prompt }).waitFor({ timeout: 15000 });
}

function readActivePointer() {
  return page.evaluate(() => {
    const value = localStorage.getItem("neobabylon.active-task:v1");
    return value === null ? null : JSON.parse(value);
  });
}

function bindingPath(threadId) {
  const fileId = createHash("sha256").update(threadId, "utf8").digest("hex");
  return path.join(applicationRoot, "Data", "NeoBabylon", "TaskBindings", `${fileId}.json`);
}

async function openTask(prompt) {
  const task = page.locator("button.saved-chat").filter({ hasText: prompt });
  await task.waitFor({ timeout: 15000 });
  await task.click();
  const conversation = page.getByRole("region", { name: "Conversation" });
  await conversation.getByText(prompt, { exact: true }).waitFor({ timeout: 20000 });
  return task;
}

const fixturePort = await listen(fixtureServer);
modelARecord.endpoint = `http://127.0.0.1:${fixturePort}/a/v1`;
modelBRecord.endpoint = `http://127.0.0.1:${fixturePort}/b/v1`;

try {
  writeFileSync(capabilityAPath, `${JSON.stringify(modelARecord, null, 2)}\n`, { encoding: "utf8", flag: "wx" });
  writeFileSync(capabilityBPath, `${JSON.stringify(modelBRecord, null, 2)}\n`, { encoding: "utf8", flag: "wx" });

  await launchHost();
  assert.equal((await showCapabilityDetails(modelARecord)).includes(modelARecord.endpoint), true);
  await sendPrompt(modelAPrompt, modelAReply);
  const modelAPointer = await readActivePointer();
  assert.equal(modelAPointer?.modelIdentifier, modelAId);
  assert.equal(modelAPointer?.providerId, "lmstudio");
  const modelABindingPath = bindingPath(modelAPointer.threadId);
  const modelABindingBeforeRestart = readFileSync(modelABindingPath, "utf8");
  const modelABinding = JSON.parse(modelABindingBeforeRestart).binding;
  assert.equal(modelABinding.threadId, modelAPointer.threadId);
  assert.equal(modelABinding.modelIdentifier, modelAId);

  await page.getByRole("button", { name: "New task" }).click();
  await selectModel(modelBId);
  await showCapabilityDetails(modelBRecord);
  assert.deepEqual(providerRequests, { modelA: 1, modelB: 0 },
    "switching capability records must not contact either provider endpoint");
  await sendPrompt(modelBPrompt, modelBReply);
  const modelBPointer = await readActivePointer();
  assert.equal(modelBPointer?.modelIdentifier, modelBId);
  assert.equal(modelBPointer?.providerId, "lmstudio");
  assert.notEqual(modelBPointer.threadId, modelAPointer.threadId);
  const modelBBindingPath = bindingPath(modelBPointer.threadId);
  const modelBBindingBeforeRestart = readFileSync(modelBBindingPath, "utf8");
  const modelBBinding = JSON.parse(modelBBindingBeforeRestart).binding;
  assert.equal(modelBBinding.threadId, modelBPointer.threadId);
  assert.equal(modelBBinding.modelIdentifier, modelBId);
  assert.deepEqual(providerRequests, { modelA: 1, modelB: 1 },
    "each deliberately authored task must issue exactly one loopback request");
  await page.screenshot({ path: screenshotModelBPath });
  await closeHost();

  const changedContext = 8192;
  assert.notEqual(modelBRecord.contextWindowEffective.value, changedContext);
  modelBRecord.contextWindowEffective = {
    ...modelBRecord.contextWindowEffective,
    value: changedContext,
    evidenceSource: "P2-02 deterministic fixture mutation only; not observed model metadata",
  };
  writeFileSync(capabilityBPath, `${JSON.stringify(modelBRecord, null, 2)}\n`, "utf8");

  await launchHost();
  const modelATaskAfterRestart = page.locator("button.saved-chat").filter({ hasText: modelAPrompt });
  await modelATaskAfterRestart.waitFor({ timeout: 30000 });
  assert.equal(await modelATaskAfterRestart.count(), 1,
    "the original Model A task must be visible after restart with Model A selected");
  await selectModel(modelBId);
  await showCapabilityDetails(modelBRecord);
  const modelBTask = await openTask(modelBPrompt);
  const readOnlyNotice = page.locator(".reconnect-warning");
  await readOnlyNotice.getByText(/different capability record/i).waitFor({ timeout: 15000 });
  assert.equal(await page.getByRole("textbox", { name: "Message NeoBabylon" }).isDisabled(), true,
    "a context-mismatched saved task must keep its composer disabled");
  assert.equal(await page.getByRole("button", { name: "Fork conversation" }).isDisabled(), true,
    "a context-mismatched saved task must keep fork disabled");
  const newTask = page.getByRole("button", { name: "New task" });
  assert.equal(await newTask.isEnabled(), true, "history-only state must retain a usable New task action");
  assert.equal(await modelBTask.getAttribute("aria-current"), "page");
  await page.getByRole("region", { name: "Conversation" }).getByText(modelBReply, { exact: true }).waitFor();
  assert.deepEqual(providerRequests, { modelA: 1, modelB: 1 },
    "restart, capability switch, and blocked history resume must issue no provider request");
  assert.equal(readFileSync(modelBBindingPath, "utf8"), modelBBindingBeforeRestart,
    "the changed context must not silently rewrite the original Model B binding");
  await page.screenshot({ path: screenshotReadOnlyPath });

  await newTask.click();
  await selectModel(modelAId);
  await showCapabilityDetails(modelARecord);
  const reopenedModelATask = await openTask(modelAPrompt);
  await page.getByRole("region", { name: "Conversation" }).getByText(modelAReply, { exact: true }).waitFor();
  assert.equal(await reopenedModelATask.getAttribute("aria-current"), "page");
  assert.equal(await page.getByRole("textbox", { name: "Message NeoBabylon" }).isDisabled(), false,
    "the unchanged Model A binding must remain executable after host restart");
  const activeAfterReopen = await readActivePointer();
  assert.deepEqual(activeAfterReopen, modelAPointer,
    "returning to Model A must restore the same saved thread identity");
  assert.equal(readFileSync(modelABindingPath, "utf8"), modelABindingBeforeRestart,
    "exact Model A resume must preserve the original capability binding bytes");
  assert.equal(readFileSync(modelBBindingPath, "utf8"), modelBBindingBeforeRestart,
    "returning to Model A must not rewrite Model B's separate binding");
  assert.deepEqual(providerRequests, { modelA: 1, modelB: 1 },
    "exact task resume must not replay either saved provider request");

  const details = await showCapabilityDetails(modelARecord);
  assert.ok(details.includes("App data is separate from the source repository"));
  assert.ok(details.includes("Ordinary Codex data root is not used"));
  await page.screenshot({ path: screenshotModelAPath });
  assert.deepEqual(pageIssues, [], "native capability switching/rebind should not produce browser errors");
  assert.deepEqual(fixtureErrors, [], "the local fixture should receive only the explicitly selected model tuple");

  const lock = JSON.parse(readFileSync(path.join(sourceRoot, "runtime", "runtime-lock.json"), "utf8"));
  console.log(JSON.stringify({
    passed: true,
    scenario: "P2-02 native WPF capability switch, host restart, stale-context history-only state, and exact original-task rebind",
    source: "WPF + WebView2 + pinned App Server + loopback-only deterministic Responses fixtures",
    runtimeVersion: lock.runtime.version,
    runtimeSha256: lock.sha256,
    modelA: {
      providerId: modelAPointer.providerId,
      modelIdentifier: modelAPointer.modelIdentifier,
      endpoint: modelARecord.endpoint,
      effectiveContext: modelARecord.contextWindowEffective.value,
      threadId: modelAPointer.threadId,
      exactBindingPreserved: true,
    },
    modelB: {
      providerId: modelBRecord.providerId,
      modelIdentifier: modelBRecord.modelIdentifier,
      endpoint: modelBRecord.endpoint,
      contextBeforeRestart: 16384,
      contextAfterRestart: changedContext,
      executionEligible: false,
      blockedReason: "capabilityRecordChanged",
      historyRetained: true,
      originalBindingPreserved: true,
    },
    providerRequests,
    noInferenceOnSwitchOrResume: true,
    applicationRoot,
    sourceRoot,
    screenshots: [screenshotModelBPath, screenshotReadOnlyPath, screenshotModelAPath],
    pageIssues,
  }));
} finally {
  if (browser) await browser.close().catch(() => {});
  if (host && host.exitCode === null) {
    try {
      requestHostClose(host.pid);
      await waitForExit(host, 20000);
      hostClosed = true;
    } catch (error) {
      host.kill();
      await waitForExit(host, 5000).catch(() => {});
      if (!hostClosed) console.error(`QA host needed targeted process cleanup after graceful-close attempt: ${error.message}`);
    }
  }
  fixtureServer.close();
  for (const testRecordPath of [capabilityAPath, capabilityBPath]) {
    const normalized = path.resolve(testRecordPath);
    if (within(normalized, releaseRoot)
      && path.basename(normalized).startsWith("MODEL_CAPABILITY_P2_02_")
      && existsSync(normalized)) {
      unlinkSync(normalized);
    }
  }
}
