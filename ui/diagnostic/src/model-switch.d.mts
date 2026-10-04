export type ConfirmedCapabilitySwitch = {
  status: "selected";
  result: Record<string, unknown>;
  capabilityRecord: Record<string, unknown>;
  activeThreadId: string | null;
  preservedThreadId: string | null;
};

export type RefreshedCapabilitySwitch = Omit<ConfirmedCapabilitySwitch, "status"> & {
  status: "selected";
  runtimeResult: Record<string, unknown>;
  diagnosticsResult: Record<string, unknown>;
};
export type CapabilitySwitchDiagnosticsWarning = Omit<ConfirmedCapabilitySwitch, "status"> & {
  status: "diagnostics-warning";
  warning: string;
};

export class CapabilitySelectionUnconfirmedError extends Error {}

export function canSelectCapability(state: { busy: boolean }): boolean;
export function sameCapabilityRecord(left: unknown, right: unknown): boolean;
export function confirmedResumeCapability(result: unknown, expected: { threadId: string; providerId: string | null; modelIdentifier: string | null }): Record<string, unknown> | null;
export function selectCapabilityForNextTurn(
  requestHost: (operation: "selectCapability" | "getRuntimeStatus" | "getDiagnostics", payload?: Record<string, unknown>) => Promise<Record<string, unknown>>,
  state: { providerId: string; modelIdentifier: string; currentThreadId: string | null; busy: boolean; capabilityRecord?: Record<string, unknown> },
  onConfirmed: (transition: ConfirmedCapabilitySwitch) => void,
): Promise<RefreshedCapabilitySwitch | CapabilitySwitchDiagnosticsWarning | { status: "blocked"; activeThreadId: string | null; preservedThreadId: string | null }>;
