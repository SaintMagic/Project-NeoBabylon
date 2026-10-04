import type { ChatMessage } from "./transcript.mjs";

export function classifyActivityOutcome(status: unknown, error?: unknown): "succeeded" | "failed" | "info";
export function restoreVisibleTranscript(turns: unknown, threadId?: string): ChatMessage[];
export function restoreSavedActivities(outputs: unknown, threadId?: string): {
  id: string;
  title: string;
  detail?: string;
  status: "succeeded" | "failed" | "info";
  itemType?: string;
  turnId?: string;
  threadId?: string;
  displayTruncated?: boolean;
  omittedCharacters?: number;
  sourceRetained?: boolean;
  upstreamTruncated?: boolean;
}[];
export function createTurnStatusMessage(status: unknown, id: string, attribution?: string): ChatMessage | null;
export function createLiveTurnStatusMessage(result: unknown, id: string): ChatMessage | null;
export function resolveLiveTurnState(result: unknown): "completed" | "interrupted" | "failed" | "unknown";
export function restoreTurnOutcome(turns: unknown): {
  state: "idle" | "completed" | "interrupted" | "failed" | "unknown";
  warning: string | null;
};
