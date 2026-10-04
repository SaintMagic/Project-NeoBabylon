import { useRef, useState } from "react";
import type { HostOperation } from "./bridge";

type ProtectedRecordKey = "Projects" | "ForkBookmarks";
type RecoveryOperation = Extract<HostOperation, "listProtectedRecordBackups" | "restoreProtectedRecordBackup">;
type HostRequest = (operation: RecoveryOperation, payload?: Record<string, unknown>) => Promise<Record<string, unknown>>;
type VerifiedBackup = { sha256: string; byteLength: number; lastWriteTimeUtc: string; canRestore: boolean };
type RecoveryListing = {
  attributedTo: "NeoBabylon.Host";
  recordKey: ProtectedRecordKey;
  backups: VerifiedBackup[];
  canRestoreMissing: boolean;
  status: unknown;
  targetExists: boolean | null;
  journalExists: boolean | null;
};

const SHA256 = /^[0-9a-f]{64}$/i;
const isObject = (value: unknown): value is Record<string, unknown> =>
  value !== null && typeof value === "object" && !Array.isArray(value);
const statusValue = (status: unknown, key: string): unknown => isObject(status) ? status[key] : undefined;
const displayData = (value: unknown): string => {
  if (value === null || value === undefined) return "Unavailable";
  if (typeof value === "string") return value;
  try { return JSON.stringify(value, null, 2); } catch { return "Status could not be displayed."; }
};
const formatBytes = (value: number): string => `${new Intl.NumberFormat("en-US").format(value)} bytes`;
const recordLabel = (key: ProtectedRecordKey): string => key === "Projects" ? "Project records" : "Fork bookmarks";

function parseProtectedRecordListing(response: unknown, expectedKey: ProtectedRecordKey): RecoveryListing {
  if (!isObject(response) || response.attributedTo !== "NeoBabylon.Host") {
    throw new Error("Backup listing has no NeoBabylon.Host attribution.");
  }
  if (response.recordKey !== expectedKey) throw new Error("Backup listing does not match the selected protected record key.");
  if (!Array.isArray(response.backups) || response.backups.length > 256
      || typeof response.canRestoreMissing !== "boolean" || !Object.hasOwn(response, "status")) {
    throw new Error("The host returned a malformed protected-record backup listing.");
  }
  const backups = response.backups.map((backup): VerifiedBackup => {
    if (!isObject(backup) || typeof backup.sha256 !== "string" || !SHA256.test(backup.sha256)
        || !Number.isSafeInteger(backup.byteLength) || (backup.byteLength as number) < 0
        || typeof backup.lastWriteTimeUtc !== "string" || !Number.isFinite(Date.parse(backup.lastWriteTimeUtc))
        || typeof backup.canRestore !== "boolean") {
      throw new Error("The host returned invalid verified-backup metadata.");
    }
    return { sha256: backup.sha256, byteLength: backup.byteLength as number, lastWriteTimeUtc: backup.lastWriteTimeUtc, canRestore: backup.canRestore };
  });
  if (new Set(backups.map((backup) => backup.sha256)).size !== backups.length) {
    throw new Error("The host returned duplicate backup identities.");
  }
  const targetExists = typeof response.liveRecordExists === "boolean"
    ? response.liveRecordExists
    : typeof response.targetExists === "boolean" ? response.targetExists
      : typeof statusValue(response.status, "targetExists") === "boolean" ? statusValue(response.status, "targetExists") as boolean : null;
  const journalExists = typeof response.pendingJournal === "boolean"
    ? response.pendingJournal
    : typeof response.journalExists === "boolean" ? response.journalExists
      : typeof statusValue(response.status, "journalExists") === "boolean" ? statusValue(response.status, "journalExists") as boolean : null;
  if (response.canRestoreMissing && (!backups.some((backup) => backup.canRestore) || targetExists === true || journalExists === true)) {
    throw new Error("The host reported restore eligibility without a compatible backup or alongside a present target or journal.");
  }
  return {
    attributedTo: "NeoBabylon.Host",
    recordKey: expectedKey,
    backups,
    canRestoreMissing: response.canRestoreMissing,
    status: response.status,
    targetExists,
    journalExists,
  };
}

