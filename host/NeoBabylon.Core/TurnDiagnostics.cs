using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NeoBabylon.Core;

public static class TurnDiagnostics
{
    private static readonly HashSet<string> ToolItemTypes =
    [
        "commandExecution",
        "fileChange",
        "mcpToolCall",
        "dynamicToolCall",
        "functionCallOutput"
    ];

    public static JsonArray Extract(IEnumerable<AppServerNotification> notifications)
    {
        var result = new JsonArray();
        foreach (var notification in notifications)
        {
            if (!string.Equals(notification.Method, "item/completed", StringComparison.Ordinal))
            {
                continue;
            }

            if (notification.Params["item"] is not JsonObject item
                || item["type"]?.GetValue<string>() is not { } itemType
                || !ToolItemTypes.Contains(itemType))
            {
                continue;
            }

            var status = StringValue(item["status"]);
            var output = FirstString(item, "aggregatedOutput", "output", "result");
            var error = StringValue(item["error"]);
            var exitCode = IntValue(item["exitCode"]);
            var succeeded = string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase)
                && exitCode is null or 0
                && string.IsNullOrWhiteSpace(error);
            var summary = new JsonObject
            {
                ["eventType"] = "toolOutcome",
                ["attributedTo"] = "Codex App Server",
                ["evidenceSource"] = "appServerNotification",
                ["itemType"] = itemType,
                ["itemId"] = StringValue(item["id"]),
                ["callId"] = StringValue(item["callId"]),
                ["status"] = status,
                ["outcome"] = succeeded ? "succeeded" : "failed",
                ["succeeded"] = succeeded
            };

            if (item["neoBabylonDisplay"] is JsonObject displayMetadata)
            {
                summary["neoBabylonDisplay"] = displayMetadata.DeepClone();
            }

            CopyString(summary, "command", item, "command");
            CopyString(summary, "output", item, "aggregatedOutput", "output", "result");
            if (exitCode is not null)
            {
                summary["exitCode"] = exitCode.Value;
            }

            if (!succeeded)
            {
                var message = !string.IsNullOrWhiteSpace(error)
                    ? error
                    : !string.IsNullOrWhiteSpace(output)
                        ? output
                        : status ?? "Tool item did not complete successfully.";
                summary["failure"] = new JsonObject
                {
                    ["type"] = "toolExecution",
                    ["attributedTo"] = "Codex App Server",
                    ["message"] = message
                };
            }

            result.Add(summary);
        }

        return result;
    }

    public static JsonArray ExtractSessionJournal(IEnumerable<SessionJournalToolCall> calls)
    {
        var result = new JsonArray();
        foreach (var call in calls)
        {
            var (outcome, exitCode, failureMessage) = ClassifySessionJournalCall(call);
            var diagnostic = new JsonObject
            {
                ["eventType"] = "toolOutcome",
                ["attributedTo"] = "Codex App Server",
                ["evidenceSource"] = "isolatedSessionJournal",
                ["itemType"] = "functionCallOutput",
                ["itemId"] = call.ItemId,
                ["callId"] = call.CallId,
                ["toolName"] = call.ToolName,
                ["outcome"] = outcome,
                ["succeeded"] = outcome switch
                {
                    "succeeded" => JsonValue.Create(true),
                    "failed" => JsonValue.Create(false),
                    _ => null
                }
            };
            if (exitCode is not null)
            {
                diagnostic["exitCode"] = exitCode.Value;
            }

            if (failureMessage is not null)
            {
                diagnostic["failure"] = new JsonObject
                {
                    ["type"] = "toolExecution",
                    ["attributedTo"] = "Codex App Server",
                    ["message"] = failureMessage
                };
            }

            result.Add(diagnostic);
        }

        return result;
    }

    public static JsonArray Combine(JsonArray notificationDiagnostics, JsonArray journalDiagnostics)
    {
        var result = new JsonArray();
        var observedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var diagnostic in notificationDiagnostics.OfType<JsonObject>())
        {
            AddId(observedIds, diagnostic["itemId"]);
            AddId(observedIds, diagnostic["callId"]);
            result.Add(diagnostic.DeepClone());
        }

        foreach (var diagnostic in journalDiagnostics.OfType<JsonObject>())
        {
            var itemId = StringValue(diagnostic["itemId"]);
            var callId = StringValue(diagnostic["callId"]);
            if (itemId is not null && observedIds.Contains(itemId)
                || callId is not null && observedIds.Contains(callId))
            {
                continue;
            }

            result.Add(diagnostic.DeepClone());
        }

        return result;
    }

    public static string OutcomeStatus(JsonArray diagnostics)
    {
        var items = diagnostics.OfType<JsonObject>().ToArray();
        if (items.Any(item => item["outcome"]?.GetValue<string>() == "failed"
            || item["succeeded"]?.GetValue<bool>() == false))
        {
            return "failed";
        }

        if (items.Length == 0)
        {
            return "notObserved";
        }

        if (items.Any(item => item["outcome"]?.GetValue<string>() == "unknown"
            || item["succeeded"] is null))
        {
            return "unknown";
        }

        return "succeeded";
    }

    public static bool HasFailure(JsonArray diagnostics) => diagnostics
        .OfType<JsonObject>()
        .Any(item => item["succeeded"]?.GetValue<bool>() == false);

    private static void CopyString(JsonObject destination, string destinationName, JsonObject source, params string[] names)
    {
        var value = FirstString(source, names);
        if (!string.IsNullOrWhiteSpace(value))
        {
            destination[destinationName] = value;
        }
    }

    private static string? FirstString(JsonObject source, params string[] names)
    {
        foreach (var name in names)
        {
            var value = StringValue(source[name]);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? StringValue(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return node.ToJsonString();
    }

    private static int? IntValue(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var number))
        {
            return number;
        }

        return null;
    }

    private static (string Outcome, int? ExitCode, string? FailureMessage) ClassifySessionJournalCall(SessionJournalToolCall call)
    {
        if (string.IsNullOrWhiteSpace(call.Output))
        {
            return ("unknown", null, null);
        }

        if (!string.Equals(call.ToolName, "exec_command", StringComparison.Ordinal))
        {
            return ("unknown", null, null);
        }

        if (call.Output.StartsWith("exec_command failed:", StringComparison.OrdinalIgnoreCase))
        {
            return ("failed", null, "The command tool failed before the process could start.");
        }

        var match = Regex.Match(call.Output, @"(?m)^\s*Process exited with code\s+(-?\d+)\s*$", RegexOptions.CultureInvariant);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var exitCode))
        {
            return ("unknown", null, null);
        }

        return exitCode == 0
            ? ("succeeded", exitCode, null)
            : ("failed", exitCode, "The command exited with a nonzero status.");
    }

    private static void AddId(HashSet<string> ids, JsonNode? node)
    {
        var id = StringValue(node);
        if (!string.IsNullOrWhiteSpace(id))
        {
            ids.Add(id);
        }
    }
}
