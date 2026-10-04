const HISTORY_MS = 90_000;
const MAX_SAMPLES = 90;
const field = (value, name) => value && typeof value === "object" && !Array.isArray(value)
  ? (Object.hasOwn(value, name) ? value[name] : value[`${name[0].toUpperCase()}${name.slice(1)}`]) : undefined;
const text = (value) => typeof value === "string" && value.trim() ? value : null;
const count = (value) => typeof value === "number" && Number.isSafeInteger(value) && value >= 0 ? value : null;
const windowCount = (value) => count(value) > 0 ? value : null;
const timestamp = (value) => typeof value === "string" && Number.isFinite(Date.parse(value)) ? Date.parse(value) : null;
const countNames = ["totalTokens", "inputTokens", "cachedInputTokens", "cacheWriteInputTokens", "outputTokens", "reasoningOutputTokens"];
const parseCounts = (value) => Object.fromEntries(countNames.map((name) => [name, count(field(value, name))]));

function parseUsage(value) {
  if (!value || typeof value !== "object" || Array.isArray(value)) return null;
  return { total: parseCounts(field(value, "total")), last: parseCounts(field(value, "last")), modelContextWindow: windowCount(field(value, "modelContextWindow")) };
}

const usageSignature = (usage) => usage ? JSON.stringify([usage.total, usage.last]) : null;
export const telemetryIdentityKey = (identity) => JSON.stringify([identity.workspace ?? null, identity.threadId ?? null, identity.providerId ?? null, identity.modelIdentifier ?? null]);

function contextFence(state, observedAfterUtcMs = null) {
  return { observedAtUtc: state.observedAtUtc, signature: usageSignature(state.usage), observedAfterUtcMs };
}

export function createTaskTelemetry(identity, { epoch = 0, fenceFrom = null, observedAfterUtcMs = null } = {}) {
  return {
    identity: { ...identity }, identityKey: telemetryIdentityKey(identity), epoch, revision: 0,
    // Thread-lifetime spend survives a same-chat model switch; current-model observations do not.
    usage: fenceFrom?.usage ?? null, usageSource: fenceFrom?.usageSource ?? null, observedAtUtc: fenceFrom?.observedAtUtc ?? null, currentUsage: null, currentSource: null,
    fence: fenceFrom ? contextFence(fenceFrom, observedAfterUtcMs) : null,
    observedReasoning: null, reasoningSource: null, lastCorrelatedTurnId: null,
    providerCost: null, activeTurn: null, pendingMeasurement: null, compaction: null,
    turnAverage: null, weightedAverage: null, measuredTurns: 0, measuredOutput: 0, measuredDurationMs: 0,
    samples: [], warning: null,
  };
}

export function rateHistory(samples, nowMs) {
  return samples.filter((sample) => Number.isFinite(sample.atMs) && sample.atMs >= nowMs - HISTORY_MS && sample.atMs <= nowMs).slice(-MAX_SAMPLES);
}

function isFresh(usage, observedAtUtc, fence) {
  if (!fence) return true;
  const newerTimestamp = timestamp(observedAtUtc);
  const oldTimestamp = timestamp(fence.observedAtUtc);
  if (newerTimestamp !== null && (oldTimestamp === null || newerTimestamp > oldTimestamp)
    && (fence.observedAfterUtcMs === null || newerTimestamp >= fence.observedAfterUtcMs)) return true;
  const signature = usageSignature(usage);
  return fence.signature !== null && signature !== null && signature !== fence.signature;
}

function isOlderUsage(current, next) {
  return current && next && ["totalTokens", "outputTokens"].some((name) =>
    current.total[name] !== null && next.total[name] !== null && next.total[name] < current.total[name]);
}

function finalizeMeasurement(state, usage, requestId, source, currentEvidence) {
  const measured = state.pendingMeasurement;
  if (!measured || measured.requestId !== requestId) return state;
  const durationMs = measured.endedAtMs - measured.startedAtMs;
  const finalOutput = usage?.total.outputTokens;
  const output = finalOutput !== null && finalOutput !== undefined && measured.baselineOutput !== null ? finalOutput - measured.baselineOutput : null;
  const valid = currentEvidence && measured.outcome === "completed" && Number.isFinite(durationMs) && durationMs > 0
    && output !== null && output >= 0;
  const rate = valid ? output * 1000 / durationMs : null;
  const measuredOutput = state.measuredOutput + (valid ? output : 0);
  const measuredDurationMs = state.measuredDurationMs + (valid ? durationMs : 0);
  return { ...state, pendingMeasurement: null, turnAverage: rate,
    measuredOutput, measuredDurationMs, measuredTurns: state.measuredTurns + (valid ? 1 : 0),
    weightedAverage: measuredDurationMs > 0 ? measuredOutput * 1000 / measuredDurationMs : null,
    samples: state.samples.map((sample) => sample.requestId === requestId ? { ...sample, rate, source } : sample),
  };
}

