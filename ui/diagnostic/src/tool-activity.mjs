const record = (value) => value && typeof value === "object" && !Array.isArray(value) ? value : undefined;
const nonBlank = (value) => typeof value === "string" && value.trim() ? value : undefined;

function parsedArguments(value) {
  if (typeof value !== "string") return record(value);
  try {
    return record(JSON.parse(value));
  } catch {
    return undefined;
  }
}

export function toolArgumentsText(value) {
  if (typeof value === "string") return value;
  if (value === undefined || value === null) return undefined;
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}

export function toolActivityTitle({ title, command, toolName, tool, itemType, argumentsValue } = {}) {
  const identity = nonBlank(toolName) ?? nonBlank(tool) ?? nonBlank(itemType);
  const generic = new Set([identity, nonBlank(itemType), "exec_command", "functionCallOutput"].filter(Boolean));
  const args = parsedArguments(argumentsValue);
  const argumentCommand = nonBlank(args?.cmd);
  if (argumentCommand && (generic.has(nonBlank(command)) || generic.has(nonBlank(title)) || !nonBlank(command))) {
    return argumentCommand;
  }

  for (const candidate of [command, title, toolName, tool, itemType]) {
    const value = nonBlank(candidate);
    if (value && !generic.has(value)) return value;
  }

  if (identity === "write_stdin") {
    return nonBlank(args?.chars) ? "Send input to command session" : "Check command session";
  }
  return identity ?? nonBlank(title) ?? "Tool activity";
}

export function mergeOutputRange(previous, next) {
  if (!previous) return next;
  const sameItem = previous.threadId === next.threadId
    && previous.turnId === next.turnId
    && previous.itemId === next.itemId;
  if (!sameItem || previous.nextOffset !== next.offset) {
    throw new Error("Saved output pages must belong to the same item and be contiguous.");
  }
  return {
    ...next,
    offset: previous.offset,
    text: previous.text + next.text,
    upstreamTruncated: previous.upstreamTruncated || next.upstreamTruncated,
    omittedParts: previous.omittedParts || next.omittedParts,
  };
}

export function projectOutputRangePage(responseValue, location, requestedOffset) {
  const response = record(responseValue);
  if (!response
      || response.threadId !== location.threadId
      || response.turnId !== location.turnId
      || response.itemId !== location.itemId
      || response.itemType !== location.itemType
      || response.offset !== requestedOffset) {
    throw new Error("The saved output response did not match the host-returned thread, turn, item, and requested range identity.");
  }
  return {
    threadId: response.threadId,
    turnId: response.turnId,
    itemId: response.itemId,
    itemType: response.itemType,
    offset: response.offset,
    totalCharacters: Number(response.totalCharacters) || 0,
    text: typeof response.text === "string" ? response.text : "",
    nextOffset: Number(response.nextOffset) || 0,
    hasMore: response.hasMore === true,
    upstreamTruncated: response.upstreamTruncated === true,
    omittedParts: response.omittedParts === true,
  };
}

export function mergeActivityHistoryEntry(previous, next) {
  const sameScope = (!previous.threadId || !next.threadId || previous.threadId === next.threadId)
    && (!previous.turnId || !next.turnId || previous.turnId === next.turnId);
  const sameOutputItem = !previous.outputItemId || !next.outputItemId || previous.outputItemId === next.outputItemId;
  const preserveRetained = sameScope && sameOutputItem && previous.sourceRetained === true;
  const merged = { ...previous, ...next };
  if (!sameScope || !sameOutputItem) return merged;
  for (const name of ["command", "argumentsText", "errorText", "itemType", "threadId", "turnId", "outputItemId", "detail", "exitCode", "durationMs"]) {
    if (next[name] === undefined && previous[name] !== undefined) merged[name] = previous[name];
  }
  if (preserveRetained) merged.sourceRetained = true;
  return merged;
}
