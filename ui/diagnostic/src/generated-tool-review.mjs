const MAX_PAGE_CHARACTERS = 32_768;
const MAX_HISTORY_RECORDS = 1_024;
const MAX_BINDING_RECORDS = 512;
const MAX_BINDING_TOOL_IDS = 512;
const MAX_RECOVERY_CANDIDATES = 64;
const BINDING_STATES = new Set(["prepared-disabled", "revoked", "cleaned"]);
const SHA256 = /^[0-9a-f]{64}$/;
const TOOL_ID = /^[a-z0-9][a-z0-9-]{0,79}$/;
const RESERVED_TOOL_ID = /^(?:con|prn|aux|nul|com[1-9]|lpt[1-9])$/;
const ACTIVATION_STATES = new Set(["disabled", "active", "revoked", "stale"]);
const ACTIVATION_QUALIFICATION_STATES = new Set(["pending", "unsupported", "qualified"]);
const ACTIVATION_FAILURE_KINDS = new Set(["qualificationPending", "unsupported", "stale"]);
const ACTIVATION_EVENTS = new Set(["denied", "activated", "revoked"]);
const MAX_ACTIVATION_RECORDS = 512;
const MAX_COMPARISON_PATHS = 512;
const MAX_COMPARISON_PREVIEW_CHARACTERS = 32_768;

function plainObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function nullableIdentity(value, label) {
  if (value === null) return null;
  if (typeof value !== "string" || !value) throw new Error(`The host returned an invalid ${label}.`);
  return value;
}

function boundedStringList(value, label, maxItems = MAX_COMPARISON_PATHS) {
  if (!Array.isArray(value) || value.length > maxItems
      || value.some((item) => typeof item !== "string" || !item || item.length > 2_048 || /[\x00-\x1f\x7f-\x9f]/.test(item))) {
    throw new Error(`The host returned a malformed or unbounded ${label}.`);
  }
  return value;
}

function nullableFailureKind(value) {
  if (value === null || value === undefined) return null;
  if (typeof value !== "string" || !ACTIVATION_FAILURE_KINDS.has(value)) {
    throw new Error("The host returned an unsupported activation failure kind.");
  }
  return value;
}

function activationHistoryRecord(record, toolId) {
  if (!plainObject(record) || record.schemaVersion !== 1 || record.toolId !== toolId
      || !Number.isSafeInteger(record.sequence) || record.sequence < 1 || record.sequence > MAX_ACTIVATION_RECORDS
      || !ACTIVATION_EVENTS.has(record.event) || !["disabled", "active", "revoked"].includes(record.state)
      || record.event === "activated" && record.state !== "active"
      || record.event === "revoked" && record.state !== "revoked"
      || record.event === "denied" && !["disabled", "active"].includes(record.state)
      || !ACTIVATION_QUALIFICATION_STATES.has(record.qualificationState)
      || typeof record.note !== "string" || !record.note.trim() || record.note.length > 2_048
      || /[\x00-\x1f\x7f-\x9f]/.test(record.note)
      || typeof record.recordedAtUtc !== "string" || !Number.isFinite(Date.parse(record.recordedAtUtc))
      || !SHA256.test(record.recordSha256)
      || typeof record.previousRecordSha256 !== "string"
      || !(record.previousRecordSha256 === "" || SHA256.test(record.previousRecordSha256))
      || !Array.isArray(record.blockers) || record.blockers.length > 32
      || record.blockers.some((blocker) => typeof blocker !== "string" || !blocker || blocker.length > 512
        || /[\x00-\x1f\x7f-\x9f]/.test(blocker))
      || record.requestedAuthority !== null && !plainObject(record.requestedAuthority)) {
    throw new Error("The host returned malformed activation history.");
  }
  nullableIdentity(record.candidateContentIdentity ?? null, "activation-history content identity");
  nullableIdentity(record.reviewIdentity ?? null, "activation-history review identity");
  nullableSha256(record.reviewRecordSha256, "activation-history review record SHA-256");
  nullableSha256(record.reviewSnapshotIdentity, "activation-history review snapshot identity");
  nullableSha256(record.bindingRecordSha256, "activation-history binding record SHA-256");
  nullableSha256(record.invocationPlanIdentity, "activation-history invocation plan identity");
  nullableSha256(record.nodeRuntimeSha256, "activation-history Node runtime SHA-256");
  nullableSha256(record.inputSchemaSha256, "activation-history input schema SHA-256");
  nullableSha256(record.outputSchemaSha256, "activation-history output schema SHA-256");
  nullableSha256(record.dependencyIdentity, "activation-history dependency identity");
  nullableSha256(record.authorityIdentity, "activation-history authority identity");
  nullableSha256(record.hostQualificationIdentity, "activation-history host qualification identity");
  nullableSha256(record.activationRecordSha256, "activation-history activation record SHA-256");
  nullableFailureKind(record.failureKind);
  return record;
}

