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
  "usage: node native-command-failure-rendering.mjs <NeoBabylon.Host.exe> <playwright-core-entry.mjs> <exact .local\\Lab\\Runs parent>");

const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const hostPath = path.resolve(hostPathArg);
const playwrightModulePath = path.resolve(playwrightModulePathArg);
const qaParent = path.resolve(qaParentArg);
assertQaRunsParent(sourceRoot, qaParent);
const releaseRoot = path.join(sourceRoot, "docs", "release");

for (const [label, filePath] of [["WPF host", hostPath], ["Playwright module", playwrightModulePath]]) {
  assert.ok(existsSync(filePath), `${label} does not exist: ${filePath}`);
}

const runtimeLock = JSON.parse(readFileSync(path.join(sourceRoot, "runtime", "runtime-lock.json"), "utf8"));
const runtime = runtimeLock.runtime;
const runtimePath = path.resolve(sourceRoot, runtime.appServerBinaryRelativePath);
assert.ok(existsSync(runtimePath), `pinned App Server binary is missing: ${runtimePath}`);
const runtimeHash = createHash("sha256").update(readFileSync(runtimePath)).digest("hex");
assert.equal(runtimeHash, runtime.sha256.toLowerCase(), "pinned App Server binary does not match its runtime lock");

const { runRoot, applicationRoot } = newQaRun(qaParent, `P3-02-native-command-failure-${Date.now()}-${process.pid}`);
const dataRoot = path.join(applicationRoot, "Data");
const workspace = path.join(dataRoot, "Workspace");
mkdirSync(workspace, { recursive: true });
const capabilityPath = path.join(releaseRoot, `MODEL_CAPABILITY_P3_02_NATIVE_${Date.now()}_${process.pid}.json`);
const resultPath = path.join(runRoot, "result.json");
const screenshotPath = path.join(runRoot, "command-failure-visible.png");
const modelIdentifier = "p3-02-native-command-failure-fixture";
const prompt = "Run the one isolated fixture command exactly once, then report its result.";
const assistantReply = "The isolated command returned exit code 23 after producing its marker.";
const marker = "NEOBABYLON_P3_02_NATIVE_FAILURE_MARKER";
const syntheticCanary = "sk-nb-synthetic-canary-not-a-credential";
const scriptPath = path.join(workspace, "p3-02-command-failure.cmd");
assert.ok(!scriptPath.match(/\s/), "fixture command path must remain unambiguous to the Windows shell");
writeFileSync(scriptPath, `@echo off\r\necho ${marker}\r\necho ${syntheticCanary}\r\nexit /b 23\r\n`, { encoding: "ascii", flag: "wx" });
const command = `cmd.exe /d /c ${scriptPath}; exit $LASTEXITCODE`;
const callId = "p3-02-native-command-failure-call";

const capability = {
  providerId: "lmstudio",
  providerDisplayName: "LM Studio · P3-02 deterministic native failure fixture",
  providerServerVersion: "Test fixture; no LM Studio server",
  endpoint: "pending-loopback-endpoint",
  modelIdentifier,
  modelVariant: "synthetic Responses fixture; no model weights or live inference",
  architecture: "Unknown",
  parameterCount: "Unknown",
  modelSizeBytes: "Unknown",
  quantization: { state: "Unknown", value: null, evidenceSource: "Synthetic P3-02 fixture; no model weights" },
  contextWindowAdvertised: { state: "Unknown", value: null, evidenceSource: "Synthetic P3-02 fixture; no provider metadata" },
  contextWindowEffective: { state: "Known", value: 32768, evidenceSource: "Synthetic test catalog only; not model metadata" },
  reasoningControls: { state: "Unknown", value: null, evidenceSource: "Synthetic P3-02 fixture; no model metadata" },
  toolFunctionCalling: { state: "Known", value: "tool_use", evidenceSource: "Synthetic fixture needed to exercise a tool-call path" },
  applyPatchToolType: { state: "Unknown", value: null, evidenceSource: "P3-02 fixture does not exercise patch calls" },
  structuredOutput: { state: "Unknown", value: null, evidenceSource: "Synthetic P3-02 fixture; no provider metadata" },
  agentMetadata: {
    state: "Known",
    value: "type=llm; inputModalities=text; vision=False; capabilities=tool_use",
    evidenceSource: "Synthetic Codex test catalog only; not a provider/model claim",
  },
};

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((complete, fail) => { resolve = complete; reject = fail; });
  return { promise, resolve, reject };
}

function sseEvent(event) {
  return `event: ${event.type}\ndata: ${JSON.stringify(event)}\n\n`;
}

function completed(responseId) {
  return {
    type: "response.completed",
    response: {
      id: responseId,
      usage: { input_tokens: 0, input_tokens_details: null, output_tokens: 0, output_tokens_details: null, total_tokens: 0 },
    },
  };
}

