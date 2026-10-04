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
  writeFileSync,
} from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { assertQaRunsParent, newQaRun } from "./qa-run-root.mjs";

const [hostPathArg, playwrightModulePathArg, qaParentArg] = process.argv.slice(2);
assert.ok(hostPathArg && playwrightModulePathArg && qaParentArg,
  "usage: node native-pending-send-recovery.mjs <NeoBabylon.Host.exe> <playwright-core-entry.mjs> <exact .local\\Lab\\Runs parent>");

const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const hostPath = path.resolve(hostPathArg);
const playwrightModulePath = path.resolve(playwrightModulePathArg);
const qaParent = path.resolve(qaParentArg);
const releaseRoot = path.join(sourceRoot, "docs", "release");
assertQaRunsParent(sourceRoot, qaParent);
const relativeWithin = (candidate, parent) => {
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
assert.equal(runtimeHash, runtimeLock.runtime.sha256.toLowerCase(), "pinned App Server binary does not match runtime-lock.json");

const { runRoot, applicationRoot } = newQaRun(qaParent, `P2-P03-native-${Date.now()}-${process.pid}`);
assert.ok(!existsSync(applicationRoot), "refusing to reuse an existing application root");
const screenshots = {
  beforeAcknowledgement: path.join(runRoot, "before-acknowledgement.png"),
  unacceptedAfterRendererReload: path.join(runRoot, "unaccepted-after-renderer-reload.png"),
  acceptedBeforeHostRestart: path.join(runRoot, "accepted-before-host-restart.png"),
  acceptedAfterHostRestart: path.join(runRoot, "accepted-after-host-restart.png"),
};
const suffix = `${Date.now()}_${process.pid}`;
const capabilityPath = path.join(releaseRoot, `MODEL_CAPABILITY_P2_03_${suffix}.json`);
assert.ok(!existsSync(capabilityPath), "refusing to overwrite an existing test capability record");

const modelIdentifier = "p2-03-loopback-recovery-model";
const baselinePrompt = "P2-03 baseline accepted before recovery checks.";
const unacceptedPrompt = "P2-03 remains retryable before App Server acknowledgement.";
const acceptedPrompt = "P2-03 accepted once before host restart.";
const replies = new Map([
  [baselinePrompt, "Deterministic baseline fixture response."],
  [unacceptedPrompt, "Deterministic deliberate-retry fixture response."],
  [acceptedPrompt, "This response must remain pending during host restart."],
]);
const providerRequests = [];
const fixtureErrors = [];
const openResponses = new Set();

const unknown = (evidenceSource) => ({ state: "Unknown", value: null, evidenceSource });
const capability = {
  providerId: "lmstudio",
  providerDisplayName: "LM Studio · P2-03 loopback fixture",
  providerServerVersion: "Unknown",
  endpoint: "pending-loopback-endpoint",
  modelIdentifier,
  modelVariant: "test-only local Responses fixture; no model weights",
  architecture: "Unknown",
  parameterCount: "Unknown",
  modelSizeBytes: "Unknown",
  quantization: unknown("P2-03 fixture; no model weights or quantization metadata"),
  contextWindowAdvertised: unknown("P2-03 fixture; provider metadata not queried"),
  contextWindowEffective: {
    state: "Known",
    value: 32768,
    evidenceSource: "P2-03 deterministic fixture value only; not observed model metadata",
  },
  reasoningControls: unknown("P2-03 fixture; no model loaded"),
  toolFunctionCalling: {
    state: "Known",
    value: "tool_use",
    evidenceSource: "P2-03 synthetic test-catalog prerequisite only; no real model capability or tool-call claim",
  },
  applyPatchToolType: unknown("P2-03 fixture; no patch format qualified"),
  structuredOutput: unknown("P2-03 fixture; provider metadata not queried"),
  agentMetadata: {
    state: "Known",
    value: "type=llm; inputModalities=text; vision=False",
    evidenceSource: "P2-03 synthetic test-catalog declaration only; no model weights or real model metadata",
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
    const requestNumber = providerRequests.length + 1;
    const expectedPrompt = [baselinePrompt, unacceptedPrompt, acceptedPrompt][requestNumber - 1];
    const serializedInput = JSON.stringify(payload.input ?? []);
    providerRequests.push({ number: requestNumber, model: payload.model ?? null,
      expectedPrompt: expectedPrompt ?? null, promptMatched: Boolean(expectedPrompt && serializedInput.includes(expectedPrompt)) });
    if (payload.model !== modelIdentifier) {
      fixtureErrors.push(`expected model ${modelIdentifier}, received ${payload.model ?? "<missing>"}`);
      response.writeHead(409).end();
      return;
    }
    if (!expectedPrompt || !serializedInput.includes(expectedPrompt)) {
      fixtureErrors.push(`unexpected or duplicate provider request #${requestNumber}`);
      response.writeHead(409).end();
      return;
    }
    if (expectedPrompt === acceptedPrompt) {
      response.writeHead(200, { "content-type": "text/event-stream", "cache-control": "no-cache" });
      response.flushHeaders();
      response.write(": P2-03 fixture holds this accepted turn until the host is restarted\n\n");
      openResponses.add(response);
      response.once("close", () => openResponses.delete(response));
      return;
    }
    response.writeHead(200, { "content-type": "text/event-stream", "cache-control": "no-cache" });
    response.end(assistantResponse(`p2-03-response-${requestNumber}`, `p2-03-item-${requestNumber}`,
      replies.get(expectedPrompt)));
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

function journalUserPrompts() {
  const sessionRoot = path.join(applicationRoot, "Data", "CodexHome", "sessions");
  const prompts = [];
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
  page.on("console", (message) => {
    if (message.type() === "error" || message.type() === "warning") pageIssues.push(`${message.type()}: ${message.text()}`);
  });
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

async function sendPrompt(prompt, reply) {
  const conversation = page.getByRole("region", { name: "Conversation" });
  await page.getByRole("textbox", { name: "Message NeoBabylon" }).fill(prompt);
  await page.getByRole("button", { name: "Send message" }).click();
  await conversation.locator("article.message-user .message-text").getByText(prompt, { exact: true })
    .waitFor({ timeout: 20000 });
  if (reply) {
    try {
      await conversation.locator("article.message-assistant .message-text").getByText(reply, { exact: true })
        .waitFor({ timeout: 30000 });
    } catch (error) {
      const state = await page.evaluate(() => ({
        turnState: document.querySelector("section.conversation")?.getAttribute("data-turn-state"),
        model: document.querySelector("button.model-select .model-short-name")?.textContent,
        alerts: [...document.querySelectorAll('[role="alert"], [role="status"]')].map((element) => element.textContent?.trim()),
        activeTask: localStorage.getItem("neobabylon.active-task:v1"),
        pending: Object.fromEntries(Object.entries(localStorage).filter(([key]) => key.startsWith("neobabylon.pending-submission:v1:"))),
        composer: document.querySelector('textarea[aria-label="Message NeoBabylon"]')?.value,
      }));
      const diagnosticScreenshot = path.join(runRoot, `failed-send-${Date.now()}.png`);
      await page.screenshot({ path: diagnosticScreenshot }).catch(() => {});
      console.error(`P2_03_NATIVE_SEND_DIAGNOSTIC ${JSON.stringify({ prompt, providerRequests, fixtureErrors, state,
        journalUserPrompts: journalUserPrompts(), screenshot: diagnosticScreenshot })}`);
      throw error;
    }
    await page.waitForFunction(() => {
      const state = document.querySelector("section.conversation")?.getAttribute("data-turn-state");
      return state !== "starting" && state !== "running";
    }, null, { timeout: 30000 });
  }
}

function messageCount(prompt) {
  return page.locator(".transcript article.message-user .message-text").getByText(prompt, { exact: true }).count();
}

async function visibleRuntimeEvidence() {
  await page.getByRole("button", { name: "Open diagnostics" }).click();
  const dialog = page.getByRole("dialog", { name: "Runtime diagnostics" });
  await dialog.waitFor();
  const evidence = await dialog.innerText();
  assert.ok(evidence.includes(modelIdentifier), "synthetic selected capability is not visible in runtime details");
  assert.ok(evidence.includes(applicationRoot), "isolated application root is not visible in runtime details");
  assert.match(evidence, /Ordinary Codex root used\s+false/i, "runtime details do not confirm ordinary Codex root isolation");
  await dialog.getByRole("button", { name: "Done" }).click();
  return evidence;
}

const fixturePort = await listen(fixtureServer);
capability.endpoint = `http://127.0.0.1:${fixturePort}/v1`;

try {
  writeFileSync(capabilityPath, `${JSON.stringify(capability, null, 2)}\n`, { encoding: "utf8", flag: "wx" });
  const capabilityBytes = readFileSync(capabilityPath, "utf8");
  await launchHost();
  const runtimeEvidence = await visibleRuntimeEvidence();
  assert.ok(runtimeEvidence.includes(String(runtimeLock.runtime.version)), "pinned App Server version is not visible in runtime details");

  await sendPrompt(baselinePrompt, replies.get(baselinePrompt));
  assert.equal(providerRequests.length, 1, "baseline turn should issue exactly one local fixture request");
  assert.equal((await messageCount(baselinePrompt)), 1);
  const activePointer = await page.evaluate(() => JSON.parse(localStorage.getItem("neobabylon.active-task:v1")));
  assert.ok(activePointer?.threadId, "the baseline turn did not persist its exact active task identity");

  await page.evaluate((targetPrompt) => {
    const bridge = window.chrome?.webview;
    if (!bridge || typeof bridge.postMessage !== "function") throw new Error("native WebView2 bridge is unavailable");
    const originalPostMessage = bridge.postMessage.bind(bridge);
    bridge.postMessage = (request) => {
      if (request?.operation === "startTurn" && request.text === targetPrompt) {
        window.__neoBabylonP203InterceptedBeforeHostHandoff = true;
        return;
      }
      originalPostMessage(request);
    };
  }, unacceptedPrompt);
  await page.getByRole("textbox", { name: "Message NeoBabylon" }).fill(unacceptedPrompt);
  await page.getByRole("button", { name: "Send message" }).click();
  await page.waitForFunction((prompt) => {
    if (window.__neoBabylonP203InterceptedBeforeHostHandoff !== true) return false;
    const key = Object.keys(localStorage).find((entry) => entry.startsWith("neobabylon.pending-submission:v1:"));
    if (!key) return false;
    const marker = JSON.parse(localStorage.getItem(key));
    return marker && localStorage.getItem(marker.draftScope) === prompt;
  }, unacceptedPrompt, { timeout: 15000 });
  await page.screenshot({ path: screenshots.beforeAcknowledgement });
  const beforeReload = await page.evaluate(() => ({
    pendingSubmissionKeys: Object.keys(localStorage).filter((key) => key.startsWith("neobabylon.pending-submission:v1:")),
    activeTask: JSON.parse(localStorage.getItem("neobabylon.active-task:v1")),
    drafts: Object.fromEntries(Object.entries(localStorage).filter(([key]) => key.startsWith("neobabylon.draft:v1:"))),
  }));
  assert.equal(beforeReload.activeTask.threadId, activePointer.threadId);
  assert.equal(beforeReload.pendingSubmissionKeys.length, 1);
  assert.equal(providerRequests.length, 1, "a send intercepted before native handoff must not reach the provider");

  await page.reload();
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  await page.getByRole("region", { name: "Conversation" }).getByText(baselinePrompt, { exact: true })
    .waitFor({ timeout: 30000 });
  await page.waitForFunction((prompt) => {
    const composer = document.querySelector('textarea[aria-label="Message NeoBabylon"]');
    return composer && !composer.disabled && composer.value === prompt
      && Object.keys(localStorage).every((key) => !key.startsWith("neobabylon.pending-submission:v1:"));
  }, unacceptedPrompt, { timeout: 30000 });
  assert.equal(await messageCount(baselinePrompt), 1, "renderer reload must restore the prior accepted message once");
  assert.equal(await messageCount(unacceptedPrompt), 0, "the unaccepted optimistic message must not become saved history");
  assert.equal(providerRequests.length, 1, "renderer reload must not replay an unaccepted send");
  assert.equal(journalUserPrompts().filter((prompt) => prompt === unacceptedPrompt).length, 0,
    "the unaccepted prompt must not appear in the App Server session journal");
  await page.screenshot({ path: screenshots.unacceptedAfterRendererReload });

  await page.evaluate(() => {
    const bridge = window.chrome?.webview;
    if (!bridge || typeof bridge.postMessage !== "function") throw new Error("native WebView2 bridge is unavailable");
    const originalPostMessage = bridge.postMessage.bind(bridge);
    window.__neoBabylonP203DelayedThreadList = { delayed: false, released: false };
    bridge.postMessage = (request) => {
      const latch = window.__neoBabylonP203DelayedThreadList;
      if (request?.operation === "listThreads" && latch && !latch.delayed) {
        latch.delayed = true;
        window.setTimeout(() => {
          originalPostMessage(request);
          latch.released = true;
        }, 2000);
        return;
      }
      originalPostMessage(request);
    };
  });
  await sendPrompt(unacceptedPrompt, replies.get(unacceptedPrompt));
  assert.equal(await messageCount(unacceptedPrompt), 1, "a deliberate retry must create exactly one accepted history item");
  assert.equal(providerRequests.length, 2, "a deliberate retry must issue exactly one additional provider request");
  await waitFor(() => journalUserPrompts().filter((prompt) => prompt === unacceptedPrompt).length === 1,
    "deliberate retry was not recorded exactly once in the isolated session journal");
  await page.waitForFunction(() => window.__neoBabylonP203DelayedThreadList?.delayed === true, null, { timeout: 10000 });
  assert.equal(await page.getByRole("textbox", { name: "Message NeoBabylon" }).isDisabled(), true,
    "the composer must stay disabled while the App Server history refresh owns the operation guard");
  await page.screenshot({ path: path.join(runRoot, "composer-disabled-during-history-refresh.png") });
  await page.waitForFunction(() => window.__neoBabylonP203DelayedThreadList?.released === true
    && document.querySelector('textarea[aria-label="Message NeoBabylon"]')?.disabled === false,
  null, { timeout: 15000 });

  await page.getByRole("textbox", { name: "Message NeoBabylon" }).fill(acceptedPrompt);
  await page.getByRole("button", { name: "Send message" }).click();
  try {
    await waitFor(() => providerRequests.length === 3, "accepted-boundary request did not reach the loopback Responses fixture");
  } catch (error) {
    const state = await page.evaluate(() => ({
      turnState: document.querySelector("section.conversation")?.getAttribute("data-turn-state"),
      alerts: [...document.querySelectorAll('[role="alert"], [role="status"]')].map((element) => element.textContent?.trim()),
      activeTask: localStorage.getItem("neobabylon.active-task:v1"),
      pending: Object.fromEntries(Object.entries(localStorage).filter(([key]) => key.startsWith("neobabylon.pending-submission:v1:"))),
      composer: document.querySelector('textarea[aria-label="Message NeoBabylon"]')?.value,
      transcript: document.querySelector('[aria-label="Conversation"]')?.innerText,
    }));
    const diagnosticScreenshot = path.join(runRoot, `failed-accepted-boundary-${Date.now()}.png`);
    await page.screenshot({ path: diagnosticScreenshot }).catch(() => {});
    console.error(`P2_03_NATIVE_ACCEPTED_DIAGNOSTIC ${JSON.stringify({ providerRequests, fixtureErrors, state,
      journalUserPrompts: journalUserPrompts(), screenshot: diagnosticScreenshot })}`);
    throw error;
  }
  await page.waitForFunction(() => {
    const pending = Object.keys(localStorage).some((key) => key.startsWith("neobabylon.pending-submission:v1:"));
    const threadId = JSON.parse(localStorage.getItem("neobabylon.active-task:v1") ?? "null")?.threadId;
    const composer = document.querySelector('textarea[aria-label="Message NeoBabylon"]');
    const draftKey = Object.keys(localStorage).find((key) => key.startsWith(`neobabylon.draft:v1:`)
      && key.endsWith(encodeURIComponent(`thread:${threadId}`)));
    return !pending && composer?.value === "" && (!draftKey || localStorage.getItem(draftKey) === "");
  }, null, { timeout: 30000 });
  assert.equal(await messageCount(acceptedPrompt), 1, "turn/started must retain the accepted user message once before provider completion");
  assert.equal(providerRequests.length, 3, "the accepted boundary should produce one request, held before completion");
  await waitFor(() => journalUserPrompts().filter((prompt) => prompt === acceptedPrompt).length === 1,
    "the acknowledged user message was not durably journaled before host restart");
  await page.screenshot({ path: screenshots.acceptedBeforeHostRestart });

  await closeHost();
  await launchHost();
  await page.getByRole("region", { name: "Conversation" }).getByText(acceptedPrompt, { exact: true })
    .waitFor({ timeout: 45000 });
  await page.waitForFunction(() => {
    const composer = document.querySelector('textarea[aria-label="Message NeoBabylon"]');
    const pending = Object.keys(localStorage).some((key) => key.startsWith("neobabylon.pending-submission:v1:"));
    return composer && composer.value === "" && !pending;
  }, null, { timeout: 30000 });
  assert.deepEqual(await page.evaluate(() => JSON.parse(localStorage.getItem("neobabylon.active-task:v1"))),
    activePointer, "host restart must restore the same project/provider/model/thread identity");
  for (const prompt of [baselinePrompt, unacceptedPrompt, acceptedPrompt]) {
    assert.equal(await messageCount(prompt), 1, `host restart must show '${prompt}' exactly once`);
    assert.equal(journalUserPrompts().filter((entry) => entry === prompt).length, 1,
      `isolated App Server journal must contain '${prompt}' exactly once`);
  }
  assert.equal(providerRequests.length, 3, "host restart must not replay the accepted pending turn");
  const outcomeWarning = await page.locator('[role="alert"]').allInnerTexts();
  assert.ok(outcomeWarning.some((text) => /not replay/i.test(text)),
    `host restart must preserve a visible no-replay outcome warning; alerts=${JSON.stringify(outcomeWarning)}`);
  await page.screenshot({ path: screenshots.acceptedAfterHostRestart });
  assert.deepEqual(pageIssues, [], "native pending-send recovery should not produce WebView errors");
  assert.deepEqual(fixtureErrors, [], "the loopback Responses fixture should receive only the exact selected model and planned requests");
  assert.equal(readFileSync(capabilityPath, "utf8"), capabilityBytes, "test capability identity must remain unchanged during the run");

  console.log(JSON.stringify({
    passed: true,
    scenario: "P2-03 native WPF/WebView2 pending-send reconciliation before and after turn/started",
    source: "native WPF + WebView2 + runtime-locked Codex App Server + loopback-only deterministic Responses fixture",
    runtime: {
      version: runtimeLock.runtime.version,
      sourceRevision: runtimeLock.runtime.sourceRevision,
      sha256: runtimeHash,
      binaryPath: runtimePath,
    },
    syntheticProvider: { providerId: capability.providerId, modelIdentifier, endpoint: capability.endpoint,
      realModelWeights: false, liveInference: false },
    evidence: {
      beforeAcknowledgement: { nativeHandoffIntercepted: true, pendingMarkerPersisted: true,
        providerRequests: 1, draftPreservedAfterRendererReload: true, autoReplay: false,
        sessionJournalUserMessageCount: 0 },
      explicitRetry: { prompt: unacceptedPrompt, providerRequests: 2,
        journalUserMessageCount: 1, visibleHistoryCount: 1 },
      afterAcknowledgement: { turnStartedAcknowledgedByDraftMarkerClear: true,
        providerRequestHeldOpen: true, acceptedPromptInJournalBeforeRestart: 1,
        acceptedPromptAfterHostRestart: 1, draftCleared: true, autoReplay: false,
        visibleNoReplayWarning: true },
      finalProviderRequestCount: providerRequests.length,
      journalUserPrompts: journalUserPrompts(),
    },
    applicationRoot,
    dataRoot: path.join(applicationRoot, "Data"),
    sourceRoot,
    ordinaryCodexDataRootUsed: false,
    screenshots,
    pageIssues,
  }));
} finally {
  if (browser) await browser.close().catch(() => {});
  if (host && host.exitCode === null) {
    try {
      requestHostClose(host.pid);
      await waitForExit(host, 20000);
    } catch (error) {
      host.kill();
      await waitForExit(host, 5000).catch(() => {});
      console.error(`QA-owned WPF process required targeted cleanup after graceful-close attempt: ${error.message}`);
    }
  }
  for (const response of openResponses) response.destroy();
  await new Promise((resolve) => fixtureServer.close(() => resolve()));
  if (relativeWithin(capabilityPath, releaseRoot)
    && path.basename(capabilityPath).startsWith("MODEL_CAPABILITY_P2_03_")
    && existsSync(capabilityPath)) {
    const currentHash = createHash("sha256").update(readFileSync(capabilityPath)).digest("hex");
    const createdHash = createHash("sha256").update(JSON.stringify(capability, null, 2) + "\n").digest("hex");
    if (currentHash === createdHash) {
      const { unlinkSync } = await import("node:fs");
      unlinkSync(capabilityPath);
    } else {
      console.error(`Refused to remove changed test capability record: ${capabilityPath}`);
    }
  }
}
