using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace NeoBabylon.Core;

// A prepared-disabled product binding, not runtime registration or approval to invoke a tool.
public sealed record GeneratedToolPreparedBindingRecord(
    int SchemaVersion,
    string ToolId,
    string CandidateContentIdentity,
    string ReviewIdentity,
    string ReviewInterfaceVersion,
    string ReviewRecordSha256,
    string State,
    string ActivationState,
    string CallableRoute,
    string Note,
    DateTimeOffset RecordedAtUtc,
    int Sequence,
    string PreviousRecordSha256,
    string RecordSha256);

// This store has no activation, MCP configuration, execution, or candidate-deletion method.
public static class GeneratedToolIntegrationStore
{
    public const string PreparedDisabled = "prepared-disabled";
    public const string Revoked = "revoked";
    public const string Cleaned = "cleaned";
    public const string Disabled = "disabled";
    public const string NoCallableRoute = "none";

    private const int SchemaVersion = 1;
    private const int MaximumRecordBytes = 8 * 1024;
    private const int MaximumRecordsPerTool = 512;
    private const string CandidateIdentityPrefix = "candidate-v1:sha256:";
    private const string ReviewIdentityPrefix = "review-v1:sha256:";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static GeneratedToolPreparedBindingRecord PrepareDisabledBinding(
        string applicationRoot, string toolId, string expectedContentIdentity,
        string expectedReviewIdentity, string note)
    {
        ValidateNote(note);
        var review = RequireCurrentReview(applicationRoot, toolId, expectedContentIdentity, expectedReviewIdentity);
        var history = History(applicationRoot, toolId);
        // Reserve both denial transitions before accepting a new preparation.
        if (history.Count > MaximumRecordsPerTool - 3
            || history.Any(record => record.CandidateContentIdentity == expectedContentIdentity)
            || history.Count != 0 && history[^1].State != Cleaned)
        {
            throw new InvalidDataException("Candidate already has a binding or its prior binding is not cleaned.");
        }

        var record = NewRecord(toolId, expectedContentIdentity, review.ReviewIdentity,
            review.ReviewInterfaceVersion, review.RecordSha256, PreparedDisabled, note, history);
        return Publish(applicationRoot, record, history,
            () =>
            {
                var currentReview = RequireCurrentReview(applicationRoot, toolId,
                    expectedContentIdentity, expectedReviewIdentity);
                if (!string.Equals(currentReview.RecordSha256, review.RecordSha256, StringComparison.Ordinal)
                    || !string.Equals(currentReview.ReviewInterfaceVersion, review.ReviewInterfaceVersion,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Prepared binding review evidence changed before publication.");
                }
            });
    }

    public static GeneratedToolPreparedBindingRecord Revoke(
        string applicationRoot, string toolId, string expectedContentIdentity,
        string expectedReviewIdentity, string expectedCurrentRecordSha256, string note) =>
        AppendTransition(applicationRoot, toolId, expectedContentIdentity, expectedReviewIdentity,
            expectedCurrentRecordSha256, PreparedDisabled, Revoked, note);

    // Logical cleanup is an append-only tombstone. Candidate, review, and binding evidence remain.
    public static GeneratedToolPreparedBindingRecord Cleanup(
        string applicationRoot, string toolId, string expectedContentIdentity,
        string expectedReviewIdentity, string expectedCurrentRecordSha256, string note) =>
        AppendTransition(applicationRoot, toolId, expectedContentIdentity, expectedReviewIdentity,
            expectedCurrentRecordSha256, Revoked, Cleaned, note);

    public static GeneratedToolPreparedBindingRecord? ReadCurrent(string applicationRoot, string toolId)
    {
        var history = History(applicationRoot, toolId);
        if (history.Count == 0) return null;
        var current = history[^1];
        if (current.State == PreparedDisabled)
        {
            var review = RequireCurrentReview(applicationRoot, toolId,
                current.CandidateContentIdentity, current.ReviewIdentity);
            if (!string.Equals(review.RecordSha256, current.ReviewRecordSha256, StringComparison.Ordinal)
                || !string.Equals(review.ReviewInterfaceVersion, current.ReviewInterfaceVersion, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Prepared binding review evidence changed.");
            }
        }
        return current;
    }

    // Recovery discovery uses only the binding store; candidate and review bytes may be missing.
    public static IReadOnlyList<string> ListToolIds(string applicationRoot)
    {
        const int maximumTools = 512;
        var root = Path.Combine(GeneratedToolCandidatePath.Root(applicationRoot), "PreparedBindings");
        GeneratedToolCandidatePath.RejectReparseComponents(root);
        if (File.Exists(root))
        {
            throw new InvalidDataException("Prepared bindings root is not a directory.");
        }
        if (!Directory.Exists(root)) return [];

        var entries = Directory.EnumerateFileSystemEntries(root).Take(maximumTools + 2).ToArray();
        if (entries.Length > maximumTools + 1)
        {
            throw new InvalidDataException("Prepared binding tool count exceeds its limit.");
        }
        var toolIds = new List<string>(entries.Length);
        foreach (var entry in entries)
        {
            GeneratedToolCandidatePath.RejectReparseComponents(entry);
            var name = Path.GetFileName(entry);
            if (name == ".staging")
            {
                if (!Directory.Exists(entry))
                {
                    throw new InvalidDataException("Prepared binding staging path is not a directory.");
                }
                continue;
            }
            ValidateToolId(name);
            if (!Directory.Exists(entry))
            {
                throw new InvalidDataException("Prepared binding tool entry is not a directory.");
            }
            // An empty directory is not a published binding; its record is checked on history read.
            if (Directory.EnumerateFileSystemEntries(entry).Any()) toolIds.Add(name);
        }
        if (toolIds.Count > maximumTools)
        {
            throw new InvalidDataException("Prepared binding tool count exceeds its limit.");
        }
        toolIds.Sort(StringComparer.Ordinal);
        return toolIds.ToArray();
    }

    public static IReadOnlyList<GeneratedToolPreparedBindingRecord> History(string applicationRoot, string toolId)
    {
        var directory = BindingDirectory(applicationRoot, toolId);
        if (File.Exists(directory))
        {
            throw new InvalidDataException("Prepared binding history path is not a directory.");
        }
        if (!Directory.Exists(directory)) return [];

        var entries = Directory.EnumerateFileSystemEntries(directory).Take(MaximumRecordsPerTool + 1).ToArray();
        if (entries.Length > MaximumRecordsPerTool)
        {
            throw new InvalidDataException("Prepared binding history exceeds its bounded record count.");
        }
        var records = new List<GeneratedToolPreparedBindingRecord>(entries.Length);
        foreach (var entry in entries)
        {
            GeneratedToolCandidatePath.RejectReparseComponents(entry);
            if (!File.Exists(entry))
            {
                throw new InvalidDataException("Prepared binding history contains a non-record entry.");
            }
            var bytes = ReadBounded(entry);
            GeneratedToolPreparedBindingRecord record;
            try
            {
                using var document = JsonDocument.Parse(bytes);
                GeneratedToolCandidateValidator.RejectDuplicateProperties(document.RootElement);
                record = JsonSerializer.Deserialize<GeneratedToolPreparedBindingRecord>(bytes, JsonOptions)
                    ?? throw new InvalidDataException("Prepared binding record is empty.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Prepared binding record is malformed.", exception);
            }
            ValidateRecord(record, toolId, Path.GetFileName(entry));
            records.Add(record);
        }
        records.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));

        var previousHash = string.Empty;
        var seenCandidates = new HashSet<string>(StringComparer.Ordinal);
        GeneratedToolPreparedBindingRecord? previous = null;
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            if (record.Sequence != index + 1
                || !string.Equals(record.PreviousRecordSha256, previousHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Prepared binding history has a missing or unlinked record.");
            }
            if (previous is null || previous.State == Cleaned)
            {
                if (record.State != PreparedDisabled || !seenCandidates.Add(record.CandidateContentIdentity))
                {
                    throw new InvalidDataException("Prepared binding history has a duplicate or invalid preparation.");
                }
            }
            else if ((previous.State == PreparedDisabled ? Revoked : Cleaned) != record.State
                || !SameBinding(previous, record))
            {
                throw new InvalidDataException("Prepared binding history has an invalid transition.");
            }
            previous = record;
            previousHash = record.RecordSha256;
        }
        return records.ToArray();
    }

