using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoBabylon.Core;

// A product-mediated local interaction record, not proof of a reviewer identity or protection
// against modification by another process running with the same unrestricted user authority.
public sealed record GeneratedToolReviewRecord(
    int SchemaVersion,
    string ToolId,
    string CandidateContentIdentity,
    string ReviewInterfaceVersion,
    string ReviewIdentity,
    string Decision,
    string Interaction,
    string Note,
    DateTimeOffset RecordedAtUtc,
    int Sequence,
    string PreviousRecordSha256,
    string RecordSha256,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ReviewSnapshotIdentity = null);

public sealed record GeneratedToolReviewFileChange(
    string Path,
    string PreviousSha256,
    string? CurrentSha256,
    string? BeforePreview,
    string? AfterPreview,
    bool BeforePreviewTruncated,
    bool AfterPreviewTruncated,
    bool PreviewAvailable);

public sealed record GeneratedToolReviewManifestChange(string Field, string BeforeJson, string AfterJson);

public sealed record GeneratedToolReviewEvidenceProjection(
    string Kind, string EvidenceId, string Outcome, string Description, string Path, string Sha256);

public sealed record GeneratedToolReviewComparison(
    string ToolId,
    string ComparisonState,
    string? UnavailableReason,
    string? FailureKind,
    string? PriorDecision,
    string? PriorReviewIdentity,
    string? ReviewedContentIdentity,
    string CurrentContentIdentity,
    IReadOnlyList<GeneratedToolReviewFileChange> AddedFiles,
    IReadOnlyList<GeneratedToolReviewFileChange> RemovedFiles,
    IReadOnlyList<GeneratedToolReviewFileChange> ModifiedFiles,
    IReadOnlyList<GeneratedToolReviewManifestChange> ChangedManifestFields,
    IReadOnlyList<GeneratedToolReviewEvidenceProjection> ReviewedEvidence,
    IReadOnlyList<GeneratedToolReviewEvidenceProjection> CurrentEvidence);

internal sealed record GeneratedToolReviewSnapshotFile(string Path, string Sha256, byte[] Content);

internal sealed record GeneratedToolReviewSnapshotData(
    int SchemaVersion,
    string ToolId,
    string CandidateContentIdentity,
    int Sequence,
    byte[] ManifestBytes,
    IReadOnlyList<GeneratedToolReviewSnapshotFile> Files,
    string SnapshotIdentity);

internal sealed record GeneratedToolReviewedBundleInfo(
    GeneratedToolCandidateManifest Manifest,
    string SnapshotIdentity,
    string BundlePath,
    string EntryPointPath);

// Stores decisions separately from candidate bytes. No method here registers or executes a tool.
public static class GeneratedToolReviewStore
{
    public const string ProductMediatedLocalInteraction = "product-mediated-local-interaction";
    // Version of the product's local review contract, not a qualified callable MCP interface.
    public const string ReviewInterfaceVersion = "neobabylon-generated-tool-review-v1";
    // v1 remains readable without snapshots; new records use v2 and bind retained review bytes.
    private const int LegacySchemaVersion = 1;
    private const int SchemaVersion = 2;
    private const int MaximumRecordBytes = 8 * 1024;
    private const int MaximumRecordsPerTool = 1024;
    private const int MaximumManifestBytes = 64 * 1024;
    private const int MaximumFileBytes = 1024 * 1024;
    private const long MaximumSnapshotContentBytes = 16 * 1024 * 1024 + MaximumManifestBytes;
    private const long MaximumSnapshotFileBytes = 24 * 1024 * 1024;
    private const long MaximumSnapshotBytesPerTool = 128 * 1024 * 1024;
    private const int MaximumPreviewCharacters = 4096;
    private const string ReviewIdentityPrefix = "review-v1:sha256:";
    private const string CandidateIdentityPrefix = "candidate-v1:sha256:";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static GeneratedToolReviewRecord RecordReview(
        string applicationRoot, string toolId, string expectedContentIdentity, string note) =>
        RecordDecision(applicationRoot, toolId, expectedContentIdentity, "reviewed", note);

    public static GeneratedToolReviewRecord RecordRejection(
        string applicationRoot, string toolId, string expectedContentIdentity, string note) =>
        RecordDecision(applicationRoot, toolId, expectedContentIdentity, "rejected", note);

    public static GeneratedToolReviewRecord? ReadCurrent(string applicationRoot, string toolId)
    {
        var candidate = GeneratedToolCandidateStore.Read(applicationRoot, toolId);
        if (candidate is null) return null;
        var identity = ReviewIdentity(candidate.ContentIdentity);
        return History(applicationRoot, toolId).SingleOrDefault(record => record.ReviewIdentity == identity);
    }

