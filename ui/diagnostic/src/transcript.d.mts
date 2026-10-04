export type ChatMessage = {
  id: string;
  role: "user" | "assistant" | "status";
  text: string;
  outcome?: "interrupted" | "failed" | "unconfirmed";
  streaming?: boolean;
  failed?: boolean;
  displayTruncated?: boolean;
  omittedCharacters?: number;
  sourceRetained?: boolean;
  upstreamTruncated?: boolean;
  transcriptWindowOmission?: {
    omittedEntries: number;
    omittedCharacters: number;
  };
  turnId?: string;
  threadId?: string;
};

export type DisplayMetadata = {
  displayTruncated?: boolean;
  omittedCharacters?: number;
  sourceRetained?: boolean;
  upstreamTruncated?: boolean;
};

export function projectDisplayText(value: string, limit: number, metadata?: DisplayMetadata): {
  text: string;
  displayTruncated?: boolean;
  omittedCharacters?: number;
  sourceRetained?: boolean;
  upstreamTruncated?: boolean;
};
export function appendAssistantDelta(current: ChatMessage[], id: string, delta: string, displayMetadata?: DisplayMetadata): ChatMessage[];
export function boundTranscript(current: ChatMessage[]): ChatMessage[];
export function finishAssistantStreams(current: ChatMessage[]): ChatMessage[];
export function markAssistantTurnFailed(current: ChatMessage[], userMessageId: string, turnId?: string, threadId?: string): ChatMessage[];
export function upsertAssistantMessage(
  current: ChatMessage[],
  text: string,
  streaming: boolean,
  failed?: boolean,
  id?: string,
  displayMetadata?: DisplayMetadata,
  turnId?: string,
  threadId?: string,
): ChatMessage[];
