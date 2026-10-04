using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoBabylon.Core;

// Lifecycle records bind a reviewed snapshot and the exact disabled preparation. A record is
// product state, not proof against another process holding the same unrestricted user authority.
public sealed record GeneratedToolActivationRecord(
    int SchemaVersion,
    string ToolId,
    string Event,
    string State,
    string? CandidateContentIdentity,
    string? ReviewIdentity,
    string? ReviewRecordSha256,
    string? ReviewSnapshotIdentity,
    string? BindingRecordSha256,
    string? InvocationPlanIdentity,
    string? NodeRuntimeSha256,
    string? InputSchemaSha256,
    string? OutputSchemaSha256,
    string? DependencyIdentity,
    string? AuthorityIdentity,
    string? HostQualificationIdentity,
    string? ActivationRecordSha256,
    string QualificationState,
    string? FailureKind,
    IReadOnlyList<string> Blockers,
    GeneratedToolAuthority? RequestedAuthority,
    string Note,
    DateTimeOffset RecordedAtUtc,
    int Sequence,
    string PreviousRecordSha256,
    string RecordSha256);

public sealed record GeneratedToolActivationStatusProjection(
    string AttributedTo,
    string ToolId,
    string? EffectiveAuthority,
    bool ActivationAllowed,
    bool CallableTurnsAllowed,
    string QualificationState,
    string? FailureKind,
    string? CallableFailureKind,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> CallableBlockers,
    GeneratedToolMcpInventoryConfirmation InventoryConfirmation,
    string State,
    string? ContentIdentity,
    string? ReviewIdentity,
    string? BindingRecordSha256,
    string? ActivationRecordSha256,
    string? HostQualificationIdentity,
    string? AppServerBinarySha256,
    GeneratedToolActivationRecord? Record,
    GeneratedToolAuthority? RequestedAuthority);

public sealed record GeneratedToolActivationCommandResult(
    string? FailureKind,
    GeneratedToolActivationStatusProjection Status,
    GeneratedToolActivationRecord? Record);

public sealed record GeneratedToolActivationHistoryProjection(
    string ToolId,
    IReadOnlyList<GeneratedToolActivationRecord> Records);

public static class GeneratedToolActivationService
{
    private const string AttributedTo = "NeoBabylon.Host";

