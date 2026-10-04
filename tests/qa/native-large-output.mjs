import assert from "node:assert/strict";
import { spawn, spawnSync } from "node:child_process";
import { createServer as createHttpServer } from "node:http";
import { createServer as createTcpServer } from "node:net";
import { once } from "node:events";
import { createHash } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, unlinkSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { assertQaRunsParent, newQaRun } from "./qa-run-root.mjs";

const [runnerPathArg, hostPathArg, playwrightModulePathArg, qaParentArg] = process.argv.slice(2);
assert.ok(runnerPathArg && hostPathArg && playwrightModulePathArg && qaParentArg,
  "usage: node native-large-output.mjs <test-runner.exe> <NeoBabylon.Host.exe> <playwright-core-entry.mjs> <exact .local\\Lab\\Runs parent>");

const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const runnerPath = path.resolve(runnerPathArg);
const hostPath = path.resolve(hostPathArg);
const playwrightModulePath = path.resolve(playwrightModulePathArg);
const qaParent = path.resolve(qaParentArg);
assertQaRunsParent(sourceRoot, qaParent);
const releaseRoot = path.join(sourceRoot, "docs", "release");
const isWithin = (parent, candidate) => {
  const relative = path.relative(path.resolve(parent), path.resolve(candidate));
  return relative !== "" && relative !== ".." && !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative);
};

for (const [label, filePath] of [["test runner", runnerPath], ["WPF host", hostPath], ["Playwright module", playwrightModulePath]]) {
  assert.ok(existsSync(filePath), `${label} does not exist: ${filePath}`);
}

const runtimeLock = JSON.parse(readFileSync(path.join(sourceRoot, "runtime", "runtime-lock.json"), "utf8"));
const runtimePath = path.resolve(sourceRoot, runtimeLock.runtime.appServerBinaryRelativePath);
assert.ok(existsSync(runtimePath), `pinned App Server binary is missing: ${runtimePath}`);
const runtimeHash = createHash("sha256").update(readFileSync(runtimePath)).digest("hex");
assert.equal(runtimeHash, runtimeLock.runtime.sha256.toLowerCase(), "pinned App Server binary does not match runtime-lock.json");

const { runRoot, applicationRoot } = newQaRun(qaParent, `P2-P211-native-${Date.now()}-${process.pid}`);
const workspace = path.join(runRoot, "workspace");
const capabilityPath = path.join(releaseRoot, `MODEL_CAPABILITY_P2_11_NATIVE_${Date.now()}_${process.pid}.json`);
assert.ok(!existsSync(capabilityPath), "refusing to overwrite an existing P2-11 temporary capability record");
const manifestPath = path.join(runRoot, "manifest.json");
const resultPath = path.join(runRoot, "result.json");
const screenshots = {
  completed: path.join(runRoot, "large-output-completed.png"),
  inspected: path.join(runRoot, "assistant-output-inspected.png"),
  restored: path.join(runRoot, "large-output-restored.png"),
  cancelled: path.join(runRoot, "partial-output-cancelled.png"),
  cancellationRestored: path.join(runRoot, "partial-output-cancellation-restored.png"),
  failed: path.join(runRoot, "partial-output-failed.png"),
  documentInspected: path.join(runRoot, "large-document-output-inspected.png"),
};

const modelIdentifier = "p2-11-loopback-large-output-fixture";
const firstPrompt = "P2-11 deterministic large-output and permitted-tool fixture.";
const cancellationPrompt = "P2-11 deterministic partial-output cancellation fixture.";
const failurePrompt = "P2-11 deterministic late-provider-failure fixture.";
const toolStart = "P2_11_TOOL_START";
const toolEnd = "P2_11_TOOL_END";
const documentStart = "# P3_04_LARGE_DOCUMENT_START";
const documentMiddle = "P3_04_LARGE_DOCUMENT_MIDDLE";
const documentEnd = "P3_04_LARGE_DOCUMENT_END";
const assistantStart = "P2_11_ASSISTANT_START";
const assistantMiddle = "P2_11_INSPECT_MIDDLE";
const assistantEnd = "P2_11_ASSISTANT_END";
const partialFailureText = "P2_11_PARTIAL_BEFORE_PROVIDER_FAILURE";
const partialCancellationText = "P2_11_PARTIAL_BEFORE_CANCELLATION";
const largeOutputCharacters = 1_100_000;
const assistantPrefix = `${assistantStart}|`;
const assistantMiddleFrame = `|${assistantMiddle}|`;
const assistantSuffix = `|${assistantEnd}`;
function patternedText(length, label, character) {
  let text = "";
  for (let index = 0; text.length < length; index++) {
    const marker = `|${label}-${String(index).padStart(3, "0")}|`;
    text += marker + character.repeat(16_384 - marker.length);
  }
  return text.slice(0, length);
}

const firstAssistantRun = patternedText(180_000, "A", "A");
const secondAssistantRun = patternedText(220_000, "B", "B");
const finalAssistantRunLength = largeOutputCharacters - assistantPrefix.length - assistantMiddleFrame.length
  - assistantSuffix.length - firstAssistantRun.length - secondAssistantRun.length;
