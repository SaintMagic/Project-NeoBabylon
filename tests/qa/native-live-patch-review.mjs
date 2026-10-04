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
  "usage: node native-live-patch-review.mjs <NeoBabylon.Host.exe> <playwright-core-entry.mjs> <exact .local\\Lab\\Runs parent>");

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
const runtime = runtimeLock.runtime;
const runtimeBinaryPath = path.resolve(sourceRoot, runtime.appServerBinaryRelativePath);
assert.ok(existsSync(runtimeBinaryPath), `pinned App Server binary is missing: ${runtimeBinaryPath}`);
const runtimeSha256 = createHash("sha256").update(readFileSync(runtimeBinaryPath)).digest("hex");
assert.equal(runtimeSha256, runtime.sha256.toLowerCase(), "pinned App Server binary does not match runtime-lock.json");

const { runRoot, applicationRoot } = newQaRun(qaParent, `P2-07-live-native-${Date.now()}-${process.pid}`);
const dataRoot = path.join(applicationRoot, "Data");
const fixtureWorkspace = path.join(dataRoot, "Workspace");
mkdirSync(fixtureWorkspace, { recursive: true });
const trackedFile = path.join(fixtureWorkspace, "tracked.txt");
writeFileSync(trackedFile, "before\n", "utf8");

const suffix = `${Date.now()}_${process.pid}`;
const capabilityPath = path.join(releaseRoot, `MODEL_CAPABILITY_P2_07_NATIVE_${suffix}.json`);
assert.ok(!existsSync(capabilityPath), "refusing to overwrite an existing test capability record");
const modelIdentifier = "p2-07-native-loopback-patch-fixture";
const prompt = "P2-07: use only apply_patch to change tracked.txt from before to after; do not run a shell command or touch another path.";
const assistantReply = "The isolated fixture patch completed.";
const patchText = "*** Begin Patch\n*** Update File: tracked.txt\n@@\n-before\n+after\n*** End Patch";
const unknown = (evidenceSource) => ({ state: "Unknown", value: null, evidenceSource });
const capability = {
  providerId: "lmstudio",
  providerDisplayName: "LM Studio · P2-07 deterministic fixture",
  providerServerVersion: "Unknown",
  endpoint: "pending-loopback-endpoint",
  modelIdentifier,
  modelVariant: "test-only loopback Responses fixture; no model weights",
  architecture: "Unknown",
  parameterCount: "Unknown",
  modelSizeBytes: "Unknown",
  quantization: unknown("P2-07 fixture; no model weights or quantization metadata"),
  contextWindowAdvertised: unknown("P2-07 fixture; provider metadata not queried"),
  contextWindowEffective: {
    state: "Known",
    value: 32768,
    evidenceSource: "P2-07 deterministic fixture value only; not observed model metadata",
  },
  reasoningControls: unknown("P2-07 fixture; no model loaded"),
  toolFunctionCalling: {
    state: "Known",
    value: "tool_use",
    evidenceSource: "P2-07 synthetic test-catalog declaration only; no real model capability claim",
  },
  applyPatchToolType: {
    state: "Known",
    value: "freeform",
    evidenceSource: "P2-07 deterministic test fixture wire format; not provider metadata",
  },
  structuredOutput: unknown("P2-07 fixture; provider metadata not queried"),
  agentMetadata: {
    state: "Known",
    value: "type=llm; inputModalities=text; vision=False",
    evidenceSource: "P2-07 synthetic test-catalog declaration only; no model weights or real model metadata",
  },
};
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
      usage: {
        input_tokens: 0,
        input_tokens_details: null,
        output_tokens: 0,
        output_tokens_details: null,
        total_tokens: 0,
      },
    },
  };
}

function patchResponse() {
  return [
    { type: "response.created", response: { id: "p2-07-patch-response" } },
    {
      type: "response.output_item.done",
      item: { type: "custom_tool_call", call_id: "p2-07-patch-call", name: "apply_patch", input: patchText },
    },
    responseUsage("p2-07-patch-response"),
  ].map(sseEvent).join("");
}

function finalResponse() {
  return [
    { type: "response.created", response: { id: "p2-07-final-response" } },
    {
      type: "response.output_item.done",
      item: {
        type: "message",
        role: "assistant",
        id: "p2-07-final-message",
        content: [{ type: "output_text", text: assistantReply }],
      },
    },
    responseUsage("p2-07-final-response"),
  ].map(sseEvent).join("");
}

