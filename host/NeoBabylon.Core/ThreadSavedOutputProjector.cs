using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NeoBabylon.Core;

public sealed record ThreadSavedOutputProjectionResult(JsonArray Outputs, bool Truncated);

public static class ThreadSavedOutputProjector
{
    private const int MaximumOutputItems = 64;
    private const int MaximumTotalDisplayCharacters = 120_000;
    private const int MaximumTitleCharacters = 2_000;

    private static readonly HashSet<string> ToolItemTypes =
    [
        "commandExecution",
        "fileChange",
        "mcpToolCall",
        "dynamicToolCall",
        "functionCallOutput"
    ];

    private static readonly Regex UpstreamOmissionMarker = new(
        @"\.\.\.\s+\d+\s+bytes omitted\s+\.\.\.",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static ThreadSavedOutputProjectionResult Project(JsonArray sourceTurns)
    {
        var newestFirst = new List<JsonObject>();
        var remaining = MaximumTotalDisplayCharacters;
        var truncated = false;
        foreach (var turn in sourceTurns.OfType<JsonObject>())
        {
            var turnId = StringValue(turn["id"]);
            if (string.IsNullOrWhiteSpace(turnId) || turn["items"] is not JsonArray items)
            {
                continue;
            }

            foreach (var item in items.OfType<JsonObject>().Reverse())
            {
                var itemType = StringValue(item["type"]);
                if (itemType is null || !ToolItemTypes.Contains(itemType))
                {
                    continue;
                }

                if (newestFirst.Count >= MaximumOutputItems)
                {
                    truncated = true;
                    break;
                }

                var outputText = FirstString(item, "error", "aggregatedOutput", "output", "result");
                var displayedLength = Math.Min(outputText?.Length ?? 0,
                    Math.Min(AppServerNotificationProjection.ToolOutputDisplayLimit, remaining));
                var omittedCharacters = (outputText?.Length ?? 0) - displayedLength;
                var displayText = outputText is null ? null : outputText[..displayedLength];
                remaining -= displayedLength;
                var itemId = StringValue(item["id"]);
                var rawStatus = StringValue(item["status"]);
                var succeeded = string.Equals(rawStatus, "completed", StringComparison.OrdinalIgnoreCase)
                    && item["error"] is null;

                var projected = new JsonObject
                {
                    ["itemId"] = itemId,
                    ["turnId"] = turnId,
                    ["itemType"] = itemType,
                    ["status"] = rawStatus,
                    ["outcome"] = string.Equals(rawStatus, "failed", StringComparison.OrdinalIgnoreCase)
                        ? "failed"
                        : succeeded ? "succeeded" : "unknown",
                    ["title"] = Clip(FirstString(item, "command", "tool") ?? itemType, MaximumTitleCharacters),
                    ["text"] = displayText
                };
                if (omittedCharacters > 0)
                {
                    projected["neoBabylonDisplay"] = new JsonObject
                    {
                        ["displayTruncated"] = true,
                        ["originalCharacters"] = outputText!.Length,
                        ["omittedCharacters"] = omittedCharacters,
                        ["sourceRetained"] = true,
                        ["upstreamTruncated"] = UpstreamOmissionMarker.IsMatch(outputText)
                    };
                }
                else if (outputText is not null && UpstreamOmissionMarker.IsMatch(outputText))
                {
                    projected["neoBabylonDisplay"] = new JsonObject
                    {
                        ["displayTruncated"] = false,
                        ["originalCharacters"] = outputText.Length,
                        ["omittedCharacters"] = 0,
                        ["sourceRetained"] = true,
                        ["upstreamTruncated"] = true
                    };
                }

                newestFirst.Add(projected);
            }

            if (truncated)
            {
                break;
            }
        }

        var chronological = new JsonArray();
        foreach (var output in newestFirst.AsEnumerable().Reverse())
        {
            chronological.Add(output);
        }

        return new ThreadSavedOutputProjectionResult(chronological, truncated);
    }

    private static string? FirstString(JsonObject source, params string[] names)
    {
        foreach (var name in names)
        {
            if (StringValue(source[name]) is { } value)
            {
                return value;
            }
        }

        return null;
    }

    private static string? Clip(string? value, int maximumCharacters) =>
        value is null ? null : value[..Math.Min(value.Length, maximumCharacters)];

    private static string? StringValue(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
