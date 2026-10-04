export type CompactContextState = { threadId: string | null; busy: boolean; executionEligible: boolean };
export type CompactContextOutcome = { status: "blocked" | "completed" | "failed" | "unknown"; message: string; attributedTo: string; threadId?: string; turnId?: string | null };
export function canCompactContext(state: CompactContextState): boolean;
export function requestContextCompaction(requestHost: (operation: "compactContext", payload: { threadId: string }) => Promise<Record<string, unknown>>, state: CompactContextState): Promise<CompactContextOutcome>;