function validateRestoreResponse(response: unknown, expectedKey: ProtectedRecordKey, expectedHash: string): string {
  if (!isObject(response) || response.attributedTo !== "NeoBabylon.Host") {
    throw new Error("Restore response has no NeoBabylon.Host attribution.");
  }
  if (response.recordKey !== expectedKey) throw new Error("Restore response does not match the selected protected record key.");
  const expectedRefreshAction = expectedKey === "Projects" ? "getRuntimeStatus" : "listThreads";
  if (!(response.status === "restored" || response.status === "restoredButRecordUnresolved")
      || response.restoredBackupSha256 !== expectedHash || response.backupRetained !== true
      || response.liveRecordExists !== true || response.pendingJournal !== false || response.canRestoreMissing !== false
      || response.refreshRequired !== true || response.refreshAction !== expectedRefreshAction
      || response.codexHistoryAffected !== false || !isObject(response.runtimeStatus)
      || response.runtimeStatus.attributedTo !== "NeoBabylon.Host") {
    throw new Error("The host returned an incomplete or contradictory restore status for the selected backup identity.");
  }
  const guidance = typeof response.reopenGuidance === "string" ? response.reopenGuidance : "";
  return `${response.status}${response.status === "restoredButRecordUnresolved" ? " · Project record remains unresolved" : ""}${guidance ? ` · ${guidance}` : ""}`;
}

function recoveryFailureMessage(error: unknown): string {
  const message = error instanceof Error ? error.message : "The host did not confirm the restore outcome.";
  const hostDetails = error && typeof error === "object" ? error as Error & { code?: string; status?: string } : null;
  const code = hostDetails?.code ?? "unknown";
  const status = hostDetails?.status ?? "unknown";
  if (code === "busy" || status === "busy" || /\bbusy\b|in progress/i.test(message)) {
    return `The host reports recovery busy (code ${code}, status ${status}). No host operation was replayed automatically. ${message}`;
  }
  if (code === "liveRecordPresent" || code === "protectedRecordStateChanged" || /duplicate|already restored|already exists/i.test(message)) {
    return `The host reports that the record state changed or the missing-record restore may already have completed (code ${code}, status ${status}). The existing record will not be overwritten. ${message}`;
  }
  if (code === "staleBackupHash" || status === "stale" || /stale|hash mismatch|identity changed|not found/i.test(message)) {
    return `The host reports a stale selected backup identity (code ${code}, status ${status}). Reload the verified listing. ${message}`;
  }
  return `Host protected-record recovery response (code ${code}, status ${status}): ${message}`;
}

