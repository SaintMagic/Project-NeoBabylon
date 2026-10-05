import assert from "node:assert/strict";
import test from "node:test";
import * as restoration from "../src/restoration.mjs";

const { restoreSavedActivities, restoreVisibleTranscript } = restoration;

test("restores bounded transcript in chronological order without exposing tool payloads", () => {
  const restored = restoreVisibleTranscript([
    {
      id: "turn-newest",
      items: [
        { id: "user-new", type: "userMessage", text: "Latest question", hasNonTextContent: true },
        { id: "assistant-new", type: "agentMessage", text: "Latest response" },
        { id: "tool-new", type: "commandExecution", command: "private command", aggregatedOutput: "private output" },
      ],
    },
    {
      id: "turn-oldest",
      items: [
        { id: "user-old", type: "userMessage", text: "Earlier question" },
        { id: "assistant-old", type: "agentMessage", text: "Earlier response" },
      ],
    },
  ]);

  assert.deepEqual(restored, [
    { id: "user-old", role: "user", text: "Earlier question" },
    { id: "assistant-old", role: "assistant", text: "Earlier response" },
    { id: "user-new", role: "user", text: "Latest question\n\n[Non-text attachment omitted from this preview.]" },
    { id: "assistant-new", role: "assistant", text: "Latest response" },
  ]);
  assert.equal(JSON.stringify(restored).includes("private command"), false);
  assert.equal(JSON.stringify(restored).includes("private output"), false);
});

test("returns an empty transcript for malformed turn input", () => {
  assert.deepEqual(restoreVisibleTranscript(null), []);
});

test("restored truncated assistant items retain exact inspection identity and omission evidence", () => {
  const restored = restoreVisibleTranscript([{
    id: "turn-large",
    items: [{
      id: "assistant-large",
      type: "agentMessage",
      text: "preview",
      neoBabylonDisplay: {
        displayTruncated: true,
        omittedCharacters: 14,
        sourceRetained: true,
        upstreamTruncated: false,
      },
    }],
  }], "thread-large");

  assert.deepEqual(restored, [{
    id: "assistant-large",
    role: "assistant",
    text: "preview",
    threadId: "thread-large",
    turnId: "turn-large",
    displayTruncated: true,
    omittedCharacters: 14,
    sourceRetained: true,
    upstreamTruncated: false,
  }]);
});

test("restores only readable reasoning content or an explicitly labeled summary", () => {
  const restored = restoreVisibleTranscript([{
    id: "turn-reasoning",
    items: [
      { id: "reasoning-content", type: "reasoning", summary: ["summary ignored"], content: [" first\n", "second "] },
      { id: "reasoning-summary", type: "reasoning", summary: [" brief", " summary "], content: [] },
      { id: "reasoning-opaque", type: "reasoning", summary: [], content: [], encryptedContent: "ciphertext" },
      { id: "reasoning-omitted", type: "reasoning", summary: [], content: [],
        neoBabylonDisplay: { displayTruncated: true, omittedCharacters: 0, omittedParts: true, sourceRetained: true } },
      { id: "agent-commentary", type: "agentMessage", text: "not reasoning" },
    ],
  }], "thread-reasoning");

  assert.deepEqual(restored, [
    { id: "reasoning-content", role: "reasoning", text: " first\nsecond ", reasoningLabel: "Reasoning",
      threadId: "thread-reasoning", turnId: "turn-reasoning", streaming: false },
    { id: "reasoning-summary", role: "reasoning", text: " brief summary ", reasoningLabel: "Reasoning summary",
      threadId: "thread-reasoning", turnId: "turn-reasoning", streaming: false },
    { id: "reasoning-omitted-thread-reasoning-turn-reasoning-reasoning-omitted", role: "status",
      text: "Additional reasoning parts were omitted from this preview.", reasoningOmission: true,
      threadId: "thread-reasoning", turnId: "turn-reasoning", itemId: "reasoning-omitted",
      displayTruncated: true, omittedCharacters: 0, sourceRetained: true, omittedParts: true },
    { id: "agent-commentary", role: "assistant", text: "not reasoning", threadId: "thread-reasoning", turnId: "turn-reasoning" },
  ]);
});

