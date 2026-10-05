using System.Text.Json.Nodes;

namespace NeoBabylon.Core;

/// <summary>Display-only serialization. Never changes provider input or the source journal.</summary>
public static class ToolOutputText
{
    public static string? Text(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        _ => node.ToJsonString()
    };

    public static string? Read(JsonObject item)
    {
        foreach (var name in new[] { "error", "aggregatedOutput", "output", "result", "contentItems" })
        {
            var text = Text(item[name]);
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        return null;
    }

    public static bool Failed(JsonObject item) =>
        !string.IsNullOrWhiteSpace(Text(item["error"]))
        || item["success"] is JsonValue success && success.TryGetValue<bool>(out var succeeded) && !succeeded
        || item["exitCode"] is JsonValue exit && exit.TryGetValue<int>(out var code) && code != 0
        || item["status"] is JsonValue status && status.TryGetValue<string>(out var text)
            && string.Equals(text, "failed", StringComparison.OrdinalIgnoreCase);
}
