import assert from "node:assert/strict";
import { test } from "node:test";
import { matchesLateCommandCompletion, mayStopCommand, requestCommandStop } from "../src/command-stop.mjs";

test("only a currently running command execution exposes the separate stop action", () => {
  assert.equal(mayStopCommand({ itemType: "commandExecution", commandStopState: "running", commandStopAvailable: true }), true);
  assert.equal(mayStopCommand({ itemType: "commandExecution", commandStopState: "unknown", commandStopAvailable: true }), true);
  assert.equal(mayStopCommand({ itemType: "commandExecution", commandStopState: "unknown", commandStopAvailable: false }), false);
  assert.equal(mayStopCommand({ itemType: "commandExecution", commandStopState: "stopping", commandStopAvailable: true }), false);
  assert.equal(mayStopCommand({ itemType: "fileChange", commandStopState: "running", commandStopAvailable: true }), false);
  assert.equal(mayStopCommand({ itemType: "commandExecution", commandStopState: "finished", commandStopAvailable: true }), false);
  assert.equal(mayStopCommand({ itemType: "commandExecution", commandStopState: "notReportedActive", commandStopAvailable: true }), false);
});

test("late completion is accepted only for its exact host request, thread, turn, and command", () => {
  const binding = { requestId: "turn-request-1", threadId: "thread-1", turnId: "turn-1", itemId: "command-1" };
  const event = {
    requestId: "turn-request-1",
    stream: true,
    attributedTo: "Codex App Server",
    method: "item/completed",
    params: {
      threadId: "thread-1",
      turnId: "turn-1",
      item: { id: "command-1", type: "commandExecution", status: "completed" },
    },
  };

  assert.equal(matchesLateCommandCompletion(event, binding), true);
  for (const [label, changed] of [
    ["request", { ...event, requestId: "different-request" }],
    ["source", { ...event, attributedTo: "NeoBabylon.Host" }],
    ["thread", { ...event, params: { ...event.params, threadId: "different-thread" } }],
    ["turn", { ...event, params: { ...event.params, turnId: "different-turn" } }],
    ["item id", { ...event, params: { ...event.params, item: { ...event.params.item, id: "different-item" } } }],
    ["item type", { ...event, params: { ...event.params, item: { ...event.params.item, type: "fileChange" } } }],
    ["method", { ...event, method: "item/started" }],
  ]) {
    assert.equal(matchesLateCommandCompletion(changed, binding), false, `accepted mismatched ${label}`);
  }
});

test("stop outcomes preserve exact runtime identity and avoid claiming an unconfirmed stop", async () => {
  const cases = [
    ["stopped", "stopped", false],
    ["already_exited", "finished", false],
    ["not_found", "unknown", false],
    ["identity_mismatch", "identityChanged", false],
    ["failed", "unknown", true],
    ["future_status", "unknown", true],
  ];

  for (const [runtimeStatus, expectedState, expectedActionAvailable] of cases) {
    const outcome = await requestCommandStop("call-exact", async (operation, payload) => {
      assert.equal(operation, "stopCommand");
      assert.deepEqual(payload, { itemId: "call-exact" });
      return {
        attributedTo: "Codex App Server",
        eventType: "commandExecutionStopResult",
        status: runtimeStatus,
        ...(runtimeStatus === "failed" ? { commandStopAvailable: true } : {}),
      };
    });
    assert.equal(outcome.state, expectedState, `status ${runtimeStatus}`);
    assert.equal(outcome.actionAvailable, expectedActionAvailable, `retry availability ${runtimeStatus}`);
    if (runtimeStatus === "failed" || runtimeStatus === "future_status") {
      assert.equal(outcome.attributedTo, runtimeStatus === "failed" ? "Codex App Server" : "NeoBabylon.Host");
    } else {
      assert.equal(outcome.attributedTo, "Codex App Server");
    }
  }
});

test("a typed failed stop is retryable only when the host confirms the same App Server binding", async () => {
  for (const [commandStopAvailable, expectedActionAvailable] of [[true, true], [false, false], [undefined, false]]) {
    const outcome = await requestCommandStop("call-exact", async () => ({
      attributedTo: "Codex App Server",
      eventType: "commandExecutionStopResult",
      status: "failed",
      ...(commandStopAvailable === undefined ? {} : { commandStopAvailable }),
    }));
    assert.equal(outcome.state, "unknown");
    assert.equal(outcome.actionAvailable, expectedActionAvailable);
    assert.equal(outcome.detail.includes("You can retry this exact command stop."), expectedActionAvailable);
  }
});

test("a transport failure leaves command state unknown and retryable", async () => {
  const outcome = await requestCommandStop("call-exact", async () => {
    throw new Error("App Server pipe closed");
  });

  assert.equal(outcome.state, "unknown");
  assert.equal(outcome.attributedTo, "NeoBabylon.Host");
  assert.match(outcome.detail, /may still be running/i);
  assert.equal(outcome.actionAvailable, true);
  assert.equal(mayStopCommand({
    itemType: "commandExecution",
    commandStopState: outcome.state,
    commandStopAvailable: outcome.actionAvailable,
  }), true);
});

test("host-reported identity loss disables stopping and a transport uncertainty remains retryable", async () => {
  const identityChanged = await requestCommandStop("call-exact", async () => ({
    attributedTo: "NeoBabylon.Host",
    eventType: "commandExecutionStopResult",
    status: "identity_mismatch",
    failure: { message: "The selected thread changed." },
  }));
  assert.equal(identityChanged.state, "identityChanged");
  assert.equal(identityChanged.actionAvailable, false);
  assert.equal(identityChanged.attributedTo, "NeoBabylon.Host");

  const transportUnknown = await requestCommandStop("call-exact", async () => ({
    attributedTo: "NeoBabylon.Host",
    eventType: "commandExecutionStopResult",
    status: "unknown",
    commandStopAvailable: true,
    failure: { message: "App Server did not confirm the stop." },
  }));
  assert.equal(transportUnknown.state, "unknown");
  assert.equal(transportUnknown.actionAvailable, true);
  assert.equal(transportUnknown.attributedTo, "NeoBabylon.Host");
  assert.match(transportUnknown.detail, /did not confirm/);
});