    public static IReadOnlyList<GeneratedToolReviewRecord> History(string applicationRoot, string toolId)
    {
        var directory = ReviewDirectory(applicationRoot, toolId);
        if (File.Exists(directory))
        {
            throw new InvalidDataException("Generated tool review history path is not a directory.");
        }
        if (!Directory.Exists(directory)) return [];

        var entries = Directory.EnumerateFileSystemEntries(directory).ToArray();
        if (entries.Length > MaximumRecordsPerTool)
        {
            throw new InvalidDataException("Generated tool review history exceeds its bounded record count.");
        }
        var records = new List<GeneratedToolReviewRecord>(entries.Length);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            GeneratedToolCandidatePath.RejectReparseComponents(entry);
            if (!File.Exists(entry))
            {
                throw new InvalidDataException("Generated tool review history contains a non-record entry.");
            }
            var bytes = ReadBounded(entry);
            GeneratedToolReviewRecord record;
            try
            {
                using var document = JsonDocument.Parse(bytes);
                GeneratedToolCandidateValidator.RejectDuplicateProperties(document.RootElement);
                record = JsonSerializer.Deserialize<GeneratedToolReviewRecord>(bytes, JsonOptions)
                    ?? throw new InvalidDataException("Generated tool review record is empty.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Generated tool review record is malformed.", exception);
            }
            ValidateRecord(record, toolId, Path.GetFileName(entry));
            if (!identities.Add(record.ReviewIdentity))
            {
                throw new InvalidDataException("Generated tool review history contains a duplicate decision identity.");
            }
            records.Add(record);
        }
        records.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
        var previousHash = string.Empty;
        for (var index = 0; index < records.Count; index++)
        {
            if (records[index].Sequence != index + 1
                || !string.Equals(records[index].PreviousRecordSha256, previousHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Generated tool review history has a missing, duplicate, or unlinked record.");
            }
            previousHash = records[index].RecordSha256;
        }
        ValidateSnapshotBudget(applicationRoot, toolId, records);
        return records.ToArray();
    }