    public static GeneratedToolActivationStatusProjection GetGeneratedToolActivationStatus(
        string applicationRoot,
        string toolId,
        GeneratedToolNodeRuntimeIdentity? nodeRuntime,
        RuntimeIdentity? appServerRuntimeIdentity,
        ModelCapabilityRecord? selectedCapability,
        ToolExecutionPolicy executionPolicy,
        McpServerStatusEntry? observedMcpStatus)
    {
        var history = GeneratedToolActivationStore.History(applicationRoot, toolId);
        var lifecycle = FindLifecycle(history);
        var activeRecord = GeneratedToolActivationStore.FindActive(history);
        GeneratedToolCandidate? candidate;
        GeneratedToolReviewRecord? review;
        GeneratedToolPreparedBindingRecord? binding;
        GeneratedToolMcpRouteAssessment? route = null;
        string? failure = null;
        var blockers = new List<string>();
        candidate = null;
        review = null;
        binding = null;
        try { candidate = GeneratedToolCandidateStore.Read(applicationRoot, toolId); }
        catch (InvalidDataException)
        {
            failure = "stale";
            blockers.Add("current-candidate-invalid");
        }
        if (candidate is not null)
        {
            try { review = GeneratedToolReviewStore.ReadCurrent(applicationRoot, toolId); }
            catch (InvalidDataException)
            {
                failure = "stale";
                blockers.Add("current-review-invalid");
            }
        }
        try { binding = GeneratedToolIntegrationStore.ReadCurrent(applicationRoot, toolId); }
        catch (InvalidDataException)
        {
            failure = "stale";
            blockers.Add("current-binding-invalid");
        }
        if (candidate is not null && review is not null)
        {
            try
            {
                route = GeneratedToolMcpRoute.Assess(applicationRoot, candidate, review,
                    nodeRuntime, appServerRuntimeIdentity, selectedCapability,
                    executionPolicy, observedMcpStatus);
                blockers.AddRange(route.Blockers);
                failure ??= route.FailureKind;
            }
            catch (InvalidDataException)
            {
                failure = "stale";
                blockers.Add("reviewed-invocation-plan-invalid");
            }
        }

        if (candidate is null) blockers.Add("current-candidate-unavailable");
        if (review is null || review.Decision != "reviewed") blockers.Add("current-reviewed-decision-unavailable");
        if (binding is null || binding.State != GeneratedToolIntegrationStore.PreparedDisabled
            || binding.ActivationState != GeneratedToolIntegrationStore.Disabled
            || binding.CallableRoute != GeneratedToolIntegrationStore.NoCallableRoute)
        {
            blockers.Add("prepared-disabled-binding-unavailable");
        }
        if (route is null)
        {
            foreach (var blocker in GeneratedToolRouteQualificationGate.BlockersFor(
                         nodeRuntime, appServerRuntimeIdentity, selectedCapability, executionPolicy))
            {
                if (!blockers.Contains(blocker, StringComparer.Ordinal)) blockers.Add(blocker);
            }
        }

        var identitiesMatch = lifecycle is null ||
            string.Equals(lifecycle.CandidateContentIdentity, candidate?.ContentIdentity, StringComparison.Ordinal)
            && string.Equals(lifecycle.ReviewIdentity, review?.ReviewIdentity, StringComparison.Ordinal)
            && string.Equals(lifecycle.BindingRecordSha256, binding?.RecordSha256, StringComparison.Ordinal);
        var activePlanMatches = activeRecord is not null && route?.InvocationPlan is { } currentPlan
            && activeRecord.CandidateContentIdentity == candidate?.ContentIdentity
            && activeRecord.ReviewIdentity == review?.ReviewIdentity
            && activeRecord.ReviewRecordSha256 == review?.RecordSha256
            && activeRecord.ReviewSnapshotIdentity == review?.ReviewSnapshotIdentity
            && activeRecord.BindingRecordSha256 == binding?.RecordSha256
            && activeRecord.InvocationPlanIdentity == currentPlan.PlanIdentity
            && activeRecord.NodeRuntimeSha256 == currentPlan.NodeSha256
            && activeRecord.InputSchemaSha256 == currentPlan.InputSchemaSha256
            && activeRecord.OutputSchemaSha256 == currentPlan.OutputSchemaSha256
            && activeRecord.DependencyIdentity == currentPlan.DependencyIdentity
            && activeRecord.AuthorityIdentity == currentPlan.AuthorityIdentity
            && activeRecord.HostQualificationIdentity == route.HostQualificationIdentity;
        var state = lifecycle?.State ?? "disabled";
        if (lifecycle is not null && !identitiesMatch)
        {
            state = "stale";
            failure = "stale";
            blockers.Add("activation-lifecycle-identities-stale");
        }
        if (activeRecord is not null && !activePlanMatches)
        {
            state = "stale";
            failure = "stale";
            blockers.Add("activation-invocation-plan-stale");
        }
        if (route?.QualificationState == "stale")
        {
            failure = "stale";
            if (lifecycle?.State is "active" or "revoked") state = "stale";
        }
        else if (route?.QualificationState == "unsupported" && failure is null)
        {
            failure = "unsupported";
        }

        var qualification = route?.QualificationState ?? "pending";
        if (failure == "stale") qualification = "pending";
        if (failure == "unsupported") qualification = "unsupported";
        if (lifecycle?.State == "active") blockers.Add("activation-already-active");

        var activationAllowed = route is not null
            && route.QualificationState == "qualified"
            && route.InvocationPlan is not null
            && route.HostQualificationIdentity is not null
            && candidate is not null
            && review?.Decision == "reviewed"
            && binding is not null
            && binding.State == GeneratedToolIntegrationStore.PreparedDisabled
            && binding.ActivationState == GeneratedToolIntegrationStore.Disabled
            && binding.CallableRoute == GeneratedToolIntegrationStore.NoCallableRoute
            && identitiesMatch
            && lifecycle?.State != "active"
            && state != "stale"
            && blockers.Count == 0;
        if (!activationAllowed && state != "active" && failure is null)
        {
            failure = "qualificationPending";
            qualification = "pending";
        }

        var inventoryConfirmation = route?.InventoryConfirmation ?? GeneratedToolMcpInventoryConfirmation.Unknown;
        var callableBlockers = route?.InventoryBlockers.ToList()
            ?? new List<string> { "callable-invocation-plan-unavailable" };
        if (activeRecord is null) callableBlockers.Add("generated-tool-not-activated");
        if (route?.HostQualificationIdentity is null) callableBlockers.Add("runtime-provider-model-evidence-not-qualified");
        if (state == "stale") callableBlockers.Add("activation-identities-or-plan-stale");
        if (activeRecord is not null && !activePlanMatches) callableBlockers.Add("activation-invocation-plan-stale");
        var callableTurnsAllowed = activePlanMatches
            && state == "active"
            && route?.QualificationState == "qualified"
            && inventoryConfirmation == GeneratedToolMcpInventoryConfirmation.ConfirmedExact
            && GeneratedToolRouteQualificationGate.FindExactMatch(
                nodeRuntime, appServerRuntimeIdentity, selectedCapability, executionPolicy) is not null
            && callableBlockers.Count == 0;
        var callableFailure = callableTurnsAllowed ? null
            : inventoryConfirmation is GeneratedToolMcpInventoryConfirmation.UnexpectedTools
                or GeneratedToolMcpInventoryConfirmation.SchemaMismatch ? "unsupported"
            : state == "stale" || route?.FailureKind == "stale" ? "stale"
            : route?.FailureKind == "unsupported" ? "unsupported"
            : "qualificationPending";

        return new GeneratedToolActivationStatusProjection(
            AttributedTo, toolId, EffectiveAuthorityFor(executionPolicy),
            activationAllowed, callableTurnsAllowed,
            qualification, failure, callableFailure,
            blockers.Distinct(StringComparer.Ordinal).ToArray(),
            callableBlockers.Distinct(StringComparer.Ordinal).ToArray(), inventoryConfirmation, state,
            candidate?.ContentIdentity, review?.ReviewIdentity, binding?.RecordSha256,
            lifecycle?.ActivationRecordSha256 ?? (lifecycle?.Event == "activated" ? lifecycle.RecordSha256 : null),
            route?.HostQualificationIdentity, appServerRuntimeIdentity?.Sha256,
            lifecycle, candidate?.Manifest.Authority);
    }

