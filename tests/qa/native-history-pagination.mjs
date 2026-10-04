import assert from "node:assert/strict";
import { spawn, spawnSync } from "node:child_process";
import { createServer } from "node:net";
import { createInterface } from "node:readline";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { assertQaRunsParent, newQaRun } from "./qa-run-root.mjs";

const [runnerPathArg, hostPathArg, playwrightModulePathArg, qaParentArg] = process.argv.slice(2);
assert.ok(runnerPathArg && hostPathArg && playwrightModulePathArg && qaParentArg,
  "usage: node native-history-pagination.mjs <test-runner.exe> <host.exe> <playwright-core-entry.mjs> <exact .local\\Lab\\Runs parent>");

const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const runnerPath = path.resolve(runnerPathArg);
const hostPath = path.resolve(hostPathArg);
const playwrightModulePath = path.resolve(playwrightModulePathArg);
const qaParent = path.resolve(qaParentArg);
assertQaRunsParent(sourceRoot, qaParent);

for (const [label, filePath] of [["test runner", runnerPath], ["WPF host", hostPath], ["Playwright module", playwrightModulePath]]) {
  assert.ok(existsSync(filePath), `${label} does not exist: ${filePath}`);
}

const { runRoot, applicationRoot } = newQaRun(qaParent, `P2-P01-native-${Date.now()}-${process.pid}`);
const manifestPath = path.join(runRoot, "history-manifest.json");
const screenshotPath = path.join(runRoot, "native-history-pagination.png");
const desktopScreenshotPath = path.join(runRoot, "p2-10-native-desktop.png");
const compactScreenshotPath = path.join(runRoot, "p2-10-native-compact.png");

