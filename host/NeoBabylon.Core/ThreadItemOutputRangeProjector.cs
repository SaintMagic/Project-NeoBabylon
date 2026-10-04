using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NeoBabylon.Core;

public sealed record ThreadItemOutputRange(
    string ThreadId,
    string TurnId,
    string ItemId,
    string ItemType,
    int Offset,
    int TotalCharacters,
    string Text,
    int NextOffset,
    bool HasMore,
    bool UpstreamTruncated);

public static class ThreadItemOutputRangeProjector
{
    public const int MaximumPageCharacters = 32_768;

    private static readonly Regex UpstreamOmissionMarker = new(
        @"\.\.\.\s+\d+\s+bytes omitted\s+\.\.\.",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static ThreadItemOutputRange Project(
        JsonObject response,
        string threadId,
        string turnId,
        string itemId,
        int offset,
        int maximumCharacters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        ArgumentException.ThrowIfNullOrWhiteSpace(turnId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Output range offset cannot be negative.");
        }

        if (maximumCharacters is < 1 or > MaximumPageCharacters)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCharacters),
                $"Output range size must be between 1 and {MaximumPageCharacters} characters.");
        }

        var data = response["data"] as JsonArray
            ?? throw new InvalidDataException("App Server item page did not contain an item array.");
        return TryProject(data, threadId, turnId, itemId, offset, maximumCharacters)
            ?? throw new InvalidDataException("The requested item was not found in the exact App Server thread and turn.");
    }

    public static ThreadItemOutputRange? TryProject(
        JsonArray data,
        string threadId,
        string turnId,
        string itemId,
        int offset,
        int maximumCharacters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        ArgumentException.ThrowIfNullOrWhiteSpace(turnId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Output range offset cannot be negative.");
        }

        if (maximumCharacters is < 1 or > MaximumPageCharacters)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCharacters),
                $"Output range size must be between 1 and {MaximumPageCharacters} characters.");
        }

        foreach (var entry in data.OfType<JsonObject>())
        {
            if (!string.Equals(StringValue(entry["turnId"]), turnId, StringComparison.Ordinal)
                || entry["item"] is not JsonObject item
                || !string.Equals(StringValue(item["id"]), itemId, StringComparison.Ordinal))
            {
                continue;
            }

            var itemType = StringValue(item["type"])
                ?? throw new InvalidDataException("The requested App Server item has no type.");
            var text = OutputText(itemType, item)
                ?? throw new InvalidDataException("The requested App Server item does not contain inspectable text output.");
            var start = Math.Min(offset, text.Length);
            var length = Math.Min(maximumCharacters, text.Length - start);
            var page = text.Substring(start, length);
            var nextOffset = start + length;
            return new ThreadItemOutputRange(
                threadId,
                turnId,
                itemId,
                itemType,
                start,
                text.Length,
                page,
                nextOffset,
                nextOffset < text.Length,
                UpstreamOmissionMarker.IsMatch(text));
        }

        return null;
    }

    private static string? OutputText(string itemType, JsonObject item)
    {
        if (itemType == "agentMessage")
        {
            return StringValue(item["text"]);
        }

        if (itemType is "commandExecution" or "fileChange" or "mcpToolCall" or "dynamicToolCall" or "functionCallOutput")
        {
            return StringValue(item["aggregatedOutput"])
                ?? StringValue(item["output"])
                ?? StringValue(item["result"]);
        }

        return null;
    }

    private static string? StringValue(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
