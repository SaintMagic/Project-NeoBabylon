import { useEffect, useRef, useState } from "react";
import type { HostOperation } from "./bridge";
import {
  parseGeneratedToolActivationHistory,
  parseGeneratedToolActivationStatus,
  parseGeneratedToolActivationTransition,
  reviewNoteError,
  type GeneratedToolActivationHistoryRecord,
  type GeneratedToolActivationStatus,
} from "./generated-tool-review.mjs";

type ActivationOperation = Extract<HostOperation,
  | "getGeneratedToolActivationStatus"
  | "activateGeneratedTool"
  | "revokeGeneratedToolActivation"
  | "listGeneratedToolActivationHistory">;
type HostRequest = (operation: ActivationOperation, payload?: Record<string, unknown>) => Promise<Record<string, unknown>>;
type PendingAction = "activate" | "revoke" | null;

type Props = {
  toolId: string;
  requestHost: HostRequest;
  mode: "candidate" | "recovery";
  contentIdentity?: string | null;
  reviewIdentity?: string | null;
  bindingRecordSha256?: string | null;
  permissions?: Record<string, unknown> | null;
  actionLocked?: boolean;
  onActionBusyChange?: (busy: boolean) => void;
};

const displayData = (value: unknown): string => {
  if (value === null || value === undefined) return "Unavailable";
  if (typeof value === "string") return value;
  try { return JSON.stringify(value, null, 2); } catch { return "Value could not be displayed."; }
};
const permissionLabel = (key: string): string => key.replaceAll(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/^./, (letter) => letter.toUpperCase());

function blockerText(blocker: string): string {
  const explanations: Record<string, string> = {
    "route-not-accepted": "The generated-tool callable route has not been accepted.",
    "locked-runtime-not-qualified": "The currently locked runtime has not passed qualification.",
    "selected-provider-model-not-qualified": "The selected provider and model have not passed qualification.",
    "generated-tool-not-activated": "The generated tool does not have a current activation record.",
    "mcp-tool-schema-unobserved": "The host has not confirmed the exact callable MCP tool and schema inventory.",
    "activation-invocation-plan-stale": "The active record no longer matches the host’s current invocation plan.",
    "runtime-provider-model-evidence-not-qualified": "Current runtime, provider, and model qualification evidence is unavailable.",
  };
  return explanations[blocker] ?? `The host reports this qualification blocker: ${blocker}`;
}

function statusBlockReason(status: GeneratedToolActivationStatus | null, mode: Props["mode"]): string {
  if (!status) return mode === "candidate"
    ? "Load current host status before activation can be considered."
    : "Activation is unavailable in the independent recovery view.";
  if (mode !== "candidate") return "Activation is available only from the candidate view after the host permits it.";
  if (!status.activationAllowed) {
    if (status.blockers.length) return status.blockers.map(blockerText).join(" ");
    if (status.failureKind === "unsupported") return "The host reports that this generated-tool route is unsupported.";
    if (status.failureKind === "stale") return "The host reports stale activation identities. Reload the candidate and its histories.";
    if (status.qualificationState === "pending") return "Route, locked-runtime, and selected provider/model qualification remain pending.";
    if (status.qualificationState === "unsupported") return "The host reports that this generated-tool route is unsupported.";
    return "The host reports activationAllowed: false and supplied no blocker detail.";
  }
  if (status.state !== "disabled") return `The host reports lifecycle state ${status.state}; a new activation is unavailable.`;
  return "Activation is available only after exact current identities are confirmed.";
}

function currentIdentityMatches(status: GeneratedToolActivationStatus, props: Props): boolean {
  return props.mode === "candidate"
    && Boolean(props.contentIdentity && props.reviewIdentity && props.bindingRecordSha256)
    && status.contentIdentity === props.contentIdentity
    && status.reviewIdentity === props.reviewIdentity
    && status.bindingRecordSha256 === props.bindingRecordSha256;
}

function historyState(history: GeneratedToolActivationHistoryRecord[] | null): GeneratedToolActivationHistoryRecord | null {
  return history?.at(-1) ?? null;
}