    public static GeneratedToolReviewComparison ReadComparison(
        string applicationRoot, string toolId, string contentIdentity)
    {
        var candidate = GeneratedToolCandidateStore.Read(applicationRoot, toolId);
        if (candidate is null)
        {
            return EmptyComparison(toolId, contentIdentity, "unavailable", "current-candidate-unavailable", "stale");
        }
        if (!string.Equals(candidate.ContentIdentity, contentIdentity, StringComparison.Ordinal))
        {
            return EmptyComparison(toolId, candidate.ContentIdentity, "unavailable", "current-content-identity-stale", "stale");
        }

        // Compare against a prior decision, not the decision for the currently displayed bytes.
        var prior = History(applicationRoot, toolId).LastOrDefault(record =>
            !string.Equals(record.CandidateContentIdentity, contentIdentity, StringComparison.Ordinal));
        if (prior is null)
        {
            return EmptyComparison(toolId, candidate.ContentIdentity, "noPriorReview", null, null);
        }
        if (prior.ReviewSnapshotIdentity is null)
        {
            return EmptyComparison(toolId, candidate.ContentIdentity, "unavailable",
                "historical-review-has-no-snapshot", null, prior);
        }

        GeneratedToolReviewSnapshotData snapshot;
        GeneratedToolCandidateManifest reviewedManifest;
        IReadOnlyList<GeneratedToolReviewSnapshotFile> currentFiles;
        byte[] currentManifestBytes;
        try
        {
            snapshot = ReadSnapshot(applicationRoot, toolId, prior);
            reviewedManifest = DeserializeSnapshotManifest(applicationRoot, toolId, snapshot);
            currentFiles = ReadCurrentFiles(candidate);
            currentManifestBytes = ReadBounded(
                GeneratedToolCandidatePath.ResolveFile(candidate.CandidatePath, "tool.json"), MaximumManifestBytes);
            var currentCandidate = GeneratedToolCandidateStore.Read(applicationRoot, toolId);
            if (currentCandidate is null
                || !string.Equals(currentCandidate.ContentIdentity, contentIdentity, StringComparison.Ordinal))
            {
                return EmptyComparison(toolId, contentIdentity, "unavailable", "current-content-changed-during-comparison", "stale", prior);
            }
        }
        catch (InvalidDataException)
        {
            return EmptyComparison(toolId, contentIdentity, "unavailable",
                "review-snapshot-or-current-content-invalid", "stale", prior);
        }
        // Comparison paths are review content: preserve case-only manifest path
        // changes as a removal/addition even on Windows' case-insensitive filesystem.
        var priorFiles = snapshot.Files.ToDictionary(file => NormalizeRelativePath(file.Path), StringComparer.Ordinal);
        var currentByPath = currentFiles.ToDictionary(file => NormalizeRelativePath(file.Path), StringComparer.Ordinal);
        var added = new List<GeneratedToolReviewFileChange>();
        var removed = new List<GeneratedToolReviewFileChange>();
        var modified = new List<GeneratedToolReviewFileChange>();

        foreach (var path in priorFiles.Keys.Union(currentByPath.Keys, StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            var hasPrior = priorFiles.TryGetValue(path, out var before);
            var hasCurrent = currentByPath.TryGetValue(path, out var after);
            if (!hasPrior && after is not null)
            {
                added.Add(ToFileChange(after.Path, null, after.Sha256, null, after.Content));
            }
            else if (before is not null && !hasCurrent)
            {
                removed.Add(ToFileChange(before.Path, before.Sha256, null, before.Content, null));
            }
            else if (before is not null && after is not null
                     && !string.Equals(before.Sha256, after.Sha256, StringComparison.Ordinal))
            {
                modified.Add(ToFileChange(after.Path, before.Sha256, after.Sha256, before.Content, after.Content));
            }
        }

        var manifestChanges = CompareManifest(snapshot.ManifestBytes, currentManifestBytes);
        return new GeneratedToolReviewComparison(
            toolId, "available", null, null, prior.Decision, prior.ReviewIdentity,
            prior.CandidateContentIdentity, candidate.ContentIdentity,
            added, removed, modified, manifestChanges,
            ProjectEvidence(reviewedManifest), ProjectEvidence(candidate.Manifest));
    }

    private static GeneratedToolReviewRecord RecordDecision(
        string applicationRoot, string toolId, string expectedContentIdentity, string decision, string note)
    {
        ValidateNote(note);
        var candidate = GeneratedToolCandidateStore.Read(applicationRoot, toolId)
            ?? throw new InvalidDataException("Generated tool candidate does not exist for review.");
        if (!string.Equals(candidate.ContentIdentity, expectedContentIdentity, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Generated tool candidate changed since the reviewable content was shown.");
        }
        var identity = ReviewIdentity(candidate.ContentIdentity);
        var history = History(applicationRoot, toolId);
        if (history.Any(record => record.ReviewIdentity == identity))
        {
            throw new InvalidDataException("This candidate and product review interface already have a recorded decision.");
        }
        if (history.Count >= MaximumRecordsPerTool)
        {
            throw new InvalidDataException("Generated tool review history has reached its bounded record count.");
        }

        var sequence = history.Count + 1;
        var snapshot = CaptureSnapshot(candidate, sequence);
        var snapshotBytes = SerializeSnapshot(snapshot);
        if (snapshotBytes.Length > MaximumSnapshotFileBytes)
        {
            throw new InvalidDataException("Generated tool review snapshot exceeds its storage bound.");
        }
        snapshot = snapshot with { SnapshotIdentity = ComputeSnapshotIdentity(snapshot) };
        snapshotBytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        EnsureSnapshotBudget(applicationRoot, toolId, history, snapshotBytes.Length);
        var snapshotPath = SnapshotPath(applicationRoot, toolId, snapshot.SnapshotIdentity);
        PublishImmutableFile(snapshotPath, snapshotBytes, MaximumSnapshotFileBytes,
            "Generated tool review snapshot");
        MaterializeReviewedBundle(applicationRoot, toolId, snapshot);

        var candidateAtPublish = GeneratedToolCandidateStore.Read(applicationRoot, toolId)
            ?? throw new InvalidDataException("Generated tool candidate disappeared before review publication; snapshot retained.");
        if (!string.Equals(candidateAtPublish.ContentIdentity, candidate.ContentIdentity, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Generated tool candidate changed before review publication; snapshot retained.");
        }

        var record = new GeneratedToolReviewRecord(
            SchemaVersion, toolId, candidate.ContentIdentity, ReviewInterfaceVersion, identity,
            decision, ProductMediatedLocalInteraction, note, DateTimeOffset.UtcNow,
            sequence, history.Count == 0 ? string.Empty : history[^1].RecordSha256, string.Empty,
            snapshot.SnapshotIdentity);
        record = record with { RecordSha256 = ComputeRecordSha256(record) };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
        if (bytes.Length > MaximumRecordBytes)
        {
            throw new InvalidDataException("Generated tool review record exceeds its size limit.");
        }
        var directory = ReviewDirectory(applicationRoot, toolId);
        var stagingDirectory = StagingDirectory(applicationRoot, toolId);
        Directory.CreateDirectory(stagingDirectory);
        Directory.CreateDirectory(directory);
        GeneratedToolCandidatePath.RejectReparseComponents(stagingDirectory);
        GeneratedToolCandidatePath.RejectReparseComponents(directory);
        var stagingPath = Path.Combine(stagingDirectory, Guid.NewGuid().ToString("N") + ".json");
        var finalPath = Path.Combine(directory, record.Sequence.ToString("D10") + ".json");
        GeneratedToolCandidatePath.RejectReparseComponents(stagingPath);
        GeneratedToolCandidatePath.RejectReparseComponents(finalPath);
        // A failed write or publish leaves staging evidence for diagnosis. History reads only
        // the final per-tool directory, so incomplete staging cannot poison it.
        using (var stream = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                   bufferSize: 4096, FileOptions.WriteThrough))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        // These checks reduce accidental path redirection, but cannot close a check/use race
        // against another process with the same unrestricted Windows-user authority.
        GeneratedToolCandidatePath.RejectReparseComponents(stagingDirectory);
        GeneratedToolCandidatePath.RejectReparseComponents(directory);
        GeneratedToolCandidatePath.RejectReparseComponents(stagingPath);
        GeneratedToolCandidatePath.RejectReparseComponents(finalPath);
        candidateAtPublish = GeneratedToolCandidateStore.Read(applicationRoot, toolId)
            ?? throw new InvalidDataException("Generated tool candidate disappeared before review publication.");
        if (!string.Equals(candidateAtPublish.ContentIdentity, candidate.ContentIdentity, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Generated tool candidate changed before review publication; staging evidence was retained.");
        }
        try
        {
            File.Move(stagingPath, finalPath, overwrite: false);
        }
        catch (IOException exception) when (File.Exists(finalPath))
        {
            throw new InvalidDataException("Generated tool review history changed during publish; staging evidence was retained.", exception);
        }
        return record;
    }

    internal static GeneratedToolReviewSnapshotData ReadSnapshot(
        string applicationRoot, string toolId, GeneratedToolReviewRecord review)
    {
        if (review.SchemaVersion != SchemaVersion || review.ReviewSnapshotIdentity is null)
        {
            throw new InvalidDataException("Historical generated-tool review has no retained content snapshot.");
        }
        var path = SnapshotPath(applicationRoot, toolId, review.ReviewSnapshotIdentity);
        var bytes = ReadBounded(path, (int)MaximumSnapshotFileBytes);
        GeneratedToolReviewSnapshotData snapshot;
        try
        {
            using var document = JsonDocument.Parse(bytes);
            GeneratedToolCandidateValidator.RejectDuplicateProperties(document.RootElement);
            snapshot = JsonSerializer.Deserialize<GeneratedToolReviewSnapshotData>(bytes, JsonOptions)
                ?? throw new InvalidDataException("Generated tool review snapshot is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Generated tool review snapshot is malformed.", exception);
        }

        if (snapshot.SchemaVersion != SchemaVersion
            || !string.Equals(snapshot.ToolId, toolId, StringComparison.Ordinal)
            || !string.Equals(snapshot.CandidateContentIdentity, review.CandidateContentIdentity, StringComparison.Ordinal)
            || snapshot.Sequence != review.Sequence
            || !string.Equals(snapshot.SnapshotIdentity, review.ReviewSnapshotIdentity, StringComparison.Ordinal)
            || !string.Equals(snapshot.SnapshotIdentity, ComputeSnapshotIdentity(snapshot), StringComparison.Ordinal))
        {
            throw new InvalidDataException("Generated tool review snapshot does not match its linked review identity.");
        }
        _ = DeserializeSnapshotManifest(applicationRoot, toolId, snapshot);
        return snapshot;
    }

    internal static GeneratedToolReviewedBundleInfo ReadReviewedBundle(
        string applicationRoot, string toolId, GeneratedToolReviewRecord review)
    {
        var snapshot = ReadSnapshot(applicationRoot, toolId, review);
        var manifest = DeserializeSnapshotManifest(applicationRoot, toolId, snapshot);
        var bundlePath = ReviewedBundlePath(applicationRoot, toolId, snapshot.SnapshotIdentity);
        ValidateBundleContents(bundlePath, snapshot);
        var entryPoint = GeneratedToolCandidatePath.ResolveFile(bundlePath, manifest.EntryPoint);
        return new GeneratedToolReviewedBundleInfo(manifest, snapshot.SnapshotIdentity, bundlePath, entryPoint);
    }

    private static GeneratedToolReviewSnapshotData CaptureSnapshot(GeneratedToolCandidate candidate, int sequence)
    {
        var manifestPath = GeneratedToolCandidatePath.ResolveFile(candidate.CandidatePath, "tool.json");
        var manifestBytes = ReadBounded(manifestPath, MaximumManifestBytes);
        using (var document = JsonDocument.Parse(manifestBytes))
        {
            GeneratedToolCandidateValidator.RejectDuplicateProperties(document.RootElement);
        }
        var manifest = JsonSerializer.Deserialize<GeneratedToolCandidateManifest>(
            manifestBytes, GeneratedToolCandidateValidator.JsonOptions)
            ?? throw new InvalidDataException("Generated tool manifest is empty during review snapshot capture.");
        GeneratedToolCandidateValidator.ValidateAtPath(manifest, candidate.CandidatePath);

        var files = new List<GeneratedToolReviewSnapshotFile>(manifest.Files.Count);
        long total = manifestBytes.Length;
        foreach (var file in manifest.Files)
        {
            var path = GeneratedToolCandidatePath.ResolveFile(candidate.CandidatePath, file.Path);
            var content = ReadBounded(path, MaximumFileBytes);
            var sha256 = Digest(content);
            if (!string.Equals(sha256, file.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Generated tool file changed while its review snapshot was captured.");
            }
            total += content.Length;
            if (total > MaximumSnapshotContentBytes)
            {
                throw new InvalidDataException("Generated tool review snapshot content exceeds its bound.");
            }
            files.Add(new GeneratedToolReviewSnapshotFile(NormalizeRelativePath(file.Path), sha256, content));
        }
        return new GeneratedToolReviewSnapshotData(SchemaVersion, candidate.Manifest.ToolId,
            candidate.ContentIdentity, sequence, manifestBytes, files, string.Empty);
    }

    private static GeneratedToolReviewSnapshotData ReadSnapshotBytes(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            GeneratedToolCandidateValidator.RejectDuplicateProperties(document.RootElement);
            return JsonSerializer.Deserialize<GeneratedToolReviewSnapshotData>(bytes, JsonOptions)
                ?? throw new InvalidDataException("Generated tool review snapshot is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Generated tool review snapshot is malformed.", exception);
        }
    }

    private static byte[] SerializeSnapshot(GeneratedToolReviewSnapshotData snapshot) =>
        JsonSerializer.SerializeToUtf8Bytes(snapshot with { SnapshotIdentity = string.Empty }, JsonOptions);

    private static string ComputeSnapshotIdentity(GeneratedToolReviewSnapshotData snapshot) =>
        Digest(SerializeSnapshot(snapshot));

    private static GeneratedToolCandidateManifest DeserializeSnapshotManifest(
        string applicationRoot, string toolId, GeneratedToolReviewSnapshotData snapshot)
    {
        if (snapshot.ManifestBytes is null || snapshot.ManifestBytes.Length is 0 or > MaximumManifestBytes
            || snapshot.Files is null || snapshot.Files.Count is 0 or > 256)
        {
            throw new InvalidDataException("Generated tool review snapshot has an invalid manifest or file inventory.");
        }
        GeneratedToolCandidateManifest manifest;
        try
        {
            using var document = JsonDocument.Parse(snapshot.ManifestBytes);
            GeneratedToolCandidateValidator.RejectDuplicateProperties(document.RootElement);
            manifest = JsonSerializer.Deserialize<GeneratedToolCandidateManifest>(
                snapshot.ManifestBytes, GeneratedToolCandidateValidator.JsonOptions)
                ?? throw new InvalidDataException("Generated tool review snapshot manifest is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Generated tool review snapshot manifest is malformed.", exception);
        }

        var candidateRoot = GeneratedToolCandidatePath.Candidate(
            GeneratedToolCandidatePath.Root(applicationRoot), toolId);
        GeneratedToolCandidateValidator.ValidateAtPath(manifest, candidateRoot);
        if (!string.Equals(manifest.ToolId, toolId, StringComparison.Ordinal)
            || snapshot.Files.Count != manifest.Files.Count)
        {
            throw new InvalidDataException("Generated tool review snapshot does not match its manifest inventory.");
        }

        var snapshotFiles = new Dictionary<string, GeneratedToolReviewSnapshotFile>(StringComparer.OrdinalIgnoreCase);
        long total = snapshot.ManifestBytes.Length;
        foreach (var file in snapshot.Files)
        {
            if (file is null || file.Content is null || file.Content.Length is 0 or > MaximumFileBytes
                || !string.Equals(file.Sha256, Digest(file.Content), StringComparison.Ordinal))
            {
                throw new InvalidDataException("Generated tool review snapshot contains a malformed or changed file.");
            }
            GeneratedToolCandidatePath.ResolveFile(candidateRoot, file.Path);
            if (!snapshotFiles.TryAdd(NormalizeRelativePath(file.Path), file))
            {
                throw new InvalidDataException("Generated tool review snapshot contains a duplicate path.");
            }
            total += file.Content.Length;
            if (total > MaximumSnapshotContentBytes)
            {
                throw new InvalidDataException("Generated tool review snapshot content exceeds its bound.");
            }
        }
        foreach (var listed in manifest.Files)
        {
            if (!snapshotFiles.TryGetValue(NormalizeRelativePath(listed.Path), out var captured)
                || !string.Equals(captured.Sha256, listed.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Generated tool review snapshot does not contain the exact manifest-listed bytes.");
            }
        }
        return manifest;
    }

    private static IReadOnlyList<GeneratedToolReviewSnapshotFile> ReadCurrentFiles(GeneratedToolCandidate candidate)
    {
        var result = new List<GeneratedToolReviewSnapshotFile>(candidate.Manifest.Files.Count);
        foreach (var file in candidate.Manifest.Files)
        {
            var path = GeneratedToolCandidatePath.ResolveFile(candidate.CandidatePath, file.Path);
            var content = ReadBounded(path, MaximumFileBytes);
            var digest = Digest(content);
            if (!string.Equals(digest, file.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Current generated tool file changed during review comparison.");
            }
            result.Add(new GeneratedToolReviewSnapshotFile(NormalizeRelativePath(file.Path), digest, content));
        }
        return result;
    }

    private static IReadOnlyList<GeneratedToolReviewEvidenceProjection> ProjectEvidence(
        GeneratedToolCandidateManifest manifest) => manifest.Evidence.Select(item =>
            new GeneratedToolReviewEvidenceProjection(item.Kind, item.EvidenceId, item.Outcome,
                item.Description, item.Path, item.Sha256)).ToArray();

    private static IReadOnlyList<GeneratedToolReviewManifestChange> CompareManifest(byte[] beforeBytes, byte[] afterBytes)
    {
        using var beforeDocument = JsonDocument.Parse(beforeBytes);
        using var afterDocument = JsonDocument.Parse(afterBytes);
        var before = beforeDocument.RootElement.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.GetRawText(), StringComparer.Ordinal);
        var after = afterDocument.RootElement.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.GetRawText(), StringComparer.Ordinal);
        return before.Keys.Union(after.Keys, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal)
            .Where(name => !before.TryGetValue(name, out var oldValue)
                           || !after.TryGetValue(name, out var newValue)
                           || !string.Equals(oldValue, newValue, StringComparison.Ordinal))
            .Select(name => new GeneratedToolReviewManifestChange(name,
                LimitPreview(before.GetValueOrDefault(name) ?? "<absent>"),
                LimitPreview(after.GetValueOrDefault(name) ?? "<absent>")))
            .ToArray();
    }

    private static GeneratedToolReviewFileChange ToFileChange(
        string path, string? beforeHash, string? afterHash, byte[]? beforeBytes, byte[]? afterBytes)
    {
        string? beforePreview = null;
        string? afterPreview = null;
        var beforeTruncated = false;
        var afterTruncated = false;
        var beforeAvailable = beforeBytes is null;
        var afterAvailable = afterBytes is null;
        if (beforeBytes is not null)
        {
            beforeAvailable = TryPreview(beforeBytes, out beforePreview, out beforeTruncated);
        }
        if (afterBytes is not null)
        {
            afterAvailable = TryPreview(afterBytes, out afterPreview, out afterTruncated);
        }
        return new GeneratedToolReviewFileChange(path, beforeHash ?? string.Empty, afterHash,
            beforePreview, afterPreview,
            beforeTruncated, afterTruncated,
            beforeAvailable && afterAvailable);
    }

    private static bool TryPreview(byte[] bytes, out string? preview, out bool truncated)
    {
        preview = null;
        truncated = false;
        try
        {
            var text = new UTF8Encoding(false, true).GetString(bytes);
            truncated = text.Length > MaximumPreviewCharacters;
            preview = text[..Math.Min(text.Length, MaximumPreviewCharacters)];
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static string LimitPreview(string value) => value.Length <= MaximumPreviewCharacters
        ? value : value[..MaximumPreviewCharacters];

    private static string NormalizeRelativePath(string path) => path.Replace('\\', '/');

    private static GeneratedToolReviewComparison EmptyComparison(
        string toolId, string currentIdentity, string comparisonState, string? reason,
        string? failureKind, GeneratedToolReviewRecord? prior = null) =>
        new(toolId, comparisonState, reason, failureKind,
            prior?.Decision, prior?.ReviewIdentity, prior?.CandidateContentIdentity, currentIdentity,
            [], [], [], [], [], []);

    private static string Digest(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void ValidateSnapshotBudget(
        string applicationRoot, string toolId, IReadOnlyList<GeneratedToolReviewRecord> records,
        long additionalBytes = 0)
    {
        var directory = SnapshotDirectory(applicationRoot, toolId);
        if (File.Exists(directory))
        {
            throw new InvalidDataException("Generated tool review snapshot path is not a directory.");
        }
        if (!Directory.Exists(directory))
        {
            if (records.Any(record => record.ReviewSnapshotIdentity is not null))
            {
                throw new InvalidDataException("Generated tool review snapshot directory is missing.");
            }
            if (additionalBytes > MaximumSnapshotBytesPerTool)
            {
                throw new InvalidDataException("Generated tool review snapshot history is full.");
            }
            return;
        }

        var entries = Directory.EnumerateFileSystemEntries(directory).ToArray();
        if (entries.Length > MaximumRecordsPerTool)
        {
            throw new InvalidDataException("Generated tool review snapshot count exceeds its bound.");
        }
        long totalBytes = additionalBytes;
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            GeneratedToolCandidatePath.RejectReparseComponents(entry);
            if (!File.Exists(entry) || !string.Equals(Path.GetExtension(entry), ".json", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Generated tool review snapshot directory contains an unexpected entry.");
            }
            var info = new FileInfo(entry);
            if (info.Length is <= 0 or > MaximumSnapshotFileBytes)
            {
                throw new InvalidDataException("Generated tool review snapshot has an invalid size.");
            }
            totalBytes += info.Length;
            present.Add(Path.GetFileNameWithoutExtension(entry));
        }
        if (totalBytes > MaximumSnapshotBytesPerTool
            || records.Where(record => record.ReviewSnapshotIdentity is not null)
                .Any(record => !present.Contains(record.ReviewSnapshotIdentity!)))
        {
            throw new InvalidDataException("Generated tool review snapshot history is missing, unbounded, or exceeds its storage budget.");
        }
    }

    private static void EnsureSnapshotBudget(
        string applicationRoot, string toolId, IReadOnlyList<GeneratedToolReviewRecord> records, long additionalBytes) =>
        ValidateSnapshotBudget(applicationRoot, toolId, records, additionalBytes);

    private static string SnapshotDirectory(string applicationRoot, string toolId)
    {
        var root = GeneratedToolCandidatePath.Root(applicationRoot);
        GeneratedToolCandidatePath.Candidate(root, toolId);
        var directory = Path.Combine(root, "ReviewSnapshots", toolId);
        GeneratedToolCandidatePath.RejectReparseComponents(directory);
        return directory;
    }

    private static string SnapshotPath(string applicationRoot, string toolId, string snapshotIdentity)
    {
        GeneratedToolCandidateValidator.RequireHash(snapshotIdentity);
        return Path.Combine(SnapshotDirectory(applicationRoot, toolId), snapshotIdentity + ".json");
    }

    private static string ReviewedBundlePath(string applicationRoot, string toolId, string snapshotIdentity)
    {
        GeneratedToolCandidateValidator.RequireHash(snapshotIdentity);
        var root = GeneratedToolCandidatePath.Root(applicationRoot);
        GeneratedToolCandidatePath.Candidate(root, toolId);
        var path = Path.Combine(root, "ReviewedBundles", toolId, snapshotIdentity);
        GeneratedToolCandidatePath.RejectReparseComponents(path);
        return path;
    }

    private static void PublishImmutableFile(string path, byte[] bytes, long maximumBytes, string description)
    {
        if (bytes.Length is 0 || bytes.LongLength > maximumBytes)
        {
            throw new InvalidDataException($"{description} is empty or exceeds its storage bound.");
        }
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidDataException($"{description} path has no parent directory.");
        Directory.CreateDirectory(directory);
        GeneratedToolCandidatePath.RejectReparseComponents(directory);
        GeneratedToolCandidatePath.RejectReparseComponents(path);
        if (File.Exists(path))
        {
            if (ReadBounded(path, checked((int)maximumBytes)).AsSpan().SequenceEqual(bytes)) return;
            throw new InvalidDataException($"{description} identity already exists with different bytes.");
        }

        var stagingDirectory = Path.Combine(Path.GetDirectoryName(directory)!, ".staging");
        Directory.CreateDirectory(stagingDirectory);
        GeneratedToolCandidatePath.RejectReparseComponents(stagingDirectory);
        var stagingPath = Path.Combine(stagingDirectory, Guid.NewGuid().ToString("N") + ".json");
        GeneratedToolCandidatePath.RejectReparseComponents(stagingPath);
        WriteDurableNewFile(stagingPath, bytes);
        GeneratedToolCandidatePath.RejectReparseComponents(directory);
        GeneratedToolCandidatePath.RejectReparseComponents(stagingPath);
        GeneratedToolCandidatePath.RejectReparseComponents(path);
        try
        {
            File.Move(stagingPath, path, overwrite: false);
        }
        catch (IOException exception) when (File.Exists(path))
        {
            if (!ReadBounded(path, checked((int)maximumBytes)).AsSpan().SequenceEqual(bytes))
            {
                throw new InvalidDataException($"{description} changed during immutable publication.", exception);
            }
        }
    }

    private static void MaterializeReviewedBundle(
        string applicationRoot, string toolId, GeneratedToolReviewSnapshotData snapshot)
    {
        _ = DeserializeSnapshotManifest(applicationRoot, toolId, snapshot);
        var finalPath = ReviewedBundlePath(applicationRoot, toolId, snapshot.SnapshotIdentity);
        if (Directory.Exists(finalPath))
        {
            ValidateBundleContents(finalPath, snapshot);
            return;
        }
        if (File.Exists(finalPath))
        {
            throw new InvalidDataException("Generated tool reviewed-bundle path is not a directory.");
        }

        var toolRoot = Path.GetDirectoryName(finalPath)!;
        var stagingRoot = Path.Combine(toolRoot, ".staging");
        Directory.CreateDirectory(stagingRoot);
        GeneratedToolCandidatePath.RejectReparseComponents(stagingRoot);
        var stagingPath = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingPath);
        GeneratedToolCandidatePath.RejectReparseComponents(stagingPath);
        WriteBundleFile(stagingPath, "tool.json", snapshot.ManifestBytes, MaximumManifestBytes);
        foreach (var file in snapshot.Files)
        {
            WriteBundleFile(stagingPath, file.Path, file.Content, MaximumFileBytes);
        }
        ValidateBundleContents(stagingPath, snapshot);
        GeneratedToolCandidatePath.RejectReparseComponents(toolRoot);
        GeneratedToolCandidatePath.RejectReparseComponents(stagingPath);
        GeneratedToolCandidatePath.RejectReparseComponents(finalPath);
        try
        {
            Directory.Move(stagingPath, finalPath);
        }
        catch (IOException exception) when (Directory.Exists(finalPath))
        {
            ValidateBundleContents(finalPath, snapshot);
            _ = exception;
        }
    }

    private static void ValidateBundleContents(string bundlePath, GeneratedToolReviewSnapshotData snapshot)
    {
        if (!Directory.Exists(bundlePath))
        {
            throw new InvalidDataException("Generated tool reviewed bundle is missing.");
        }
        GeneratedToolCandidatePath.RejectReparseComponents(bundlePath);
        var expected = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["tool.json"] = snapshot.ManifestBytes
        };
        foreach (var file in snapshot.Files)
        {
            if (!expected.TryAdd(NormalizeRelativePath(file.Path), file.Content))
            {
                throw new InvalidDataException("Generated tool reviewed bundle snapshot contains a duplicate path.");
            }
        }
        var actualPaths = EnumerateBundleFiles(bundlePath);
        if (actualPaths.Count != expected.Count
            || actualPaths.Any(path => !expected.ContainsKey(NormalizeRelativePath(path))))
        {
            throw new InvalidDataException("Generated tool reviewed bundle contains missing or unreviewed files.");
        }
        foreach (var (relative, content) in expected)
        {
            var path = GeneratedToolCandidatePath.ResolveFile(bundlePath, relative);
            var maximum = string.Equals(relative, "tool.json", StringComparison.OrdinalIgnoreCase)
                ? MaximumManifestBytes : MaximumFileBytes;
            if (!ReadBounded(path, maximum).AsSpan().SequenceEqual(content))
            {
                throw new InvalidDataException("Generated tool reviewed bundle differs from its retained review snapshot.");
            }
        }
    }

    private static IReadOnlyList<string> EnumerateBundleFiles(string root)
    {
        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            GeneratedToolCandidatePath.RejectReparseComponents(directory);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                GeneratedToolCandidatePath.RejectReparseComponents(entry);
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                }
                else
                {
                    files.Add(Path.GetRelativePath(root, entry).Replace('\\', '/'));
                    if (files.Count > 257)
                    {
                        throw new InvalidDataException("Generated tool reviewed bundle exceeds its file count bound.");
                    }
                }
            }
        }
        return files;
    }

    private static void WriteBundleFile(string root, string relativePath, byte[] bytes, int maximumBytes)
    {
        if (bytes.Length is 0 || bytes.Length > maximumBytes)
        {
            throw new InvalidDataException("Generated tool reviewed bundle file is empty or exceeds its bound.");
        }
        var path = GeneratedToolCandidatePath.ResolveFile(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        GeneratedToolCandidatePath.RejectReparseComponents(path);
        WriteDurableNewFile(path, bytes);
    }

    private static void WriteDurableNewFile(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 4096, FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static string ReviewDirectory(string applicationRoot, string toolId)
    {
        var root = GeneratedToolCandidatePath.Root(applicationRoot);
        GeneratedToolCandidatePath.Candidate(root, toolId);
        var reviewsRoot = Path.Combine(root, "Reviews");
        GeneratedToolCandidatePath.RejectReparseComponents(reviewsRoot);
        if (File.Exists(reviewsRoot))
        {
            throw new InvalidDataException("Generated tool reviews root is not a directory.");
        }
        var directory = Path.Combine(reviewsRoot, toolId);
        GeneratedToolCandidatePath.RejectReparseComponents(directory);
        return directory;
    }

    private static string StagingDirectory(string applicationRoot, string toolId)
    {
        var directory = ReviewDirectory(applicationRoot, toolId);
        var staging = Path.Combine(Path.GetDirectoryName(directory)!, ".staging", toolId);
        GeneratedToolCandidatePath.RejectReparseComponents(staging);
        if (File.Exists(staging))
        {
            throw new InvalidDataException("Generated tool review staging path is not a directory.");
        }
        return staging;
    }

    private static string ReviewIdentity(string candidateContentIdentity)
    {
        if (candidateContentIdentity is null
            || !candidateContentIdentity.StartsWith(CandidateIdentityPrefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Generated tool review requires a validated candidate content identity.");
        }
        GeneratedToolCandidateValidator.RequireHash(candidateContentIdentity[CandidateIdentityPrefix.Length..]);
        var input = Encoding.UTF8.GetBytes($"generated-tool-review-v1\n{candidateContentIdentity}\n{ReviewInterfaceVersion}");
        return ReviewIdentityPrefix + Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    }

    private static string ComputeRecordSha256(GeneratedToolReviewRecord record)
    {
        // Canonical serializer input includes the decision, note, timestamp, candidate and
        // interface identities, sequence, and previous hash; excludes only this hash value.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record with { RecordSha256 = string.Empty }, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static void ValidateRecord(GeneratedToolReviewRecord record, string toolId, string fileName)
    {
        if (record.SchemaVersion is not (LegacySchemaVersion or SchemaVersion)
            || !string.Equals(record.ToolId, toolId, StringComparison.Ordinal)
            || !string.Equals(record.ReviewInterfaceVersion, ReviewInterfaceVersion, StringComparison.Ordinal)
            || record.Decision is not ("reviewed" or "rejected")
            || !string.Equals(record.Interaction, ProductMediatedLocalInteraction, StringComparison.Ordinal)
            || record.RecordedAtUtc == default || record.RecordedAtUtc.Offset != TimeSpan.Zero
            || record.Sequence is < 1 or > MaximumRecordsPerTool
            || record.PreviousRecordSha256 is null
            || (record.Sequence == 1 ? record.PreviousRecordSha256.Length != 0
                : !IsLowercaseSha256(record.PreviousRecordSha256))
            || !IsLowercaseSha256(record.RecordSha256))
        {
            throw new InvalidDataException("Generated tool review record has an invalid state or provenance.");
        }
        if (record.SchemaVersion == LegacySchemaVersion
            ? record.ReviewSnapshotIdentity is not null
            : !IsLowercaseSha256(record.ReviewSnapshotIdentity))
        {
            throw new InvalidDataException("Generated tool review snapshot identity is missing or invalid for its schema version.");
        }
        ValidateNote(record.Note);
        var expected = ReviewIdentity(record.CandidateContentIdentity);
        if (!string.Equals(record.ReviewIdentity, expected, StringComparison.Ordinal)
            || !string.Equals(fileName, record.Sequence.ToString("D10") + ".json", StringComparison.Ordinal)
            || !string.Equals(record.RecordSha256, ComputeRecordSha256(record), StringComparison.Ordinal))
        {
            throw new InvalidDataException("Generated tool review record does not match its identity or integrity hash.");
        }
    }

    private static bool IsLowercaseSha256(string? value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void ValidateNote(string note)
    {
        if (string.IsNullOrWhiteSpace(note) || note.Length > 2048 || note.Any(char.IsControl))
        {
            throw new InvalidDataException("Generated tool review requires a bounded single-line decision note.");
        }
        GeneratedToolCandidateValidator.RejectCredentialText(note);
    }

    private static byte[] ReadBounded(string path) => ReadBounded(path, MaximumRecordBytes);

    private static byte[] ReadBounded(string path, int maximumBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length <= 0 || stream.Length > maximumBytes)
        {
            throw new InvalidDataException("Generated tool review data is empty or exceeds its size limit.");
        }
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
