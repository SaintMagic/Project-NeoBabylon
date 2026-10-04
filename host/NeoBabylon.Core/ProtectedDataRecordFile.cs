using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

[assembly: InternalsVisibleTo("NeoBabylon.Phase1A.Tests")]

namespace NeoBabylon.Core;

internal enum ProtectedDataWriteCheckpoint
{
    BackupStageWrite,
    ReplacementStageWrite,
    RecordCommit
}

/// <summary>
/// Crash-recoverable replacement for NeoBabylon-owned records below Data\NeoBabylon.
/// This does not snapshot CodexHome, WebView2, or any other runtime-owned Data.
/// </summary>
public static class ProtectedDataRecordFile
{
    private const int JournalSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static byte[]? Read(string recordPath, Action<byte[]> validate)
    {
        ArgumentNullException.ThrowIfNull(validate);
        var paths = WithStorageDiagnostics(ProtectedDataRecordOperation.ValidatePath, () => Resolve(recordPath));
        using var guard = WithStorageDiagnostics(ProtectedDataRecordOperation.AcquireLock, () => Lock(paths));
        var hasPendingJournal = WithStorageDiagnostics(ProtectedDataRecordOperation.InspectRecord,
            () => FileExists(paths.Journal));
        WithStorageDiagnostics(ProtectedDataRecordOperation.ResolveJournal, () =>
        {
            Recover(paths, validate, validate);
            return true;
        }, recoveryPending: hasPendingJournal);
        if (!WithStorageDiagnostics(ProtectedDataRecordOperation.InspectRecord, () => FileExists(paths.Record)))
        {
            WithStorageDiagnostics(ProtectedDataRecordOperation.InspectBackups, () =>
            {
                RequireNoOrphanedBackup(paths);
                return true;
            });
            return null;
        }
        var bytes = WithStorageDiagnostics(ProtectedDataRecordOperation.ReadRecord,
            () => File.ReadAllBytes(paths.Record));
        validate(bytes);
        return bytes;
    }

    public static void Replace(string recordPath, byte[] replacement, Action<byte[]> validateExisting,
        Action<byte[]> validateReplacement)
        => Replace(recordPath, replacement, validateExisting, validateReplacement, null);

    // Per-call fault seam for isolated host checks; ordinary callers use the public four-argument API.
    internal static void Replace(string recordPath, byte[] replacement, Action<byte[]> validateExisting,
        Action<byte[]> validateReplacement, Action<ProtectedDataWriteCheckpoint>? checkpoint)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(validateExisting);
        ArgumentNullException.ThrowIfNull(validateReplacement);
        validateReplacement(replacement);
        var paths = WithStorageDiagnostics(ProtectedDataRecordOperation.ValidatePath, () => Resolve(recordPath));

        using var guard = WithStorageDiagnostics(ProtectedDataRecordOperation.AcquireLock, () => Lock(paths));
        var hasPendingJournal = WithStorageDiagnostics(ProtectedDataRecordOperation.InspectRecord,
            () => FileExists(paths.Journal));
        WithStorageDiagnostics(ProtectedDataRecordOperation.ResolveJournal, () =>
        {
            Recover(paths, validateExisting, validateReplacement);
            return true;
        }, recoveryPending: hasPendingJournal);
        var original = WithStorageDiagnostics(ProtectedDataRecordOperation.InspectRecord, () =>
            FileExists(paths.Record) ? File.ReadAllBytes(paths.Record) : null);
        if (original is null)
        {
            WithStorageDiagnostics(ProtectedDataRecordOperation.InspectBackups, () =>
            {
                RequireNoOrphanedBackup(paths);
                return true;
            });
        }
        if (original is not null) validateExisting(original);
        var originalHash = original is null ? null : Digest(original);
        if (original is not null)
        {
            WithStorageDiagnostics(ProtectedDataRecordOperation.WriteBackup,
                () => WriteVerifiedBackup(paths, originalHash!, original, checkpoint));
        }