export function parseGeneratedToolActivationStatus(response, toolId) {
  if (!plainObject(response) || response.attributedTo !== "NeoBabylon.Host") {
    throw new Error("Activation status has no NeoBabylon.Host attribution.");
  }
  if (response.toolId !== toolId) throw new Error("Activation status does not match the requested tool ID.");
  if (typeof response.activationAllowed !== "boolean"
      || !ACTIVATION_QUALIFICATION_STATES.has(response.qualificationState)
      || !ACTIVATION_STATES.has(response.state)
      || !Array.isArray(response.blockers) || response.blockers.length > 64
      || response.blockers.some((blocker) => typeof blocker !== "string" || !blocker || blocker.length > 512)
      || response.callableTurnsAllowed !== undefined && typeof response.callableTurnsAllowed !== "boolean"
      || response.callableBlockers !== undefined && (!Array.isArray(response.callableBlockers)
        || response.callableBlockers.length > 64
        || response.callableBlockers.some((blocker) => typeof blocker !== "string" || !blocker || blocker.length > 512))
      || response.inventoryConfirmation !== undefined && !(Number.isSafeInteger(response.inventoryConfirmation)
        && response.inventoryConfirmation >= 0 && response.inventoryConfirmation <= 3
        || ["Unknown", "ConfirmedExact", "UnexpectedTools", "SchemaMismatch"].includes(response.inventoryConfirmation))
      || response.record != null && !plainObject(response.record)) {
    throw new Error("The host returned malformed activation status.");
  }
  return {
    attributedTo: response.attributedTo,
    toolId,
    activationAllowed: response.activationAllowed,
    qualificationState: response.qualificationState,
    failureKind: nullableFailureKind(response.failureKind),
    blockers: response.blockers,
    callableTurnsAllowed: response.callableTurnsAllowed ?? null,
    callableFailureKind: nullableFailureKind(response.callableFailureKind),
    callableBlockers: response.callableBlockers ?? [],
    inventoryConfirmation: response.inventoryConfirmation ?? null,
    state: response.state,
    contentIdentity: nullableIdentity(response.contentIdentity, "candidate content identity"),
    reviewIdentity: nullableIdentity(response.reviewIdentity, "review identity"),
    bindingRecordSha256: nullableIdentity(response.bindingRecordSha256, "prepared-binding record SHA-256"),
    activationRecordSha256: nullableIdentity(response.activationRecordSha256, "activation record SHA-256"),
    hostQualificationIdentity: nullableSha256(response.hostQualificationIdentity ?? null, "host qualification identity"),
    record: response.record == null ? null : activationHistoryRecord(response.record, toolId),
    requestedAuthority: response.requestedAuthority == null ? null : plainObject(response.requestedAuthority)
      ? response.requestedAuthority
      : (() => { throw new Error("The host returned malformed requested-authority details."); })(),
  };
}

