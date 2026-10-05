import assert from "node:assert/strict";
import test from "node:test";
import * as transcript from "../src/transcript.mjs";
const { appendAssistantDelta, upsertAssistantMessage, markAssistantTurnFailed, boundTranscript } = transcript;

test("stream delta, completed item, and final fallback render one assistant message", () => {
  const user = { id: "user-1", role: "user", text: "Run the version command." };
  const streamed = appendAssistantDelta([user], "agent-1", "Microsoft Windows ");
  const completed = upsertAssistantMessage(streamed, "Microsoft Windows [Version 10.0.26200.9457]", false, false, "agent-1");
  const final = upsertAssistantMessage(completed, "Microsoft Windows [Version 10.0.26200.9457]", false);

  assert.equal(final.length, 2);
  assert.equal(final[1].id, "agent-1");
  assert.equal(final[1].text, "Microsoft Windows [Version 10.0.26200.9457]");
  assert.equal(final[1].streaming, false);
});

test("assistant messages with different text remain distinct", () => {
  const first = { id: "agent-1", role: "assistant", text: "First answer", streaming: false };
  const next = upsertAssistantMessage([first], "Second answer", false);

  assert.equal(next.length, 2);
  assert.equal(next[1].text, "Second answer");
});

test("terminal turn preserves partial assistant text but clears its active-stream state", () => {
  const user = { id: "user-1", role: "user", text: "Explain this slowly." };
  const streamed = appendAssistantDelta([user], "agent-partial", "The first useful part");
  assert.equal(typeof transcript.finishAssistantStreams, "function",
    "a terminal host outcome must finish any partial assistant stream");

  assert.deepEqual(transcript.finishAssistantStreams(streamed), [
    user,
    { id: "agent-partial", role: "assistant", text: "The first useful part", streaming: false },
  ]);
});

test("late provider failure marks only the partial assistant reply after its user prompt", () => {
  const priorUser = { id: "user-1", role: "user", text: "Earlier request" };
  const priorAssistant = { id: "agent-1", role: "assistant", text: "Earlier success", failed: false };
  const currentUser = { id: "user-2", role: "user", text: "Current request" };
  const partial = { id: "agent-2", role: "assistant", text: "Partial answer", streaming: true };

  const failed = markAssistantTurnFailed(
    [priorUser, priorAssistant, currentUser, partial], "user-2", "turn-2", "thread-2");

  assert.equal(failed[1].failed, false);
  assert.equal(failed[3].text, "Partial answer");
  assert.equal(failed[3].failed, true);
  assert.equal(failed[3].streaming, false);
  assert.equal(failed[3].turnId, "turn-2");
  assert.equal(failed[3].threadId, "thread-2");
});

test("assistant stream and final replacement stay within the display budget and retain exact omission metadata", () => {
  const displayLimit = 120_000;
  const first = appendAssistantDelta([], "agent-large", "a".repeat(displayLimit));
  const streamed = appendAssistantDelta(first, "agent-large", "b".repeat(9));
  assert.equal(streamed[0].text.length, displayLimit);
  assert.equal(streamed[0].displayTruncated, true);
  assert.equal(streamed[0].omittedCharacters, 9);

  const completed = upsertAssistantMessage(streamed, "c".repeat(displayLimit + 13), false, false, "agent-large");
  assert.equal(completed[0].text.length, displayLimit);
  assert.equal(completed[0].displayTruncated, true);
  assert.equal(completed[0].omittedCharacters, 13);
});

test("transcript projection bounds total entries and clearly preserves an omission disclosure", () => {
  assert.equal(typeof boundTranscript, "function", "React transcript updates must pass through a total-budget projection");
  const messages = Array.from({ length: 205 }, (_, index) => ({
    id: `entry-${index + 1}`,
    role: index % 2 === 0 ? "user" : "assistant",
    text: `text-${index + 1}`,
  }));

  const projected = boundTranscript(messages);
  const notices = projected.filter((message) => message.transcriptWindowOmission);
  const entries = projected.filter((message) => !message.transcriptWindowOmission);

  assert.equal(entries.length, 200);
  assert.deepEqual(entries.map((message) => message.id),
    Array.from({ length: 200 }, (_, index) => `entry-${index + 6}`));
  assert.equal(notices.length, 1);
  assert.equal(notices[0].transcriptWindowOmission.omittedEntries, 5);
  assert.ok(projected.reduce((total, message) => total + message.text.length, 0) <= 500_000);
  assert.match(notices[0].text, /Codex App Server/);
  assert.match(notices[0].text, /older saved/i);
});

test("transcript projection enforces the total text ceiling and carries omissions across updates", () => {
  assert.equal(typeof boundTranscript, "function", "React transcript updates must pass through a total-budget projection");
  const oversized = [{ id: "latest", role: "user", text: "x".repeat(500_100) }];
  const first = boundTranscript(oversized);
  const firstEntries = first.filter((message) => !message.transcriptWindowOmission);

  assert.equal(firstEntries.reduce((total, message) => total + message.text.length, 0), 499_488);
  assert.equal(firstEntries[0].text, "x".repeat(499_488));
  assert.ok(first.reduce((total, message) => total + message.text.length, 0) <= 500_000);
  assert.equal(first.find((message) => message.transcriptWindowOmission)
    .transcriptWindowOmission.omittedCharacters, 612);

  const next = boundTranscript([...first, { id: "next", role: "assistant", text: "newest" }]);
  const nextEntries = next.filter((message) => !message.transcriptWindowOmission);
  const notice = next.find((message) => message.transcriptWindowOmission);

  assert.deepEqual(nextEntries.map((message) => message.id), ["next"]);
  assert.equal(nextEntries.reduce((total, message) => total + message.text.length, 0), 6);
  assert.equal(notice.transcriptWindowOmission.omittedCharacters, 500_100);
  assert.equal(notice.transcriptWindowOmission.omittedEntries, 1);
  assert.ok(next.reduce((total, message) => total + message.text.length, 0) <= 500_000);
});

test("explicit assistant item identities are not merged by equal text", () => {
  const first = { id: "agent-1", role: "assistant", text: "Done.", turnId: "turn-1" };
  const next = upsertAssistantMessage([first], "Done.", false, false, "agent-2", undefined, "turn-1");
  assert.deepEqual(next.map(message => message.id), ["agent-1", "agent-2"]);
  const repeated = upsertAssistantMessage(next, "Done.", false, false, "agent-2", undefined, "turn-1");
  assert.equal(repeated.length, 2, "a repeated completion of the same item is idempotent");
});

test("a fallback completion never overwrites an earlier user turn", () => {
  const first = upsertAssistantMessage([{ id: "user-1", role: "user", text: "First" }], "Done.", false);
  const next = upsertAssistantMessage([...first, { id: "user-2", role: "user", text: "Second" }], "Done.", false);
  assert.equal(next.length, 4);
  assert.equal(next[1].text, "Done.");
  assert.notEqual(next[1].id, next[3].id);
});

test("fallback deduplication respects known thread and turn identity", () => {
  const first = { id: "agent-1", role: "assistant", text: "Done.", turnId: "turn-1", threadId: "thread-1" };
  const next = upsertAssistantMessage([first], "Done.", false, false, undefined, undefined, "turn-2", "thread-1");
  assert.equal(next.length, 2);
  assert.equal(next[0].turnId, "turn-1");
  assert.equal(next[1].turnId, "turn-2");
});
