using System.Text.Json.Nodes;
using NeoBabylon.Core;

if (args is ["--listen", "stdio://"])
{
    while (Console.ReadLine() is { } line)
    {
        var request = JsonNode.Parse(line)?.AsObject();
        if (request is null || request["id"] is not JsonNode id) continue;
        var method = request["method"]?.GetValue<string>();
        var threadId = request["params"]?["threadId"]?.GetValue<string>();
        if (threadId == "qa-timeout") continue;
        if (threadId == "qa-rpc")
        {
            Console.WriteLine(new JsonObject
            {
                ["id"] = id.DeepClone(),
                ["error"] = new JsonObject { ["code"] = -32600, ["message"] = "fixture rejection" }
            }.ToJsonString());
            continue;
        }
        var result = method switch
        {
            "initialize" => new JsonObject { ["codexHome"] = Environment.GetEnvironmentVariable("CODEX_HOME") },
            "config/mcpServer/reload" => new JsonObject(),
            "mcpServerStatus/list" when threadId == "qa-invalid" => new JsonObject { ["data"] = "bad" },
            "mcpServerStatus/list" => JsonNode.Parse("""
                {"data":[{"name":"p4_private","runtimeStatus":"disabled","tools":{},"toolsError":null,"authStatus":"unsupported"}],"nextCursor":null}
                """)!.AsObject(),
            _ => new JsonObject()
        };
        Console.WriteLine(new JsonObject { ["id"] = id.DeepClone(), ["result"] = result }.ToJsonString());
    }
    return 0;
}

void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

void RejectsInvalidResponse(Action action)
{
    try { action(); }
    catch (InvalidDataException) { return; }
    throw new InvalidOperationException("Malformed MCP status was accepted.");
}

async Task RequireFailureAsync(Func<Task> action, McpControlFailureKind expected)
{
    try { await action(); }
    catch (McpControlException exception) when (exception.Kind == expected) { return; }
    throw new InvalidOperationException($"Expected typed MCP control failure: {expected}.");
}

var reload = AppServerProtocol.BuildMcpServerReloadRequest(21);
Require(reload.Method == "config/mcpServer/reload" && reload.Params.Count == 0,
    "Reload changed method or gained parameters.");
Require(JsonNode.Parse(reload.ToJsonLine())?["params"] is null,
    "Reload must omit its optional unit params on the wire.");
var status = AppServerProtocol.BuildMcpServerStatusListRequest(22, "thread-1", "3");
Require(status.Method == "mcpServerStatus/list"
    && status.Params["threadId"]?.GetValue<string>() == "thread-1"
    && status.Params["cursor"]?.GetValue<string>() == "3"
    && status.Params["limit"]?.GetValue<int>() == 16
    && status.Params["detail"]?.GetValue<string>() == "toolsAndAuthOnly",
    "Status request does not match the bounded stable protocol.");
Require(AppServerProtocol.ParseMcpServerReloadResponse(new JsonObject()).SchemaVersion == 1,
    "Reload receipt was not versioned.");
RejectsInvalidResponse(() => AppServerProtocol.ParseMcpServerReloadResponse(new JsonObject { ["enabled"] = true }));

var page = AppServerProtocol.ParseMcpServerStatusListResponse(JsonNode.Parse("""
    {"data":[{"name":"p4_private","runtimeStatus":"disabled","tools":{},"toolsError":null,"authStatus":"unsupported"}],"nextCursor":null}
    """)!.AsObject());
Require(page.SchemaVersion == 1 && page.Servers.Count == 1
    && page.Servers[0].Name == "p4_private"
    && page.Servers[0].RuntimeStatus == McpServerRuntimeStatus.Disabled
    && page.Servers[0].ToolNames.Count == 0,
    "Disabled status was not parsed as a bounded typed page.");
var connected = AppServerProtocol.ParseMcpServerStatusListResponse(JsonNode.Parse("""
    {"data":[{"name":"p4_private","runtimeStatus":"connected","tools":{"echo_reviewed":{"name":"echo_reviewed","inputSchema":{"type":"object"}}},"toolsError":null,"authStatus":"unsupported"}],"nextCursor":"1"}
    """)!.AsObject());
Require(connected.Servers[0].RuntimeStatus == McpServerRuntimeStatus.Connected
    && connected.Servers[0].ToolNames.Single() == "echo_reviewed"
    && connected.NextCursor == "1",
    "Status parser lost connected inventory or pagination.");
RejectsInvalidResponse(() => AppServerProtocol.ParseMcpServerStatusListResponse(
    JsonNode.Parse("""{"data":[{"name":"x","runtimeStatus":"future","tools":{},"toolsError":null,"authStatus":"unsupported"}],"nextCursor":null}""")!.AsObject()));
RejectsInvalidResponse(() => AppServerProtocol.ParseMcpServerStatusListResponse(
    new JsonObject { ["data"] = "bad", ["nextCursor"] = null }));

var binary = Environment.ProcessPath ?? throw new InvalidOperationException("QA executable path unavailable.");
var qaRoot = Path.Combine(Path.GetTempPath(), "NeoBabylon-McpControlQA-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(qaRoot);
try
{
    await using var client = AppServerClient.Start(new AppServerLaunchOptions(
        binary, qaRoot, Path.Combine(qaRoot, "CodexHome"), null));
    using var testDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await client.InitializeAsync(testDeadline.Token);
    Require((await client.ReloadMcpServersAsync(testDeadline.Token)).SchemaVersion == 1,
        "Typed reload did not parse the empty receipt.");
    Require((await client.ListMcpServerStatusAsync("thread-1", cancellationToken: testDeadline.Token))
        .Servers.Single().RuntimeStatus == McpServerRuntimeStatus.Disabled,
        "Typed client lost disabled status.");
    await RequireFailureAsync(() => client.ListMcpServerStatusAsync("qa-rpc", cancellationToken: testDeadline.Token),
        McpControlFailureKind.RpcRejected);
    await RequireFailureAsync(() => client.ListMcpServerStatusAsync("qa-invalid", cancellationToken: testDeadline.Token),
        McpControlFailureKind.InvalidResponse);
    await RequireFailureAsync(() => client.ListMcpServerStatusAsync("qa-timeout",
        timeout: TimeSpan.FromMilliseconds(150)), McpControlFailureKind.Timeout);
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    await RequireFailureAsync(() => client.ReloadMcpServersAsync(cancelled.Token),
        McpControlFailureKind.Cancelled);
}
finally
{
    // Non-recursive deletion leaves unexpected QA artifacts visible instead of
    // following a changed path or deleting data this fixture did not create.
    var codexHome = Path.Combine(qaRoot, "CodexHome");
    if (Directory.Exists(codexHome)) Directory.Delete(codexHome);
    Directory.Delete(qaRoot);
}

Console.WriteLine("MCP control checks passed.");
return 0;
