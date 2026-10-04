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
  "usage: node native-turn-interruption.mjs <NeoBabylon.Host.exe> <playwright-core-entry.mjs> <exact .local\\Lab\\Runs parent>");

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

const { runRoot, applicationRoot } = newQaRun(qaParent, `P2-P206-native-${Date.now()}-${process.pid}`);
assert.ok(!existsSync(applicationRoot), "refusing to reuse an existing application root");
const screenshots = {
  partial: path.join(runRoot, "partial-stream.png"),
  interrupted: path.join(runRoot, "interrupted.png"),
  restored: path.join(runRoot, "restored-interrupted.png"),
  continued: path.join(runRoot, "continued.png"),
};
const suffix = `${Date.now()}_${process.pid}`;
const capabilityPath = path.join(releaseRoot, `MODEL_CAPABILITY_P2_06_${suffix}.json`);
assert.ok(!existsSync(capabilityPath), "refusing to overwrite an existing test capability record");

const modelIdentifier = "p2-06-loopback-interruption-model";
const originalPrompt = "P2-06 original prompt must be sent once and then stopped.";
const continuationPrompt = "P2-06 deliberate continuation after the interrupted turn.";
const partialText = "Partial answer before stop.";
const continuationReply = "The deliberate continuation completed.";
const providerRequests = [];
const fixtureErrors = [];
const openResponses = new Set();
const unknown = (evidenceSource) => ({ state: "Unknown", value: null, evidenceSource });
const capability = {
  providerId: "lmstudio",
  providerDisplayName: "LM Studio · P2-06 loopback fixture",
  providerServerVersion: "Unknown",
  endpoint: "pending-loopback-endpoint",
  modelIdentifier,
  modelVariant: "test-only local Responses fixture; no model weights",
  architecture: "Unknown",
  parameterCount: "Unknown",
  modelSizeBytes: "Unknown",
  quantization: unknown("P2-06 fixture; no model weights or quantization metadata"),
  contextWindowAdvertised: unknown("P2-06 fixture; provider metadata not queried"),
  contextWindowEffective: {
    state: "Known",
    value: 32768,
    evidenceSource: "P2-06 deterministic fixture value only; not observed model metadata",
  },
  reasoningControls: unknown("P2-06 fixture; no model loaded"),
  toolFunctionCalling: {
    state: "Known",
    value: "tool_use",
    evidenceSource: "P2-06 synthetic test-catalog declaration only; no real model capability or tool call",
  },
  applyPatchToolType: unknown("P2-06 fixture; no patch format qualified"),
  structuredOutput: unknown("P2-06 fixture; provider metadata not queried"),
  agentMetadata: {
    state: "Known",
    value: "type=llm; inputModalities=text; vision=False",
    evidenceSource: "P2-06 synthetic test-catalog declaration only; no model weights or real model metadata",
  },
};

function sseEvent(event) {
  return `event: ${event.type}\ndata: ${JSON.stringify(event)}\n\n`;
}

