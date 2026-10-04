import assert from "node:assert/strict";
import { pathToFileURL } from "node:url";

const [portText, playwrightModulePath, screenshotPath] = process.argv.slice(2);
const port = Number(portText);
assert.ok(Number.isInteger(port) && port >= 1024 && port <= 65535, "a valid QA WebView2 debug port is required");
assert.ok(playwrightModulePath && screenshotPath, "Playwright module and screenshot paths are required");
const { chromium } = await import(pathToFileURL(playwrightModulePath).href);
const browser = await chromium.connectOverCDP(`http://127.0.0.1:${port}`);
const page = browser.contexts().flatMap((context) => context.pages())
  .find((candidate) => candidate.url().startsWith("https://neobabylon.local/"));
assert.ok(page, "the isolated native NeoBabylon page was not found");

const issues = [];
page.on("pageerror", (error) => issues.push(error.message));
page.on("console", (message) => {
  if (message.type() === "error") issues.push(message.text());
});
await page.addInitScript(() => {
  window.__qaMessages = [];
  window.chrome.webview.addEventListener("message", (event) => {
    const message = event.data;
    window.__qaMessages.push({ requestId: message.requestId, ok: message.ok,
      stream: message.stream, error: message.error?.message,
      resultKeys: message.result ? Object.keys(message.result) : [] });
  });
});
await page.reload();

async function openSavedPatchReview() {
  await page.getByText("Desktop host ready").waitFor({ timeout: 20000 });
  assert.equal(await page.title(), "NeoBabylon");
  assert.equal(await page.locator("vite-error-overlay").count(), 0);
  assert.match(await page.locator("body").innerText(), /Saved conversations|CHATS/i);
  const task = page.getByRole("button", { name: /Apply the isolated fixture patch/ });
  await task.waitFor({ timeout: 30000 });
  if (await task.getAttribute("aria-current") !== "page") {
    try {
      await page.waitForFunction(() => {
        const candidate = [...document.querySelectorAll("button.saved-chat")]
          .find((button) => button.textContent?.includes("Apply the isolated fixture patch."));
        return candidate && (!candidate.disabled || candidate.getAttribute("aria-current") === "page");
      }, null, { timeout: 15000 });
    } catch {
      const state = await page.evaluate(() => ({
        alerts: [...document.querySelectorAll('[role="alert"]')].map((element) => element.textContent),
        current: [...document.querySelectorAll('button.saved-chat')].map((element) => ({
          current: element.getAttribute("aria-current"), disabled: element.disabled, text: element.textContent,
          loadingDot: Boolean(element.querySelector(".thread-loading-dot")),
        })),
        composerDisabled: document.querySelector("textarea")?.disabled,
        bridgeMessages: window.__qaMessages,
        text: document.body.innerText.slice(0, 1800),
      }));
      throw new Error(`saved task is disabled: ${JSON.stringify(state)}`);
    }
    if (await task.getAttribute("aria-current") !== "page") await task.click();
  }
  try {
    await page.getByRole("region", { name: "Conversation" })
      .getByText("Apply the isolated fixture patch.", { exact: true }).waitFor({ timeout: 12000 });
  } catch (error) {
    const messages = await page.evaluate(() => window.__qaMessages);
    throw new Error(`native saved task did not open; bridge messages=${JSON.stringify(messages)}; issues=${JSON.stringify(issues)}`, { cause: error });
  }
  const reviewButton = page.getByRole("button", { name: "Review changes" });
  await reviewButton.waitFor({ timeout: 30000 });
  await reviewButton.click();
  const dialog = page.getByRole("dialog", { name: "Review changes" });
  await dialog.waitFor();
  await page.waitForTimeout(300);
  const text = await dialog.innerText();
  assert.match(text, /patch item diffs restored from App Server-owned task history/i);
  assert.match(text, /tracked\.txt/);
  assert.match(text, /-before/);
  assert.match(text, /\+after/);
  assert.match(text, /not an approval/i);
  assert.equal(await dialog.getByRole("button", { name: /approve|allow|apply/i }).count(), 0);
  return { text, taskCount: await page.locator("button.saved-chat").count() };
}

try {
  const first = await openSavedPatchReview();
  await page.screenshot({ path: screenshotPath });
  await page.reload();
  const afterReload = await openSavedPatchReview();
  assert.deepEqual(issues, []);
  console.log(JSON.stringify({ passed: true, source: "native WPF/WebView2 + pinned App Server saved task", reloadPassed: true,
    taskCount: afterReload.taskCount, reviewCharacters: afterReload.text.length,
    firstReviewCharacters: first.text.length, issues }));
} finally {
  await browser.close();
}
