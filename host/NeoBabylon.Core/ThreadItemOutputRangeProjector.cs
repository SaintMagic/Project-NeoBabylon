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
    bool UpstreamTruncated,
    bool OmittedParts);

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
            var output = OutputText(itemType, item)
                ?? throw new InvalidDataException("The requested App Server item does not contain inspectable text output.");
            var text = output.Text;
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
                UpstreamOmissionMarker.IsMatch(text),
                output.OmittedParts);
        }

        return null;
    }

    private static OutputTextResult? OutputText(string itemType, JsonObject item)
    {
        if (itemType == "agentMessage")
        {
            var text = StringValue(item["text"]);
            return text is null ? null : new OutputTextResult(text, false);
        }

        if (itemType == "reasoning")
        {
            const int maximumParts = 128;
            var content = item["content"] as JsonArray;
            var summary = item["summary"] as JsonArray;
            var useContent = HasReadableText(content, maximumParts);
            var selected = useContent ? content : summary;
            if (selected is null)
            {
                return null;
            }

            var parts = new List<string>(Math.Min(selected.Count, maximumParts));
            for (var index = 0; index < Math.Min(selected.Count, maximumParts); index++)
            {
                if (StringValue(selected[index]) is { } part)
                {
                    parts.Add(part);
                }
            }

            var text = string.Concat(parts);
            var omittedParts = selected.Count > maximumParts;
            return text.Length == 0 && !omittedParts ? null : new OutputTextResult(text, omittedParts);
        }

        if (itemType is "commandExecution" or "fileChange" or "mcpToolCall" or "dynamicToolCall" or "functionCallOutput")
        {
            var text = ToolOutputText.Read(item);
            return text is null ? null : new OutputTextResult(text, false);
        }

        return null;
    }

    private static bool HasReadableText(JsonArray? parts, int maximumParts)
    {
        if (parts is null)
        {
            return false;
        }

        for (var index = 0; index < Math.Min(parts.Count, maximumParts); index++)
        {
            if (StringValue(parts[index]) is { Length: > 0 })
            {
                return true;
            }
        }

        return parts.Count > maximumParts;
    }

    private sealed record OutputTextResult(string Text, bool OmittedParts);

    private static string? StringValue(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
