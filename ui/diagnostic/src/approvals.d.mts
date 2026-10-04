export type ApprovalChoice = {
  decision: string;
  label: string;
  tone: "primary" | "secondary" | "danger";
};

export type ApprovalReviewPreview = {
  threadId: string;
  turnId: string;
  itemId: string;
  fingerprint: string;
  changes: Array<{
    path: string;
    kind: "add" | "delete" | "update";
    diff: string;
    movePath?: string;
  }>;
};

export type ApprovalPresentation = {
  supported: boolean;
  title: string;
  details: Record<string, unknown>;
  permissionProfile?: Record<string, unknown> | null;
  reviewPreview?: ApprovalReviewPreview | null;
  warning?: string;
  choices: ApprovalChoice[];
};

export function approvalPresentation(request: {
  requestId?: string | number;
  method: string;
  params?: Record<string, unknown>;
  reviewPreview?: unknown;
  reviewInvalidated?: boolean;
}): ApprovalPresentation;

export function invalidateApprovalReview<T extends {
  requestId: number;
  approvalInstanceId: string;
  method: string;
  params: Record<string, unknown>;
  reviewPreview?: unknown;
  reviewInvalidated?: boolean;
}>(request: T, event: Record<string, unknown>): T;