export function reduceTaskTelemetry(state, action) {
  if (action.type === "invalidateContext") return { ...state, revision: state.revision + 1,
    currentUsage: null, currentSource: null, fence: contextFence(state, action.observedAfterUtcMs ?? null) };
  if (action.type === "warning") return { ...state, warning: action.message };
  if (action.type === "beginCompaction") {
    if (!state.identity.threadId || state.activeTurn || typeof action.requestId !== "string") return state;
    return { ...state, revision: state.revision + 1, compaction: { requestId: action.requestId, turnId: null, usage: null } };
  }
  if (action.type === "finishCompaction") {
    const compact = state.compaction;
    if (!compact || action.requestId !== compact.requestId) return state;
    const invalidated = reduceTaskTelemetry(state, { type: "invalidateContext", observedAfterUtcMs: action.observedAfterUtcMs });
    const freshLive = action.confirmed && compact.usage && (action.turnId === null || action.turnId === compact.turnId);
    return { ...invalidated, compaction: null, ...(freshLive ? { currentUsage: compact.usage, currentSource: state.currentSource, fence: null } : {}) };
  }
  if (action.type === "begin") {
    if (!state.identity.threadId || typeof action.requestId !== "string") return state;
    return { ...state, revision: state.revision + 1, turnAverage: null, pendingMeasurement: null,
      activeTurn: { requestId: action.requestId, turnId: null, startedAtMs: action.nowMs, baselineOutput: state.usage?.total.outputTokens ?? null } };
  }
  if (action.type === "notification") {
    const event = action.event;
    const active = state.activeTurn ?? state.compaction;
    const activeKey = state.activeTurn ? "activeTurn" : "compaction";
    const params = field(event, "params");
    if (!active || field(event, "requestId") !== active.requestId || field(params, "threadId") !== state.identity.threadId) return state;
    if (field(event, "method") === "turn/started") {
      const turnId = text(field(field(params, "turn"), "id")) ?? text(field(params, "turnId"));
      if (!turnId || (active.turnId !== null && active.turnId !== turnId)) return state;
      return { ...state, [activeKey]: { ...active, turnId } };
    }
    if (field(event, "method") !== "thread/tokenUsage/updated" || !active.turnId || field(params, "turnId") !== active.turnId) return state;
    const usage = parseUsage(field(params, "tokenUsage"));
    if (!usage || isOlderUsage(state.usage, usage)) return state;
    const source = "Codex thread/tokenUsage/updated · captured request/provider/model/thread/turn";
    return { ...state, revision: state.revision + 1, usage, usageSource: source, currentUsage: usage, currentSource: source,
      lastCorrelatedTurnId: active.turnId, fence: null, warning: null,
      ...(activeKey === "compaction" ? { compaction: { ...active, usage } } : {}) };
  }
  if (action.type === "complete") {
    const active = state.activeTurn;
    if (!active || action.requestId !== active.requestId || action.threadId !== state.identity.threadId
      || !active.turnId || action.turnId !== active.turnId) return state;
    return { ...state, revision: state.revision + 1, activeTurn: null,
      pendingMeasurement: { ...active, endedAtMs: action.nowMs, outcome: action.outcome },
      samples: [...rateHistory(state.samples, action.nowMs), { atMs: action.nowMs, rate: null, requestId: action.requestId, source: "Awaiting final attributable Codex usage" }].slice(-MAX_SAMPLES) };
  }
  if (action.type === "abandon") {
    if (state.activeTurn?.requestId !== action.requestId) return state;
    return { ...state, revision: state.revision + 1, activeTurn: null, pendingMeasurement: null, turnAverage: null,
      samples: [...rateHistory(state.samples, action.nowMs), { atMs: action.nowMs, rate: null, requestId: action.requestId, source: "Turn completion/usage unconfirmed" }].slice(-MAX_SAMPLES) };
  }
  if (action.type !== "snapshot" || action.epoch !== state.epoch || action.revision !== state.revision
    || field(action.result, "attributedTo") !== "NeoBabylon.Host" || field(action.result, "threadId") !== state.identity.threadId) return state;
  const result = action.result;
  const usage = parseUsage(field(result, "tokenUsage"));
  if (isOlderUsage(state.usage, usage)) return state;
  const source = text(field(result, "evidenceSource"));
  const observedAtUtc = text(field(result, "observedAtUtc"));
  const provider = text(field(result, "usageModelProvider"));
  const model = text(field(result, "usageModelIdentifier"));
  const turnId = text(field(result, "usageTurnId"));
  const tupleMatches = provider === state.identity.providerId && model === state.identity.modelIdentifier;
  const correlated = turnId !== null && (turnId === state.lastCorrelatedTurnId || turnId === state.activeTurn?.turnId
    || (action.finalForRequestId === state.pendingMeasurement?.requestId && turnId === state.pendingMeasurement?.turnId));
  const noncontradictoryTuple = (provider === null || provider === state.identity.providerId) && (model === null || model === state.identity.modelIdentifier);
  // A journal may omit provider metadata. Only an exact captured turn can supply that missing provenance.
  const matchingEvidence = noncontradictoryTuple && (tupleMatches || correlated);
  const fresh = isFresh(usage, observedAtUtc, state.fence);
  const currentEvidence = matchingEvidence && source !== null && fresh;
  const keepLiveEvidence = provider === null && model === null && state.currentUsage
    && usageSignature(usage) === usageSignature(state.currentUsage) && !state.fence;
  let next = { ...state,
    usage: source && usage && !isOlderUsage(state.usage, usage) ? usage : state.usage,
    usageSource: source && usage ? source : state.usageSource,
    observedAtUtc: source && usage ? observedAtUtc : state.observedAtUtc,
    currentUsage: currentEvidence && usage ? usage : keepLiveEvidence ? state.currentUsage : null,
    currentSource: currentEvidence && usage ? source : keepLiveEvidence ? state.currentSource : null,
    fence: currentEvidence && usage ? null : state.fence,
    observedReasoning: currentEvidence && text(field(result, "reasoningEvidenceSource")) ? text(field(result, "reasoningEffort")) : null,
    reasoningSource: currentEvidence ? text(field(result, "reasoningEvidenceSource")) : null,
    warning: null,
  };
  const measuredTurn = state.pendingMeasurement?.turnId;
  const finalTurnMatches = measuredTurn && (turnId === measuredTurn || (turnId === null && state.lastCorrelatedTurnId === measuredTurn
    && usageSignature(usage) === usageSignature(state.currentUsage)));
  if (action.finalForRequestId) next = finalizeMeasurement(next, usage, action.finalForRequestId, source, currentEvidence && Boolean(finalTurnMatches));
  return next;
}

