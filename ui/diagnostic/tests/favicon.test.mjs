import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { createServer } from "vite";

test("the rendered document references a locally served SVG favicon", async () => {
  const root = fileURLToPath(new URL("../", import.meta.url));
  const server = await createServer({
    root,
    configFile: fileURLToPath(new URL("../vite.config.ts", import.meta.url)),
    logLevel: "silent",
    server: { host: "127.0.0.1", port: 0, strictPort: false },
  });

  try {
    await server.listen();
    const address = server.httpServer.address();
    assert.ok(address && typeof address !== "string", "Vite did not expose its local test server");
    const origin = `http://127.0.0.1:${address.port}`;
    const pageResponse = await fetch(origin);
    assert.equal(pageResponse.status, 200, "the diagnostic entry page should be served");
    const pageHtml = await pageResponse.text();
    const iconLink = pageHtml.match(/<link\b[^>]*\brel=["']icon["'][^>]*>/i)?.[0];
    assert.ok(iconLink, "the served document should declare its favicon explicitly");

    const href = iconLink.match(/\bhref=["']([^"']+)["']/i)?.[1];
    assert.ok(href, "the favicon link should include a URL");
    const iconUrl = new URL(href, origin);
    assert.equal(iconUrl.origin, origin, "the favicon must be served locally");

    const iconResponse = await fetch(iconUrl);
    assert.equal(iconResponse.status, 200, "the local favicon should not return file-not-found");
    assert.match(iconResponse.headers.get("content-type") ?? "", /image\/svg\+xml/i);
    assert.match(await iconResponse.text(), /<svg\b/i);
  } finally {
    await server.close();
  }
});

test("the Babylon tower mark is wired into the browser and Windows program icons", async () => {
  const repositoryRoot = fileURLToPath(new URL("../../../", import.meta.url));
  const iconPath = `${repositoryRoot}/ui/diagnostic/public/favicon.svg`;
  const icon = await readFile(iconPath, "utf8");
  assert.match(icon, /<title[^>]*>NeoBabylon tower<\/title>/i);
  assert.match(icon, /<path[^>]*id=["']tower["']/i);

  const project = await readFile(
    `${repositoryRoot}/host/NeoBabylon.Host/NeoBabylon.Host.csproj`,
    "utf8",
  );
  assert.match(project, /<ApplicationIcon>Assets\\NeoBabylon\.ico<\/ApplicationIcon>/i);

  const window = await readFile(
    `${repositoryRoot}/host/NeoBabylon.Host/MainWindow.xaml`,
    "utf8",
  );
  assert.match(window, /Icon=["']pack:\/\/application:,,,\/Assets\/NeoBabylon\.ico["']/i);

  const executableIcon = await readFile(
    `${repositoryRoot}/host/NeoBabylon.Host/Assets/NeoBabylon.ico`,
  );
  assert.equal(executableIcon.readUInt16LE(0), 0, "ICO reserved field must be zero");
  assert.equal(executableIcon.readUInt16LE(2), 1, "ICO image type must be one");
  const imageCount = executableIcon.readUInt16LE(4);
  assert.ok(imageCount >= 4, "the Windows icon should contain multiple resolutions");
  const sizes = new Set(
    Array.from({ length: imageCount }, (_, index) => {
      const width = executableIcon.readUInt8(6 + index * 16);
      return width === 0 ? 256 : width;
    }),
  );
  for (const size of [16, 32, 48, 256]) {
    assert.ok(sizes.has(size), `the Windows icon should include a ${size}px image`);
  }
});