    public static GeneratedToolActivationCommandResult ActivateGeneratedTool(
        string applicationRoot,
        string toolId,
        string contentIdentity,
        string reviewIdentity,
        string expectedBindingRecordSha256,
        string note,
        GeneratedToolNodeRuntimeIdentity? nodeRuntime,
        RuntimeIdentity? appServerRuntimeIdentity,
        ModelCapabilityRecord? selectedCapability,
        ToolExecutionPolicy executionPolicy,
        McpServerStatusEntry? observedMcpStatus)
    {
        GeneratedToolActivationStore.ValidateNote(note);
        var status = GetGeneratedToolActivationStatus(applicationRoot, toolId, nodeRuntime,
            appServerRuntimeIdentity, selectedCapability, executionPolicy, observedMcpStatus);
        if (!string.Equals(status.ContentIdentity, contentIdentity, StringComparison.Ordinal)
            || !string.Equals(status.ReviewIdentity, reviewIdentity, StringComparison.Ordinal)
            || !string.Equals(status.BindingRecordSha256, expectedBindingRecordSha256, StringComparison.Ordinal))
        {
            return new GeneratedToolActivationCommandResult("stale", status with
            {
                FailureKind = "stale",
                Blockers = status.Blockers.Append("activation-request-identities-stale").Distinct(StringComparer.Ordinal).ToArray()
            }, null);
        }

        var current = GeneratedToolActivationStore.History(applicationRoot, toolId);
        var active = GeneratedToolActivationStore.FindActive(current);
        if (active is not null)
        {
            return new GeneratedToolActivationCommandResult("stale", status with
            {
                FailureKind = "stale",
                Blockers = status.Blockers.Append("revoke-current-activation-before-another-activation").ToArray()
            }, null);
        }

        GeneratedToolCandidate candidate;
        GeneratedToolReviewRecord review;
        GeneratedToolPreparedBindingRecord binding;
        GeneratedToolMcpRouteAssessment route;
        try
        {
            candidate = GeneratedToolCandidateStore.Read(applicationRoot, toolId)
                ?? throw new InvalidDataException("Generated tool candidate disappeared during activation confirmation.");
            review = GeneratedToolReviewStore.ReadCurrent(applicationRoot, toolId)
                ?? throw new InvalidDataException("Current generated tool review disappeared during activation confirmation.");
            binding = GeneratedToolIntegrationStore.ReadCurrent(applicationRoot, toolId)
                ?? throw new InvalidDataException("Current prepared binding disappeared during activation confirmation.");
            route = GeneratedToolMcpRoute.Assess(applicationRoot, candidate, review,
                nodeRuntime, appServerRuntimeIdentity, selectedCapability,
                executionPolicy, observedMcpStatus);
        }
        catch (InvalidDataException)
        {
            var stale = status with
            {
                FailureKind = "stale",
                QualificationState = "pending",
                Blockers = status.Blockers.Append("activation-identities-changed-during-confirmation").Distinct(StringComparer.Ordinal).ToArray()
            };
            return new GeneratedToolActivationCommandResult("stale", stale, null);
        }

        var confirmedIdentitiesMatch = string.Equals(candidate.ContentIdentity, contentIdentity, StringComparison.Ordinal)
            && string.Equals(review.ReviewIdentity, reviewIdentity, StringComparison.Ordinal)
            && string.Equals(binding.RecordSha256, expectedBindingRecordSha256, StringComparison.Ordinal)
            && binding.State == GeneratedToolIntegrationStore.PreparedDisabled
            && binding.ActivationState == GeneratedToolIntegrationStore.Disabled
            && binding.CallableRoute == GeneratedToolIntegrationStore.NoCallableRoute;
        var qualifies = confirmedIdentitiesMatch
            && route.QualificationState == "qualified"
            && route.InvocationPlan is not null
            && route.HostQualificationIdentity is not null
            && status.ActivationAllowed;
        var eventType = qualifies ? "activated" : "denied";
        var previousState = current.LastOrDefault()?.State ?? "disabled";
        var blockers = qualifies ? Array.Empty<string>()
            : status.Blockers.Concat(route.Blockers)
                .Concat(confirmedIdentitiesMatch ? [] : ["activation-request-identities-changed-during-confirmation"])
                .Distinct(StringComparer.Ordinal).ToArray();
        var failureKind = qualifies ? null
            : confirmedIdentitiesMatch
                ? status.FailureKind ?? route.FailureKind ?? "qualificationPending"
            : "stale";
        var record = GeneratedToolActivationStore.Append(applicationRoot, toolId, eventType,
            qualifies ? "active" : previousState,
            candidate.ContentIdentity, review.ReviewIdentity, review.RecordSha256,
            review.ReviewSnapshotIdentity, binding.RecordSha256,
            route.InvocationPlan, route.HostQualificationIdentity,
            route.QualificationState,
            failureKind,
            blockers, candidate.Manifest.Authority, note);

        var nextStatus = GetGeneratedToolActivationStatus(applicationRoot, toolId, nodeRuntime,
            appServerRuntimeIdentity, selectedCapability, executionPolicy, observedMcpStatus);
        return new GeneratedToolActivationCommandResult(
            failureKind, nextStatus, record);
    }