export function contextMetrics(state, capability) {
  const usedTokens = state.currentUsage?.last.totalTokens ?? null;
  let windowTokens = state.currentUsage?.modelContextWindow ?? null;
  let windowSource = windowTokens !== null ? "Codex reported model window" : "Unknown window";
  let windowEvidence = windowTokens !== null ? state.currentSource : null;
  if (windowTokens === null) {
    for (const [key, label] of [["contextWindowEffective", "Capability record · effective window"], ["contextWindowAdvertised", "Capability record · advertised window (effective not established)"]]) {
      const observation = field(capability, key);
      const value = windowCount(field(observation, "value"));
      if (field(observation, "state") === "Known" && value !== null) {
        windowTokens = value; windowSource = label; windowEvidence = text(field(observation, "evidenceSource")); break;
      }
    }
  }
  return { usedTokens, windowTokens, windowSource, windowEvidence, usageSource: state.currentSource,
    percent: usedTokens !== null && windowTokens !== null ? usedTokens * 100 / windowTokens : null };
}

export function createTelemetryFramePublisher({ schedule, cancel, onValue }) {
  let frame = null;
  let latest;
  return {
    push(value) {
      latest = value;
      if (frame === null) frame = schedule(() => { frame = null; onValue(latest); });
    },
    clear() { if (frame !== null) cancel(frame); frame = null; latest = undefined; },
  };
}