export function parseGeneratedToolActivationHistory(response, toolId) {
  if (!plainObject(response) || response.attributedTo !== "NeoBabylon.Host") {
    throw new Error("Activation history has no NeoBabylon.Host attribution.");
  }
  if (response.toolId !== toolId) throw new Error("Activation history does not match the requested tool ID.");
  const records = response.records;
  if (!Array.isArray(records) || records.length > MAX_ACTIVATION_RECORDS) {
    throw new Error("The host returned malformed or unbounded activation history.");
  }
  let previous = null;
  const normalizedRecords = [];
  for (const record of records) {
    activationHistoryRecord(record, toolId);
    if (previous && (record.sequence !== previous.sequence + 1 || record.previousRecordSha256 !== previous.recordSha256)) {
      throw new Error("Activation history is not sequence-linked to its previous record.");
    }
    if (!previous && (record.sequence !== 1 || record.previousRecordSha256 !== "")) {
      throw new Error("The first activation history record has an unexpected predecessor.");
    }
    normalizedRecords.push(record);
    previous = record;
  }
  return normalizedRecords;
}

export function parseGeneratedToolActivationTransition(response, toolId, expectedState) {
  if (!plainObject(response) || response.attributedTo !== "NeoBabylon.Host") {
    throw new Error("Activation transition has no NeoBabylon.Host attribution.");
  }
  if (!plainObject(response.status)) {
    throw new Error("Activation transition did not include the host's current status projection.");
  }
  const status = parseGeneratedToolActivationStatus(response.status, toolId);
  const failureKind = nullableFailureKind(response.failureKind);
  const record = response.record == null ? null : activationHistoryRecord(response.record, toolId);
  if (expectedState && status.state !== expectedState) {
    throw new Error(failureKind === "qualificationPending"
      ? "The host denied activation because qualification is still pending."
      : failureKind === "unsupported"
        ? "The host denied activation because this route is unsupported."
        : failureKind === "stale"
          ? "The host rejected this lifecycle identity as stale. Reload current status and histories."
          : "The host did not confirm the requested activation state.");
  }
  return { attributedTo: response.attributedTo, failureKind, status, record };
}

function nullableSha256(value, label) {
  if (value === null) return null;
  if (typeof value !== "string" || !SHA256.test(value)) throw new Error(`The host returned an invalid ${label}.`);
  return value;
}

function previousFileSha256(value) {
  if (value === "") return "";
  if (typeof value !== "string" || !SHA256.test(value)) throw new Error("The host returned an invalid previous file SHA-256.");
  return value;
}

function comparisonFileChange(value) {
  if (!plainObject(value) || typeof value.path !== "string" || !value.path || value.path.length > 2_048
      || typeof value.beforePreviewTruncated !== "boolean" || typeof value.afterPreviewTruncated !== "boolean"
      || typeof value.previewAvailable !== "boolean") {
    throw new Error("The host returned a malformed comparison file change.");
  }
  const beforePreview = value.beforePreview;
  const afterPreview = value.afterPreview;
  if (!(beforePreview === null || typeof beforePreview === "string")
      || !(afterPreview === null || typeof afterPreview === "string")
      || typeof beforePreview === "string" && beforePreview.length > MAX_COMPARISON_PREVIEW_CHARACTERS
      || typeof afterPreview === "string" && afterPreview.length > MAX_COMPARISON_PREVIEW_CHARACTERS
      || value.previewAvailable && beforePreview === null && afterPreview === null) {
    throw new Error("The host returned an invalid or unbounded comparison preview.");
  }
  return {
    path: value.path,
    previousSha256: previousFileSha256(value.previousSha256),
    currentSha256: nullableSha256(value.currentSha256, "current file SHA-256"),
    beforePreview,
    afterPreview,
    beforePreviewTruncated: value.beforePreviewTruncated,
    afterPreviewTruncated: value.afterPreviewTruncated,
    previewAvailable: value.previewAvailable,
  };
}

function comparisonManifestChange(value) {
  if (!plainObject(value) || typeof value.field !== "string" || !value.field
      || typeof value.beforeJson !== "string" || typeof value.afterJson !== "string"
      || value.beforeJson.length > MAX_COMPARISON_PREVIEW_CHARACTERS
      || value.afterJson.length > MAX_COMPARISON_PREVIEW_CHARACTERS) {
    throw new Error("The host returned malformed changed-manifest details.");
  }
  return { field: value.field, beforeJson: value.beforeJson, afterJson: value.afterJson };
}

