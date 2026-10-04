import assert from "node:assert/strict";
import { test } from "node:test";
import * as restoration from "../src/restoration.mjs";

test("live tool outcomes require an explicit completed status", () => {
  const classify = restoration.classifyActivityOutcome;
  assert.equal(typeof classify, "function", "the shared activity classifier is available");
  assert.equal(classify("completed", null), "succeeded");
  assert.equal(classify("failed", null), "failed");
  assert.equal(classify("completed", "provider error"), "failed");
  assert.equal(classify(undefined, null), "info");
  assert.equal(classify("future-status", null), "info");
});

test("requestHost preserves and visibly formats host failure attribution", async () => {
  let onMessage;
  const posted = [];
  globalThis.window = {
    clearTimeout,
    setTimeout,
    chrome: {
      webview: {
        addEventListener(_type, listener) { onMessage = listener; },
        postMessage(message) { posted.push(message); },
      },
    },
  };

  const { requestHost } = await import(new URL("../src/bridge.ts", import.meta.url));

  const request = requestHost("getDiagnostics", {}, "typed-host-failure");
  assert.deepEqual(posted, [{ operation: "getDiagnostics", requestId: "typed-host-failure" }]);
  onMessage({ data: {
    requestId: "typed-host-failure",
    ok: false,
    error: {
      attributedTo: "NeoBabylon.Host",
      type: "IOException",
      message: "The local runtime exited unexpectedly.",
    },
  } });

  await assert.rejects(request, (error) => {
    assert.equal(error.name, "HostOperationError");
    assert.equal(error.attributedTo, "NeoBabylon.Host");
    assert.equal(error.type, "IOException");
    assert.match(error.message, /NeoBabylon\.Host/);
    assert.match(error.message, /IOException/);
    assert.match(error.message, /local runtime exited unexpectedly/);
    return true;
  });
});
