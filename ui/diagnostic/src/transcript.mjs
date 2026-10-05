const MAX_ASSISTANT_DISPLAY_CHARACTERS = 120_000;
// Keep only a bounded React transcript preview; Codex App Server remains authoritative.
const MAX_TRANSCRIPT_ENTRIES = 200;
const MAX_TRANSCRIPT_TEXT_CHARACTERS = 500_000;
const TRANSCRIPT_NOTICE_TEXT_RESERVE = 512;
const MAX_TRANSCRIPT_ENTRY_TEXT_CHARACTERS = MAX_TRANSCRIPT_TEXT_CHARACTERS - TRANSCRIPT_NOTICE_TEXT_RESERVE;
const TRANSCRIPT_WINDOW_NOTICE_ID = "neobabylon-local-transcript-window";

export function transcriptMessageKey(message) {
  const itemId = message.reasoningOmission === true && typeof message.itemId === "string"
    ? message.itemId
    : message.id;
  return JSON.stringify([message.role, message.threadId ?? "", message.turnId ?? "", itemId]);
}

export function boundTranscript(current) {
  const previousNotice = current.find((message) => message.transcriptWindowOmission);
  let omittedEntries = previousNotice?.transcriptWindowOmission?.omittedEntries ?? 0;
  let omittedCharacters = previousNotice?.transcriptWindowOmission?.omittedCharacters ?? 0;
  const entries = current.filter((message) => !message.transcriptWindowOmission);
  let totalCharacters = entries.reduce((total, message) => total + message.text.length, 0);

  while (entries.length > MAX_TRANSCRIPT_ENTRIES
    || (totalCharacters > MAX_TRANSCRIPT_ENTRY_TEXT_CHARACTERS && entries.length > 1)) {
    const removed = entries.shift();
    omittedEntries += 1;
    omittedCharacters += removed.text.length;
    totalCharacters -= removed.text.length;
  }

  if (entries.length === 1 && totalCharacters > MAX_TRANSCRIPT_ENTRY_TEXT_CHARACTERS) {
    const latest = entries[0];
    const removedCharacters = latest.text.length - MAX_TRANSCRIPT_ENTRY_TEXT_CHARACTERS;
    entries[0] = {
      ...latest,
      text: latest.text.slice(0, MAX_TRANSCRIPT_ENTRY_TEXT_CHARACTERS),
    };
    omittedCharacters += removedCharacters;
    totalCharacters = MAX_TRANSCRIPT_ENTRY_TEXT_CHARACTERS;
  }

  if (omittedEntries === 0 && omittedCharacters === 0) return entries;

  const omittedParts = [];
  if (omittedEntries > 0) omittedParts.push(`${omittedEntries.toLocaleString()} earlier entr${omittedEntries === 1 ? "y is" : "ies are"} omitted`);
  if (omittedCharacters > 0) omittedParts.push(`${omittedCharacters.toLocaleString()} characters are omitted`);
  return [{
    id: TRANSCRIPT_WINDOW_NOTICE_ID,
    role: "status",
    text: `This local preview is limited to the latest ${MAX_TRANSCRIPT_ENTRIES} entries and ${MAX_TRANSCRIPT_TEXT_CHARACTERS.toLocaleString()} characters. ${omittedParts.join("; ")} from the displayed transcript. Older saved conversation data remains in Codex App Server; unsent or local-only text may not have been saved.`,
    transcriptWindowOmission: { omittedEntries, omittedCharacters },
  }, ...entries];
}

export function projectDisplayText(value, limit, metadata = {}) {
  const text = typeof value === "string" ? value : "";
  const displayText = text.slice(0, limit);
  const locallyOmitted = Math.max(0, text.length - displayText.length);
  const reportedOmitted = Number.isSafeInteger(metadata.omittedCharacters) && metadata.omittedCharacters > 0
    ? metadata.omittedCharacters
    : 0;
  const omittedCharacters = Math.max(locallyOmitted, reportedOmitted);
  return {
    text: displayText,
    ...(omittedCharacters > 0 || metadata.displayTruncated === true ? {
      displayTruncated: true,
      omittedCharacters,
      sourceRetained: metadata.sourceRetained === true,
      upstreamTruncated: metadata.upstreamTruncated === true,
    } : {}),
  };
}

export function appendAssistantDelta(current, id, delta, displayMetadata) {
  const index = current.findIndex((message) => message.id === id && message.role === "assistant");
  if (index < 0) {
    const projected = projectDisplayText(delta, MAX_ASSISTANT_DISPLAY_CHARACTERS, displayMetadata);
    return [...current, { id, role: "assistant", ...projected, streaming: true }];
  }
  return current.map((message, itemIndex) => itemIndex === index
    ? {
      ...message,
      ...projectDisplayText(message.text + delta, MAX_ASSISTANT_DISPLAY_CHARACTERS, {
        ...displayMetadata,
        omittedCharacters: Math.max(message.omittedCharacters ?? 0, displayMetadata?.omittedCharacters ?? 0),
        displayTruncated: message.displayTruncated || displayMetadata?.displayTruncated === true,
        sourceRetained: message.sourceRetained || displayMetadata?.sourceRetained === true,
        upstreamTruncated: message.upstreamTruncated || displayMetadata?.upstreamTruncated === true,
      }),
      streaming: true,
    }
    : message);
}

export function finishAssistantStreams(current) {
  let changed = false;
  const next = current.map((message) => {
    if ((message.role !== "assistant" && message.role !== "reasoning") || message.streaming !== true) return message;
    changed = true;
    return { ...message, streaming: false };
  });
  return changed ? next : current;
}

export function markAssistantTurnFailed(current, userMessageId, turnId, threadId) {
  const userIndex = current.findIndex((message) => message.id === userMessageId && message.role === "user");
  if (userIndex < 0) return current;
  const assistantIndex = current.findLastIndex((message, index) => index > userIndex && message.role === "assistant");
  if (assistantIndex < 0) return current;
  return current.map((message, index) => index === assistantIndex
    ? {
      ...message,
      failed: true,
      streaming: false,
      ...(turnId ? { turnId } : {}),
      ...(threadId ? { threadId } : {}),
    }
    : message);
}

export function upsertAssistantMessage(current, text, streaming, failed = false, id, displayMetadata, turnId, threadId) {
  // A provider item id is authoritative. Text equality is only a fallback for
  // the host's id-less final summary, and only inside the current user turn.
  const userIndex = current.findLastIndex((message) => message.role === "user");
  const sameTurn = (message) => message.role === "assistant"
    && (!turnId || !message.turnId || message.turnId === turnId)
    && (!threadId || !message.threadId || message.threadId === threadId);
  const tail = current.slice(userIndex + 1);
  const last = tail.at(-1);
  const fallback = id === undefined
    ? tail.findLast((message) => sameTurn(message) && message.streaming)
      ?? (last && sameTurn(last) && last.text.trim() === text.trim() ? last : undefined)
    : undefined;
  const targetId = id ?? fallback?.id ?? `assistant-fallback-${crypto.randomUUID()}`;
  const index = current.findIndex((message) => message.id === targetId && message.role === "assistant");
  const next = {
    id: targetId,
    role: "assistant",
    ...projectDisplayText(text, MAX_ASSISTANT_DISPLAY_CHARACTERS, displayMetadata),
    streaming,
    failed,
    ...(turnId ? { turnId } : {}),
    ...(threadId ? { threadId } : {}),
  };
  if (index >= 0) return current.map((message, itemIndex) => itemIndex === index ? next : message);

  return [...current, next];
}
