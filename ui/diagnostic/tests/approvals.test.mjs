import assert from "node:assert/strict";
import test from "node:test";
import { approvalPresentation, invalidateApprovalReview } from "../src/approvals.mjs";

test("command approval offers only a one-shot accept that App Server advertised", () => {
  const presentation = approvalPresentation({
    requestId: 41,
    method: "item/commandExecution/requestApproval",
    params: {
      command: "cmd.exe /d /c ver",
      cwd: "D:\\workspace",
      availableDecisions: ["accept", "acceptForSession", "decline", "cancel"],
    },
  });

  assert.equal(presentation.title, "Command approval");
  assert.equal(presentation.details.command, "cmd.exe /d /c ver");
  assert.deepEqual(presentation.choices.map(({ decision }) => decision), ["accept", "decline", "cancel"]);
  assert.equal(presentation.choices.some(({ decision }) => decision === "acceptForSession"), false);
});

test("command approval does not invent an accept choice when App Server omitted it", () => {
  const presentation = approvalPresentation({
    requestId: 42,
    method: "item/commandExecution/requestApproval",
    params: { availableDecisions: ["acceptForSession", "decline", "cancel"] },
  });

  assert.deepEqual(presentation.choices.map(({ decision }) => decision), ["decline", "cancel"]);
});

test("command approval remains non-approvable when App Server omits optional decision metadata", () => {
  const presentation = approvalPresentation({
    requestId: 48,
    method: "item/commandExecution/requestApproval",
    params: { command: "cmd.exe /d /c ver", cwd: "D:\\workspace" },
  });

  assert.deepEqual(presentation.choices.map(({ decision }) => decision), ["decline", "cancel"]);
});

test("command approval always keeps a visible fail-closed deny choice", () => {
  const presentation = approvalPresentation({
    requestId: 47,
    method: "item/commandExecution/requestApproval",
    params: { command: "ver", availableDecisions: ["acceptForSession"] },
  });

  assert.deepEqual(presentation.choices.map(({ decision }) => decision), ["decline"]);
});

test("file-change request for a persistent grant root is not approved by the ordinary one-shot card", () => {
  const presentation = approvalPresentation({
    requestId: 43,
    method: "item/fileChange/requestApproval",
    params: { reason: "Needs a broader write root", grantRoot: "D:\\outside" },
  });

  assert.equal(presentation.warning, "This request asks to expand a persistent write root.");
  assert.deepEqual(presentation.choices.map(({ decision }) => decision), ["decline", "cancel"]);
});

test("file-change request without a diff preview cannot be approved", () => {
  const presentation = approvalPresentation({
    requestId: 49,
    method: "item/fileChange/requestApproval",
    params: { reason: "Write proposed changes" },
  });

  assert.equal(presentation.warning, "The App Server request does not include a diff to review; approval is withheld.");
  assert.deepEqual(presentation.choices.map(({ decision }) => decision), ["decline", "cancel"]);
});

test("file-change approval exposes one-shot accept only for an exact correlated review preview", () => {
  const reviewPreview = {
    threadId: "thread-1",
    turnId: "turn-1",
    itemId: "file-1",
    fingerprint: "a".repeat(64),
    changes: [{ path: "D:\\workspace\\approved.txt", kind: "add", diff: "+approved\n" }],
  };
  const presentation = approvalPresentation({
    requestId: 50,
    method: "item/fileChange/requestApproval",
    params: { threadId: "thread-1", turnId: "turn-1", itemId: "file-1", reason: "Write proposed changes" },
    reviewPreview,
  });

  assert.deepEqual(presentation.choices.map(({ decision }) => decision), ["accept", "decline", "cancel"]);
  assert.deepEqual(presentation.reviewPreview, reviewPreview);
  assert.equal(presentation.warning, undefined);
});

test("file-change approval becomes deny-only when its exact preview is invalidated", () => {
  const reviewPreview = {
    threadId: "thread-1",
    turnId: "turn-1",
    itemId: "file-1",
    fingerprint: "d".repeat(64),
    changes: [{ path: "D:\\workspace\\approved.txt", kind: "add", diff: "+approved\n" }],
  };
  const presentation = approvalPresentation({
    requestId: 53,
    method: "item/fileChange/requestApproval",
    params: { threadId: "thread-1", turnId: "turn-1", itemId: "file-1" },
    reviewPreview,
    reviewInvalidated: true,
  });

  assert.deepEqual(presentation.choices.map(({ decision }) => decision), ["decline", "cancel"]);
  assert.equal(presentation.reviewPreview, null);
  assert.equal(presentation.warning, "The proposed changes changed after preview; approval is withheld. Deny this request.");
});

