export function executionPolicyPresentation(policy, authority = null) {
  const warning = "Model-controlled commands run with your Windows account's permissions and may read, modify, or delete files outside the workspace or access the network. Windows containment is not qualified.";
  if (policy?.requestedToolPolicy === "approval-qualification") {
    const qaWarning = "QA-only approval qualification: read-only is the baseline. Accepting a command runs that exact command; accepting file changes applies the exact previewed patch to its displayed paths. Both use your Windows account's permissions because Windows containment is not qualified. No session policy is added; this is not the production tool policy.";
    if (policy.configSandboxMode !== "read-only" || policy.approvalPolicy !== "on-request") {
      return { label: "QA approval policy unavailable", warning: qaWarning, verified: false };
    }

    const effectiveMatches = !authority
      || (authority.configSandboxMode === "read-only"
        && authority.configApprovalPolicy === "on-request"
        && authority.effectiveSandboxType === "readOnly"
        && authority.effectiveApprovalPolicy === "on-request"
        && authority.windowsSandboxMode == null);
    if (!effectiveMatches) {
      return { label: "Authority mismatch · QA approvals blocked", warning: qaWarning, verified: false };
    }

    return {
      label: "QA approval qualification · read-only/on-request",
      warning: qaWarning,
      verified: authority?.effectiveSandboxType === "readOnly",
    };
  }

  if (policy?.requestedToolPolicy !== "unrestricted" || policy?.configSandboxMode !== "danger-full-access") {
    return { label: "Tool policy unavailable", warning, verified: false };
  }

  const effective = authority?.effectiveSandboxType;
  if (effective && effective !== "dangerFullAccess") {
    return { label: "Authority mismatch · tools blocked", warning, verified: false };
  }

  return {
    label: "Full access · no containment",
    warning,
    verified: effective === "dangerFullAccess" && authority?.containedToolsQualified === false,
  };
}