function firstResponse() {
  const responseId = "p3-02-native-tool-response";
  return [
    { type: "response.created", response: { id: responseId } },
    {
      type: "response.output_item.done",
      item: {
        type: "function_call",
        call_id: callId,
        name: "exec_command",
        arguments: JSON.stringify({ cmd: command, workdir: workspace }),
      },
    },
    completed(responseId),
  ].map(sseEvent).join("");
}

function finalResponse() {
  const responseId = "p3-02-native-final-response";
  return [
    { type: "response.created", response: { id: responseId } },
    {
      type: "response.output_item.done",
      item: {
        type: "message",
        role: "assistant",
        id: "p3-02-native-final-message",
        content: [{ type: "output_text", text: assistantReply }],
      },
    },
    completed(responseId),
  ].map(sseEvent).join("");
}

const firstRequest = deferred();
const secondRequest = deferred();
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
    const number = providerRequests.length;
    if (payload.model !== modelIdentifier) {
      fixtureErrors.push(`request ${number} used unexpected model ${payload.model ?? "<missing>"}`);
      response.writeHead(409).end();
      return;
    }
    response.writeHead(200, { "content-type": "text/event-stream", "cache-control": "no-cache" });
    response.flushHeaders();
    if (number === 1) {
      if (!JSON.stringify(payload.input ?? []).includes(prompt)) {
        fixtureErrors.push("the exact native prompt was absent from the first Responses request");
      }
      firstRequest.resolve(payload);
      response.end(firstResponse());
      return;
    }
    if (number === 2) {
      const inputText = JSON.stringify(payload.input ?? []);
      if (!inputText.includes("function_call_output") || !inputText.includes(callId)
        || !inputText.includes(marker) || !inputText.includes(syntheticCanary)
        || !inputText.includes("Process exited with code 23")) {
        fixtureErrors.push("the exact failed tool result, partial output, and synthetic canary were not sent to its single continuation");
      }
      secondRequest.resolve(payload);
      response.end(finalResponse());
      return;
    }
    fixtureErrors.push(`unexpected extra Responses request ${number}; retry or fallback occurred`);
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
      if (!address || typeof address === "string") reject(new Error("could not reserve a local WebView2 debug port"));
      else server.close((error) => error ? reject(error) : resolve(address.port));
    });
  });
}

function requestHostClose(pid) {
  const commandText = `$process = Get-Process -Id ${pid} -ErrorAction Stop; if (-not $process.CloseMainWindow()) { exit 2 }; exit 0`;
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", commandText], {
    encoding: "utf8",
    timeout: 10000,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`could not gracefully close QA host PID ${pid}: ${result.stderr || result.stdout}`);
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

function installBridgeCapture() {
  if (window.__p302FailureCaptureInstalled) return;
  window.__p302FailureCaptureInstalled = true;
  window.__p302FailureEvents = [];
  window.__p302FailureStartTurnResults = [];
  window.chrome.webview.addEventListener("message", ({ data }) => {
    if (data?.attributedTo === "Codex App Server" && typeof data.method === "string") {
      window.__p302FailureEvents.push({ method: data.method, params: data.params });
    }
    if (!data?.stream && data?.ok === true && Array.isArray(data.result?.toolDiagnostics)) {
      window.__p302FailureStartTurnResults.push(data.result);
    }
  });
}

const { chromium } = await import(pathToFileURL(playwrightModulePath).href);
const pageIssues = [];
let host = null;
let browser = null;
let page = null;
let capabilityFileBytes = null;
let capabilityFileCreated = false;
let fixturePort = null;
let runError = null;

