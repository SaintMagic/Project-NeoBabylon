const knownPermissionKeys = new Set(["network", "fileSystem"]);
const fileSystemPermissionKeys = new Set(["read", "write", "globScanMaxDepth", "entries"]);
const maximumFileChangeReviewCharacters = 32_768;

const isRecord = (value) => value !== null && typeof value === "object" && !Array.isArray(value);
const hasOnlyKeys = (value, allowed) => Object.keys(value).every((key) => allowed.has(key));

function isPath(value) {
  if (!isRecord(value) || !hasOnlyKeys(value, new Set(["type", "path"])) || value.type !== "path") return false;
  return typeof value.path === "string" && value.path.length > 0;
}

function isGlobPattern(value) {
  if (!isRecord(value) || !hasOnlyKeys(value, new Set(["type", "pattern"])) || value.type !== "globPattern") return false;
  return typeof value.pattern === "string" && value.pattern.length > 0;
}

function isSpecialPath(value) {
  if (!isRecord(value) || !hasOnlyKeys(value, new Set(["type", "value"])) || value.type !== "special") return false;
  const special = value.value;
  if (!isRecord(special) || typeof special.kind !== "string") return false;
  if (["root", "minimal", "tmpdir", "slash_tmp"].includes(special.kind)) {
    return hasOnlyKeys(special, new Set(["kind"]));
  }
  if (special.kind === "project_roots") {
    return hasOnlyKeys(special, new Set(["kind", "subpath"]))
      && (special.subpath === undefined || special.subpath === null || typeof special.subpath === "string");
  }
  return false;
}

function isFileSystemEntry(value) {
  if (!isRecord(value) || !hasOnlyKeys(value, new Set(["path", "access"]))) return false;
  const path = value.path;
  const knownPath = isPath(path) || isGlobPattern(path) || isSpecialPath(path);
  return knownPath && ["read", "write", "deny"].includes(value.access);
}

function isFileSystemProfile(value) {
  if (!isRecord(value) || !hasOnlyKeys(value, fileSystemPermissionKeys)) return false;
  for (const key of ["read", "write"]) {
    if (value[key] !== undefined && value[key] !== null
      && (!Array.isArray(value[key]) || value[key].some((path) => typeof path !== "string"))) return false;
  }
  if (value.globScanMaxDepth !== undefined && value.globScanMaxDepth !== null
    && (!Number.isInteger(value.globScanMaxDepth) || value.globScanMaxDepth < 1)) return false;
  if (value.entries !== undefined && value.entries !== null
    && (!Array.isArray(value.entries) || value.entries.some((entry) => !isFileSystemEntry(entry)))) return false;
  return true;
}

function isKnownPermissionProfile(value) {
  if (!isRecord(value) || !hasOnlyKeys(value, knownPermissionKeys)) return false;
  if (!Object.keys(value).length) return false;
  if (value.network !== undefined && value.network !== null) {
    if (!isRecord(value.network) || !hasOnlyKeys(value.network, new Set(["enabled"]))) return false;
    if (value.network.enabled !== undefined && value.network.enabled !== null && typeof value.network.enabled !== "boolean") return false;
  }
  if (value.fileSystem !== undefined && value.fileSystem !== null && !isFileSystemProfile(value.fileSystem)) return false;
  return Object.values(value).some((profile) => isRecord(profile) && Object.keys(profile).length > 0);
}

function projectFileChangeReviewPreview(request, params) {
  const preview = request.reviewPreview;
  if (!isRecord(preview)) return null;
  if (preview.threadId !== params.threadId
    || preview.turnId !== params.turnId
    || preview.itemId !== params.itemId
    || typeof preview.fingerprint !== "string"
    || !/^[a-f0-9]{64}$/i.test(preview.fingerprint)
    || !Array.isArray(preview.changes)
    || preview.changes.length === 0) return null;

  const changes = [];
  for (const change of preview.changes) {
    if (!isRecord(change)
      || typeof change.path !== "string" || change.path.trim().length === 0
      || !["add", "delete", "update"].includes(change.kind)
      || typeof change.diff !== "string" || change.diff.length === 0
      || change.movePath !== undefined && change.movePath !== null && typeof change.movePath !== "string") return null;
    changes.push({
      path: change.path,
      kind: change.kind,
      diff: change.diff,
      ...(typeof change.movePath === "string" ? { movePath: change.movePath } : {}),
    });
  }

  const projected = { ...preview, changes };
  if (JSON.stringify(projected).length > maximumFileChangeReviewCharacters) return null;
  return projected;
}

