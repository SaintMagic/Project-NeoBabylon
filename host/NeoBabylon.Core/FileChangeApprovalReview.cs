using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace NeoBabylon.Core;

public readonly record struct FileChangeApprovalIdentity(string ThreadId, string TurnId, string ItemId);

public sealed record FileChangeApprovalReview(
    FileChangeApprovalIdentity Identity,
    string Fingerprint,
    JsonArray Changes)
{
    public bool MatchesRequest(JsonObject parameters) =>
        string.Equals(parameters["threadId"]?.GetValue<string>(), Identity.ThreadId, StringComparison.Ordinal)
        && string.Equals(parameters["turnId"]?.GetValue<string>(), Identity.TurnId, StringComparison.Ordinal)
        && string.Equals(parameters["itemId"]?.GetValue<string>(), Identity.ItemId, StringComparison.Ordinal);

    public JsonObject ToJsonObject() => new()
    {
        ["threadId"] = Identity.ThreadId,
        ["turnId"] = Identity.TurnId,
        ["itemId"] = Identity.ItemId,
        ["fingerprint"] = Fingerprint,
        ["changes"] = Changes.DeepClone()
    };
}

public static class FileChangeApprovalReviewProjector
{
    public static bool TryGetRequestIdentity(JsonObject parameters, out FileChangeApprovalIdentity identity)
    {
        var threadId = Text(parameters["threadId"]);
        var turnId = Text(parameters["turnId"]);
        var itemId = Text(parameters["itemId"]);
        if (!string.IsNullOrWhiteSpace(threadId)
            && !string.IsNullOrWhiteSpace(turnId)
            && !string.IsNullOrWhiteSpace(itemId))
        {
            identity = new FileChangeApprovalIdentity(threadId, turnId, itemId);
            return true;
        }

        identity = default;
        return false;
    }

    public static bool TryGetIdentity(JsonObject itemStartedParams, out FileChangeApprovalIdentity identity)
    {
        var item = itemStartedParams["item"] as JsonObject;
        var threadId = Text(itemStartedParams["threadId"]);
        var turnId = Text(itemStartedParams["turnId"]);
        var itemId = Text(item?["id"]);
        if (Text(item?["type"]) == "fileChange"
            && !string.IsNullOrWhiteSpace(threadId)
            && !string.IsNullOrWhiteSpace(turnId)
            && !string.IsNullOrWhiteSpace(itemId))
        {
            identity = new FileChangeApprovalIdentity(threadId, turnId, itemId);
            return true;
        }

        identity = default;
        return false;
    }

    public static bool TryGetPatchUpdatedIdentity(JsonObject patchUpdatedParams, out FileChangeApprovalIdentity identity)
    {
        var threadId = Text(patchUpdatedParams["threadId"]);
        var turnId = Text(patchUpdatedParams["turnId"]);
        var itemId = Text(patchUpdatedParams["itemId"]);
        if (!string.IsNullOrWhiteSpace(threadId)
            && !string.IsNullOrWhiteSpace(turnId)
            && !string.IsNullOrWhiteSpace(itemId))
        {
            identity = new FileChangeApprovalIdentity(threadId, turnId, itemId);
            return true;
        }

        identity = default;
        return false;
    }

    public static FileChangeApprovalReview? ProjectItemStarted(JsonObject itemStartedParams)
    {
        if (!TryGetIdentity(itemStartedParams, out var identity)
            || itemStartedParams["item"]?["status"]?.GetValue<string>() != "inProgress"
            || itemStartedParams["item"]?["changes"] is not JsonArray changes)
        {
            return null;
        }

        return Project(identity, changes);
    }

    public static FileChangeApprovalReview? ProjectPatchUpdated(JsonObject patchUpdatedParams)
    {
        if (!TryGetPatchUpdatedIdentity(patchUpdatedParams, out var identity)
            || patchUpdatedParams["changes"] is not JsonArray sourceChanges
            || sourceChanges.Count == 0)
        {
            return null;
        }

        var normalized = new JsonArray();
        foreach (var candidate in sourceChanges)
        {
            if (candidate is not JsonObject change
                || !HasOnlyKeys(change, "path", "kind", "diff")
                || Text(change["kind"]) is not ("add" or "delete" or "update"))
            {
                return null;
            }

            normalized.Add(new JsonObject
            {
                ["path"] = change["path"]?.DeepClone(),
                ["kind"] = new JsonObject { ["type"] = change["kind"]!.DeepClone() },
                ["diff"] = change["diff"]?.DeepClone()
            });
        }

        return Project(identity, normalized);
    }

    private static FileChangeApprovalReview? Project(FileChangeApprovalIdentity identity, JsonArray sourceChanges)
    {
        if (sourceChanges.Count == 0)
        {
            return null;
        }

        var changes = new JsonArray();
        foreach (var candidate in sourceChanges)
        {
            if (candidate is not JsonObject change
                || !HasOnlyKeys(change, "path", "kind", "diff")
                || Text(change["path"]) is not { Length: > 0 } path
                || string.IsNullOrWhiteSpace(path)
                || change["kind"] is not JsonObject kind
                || !HasOnlyKeys(kind, "type", "move_path")
                || Text(kind["type"]) is not ("add" or "delete" or "update")
                || Text(change["diff"]) is not { Length: > 0 } diff)
            {
                return null;
            }

            var kindType = Text(kind["type"])!;
            string? movePath = null;
            if (kind["move_path"] is JsonNode movePathNode)
            {
                if (kindType != "update"
                    || movePathNode is not JsonValue movePathValue
                    || !movePathValue.TryGetValue<string>(out movePath)
                    || string.IsNullOrWhiteSpace(movePath))
                {
                    return null;
                }
            }

            changes.Add(new JsonObject
            {
                ["path"] = path,
                ["kind"] = kindType,
                ["diff"] = diff,
                ["movePath"] = movePath
            });
        }

        var fingerprintSource = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["threadId"] = identity.ThreadId,
            ["turnId"] = identity.TurnId,
            ["itemId"] = identity.ItemId,
            ["changes"] = changes.DeepClone()
        };
        if (fingerprintSource.ToJsonString().Length > AppServerProtocol.MaximumApprovalPayloadCharacters)
        {
            return null;
        }

        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource.ToJsonString())))
            .ToLowerInvariant();
        return new FileChangeApprovalReview(identity, fingerprint, changes);
    }

    private static bool HasOnlyKeys(JsonObject value, params string[] allowed) =>
        value.Select(pair => pair.Key).All(key => allowed.Contains(key, StringComparer.Ordinal));

    private static string? Text(JsonNode? node) => node is JsonValue value
        && value.TryGetValue<string>(out var text) ? text : null;
}