async function waitForCdp(port) {
  const deadline = Date.now() + 45000;
  while (Date.now() < deadline) {
    if (host && host.exitCode !== null) throw new Error(`WPF host exited before CDP opened (code ${host.exitCode})`);
    try {
      const result = await fetch(`http://127.0.0.1:${port}/json/version`, { signal: AbortSignal.timeout(1000) });
      if (result.ok) return;
    } catch { /* WebView2 is starting. */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`WebView2 CDP did not open on port ${port}`);
}

async function waitForNativePage(port) {
  const deadline = Date.now() + 30000;
  while (Date.now() < deadline) {
    if (host && host.exitCode !== null) throw new Error(`WPF host exited before the native page appeared (code ${host.exitCode})`);
    const candidate = browser?.contexts().flatMap((context) => context.pages())
      .find((item) => item.url().startsWith("https://neobabylon.local/"));
    if (candidate) return candidate;
    try {
      await fetch(`http://127.0.0.1:${port}/json/list`, { signal: AbortSignal.timeout(1000) });
    } catch { /* Native page is still loading. */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error("native NeoBabylon WebView page did not appear");
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
  await waitForCdp(debugPort);
  browser = await chromium.connectOverCDP(`http://127.0.0.1:${debugPort}`);
  page = await waitForNativePage(debugPort);
  page.on("pageerror", (error) => pageIssues.push(error.message));
  page.on("console", (message) => { if (message.type() === "error") pageIssues.push(message.text()); });
  await page.addInitScript(installBridgeCapture);
  await page.evaluate(installBridgeCapture);
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  await page.getByRole("textbox", { name: "Message NeoBabylon" }).waitFor({ timeout: 15000 });
  assert.equal(await page.title(), "NeoBabylon");
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

const fixtureReady = await listen(fixtureServer);
fixturePort = fixtureReady;
capability.endpoint = `http://127.0.0.1:${fixturePort}/v1`;

try {
  assert.equal(existsSync(capabilityPath), false, "refusing to overwrite an existing synthetic capability record");
  capabilityFileBytes = `${JSON.stringify(capability, null, 2)}\n`;
  writeFileSync(capabilityPath, capabilityFileBytes, { encoding: "utf8", flag: "wx" });
  capabilityFileCreated = true;

  await launchHost();
  const diagnosticsButton = page.getByRole("button", { name: "Open diagnostics" });
  await diagnosticsButton.click();
  const diagnostics = page.getByRole("dialog", { name: "Runtime diagnostics" });
  await diagnostics.waitFor();
  const diagnosticsText = await diagnostics.innerText();
  assert.ok(diagnosticsText.includes(applicationRoot), "diagnostics omitted the isolated application root");
  assert.ok(diagnosticsText.includes(dataRoot), "diagnostics omitted the isolated Data root");
  assert.match(diagnosticsText, /Ordinary Codex root used\s+false/i, "ordinary Codex root use was not false");
  await diagnostics.getByRole("button", { name: "Done" }).click();

  const composer = page.getByRole("textbox", { name: "Message NeoBabylon" });
  await composer.fill(prompt);
  assert.equal(await composer.evaluate((element) => document.activeElement === element), true,
    "the native composer should retain keyboard focus before Enter submission");
  await page.keyboard.press("Enter");
  const request = await Promise.race([
    firstRequest.promise,
    new Promise((_, reject) => setTimeout(() => reject(new Error("first request did not reach the loopback fixture")), 45000)),
  ]);
  assert.equal(request.model, modelIdentifier, "the exact synthetic selected tuple changed on the first request");
  assert.ok(!request.allow_fallback, "provider/model fallback was unexpectedly enabled");

  const failedActivity = page.locator(".activity-card.activity-failed").filter({ hasText: marker });
  await failedActivity.waitFor({ timeout: 60000 });
  assert.equal(await page.locator(".activity-card.activity-failed").count(), 1,
    "one ordinary failed command should render as exactly one failed activity");
  assert.equal((await failedActivity.locator(".activity-status-label").innerText()).trim(), "FAILED",
    "the nonzero command was not visibly labeled failed");
  assert.equal((await failedActivity.locator(".activity-attribution").innerText()).trim(), "Codex App Server",
    "the visible command failure lost its App Server attribution");
  const capturedCommandItem = await page.evaluate((expectedMarker) => {
    const event = (window.__p302FailureEvents ?? []).find((candidate) => candidate.method === "item/completed"
      && candidate.params?.item?.type === "commandExecution"
      && candidate.params?.item?.aggregatedOutput?.includes(expectedMarker));
    return event?.params?.item ?? null;
  }, marker);
  assert.equal(capturedCommandItem?.exitCode, 23, "pinned App Server did not send the exact exit code to the native bridge");
  assert.ok((await failedActivity.innerText()).includes(marker), "the native failure card omitted retained partial stdout");
  assert.ok((await failedActivity.innerText()).includes(syntheticCanary),
    "the native activity card did not reflect ordinary App Server command output containing the synthetic canary");

  const continuation = await Promise.race([
    secondRequest.promise,
    new Promise((_, reject) => setTimeout(() => reject(new Error("failed command result did not reach its single continuation")), 30000)),
  ]);
  assert.equal(continuation.model, modelIdentifier, "the failure continuation changed the selected tuple");
  assert.equal(providerRequests.length, 2, "the failure must have exactly one tool request and one continuation");
  assert.deepEqual(fixtureErrors, [], fixtureErrors.join("; "));
  await page.getByRole("region", { name: "Conversation" }).locator("article.message-assistant .message-text")
    .getByText(assistantReply, { exact: true }).waitFor({ timeout: 30000 });
  await page.waitForFunction(() =>
    document.querySelector("section.conversation")?.getAttribute("data-turn-state") === "failed", null, { timeout: 30000 });

  const turnResults = await page.evaluate(() => window.__p302FailureStartTurnResults ?? []);
  const finalToolDiagnostic = turnResults.flatMap((result) => result.toolDiagnostics ?? [])
    .find((diagnostic) => diagnostic.itemId === capturedCommandItem.id);
  assert.ok(finalToolDiagnostic, "native startTurn response did not project the exact failed App Server item");
  assert.equal(finalToolDiagnostic.exitCode, 23, "native final tool diagnostic dropped the exact command exit code");
  assert.equal(finalToolDiagnostic.succeeded, false, "native final tool diagnostic did not retain the failed outcome");
  assert.equal(finalToolDiagnostic.attributedTo, "Codex App Server", "native final tool diagnostic lost its App Server attribution");
  const exitCodeLabel = failedActivity.locator(".activity-exit-code");
  assert.equal(await exitCodeLabel.count(), 1, "the native failure card omitted a visible exit-code label");
  assert.equal((await exitCodeLabel.innerText()).trim(), "Exit code 23");
  assert.match(await failedActivity.getAttribute("aria-label"), /exit code 23/i,
    "the accessible activity label omitted the exact exit code");

  const events = await page.evaluate(() => window.__p302FailureEvents ?? []);
  const started = events.find((event) => event.method === "turn/started" && event.params?.turn?.id);
  assert.ok(started, "native UI did not receive an attributed App Server turn-start event");
  const completedItem = events.find((event) => event.method === "item/completed"
    && event.params?.item?.type === "commandExecution"
    && event.params?.item?.aggregatedOutput?.includes(marker));
  assert.ok(completedItem, "native bridge did not retain the exact failed command-completion event");
  assert.equal(completedItem.params.threadId, started.params.threadId, "failure event changed thread identity");
  assert.equal(completedItem.params.turnId, started.params.turn.id, "failure event changed turn identity");
  assert.deepEqual(pageIssues, [], "native WebView reported page or console errors");
  await page.screenshot({ path: screenshotPath });

  const result = {
    passed: true,
    slice: "P3-02 native command failure rendering",
    evidenceKind: "pinned App Server plus deterministic loopback Responses; no live inference",
    runtime: { version: runtime.version, sourceRevision: runtime.sourceRevision, path: runtimePath, sha256: runtimeHash },
    selectedModel: modelIdentifier,
    applicationRoot,
    dataRoot,
    ordinaryCodexRootUsed: false,
    commandExitCode: 23,
    finalToolDiagnosticItemId: finalToolDiagnostic.itemId,
    finalToolDiagnosticExitCode: finalToolDiagnostic.exitCode,
    finalToolDiagnosticAttribution: finalToolDiagnostic.attributedTo,
    partialOutputMarker: marker,
    syntheticCredentialCanary: syntheticCanary,
    syntheticCanaryForwardedToLoopbackModel: true,
    syntheticCanaryVisibleInRenderer: true,
    visibleStatus: "failed",
    visibleAttribution: "Codex App Server",
    terminalTurnState: "failed",
    keyboardSubmission: "Enter",
    threadId: started.params.threadId,
    turnId: started.params.turn.id,
    commandItemId: completedItem.params.item.id,
    providerRequestCount: providerRequests.length,
    retryOrFallback: false,
    pageIssues,
    screenshots: [screenshotPath],
    runRoot,
  };
  writeFileSync(resultPath, `${JSON.stringify(result, null, 2)}\n`, "utf8");
  console.log(JSON.stringify(result));
} catch (error) {
  runError = error;
  const diagnostics = page ? await page.evaluate(() => ({
    turnState: document.querySelector("section.conversation")?.getAttribute("data-turn-state"),
    activity: document.querySelector(".activity-stack")?.innerText ?? null,
    events: window.__p302FailureEvents ?? [],
    body: document.body.innerText.slice(0, 2500),
  })).catch((inspectionError) => ({ inspectionError: String(inspectionError) })) : null;
  writeFileSync(resultPath, `${JSON.stringify({
    passed: false,
    slice: "P3-02 native command failure rendering",
    error: error instanceof Error ? `${error.name}: ${error.message}` : String(error),
    runtime: { version: runtime.version, sourceRevision: runtime.sourceRevision, sha256: runtimeHash },
    selectedModel: modelIdentifier,
    providerRequestCount: providerRequests.length,
    fixtureErrors,
    pageIssues,
    diagnostics,
    runRoot,
  }, null, 2)}\n`, "utf8");
  throw error;
} finally {
  await closeHost().catch((closeError) => {
    if (!runError) throw closeError;
  });
  if (fixtureServer.listening) {
    fixtureServer.close();
    fixtureServer.closeAllConnections();
  }
  if (capabilityFileCreated && capabilityFileBytes !== null && existsSync(capabilityPath)) {
    if (readFileSync(capabilityPath, "utf8") === capabilityFileBytes) unlinkSync(capabilityPath);
    else console.error(`Synthetic capability record changed externally and was preserved: ${capabilityPath}`);
  }
}
