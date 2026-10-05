export type ConversationLayout = "readable" | "wide";
export const CONVERSATION_LAYOUT_KEY: string;
export function readConversationLayout(storage?: Pick<Storage, "getItem">): ConversationLayout;
export function saveConversationLayout(mode: ConversationLayout, storage?: Pick<Storage, "setItem">): boolean;
