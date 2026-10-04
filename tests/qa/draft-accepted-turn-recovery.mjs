import assert from "node:assert/strict";
import { mkdirSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const [portText, playwrightModulePath, screenshotDirectory, ...requestedScenarios] = process.argv.slice(2);
const port = Number(portText);
assert.ok(Number.isInteger(port) && port >= 1024 && port <= 65535, "a valid local Vite port is required");
assert.ok(playwrightModulePath && screenshotDirectory, "Playwright module and screenshot directory are required");
const scenarios = requestedScenarios.length ? requestedScenarios : ["accepted", "unaccepted"];
assert.ok(scenarios.every((scenario) => scenario === "accepted" || scenario === "unaccepted"), "scenarios must be accepted and/or unaccepted");

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const capability = JSON.parse(readFileSync(resolve(repositoryRoot,
  "docs/release/MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json"), "utf8"));
const { chromium } = await import(pathToFileURL(playwrightModulePath).href);
const browser = await chromium.launch({ channel: "msedge", headless: true });
const issues = [];
mkdirSync(screenshotDirectory, { recursive: true });

const hostFixture = ({ capability: selectedCapability, scenario, workspace }) => {
  const stateKey = "neoBabylon.qaAppServerState";
  const state = JSON.parse(localStorage.getItem(stateKey) ?? "null") ?? {
    threadId: null,
    turns: [],
    turnStartRequests: 0,
    resumeThreadRequests: 0,
    acceptedPrompt: null,
  };
  const listeners = new Set();
  const save = () => localStorage.setItem(stateKey, JSON.stringify(state));
  const threadRow = () => ({
    id: state.threadId,
    cwd: workspace,
    modelProvider: selectedCapability.providerId,
    model: selectedCapability.modelIdentifier,
    preview: state.acceptedPrompt ?? "Recovered task",
    updatedAt: 1,
  });
  const runtime = {
    attributedTo: "NeoBabylon.Host",
    runtime: { kind: "https://github.com/openai/codex.git", version: "0.155.1", sourceRef: "rust-v0.155.1",
      sourceRevision: "be2951ea34f0d295ed0becf97079f92fa5f6950e", binaryPath: "isolated-test-runtime",
      sha256: "0".repeat(64), protocol: "stable" },
    sourceRepositoryRoot: "D:\\CODING\\NeoBabylon",
    applicationRoot: "D:\\QA\\NeoBabylon-Phase2",
    dataRoot: "D:\\QA\\NeoBabylon-Phase2\\Data",
    webView2UserDataFolder: "D:\\QA\\NeoBabylon-Phase2\\Data\\WebView2",
    codexHome: "D:\\QA\\NeoBabylon-Phase2\\Data\\CodexHome",
    fixtureWorkspace: workspace,
    selectedWorkspace: workspace,
    selectedWorkspaceAvailable: true,
    projects: [{ name: "QA Workspace", workspacePath: workspace, available: true }],
    ordinaryCodexRootUsed: false,
    executionPolicy: { requestedToolPolicy: "unrestricted", configSandboxMode: "danger-full-access",
      approvalPolicy: "never", containedToolsQualified: false, selectionSource: "NeoBabylon product policy" },
  };
  const threadResult = () => ({ attributedTo: "Codex App Server", threadId: state.threadId,
    cwd: workspace, modelProvider: selectedCapability.providerId, model: selectedCapability.modelIdentifier,
    turns: state.turns, historyTruncated: false, savedReviews: [] });

  function respond(request, result) {
    setTimeout(() => {
      const response = { requestId: request.requestId, ok: true, result };
      for (const listener of listeners) listener(new MessageEvent("message", { data: response }));
    }, 0);
  }

  function respondFailure(request, message) {
    setTimeout(() => {
      const response = { requestId: request.requestId, ok: false,
        error: { attributedTo: "NeoBabylon.Host", type: "upstreamFailure", message } };
      for (const listener of listeners) listener(new MessageEvent("message", { data: response }));
    }, 0);
  }

  const webview = {
    addEventListener(_type, listener) { listeners.add(listener); },
    postMessage(request) {
      const payload = { ...request };
      delete payload.operation;
      delete payload.requestId;
      switch (request.operation) {
        case "getRuntimeStatus": respond(request, runtime); break;
        case "getDiagnostics": respond(request, { attributedTo: "NeoBabylon.Host",
          capabilityRecordPath: "D:\\CODING\\NeoBabylon\\docs\\release\\MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json",
          providerCredentialCaptured: false, capabilityRecord: selectedCapability }); break;
        case "listCapabilities": respond(request, { attributedTo: "NeoBabylon.Host",
          selectedProviderId: selectedCapability.providerId, selectedModelIdentifier: selectedCapability.modelIdentifier,
          records: [{ sourceFile: "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json", capabilityRecord: selectedCapability }],
          rejectedRecords: [] }); break;
        case "setAppearance": respond(request, { appearance: payload.appearance }); break;
        case "listThreads": respond(request, { attributedTo: "Codex App Server",
          threads: state.threadId && state.turns.length ? [threadRow()] : [], nextCursor: null }); break;
        case "startThread":
          state.threadId = `qa-${scenario}`;
          state.turns = [];
          save();
          respond(request, { attributedTo: "Codex App Server", thread: { thread: { id: state.threadId } } });
          break;
        case "startTurn":
          state.turnStartRequests += 1;
          if (scenario === "accepted") {
            state.acceptedPrompt = payload.text;
            state.turns = [{ id: "turn-accepted-by-app-server", status: "interrupted", items: [
              { id: "user-accepted-by-app-server", type: "userMessage", text: payload.text },
            ] }];
          }
          save();
          window.__neoBabylonQATurnStartObserved = true;
          // Deliberately omit turn/started and the operation response to simulate
          // WPF/WebView termination after the App Server accepted the turn.
          break;
        case "resumeThread":
          state.resumeThreadRequests += 1;
          save();
          if (state.turns.length === 0) respondFailure(request, "App Server thread/read failed: thread not loaded");
          else respond(request, threadResult());
          break;
        case "newTask": respond(request, { attributedTo: "NeoBabylon.Host", newTaskStarted: true }); break;
        default: respond(request, {});
      }
    },
  };
  window.chrome = window.chrome ?? {};
  window.chrome.webview = webview;
  window.__neoBabylonQAServerState = state;
};

async function runScenario(scenario) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();
  page.on("pageerror", (error) => issues.push(`${scenario}: ${error.message}`));
  page.on("console", (message) => {
    if (message.type() === "error" || message.type() === "warning") issues.push(`${scenario}: ${message.type()}: ${message.text()}`);
  });
  await page.addInitScript(hostFixture, {
    capability,
    scenario,
    workspace: "D:\\QA\\Project Alpha",
  });
  await page.goto(`http://127.0.0.1:${port}/`);
  await page.waitForFunction(() => document.title === "NeoBabylon", null, { timeout: 10000 });
  const pageTitle = await page.title();
  if (pageTitle !== "NeoBabylon") {
    console.error(`Page identity mismatch: ${JSON.stringify({ scenario, url: page.url(), pageTitle,
      readyState: await page.evaluate(() => document.readyState), body: await page.locator("body").innerText().catch(() => "unavailable"), issues })}`);
    await page.screenshot({ path: resolve(screenshotDirectory, `draft-recovery-${scenario}-page-error.png`) });
  }
  assert.equal(pageTitle, "NeoBabylon");
  await page.getByText("Desktop host ready").waitFor({ timeout: 20000 });

  const prompt = scenario === "accepted" ? "Run the accepted QA recovery prompt" : "Keep this unaccepted QA prompt";
  const composer = page.getByRole("textbox", { name: "Message NeoBabylon" });
  await composer.fill(prompt);
  await page.getByRole("button", { name: "Send message" }).click();
  await page.waitForFunction(() => window.__neoBabylonQATurnStartObserved === true, null, { timeout: 15000 });

  const beforeReload = await page.evaluate(() => ({
    acceptedPrompt: window.__neoBabylonQAServerState.acceptedPrompt,
    turnStartRequests: window.__neoBabylonQAServerState.turnStartRequests,
    draftEntries: Object.entries(localStorage).filter(([key]) => key.startsWith("neobabylon.draft:v1:")),
  }));
  await page.reload();
  await page.getByText("Desktop host ready").waitFor({ timeout: 20000 });
  await page.getByRole("region", { name: "Conversation" }).waitFor();

  const recovered = await page.evaluate(() => ({
    turnStartRequests: window.__neoBabylonQAServerState.turnStartRequests,
    resumeThreadRequests: window.__neoBabylonQAServerState.resumeThreadRequests,
    state: window.__neoBabylonQAServerState.turns[0]?.status ?? "idle",
    turns: window.__neoBabylonQAServerState.turns,
    drafts: Object.fromEntries(Object.entries(localStorage).filter(([key]) => key.startsWith("neobabylon.draft:v1:"))),
  }));
  assert.equal(recovered.turnStartRequests, 1, "reconnect must not replay the submitted turn");
  assert.equal(recovered.resumeThreadRequests, 1,
    "an exact active-task hint must be verified through thread/resume even when an empty thread is omitted from saved history");
  const restoredPromptCount = await page.locator(".transcript").getByText(prompt, { exact: true }).count();
  if (restoredPromptCount !== (scenario === "accepted" ? 1 : 0)) {
    await page.screenshot({ path: resolve(screenshotDirectory, `draft-recovery-${scenario}-mismatch.png`) });
    console.error(`Recovery transcript mismatch: ${JSON.stringify({ scenario, prompt, restoredPromptCount, recovered,
      conversation: await page.getByRole("region", { name: "Conversation" }).innerText() })}`);
  }
  assert.equal(restoredPromptCount, scenario === "accepted" ? 1 : 0,
    "the transcript must reflect only App Server-owned accepted history");

  if (scenario === "accepted") {
    assert.equal(recovered.state, "interrupted", "accepted work must restore with its authoritative interrupted state");
    await page.getByRole("button", { name: "New task" }).click();
    const newTaskDraftKey = "neobabylon.draft:v1:d%3A%2Fqa%2Fproject%20alpha:new";
    try {
      await page.waitForFunction((key) => {
        const composerValue = document.querySelector('textarea[aria-label="Message NeoBabylon"]')?.value;
        return localStorage.getItem("neobabylon.active-task:v1") === null
          && document.querySelector(".breadcrumb-current")?.textContent?.trim() === "New task"
          && composerValue === (localStorage.getItem(key) ?? "");
      }, newTaskDraftKey, { timeout: 10000 });
    } catch (error) {
      const observed = await page.evaluate(() => ({
        activeTask: localStorage.getItem("neobabylon.active-task:v1"),
        drafts: Object.fromEntries(Object.entries(localStorage).filter(([key]) => key.startsWith("neobabylon.draft:v1:"))),
        conversation: document.querySelector('[aria-label="Conversation"]')?.textContent?.slice(0, 500),
        breadcrumb: document.querySelector(".breadcrumb-current")?.textContent,
        composer: document.querySelector('textarea[aria-label="Message NeoBabylon"]')?.value,
        body: document.body.innerText.slice(0, 1200),
      }));
      console.error(`Post-New-task UI did not settle: ${JSON.stringify(observed)}`);
      throw error;
    }
  } else {
    await page.waitForFunction(() => {
      const composer = document.querySelector('textarea[aria-label="Message NeoBabylon"]');
      return composer && !composer.disabled && composer.value.length > 0;
    }, null, { timeout: 15000 });
    const sourceDraftKey = "neobabylon.draft:v1:d%3A%2Fqa%2Fproject%20alpha:new";
    assert.equal(await composer.inputValue(), prompt,
      "an unaccepted prompt must remain visible in the new-task composer when App Server cannot reload its empty thread");
    assert.equal(recovered.drafts[sourceDraftKey], prompt,
      "the source draft must remain available after the empty-thread resume failure");
    assert.match(await page.getByRole("alert").first().innerText(), /thread not loaded/i,
      "the UI must expose the attributed reconnect failure instead of implying the task was restored");
    await page.screenshot({ path: resolve(screenshotDirectory, `draft-recovery-${scenario}.png`) });
  }

  await page.screenshot({ path: resolve(screenshotDirectory, `draft-recovery-${scenario}.png`) });
  const finalDraft = await composer.inputValue();
  assert.equal(finalDraft, scenario === "accepted" ? "" : prompt,
    scenario === "accepted"
      ? "a prompt already present in restored App Server history must not reappear as a new-task draft"
      : "an unaccepted prompt must remain available in the new-task composer when resume fails");
  await context.close();
  return { scenario,
    flow: scenario === "accepted"
      ? "accepted turn -> renderer restart -> exact task resume -> reconciliation clears stale drafts"
      : "unaccepted turn -> renderer restart -> exact resume refused (thread not loaded) -> source draft retained; no replay",
    beforeReload, recovered, finalDraft };
}

try {
  const results = [];
  for (const scenario of scenarios) results.push(await runScenario(scenario));
  assert.deepEqual(issues, []);
  console.log(JSON.stringify({ passed: true,
    browser: "Microsoft Edge via Playwright Core; Browser plugin unavailable",
    viewport: "1440x900", results, issues }));
} finally {
  await browser.close();
}
