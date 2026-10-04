export type ActivityHistoryEntry = {
  id: string;
  title: string;
  detail?: string;
  exitCode?: number;
  status: "running" | "succeeded" | "failed" | "info";
  displayTruncated?: boolean;
  omittedCharacters?: number;
  sourceRetained?: boolean;
  upstreamTruncated?: boolean;
  itemType?: string;
  threadId?: string;
  turnId?: string;
  commandStopState?: "running" | "stopping" | "stopped" | "finished" | "notReportedActive" | "identityChanged" | "unknown";
  commandStopAvailable?: boolean;
  commandStopDetail?: string;
  commandStopAttribution?: "Codex App Server" | "NeoBabylon.Host";
};

export const MAX_ACTIVITY_ENTRIES: 64;
export const MAX_ACTIVITY_TEXT_CHARACTERS: 250000;
export function boundActivityHistory(current: ActivityHistoryEntry[]): {
  entries: ActivityHistoryEntry[];
  truncated: boolean;
};
