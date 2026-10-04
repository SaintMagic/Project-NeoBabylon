using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using NeoBabylon.Core;

namespace NeoBabylon.Phase1AQualification;

public sealed record SessionJournalReadResult(JsonObject Evidence, Exception? Failure);

public static class SessionJournalEvidenceReader
{
    public static SessionJournalReadResult ReadToolEvidence(
        string codexHome,
        int maxAttempts = 4,
        TimeSpan? retryDelay = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);
        var delay = retryDelay ?? TimeSpan.FromMilliseconds(100);
        var evidence = new JsonObject
        {
            ["sessionPath"] = null,
            ["readStatus"] = "notFound",
            ["functionCallObserved"] = false,
            ["functionCallName"] = null,
            ["functionCallArguments"] = null,
            ["functionCallOutputObserved"] = false,
            ["functionCallOutput"] = null,
            ["toolDiagnostics"] = new JsonArray(),
            ["toolOutcomeReadStatus"] = "notFound",
            ["toolOutcomeReadFailure"] = null,
            ["model"] = null,
            ["modelContextWindow"] = null,
            ["sandboxPolicy"] = null
        };

        string? sessionPath;
        try
        {
            var sessionsRoot = Path.Combine(codexHome, "sessions");
            sessionPath = Directory.Exists(sessionsRoot)
                ? Directory.EnumerateFiles(sessionsRoot, "*.jsonl", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault()
                : null;
        }
        catch (IOException ex)
        {
            evidence["readStatus"] = "unavailable";
            return new SessionJournalReadResult(evidence, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            evidence["readStatus"] = "unavailable";
            return new SessionJournalReadResult(evidence, ex);
        }
        if (sessionPath is null)
        {
            return new SessionJournalReadResult(evidence, null);
        }

        evidence["sessionPath"] = sessionPath;
        Exception? lastFailure = null;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var stream = new FileStream(
                    sessionPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    try
                    {
                        var payload = JsonNode.Parse(line)?["payload"] as JsonObject;
                        if (payload is null)
                        {
                            continue;
                        }

                        var type = payload["type"]?.GetValue<string>();
                        if (type == "session_meta")
                        {
                            evidence["model"] = payload["base_instructions"]?["provenance"]?["model"]?.DeepClone();
                        }
                        else if (type == "task_started")
                        {
                            evidence["modelContextWindow"] = payload["model_context_window"]?.DeepClone();
                        }
                        else if (type == "turn_context")
                        {
                            evidence["model"] = payload["model"]?.DeepClone();
                            evidence["sandboxPolicy"] = payload["sandbox_policy"]?.DeepClone();
                        }
                        else if (type == "function_call")
                        {
                            evidence["functionCallObserved"] = true;
                            evidence["functionCallName"] = payload["name"]?.GetValue<string>();
                            evidence["functionCallArguments"] = payload["arguments"]?.GetValue<string>();
                        }
                        else if (type == "function_call_output")
                        {
                            evidence["functionCallOutputObserved"] = true;
                            evidence["functionCallOutput"] = payload["output"]?.GetValue<string>();
                        }
                    }
                    catch (JsonException)
                    {
                        // The session is append-only JSONL; ignore a partial final line.
                    }
                }

                var toolEvidence = SessionJournalToolEvidenceReader.ReadAppended(sessionPath, codexHome, 0);
                evidence["toolDiagnostics"] = TurnDiagnostics.ExtractSessionJournal(toolEvidence.Calls);
                evidence["toolOutcomeReadStatus"] = toolEvidence.Status;
                evidence["toolOutcomeReadFailure"] = toolEvidence.Failure;
                evidence["readStatus"] = "read";
                return new SessionJournalReadResult(evidence, null);
            }
            catch (IOException ex)
            {
                lastFailure = ex;
                if (attempt + 1 < maxAttempts)
                {
                    Thread.Sleep(delay);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                lastFailure = ex;
                break;
            }
        }

        evidence["readStatus"] = "unavailable";
        return new SessionJournalReadResult(evidence, lastFailure);
    }
}
