export type TurnReview = {
  threadId: string;
  turnId: string;
  source: "liveTurnDiff" | "savedFileChangeItems";
  status: "awaiting" | "available" | "invalidated" | "oversized" | "unavailable";
  diff: string | null;
};

export function reduceTurnReview(
  current: TurnReview | null,
  method: string,
  params: unknown,
  expectedThreadId: string | null | undefined,
  maxDiffCharacters?: number,
): TurnReview | null;

export function restoreSavedTurnReview(
  reviews: unknown,
  threadId: string,
  maxDiffCharacters?: number,
): TurnReview | null;
