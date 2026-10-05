const MAX_REASONING_DISPLAY_CHARACTERS = 120_000;
const MAX_REASONING_PARTS = 128;

function strings(value) {
  if (!Array.isArray(value)) return { parts: [], omittedParts: false };
  const parts = [];
  const inspected = Math.min(value.length, MAX_REASONING_PARTS);
  for (let index = 0; index < inspected; index++) {
    if (typeof value[index] === "string") parts.push(value[index]);
  }
  return { parts, omittedParts: value.length > inspected };
}

function identity(message, threadId, turnId) {
  return (message.role === "reasoning" || message.reasoningOmission === true)
    && message.threadId === threadId
    && message.turnId === turnId;
}

function logicalItemId(message) {
  return message.reasoningOmission === true && typeof message.itemId === "string"
    ? message.itemId
    : message.id;
}

function projectParts(parts) {
  let remaining = MAX_REASONING_DISPLAY_CHARACTERS;
  let omittedCharacters = 0;
  const text = parts.map((part) => {
    const displayed = part.slice(0, remaining);
    remaining -= displayed.length;
    omittedCharacters += part.length - displayed.length;
    return displayed;
  }).join("");
  return { text, omittedCharacters };
}

function upsert(current, next) {
  const index = current.findIndex((message) =>
    identity(message, next.threadId, next.turnId) && logicalItemId(message) === logicalItemId(next));
  if (index < 0) return [...current, next];
  return current.map((message, messageIndex) => messageIndex === index ? next : message);
}

function remove(current, itemId, threadId, turnId) {
  return current.filter((message) => !identity(message, threadId, turnId) || logicalItemId(message) !== itemId);
}

export function upsertReasoningItem(current, item, { threadId, turnId } = {}) {
  const itemId = typeof item?.id === "string" ? item.id : null;
  if (item?.type !== "reasoning" || !itemId || !threadId || !turnId) return current;
  const content = strings(item.content);
  const summary = strings(item.summary);
  const hasContent = content.parts.join("").length > 0 || content.omittedParts;
  const parts = hasContent ? content.parts : summary.parts;
  const omittedParts = hasContent ? content.omittedParts : summary.omittedParts;
  const projected = projectParts(parts);
  const metadata = item.neoBabylonDisplay && typeof item.neoBabylonDisplay === "object"
    ? item.neoBabylonDisplay
    : {};
  const omittedCharacters = Math.max(projected.omittedCharacters,
    Number.isSafeInteger(metadata.omittedCharacters) ? metadata.omittedCharacters : 0);
  const didOmitParts = omittedParts || metadata.omittedParts === true;
  if (!projected.text) {
    if (!didOmitParts && omittedCharacters === 0 && metadata.displayTruncated !== true) {
      return remove(current, itemId, threadId, turnId);
    }
    const existing = current.find((message) =>
      identity(message, threadId, turnId) && logicalItemId(message) === itemId);
    if (existing?.role === "reasoning") {
      return current.map((message) => message === existing
        ? {
          ...message,
          displayTruncated: true,
          omittedCharacters: Math.max(message.omittedCharacters ?? 0, omittedCharacters),
          sourceRetained: metadata.sourceRetained === true,
          upstreamTruncated: metadata.upstreamTruncated === true,
          omittedParts: didOmitParts,
          streaming: false,
        }
        : message);
    }
    return upsert(current, {
      id: "reasoning-omitted-" + threadId + "-" + turnId + "-" + itemId,
      role: "status",
      text: didOmitParts
        ? "Additional reasoning parts were omitted from this preview."
        : omittedCharacters > 0
          ? omittedCharacters + " reasoning characters were omitted from this preview."
          : "The reasoning preview was omitted.",
      reasoningOmission: true,
      threadId,
      turnId,
      itemId,
      displayTruncated: true,
      omittedCharacters,
      sourceRetained: metadata.sourceRetained === true,
      omittedParts: didOmitParts,
    });
  }
  return upsert(current, {
    id: itemId,
    role: "reasoning",
    text: projected.text,
    reasoningLabel: hasContent ? "Reasoning" : "Reasoning summary",
    threadId,
    turnId,
    streaming: false,
    ...(omittedCharacters > 0 || didOmitParts || metadata.displayTruncated === true ? {
      displayTruncated: true,
      omittedCharacters,
      sourceRetained: metadata.sourceRetained === true,
      upstreamTruncated: metadata.upstreamTruncated === true,
      omittedParts: didOmitParts,
    } : {}),
  });
}

export function upsertReasoningDelta(current, method, params) {
  if (method !== "item/reasoning/summaryTextDelta" && method !== "item/reasoning/textDelta") return current;
  const threadId = typeof params?.threadId === "string" ? params.threadId : null;
  const turnId = typeof params?.turnId === "string" ? params.turnId : null;
  const itemId = typeof params?.itemId === "string" ? params.itemId : null;
  const delta = typeof params?.delta === "string" ? params.delta : null;
  const field = method.endsWith("summaryTextDelta") ? "summaryIndex" : "contentIndex";
  const index = params?.[field];
  if (!threadId || !turnId || !itemId || delta === null || delta.length === 0
    || !Number.isSafeInteger(index) || index < 0 || index >= MAX_REASONING_PARTS) return current;

  const existing = current.find((message) => identity(message, threadId, turnId) && message.id === itemId);
  const summaryParts = existing?.summaryParts ? [...existing.summaryParts] : [];
  const contentParts = existing?.contentParts ? [...existing.contentParts] : [];
  const target = field === "summaryIndex" ? summaryParts : contentParts;
  target[index] = (typeof target[index] === "string" ? target[index] : "") + delta;
  const hasContent = contentParts.some((part) => typeof part === "string" && part.length > 0);
  const projected = projectParts(hasContent ? contentParts : summaryParts);
  if (!projected.text) return current;
  const metadata = params.neoBabylonDisplay && typeof params.neoBabylonDisplay === "object"
    ? params.neoBabylonDisplay
    : {};
  const omittedCharacters = Math.max(projected.omittedCharacters, Number(metadata.omittedCharacters) || 0);
  return upsert(current, {
    id: itemId,
    role: "reasoning",
    text: projected.text,
    reasoningLabel: hasContent ? "Reasoning" : "Reasoning summary",
    threadId,
    turnId,
    summaryParts,
    contentParts,
    streaming: true,
    ...(omittedCharacters > 0 || metadata.displayTruncated === true ? {
      displayTruncated: true,
      omittedCharacters,
      sourceRetained: metadata.sourceRetained === true,
      upstreamTruncated: metadata.upstreamTruncated === true,
    } : {}),
  });
}
