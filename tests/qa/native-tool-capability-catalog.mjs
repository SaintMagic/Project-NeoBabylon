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
  "usage: node native-tool-capability-catalog.mjs <NeoBabylon.Host.exe> <playwright-core-entry.mjs> <exact .local\\Lab\\Runs parent>");

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
const runtimePath = path.resolve(sourceRoot, runtimeLock.runtime.appServerBinaryRelativePath);
assert.ok(existsSync(runtimePath), `pinned App Server binary is missing: ${runtimePath}`);
const runtimeHash = createHash("sha256").update(readFileSync(runtimePath)).digest("hex");
assert.equal(runtimeHash, runtimeLock.runtime.sha256.toLowerCase(), "pinned App Server binary does not match its runtime lock");

const { runRoot, applicationRoot } = newQaRun(qaParent, `P3-03-native-tool-catalog-${Date.now()}-${process.pid}`);
const capabilityPath = path.join(releaseRoot, `MODEL_CAPABILITY_P3_03_${Date.now()}_${process.pid}.json`);
const resultPath = path.join(runRoot, "result.json");
const screenshotBeforeRestart = path.join(runRoot, "tool-catalog-before-restart.png");
const screenshotAfterRestart = path.join(runRoot, "tool-catalog-after-restart.png");
const providerModel = "p3-03-deterministic-catalog-fixture";
const providerRequests = [];
const fixtureErrors = [];

const capability = {
  providerId: "lmstudio",
  providerDisplayName: "LM Studio · P3-03 test fixture",
  providerServerVersion: "Test fixture; no LM Studio server",
  endpoint: "pending-loopback-endpoint",
  modelIdentifier: providerModel,
  modelVariant: "deterministic UI-only record; no weights or live inference",
  architecture: "Unknown",
  parameterCount: "Unknown",
  modelSizeBytes: "Unknown",
  quantization: { state: "Unknown", value: null, evidenceSource: "P3-03 UI fixture; no model weights" },
  contextWindowAdvertised: { state: "Unknown", value: null, evidenceSource: "P3-03 UI fixture; no provider metadata" },
  contextWindowEffective: { state: "Unknown", value: null, evidenceSource: "P3-03 UI fixture; no model turn" },
  reasoningControls: { state: "Unknown", value: null, evidenceSource: "P3-03 UI fixture; no model metadata" },
  toolFunctionCalling: {
    state: "Known",
    value: "tool_use",
    evidenceSource: "Synthetic catalog fixture only; must not override exact blocked operation status",
  },
  applyPatchToolType: { state: "Unknown", value: null, evidenceSource: "P3-03 UI fixture; patch was not called" },
  structuredOutput: { state: "Unknown", value: null, evidenceSource: "P3-03 UI fixture; no provider metadata" },
  agentMetadata: { state: "Unknown", value: null, evidenceSource: "P3-03 UI fixture; no live model" },
  toolQualifications: [{
    operationId: "exec_command",
    state: "blocked",
    observedOn: "2026-09-25",
    scope: "Synthetic UI denial state only; no command was submitted or executed.",
    evidenceSource: "tests/qa/native-tool-capability-catalog.mjs test fixture; not provider qualification",
  }],
};