assert.ok(finalAssistantRunLength > 0);
const largeAssistantText = assistantPrefix + firstAssistantRun + assistantMiddleFrame + secondAssistantRun
  + "C".repeat(finalAssistantRunLength) + assistantSuffix;
assert.equal(largeAssistantText.length, largeOutputCharacters);
const documentPrefix = `${toolStart}\n${documentStart}\n`;
const documentMiddleFrame = `\n${documentMiddle}\n`;
const documentSuffix = `\n${documentEnd}\n${toolEnd}`;
const documentBodyLength = largeOutputCharacters - documentPrefix.length - documentMiddleFrame.length - documentSuffix.length;
const documentBody = patternedText(documentBodyLength, "DOC", "D");
const documentBodyMiddle = Math.floor(documentBody.length / 2);
const largeDocumentText = documentPrefix + documentBody.slice(0, documentBodyMiddle) + documentMiddleFrame
  + documentBody.slice(documentBodyMiddle) + documentSuffix;
assert.equal(largeDocumentText.length, largeOutputCharacters);
const documentPath = path.join(workspace, "P3_04_LARGE_DOCUMENT.md");
const documentSha256 = createHash("sha256").update(largeDocumentText, "utf8").digest("hex");

const capability = {
  providerId: "lmstudio",
  providerDisplayName: "LM Studio · P2-11 loopback fixture",
  providerServerVersion: "Unknown",
  endpoint: "pending-loopback-endpoint",
  modelIdentifier,
  modelVariant: "P2-11 deterministic loopback fixture; no model weights",
  architecture: "Unknown",
  parameterCount: "Unknown",
  modelSizeBytes: "Unknown",
  quantization: { state: "Unknown", value: null, evidenceSource: "P2-11 deterministic loopback fixture; no model weights" },
  contextWindowAdvertised: { state: "Unknown", value: null, evidenceSource: "P2-11 deterministic loopback fixture" },
  contextWindowEffective: { state: "Known", value: 32768, evidenceSource: "P2-11 deterministic test catalog only" },
  reasoningControls: { state: "Unknown", value: null, evidenceSource: "P2-11 deterministic loopback fixture" },
  toolFunctionCalling: { state: "Known", value: "tool_use", evidenceSource: "P2-11 deterministic Responses fixture tool-call path" },
  applyPatchToolType: { state: "Unknown", value: null, evidenceSource: "P2-11 fixture does not qualify apply_patch" },
  structuredOutput: { state: "Unknown", value: null, evidenceSource: "P2-11 deterministic loopback fixture" },
  agentMetadata: {
    state: "Known",
    value: "type=llm; inputModalities=text; vision=False; capabilities=tool_use",
    evidenceSource: "P2-11 synthetic loopback test catalog only; no model weights or provider metadata",
  },
};

function sseEvent(event) {
  return `event: ${event.type}\ndata: ${JSON.stringify(event)}\n\n`;
}

async function sendEvent(response, event) {
  if (!response.write(sseEvent(event))) await once(response, "drain");
}

function responseCompleted(id) {
  return {
    type: "response.completed",
    response: { id, usage: { input_tokens: 0, input_tokens_details: null, output_tokens: 0,
      output_tokens_details: null, total_tokens: 0 } },
  };
}

function functionCallResponse(documentPath) {
  return [
    { type: "response.created", response: { id: "p2-11-tool-call-response" } },
    {
      type: "response.output_item.done",
      item: {
        type: "function_call",
        call_id: "p2-11-output-canary-command",
        name: "exec_command",
        arguments: JSON.stringify({
          cmd: `powershell.exe -NoLogo -NoProfile -NonInteractive -Command "[Console]::Out.Write([IO.File]::ReadAllText('${documentPath.replaceAll("'", "''")}'))"`,
        }),
      },
    },
    responseCompleted("p2-11-tool-call-response"),
  ].map(sseEvent).join("");
}

