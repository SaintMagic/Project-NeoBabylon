using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NeoBabylon.Core;

public sealed record ThreadTranscriptProjectionResult(JsonArray Turns, bool Truncated);

public static class ThreadTranscriptProjector
{
    private const int MaximumReasoningParts = 128;
    private static readonly Regex UpstreamOmissionMarker = new(
        @"\.\.\.\s+\d+\s+bytes omitted\s+\.\.\.",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static bool HasReadableText(JsonArray? parts)
    {
        if (parts is null) return false;
        for (var index = 0; index < Math.Min(parts.Count, MaximumReasoningParts); index++)
        {
            if (parts[index] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0)
            {
                return true;
            }
        }
        return parts.Count > MaximumReasoningParts;
    }

    public static ThreadTranscriptProjectionResult Project(JsonArray sourceTurns, int maxCharacters)
    {
        if (maxCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCharacters), "Transcript display budget must be positive.");
        }

        var turns = new JsonArray();
        var remaining = maxCharacters;
        var truncated = false;

        foreach (var sourceTurn in sourceTurns.OfType<JsonObject>())
        {
            var sourceItems = sourceTurn["items"] as JsonArray;
            var rawStatus = sourceTurn["status"] is JsonValue statusValue
                && statusValue.TryGetValue<string>(out var statusText) ? statusText : null;
            var status = rawStatus is "completed" or "interrupted" or "failed" or "inProgress"
                ? rawStatus : "unknown";
            if (sourceItems is null)
            {
                if (rawStatus is not null)
                {
                    turns.Add(new JsonObject
                    {
                        ["id"] = sourceTurn["id"]?.DeepClone(),
                        ["status"] = status,
                        ["items"] = new JsonArray()
                    });
                }
                continue;
            }

            var items = new JsonArray();
            foreach (var sourceItem in sourceItems.OfType<JsonObject>())
            {
                var type = sourceItem["type"]?.GetValue<string>();
                string? text = null;
                var hasNonTextContent = false;

                if (type == "agentMessage")
                {
                    text = sourceItem["text"]?.GetValue<string>();
                }
                else if (type == "userMessage" && sourceItem["content"] is JsonArray content)
                {
                    var textParts = new List<string>();
                    foreach (var part in content.OfType<JsonObject>())
                    {
                        if (part["type"]?.GetValue<string>() == "text")
                        {
                            var value = part["text"]?.GetValue<string>();
                            if (!string.IsNullOrEmpty(value))
                            {
                                textParts.Add(value);
                            }
                        }
                        else
                        {
                            hasNonTextContent = true;
                        }
                    }

                    text = string.Concat(textParts);
                    if (text.Length == 0 && hasNonTextContent)
                    {
                        text = "[Non-text input omitted from this transcript preview.]";
                    }
                }
                else if (type == "reasoning")
                {
                    var contentParts = sourceItem["content"] as JsonArray;
                    var summaryParts = sourceItem["summary"] as JsonArray;
                    var useContent = HasReadableText(contentParts);
                    var sourceParts = useContent ? contentParts : summaryParts;
                    if (sourceParts is null)
                    {
                        continue;
                    }

                    var parts = new JsonArray();
                    var selectedParts = new List<string>();
                    var retainedPartCount = Math.Min(sourceParts.Count, MaximumReasoningParts);
                    var omittedParts = sourceParts.Count > retainedPartCount;
                    for (var index = 0; index < retainedPartCount; index++)
                    {
                        var part = sourceParts[index];
                        if (part is JsonValue value && value.TryGetValue<string>(out var partText))
                        {
                            selectedParts.Add(partText);
                        }
                    }
                    var reasoningText = string.Concat(selectedParts);
                    if (reasoningText.Length == 0)
                    {
                        truncated |= omittedParts;
                        continue;
                    }
                    var reasoningDisplayedLength = Math.Min(reasoningText.Length, Math.Max(0, remaining));
                    var reasoningOmittedCharacters = reasoningText.Length - reasoningDisplayedLength;
                    var remainingForParts = reasoningDisplayedLength;
                    foreach (var part in selectedParts)
                    {
                        var displayedPart = part[..Math.Min(part.Length, remainingForParts)];
                        parts.Add(displayedPart);
                        remainingForParts -= displayedPart.Length;
                    }
                    remaining -= reasoningDisplayedLength;
                    if (reasoningOmittedCharacters > 0 || omittedParts)
                    {
                        truncated = true;
                    }

                    var reasoningItem = new JsonObject
                    {
                        ["id"] = sourceItem["id"]?.DeepClone(),
                        ["type"] = "reasoning",
                        ["summary"] = useContent ? new JsonArray() : parts,
                        ["content"] = useContent ? parts : new JsonArray()
                    };
                    if (reasoningOmittedCharacters > 0 || omittedParts)
                    {
                        reasoningItem["neoBabylonDisplay"] = new JsonObject
                        {
                            ["displayTruncated"] = true,
                            ["originalCharacters"] = reasoningText.Length,
                            ["omittedCharacters"] = reasoningOmittedCharacters,
                            ["sourceRetained"] = true,
                            ["upstreamTruncated"] = false,
                            ["omittedParts"] = omittedParts
                        };
                    }
                    items.Add(reasoningItem);
                    if (truncated) break;
                    continue;
                }

                if (string.IsNullOrEmpty(text) || type is not ("agentMessage" or "userMessage"))
                {
                    continue;
                }

                var available = Math.Max(0, remaining);
                var displayedLength = Math.Min(text.Length, available);
                var omittedCharacters = text.Length - displayedLength;
                var displayText = text[..displayedLength];
                remaining -= displayedLength;
                if (omittedCharacters > 0)
                {
                    truncated = true;
                }

                var item = new JsonObject
                {
                    ["id"] = sourceItem["id"]?.DeepClone(),
                    ["type"] = type,
                    ["text"] = displayText,
                    ["hasNonTextContent"] = hasNonTextContent
                };
                if (omittedCharacters > 0 && type == "agentMessage")
                {
                    item["neoBabylonDisplay"] = new JsonObject
                    {
                        ["displayTruncated"] = true,
                        ["originalCharacters"] = text.Length,
                        ["omittedCharacters"] = omittedCharacters,
                        ["sourceRetained"] = true,
                        ["upstreamTruncated"] = UpstreamOmissionMarker.IsMatch(text)
                    };
                }
                else if (type == "agentMessage" && UpstreamOmissionMarker.IsMatch(text))
                {
                    item["neoBabylonDisplay"] = new JsonObject
                    {
                        ["displayTruncated"] = false,
                        ["originalCharacters"] = text.Length,
                        ["omittedCharacters"] = 0,
                        ["sourceRetained"] = true,
                        ["upstreamTruncated"] = true
                    };
                }
                else if (omittedCharacters > 0)
                {
                    item["text"] = displayText + "… [display truncated]";
                }

                items.Add(item);
                if (truncated)
                {
                    break;
                }
            }

            if (items.Count > 0 || rawStatus is not null)
            {
                turns.Add(new JsonObject
                {
                    ["id"] = sourceTurn["id"]?.DeepClone(),
                    ["status"] = status,
                    ["items"] = items
                });
            }

            if (truncated)
            {
                break;
            }
        }

        return new ThreadTranscriptProjectionResult(turns, truncated);
    }
}
