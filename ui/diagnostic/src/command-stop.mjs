const UNKNOWN = {
  state: "unknown",
  actionAvailable: true,
  attributedTo: "NeoBabylon.Host",
  detail: "NeoBabylon could not confirm whether the command stopped; it may still be running. You can retry this exact command stop.",
};

export function mayStopCommand(activity) {
  return activity?.itemType === "commandExecution"
    && activity.commandStopAvailable === true
    && ["running", "unknown"].includes(activity.commandStopState);
}

export function matchesLateCommandCompletion(event, binding) {
  const params = event?.params;
  const item = params?.item;
  return event?.stream === true
    && event?.attributedTo === "Codex App Server"
    && event?.method === "item/completed"
    && typeof binding?.requestId === "string"
    && event?.requestId === binding.requestId
    && params?.threadId === binding.threadId
    && params?.turnId === binding.turnId
    && item?.type === "commandExecution"
    && item?.id === binding.itemId;
}

export async function requestCommandStop(itemId, requestHost) {
  if (typeof itemId !== "string" || !itemId.trim() || typeof requestHost !== "function") {
    return UNKNOWN;
  }

  try {
    const result = await requestHost("stopCommand", { itemId });
    if (result?.eventType !== "commandExecutionStopResult") {
      return UNKNOWN;
    }

    if (result.attributedTo === "NeoBabylon.Host") {
      const failureMessage = typeof result.failure?.message === "string" ? result.failure.message : UNKNOWN.detail;
      if (result.status === "identity_mismatch") {
        return {
          state: "identityChanged",
          actionAvailable: false,
          attributedTo: "NeoBabylon.Host",
          detail: failureMessage,
        };
      }
      if (result.status === "not_found") {
        return {
          state: "unknown",
          actionAvailable: false,
          attributedTo: "NeoBabylon.Host",
          detail: failureMessage,
        };
      }
      if (result.status === "unknown") {
        return {
          state: "unknown",
          actionAvailable: result.commandStopAvailable === true,
          attributedTo: "NeoBabylon.Host",
          detail: failureMessage,
        };
      }
      return UNKNOWN;
    }

    if (result.attributedTo !== "Codex App Server") return UNKNOWN;

    switch (result.status) {
      case "stopped":
        return {
          state: "stopped",
          actionAvailable: false,
          attributedTo: "Codex App Server",
          detail: "Codex App Server confirmed that this exact command was stopped.",
        };
      case "already_exited":
        return {
          state: "finished",
          actionAvailable: false,
          attributedTo: "Codex App Server",
          detail: "Codex App Server confirmed that this exact command had already exited.",
        };
      case "not_found":
        return {
          state: "unknown",
          actionAvailable: false,
          attributedTo: "Codex App Server",
          detail: "Codex App Server no longer tracks this command. NeoBabylon cannot confirm whether it exited and will not target a different process.",
        };
      case "identity_mismatch":
        return {
          state: "identityChanged",
          actionAvailable: false,
          attributedTo: "Codex App Server",
          detail: "The command identity changed. NeoBabylon did not stop a different process.",
        };
      case "failed": {
        const actionAvailable = result.commandStopAvailable === true;
        return {
          ...UNKNOWN,
          actionAvailable,
          attributedTo: "Codex App Server",
          detail: actionAvailable
            ? UNKNOWN.detail
            : "Codex App Server could not stop the command, and NeoBabylon cannot verify a current retry binding. The command may still be running; no retry is available.",
        };
      }
      default:
        return UNKNOWN;
    }
  } catch (error) {
    const reason = error instanceof Error && error.message.trim()
      ? ` ${error.message.trim()}`
      : "";
    return {
      ...UNKNOWN,
      detail: `${UNKNOWN.detail}${reason}`,
    };
  }
}