const providerRequests = [];
const fixtureErrors = [];
const firstRequestReceived = deferred();
const secondRequestReceived = deferred();
const releaseSecondResponse = deferred();
const fixtureServer = createHttpServer((request, response) => {
  if (request.method !== "POST" || request.url !== "/v1/responses") {
    fixtureErrors.push(`unexpected fixture request ${request.method} ${request.url}`);
    response.writeHead(404).end();
    return;
  }

  let body = "";
  request.setEncoding("utf8");
  request.on("data", (chunk) => { body += chunk; });
  request.on("end", async () => {
    let payload;
    try { payload = JSON.parse(body); }
    catch {
      fixtureErrors.push("Responses request body was not valid JSON");
      response.writeHead(400).end();
      return;
    }

    const number = providerRequests.length + 1;
    providerRequests.push(payload);
    if (number === 1) firstRequestReceived.resolve(payload);
    if (payload.model !== modelIdentifier) {
      fixtureErrors.push(`request ${number} used unexpected model ${payload.model ?? "<missing>"}`);
      response.writeHead(409).end();
      return;
    }
    if (number === 1) {
      response.writeHead(200, { "content-type": "text/event-stream", "cache-control": "no-cache" });
      response.end(patchResponse());
      return;
    }
    if (number === 2) {
      secondRequestReceived.resolve(payload);
      await releaseSecondResponse.promise;
      if (!response.destroyed) {
        response.writeHead(200, { "content-type": "text/event-stream", "cache-control": "no-cache" });
        response.end(finalResponse());
      }
      return;
    }
    fixtureErrors.push(`unexpected replay/additional provider request ${number}`);
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

function withTimeout(promise, timeoutMs, message) {
  let timer;
  return Promise.race([
    promise,
    new Promise((_, reject) => { timer = setTimeout(() => reject(new Error(message)), timeoutMs); }),
  ]).finally(() => clearTimeout(timer));
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
    } catch { /* WebView2 is still starting. */ }
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
    } catch { /* The native WebView target is not ready yet. */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`native NeoBabylon page was not found; CDP targets=${JSON.stringify(targets)}`);
}

const { chromium } = await import(pathToFileURL(playwrightModulePath).href);
let browser;
let host;
let page;
const pageIssues = [];

async function waitForReady() {
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  assert.equal(await page.title(), "NeoBabylon");
  assert.equal(await page.locator("vite-error-overlay").count(), 0);
  await page.getByRole("textbox", { name: "Message NeoBabylon" }).waitFor();
  await page.waitForFunction(() => {
    const composer = document.querySelector('textarea[aria-label="Message NeoBabylon"]');
    return composer && !composer.disabled && !document.querySelector(".chat-list-error");
  }, null, { timeout: 45000 });
}

function installBridgeCapture() {
  window.__p2_07BridgeRequests = [];
  window.__p2_07BridgeResponses = [];
  window.__p2_07AppServerEvents = [];
  const webview = window.chrome.webview;
  const postMessage = webview.postMessage.bind(webview);
  try {
    webview.postMessage = (message) => {
      try {
        const parsed = typeof message === "string" ? JSON.parse(message) : message;
        window.__p2_07BridgeRequests.push({ operation: parsed?.operation, requestId: parsed?.requestId });
      } catch { /* The native bridge still receives the original message. */ }
      return postMessage(message);
    };
  } catch { /* Incoming host responses remain available if WebView2 methods cannot be shadowed. */ }
  webview.addEventListener("message", ({ data }) => {
    window.__p2_07BridgeResponses.push({
      requestId: data?.requestId,
      ok: data?.ok,
      stream: data?.stream,
      method: data?.method,
      error: data?.error?.message,
    });
    if (data?.attributedTo === "Codex App Server" && typeof data.method === "string") {
      window.__p2_07AppServerEvents.push({ attributedTo: data.attributedTo, method: data.method, params: data.params });
    }
  });
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
  page = await waitForNativePage(browser, debugPort, host);
  page.on("pageerror", (error) => pageIssues.push(error.message));
  page.on("console", (message) => {
    if (message.type() === "error") pageIssues.push(message.text());
  });
  await page.addInitScript(installBridgeCapture);
  await page.evaluate(installBridgeCapture);
  await waitForReady();
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

async function openTaskAndSavedReview() {
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  const task = page.locator("button.saved-chat").filter({ hasText: prompt });
  await task.waitFor({ timeout: 30000 });
  if (await task.getAttribute("aria-current") !== "page") await task.click();
  await page.getByRole("region", { name: "Conversation" }).getByText(prompt, { exact: true }).waitFor({ timeout: 20000 });
  const reviewButton = page.getByRole("button", { name: "Review changes" });
  await reviewButton.waitFor({ timeout: 20000 });
  await reviewButton.click();
  const dialog = page.getByRole("dialog", { name: "Review changes" });
  await dialog.waitFor();
  const text = await dialog.innerText();
  assert.match(text, /patch item diffs restored from App Server-owned task history/i);
  assert.match(text, /tracked\.txt/);
  assert.match(text, /-before/);
  assert.match(text, /\+after/);
  assert.match(text, /not an approval/i);
  assert.equal(await dialog.getByRole("button", { name: /approve|allow|apply/i }).count(), 0);
  return { dialog, text };
}

let capabilityWritten = false;
let liveDiffScreenshot;
let reloadScreenshot;
let restartScreenshot;
let runError = null;
const resultPath = path.join(runRoot, "result.json");
try {
  const fixturePort = await listen(fixtureServer);
  capability.endpoint = `http://127.0.0.1:${fixturePort}/v1`;
  const finalCapabilityBytes = `${JSON.stringify(capability, null, 2)}\n`;
  writeFileSync(capabilityPath, finalCapabilityBytes, { encoding: "utf8", flag: "wx" });
  capabilityWritten = true;
  capabilityFileHash = createHash("sha256").update(finalCapabilityBytes).digest("hex");

  await launchHost();
  await page.getByRole("button", { name: "See runtime details" }).click();
  const diagnostics = page.getByRole("dialog", { name: "Runtime diagnostics" });
  await diagnostics.waitFor();
  const diagnosticsText = await diagnostics.innerText();
  assert.ok(diagnosticsText.includes(applicationRoot), "native runtime diagnostics did not show the isolated application root");
  assert.ok(diagnosticsText.includes(dataRoot), "native runtime diagnostics did not show its Data root");
  assert.ok(diagnosticsText.includes("Ordinary Codex root used") && /Ordinary Codex root used\s+false/i.test(diagnosticsText),
    "native runtime diagnostics did not confirm ordinary Codex state is unused");
  await diagnostics.getByRole("button", { name: "Done" }).click();

  await page.getByRole("textbox", { name: "Message NeoBabylon" }).fill(prompt);
  await page.getByRole("button", { name: "Send message" }).click();
  await page.getByRole("region", { name: "Conversation" }).locator("article.message-user .message-text")
    .getByText(prompt, { exact: true }).waitFor({ timeout: 20000 });

  await withTimeout(
    firstRequestReceived.promise,
    45000,
    "the deterministic patch round trip did not reach its first Responses request",
  );
  const secondPayload = await withTimeout(
    secondRequestReceived.promise,
    45000,
    "the deterministic patch round trip did not reach its second Responses request",
  );
  assert.equal(providerRequests.length, 2, "one patch response and one post-tool response are required");
  assert.match(JSON.stringify(secondPayload.input), /custom_tool_call_output/,
    "Codex did not send the apply_patch result back through the provider round trip");
  assert.equal(secondPayload.model, modelIdentifier, "the exact test model identifier changed during the round trip");
  assert.deepEqual(fixtureErrors, []);

  const liveReviewButton = page.getByRole("button", { name: "Review changes" });
  await liveReviewButton.waitFor({ timeout: 30000 });
  assert.equal(await page.locator("section.conversation").getAttribute("data-turn-state"), "running",
    "the live diff must be reviewable before the provider finishes the active turn");
  const observedEvents = await page.evaluate(() => window.__p2_07AppServerEvents);
  const started = observedEvents.find((event) => event.method === "turn/started"
    && event.params?.threadId && event.params?.turn?.id);
  assert.ok(started, "native WPF did not receive an attributed App Server turn/started event");
  const liveDiffEvent = observedEvents.find((event) => event.method === "turn/diff/updated"
    && event.attributedTo === "Codex App Server"
    && event.params?.threadId === started.params.threadId
    && event.params?.turnId === started.params.turn.id
    && typeof event.params?.diff === "string");
  assert.ok(liveDiffEvent, "native WPF did not receive the exact thread/turn-attributed live App Server diff");
  assert.match(liveDiffEvent.params.diff, /tracked\.txt/);
  assert.match(liveDiffEvent.params.diff, /-before/);
  assert.match(liveDiffEvent.params.diff, /\+after/);
  assert.equal(readFileSync(trackedFile, "utf8"), "after\n", "the isolated apply_patch tool did not change only the fixture file");

  await liveReviewButton.focus();
  assert.equal(await liveReviewButton.evaluate((element) => document.activeElement === element), true,
    "the native Review changes action should be keyboard-focusable");
  await page.keyboard.press("Enter");
  const liveDialog = page.getByRole("dialog", { name: "Review changes" });
  await liveDialog.waitFor();
  const liveText = await liveDialog.innerText();
  assert.match(liveText, /latest live App Server turn diff/i);
  assert.match(liveText, /tracked\.txt/);
  assert.match(liveText, /-before/);
  assert.match(liveText, /\+after/);
  assert.match(liveText, /not an approval/i);
  assert.equal(await liveDialog.getByRole("button", { name: /approve|allow|apply/i }).count(), 0);
  assert.equal(await page.locator("main").evaluate((element) => element.inert), true,
    "the native review dialog should make background task content inert");
  const reviewClose = liveDialog.getByRole("button", { name: "Close review" });
  const reviewContent = liveDialog.getByRole("region", { name: "Change review content" });
  await page.waitForFunction(() => document.activeElement?.getAttribute("aria-label") === "Close review");
  assert.equal(await reviewClose.evaluate((element) => document.activeElement === element), true,
    "opening native review should move keyboard focus to its close control");
  await page.keyboard.press("Tab");
  assert.equal(await reviewContent.evaluate((element) => document.activeElement === element), true,
    "native review Tab should reach the named keyboard stop for scrollable diff content");
  await page.keyboard.press("Tab");
  const reviewDone = liveDialog.getByRole("button", { name: "Done" });
  assert.equal(await reviewDone.evaluate((element) => document.activeElement === element), true,
    "native review Tab should move from diff content to Done");
  await page.keyboard.press("Tab");
  assert.equal(await reviewClose.evaluate((element) => document.activeElement === element), true,
    "native review Tab should wrap from the final control to the close control");
  await page.keyboard.press("Shift+Tab");
  assert.equal(await reviewDone.evaluate((element) => document.activeElement === element), true,
    "native review Shift+Tab should wrap from the close control to Done");
  liveDiffScreenshot = path.join(runRoot, "native-live-diff.png");
  await page.screenshot({ path: liveDiffScreenshot });
  await page.keyboard.press("Escape");
  await liveDialog.waitFor({ state: "detached" });
  assert.equal(await liveReviewButton.evaluate((element) => document.activeElement === element), true,
    "closing native review with Escape should restore focus to its opener");

  releaseSecondResponse.resolve();
  await page.getByRole("region", { name: "Conversation" }).locator("article.message-assistant .message-text")
    .getByText(assistantReply, { exact: true }).waitFor({ timeout: 30000 });
  await page.waitForFunction(() => document.querySelector("section.conversation")?.getAttribute("data-turn-state") === "completed", null, { timeout: 30000 });
  const activePointer = await page.evaluate(() => {
    const value = localStorage.getItem("neobabylon.active-task:v1");
    return value === null ? null : JSON.parse(value);
  });
  assert.equal(activePointer?.threadId, started.params.threadId, "the live task pointer did not match the attributed diff thread");

  await page.reload();
  await waitForReady();
  const afterReload = await openTaskAndSavedReview();
  reloadScreenshot = path.join(runRoot, "native-saved-review-after-renderer-reload.png");
  await page.screenshot({ path: reloadScreenshot });
  await afterReload.dialog.getByRole("button", { name: "Close review" }).click();
  assert.equal(providerRequests.length, 2, "renderer reload or saved-review read unexpectedly called the provider");

  await closeHost();
  await launchHost();
  const afterRestart = await openTaskAndSavedReview();
  restartScreenshot = path.join(runRoot, "native-saved-review-after-host-restart.png");
  await page.screenshot({ path: restartScreenshot });
  await afterRestart.dialog.getByRole("button", { name: "Close review" }).click();
  assert.equal(providerRequests.length, 2, "host restart or saved-review read unexpectedly called the provider");
  assert.deepEqual(fixtureErrors, []);
  assert.deepEqual(pageIssues, [], "the native page reported errors during the live or saved review journey");
  assert.equal(readFileSync(trackedFile, "utf8"), "after\n");
  await closeHost();

  writeFileSync(resultPath, `${JSON.stringify({
    passed: true,
    source: "native WPF/WebView2 + pinned Codex App Server + deterministic loopback Responses fixture",
    runtimeVersion: runtime.version,
    runtimeSourceRevision: runtime.sourceRevision,
    runtimeSha256,
    modelIdentifier,
    endpoint: capability.endpoint,
    providerRequests: providerRequests.length,
    toolResultRoundTrip: true,
    attributedThreadId: started.params.threadId,
    attributedTurnId: started.params.turn.id,
    liveReview: true,
    rendererReloadSavedReview: true,
    hostRestartSavedReview: true,
    applicationRoot,
    dataRoot,
    ordinaryCodexRootUsed: false,
    capabilityRecordPath: capabilityPath,
    capabilityRecordSha256: capabilityFileHash,
    screenshots: [liveDiffScreenshot, reloadScreenshot, restartScreenshot],
    pageIssues,
  }, null, 2)}\n`, "utf8");
  console.log(JSON.stringify({ passed: true, runtimeVersion: runtime.version, runtimeSourceRevision: runtime.sourceRevision,
    runtimeSha256, modelIdentifier, endpoint: capability.endpoint, providerRequests: providerRequests.length,
    toolResultRoundTrip: true, liveReview: true, rendererReloadSavedReview: true, hostRestartSavedReview: true,
    applicationRoot, ordinaryCodexRootUsed: false, screenshots: [liveDiffScreenshot, reloadScreenshot, restartScreenshot] }));
} catch (error) {
  runError = error;
  if (page) {
    const failedScreenshot = path.join(runRoot, `failed-${Date.now()}.png`);
    await page.screenshot({ path: failedScreenshot }).catch(() => {});
    const uiDiagnostics = await page.evaluate(() => ({
      title: document.title,
      turnState: document.querySelector("section.conversation")?.getAttribute("data-turn-state"),
      alerts: [...document.querySelectorAll('[role="alert"], [role="status"]')].map((element) => element.textContent?.trim()),
      composerDisabled: document.querySelector('textarea[aria-label="Message NeoBabylon"]')?.disabled,
      bridgeRequests: window.__p2_07BridgeRequests ?? [],
      bridgeResponses: window.__p2_07BridgeResponses ?? [],
      appServerEvents: window.__p2_07AppServerEvents ?? [],
      body: document.body.innerText.slice(0, 2400),
    })).catch((diagnosticError) => ({ diagnosticError: String(diagnosticError) }));
    writeFileSync(resultPath, `${JSON.stringify({
      passed: false,
      error: error instanceof Error ? `${error.name}: ${error.message}` : String(error),
      providerRequests: providerRequests.length,
      fixtureErrors,
      uiDiagnostics,
      applicationRoot,
      screenshot: failedScreenshot,
    }, null, 2)}\n`, "utf8");
    console.error(`P2_07_NATIVE_PATCH_DIAGNOSTIC ${JSON.stringify({ error: error instanceof Error ? error.message : String(error), providerRequests: providerRequests.length, fixtureErrors, uiDiagnostics, screenshot: failedScreenshot })}`);
  }
  throw error;
} finally {
  releaseSecondResponse.resolve();
  if (browser) await browser.close().catch(() => {});
  browser = null;
  if (host && host.exitCode === null) {
    try {
      requestHostClose(host.pid);
      const result = await waitForExit(host, 20000);
      assert.equal(result.code, 0, `WPF host exited unexpectedly during cleanup: ${JSON.stringify(result)}`);
    } catch (closeError) {
      host.kill();
      await waitForExit(host, 5000).catch(() => {});
      if (!runError) throw closeError;
      console.error(`P2_07_NATIVE_HOST_CLEANUP ${closeError.message}`);
    }
  }
  if (fixtureServer.listening) {
    fixtureServer.close();
    fixtureServer.closeAllConnections();
  }
  if (capabilityWritten && existsSync(capabilityPath)) {
    const currentHash = createHash("sha256").update(readFileSync(capabilityPath)).digest("hex");
    if (currentHash === capabilityFileHash) unlinkSync(capabilityPath);
    else console.error(`P2_07_CAPABILITY_RETAINED path=${capabilityPath} reason=content-changed-after-test-write`);
  }
}
