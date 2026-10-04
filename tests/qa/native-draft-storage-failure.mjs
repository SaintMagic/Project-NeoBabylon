import assert from "node:assert/strict";
import { spawn, spawnSync } from "node:child_process";
import { createServer as createHttpServer } from "node:http";
import { createServer as createTcpServer } from "node:net";
import { createHash } from "node:crypto";
import {
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  unlinkSync,
  writeFileSync,
} from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { assertQaRunsParent, newQaRun } from "./qa-run-root.mjs";

const [hostPathArg, playwrightModulePathArg, qaParentArg] = process.argv.slice(2);
assert.ok(hostPathArg && playwrightModulePathArg && qaParentArg,
  "usage: node native-draft-storage-failure.mjs <NeoBabylon.Host.exe> <playwright-core-entry.mjs> <exact .local\\Lab\\Runs parent>");

const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const hostPath = path.resolve(hostPathArg);
const playwrightModulePath = path.resolve(playwrightModulePathArg);
const qaParent = path.resolve(qaParentArg);
const releaseRoot = path.join(sourceRoot, "docs", "release");
assertQaRunsParent(sourceRoot, qaParent);
for (const [label, filePath] of [["WPF host", hostPath], ["Playwright module", playwrightModulePath]]) {
  assert.ok(existsSync(filePath), `${label} does not exist: ${filePath}`);
}

const runtimeLock = JSON.parse(readFileSync(path.join(sourceRoot, "runtime", "runtime-lock.json"), "utf8"));
const runtimePath = path.resolve(sourceRoot, runtimeLock.runtime.appServerBinaryRelativePath);
assert.ok(existsSync(runtimePath), `pinned App Server binary is missing: ${runtimePath}`);
const runtimeHash = createHash("sha256").update(readFileSync(runtimePath)).digest("hex");
assert.equal(runtimeHash, runtimeLock.runtime.sha256.toLowerCase(), "pinned App Server binary does not match runtime-lock.json");

const { runRoot, applicationRoot } = newQaRun(qaParent, `P2-P04-storage-${Date.now()}-${process.pid}`);
const modelIdentifier = "p2-04-storage-failure-fixture";
const prompts = {
  writeBaseline: "P2-04 write-fault baseline; saved history remains available.",
  writeDraft: "P2-04 unsent prompt; local write is deliberately rejected.",
  clearBaseline: "P2-04 clear-fault baseline; open the saved task after restart.",
  clearDraft: "P2-04 accepted prompt whose local draft clear will fail.",
  totalBaseline: "P2-04 total-fault baseline; inspect this history without storage.",
  totalDraft: "P2-04 unsent prompt while all local storage access fails.",
};
const replyFor = new Map(Object.entries(prompts).map(([name, prompt]) =>
  [prompt, `Deterministic fixture response for ${name}.`]));
const expectedRequests = [
  prompts.writeBaseline,
  prompts.clearBaseline,
  prompts.clearDraft,
  prompts.totalBaseline,
];
const providerRequests = [];
const fixtureErrors = [];
const pageIssues = [];

