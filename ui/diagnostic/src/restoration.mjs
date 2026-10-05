import { upsertReasoningItem } from "./reasoning-transcript.mjs";
import { toolActivityTitle, toolArgumentsText } from "./tool-activity.mjs";

const recordValue = (value) => value && typeof value === "object" && !Array.isArray(value) ? value : undefined;
const textValue = (value) => typeof value === "string" && value.trim() ? value : undefined;

export function classifyActivityOutcome(statusValue, errorValue) {
  const status = textValue(statusValue)?.toLowerCase();
  const hasError = typeof errorValue === "string"
    ? errorValue.trim().length > 0
    : errorValue !== undefined && errorValue !== null;
  if (hasError || status === "failed") return "failed";
  if (status === "completed" || status === "succeeded") return "succeeded";
  return "info";
}

export function createTurnStatusMessage(status, id, attribution = "Codex App Server") {
  if (status !== "interrupted" && status !== "failed" && status !== "unconfirmed") return null;
  return {
    id,
    role: "status",
    outcome: status,
    text: status === "unconfirmed"
      ? `${attribution}: Turn outcome unconfirmed. No automatic replay.`
      : status === "interrupted"
        ? `${attribution}: Turn interrupted. No automatic replay. Only server-saved content can be restored; any partial streamed response not retained by App Server is unavailable after reopening.`
        : `${attribution}: Turn ${status}. No automatic replay.`,
  };
}

export function createLiveTurnStatusMessage(resultValue, id) {
  const result = recordValue(resultValue);
  if (result?.eventType !== "turnFailure" && result?.eventType !== "turnInterrupted") return null;
  if (result.terminal === true && result.turnStatus === "interrupted") {
    return createTurnStatusMessage("interrupted", id);
  }
  if (result.terminal === true && result.turnStatus === "failed") {
    return createTurnStatusMessage("failed", id);
  }
  return createTurnStatusMessage("unconfirmed", id, "NeoBabylon host");
}

export function resolveLiveTurnState(resultValue) {
  const result = recordValue(resultValue);
  const marker = createLiveTurnStatusMessage(result, "live-turn-state");
  if (marker?.outcome === "unconfirmed") return "unknown";
  if (marker?.outcome === "interrupted") return "interrupted";
  if (marker?.outcome === "failed") return "failed";
  if (result?.terminal === true && result.turnStatus === "completed" && result.completed === true
    && (result.eventType === "turnCompleted" || result.eventType === "turnCompletedWithToolFailure")) return "completed";
  return "unknown";
}

export function resolveLiveTurnOutcome(resultValue) {
  const result = recordValue(resultValue);
  const state = resolveLiveTurnState(result);
  const failure = recordValue(result?.failure);
  const providerTurnFailure = recordValue(result?.providerTurnFailure);
  const providerFailureMessage = textValue(providerTurnFailure?.message);
  const toolDiagnostics = Array.isArray(result?.toolDiagnostics) ? result.toolDiagnostics : [];
  const failedTools = toolDiagnostics.filter((value) => recordValue(value)?.succeeded === false).length;
  const toolFailure = failure
    ? failure.type === "toolExecution"
      || (failure.type === undefined && result?.eventType === "turnCompletedWithToolFailure")
    : result?.eventType === "turnCompletedWithToolFailure" || failedTools > 0;
  const fatal = state === "failed" || Boolean(providerFailureMessage) || Boolean(failure && !toolFailure);
  const toolCount = failedTools || 1;
  return {
    state,
    fatal,
    warning: state === "completed" && toolFailure
      ? `The model turn completed, but ${toolCount} tool ${toolCount === 1 ? "action failed" : "actions failed"}. See the failed tool activity for its exact error and output.`
      : null,
  };
}