function comparisonEvidence(value) {
  if (!plainObject(value)
      || ["kind", "evidenceId", "outcome", "description", "path", "sha256"].some((key) =>
        typeof value[key] !== "string" || value[key].length > 4_096)
      || !SHA256.test(value.sha256)) {
    throw new Error("The host returned malformed review-comparison evidence.");
  }
  return value;
}

export function parseGeneratedToolReviewComparison(response, toolId, contentIdentity) {
  if (!plainObject(response) || response.attributedTo !== "NeoBabylon.Host") {
    throw new Error("Review comparison has no NeoBabylon.Host attribution.");
  }
  const projection = response.comparison;
  if (!plainObject(projection)) throw new Error("The host did not include its review-comparison projection.");
  if (projection.toolId !== toolId) throw new Error("Review comparison does not match the requested tool ID.");
  const currentContentIdentity = projection.currentContentIdentity;
  if (currentContentIdentity !== contentIdentity) {
    throw new Error("Review comparison does not match the requested current content identity.");
  }
  if (!["available", "unavailable", "noPriorReview"].includes(projection.comparisonState)) {
    throw new Error("The host returned an unknown review comparison state.");
  }
  const addedFiles = projection.addedFiles;
  const removedFiles = projection.removedFiles;
  const modifiedFiles = projection.modifiedFiles;
  const changedManifestFields = projection.changedManifestFields;
  const reviewedEvidence = projection.reviewedEvidence;
  const currentEvidence = projection.currentEvidence;
  if (!Array.isArray(addedFiles) || addedFiles.length > MAX_COMPARISON_PATHS
      || !Array.isArray(removedFiles) || removedFiles.length > MAX_COMPARISON_PATHS
      || !Array.isArray(modifiedFiles) || modifiedFiles.length > MAX_COMPARISON_PATHS
      || !Array.isArray(changedManifestFields) || changedManifestFields.length > MAX_COMPARISON_PATHS
      || !Array.isArray(reviewedEvidence) || reviewedEvidence.length > MAX_COMPARISON_PATHS
      || !Array.isArray(currentEvidence) || currentEvidence.length > MAX_COMPARISON_PATHS
      || projection.comparisonState === "unavailable"
        && (typeof projection.unavailableReason !== "string" || !projection.unavailableReason.trim())) {
    throw new Error("The host returned malformed review comparison details.");
  }
  const result = {
    attributedTo: response.attributedTo,
    toolId,
    comparisonState: projection.comparisonState,
    reviewedContentIdentity: nullableIdentity(projection.reviewedContentIdentity, "reviewed content identity"),
    currentContentIdentity,
    priorReviewIdentity: nullableIdentity(projection.priorReviewIdentity ?? null, "prior review identity"),
    priorDecision: projection.priorDecision == null || projection.priorDecision === "reviewed" || projection.priorDecision === "rejected"
      ? projection.priorDecision : (() => { throw new Error("The host returned an invalid prior review decision."); })(),
    failureKind: projection.failureKind == null ? null : typeof projection.failureKind === "string" ? projection.failureKind
      : (() => { throw new Error("The host returned an invalid comparison failure kind."); })(),
    addedFiles: addedFiles.map(comparisonFileChange),
    removedFiles: removedFiles.map(comparisonFileChange),
    modifiedFiles: modifiedFiles.map(comparisonFileChange),
    changedManifestFields: changedManifestFields.map(comparisonManifestChange),
    reviewedEvidence: reviewedEvidence.map(comparisonEvidence),
    currentEvidence: currentEvidence.map(comparisonEvidence),
    unavailableReason: typeof projection.unavailableReason === "string" ? projection.unavailableReason : null,
  };
  if (result.comparisonState === "unavailable" && (result.addedFiles.length || result.removedFiles.length || result.modifiedFiles.length)) {
    throw new Error("The host returned file changes for a comparison with no available historical snapshot.");
  }
  if (result.comparisonState === "noPriorReview" && (result.addedFiles.length || result.removedFiles.length || result.modifiedFiles.length
      || result.priorDecision !== null || result.priorReviewIdentity !== null || result.reviewedContentIdentity !== null)) {
    throw new Error("The host returned prior-review details despite reporting that no earlier decision exists.");
  }
  if (result.comparisonState === "unavailable" && result.reviewedEvidence.length) {
    throw new Error("The host returned historical evidence despite reporting that the prior snapshot is unavailable.");
  }
  return result;
}