const unknown = (evidenceSource) => ({ state: "Unknown", value: null, evidenceSource });
const capability = {
  providerId: "lmstudio",
  providerDisplayName: "LM Studio · P2-04 local storage fixture",
  providerServerVersion: "Unknown",
  endpoint: "pending-loopback-endpoint",
  modelIdentifier,
  modelVariant: "test-only local Responses fixture; no model weights",
  architecture: "Unknown",
  parameterCount: "Unknown",
  modelSizeBytes: "Unknown",
  quantization: unknown("P2-04 fixture; no model weights or quantization metadata"),
  contextWindowAdvertised: unknown("P2-04 fixture; provider metadata not queried"),
  contextWindowEffective: {
    state: "Known",
    value: 32768,
    evidenceSource: "P2-04 deterministic fixture value only; not observed model metadata",
  },
  reasoningControls: unknown("P2-04 fixture; no model loaded"),
  toolFunctionCalling: {
    state: "Known",
    value: "tool_use",
    evidenceSource: "P2-04 synthetic test-catalog prerequisite only; no real model capability claim",
  },
  applyPatchToolType: unknown("P2-04 fixture; no patch format qualified"),
  structuredOutput: unknown("P2-04 fixture; provider metadata not queried"),
  agentMetadata: {
    state: "Known",
    value: "type=llm; inputModalities=text; vision=False",
    evidenceSource: "P2-04 synthetic test-catalog declaration only; no model weights or real metadata",
  },
};

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
        usage: { input_tokens: 0, input_tokens_details: null, output_tokens: 0,
          output_tokens_details: null, total_tokens: 0 },
      },
    },
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
    const serializedInput = JSON.stringify(payload.input ?? []);
    const requestNumber = providerRequests.length + 1;
    const expectedPrompt = expectedRequests[requestNumber - 1];
    const prompt = expectedPrompt && serializedInput.includes(expectedPrompt) ? expectedPrompt : null;
    providerRequests.push({ number: requestNumber, model: payload.model ?? null, prompt: prompt ?? null });
    if (payload.model !== modelIdentifier || !expectedPrompt || prompt !== expectedPrompt) {
      fixtureErrors.push(`unexpected provider request #${requestNumber}: ${JSON.stringify({ model: payload.model ?? null, prompt })}`);
      response.writeHead(409).end();
      return;
    }
    response.writeHead(200, { "content-type": "text/event-stream", "cache-control": "no-cache" });
    response.end(assistantResponse(`p2-04-response-${requestNumber}`, `p2-04-item-${requestNumber}`,
      replyFor.get(prompt)));
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
    const timer = setTimeout(() => reject(new Error(`QA host PID ${child.pid} did not exit within ${timeoutMs}ms`)), timeoutMs);
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
  if (result.status !== 0) throw new Error(`could not request graceful close for QA host PID ${pid}: ${result.stderr || result.stdout}`);
}

function findFiles(directory, suffix) {
  if (!existsSync(directory)) return [];
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const child = path.join(directory, entry.name);
    return entry.isDirectory() ? findFiles(child, suffix) : entry.name.endsWith(suffix) ? [child] : [];
  });
}

function journalUserPrompts(applicationRoot) {
  const sessionRoot = path.join(applicationRoot, "Data", "CodexHome", "sessions");
  const result = [];
  for (const filePath of findFiles(sessionRoot, ".jsonl")) {
    for (const line of readFileSync(filePath, "utf8").split(/\r?\n/)) {
      if (!line) continue;
      let record;
      try { record = JSON.parse(line); } catch { continue; }
      if (record.type !== "event_msg" || record.payload?.type !== "item_completed"
        || record.payload?.item?.type !== "UserMessage") continue;
      const text = (record.payload.item.content ?? [])
        .filter((part) => part.type === "text")
        .map((part) => part.text ?? "")
        .join("");
      if (text) result.push(text);
    }
  }
  return result;
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
let currentApplicationRoot;
let capabilityHash;

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
    } catch { /* The native target is not ready yet. */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`native NeoBabylon page was not found; CDP targets=${JSON.stringify(targets)}`);
}