export function restoreVisibleTranscript(turns, threadId) {
  if (!Array.isArray(turns)) return [];

  const restored = [];
  for (const turnValue of [...turns].reverse()) {
    const turn = recordValue(turnValue);
    const items = Array.isArray(turn?.items) ? turn.items : [];

    for (const itemValue of items) {
      const item = recordValue(itemValue);
      const kind = textValue(item?.type);
      const itemId = textValue(item?.id) ?? crypto.randomUUID();
      if (kind === "agentMessage") {
        const body = textValue(item?.text);
        const display = recordValue(item?.neoBabylonDisplay);
        if (body || display?.displayTruncated === true) restored.push({
          id: itemId,
          role: "assistant",
          text: body ?? "",
          ...(threadId ? { threadId } : {}),
          ...(threadId && textValue(turn?.id) ? { turnId: textValue(turn.id) } : {}),
          ...(display?.displayTruncated === true ? {
            displayTruncated: true,
            omittedCharacters: Number(display.omittedCharacters) || 0,
            sourceRetained: display.sourceRetained === true,
            upstreamTruncated: display.upstreamTruncated === true,
          } : {}),
        });
      } else if (kind === "userMessage") {
        const body = textValue(item?.text) ?? "";
        const contentNote = item?.hasNonTextContent === true
          ? "\n\n[Non-text attachment omitted from this preview.]"
          : "";
        if (body) restored.push({ id: itemId, role: "user", text: body + contentNote });
      } else if (kind === "reasoning") {
        const reasoning = upsertReasoningItem(restored, item, {
          threadId,
          turnId: textValue(turn?.id),
        });
        if (reasoning !== restored) restored.splice(0, restored.length, ...reasoning);
      }
    }

    const marker = createTurnStatusMessage(turn?.status,
      `${textValue(turn?.id) ?? crypto.randomUUID()}-status`);
    if (marker) restored.push(marker);
  }
  return restored;
}

export function restoreSavedActivities(outputs, threadId) {
  if (!Array.isArray(outputs)) return [];
  return outputs.flatMap((outputValue) => {
    const output = recordValue(outputValue);
    const id = textValue(output?.itemId);
    if (!output || !id) return [];
    const display = recordValue(output.neoBabylonDisplay);
    const status = classifyActivityOutcome(output.outcome, output.error);
    const argumentsText = toolArgumentsText(output.arguments);
    const errorText = textValue(output.error);
    return [{
      id,
      title: toolActivityTitle({
        title: textValue(output.title),
        command: textValue(output.command),
        toolName: textValue(output.toolName),
        tool: textValue(output.tool),
        itemType: textValue(output.itemType),
        argumentsValue: output.arguments,
      }),
      detail: typeof output.text === "string" ? output.text
        : typeof output.output === "string" ? output.output
          : typeof output.result === "string" ? output.result : undefined,
      ...(textValue(output.command) ? { command: textValue(output.command) } : {}),
      ...(argumentsText === undefined ? {} : { argumentsText }),
      ...(errorText ? { errorText } : {}),
      status,
      ...(Number.isSafeInteger(output.exitCode) ? { exitCode: output.exitCode } : {}),
      ...(Number.isFinite(output.durationMs) && output.durationMs >= 0 ? { durationMs: output.durationMs } : {}),
      itemType: textValue(output.itemType),
      ...(textValue(output.outputItemId) ? { outputItemId: textValue(output.outputItemId) } : {}),
      turnId: textValue(output.turnId),
      sourceRetained: output.sourceRetained === true || Boolean(textValue(output.outputItemId)),
      ...(threadId ? { threadId } : {}),
      ...(display?.displayTruncated === true || display?.upstreamTruncated === true ? {
        displayTruncated: display.displayTruncated === true,
        omittedCharacters: Number(display.omittedCharacters) || 0,
        sourceRetained: display.sourceRetained === true,
        upstreamTruncated: display.upstreamTruncated === true,
      } : {}),
    }];
  });
}

export function restoreTurnOutcome(turns) {
  if (!Array.isArray(turns)) {
    return {
      state: "unknown",
      warning: "The last saved turn has no confirmed final outcome. It was not replayed. Check its history before continuing.",
    };
  }
  if (turns.length === 0) return { state: "idle", warning: null };

  const status = recordValue(turns[0])?.status;
  if (status === "completed") return { state: "completed", warning: null };
  if (status === "interrupted") {
    return {
      state: "interrupted",
      warning: "The last saved turn was interrupted. It was not replayed when this task reopened.",
    };
  }
  if (status === "failed") {
    return {
      state: "failed",
      warning: "The last saved turn failed. It was not replayed when this task reopened.",
    };
  }
  return {
    state: "unknown",
    warning: "The last saved turn has no confirmed final outcome. It was not replayed. Check its history before continuing.",
  };
}