test("restores saved tool output with exact item identity, attribution, and source omission status", () => {
  assert.deepEqual(restoreSavedActivities([{
    itemId: "tool-large",
    turnId: "turn-large",
    itemType: "commandExecution",
    title: "cmd /c ver",
    text: "retained head and tail",
    outcome: "succeeded",
    neoBabylonDisplay: {
      displayTruncated: true,
      omittedCharacters: 12,
      sourceRetained: true,
      upstreamTruncated: true,
    },
  }], "thread-large"), [{
    id: "tool-large",
    title: "cmd /c ver",
    detail: "retained head and tail",
    status: "succeeded",
    itemType: "commandExecution",
    turnId: "turn-large",
    threadId: "thread-large",
    displayTruncated: true,
    omittedCharacters: 12,
    sourceRetained: true,
    upstreamTruncated: true,
  }]);
});

test("a prior interrupted turn remains attributed after a later completed turn", () => {
  const restored = restoreVisibleTranscript([
    { id: "turn-later", status: "completed", items: [
      { id: "later-user", type: "userMessage", text: "Continue" },
      { id: "later-answer", type: "agentMessage", text: "Done" },
    ] },
    { id: "turn-earlier", status: "interrupted", items: [
      { id: "earlier-user", type: "userMessage", text: "Held request" },
    ] },
  ]);
  assert.deepEqual(restored, [
    { id: "earlier-user", role: "user", text: "Held request" },
    { id: "turn-earlier-status", role: "status", outcome: "interrupted",
      text: "Codex App Server: Turn interrupted. No automatic replay. Only server-saved content can be restored; any partial streamed response not retained by App Server is unavailable after reopening." },
    { id: "later-user", role: "user", text: "Continue" },
    { id: "later-answer", role: "assistant", text: "Done" },
  ]);
});

test("live transport loss stays unconfirmed until App Server reports a terminal turn status", () => {
  assert.deepEqual(restoration.createLiveTurnStatusMessage(
    { eventType: "turnFailure", terminal: false, turnStatus: null }, "lost-status"),
    { id: "lost-status", role: "status", outcome: "unconfirmed",
      text: "NeoBabylon host: Turn outcome unconfirmed. No automatic replay." });
  assert.deepEqual(restoration.createLiveTurnStatusMessage(
    { eventType: "turnFailure", terminal: true, turnStatus: "failed" }, "failed-status"),
    { id: "failed-status", role: "status", outcome: "failed",
      text: "Codex App Server: Turn failed. No automatic replay." });
  assert.deepEqual(restoration.createLiveTurnStatusMessage(
    { eventType: "turnInterrupted", terminal: true, turnStatus: "interrupted" }, "interrupted-status"),
    { id: "interrupted-status", role: "status", outcome: "interrupted",
      text: "Codex App Server: Turn interrupted. No automatic replay. Only server-saved content can be restored; any partial streamed response not retained by App Server is unavailable after reopening." });
  assert.equal(restoration.resolveLiveTurnState(
    { eventType: "turnFailure", terminal: false, turnStatus: null }), "unknown");
  assert.equal(restoration.resolveLiveTurnState(
    { eventType: "turnFailure", terminal: true, turnStatus: "failed" }), "failed");
  assert.equal(restoration.resolveLiveTurnState(
    { eventType: "turnInterrupted", terminal: true, turnStatus: "interrupted" }), "interrupted");
});