function historyDisplayFields(record: GeneratedToolActivationHistoryRecord) {
  return [
    ["Schema version", record.schemaVersion],
    ["Tool ID", record.toolId],
    ["Sequence", record.sequence],
    ["Event", record.event],
    ["Current state", record.state],
    ["Candidate content identity", record.candidateContentIdentity],
    ["Review identity", record.reviewIdentity],
    ["Review record SHA-256", record.reviewRecordSha256],
    ["Review snapshot identity", record.reviewSnapshotIdentity],
    ["Prepared binding SHA-256", record.bindingRecordSha256],
    ["Invocation plan identity", record.invocationPlanIdentity],
    ["Node runtime SHA-256", record.nodeRuntimeSha256],
    ["Input schema SHA-256", record.inputSchemaSha256],
    ["Output schema SHA-256", record.outputSchemaSha256],
    ["Dependency identity", record.dependencyIdentity],
    ["Authority identity", record.authorityIdentity],
    ["Host qualification identity", record.hostQualificationIdentity],
    ["Activation record SHA-256", record.activationRecordSha256],
    ["Qualification state", record.qualificationState],
    ["Failure kind", record.failureKind],
    ["Blockers", record.blockers],
    ["Requested authority", record.requestedAuthority],
    ["Note", record.note],
    ["Recorded at UTC", record.recordedAtUtc],
    ["Previous record SHA-256", record.previousRecordSha256],
    ["Record SHA-256", record.recordSha256],
  ] as const;
}

function inventoryConfirmationLabel(value: number | string | null): string {
  if (value === null) return "Not reported";
  if (typeof value === "string") return value;
  return ["Unknown", "Confirmed exact", "Unexpected tools", "Schema mismatch"][value] ?? `Unrecognized host value (${value})`;
}