test("review invalidation only affects the exact pending approval instance and preview fingerprint", () => {
  const request = {
    requestId: 53,
    approvalInstanceId: "instance-current",
    method: "item/fileChange/requestApproval",
    params: { threadId: "thread-1", turnId: "turn-1", itemId: "file-1" },
    reviewPreview: {
      threadId: "thread-1", turnId: "turn-1", itemId: "file-1", fingerprint: "d".repeat(64),
      changes: [{ path: "D:\\workspace\\approved.txt", kind: "add", diff: "+approved\n" }],
    },
  };
  const event = {
    requestId: 53,
    approvalInstanceId: "instance-current",
    threadId: "thread-1",
    turnId: "turn-1",
    itemId: "file-1",
    previewFingerprint: "d".repeat(64),
  };

  const invalidated = invalidateApprovalReview(request, event);
  assert.equal(invalidated.reviewInvalidated, true);
  assert.equal(invalidated.reviewPreview, undefined);
  assert.equal(invalidateApprovalReview(request, { ...event, approvalInstanceId: "stale-instance" }), request);
  assert.equal(invalidateApprovalReview(request, { ...event, previewFingerprint: "e".repeat(64) }), request);
  assert.equal(invalidateApprovalReview(request, { ...event, itemId: "different-item" }), request);
});

test("file-change approval remains deny-only when a preview identity differs from its request", () => {
  const presentation = approvalPresentation({
    requestId: 51,
    method: "item/fileChange/requestApproval",
    params: { threadId: "thread-1", turnId: "turn-1", itemId: "file-1" },
    reviewPreview: {
      threadId: "thread-1", turnId: "turn-2", itemId: "file-1", fingerprint: "b".repeat(64),
      changes: [{ path: "D:\\workspace\\other.txt", kind: "update", diff: "-old\n+new\n" }],
    },
  });

  assert.deepEqual(presentation.choices.map(({ decision }) => decision), ["decline", "cancel"]);
  assert.equal(presentation.warning, "The proposed changes could not be bound to this exact App Server request; approval is withheld.");
});

test("file-change approval remains deny-only for an oversized review preview", () => {
  const presentation = approvalPresentation({
    requestId: 52,
    method: "item/fileChange/requestApproval",
    params: { threadId: "thread-1", turnId: "turn-1", itemId: "file-1" },
    reviewPreview: {
      threadId: "thread-1", turnId: "turn-1", itemId: "file-1", fingerprint: "c".repeat(64),
      changes: [{ path: "D:\\workspace\\large.txt", kind: "add", diff: "+".repeat(32_769) }],
    },
  });

  assert.deepEqual(presentation.choices.map(({ decision }) => decision), ["decline", "cancel"]);
  assert.equal(presentation.warning, "The proposed changes could not be bound to this exact App Server request; approval is withheld.");
});

test("permission request can grant only the exact displayed profile for this turn", () => {
  const permissions = { network: { enabled: true }, fileSystem: { write: ["D:\\workspace\\out.txt"] } };
  const presentation = approvalPresentation({
    requestId: 44,
    method: "item/permissions/requestApproval",
    params: { cwd: "D:\\workspace", reason: "Write the requested output", permissions },
  });

  assert.equal(presentation.title, "Additional permissions");
  assert.deepEqual(presentation.permissionProfile, permissions);
  assert.deepEqual(presentation.choices.map(({ decision }) => decision), ["grantRequestedForTurn", "deny"]);
});

test("unknown permission fields remain non-approvable and unknown request methods are not rendered as approvals", () => {
  const unsupportedPermissions = approvalPresentation({
    requestId: 45,
    method: "item/permissions/requestApproval",
    params: { permissions: { futureCapability: { enabled: true } } },
  });
  const unknownMethod = approvalPresentation({
    requestId: 46,
    method: "mcpServer/elicitation/request",
    params: { prompt: "secret" },
  });

  assert.deepEqual(unsupportedPermissions.choices.map(({ decision }) => decision), ["deny"]);
  assert.equal(unsupportedPermissions.warning, "This request contains permission fields NeoBabylon does not recognize.");
  assert.equal(unknownMethod.supported, false);
  assert.deepEqual(unknownMethod.choices, []);
});
