export type CommandStopState = "running" | "stopping" | "stopped" | "finished" | "notReportedActive" | "identityChanged" | "unknown";

export type CommandStopOutcome = {
  state: Exclude<CommandStopState, "stopping">;
  actionAvailable: boolean;
  attributedTo: "Codex App Server" | "NeoBabylon.Host";
  detail: string;
};

export type LateCommandNotificationBinding = {
  requestId: string;
  threadId: string;
  turnId: string;
  itemId: string;
};

export function matchesLateCommandCompletion(
  event: {
    requestId?: string;
    stream?: boolean;
    attributedTo?: string;
    method?: string;
    params?: unknown;
  } | null | undefined,
  binding: LateCommandNotificationBinding | null | undefined,
): boolean;

export function mayStopCommand(activity: {
  itemType?: string;
  commandStopState?: CommandStopState;
  commandStopAvailable?: boolean;
} | null | undefined): boolean;

export function requestCommandStop(
  itemId: string,
  requestHost: (operation: "stopCommand", payload: { itemId: string }) => Promise<Record<string, unknown>>,
): Promise<CommandStopOutcome>;
