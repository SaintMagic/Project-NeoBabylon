export type ReasoningSelection = { key: string; effort: string | null };
export type ReasoningOptions = { key: string; known: boolean; controlType: "enabled" | "effort" | null; canOverride: boolean; defaultEnabled: boolean | null; efforts: string[]; defaultEffort: string | null; evidenceSource: string | null; reason: string };
export function reasoningOptions(capability: unknown): ReasoningOptions;
export function initialReasoningSelection(capability: unknown): ReasoningSelection;
export function chooseReasoningEffort(capability: unknown, effort: unknown): ReasoningSelection;
export function parseReasoningSelection(capability: unknown, selection: ReasoningSelection | null): { effort: string | null; error: string | null; wire: { reasoningEffort?: string } };
