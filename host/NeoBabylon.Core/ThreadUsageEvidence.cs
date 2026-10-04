using System.Text.Json;
using System.Text.Json.Nodes;

namespace NeoBabylon.Core;

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
        ["usageTurnId"] = UsageTurnId
    };
}

public static class ThreadUsageEvidence
{
    public const int MaximumReadBytes = 4 * 1024 * 1024;
    private const int MaximumHeaderBytes = 256 * 1024;
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
            var discardPartialLine = false;
            stream.Position = tailOffset > 0 ? tailOffset - 1 : 0;
            if (tailOffset > 0) discardPartialLine = stream.ReadByte() != '\n';
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
            bool freshContext = false;
            bool contextAfterSelection = false;
            while (lineStart < bytes.Length)
            {
                var offset = tailOffset + lineStart; // Absolute journal byte provenance, not rebased tail offsets.
                var lineEnd = Array.IndexOf(bytes, (byte)'\n', lineStart);
                if (lineEnd < 0) lineEnd = bytes.Length;
                var line = bytes.AsSpan(lineStart, lineEnd - lineStart);
                lineStart = lineEnd + 1;
                if (line.IsEmpty || line.IndexOfAnyExcept((byte)' ', (byte)'\r', (byte)'\t') < 0) continue;
                var record = JsonNode.Parse(line);
                var payload = record?["payload"] as JsonObject;
                if (payload is null) return new();
                if (Text(record?["type"]) == "turn_context")
                {
                    contextModel = Text(payload["model"]);
                    contextProvider = Text(payload["model_provider"]); // Absent in pinned TurnContext; do not infer it from current selection/session creation.
                    contextTurn = Text(payload["turn_id"]);
                    contextAfterSelection = offset >= minimumReasoningOffset;
                    freshContext = contextAfterSelection && contextModel == model && SamePath(Text(payload["cwd"]), workspace);
                    effort = freshContext && CapabilitySwitchSafety.IsPinnedReasoningEffort(Text(payload["effort"])) ? Text(payload["effort"]) : null;
                }
                if (Text(record?["type"]) == "event_msg" && Text(payload["type"]) == "token_count")
                {
                    tokenUsage = MapUsage(payload["info"] as JsonObject);
                    timestamp = ValidTimestamp(Text(record?["timestamp"]));
                    usageModel = contextModel;
                    usageProvider = contextProvider;
                    usageTurn = contextTurn;
                    if (tokenUsage is not null && !freshContext) tokenUsage["modelContextWindow"] = null;
                }
            }
            return new(tokenUsage, tokenUsage is null ? null : "isolated Codex session journal event_msg.token_count.info",
                tokenUsage is null ? null : timestamp, effort, contextAfterSelection
                    ? freshContext ? "isolated Codex session journal turn_context.effort" : "isolated Codex session journal turn_context (selected identity mismatch)"
                    : tailOffset > 0 ? "isolated Codex session journal bounded tail (matching turn context unavailable)" : null,
                tokenUsage is null ? null : usageModel, tokenUsage is null ? null : usageProvider, tokenUsage is null ? null : usageTurn);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return new();
        }
    }

    private static JsonObject? MapUsage(JsonObject? source)
    {
        if (source is null) return null;
        var total = Breakdown(source["total_token_usage"] as JsonObject);
        var last = Breakdown(source["last_token_usage"] as JsonObject);
        var context = source["model_context_window"];
        if (total is null || last is null || context is not null && (context is not JsonValue value || !value.TryGetValue<long>(out var count) || count <= 0 || count > 9_007_199_254_740_991L)) return null;
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
            if (field is not JsonValue value || !value.TryGetValue<long>(out var count) || count < 0 || count > 9_007_199_254_740_991L) return null;
            result[exposed] = count;
        }
        return result;
    }

    private static bool SamePath(string? actual, string expected) => !string.IsNullOrWhiteSpace(actual)
        && Path.IsPathFullyQualified(actual)
        && string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(actual)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(expected)), StringComparison.OrdinalIgnoreCase);
    private static string? ValidTimestamp(string? timestamp) => DateTimeOffset.TryParse(timestamp, out var parsed) ? parsed.ToUniversalTime().ToString("O") : null;
    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