const fixtureErrors = [];
const providerRequests = [];
let assistantStreamStartedAt = null;
let assistantStreamCompletedAt = null;
let cancellationClientClosedAt = null;
const fixtureServer = createHttpServer((request, response) => {
  if (request.method !== "POST" || request.url !== "/v1/responses") {
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
      fixtureErrors.push("Responses request body was not JSON");
      response.writeHead(400).end();
      return;
    }
    const number = providerRequests.length + 1;
    const serializedInput = JSON.stringify(payload.input ?? []);
    const record = {
      number,
      model: payload.model ?? null,
      inputCharacters: serializedInput.length,
      containsLargeToolOutput: serializedInput.includes(toolStart),
      containsToolTail: serializedInput.includes(toolEnd),
      containsDocumentStart: serializedInput.includes(documentStart),
      containsDocumentMiddle: serializedInput.includes(documentMiddle),
      containsDocumentEnd: serializedInput.includes(documentEnd),
      containsUpstreamOmissionMarker: /\.\.\.\s*\d+\s+bytes omitted\s*\.\.\./i.test(serializedInput),
      containsFailurePrompt: serializedInput.includes(failurePrompt),
      containsCancellationPrompt: serializedInput.includes(cancellationPrompt),
    };
    providerRequests.push(record);
    if (payload.model !== modelIdentifier) {
      fixtureErrors.push(`expected exact synthetic model ${modelIdentifier}, received ${payload.model ?? "<missing>"}`);
      response.writeHead(409).end();
      return;
    }
    response.writeHead(200, { "content-type": "text/event-stream", "cache-control": "no-cache" });
    response.flushHeaders();
    try {
      if (number === 1) {
        if (!serializedInput.includes(firstPrompt)) fixtureErrors.push("the large-output prompt was absent from the first request");
        response.end(functionCallResponse(documentPath));
        return;
      }
      if (number === 2) {
        if (!record.containsLargeToolOutput || !record.containsToolTail) {
          fixtureErrors.push("the ordinary tool result did not reach the same selected model continuation");
        }
        if (!record.containsDocumentStart || record.containsDocumentMiddle || !record.containsDocumentEnd) {
          fixtureErrors.push("the large-document read did not preserve its retained head/tail and omit its middle range");
        }
        if (!record.containsUpstreamOmissionMarker) {
          fixtureErrors.push("the command output did not disclose App Server's upstream head/tail omission marker");
        }
        assistantStreamStartedAt = Date.now();
        await sendEvent(response, { type: "response.created", response: { id: "p2-11-large-response" } });
        await sendEvent(response, {
          type: "response.output_item.added",
          item: { type: "message", role: "assistant", id: "p2-11-large-assistant-item", content: [] },
        });
        for (let offset = 0; offset < largeAssistantText.length; offset += 8192) {
          await sendEvent(response, {
            type: "response.output_text.delta",
            output_index: 0,
            item_id: "p2-11-large-assistant-item",
            content_index: 0,
            delta: largeAssistantText.slice(offset, offset + 8192),
          });
          if (offset % 65_536 === 0) await new Promise((resolve) => setTimeout(resolve, 2));
        }
        await sendEvent(response, {
          type: "response.output_item.done",
          item: {
            type: "message",
            role: "assistant",
            id: "p2-11-large-assistant-item",
            content: [{ type: "output_text", text: largeAssistantText }],
          },
        });
        await sendEvent(response, responseCompleted("p2-11-large-response"));
        assistantStreamCompletedAt = Date.now();
        response.end();
        return;
      }
      if (number === 3 && record.containsCancellationPrompt) {
        const clientClosed = new Promise((resolve) => {
          response.once("close", () => {
            cancellationClientClosedAt = Date.now();
            resolve();
          });
        });
        await sendEvent(response, { type: "response.created", response: { id: "p2-11-cancel-response" } });
        await sendEvent(response, {
          type: "response.output_item.added",
          item: { type: "message", role: "assistant", id: "p2-11-cancel-assistant-item", content: [] },
        });
        await sendEvent(response, {
          type: "response.output_text.delta",
          output_index: 0,
          item_id: "p2-11-cancel-assistant-item",
          content_index: 0,
          delta: partialCancellationText,
        });
        await clientClosed;
        return;
      }
      if (record.containsFailurePrompt) {
        await sendEvent(response, { type: "response.created", response: { id: "p2-11-late-failure-response" } });
        await sendEvent(response, {
          type: "response.output_item.added",
          item: { type: "message", role: "assistant", id: "p2-11-partial-failure-item", content: [] },
        });
        await sendEvent(response, {
          type: "response.output_text.delta",
          output_index: 0,
          item_id: "p2-11-partial-failure-item",
          content_index: 0,
          delta: partialFailureText,
        });
        await sendEvent(response, {
          type: "response.failed",
          response: { id: "p2-11-late-failure-response", error: { type: "invalid_request_error", code: "invalid_prompt", message: "P2-11 deterministic late provider failure" } },
        });
        response.end();
        return;
      }
      fixtureErrors.push(`unexpected provider request ${number}`);
      response.end();
    } catch (error) {
      fixtureErrors.push(`loopback SSE write failed: ${error.message}`);
      response.destroy(error);
    }
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
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", command], { encoding: "utf8", timeout: 10000 });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`could not request graceful close for QA host PID ${pid}: ${result.stderr || result.stdout}`);
}

function getProcessSample(pid) {
  const expectedPath = runtimePath.replaceAll("'", "''");
  const command = `$expected = [IO.Path]::GetFullPath('${expectedPath}'); $rootProcessId = ${pid}; $all = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue); $owned = @{}; $owned[$rootProcessId] = $true; $changed = $true; while ($changed) { $changed = $false; foreach ($entry in $all) { $childId = [int]$entry.ProcessId; if ($owned.ContainsKey([int]$entry.ParentProcessId) -and -not $owned.ContainsKey($childId)) { $owned[$childId] = $true; $changed = $true } } }; $items = @(); $hostProcess = Get-Process -Id ${pid} -ErrorAction SilentlyContinue; if ($hostProcess) { $items += [pscustomobject]@{ role='WPF host'; pid=$hostProcess.Id; workingSetBytes=$hostProcess.WorkingSet64; privateBytes=$hostProcess.PrivateMemorySize64 } }; $all | Where-Object { $owned.ContainsKey([int]$_.ProcessId) -and $_.Name -ieq 'codex-app-server.exe' -and $_.ExecutablePath -and [IO.Path]::GetFullPath($_.ExecutablePath) -ieq $expected } | ForEach-Object { $p = Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue; if ($p) { $items += [pscustomobject]@{ role='pinned App Server'; pid=$p.Id; workingSetBytes=$p.WorkingSet64; privateBytes=$p.PrivateMemorySize64 } } }; ConvertTo-Json -InputObject $items -Compress`;
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", command], { encoding: "utf8", timeout: 10000 });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`could not sample the owned host/runtime processes: ${result.stderr || result.stdout}`);
  const raw = result.stdout.trim();
  const decoded = raw ? JSON.parse(raw) : [];
  return { sampledAt: new Date().toISOString(), processes: Array.isArray(decoded) ? decoded : decoded ? [decoded] : [] };
}

function stopHostWindow(pid) {
  requestHostClose(pid);
}

async function waitFor(predicate, message, timeoutMs = 45000) {
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
let memorySampler;
const memorySamples = [];
const pageIssues = [];
let mainPromptSentAt = null;
let largeOutputVisibleAt = null;
let cancellationPromptSentAt = null;
let cancellationVisibleAt = null;
let failurePromptSentAt = null;

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
    if (message.type() === "error") pageIssues.push(`console: ${message.text()}`);
  });
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  assert.equal(await page.title(), "NeoBabylon");
  assert.equal(await page.locator("vite-error-overlay").count(), 0);
  await page.getByRole("textbox", { name: "Message NeoBabylon" }).waitFor();
  memorySamples.push(getProcessSample(host.pid));
  memorySampler = setInterval(() => {
    try { memorySamples.push(getProcessSample(host.pid)); } catch { /* A process can exit while a sample is queued. */ }
  }, 1500);
}