const fixtureServer = createHttpServer((request, response) => {
  const requestUrl = new URL(request.url ?? "/", "http://127.0.0.1");
  providerRequests.push({ method: request.method ?? "UNKNOWN", path: requestUrl.pathname });
  if (request.method === "GET" && requestUrl.pathname.endsWith("/models")) {
    response.writeHead(200, { "content-type": "application/json" })
      .end(JSON.stringify({ data: [{ id: providerModel, object: "model", owned_by: "p3-03-fixture" }] }));
    return;
  }
  if (request.method === "POST" && requestUrl.pathname.endsWith("/responses")) {
    fixtureErrors.push("unexpected provider inference request in read-only catalog test");
  }
  response.writeHead(404).end();
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

function requestHostClose(pid) {
  const command = `$process = Get-Process -Id ${pid} -ErrorAction Stop; if (-not $process.CloseMainWindow()) { exit 2 }; exit 0`;
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", command], {
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

const { chromium } = await import(pathToFileURL(playwrightModulePath).href);
const pageIssues = [];
const launches = [];
let host = null;
let browser = null;
let page = null;
let fixturePort = null;
let initialCategoryCount = 0;
let capabilityFixtureContent = null;
let capabilityFixtureCreated = false;

async function waitForCdp(port) {
  const deadline = Date.now() + 45000;
  while (Date.now() < deadline) {
    if (host && host.exitCode !== null) throw new Error(`WPF host exited before CDP opened (code ${host.exitCode})`);
    try {
      const result = await fetch(`http://127.0.0.1:${port}/json/version`, { signal: AbortSignal.timeout(1000) });
      if (result.ok) return await result.json();
    } catch { /* WebView2 is starting. */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`WebView2 CDP did not open on port ${port}`);
}

async function waitForNativePage(port) {
  const deadline = Date.now() + 30000;
  let targets = [];
  while (Date.now() < deadline) {
    if (host && host.exitCode !== null) throw new Error(`WPF host exited before its page appeared (code ${host.exitCode})`);
    const candidate = browser?.contexts().flatMap((context) => context.pages())
      .find((item) => item.url().startsWith("https://neobabylon.local/"));
    if (candidate) return candidate;
    try {
      const result = await fetch(`http://127.0.0.1:${port}/json/list`, { signal: AbortSignal.timeout(1000) });
      if (result.ok) targets = await result.json();
    } catch { /* Native page is still loading. */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`native page did not appear; CDP targets=${JSON.stringify(targets)}`);
}

async function launchHost() {
  const debugPort = await reservePort();
  const hostEnv = { ...process.env };
  for (const name of ["OPENROUTER_API_KEY", "OPENAI_API_KEY", "NEOBABYLON_WINDOWS_SANDBOX_MODE"]) delete hostEnv[name];
  Object.assign(hostEnv, {
    NEOBABYLON_SOURCE_ROOT: sourceRoot,
    NEOBABYLON_APPLICATION_ROOT: applicationRoot,
    NEOBABYLON_MODEL_CAPABILITY_PATH: capabilityPath,
    NEOBABYLON_ENABLE_WEBVIEW2_REMOTE_DEBUG: "1",
    NEOBABYLON_WEBVIEW2_DEBUG_PORT: String(debugPort),
  });
  host = spawn(hostPath, [], { cwd: sourceRoot, env: hostEnv, stdio: "ignore", windowsHide: false });
  launches.push({ hostPid: host.pid, debugPort });
  await waitForCdp(debugPort);
  browser = await chromium.connectOverCDP(`http://127.0.0.1:${debugPort}`);
  page = await waitForNativePage(debugPort);
  page.on("pageerror", (error) => pageIssues.push(error.message));
  page.on("console", (message) => {
    if (message.type() === "error") pageIssues.push(message.text());
  });
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  assert.equal(await page.title(), "NeoBabylon");
  assert.equal(await page.locator("vite-error-overlay").count(), 0, "native UI should not show a framework error overlay");
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

async function openCatalog() {
  await page.getByRole("button", { name: "Open diagnostics" }).click();
  const drawer = page.getByRole("dialog", { name: "Runtime diagnostics" });
  await drawer.waitFor();
  const catalog = drawer.locator("[data-tool-capability-catalog]");
  await catalog.waitFor();
  assert.ok((await catalog.innerText()).includes(providerModel), "catalog should reflect the selected capability record");
  return catalog;
}

async function alignCatalogTopForScreenshot(catalog) {
  await catalog.evaluate((element) => {
    const scrollRegion = element.closest(".drawer-scroll");
    if (!(scrollRegion instanceof HTMLElement)) throw new Error("catalog is outside the diagnostics scroll region");
    scrollRegion.scrollTop += element.getBoundingClientRect().top - scrollRegion.getBoundingClientRect().top - 16;
    if (document.activeElement instanceof HTMLElement) document.activeElement.blur();
  });
}

const fixturePortReady = await listen(fixtureServer);
fixturePort = fixturePortReady;
capability.endpoint = `http://127.0.0.1:${fixturePort}/v1`;

try {
  assert.equal(existsSync(capabilityPath), false, "refusing to overwrite an existing capability fixture");
  capabilityFixtureContent = `${JSON.stringify(capability, null, 2)}\n`;
  writeFileSync(capabilityPath, capabilityFixtureContent, { encoding: "utf8", flag: "wx" });
  capabilityFixtureCreated = true;

  await launchHost();
  let catalog = await openCatalog();
  initialCategoryCount = await catalog.locator("details.tool-catalog-category").count();
  assert.equal(initialCategoryCount, 13, "the native drawer should show every upstream inventory category");
  const search = catalog.getByRole("searchbox", { name: "Search tools and capabilities" });
  await search.fill("exec_command");
  let category = catalog.locator("details.tool-catalog-category").filter({ hasText: "Command execution" });
  await category.waitFor();
  await category.locator("summary").click();
  const blockedOperation = category.locator('[data-qualification-state="blocked"]');
  await blockedOperation.waitFor();
  assert.ok((await blockedOperation.innerText()).includes("Blocked"), "the explicit denial should remain visible");
  assert.equal(await catalog.locator("button").count(), 0, "catalog rows must not expose executable controls");

  await search.fill("apply_patch");
  const patchCategory = catalog.locator("details.tool-catalog-category").filter({ hasText: "Patch application" });
  await patchCategory.waitFor();
  await patchCategory.locator("summary").click();
  const unknownPatch = patchCategory.locator('[data-qualification-state="unknown"]');
  await unknownPatch.waitFor();
  assert.ok((await unknownPatch.innerText()).includes("Unknown"), "missing operation evidence must remain Unknown");
  assert.equal(await catalog.locator("button").count(), 0, "Unknown operations must remain non-callable");

  await search.fill("");
  await patchCategory.locator("summary").click();
  await alignCatalogTopForScreenshot(catalog);
  await page.screenshot({ path: screenshotBeforeRestart });
  await closeHost();

  await launchHost();
  catalog = await openCatalog();
  const restartedSearch = catalog.getByRole("searchbox", { name: "Search tools and capabilities" });
  await restartedSearch.fill("exec_command");
  const restartedCategory = catalog.locator("details.tool-catalog-category").filter({ hasText: "Command execution" });
  await restartedCategory.waitFor();
  await restartedCategory.locator("summary").click();
  const restartedBlocked = restartedCategory.locator('[data-qualification-state="blocked"]');
  await restartedBlocked.waitFor();
  assert.ok((await restartedBlocked.innerText()).includes("Blocked"), "restart must reload the same tuple evidence");
  assert.equal(await catalog.locator("button").count(), 0, "restart must not add a tool-execution control");
  await restartedSearch.fill("");
  await alignCatalogTopForScreenshot(catalog);
  await page.screenshot({ path: screenshotAfterRestart });

  assert.equal(fixtureErrors.length, 0, `provider inference must not occur: ${fixtureErrors.join("; ")}`);
  assert.equal(providerRequests.some((request) => request.method === "POST" && request.path.endsWith("/responses")), false,
    "the test must not perform live or mock inference");
  assert.deepEqual(pageIssues, [], "the native catalog should render without page or console errors");

  const result = {
    passed: true,
    source: "native WPF/WebView2",
    runtime: {
      version: runtimeLock.runtime.version,
      sourceRevision: runtimeLock.runtime.sourceRevision,
      sha256: runtimeHash,
    },
    selectedModel: providerModel,
    capabilityFixturePath: capabilityPath,
    applicationRoot,
    restartReopen: true,
    unknownAndBlockedNonCallable: true,
    inferenceRequests: providerRequests.filter((request) => request.method === "POST" && request.path.endsWith("/responses")).length,
    localProviderFixtureRequests: providerRequests,
    categoryCountBeforeFilter: initialCategoryCount,
    pageIssues,
    screenshots: [screenshotBeforeRestart, screenshotAfterRestart],
  };
  writeFileSync(resultPath, `${JSON.stringify(result, null, 2)}\n`, "utf8");
  console.log(JSON.stringify(result));
} finally {
  await closeHost().catch(() => {});
  await new Promise((resolve) => fixtureServer.close(resolve));
  if (capabilityFixtureCreated && capabilityFixtureContent !== null && existsSync(capabilityPath)) {
    const currentFixture = readFileSync(capabilityPath, "utf8");
    if (currentFixture === capabilityFixtureContent) unlinkSync(capabilityPath);
    else console.error(`Test capability fixture changed externally and was preserved: ${capabilityPath}`);
  }
}
