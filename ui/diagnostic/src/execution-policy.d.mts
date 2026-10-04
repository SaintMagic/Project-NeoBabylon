export type ExecutionPolicyPresentation = {
  label: string;
  warning: string;
  verified: boolean;
};

export function executionPolicyPresentation(
  policy: Record<string, unknown> | null | undefined,
  authority?: Record<string, unknown> | null,
): ExecutionPolicyPresentation;