    private static GeneratedToolPreparedBindingRecord AppendTransition(
        string applicationRoot, string toolId, string expectedContentIdentity,
        string expectedReviewIdentity, string expectedCurrentRecordSha256,
        string requiredState, string nextState, string note)
    {
        ValidateNote(note);
        var history = History(applicationRoot, toolId);
        if (history.Count == 0 || history.Count >= MaximumRecordsPerTool
            || history[^1].State != requiredState
            || !string.Equals(history[^1].CandidateContentIdentity, expectedContentIdentity, StringComparison.Ordinal)
            || !string.Equals(history[^1].ReviewIdentity, expectedReviewIdentity, StringComparison.Ordinal)
            || !string.Equals(history[^1].RecordSha256, expectedCurrentRecordSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Prepared binding transition is stale, duplicate, or invalid.");
        }
        var current = history[^1];
        var record = NewRecord(toolId, current.CandidateContentIdentity, current.ReviewIdentity,
            current.ReviewInterfaceVersion, current.ReviewRecordSha256, nextState, note, history);
        // Revocation must remain recordable even if candidate bytes or review evidence later change.
        return Publish(applicationRoot, record, history, null);
    }

    private static GeneratedToolReviewRecord RequireCurrentReview(
        string applicationRoot, string toolId, string expectedContentIdentity, string expectedReviewIdentity)
    {
        var candidate = GeneratedToolCandidateStore.Read(applicationRoot, toolId)
            ?? throw new InvalidDataException("Prepared binding requires an existing candidate.");
        if (!string.Equals(candidate.ContentIdentity, expectedContentIdentity, StringComparison.Ordinal)
            || candidate.State != GeneratedToolCandidateValidator.Unapproved)
        {
            throw new InvalidDataException("Prepared binding candidate content is stale or invalid.");
        }
        var review = GeneratedToolReviewStore.ReadCurrent(applicationRoot, toolId)
            ?? throw new InvalidDataException("Prepared binding requires a current review decision.");
        if (review.Decision != "reviewed"
            || !string.Equals(review.CandidateContentIdentity, expectedContentIdentity, StringComparison.Ordinal)
            || !string.Equals(review.ReviewIdentity, expectedReviewIdentity, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Prepared binding requires the exact current reviewed identity.");
        }
        return review;
    }

    private static GeneratedToolPreparedBindingRecord NewRecord(
        string toolId, string contentIdentity, string reviewIdentity, string reviewInterfaceVersion,
        string reviewRecordSha256,
        string state, string note, IReadOnlyList<GeneratedToolPreparedBindingRecord> history)
    {
        var record = new GeneratedToolPreparedBindingRecord(
            SchemaVersion, toolId, contentIdentity, reviewIdentity, reviewInterfaceVersion, reviewRecordSha256,
            state, Disabled, NoCallableRoute, note, DateTimeOffset.UtcNow,
            history.Count + 1, history.Count == 0 ? string.Empty : history[^1].RecordSha256, string.Empty);
        return record with { RecordSha256 = ComputeRecordSha256(record) };
    }

    private static GeneratedToolPreparedBindingRecord Publish(
        string applicationRoot, GeneratedToolPreparedBindingRecord record,
        IReadOnlyList<GeneratedToolPreparedBindingRecord> priorHistory,
        Action? revalidateReview)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
        if (bytes.Length > MaximumRecordBytes)
        {
            throw new InvalidDataException("Prepared binding record exceeds its size limit.");
        }
        var directory = BindingDirectory(applicationRoot, record.ToolId);
        var stagingDirectory = StagingDirectory(applicationRoot, record.ToolId);
        Directory.CreateDirectory(stagingDirectory);
        Directory.CreateDirectory(directory);
        GeneratedToolCandidatePath.RejectReparseComponents(stagingDirectory);
        GeneratedToolCandidatePath.RejectReparseComponents(directory);
        var stagingPath = Path.Combine(stagingDirectory, Guid.NewGuid().ToString("N") + ".json");
        var finalPath = Path.Combine(directory, record.Sequence.ToString("D10") + ".json");
        GeneratedToolCandidatePath.RejectReparseComponents(stagingPath);
        GeneratedToolCandidatePath.RejectReparseComponents(finalPath);
        // Incomplete staging is retained for diagnosis, never read as published history.
        using (var stream = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                   bufferSize: 4096, FileOptions.WriteThrough))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
            RequireSingleLink(stream.SafeFileHandle);
        }