async function closeHost() {
  if (memorySampler) clearInterval(memorySampler);
  memorySampler = null;
  if (browser) await browser.close().catch(() => {});
  browser = null;
  if (host && host.exitCode === null) {
    stopHostWindow(host.pid);
    const result = await waitForExit(host, 20000);
    assert.equal(result.code, 0, `WPF host exited unexpectedly: ${JSON.stringify(result)}`);
  }
  host = null;
  page = null;
}

async function waitForConversation() {
  const conversation = page.getByRole("region", { name: "Conversation" });
  await conversation.waitFor();
  return conversation;
}

async function openMiddleAssistantPage(conversation) {
  const note = conversation.locator("article.message-assistant .output-truncation-note").last();
  const inspectButton = note.getByRole("button", { name: "Inspect saved output" });
  assert.equal(await inspectButton.count(), 1,
    `assistant output inspection was unavailable; captured host metadata=${JSON.stringify(await page.evaluate(() => window.__p2_11Evidence ?? []))}`);
  await inspectButton.click();
  const drawer = page.getByRole("dialog", { name: "Inspect output" });
  await drawer.waitFor();
  const content = drawer.getByRole("region", { name: "Saved output page" }).locator("pre");
  await content.getByText(assistantStart, { exact: false }).waitFor();
  assert.ok(!(await content.innerText()).includes(assistantMiddle), "the middle marker should be outside the initial inspection page");
  let pageCount = 0;
  while (!(await content.innerText()).includes(assistantMiddle) && pageCount < 12) {
    const priorPage = await content.innerText();
    await drawer.getByRole("button", { name: "Next" }).click();
    await page.waitForFunction((previous) => {
      const current = document.querySelector(".output-inspection-content");
      return current && current.innerText !== previous;
    }, priorPage, { timeout: 30000 });
    pageCount++;
  }
  assert.ok((await content.innerText()).includes(assistantMiddle), "paged native inspection did not reach retained assistant middle content");
  assert.ok((await content.innerText()).length <= 32_768, "the host returned more than one bounded inspection page");
  return { drawer, content, pagesAdvanced: pageCount };
}

