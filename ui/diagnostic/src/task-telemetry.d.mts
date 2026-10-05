export type TelemetryIdentity = { workspace?: string | null; threadId: string | null; providerId: string | null; modelIdentifier: string | null };
export type TokenCounts = { totalTokens: number | null; inputTokens: number | null; cachedInputTokens: number | null; cacheWriteInputTokens: number | null; outputTokens: number | null; reasoningOutputTokens: number | null };
export type TokenUsage = { total: TokenCounts; last: TokenCounts; modelContextWindow: number | null };
export type RateSample = { atMs: number; rate: number | null; turnId?: string; requestId?: string; source?: string | null };
export type RateMeasurement = { turnId: string; modelIdentifier: string; providerId: string | null; outputTokens: number; durationMs: number; source: string; outputSource: string; startedAtUtc?: string | null; completedAtUtc?: string | null; responseId?: string | null; atMs?: number | null; requestId?: string | null };
type MeasuredTurn = { requestId: string; turnId: string | null; startedAtMs: number; baselineOutput: number | null; anchorOutput: number | null; anchorAtMs: number | null; latestOutput: number | null; observationCount: number; lastObservedAtMs: number | null };
export type TaskTelemetry = {
  identity: TelemetryIdentity; identityKey: string; epoch: number; revision: number;
  usage: TokenUsage | null; usageSource: string | null; observedAtUtc: string | null;
  currentUsage: TokenUsage | null; currentSource: string | null;
  fence: { observedAtUtc: string | null; signature: string | null; observedAfterUtcMs: number | null } | null;
  observedReasoning: string | null; reasoningSource: string | null; lastCorrelatedTurnId: string | null;
  providerCost: null; activeTurn: MeasuredTurn | null;
  pendingMeasurement: (MeasuredTurn & { endedAtMs: number; outcome: string }) | null;
  compaction: { requestId: string; turnId: string | null; usage: TokenUsage | null } | null;
  turnAverage: number | null; weightedAverage: number | null; measuredTurns: number;
  measuredOutput: number; measuredDurationMs: number; measurements: RateMeasurement[]; samples: RateSample[]; warning: string | null;
};
export type TelemetryAction =
  | { type: "invalidateContext"; observedAfterUtcMs?: number }
  | { type: "warning"; message: string }
  | { type: "beginCompaction"; requestId: string }
  | { type: "finishCompaction"; requestId: string; confirmed: boolean; turnId: string | null; observedAfterUtcMs?: number }
  | { type: "begin"; requestId: string; nowMs: number }
  | { type: "notification"; event: { requestId: string; method: string; params: unknown }; nowMs?: number }
  | { type: "complete"; requestId: string; threadId: string | null; turnId: string | null; outcome: string; nowMs: number }
  | { type: "abandon"; requestId: string; nowMs: number }
  | { type: "snapshot"; epoch: number; revision: number; result: unknown; finalForRequestId?: string };
export function telemetryIdentityKey(identity: TelemetryIdentity): string;
export function normalizeTurnMeasurementOutcome(result: unknown): string;
export function createTaskTelemetry(identity: TelemetryIdentity, options?: { epoch?: number; fenceFrom?: TaskTelemetry | null; observedAfterUtcMs?: number | null }): TaskTelemetry;
export function reduceTaskTelemetry(state: TaskTelemetry, action: TelemetryAction): TaskTelemetry;
export function rateHistory(samples: RateSample[], nowMs: number): RateSample[];
export function contextMetrics(state: TaskTelemetry, capability: unknown): { usedTokens: number | null; windowTokens: number | null; windowSource: string; windowEvidence: string | null; usageSource: string | null; percent: number | null };
export function createTelemetryFramePublisher<T>(options: { schedule: (callback: () => void) => number; cancel: (frame: number) => void; onValue: (value: T) => void }): { push: (value: T) => void; clear: () => void };
