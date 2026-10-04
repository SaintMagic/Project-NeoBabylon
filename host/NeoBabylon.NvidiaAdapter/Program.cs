using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;

internal static class Program
{
    private const string Provider = "nvidia";
    private const string UpstreamUrl = "https://integrate.api.nvidia.com/v1/chat/completions";
    private const int MaxBodyBytes = 2 * 1024 * 1024;
    private const int MaxCachedAssistantBytes = 4 * 1024 * 1024;
    private const int MaxCachedAssistantMessages = 128;
    private static readonly HashSet<string> AllowedModels = new(StringComparer.Ordinal)
    {
        "deepseek-ai/deepseek-v4.1-flash",
        "z-ai/glm-5.3",
        "moonshotai/kimi-k3"
    };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> Main()
    {
        try
        {
            var bootstrapLine = await Console.In.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
            if (bootstrapLine is null || bootstrapLine.Length > 16_384)
                throw new InvalidDataException();
            var bootstrap = JsonNode.Parse(bootstrapLine)?.AsObject() ?? throw new InvalidDataException();
            var apiKey = RequiredString(bootstrap, "apiKey", 4096);
            var model = RequiredString(bootstrap, "modelIdentifier", 256);
            var token = RequiredString(bootstrap, "sessionToken", 512);
            if (!AllowedModels.Contains(model))
                throw new InvalidDataException();
            var efforts = ParseEfforts(bootstrap["supportedReasoningEfforts"]);
            var maxCompletionTokens = PositiveInt(bootstrap["maxCompletionTokens"], 32768);
            var state = new AdapterState(apiKey, model, token, efforts, maxCompletionTokens);
            using var client = CreateHttpClient();

            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(server =>
            {
                server.Limits.MaxRequestBodySize = MaxBodyBytes;
                server.Listen(IPAddress.Loopback, 0);
            });
            builder.Services.AddSingleton(state);
            builder.Services.AddSingleton(client);
            var app = builder.Build();
            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/v1/responses" && context.Request.Method == HttpMethods.Post)
                {
                    try { await next(); }
                    catch (BadRequestException ex)
                    {
                        await WriteError(context, 400, "invalid_request_error", ex.Message);
                    }
                    catch (UpstreamException ex)
                    {
                        await WriteError(context, ex.StatusCode, "upstream_error", "NVIDIA upstream request failed.", ex.Code);
                    }
                    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                    {
                        context.Abort();
                    }
                    catch (Exception)
                    {
                        Console.Error.WriteLine("Adapter request failed.");
                        await WriteError(context, 500, "adapter_error", "NVIDIA adapter request failed.");
                    }
                    return;
                }
                context.Response.StatusCode = StatusCodes.Status404NotFound;
            });
            app.MapPost("/v1/responses", HandleRequest);
            await app.StartAsync();
            var addresses = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
                .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
            var listen = addresses?.Addresses.Select(value => new Uri(value)).SingleOrDefault()
                ?? throw new InvalidOperationException();
            if (listen.Host != "127.0.0.1" || !AllowedModels.Contains(state.Model))
                throw new InvalidOperationException();
            var ready = JsonSerializer.Serialize(new { endpoint = "http://127.0.0.1:" + listen.Port + "/v1", provider = Provider, model = state.Model }, JsonOptions);
            Console.Out.WriteLine(ready);
            Console.Out.Flush();
            await app.WaitForShutdownAsync();
            return 0;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Adapter startup failed.");
            return 1;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.None };
        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    private static async Task HandleRequest(HttpContext context, AdapterState state, HttpClient client)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        var supplied = authorization.StartsWith("Bearer ", StringComparison.Ordinal) ? authorization[7..] : "";
        if (!FixedEquals(supplied, state.SessionToken))
        {
            await WriteError(context, 401, "authentication_error", "Invalid adapter session token.");
            return;
        }
        if (context.Request.ContentLength is > MaxBodyBytes)
            throw new BadRequestException("Request body exceeds the adapter size limit.");
        if (!await state.RequestGate.WaitAsync(0, context.RequestAborted))
        {
            await WriteError(context, 429, "adapter_busy", "Another request is using this model session.");
            return;
        }
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromMinutes(10));
            using var body = new MemoryStream();
            var buffer = new byte[32 * 1024];
            while (true)
            {
                var count = await context.Request.Body.ReadAsync(buffer, timeout.Token);
                if (count == 0) break;
                if (body.Length + count > MaxBodyBytes)
                    throw new BadRequestException("Request body exceeds the adapter size limit.");
                await body.WriteAsync(buffer.AsMemory(0, count), timeout.Token);
            }
            JsonObject request;
            try { request = JsonNode.Parse(body.ToArray())?.AsObject() ?? throw new JsonException(); }
            catch (JsonException) { throw new BadRequestException("Request body must be a JSON object."); }
            var translated = TranslateRequest(request, state);
            if (translated.Warnings.Count > 0)
                context.Response.Headers["X-NeoBabylon-Adapter-Warnings"] = string.Join("; ", translated.Warnings);
            using var upstreamRequest = new HttpRequestMessage(HttpMethod.Post, GetUpstreamUrl());
            upstreamRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", state.ApiKey);
            upstreamRequest.Headers.Accept.ParseAdd(translated.Stream ? "text/event-stream" : "application/json");
            upstreamRequest.Content = new StringContent(translated.Body.ToJsonString(JsonOptions), Encoding.UTF8, "application/json");
            using var upstreamResponse = await client.SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!upstreamResponse.IsSuccessStatusCode)
            {
                var errorBytes = await ReadBounded(upstreamResponse.Content, 64 * 1024, timeout.Token);
                string? upstreamCode = null;
                try
                {
                    if (JsonNode.Parse(errorBytes)?["error"] is JsonObject error
                        && error["code"] is JsonValue codeNode
                        && codeNode.TryGetValue<string>(out var parsedCode))
                        upstreamCode = parsedCode;
                }
                catch (JsonException) { }
                throw new UpstreamException((int)upstreamResponse.StatusCode, upstreamCode, state.ApiKey, state.SessionToken);
            }
            if (translated.Stream)
                await StreamResponse(context, upstreamResponse, translated, state, timeout.Token);
            else
            {
                var responseBytes = await ReadBounded(upstreamResponse.Content, MaxBodyBytes, timeout.Token);
                JsonObject completion;
                try { completion = JsonNode.Parse(responseBytes)?.AsObject() ?? throw new JsonException(); }
                catch (JsonException) { throw new UpstreamException(); }
                var response = TranslateCompletion(completion, translated, state);
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(response.ToJsonString(JsonOptions), timeout.Token);
            }
        }
        finally { state.RequestGate.Release(); }
    }

    private static string GetUpstreamUrl()
    {
#if NVIDIA_ADAPTER_TEST_BUILD
        var testUrl = Environment.GetEnvironmentVariable("NEOBABYLON_NVIDIA_TEST_UPSTREAM");
        if (!string.IsNullOrWhiteSpace(testUrl)
            && Uri.TryCreate(testUrl, UriKind.Absolute, out var parsed)
            && parsed.Scheme == Uri.UriSchemeHttp
            && IPAddress.TryParse(parsed.Host, out var address)
            && IPAddress.IsLoopback(address)
            && parsed.AbsolutePath == "/v1/chat/completions")
            return parsed.ToString();
#endif
        return UpstreamUrl;
    }

    private static Translation TranslateRequest(JsonObject request, AdapterState state)
    {
        var known = new HashSet<string>(StringComparer.Ordinal)
        {
            "model", "provider", "instructions", "input", "tools", "tool_choice", "parallel_tool_calls",
            "reasoning", "store", "stream", "max_output_tokens", "stream_options", "include",
            "service_tier", "prompt_cache_key", "text", "client_metadata", "access_programs"
        };
        foreach (var key in request.Select(pair => pair.Key))
            if (!known.Contains(key)) throw new BadRequestException("Unsupported request field: " + key + ".");
        if (request.ContainsKey("previous_response_id"))
            throw new BadRequestException("Stateful previous_response_id is not supported; send complete stateless input.");
        if (StringValue(request["model"], "model") != state.Model)
            throw new BadRequestException("Request model does not match the adapter's fixed model.");
        if (request["provider"] is not null)
            throw new BadRequestException("Provider routing is fixed by the selected model capability.");
        if (request["store"] is null || BooleanValue(request["store"], "store"))
            throw new BadRequestException("Only store=false is supported.");
        if (request["include"] is not JsonArray include)
            throw new BadRequestException("include must be explicitly supplied as an array.");
        foreach (var includeNode in include)
        {
            if (includeNode is not JsonValue includeValue || !includeValue.TryGetValue<string>(out var includeName))
                throw new BadRequestException("include entries must be strings.");
            if (includeName != "reasoning.encrypted_content")
                throw new BadRequestException("Unsupported include field: " + includeName + ".");
        }
        if (request["stream_options"] is not null)
            throw new BadRequestException("Responses stream_options are unsupported.");
        if (request["service_tier"] is not null)
            throw new BadRequestException("service_tier is unsupported.");
        if (request["text"] is not null)
            throw new BadRequestException("Structured or constrained text controls are unsupported.");
        if (request["access_programs"] is not null)
            throw new BadRequestException("Access programs are unsupported.");
        var tokenLimit = RequiredPositiveInt(request["max_output_tokens"], "max_output_tokens");
        if (tokenLimit > state.MaxCompletionTokens)
            throw new BadRequestException("max_output_tokens exceeds the selected model's accepted limit.");
        var stream = BooleanValue(request["stream"], "stream");
        var parallel = BooleanValue(request["parallel_tool_calls"], "parallel_tool_calls");
        var warnings = new List<string>();
        warnings.Add("store=false is acknowledged locally; the adapter does not persist NVIDIA conversations.");
        if (include.Count > 0)
            warnings.Add("reasoning.encrypted_content is unavailable from Chat Completions; plaintext reasoning is preserved when returned, and no synthetic ciphertext is produced.");
        if (request["prompt_cache_key"] is not null) warnings.Add("prompt_cache_key is local-only and is not forwarded.");
        string? threadId = null;
        if (request["client_metadata"] is JsonObject clientMetadata)
        {
            threadId = OptionalString(clientMetadata, "thread_id");
            warnings.Add("client_metadata is local-only and is not forwarded.");
        }
        else if (request["client_metadata"] is not null)
            throw new BadRequestException("client_metadata must be an object.");
        if (state.RequiresReasoningReplay && string.IsNullOrWhiteSpace(threadId))
            throw new BadRequestException("Kimi continuation requires client_metadata.thread_id to bind reasoning history to one conversation.");
        if (request["tools"] is not null && request["tools"] is not JsonArray)
            throw new BadRequestException("tools must be an array when specified.");
        var tools = TranslateTools(request["tools"] as JsonArray);
        var messages = new JsonArray();
        if (request["instructions"] is not null)
        {
            var instructions = StringValue(request["instructions"], "instructions");
            if (instructions.Length > 1_000_000) throw new BadRequestException("instructions exceeds the adapter text limit.");
            if (instructions.Length > 0) messages.Add(new JsonObject { ["role"] = "system", ["content"] = instructions });
        }
        if (request["input"] is not JsonArray input)
            throw new BadRequestException("input must be a stateless Responses input array.");
        var seenAssistant = new HashSet<string>(StringComparer.Ordinal);
        foreach (var itemNode in input)
        {
            if (itemNode is not JsonObject item) throw new BadRequestException("input contains an invalid item.");
            TranslateInputItem(item, messages, state, seenAssistant, tools?.ByWireName, threadId);
        }
        var choice = request["tool_choice"] is null ? "auto" : StringValue(request["tool_choice"], "tool_choice");
        if (choice is not ("auto" or "none" or "required"))
            throw new BadRequestException("Only auto, none, or required tool_choice is supported.");
        if (request["reasoning"] is not null && request["reasoning"] is not JsonObject)
            throw new BadRequestException("reasoning must be an object when specified.");
        var effort = TranslateReasoning(request["reasoning"] as JsonObject, state);
        var body = new JsonObject
        {
            ["model"] = state.Model,
            ["messages"] = messages,
            ["stream"] = stream,
            ["max_tokens"] = tokenLimit,
            ["parallel_tool_calls"] = parallel
        };
        if (tools is not null) body["tools"] = tools.Items;
        if (tools is not null) body["tool_choice"] = choice;
        if (effort is not null) body["reasoning_effort"] = effort;
        if (request["prompt_cache_key"] is not null && StringValue(request["prompt_cache_key"], "prompt_cache_key").Length > 4096)
            throw new BadRequestException("prompt_cache_key exceeds the adapter text limit.");
        if (request["client_metadata"] is JsonObject metadata && metadata.Count > 128)
            throw new BadRequestException("client_metadata exceeds the adapter entry limit.");
        return new Translation(body, stream, tools is not null, tools?.ByWireName ?? new Dictionary<string, ToolIdentity>(StringComparer.Ordinal), warnings, threadId);
    }

    private static void TranslateInputItem(JsonObject item, JsonArray messages, AdapterState state, HashSet<string> seenAssistant, Dictionary<string, ToolIdentity>? tools, string? threadId)
    {
        var type = item["type"]?.GetValue<string>();
        switch (type)
        {
            case "message":
                ValidateItemFields(item, "type", "id", "role", "content", "phase", "internal_chat_message_metadata_passthrough");
                var role = StringValue(item["role"], "message.role");
                if (role == "assistant" && item["id"] is JsonNode assistantIdNode
                    && state.AssistantReplay.TryGetValue(StringValue(assistantIdNode, "message.id"), out var replay))
                {
                    AppendReplay(StringValue(assistantIdNode, "message.id"), messages, state, seenAssistant, threadId);
                    return;
                }
                if (role == "assistant" && state.RequiresReasoningReplay) throw MissingReplay();
                if (role is not ("system" or "developer" or "user" or "assistant"))
                    throw new BadRequestException("Unsupported message role.");
                var text = ExtractText(item["content"] as JsonArray, role == "assistant" ? "output_text" : "input_text");
                if (item["phase"] is not null)
                    throw new BadRequestException("Assistant message phase cannot be preserved by the qualified Chat Completions mapping.");
                messages.Add(new JsonObject { ["role"] = role == "developer" ? "system" : role, ["content"] = text });
                break;
            case "function_call":
                ValidateItemFields(item, "type", "id", "name", "namespace", "arguments", "encrypted_function_args", "call_id", "internal_chat_message_metadata_passthrough");
                if (item["encrypted_function_args"] is not null)
                    throw new BadRequestException("Encrypted function arguments are unsupported.");
                var callId = RequiredString(item, "call_id", 256);
                if (state.AssistantReplay.ContainsKey(callId))
                {
                    AppendReplay(callId, messages, state, seenAssistant, threadId);
                    break;
                }
                if (state.RequiresReasoningReplay) throw MissingReplay();
                var originalName = RequiredString(item, "name", 256);
                var originalNamespace = OptionalString(item, "namespace");
                var name = WireName(originalName, originalNamespace);
                if (tools is null || !tools.ContainsKey(name)) throw new BadRequestException("Function call does not match a request function tool.");
                var arguments = ParseArguments(RequiredString(item, "arguments", 256 * 1024));
                messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = null, ["tool_calls"] = new JsonArray(new JsonObject
                {
                    ["id"] = callId, ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = name, ["arguments"] = arguments.ToJsonString(JsonOptions) }
                }) });
                break;
            case "function_call_output":
                ValidateItemFields(item, "type", "id", "call_id", "name", "namespace", "output", "internal_chat_message_metadata_passthrough");
                var resultCallId = RequiredString(item, "call_id", 256);
                if (state.AssistantReplay.ContainsKey(resultCallId))
                    AppendReplay(resultCallId, messages, state, seenAssistant, threadId);
                else if (state.RequiresReasoningReplay) throw MissingReplay();
                var output = ExtractOutput(item["output"]);
                messages.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = resultCallId, ["content"] = output });
                break;
            case "reasoning":
                ValidateItemFields(item, "type", "id", "summary", "content", "encrypted_content", "internal_chat_message_metadata_passthrough");
                if (item["encrypted_content"] is not null)
                    throw new BadRequestException("Encrypted reasoning input cannot be replayed by this adapter.");
                var reasoningId = OptionalString(item, "id");
                if (reasoningId is null || !state.AssistantReplay.ContainsKey(reasoningId)) throw MissingReplay();
                AppendReplay(reasoningId, messages, state, seenAssistant, threadId);
                break;
            default:
                throw new BadRequestException("Unsupported Responses input item type.");
        }
    }

    private static void AppendReplay(string key, JsonArray messages, AdapterState state, HashSet<string> seen, string? threadId)
    {
        if (!state.AssistantReplay.TryGetValue(key, out var entry) || entry.ThreadId != threadId)
            throw MissingReplay();
        if (seen.Add(entry.CacheKey))
            messages.Add(entry.Message.DeepClone());
    }

    private static BadRequestException MissingReplay() =>
        new("Kimi continuation requires the exact assistant reasoning_content and tool_calls from this adapter session; matching history is unavailable.");

    private static ToolTranslation? TranslateTools(JsonArray? tools)
    {
        if (tools is null) return null;
        if (tools.Count > 256) throw new BadRequestException("Too many function tools.");
        var result = new JsonArray();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var reverse = new Dictionary<string, ToolIdentity>(StringComparer.Ordinal);
        foreach (var node in tools)
        {
            if (node is not JsonObject spec) throw new BadRequestException("Tool definition must be an object.");
            var type = StringValue(spec["type"], "tool.type");
            if (type == "function") AddFunctionTool(spec, OptionalString(spec, "namespace"), result, names, reverse);
            else if (type == "namespace")
            {
                ValidateItemFields(spec, "type", "name", "description", "tools");
                var ns = RequiredString(spec, "name", 256);
                if (spec["tools"] is not JsonArray children) throw new BadRequestException("Namespace tools must be an array.");
                foreach (var child in children)
                {
                    if (child is not JsonObject function || StringValue(function["type"], "namespace tool.type") != "function")
                        throw new BadRequestException("Only namespace function tools are supported.");
                    AddFunctionTool(function, ns, result, names, reverse);
                }
            }
            else throw new BadRequestException("Unsupported Responses tool type: " + type + ". Only function and namespace function tools are supported.");
        }
        return new ToolTranslation(result, reverse);
    }

    private static void AddFunctionTool(JsonObject tool, string? ns, JsonArray result, HashSet<string> names, Dictionary<string, ToolIdentity> reverse)
    {
        ValidateItemFields(tool, "type", "name", "namespace", "description", "parameters", "strict", "defer_loading");
        var originalName = RequiredString(tool, "name", 1024);
        var identity = new ToolIdentity(originalName, ns);
        var wire = WireName(identity.Name, identity.Namespace);
        if (!names.Add(wire)) throw new BadRequestException("Function tool names are ambiguous.");
        var parameters = tool["parameters"]?.DeepClone() ?? throw new BadRequestException("Function parameters schema is required.");
        if (parameters is not JsonObject) throw new BadRequestException("Function parameters must be a JSON schema object.");
        var mapped = new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = wire,
                ["description"] = OptionalString(tool, "description") ?? "",
                ["parameters"] = parameters
            }
        };
        if (tool["strict"] is not null) mapped["function"]!["strict"] = BooleanValue(tool["strict"], "tool.strict");
        if (tool["defer_loading"] is not null && BooleanValue(tool["defer_loading"], "tool.defer_loading"))
            throw new BadRequestException("Deferred function loading is unsupported.");
        result.Add(mapped);
        reverse.Add(wire, identity);
    }

    private static string WireName(string name, string? ns)
    {
        var material = Encoding.UTF8.GetBytes((ns ?? "") + "\0" + name);
        var hash = Convert.ToHexString(SHA256.HashData(material)).ToLowerInvariant()[..24];
        return "nbx_" + hash;
    }

    private static string? TranslateReasoning(JsonObject? reasoning, AdapterState state)
    {
        if (reasoning is null) return null;
        foreach (var pair in reasoning)
        {
            if (pair.Key == "effort")
            {
                if (pair.Value is null) continue;
                var effortValue = StringValue(pair.Value, "reasoning.effort");
                if (!state.SupportedEfforts.Contains(effortValue))
                    throw new BadRequestException("Reasoning effort is not explicitly supported by this model capability record.");
                return effortValue;
            }
            if (pair.Key == "summary" && (pair.Value is null || StringValue(pair.Value, "reasoning.summary") == "none"))
                continue;
            if (pair.Key == "context" && pair.Value is null)
                continue;
            throw new BadRequestException("Unsupported reasoning control: " + pair.Key + ".");
        }
        return null;
    }

    private static string ExtractText(JsonArray? content, string expectedType)
    {
        if (content is null) throw new BadRequestException("message content must be a text array.");
        var parts = new List<string>();
        foreach (var part in content)
        {
            if (part is not JsonObject entry || entry["type"]?.GetValue<string>() != expectedType)
                throw new BadRequestException("Only text message content is supported.");
            parts.Add(RequiredString(entry, "text", MaxBodyBytes));
        }
        return string.Join("", parts);
    }

    private static string ExtractOutput(JsonNode? output)
    {
        if (output is JsonValue value && value.TryGetValue<string>(out var text)) return text;
        if (output is JsonArray array)
        {
            var parts = new List<string>();
            foreach (var node in array)
            {
                if (node is not JsonObject entry || entry["type"]?.GetValue<string>() != "input_text")
                    throw new BadRequestException("Only text function results are supported.");
                parts.Add(RequiredString(entry, "text", MaxBodyBytes));
            }
            return string.Join("", parts);
        }
        throw new BadRequestException("Function result must contain text.");
    }

    private static JsonObject ParseArguments(string value)
    {
        try { return JsonNode.Parse(value)?.AsObject() ?? throw new JsonException(); }
        catch (JsonException) { throw new BadRequestException("Function arguments must be a JSON object."); }
    }

    private static JsonNode? TranslateUsage(JsonNode? source)
    {
        if (source is not JsonObject usage
            || !NonnegativeInt(usage["prompt_tokens"], out var input)
            || !NonnegativeInt(usage["completion_tokens"], out var output)
            || !NonnegativeInt(usage["total_tokens"], out var total))
            return null;
        var mapped = new JsonObject { ["input_tokens"] = input, ["output_tokens"] = output, ["total_tokens"] = total };
        if (usage["prompt_tokens_details"] is JsonObject promptDetails
            && NonnegativeInt(promptDetails["cached_tokens"], out var cached))
            mapped["input_tokens_details"] = new JsonObject { ["cached_tokens"] = cached };
        if (usage["completion_tokens_details"] is JsonObject completionDetails
            && NonnegativeInt(completionDetails["reasoning_tokens"], out var reasoning))
            mapped["output_tokens_details"] = new JsonObject { ["reasoning_tokens"] = reasoning };
        return mapped;
    }

    private static bool NonnegativeInt(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue json && json.TryGetValue<int>(out value) && value >= 0;
    }

    private static JsonNode TranslateCompletion(JsonObject completion, Translation translated, AdapterState state)
    {
        var choices = completion["choices"] as JsonArray;
        if (choices is null || choices.Count != 1 || choices[0] is not JsonObject choice || choice["message"] is not JsonObject message)
            throw new UpstreamException();
        var responseId = NewId("resp");
        var items = new JsonArray();
        AddAssistantItems(message, responseId, items, state, translated);
        var finish = OptionalString(choice, "finish_reason");
        if (finish is not ("stop" or "length" or "tool_calls" or "function_call"))
            throw new UpstreamException();
        var status = finish == "length" ? "incomplete" : "completed";
        var echoedModel = OptionalString(completion, "model");
        if (echoedModel is not null && echoedModel != state.Model)
            throw new UpstreamException();
        var response = new JsonObject
        {
            ["id"] = responseId, ["object"] = "response", ["status"] = status,
            ["output"] = items, ["end_turn"] = items.All(x => x?["type"]?.GetValue<string>() is "message" or "reasoning"),
            ["usage"] = TranslateUsage(completion["usage"])
        };
        if (echoedModel is not null) response["model"] = echoedModel;
        if (finish == "length")
            response["incomplete_details"] = new JsonObject { ["reason"] = "max_output_tokens" };
        return response;
    }

    private static async Task StreamResponse(HttpContext context, HttpResponseMessage upstream, Translation translated, AdapterState state, CancellationToken cancellationToken)
    {
        if (upstream.Content.Headers.ContentType?.MediaType != "text/event-stream")
            throw new UpstreamException();
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        var responseId = NewId("resp");
        await WriteSse(context, "response.created", new JsonObject { ["response"] = new JsonObject { ["id"] = responseId, ["object"] = "response", ["status"] = "in_progress" } }, cancellationToken);
        var messageId = NewId("msg");
        var text = new StringBuilder();
        var reasoning = new StringBuilder();
        var calls = new SortedDictionary<int, StreamCall>();
        var announcedMessage = false;
        var announcedReasoning = false;
        var nextOutputIndex = 0;
        var messageOutputIndex = -1;
        var reasoningOutputIndex = -1;
        var reasoningId = NewId("rs");
        string? finish = null;
        JsonNode? usage = null;
        string? echoedModel = null;
        var streamBytes = 0;
        using var reader = new StreamReader(await upstream.Content.ReadAsStreamAsync(cancellationToken), Encoding.UTF8);
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;
            streamBytes += Encoding.UTF8.GetByteCount(line) + 1;
            if (streamBytes > 8 * 1024 * 1024) throw new UpstreamException();
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var data = line[5..].TrimStart();
            if (data == "[DONE]") break;
            JsonObject chunk;
            try { chunk = JsonNode.Parse(data)?.AsObject() ?? throw new JsonException(); }
            catch (JsonException) { throw new UpstreamException(); }
            var chunkModel = OptionalString(chunk, "model");
            if (chunkModel is not null)
            {
                if (chunkModel != state.Model || echoedModel is not null && echoedModel != chunkModel) throw new UpstreamException();
                echoedModel = chunkModel;
            }
            if (chunk["usage"] is not null) usage = chunk["usage"]!.DeepClone();
            if (chunk["choices"] is not JsonArray choices || choices.Count == 0) continue;
            if (choices[0] is not JsonObject choice) throw new UpstreamException();
            finish ??= OptionalString(choice, "finish_reason");
            if (choice["delta"] is not JsonObject delta) continue;
            if (delta["content"] is JsonValue contentNode && contentNode.TryGetValue<string>(out var deltaText) && deltaText.Length > 0)
            {
                if (!announcedMessage)
                {
                    messageOutputIndex = nextOutputIndex++;
                    await WriteSse(context, "response.output_item.added", OutputItemEvent(MessageItem(messageId), messageOutputIndex), cancellationToken);
                    announcedMessage = true;
                }
                text.Append(deltaText);
                await WriteSse(context, "response.output_text.delta", new JsonObject { ["item_id"] = messageId, ["output_index"] = messageOutputIndex, ["content_index"] = 0, ["delta"] = deltaText }, cancellationToken);
            }
            if (delta["reasoning_content"] is JsonValue reasonNode && reasonNode.TryGetValue<string>(out var deltaReason) && deltaReason.Length > 0)
            {
                if (!announcedReasoning)
                {
                    reasoningOutputIndex = nextOutputIndex++;
                    await WriteSse(context, "response.output_item.added", OutputItemEvent(ReasoningItem(reasoningId, "", null), reasoningOutputIndex), cancellationToken);
                    announcedReasoning = true;
                }
                reasoning.Append(deltaReason);
                await WriteSse(context, "response.reasoning_text.delta", new JsonObject { ["item_id"] = reasoningId, ["content_index"] = 0, ["delta"] = deltaReason }, cancellationToken);
            }
            if (delta["tool_calls"] is JsonArray toolDeltas)
            {
                foreach (var toolNode in toolDeltas)
                {
                    if (toolNode is not JsonObject tool) throw new UpstreamException();
                    var index = tool["index"]?.GetValue<int>() ?? throw new UpstreamException();
                    if (!calls.TryGetValue(index, out var call)) calls[index] = call = new StreamCall(index);
                    call.Accept(tool);
                    if (!call.Announced && call.Id is not null && call.WireName is not null)
                    {
                        call.Decoded = translated.ToolNames.TryGetValue(call.WireName, out var identity) ? identity : throw new UpstreamException();
                        call.OutputIndex = nextOutputIndex++;
                        await WriteSse(context, "response.output_item.added", OutputItemEvent(FunctionItem(call), call.OutputIndex), cancellationToken);
                        call.Announced = true;
                    }
                    if (tool["function"] is JsonObject fn && fn["arguments"] is JsonValue argNode && argNode.TryGetValue<string>(out var argDelta) && argDelta.Length > 0)
                    {
                        call.Arguments.Append(argDelta);
                        if (!call.Announced) throw new UpstreamException();
                        await WriteSse(context, "response.function_call_arguments.delta", new JsonObject { ["item_id"] = call.ItemId, ["output_index"] = call.OutputIndex, ["delta"] = argDelta }, cancellationToken);
                    }
                }
            }
        }
        if (finish is not ("stop" or "length" or "tool_calls" or "function_call"))
            throw new UpstreamException();
        foreach (var call in calls.Values)
        {
            if (!call.Announced || call.Id is null) throw new UpstreamException();
            _ = ParseArguments(call.Arguments.ToString());
        }
        var finalMessage = new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = text.Length == 0 ? null : JsonValue.Create(text.ToString()),
            ["tool_calls"] = calls.Count == 0 ? null : new JsonArray(calls.Values.Select(call => (JsonNode?)ChatToolCall(call)).ToArray())
        };
        if (state.RequiresReasoningReplay && reasoning.Length == 0)
            throw new UpstreamException();
        if (reasoning.Length > 0 || calls.Count > 0)
        {
            if (reasoning.Length > 0) finalMessage["reasoning_content"] = reasoning.ToString();
            var keys = new List<string> { responseId, messageId };
            if (announcedReasoning) keys.Add(reasoningId);
            keys.AddRange(calls.Values.Select(call => call.Id!));
            state.Cache(finalMessage, keys, translated.ThreadId);
        }
        if (announcedMessage)
            await WriteSse(context, "response.output_item.done", OutputItemEvent(MessageItem(messageId, text.ToString(), "completed"), messageOutputIndex), cancellationToken);
        var items = new JsonArray();
        var orderedItems = new List<(int Index, JsonObject Item)>();
        if (announcedReasoning)
        {
            var reasonItem = ReasoningItem(reasoningId, reasoning.ToString(), null);
            orderedItems.Add((reasoningOutputIndex, reasonItem));
            await WriteSse(context, "response.output_item.done", OutputItemEvent(reasonItem, reasoningOutputIndex), cancellationToken);
        }
        if (announcedMessage) orderedItems.Add((messageOutputIndex, MessageItem(messageId, text.ToString(), "completed")));
        foreach (var call in calls.Values)
        {
            var functionItem = FunctionItem(call, call.Arguments.ToString());
            orderedItems.Add((call.OutputIndex, functionItem));
            await WriteSse(context, "response.output_item.done", OutputItemEvent(functionItem, call.OutputIndex), cancellationToken);
        }
        foreach (var ordered in orderedItems.OrderBy(item => item.Index)) items.Add(ordered.Item.DeepClone());
        var incomplete = finish == "length";
        await WriteSse(context, incomplete ? "response.incomplete" : "response.completed",
            new JsonObject { ["response"] = new JsonObject
            {
                ["id"] = responseId, ["object"] = "response", ["status"] = incomplete ? "incomplete" : "completed",
                ["model"] = echoedModel, ["output"] = items, ["usage"] = TranslateUsage(usage),
                ["end_turn"] = calls.Count == 0, ["incomplete_details"] = incomplete ? new JsonObject { ["reason"] = "max_output_tokens" } : null
            } }, cancellationToken);
    }

    private static void AddAssistantItems(JsonObject source, string responseId, JsonArray output, AdapterState state, Translation translated)
    {
        var text = source["content"] is JsonValue textNode && textNode.TryGetValue<string>(out var content) ? content : "";
        var reasoning = source["reasoning_content"] is JsonValue reasoningNode && reasoningNode.TryGetValue<string>(out var thought) ? thought : "";
        if (state.RequiresReasoningReplay && reasoning.Length == 0)
            throw new UpstreamException();
        var outputIds = new List<string>();
        if (reasoning.Length > 0)
        {
            var id = NewId("rs");
            output.Add(ReasoningItem(id, reasoning, null));
            outputIds.Add(id);
        }
        if (text.Length > 0)
        {
            var id = NewId("msg");
            output.Add(MessageItem(id, text, "completed"));
            outputIds.Add(id);
        }
        var chatTools = source["tool_calls"] as JsonArray;
        var mappedCalls = new List<(JsonObject Chat, JsonObject Item)>();
        if (chatTools is not null)
        {
            foreach (var node in chatTools)
            {
                if (node is not JsonObject call || call["function"] is not JsonObject fn) throw new UpstreamException();
                var callId = RequiredString(call, "id", 256);
                var wireName = RequiredString(fn, "name", 256);
                if (!translated.ToolNames.TryGetValue(wireName, out var identity)) throw new UpstreamException();
                var (name, ns) = (identity.Name, identity.Namespace);
                var args = ParseArguments(RequiredString(fn, "arguments", 256 * 1024));
                var item = new JsonObject { ["type"] = "function_call", ["id"] = NewId("fc"), ["call_id"] = callId, ["name"] = name, ["arguments"] = args.ToJsonString(JsonOptions) };
                if (ns is not null) item["namespace"] = ns;
                output.Add(item);
                mappedCalls.Add((ChatToolCall(callId, fn["name"]!.GetValue<string>(), args.ToJsonString(JsonOptions)), item));
                outputIds.Add(item["id"]!.GetValue<string>());
                state.CallKeys[callId] = true;
            }
        }
        if (state.RequiresReasoningReplay && reasoning.Length == 0)
            throw new UpstreamException();
        if (reasoning.Length > 0 || chatTools is { Count: > 0 })
        {
            var chatMessage = new JsonObject { ["role"] = "assistant", ["content"] = text.Length == 0 ? null : JsonValue.Create(text), ["reasoning_content"] = reasoning };
            if (chatTools is not null) chatMessage["tool_calls"] = chatTools.DeepClone();
            var keys = new List<string> { responseId };
            keys.AddRange(outputIds);
            if (chatTools is not null)
                foreach (var call in chatTools)
                    if (call?["id"]?.GetValue<string>() is string callId) keys.Add(callId);
            state.Cache(chatMessage, keys, translated.ThreadId);
        }
    }

    private static JsonObject MessageItem(string id, string? text = null, string status = "in_progress") => new()
    {
        ["type"] = "message", ["id"] = id, ["role"] = "assistant", ["status"] = status,
        ["content"] = text is null ? new JsonArray() : new JsonArray(new JsonObject { ["type"] = "output_text", ["text"] = text })
    };

    private static JsonObject OutputItemEvent(JsonObject item, int outputIndex) => new()
    {
        ["item"] = item,
        ["output_index"] = outputIndex
    };

    private static JsonObject ReasoningItem(string id, string text, string? status) => new()
    {
        ["type"] = "reasoning", ["id"] = id,
        ["summary"] = new JsonArray(), ["content"] = new JsonArray(new JsonObject { ["type"] = "reasoning_text", ["text"] = text }),
        ["encrypted_content"] = null,
        ["status"] = status
    };

    private static JsonObject FunctionItem(StreamCall call, string? arguments = null)
    {
        var item = new JsonObject
        {
            ["type"] = "function_call", ["id"] = call.ItemId, ["call_id"] = call.Id,
            ["name"] = call.Decoded?.Name ?? throw new UpstreamException(),
            ["arguments"] = arguments ?? ""
        };
        if (call.Decoded is { } decoded && decoded.Namespace is not null) item["namespace"] = decoded.Namespace;
        return item;
    }

    private static JsonObject ChatToolCall(StreamCall call) => ChatToolCall(call.Id!, call.WireName!, call.Arguments.ToString());

    private static JsonObject ChatToolCall(string id, string name, string arguments) => new()
    {
        ["id"] = id, ["type"] = "function",
        ["function"] = new JsonObject { ["name"] = name, ["arguments"] = arguments }
    };

    private static JsonObject? FindAssistantWithCall(JsonArray messages, string callId)
    {
        foreach (var node in messages)
            if (node is JsonObject message && message["tool_calls"] is JsonArray calls
                && calls.Any(call => call?["id"]?.GetValue<string>() == callId)) return message;
        return null;
    }

    private static async Task WriteSse(HttpContext context, string type, JsonObject payload, CancellationToken token)
    {
        payload["type"] = type;
        await context.Response.WriteAsync("data: " + payload.ToJsonString(JsonOptions) + "\n\n", token);
        await context.Response.Body.FlushAsync(token);
    }

    private static async Task<byte[]> ReadBounded(HttpContent content, int maximum, CancellationToken token)
    {
        await using var stream = await content.ReadAsStreamAsync(token);
        using var result = new MemoryStream();
        var buffer = new byte[32 * 1024];
        while (true)
        {
            var count = await stream.ReadAsync(buffer, token);
            if (count == 0) break;
            if (result.Length + count > maximum) throw new UpstreamException();
            result.Write(buffer, 0, count);
        }
        return result.ToArray();
    }

    private static async Task WriteError(HttpContext context, int status, string type, string message, string? code = null)
    {
        if (context.Response.HasStarted) { context.Abort(); return; }
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        var error = new JsonObject { ["type"] = type, ["message"] = message };
        if (code is not null) error["code"] = code;
        await context.Response.WriteAsync(new JsonObject { ["error"] = error }.ToJsonString(JsonOptions));
    }

    private static HashSet<string> ParseEfforts(JsonNode? node)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (node is not JsonArray array) return result;
        foreach (var effortNode in array)
        {
            var effort = effortNode?.GetValue<string>();
            if (effort is not ("low" or "high" or "max") || !result.Add(effort))
                throw new InvalidDataException();
        }
        return result;
    }

    private static int PositiveInt(JsonNode? node, int fallback)
    {
        if (node is null) return fallback;
        var value = node.GetValue<int>();
        return value > 0 ? value : throw new InvalidDataException();
    }

    private static int RequiredPositiveInt(JsonNode? node, string name)
    {
        if (node is not JsonValue value || !value.TryGetValue<int>(out var result) || result <= 0)
            throw new BadRequestException(name + " must be a positive integer.");
        return result;
    }

    private static string RequiredString(JsonObject obj, string key, int maximum)
    {
        var value = StringValue(obj[key], key);
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
            throw new BadRequestException(key + " is required and must fit the adapter limit.");
        return value;
    }

    private static string StringValue(JsonNode? node, string name)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var result)) return result;
        throw new BadRequestException(name + " must be a string.");
    }

    private static bool BooleanValue(JsonNode? node, string name)
    {
        if (node is JsonValue value && value.TryGetValue<bool>(out var result)) return result;
        throw new BadRequestException(name + " must be a boolean.");
    }

    private static void ValidateItemFields(JsonObject item, params string[] allowed)
    {
        var allowedNames = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var pair in item)
            if (!allowedNames.Contains(pair.Key))
                throw new BadRequestException("Unsupported item field: " + pair.Key + ".");
    }

    private static string? OptionalString(JsonObject obj, string key)
    {
        if (obj[key] is null) return null;
        if (obj[key] is not JsonValue value || !value.TryGetValue<string>(out var result))
            throw new BadRequestException(key + " must be a string.");
        return result;
    }

    private static bool FixedEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string NewId(string prefix) => prefix + "_" + Guid.NewGuid().ToString("N");

    private sealed class AdapterState(string apiKey, string model, string sessionToken, HashSet<string> efforts, int maxCompletionTokens)
    {
        public string ApiKey { get; } = apiKey;
        public string Model { get; } = model;
        public string SessionToken { get; } = sessionToken;
        public HashSet<string> SupportedEfforts { get; } = efforts;
        public int MaxCompletionTokens { get; } = maxCompletionTokens;
        public bool RequiresReasoningReplay => Model == "moonshotai/kimi-k3";
        public SemaphoreSlim RequestGate { get; } = new(1, 1);
        public ConcurrentDictionary<string, ReplayEntry> AssistantReplay { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<string, bool> CallKeys { get; } = new(StringComparer.Ordinal);
        private readonly ConcurrentQueue<ReplayEntry> _cacheOrder = new();
        private int _cachedBytes;

        public void Cache(JsonObject message, IEnumerable<string> keys, string? threadId)
        {
            var bytes = Encoding.UTF8.GetByteCount(message.ToJsonString(JsonOptions));
            if (bytes > MaxCachedAssistantBytes) throw new UpstreamException();
            var entry = new ReplayEntry(NewId("cache"), message.DeepClone().AsObject(), bytes, threadId);
            foreach (var key in keys)
            {
                if (AssistantReplay.TryGetValue(key, out var existing) && existing.CacheKey != entry.CacheKey)
                    throw new UpstreamException();
                AssistantReplay[key] = entry;
            }
            _cacheOrder.Enqueue(entry);
            Interlocked.Add(ref _cachedBytes, bytes);
            while (_cacheOrder.Count > MaxCachedAssistantMessages || Volatile.Read(ref _cachedBytes) > MaxCachedAssistantBytes)
            {
                if (!_cacheOrder.TryDequeue(out var evicted)) break;
                Interlocked.Add(ref _cachedBytes, -evicted.Bytes);
                foreach (var pair in AssistantReplay)
                    if (ReferenceEquals(pair.Value, evicted)) AssistantReplay.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed record ReplayEntry(string CacheKey, JsonObject Message, int Bytes, string? ThreadId);
    private sealed record ToolIdentity(string Name, string? Namespace);
    private sealed record ToolTranslation(JsonArray Items, Dictionary<string, ToolIdentity> ByWireName);
    private sealed record Translation(JsonObject Body, bool Stream, bool HasTools, Dictionary<string, ToolIdentity> ToolNames, List<string> Warnings, string? ThreadId);
    private sealed class StreamCall(int index)
    {
        public int Index { get; } = index;
        public string ItemId { get; } = NewId("fc");
        public string? Id { get; private set; }
        public string? WireName { get; private set; }
        public bool Announced { get; set; }
        public int OutputIndex { get; set; } = -1;
        public ToolIdentity? Decoded { get; set; }
        public StringBuilder Arguments { get; } = new();

        public void Accept(JsonObject delta)
        {
            if (delta["id"] is JsonValue id && id.TryGetValue<string>(out var idValue))
            {
                if (Id is not null && Id != idValue) throw new UpstreamException();
                Id = idValue;
            }
            if (delta["function"] is JsonObject function && function["name"] is JsonValue name && name.TryGetValue<string>(out var nameValue))
            {
                if (WireName is not null && WireName != nameValue) throw new UpstreamException();
                WireName = nameValue;
            }
        }
    }

    private sealed class BadRequestException(string message) : Exception(message);
    private sealed class UpstreamException(int statusCode = 502, string? code = null, string? apiKey = null, string? sessionToken = null) : Exception
    {
        public int StatusCode { get; } = statusCode is >= 400 and <= 599 ? statusCode : 502;
        public string? Code { get; } = code is not null && code.Length <= 64
            && code.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-')
            && (string.IsNullOrEmpty(apiKey) || !code.Contains(apiKey, StringComparison.Ordinal))
            && (string.IsNullOrEmpty(sessionToken) || !code.Contains(sessionToken, StringComparison.Ordinal))
            ? code : null;
    }
}
