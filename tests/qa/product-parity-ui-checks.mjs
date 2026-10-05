import assert from "node:assert/strict";

// Called only by the synthetic browser fixture, never against a user's desktop.
export async function verifyProductParityUi(page, runRoot) {
  const { default: path } = await import("node:path");
  await page.evaluate(() => {
    window.__parityCopies = [];
    Object.defineProperty(navigator, "clipboard", { configurable: true, value: {
      writeText: async (text) => { window.__parityCopies.push(text); },
    } });
  });
  const message = page.locator(".message").first();
  const visible = await message.locator(".message-text").textContent();
  await message.getByRole("button", { name: "Copy message", exact: true }).click();
  await message.getByRole("status").filter({ hasText: "Copied" }).waitFor();
  assert.deepEqual(await page.evaluate(() => window.__parityCopies), [visible]);
  await page.evaluate(() => {
    Object.defineProperty(navigator, "clipboard", { configurable: true, value: {
      writeText: async () => { throw new Error("synthetic clipboard denial"); },
    } });
  });
  await message.getByRole("button", { name: "Copy message", exact: true }).click();
  await message.getByRole("alert").filter({ hasText: "Could not copy" }).waitFor();
  await page.setViewportSize({ width: 2560, height: 1440 });
  const transcript = page.locator(".transcript");
  const readable = await transcript.evaluate((element) => element.getBoundingClientRect().width);
  await page.getByRole("button", { name: "Wide chat", exact: true }).click();
  const wide = await transcript.evaluate((element) => element.getBoundingClientRect().width);
  assert.ok(wide > readable + 200, "Wide mode should use appreciably more space on a large display");
  assert.equal(await page.evaluate(() => localStorage.getItem("neobabylon.conversation-layout:v1")), "wide");
  for (const width of [2560, 1440, 960]) {
    await page.setViewportSize({ width, height: 900 });
    const metrics = await page.evaluate(() => ({
      overflow: document.documentElement.scrollWidth - innerWidth,
      transcript: document.querySelector(".transcript").getBoundingClientRect().width,
      composer: document.querySelector(".composer-area").getBoundingClientRect().width,
    }));
    assert.ok(metrics.overflow <= 1, `Page overflow at ${width}px: ${JSON.stringify(metrics)}`);
    assert.ok(Math.abs(metrics.transcript - metrics.composer) <= 1, "Wide composer and transcript should align");
    await page.screenshot({ path: path.join(runRoot, `parity-wide-${width}.png`), fullPage: true });
  }
  await page.reload({ waitUntil: "networkidle" });
  assert.equal(await page.locator(".app-shell").getAttribute("data-conversation-layout"), "wide", "Layout should survive reload");
  await page.getByRole("button", { name: "Readable chat", exact: true }).click();
  assert.equal(await page.locator(".app-shell").getAttribute("data-conversation-layout"), "readable");
}