    public static GeneratedToolActivationCommandResult RevokeGeneratedToolActivation(
        string applicationRoot,
        string toolId,
        string expectedActivationRecordSha256,
        string note)
    {
        GeneratedToolActivationStore.ValidateNote(note);
        var current = GeneratedToolActivationStore.History(applicationRoot, toolId);
        var active = GeneratedToolActivationStore.FindActive(current);
        if (active is null || !GeneratedToolActivationStore.IsRecordHash(expectedActivationRecordSha256)
            || !string.Equals(active.RecordSha256, expectedActivationRecordSha256, StringComparison.Ordinal))
        {
            return new GeneratedToolActivationCommandResult("stale",
                GetRecoveryStatus(applicationRoot, toolId) with { FailureKind = "stale" }, null);
        }
        var record = GeneratedToolActivationStore.Revoke(
            applicationRoot, toolId, expectedActivationRecordSha256, note);
        // Recovery revocation deliberately does not inspect candidate/review/runtime files.
        var status = GetRecoveryStatus(applicationRoot, toolId);
        return new GeneratedToolActivationCommandResult(null, status, record);
    }

    public static GeneratedToolActivationHistoryProjection ListGeneratedToolActivationHistory(
        string applicationRoot, string toolId) =>
        new(toolId, GeneratedToolActivationStore.History(applicationRoot, toolId));

