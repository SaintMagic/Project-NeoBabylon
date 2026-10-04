import assert from "node:assert/strict";
import { spawn, spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { createServer as createTcpServer } from "node:net";
import { existsSync, mkdirSync, readFileSync, readdirSync, statSync, unlinkSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { assertQaRunsParent, newQaRun } from "./qa-run-root.mjs";

const [runnerPathArg, hostPathArg, playwrightPathArg, qaParentArg] = process.argv.slice(2);
assert.ok(runnerPathArg && hostPathArg && playwrightPathArg && qaParentArg,
  "usage: node native-live-nex-output.mjs <phase1a-test-runner.exe> <NeoBabylon.Host.exe> <playwright-core-entry.mjs> <exact .local\\Lab\\Runs parent>");

const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const releaseRoot = path.join(sourceRoot, "docs", "release");
const qaParent = path.resolve(qaParentArg);
assertQaRunsParent(sourceRoot, qaParent);
const runnerPath = path.resolve(runnerPathArg);
const hostPath = path.resolve(hostPathArg);
const playwrightPath = path.resolve(playwrightPathArg);
const modelId = "nex-agi/nex-n2.5-pro:free";
const modelVariant = "nex-agi/nex-n2.5-pro-20260907:free";
const routeTag = "nex-agi/fp8";
const minimumVisibleCharacters = 8_000;
const startedAt = new Date().toISOString();
const { runRoot, applicationRoot } = newQaRun(qaParent, `P2-11-native-live-nex-${Date.now()}-${process.pid}`);
const workspace = path.join(runRoot, "workspace");
const prepManifestPath = path.join(runRoot, "preparation-manifest.json");
const manifestPath = path.join(runRoot, "live-manifest.json");
const resultPath = path.join(runRoot, "result.json");
const screenshotPath = path.join(runRoot, "native-live-nex-output.png");
const runtimeLockPath = path.join(sourceRoot, "runtime", "runtime-lock.json");
const capabilityPath = path.join(releaseRoot, "MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json");
const directResultPath = path.join(qaParent, "P2-11-live-nex-user-key-20260925", "result.json");
const temporaryCapabilityPath = path.join(releaseRoot, `MODEL_CAPABILITY_P2_11_NATIVE_LIVE_${Date.now()}_${process.pid}.json`);
const runtimeLock = JSON.parse(readFileSync(runtimeLockPath, "utf8"));
const capability = JSON.parse(readFileSync(capabilityPath, "utf8"));
const runtimePath = path.resolve(sourceRoot, runtimeLock.runtime.appServerBinaryRelativePath);
const runtimeSha256 = createHash("sha256").update(readFileSync(runtimePath)).digest("hex");

for (const [label, filePath] of [["runner", runnerPath], ["host", hostPath], ["Playwright", playwrightPath], ["runtime", runtimePath], ["capability", capabilityPath]]) {
  assert.ok(existsSync(filePath), `${label} is missing: ${filePath}`);
}
assert.equal(runtimeSha256, runtimeLock.runtime.sha256.toLowerCase(), "locked App Server binary does not match runtime-lock.json");
assert.equal(capability.providerId, "openrouter");
assert.equal(capability.modelIdentifier, modelId);
assert.equal(capability.providerRoute?.state, "Known");
assert.equal(capability.providerRoute?.value?.endpointTag, routeTag);
assert.ok(process.env.OPENROUTER_API_KEY, "OPENROUTER_API_KEY must be supplied through the hidden process-only prompt");
assert.ok(!existsSync(temporaryCapabilityPath), `refusing to overwrite a temporary capability: ${temporaryCapabilityPath}`);

const directResult = JSON.parse(readFileSync(directResultPath, "utf8"));
assert.equal(directResult.preflight?.endpoint?.tag, routeTag, "the recent live preflight did not observe the accepted NEX route");
assert.equal(directResult.response?.resolvedModel, modelId, "the recent direct response did not resolve to the accepted NEX alias");
assert.equal(directResult.response?.status, "completed", "the recent direct response was not complete");
assert.equal(directResult.response?.usage?.cost, 0, "the recent direct response did not report zero cost");
assert.equal(directResult.response?.routeTagPostHocAttested, false, "unexpected route-tag attestation semantics");

const result = {
  test: "P2-11 native WPF/WebView2 + pinned App Server + live OpenRouter NEX response",
  startedAt,
  outcome: "not_started",
  sourceRoot,
  runRoot,
  applicationRoot,
  dataRoot: path.join(applicationRoot, "Data"),
  workspace,
  ordinaryCodexRootUsed: false,
  runtime: {
    version: runtimeLock.runtime.version,
    sourceRevision: runtimeLock.runtime.sourceRevision,
    binarySha256: runtimeSha256,
    binaryPath: runtimePath,
  },
  provider: {
    id: "openrouter",
    modelAlias: modelId,
    modelVariant,
    endpoint: capability.endpoint,
    routeTag,
    requestFallbacksDisabled: true,
    postHocRouteAttestation: "Unknown; the direct preflight is request-side evidence and the prior generation lookup returned 404.",
    directRequestEvidence: {
      resultPath: directResultPath,
      resolvedModel: directResult.response.resolvedModel,
      outputChars: directResult.response.outputChars,
      reportedCost: directResult.response.usage.cost,
      isByok: directResult.response.usage.is_byok,
      generationLookup: directResult.generationAttestation?.lookupStatus ?? null,
    },
    credentialHandling: "The user-supplied key is held in the hidden PowerShell prompt and process memory only; its value is not logged or persisted.",
  },
  request: {
    maxOutputTokens: "Unknown; the native UI/App Server call does not expose an explicit output-token cap.",
    boundedPromptShape: "24 numbered sections, two distinct paragraphs per section, with no tool request.",
    minimumVisibleCharacters,
    tools: "Prompt explicitly requests no tools; tool activity is asserted absent.",
    promptSha256: null,
  },
  response: null,
  memory: { sampleCount: 0, samples: [], maxima: [] },
  screenshotPath,
  pageIssues: [],
};

const syntheticCapability = {
  providerId: "lmstudio",
  providerDisplayName: "LM Studio · native live QA setup fixture",
  providerServerVersion: "Unknown",
  endpoint: "http://127.0.0.1:1/v1",
  modelIdentifier: "p2-11-native-live-qa-setup-fixture",
  modelVariant: "P2-11 deterministic loopback fixture; no model weights",
  architecture: "Unknown",
  parameterCount: "Unknown",
  modelSizeBytes: "Unknown",
  quantization: { state: "Unknown", value: null, evidenceSource: "QA setup fixture only" },
  contextWindowAdvertised: { state: "Unknown", value: null, evidenceSource: "QA setup fixture only" },
  contextWindowEffective: { state: "Known", value: 32768, evidenceSource: "QA setup fixture only" },
  reasoningControls: { state: "Unknown", value: null, evidenceSource: "QA setup fixture only" },
  toolFunctionCalling: {
    state: "Known",
    value: "tool_use",
    evidenceSource: "Synthetic QA bootstrap fixture only; replaced by the canonical NEX capability record before any live model turn",
  },
  applyPatchToolType: { state: "Unknown", value: null, evidenceSource: "QA setup fixture only" },
  structuredOutput: { state: "Unknown", value: null, evidenceSource: "QA setup fixture only" },
  agentMetadata: {
    state: "Known",
    value: "inputModalities=text; vision=False",
    evidenceSource: "Synthetic QA bootstrap fixture only; replaced by the canonical NEX capability record before any live model turn",
  },
};
const temporaryCapabilityBytes = Buffer.from(`${JSON.stringify(syntheticCapability, null, 2)}\n`, "utf8");

let host;
let browser;
let page;
let memorySampler;
let firstTextAt = null;
let sendStartedAt = null;
let preparationOutput = "";
const memorySamples = [];

function dateInBratislava() {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Europe/Bratislava",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(new Date());
  const part = (type) => parts.find((item) => item.type === type)?.value;
  return `${part("year")}-${part("month")}-${part("day")}`;
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
  const closeResult = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", command], { encoding: "utf8", timeout: 10000 });
  if (closeResult.error) throw closeResult.error;
  if (closeResult.status !== 0) throw new Error(`could not request graceful close for QA host PID ${pid}`);
}

function getProcessSample(pid) {
  const expectedPath = runtimePath.replaceAll("'", "''");
  const command = `$expected = [IO.Path]::GetFullPath('${expectedPath}'); $rootProcessId = ${pid}; $all = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue); $owned = @{}; $owned[$rootProcessId] = $true; $changed = $true; while ($changed) { $changed = $false; foreach ($entry in $all) { $childId = [int]$entry.ProcessId; if ($owned.ContainsKey([int]$entry.ParentProcessId) -and -not $owned.ContainsKey($childId)) { $owned[$childId] = $true; $changed = $true } } }; $items = @(); $hostProcess = Get-Process -Id ${pid} -ErrorAction SilentlyContinue; if ($hostProcess) { $items += [pscustomobject]@{ role='WPF host'; pid=$hostProcess.Id; workingSetBytes=$hostProcess.WorkingSet64; privateBytes=$hostProcess.PrivateMemorySize64 } }; $all | Where-Object { $owned.ContainsKey([int]$_.ProcessId) -and $_.Name -ieq 'codex-app-server.exe' -and $_.ExecutablePath -and [IO.Path]::GetFullPath($_.ExecutablePath) -ieq $expected } | ForEach-Object { $p = Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue; if ($p) { $items += [pscustomobject]@{ role='pinned App Server'; pid=$p.Id; workingSetBytes=$p.WorkingSet64; privateBytes=$p.PrivateMemorySize64 } } }; ConvertTo-Json -InputObject $items -Compress`;
  const sampleResult = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", command], { encoding: "utf8", timeout: 10000 });
  if (sampleResult.error || sampleResult.status !== 0) throw new Error("owned-process memory sample failed");
  const raw = sampleResult.stdout.trim();
  const decoded = raw ? JSON.parse(raw) : [];
  const processes = Array.isArray(decoded) ? decoded : decoded ? [decoded] : [];
  return { sampledAt: new Date().toISOString(), processes };
}

function safeTestEnvironment() {
  const environment = { ...process.env };
  for (const name of Object.keys(environment)) {
    if (/API_KEY|ACCESS_TOKEN|AUTH_TOKEN|PASSWORD|SECRET/i.test(name)) delete environment[name];
  }
  delete environment.CODEX_HOME;
  return environment;
}

function safeHostEnvironment(debugPort) {
  const environment = { ...process.env };
  const openRouterKey = environment.OPENROUTER_API_KEY;
  for (const name of Object.keys(environment)) {
    if (/API_KEY|ACCESS_TOKEN|AUTH_TOKEN|PASSWORD|SECRET/i.test(name)) delete environment[name];
  }
  environment.OPENROUTER_API_KEY = openRouterKey;
  delete environment.CODEX_HOME;
  delete environment.NEOBABYLON_WINDOWS_SANDBOX_MODE;
  Object.assign(environment, {
    NEOBABYLON_SOURCE_ROOT: sourceRoot,
    NEOBABYLON_APPLICATION_ROOT: applicationRoot,
    NEOBABYLON_MODEL_CAPABILITY_PATH: capabilityPath,
    NEOBABYLON_ENABLE_WEBVIEW2_REMOTE_DEBUG: "1",
    NEOBABYLON_WEBVIEW2_DEBUG_PORT: String(debugPort),
  });
  return environment;
}

function writePreparationFixture() {
  writeFileSync(temporaryCapabilityPath, temporaryCapabilityBytes, { flag: "wx" });
}

function removePreparationFixture() {
  if (!existsSync(temporaryCapabilityPath)) return result.temporaryCapabilityCleanup ?? "not-created";
  const actualBytes = readFileSync(temporaryCapabilityPath);
  const expectedHash = createHash("sha256").update(temporaryCapabilityBytes).digest("hex");
  const actualHash = createHash("sha256").update(actualBytes).digest("hex");
  if (actualHash !== expectedHash) {
    result.temporaryCapabilityCleanup = "retained; bytes no longer match this run's generated fixture";
    return result.temporaryCapabilityCleanup;
  }
  unlinkSync(temporaryCapabilityPath);
  result.temporaryCapabilityCleanup = "removed; generated fixture bytes matched exactly";
  return result.temporaryCapabilityCleanup;
}

function collectCredentialPatternEvidence(root) {
  const textExtensions = new Set([".json", ".jsonl", ".toml", ".txt", ".md", ".log", ".yaml", ".yml"]);
  const credentialPattern = /sk-or-v1-[A-Za-z0-9-]{30,}/i;
  let inspectedFiles = 0;
  let skippedLargeFiles = 0;
  let patternFound = false;
  const pending = [root];
  while (pending.length) {
    const current = pending.pop();
    for (const entry of readdirSync(current, { withFileTypes: true })) {
      const target = path.join(current, entry.name);
      if (entry.isDirectory()) pending.push(target);
      else if (entry.isFile() && textExtensions.has(path.extname(entry.name).toLowerCase())) {
        const size = statSync(target).size;
        if (size > 20 * 1024 * 1024) {
          skippedLargeFiles++;
          continue;
        }
        inspectedFiles++;
        if (credentialPattern.test(readFileSync(target, "utf8"))) patternFound = true;
      }
    }
  }
  return { inspectedFiles, skippedLargeFiles, credentialPatternFound: patternFound };
}

function memoryMaxima(samples) {
  const maxima = new Map();
  for (const sample of samples) {
    for (const process of sample.processes ?? []) {
      const prior = maxima.get(process.role) ?? { role: process.role, maxWorkingSetBytes: 0, maxPrivateBytes: 0 };
      prior.maxWorkingSetBytes = Math.max(prior.maxWorkingSetBytes, process.workingSetBytes ?? 0);
      prior.maxPrivateBytes = Math.max(prior.maxPrivateBytes, process.privateBytes ?? 0);
      maxima.set(process.role, prior);
    }
  }
  return [...maxima.values()];
}

async function run() {
  const { chromium } = await import(pathToFileURL(playwrightPath).href);
  writePreparationFixture();
  try {
    const prepare = spawnSync(runnerPath, [
      "--prepare-p2-11-native-output-qa",
      applicationRoot,
      workspace,
      temporaryCapabilityPath,
      prepManifestPath,
    ], { cwd: sourceRoot, env: safeTestEnvironment(), encoding: "utf8", timeout: 120_000 });
    preparationOutput = `${prepare.stdout ?? ""}${prepare.stderr ?? ""}`;
    writeFileSync(path.join(runRoot, "preparation-output.log"), preparationOutput, { encoding: "utf8", flag: "wx" });
    if (prepare.error) throw prepare.error;
    if (prepare.status !== 0) throw new Error(`isolated QA preparation failed with exit code ${prepare.status}`);
  } finally {
    removePreparationFixture();
  }

  const capabilityLocalDate = dateInBratislava();
  const modelExpiry = capability.providerRoute?.value?.modelExpirationDate;
  if (typeof modelExpiry === "string" && modelExpiry < capabilityLocalDate) {
    throw new Error(`the accepted NEX model catalog entry expired on ${modelExpiry}; no provider request was sent by this WPF run`);
  }

  const liveManifest = {
    observedAt: new Date().toISOString(),
    sourceRoot,
    applicationRoot,
    dataRoot: path.join(applicationRoot, "Data"),
    workspace,
    capabilityRecordPath: capabilityPath,
    providerId: capability.providerId,
    modelIdentifier: capability.modelIdentifier,
    modelVariant: capability.modelVariant,
    endpoint: capability.endpoint,
    routeTag,
    requestFallbacksDisabled: true,
    runtimeVersion: runtimeLock.runtime.version,
    runtimeRevision: runtimeLock.runtime.sourceRevision,
    runtimeSha256,
    ordinaryCodexRootUsed: false,
    credentialValuePersisted: false,
  };
  writeFileSync(manifestPath, `${JSON.stringify(liveManifest, null, 2)}\n`, { encoding: "utf8", flag: "wx" });

  const debugPort = await reservePort();
  const hostEnvironment = safeHostEnvironment(debugPort);
  host = spawn(hostPath, [], { cwd: sourceRoot, env: hostEnvironment, stdio: "ignore", windowsHide: false });
  delete hostEnvironment.OPENROUTER_API_KEY;
  delete process.env.OPENROUTER_API_KEY;

  const debugDeadline = Date.now() + 45_000;
  while (Date.now() < debugDeadline) {
    if (host.exitCode !== null) throw new Error(`WPF host exited before WebView2 CDP opened (code ${host.exitCode})`);
    try {
      const response = await fetch(`http://127.0.0.1:${debugPort}/json/version`, { signal: AbortSignal.timeout(1000) });
      if (response.ok) break;
    } catch { /* WebView2 is starting. */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }

  browser = await chromium.connectOverCDP(`http://127.0.0.1:${debugPort}`);
  const pageDeadline = Date.now() + 30_000;
  while (Date.now() < pageDeadline) {
    page = browser.contexts().flatMap((context) => context.pages())
      .find((candidate) => candidate.url().startsWith("https://neobabylon.local/"));
    if (page) break;
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  assert.ok(page, "native NeoBabylon WebView page did not appear");
  page.on("pageerror", (error) => result.pageIssues.push(error.message));
  page.on("console", (message) => {
    if (message.type() === "error") result.pageIssues.push(`console: ${message.text()}`);
  });
  await page.getByText("Desktop host ready").waitFor({ timeout: 30_000 });
  assert.equal(await page.title(), "NeoBabylon");
  assert.equal(await page.locator("vite-error-overlay").count(), 0);

  const composer = page.getByRole("textbox", { name: "Message NeoBabylon" });
  await composer.waitFor();
  const selectedModel = await page.locator("button.model-select").innerText();
  const visibleModelLabel = modelId.split("/").at(-1);
  assert.ok(selectedModel.includes(visibleModelLabel),
    `selected UI model did not show the exact model's visible identifier ${visibleModelLabel}: ${selectedModel}`);

  await page.getByRole("button", { name: "See runtime details" }).click();
  const diagnostics = page.getByRole("dialog", { name: "Runtime diagnostics" });
  await diagnostics.waitFor();
  const diagnosticText = await diagnostics.innerText();
  for (const [label, value] of [
    ["runtime SHA-256", runtimeSha256],
    ["NEX model", modelId],
    ["application root", applicationRoot],
    ["Data root", path.join(applicationRoot, "Data")],
    ["source root", sourceRoot],
  ]) assert.ok(diagnosticText.includes(value), `native diagnostics omitted ${label}`);
  assert.match(diagnosticText, /Credential captured at startup\s+Yes/i, "native host did not report that it captured the selected provider credential");
  assert.match(diagnosticText, /Ordinary Codex root used\s+false/i, "native host diagnostics did not confirm isolated Codex state");
  result.nativeDiagnostics = {
    exactRuntimeIdentityVisible: diagnosticText.includes(runtimeSha256),
    exactModelVisible: diagnosticText.includes(modelId),
    applicationRootVisible: diagnosticText.includes(applicationRoot),
    dataRootVisible: diagnosticText.includes(path.join(applicationRoot, "Data")),
    sourceRootVisible: diagnosticText.includes(sourceRoot),
    credentialCapturedBooleanVisible: /Credential captured at startup\s+Yes/i.test(diagnosticText),
    ordinaryCodexRootUsedFalseVisible: /Ordinary Codex root used\s+false/i.test(diagnosticText),
  };
  await diagnostics.getByRole("button", { name: "Done" }).click();

  const prompt = [
    "This is a bounded response-size test. Write a substantial, self-contained technical handbook about evidence quality and repeatable QA for desktop agent applications.",
    "Produce 24 numbered sections. Each section must contain two distinct paragraphs of at least 45 words each. Avoid filler and do not repeat earlier material.",
    "Return plain text only. Do not use tools, make network requests, or perform any action. Continue until all sections are complete or the explicit output-token limit stops the response.",
  ].join("\n\n");
  result.request.promptSha256 = createHash("sha256").update(prompt).digest("hex");
  const conversation = page.getByRole("region", { name: "Conversation" });
  await conversation.waitFor();
  await composer.fill(prompt);
  sendStartedAt = Date.now();
  await page.getByRole("button", { name: "Send message" }).click();
  await conversation.locator("article.message-user .message-text").getByText(prompt, { exact: true }).waitFor();
  const assistant = conversation.locator("article.message-assistant").last();
  await assistant.waitFor();

  memorySamples.push(getProcessSample(host.pid));
  memorySampler = setInterval(() => {
    try { memorySamples.push(getProcessSample(host.pid)); }
    catch { /* The QA host may exit while a sample is queued. */ }
  }, 5_000);

  await page.waitForFunction(() => {
    const text = document.querySelector("article.message-assistant:last-of-type .message-text")?.textContent ?? "";
    return text.length > 0;
  }, null, { timeout: 15 * 60_000 });
  firstTextAt = Date.now();
  await page.waitForFunction(() => ["completed", "failed", "interrupted"].includes(
    document.querySelector("section.conversation")?.getAttribute("data-turn-state") ?? ""),
  null, { timeout: 15 * 60_000 });
  memorySamples.push(getProcessSample(host.pid));

  const turnState = await page.locator("section.conversation").getAttribute("data-turn-state");
  const outputText = await assistant.locator(".message-text").innerText();
  const activityCount = await conversation.locator(".activity-card").count();
  const configPath = path.join(applicationRoot, "Data", "CodexHome", "config.toml");
  const config = readFileSync(configPath, "utf8");
  assert.ok(config.includes(`model = \"${modelId}\"`), "effective Codex config did not retain the exact NEX alias");
  assert.ok(config.includes(`openrouter_provider_endpoint = \"${routeTag}\"`), "effective Codex config did not pin the exact NEX route");
  assert.ok(config.includes("env_key = \"OPENROUTER_API_KEY\""), "effective Codex config did not reference the process-only credential by environment variable name");
  assert.ok(!/sk-or-v1-[A-Za-z0-9-]{30,}/i.test(config), "provider credential material was written to Codex config");

  await page.screenshot({ path: screenshotPath, fullPage: false });
  const identityLeaks = collectCredentialPatternEvidence(applicationRoot);
  assert.equal(identityLeaks.credentialPatternFound, false, "a credential-like OpenRouter key pattern was found in application-root text files");

  const outputSha256 = createHash("sha256").update(outputText, "utf8").digest("hex");
  result.response = {
    turnState,
    outputCharacters: outputText.length,
    outputUtf8Bytes: Buffer.byteLength(outputText, "utf8"),
    outputSha256,
    toolActivityCount: activityCount,
    timeToFirstVisibleTextMs: firstTextAt - sendStartedAt,
    totalElapsedMs: Date.now() - sendStartedAt,
    outputCompleteInUi: turnState === "completed",
    modelOutputExceedsMinimum: outputText.length >= minimumVisibleCharacters,
    toolFreeAsRequested: activityCount === 0,
  };
  result.credentialPersistenceScan = identityLeaks;
  result.memory = { sampleCount: memorySamples.length, samples: memorySamples, maxima: memoryMaxima(memorySamples) };
  result.outcome = turnState === "completed"
    && outputText.length >= minimumVisibleCharacters
    && activityCount === 0
    && result.pageIssues.length === 0
    ? "native-live-output-passed-route-request-pinned-provider-route-posthoc-unattested"
    : "native-live-output-incomplete-or-failed";
}

try {
  await run();
} catch (error) {
  result.outcome = "native-live-output-failed";
  result.error = {
    name: error?.name ?? "Error",
    message: typeof error?.message === "string"
      ? error.message.replace(/sk-or-v1-[A-Za-z0-9-]+/g, "<redacted>").slice(0, 500)
      : "Unknown error",
  };
} finally {
  if (memorySampler) clearInterval(memorySampler);
  memorySampler = null;
  if (browser) await browser.close().catch(() => {});
  browser = null;
  if (host && host.exitCode === null) {
    try {
      requestHostClose(host.pid);
      const closed = await waitForExit(host, 30_000);
      result.hostExit = closed;
    } catch (error) {
      result.hostCloseError = String(error?.message ?? error).replace(/sk-or-v1-[A-Za-z0-9-]+/g, "<redacted>").slice(0, 300);
    }
  }
  removePreparationFixture();
  result.finishedAt = new Date().toISOString();
  if (memorySamples.length) result.memory = { sampleCount: memorySamples.length, samples: memorySamples, maxima: memoryMaxima(memorySamples) };
  if (!existsSync(resultPath)) {
    writeFileSync(resultPath, `${JSON.stringify(result, null, 2)}\n`, { encoding: "utf8", flag: "wx" });
  }
  console.log(`P2_11_NATIVE_LIVE_NEX ${JSON.stringify({ outcome: result.outcome, resultPath, response: result.response ?? null, runtime: result.runtime, provider: { model: result.provider.modelAlias, routeTag: result.provider.routeTag, postHocRouteAttestation: result.provider.postHocRouteAttestation }, error: result.error ?? null })}`);
}

if (!result.outcome.startsWith("native-live-output-passed")) process.exitCode = 1;