export function GeneratedToolActivationPanel({
  toolId,
  requestHost,
  mode,
  contentIdentity = null,
  reviewIdentity = null,
  bindingRecordSha256 = null,
  permissions = null,
  actionLocked = false,
  onActionBusyChange,
}: Props) {
  const [status, setStatus] = useState<GeneratedToolActivationStatus | null>(null);
  const [history, setHistory] = useState<GeneratedToolActivationHistoryRecord[] | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);
  const [historyError, setHistoryError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionStatus, setActionStatus] = useState<string | null>(null);
  const [attemptFailed, setAttemptFailed] = useState(false);
  const requestGenerationRef = useRef(0);
  const noteRef = useRef<HTMLInputElement>(null);
  const activateRef = useRef<HTMLButtonElement>(null);
  const revokeRef = useRef<HTMLButtonElement>(null);
  const outcomeRef = useRef<HTMLParagraphElement>(null);
  const latestHistory = historyState(history);
  const identityMatches = status ? currentIdentityMatches(status, {
    toolId, requestHost, mode, contentIdentity, reviewIdentity, bindingRecordSha256, permissions, actionLocked, onActionBusyChange,
  }) : false;
  const activationBlock = statusBlockReason(status, mode);
  const activationIdentityReady = Boolean(status?.contentIdentity && status.reviewIdentity && status.bindingRecordSha256
    && identityMatches);
  const activationEligible = Boolean(mode === "candidate" && status?.activationAllowed === true
    && status.state === "disabled" && activationIdentityReady && !actionLocked && !loading && !busy);
  const canBeginActivation = activationEligible && !pendingAction;
  const revokeIdentity = status && (status.state === "active" || status.state === "stale")
      ? status.activationRecordSha256
      : mode === "recovery" && latestHistory?.state === "active"
        ? latestHistory.activationRecordSha256 ?? (latestHistory.event === "activated" ? latestHistory.recordSha256 : null)
        : null;
  const revocationEligible = Boolean(revokeIdentity && !actionLocked && !loading && !busy);
  const canBeginRevoke = revocationEligible && !pendingAction;
  const noteError = pendingAction ? reviewNoteError(note) : null;

  useEffect(() => () => { requestGenerationRef.current += 1; }, []);

  async function refresh(): Promise<{ status: GeneratedToolActivationStatus | null; history: GeneratedToolActivationHistoryRecord[] | null }> {
    const generation = ++requestGenerationRef.current;
    setLoading(true);
    setStatusError(null);
    setHistoryError(null);
    setHistory(null);
    if (mode === "candidate") setStatus(null);
    const historyRequest = requestHost("listGeneratedToolActivationHistory", { toolId })
      .then((response) => parseGeneratedToolActivationHistory(response, toolId));
    const statusRequest = mode === "candidate"
      ? requestHost("getGeneratedToolActivationStatus", { toolId })
        .then((response) => parseGeneratedToolActivationStatus(response, toolId))
      : null;
    const [historyResult, statusResult] = await Promise.allSettled([
      historyRequest,
      ...(statusRequest ? [statusRequest] : []),
    ]);
    if (generation !== requestGenerationRef.current) return { status: null, history: null };
    const nextHistory = historyResult.status === "fulfilled" ? historyResult.value as GeneratedToolActivationHistoryRecord[] : null;
    if (nextHistory) setHistory(nextHistory);
    else setHistoryError(historyResult.status === "rejected" && historyResult.reason instanceof Error
      ? historyResult.reason.message : "Activation history could not be loaded.");
    let nextStatus: GeneratedToolActivationStatus | null = null;
    if (statusRequest) {
      const result = statusResult as PromiseSettledResult<GeneratedToolActivationStatus>;
      if (result.status === "fulfilled") { nextStatus = result.value; setStatus(result.value); }
      else setStatusError(result.reason instanceof Error ? result.reason.message : "Activation status could not be loaded.");
    }
    setLoading(false);
    return { status: nextStatus, history: nextHistory };
  }

  function beginAction(action: Exclude<PendingAction, null>) {
    if (action === "activate" && !canBeginActivation) return;
    if (action === "revoke" && !canBeginRevoke) return;
    setPendingAction(action);
    setNote("");
    setActionError(null);
    setActionStatus(null);
    setAttemptFailed(false);
    requestAnimationFrame(() => noteRef.current?.focus());
  }

  function cancelAction() {
    const opener = pendingAction === "activate" ? activateRef.current : revokeRef.current;
    setPendingAction(null);
    setNote("");
    setActionError(null);
    setAttemptFailed(false);
    requestAnimationFrame(() => opener?.focus());
  }

  async function submitActivation() {
    if (!activationEligible || pendingAction !== "activate" || !status
        || !contentIdentity || !reviewIdentity || !bindingRecordSha256 || noteError || busy
        || !status.contentIdentity || !status.reviewIdentity || !status.bindingRecordSha256
        || status.contentIdentity !== contentIdentity || status.reviewIdentity !== reviewIdentity
        || status.bindingRecordSha256 !== bindingRecordSha256) return;
    const submittedContentIdentity = status.contentIdentity;
    const submittedReviewIdentity = status.reviewIdentity;
    const submittedBindingRecordSha256 = status.bindingRecordSha256;
    setBusy(true);
    onActionBusyChange?.(true);
    setActionError(null);
    setActionStatus(null);
    try {
      const response = await requestHost("activateGeneratedTool", {
        toolId,
        contentIdentity: submittedContentIdentity,
        reviewIdentity: submittedReviewIdentity,
        expectedBindingRecordSha256: submittedBindingRecordSha256,
        note,
      });
      const transition = parseGeneratedToolActivationTransition(response, toolId, "active");
      const refreshed = await refresh();
      const confirmedStatus = refreshed.status;
      const confirmedHistory = refreshed.history;
      const latest = confirmedHistory?.at(-1);
      if (!confirmedStatus || !confirmedHistory || transition.status.state !== "active" || confirmedStatus.state !== "active"
          || !latest || latest.event !== "activated" || latest.state !== "active"
          || latest.candidateContentIdentity !== submittedContentIdentity
          || latest.reviewIdentity !== submittedReviewIdentity
          || latest.bindingRecordSha256 !== submittedBindingRecordSha256
          || confirmedStatus.contentIdentity !== submittedContentIdentity
          || confirmedStatus.reviewIdentity !== submittedReviewIdentity
          || confirmedStatus.bindingRecordSha256 !== submittedBindingRecordSha256) {
        throw new Error("The host did not confirm activation for the exact displayed candidate, review, and binding identities.");
      }
      setStatus(confirmedStatus);
      setHistory(confirmedHistory);
      setPendingAction(null);
      setNote("");
      setAttemptFailed(false);
      setActionStatus("The host confirmed activation and its linked history record.");
    } catch (error) {
      setAttemptFailed(true);
      setActionError(`${error instanceof Error ? error.message : "Activation outcome is unknown."} The note and confirmation are retained. Refresh status and history, then retry only by pressing the confirmation button.`);
      await refresh();
    } finally {
      setBusy(false);
      onActionBusyChange?.(false);
      requestAnimationFrame(() => outcomeRef.current?.focus());
    }
  }

  async function submitRevocation() {
    if (!revocationEligible || pendingAction !== "revoke" || !revokeIdentity || noteError || busy) return;
    const expectedActivationRecordSha256 = revokeIdentity;
    setBusy(true);
    onActionBusyChange?.(true);
    setActionError(null);
    setActionStatus(null);
    try {
      const response = await requestHost("revokeGeneratedToolActivation", {
        toolId,
        expectedActivationRecordSha256,
        note,
      });
      const transition = parseGeneratedToolActivationTransition(response, toolId, "revoked");
      const refreshed = await refresh();
      const confirmedHistory = refreshed.history;
      const latest = confirmedHistory?.at(-1);
      if (transition.status.state !== "revoked" || !confirmedHistory || !latest
          || latest.event !== "revoked" || latest.state !== "revoked"
          || latest.activationRecordSha256 !== expectedActivationRecordSha256) {
        throw new Error("The host did not confirm a linked revocation record.");
      }
      setHistory(confirmedHistory);
      setPendingAction(null);
      setNote("");
      setAttemptFailed(false);
      setActionStatus("The host confirmed revocation in activation history. The record remains available for audit.");
    } catch (error) {
      setAttemptFailed(true);
      setActionError(`${error instanceof Error ? error.message : "Revocation outcome is unknown."} The note and confirmation are retained. Refresh activation history, then retry only by pressing the confirmation button.`);
      await refresh();
    } finally {
      setBusy(false);
      onActionBusyChange?.(false);
      requestAnimationFrame(() => outcomeRef.current?.focus());
    }
  }

  const confirmContentIdentity = pendingAction === "activate" ? status?.contentIdentity
    : latestHistory?.candidateContentIdentity ?? status?.contentIdentity;
  const confirmReviewIdentity = pendingAction === "activate" ? status?.reviewIdentity
    : latestHistory?.reviewIdentity ?? status?.reviewIdentity;
  const confirmBindingIdentity = pendingAction === "activate" ? status?.bindingRecordSha256
    : latestHistory?.bindingRecordSha256 ?? status?.bindingRecordSha256;
  const permissionEntries = permissions ? Object.entries(permissions) : [];
  return <section className="candidate-subsection candidate-activation" aria-label={mode === "candidate" ? "Generated tool activation lifecycle" : "Candidate-independent activation recovery"}>
    <h4>{mode === "candidate" ? "Generated-tool activation · host qualification gate" : "Activation recovery · history and revoke only"}</h4>
    <p>{mode === "candidate"
      ? "Activation is separate from Review, Prepare disabled, and Stage disabled. The host is the sole authority for whether activation is allowed."
      : "This history and revocation path uses only the selected tool ID and host records. It does not load or validate candidate, review, runtime, or schema files."}</p>
    <p className="candidate-activation-authority">Permission declarations are informational. Model-directed tools run with the logged-in Windows user’s full authority; these declarations do not technically contain execution.</p>
    <button type="button" className="candidate-inline-action" disabled={loading || busy || actionLocked} onClick={() => void refresh()}>
      {loading ? "Loading activation records…" : mode === "candidate"
        ? status || history ? "Reload activation status and history" : "Load activation status and history"
        : history ? "Reload activation history" : "Load activation history"}
    </button>
    {mode === "candidate" && statusError && <p className="candidate-error" role="alert">{statusError}</p>}
    {historyError && <p className="candidate-error" role="alert">{historyError}</p>}
    {mode === "candidate" && status && <>
      <p className="candidate-binding-current" role="status">Host-attributed current state: <strong>{status.state}</strong> · activation qualification: <strong>{status.qualificationState}</strong> · activationAllowed: <strong>{String(status.activationAllowed)}</strong> · callable turns allowed: <strong>{status.callableTurnsAllowed === null ? "Not reported" : String(status.callableTurnsAllowed)}</strong> · attribution: <strong>{status.attributedTo}</strong></p>
      <dl className="candidate-activation-identities">
        <div><dt>Tool ID</dt><dd>{status.toolId}</dd></div>
        <div><dt>Current content identity</dt><dd>{status.contentIdentity ?? "Unavailable"}</dd></div>
        <div><dt>Current review identity</dt><dd>{status.reviewIdentity ?? "Unavailable"}</dd></div>
        <div><dt>Current prepared-binding SHA-256</dt><dd>{status.bindingRecordSha256 ?? "Unavailable"}</dd></div>
        <div><dt>Current activation-record SHA-256</dt><dd>{status.activationRecordSha256 ?? "Unavailable"}</dd></div>
        <div><dt>Host qualification identity</dt><dd>{status.hostQualificationIdentity ?? "Unavailable"}</dd></div>
        <div><dt>Callable failure kind</dt><dd>{status.callableFailureKind ?? "None reported"}</dd></div>
        <div><dt>Callable inventory confirmation</dt><dd>{inventoryConfirmationLabel(status.inventoryConfirmation)}</dd></div>
        <div><dt>Failure kind</dt><dd>{status.failureKind ?? "None reported"}</dd></div>
        <div><dt>Host-requested authority</dt><dd>{displayData(status.requestedAuthority)}</dd></div>
      </dl>
      {status.blockers.length > 0 && <><h5>Activation blockers</h5><ul className="candidate-activation-blockers">{status.blockers.map((blocker) => <li key={blocker}>{blockerText(blocker)}</li>)}</ul></>}
      {status.callableBlockers.length > 0 && <><h5>Callable-turn blockers</h5><ul className="candidate-activation-blockers">{status.callableBlockers.map((blocker) => <li key={blocker}>{blockerText(blocker)}</li>)}</ul></>}
      {!status.activationAllowed && <p className="candidate-activation-reason" role="status">{activationBlock}</p>}
      {status.callableTurnsAllowed === false && <p className="candidate-activation-reason" role="status">Callable turns remain blocked by the host. Activation lifecycle state and callable-route qualification are separate gates; the UI cannot override either.</p>}
      {status.activationAllowed && !identityMatches && <p className="candidate-error" role="alert">The host’s content, review, or prepared-binding identity differs from the current displayed candidate. Reload the candidate and histories before any activation.</p>}
      {status.record && <details className="candidate-binding-history"><summary>Current host activation record</summary><pre className="candidate-activation-json" tabIndex={0}>{displayData(status.record)}</pre></details>}
    </>}
    {mode === "recovery" && <p className="candidate-activation-reason">Current state below comes from the latest linked activation-history record. A current status read is not required for candidate-independent history or revocation.</p>}
    {history && !history.length && <p role="status">No activation history is recorded for this tool. Attribution: NeoBabylon.Host.</p>}
    {history && history.length > 0 && <>
      <p className="candidate-binding-current" role="status">Latest host-attributed activation history state: <strong>{latestHistory?.state}</strong> · event: <strong>{latestHistory?.event}</strong> · attribution: <strong>NeoBabylon.Host</strong></p>
      <details className="candidate-binding-history"><summary>Activation history ({history.length} linked records)</summary>
        {history.map((record) => <article className="candidate-record" key={record.recordSha256}>
          <strong className="candidate-history-title">{record.event} · state {record.state} · sequence {record.sequence}</strong>
          <dl className="candidate-activation-identities">{historyDisplayFields(record).map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{displayData(value)}</dd></div>)}</dl>
        </article>)}
      </details>
    </>}
    {history && <div className="candidate-decision-actions">
      <button ref={activateRef} type="button" disabled={!canBeginActivation} title={activationBlock} onClick={() => beginAction("activate")}>NeoBabylon Activate</button>
      <button ref={revokeRef} type="button" disabled={!canBeginRevoke} onClick={() => beginAction("revoke")}>Revoke activation</button>
    </div>}
    {mode === "candidate" && !history && <p className="candidate-activation-reason">{status ? "Activation history has not loaded; load it before a lifecycle action." : activationBlock}</p>}
    {mode === "recovery" && !history && <p className="candidate-activation-reason">Load host-attributed history for this tool ID to inspect state or revoke an active record.</p>}
    {pendingAction && <fieldset className="candidate-confirm candidate-activation-confirm" disabled={busy}>
      <legend>Confirm {pendingAction === "activate" ? "NeoBabylon Activate" : "activation revocation"}</legend>
      <p className="candidate-confirm-identity">Tool: {toolId}<br />Content: {confirmContentIdentity ?? "Unavailable"}<br />Review: {confirmReviewIdentity ?? "Unavailable"}<br />Prepared binding SHA-256: {confirmBindingIdentity ?? "Unavailable"}{pendingAction === "revoke" && <><br />Expected activation record SHA-256: {revokeIdentity ?? "Unavailable"}</>}</p>
      {pendingAction === "activate" && <>
        <p>The host currently reports activationAllowed: {String(status?.activationAllowed ?? false)}. No local override is available.</p>
        <h5>All declared permissions</h5>
        {permissionEntries.length > 0
          ? <dl className="candidate-activation-permissions">{permissionEntries.map(([key, value]) => <div key={key}><dt>{permissionLabel(key)}</dt><dd>{displayData(value)}</dd></div>)}</dl>
          : <p className="candidate-error" role="alert">No permission declaration is available for this candidate; activation cannot be confirmed.</p>}
        <h5>Host-requested authority projection</h5>
        {status?.requestedAuthority
          ? <pre className="candidate-activation-json" tabIndex={0}>{displayData(status.requestedAuthority)}</pre>
          : <p>The host supplied no separate requested-authority object. The full logged-in Windows-user authority still applies.</p>}
        <p className="candidate-activation-authority">The declared permissions do not limit the tool’s full logged-in Windows-user authority.</p>
      </>}
      <label htmlFor={`generated-tool-activation-note-${toolId}`}>Required single-line {pendingAction === "activate" ? "activation" : "revocation"} note</label>
      <input ref={noteRef} id={`generated-tool-activation-note-${toolId}`} type="text" value={note} onChange={(event) => setNote(event.currentTarget.value)} aria-invalid={Boolean(note && noteError)} aria-describedby={`generated-tool-activation-note-help-${toolId}`} />
      <p id={`generated-tool-activation-note-help-${toolId}`}>The note is sent exactly as entered (maximum 2,048 characters). A failed or uncertain operation is never replayed automatically.</p>
      {note && noteError && <p className="candidate-error" role="alert">{noteError}</p>}
      {actionError && <p ref={outcomeRef} tabIndex={-1} className="candidate-error" role="alert">{actionError}</p>}
      {actionStatus && <p ref={outcomeRef} tabIndex={-1} role="status">{actionStatus}</p>}
      <div className="candidate-decision-actions">
        <button type="button" disabled={Boolean(noteError) || actionLocked || (pendingAction === "activate"
          ? !activationEligible || permissionEntries.length === 0
          : !revocationEligible)} onClick={() => void (pendingAction === "activate" ? submitActivation() : submitRevocation())}>
          {busy ? "Recording host transition…" : attemptFailed
            ? pendingAction === "activate" ? "Retry activation explicitly" : "Retry revocation explicitly"
            : pendingAction === "activate" ? "Confirm NeoBabylon Activate" : "Confirm revocation"}
        </button>
        <button type="button" disabled={busy} onClick={cancelAction}>Cancel</button>
      </div>
    </fieldset>}
    {!pendingAction && (actionError || actionStatus) && <p ref={outcomeRef} tabIndex={-1} className={actionError ? "candidate-error" : undefined} role={actionError ? "alert" : "status"}>{actionError ?? actionStatus}</p>}
    {actionLocked && <p className="candidate-activation-reason">Another generated-tool transition is in progress. This panel is read-only until it completes.</p>}
  </section>;
}
