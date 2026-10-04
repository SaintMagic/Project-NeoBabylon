export function filterSavedThreads(threads, query) {
  const normalizedQuery = query.trim().toLowerCase();
  if (!normalizedQuery) return threads;

  return threads.filter((thread) => [thread.name, thread.preview, thread.modelProvider, thread.model]
    .some((value) => typeof value === "string" && value.toLowerCase().includes(normalizedQuery)));
}

export function savedThreadLabel(thread) {
  if (typeof thread.preview === "string" && thread.preview.trim()) return thread.preview;
  if (typeof thread.forkedFromId === "string" && thread.forkedFromId.trim()) {
    return `Fork of ${thread.forkedFromId.slice(0, 8)}`;
  }
  return "Untitled task";
}

export function mergeHistoryPages(current, incoming) {
  const merged = [...current];
  const indexes = new Map(current.map((thread, index) => [thread.id, index]));
  for (const thread of incoming) {
    const existing = indexes.get(thread.id);
    if (existing === undefined) {
      indexes.set(thread.id, merged.length);
      merged.push(thread);
    } else {
      if (savedThreadUpdatedAt(thread.updatedAt) >= savedThreadUpdatedAt(merged[existing].updatedAt)) {
        merged[existing] = thread;
      }
    }
  }

  return merged.map((thread, index) => ({ thread, index, updatedAt: savedThreadUpdatedAt(thread.updatedAt) }))
    .sort((left, right) => {
      if (left.updatedAt === 0 && right.updatedAt !== 0) return 1;
      if (left.updatedAt !== 0 && right.updatedAt === 0) return -1;
      return right.updatedAt - left.updatedAt || left.index - right.index;
    })
    .map(({ thread }) => thread);
}

export function savedThreadUpdatedAt(value) {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value === "string" && value.trim()) {
    const parsed = Date.parse(value);
    if (Number.isFinite(parsed)) return parsed;
  }
  return 0;
}

export function savedThreadSearchEmptyMessage(query, hasMorePages) {
  const normalizedQuery = query.trim();
  if (!normalizedQuery) return null;
  if (hasMorePages) {
    return "No matches in loaded conversations. Load older conversations to search more.";
  }
  return `No saved conversations match “${normalizedQuery}”.`;
}

export function savedHistoryState({ unavailable, query, resultCount }) {
  if (unavailable) return "unavailable";
  if (query.trim() && resultCount === 0) return "search-empty";
  if (resultCount === 0) return "empty";
  return "loaded";
}
