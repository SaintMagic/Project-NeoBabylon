namespace NeoBabylon.Core;

/// <summary>Fixed app-owned protected records supported by explicit recovery.</summary>
public enum ProtectedDataRecordKey
{
    Projects,
    ForkBookmarks
}

/// <summary>Verified backup metadata; record contents are never returned.</summary>
public sealed record ProtectedDataBackupMetadata(
    string Sha256,
    long ByteLength,
    DateTimeOffset LastWriteTimeUtc,
    bool CanRestore);

/// <summary>Verified backup metadata and the locked live recovery state.</summary>
public sealed record ProtectedDataRecordRecoveryStatus(
    ProtectedDataRecordKey RecordKey,
    IReadOnlyList<ProtectedDataBackupMetadata> VerifiedBackups,
    bool TargetExists,
    bool JournalExists,
    bool CanRestoreMissing);

/// <summary>
/// Lists and restores only NeoBabylon-owned project and fork-bookmark records
/// beneath the application's Data directory.
/// </summary>
public sealed class ProtectedDataRecordRecoveryService
{
    private readonly string _dataRoot;

    public ProtectedDataRecordRecoveryService(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        _dataRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot));
        if (!string.Equals(Path.GetFileName(_dataRoot), "Data", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Protected record recovery requires the application Data directory.", nameof(dataRoot));
        }
    }

    public ProtectedDataRecordRecoveryStatus ListVerifiedBackups(ProtectedDataRecordKey recordKey)
    {
        var record = Resolve(recordKey);
        return ProtectedDataRecordFile.ListVerifiedBackups(record.Path, recordKey, record.Validate);
    }

    public void RestoreMissing(ProtectedDataRecordKey recordKey, string expectedBackupSha256)
    {
        var record = Resolve(recordKey);
        ProtectedDataRecordFile.RestoreMissingFromBackup(record.Path, expectedBackupSha256, record.Validate);
    }

    private RecordBinding Resolve(ProtectedDataRecordKey recordKey) => recordKey switch
    {
        ProtectedDataRecordKey.Projects => new RecordBinding(
            Path.Combine(_dataRoot, "NeoBabylon", "projects.json"),
            WorkspaceProjectRegistry.ValidateProtectedRecord),
        ProtectedDataRecordKey.ForkBookmarks => new RecordBinding(
            Path.Combine(_dataRoot, "NeoBabylon", "fork-bookmarks.json"),
            ForkBookmarkStore.ValidateProtectedRecord),
        _ => throw new ArgumentOutOfRangeException(nameof(recordKey), recordKey, "Only fixed NeoBabylon protected record keys can be recovered.")
    };

    private sealed record RecordBinding(string Path, Action<byte[]> Validate);
}