export function ProtectedRecordRecoveryPanel({ requestHost, onRestoreComplete }: {
  requestHost: HostRequest;
  onRestoreComplete?: () => Promise<string | void>;
}) {
  const [recordKey, setRecordKey] = useState<ProtectedRecordKey | null>(null);
  const [listing, setListing] = useState<RecoveryListing | null>(null);
  const [listingFresh, setListingFresh] = useState(false);
  const [selectedBackupSha256, setSelectedBackupSha256] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [pendingRestore, setPendingRestore] = useState(false);
  const [busy, setBusy] = useState(false);
  const [needsRefresh, setNeedsRefresh] = useState(false);
  const [restoreError, setRestoreError] = useState<string | null>(null);
  const [restoreStatus, setRestoreStatus] = useState<string | null>(null);
  const [restoredStatus, setRestoredStatus] = useState<string | null>(null);
  const requestGenerationRef = useRef(0);
  const confirmRef = useRef<HTMLButtonElement>(null);
  const restoreOpenerRef = useRef<HTMLButtonElement>(null);

  async function loadListing(key: ProtectedRecordKey = recordKey as ProtectedRecordKey): Promise<RecoveryListing | null> {
    if (!key) return null;
    const generation = ++requestGenerationRef.current;
    setLoading(true);
    setListingFresh(false);
    setListError(null);
    try {
      const response = await requestHost("listProtectedRecordBackups", { recordKey: key });
      const next = parseProtectedRecordListing(response, key);
      if (generation !== requestGenerationRef.current) return null;
      setListing(next);
      setListingFresh(true);
      setNeedsRefresh(false);
      setListError(null);
      setRestoreError(null);
      return next;
    } catch (error) {
      if (generation === requestGenerationRef.current) {
        setListError(recoveryFailureMessage(error));
      }
      return null;
    } finally {
      if (generation === requestGenerationRef.current) setLoading(false);
    }
  }

  function selectRecord(key: ProtectedRecordKey) {
    if (busy || loading) return;
    requestGenerationRef.current += 1;
    setRecordKey(key);
    setListing(null);
    setListingFresh(false);
    setSelectedBackupSha256(null);
    setPendingRestore(false);
    setNeedsRefresh(false);
    setListError(null);
    setRestoreError(null);
    setRestoreStatus(null);
    setRestoredStatus(null);
    void loadListing(key);
  }

  const selectedBackup = listing?.backups.find((backup) => backup.sha256 === selectedBackupSha256) ?? null;
  const restorePrerequisites = Boolean(recordKey && listing && listingFresh && !needsRefresh && !loading && !busy
    && listing.canRestoreMissing && selectedBackup?.canRestore);
  const canBeginRestore = restorePrerequisites && !pendingRestore;
  const canConfirmRestore = restorePrerequisites && pendingRestore;

  function beginRestore() {
    if (!canBeginRestore) return;
    setPendingRestore(true);
    setRestoreError(null);
    setRestoreStatus(null);
    requestAnimationFrame(() => confirmRef.current?.focus());
  }

  function cancelRestore() {
    setPendingRestore(false);
    setRestoreError(null);
    requestAnimationFrame(() => restoreOpenerRef.current?.focus());
  }

  async function confirmRestore() {
    if (!canConfirmRestore || !recordKey || !selectedBackup || !listing) return;
    setBusy(true);
    setRestoreError(null);
    setRestoreStatus(null);
    setRestoredStatus(null);
    let hostConfirmedRestore = false;
    try {
      const response = await requestHost("restoreProtectedRecordBackup", {
        recordKey,
        expectedBackupSha256: selectedBackup.sha256,
      });
      const hostStatus = validateRestoreResponse(response, recordKey, selectedBackup.sha256);
      hostConfirmedRestore = true;
      const refreshed = await loadListing(recordKey);
      if (!refreshed) throw new Error("Restore returned, but the follow-up host listing failed. Refresh manually before retrying.");
      if (!refreshed.backups.some((backup) => backup.sha256 === selectedBackup.sha256)) {
        throw new Error("The selected verified backup is no longer listed after the restore response.");
      }
      if (refreshed.canRestoreMissing) {
        throw new Error("The follow-up host listing still reports canRestoreMissing: true. Do not repeat the confirmed restore.");
      }
      const runtimeRefresh = await onRestoreComplete?.();
      setPendingRestore(false);
      setNeedsRefresh(false);
      setRestoreStatus(`Host restore response: ${hostStatus}. Refreshed host status: ${displayData(refreshed.status)}; canRestoreMissing: ${String(refreshed.canRestoreMissing)}.${runtimeRefresh ? ` ${runtimeRefresh}` : ""}`);
      setRestoredStatus(refreshed.targetExists === true && refreshed.journalExists === false
        ? "The host now reports the record present with no pending journal. The selected backup remains retained."
        : "The host listing was refreshed. Review its status before taking another action; no record contents were returned to the UI.");
    } catch (error) {
      setNeedsRefresh(true);
      if (hostConfirmedRestore) setPendingRestore(false);
      const detail = recoveryFailureMessage(error);
      setRestoreError(`${hostConfirmedRestore ? `The host confirmed the selected restore, but follow-up refresh did not complete. ${detail}` : detail} The selected record and backup remain selected. No restore was replayed; reload host status before deciding what to do next.`);
    } finally {
      setBusy(false);
    }
  }

  const restoreUnavailableReason = !listing ? "Load a host-attributed backup listing first."
    : listing.targetExists === true ? "A live record is present; explicit missing-record restore is unavailable."
      : listing.journalExists === true ? "A recovery journal is present; ordinary record access owns journal recovery."
        : !listing.backups.some((backup) => backup.canRestore) ? "No current-schema-compatible verified backup is available. Future-version or otherwise incompatible backups remain visible but cannot be restored."
          : "The host reports canRestoreMissing: false.";

  return <section className="protected-record-recovery-panel" aria-label="Projects and fork bookmarks backup recovery">
    <p className="protected-record-recovery-scope">This restores only the missing Projects or Fork bookmarks application record. Runtime conversation history is unaffected. There is no bulk restore and no restore of all Data.</p>
    <div className="protected-record-key-choices" role="group" aria-label="Choose an application record">
      <button type="button" aria-pressed={recordKey === "Projects"} disabled={busy || loading} onClick={() => selectRecord("Projects")}>Project records</button>
      <button type="button" aria-pressed={recordKey === "ForkBookmarks"} disabled={busy || loading} onClick={() => selectRecord("ForkBookmarks")}>Fork bookmarks</button>
    </div>
    {recordKey && <>
      <h4>{recordLabel(recordKey)} · verified backups</h4>
      <p>Listing is read-only. It returns backup identity and file-system metadata, never record bytes or parsed contents. Last-write times are file-system metadata, not signed creation times.</p>
      <button type="button" className="candidate-inline-action" disabled={loading || busy} onClick={() => void loadListing()}>{loading ? "Checking verified backups…" : listing ? "Reload verified backups" : "Load verified backups"}</button>
      {listError && <p className="candidate-error" role="alert">{listError}</p>}
      {listing && <>
        <p className="candidate-binding-current" role="status">Host: {listing.attributedTo} · record key: {listing.recordKey} · canRestoreMissing: {String(listing.canRestoreMissing)}</p>
        <dl className="protected-record-status">
          <div><dt>Host status</dt><dd>{displayData(listing.status)}</dd></div>
          <div><dt>Target exists</dt><dd>{listing.targetExists === null ? "Not reported" : String(listing.targetExists)}</dd></div>
          <div><dt>Recovery journal exists</dt><dd>{listing.journalExists === null ? "Not reported" : String(listing.journalExists)}</dd></div>
        </dl>
        {!listingFresh && <p className="candidate-error" role="alert">This listing is not fresh enough for restore. Reload it before continuing.</p>}
        {listing.backups.length === 0 && <p role="status">No verified backups were returned.</p>}
        <div className="protected-record-backups" role="radiogroup" aria-label={`${recordLabel(recordKey)} verified backup selection`}>
          {listing.backups.map((backup) => <label className="protected-record-backup" key={backup.sha256}>
            <input type="radio" name={`protected-record-backup-${recordKey}`} value={backup.sha256}
              checked={selectedBackupSha256 === backup.sha256} disabled={busy || loading || pendingRestore || !backup.canRestore}
              onChange={() => { setSelectedBackupSha256(backup.sha256); setRestoreError(null); setRestoreStatus(null); setRestoredStatus(null); }} />
            <span><strong>SHA-256</strong><code>{backup.sha256}</code><small>{formatBytes(backup.byteLength)} · last write {backup.lastWriteTimeUtc} (UTC) · Can restore: {String(backup.canRestore)}</small>
              {!backup.canRestore && <small>This verified backup is incompatible with the current record schema; Restore is unavailable.</small>}</span>
          </label>)}
        </div>
        {!listing.canRestoreMissing && <p className="candidate-activation-reason">{restoreUnavailableReason}</p>}
        {needsRefresh && <p className="candidate-error" role="alert">Reload status before an explicit retry. The selection is retained.</p>}
        {restoreError && <p className="candidate-error" role="alert">{restoreError}</p>}
        {restoreStatus && <p role="status">{restoreStatus}</p>}
        {restoredStatus && <p role="status">{restoredStatus}</p>}
        {!pendingRestore && <button ref={restoreOpenerRef} type="button" className="candidate-inline-action" disabled={!canBeginRestore} onClick={beginRestore}>Restore missing {recordLabel(recordKey)}</button>}
        {pendingRestore && <fieldset className="candidate-confirm protected-record-confirm" disabled={busy}>
          <legend>Confirm restore of one missing record</legend>
          <p>The host will restore only the selected backup if the record and its recovery journal are both absent. An existing target is never replaced; the backup is retained.</p>
          <p className="candidate-confirm-identity">Record key: {recordKey}<br />Expected backup SHA-256: {selectedBackup?.sha256 ?? "Unavailable"}<br />Verified size: {selectedBackup ? formatBytes(selectedBackup.byteLength) : "Unavailable"}<br />Last write: {selectedBackup?.lastWriteTimeUtc ?? "Unavailable"} (UTC)<br />Host status: {displayData(listing.status)}</p>
          {restoreError && <p className="candidate-error" role="alert">{restoreError}</p>}
          <div className="candidate-decision-actions">
            <button ref={confirmRef} type="button" disabled={!canConfirmRestore} onClick={() => void confirmRestore()}>{busy ? "Restoring selected record…" : restoreError ? "Retry restore explicitly" : "Confirm restore"}</button>
            <button type="button" disabled={busy} onClick={cancelRestore}>Cancel</button>
          </div>
        </fieldset>}
      </>}
    </>}
  </section>;
}
