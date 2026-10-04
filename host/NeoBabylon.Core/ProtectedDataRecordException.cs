namespace NeoBabylon.Core;

public enum ProtectedDataRecordOperation
{
    ValidatePath,
    AcquireLock,
    InspectRecord,
    InspectBackups,
    ReadRecord,
    ResolveJournal,
    WriteBackup,
    StageReplacement,
    PublishJournal,
    CommitReplacement,
    RestoreBackup,
    ListBackups,
    CleanupJournal,
    CleanupStage
}

public enum ProtectedDataRecordFailureKind
{
    AccessDenied,
    StorageFailure
}

/// <summary>
/// Safe, operation-level diagnostics for protected app-owned record I/O.
/// The message omits paths and record contents; the inner exception remains
/// available to trusted diagnostics when lower-level details are needed.
/// </summary>
public sealed class ProtectedDataRecordException : IOException
{
    internal ProtectedDataRecordException(
        ProtectedDataRecordOperation operation,
        ProtectedDataRecordFailureKind failureKind,
        Exception innerException,
        bool recordCommitConfirmed,
        bool recordMayHaveChanged,
        bool recoveryPending)
        : base(BuildMessage(operation, failureKind, recordCommitConfirmed, recordMayHaveChanged, recoveryPending), innerException)
    {
        Operation = operation;
        FailureKind = failureKind;
        RecordCommitConfirmed = recordCommitConfirmed;
        RecordMayHaveChanged = recordMayHaveChanged;
        RecoveryPending = recoveryPending;
    }

    public ProtectedDataRecordOperation Operation { get; }
    public ProtectedDataRecordFailureKind FailureKind { get; }
    public bool RecordCommitConfirmed { get; }
    public bool RecordMayHaveChanged { get; }
    public bool RecoveryPending { get; }

    public ProtectedDataRecordException? CleanupFailure { get; internal set; }

    internal static ProtectedDataRecordException FromStorageError(
        ProtectedDataRecordOperation operation,
        Exception exception,
        bool recordCommitConfirmed = false,
        bool recordMayHaveChanged = false,
        bool recoveryPending = false)
    {
        var kind = exception is UnauthorizedAccessException or System.Security.SecurityException
            ? ProtectedDataRecordFailureKind.AccessDenied
            : ProtectedDataRecordFailureKind.StorageFailure;
        return new ProtectedDataRecordException(operation, kind, exception,
            recordCommitConfirmed, recordMayHaveChanged, recoveryPending);
    }

    private static string BuildMessage(
        ProtectedDataRecordOperation operation,
        ProtectedDataRecordFailureKind failureKind,
        bool recordCommitConfirmed,
        bool recordMayHaveChanged,
        bool recoveryPending)
    {
        var cause = failureKind == ProtectedDataRecordFailureKind.AccessDenied
            ? "Access was denied"
            : "A storage or file-system error occurred";
        var outcome = operation switch
        {
            ProtectedDataRecordOperation.ValidatePath => "The protected record path could not be safely inspected; no record was changed.",
            ProtectedDataRecordOperation.AcquireLock => "The record store could not be locked; no record was changed.",
            ProtectedDataRecordOperation.InspectRecord => "The current record state could not be confirmed; no record was changed.",
            ProtectedDataRecordOperation.InspectBackups => "The backup state could not be confirmed; no record was changed.",
            ProtectedDataRecordOperation.ReadRecord => "The saved record could not be read; its contents were not changed.",
            ProtectedDataRecordOperation.ResolveJournal => recoveryPending
                ? "An interrupted record change could not be resolved. The recovery journal and verified backup remain available for another access attempt."
                : "Recovery state could not be resolved; no new record change was made.",
            ProtectedDataRecordOperation.WriteBackup => "The pre-change backup could not be written or verified; the current record was not changed.",
            ProtectedDataRecordOperation.StageReplacement => "The replacement could not be staged; the current record was not changed.",
            ProtectedDataRecordOperation.PublishJournal => "The recovery journal could not be published; the current record was not changed.",
            ProtectedDataRecordOperation.CommitReplacement when recordCommitConfirmed =>
                "The replacement was committed and verified, but final cleanup failed.",
            ProtectedDataRecordOperation.CommitReplacement when recordMayHaveChanged || recoveryPending =>
                "The replacement outcome is not confirmed; the recovery journal will be checked on the next access before another write.",
            ProtectedDataRecordOperation.CommitReplacement => "The replacement was not committed; the current record was not changed.",
            ProtectedDataRecordOperation.RestoreBackup when recordMayHaveChanged =>
                "The restore outcome could not be confirmed. The selected verified backup remains available; inspect the target before retrying.",
            ProtectedDataRecordOperation.RestoreBackup =>
                "The backup could not be restored. The selected backup remains available and an existing target was not overwritten.",
            ProtectedDataRecordOperation.ListBackups => "Verified backup metadata could not be listed; no backup contents were returned.",
            ProtectedDataRecordOperation.CleanupJournal when recordCommitConfirmed =>
                "The record change was committed and verified. Recovery-journal cleanup remains pending and will be retried on the next access.",
            ProtectedDataRecordOperation.CleanupJournal =>
                "Recovery-journal cleanup failed. The journal remains available for the next access to resolve safely.",
            ProtectedDataRecordOperation.CleanupStage when recordCommitConfirmed =>
                "The record change was committed and verified, but a temporary file could not be removed.",
            ProtectedDataRecordOperation.CleanupStage =>
                "A temporary file could not be removed; the primary operation outcome is reported separately.",
            _ => "The protected record was not intentionally reset or overwritten."
        };

        return $"{cause} during protected NeoBabylon record operation {operation}. {outcome}";
    }
}