export function parsePreparedBindingToolIds(response) {
  if (response?.attributedTo !== "NeoBabylon.Host") throw new Error("Prepared binding tool list has no NeoBabylon.Host attribution.");
  const ids = response.toolIds;
  if (!Array.isArray(ids) || ids.length > MAX_BINDING_TOOL_IDS) throw new Error("Prepared binding tool list is malformed or unbounded.");
  if (ids.some((id) => typeof id !== "string" || !TOOL_ID.test(id) || RESERVED_TOOL_ID.test(id))) {
    throw new Error("Prepared binding tool list contains an invalid tool ID.");
  }
  if (new Set(ids).size !== ids.length) throw new Error("Prepared binding tool list contains duplicate tool IDs.");
  return ids;
}

function validBindingRecord(record, toolId) {
  return record && typeof record === "object" && !Array.isArray(record)
    && record.schemaVersion === 1 && record.toolId === toolId
    && typeof record.candidateContentIdentity === "string" && !!record.candidateContentIdentity
    && typeof record.reviewIdentity === "string" && !!record.reviewIdentity
    && typeof record.reviewInterfaceVersion === "string" && !!record.reviewInterfaceVersion
    && typeof record.reviewRecordSha256 === "string" && SHA256.test(record.reviewRecordSha256)
    && BINDING_STATES.has(record.state)
    && record.activationState === "disabled" && record.callableRoute === "none"
    && typeof record.note === "string" && !!record.note.trim() && record.note.length <= 2_048
    && !/[\x00-\x1f\x7f-\x9f]/.test(record.note)
    && typeof record.recordedAtUtc === "string" && !!record.recordedAtUtc
    && Number.isSafeInteger(record.sequence) && record.sequence > 0
    && typeof record.previousRecordSha256 === "string"
    && (record.previousRecordSha256 === "" || SHA256.test(record.previousRecordSha256))
    && typeof record.recordSha256 === "string" && SHA256.test(record.recordSha256);
}

export function parsePreparedBindingHistory(response, toolId) {
  if (response?.attributedTo !== "NeoBabylon.Host") throw new Error("Prepared binding history has no NeoBabylon.Host attribution.");
  if (response.toolId !== toolId) throw new Error("Prepared binding history does not match the requested tool identity.");
  const records = response.history;
  if (!Array.isArray(records) || records.length > MAX_BINDING_RECORDS) throw new Error("Prepared binding history is malformed or unbounded.");
  let previous = null;
  const seenContent = new Set();
  for (const record of records) {
    if (!validBindingRecord(record, toolId)) throw new Error("Prepared binding history contains an invalid or callable record; disabled state is required.");
    if (record.sequence !== (previous?.sequence ?? 0) + 1
        || record.previousRecordSha256 !== (previous?.recordSha256 ?? "")) {
      throw new Error("Prepared binding history is not sequence-linked.");
    }
    if (!previous || previous.state === "cleaned") {
      if (record.state !== "prepared-disabled" || seenContent.has(record.candidateContentIdentity)) {
        throw new Error("Prepared binding history has an invalid or duplicate preparation.");
      }
      seenContent.add(record.candidateContentIdentity);
    } else if (record.state !== (previous.state === "prepared-disabled" ? "revoked" : "cleaned")
        || record.candidateContentIdentity !== previous.candidateContentIdentity
        || record.reviewIdentity !== previous.reviewIdentity
        || record.reviewInterfaceVersion !== previous.reviewInterfaceVersion
        || record.reviewRecordSha256 !== previous.reviewRecordSha256) {
      throw new Error("Prepared binding history has an invalid transition.");
    }
    previous = record;
  }
  return records;
}

