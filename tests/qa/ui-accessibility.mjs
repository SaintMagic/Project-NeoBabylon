import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { createServer as createTcpServer } from "node:net";
import { mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { assertQaRunsParent, newQaRun } from "./qa-run-root.mjs";

const [playwrightModulePathArg] = process.argv.slice(2);
assert.ok(playwrightModulePathArg, "usage: node ui-accessibility.mjs <playwright-core-entry.mjs>");
const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const uiRoot = path.join(sourceRoot, "ui", "diagnostic");
const qaParent = assertQaRunsParent(sourceRoot, path.join(sourceRoot, ".local", "Lab", "Runs"));
const playwrightModulePath = path.resolve(playwrightModulePathArg);
const { runRoot } = newQaRun(qaParent, `P2-10-ui-accessibility-${Date.now()}-${process.pid}`);

function reservePort() {
  return new Promise((resolve, reject) => {
    const server = createTcpServer();
    server.once("error", reject);
    server.listen(0, "127.0.0.1", () => {
      const address = server.address();
      server.close((error) => error ? reject(error) : resolve(address.port));
    });
  });
}

async function auditVisibleTextContrast(page, label) {
  return page.evaluate((auditLabel) => {
    const color = (value) => {
      const match = value.match(/rgba?\(([^)]+)\)/);
      if (!match) return null;
      const parts = match[1].split(/[\s,\/]+/).filter(Boolean).map((part) => Number.parseFloat(part));
      return { r: parts[0], g: parts[1], b: parts[2], a: parts[3] ?? 1 };
    };
    const over = (top, bottom) => {
      if (!top) return bottom;
      const a = top.a ?? 1;
      return {
        r: top.r * a + bottom.r * (1 - a),
        g: top.g * a + bottom.g * (1 - a),
        b: top.b * a + bottom.b * (1 - a),
        a: 1,
      };
    };
    const effectiveBackground = (element) => {
      const path = [];
      for (let current = element; current; current = current.parentElement) path.push(current);
      let result = document.documentElement.dataset.appearance === "dark"
        ? { r: 24, g: 24, b: 24, a: 1 }
        : { r: 255, g: 255, b: 255, a: 1 };
      let hasImage = false;
      for (const current of path.reverse()) {
        const style = getComputedStyle(current);
        if (style.backgroundImage !== "none") hasImage = true;
        result = over(color(style.backgroundColor), result);
      }
      return { color: result, hasImage };
    };
    const luminance = ({ r, g, b }) => {
      const channel = (value) => {
        const normalized = value / 255;
        return normalized <= 0.04045 ? normalized / 12.92 : ((normalized + 0.055) / 1.055) ** 2.4;
      };
      return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
    };
    const ratio = (foreground, background) => {
      const values = [luminance(foreground), luminance(background)].sort((a, b) => b - a);
      return Math.round(((values[0] + 0.05) / (values[1] + 0.05)) * 100) / 100;
    };
    const results = [];
    const inspect = (element, text, foregroundValue, bounds) => {
      if (!text?.trim() || element.closest("svg, [aria-hidden='true']")) return;
      const style = getComputedStyle(element);
      if (style.display === "none" || style.visibility === "hidden" || !bounds.width || !bounds.height) return;
      const ancestors = [];
      for (let current = element; current; current = current.parentElement) ancestors.push(current);
      if (ancestors.some((current) => {
        const ancestorStyle = getComputedStyle(current);
        return ancestorStyle.display === "none" || ancestorStyle.visibility === "hidden" || current.inert;
      })) return;
      const background = effectiveBackground(element);
      const foreground = color(foregroundValue);
      if (!foreground || background.hasImage) {
        results.push({
          text: text.trim().replace(/\s+/g, " ").slice(0, 100),
          tag: element.tagName.toLowerCase(),
          className: typeof element.className === "string" ? element.className : "",
          selectorPath: ancestors.slice(0, 5).map((current) => `${current.tagName.toLowerCase()}${current.id ? `#${current.id}` : ""}${typeof current.className === "string" && current.className ? `.${current.className.trim().replace(/\s+/g, ".")}` : ""}`).join(" > "),
          foreground: foregroundValue,
          background: `rgb(${Math.round(background.color.r)}, ${Math.round(background.color.g)}, ${Math.round(background.color.b)})`,
          state: "indeterminate-background",
        });
        return;
      }
      const opacity = ancestors.reduce((value, current) => value * Number.parseFloat(getComputedStyle(current).opacity || "1"), 1);
      foreground.a *= opacity;
      const displayedForeground = over(foreground, background.color);
      const fontSize = Number.parseFloat(style.fontSize) || 0;
      const weight = Number.parseInt(style.fontWeight, 10) || 400;
      const minimum = fontSize >= 24 || (fontSize >= 18.67 && weight >= 700) ? 3 : 4.5;
      const disabled = Boolean(element.closest(":disabled, [aria-disabled='true']"));
      const contrast = ratio(displayedForeground, background.color);
      results.push({
        text: text.trim().replace(/\s+/g, " ").slice(0, 100),
        tag: element.tagName.toLowerCase(),
        className: typeof element.className === "string" ? element.className : "",
        foreground: foregroundValue,
        displayedForeground: `rgb(${Math.round(displayedForeground.r)}, ${Math.round(displayedForeground.g)}, ${Math.round(displayedForeground.b)})`,
        opacity: Math.round(opacity * 1000) / 1000,
        selectorPath: ancestors.slice(0, 5).map((current) => `${current.tagName.toLowerCase()}${current.id ? `#${current.id}` : ""}${typeof current.className === "string" && current.className ? `.${current.className.trim().replace(/\s+/g, ".")}` : ""}`).join(" > "),
        background: `rgb(${Math.round(background.color.r)}, ${Math.round(background.color.g)}, ${Math.round(background.color.b)})`,
        contrast,
        minimum,
        disabled,
        state: contrast < minimum ? "below-aa" : "pass",
      });
    };
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      const range = document.createRange();
      range.selectNodeContents(node);
      const bounds = range.getBoundingClientRect();
      inspect(node.parentElement, node.nodeValue, getComputedStyle(node.parentElement).color, bounds);
    }
    for (const input of document.querySelectorAll("input[placeholder], textarea[placeholder]")) {
      const bounds = input.getBoundingClientRect();
      if (!bounds.width || !bounds.height || !input.placeholder) continue;
      inspect(input, input.placeholder, getComputedStyle(input, "::placeholder").color, bounds);
    }
    const violations = results.filter((item) => item.state === "below-aa");
    const unique = new Map();
    for (const item of violations) {
      const key = [item.className, item.text, item.foreground, item.background, item.contrast, item.disabled].join("|");
      if (!unique.has(key)) unique.set(key, { ...item, occurrences: 0 });
      unique.get(key).occurrences += 1;
    }
    return {
      label: auditLabel,
      appearance: document.documentElement.dataset.appearance,
      visibleTextRuns: results.length,
      belowAaRuns: violations.length,
      indeterminateRuns: results.filter((item) => item.state === "indeterminate-background").length,
      indeterminate: results.filter((item) => item.state === "indeterminate-background"),
      violations: [...unique.values()],
    };
  }, label);
}

async function waitForServer(url, child, timeoutMs = 30_000) {
  const until = Date.now() + timeoutMs;
  while (Date.now() < until) {
    if (child.exitCode !== null) throw new Error(`Vite exited early with code ${child.exitCode}`);
    try {
      const response = await fetch(url);
      if (response.ok) return;
    } catch { /* Vite has not bound the loopback port yet. */ }
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  throw new Error(`Vite did not become ready at ${url}`);
}

const port = await reservePort();
const url = `http://127.0.0.1:${port}/`;
const vite = spawn(process.execPath, [path.join(uiRoot, "node_modules", "vite", "bin", "vite.js"),
  "--host", "127.0.0.1", "--port", String(port), "--strictPort"], { cwd: uiRoot, stdio: ["ignore", "pipe", "pipe"] });
let viteOutput = "";
vite.stdout.on("data", (chunk) => { viteOutput += chunk.toString(); });
vite.stderr.on("data", (chunk) => { viteOutput += chunk.toString(); });

let browser;
let page;
const consoleErrors = [];
const externalRequests = [];
const contrastAudits = [];
const auditContrast = async (label) => contrastAudits.push(await auditVisibleTextContrast(page, label));
try {
  await waitForServer(url, vite);
  const { chromium } = await import(pathToFileURL(playwrightModulePath).href);
  browser = await chromium.launch({ channel: "msedge", headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1 });
  page = await context.newPage();
  page.on("pageerror", (error) => consoleErrors.push(`pageerror: ${error.message}`));
  page.on("console", (message) => { if (message.type() === "error") consoleErrors.push(message.text()); });
  page.on("request", (request) => {
    if (!request.url().startsWith(url)) externalRequests.push(request.url());
  });
  await page.addInitScript(() => {
    const records = [
      { sourceFile: "lmstudio.json", capabilityRecord: {
        providerId: "lmstudio", providerDisplayName: "LM Studio", modelIdentifier: "duplicate-id",
        endpoint: "http://127.0.0.1:1234/v1", contextWindowEffective: { state: "Known", value: 32768 },
        reasoningControls: { state: "Unknown", value: null, evidenceSource: "synthetic UI fixture" },
        toolFunctionCalling: { state: "Known", value: "tool_use", evidenceSource: "synthetic UI fixture" },
        structuredOutput: { state: "Unknown", value: null, evidenceSource: "synthetic UI fixture" },
        providerRoute: { state: "Unknown", value: null, evidenceSource: "synthetic UI fixture" },
      } },
      { sourceFile: "openrouter.json", capabilityRecord: {
        providerId: "openrouter", providerDisplayName: "OpenRouter", modelIdentifier: "duplicate-id",
        endpoint: "https://openrouter.ai/api/v1", contextWindowEffective: { state: "Unknown", value: null },
        reasoningControls: { state: "Unknown", value: null, evidenceSource: "synthetic UI fixture" },
        toolFunctionCalling: { state: "Unknown", value: null, evidenceSource: "synthetic UI fixture" },
        structuredOutput: { state: "Unknown", value: null, evidenceSource: "synthetic UI fixture" },
        providerRoute: { state: "Unknown", value: null, evidenceSource: "synthetic UI fixture" },
      } },
      { sourceFile: "other-model.json", capabilityRecord: {
        providerId: "lmstudio", providerDisplayName: "LM Studio", modelIdentifier: "other-model",
        endpoint: "http://127.0.0.1:1234/v1", contextWindowEffective: { state: "Unknown", value: null },
        reasoningControls: { state: "Unknown", value: null, evidenceSource: "synthetic UI fixture" },
        toolFunctionCalling: { state: "Unknown", value: null, evidenceSource: "synthetic UI fixture" },
        structuredOutput: { state: "Unknown", value: null, evidenceSource: "synthetic UI fixture" },
        providerRoute: { state: "Unknown", value: null, evidenceSource: "synthetic UI fixture" },
      } },
    ];
    const projects = [
      { workspacePath: "C:\\QA\\Alpha", name: "Alpha", available: true },
      { workspacePath: "C:\\QA\\Beta", name: "Beta", available: true },
    ];
    const state = {
      selectedWorkspace: "C:\\QA\\Alpha",
      selectedCapability: records[0].capabilityRecord,
      activeThreadId: null,
      nextThreadId: 0,
      turnCount: 0,
      pendingTurn: null,
      requests: [],
      listeners: [],
    };
    const dispatch = (data) => { for (const listener of state.listeners) listener({ data }); };
    const notify = (requestId, method, params) => dispatch({ requestId, ok: true, stream: true, method, params });
    const runtime = () => ({
      selectedWorkspace: state.selectedWorkspace,
      selectedWorkspaceAvailable: true,
      projects,
      runtime: { version: "P2-10 deterministic UI host" },
      executionPolicy: { requestedToolPolicy: "unrestricted", configSandboxMode: "danger-full-access", approvalPolicy: "never" },
      dataRoot: "C:\\QA\\Application\\Data",
      codexHome: "C:\\QA\\IsolatedCodexHome",
    });
    const savedThreads = [
      { id: "saved-alpha", preview: "Saved Alpha accessibility task", modelProvider: "lmstudio", model: "duplicate-id", cwd: "C:\\QA\\Alpha", updatedAt: 2 },
      { id: "saved-beta", preview: "Saved Beta accessibility task", modelProvider: "lmstudio", model: "duplicate-id", cwd: "C:\\QA\\Beta", updatedAt: 1 },
    ];
    const responseFor = (message) => {
      switch (message.operation) {
        case "setAppearance": return { appearance: message.appearance };
        case "getRuntimeStatus": return runtime();
        case "getDiagnostics": return { capabilityRecord: state.selectedCapability, providerCredentialCaptured: false, capabilityRecordPath: "C:\\QA\\capability.json" };
        case "listCapabilities": return { records };
        case "listThreads": return { threads: savedThreads, nextCursor: null, historyQuery: { unresolvedForkBookmarkCount: 0 } };
        case "selectProject":
          state.selectedWorkspace = message.workspacePath;
          return { ...runtime(), selectedWorkspace: state.selectedWorkspace };
        case "addProject": return { ...runtime(), cancelled: true };
        case "selectCapability": {
          const record = records.find((entry) => entry.capabilityRecord.providerId === message.providerId
            && entry.capabilityRecord.modelIdentifier === message.modelIdentifier)?.capabilityRecord;
          if (!record) throw new Error("fixture refused an unknown capability binding");
          state.selectedCapability = record;
          return { capabilityRecord: record, threadId: state.activeThreadId };
        }
        case "resumeThread": return {
          threadId: message.threadId, cwd: state.selectedWorkspace, modelProvider: "lmstudio", model: "duplicate-id",
          capabilityRecord: records[0].capabilityRecord, executionEligible: true, turns: [],
        };
        case "newTask": state.activeThreadId = null; return {};
        case "startThread":
          state.activeThreadId = `ui-thread-${++state.nextThreadId}`;
          return { executionEligible: true, thread: { thread: { id: state.activeThreadId } }, attributedTo: "Codex App Server" };
        case "startTurn": return new Promise((resolve) => {
          const requestId = message.requestId;
          const threadId = state.activeThreadId;
          const turnId = `ui-turn-${++state.turnCount}`;
          state.pendingTurn = { requestId, threadId, turnId, resolve };
          setTimeout(() => {
            notify(requestId, "turn/started", { threadId, turn: { id: turnId } });
            if (state.turnCount === 1) {
              notify(requestId, "turn/diff/updated", { threadId, turnId, diff: "--- a/accessibility.txt\\n+++ b/accessibility.txt\\n+mock review evidence" });
              notify(requestId, "item/started", { item: { id: "tool-fail", type: "commandExecution", command: "cmd /c ver" } });
              notify(requestId, "item/completed", { item: { id: "tool-fail", type: "commandExecution", command: "cmd /c ver", status: "failed", error: "Denied by deterministic fixture." } });
              notify(requestId, "neobabylon/approvalRequested", {
                requestId: 71, approvalInstanceId: "approval-fixture-71", method: "item/commandExecution/requestApproval",
                params: { threadId, turnId, command: "cmd /c ver", cwd: state.selectedWorkspace, reason: "UI-only approval accessibility fixture", availableDecisions: ["accept", "decline", "cancel"] },
              });
              resolve({ eventType: "turnFailure", terminal: true, turnStatus: "failed",
                assistantText: "The deterministic tool fixture reported failure.",
                failure: { message: "The ordinary command failed in this UI fixture." },
                toolDiagnostics: [{ itemId: "tool-fail", toolName: "cmd /c ver", output: "Denied by deterministic fixture.", succeeded: false }] });
              state.pendingTurn = null;
            } else if (state.turnCount === 2) {
              notify(requestId, "item/started", { item: { id: "tool-unknown", type: "commandExecution", command: "unknown status fixture command" } });
              notify(requestId, "item/completed", { item: { id: "tool-unknown", type: "commandExecution", command: "unknown status fixture command", status: "future-provider-status" } });
              resolve({ eventType: "turnComplete", terminal: true, turnStatus: "completed",
                assistantText: "Unknown tool status stays informational." });
              state.pendingTurn = null;
            }
          }, 10);
        });
        case "interruptTurn": {
          const pending = state.pendingTurn;
          if (pending) {
            state.pendingTurn = null;
            pending.resolve({ eventType: "turnInterrupted", terminal: true, turnStatus: "interrupted" });
          }
          return {};
        }
        case "respondToApproval": return { accepted: true };
        default: throw new Error(`Unexpected fixture operation: ${message.operation}`);
      }
    };
    window.__qaAccessibilityState = state;
    window.chrome = window.chrome || {};
    window.chrome.webview = {
      addEventListener(type, listener) { if (type === "message") state.listeners.push(listener); },
      postMessage(message) {
        state.requests.push(message);
        Promise.resolve().then(async () => {
          try {
            const result = await responseFor(message);
            dispatch({ requestId: message.requestId, ok: true, result });
          } catch (error) {
            dispatch({ requestId: message.requestId, ok: false, error: { message: error.message } });
          }
        });
      },
    };
  });

  await page.goto(url, { waitUntil: "networkidle" });
  await page.addStyleTag({ content: "*, *::before, *::after { animation-delay: 0s !important; animation-duration: 0s !important; transition-delay: 0s !important; transition-duration: 0s !important; }" });
  await page.getByRole("heading", { name: /what are we working on today/i }).waitFor();
  await page.locator("button.project-row[aria-current='page']").waitFor();
  assert.equal(await page.locator("html").getAttribute("data-appearance"), "dark", "first-run appearance should be dark");
  assert.equal(await page.getByRole("textbox", { name: "Message NeoBabylon" }).count(), 1);
  assert.equal(await page.getByRole("searchbox", { name: "Search loaded conversations" }).count(), 1);
  await page.screenshot({ path: path.join(runRoot, "dark-1440x900.png"), fullPage: true });
  await auditContrast("dark-home");

  const betaProject = page.locator("button.project-row").filter({ hasText: "Beta" });
  await betaProject.focus();
  await page.keyboard.press("Enter");
  await page.waitForFunction(() => window.__qaAccessibilityState.requests.some((request) => request.operation === "selectProject"));
  await page.waitForFunction(() => document.activeElement?.getAttribute("aria-label") === "Message NeoBabylon");
  assert.equal(await betaProject.getAttribute("aria-current"), "page", "selected project should be exposed as current");

  const search = page.getByRole("searchbox", { name: "Search loaded conversations" });
  await search.fill("Saved Beta");
  const savedBeta = page.locator("button.saved-chat").filter({ hasText: "Saved Beta accessibility task" });
  await savedBeta.focus();
  await page.keyboard.press("Enter");
  await page.waitForFunction(() => window.__qaAccessibilityState.requests.some((request) => request.operation === "resumeThread"));
  await page.waitForFunction(() => document.activeElement?.getAttribute("aria-label") === "Message NeoBabylon");
  assert.equal(await page.locator("button.saved-chat[aria-current='page']").getAttribute("aria-current"), "page");

  const newTask = page.getByRole("button", { name: "New task" });
  await newTask.focus();
  await page.keyboard.press("Enter");
  await page.waitForFunction(() => window.__qaAccessibilityState.requests.some((request) => request.operation === "newTask"));
  const modelPicker = page.getByRole("button", { name: /LM Studio.*duplicate-id/ });
  await modelPicker.focus();
  await page.keyboard.press("Enter");
  const listbox = page.getByRole("listbox", { name: "VERIFIED CAPABILITY RECORDS" });
  await listbox.waitFor();
  await auditContrast("dark-model-picker");
  await page.waitForFunction(() => document.activeElement?.getAttribute("role") === "listbox");
  assert.equal(await listbox.getAttribute("aria-activedescendant"), "model-option-0");
  assert.equal(await listbox.evaluate((element) => getComputedStyle(element).outlineWidth), "3px", "keyboard focus ring should be visible on the listbox");
  await page.keyboard.press("ArrowDown");
  assert.equal(await listbox.getAttribute("aria-activedescendant"), "model-option-1");
  await page.keyboard.press("Enter");
  await page.waitForFunction(() => window.__qaAccessibilityState.requests.some((request) =>
    request.operation === "selectCapability" && request.providerId === "openrouter" && request.modelIdentifier === "duplicate-id"));
  await page.getByRole("button", { name: /OpenRouter.*duplicate-id/ }).waitFor();
  assert.equal(await page.locator("html").getAttribute("data-appearance"), "dark");

  const composer = page.getByRole("textbox", { name: "Message NeoBabylon" });
  await composer.fill("Accessibility UI fixture only");
  await composer.press("Enter");
  await page.getByRole("alert").filter({ hasText: "ordinary command failed in this UI fixture" }).waitFor();
  await page.getByRole("group", { name: "cmd /c ver, failed, Codex App Server" }).waitFor();
  const approvalRegion = page.getByRole("region", { name: "Command approval" });
  await approvalRegion.waitFor();
  await auditContrast("dark-failure-and-approval");
  await approvalRegion.getByRole("button", { name: "Deny for Command approval, request 71", exact: true }).focus();
  await page.keyboard.press("Enter");
  await page.waitForFunction(() => window.__qaAccessibilityState.requests.some((request) =>
    request.operation === "respondToApproval" && request.decision === "decline"));

  const reviewButton = page.getByRole("button", { name: "Review changes" });
  await reviewButton.focus();
  await page.keyboard.press("Enter");
  const reviewDialog = page.getByRole("dialog", { name: "Review changes" });
  await reviewDialog.waitFor();
  await auditContrast("dark-review-dialog");
  assert.equal(await page.locator("main").evaluate((element) => element.inert), true, "background main must be inert in a modal");
  assert.equal(await reviewDialog.locator("button").first().evaluate((element) => document.activeElement === element), true,
    "review drawer should place focus on its close control");
  const reviewContent = reviewDialog.getByRole("region", { name: "Change review content" });
  const reviewDone = reviewDialog.getByRole("button", { name: "Done" });
  await page.keyboard.press("Tab");
  assert.equal(await reviewContent.evaluate((element) => document.activeElement === element), true,
    "Tab should reach the named keyboard stop for scrollable review content");
  await page.keyboard.press("Tab");
  assert.equal(await reviewContent.locator("pre[tabindex='0']").evaluate((element) => document.activeElement === element), true,
    "Tab should reach the separately scrollable diff");
  await page.keyboard.press("Tab");
  assert.equal(await reviewDone.evaluate((element) => document.activeElement === element), true,
    "Tab should move from the diff to Done");
  await page.keyboard.press("Tab");
  assert.equal(await reviewDialog.locator("button").first().evaluate((element) => document.activeElement === element), true,
    "Tab at the last review control should wrap to the first");
  await page.keyboard.press("Shift+Tab");
  assert.equal(await reviewDialog.locator("button").last().evaluate((element) => document.activeElement === element), true,
    "Shift+Tab at the first review control should wrap to the last");
  await page.keyboard.press("Escape");
  await reviewDialog.waitFor({ state: "detached" });
  await page.waitForFunction(() => document.activeElement?.textContent?.trim() === "Review changes");
  assert.equal(await reviewButton.evaluate((element) => document.activeElement === element), true,
    "closing review should restore focus to its opener");

  const unknownStatusComposer = page.getByRole("textbox", { name: "Message NeoBabylon" });
  await unknownStatusComposer.fill("Unknown activity status fixture only");
  await unknownStatusComposer.press("Enter");
  await page.getByRole("group", { name: "unknown status fixture command, info, Codex App Server" }).waitFor();
  assert.equal(await page.getByRole("group", { name: "unknown status fixture command, succeeded, Codex App Server" }).count(), 0,
    "an unknown App Server tool status must not be shown as success");

  const diagnosticsButton = page.getByRole("button", { name: "Open diagnostics" });
  await diagnosticsButton.focus();
  await page.keyboard.press("Enter");
  const diagnosticsDialog = page.getByRole("dialog", { name: "Runtime diagnostics" });
  await diagnosticsDialog.waitFor();
  await auditContrast("dark-diagnostics-dialog");
  assert.equal(await page.locator("aside.sidebar").evaluate((element) => element.inert), true,
    "background navigation must be inert in diagnostics");
  await page.keyboard.press("Shift+Tab");
  assert.equal(await diagnosticsDialog.locator("button").last().evaluate((element) => document.activeElement === element), true,
    "Shift+Tab from close should wrap to the last diagnostics control");
  await page.keyboard.press("Escape");
  await diagnosticsDialog.waitFor({ state: "detached" });
  await page.waitForFunction(() => document.activeElement?.getAttribute("aria-label") === "Open diagnostics");
  assert.equal(await diagnosticsButton.evaluate((element) => document.activeElement === element), true,
    "closing diagnostics should restore focus to its opener");

  await page.getByRole("button", { name: "Switch to light mode" }).focus();
  await page.keyboard.press("Enter");
  assert.equal(await page.locator("html").getAttribute("data-appearance"), "light");
  const lightModelPicker = page.getByRole("button", { name: /OpenRouter.*duplicate-id/ });
  await lightModelPicker.focus();
  await page.keyboard.press("Enter");
  const lightListbox = page.getByRole("listbox", { name: "VERIFIED CAPABILITY RECORDS" });
  await lightListbox.waitFor();
  await auditContrast("light-model-picker");
  await page.keyboard.press("Escape");
  await lightListbox.waitFor({ state: "detached" });
  const lightDiagnosticsButton = page.getByRole("button", { name: "Open diagnostics" });
  await lightDiagnosticsButton.focus();
  await page.keyboard.press("Enter");
  const lightDiagnosticsDialog = page.getByRole("dialog", { name: "Runtime diagnostics" });
  await lightDiagnosticsDialog.waitFor();
  await auditContrast("light-diagnostics-dialog");
  await page.keyboard.press("Escape");
  await lightDiagnosticsDialog.waitFor({ state: "detached" });
  assert.equal(await lightDiagnosticsButton.evaluate((element) => document.activeElement === element), true,
    "closing light diagnostics should restore focus to its opener");
  await page.setViewportSize({ width: 1024, height: 768 });
  await page.screenshot({ path: path.join(runRoot, "light-1024x768.png"), fullPage: true });
  await auditContrast("light-home");

  await page.getByRole("button", { name: "New task" }).focus();
  await page.keyboard.press("Enter");
  const stopDraft = page.getByRole("textbox", { name: "Message NeoBabylon" });
  await stopDraft.fill("Accessibility stop fixture only");
  await stopDraft.press("Enter");
  await page.getByRole("button", { name: "Stop turn" }).waitFor();
  await auditContrast("light-active-turn");
  await page.getByRole("button", { name: "Stop turn" }).focus();
  await page.keyboard.press("Enter");
  await page.waitForFunction(() => window.__qaAccessibilityState.requests.some((request) => request.operation === "interruptTurn"));
  await page.waitForFunction(() => document.querySelector(".composer")?.classList.contains("composer-busy") === false);
  assert.ok(await page.getByRole("status").filter({ hasText: "Turn interrupted" }).count() > 0,
    "the deterministic stop path should show an attributed interrupted status");

  const requestSummary = await page.evaluate(() => window.__qaAccessibilityState.requests.map((request) => ({
    operation: request.operation,
    providerId: request.providerId,
    modelIdentifier: request.modelIdentifier,
    workspacePath: request.workspacePath,
    decision: request.decision,
  })));
  if (process.argv.includes("--product-parity")) {
    const { verifyProductParityUi } = await import("./product-parity-ui-checks.mjs");
    await verifyProductParityUi(page, runRoot);
  }
  assert.deepEqual(consoleErrors, [], "browser console should stay error-free");
  assert.deepEqual(externalRequests, [], "the deterministic UI qualification must not contact a provider or external site");
  assert.ok(requestSummary.some((request) => request.operation === "startTurn"), "expected a mocked task send");
  const evidence = {
    kind: "deterministic UI accessibility fixture; not live inference or real App Server",
    browser: await browser.version(),
    url,
    viewports: ["1440x900 dark", "1024x768 light"],
    flows: ["project selection and focus return", "loaded-history search and resume", "model list arrow/enter and exact provider binding", "send and attributed tool failure", "unknown tool status remains informational", "approval deny", "review dialog Tab/Shift+Tab/Escape and focus restore", "diagnostics inert/keyboard/Escape and focus restore", "stop-turn keyboard activation"],
    requestSummary,
    contrastAudits,
    consoleErrors,
    externalRequests,
    nativeWpf: false,
    narrator: "not exercised by this browser-only run",
  };
  writeFileSync(path.join(runRoot, "accessibility-evidence.json"), `${JSON.stringify(evidence, null, 2)}\n`, "utf8");
  const contrastFailures = contrastAudits.flatMap((audit) => audit.violations.filter((item) => !item.disabled)
    .map((item) => ({ ...item, audit: audit.label, appearance: audit.appearance })));
  const inactiveContrastExceptions = contrastAudits.flatMap((audit) => audit.violations.filter((item) => item.disabled)
    .map((item) => ({ ...item, audit: audit.label, appearance: audit.appearance })));
  const contrastUnknowns = contrastAudits.flatMap((audit) => audit.indeterminate.map((item) => ({ ...item, audit: audit.label, appearance: audit.appearance })));
  console.log(`P2_10_UI_ACCESSIBILITY result=${contrastFailures.length || inactiveContrastExceptions.length || contrastUnknowns.length ? "contrast-fail-or-unknown" : "pass"} browser=${evidence.browser} flows=${evidence.flows.length} contrastAudits=${contrastAudits.length} contrastFailures=${contrastFailures.length} inactiveContrastExceptions=${inactiveContrastExceptions.length} contrastUnknowns=${contrastUnknowns.length} nativeWpf=false narrator=not-run evidence=${runRoot}`);
  if (contrastFailures.length || inactiveContrastExceptions.length || contrastUnknowns.length) {
    if (contrastFailures.length) console.error(`P2_10_CONTRAST_FAILURES ${JSON.stringify(contrastFailures, null, 2)}`);
    if (inactiveContrastExceptions.length) console.error(`P2_10_INACTIVE_CONTRAST_EXCEPTIONS ${JSON.stringify(inactiveContrastExceptions, null, 2)}`);
    if (contrastUnknowns.length) console.error(`P2_10_CONTRAST_UNKNOWN ${JSON.stringify(contrastUnknowns, null, 2)}`);
    process.exitCode = 1;
  }
} finally {
  if (browser) await browser.close();
  if (vite.exitCode === null) vite.kill();
  if (vite.exitCode === null) await new Promise((resolve) => vite.once("exit", resolve));
}
