export type ActivityHistoryEntry = {
  id: string;
  title: string;
  detail?: string;
  command?: string;
  argumentsText?: string;
  errorText?: string;
  exitCode?: number;
  durationMs?: number;
  status: "running" | "succeeded" | "failed" | "info";
  displayTruncated?: boolean;
  omittedCharacters?: number;
  sourceRetained?: boolean;
  upstreamTruncated?: boolean;
  itemType?: string;
  outputItemId?: string;
  threadId?: string;
  turnId?: string;
  commandStopState?: "running" | "stopping" | "stopped" | "finished" | "notReportedActive" | "identityChanged" | "unknown";
  commandStopAvailable?: boolean;
  commandStopDetail?: string;
  commandStopAttribution?: "Codex App Server" | "NeoBabylon.Host";
};

export function boundActivityHistory(current: ActivityHistoryEntry[]): {
  entries: ActivityHistoryEntry[];
  truncated: boolean;
};