function reservePort() {
  return new Promise((resolve, reject) => {
    const server = createServer();
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

function stopHostWindow(pid) {
  const command = `$process = Get-Process -Id ${pid} -ErrorAction Stop; if (-not $process.CloseMainWindow()) { exit 2 }; exit 0`;
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", command], { encoding: "utf8", timeout: 10000 });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`could not request a graceful close for QA host PID ${pid}: ${result.stderr || result.stdout}`);
}

function resizeHostWindow(pid, width, height) {
  const command = `
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class P210WindowSize {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
  [DllImport("user32.dll", SetLastError=true)] public static extern bool GetWindowRect(IntPtr handle, out RECT rect);
  [DllImport("user32.dll", SetLastError=true)] public static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
}
'@
$process = Get-Process -Id ${pid} -ErrorAction Stop
$handle = $process.MainWindowHandle
if ($handle -eq 0) { throw 'QA WPF host has no main window.' }
$rect = New-Object P210WindowSize+RECT
if (-not [P210WindowSize]::GetWindowRect($handle, [ref]$rect)) { throw 'GetWindowRect failed.' }
if (-not [P210WindowSize]::SetWindowPos($handle, [IntPtr]::Zero, $rect.Left, $rect.Top, ${width}, ${height}, [uint32]0x0014)) { throw 'SetWindowPos failed.' }
`;
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", command], {
    encoding: "utf8", timeout: 20000,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`could not resize QA host PID ${pid}: ${result.stderr || result.stdout}`);
}

async function waitForCdp(port, child) {
  const deadline = Date.now() + 45000;
  while (Date.now() < deadline) {
    if (child.exitCode !== null) throw new Error(`WPF host exited before CDP opened (code ${child.exitCode})`);
    try {
      const response = await fetch(`http://127.0.0.1:${port}/json/version`, { signal: AbortSignal.timeout(1000) });
      if (response.ok) return await response.json();
    } catch { /* WebView2 is still starting. */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`WebView2 CDP did not open on port ${port}`);
}

async function waitForNativePage(browser, port, child) {
  const deadline = Date.now() + 30000;
  let targets = [];
  while (Date.now() < deadline) {
    if (child.exitCode !== null) throw new Error(`WPF host exited before its WebView page appeared (code ${child.exitCode})`);
    const page = browser.contexts().flatMap((context) => context.pages())
      .find((candidate) => candidate.url().startsWith("https://neobabylon.local/"));
    if (page) return page;
    try {
      const response = await fetch(`http://127.0.0.1:${port}/json/list`, { signal: AbortSignal.timeout(1000) });
      if (response.ok) targets = await response.json();
    } catch { /* The native WebView target may not exist yet. */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`the isolated native NeoBabylon page was not found; CDP targets=${JSON.stringify(targets)}`);
}

async function keyboardActivateReadyButton(page, button, description) {
  const handle = await button.elementHandle();
  assert.ok(handle, `${description} must exist before keyboard activation`);
  try {
    await page.waitForFunction((element) => element instanceof HTMLButtonElement && !element.disabled,
      handle, { timeout: 30000 });
  } catch (error) {
    const state = await button.evaluate((element) => ({
      text: element.textContent?.trim(),
      disabled: element.disabled,
      current: element.getAttribute("aria-current"),
    }));
    throw new Error(`${description} did not become enabled: ${JSON.stringify(state)}; ${error.message}`);
  }
  await button.focus();
  assert.equal(await button.evaluate((element) => document.activeElement === element), true,
    `${description} should receive keyboard focus before activation`);
  await page.keyboard.press("Enter");
}

const runnerEnv = { ...process.env };
for (const name of ["OPENROUTER_API_KEY", "OPENAI_API_KEY", "NEOBABYLON_WINDOWS_SANDBOX_MODE"]) delete runnerEnv[name];
const runner = spawn(runnerPath, ["--prepare-history-pagination-qa", applicationRoot, manifestPath], {
  cwd: sourceRoot,
  env: runnerEnv,
  stdio: ["pipe", "pipe", "pipe"],
  windowsHide: true,
});
let runnerStderr = "";
runner.stderr.setEncoding("utf8");
runner.stderr.on("data", (chunk) => { runnerStderr += chunk; });
const lines = [];
const readyLine = new Promise((resolve, reject) => {
  const timeout = setTimeout(() => reject(new Error(`history QA fixture did not become ready; stderr=${runnerStderr}`)), 5 * 60 * 1000);
  const output = createInterface({ input: runner.stdout });
  output.on("line", (line) => {
    lines.push(line);
    if (line.startsWith("HISTORY_PAGINATION_QA_PROGRESS=")) console.log(line);
    if (line.startsWith("HISTORY_PAGINATION_QA_READY=")) {
      clearTimeout(timeout);
      resolve(JSON.parse(line.slice("HISTORY_PAGINATION_QA_READY=".length)));
    }
  });
  runner.once("error", (error) => { clearTimeout(timeout); reject(error); });
  runner.once("exit", (code) => {
    if (!lines.some((line) => line.startsWith("HISTORY_PAGINATION_QA_READY="))) {
      clearTimeout(timeout);
      reject(new Error(`history QA fixture exited before readiness (code ${code}); stderr=${runnerStderr}; stdout=${lines.join("\n")}`));
    }
  });
});

let host;
let browser;
let observedPageIssues = [];
let hostClosed = false;
let resultSummary = null;
try {
  const manifest = await readyLine;
  assert.equal(path.resolve(manifest.sourceRoot).toLowerCase(), sourceRoot.toLowerCase());
  assert.equal(path.resolve(manifest.applicationRoot).toLowerCase(), applicationRoot.toLowerCase());
  assert.equal(manifest.expectedThreadCount, 56);
  assert.equal(manifest.providerRequestsBeforeNativeOpen, 56);
  assert.equal(manifest.firstPageIds.length + manifest.secondPageIds.length, manifest.expectedThreadCount);
  assert.equal(new Set([...manifest.firstPageIds, ...manifest.secondPageIds]).size, manifest.expectedThreadCount);

  const capability = JSON.parse(readFileSync(manifest.capabilityRecordPath, "utf8"));
  const expectedProvider = capability.providerId;
  const expectedModel = capability.modelIdentifier;
  const expectedPrompts = [...manifest.firstPageIds, ...manifest.secondPageIds]
    .map((threadId) => manifest.promptByThreadId[threadId]);
  assert.equal(expectedPrompts.length, manifest.expectedThreadCount);
  assert.equal(expectedPrompts[0], manifest.newestPrompt);
  assert.equal(expectedPrompts.at(-1), manifest.oldestPrompt);

  const debugPort = await reservePort();
  const hostEnv = { ...process.env };
  for (const name of ["OPENROUTER_API_KEY", "OPENAI_API_KEY", "NEOBABYLON_WINDOWS_SANDBOX_MODE"]) delete hostEnv[name];
  Object.assign(hostEnv, {
    NEOBABYLON_SOURCE_ROOT: sourceRoot,
    NEOBABYLON_APPLICATION_ROOT: applicationRoot,
    NEOBABYLON_MODEL_CAPABILITY_PATH: manifest.capabilityRecordPath,
    NEOBABYLON_ENABLE_WEBVIEW2_REMOTE_DEBUG: "1",
    NEOBABYLON_WEBVIEW2_DEBUG_PORT: String(debugPort),
  });
  host = spawn(hostPath, [], { cwd: sourceRoot, env: hostEnv, stdio: "ignore", windowsHide: false });
  await waitForCdp(debugPort, host);

  const { chromium } = await import(pathToFileURL(playwrightModulePath).href);
  browser = await chromium.connectOverCDP(`http://127.0.0.1:${debugPort}`);
  const page = await waitForNativePage(browser, debugPort, host);
  page.on("pageerror", (error) => observedPageIssues.push(error.message));
  page.on("console", (message) => {
    if (message.type() === "error") observedPageIssues.push(message.text());
  });

  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  assert.equal(await page.title(), "NeoBabylon");
  assert.equal(await page.locator("vite-error-overlay").count(), 0);
  resizeHostWindow(host.pid, 1440, 900);
  await page.waitForFunction(() => innerWidth > 1100 && innerHeight > 700);
  const desktopViewport = await page.evaluate(() => ({ width: innerWidth, height: innerHeight }));
  await page.waitForFunction((count) => document.querySelectorAll("button.saved-chat").length === count,
    manifest.firstPageCount, { timeout: 30000 });
  const modelPicker = page.locator("button.model-select");
  await modelPicker.focus();
  await page.keyboard.press("Enter");
  const capabilityList = page.getByRole("listbox", { name: "VERIFIED CAPABILITY RECORDS" });
  await capabilityList.waitFor();
  await page.waitForFunction(() => document.activeElement?.getAttribute("role") === "listbox");
  assert.equal(await capabilityList.evaluate((element) => getComputedStyle(element).outlineWidth), "3px",
    "the native WebView2 capability picker should expose its keyboard focus ring");
  const activeOption = await capabilityList.getAttribute("aria-activedescendant");
  assert.ok(activeOption, "the model list should expose its keyboard-active option");
  assert.equal(await page.locator(`#${activeOption}`).getAttribute("aria-selected"), "true",
    "opening the model list should initialize keyboard navigation to the exact selected capability");
  await page.keyboard.press("ArrowDown");
  const optionCount = await capabilityList.getByRole("option").count();
  const expectedNextOption = `model-option-${(Number(activeOption.split("-").at(-1)) + 1) % optionCount}`;
  assert.equal(await capabilityList.getAttribute("aria-activedescendant"), expectedNextOption,
    "ArrowDown should advance the native model listbox active option");
  assert.equal(await page.locator(`#${activeOption}`).getAttribute("aria-selected"), "true",
    "arrow navigation alone must not switch the selected model");
  await page.keyboard.press("Escape");
  await page.waitForFunction(() => document.activeElement?.classList.contains("model-select"));
  assert.equal(await modelPicker.getAttribute("aria-expanded"), "false",
    "Escape should close the native model picker without changing the selected capability");
  await page.screenshot({ path: desktopScreenshotPath });

  const diagnosticsButton = page.getByRole("button", { name: "Open diagnostics" });
  await diagnosticsButton.focus();
  await page.keyboard.press("Enter");
  const diagnosticsDialog = page.getByRole("dialog", { name: "Runtime diagnostics" });
  await diagnosticsDialog.waitFor();
  assert.equal(await page.locator("main").evaluate((element) => element.inert), true,
    "the native diagnostics dialog should inert the background work area");
  await page.waitForFunction(() => document.activeElement?.getAttribute("aria-label") === "Close diagnostics");
  const diagnosticDetails = diagnosticsDialog.getByRole("region", { name: "Runtime diagnostic details" });
  const capabilitySearch = diagnosticsDialog.getByRole("searchbox", { name: "Search tools and capabilities" });
  const diagnosticsDone = diagnosticsDialog.getByRole("button", { name: "Done" });
  await page.keyboard.press("Tab");
  assert.equal(await diagnosticDetails.evaluate((element) => document.activeElement === element), true,
    "native WebView2 Tab should reach the named keyboard stop for scrollable diagnostics");
  await page.keyboard.press("Tab");
  assert.equal(await capabilitySearch.evaluate((element) => document.activeElement === element), true,
    "native WebView2 Tab should reach the categorized capability search");
  const capabilityCategories = diagnosticsDialog.locator(".tool-catalog-category > summary");
  const capabilityCategoryCount = await capabilityCategories.count();
  assert.equal(capabilityCategoryCount, 13,
    "the native diagnostics keyboard path should expose all 13 capability-category disclosures");
  for (let index = 0; index < capabilityCategoryCount; index++) {
    await page.keyboard.press("Tab");
    assert.equal(await capabilityCategories.nth(index).evaluate((element) => document.activeElement === element), true,
      `native WebView2 Tab should reach capability category ${index + 1} in order`);
  }
  await page.keyboard.press("Tab");
  assert.equal(await diagnosticsDone.evaluate((element) => document.activeElement === element), true,
    "native WebView2 Tab should move from the last capability category to Done");
  await page.keyboard.press("Tab");
  assert.equal(await diagnosticsDialog.locator("button").first().evaluate((element) => document.activeElement === element), true,
    "native WebView2 Tab should wrap from the last diagnostics control to the first");
  await page.keyboard.press("Shift+Tab");
  assert.equal(await diagnosticsDialog.locator("button").last().evaluate((element) => document.activeElement === element), true,
    "native WebView2 Shift+Tab should wrap from the first diagnostics control to the last");
  await page.keyboard.press("Escape");
  await diagnosticsDialog.waitFor({ state: "detached" });
  await page.waitForFunction(() => document.activeElement?.getAttribute("aria-label") === "Open diagnostics");
  assert.equal(await diagnosticsButton.evaluate((element) => document.activeElement === element), true,
    "closing native diagnostics should restore focus to its original opener");

  resizeHostWindow(host.pid, 960, 720);
  await page.waitForFunction(() => innerWidth < 1050 && innerHeight < 750);
  const compactViewport = await page.evaluate(() => ({ width: innerWidth, height: innerHeight }));
  assert.ok(compactViewport.width < desktopViewport.width && compactViewport.height < desktopViewport.height,
    "the compact journey should run in a smaller real WPF window");
  await page.screenshot({ path: compactScreenshotPath });

  await page.waitForFunction((count) => document.querySelectorAll("button.saved-chat").length === count,
    manifest.firstPageCount, { timeout: 30000 });
  const firstPageRows = await page.locator("button.saved-chat").allInnerTexts();
  assert.ok(firstPageRows[0]?.includes(manifest.newestPrompt),
    `the first loaded history page should start with the newest task, got: ${firstPageRows[0] ?? "<empty>"}`);
  assert.ok(await page.locator(".history-load-more").isVisible(), "older history must be reachable from the first page");

  const search = page.getByRole("searchbox", { name: "Search loaded conversations" });
  await search.focus();
  await page.keyboard.type(manifest.oldestPrompt);
  const searchMissStatus = page.getByRole("status").filter({ hasText: "No matches in loaded conversations" });
  await searchMissStatus.waitFor();
  assert.equal(await page.locator("button.saved-chat").count(), 0,
    "an older task outside loaded pages must not be implied to be absent from all history");
  assert.match(await searchMissStatus.innerText(), /Load older conversations to search more/i);

  await page.locator(".history-load-more").focus();
  await page.keyboard.press("Enter");
  await page.waitForFunction((count) => document.querySelector('[aria-label^="Saved conversations"]')?.getAttribute("aria-label") === `Saved conversations, ${count} loaded`,
    manifest.expectedThreadCount, { timeout: 30000 });
  assert.equal(await page.locator(".history-load-more").count(), 0,
    "cursor exhaustion should remove the older-history action after the final page");
  const oldestRow = page.locator("button.saved-chat").filter({ hasText: manifest.oldestPrompt });
  await oldestRow.waitFor({ timeout: 10000 });
  assert.equal(await page.locator("button.saved-chat").count(), 1,
    "loading the older page should reveal exactly one search match");

  await search.focus();
  await page.keyboard.press("Control+A");
  await page.keyboard.press("Backspace");
  await page.waitForFunction((count) => document.querySelectorAll("button.saved-chat").length === count,
    manifest.expectedThreadCount, { timeout: 10000 });
  const allRows = await page.locator("button.saved-chat").allInnerTexts();
  assert.equal(allRows.length, manifest.expectedThreadCount);
  for (let index = 0; index < expectedPrompts.length; index++) {
    assert.ok(allRows[index].includes(expectedPrompts[index]),
      `history order/coverage differs at row ${index}: expected ${expectedPrompts[index]}, got ${allRows[index]}`);
  }

  const exactOldestRow = page.locator("button.saved-chat").filter({ hasText: manifest.oldestPrompt });
  const attribution = (await exactOldestRow.locator("small").innerText()).trim();
  assert.equal(attribution, `${expectedProvider} · ${expectedModel}`,
    "the saved task must retain its exact provider/model attribution");
  await keyboardActivateReadyButton(page, exactOldestRow, "oldest saved task");
  const conversation = page.getByRole("region", { name: "Conversation" });
  try {
    await conversation.getByText(manifest.oldestPrompt, { exact: true }).waitFor({ timeout: 20000 });
  } catch (error) {
    const state = await page.evaluate(() => ({
      selectedTask: document.querySelector("button.saved-chat[aria-current='page']")?.innerText ?? null,
      conversation: document.querySelector(".conversation")?.innerText.slice(0, 1000) ?? null,
      turnError: document.querySelector(".turn-error")?.innerText ?? null,
      openingTask: document.querySelector(".thread-loading-dot")?.parentElement?.innerText ?? null,
    }));
    throw new Error(`oldest saved task did not reopen: ${error.message}; visible state=${JSON.stringify(state)}`);
  }
  await conversation.getByText(manifest.assistantReply, { exact: true }).waitFor({ timeout: 20000 });
  assert.equal(await exactOldestRow.getAttribute("aria-current"), "page");

  const overflowRow = page.locator("button.saved-chat").filter({ hasText: manifest.overflowPrompt });
  await keyboardActivateReadyButton(page, overflowRow, "transcript-overflow saved task");
  await conversation.getByText(manifest.overflowPrompt, { exact: true }).waitFor({ timeout: 20000 });
  const omissionNotice = page.getByRole("note").filter({ hasText: "Full history remains in Codex App Server." });
  await omissionNotice.waitFor({ timeout: 10000 });
  const overflowPreview = await conversation.locator(".message-assistant .message-text").last().evaluate((element) => element.textContent ?? "");
  assert.ok(overflowPreview.startsWith(manifest.overflowAssistantPrefix),
    "the overflow task must reopen with the original assistant response prefix");
  assert.ok(overflowPreview.length < manifest.overflowAssistantCharacters,
    "the native transcript preview must stay below the complete saved response");
  assert.ok(overflowPreview.length <= manifest.transcriptProjectionLimit,
    "the native transcript preview must stay within its 120,000-character projection limit");
  assert.equal(await overflowRow.getAttribute("aria-current"), "page",
    "the overflow transcript must remain bound to its exact selected task");
  assert.deepEqual(observedPageIssues, [], "native history search/reopen should not produce browser errors");
  await page.screenshot({ path: screenshotPath });

  const emptyProjectRow = page.locator("button.project-row").filter({ hasText: "empty-workspace" });
  const emptyProjectBeforeActivation = await emptyProjectRow.evaluate((element) => ({
    text: element.textContent?.trim(),
    disabled: element.disabled,
    current: element.getAttribute("aria-current"),
    active: document.activeElement === element,
  }));
  await keyboardActivateReadyButton(page, emptyProjectRow, "empty project row");
  await page.waitForFunction(() => document.activeElement?.getAttribute("aria-label") === "Message NeoBabylon",
    null, { timeout: 10000 });
  assert.equal(await page.getByRole("textbox", { name: "Message NeoBabylon" })
    .evaluate((element) => document.activeElement === element), true,
  "native keyboard project selection should return focus to the task composer");
  try {
    await page.waitForFunction(() => document.querySelector('button.project-row[aria-current="page"] .project-name')?.textContent === "empty-workspace",
      null, { timeout: 15000 });
  } catch (error) {
    const state = await page.evaluate(() => ({
      active: document.activeElement ? {
        tag: document.activeElement.tagName,
        label: document.activeElement.getAttribute("aria-label"),
        text: document.activeElement.textContent?.trim().slice(0, 100),
      } : null,
      projects: [...document.querySelectorAll("button.project-row")].map((element) => ({
        name: element.querySelector(".project-name")?.textContent,
        disabled: element.disabled,
        current: element.getAttribute("aria-current"),
      })),
      alerts: [...document.querySelectorAll('[role="alert"]')].map((element) => element.textContent?.trim()),
      history: document.querySelector('[aria-label^="Saved conversations"]')?.getAttribute("aria-label"),
    }));
    throw new Error(`empty project selection did not complete; before=${JSON.stringify(emptyProjectBeforeActivation)}; after=${JSON.stringify(state)}; cause=${error.message}`);
  }
  await page.waitForFunction(() => document.querySelector('[aria-label^="Saved conversations"]')?.getAttribute("aria-label") === "Saved conversations, 0 loaded",
    null, { timeout: 15000 });
  await page.getByText("Your conversations will appear here.", { exact: true }).waitFor();
  assert.equal(await page.locator("button.saved-chat").count(), 0,
    "switching to the empty project must not leak another project's saved history");
  assert.equal(await page.locator(".history-load-more").count(), 0,
    "empty project history must not retain the prior project's cursor");
  const newTask = page.getByRole("button", { name: "New task" });
  await keyboardActivateReadyButton(page, newTask, "new task");
  await page.waitForFunction(() => document.activeElement?.getAttribute("aria-label") === "Message NeoBabylon");
  assert.equal(await page.getByRole("textbox", { name: "Message NeoBabylon" }).evaluate((element) => document.activeElement === element), true,
    "native New task keyboard activation should return focus to the composer");
  assert.deepEqual(observedPageIssues, [], "project-scoped empty history should not produce browser errors");

  resultSummary = {
    passed: true,
    scenario: "P2-01/P2-10 native keyboard model/dialog/history/project flows with pagination, exact task resume, and explicit transcript-overflow disclosure",
    source: "WPF + WebView2 + pinned App Server + deterministic local Responses fixture",
    expectedThreadCount: manifest.expectedThreadCount,
    firstPageCount: manifest.firstPageCount,
    secondPageCount: manifest.secondPageCount,
    olderSearchMessage: "No matches in loaded conversations. Load older conversations to search more.",
    reopenedThreadId: manifest.oldestThreadId,
    overflowThreadId: manifest.overflowThreadId,
    transcriptProjectionLimit: manifest.transcriptProjectionLimit,
    transcriptOverflowDisclosureVisible: true,
    transcriptOverflowPreviewCharacters: overflowPreview.length,
    completeOverflowAssistantCharacters: manifest.overflowAssistantCharacters,
    provider: expectedProvider,
    model: expectedModel,
    nativeCapabilityCategoryTabStops: capabilityCategoryCount,
    transcriptRestored: true,
    projectSelectionReturnedFocusToComposer: true,
    newTaskReturnedFocusToComposer: true,
    nativeWindowJourneys: [
      { outerSize: "1440x900", contentViewport: desktopViewport, screenshot: desktopScreenshotPath },
      { outerSize: "960x720", contentViewport: compactViewport, screenshot: compactScreenshotPath },
    ],
    screenshotPath,
    applicationRoot,
    runtimeSha256: manifest.runtimeSha256,
    pageIssues: observedPageIssues,
  };
} finally {
  if (browser) await browser.close().catch(() => {});
  if (host && host.exitCode === null) {
    try {
      stopHostWindow(host.pid);
      await waitForExit(host, 20000);
      hostClosed = true;
    } catch (error) {
      host.kill();
      await waitForExit(host, 5000).catch(() => {});
      if (!hostClosed) console.error(`QA host needed targeted process cleanup after graceful-close attempt: ${error.message}`);
    }
  }
  if (runner.exitCode === null) {
    runner.stdin.write("\n");
    runner.stdin.end();
  }
  const runnerExit = await waitForExit(runner, 30000).catch((error) => {
    runner.kill();
    throw error;
  });
  const requestLine = lines.find((line) => line.startsWith("HISTORY_PAGINATION_QA_PROVIDER_REQUESTS="));
  const requestCount = requestLine ? Number(requestLine.split("=")[1]) : null;
  if (runnerExit.code !== 0) throw new Error(`history QA fixture exited with code ${runnerExit.code}; stderr=${runnerStderr}`);
  assert.equal(requestCount, 56, "listing and reopening saved tasks must not issue any provider inference");
}
assert.ok(resultSummary, "native history QA did not produce a completed result summary");
resultSummary.providerRequestsAfterNativeResume = 56;
resultSummary.noInferenceOnReopen = true;
resultSummary.runRoot = runRoot;
resultSummary.resultPath = path.join(runRoot, "result.json");
writeFileSync(resultSummary.resultPath, `${JSON.stringify(resultSummary, null, 2)}\n`, "utf8");
console.log(JSON.stringify(resultSummary));