export function bindingActionContext(action, currentReview, history, contentIdentity) {
  if (!Array.isArray(history)) return null;
  const current = history.at(-1);
  if (action === "prepare") {
    if (!currentReview || currentReview.decision !== "reviewed"
        || currentReview.candidateContentIdentity !== contentIdentity
        || typeof currentReview.reviewIdentity !== "string" || !currentReview.reviewIdentity
        || current && current.state !== "cleaned"
        || history.some((record) => record.candidateContentIdentity === contentIdentity)) return null;
    return { contentIdentity, reviewIdentity: currentReview.reviewIdentity };
  }
  if (!current || (action === "revoke" && current.state !== "prepared-disabled")
      || (action === "cleanup" && current.state !== "revoked")
      || !["revoke", "cleanup"].includes(action)) return null;
  return { contentIdentity: current.candidateContentIdentity, reviewIdentity: current.reviewIdentity, currentRecordSha256: current.recordSha256 };
}

export function stageBindingContext(currentReview, history, contentIdentity) {
  const current = Array.isArray(history) ? history.at(-1) : null;
  if (!current || current.state !== "prepared-disabled"
      || current.activationState !== "disabled" || current.callableRoute !== "none"
      || current.candidateContentIdentity !== contentIdentity
      || currentReview?.decision !== "reviewed"
      || currentReview.candidateContentIdentity !== contentIdentity
      || currentReview.reviewIdentity !== current.reviewIdentity
      || currentReview.recordSha256 !== current.reviewRecordSha256
      || !SHA256.test(current.recordSha256)) return null;
  return { contentIdentity, reviewIdentity: current.reviewIdentity, currentRecordSha256: current.recordSha256 };
}

export function confirmDisabledStageResponse(response, toolId, context) {
  if (response?.attributedTo !== "NeoBabylon.Host") throw new Error("Stage response has no NeoBabylon.Host attribution.");
  if (!context || response.toolId !== toolId || response.currentRecordSha256 !== context.currentRecordSha256) {
    throw new Error("Stage response does not match the requested binding identity and current record hash.");
  }
  if (response.state !== "staged-disabled") throw new Error("Stage response did not confirm a disabled stage.");
  if (response.runtimeConfirmation !== "Unknown") throw new Error("Stage runtime confirmation was not reported as Unknown.");
  return { state: "staged-disabled", currentRecordSha256: response.currentRecordSha256, runtimeConfirmation: "Unknown" };
}

export function verifyStageRecovery(listing, reviews, bindings, target) {
  if (listing?.attributedTo !== "NeoBabylon.Host") throw new Error("Fresh candidate listing has no NeoBabylon.Host attribution.");
  if (!Array.isArray(listing.candidates) || listing.candidates.length > MAX_RECOVERY_CANDIDATES
      || listing.candidates.some((candidate) => !candidate || typeof candidate !== "object" || Array.isArray(candidate)
        || candidate.state !== "unapproved")) throw new Error("Fresh candidate listing is malformed.");
  const matches = listing.candidates.filter((candidate) => candidate.toolId === target.toolId);
  if (matches.length !== 1 || matches[0].contentIdentity !== target.contentIdentity || !candidateContract(matches[0])) {
    throw new Error("Fresh candidate identity or contract is missing, changed, or duplicated.");
  }
  const reviewHistory = parseReviewHistory(reviews, target.toolId);
  const bindingHistory = parsePreparedBindingHistory(bindings, target.toolId);
  const currentReview = currentReviewRecord(reviewHistory, target.contentIdentity);
  const current = stageBindingContext(currentReview, bindingHistory, target.contentIdentity);
  if (!current || current.reviewIdentity !== target.reviewIdentity
      || current.currentRecordSha256 !== target.currentRecordSha256) {
    throw new Error("Fresh review or binding history does not confirm the same prepared-disabled identity.");
  }
  return { reviewHistory, bindingHistory };
}