function assistantResponse(responseId, itemId, text) {
  return [
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
  ].map(sseEvent).join("");
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
    const serializedInput = JSON.stringify(payload.input ?? []);
    providerRequests.push({
      number,
      model: payload.model ?? null,
      originalPromptCount: serializedInput.split(originalPrompt).length - 1,
      continuationPromptCount: serializedInput.split(continuationPrompt).length - 1,
    });
    if (payload.model !== modelIdentifier) {
      fixtureErrors.push(`expected exact model ${modelIdentifier}, received ${payload.model ?? "<missing>"}`);
      response.writeHead(409).end();
      return;
    }
    response.writeHead(200, { "content-type": "text/event-stream", "cache-control": "no-cache" });
    response.flushHeaders();
    if (number === 1) {
      if (!serializedInput.includes(originalPrompt)) {
        fixtureErrors.push("the original prompt was absent from the first provider request");
        response.end();
        return;
      }
      response.write(sseEvent({ type: "response.created", response: { id: "p2-06-response-1" } }));
      response.write(sseEvent({
        type: "response.output_item.added",
        item: { type: "message", role: "assistant", id: "p2-06-partial-item", content: [] },
      }));
      response.write(sseEvent({
        type: "response.output_text.delta",
        output_index: 0,
        item_id: "p2-06-partial-item",
        content_index: 0,
        delta: partialText,
      }));
      openResponses.add(response);
      response.once("close", () => openResponses.delete(response));
      return;
    }
    if (number !== 2 || !serializedInput.includes(continuationPrompt)) {
      fixtureErrors.push(`unexpected provider request ${number} or missing deliberate continuation`);
      response.writeHead(409).end();
      return;
    }
    response.end(assistantResponse("p2-06-response-2", "p2-06-complete-item", continuationReply));
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
  const sessionRoot = path.join(applicationRoot, "Data", "CodexHome", "sessions");
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

function pinnedServerPids() {
  const command = `$expected = [IO.Path]::GetFullPath('${runtimePath.replaceAll("'", "''")}'); Get-CimInstance Win32_Process -Filter \"Name = 'codex-app-server.exe'\" | Where-Object { $_.ExecutablePath -and [IO.Path]::GetFullPath($_.ExecutablePath) -ieq $expected } | ForEach-Object { $_.ProcessId }`;
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", command], {
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

const { chromium } = await import(pathToFileURL(playwrightModulePath).href);
let browser;
let host;
let page;
const pageIssues = [];
const originalServerPids = pinnedServerPids();

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
  const deadline = Date.now() + 45000;
  while (Date.now() < deadline) {
    if (host.exitCode !== null) throw new Error(`WPF host exited before CDP opened (code ${host.exitCode})`);
    try {
      const result = await fetch(`http://127.0.0.1:${debugPort}/json/version`, { signal: AbortSignal.timeout(1000) });
      if (result.ok) break;
    } catch { /* WebView2 is starting. */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  browser = await chromium.connectOverCDP(`http://127.0.0.1:${debugPort}`);
  const deadlinePage = Date.now() + 30000;
  while (Date.now() < deadlinePage) {
    page = browser.contexts().flatMap((context) => context.pages())
      .find((candidate) => candidate.url().startsWith("https://neobabylon.local/"));
    if (page) break;
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  assert.ok(page, "native NeoBabylon WebView page did not appear");
  page.on("pageerror", (error) => pageIssues.push(error.message));
  page.on("console", (message) => {
    if (message.type() === "error" || message.type() === "warning") pageIssues.push(`${message.type()}: ${message.text()}`);
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

async function readRuntimeEvidence() {
  await page.getByRole("button", { name: "Open diagnostics" }).click();
  const dialog = page.getByRole("dialog", { name: "Runtime diagnostics" });
  await dialog.waitFor();
  const evidence = await dialog.innerText();
  assert.ok(evidence.includes(modelIdentifier), "synthetic selected capability is not visible in runtime details");
  assert.ok(evidence.includes(applicationRoot), "isolated application root is not visible in runtime details");
  assert.ok(evidence.includes(path.join(applicationRoot, "Data")), "runtime state is not under the application Data root");
  assert.match(evidence, /Ordinary Codex root used\s+false/i, "runtime details do not confirm ordinary Codex root isolation");
  await dialog.getByRole("button", { name: "Done" }).click();
  return evidence;
}

const fixturePort = await listen(fixtureServer);
capability.endpoint = `http://127.0.0.1:${fixturePort}/v1`;
let capabilityHash;

try {
  writeFileSync(capabilityPath, `${JSON.stringify(capability, null, 2)}\n`, { encoding: "utf8", flag: "wx" });
  capabilityHash = createHash("sha256").update(readFileSync(capabilityPath)).digest("hex");
  await launchHost();
  const runtimeEvidence = await readRuntimeEvidence();

  const conversation = page.getByRole("region", { name: "Conversation" });
  const composer = page.getByRole("textbox", { name: "Message NeoBabylon" });
  await composer.fill(originalPrompt);
  await composer.focus();
  await page.keyboard.press("Enter");
  await conversation.locator("article.message-user .message-text").getByText(originalPrompt, { exact: true }).waitFor();
  await conversation.locator("article.message-assistant .message-text").getByText(partialText, { exact: true }).waitFor();
  assert.equal(providerRequests.length, 1, "the partial stream must use exactly one deterministic fixture request");
  await page.screenshot({ path: screenshots.partial });
  assert.equal(await conversation.locator(".typing-indicator").count(), 1,
    "the partial assistant bubble should be marked active before interruption");

  const stopButton = page.getByRole("button", { name: "Stop turn" });
  await stopButton.focus();
  assert.equal(await stopButton.evaluate((element) => document.activeElement === element), true,
    "the native stop action should be keyboard-focusable before Enter activation");
  await page.keyboard.press("Enter");
  await page.waitForFunction(() => document.querySelector("section.conversation")?.getAttribute("data-turn-state") === "interrupted");
  const statusLine = conversation.getByRole("status");
  await statusLine.getByText(/^Codex App Server: Turn interrupted\. No automatic replay\./).waitFor();
  assert.equal(await conversation.locator("article.message-assistant .message-text").getByText(partialText, { exact: true }).count(), 1,
    "interruption must retain the partial assistant output");
  assert.equal(await conversation.locator(".typing-indicator, .stream-cursor").count(), 0,
    "interrupted partial output must no longer look active");
  assert.equal(providerRequests.length, 1, "stopping the turn must not issue a second provider request");
  await page.screenshot({ path: screenshots.interrupted });

  await page.reload();
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  const restoredConversation = page.getByRole("region", { name: "Conversation" });
  await restoredConversation.getByText(originalPrompt, { exact: true }).waitFor({ timeout: 30000 });
  await restoredConversation.getByRole("status").getByText(/^Codex App Server: Turn interrupted\. No automatic replay\./).waitFor();
  const partialOutputRestored = await restoredConversation.locator("article.message-assistant .message-text")
    .getByText(partialText, { exact: true }).count() === 1;
  await page.screenshot({ path: screenshots.restored });
  assert.equal(providerRequests.length, 1, "renderer reload must restore saved status without replay");
  const continuationComposer = page.getByRole("textbox", { name: "Message NeoBabylon" });
  await continuationComposer.fill(continuationPrompt);
  await continuationComposer.focus();
  await page.keyboard.press("Enter");
  await restoredConversation.locator("article.message-assistant .message-text").getByText(continuationReply, { exact: true })
    .waitFor({ timeout: 30000 });
  await page.waitForFunction(() => {
    const state = document.querySelector("section.conversation")?.getAttribute("data-turn-state");
    return state !== "starting" && state !== "running";
  }, null, { timeout: 30000 });
  assert.equal(providerRequests.length, 2, "one deliberate continuation should add exactly one provider request");
  const prompts = journalUserPrompts();
  assert.equal(prompts.filter((text) => text === originalPrompt).length, 1,
    "the original user-authored request must appear exactly once in the saved turn journal");
  assert.equal(prompts.filter((text) => text === continuationPrompt).length, 1,
    "the explicit continuation must appear exactly once in the saved turn journal");
  assert.equal(providerRequests[0].originalPromptCount, 1, "the first request did not contain the original prompt exactly once");
  assert.equal(providerRequests[1].continuationPromptCount, 1, "the continuation request did not contain the new prompt exactly once");
  assert.equal(fixtureErrors.length, 0, fixtureErrors.join("; "));
  assert.equal(pageIssues.length, 0, pageIssues.join("; "));
  await page.screenshot({ path: screenshots.continued });
  await closeHost();
  await waitFor(() => [...pinnedServerPids()].every((pid) => originalServerPids.has(pid)),
    "the owned pinned App Server remained after WPF host shutdown");

  const result = {
    slice: "P2-06",
    result: "pass",
    runtime: { version: runtimeLock.runtime.version, revision: runtimeLock.runtime.sourceRevision, sha256: runtimeHash },
    capability: { providerId: capability.providerId, modelIdentifier, endpoint: capability.endpoint,
      evidence: "synthetic loopback Responses fixture; no model weights, external provider, or live inference" },
    userPrompts: { originalPrompt, continuationPrompt },
    keyboardActions: ["Enter submitted the original prompt", "Enter activated the focused Stop turn button",
      "Enter submitted the deliberate continuation"],
    providerRequests,
    savedJournalPrompts: prompts,
    savedInterruptedStatusObservedAfterRendererReload: true,
    visibleInterruptedAttribution: "Codex App Server",
    partialOutputRetainedAndSettled: partialText,
    partialOutputRestoredAfterRendererReload: partialOutputRestored,
    automaticReplay: false,
    appServerExitedAfterHostClose: true,
    runtimeDataRoot: path.join(applicationRoot, "Data"),
    runtimeEvidence,
    screenshots,
    fixtureErrors,
    pageIssues,
  };
  writeFileSync(path.join(runRoot, "result.json"), `${JSON.stringify(result, null, 2)}\n`);
  console.log(`P2_06_NATIVE result=pass runtime=${runtimeLock.runtime.version} providerRequests=${providerRequests.length} savedStatus=interrupted reloadNoReplay=true explicitContinuation=true appServerExited=true root=${applicationRoot}`);
} finally {
  await closeHost().catch(() => {});
  for (const response of openResponses) response.destroy();
  await new Promise((resolve) => fixtureServer.close(() => resolve()));
  if (existsSync(capabilityPath) && capabilityHash
    && createHash("sha256").update(readFileSync(capabilityPath)).digest("hex") === capabilityHash) {
    unlinkSync(capabilityPath);
  }
}
