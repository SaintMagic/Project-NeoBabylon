using System.Text.Json.Nodes;
using System.Text;
using System.Text.RegularExpressions;

namespace NeoBabylon.Core;

/// <summary>
/// Bounds output payloads before App Server notifications are retained or sent to WebView.
/// App Server history remains the authoritative source for completed items.
/// </summary>
public sealed class AppServerNotificationProjection
{
    public const int AssistantDisplayLimit = 120_000;
    public const int ToolOutputDisplayLimit = 40_000;
    private const int ReasoningDisplayLimit = 120_000;
    private const int MaximumReasoningParts = 128;

    private static readonly HashSet<string> ToolItemTypes =
    [
        "commandExecution",
        "fileChange",
        "mcpToolCall",
        "dynamicToolCall",
        "functionCallOutput"
    ];

    private static readonly HashSet<string> AssistantCompletedItemFields = ["id", "type", "text"];
    private static readonly HashSet<string> ContextCompactionCompletedItemFields = ["id", "type", "status"];
    private static readonly HashSet<string> ToolCompletedItemFields =
    [
        "id", "type", "status", "command", "tool", "aggregatedOutput", "output", "result", "error",
        "exitCode", "durationMs", "callId", "contentItems", "success"
    ];

    private static readonly Regex UpstreamOmissionMarker = new(
        @"\.\.\.\s+\d+\s+bytes omitted\s+\.\.\.",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly Dictionary<string, AssistantDeltaState> _assistantDeltas = new(StringComparer.Ordinal);
    private readonly HashSet<string> _completedAssistantItems = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ReasoningDeltaState> _reasoningDeltas = new(StringComparer.Ordinal);
    private readonly HashSet<string> _completedReasoningItems = new(StringComparer.Ordinal);
    private int _assistantCharactersDisplayed;
    private int _reasoningCharactersDisplayed;

    private sealed class AssistantDeltaState
    {
        public int DisplayedCharacters { get; set; }
        public int OmittedCharacters { get; set; }
        public int OriginalCharacters { get; set; }
    }

    private sealed class ReasoningDeltaState
    {
        public string ThreadId { get; set; } = "";
        public string TurnId { get; set; } = "";
        public string ItemId { get; set; } = "";
        public int DisplayedCharacters { get; set; }
        public int OmittedCharacters { get; set; }
        public int OriginalCharacters { get; set; }
        public int OmittedParts { get; set; }
    }

    public AppServerNotification? Project(AppServerNotification notification)
    {
        if (notification.Method == "item/commandExecution/outputDelta")
        {
            return null;
        }

        if (notification.Method is "item/agentMessage/delta" or "item/assistantMessage/delta")
        {
            return ProjectAssistantDelta(notification);
        }

        if (notification.Method is "item/reasoning/summaryTextDelta" or "item/reasoning/textDelta")
        {
            return ProjectReasoningDelta(notification);
        }

        if (notification.Method == "item/completed"
            && notification.Params["item"] is JsonObject sourceItem
            && sourceItem["type"]?.GetValue<string>() is { } itemType)
        {
            if (itemType == "agentMessage" || ToolItemTypes.Contains(itemType))
            {
                if (itemType == "agentMessage" && StringValue(sourceItem["id"]) is { } itemId)
                {
                    _completedAssistantItems.Add(itemId);
                }

                return ProjectCompletedItem(notification, sourceItem, itemType);
            }

            if (itemType == "reasoning")
            {
                var key = ReasoningKey(
                    StringValue(notification.Params["threadId"]),
                    StringValue(notification.Params["turnId"]),
                    StringValue(sourceItem["id"]));
                if (key is not null)
                {
                    _completedReasoningItems.Add(key);
                    _reasoningDeltas.Remove(key);
                }
                return ProjectCompletedReasoningItem(notification, sourceItem);
            }

            if (string.Equals(
                    itemType.Replace("_", string.Empty, StringComparison.Ordinal),
                    "contextCompaction",
                    StringComparison.OrdinalIgnoreCase))
            {
                return ProjectCompletedItem(notification, sourceItem, "contextCompaction");
            }

            // Completed user/reasoning and unknown item payloads are persisted by App Server;
            // NeoBabylon's renderer does not need a second copy of them.
            return null;
        }

        return notification;
    }

    public IReadOnlyList<AppServerNotification> Complete(bool turnCompleted)
    {
        var result = new List<AppServerNotification>();
        foreach (var (itemId, state) in _assistantDeltas)
        {
            if (state.OmittedCharacters == 0 || _completedAssistantItems.Contains(itemId))
            {
                continue;
            }

            result.Add(new AppServerNotification("neobabylon/outputTruncated", new JsonObject
            {
                ["itemId"] = itemId,
                ["itemType"] = "agentMessage",
                ["neoBabylonDisplay"] = DisplayMetadata(
                    state.OriginalCharacters,
                    state.OmittedCharacters,
                    turnCompleted && _completedAssistantItems.Contains(itemId),
                    upstreamTruncated: false)
            }));
        }

        foreach (var state in _reasoningDeltas.Values)
        {
            if (state.OmittedCharacters == 0 && state.OmittedParts == 0) continue;
            var metadata = DisplayMetadata(
                state.OriginalCharacters,
                state.OmittedCharacters,
                sourceRetained: false,
                upstreamTruncated: false);
            if (state.OmittedParts > 0) metadata["omittedParts"] = true;
            result.Add(new AppServerNotification("neobabylon/reasoningTruncated", new JsonObject
            {
                ["threadId"] = state.ThreadId,
                ["turnId"] = state.TurnId,
                ["itemId"] = state.ItemId,
                ["itemType"] = "reasoning",
                ["neoBabylonDisplay"] = metadata
            }));
        }

        return result;
    }

    private AppServerNotification? ProjectAssistantDelta(AppServerNotification notification)
    {
        var delta = StringValue(notification.Params["delta"]);
        if (string.IsNullOrEmpty(delta))
        {
            return notification;
        }

        var itemId = StringValue(notification.Params["itemId"]) ?? "assistant-current";
        if (!_assistantDeltas.TryGetValue(itemId, out var state))
        {
            if (_assistantDeltas.Count >= 64)
            {
                return null;
            }

            state = new AssistantDeltaState();
            _assistantDeltas.Add(itemId, state);
        }

        var remaining = Math.Max(0, AssistantDisplayLimit - _assistantCharactersDisplayed);
        var displayed = Math.Min(remaining, delta.Length);
        state.DisplayedCharacters += displayed;
        state.OriginalCharacters += delta.Length;
        state.OmittedCharacters += delta.Length - displayed;
        _assistantCharactersDisplayed += displayed;
        if (displayed == 0)
        {
            return null;
        }

        var parameters = (JsonObject)notification.Params.DeepClone();
        parameters["delta"] = delta[..displayed];
        if (state.OmittedCharacters > 0)
        {
            parameters["neoBabylonDisplay"] = DisplayMetadata(
                state.OriginalCharacters,
                state.OmittedCharacters,
                sourceRetained: false,
                upstreamTruncated: false);
        }

        return new AppServerNotification(notification.Method, parameters);
    }

    private AppServerNotification? ProjectReasoningDelta(AppServerNotification notification)
    {
        var threadId = StringValue(notification.Params["threadId"]);
        var turnId = StringValue(notification.Params["turnId"]);
        var itemId = StringValue(notification.Params["itemId"]);
        var delta = StringValue(notification.Params["delta"]);
        var indexName = notification.Method.EndsWith("summaryTextDelta", StringComparison.Ordinal)
            ? "summaryIndex"
            : "contentIndex";
        if (threadId is null || turnId is null || itemId is null || delta is null
            || notification.Params[indexName] is not JsonValue indexValue
            || !indexValue.TryGetValue<int>(out var index)
            || index < 0)
        {
            return null;
        }

        var key = ReasoningKey(threadId, turnId, itemId)!;
        if (_completedReasoningItems.Contains(key)) return null;
        if (!_reasoningDeltas.TryGetValue(key, out var state))
        {
            if (_reasoningDeltas.Count >= 64) return null;
            state = new ReasoningDeltaState { ThreadId = threadId, TurnId = turnId, ItemId = itemId };
            _reasoningDeltas.Add(key, state);
        }

        if (index >= MaximumReasoningParts)
        {
            state.OriginalCharacters += delta.Length;
            state.OmittedCharacters += delta.Length;
            state.OmittedParts++;
            return null;
        }

        var remaining = Math.Max(0, ReasoningDisplayLimit - _reasoningCharactersDisplayed);
        var displayed = Math.Min(delta.Length, remaining);
        state.DisplayedCharacters += displayed;
        state.OriginalCharacters += delta.Length;
        state.OmittedCharacters += delta.Length - displayed;
        _reasoningCharactersDisplayed += displayed;
        if (displayed == 0) return null;

        var parameters = (JsonObject)notification.Params.DeepClone();
        parameters["delta"] = delta[..displayed];
        if (state.OmittedCharacters > 0)
        {
            parameters["neoBabylonDisplay"] = DisplayMetadata(
                state.OriginalCharacters,
                state.OmittedCharacters,
                sourceRetained: false,
                upstreamTruncated: false);
        }
        return new AppServerNotification(notification.Method, parameters);
    }

    private static AppServerNotification ProjectCompletedReasoningItem(
        AppServerNotification notification,
        JsonObject sourceItem)
    {
        const int displayLimit = ReasoningDisplayLimit;
        var parameters = new JsonObject();
        foreach (var property in notification.Params)
        {
            if (property.Key != "item") parameters[property.Key] = property.Value?.DeepClone();
        }

        var item = new JsonObject
        {
            ["id"] = sourceItem["id"]?.DeepClone(),
            ["type"] = "reasoning"
        };
        var remaining = displayLimit;
        var omittedCharacters = 0;
        var useContent = HasReasoningText(sourceItem["content"]);
        var selectedSource = sourceItem[useContent ? "content" : "summary"];
        var omittedParts = CopyReasoningParts(
            selectedSource, item, useContent ? "content" : "summary", ref remaining, ref omittedCharacters);
        item[useContent ? "summary" : "content"] = new JsonArray();
        if (omittedCharacters > 0 || omittedParts)
        {
            var metadata = DisplayMetadata(
                displayLimit + omittedCharacters,
                omittedCharacters,
                sourceRetained: true,
                upstreamTruncated: false);
            if (omittedParts) metadata["omittedParts"] = true;
            item["neoBabylonDisplay"] = metadata;
        }
        parameters["item"] = item;
        return new AppServerNotification(notification.Method, parameters);
    }

    private static bool HasReasoningText(JsonNode? source)
    {
        if (source is not JsonArray parts) return false;
        for (var index = 0; index < Math.Min(parts.Count, MaximumReasoningParts); index++)
        {
            if (parts[index] is JsonValue value
                && value.TryGetValue<string>(out var text)
                && text.Length > 0) return true;
        }
        return parts.Count > MaximumReasoningParts;
    }

    private static bool CopyReasoningParts(
        JsonNode? source,
        JsonObject destination,
        string propertyName,
        ref int remaining,
        ref int omittedCharacters)
    {
        var parts = new JsonArray();
        var omittedParts = false;
        if (source is JsonArray sourceParts)
        {
            var retainedParts = Math.Min(sourceParts.Count, MaximumReasoningParts);
            for (var index = 0; index < retainedParts; index++)
            {
                var part = sourceParts[index];
                if (part is not JsonValue value || !value.TryGetValue<string>(out var text)) continue;
                var displayedLength = Math.Min(text.Length, remaining);
                parts.Add(text[..displayedLength]);
                remaining -= displayedLength;
                omittedCharacters += text.Length - displayedLength;
            }
            omittedParts = sourceParts.Count > retainedParts;
        }
        destination[propertyName] = parts;
        return omittedParts;
    }

    private static string? ReasoningKey(string? threadId, string? turnId, string? itemId) =>
        threadId is null || turnId is null || itemId is null
            ? null
            : $"{threadId.Length}:{threadId}{turnId.Length}:{turnId}{itemId.Length}:{itemId}";

    private static AppServerNotification ProjectCompletedItem(
        AppServerNotification notification,
        JsonObject sourceItem,
        string itemType)
    {
        var parameters = new JsonObject();
        foreach (var property in notification.Params)
        {
            if (property.Key != "item")
            {
                parameters[property.Key] = property.Value?.DeepClone();
            }
        }

        var item = new JsonObject();
        JsonObject? displayMetadata = null;
        foreach (var property in sourceItem)
        {
            var allowedFields = itemType switch
            {
                "agentMessage" => AssistantCompletedItemFields,
                "contextCompaction" => ContextCompactionCompletedItemFields,
                _ => ToolCompletedItemFields
            };
            if (!allowedFields.Contains(property.Key))
            {
                continue;
            }

            var limit = itemType == "agentMessage" && property.Key == "text"
                ? AssistantDisplayLimit
                : itemType != "agentMessage" && itemType != "contextCompaction" ? property.Key switch
                {
                    "aggregatedOutput" or "output" or "result" or "contentItems" => ToolOutputDisplayLimit,
                    "error" => 8_000,
                    "command" => 4_000,
                    "tool" => 2_000,
                    _ => 0
                }
                : 0;

            if (limit > 0 && ToolOutputText.Text(property.Value) is { } text)
            {
                var displayedLength = Math.Min(text.Length, limit);
                item[property.Key] = text[..displayedLength];
                if (displayedLength < text.Length)
                {
                    displayMetadata = DisplayMetadata(
                        text.Length,
                        text.Length - displayedLength,
                        sourceRetained: true,
                        upstreamTruncated: UpstreamOmissionMarker.IsMatch(text));
                }
            }
            else
            {
                item[property.Key] = property.Value?.DeepClone();
            }
        }

        if (ToolItemTypes.Contains(itemType))
        {
            var originalOutput = ToolOutputText.Read(sourceItem);
            var displayedOutput = ToolOutputText.Read(item);
            displayMetadata = originalOutput is not null
                && (originalOutput.Length > (displayedOutput?.Length ?? 0) || UpstreamOmissionMarker.IsMatch(originalOutput))
                ? DisplayMetadata(originalOutput.Length, Math.Max(0, originalOutput.Length - (displayedOutput?.Length ?? 0)), true, UpstreamOmissionMarker.IsMatch(originalOutput))
                : null;
        }

        if (displayMetadata is not null)
        {
            item["neoBabylonDisplay"] = displayMetadata;
        }

        parameters["item"] = item;
        return new AppServerNotification(notification.Method, parameters);
    }

    private static JsonObject DisplayMetadata(
        int originalCharacters,
        int omittedCharacters,
        bool sourceRetained,
        bool upstreamTruncated) => new()
    {
        ["displayTruncated"] = omittedCharacters > 0,
        ["originalCharacters"] = originalCharacters,
        ["omittedCharacters"] = omittedCharacters,
        ["sourceRetained"] = sourceRetained,
        ["upstreamTruncated"] = upstreamTruncated
    };

    private static string? StringValue(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}

/// <summary>
/// Retains a bounded summary for post-turn host logic while the notification observer
/// receives the ordered display-safe stream directly.
/// </summary>
internal sealed class BoundedTurnNotificationAccumulator
{
    private const int MaximumRetainedNotifications = 512;
    private const int MaximumRetainedJsonCharacters = 1_000_000;
    private const int MaximumAssistantItems = 64;

    private sealed class DeltaEntry(string itemId)
    {
        public string ItemId { get; } = itemId;
        public StringBuilder Text { get; } = new();
        public JsonObject? DisplayMetadata { get; set; }
    }

    private readonly List<AppServerNotification> _notifications = [];
    private readonly Dictionary<string, DeltaEntry> _deltas = new(StringComparer.Ordinal);
    private int _retainedJsonCharacters;

    public bool Truncated { get; private set; }

    public void Add(AppServerNotification notification)
    {
        if (notification.Method is "item/agentMessage/delta" or "item/assistantMessage/delta")
        {
            AddAssistantDelta(notification);
            return;
        }

        var terminal = notification.Method is "turn/completed" or "turn/failed" or "turn/aborted" or "error";
        if (_notifications.Count >= MaximumRetainedNotifications && !terminal)
        {
            Truncated = true;
            return;
        }

        var size = notification.Params.ToJsonString().Length;
        if (_retainedJsonCharacters + size > MaximumRetainedJsonCharacters && !terminal)
        {
            Truncated = true;
            return;
        }

        _notifications.Add(notification);
        _retainedJsonCharacters += size;
        if (_notifications.Count > MaximumRetainedNotifications)
        {
            Truncated = true;
        }
    }

    public IReadOnlyList<AppServerNotification> Snapshot()
    {
        var result = new List<AppServerNotification>(_notifications.Count + _deltas.Count);
        result.AddRange(_notifications);
        foreach (var delta in _deltas.Values)
        {
            var parameters = new JsonObject
            {
                ["itemId"] = delta.ItemId,
                ["delta"] = delta.Text.ToString()
            };
            if (delta.DisplayMetadata is not null)
            {
                parameters["neoBabylonDisplay"] = delta.DisplayMetadata.DeepClone();
            }

            result.Add(new AppServerNotification("item/agentMessage/delta", parameters));
        }

        return result;
    }

    private void AddAssistantDelta(AppServerNotification notification)
    {
        var itemId = notification.Params["itemId"]?.GetValue<string>() ?? "assistant-current";
        if (!_deltas.TryGetValue(itemId, out var entry))
        {
            if (_deltas.Count >= MaximumAssistantItems)
            {
                Truncated = true;
                return;
            }

            entry = new DeltaEntry(itemId);
            _deltas.Add(itemId, entry);
        }

        if (notification.Params["delta"]?.GetValue<string>() is { } text)
        {
            entry.Text.Append(text);
        }

        if (notification.Params["neoBabylonDisplay"] is JsonObject metadata)
        {
            entry.DisplayMetadata = metadata;
        }
    }
}