export function confirmBindingTransition(response, history, action, toolId, context, previous) {
  if (response?.attributedTo !== "NeoBabylon.Host") throw new Error("Prepared binding transition has no NeoBabylon.Host attribution.");
  const record = response.record;
  const expectedState = { prepare: "prepared-disabled", revoke: "revoked", cleanup: "cleaned" }[action];
  if (!expectedState || !validBindingRecord(record, toolId)) throw new Error("Prepared binding transition is invalid or callable; disabled state is required.");
  const latest = history?.at(-1);
  if (record.state !== expectedState || record.candidateContentIdentity !== context.contentIdentity
      || record.reviewIdentity !== context.reviewIdentity
      || record.sequence !== (previous?.sequence ?? 0) + 1
      || record.previousRecordSha256 !== (previous?.recordSha256 ?? "")
      || !latest || latest.recordSha256 !== record.recordSha256
      || latest.sequence !== record.sequence || latest.state !== record.state
      || latest.candidateContentIdentity !== record.candidateContentIdentity
      || latest.reviewIdentity !== record.reviewIdentity) {
    throw new Error("Prepared binding transition could not be confirmed as the current history record.");
  }
  return record;
}

export function candidateContract(candidate) {
  const contract = candidate?.contract;
  if (!contract || typeof contract !== "object" || Array.isArray(contract)
      || typeof contract.invocation !== "string" || !contract.invocation.trim()
      || typeof contract.inputSchema !== "string" || !contract.inputSchema.trim()
      || typeof contract.outputSchema !== "string" || !contract.outputSchema.trim()) return null;
  return { invocation: contract.invocation, inputSchema: contract.inputSchema, outputSchema: contract.outputSchema };
}

export function createSerialRequestQueue() {
  let tail = Promise.resolve();
  return (request) => {
    const result = tail.then(request);
    tail = result.then(() => undefined, () => undefined);
    return result;
  };
}

export function parseCandidateFilePage(response, request) {
  if (response?.attributedTo !== "NeoBabylon.Host") {
    throw new Error("Candidate file page has no NeoBabylon.Host attribution.");
  }
  if (response.toolId !== request.toolId || response.contentIdentity !== request.contentIdentity
      || response.path !== request.path || response.offset !== request.offset) {
    throw new Error("Candidate file page does not match the requested content identity, path, and offset.");
  }
  const { text, total, next, hasMore } = response;
  if (typeof text !== "string" || text.length > MAX_PAGE_CHARACTERS
      || !Number.isSafeInteger(total) || total < 0
      || !Number.isSafeInteger(next) || next !== request.offset + text.length || next > total
      || typeof hasMore !== "boolean" || hasMore !== (next < total)
      || hasMore && next <= request.offset) {
    throw new Error("Candidate file page has an invalid bounded UTF-16 range.");
  }
  return { path: request.path, offset: request.offset, text, total, next, hasMore };
}

export function parseReviewHistory(response, toolId) {
  if (response?.attributedTo !== "NeoBabylon.Host") {
    throw new Error("Review history has no NeoBabylon.Host attribution.");
  }
  if (response.toolId !== toolId) {
    throw new Error("Review history does not match the requested tool identity.");
  }
  const records = response.history;
  if (!Array.isArray(records) || records.length > MAX_HISTORY_RECORDS
      || records.some((record) => !record || typeof record !== "object" || Array.isArray(record)
        || record.toolId !== toolId || typeof record.candidateContentIdentity !== "string"
        || !["reviewed", "rejected"].includes(record.decision)
        || typeof record.reviewIdentity !== "string" || typeof record.note !== "string"
        || typeof record.recordedAtUtc !== "string" || !Number.isSafeInteger(record.sequence)
        || record.sequence < 1 || typeof record.recordSha256 !== "string")) {
    throw new Error("The host returned malformed review history.");
  }
  return records;
}

export function currentReviewRecord(history, contentIdentity) {
  return history.find((record) => record.candidateContentIdentity === contentIdentity) ?? null;
}

export function reviewNoteError(note) {
  if (typeof note !== "string" || !note.trim()) return "A nonblank decision note is required.";
  if (note.length > 2_048) return "The decision note must be at most 2,048 characters.";
  if (/[\x00-\x1f\x7f-\x9f\u2028\u2029]/.test(note)) return "The decision note must be a single line without control characters.";
  return null;
}