    internal static GeneratedToolActivationRecord? FindLifecycle(
        IReadOnlyList<GeneratedToolActivationRecord> records) =>
        records.LastOrDefault(record => record.Event is "activated" or "revoked");

    private static GeneratedToolActivationStatusProjection GetRecoveryStatus(string applicationRoot, string toolId)
    {
        var history = GeneratedToolActivationStore.History(applicationRoot, toolId);
        var lifecycle = FindLifecycle(history);
        var state = GeneratedToolActivationStore.FindActive(history) is null
            ? lifecycle?.State ?? "disabled" : "active";
        return new GeneratedToolActivationStatusProjection(
            AttributedTo, toolId, null, false, false,
            "pending", "qualificationPending", "qualificationPending",
            GeneratedToolRouteQualificationGate.Blockers, ["generated-tool-not-activated", "mcp-tool-schema-unobserved"],
            GeneratedToolMcpInventoryConfirmation.Unknown, state,
            null, null, null,
            GeneratedToolActivationStore.FindActive(history)?.RecordSha256 ?? lifecycle?.ActivationRecordSha256,
            null, null, lifecycle, null);
    }

    private static string? EffectiveAuthorityFor(ToolExecutionPolicy executionPolicy) => executionPolicy switch
    {
        ToolExecutionPolicy.Unrestricted => "danger-full-access/never",
        ToolExecutionPolicy.ApprovalQualification => "read-only/on-request",
        ToolExecutionPolicy.QualificationWorkspaceWrite => "workspace-write/never",
        _ => null
    };
}

internal static class GeneratedToolActivationStore
{
    private const int SchemaVersion = 1;
    private const int MaximumRecordBytes = 16 * 1024;
    private const int MaximumRecordsPerTool = 512;
    private const int MaximumRecordsBeforeActivationAttempt = MaximumRecordsPerTool - 2;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal static IReadOnlyList<GeneratedToolActivationRecord> History(string applicationRoot, string toolId)
    {
        var directory = DirectoryPath(applicationRoot, toolId);
        if (File.Exists(directory)) throw new InvalidDataException("Generated-tool activation history path is not a directory.");
        if (!Directory.Exists(directory)) return [];
        var entries = Directory.EnumerateFileSystemEntries(directory).Take(MaximumRecordsPerTool + 1).ToArray();
        if (entries.Length > MaximumRecordsPerTool) throw new InvalidDataException("Generated-tool activation history exceeds its record bound.");

        var records = new List<GeneratedToolActivationRecord>(entries.Length);
        foreach (var entry in entries)
        {
            GeneratedToolCandidatePath.RejectReparseComponents(entry);
            if (!File.Exists(entry) || new FileInfo(entry).Length is <= 0 or > MaximumRecordBytes)
            {
                throw new InvalidDataException("Generated-tool activation history contains an invalid record entry.");
            }
            var bytes = ReadBounded(entry);
            GeneratedToolActivationRecord record;
            try
            {
                using var document = JsonDocument.Parse(bytes);
                GeneratedToolCandidateValidator.RejectDuplicateProperties(document.RootElement);
                record = JsonSerializer.Deserialize<GeneratedToolActivationRecord>(bytes, JsonOptions)
                    ?? throw new InvalidDataException("Generated-tool activation record is empty.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Generated-tool activation record is malformed.", exception);
            }
            ValidateRecord(record, toolId, Path.GetFileName(entry));
            records.Add(record);
        }

        records.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
        var previousHash = string.Empty;
        GeneratedToolActivationRecord? active = null;
        var lifecycleState = "disabled";
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            if (record.Sequence != index + 1
                || !string.Equals(record.PreviousRecordSha256, previousHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Generated-tool activation history has a missing or unlinked record.");
            }
            switch (record.Event)
            {
                case "activated" when active is null && record.State == "active"
                    && record.QualificationState == "qualified" && record.Blockers.Count == 0:
                    active = record;
                    lifecycleState = "active";
                    break;
                case "denied" when record.State == lifecycleState
                    && (active is null
                        ? record.ActivationRecordSha256 is null
                        : string.Equals(record.ActivationRecordSha256, active.RecordSha256, StringComparison.Ordinal)):
                    break;
                case "revoked" when active is not null
                    && string.Equals(record.ActivationRecordSha256, active.RecordSha256, StringComparison.Ordinal)
                    && record.State == "revoked"
                    && SameActivationEvidence(active, record):
                    active = null;
                    lifecycleState = "revoked";
                    break;
                default:
                    throw new InvalidDataException("Generated-tool activation history contains an invalid lifecycle transition.");
            }
            previousHash = record.RecordSha256;
        }
        return records.ToArray();
    }