        GeneratedToolCandidatePath.RejectReparseComponents(stagingDirectory);
        GeneratedToolCandidatePath.RejectReparseComponents(directory);
        GeneratedToolCandidatePath.RejectReparseComponents(stagingPath);
        GeneratedToolCandidatePath.RejectReparseComponents(finalPath);
        var currentHistory = History(applicationRoot, record.ToolId);
        if (currentHistory.Count != priorHistory.Count
            || currentHistory.Count != 0
                && currentHistory[^1].RecordSha256 != priorHistory[^1].RecordSha256)
        {
            throw new InvalidDataException("Prepared binding history changed before publication; staging was retained.");
        }
        revalidateReview?.Invoke();
        try
        {
            File.Move(stagingPath, finalPath, overwrite: false);
        }
        catch (IOException exception) when (File.Exists(finalPath))
        {
            throw new InvalidDataException("Prepared binding history changed during publication; staging was retained.", exception);
        }
        return record;
    }

    private static string BindingDirectory(string applicationRoot, string toolId)
    {
        ValidateToolId(toolId);
        var root = GeneratedToolCandidatePath.Root(applicationRoot);
        var bindingsRoot = Path.Combine(root, "PreparedBindings");
        GeneratedToolCandidatePath.RejectReparseComponents(bindingsRoot);
        if (File.Exists(bindingsRoot))
        {
            throw new InvalidDataException("Prepared bindings root is not a directory.");
        }
        var directory = Path.Combine(bindingsRoot, toolId);
        GeneratedToolCandidatePath.RejectReparseComponents(directory);
        return directory;
    }

    private static string StagingDirectory(string applicationRoot, string toolId)
    {
        var directory = BindingDirectory(applicationRoot, toolId);
        var staging = Path.Combine(Path.GetDirectoryName(directory)!, ".staging", toolId);
        GeneratedToolCandidatePath.RejectReparseComponents(staging);
        if (File.Exists(staging))
        {
            throw new InvalidDataException("Prepared binding staging path is not a directory.");
        }
        return staging;
    }

    private static void ValidateToolId(string? toolId)
    {
        if (string.IsNullOrEmpty(toolId)
            || !Regex.IsMatch(toolId, "^[a-z0-9][a-z0-9-]{0,79}$", RegexOptions.CultureInvariant)
            || Regex.IsMatch(toolId, @"^(?:con|prn|aux|nul|com[1-9]|lpt[1-9])$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            throw new InvalidDataException("Prepared binding tool ID is invalid.");
        }
    }

    private static void ValidateRecord(GeneratedToolPreparedBindingRecord record, string toolId, string fileName)
    {
        if (record.SchemaVersion != SchemaVersion
            || !string.Equals(record.ToolId, toolId, StringComparison.Ordinal)
            || record.State is not (PreparedDisabled or Revoked or Cleaned)
            || record.ActivationState != Disabled || record.CallableRoute != NoCallableRoute
            || record.RecordedAtUtc == default || record.RecordedAtUtc.Offset != TimeSpan.Zero
            || record.Sequence is < 1 or > MaximumRecordsPerTool
            || !IsIdentity(record.CandidateContentIdentity, CandidateIdentityPrefix)
            || !IsIdentity(record.ReviewIdentity, ReviewIdentityPrefix)
            || string.IsNullOrWhiteSpace(record.ReviewInterfaceVersion)
            || record.ReviewInterfaceVersion.Length > 128
            || record.ReviewInterfaceVersion.Any(char.IsControl)
            || !IsLowercaseSha256(record.ReviewRecordSha256)
            || record.PreviousRecordSha256 is null
            || (record.Sequence == 1 ? record.PreviousRecordSha256.Length != 0
                : !IsLowercaseSha256(record.PreviousRecordSha256))
            || !IsLowercaseSha256(record.RecordSha256))
        {
            throw new InvalidDataException("Prepared binding record has an invalid disabled state or identity.");
        }
        ValidateNote(record.Note);
        var reviewInput = Encoding.UTF8.GetBytes(
            $"generated-tool-review-v1\n{record.CandidateContentIdentity}\n{record.ReviewInterfaceVersion}");
        var expectedReviewIdentity = ReviewIdentityPrefix
            + Convert.ToHexString(SHA256.HashData(reviewInput)).ToLowerInvariant();
        if (!string.Equals(record.ReviewIdentity, expectedReviewIdentity, StringComparison.Ordinal)
            || !string.Equals(fileName, record.Sequence.ToString("D10") + ".json", StringComparison.Ordinal)
            || !string.Equals(record.RecordSha256, ComputeRecordSha256(record), StringComparison.Ordinal))
        {
            throw new InvalidDataException("Prepared binding record does not match its review or chain identity.");
        }
    }

    private static bool SameBinding(GeneratedToolPreparedBindingRecord left, GeneratedToolPreparedBindingRecord right) =>
        left.CandidateContentIdentity == right.CandidateContentIdentity
        && left.ReviewIdentity == right.ReviewIdentity
        && left.ReviewInterfaceVersion == right.ReviewInterfaceVersion
        && left.ReviewRecordSha256 == right.ReviewRecordSha256;

    private static bool IsIdentity(string? value, string prefix) =>
        value is not null && value.StartsWith(prefix, StringComparison.Ordinal)
        && IsLowercaseSha256(value[prefix.Length..]);

    private static bool IsLowercaseSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string ComputeRecordSha256(GeneratedToolPreparedBindingRecord record) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            record with { RecordSha256 = string.Empty }, JsonOptions))).ToLowerInvariant();

    private static void ValidateNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note) || note.Length > 2048 || note.Any(char.IsControl))
        {
            throw new InvalidDataException("Prepared binding requires a bounded single-line note.");
        }
        GeneratedToolCandidateValidator.RejectCredentialText(note);
    }

    private static byte[] ReadBounded(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        RequireSingleLink(stream.SafeFileHandle);
        if (stream.Length <= 0 || stream.Length > MaximumRecordBytes)
        {
            throw new InvalidDataException("Prepared binding record is empty or exceeds its size limit.");
        }
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        RequireSingleLink(stream.SafeFileHandle);
        return bytes;
    }

    private static void RequireSingleLink(SafeFileHandle handle)
    {
        if (!OperatingSystem.IsWindows() || !GetFileInformationByHandle(handle, out var information)
            || information.NumberOfLinks == 0)
        {
            throw new InvalidDataException("Prepared binding record link count could not be established.");
        }
        if (information.NumberOfLinks != 1)
        {
            throw new InvalidDataException("Prepared binding record has multiple hard links.");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleFileInformation information);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public uint CreationTimeLow;
        public uint CreationTimeHigh;
        public uint LastAccessTimeLow;
        public uint LastAccessTimeHigh;
        public uint LastWriteTimeLow;
        public uint LastWriteTimeHigh;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
}
