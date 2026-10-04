using System.Text.Json.Nodes;

namespace NeoBabylon.Core;

public static class ThreadSavedChangeProjector
{
    public static JsonArray Project(JsonArray sourceTurns, int maxCharacters)
    {
        if (maxCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCharacters));
        }

        var reviews = new JsonArray();
        foreach (var turn in sourceTurns.OfType<JsonObject>())
        {
            var turnId = Text(turn["id"]);
            if (turnId is null || turn["items"] is not JsonArray items)
            {
                continue;
            }

            var patches = items.OfType<JsonObject>()
                .Where(item => Text(item["type"]) == "fileChange").ToArray();
            if (patches.Length == 0)
            {
                continue;
            }

            var changes = new JsonArray();
            var remaining = maxCharacters;
            var status = "available";
            foreach (var patch in patches)
            {
                if (Text(patch["status"]) != "completed"
                    || patch["changes"] is not JsonArray patchChanges
                    || patchChanges.Count == 0)
                {
                    status = "unavailable";
                    break;
                }

                foreach (var candidate in patchChanges)
                {
                    if (candidate is not JsonObject change
                        || Text(change["path"]) is not string path || path.Length == 0
                        || change["kind"] is not JsonObject kind
                        || Text(kind["type"]) is not string kindType
                        || kindType is not ("add" or "delete" or "update")
                        || kindType == "update" && kind["move_path"] is JsonNode movePath
                            && Text(movePath) is null
                        || Text(change["diff"]) is not string diff || diff.Length == 0)
                    {
                        status = "unavailable";
                        break;
                    }

                    if (path.Length + diff.Length > remaining)
                    {
                        status = "oversized";
                        break;
                    }

                    remaining -= path.Length + diff.Length;
                    changes.Add(new JsonObject
                    {
                        ["path"] = path,
                        ["kind"] = kindType,
                        ["diff"] = diff,
                        ["movePath"] = kindType == "update" ? Text(kind["move_path"]) : null
                    });
                }

                if (status != "available")
                {
                    break;
                }
            }

            reviews.Add(new JsonObject
            {
                ["turnId"] = turnId,
                ["source"] = "savedFileChangeItems",
                ["status"] = status,
                ["changes"] = status == "available" ? changes : null
            });
        }

        return reviews;
    }

    private static string? Text(JsonNode? node) => node is JsonValue value
        && value.TryGetValue<string>(out var text) ? text : null;
}
