import assert from "node:assert/strict";
import { pathToFileURL } from "node:url";

const [portText, playwrightModulePath, screenshotPath] = process.argv.slice(2);
const port = Number(portText);
assert.ok(Number.isInteger(port) && port >= 1024 && port <= 65535, "a valid QA WebView2 debug port is required");
assert.ok(playwrightModulePath && screenshotPath, "Playwright module and screenshot paths are required");

const { chromium } = await import(pathToFileURL(playwrightModulePath).href);
const browser = await chromium.connectOverCDP(`http://127.0.0.1:${port}`);
let page;
const pageDeadline = Date.now() + 30000;
while (Date.now() < pageDeadline) {
  page = browser.contexts().flatMap((context) => context.pages())
    .find((candidate) => candidate.url().startsWith("https://neobabylon.local/"));
  if (page) break;
  await new Promise((resolve) => setTimeout(resolve, 250));
}
assert.ok(page, "the isolated native NeoBabylon page did not appear within 30 seconds");

const issues = [];
page.on("pageerror", (error) => issues.push(error.message));
page.on("console", (message) => {
  if (message.type() === "error") issues.push(message.text());
});

try {
  await page.reload();
  await page.getByText("Desktop host ready").waitFor({ timeout: 20000 });
  assert.equal(await page.title(), "NeoBabylon");

  const mark = page.locator(".brand-symbol");
  await mark.waitFor({ timeout: 10000 });
  const renderedMark = await mark.evaluate((element) => {
    const bounds = element.getBoundingClientRect();
    return {
      tagName: element.tagName,
      source: element instanceof HTMLImageElement ? new URL(element.src).pathname : null,
      loaded: element instanceof HTMLImageElement && element.complete && element.naturalWidth > 0,
      decorative: element.getAttribute("aria-hidden") === "true" && element.getAttribute("alt") === "",
      width: bounds.width,
      height: bounds.height,
    };
  });

  assert.equal(renderedMark.tagName, "IMG", "the sidebar should use the tower asset instead of the old circles");
  assert.equal(renderedMark.source, "/favicon.svg", "the sidebar should reuse the NeoBabylon tower favicon");
  assert.equal(renderedMark.loaded, true, "the tower image should load in the native WebView");
  assert.equal(renderedMark.decorative, true, "the adjacent brand text should remain the accessible name");
  assert.equal(renderedMark.width, 29, "the brand mark should retain its established sidebar footprint");
  assert.equal(renderedMark.height, 29, "the brand mark should remain square");

  assert.equal(await page.locator("vite-error-overlay").count(), 0, "the page should not show a framework error overlay");
  assert.deepEqual(issues, [], "the brand mark should render without page or console errors");
  const viewport = await page.evaluate(() => ({ width: window.innerWidth, height: window.innerHeight }));
  await page.screenshot({ path: screenshotPath });

  await page.getByRole("button", { name: "Open diagnostics" }).click();
  const diagnostics = page.getByRole("dialog", { name: "Runtime diagnostics" });
  await diagnostics.waitFor();
  await page.waitForTimeout(250);
  const drawer = await page.locator(".diagnostics-drawer").evaluate((element) => {
    const bounds = element.getBoundingClientRect();
    const style = getComputedStyle(element);
    return { x: bounds.x, width: bounds.width, opacity: style.opacity, background: style.backgroundColor };
  });
  assert.equal(drawer.x + drawer.width, viewport.width, "the settled diagnostics drawer should align to the right edge");
  assert.equal(drawer.width, 510, "the desktop diagnostics drawer should retain its intended width");
  assert.equal(drawer.opacity, "1", "the settled diagnostics drawer should be opaque");
  await diagnostics.getByRole("button", { name: "Close diagnostics" }).click();
  assert.equal(await page.getByRole("dialog", { name: "Runtime diagnostics" }).count(), 0,
    "closing diagnostics should return to the main workspace");

  assert.deepEqual(issues, [], "the diagnostics interaction should not produce page or console errors");
  console.log(JSON.stringify({
    passed: true,
    source: "native WPF/WebView2",
    title: await page.title(),
    viewport,
    renderedMark,
    diagnosticsInteraction: { opened: true, closed: true, drawer },
    issues,
  }));
} finally {
  await browser.close();
}