export function invalidateApprovalReview(request, event) {
  if (!isRecord(request)
    || !isRecord(event)
    || request.method !== "item/fileChange/requestApproval"
    || !Number.isSafeInteger(request.requestId)
    || request.requestId !== event.requestId
    || typeof request.approvalInstanceId !== "string"
    || request.approvalInstanceId !== event.approvalInstanceId
    || !isRecord(request.params)
    || !isRecord(request.reviewPreview)) return request;

  const params = request.params;
  const preview = request.reviewPreview;
  const exactIdentity = params.threadId === event.threadId
    && params.turnId === event.turnId
    && params.itemId === event.itemId
    && preview.threadId === params.threadId
    && preview.turnId === params.turnId
    && preview.itemId === params.itemId;
  if (!exactIdentity
    || typeof preview.fingerprint !== "string"
    || preview.fingerprint !== event.previewFingerprint) return request;

  return { ...request, reviewPreview: undefined, reviewInvalidated: true };
}

const choice = (decision, label, tone) => ({ decision, label, tone });

export function approvalPresentation(request) {
  if (!isRecord(request) || typeof request.method !== "string") {
    return { supported: false, title: "Unsupported App Server request", details: {}, choices: [] };
  }

  const params = isRecord(request.params) ? request.params : {};
  if (request.method === "item/commandExecution/requestApproval") {
    const decisions = Array.isArray(params.availableDecisions)
      ? params.availableDecisions.filter((decision) => typeof decision === "string")
      : [];
    const hasCommand = typeof params.command === "string" && params.command.trim().length > 0;
    const experimentalPermissions = isRecord(params.additionalPermissions)
      && Object.keys(params.additionalPermissions).length > 0;
    const choices = [];
    if (hasCommand && Array.isArray(params.availableDecisions) && decisions.includes("accept") && !experimentalPermissions) {
      choices.push(choice("accept", "Approve once", "primary"));
    }
    choices.push(choice("decline", "Deny", "secondary"));
    if (!Array.isArray(params.availableDecisions) || decisions.includes("cancel")) {
      choices.push(choice("cancel", "Deny and stop", "danger"));
    }
    return {
      supported: true,
      title: "Command approval",
      details: {
        command: hasCommand ? params.command : "Command details unavailable",
        workingDirectory: typeof params.cwd === "string" ? params.cwd : "Unknown",
        reason: typeof params.reason === "string" ? params.reason : "No reason supplied",
        network: isRecord(params.networkApprovalContext) ? params.networkApprovalContext : null,
      },
      warning: experimentalPermissions
        ? "This request includes an experimental permission profile; approval is withheld."
        : !hasCommand ? "App Server did not provide a command to review." : undefined,
      choices,
    };
  }

  if (request.method === "item/fileChange/requestApproval") {
    const persistentRoot = typeof params.grantRoot === "string" && params.grantRoot.trim().length > 0;
    const reviewInvalidated = request.reviewInvalidated === true;
    const reviewPreview = projectFileChangeReviewPreview(request, params);
    const choices = [];
    if (reviewPreview && !persistentRoot && !reviewInvalidated) choices.push(choice("accept", "Approve once", "primary"));
    choices.push(choice("decline", "Deny", "secondary"), choice("cancel", "Deny and stop", "danger"));
    return {
      supported: true,
      title: "File changes approval",
      details: {
        reason: typeof params.reason === "string" ? params.reason : "No reason supplied",
        requestedRoot: persistentRoot ? params.grantRoot : null,
      },
      reviewPreview: reviewPreview && !persistentRoot && !reviewInvalidated ? reviewPreview : null,
      warning: persistentRoot
        ? "This request asks to expand a persistent write root."
        : reviewInvalidated
          ? "The proposed changes changed after preview; approval is withheld. Deny this request."
          : reviewPreview ? undefined
            : isRecord(request.reviewPreview)
              ? "The proposed changes could not be bound to this exact App Server request; approval is withheld."
              : "The App Server request does not include a diff to review; approval is withheld.",
      choices,
    };
  }

  if (request.method === "item/permissions/requestApproval") {
    const permissionProfile = isRecord(params.permissions) ? params.permissions : null;
    const known = permissionProfile !== null && isKnownPermissionProfile(permissionProfile);
    return {
      supported: true,
      title: "Additional permissions",
      details: {
        workingDirectory: typeof params.cwd === "string" ? params.cwd : "Unknown",
        reason: typeof params.reason === "string" ? params.reason : "No reason supplied",
      },
      permissionProfile,
      warning: known ? undefined : "This request contains permission fields NeoBabylon does not recognize.",
      choices: [
        ...(known ? [choice("grantRequestedForTurn", "Grant for this turn", "primary")] : []),
        choice("deny", "Deny", "secondary"),
      ],
    };
  }

  return { supported: false, title: "Unsupported App Server request", details: {}, choices: [] };
}
