import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { spawn, spawnSync } from "node:child_process";
import { createServer } from "node:net";
import {
  existsSync,
  mkdirSync,
  readFileSync,
  writeFileSync,
} from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { assertQaRunsParent, newQaRun } from "./qa-run-root.mjs";

const [hostPathArg, playwrightModulePathArg, qaParentArg] = process.argv.slice(2);
assert.ok(hostPathArg && playwrightModulePathArg && qaParentArg,
  "usage: node native-visual-acceptance.mjs <NeoBabylon.Host.exe> <playwright-core-entry.mjs> <exact .local\\Lab\\Runs parent>");

const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const hostPath = path.resolve(hostPathArg);
const playwrightModulePath = path.resolve(playwrightModulePathArg);
const qaParent = path.resolve(qaParentArg);
assertQaRunsParent(sourceRoot, qaParent);
for (const [label, filePath] of [["WPF host", hostPath], ["Playwright module", playwrightModulePath]]) {
  assert.ok(existsSync(filePath), `${label} does not exist: ${filePath}`);
}

const runtimeLock = JSON.parse(readFileSync(path.join(sourceRoot, "runtime", "runtime-lock.json"), "utf8"));
const runtimePath = path.resolve(sourceRoot, runtimeLock.runtime.appServerBinaryRelativePath);
assert.ok(existsSync(runtimePath), `pinned App Server binary is missing: ${runtimePath}`);
const runtimeHash = createHash("sha256").update(readFileSync(runtimePath)).digest("hex");
assert.equal(runtimeHash, runtimeLock.runtime.sha256.toLowerCase(), "pinned App Server binary does not match runtime-lock.json");

const { runRoot, applicationRoot } = newQaRun(qaParent, `P2-12-native-visual-${Date.now()}-${process.pid}`);
assert.ok(!existsSync(applicationRoot), "refusing to reuse an existing application root");
const resultPath = path.join(runRoot, "result.json");
const screenshots = Object.fromEntries([
  "dark-desktop",
  "dark-compact",
  "light-compact",
  "light-desktop",
  "light-after-restart",
  "dark-after-toggle",
  "dark-after-restart",
].flatMap((name) => [[`${name}WebView`, path.join(runRoot, `${name}-webview.png`)],
  [`${name}Native`, path.join(runRoot, `${name}-window.png`)]]));
