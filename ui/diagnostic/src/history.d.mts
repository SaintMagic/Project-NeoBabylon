export type SavedThreadSearchRecord = {
  name?: string | null;
  preview?: string | null;
  forkedFromId?: string | null;
  modelProvider?: string | null;
  model?: string | null;
};

export function filterSavedThreads<T extends SavedThreadSearchRecord>(threads: T[], query: string): T[];
export function savedThreadLabel(thread: SavedThreadSearchRecord): string;
export function mergeHistoryPages<T extends { id: string; updatedAt?: number | string | null }>(current: T[], incoming: T[]): T[];
export function savedThreadUpdatedAt(value: unknown): number;
export function savedThreadSearchEmptyMessage(query: string, hasMorePages: boolean): string | null;
export function savedHistoryState(input: { unavailable: boolean; query: string; resultCount: number }): "unavailable" | "search-empty" | "empty" | "loaded";