async function inspectLargeDocumentToolOutput(conversation) {
  const activity = conversation.locator(".activity-card").last();
  const note = activity.locator(".output-truncation-note");
  assert.match(await note.innerText(), /characters omitted from this preview/i,
    "the activity card did not disclose that its saved-output preview was shortened");
  const inspectButton = note.getByRole("button", { name: "Inspect saved output" });
  assert.equal(await inspectButton.count(), 1, "the retained tool-output head/tail was not inspectable");
  await inspectButton.click();
  const drawer = page.getByRole("dialog", { name: "Inspect output" });
  await drawer.waitFor();
  await page.waitForFunction(() => Boolean(document.querySelector(".output-page-position"))
    || Boolean(document.querySelector(".review-unavailable[role=note]")), null, { timeout: 30000 });
  const content = drawer.getByRole("region", { name: "Saved output page" }).locator("pre");
  const position = drawer.locator(".output-page-position");
  const unavailableNote = drawer.locator(".review-unavailable[role=note]");
  await unavailableNote.waitFor({ timeout: 30000 });
  const unavailableText = await unavailableNote.innerText();
  assert.match(unavailableText, /not proof|cannot (?:confirm|verify)|not available/i,
    "the inspector did not explain that source output completeness may be unknown");
  await content.getByText(documentStart, { exact: false }).waitFor({ timeoutMs: 30000 });
  let expectedOffset = 0;
  let totalCharacters = null;
  let firstRange = null;
  let lastRange = null;
  let pagesVisited = 0;
  let documentMiddleRecovered = false;
  let documentEndRecovered = false;
  const retainedPages = [];
  while (pagesVisited < 256) {
    const positionText = await position.innerText();
    const match = positionText.match(/^Characters\s+([\d,]+)[–-]([\d,]+)\s+of\s+([\d,]+)$/);
    assert.ok(match, `saved-output inspector omitted a parseable exact range: ${positionText}`);
    const range = {
      start: Number(match[1].replaceAll(",", "")),
      end: Number(match[2].replaceAll(",", "")),
      total: Number(match[3].replaceAll(",", "")),
    };
    const text = await content.innerText();
    retainedPages.push(text);
    assert.equal(range.start, expectedOffset, "saved-output pages were missing, duplicated, or out of order");
    assert.equal(text.length, range.end - range.start + 1, "saved-output page bounds did not match the visible range");
    assert.ok(text.length <= 32_768, "the host returned more than the bounded saved-output page size");
    documentMiddleRecovered ||= text.includes(documentMiddle);
    documentEndRecovered ||= text.includes(documentEnd);
    if (pagesVisited === 0) {
      firstRange = range;
      assert.ok(text.includes(documentStart), "the first retained output page omitted the document start marker");
    }
    if (totalCharacters !== null) assert.equal(range.total, totalCharacters, "saved-output total changed between pages");
    totalCharacters = range.total;
    lastRange = range;
    expectedOffset = range.end + 1;
    pagesVisited++;
    if (documentEndRecovered) break;
    const next = drawer.getByRole("button", { name: "Next" });
    assert.ok(await next.isEnabled(), "the retained document tail was unavailable before the saved item ended");
    await next.click();
    await page.waitForFunction((previous) => document.querySelector(".output-page-position")?.innerText !== previous,
      positionText, { timeout: 30000 });
  }
  assert.ok(pagesVisited < 256, "saved-output inspection exceeded the App Server page safety cap");
  assert.ok(pagesVisited > 1, "the retained large-document output did not exercise multiple bounded pages");
  assert.equal(documentMiddleRecovered, false,
    "the fixture's middle marker should remain unavailable when App Server reports an upstream omission");
  assert.ok(documentEndRecovered, "the final retained range did not recover the document end marker");
  const retainedOutput = retainedPages.join("");
  const omissionMatch = retainedOutput.match(/\.\.\.\s+([\d,]+)\s+bytes omitted\s+\.\.\./);
  assert.ok(totalCharacters < largeDocumentText.length,
    "App Server's saved output unexpectedly covered the complete deterministic source document");
  if (omissionMatch) {
    assert.match(unavailableText, /App Server marks this saved tool output as truncated upstream/i,
      "a detected upstream omission was not explicitly reported to the user");
  } else {
    assert.match(unavailableText, /no recognized upstream omission marker/i,
      "the unknown upstream completeness state was presented as confirmed complete");
  }
  return {
    drawer, pagesVisited, totalCharacters, firstRange, lastRange,
    sourceDocumentCharacters: largeDocumentText.length,
    documentMiddleRecovered, documentEndRecovered,
    upstreamOmissionMarkerPresent: omissionMatch !== null,
    upstreamCompletenessDisclosure: unavailableText,
  };
}

