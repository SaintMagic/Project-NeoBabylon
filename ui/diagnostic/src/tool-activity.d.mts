export type OutputRangePage = {
  threadId: string;
  turnId: string;
  itemId: string;
  itemType: string;
  offset: number;
  totalCharacters: number;
  text: string;
  nextOffset: number;
  hasMore: boolean;
  upstreamTruncated: boolean;
  omittedParts: boolean;
};

export function toolArgumentsText(value: unknown): string | undefined;
export function toolActivityTitle(value: {
  title?: string;
  command?: string;
  toolName?: string;
  tool?: string;
  itemType?: string;
  argumentsValue?: unknown;
}): string;
export function mergeOutputRange(previous: OutputRangePage | null, next: OutputRangePage): OutputRangePage;
export function projectOutputRangePage(response: unknown, location: { threadId: string; turnId: string; itemId: string; itemType: string }, requestedOffset: number): OutputRangePage;
export function mergeActivityHistoryEntry<T extends Record<string, unknown>>(previous: T, next: T): T;
