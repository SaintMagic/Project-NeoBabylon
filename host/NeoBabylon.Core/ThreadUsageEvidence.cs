using System.Text.Json;
using System.Text.Json.Nodes;

namespace NeoBabylon.Core;

public sealed record ThreadRateMeasurement(
    string TurnId,
    string ModelIdentifier,
    string? ProviderId,
    long OutputTokens,
    long DurationMs,
    string OutputSource,
    string Source,
    string? StartedAtUtc,
    string? CompletedAtUtc)
{
    public JsonObject ToJson() => new()
    {
        ["turnId"] = TurnId,
        ["modelIdentifier"] = ModelIdentifier,
        ["providerId"] = ProviderId,
        ["outputTokens"] = OutputTokens,
        ["durationMs"] = DurationMs,
        ["outputSource"] = OutputSource,
        ["source"] = Source,
        ["startedAtUtc"] = StartedAtUtc,
        ["completedAtUtc"] = CompletedAtUtc
    };
}

public sealed record LatestCompletedResponseEvidence(
    string ThreadId,
    string TurnId,
    string ResponseId,
    string ModelIdentifier,
    string? ProviderId,
    long OutputTokens,
    string OutputSource,
    string? ObservedAtUtc)
{
    public JsonObject ToJson() => new()
    {
        ["threadId"] = ThreadId,
        ["turnId"] = TurnId,
        ["responseId"] = ResponseId,
        ["modelIdentifier"] = ModelIdentifier,
        ["modelProvider"] = ProviderId,
        ["outputTokens"] = OutputTokens,
        ["outputSource"] = OutputSource,
        ["observedAtUtc"] = ObservedAtUtc
    };
}

public sealed record ThreadUsageSnapshot(
    JsonObject? TokenUsage = null,
    string? EvidenceSource = null,
    string? ObservedAtUtc = null,
    string? ReasoningEffort = null,
    string? ReasoningEvidenceSource = null,
    string? UsageModelIdentifier = null,
    string? UsageModelProvider = null,
    string? UsageTurnId = null)
{
    public IReadOnlyList<ThreadRateMeasurement> RateMeasurements { get; init; } = [];
    public LatestCompletedResponseEvidence? LatestCompletedResponse { get; init; }

    public JsonObject ToResult(string threadId) => new()
    {
        ["attributedTo"] = "NeoBabylon.Host",
        ["threadId"] = threadId,
        ["tokenUsage"] = TokenUsage?.DeepClone(),
        ["evidenceSource"] = EvidenceSource,
        ["observedAtUtc"] = ObservedAtUtc,
        ["reasoningEffort"] = ReasoningEffort,
        ["reasoningEvidenceSource"] = ReasoningEvidenceSource,
        ["usageModelIdentifier"] = UsageModelIdentifier,
        ["usageModelProvider"] = UsageModelProvider,
        ["usageTurnId"] = UsageTurnId,
        ["rateEvidenceVersion"] = 1,
        ["rateEvidenceSource"] = "isolated Codex session journal completed-turn measurements",
        ["rateMeasurements"] = new JsonArray(RateMeasurements.Select(measurement => (JsonNode?)measurement.ToJson()).ToArray()),
        ["latestCompletedResponse"] = LatestCompletedResponse?.ToJson()
    };
}

public static class ThreadUsageEvidence
{
    public const int MaximumReadBytes = 4 * 1024 * 1024;
    private const int MaximumHeaderBytes = 256 * 1024;
    private const int MaximumRateMeasurements = 256;
    private const long MaximumJsonInteger = 9_007_199_254_740_991L;
    private const string RateSource = "isolated Codex session journal task timing + exact turn usage";

