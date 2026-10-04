using System.Text.Json.Nodes;
using System.Globalization;

namespace NeoBabylon.Core;

public static class ThreadListProjector
{
    public static JsonArray FilterByWorkspace(JsonArray threads, string workspace)
    {
        var selected = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspace));
        var visible = new JsonArray();
        foreach (var node in threads.OfType<JsonObject>())
        {
            if (node["cwd"] is not JsonValue cwdNode || !cwdNode.TryGetValue<string>(out var cwd)) continue;
            try
            {
                if (string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(cwd)), selected, StringComparison.OrdinalIgnoreCase))
                {
                    visible.Add(node.DeepClone());
                }
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // An untrusted or incomplete App Server record cannot be attributed to this project.
            }
        }

        return visible;
    }

    public static JsonArray MergeScanAndStateDb(JsonArray scanAndRepair, JsonArray stateDbOnly)
    {
        var result = new JsonArray();
        var indexesById = new Dictionary<string, int>(StringComparer.Ordinal);
        AddOrEnrichThreads(scanAndRepair, result, indexesById);
        AddOrEnrichThreads(stateDbOnly, result, indexesById);
        var ordered = result.OfType<JsonObject>()
            .Select((thread, index) => new
            {
                Thread = thread,
                Index = index,
                UpdatedAt = UpdatedAtOrder(thread)
            })
            .OrderByDescending(item => item.UpdatedAt.HasValue)
            .ThenByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.Index);
        var sorted = new JsonArray();
        foreach (var item in ordered)
        {
            sorted.Add(item.Thread.DeepClone());
        }

        return sorted;
    }

    private static decimal? UpdatedAtOrder(JsonObject thread)
    {
        if (thread["updatedAt"] is not JsonValue value) return null;
        if (value.TryGetValue<long>(out var unixSeconds)) return unixSeconds * 1000m;
        if (value.TryGetValue<double>(out var numericSeconds) && double.IsFinite(numericSeconds))
        {
            return (decimal)numericSeconds * 1000m;
        }

        if (value.TryGetValue<string>(out var timestamp)
            && DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return parsed.ToUnixTimeMilliseconds();
        }

        return null;
    }

    private static void AddOrEnrichThreads(JsonArray source, JsonArray destination, Dictionary<string, int> indexesById)
    {
        foreach (var node in source)
        {
            if (node is not JsonObject thread
                || thread["id"] is not JsonValue idNode
                || !idNode.TryGetValue<string>(out var id)
                || string.IsNullOrWhiteSpace(id)
                )
            {
                continue;
            }

            if (indexesById.TryGetValue(id, out var existingIndex))
            {
                if (destination[existingIndex] is JsonObject existing)
                {
                    foreach (var property in thread)
                    {
                        if (existing[property.Key] is null && property.Value is not null)
                        {
                            existing[property.Key] = property.Value.DeepClone();
                        }
                    }
                }

                continue;
            }

            indexesById.Add(id, destination.Count);
            destination.Add(thread.DeepClone());
        }
    }
}