    internal static GeneratedToolActivationRecord? FindActive(IReadOnlyList<GeneratedToolActivationRecord> records)
    {
        GeneratedToolActivationRecord? active = null;
        foreach (var record in records)
        {
            if (record.Event == "activated") active = record;
            else if (record.Event == "revoked") active = null;
        }
        return active;
    }

    internal static bool IsRecordHash(string? value) => IsLowercaseSha256(value);

    private static bool SameActivationEvidence(
        GeneratedToolActivationRecord active, GeneratedToolActivationRecord revoked) =>
        active.CandidateContentIdentity == revoked.CandidateContentIdentity
        && active.ReviewIdentity == revoked.ReviewIdentity
        && active.ReviewRecordSha256 == revoked.ReviewRecordSha256
        && active.ReviewSnapshotIdentity == revoked.ReviewSnapshotIdentity
        && active.BindingRecordSha256 == revoked.BindingRecordSha256
        && active.InvocationPlanIdentity == revoked.InvocationPlanIdentity
        && active.NodeRuntimeSha256 == revoked.NodeRuntimeSha256
        && active.InputSchemaSha256 == revoked.InputSchemaSha256
        && active.OutputSchemaSha256 == revoked.OutputSchemaSha256
        && active.DependencyIdentity == revoked.DependencyIdentity
        && active.AuthorityIdentity == revoked.AuthorityIdentity
        && active.HostQualificationIdentity == revoked.HostQualificationIdentity
        && active.QualificationState == revoked.QualificationState;

    private static byte[] ReadBounded(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaximumRecordBytes)
        {
            throw new InvalidDataException("Generated-tool activation record is empty or exceeds its size bound.");
        }
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    internal static GeneratedToolActivationRecord Append(
        string applicationRoot, string toolId, string eventType, string state,
        string? contentIdentity, string? reviewIdentity, string? reviewRecordSha256,
        string? snapshotIdentity, string? bindingRecordSha256,
        GeneratedToolMcpInvocationPlan? plan, string? hostQualificationIdentity,
        string qualificationState, string? failureKind,
        IReadOnlyList<string> blockers, GeneratedToolAuthority? authority, string note)
    {
        var history = History(applicationRoot, toolId);
        if (history.Count >= MaximumRecordsBeforeActivationAttempt)
        {
            throw new InvalidDataException("Generated-tool activation history is full; denial/revocation capacity is reserved.");
        }
        var active = FindActive(history);
        if (eventType == "activated" && active is not null
            || eventType == "denied" && state != (active is null ? (history.LastOrDefault()?.State ?? "disabled") : "active"))
        {
            throw new InvalidDataException("Generated-tool activation transition is stale or invalid.");
        }
        if (eventType is not ("activated" or "denied"))
        {
            throw new InvalidDataException("Only activation and denial events can be appended by this operation.");
        }

        var record = new GeneratedToolActivationRecord(
            SchemaVersion, toolId, eventType, state,
            contentIdentity, reviewIdentity, reviewRecordSha256, snapshotIdentity,
            bindingRecordSha256, plan?.PlanIdentity, plan?.NodeSha256,
            plan?.InputSchemaSha256, plan?.OutputSchemaSha256,
            plan?.DependencyIdentity, plan?.AuthorityIdentity, hostQualificationIdentity,
            active?.RecordSha256, qualificationState, failureKind,
            blockers.ToArray(), authority, note, DateTimeOffset.UtcNow,
            history.Count + 1, history.Count == 0 ? string.Empty : history[^1].RecordSha256, string.Empty);
        record = record with { RecordSha256 = ComputeRecordSha256(record) };
        return Publish(applicationRoot, record, history);
    }

