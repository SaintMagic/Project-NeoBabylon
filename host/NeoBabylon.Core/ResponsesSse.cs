using System.Text.Json.Nodes;

namespace NeoBabylon.Core;

public static class ResponsesSse
{
    public static string FunctionCall(string responseId, string callId, string name, string arguments)
    {
        var item = new JsonObject
        {
            ["type"] = "function_call",
            ["call_id"] = callId,
            ["name"] = name,
            ["arguments"] = arguments
        };
        return Stream(
            Created(responseId),
            new JsonObject
            {
                ["type"] = "response.output_item.done",
                ["item"] = item
            },
            Completed(responseId));
    }

    public static string AssistantMessage(string responseId, string itemId, string text)
    {
        return Stream(
            Created(responseId),
            new JsonObject
            {
                ["type"] = "response.output_item.done",
                ["item"] = new JsonObject
                {
                    ["type"] = "message",
                    ["role"] = "assistant",
                    ["id"] = itemId,
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "output_text",
                            ["text"] = text
                        }
                    }
                }
            },
            Completed(responseId));
    }

    private static JsonObject Created(string responseId) => new()
    {
        ["type"] = "response.created",
        ["response"] = new JsonObject { ["id"] = responseId }
    };

    private static JsonObject Completed(string responseId) => new()
    {
        ["type"] = "response.completed",
        ["response"] = new JsonObject
        {
            ["id"] = responseId,
            ["usage"] = new JsonObject
            {
                ["input_tokens"] = 0,
                ["input_tokens_details"] = null,
                ["output_tokens"] = 0,
                ["output_tokens_details"] = null,
                ["total_tokens"] = 0
            }
        }
    };

    private static string Stream(params JsonObject[] events)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var item in events)
        {
            builder.Append("event: ").Append(item["type"]?.GetValue<string>()).Append("\n");
            builder.Append("data: ").Append(item.ToJsonString()).Append("\n\n");
        }

        return builder.ToString();
    }
}