    public static ThreadUsageSnapshot ReadJournal(
        string? sessionPath, string codexHome, string threadId, string workspace, string model,
        long minimumReasoningOffset = 0)
    {
        if (!SessionJournalToolEvidenceReader.TryResolveSessionPath(sessionPath, codexHome, out var path)) return new();
        try
        {
            // Reject junctions/symlinks anywhere on the resolved path, not just lexical escapes.
            for (var entry = new FileInfo(path) as FileSystemInfo; entry is not null; entry = entry is FileInfo file ? file.Directory : ((DirectoryInfo)entry).Parent)
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) return new();
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var snapshotLength = stream.Length;
            if (snapshotLength == 0) return new();
            var header = new byte[(int)Math.Min(snapshotLength, MaximumHeaderBytes)];
            stream.ReadExactly(header);
            var headerEnd = Array.IndexOf(header, (byte)'\n');
            if (headerEnd < 0) return new(); // Never scan an unbounded metadata record.
            var first = JsonNode.Parse(header.AsSpan(0, headerEnd));
            if (Text(first?["type"]) != "session_meta" || Text(first?["payload"]?["id"]) != threadId
                || !SamePath(Text(first?["payload"]?["cwd"]), workspace)) return new();
            var tailOffset = Math.Max(0L, snapshotLength - MaximumReadBytes);
            stream.Position = tailOffset > 0 ? tailOffset - 1 : 0;
            var discardPartialLine = tailOffset > 0 && stream.ReadByte() != '\n';
            var bytes = new byte[(int)(snapshotLength - tailOffset)];
            stream.ReadExactly(bytes); // Fixed snapshot, at most 4 MiB even when the live file grows.
            var lineStart = 0;
            if (discardPartialLine)
            {
                var partialEnd = Array.IndexOf(bytes, (byte)'\n');
                if (partialEnd < 0) return new(ReasoningEvidenceSource: "isolated Codex session journal bounded tail (no complete record)");
                lineStart = partialEnd + 1;
            }

            JsonObject? tokenUsage = null;
            string? timestamp = null, contextModel = null, contextProvider = null, contextTurn = null, effort = null;
            string? usageModel = null, usageProvider = null, usageTurn = null;
            var freshContext = false;
            var contextAfterSelection = false;
            var latestCumulativeOutput = (long?)null;
            var measurements = new List<ThreadRateMeasurement>();
            var invalidMeasurementIds = new HashSet<string>(StringComparer.Ordinal);
            LatestCompletedResponseEvidence? latestResponse = null;
            ActiveTurn? active = null;

            while (lineStart < bytes.Length)
            {
                var offset = tailOffset + lineStart; // Absolute journal byte provenance, not rebased tail offsets.
                var lineEnd = Array.IndexOf(bytes, (byte)'\n', lineStart);
                var hasNewline = lineEnd >= 0;
                if (!hasNewline) lineEnd = bytes.Length;
                var line = bytes.AsSpan(lineStart, lineEnd - lineStart);
                lineStart = hasNewline ? lineEnd + 1 : bytes.Length;
                if (line.IsEmpty || line.IndexOfAnyExcept((byte)' ', (byte)'\r', (byte)'\t') < 0) continue;

                JsonObject? record;
                try { record = JsonNode.Parse(line) as JsonObject; }
                catch (JsonException)
                {
                    if (!hasNewline) break; // Ignore a live, truncated final record; complete earlier pairs remain usable.
                    return new();
                }
                var payload = record?["payload"] as JsonObject;
                if (payload is null) return new();
                var recordType = Text(record?["type"]);
                var type = recordType == "event_msg" ? Text(payload["type"]) : recordType;

                if (active is not null && (type is "compacted" or "context_compacted" or "context_compaction" or "compaction_trigger" or "compaction"))
                    active.Invalid = true;

                if (type == "turn_context")
                {
                    contextModel = Text(payload["model"]);
                    contextProvider = Text(payload["model_provider"]); // Missing provider stays Unknown.
                    contextTurn = Text(payload["turn_id"]);
                    var contextWorkspace = SamePath(Text(payload["cwd"]), workspace);
                    contextAfterSelection = offset >= minimumReasoningOffset;
                    freshContext = contextAfterSelection && contextModel == model && contextWorkspace;
                    effort = freshContext && CapabilitySwitchSafety.IsPinnedReasoningEffort(Text(payload["effort"])) ? Text(payload["effort"]) : null;

                    if (active is { Completed: false })
                    {
                        if (string.IsNullOrWhiteSpace(contextTurn) || !contextWorkspace
                            || active.TurnId is not null && active.TurnId != contextTurn
                            || active.ModelIdentifier is not null && active.ModelIdentifier != contextModel
                            || active.ProviderId is not null && active.ProviderId != contextProvider)
                            active.Invalid = true;
                        else
                        {
                            active.TurnId ??= contextTurn;
                            active.ModelIdentifier ??= contextModel;
                            active.ProviderId ??= contextProvider;
                        }
                    }
                }

                if (type == "task_started")
                {
                    FinalizeActive(active, measurements, invalidMeasurementIds, threadId, ref latestResponse);
                    active = new ActiveTurn
                    {
                        DeclaredTurnId = Text(payload["turn_id"]),
                        StartedAtUtc = ValidTimestamp(Text(payload["started_at"]) ?? Text(record?["timestamp"])),
                        BaselineOutput = latestCumulativeOutput
                    };
                    continue;
                }

                if (type is "task_aborted" or "turn_aborted" or "task_failed" or "turn_failed")
                {
                    if (active is not null) active.Invalid = true;
                    continue;
                }

                if (type == "token_count")
                {
                    var mapped = MapUsage(payload["info"] as JsonObject);
                    tokenUsage = mapped;
                    timestamp = ValidTimestamp(Text(record?["timestamp"]));
                    usageModel = contextModel;
                    usageProvider = contextProvider;
                    usageTurn = contextTurn;
                    latestCumulativeOutput = mapped?["total"]?["outputTokens"] is JsonValue outputValue
                        && outputValue.TryGetValue<long>(out var outputCount) ? outputCount : null;
                    if (tokenUsage is not null && !freshContext) tokenUsage["modelContextWindow"] = null;
                    if (active is { Completed: false } && active.TurnId == contextTurn)
                    {
                        if (contextModel != active.ModelIdentifier || contextProvider != active.ProviderId || mapped is null || latestCumulativeOutput is null)
                            active.Invalid = true;
                        else active.AfterOutput = latestCumulativeOutput;
                    }
                    continue;
                }

                if (type == "token_usage_record")
                {
                    if (active is not null && !active.Invalid && Text(payload["thread_id"]) == threadId
                        && Text(payload["turn_id"]) == active.TurnId)
                    {
                        var responseId = Text(payload["response_id"]);
                        var responseOutput = TryCount(payload["usage"]?["output_tokens"]);
                        var turnOutput = TryCount(payload["turn_token_usage"]?["output_tokens"]);
                        if (responseId is null || responseOutput is null)
                            active.Invalid = true;
                        else if (active.ResponseRows.TryGetValue(responseId, out var existing))
                        {
                            if (existing.ResponseOutput != responseOutput || existing.TurnOutput != turnOutput) active.Invalid = true;
                        }
                        else active.ResponseRows.Add(responseId, new ResponseUsage(responseId, responseOutput.Value, turnOutput));
                    }
                    continue;
                }

                if (type == "task_complete" && active is not null)
                {
                    var completedTurnId = Text(payload["turn_id"]);
                    if (active.TurnId is null || active.TurnId != completedTurnId || active.DeclaredTurnId is not null && active.DeclaredTurnId != completedTurnId
                        || payload.ContainsKey("error") && payload["error"] is not null)
                    {
                        active.Invalid = true;
                        continue;
                    }
                    active.DurationMs = PositiveCount(payload["duration_ms"]);
                    active.CompletedAtUtc = ValidTimestamp(Text(payload["completed_at"]) ?? Text(record?["timestamp"]));
                    var completedStart = ValidTimestamp(Text(payload["started_at"]));
                    active.StartedAtUtc ??= completedStart;
                    if (active.DurationMs is null && TryElapsedMilliseconds(active.StartedAtUtc, active.CompletedAtUtc, out var elapsed)) active.DurationMs = elapsed;
                    if (active.DurationMs is null) active.Invalid = true;
                    active.Completed = true;
                }
            }

            FinalizeActive(active, measurements, invalidMeasurementIds, threadId, ref latestResponse);
            return new ThreadUsageSnapshot(tokenUsage, tokenUsage is null ? null : "isolated Codex session journal event_msg.token_count.info",
                tokenUsage is null ? null : timestamp, effort, contextAfterSelection
                    ? freshContext ? "isolated Codex session journal turn_context.effort" : "isolated Codex session journal turn_context (selected identity mismatch)"
                    : tailOffset > 0 ? "isolated Codex session journal bounded tail (matching turn context unavailable)" : null,
                tokenUsage is null ? null : usageModel, tokenUsage is null ? null : usageProvider, tokenUsage is null ? null : usageTurn)
            {
                RateMeasurements = measurements.TakeLast(MaximumRateMeasurements).ToArray(),
                LatestCompletedResponse = latestResponse
            };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return new();
        }
    }

    private static void FinalizeActive(ActiveTurn? active, List<ThreadRateMeasurement> measurements,
        HashSet<string> invalidIds, string threadId, ref LatestCompletedResponseEvidence? latestResponse)
    {
        if (active is null || !active.Completed || active.Invalid || active.TurnId is null || active.ModelIdentifier is null) return;

        foreach (var response in active.ResponseRows.Values)
            latestResponse = new LatestCompletedResponseEvidence(threadId, active.TurnId, response.ResponseId,
                active.ModelIdentifier, active.ProviderId, response.ResponseOutput, "token_usage_record.usage.output_tokens", active.CompletedAtUtc);

        if (active.DurationMs is null || active.DurationMs <= 0 || invalidIds.Contains(active.TurnId)) return;

        long output;
        string outputSource;
        if (active.ResponseRows.Count > 0)
        {
            var rows = active.ResponseRows.Values.ToArray();
            var turnCounts = rows.Select(row => row.TurnOutput).ToArray();
            if (turnCounts.All(value => value is not null))
            {
                long? previous = null;
                foreach (var count in turnCounts)
                {
                    if (previous is not null && count < previous) return;
                    previous = count;
                }
                output = turnCounts[^1]!.Value;
                outputSource = "token_usage_record.turn_token_usage.output_tokens";
            }
            else if (turnCounts.All(value => value is null))
            {
                try { output = rows.Aggregate(0L, (sum, row) => checked(sum + row.ResponseOutput)); }
                catch (OverflowException) { return; }
                outputSource = "deduplicated token_usage_record.usage.output_tokens";
            }
            else return; // A partial or contradictory exact usage set must not fall back to a looser count.
        }
        else
        {
            if (active.BaselineOutput is null || active.AfterOutput is null || active.AfterOutput < active.BaselineOutput) return;
            output = active.AfterOutput.Value - active.BaselineOutput.Value;
            outputSource = "event_msg.token_count cumulative output delta";
        }

        var measurement = new ThreadRateMeasurement(active.TurnId, active.ModelIdentifier, active.ProviderId,
            output, active.DurationMs.Value, outputSource, RateSource, active.StartedAtUtc, active.CompletedAtUtc);
        var existing = measurements.FindIndex(value => value.TurnId == active.TurnId);
        if (existing >= 0)
        {
            if (measurements[existing] != measurement)
            {
                measurements.RemoveAt(existing);
                invalidIds.Add(active.TurnId);
                return;
            }
        }
        else measurements.Add(measurement);

    }

    private static bool TryElapsedMilliseconds(string? startedAtUtc, string? completedAtUtc, out long elapsed)
    {
        elapsed = 0;
        if (!DateTimeOffset.TryParse(startedAtUtc, out var started) || !DateTimeOffset.TryParse(completedAtUtc, out var completed)) return false;
        var value = (completed - started).TotalMilliseconds;
        if (!double.IsFinite(value) || value <= 0 || value > long.MaxValue) return false;
        elapsed = (long)Math.Round(value, MidpointRounding.AwayFromZero);
        return elapsed > 0;
    }

    private static JsonObject? MapUsage(JsonObject? source)
    {
        if (source is null) return null;
        var total = Breakdown(source["total_token_usage"] as JsonObject);
        var last = Breakdown(source["last_token_usage"] as JsonObject);
        var context = source["model_context_window"];
        if (total is null || last is null || context is not null && (context is not JsonValue value || !value.TryGetValue<long>(out var count) || count <= 0 || count > MaximumJsonInteger)) return null;
        return new JsonObject { ["total"] = total, ["last"] = last, ["modelContextWindow"] = context?.DeepClone() };
    }

    private static JsonObject? Breakdown(JsonObject? source)
    {
        if (source is null) return null;
        var result = new JsonObject();
        foreach (var (wire, exposed) in new[] { ("total_tokens", "totalTokens"), ("input_tokens", "inputTokens"), ("cached_input_tokens", "cachedInputTokens"), ("cache_write_input_tokens", "cacheWriteInputTokens"), ("output_tokens", "outputTokens"), ("reasoning_output_tokens", "reasoningOutputTokens") })
        {
            var field = source[wire];
            if (field is null && wire == "cache_write_input_tokens") { result[exposed] = null; continue; }
            if (field is not JsonValue value || !value.TryGetValue<long>(out var count) || count < 0 || count > MaximumJsonInteger) return null;
            result[exposed] = count;
        }
        return result;
    }

    private static long? TryCount(JsonNode? value) => value is JsonValue json && json.TryGetValue<long>(out var count) && count >= 0 && count <= MaximumJsonInteger ? count : null;
    private static long? PositiveCount(JsonNode? value) => TryCount(value) is > 0 and var count ? count : null;
    private static bool SamePath(string? actual, string expected) => !string.IsNullOrWhiteSpace(actual)
        && Path.IsPathFullyQualified(actual)
        && string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(actual)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(expected)), StringComparison.OrdinalIgnoreCase);
    private static string? ValidTimestamp(string? timestamp) => DateTimeOffset.TryParse(timestamp, out var parsed) ? parsed.ToUniversalTime().ToString("O") : null;
    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private sealed class ActiveTurn
    {
        public string? DeclaredTurnId { get; init; }
        public string? TurnId { get; set; }
        public string? ModelIdentifier { get; set; }
        public string? ProviderId { get; set; }
        public string? StartedAtUtc { get; set; }
        public string? CompletedAtUtc { get; set; }
        public long? DurationMs { get; set; }
        public long? BaselineOutput { get; init; }
        public long? AfterOutput { get; set; }
        public bool Completed { get; set; }
        public bool Invalid { get; set; }
        public Dictionary<string, ResponseUsage> ResponseRows { get; } = new(StringComparer.Ordinal);
    }

    private sealed record ResponseUsage(string ResponseId, long ResponseOutput, long? TurnOutput);
}