test("completed turns with tool warnings stay completed while real failures and uncertain outcomes remain distinct", () => {
  const missingOutput = {
    eventType: "turnCompletedWithToolFailure",
    terminal: true,
    turnStatus: "completed",
    completed: true,
    failure: {
      type: "toolExecution",
      attributedTo: "Codex App Server",
      message: "Command exited with code 1.",
    },
    toolDiagnostics: [{
      itemId: "f15a0939-41e0-47fb-803e-c69c339f1b35",
      toolName: "shell",
      command: "Get-Content out.log",
      output: "Get-Content: Cannot find path 'out.log'.",
      failure: { message: "Command exited with code 1." },
      succeeded: false,
      exitCode: 1,
    }],
  };

  assert.deepEqual(restoration.resolveLiveTurnOutcome(missingOutput), {
    state: "completed",
    fatal: false,
    warning: "The model turn completed, but 1 tool action failed. See the failed tool activity for its exact error and output.",
  });
  assert.deepEqual(restoration.resolveLiveTurnOutcome({
    eventType: "turnCompleted", terminal: true, turnStatus: "completed", completed: true,
  }), { state: "completed", fatal: false, warning: null });
  assert.deepEqual(restoration.resolveLiveTurnOutcome({
    eventType: "turnFailure", terminal: true, turnStatus: "failed", completed: false,
    failure: { type: "providerFailure", message: "The provider rejected the request." },
    providerTurnFailure: { attributedTo: "provider", message: "Request rejected." },
    toolDiagnostics: [{ succeeded: false, failure: { message: "An earlier tool failed." } }],
  }), { state: "failed", fatal: true, warning: null });
  assert.deepEqual(restoration.resolveLiveTurnOutcome({
    eventType: "turnFailure", terminal: true, turnStatus: "failed", completed: false,
    failure: { type: "toolExecution", message: "An earlier tool failed." },
  }), { state: "failed", fatal: true, warning: null });
  assert.deepEqual(restoration.resolveLiveTurnOutcome({
    eventType: "turnInterrupted", terminal: true, turnStatus: "interrupted", completed: false,
  }), { state: "interrupted", fatal: false, warning: null });
  assert.deepEqual(restoration.resolveLiveTurnOutcome({
    eventType: "turnFailure", terminal: false, turnStatus: null, completed: false,
  }), { state: "unknown", fatal: false, warning: null });
  assert.deepEqual(restoration.resolveLiveTurnOutcome({
    eventType: "turnCompleted", terminal: true, turnStatus: "completed", completed: false,
  }), { state: "unknown", fatal: false, warning: null });

  assert.deepEqual(restoration.restoreTurnOutcome([{ status: "completed", items: missingOutput.toolDiagnostics }]), {
    state: "completed", warning: null,
  });
  assert.deepEqual(restoration.restoreSavedActivities([{
    itemId: "f15a0939-41e0-47fb-803e-c69c339f1b35",
    itemType: "commandExecution",
    title: "Get-Content out.log",
    command: "Get-Content out.log",
    text: "Get-Content: Cannot find path 'out.log'.",
    error: "Command exited with code 1.",
    outcome: "failed",
    exitCode: 1,
  }]), [{
    id: "f15a0939-41e0-47fb-803e-c69c339f1b35",
    title: "Get-Content out.log",
    detail: "Get-Content: Cannot find path 'out.log'.",
    command: "Get-Content out.log",
    errorText: "Command exited with code 1.",
    status: "failed",
    exitCode: 1,
    itemType: "commandExecution",
    turnId: undefined,
    sourceRetained: false,
  }]);
});

test("restored task reports the latest saved turn outcome without replaying it", () => {
  assert.equal(typeof restoration.restoreTurnOutcome, "function", "a saved-turn outcome reader is required");
  assert.deepEqual(restoration.restoreTurnOutcome([{ status: "completed" }]), { state: "completed", warning: null });
  assert.deepEqual(restoration.restoreTurnOutcome([{ status: "interrupted" }, { status: "completed" }]), {
    state: "interrupted",
    warning: "The last saved turn was interrupted. It was not replayed when this task reopened.",
  });
  assert.deepEqual(restoration.restoreTurnOutcome([{ status: "failed" }]), {
    state: "failed",
    warning: "The last saved turn failed. It was not replayed when this task reopened.",
  });
  assert.deepEqual(restoration.restoreTurnOutcome([{ status: "inProgress" }]), {
    state: "unknown",
    warning: "The last saved turn has no confirmed final outcome. It was not replayed. Check its history before continuing.",
  });
  assert.deepEqual(restoration.restoreTurnOutcome([{ status: "surprise" }]), {
    state: "unknown",
    warning: "The last saved turn has no confirmed final outcome. It was not replayed. Check its history before continuing.",
  });
  assert.deepEqual(restoration.restoreTurnOutcome([]), { state: "idle", warning: null });
});