async function launchHost(applicationRoot) {
  currentApplicationRoot = applicationRoot;
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
  page.on("console", (message) => {
    if (message.type() === "error") pageIssues.push(`error: ${message.text()}`);
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
}

async function waitForHistoryReady() {
  await page.locator(".saved-thread-list .saved-chat").first().waitFor({ timeout: 30000 });
  try {
    await page.waitForFunction(() => {
      const row = document.querySelector(".saved-thread-list .saved-chat");
      return row && (row.getAttribute("aria-current") === "page" || !row.disabled);
    }, null, { timeout: 10000 });
  } catch (error) {
    const state = await page.evaluate(() => ({
      rows: [...document.querySelectorAll(".saved-thread-list .saved-chat")]
        .map((row) => ({ text: row.innerText, disabled: row.disabled, current: row.getAttribute("aria-current") })),
      composerDisabled: document.querySelector('textarea[aria-label="Message NeoBabylon"]')?.disabled,
      alerts: [...document.querySelectorAll('[role="alert"], [role="status"]')].map((item) => item.textContent?.trim()),
    }));
    const diagnosticScreenshot = path.join(runRoot, `history-disabled-${Date.now()}.png`);
    await page.screenshot({ path: diagnosticScreenshot }).catch(() => {});
    console.error(`P2_04_HISTORY_DIAGNOSTIC ${JSON.stringify({ state, diagnosticScreenshot })}`);
    throw error;
  }
}

async function openFirstSavedTask(prompt) {
  const row = page.locator(".saved-thread-list .saved-chat").first();
  await row.waitFor({ timeout: 30000 });
  await waitForHistoryReady();
  if (await row.getAttribute("aria-current") !== "page") {
    await row.click();
  }
  await page.getByRole("region", { name: "Conversation" })
    .locator("article.message-user .message-text").getByText(prompt, { exact: true })
    .waitFor({ timeout: 30000 });
}

async function sendPrompt(prompt) {
  const before = providerRequests.length;
  await page.getByRole("textbox", { name: "Message NeoBabylon" }).fill(prompt);
  await page.getByRole("button", { name: "Send message" }).click();
  await page.getByRole("region", { name: "Conversation" })
    .locator("article.message-user .message-text").getByText(prompt, { exact: true })
    .waitFor({ timeout: 20000 });
  const reply = replyFor.get(prompt);
  await page.getByRole("region", { name: "Conversation" })
    .locator("article.message-assistant .message-text").getByText(reply, { exact: true })
    .waitFor({ timeout: 30000 });
  await page.waitForFunction(() => {
    const state = document.querySelector("section.conversation")?.getAttribute("data-turn-state");
    return state !== "starting" && state !== "running";
  }, null, { timeout: 30000 });
  await waitFor(() => providerRequests.length === before + 1,
    `exactly one fixture request was not observed for prompt: ${prompt}`);
  assert.equal(providerRequests[before]?.prompt, prompt);
}

async function installStorageFault(mode) {
  await page.addInitScript((initialMode) => {
    const state = { mode: initialMode, failures: [] };
    Object.defineProperty(window, "__neoBabylonP204StorageFault", {
      configurable: true,
      value: state,
    });
    const draftPrefix = "neobabylon.draft:v1:";
    const original = {
      getItem: Storage.prototype.getItem,
      setItem: Storage.prototype.setItem,
      removeItem: Storage.prototype.removeItem,
      clear: Storage.prototype.clear,
      key: Storage.prototype.key,
    };
    const fail = (operation, key, value) => {
      const currentMode = state.mode;
      const isDraft = typeof key === "string" && key.startsWith(draftPrefix);
      const denied = currentMode === "total"
        || currentMode === "write" && operation === "setItem" && isDraft
        || currentMode === "clear" && isDraft
          && (operation === "removeItem" || operation === "setItem" && value === "");
      if (denied) {
        state.failures.push({ operation, key: key ?? null, mode: currentMode });
        throw new DOMException("P2-04 injected WebView storage failure", "QuotaExceededError");
      }
      return false;
    };
    Storage.prototype.getItem = function getItem(key) {
      if (fail("getItem", key)) return null;
      return original.getItem.call(this, key);
    };
    Storage.prototype.setItem = function setItem(key, value) {
      if (fail("setItem", key, String(value))) return undefined;
      return original.setItem.call(this, key, value);
    };
    Storage.prototype.removeItem = function removeItem(key) {
      if (fail("removeItem", key)) return undefined;
      return original.removeItem.call(this, key);
    };
    Storage.prototype.clear = function clear() {
      if (fail("clear", null)) return undefined;
      return original.clear.call(this);
    };
    Storage.prototype.key = function storageKey(index) {
      if (fail("key", String(index))) return null;
      return original.key.call(this, index);
    };
  }, mode);
  await page.reload();
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  await page.getByRole("textbox", { name: "Message NeoBabylon" }).waitFor();
}

async function setStorageFaultMode(mode) {
  await page.evaluate((value) => {
    const state = window.__neoBabylonP204StorageFault;
    if (!state) throw new Error("P2-04 local-storage fault injection is not installed");
    state.mode = value;
  }, mode);
}

async function clearActiveTaskPointerForManualHistoryReopen() {
  await page.evaluate(() => window.localStorage.removeItem("neobabylon.active-task:v1"));
}

async function alertTexts() {
  return page.locator('[role="alert"]').allInnerTexts();
}

async function runWriteFailure() {
  const scenarioRoot = path.join(runRoot, "write-failure");
  mkdirSync(scenarioRoot);
  const { applicationRoot } = newQaRun(qaParent, `P2-P04-write-${Date.now()}-${process.pid}`);
  await launchHost(applicationRoot);
  await sendPrompt(prompts.writeBaseline);
  await clearActiveTaskPointerForManualHistoryReopen();
  await installStorageFault("write");
  await openFirstSavedTask(prompts.writeBaseline);

  const composer = page.getByRole("textbox", { name: "Message NeoBabylon" });
  await composer.fill(prompts.writeDraft);
  await page.getByText("This draft could not be saved locally. Keep this window open until you can send or copy it.")
    .waitFor({ timeout: 10000 });
  assert.equal(await composer.inputValue(), prompts.writeDraft,
    "a rejected persistence write must leave the exact draft visible in the current window");
  assert.equal(await page.getByRole("region", { name: "Conversation" })
    .locator("article.message-user .message-text").getByText(prompts.writeDraft, { exact: true }).count(), 0,
  "an unsent write-failed prompt must not appear in saved conversation history");
  assert.equal(providerRequests.length, 1, "typing an unsaved draft must not send a provider request");
  assert.ok(await page.evaluate(() => window.__neoBabylonP204StorageFault.failures
    .some((failure) => failure.operation === "setItem" && failure.key?.startsWith("neobabylon.draft:v1:"))),
  "the write-failure path was not actually injected");
  const writeBeforeRestart = path.join(scenarioRoot, "write-failure-before-host-restart.png");
  await page.screenshot({ path: writeBeforeRestart });

  await closeHost();
  await launchHost(applicationRoot);
  await installStorageFault("write");
  await openFirstSavedTask(prompts.writeBaseline);
  assert.equal(await page.getByRole("textbox", { name: "Message NeoBabylon" }).inputValue(), "",
    "a write-rejected prompt is not durably recoverable after process restart");
  assert.equal(await page.getByRole("region", { name: "Conversation" })
    .locator("article.message-user .message-text").getByText(prompts.writeDraft, { exact: true }).count(), 0);
  assert.equal(providerRequests.length, 1, "host restart must not submit or replay an unpersisted draft");
  assert.deepEqual(journalUserPrompts(applicationRoot), [prompts.writeBaseline]);
  const writeAfterRestart = path.join(scenarioRoot, "write-failure-after-host-restart.png");
  await page.screenshot({ path: writeAfterRestart });
  await clearActiveTaskPointerForManualHistoryReopen();
  await closeHost();
  return { visibleDraftAndWarningBeforeRestart: true, draftUnavailableAfterRestart: true,
    historyRemainsInspectable: true, unsolicitedRequests: 0,
    screenshots: [writeBeforeRestart, writeAfterRestart] };
}

async function runClearFailure() {
  const scenarioRoot = path.join(runRoot, "clear-failure");
  mkdirSync(scenarioRoot);
  const { applicationRoot } = newQaRun(qaParent, `P2-P04-clear-${Date.now()}-${process.pid}`);
  await launchHost(applicationRoot);
  await sendPrompt(prompts.clearBaseline);
  await clearActiveTaskPointerForManualHistoryReopen();
  await installStorageFault("normal");
  await openFirstSavedTask(prompts.clearBaseline);

  const composer = page.getByRole("textbox", { name: "Message NeoBabylon" });
  await composer.fill(prompts.clearDraft);
  await setStorageFaultMode("clear");
  await page.getByRole("button", { name: "Send message" }).click();
  await page.getByRole("region", { name: "Conversation" })
    .locator("article.message-assistant .message-text")
    .getByText(replyFor.get(prompts.clearDraft), { exact: true }).waitFor({ timeout: 30000 });
  await page.getByText("The sent draft could not be cleared from local storage. Check history before resending after a restart.")
    .waitFor({ timeout: 10000 });
  assert.equal(await composer.inputValue(), "", "the accepted prompt must clear from the live composer even if local cleanup fails");
  assert.equal(providerRequests.length, 3, "one explicit accepted clear-fault prompt is allowed; no extra request is");
  assert.equal(journalUserPrompts(applicationRoot).filter((entry) => entry === prompts.clearDraft).length, 1,
    "the clear-fault prompt must be accepted in App Server history exactly once");
  assert.ok(await page.evaluate((prompt) => {
    const values = Object.keys(localStorage).filter((key) => key.startsWith("neobabylon.draft:v1:"))
      .map((key) => localStorage.getItem(key));
    return values.includes(prompt)
      && window.__neoBabylonP204StorageFault.failures.some((failure) => failure.operation === "removeItem")
      && window.__neoBabylonP204StorageFault.failures.some((failure) => failure.operation === "setItem" && failure.key?.startsWith("neobabylon.draft:v1:"));
  }, prompts.clearDraft), "the delete and empty-value fallback failures were not both exercised");
  const clearBeforeRestart = path.join(scenarioRoot, "clear-failure-before-host-restart.png");
  await page.screenshot({ path: clearBeforeRestart });
  await clearActiveTaskPointerForManualHistoryReopen();
  assert.equal(await page.evaluate(() => window.localStorage.getItem("neobabylon.active-task:v1")), null,
    "the QA restart must reopen through saved history instead of the active-task hint");

  await closeHost();
  await launchHost(applicationRoot);
  await installStorageFault("clear");
  await openFirstSavedTask(prompts.clearDraft);
  try {
    await page.getByText("A prior send could not be matched safely to saved task history. Check this conversation before resending.")
      .waitFor({ timeout: 15000 });
  } catch (error) {
    const state = await page.evaluate(() => ({
      row: document.querySelector(".saved-thread-list .saved-chat")?.outerHTML,
      alerts: [...document.querySelectorAll('[role="alert"], [role="status"]')].map((item) => item.textContent?.trim()),
      composer: document.querySelector('textarea[aria-label="Message NeoBabylon"]')?.value,
      storageKeys: Object.keys(localStorage),
      drafts: Object.keys(localStorage).filter((key) => key.startsWith("neobabylon.draft:v1:"))
        .map((key) => ({ key, value: localStorage.getItem(key) })),
      pending: Object.keys(localStorage).filter((key) => key.startsWith("neobabylon.pending-submission:v1:"))
        .map((key) => ({ key, value: localStorage.getItem(key) })),
      activeTask: localStorage.getItem("neobabylon.active-task:v1"),
    }));
    const diagnosticScreenshot = path.join(scenarioRoot, "clear-restart-warning-missing.png");
    await page.screenshot({ path: diagnosticScreenshot }).catch(() => {});
    console.error(`P2_04_CLEAR_RESTART_DIAGNOSTIC ${JSON.stringify({ state, diagnosticScreenshot })}`);
    throw error;
  }
  assert.equal(await page.getByRole("region", { name: "Conversation" })
    .locator("article.message-user .message-text").getByText(prompts.clearDraft, { exact: true }).count(), 1,
  "the accepted prompt must remain visible exactly once in history after restart");
  assert.equal(await page.getByRole("textbox", { name: "Message NeoBabylon" }).inputValue(), prompts.clearDraft,
    "the retained stored draft must be visible for comparison with accepted history");
  assert.equal(providerRequests.length, 3, "reopening after a failed clear must not reissue the request");
  assert.equal(journalUserPrompts(applicationRoot).filter((entry) => entry === prompts.clearDraft).length, 1);
  const clearAfterRestart = path.join(scenarioRoot, "clear-failure-after-host-restart.png");
  await page.screenshot({ path: clearAfterRestart });
  await closeHost();
  return { clearFailureVisibleBeforeRestart: true, acceptedHistoryOnceAfterRestart: true,
    retainedDraftComparedAgainstHistory: true, noAutomaticReplay: true, unsolicitedRequests: 0,
    screenshots: [clearBeforeRestart, clearAfterRestart] };
}

async function runTotalFailure() {
  const scenarioRoot = path.join(runRoot, "total-storage-failure");
  mkdirSync(scenarioRoot);
  const { applicationRoot } = newQaRun(qaParent, `P2-P04-total-${Date.now()}-${process.pid}`);
  await launchHost(applicationRoot);
  await sendPrompt(prompts.totalBaseline);
  await clearActiveTaskPointerForManualHistoryReopen();
  await installStorageFault("total");
  await openFirstSavedTask(prompts.totalBaseline);

  const composer = page.getByRole("textbox", { name: "Message NeoBabylon" });
  await composer.fill(prompts.totalDraft);
  await page.getByText("This draft could not be saved locally. Keep this window open until you can send or copy it.")
    .waitFor({ timeout: 10000 });
  assert.equal(await composer.inputValue(), prompts.totalDraft,
    "total storage failure must retain typed text in the live React view");
  assert.equal(providerRequests.length, 4, "an unsent draft under total storage failure must not reach the provider");
  const totalBeforeRestart = path.join(scenarioRoot, "total-failure-before-host-restart.png");
  await page.screenshot({ path: totalBeforeRestart });

  await closeHost();
  await launchHost(applicationRoot);
  await installStorageFault("total");
  await openFirstSavedTask(prompts.totalBaseline);
  await page.getByText("A prior send could not be matched safely to saved task history. Check this conversation before resending.")
    .waitFor({ timeout: 15000 });
  await page.getByText("Saved drafts are unavailable in this WebView profile. Unsent text may not survive a restart.")
    .waitFor({ timeout: 15000 });
  assert.equal(await page.getByRole("region", { name: "Conversation" })
    .locator("article.message-user .message-text").getByText(prompts.totalBaseline, { exact: true }).count(), 1,
  "App Server history must remain inspectable while WebView storage is unavailable");
  assert.equal(await page.getByRole("textbox", { name: "Message NeoBabylon" }).inputValue(), "",
    "text that could not be persisted cannot be reconstructed after the real host restart");
  assert.equal(providerRequests.length, 4, "storage failure and host restart must issue no provider request");
  assert.deepEqual(journalUserPrompts(applicationRoot), [prompts.totalBaseline]);
  const totalAfterRestart = path.join(scenarioRoot, "total-failure-after-host-restart.png");
  await page.screenshot({ path: totalAfterRestart });
  await closeHost();
  return { warningAndInMemoryDraftBeforeRestart: true, storageUnavailableWarningAfterRestart: true,
    unresolvedPendingWarningAfterRestart: true, savedHistoryInspectable: true,
    unsavedDraftNotRecoverable: true, noAutomaticProviderRequest: true,
    screenshots: [totalBeforeRestart, totalAfterRestart] };
}

const fixturePort = await listen(fixtureServer);
capability.endpoint = `http://127.0.0.1:${fixturePort}/v1`;
const capabilitySuffix = `${Date.now()}_${process.pid}`;
const capabilityPath = path.join(releaseRoot, `MODEL_CAPABILITY_P2_04_${capabilitySuffix}.json`);

try {
  writeFileSync(capabilityPath, `${JSON.stringify(capability, null, 2)}\n`, { encoding: "utf8", flag: "wx" });
  capabilityHash = createHash("sha256").update(readFileSync(capabilityPath)).digest("hex");
  const writeFailure = await runWriteFailure();
  const clearFailure = await runClearFailure();
  const totalStorageFailure = await runTotalFailure();
  assert.deepEqual(providerRequests.map((entry) => entry.prompt), expectedRequests,
    "only the planned explicit baseline/accepted prompts may call the loopback provider");
  assert.deepEqual(fixtureErrors, [], "fixture must reject no planned requests and record no unexpected request");
  assert.deepEqual(pageIssues, [], "native WebView2 pages must not report uncaught errors");
  assert.equal(createHash("sha256").update(readFileSync(capabilityPath)).digest("hex"), capabilityHash,
    "the synthetic capability record must remain unchanged during the test");
  console.log(JSON.stringify({
    passed: true,
    scenario: "P2-04 native WPF/WebView2 draft write, clear, and total-storage failure recovery",
    source: "native WPF + WebView2 + runtime-locked Codex App Server + loopback-only deterministic Responses fixture",
    runtime: {
      version: runtimeLock.runtime.version,
      sourceRevision: runtimeLock.runtime.sourceRevision,
      sha256: runtimeHash,
      binaryPath: runtimePath,
    },
    syntheticProvider: { providerId: capability.providerId, modelIdentifier, endpoint: capability.endpoint,
      realModelWeights: false, liveInference: false },
    applicationRoots: ["write-failure", "clear-failure", "total-storage-failure"]
      .map((name) => path.join(runRoot, name, "application")),
    providerRequests: providerRequests.map(({ number, prompt }) => ({ number, prompt })),
    evidence: { writeFailure, clearFailure, totalStorageFailure },
    noUnsolicitedProviderRequests: true,
    pageIssues,
    screenshotsRoot: runRoot,
  }));
} catch (error) {
  console.error(JSON.stringify({ passed: false, providerRequests, fixtureErrors, pageIssues,
    applicationRoot: currentApplicationRoot, screenshotsRoot: runRoot }));
  throw error;
} finally {
  await closeHost().catch((error) => console.error(`P2-04 graceful host cleanup failed: ${error.message}`));
  await new Promise((resolve) => fixtureServer.close(resolve)).catch(() => {});
  if (existsSync(capabilityPath)) {
    const currentHash = createHash("sha256").update(readFileSync(capabilityPath)).digest("hex");
    if (!capabilityHash || currentHash !== capabilityHash) {
      throw new Error(`refusing to remove changed synthetic capability record: ${capabilityPath}`);
    }
    unlinkSync(capabilityPath);
  }
}
