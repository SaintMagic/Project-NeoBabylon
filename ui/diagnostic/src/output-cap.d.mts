export type CompletionLimit = {
  state: "Known" | "Unknown" | "Invalid";
  defaultTokens: number | null;
  maximumTokens: number | null;
};

export function completionLimit(observation: { state?: unknown; value?: unknown } | null | undefined): CompletionLimit;
export function parseOutputOverride(input: string, limit: CompletionLimit): { value: number | null; error: string | null };
