import type { ChatMessage } from "./transcript.mjs";

export function upsertReasoningItem(
  current: ChatMessage[],
  item: unknown,
  identity: { threadId: string; turnId: string },
): ChatMessage[];

export function upsertReasoningDelta(
  current: ChatMessage[],
  method: string,
  params: unknown,
): ChatMessage[];