const capabilityPath = path.join(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json");
assert.ok(existsSync(capabilityPath), `default local capability record is missing: ${capabilityPath}`);

const nativeWindowType = String.raw`
using System;
using System.Runtime.InteropServices;
public static class NeoBabylonVisualCapture {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
  [DllImport("user32.dll", SetLastError=true)] public static extern bool GetWindowRect(IntPtr handle, out RECT rect);
  [DllImport("user32.dll", SetLastError=true)] public static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
  [DllImport("user32.dll", SetLastError=true)] public static extern bool PrintWindow(IntPtr handle, IntPtr hdc, uint flags);
}`;

function powerShell(command) {
  const encoded = Buffer.from(command, "utf16le").toString("base64");
  const result = spawnSync("powershell.exe", ["-NoProfile", "-NonInteractive", "-EncodedCommand", encoded], {
    encoding: "utf8",
    timeout: 20000,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`PowerShell native-window operation failed: ${result.stderr || result.stdout}`);
  return result.stdout.trim();
}

function setNativeWindowSize(pid, width, height) {
  const command = `
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
${nativeWindowType}
'@
$windowProcess = Get-Process -Id ${pid} -ErrorAction Stop
$handle = $windowProcess.MainWindowHandle
if ($handle -eq 0) { throw 'QA WPF host has no main window handle.' }
$before = New-Object NeoBabylonVisualCapture+RECT
if (-not [NeoBabylonVisualCapture]::GetWindowRect($handle, [ref]$before)) { throw 'GetWindowRect failed.' }
$flags = [uint32]0x0014
if (-not [NeoBabylonVisualCapture]::SetWindowPos($handle, [IntPtr]::Zero, $before.Left, $before.Top, ${width}, ${height}, $flags)) { throw 'SetWindowPos failed.' }
Start-Sleep -Milliseconds 300
$after = New-Object NeoBabylonVisualCapture+RECT
if (-not [NeoBabylonVisualCapture]::GetWindowRect($handle, [ref]$after)) { throw 'GetWindowRect failed after resize.' }
[pscustomobject]@{ x=$after.Left; y=$after.Top; width=($after.Right-$after.Left); height=($after.Bottom-$after.Top) } | ConvertTo-Json -Compress
`;
  return JSON.parse(powerShell(command));
}

function getNativeWindowBounds(pid) {
  const command = `
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
${nativeWindowType}
'@
$windowProcess = Get-Process -Id ${pid} -ErrorAction Stop
$handle = $windowProcess.MainWindowHandle
if ($handle -eq 0) { throw 'QA WPF host has no main window handle.' }
$rect = New-Object NeoBabylonVisualCapture+RECT
if (-not [NeoBabylonVisualCapture]::GetWindowRect($handle, [ref]$rect)) { throw 'GetWindowRect failed.' }
[pscustomobject]@{ x=$rect.Left; y=$rect.Top; width=($rect.Right-$rect.Left); height=($rect.Bottom-$rect.Top) } | ConvertTo-Json -Compress
`;
  return JSON.parse(powerShell(command));
}

function captureNativeWindow(pid, screenshotPath) {
  const escapedPath = screenshotPath.replaceAll("'", "''");
  const command = `
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
${nativeWindowType}
'@
$windowProcess = Get-Process -Id ${pid} -ErrorAction Stop
$handle = $windowProcess.MainWindowHandle
if ($handle -eq 0) { throw 'QA WPF host has no main window handle.' }
$rect = New-Object NeoBabylonVisualCapture+RECT
if (-not [NeoBabylonVisualCapture]::GetWindowRect($handle, [ref]$rect)) { throw 'GetWindowRect failed.' }
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
$bitmap = New-Object System.Drawing.Bitmap($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$hdc = $graphics.GetHdc()
try { $captured = [NeoBabylonVisualCapture]::PrintWindow($handle, $hdc, [uint32]2) }
finally { $graphics.ReleaseHdc($hdc) }
if (-not $captured) { throw 'PrintWindow failed to capture the native WPF window.' }
$bitmap.Save('${escapedPath}', [System.Drawing.Imaging.ImageFormat]::Png)
$sample = $bitmap.GetPixel([int]($width / 2), [Math]::Min(15, $height - 1))
$bitmap.Dispose(); $graphics.Dispose()
[pscustomobject]@{ width=$width; height=$height; captionSample=@($sample.R,$sample.G,$sample.B) } | ConvertTo-Json -Compress
`;
  return JSON.parse(powerShell(command));
}

function reservePort() {
  const server = createServer();
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
    encoding: "utf8",
    timeout: 10000,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`could not request graceful close for QA host PID ${pid}: ${result.stderr || result.stdout}`);
}

async function waitFor(predicate, message, timeoutMs = 30000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (await predicate()) return;
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  throw new Error(message);
}

const { chromium } = await import(pathToFileURL(playwrightModulePath).href);
const issues = [];
const launches = [];
const observations = [];
const persistedChrome = [];
const nativeAccessibilitySnapshots = [];
const nativeContrastSnapshots = [];
let host;
let browser;
let page;
let currentPort;
let firstRun = null;
let focused = null;
let startupSnapshot = null;

async function waitForCdp(port) {
  const deadline = Date.now() + 45000;
  while (Date.now() < deadline) {
    if (host.exitCode !== null) throw new Error(`WPF host exited before CDP opened (code ${host.exitCode})`);
    try {
      const result = await fetch(`http://127.0.0.1:${port}/json/version`, { signal: AbortSignal.timeout(1000) });
      if (result.ok) return;
    } catch { /* WebView2 is starting. */ }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`WebView2 CDP did not open on port ${port}`);
}

async function attachNativePage(port) {
  const deadline = Date.now() + 30000;
  while (Date.now() < deadline) {
    if (host.exitCode !== null) throw new Error(`WPF host exited before its page appeared (code ${host.exitCode})`);
    const found = browser.contexts().flatMap((context) => context.pages())
      .find((candidate) => candidate.url().startsWith("https://neobabylon.local/"));
    if (found) return found;
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error("the isolated native NeoBabylon WebView page did not appear");
}

async function launchHost() {
  currentPort = await reservePort();
  const hostEnvironment = { ...process.env };
  for (const name of ["OPENROUTER_API_KEY", "OPENAI_API_KEY", "LM_STUDIO_API_KEY", "NEOBABYLON_WINDOWS_SANDBOX_MODE"]) {
    delete hostEnvironment[name];
  }
  Object.assign(hostEnvironment, {
    NEOBABYLON_SOURCE_ROOT: sourceRoot,
    NEOBABYLON_APPLICATION_ROOT: applicationRoot,
    NEOBABYLON_MODEL_CAPABILITY_PATH: capabilityPath,
    NEOBABYLON_ENABLE_WEBVIEW2_REMOTE_DEBUG: "1",
    NEOBABYLON_WEBVIEW2_DEBUG_PORT: String(currentPort),
  });
  host = spawn(hostPath, [], { cwd: sourceRoot, env: hostEnvironment, stdio: "ignore", windowsHide: false });
  launches.push({ pid: host.pid, debugPort: currentPort });
  await waitForCdp(currentPort);
  browser = await chromium.connectOverCDP(`http://127.0.0.1:${currentPort}`);
  page = await attachNativePage(currentPort);
  page.on("pageerror", (error) => issues.push({ type: "pageerror", message: error.message }));
  page.on("console", (message) => {
    if (message.type() === "error") issues.push({ type: "console", message: message.text() });
  });
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  assert.equal(await page.title(), "NeoBabylon");
  assert.equal(await page.locator("vite-error-overlay").count(), 0, "no framework error overlay should be present");
  await page.getByRole("textbox", { name: "Message NeoBabylon" }).waitFor();
}

async function closeHost() {
  if (browser) await browser.close().catch(() => {});
  browser = null;
  if (host && host.exitCode === null) {
    try {
      requestHostClose(host.pid);
      const exit = await waitForExit(host, 20000);
      assert.equal(exit.code, 0, `QA WPF host exited unexpectedly: ${JSON.stringify(exit)}`);
    } catch (error) {
      host.kill();
      await waitForExit(host, 10000).catch(() => {});
      throw error;
    }
  }
  host = null;
  page = null;
}

async function appearance() {
  return page.evaluate(() => ({
    mode: document.documentElement.dataset.appearance,
    stored: localStorage.getItem("neobabylon.appearance:v1"),
  }));
}

async function assertAppearance(mode) {
  await waitFor(async () => (await appearance()).mode === mode, `appearance did not settle to ${mode}`);
  const current = await appearance();
  assert.equal(current.mode, mode);
  assert.equal(current.stored, mode, `${mode} appearance was not persisted in the WebView2 profile`);
}

async function switchAppearance(mode) {
  const label = mode === "light" ? "Switch to light mode" : "Switch to dark mode";
  await page.getByRole("button", { name: label }).click();
  await assertAppearance(mode);
  await page.waitForTimeout(150);
}

async function resizeNativeWindow(targetWidth, targetHeight) {
  const before = await page.evaluate(() => ({
    innerWidth: window.innerWidth,
    innerHeight: window.innerHeight,
    outerWidth: window.outerWidth,
    outerHeight: window.outerHeight,
    devicePixelRatio: window.devicePixelRatio,
  }));
  const scale = before.devicePixelRatio || 1;
  const nativeBefore = getNativeWindowBounds(host.pid);
  const chromeWidth = Math.max(0, nativeBefore.width - before.innerWidth * scale);
  const chromeHeight = Math.max(0, nativeBefore.height - before.innerHeight * scale);
  const width = Math.round(targetWidth * scale + chromeWidth);
  const height = Math.round(targetHeight * scale + chromeHeight);
  const nativeBounds = setNativeWindowSize(host.pid, width, height);
  try {
    await waitFor(async () => {
      const current = await page.evaluate(() => ({ width: window.innerWidth, height: window.innerHeight }));
      return Math.abs(current.width - targetWidth) <= 36 && Math.abs(current.height - targetHeight) <= 36;
    }, `native host did not reach the requested ${targetWidth}x${targetHeight} content size`, 5000);
  } catch {
    const actual = await page.evaluate(() => ({
      width: window.innerWidth,
      height: window.innerHeight,
      outerWidth: window.outerWidth,
      outerHeight: window.outerHeight,
      devicePixelRatio: window.devicePixelRatio,
    }));
    throw new Error(`native host resize mismatch ${JSON.stringify({ targetWidth, targetHeight, before, nativeBefore, computedOuterPixels: { width, height }, nativeBounds, actual })}`);
  }
  const after = await page.evaluate(() => ({ width: window.innerWidth, height: window.innerHeight, devicePixelRatio: window.devicePixelRatio }));
  assert.ok(after.width <= targetWidth + 36 && after.width >= targetWidth - 36);
  assert.ok(after.height <= targetHeight + 36 && after.height >= targetHeight - 36);
  return { requestedContent: { width: targetWidth, height: targetHeight }, actualContent: after, nativeBefore, nativeBounds };
}

async function visualObservation(label) {
  return page.evaluate((observationLabel) => {
    const cssColor = (value) => {
      const match = value.match(/rgba?\(([^)]+)\)/);
      if (!match) return null;
      const parts = match[1].split(",").map((part) => Number.parseFloat(part.trim()));
      return { r: parts[0], g: parts[1], b: parts[2], a: parts[3] ?? 1 };
    };
    const background = (element) => {
      for (let current = element; current; current = current.parentElement) {
        const value = cssColor(getComputedStyle(current).backgroundColor);
        if (value && value.a > 0) return value;
      }
      return cssColor(getComputedStyle(document.documentElement).backgroundColor);
    };
    const luminance = ({ r, g, b }) => {
      const channel = (value) => {
        const normalized = value / 255;
        return normalized <= 0.04045 ? normalized / 12.92 : ((normalized + 0.055) / 1.055) ** 2.4;
      };
      return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
    };
    const contrast = (foreground, surface) => {
      if (!foreground || !surface) return null;
      const values = [luminance(foreground), luminance(surface)].sort((a, b) => b - a);
      return Math.round(((values[0] + 0.05) / (values[1] + 0.05)) * 100) / 100;
    };
    const sample = (selector) => {
      const element = document.querySelector(selector);
      if (!element) return null;
      const rect = element.getBoundingClientRect();
      const style = getComputedStyle(element);
      const foreground = cssColor(style.color);
      const surface = background(element);
      return {
        text: element.textContent?.trim().slice(0, 100) ?? "",
        color: style.color,
        background: `rgb(${surface?.r}, ${surface?.g}, ${surface?.b})`,
        contrastRatio: contrast(foreground, surface),
        bounds: { x: Math.round(rect.x), y: Math.round(rect.y), width: Math.round(rect.width), height: Math.round(rect.height) },
      };
    };
    const rootStyle = getComputedStyle(document.documentElement);
    const shell = document.querySelector(".app-shell");
    const shellStyle = shell ? getComputedStyle(shell) : null;
    const sidebar = document.querySelector(".sidebar");
    const topbar = document.querySelector(".topbar");
    const conversation = document.querySelector(".conversation");
    const composer = document.querySelector(".composer-area");
    const warning = document.querySelector('[aria-label="Full-access notice"]');
    const permission = document.querySelector(".permission-chip");
    const boxes = [sidebar, topbar, conversation, composer, warning, permission].filter(Boolean).map((element) => {
      const rect = element.getBoundingClientRect();
      return { x: Math.round(rect.x), y: Math.round(rect.y), right: Math.round(rect.right), bottom: Math.round(rect.bottom) };
    });
    return {
      label: observationLabel,
      mode: document.documentElement.dataset.appearance,
      viewport: { width: innerWidth, height: innerHeight, scrollWidth: document.documentElement.scrollWidth, devicePixelRatio },
      colors: {
        root: rootStyle.backgroundColor,
        shell: shellStyle?.backgroundColor ?? null,
        sidebar: sidebar ? getComputedStyle(sidebar).backgroundColor : null,
        topbar: topbar ? getComputedStyle(topbar).backgroundColor : null,
        conversation: conversation ? getComputedStyle(conversation).backgroundColor : null,
      },
      contrast: {
        brand: sample(".brand-copy strong"),
        primaryAction: sample(".new-task-button"),
        welcomeHeading: sample(".welcome-wrap h1"),
        permissionChip: sample(".permission-chip"),
        warning: sample('[aria-label="Full-access notice"]'),
      },
      layout: {
        towerLoaded: Boolean(document.querySelector(".brand-symbol")?.complete)
          && (document.querySelector(".brand-symbol")?.naturalWidth ?? 0) > 0,
        sidebarVisible: Boolean(sidebar && getComputedStyle(sidebar).display !== "none"),
        composerVisible: Boolean(composer && composer.getBoundingClientRect().height > 0),
        warningVisible: Boolean(warning && getComputedStyle(warning).display !== "none"),
        permissionChipVisible: Boolean(permission && getComputedStyle(permission).display !== "none"),
        allPrimaryBoundsInsideViewport: boxes.every((rect) => rect.x >= -1 && rect.y >= -1
          && rect.right <= innerWidth + 2 && rect.bottom <= innerHeight + 2),
      },
    };
  }, label);
}

async function assertPrimaryShell() {
  const sidebar = page.locator('aside[aria-label="Workspace navigation"]');
  await sidebar.waitFor();
  await sidebar.getByRole("button", { name: "New task" }).waitFor();
  await sidebar.getByRole("button", { name: "Projects" }).waitFor();
  const search = sidebar.getByRole("searchbox", { name: "Search loaded conversations" });
  try {
    await search.waitFor({ state: "visible", timeout: 30000 });
  } catch {
    startupSnapshot = await page.evaluate(() => ({
      connection: document.querySelector(".connection-status")?.textContent?.trim() ?? null,
      startupError: document.querySelector(".startup-error")?.textContent?.trim() ?? null,
      historyError: document.querySelector(".chat-list-error")?.textContent?.trim() ?? null,
      historyErrorDetail: document.querySelector(".chat-list-error")?.getAttribute("title") ?? null,
      historyWarning: document.querySelector(".history-warning")?.textContent?.trim() ?? null,
      historySearchElements: document.querySelectorAll(".history-search input").length,
      sidebarText: document.querySelector("aside[aria-label='Workspace navigation']")?.innerText ?? null,
      appServerVersion: document.querySelector(".footer-version")?.textContent?.trim() ?? null,
      statusAndAlerts: [...document.querySelectorAll('[role="status"], [role="alert"]')]
        .map((element) => element.textContent?.trim()).filter(Boolean),
    }));
    const diagnosticScreenshot = path.join(runRoot, "startup-history-state.png");
    await page.screenshot({ path: diagnosticScreenshot });
    const diagnosticsButton = page.getByRole("button", { name: "Open diagnostics" });
    if (await diagnosticsButton.isVisible()) {
      await diagnosticsButton.click();
      const diagnosticPanel = page.getByRole("dialog", { name: "Runtime diagnostics" });
      await diagnosticPanel.waitFor({ timeout: 10000 });
      startupSnapshot.runtimeDiagnostics = await diagnosticPanel.innerText();
      await diagnosticPanel.getByRole("button", { name: "Done" }).click();
    }
    startupSnapshot.screenshot = diagnosticScreenshot;
    console.error(`P2_12_STARTUP_SNAPSHOT ${JSON.stringify(startupSnapshot)}`);
    throw new Error(`the history search control was absent or hidden: ${JSON.stringify(startupSnapshot)}`);
  }
  await page.getByRole("button", { name: "Open diagnostics" }).waitFor();
  await page.getByRole("textbox", { name: "Message NeoBabylon" }).waitFor();
  await page.locator(".brand-symbol").evaluate((element) => {
    if (!(element instanceof HTMLImageElement) || !element.complete || element.naturalWidth === 0) {
      throw new Error("the tower brand asset did not load");
    }
  });
  await page.locator(".permission-chip").waitFor();
  await inspectAccessibilityTree("native primary shell");
}

async function inspectAccessibilityTree(label, {
  required = [
    { role: "searchbox", name: "Search loaded conversations" },
    { role: "button", name: "New task" },
    { role: "button", name: "Open diagnostics" },
    { role: "textbox", name: "Message NeoBabylon" },
  ],
  absent = [],
} = {}) {
  const session = await page.context().newCDPSession(page);
  try {
    await session.send("Accessibility.enable");
    const { nodes } = await session.send("Accessibility.getFullAXTree");
    const exposedNodes = nodes
      .filter((node) => node.ignored !== true)
      .map((node) => ({
        role: node.role?.value ?? "",
        name: node.name?.value ?? "",
      }));
    for (const entry of required) {
      assert.ok(exposedNodes.some((node) => node.role === entry.role && node.name === entry.name),
        `${label}: native WebView2 accessibility tree omitted ${entry.role} named ${JSON.stringify(entry.name)}`);
    }
    for (const entry of absent) {
      assert.ok(!exposedNodes.some((node) => node.role === entry.role && node.name === entry.name),
        `${label}: native WebView2 accessibility tree exposed background ${entry.role} named ${JSON.stringify(entry.name)}`);
    }

    const snapshot = {
      label,
      exposedNodeCount: exposedNodes.length,
      required,
      absent,
      matchingNodes: exposedNodes.filter((node) => required.some((entry) =>
        entry.role === node.role && entry.name === node.name)),
    };
    nativeAccessibilitySnapshots.push(snapshot);
    return snapshot;
  } finally {
    await session.detach();
  }
}

async function inspectDiagnosticsSearchPlaceholderContrast(diagnostics) {
  const search = diagnostics.getByRole("searchbox", { name: "Search tools and capabilities" });
  const observation = await search.evaluate((input) => {
    const color = (value) => {
      const channels = value.match(/[\d.]+/g)?.map(Number) ?? [];
      return channels.length >= 3 ? channels.slice(0, 3) : null;
    };
    const luminance = (channels) => {
      const [r, g, b] = channels.map((value) => {
        const normalized = value / 255;
        return normalized <= 0.04045 ? normalized / 12.92 : ((normalized + 0.055) / 1.055) ** 2.4;
      });
      return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    };
    const foreground = color(getComputedStyle(input, "::placeholder").color);
    const background = color(getComputedStyle(input).backgroundColor);
    if (!foreground || !background) throw new Error("native diagnostics search placeholder colors were not measurable");
    const values = [luminance(foreground), luminance(background)].sort((a, b) => b - a);
    return {
      appearance: document.documentElement.dataset.appearance,
      foreground: getComputedStyle(input, "::placeholder").color,
      background: getComputedStyle(input).backgroundColor,
      contrastRatio: Math.round(((values[0] + 0.05) / (values[1] + 0.05)) * 100) / 100,
    };
  });
  assert.ok(observation.contrastRatio >= 4.5,
    `native ${observation.appearance} diagnostics search placeholder contrast ${observation.contrastRatio}:1 is below 4.5:1`);
  nativeContrastSnapshots.push({ label: "native diagnostics search placeholder", ...observation });
}

async function captureState(name) {
  await page.screenshot({ path: screenshots[`${name}WebView`] });
  const native = captureNativeWindow(host.pid, screenshots[`${name}Native`]);
  const observation = await visualObservation(name);
  observation.nativeWindow = native;
  observations.push(observation);
  assert.ok(observation.layout.sidebarVisible, `${name}: workspace navigation should remain visible`);
  assert.ok(observation.layout.composerVisible, `${name}: task composer should remain visible`);
  assert.ok(observation.layout.permissionChipVisible, `${name}: authority chip should remain visible`);
  assert.ok(observation.layout.allPrimaryBoundsInsideViewport, `${name}: primary UI bounds should remain inside the viewport`);
  assert.ok(observation.viewport.scrollWidth <= observation.viewport.width + 2, `${name}: the document should not overflow horizontally`);
  assert.ok(observation.contrast.brand?.contrastRatio >= 4.5, `${name}: brand text contrast should be at least 4.5:1`);
  assert.ok(observation.contrast.permissionChip?.contrastRatio >= 4.5, `${name}: authority chip contrast should be at least 4.5:1`);
  const [red, green, blue] = native.captionSample;
  if (name.startsWith("dark")) {
    assert.ok(Math.max(red, green, blue) - Math.min(red, green, blue) <= 4,
      `${name}: native dark caption should be neutral gray`);
    assert.ok(red >= 12 && red <= 45, `${name}: native dark caption should be charcoal, not white or blue`);
    for (const background of [observation.colors.root, observation.colors.shell,
      observation.colors.sidebar, observation.colors.topbar, observation.colors.conversation]) {
      const values = background?.match(/\d+/g)?.slice(0, 3).map(Number) ?? [];
      assert.equal(values.length, 3, `${name}: expected computed background color ${background}`);
      assert.ok(Math.max(...values) - Math.min(...values) <= 2,
        `${name}: dark shell surfaces should use a neutral black/gray palette (${background})`);
    }
  } else {
    assert.ok(Math.max(red, green, blue) - Math.min(red, green, blue) <= 4,
      `${name}: native light caption should be neutral gray`);
    assert.ok(red >= 225, `${name}: native light caption should be light, not dark`);
  }
  return observation;
}

function assertNativeCaption(capture, mode) {
  const [red, green, blue] = capture.captionSample;
  assert.ok(Math.max(red, green, blue) - Math.min(red, green, blue) <= 4,
    `${mode}: native caption should be neutral gray`);
  if (mode === "dark") assert.ok(red >= 12 && red <= 45, "dark native caption should be charcoal");
  else assert.ok(red >= 225, "light native caption should be light");
  return capture;
}

let passed = false;
let failure = null;
try {
  await launchHost();
  await assertPrimaryShell();
  firstRun = await page.evaluate(() => ({
    appearance: document.documentElement.dataset.appearance,
    appearancePreference: localStorage.getItem("neobabylon.appearance:v1"),
    fullAccessNoticePreference: localStorage.getItem("neobabylon.full-access-notice-hidden:v1"),
  }));
  assert.equal(firstRun.appearance, "dark", "a fresh isolated profile should default to dark appearance");
  assert.equal(firstRun.appearancePreference, null, "the first dark render should not depend on a pre-seeded preference");
  assert.equal(firstRun.fullAccessNoticePreference, null, "the fresh profile should not have a pre-seeded notice decision");
  const notice = page.getByRole("note", { name: "Full-access notice" });
  await notice.waitFor();
  assert.match(await notice.innerText(), /Full access · no containment/i);
  assert.match(await page.locator(".permission-chip").innerText(), /Full access · no containment/i);

  await resizeNativeWindow(1440, 900);
  await captureState("dark-desktop");
  await resizeNativeWindow(960, 720);
  await captureState("dark-compact");

  await switchAppearance("light");
  await captureState("light-compact");
  await resizeNativeWindow(1440, 900);
  await captureState("light-desktop");

  await page.getByRole("button", { name: "Dismiss full-access notice" }).click();
  assert.equal(await page.getByRole("note", { name: "Full-access notice" }).count(), 0,
    "session dismissal should immediately remove the notice");
  await page.reload();
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  await assertAppearance("light");
  await page.getByRole("note", { name: "Full-access notice" }).waitFor();
  await page.getByRole("button", { name: "Don’t show again" }).click();
  assert.equal(await page.getByRole("note", { name: "Full-access notice" }).count(), 0,
    "persistent dismissal should immediately remove the notice");
  assert.equal(await page.evaluate(() => localStorage.getItem("neobabylon.full-access-notice-hidden:v1")), "1");
  assert.match(await page.locator(".permission-chip").innerText(), /Full access · no containment/i,
    "hiding the notice must not hide the authority chip");
  await page.locator(".permission-chip").click();
  const diagnostics = page.getByRole("dialog", { name: "Runtime diagnostics" });
  await diagnostics.waitFor();
  await inspectAccessibilityTree("native runtime diagnostics dialog", {
    required: [
      { role: "dialog", name: "Runtime diagnostics" },
      { role: "button", name: "Done" },
    ],
    absent: [{ role: "searchbox", name: "Search loaded conversations" }],
  });
  await inspectDiagnosticsSearchPlaceholderContrast(diagnostics);
  assert.match(await diagnostics.innerText(), /Selected tool policy[\s\S]*Full access · no containment/i,
    "runtime details should retain the selected authority after dismissing the notice");
  await diagnostics.getByRole("button", { name: "Done" }).click();
  await page.reload();
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  assert.equal(await page.getByRole("note", { name: "Full-access notice" }).count(), 0,
    "the do-not-show-again decision should survive a renderer reload");
  await assertAppearance("light");
  assert.match(await page.locator(".permission-chip").innerText(), /Full access · no containment/i);
  await closeHost();

  await launchHost();
  await assertAppearance("light");
  assert.equal(await page.getByRole("note", { name: "Full-access notice" }).count(), 0,
    "the notice preference should survive a WPF host restart");
  assert.match(await page.locator(".permission-chip").innerText(), /Full access · no containment/i);
  const lightAfterRestart = assertNativeCaption(captureNativeWindow(host.pid, screenshots["light-after-restartNative"]), "light");
  persistedChrome.push({ state: "light-after-restart", ...lightAfterRestart });
  await switchAppearance("dark");
  const darkAfterToggle = assertNativeCaption(captureNativeWindow(host.pid, screenshots["dark-after-toggleNative"]), "dark");
  persistedChrome.push({ state: "dark-after-toggle", ...darkAfterToggle });
  await page.reload();
  await page.getByText("Desktop host ready").waitFor({ timeout: 30000 });
  await assertAppearance("dark");
  await resizeNativeWindow(960, 720);
  const darkAfterRestart = assertNativeCaption(captureNativeWindow(host.pid, screenshots["dark-after-restartNative"]), "dark");
  persistedChrome.push({ state: "dark-after-reload", ...darkAfterRestart });
  await closeHost();

  await launchHost();
  await assertAppearance("dark");
  assert.equal(await page.getByRole("note", { name: "Full-access notice" }).count(), 0);
  assert.match(await page.locator(".permission-chip").innerText(), /Full access · no containment/i);
  const darkAfterHostRestartPath = path.join(runRoot, "dark-after-host-restart-window.png");
  screenshots["dark-after-host-restartNative"] = darkAfterHostRestartPath;
  const darkAfterHostRestart = assertNativeCaption(captureNativeWindow(host.pid, darkAfterHostRestartPath), "dark");
  persistedChrome.push({ state: "dark-after-host-restart", ...darkAfterHostRestart });

  await page.getByRole("button", { name: "New task" }).focus();
  await page.keyboard.press("Tab");
  focused = await page.evaluate(() => {
    const element = document.activeElement;
    return {
      tagName: element?.tagName ?? null,
      accessibleName: element?.getAttribute("aria-label") ?? element?.textContent?.trim() ?? "",
      focusVisible: element instanceof HTMLElement && element.matches(":focus-visible"),
      outlineStyle: element instanceof HTMLElement ? getComputedStyle(element).outlineStyle : "none",
    };
  });
  assert.ok(focused.tagName && focused.tagName !== "BODY", "keyboard Tab should move focus to a visible control");
  assert.notEqual(focused.outlineStyle, "none", "keyboard focus should have a visible outline");
  await waitFor(async () => issues.length === 0, `native page/console errors: ${JSON.stringify(issues)}`, 1000);
  assert.deepEqual(issues, [], "the native visual and appearance flow should not emit page or console errors");
  passed = true;
} catch (error) {
  failure = error instanceof Error ? `${error.name}: ${error.message}` : String(error);
} finally {
  try { await closeHost(); }
  catch (error) { failure ??= `cleanup: ${error instanceof Error ? error.message : String(error)}`; passed = false; }
  const result = {
    slice: "P2-12",
    passed,
    failure,
    evidenceKind: "Native WPF/WebView2; local UI only; no provider turn or live inference",
    runtime: {
      version: runtimeLock.runtime.version,
      revision: runtimeLock.runtime.sourceRevision,
      sha256: runtimeHash,
      binaryPath: runtimePath,
    },
    sourceRoot,
    applicationRoot,
    dataRoot: path.join(applicationRoot, "Data"),
    capabilityRecord: capabilityPath,
    ordinaryCodexRootUsed: false,
    providerInference: false,
    launches,
    firstRun,
    observations,
    p2_10NativeAccessibilityTree: nativeAccessibilitySnapshots,
    p2_10NativeContrast: nativeContrastSnapshots,
    persistedChrome,
    startupSnapshot,
    keyboardFocus: typeof focused === "undefined" ? null : focused,
    screenshots,
    issues,
    runRoot,
  };
  writeFileSync(resultPath, `${JSON.stringify(result, null, 2)}\n`, { encoding: "utf8", flag: "wx" });
  console.log(JSON.stringify({ ...result, screenshots, resultPath }));
}
if (!passed) process.exitCode = 1;
