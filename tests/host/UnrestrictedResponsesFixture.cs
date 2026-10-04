using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using NeoBabylon.Core;


sealed class UnrestrictedResponsesFixture : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly Task _serverLoop;
    private readonly List<string> _requestBodies = [];
    private readonly bool _pauseFirstResponse;
    private readonly string? _patchText;
    private readonly PatchFixtureFormat _patchFormat;
    private readonly IReadOnlyList<string>? _responseSequence;
    private readonly TaskCompletionSource _firstRequestReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releaseFirstResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _responseCount;
    private int _stopping;

    private UnrestrictedResponsesFixture(
        HttpListener listener,
        string baseUrl,
        bool pauseFirstResponse,
        string? patchText,
        PatchFixtureFormat patchFormat,
        IReadOnlyList<string>? responseSequence)
    {
        _listener = listener;
        BaseUrl = baseUrl;
        _pauseFirstResponse = pauseFirstResponse;
        _patchText = patchText;
        _patchFormat = patchFormat;
        _responseSequence = responseSequence?.ToArray();
        _serverLoop = ServeAsync();
    }

    public string BaseUrl { get; }
    public IReadOnlyList<string> RequestBodies
    {
        get { lock (_requestBodies) return _requestBodies.ToArray(); }
    }

    public Task WaitForFirstRequestAsync(TimeSpan timeout) => _firstRequestReceived.Task.WaitAsync(timeout);

    public void ReleaseFirstResponse() => _releaseFirstResponse.TrySetResult();

    public static Task<UnrestrictedResponsesFixture> StartAsync(
        bool pauseFirstResponse = false,
        string? patchText = null,
        PatchFixtureFormat patchFormat = PatchFixtureFormat.Freeform,
        IReadOnlyList<string>? responseSequence = null)
    {
        var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        return Task.FromResult(new UnrestrictedResponsesFixture(
            listener, $"http://127.0.0.1:{port}/v1", pauseFirstResponse, patchText, patchFormat, responseSequence));
    }

    private async Task ServeAsync()
    {
        try
        {
            while (_listener.IsListening)
            {
                var context = await _listener.GetContextAsync();
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();
                lock (_requestBodies) _requestBodies.Add(body);
                if (context.Request.Url?.AbsolutePath != "/v1/responses" || context.Request.HttpMethod != "POST")
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    continue;
                }

                var responseNumber = Interlocked.Increment(ref _responseCount);
                var firstResponse = responseNumber == 1;
                if (firstResponse)
                {
                    _firstRequestReceived.TrySetResult();
                    if (_pauseFirstResponse)
                    {
                        await _releaseFirstResponse.Task;
                    }
                }

                string response;
                if (_responseSequence is not null)
                {
                    var responseIndex = responseNumber - 1;
                    if (responseIndex >= _responseSequence.Count)
                    {
                        context.Response.StatusCode = 500;
                        context.Response.Close();
                        continue;
                    }

                    response = _responseSequence[responseIndex];
                }
                else
                {
                    response = firstResponse
                        ? _patchText is null
                            ? ResponsesSse.FunctionCall("unrestricted-response-1", "unrestricted-call-1", "exec_command", "{\"cmd\":\"cmd.exe /d /c ver\"}")
                            : _patchFormat == PatchFixtureFormat.Function
                                ? ResponsesSse.FunctionCall("patch-response-1", "patch-call-1", "apply_patch", new JsonObject { ["patch"] = _patchText }.ToJsonString())
                                : PatchCall(_patchText)
                        : ResponsesSse.AssistantMessage("unrestricted-response-2", "unrestricted-message-2", _patchText is null
                            ? "Fixture command completed." : "Fixture patch completed.");
                }
                var bytes = Encoding.UTF8.GetBytes(response);
                context.Response.ContentType = "text/event-stream";
                context.Response.ContentLength64 = bytes.Length;
                try
                {
                    await context.Response.OutputStream.WriteAsync(bytes);
                    context.Response.Close();
                }
                catch (HttpListenerException) when (_pauseFirstResponse && firstResponse) { }
                catch (IOException) when (_pauseFirstResponse && firstResponse) { }
            }
        }
        catch (HttpListenerException) when (Volatile.Read(ref _stopping) != 0 || !_listener.IsListening) { }
        catch (ObjectDisposedException) when (Volatile.Read(ref _stopping) != 0 || !_listener.IsListening) { }
    }

    public static string AssistantMessageWithTokens(string responseId, string itemId, string text, int totalTokens)
    {
        var events = new JsonObject[]
        {
            new() { ["type"] = "response.created", ["response"] = new JsonObject { ["id"] = responseId } },
            new()
            {
                ["type"] = "response.output_item.done",
                ["item"] = new JsonObject
                {
                    ["type"] = "message",
                    ["role"] = "assistant",
                    ["id"] = itemId,
                    ["content"] = new JsonArray
                    {
                        new JsonObject { ["type"] = "output_text", ["text"] = text }
                    }
                }
            },
            new()
            {
                ["type"] = "response.completed",
                ["response"] = new JsonObject
                {
                    ["id"] = responseId,
                    ["usage"] = new JsonObject
                    {
                        ["input_tokens"] = totalTokens,
                        ["input_tokens_details"] = null,
                        ["output_tokens"] = 0,
                        ["output_tokens_details"] = null,
                        ["total_tokens"] = totalTokens
                    }
                }
            }
        };
        var stream = new StringBuilder();
        foreach (var item in events)
        {
            stream.Append("event: ").Append(item["type"]?.GetValue<string>()).Append('\n');
            stream.Append("data: ").Append(item.ToJsonString()).Append("\n\n");
        }
        return stream.ToString();
    }

    private static string PatchCall(string patch)
    {
        var events = new JsonObject[]
        {
            new() { ["type"] = "response.created", ["response"] = new JsonObject { ["id"] = "patch-response-1" } },
            new()
            {
                ["type"] = "response.output_item.done",
                ["item"] = new JsonObject
                {
                    ["type"] = "custom_tool_call",
                    ["call_id"] = "patch-call-1",
                    ["name"] = "apply_patch",
                    ["input"] = patch
                }
            },
            new()
            {
                ["type"] = "response.completed",
                ["response"] = new JsonObject
                {
                    ["id"] = "patch-response-1",
                    ["usage"] = new JsonObject
                    {
                        ["input_tokens"] = 0,
                        ["input_tokens_details"] = null,
                        ["output_tokens"] = 0,
                        ["output_tokens_details"] = null,
                        ["total_tokens"] = 0
                    }
                }
            }
        };
        var stream = new StringBuilder();
        foreach (var item in events)
        {
            stream.Append("event: ").Append(item["type"]?.GetValue<string>()).Append('\n');
            stream.Append("data: ").Append(item.ToJsonString()).Append("\n\n");
        }
        return stream.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _stopping, 1);
        ReleaseFirstResponse();
        _listener.Stop();
        _listener.Close();
        await _serverLoop;
    }
}

enum PatchFixtureFormat
{
    Freeform,
    Function
}
