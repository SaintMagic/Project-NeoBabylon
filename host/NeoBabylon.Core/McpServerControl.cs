using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;

namespace NeoBabylon.Core;

// Product-side parsed view of the pinned App Server v2 MCP control responses.
// This is status/control metadata, not a callable-tool registration.
public sealed record McpServerReloadReceipt(int SchemaVersion);

public enum McpServerRuntimeStatus
{
    NotStarted,
    Starting,
    Connected,
    AuthenticationRequired,
    Failed,
    Cancelled,
    Disabled
}

public enum McpServerAuthStatus
{
    Unknown,
    Unsupported,
    NotLoggedIn,
    BearerToken,
    OAuth
}

public sealed record McpServerStatusEntry(
    string Name,
    McpServerRuntimeStatus? RuntimeStatus,
    McpServerAuthStatus AuthStatus,
    IReadOnlyList<string> ToolNames,
    string? ToolsError,
    IReadOnlyList<McpServerToolDescriptor>? ToolDescriptors = null);

// Raw schema JSON is preserved from the pinned mcpServerStatus/list Tool records.
// A names-only observation leaves ToolDescriptors null and cannot qualify a route.
public sealed record McpServerToolDescriptor(string Name, string InputSchemaJson, string? OutputSchemaJson);

public sealed record McpServerStatusPage(
    int SchemaVersion,
    IReadOnlyList<McpServerStatusEntry> Servers,
    string? NextCursor);

public enum GeneratedToolMcpConfirmation
{
    Unknown,
    ConfirmedDisabledEmpty,
    UnexpectedTools
}

public static class GeneratedToolMcpStatus
{
    public static GeneratedToolMcpConfirmation Evaluate(McpServerStatusEntry? entry)
    {
        if (entry is null || !string.Equals(entry.Name, CodexConfigBuilder.ProductMcpServerName,
                StringComparison.Ordinal)) return GeneratedToolMcpConfirmation.Unknown;
        if (entry.ToolNames.Count != 0 || entry.ToolDescriptors is { Count: > 0 })
        {
            return GeneratedToolMcpConfirmation.UnexpectedTools;
        }
        return entry.RuntimeStatus == McpServerRuntimeStatus.Disabled && entry.ToolsError is null
            ? GeneratedToolMcpConfirmation.ConfirmedDisabledEmpty
            : GeneratedToolMcpConfirmation.Unknown;
    }

    public static IReadOnlyList<McpServerToolDescriptor> ParseToolDescriptors(JsonObject tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        if (tools.Count > 128)
        {
            throw new InvalidDataException("App Server MCP tool inventory exceeds its descriptor bound.");
        }

        var descriptors = new List<McpServerToolDescriptor>(tools.Count);
        foreach (var (mapName, node) in tools.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!IsBoundedName(mapName) || node is not JsonObject tool
                || tool["name"] is not JsonValue nameValue
                || !nameValue.TryGetValue<string>(out var name)
                || !string.Equals(name, mapName, StringComparison.Ordinal)
                || !tool.TryGetPropertyValue("inputSchema", out var inputSchema)
                || inputSchema is null)
            {
                throw new InvalidDataException("App Server MCP tool descriptor is incomplete or inconsistent.");
            }

            var inputJson = inputSchema.ToJsonString();
            string? outputJson = null;
            if (tool.TryGetPropertyValue("outputSchema", out var outputSchema) && outputSchema is not null)
            {
                outputJson = outputSchema.ToJsonString();
            }
            if (Encoding.UTF8.GetByteCount(inputJson) > 64 * 1024
                || outputJson is not null && Encoding.UTF8.GetByteCount(outputJson) > 64 * 1024)
            {
                throw new InvalidDataException("App Server MCP tool schema exceeds its descriptor bound.");
            }
            descriptors.Add(new McpServerToolDescriptor(mapName, inputJson, outputJson));
        }
        return descriptors.AsReadOnly();
    }

    public static GeneratedToolMcpInventoryConfirmation EvaluateCallableInventory(
        McpServerStatusEntry? entry,
        string expectedServerName,
        string expectedToolName,
        string expectedInputSchemaJson,
        string expectedOutputSchemaJson)
    {
        if (entry is null
            || !string.Equals(entry.Name, expectedServerName, StringComparison.Ordinal)
            || entry.ToolsError is not null
            || entry.RuntimeStatus != McpServerRuntimeStatus.Connected
            || entry.ToolNames is null
            || entry.ToolDescriptors is null)
        {
            return GeneratedToolMcpInventoryConfirmation.Unknown;
        }

        if (entry.ToolNames.Count != entry.ToolDescriptors.Count
            || !entry.ToolNames.OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(entry.ToolDescriptors.Select(tool => tool.Name).OrderBy(name => name, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            return GeneratedToolMcpInventoryConfirmation.UnexpectedTools;
        }
        if (entry.ToolDescriptors.Count != 1
            || !string.Equals(entry.ToolDescriptors[0].Name, expectedToolName, StringComparison.Ordinal))
        {
            return GeneratedToolMcpInventoryConfirmation.UnexpectedTools;
        }

        var observed = entry.ToolDescriptors[0];
        if (observed.OutputSchemaJson is null)
        {
            return GeneratedToolMcpInventoryConfirmation.Unknown;
        }
        try
        {
            var inputMatches = GeneratedToolJsonSchema.Canonicalize(observed.InputSchemaJson)
                == GeneratedToolJsonSchema.Canonicalize(expectedInputSchemaJson);
            var outputMatches = GeneratedToolJsonSchema.Canonicalize(observed.OutputSchemaJson)
                == GeneratedToolJsonSchema.Canonicalize(expectedOutputSchemaJson);
            return inputMatches && outputMatches
                ? GeneratedToolMcpInventoryConfirmation.ConfirmedExact
                : GeneratedToolMcpInventoryConfirmation.SchemaMismatch;
        }
        catch (InvalidDataException)
        {
            return GeneratedToolMcpInventoryConfirmation.SchemaMismatch;
        }
    }

    private static bool IsBoundedName(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && !value.Any(char.IsControl);
}

public enum GeneratedToolMcpInventoryConfirmation
{
    Unknown,
    ConfirmedExact,
    UnexpectedTools,
    SchemaMismatch
}

public enum McpControlFailureKind
{
    Cancelled,
    Timeout,
    RpcRejected,
    InvalidResponse,
    Transport
}

public sealed class McpControlException : Exception
{
    public McpControlFailureKind Kind { get; }

    public McpControlException(McpControlFailureKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }
}