    internal static GeneratedToolActivationRecord Revoke(
        string applicationRoot, string toolId, string expectedActivationRecordSha256, string note)
    {
        var history = History(applicationRoot, toolId);
        var active = FindActive(history);
        if (active is null || !IsLowercaseSha256(expectedActivationRecordSha256)
            || !string.Equals(active.RecordSha256, expectedActivationRecordSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Generated-tool activation revocation identity is stale or already revoked.");
        }
        if (history.Count >= MaximumRecordsPerTool)
        {
            throw new InvalidDataException("Generated-tool activation history has no remaining revocation capacity.");
        }
        var record = new GeneratedToolActivationRecord(
            SchemaVersion, toolId, "revoked", "revoked",
            active.CandidateContentIdentity, active.ReviewIdentity, active.ReviewRecordSha256,
            active.ReviewSnapshotIdentity, active.BindingRecordSha256,
            active.InvocationPlanIdentity, active.NodeRuntimeSha256,
            active.InputSchemaSha256, active.OutputSchemaSha256,
            active.DependencyIdentity, active.AuthorityIdentity,
            active.HostQualificationIdentity,
            active.RecordSha256, active.QualificationState, null, [], active.RequestedAuthority,
            note, DateTimeOffset.UtcNow, history.Count + 1,
            history.Count == 0 ? string.Empty : history[^1].RecordSha256, string.Empty);
        record = record with { RecordSha256 = ComputeRecordSha256(record) };
        return Publish(applicationRoot, record, history);
    }

    internal static void ValidateNote(string note)
    {
        if (string.IsNullOrWhiteSpace(note) || note.Length > 2048 || note.Any(char.IsControl))
        {
            throw new InvalidDataException("Generated-tool activation requires a bounded single-line note.");
        }
        GeneratedToolCandidateValidator.RejectCredentialText(note);
    }

    private static GeneratedToolActivationRecord Publish(
        string applicationRoot, GeneratedToolActivationRecord record,
        IReadOnlyList<GeneratedToolActivationRecord> priorHistory)
    {
        var directory = DirectoryPath(applicationRoot, record.ToolId);
        Directory.CreateDirectory(directory);
        GeneratedToolCandidatePath.RejectReparseComponents(directory);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
        if (bytes.Length > MaximumRecordBytes) throw new InvalidDataException("Generated-tool activation record exceeds its size bound.");
        var stagingRoot = Path.Combine(Path.GetDirectoryName(directory)!, ".staging");
        Directory.CreateDirectory(stagingRoot);
        GeneratedToolCandidatePath.RejectReparseComponents(stagingRoot);
        var stage = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N") + ".json");
        GeneratedToolCandidatePath.RejectReparseComponents(stage);
        using (var stream = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                   bufferSize: 4096, FileOptions.WriteThrough))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        var final = Path.Combine(directory, record.Sequence.ToString("D10") + ".json");
        GeneratedToolCandidatePath.RejectReparseComponents(final);
        if (Directory.Exists(final) || File.Exists(final))
        {
            throw new InvalidDataException("Generated-tool activation history changed during publication.");
        }
        if (priorHistory.Count != 0
            && !string.Equals(History(applicationRoot, record.ToolId)[^1].RecordSha256,
                priorHistory[^1].RecordSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Generated-tool activation history changed during publication.");
        }
        try
        {
            File.Move(stage, final, overwrite: false);
        }
        catch (IOException exception) when (File.Exists(final))
        {
            throw new InvalidDataException("Generated-tool activation history changed during publication.", exception);
        }
        return record;
    }

    private static void ValidateRecord(GeneratedToolActivationRecord record, string toolId, string fileName)
    {
        if (record.SchemaVersion != SchemaVersion || record.ToolId != toolId
            || record.Event is not ("activated" or "denied" or "revoked")
            || record.State is not ("disabled" or "active" or "revoked")
            || record.QualificationState is not ("pending" or "unsupported" or "qualified")
            || record.FailureKind is not (null or "qualificationPending" or "unsupported" or "stale")
            || record.Blockers is null || record.Blockers.Count > 32
            || record.Blockers.Any(string.IsNullOrWhiteSpace)
            || record.Note is null || record.RecordedAtUtc == default || record.RecordedAtUtc.Offset != TimeSpan.Zero
            || record.Sequence is < 1 or > MaximumRecordsPerTool
            || record.PreviousRecordSha256 is null
            || (record.Sequence == 1 ? record.PreviousRecordSha256.Length != 0 : !IsLowercaseSha256(record.PreviousRecordSha256))
            || !IsLowercaseSha256(record.RecordSha256)
            || !string.Equals(fileName, record.Sequence.ToString("D10") + ".json", StringComparison.Ordinal)
            || !string.Equals(record.RecordSha256, ComputeRecordSha256(record), StringComparison.Ordinal))
        {
            throw new InvalidDataException("Generated-tool activation record has malformed fields or integrity identity.");
        }
        ValidateNote(record.Note);
        ValidateIdentity(record.CandidateContentIdentity, "candidate-v1:sha256:");
        ValidateIdentity(record.ReviewIdentity, "review-v1:sha256:");
        ValidateOptionalHash(record.ReviewRecordSha256);
        ValidateOptionalHash(record.ReviewSnapshotIdentity);
        ValidateOptionalHash(record.BindingRecordSha256);
        ValidateOptionalHash(record.InvocationPlanIdentity);
        ValidateOptionalHash(record.NodeRuntimeSha256);
        ValidateOptionalHash(record.InputSchemaSha256);
        ValidateOptionalHash(record.OutputSchemaSha256);
        ValidateOptionalHash(record.DependencyIdentity);
        ValidateOptionalHash(record.AuthorityIdentity);
        ValidateOptionalHash(record.HostQualificationIdentity);
        ValidateOptionalHash(record.ActivationRecordSha256);
        if (record.Event == "activated"
            && (record.State != "active" || record.CandidateContentIdentity is null
                || record.ReviewIdentity is null || record.ReviewSnapshotIdentity is null
                || record.ReviewRecordSha256 is null
                || record.BindingRecordSha256 is null || record.InvocationPlanIdentity is null
                || record.NodeRuntimeSha256 is null || record.InputSchemaSha256 is null
                || record.OutputSchemaSha256 is null || record.DependencyIdentity is null
                || record.AuthorityIdentity is null || record.HostQualificationIdentity is null
                || record.FailureKind is not null
                || record.QualificationState != "qualified" || record.Blockers.Count != 0
                || record.ActivationRecordSha256 is not null))
        {
            throw new InvalidDataException("Activated record does not bind a complete reviewed invocation plan.");
        }
        if (record.Event == "revoked" && (record.State != "revoked" || record.ActivationRecordSha256 is null))
        {
            throw new InvalidDataException("Revocation record does not bind an activation identity.");
        }
        if (record.Event == "denied" && record.State == "active" && record.ActivationRecordSha256 is null)
        {
            throw new InvalidDataException("Denied record lost the active activation identity.");
        }
        if (record.Event == "denied" && record.FailureKind is null)
        {
            throw new InvalidDataException("Denied activation record is missing its typed failure reason.");
        }
    }

    private static void ValidateOptionalHash(string? value)
    {
        if (value is not null && !IsLowercaseSha256(value))
        {
            throw new InvalidDataException("Generated-tool activation record contains an invalid hash.");
        }
    }

    private static void ValidateIdentity(string? value, string prefix)
    {
        if (value is not null
            && (!value.StartsWith(prefix, StringComparison.Ordinal)
                || !IsLowercaseSha256(value[prefix.Length..])))
        {
            throw new InvalidDataException("Generated-tool activation record contains an invalid product identity.");
        }
    }

    private static string DirectoryPath(string applicationRoot, string toolId)
    {
        var root = GeneratedToolCandidatePath.Root(applicationRoot);
        GeneratedToolCandidatePath.Candidate(root, toolId);
        var histories = Path.Combine(root, "Activations");
        GeneratedToolCandidatePath.RejectReparseComponents(histories);
        var directory = Path.Combine(histories, toolId);
        GeneratedToolCandidatePath.RejectReparseComponents(directory);
        return directory;
    }

    private static string ComputeRecordSha256(GeneratedToolActivationRecord record) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            record with { RecordSha256 = string.Empty }, JsonOptions))).ToLowerInvariant();

    private static bool IsLowercaseSha256(string? value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
