using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NeoBabylon.Core;

public sealed record SessionJournalToolCall(string? ItemId, string CallId, string ToolName, string? Output)
{
    public string? Arguments { get; init; }
    public string? OutputItemId { get; init; }
}

public sealed record SessionJournalToolReadResult(
    IReadOnlyList<SessionJournalToolCall> Calls,
    string Status,
    string? Failure)
{
    public int? ModelContextWindow { get; init; }
    public string? TaskCompleteErrorMessage { get; init; }
    public JsonNode? TaskCompleteError { get; init; }
}

public static class SessionJournalToolEvidenceReader
{
    private const long MaximumReadBytes = 4 * 1024 * 1024;

    public static long? CaptureOffset(string? sessionPath, string codexHome)
    {
        if (!TryResolveSessionPath(sessionPath, codexHome, out var fullPath))
        {
            return null;
        }

        if (!File.Exists(fullPath))
        {
            return 0;
        }

        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return stream.Length;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static SessionJournalToolReadResult ReadAppended(
        string? sessionPath,
        string codexHome,
        long? startOffset)
    {
        if (!TryResolveSessionPath(sessionPath, codexHome, out var fullPath))
        {
            return new SessionJournalToolReadResult([], "unavailable", "The pinned App Server did not provide a journal path inside the isolated CodexHome sessions directory.");
        }

        if (startOffset is null || startOffset < 0)
        {
            return new SessionJournalToolReadResult([], "unavailable", "The host could not capture a safe journal offset before the turn.");
        }

        if (!File.Exists(fullPath))
        {
            return new SessionJournalToolReadResult([], "notFound", "The App Server session journal was not created or is no longer available.");
        }

        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (startOffset > stream.Length)
            {
                return new SessionJournalToolReadResult([], "unavailable", "The journal changed before the captured turn offset.");
            }

            if (stream.Length - startOffset > MaximumReadBytes)
            {
                return new SessionJournalToolReadResult([], "tooLarge", "Turn journal evidence exceeded the bounded diagnostic read limit.");
            }

            stream.Position = startOffset.Value;
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            var calls = new Dictionary<string, MutableCall>(StringComparer.Ordinal);
            var outputsBeforeCalls = new Dictionary<string, JournalOutput>(StringComparer.Ordinal);
            int? modelContextWindow = null;
            JsonNode? taskCompleteError = null;
            string? taskCompleteErrorMessage = null;
            var malformedRecord = false;
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                JsonObject? payload;
                try
                {
                    payload = JsonNode.Parse(line)?["payload"] as JsonObject;
                }
                catch (JsonException)
                {
                    malformedRecord = true;
                    continue;
                }

                if (payload is null)
                {
                    malformedRecord = true;
                    continue;
                }

                var type = StringValue(payload["type"]);
                if (type == "task_started" && payload["model_context_window"] is JsonValue contextValue
                    && contextValue.TryGetValue<int>(out var contextWindow) && contextWindow > 0)
                {
                    modelContextWindow = contextWindow;
                    continue;
                }

                if (type == "task_complete")
                {
                    if (payload["error"] is JsonNode error)
                    {
                        taskCompleteError = error.DeepClone();
                        taskCompleteErrorMessage = StringValue(error is JsonObject errorObject ? errorObject["message"] : null)
                            ?? StringValue(error)
                            ?? error.ToJsonString();
                    }
                    continue;
                }

                var callId = StringValue(payload["call_id"]);
                if (string.IsNullOrWhiteSpace(callId))
                {
                    continue;
                }

                if (type == "function_call")
                {
                    var call = new MutableCall(
                        StringValue(payload["id"]),
                        callId,
                        StringValue(payload["name"]) ?? "Unknown",
                        StringValue(payload["arguments"]) ?? payload["arguments"]?.ToJsonString());
                    if (outputsBeforeCalls.Remove(callId, out var priorOutput))
                    {
                        call.Output = priorOutput.Text;
                        call.OutputItemId = priorOutput.ItemId;
                    }

                    calls[callId] = call;
                }
                else if (type == "function_call_output")
                {
                    var output = StringValue(payload["output"]) ?? payload["output"]?.ToJsonString();
                    var outputItemId = StringValue(payload["id"]);
                    if (calls.TryGetValue(callId, out var call))
                    {
                        call.Output = output;
                        call.OutputItemId = outputItemId;
                    }
                    else if (output is not null)
                    {
                        outputsBeforeCalls[callId] = new JournalOutput(outputItemId, output);
                    }
                }
            }

            var result = calls.Values
                .Select(call => new SessionJournalToolCall(call.ItemId, call.CallId, call.ToolName, call.Output)
                {
                    Arguments = call.Arguments,
                    OutputItemId = call.OutputItemId
                })
                .ToArray();
            return new SessionJournalToolReadResult(
                result,
                malformedRecord ? "partial" : "read",
                malformedRecord ? "Some journal records were malformed; missing outcomes remain Unknown." : null)
            {
                ModelContextWindow = modelContextWindow,
                TaskCompleteErrorMessage = taskCompleteErrorMessage,
                TaskCompleteError = taskCompleteError
            };
        }
        catch (IOException)
        {
            return new SessionJournalToolReadResult([], "unavailable", "The App Server session journal could not be read while the turn was active.");
        }
        catch (UnauthorizedAccessException)
        {
            return new SessionJournalToolReadResult([], "unavailable", "The isolated App Server session journal is not readable by the host.");
        }
    }

    internal static bool TryResolveSessionPath(string? sessionPath, string codexHome, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(sessionPath) || !Path.IsPathFullyQualified(sessionPath))
        {
            return false;
        }

        try
        {
            var sessionsRoot = Path.GetFullPath(Path.Combine(codexHome, "sessions"));
            var candidate = Path.GetFullPath(sessionPath);
            var rootWithSeparator = sessionsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }

    private static string? StringValue(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return null;
    }

    private sealed record JournalOutput(string? ItemId, string Text);

    private sealed class MutableCall(string? itemId, string callId, string toolName, string? arguments)
    {
        public string? ItemId { get; } = itemId;
        public string CallId { get; } = callId;
        public string ToolName { get; } = toolName;
        public string? Arguments { get; } = arguments;
        public string? Output { get; set; }
        public string? OutputItemId { get; set; }
    }
}
