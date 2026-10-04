const DEFAULT_MAX_DIFF_CHARACTERS = 250_000;

export function reduceTurnReview(current, method, params, expectedThreadId, maxDiffCharacters = DEFAULT_MAX_DIFF_CHARACTERS) {
  if (!params || typeof params !== "object" || Array.isArray(params) || !expectedThreadId) return current;
  if (method === "turn/started") {
    const turnId = params.turn?.id;
    if (params.threadId !== expectedThreadId || typeof turnId !== "string" || !turnId) return current;
    return { threadId: expectedThreadId, turnId, source: "liveTurnDiff", status: "awaiting", diff: null };
  }
  if (method !== "turn/diff/updated" || !current
    || params.threadId !== expectedThreadId || params.threadId !== current.threadId
    || params.turnId !== current.turnId || typeof params.diff !== "string") return current;
  if (!params.diff.length) return { ...current, status: "invalidated", diff: null };
  if (params.diff.length > maxDiffCharacters) return { ...current, status: "oversized", diff: null };
  return { ...current, status: "available", diff: params.diff };
}

export function restoreSavedTurnReview(reviews, threadId, maxDiffCharacters = DEFAULT_MAX_DIFF_CHARACTERS) {
  if (!Array.isArray(reviews) || typeof threadId !== "string" || !threadId) return null;
  const selected = reviews.find((entry) => entry && typeof entry === "object"
    && entry.source === "savedFileChangeItems" && typeof entry.turnId === "string" && entry.turnId);
  if (!selected) return null;
  const base = { threadId, turnId: selected.turnId, source: "savedFileChangeItems" };
  if (selected.status === "oversized") return { ...base, status: "oversized", diff: null };
  if (selected.status !== "available" || !Array.isArray(selected.changes) || !selected.changes.length) {
    return { ...base, status: "unavailable", diff: null };
  }
  const pieces = [];
  let length = 0;
  for (const change of selected.changes) {
    if (!change || typeof change.path !== "string" || !change.path
      || !["add", "delete", "update"].includes(change.kind)
      || typeof change.diff !== "string" || !change.diff
      || change.movePath != null && typeof change.movePath !== "string") {
      return { ...base, status: "unavailable", diff: null };
    }
    const label = `${change.path} · ${change.kind}${change.movePath ? ` → ${change.movePath}` : ""}\n`;
    const piece = label + change.diff;
    length += piece.length + (pieces.length ? 2 : 0);
    if (length > maxDiffCharacters) return { ...base, status: "oversized", diff: null };
    pieces.push(piece);
  }
  return { ...base, status: "available", diff: pieces.join("\n\n") };
}
