import assert from "node:assert/strict";
import test from "node:test";
import { executionPolicyPresentation } from "../src/execution-policy.mjs";

test("unrestricted tools are visibly named as uncontained even before a thread starts", () => {
  const view = executionPolicyPresentation({
    requestedToolPolicy: "unrestricted",
    configSandboxMode: "danger-full-access",
    containedToolsQualified: false,
  });

  assert.equal(view.label, "Full access · no containment");
  assert.match(view.warning, /read, modify, or delete files outside the workspace/);
  assert.equal(view.verified, false);
});

test("an effective App Server mismatch never receives a full-access verified label", () => {
  const view = executionPolicyPresentation(
    { requestedToolPolicy: "unrestricted", configSandboxMode: "danger-full-access", containedToolsQualified: false },
    { effectiveSandboxType: "readOnly" },
  );
  assert.equal(view.verified, false);
  assert.match(view.label, /authority mismatch/i);
});

test("matching effective full-access authority is attributed without claiming containment", () => {
  const view = executionPolicyPresentation(
    { requestedToolPolicy: "unrestricted", configSandboxMode: "danger-full-access", containedToolsQualified: false },
    { effectiveSandboxType: "dangerFullAccess", containedToolsQualified: false },
  );
  assert.equal(view.verified, true);
  assert.equal(view.label, "Full access · no containment");
});

test("QA approval qualification is distinct from production unrestricted authority", () => {
  const view = executionPolicyPresentation(
    {
      requestedToolPolicy: "approval-qualification",
      configSandboxMode: "read-only",
      approvalPolicy: "on-request",
      containedToolsQualified: false,
    },
    {
      configSandboxMode: "read-only",
      configApprovalPolicy: "on-request",
      effectiveSandboxType: "readOnly",
      effectiveApprovalPolicy: "on-request",
      windowsSandboxMode: null,
      containedToolsQualified: false,
    },
  );

  assert.equal(view.label, "QA approval qualification · read-only/on-request");
  assert.equal(view.verified, true);
  assert.match(view.warning, /QA-only/i);
  assert.match(view.warning, /accepting a command runs that exact command/i);
  assert.match(view.warning, /accepting file changes applies the exact previewed patch to its displayed paths/i);
  assert.match(view.warning, /Windows account's permissions/i);
});

test("QA approval qualification fails closed on any effective policy mismatch", () => {
  const view = executionPolicyPresentation(
    {
      requestedToolPolicy: "approval-qualification",
      configSandboxMode: "read-only",
      approvalPolicy: "on-request",
      containedToolsQualified: false,
    },
    {
      configSandboxMode: "read-only",
      configApprovalPolicy: "on-request",
      effectiveApprovalPolicy: "never",
      effectiveSandboxType: "workspaceWrite",
    },
  );

  assert.equal(view.verified, false);
  assert.match(view.label, /authority mismatch/i);
});