const fixturePort = await listen(fixtureServer);
capability.endpoint = `http://127.0.0.1:${fixturePort}/v1`;
let hostClosed = false;
let capabilityHash;
try {
  writeFileSync(capabilityPath, `${JSON.stringify(capability, null, 2)}\n`, { encoding: "utf8", flag: "wx" });
  capabilityHash = createHash("sha256").update(readFileSync(capabilityPath)).digest("hex");
  const prepared = spawnSync(runnerPath, [
    "--prepare-p2-11-native-output-qa", applicationRoot, workspace, capabilityPath, manifestPath,
  ], { cwd: sourceRoot, encoding: "utf8", timeout: 120000, windowsHide: true, maxBuffer: 2 * 1024 * 1024 });
  if (prepared.error) throw prepared.error;
  assert.equal(prepared.status, 0, `isolated fixture preparation failed: ${prepared.stderr || prepared.stdout}`);
  const readyLine = prepared.stdout.split(/\r?\n/).find((line) => line.startsWith("P2_11_NATIVE_QA_READY="));
  assert.ok(readyLine, `fixture preparation omitted its manifest: ${prepared.stdout}`);
  const manifest = JSON.parse(readyLine.slice("P2_11_NATIVE_QA_READY=".length));
  assert.equal(path.resolve(manifest.applicationRoot).toLowerCase(), applicationRoot.toLowerCase());
  assert.equal(path.resolve(manifest.workspace).toLowerCase(), workspace.toLowerCase());
  assert.ok(isWithin(runRoot, applicationRoot) && isWithin(runRoot, workspace),
    "application and fixture workspace must stay inside the QA run");
  assert.equal(manifest.runtimeSha256, runtimeHash);
  assert.ok(isWithin(workspace, documentPath), "the large test document escaped its isolated workspace");
  writeFileSync(documentPath, largeDocumentText, { encoding: "utf8", flag: "wx" });
  assert.equal(readFileSync(documentPath).length, largeOutputCharacters,
    "the generated large document was not written at its expected ASCII byte length");

  await launchHost();
  const diagnosticsButton = page.getByRole("button", { name: "Open diagnostics" });
  await diagnosticsButton.click();
  const diagnostics = page.getByRole("dialog", { name: "Runtime diagnostics" });
  await diagnostics.waitFor();
  const runtimeEvidence = await diagnostics.innerText();
  assert.ok(runtimeEvidence.includes(manifest.runtimeVersion), "runtime diagnostics omitted the pinned App Server version");
  assert.ok(runtimeEvidence.includes(manifest.runtimeRevision), "runtime diagnostics omitted the pinned App Server revision");
  assert.ok(runtimeEvidence.includes(manifest.runtimeSha256), "runtime diagnostics omitted the pinned App Server hash");
  assert.ok(runtimeEvidence.includes(modelIdentifier), "runtime diagnostics omitted the exact synthetic selected model");
  assert.ok(runtimeEvidence.includes(applicationRoot), "runtime diagnostics omitted the isolated application root");
  assert.ok(runtimeEvidence.includes(path.join(applicationRoot, "Data")), "runtime state is not under the application Data root");
  assert.match(runtimeEvidence, /Ordinary Codex root used\s+false/i, "ordinary Codex root isolation is not visible");
  await diagnostics.getByRole("button", { name: "Done" }).click();

  const conversation = await waitForConversation();
  await page.evaluate(() => {
    window.__p2_11Evidence = [];
    window.chrome?.webview?.addEventListener("message", (event) => {
      const message = event.data;
      const item = message?.params?.item;
      const display = item?.neoBabylonDisplay ?? message?.params?.neoBabylonDisplay ?? message?.result?.assistantDisplay;
      window.__p2_11Evidence.push({
        method: message?.method ?? null,
        itemType: item?.type ?? message?.params?.itemType ?? null,
        itemId: item?.id ?? message?.params?.itemId ?? null,
        sourceRetained: display?.sourceRetained ?? null,
        omittedCharacters: display?.omittedCharacters ?? null,
        resultAssistantItemId: message?.result?.assistantItemId ?? null,
      });
    });
  });
  await page.getByRole("textbox", { name: "Message NeoBabylon" }).fill(firstPrompt);
  mainPromptSentAt = Date.now();
  await page.getByRole("button", { name: "Send message" }).click();
  await conversation.locator("article.message-user .message-text").getByText(firstPrompt, { exact: true }).waitFor();
  const assistant = conversation.locator("article.message-assistant").last();
  await assistant.locator(".output-truncation-note").waitFor({ timeout: 90000 });
  await page.waitForFunction(() => {
    const state = document.querySelector("section.conversation")?.getAttribute("data-turn-state");
    return state === "completed" || state === "failed" || state === "interrupted";
  }, null, { timeout: 90000 });
  largeOutputVisibleAt = Date.now();
  assert.equal(await page.locator("section.conversation").getAttribute("data-turn-state"), "completed");
  const assistantPreview = await assistant.locator(".message-text").innerText();
  assert.ok(assistantPreview.length <= 120_100, `assistant preview exceeded its bound: ${assistantPreview.length}`);
  assert.ok(assistantPreview.includes(assistantStart), "bounded assistant preview omitted the start attribution marker");
  assert.ok(!assistantPreview.includes(assistantMiddle), "the test marker should be in output omitted from the main preview");
  assert.match(await assistant.locator(".output-truncation-note").innerText(), /980\s*,?\s*000 characters are omitted/i);

  const toolActivity = conversation.locator(".activity-card").last();
  await toolActivity.waitFor();
  assert.equal((await toolActivity.locator(".activity-status-label").innerText()).toLowerCase(), "succeeded");
  assert.match(await toolActivity.locator(".activity-title").innerText(), /command/i);
  assert.ok(await toolActivity.locator(".output-truncation-note").count() > 0,
    "the large App Server command output did not disclose its bounded UI preview");
  assert.equal(providerRequests.length, 2, "one tool call and its continuation should use exactly two fixture requests");
  assert.ok(providerRequests[1].containsLargeToolOutput && providerRequests[1].containsToolTail,
    "the App Server did not continue the same turn with the ordinary tool's result");
  assert.ok(providerRequests[1].containsDocumentStart,
    "the permitted command result did not contain the start marker from the isolated large document");
  assert.ok(providerRequests[1].containsDocumentEnd,
    "the permitted command result did not contain the end marker from the isolated large document");
  assert.ok(!providerRequests[1].containsDocumentMiddle,
    "the marker in the App Server-omitted document range unexpectedly reached the continuation");
  assert.ok(providerRequests[1].containsUpstreamOmissionMarker,
    "the provider continuation did not disclose App Server's own output truncation");

  const documentInspection = await inspectLargeDocumentToolOutput(conversation);
  await page.screenshot({ path: screenshots.documentInspected });
  await documentInspection.drawer.getByRole("button", { name: "Done" }).click();

  assert.equal(fixtureErrors.length, 0, fixtureErrors.join("; "));
  assert.ok(assistantStreamStartedAt && assistantStreamCompletedAt);
  const hostOutputEvidence = await page.evaluate(() => window.__p2_11Evidence ?? []);
  const turnEvidence = {
    turnState: await page.locator("section.conversation").getAttribute("data-turn-state"),
    assistantMetadata: await assistant.locator(".output-truncation-note").innerText(),
    assistantDisplayCharacters: assistantPreview.length,
    toolStatus: await toolActivity.locator(".activity-status-label").innerText(),
    hostOutputEvidence,
  };
  writeFileSync(path.join(runRoot, "large-turn-evidence.json"), `${JSON.stringify(turnEvidence, null, 2)}\n`, { encoding: "utf8", flag: "wx" });
  console.log(`P2_11_NATIVE_LARGE_TURN=${JSON.stringify(turnEvidence)}`);
  await page.screenshot({ path: screenshots.completed });

  let inspection = await openMiddleAssistantPage(conversation);
  await page.screenshot({ path: screenshots.inspected });
  const inspectionEvidence = {
    pagesAdvanced: inspection.pagesAdvanced,
    visibleMarker: assistantMiddle,
    pageCharacters: (await inspection.content.innerText()).length,
  };
  await inspection.drawer.getByRole("button", { name: "Done" }).click();

  memorySamples.push(getProcessSample(host.pid));
  await page.reload();
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  let restoredConversation = await waitForConversation();
  await restoredConversation.locator("article.message-user .message-text").getByText(firstPrompt, { exact: true }).waitFor({ timeout: 30000 });
  const restoredAssistant = restoredConversation.locator("article.message-assistant").last();
  await restoredAssistant.locator(".output-truncation-note").waitFor({ timeout: 30000 });
  assert.ok(await restoredConversation.locator(".activity-card").count() > 0, "saved tool outcome did not reconstruct after renderer reload");
  assert.equal(providerRequests.length, 2, "renderer reload must not replay a provider request");
  const restoredInspection = await openMiddleAssistantPage(restoredConversation);
  await page.screenshot({ path: screenshots.restored });
  await restoredInspection.drawer.getByRole("button", { name: "Done" }).click();

  await page.getByRole("textbox", { name: "Message NeoBabylon" }).fill(cancellationPrompt);
  cancellationPromptSentAt = Date.now();
  await page.getByRole("button", { name: "Send message" }).click();
  await restoredConversation.locator("article.message-user .message-text").getByText(cancellationPrompt, { exact: true }).waitFor();
  const cancellationAssistant = restoredConversation.locator("article.message-assistant").last();
  await cancellationAssistant.locator(".message-text").getByText(partialCancellationText, { exact: true }).waitFor({ timeout: 30000 });
  await page.getByRole("button", { name: "Stop turn" }).click();
  await page.waitForFunction(() => document.querySelector("section.conversation")?.getAttribute("data-turn-state") === "interrupted", null,
    { timeout: 45000 });
  cancellationVisibleAt = Date.now();
  assert.equal(await cancellationAssistant.locator(".message-text").innerText(), partialCancellationText,
    "cancelling a streamed response discarded or replaced its partial assistant text");
  assert.equal(await restoredConversation.locator("article.message-assistant.message-failed").count(), 0,
    "a user-requested cancellation was mislabeled as a provider failure");
  assert.equal(providerRequests.length, 3, "cancellation must not trigger a hidden retry or provider/model fallback");
  await waitFor(() => cancellationClientClosedAt !== null, "App Server did not cancel its live provider request", 30000);
  await page.screenshot({ path: screenshots.cancelled });
  await page.reload();
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  restoredConversation = await waitForConversation();
  await restoredConversation.locator("article.message-user .message-text").getByText(cancellationPrompt, { exact: true }).waitFor({ timeout: 30000 });
  assert.equal(await restoredConversation.locator("article.message-assistant .message-text").getByText(partialCancellationText, { exact: true }).count(), 0,
    "the pinned App Server unexpectedly restored streamed text that it did not save");
  await restoredConversation.locator(".turn-status-line").filter({ hasText: /Only server-saved content can be restored/i })
    .waitFor({ timeout: 30000 });
  await page.screenshot({ path: screenshots.cancellationRestored });
  assert.equal(providerRequests.length, 3, "renderer reload replayed the cancelled provider request");

  await page.getByRole("textbox", { name: "Message NeoBabylon" }).fill(failurePrompt);
  failurePromptSentAt = Date.now();
  await page.getByRole("button", { name: "Send message" }).click();
  await restoredConversation.locator("article.message-user .message-text").getByText(failurePrompt, { exact: true }).waitFor();
  await restoredConversation.locator("article.message-assistant.message-failed").last().waitFor({ timeout: 45000 });
  const failedAssistant = restoredConversation.locator("article.message-assistant.message-failed").last();
  await failedAssistant.locator(".message-text").getByText(partialFailureText, { exact: true }).waitFor();
  await page.locator(".turn-error[role=alert]").filter({ hasText: /P2-11 deterministic late provider failure/i }).waitFor({ timeout: 45000 });
  const failureVisibleAt = Date.now();
  assert.equal(await page.locator("section.conversation").getAttribute("data-turn-state"), "failed");
  assert.equal(providerRequests.length, 4, "a non-retryable late provider failure should be one additional request");
  assert.ok(providerRequests.every((request) => request.model === modelIdentifier), "provider retry silently selected a different model");
  assert.equal(fixtureErrors.length, 0, fixtureErrors.join("; "));
  assert.equal(pageIssues.length, 0, pageIssues.join("; "));
  await page.screenshot({ path: screenshots.failed });
  memorySamples.push(getProcessSample(host.pid));
  await closeHost();
  hostClosed = true;

  const allProcesses = memorySamples.flatMap((sample) => sample.processes);
  const processMemory = ["WPF host", "pinned App Server"].map((role) => {
    const rows = allProcesses.filter((item) => item.role === role);
    return {
      role,
      samples: rows.length,
      maximumObservedWorkingSetBytes: rows.length ? Math.max(...rows.map((item) => item.workingSetBytes)) : null,
      maximumObservedPrivateBytes: rows.length ? Math.max(...rows.map((item) => item.privateBytes)) : null,
      finalObservedWorkingSetBytes: rows.at(-1)?.workingSetBytes ?? null,
    };
  });
  assert.ok(processMemory.find((item) => item.role === "pinned App Server")?.samples > 0,
    "no memory sample was collected from the exact pinned App Server child process");
  const result = {
    slice: "P2-11",
    result: "native-deterministic-proof-passed; live-model-size-proof-not-run",
    evidenceKind: "Native WPF + pinned Codex App Server + deterministic local Responses fixture; no live inference/model weights",
    runtime: {
      version: manifest.runtimeVersion,
      revision: manifest.runtimeRevision,
      sha256: manifest.runtimeSha256,
      binaryPath: manifest.runtimeBinaryPath,
    },
    capability: {
      providerId: capability.providerId,
      modelIdentifier,
      endpoint: capability.endpoint,
      modelWeights: false,
      toolCall: "exec_command with isolated QA workspace and successful captured result",
    },
    roots: { sourceRoot, applicationRoot, dataRoot: path.join(applicationRoot, "Data"), workspace },
    output: {
      assistantCharacters: largeAssistantText.length,
      assistantDisplayCharacters: assistantPreview.length,
      assistantOmittedCharacters: largeAssistantText.length - assistantPreview.length,
      toolCommandOutputCharactersRequested: largeOutputCharacters,
      toolOutputUpstreamOmissionMarkerForwardedToContinuation: providerRequests[1].containsUpstreamOmissionMarker,
      inspectionEvidence,
      largeDocumentToolRead: {
        isolatedRelativePath: path.relative(workspace, documentPath),
        characters: largeDocumentText.length,
        sha256: documentSha256,
        middleRangeRetainedByAppServer: false,
        retainedOutputPages: documentInspection.pagesVisited,
        retainedCharacters: documentInspection.totalCharacters,
        upstreamOmissionMarkerPresent: documentInspection.upstreamOmissionMarkerPresent,
        sourceCompletenessExplicitlyQualified: true,
        firstRange: documentInspection.firstRange,
        lastRange: documentInspection.lastRange,
        documentMiddleRecoveredFromSavedOutput: documentInspection.documentMiddleRecovered,
        documentEndRecoveredFromSavedOutput: documentInspection.documentEndRecovered,
      },
      savedHistoryAndInspectionReconstructedAfterWebViewReload: true,
      partialAssistantVisibleBeforeReloadButNotPersistedByPinnedAppServer: true,
      unsavedPartialResponseLimitationDisclosedAfterReload: true,
      partialAssistantRetainedAndTypedAfterProviderFailure: true,
      partialCancellationText,
      partialFailureText,
      hostOutputEvidence,
    },
    timingMs: {
      promptToCompletedVisible: largeOutputVisibleAt - mainPromptSentAt,
      providerLargeStream: assistantStreamCompletedAt - assistantStreamStartedAt,
      failurePromptToTypedFailureVisible: failureVisibleAt - failurePromptSentAt,
      cancellationPromptToInterruptionVisible: cancellationVisibleAt - cancellationPromptSentAt,
      providerRequestClosedAfterCancellationMs: cancellationClientClosedAt - cancellationPromptSentAt,
    },
    processMemory,
    providerRequests,
    runtimeEvidence,
    screenshots,
    pageIssues,
    fixtureErrors,
    runRoot,
  };
  writeFileSync(resultPath, `${JSON.stringify(result, null, 2)}\n`, { encoding: "utf8", flag: "wx" });
  console.log(`P2_11_NATIVE result=pass assistantChars=${largeAssistantText.length} assistantPreviewChars=${assistantPreview.length} inspectPages=${inspectionEvidence.pagesAdvanced} P3_04_documentPages=${documentInspection.pagesVisited} requests=${providerRequests.length} cancellation=partial-visible-before-reload-not-saved-disclosed partialFailure=typed-and-visible runtime=${manifest.runtimeVersion} sha256=${manifest.runtimeSha256} runRoot=${runRoot}`);
  console.log(`P2_11_NATIVE_RESULT=${resultPath}`);
} finally {
  if (!hostClosed) await closeHost().catch(() => {});
  await new Promise((resolve) => fixtureServer.close(() => resolve()));
  if (existsSync(capabilityPath) && capabilityHash
    && createHash("sha256").update(readFileSync(capabilityPath)).digest("hex") === capabilityHash) {
    unlinkSync(capabilityPath);
  }
}
