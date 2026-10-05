export function assistantDeltaText(value: unknown): string | undefined;

export type AssistantDisplayMetadata = {
  displayTruncated?: boolean;
  omittedCharacters?: number;
  sourceRetained?: boolean;
  upstreamTruncated?: boolean;
};
export type AssistantDeltaBatch = { id: string; delta: string; displayMetadata?: AssistantDisplayMetadata }[];
export type AssistantDeltaBatcher = {
  push(id: string, delta: string, displayMetadata?: AssistantDisplayMetadata): void;
  discard(id: string): void;
  flush(): void;
  clear(): void;
};

export function createAssistantDeltaBatcher(options: {
  schedule: (callback: () => void, delayMs: number) => number;
  cancel: (timer: number) => void;
  onBatch: (batch: AssistantDeltaBatch) => void;
  delayMs?: number;
}): AssistantDeltaBatcher;
