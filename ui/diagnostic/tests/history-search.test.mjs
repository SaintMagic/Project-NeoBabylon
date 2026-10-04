import assert from "node:assert/strict";
import test from "node:test";
import * as history from "../src/history.mjs";

const { filterSavedThreads, mergeHistoryPages, savedThreadLabel } = history;

const threads = [
  { id: "thread-1", preview: "Draft the release notes", modelProvider: "LM Studio", model: "qwen3-14b", updatedAt: 3 },
  { id: "thread-2", preview: "Review the runtime", modelProvider: "OpenRouter", model: "nex-n2.5-pro:free", updatedAt: 2 },
  { id: "thread-3", preview: "Untitled task", modelProvider: "LM Studio", model: "qwen3-14b", updatedAt: 1 },
];

test("saved thread search is trimmed, case-insensitive, and matches preview/provider/model", () => {
  assert.deepEqual(filterSavedThreads(threads, "  RELEASE "), [threads[0]]);
  assert.deepEqual(filterSavedThreads(threads, "openrouter"), [threads[1]]);
  assert.deepEqual(filterSavedThreads(threads, "NEX-N2.5-PRO"), [threads[1]]);
});

test("saved thread search includes the confirmed display name", () => {
  const renamed = { ...threads[0], name: "Quarterly planning" };
  assert.deepEqual(filterSavedThreads([renamed], "quarterly"), [renamed]);
});

test("an empty search preserves the original thread order and a miss returns no threads", () => {
  assert.deepEqual(filterSavedThreads(threads, "  "), threads);
  assert.deepEqual(filterSavedThreads(threads, "no matching conversation"), []);
});

test("saved fork rows get an explicit label when App Server has no preview", () => {
  assert.equal(savedThreadLabel({ preview: "Original prompt", forkedFromId: "parent-thread" }), "Original prompt");
  assert.equal(savedThreadLabel({ preview: "", forkedFromId: "0123456789abcdef" }), "Fork of 01234567");
  assert.equal(savedThreadLabel({ preview: "", forkedFromId: null }), "Untitled task");
});

test("older saved-history pages append once without replacing or duplicating loaded tasks", () => {
  const updated = { ...threads[1], preview: "Review the pinned runtime" };
  assert.deepEqual(mergeHistoryPages(threads.slice(0, 2), [updated, threads[2]]), [threads[0], updated, threads[2]]);
  assert.deepEqual(mergeHistoryPages(threads.slice(0, 2), []), threads.slice(0, 2));
});

test("history pages remain newest-first and stale overlap cannot replace newer thread metadata", () => {
  const firstPage = [
    { id: "thread-first", preview: "First page row", updatedAt: 3000 },
    { id: "thread-overlap", preview: "Earlier overlap", updatedAt: 2000 },
  ];
  const olderPage = [
    { id: "thread-oldest", preview: "Oldest row", updatedAt: 1000 },
    { id: "thread-overlap", preview: "Latest overlap", updatedAt: 4000 },
    { id: "thread-first", preview: "Stale first row", updatedAt: 2500 },
  ];

  assert.deepEqual(mergeHistoryPages(firstPage, olderPage), [
    { id: "thread-overlap", preview: "Latest overlap", updatedAt: 4000 },
    firstPage[0],
    olderPage[0],
  ]);
});

test("history merge orders RFC3339 timestamps and keeps untimed rows last", () => {
  const page = [
    { id: "unknown", preview: "Unknown date", updatedAt: 0 },
    { id: "old", preview: "Older date", updatedAt: "2026-09-23T10:00:00.000Z" },
    { id: "new", preview: "Newer date", updatedAt: "2026-09-24T10:00:00.000Z" },
  ];

  assert.deepEqual(mergeHistoryPages([], page).map((thread) => thread.id), ["new", "old", "unknown"]);
});

test("history search explains when more unloaded pages may contain a match", () => {
  assert.equal(typeof history.savedThreadSearchEmptyMessage, "function");
  assert.equal(history.savedThreadSearchEmptyMessage("  Older task ", true),
    "No matches in loaded conversations. Load older conversations to search more.");
  assert.equal(history.savedThreadSearchEmptyMessage("Older task", false),
    "No saved conversations match “Older task”.");
  assert.equal(history.savedThreadSearchEmptyMessage("  ", true), null);
});

test("saved-history state distinguishes empty, unavailable, search miss, and loaded results", () => {
  assert.equal(typeof history.savedHistoryState, "function");
  assert.equal(history.savedHistoryState({ unavailable: false, query: "", resultCount: 0, hasMorePages: false }), "empty");
  assert.equal(history.savedHistoryState({ unavailable: false, query: "older", resultCount: 0, hasMorePages: true }), "search-empty");
  assert.equal(history.savedHistoryState({ unavailable: false, query: "older", resultCount: 1, hasMorePages: false }), "loaded");
  assert.equal(history.savedHistoryState({ unavailable: true, query: "older", resultCount: 0, hasMorePages: true }), "unavailable");
});