        var replacementHash = Digest(replacement);
        var stage = paths.Record + ".stage-" + Guid.NewGuid().ToString("N");
        var journalStage = paths.Journal + ".stage-" + Guid.NewGuid().ToString("N");
        var journalPublished = false;
        var recordCommitConfirmed = false;
        Exception? primaryFailure = null;
        try
        {
            WithStorageDiagnostics(ProtectedDataRecordOperation.StageReplacement, () =>
            {
                WriteDurable(stage, replacement, checkpoint, ProtectedDataWriteCheckpoint.ReplacementStageWrite);
                RequireDigest(stage, replacementHash);
                if (original is not null) RequireDigest(paths.Record, originalHash!);
                return true;
            });

            var journal = new PendingReplacement(JournalSchemaVersion, "NeoBabylon", originalHash, replacementHash);
            WithStorageDiagnostics(ProtectedDataRecordOperation.PublishJournal, () =>
            {
                WriteDurable(journalStage, JsonSerializer.SerializeToUtf8Bytes(journal, JsonOptions));
                RejectReparseComponents(paths.Journal);
                File.Move(journalStage, paths.Journal);
                journalPublished = true;
                return true;
            }, recoveryPending: true);

            WithStorageDiagnostics(ProtectedDataRecordOperation.CommitReplacement, () =>
            {
                if (original is not null) RequireDigest(paths.Record, originalHash!);
                checkpoint?.Invoke(ProtectedDataWriteCheckpoint.RecordCommit);
                RejectReparseComponents(paths.Record);
                if (original is null) File.Move(stage, paths.Record);
                else File.Replace(stage, paths.Record, null);
                RequireDigest(paths.Record, replacementHash);
                recordCommitConfirmed = true;
                return true;
            }, recordMayHaveChanged: true, recoveryPending: true);

            WithStorageDiagnostics(ProtectedDataRecordOperation.CleanupJournal,
                () =>
                {
                    File.Delete(paths.Journal);
                    journalPublished = false;
                    return true;
                },
                recordCommitConfirmed: true, recoveryPending: true);
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
            throw;
        }
        finally
        {
            // A published journal and verified backup remain available if replacement was interrupted.
            if (journalPublished)
                CleanupStagingFiles(primaryFailure, recordCommitConfirmed, recoveryPending: true, journalStage);
            else
                CleanupStagingFiles(primaryFailure, recordCommitConfirmed, recoveryPending: false, stage, journalStage);
        }
    }

    /// <summary>Explicit non-overwriting recovery from an immutable, content-addressed backup.</summary>
    internal static void RestoreMissingFromBackup(string recordPath, string originalSha256, Action<byte[]> validate)
    {
        ArgumentNullException.ThrowIfNull(validate);
        if (!IsDigest(originalSha256)) throw new InvalidDataException("Invalid protected-record backup identity.");
        var paths = WithStorageDiagnostics(ProtectedDataRecordOperation.ValidatePath, () => Resolve(recordPath));
        using var guard = WithStorageDiagnostics(ProtectedDataRecordOperation.AcquireLock, () => Lock(paths));
        if (WithStorageDiagnostics(ProtectedDataRecordOperation.InspectRecord,
                () => FileExists(paths.Record) || FileExists(paths.Journal)))
            throw new InvalidOperationException("Protected-record restore requires a missing record and no pending migration.");
        RestoreMissing(paths, originalSha256, validate);
    }

    internal static ProtectedDataRecordRecoveryStatus ListVerifiedBackups(
        string recordPath, ProtectedDataRecordKey recordKey, Action<byte[]> validate)
    {
        ArgumentNullException.ThrowIfNull(validate);
        var paths = WithStorageDiagnostics(ProtectedDataRecordOperation.ValidatePath, () => Resolve(recordPath));
        using var guard = WithStorageDiagnostics(ProtectedDataRecordOperation.AcquireLock, () => Lock(paths));
        return WithStorageDiagnostics(ProtectedDataRecordOperation.ListBackups, () =>
        {
            var directory = Path.Combine(paths.DataRoot, "Backups", "NeoBabylon", paths.RecordKey);
            RejectReparseComponents(directory);
            var targetExists = FileExists(paths.Record);
            var journalExists = FileExists(paths.Journal);
            if (!DirectoryExists(directory))
            {
                return new ProtectedDataRecordRecoveryStatus(recordKey,
                    Array.Empty<ProtectedDataBackupMetadata>(), targetExists, journalExists, false);
            }

            var backups = new List<ProtectedDataBackupMetadata>();
            foreach (var path in Directory.EnumerateFiles(directory, "*.bin", SearchOption.TopDirectoryOnly))
            {
                RejectReparseComponents(path);
                var hash = Path.GetFileNameWithoutExtension(path);
                if (!IsDigest(hash))
                    throw new InvalidDataException("Protected-record backup name is invalid; no backup contents were returned.");

                var bytes = File.ReadAllBytes(path);
                if (!string.Equals(Digest(bytes), hash, StringComparison.Ordinal))
                    throw new InvalidDataException("Protected-record backup failed SHA-256 verification; no backup contents were returned.");
                var canRestore = IsRecordCompatible(bytes, validate);
                var lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
                backups.Add(new ProtectedDataBackupMetadata(
                    hash,
                    bytes.LongLength,
                    new DateTimeOffset(DateTime.SpecifyKind(lastWriteTimeUtc, DateTimeKind.Utc)),
                    canRestore));
            }

            var orderedBackups = (IReadOnlyList<ProtectedDataBackupMetadata>)backups
                .OrderByDescending(backup => backup.LastWriteTimeUtc)
                .ThenBy(backup => backup.Sha256, StringComparer.Ordinal)
                .ToArray();
            var canRestoreMissing = !targetExists && !journalExists && orderedBackups.Any(backup => backup.CanRestore);
            return new ProtectedDataRecordRecoveryStatus(
                recordKey, orderedBackups, targetExists, journalExists, canRestoreMissing);
        });
    }

    private static void Recover(RecordPaths paths, Action<byte[]> validateOriginal,
        Action<byte[]> validateReplacement)
    {
        if (!FileExists(paths.Journal)) return;
        var journal = JsonSerializer.Deserialize<PendingReplacement>(File.ReadAllBytes(paths.Journal), JsonOptions)
            ?? throw new InvalidDataException("Protected-record migration journal is empty.");
        if (journal.SchemaVersion != JournalSchemaVersion
            || journal.Product != "NeoBabylon"
            || !IsDigest(journal.ReplacementSha256)
            || (journal.OriginalSha256 is not null && !IsDigest(journal.OriginalSha256)))
            throw new InvalidDataException("Protected-record migration journal is invalid or unsupported.");

        if (journal.OriginalSha256 is not null)
            RequireDigest(BackupPath(paths, journal.OriginalSha256), journal.OriginalSha256);

        var replacementIsLive = false;
        if (FileExists(paths.Record))
        {
            var bytes = File.ReadAllBytes(paths.Record);
            var currentHash = Digest(bytes);
            if (currentHash != journal.OriginalSha256 && currentHash != journal.ReplacementSha256)
                throw new InvalidDataException("Protected record diverged during migration; backup retained for explicit recovery.");
            if (currentHash == journal.ReplacementSha256)
            {
                validateReplacement(bytes);
                replacementIsLive = true;
            }
            else validateOriginal(bytes);
        }
        else if (journal.OriginalSha256 is not null)
        {
            RestoreMissing(paths, journal.OriginalSha256, validateOriginal, recoveryPending: true);
        }
        else
        {
            RecoverFirstWriteReplacement(paths, journal.ReplacementSha256, validateReplacement);
            replacementIsLive = true;
        }

        // Old bytes mean the commit was interrupted; new bytes mean it completed.
        WithStorageDiagnostics(ProtectedDataRecordOperation.CleanupJournal,
            () => File.Delete(paths.Journal),
            recordCommitConfirmed: replacementIsLive, recoveryPending: true);
    }

    private static void RecoverFirstWriteReplacement(RecordPaths paths, string replacementSha256,
        Action<byte[]> validateReplacement)
    {
        var recordDirectory = Path.GetDirectoryName(paths.Record)!;
        var stagePattern = Path.GetFileName(paths.Record) + ".stage-*";
        string? verifiedStage = null;
        foreach (var candidate in Directory.EnumerateFiles(recordDirectory, stagePattern, SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            if (!IsReplacementStage(paths.Record, candidate)) continue;

            RejectReparseComponents(candidate);
            var bytes = File.ReadAllBytes(candidate);
            if (!string.Equals(Digest(bytes), replacementSha256, StringComparison.Ordinal)) continue;

            validateReplacement(bytes);
            verifiedStage = candidate;
            break;
        }

        if (verifiedStage is null)
        {
            throw new InvalidDataException(
                "Protected first-write migration is unresolved: no exact staged replacement matches the journal SHA-256. The journal and stages were preserved; no default record was created.");
        }

        WithStorageDiagnostics(ProtectedDataRecordOperation.CommitReplacement, () =>
        {
            if (FileExists(paths.Record))
                throw new InvalidDataException("Protected record appeared during first-write recovery; the journal and stage were preserved.");

            RejectReparseComponents(verifiedStage);
            RejectReparseComponents(paths.Record);
            File.Move(verifiedStage, paths.Record);

            RejectReparseComponents(paths.Record);
            var publishedBytes = File.ReadAllBytes(paths.Record);
            if (!string.Equals(Digest(publishedBytes), replacementSha256, StringComparison.Ordinal))
                throw new InvalidDataException("Recovered protected record failed SHA-256 readback; the migration journal was preserved.");
            validateReplacement(publishedBytes);
            return true;
        }, recordMayHaveChanged: true, recoveryPending: true);
    }

    private static bool IsReplacementStage(string recordPath, string candidatePath)
    {
        var expectedPrefix = Path.GetFileName(recordPath) + ".stage-";
        var candidateName = Path.GetFileName(candidatePath);
        if (!candidateName.StartsWith(expectedPrefix, StringComparison.Ordinal)) return false;
        var suffix = candidateName[expectedPrefix.Length..];
        return suffix.Length == 32
            && suffix.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static void RestoreMissing(RecordPaths paths, string originalSha256, Action<byte[]> validate,
        bool recoveryPending = false)
    {
        var backup = BackupPath(paths, originalSha256);
        WithStorageDiagnostics(ProtectedDataRecordOperation.RestoreBackup,
            () => RequireDigest(backup, originalSha256), recoveryPending: recoveryPending);
        var bytes = WithStorageDiagnostics(ProtectedDataRecordOperation.RestoreBackup,
            () => File.ReadAllBytes(backup), recoveryPending: recoveryPending);
        validate(bytes);
        var stage = paths.Record + ".restore-" + Guid.NewGuid().ToString("N");
        var restoreConfirmed = false;
        Exception? primaryFailure = null;
        try
        {
            WithStorageDiagnostics(ProtectedDataRecordOperation.RestoreBackup, () =>
            {
                WriteDurable(stage, bytes);
                RequireDigest(stage, originalSha256);
                return true;
            }, recoveryPending: recoveryPending);
            WithStorageDiagnostics(ProtectedDataRecordOperation.RestoreBackup, () =>
            {
                RejectReparseComponents(paths.Record);
                File.Move(stage, paths.Record);
                RequireDigest(paths.Record, originalSha256);
                restoreConfirmed = true;
                return true;
            }, recordMayHaveChanged: true, recoveryPending: recoveryPending);
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
            throw;
        }
        finally
        {
            CleanupStagingFiles(primaryFailure, restoreConfirmed, recoveryPending, stage);
        }
    }

    private static void WriteVerifiedBackup(RecordPaths paths, string sha256, byte[] bytes,
        Action<ProtectedDataWriteCheckpoint>? checkpoint)
    {
        var destination = BackupPath(paths, sha256);
        RejectReparseComponents(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        RejectReparseComponents(destination);
        if (FileExists(destination))
        {
            RequireDigest(destination, sha256);
            return;
        }

        var stage = destination + ".stage-" + Guid.NewGuid().ToString("N");
        Exception? primaryFailure = null;
        try
        {
            WriteDurable(stage, bytes, checkpoint, ProtectedDataWriteCheckpoint.BackupStageWrite);
            RequireDigest(stage, sha256);
            try
            {
                File.Move(stage, destination);
            }
            catch (IOException) when (FileExists(destination))
            {
                // Another writer may have installed the same content-addressed backup.
            }
            RequireDigest(destination, sha256);
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
            throw;
        }
        finally
        {
            CleanupStagingFiles(primaryFailure, recordCommitConfirmed: false, recoveryPending: false, stage);
        }
    }

    private static FileStream Lock(RecordPaths paths)
    {
        var recordDirectory = Path.GetDirectoryName(paths.Record)!;
        RejectReparseComponents(recordDirectory);
        Directory.CreateDirectory(recordDirectory);
        RejectReparseComponents(recordDirectory);
        RejectReparseComponents(paths.Record);
        RejectReparseComponents(paths.Journal);
        var lockPath = paths.Record + ".migration.lock";
        RejectReparseComponents(lockPath);
        return new FileStream(lockPath, FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
    }

    private static void WriteDurable(string path, byte[] bytes,
        Action<ProtectedDataWriteCheckpoint>? checkpoint = null,
        ProtectedDataWriteCheckpoint writeCheckpoint = ProtectedDataWriteCheckpoint.ReplacementStageWrite)
    {
        RejectReparseComponents(path);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        if (checkpoint is null) stream.Write(bytes);
        else
        {
            // Exercise cleanup after a partially written stage, not only an unopened destination.
            var prefixLength = Math.Max(1, bytes.Length / 2);
            stream.Write(bytes.AsSpan(0, prefixLength));
            checkpoint(writeCheckpoint);
            stream.Write(bytes.AsSpan(prefixLength));
        }
        stream.Flush(flushToDisk: true);
    }

    private static void RequireDigest(string path, string expected)
    {
        RejectReparseComponents(path);
        if (!FileExists(path) || Digest(File.ReadAllBytes(path)) != expected)
            throw new InvalidDataException("Protected-record backup or staged bytes failed readback verification.");
    }

    private static string BackupPath(RecordPaths paths, string sha256) =>
        Path.Combine(paths.DataRoot, "Backups", "NeoBabylon", paths.RecordKey, sha256 + ".bin");

    private static void RequireNoOrphanedBackup(RecordPaths paths)
    {
        var backupDirectory = Path.Combine(paths.DataRoot, "Backups", "NeoBabylon", paths.RecordKey);
        RejectReparseComponents(backupDirectory);
        if (DirectoryExists(backupDirectory)
            && Directory.EnumerateFileSystemEntries(backupDirectory).Any())
            throw new InvalidDataException("Protected record is missing but a backup exists; explicit verified restore is required.");
    }

    private static bool IsRecordCompatible(byte[] bytes, Action<byte[]> validate)
    {
        try
        {
            validate(bytes);
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException
            or ArgumentException or NotSupportedException or PathTooLongException or FormatException or OverflowException)
        {
            return false;
        }
    }

    private static T WithStorageDiagnostics<T>(
        ProtectedDataRecordOperation operation,
        Func<T> action,
        bool recordCommitConfirmed = false,
        bool recordMayHaveChanged = false,
        bool recoveryPending = false)
    {
        try
        {
            return action();
        }
        catch (ProtectedDataRecordException)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw ProtectedDataRecordException.FromStorageError(operation, exception,
                recordCommitConfirmed, recordMayHaveChanged, recoveryPending);
        }
        catch (System.Security.SecurityException exception)
        {
            throw ProtectedDataRecordException.FromStorageError(operation, exception,
                recordCommitConfirmed, recordMayHaveChanged, recoveryPending);
        }
        catch (IOException exception)
        {
            throw ProtectedDataRecordException.FromStorageError(operation, exception,
                recordCommitConfirmed, recordMayHaveChanged, recoveryPending);
        }
    }

    private static void WithStorageDiagnostics(
        ProtectedDataRecordOperation operation,
        Action action,
        bool recordCommitConfirmed = false,
        bool recordMayHaveChanged = false,
        bool recoveryPending = false) =>
        WithStorageDiagnostics(operation, () =>
        {
            action();
            return true;
        }, recordCommitConfirmed, recordMayHaveChanged, recoveryPending);

    private static void CleanupStagingFiles(Exception? primaryFailure,
        bool recordCommitConfirmed, bool recoveryPending, params string[] paths)
    {
        Exception? firstCleanupFailure = null;
        foreach (var path in paths)
        {
            try
            {
                WithStorageDiagnostics(ProtectedDataRecordOperation.CleanupStage, () =>
                {
                    if (FileExists(path)) File.Delete(path);
                    return true;
                }, recordCommitConfirmed, recoveryPending: recoveryPending);
            }
            catch (Exception cleanupFailure)
            {
                if (primaryFailure is ProtectedDataRecordException protectedFailure
                    && cleanupFailure is ProtectedDataRecordException protectedCleanupFailure)
                {
                    protectedFailure.CleanupFailure ??= protectedCleanupFailure;
                }
                else if (primaryFailure is not null)
                {
                    primaryFailure.Data[nameof(ProtectedDataRecordException.CleanupFailure)] = cleanupFailure;
                }
                else if (firstCleanupFailure is null)
                {
                    firstCleanupFailure = cleanupFailure;
                }
                else
                {
                    firstCleanupFailure.Data["AdditionalProtectedRecordCleanupFailure"] = cleanupFailure;
                }
            }
        }

        if (primaryFailure is null && firstCleanupFailure is not null) throw firstCleanupFailure;
    }

    private static bool FileExists(string path)
    {
        var attributes = GetAttributesIfPresent(path);
        if (attributes is null) return false;
        if ((attributes.Value & FileAttributes.Directory) != 0)
            throw new InvalidDataException("Protected record file path points to a directory.");
        return true;
    }

    private static bool DirectoryExists(string path)
    {
        var attributes = GetAttributesIfPresent(path);
        if (attributes is null) return false;
        if ((attributes.Value & FileAttributes.Directory) == 0)
            throw new InvalidDataException("Protected record backup directory path points to a file.");
        return true;
    }

    private static FileAttributes? GetAttributesIfPresent(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static RecordPaths Resolve(string recordPath)
    {
        var full = Path.GetFullPath(recordPath);
        var parent = Path.GetDirectoryName(full)
            ?? throw new InvalidDataException("Protected record has no parent directory.");
        DirectoryInfo? cursor = new DirectoryInfo(parent);
        while (cursor is not null && !string.Equals(cursor.Name, "NeoBabylon", StringComparison.OrdinalIgnoreCase))
            cursor = cursor.Parent;
        var productRoot = cursor?.FullName;
        var dataDirectory = cursor?.Parent;
        if (productRoot is null || dataDirectory is null
            || !string.Equals(dataDirectory.Name, "Data", StringComparison.OrdinalIgnoreCase)
            || !full.StartsWith(productRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Protected record must be below <application root>\\Data\\NeoBabylon.");
        RejectReparseComponents(full);
        var relative = Path.GetRelativePath(productRoot, full).ToUpperInvariant();
        var recordKey = Digest(Encoding.UTF8.GetBytes(relative));
        return new RecordPaths(full, full + ".migration.json", dataDirectory.FullName, recordKey);
    }

    private static void RejectReparseComponents(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        var current = root;
        foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, part);
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Protected Data migration cannot traverse a reparse point.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static bool IsDigest(string? value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private sealed record RecordPaths(string Record, string Journal, string DataRoot, string RecordKey);
    private sealed record PendingReplacement(int SchemaVersion, string Product, string? OriginalSha256,
        string ReplacementSha256);
}
