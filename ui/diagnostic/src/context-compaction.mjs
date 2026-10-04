const field = (value, name) => value && typeof value === "object" && !Array.isArray(value)
  ? (Object.hasOwn(value, name) ? value[name] : value[`${name[0].toUpperCase()}${name.slice(1)}`]) : undefined;

export function canCompactContext({ threadId, busy, executionEligible }) {
  return typeof threadId === "string" && threadId.length > 0 && busy !== true && executionEligible === true;
}

export async function requestContextCompaction(requestHost, state) {
  if (!canCompactContext(state)) return { status: "blocked", message: "Open an idle, executable chat before compacting context.", attributedTo: "NeoBabylon.UI" };
  const result = await requestHost("compactContext", { threadId: state.threadId });
  const outcome = field(result, "outcome");
  const succeeded = field(result, "succeeded");
  if (field(result, "eventType") !== "compactionOutcome" || field(result, "attributedTo") !== "NeoBabylon.Host"
    || field(result, "threadId") !== state.threadId
    || !((outcome === "completed" && succeeded === true) || (outcome === "failed" && succeeded === false) || (outcome === "unknown" && succeeded === null))) {
    throw new Error("NeoBabylon.UI: The host did not confirm a final compaction outcome for this exact chat. Completion is Unknown.");
  }
  const failure = field(result, "failure");
  const failureDetail = [field(failure, "attributedTo"), field(failure, "type"), field(failure, "message")]
    .filter((value) => typeof value === "string" && value.length > 0).join(" · ");
  return {
    status: outcome, threadId: state.threadId, turnId: field(result, "turnId") ?? null, attributedTo: "NeoBabylon.Host",
    message: outcome === "completed" ? "NeoBabylon.Host: Codex confirmed context compaction completed. Chat history and draft were preserved."
      : outcome === "failed" ? `NeoBabylon.Host: Context compaction failed${failureDetail ? ` · ${failureDetail}` : "; no failure detail was reported"}.`
        : "NeoBabylon.Host: Context compaction outcome is Unknown; completion was not confirmed.",
  };
}
