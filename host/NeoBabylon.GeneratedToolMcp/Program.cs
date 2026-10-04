using System.Text;
using System.Text.Json;

namespace NeoBabylon.GeneratedToolMcp;

// This process deliberately has no candidate loader, command runner, or activation path.
internal static class Program
{
    private const int MaximumInputLineBytes = 65_536;
    private const int MaximumOutputLineBytes = 4_096;
    private const string ProtocolVersion = "2025-06-18";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static int Main(string[] args)
    {
        if (args.Length != 0)
        {
            Console.Error.WriteLine("The disabled generated-tool adapter accepts no arguments.");
            return 2;
        }

        try
        {
            using var input = new BufferedStream(Console.OpenStandardInput(), 4_096);
            using var output = Console.OpenStandardOutput();
            var initialized = false;
            while (ReadLine(input) is { } line)
            {
                if (line.Length == 0) continue;
                HandleLine(line, output, ref initialized);
            }
            return 0;
        }
        catch (Exception)
        {
            // Never echo request bytes, paths, or environment values to stderr.
            Console.Error.WriteLine("The disabled generated-tool adapter stopped after a bounded I/O failure.");
            return 2;
        }
    }

    private static byte[]? ReadLine(Stream input)
    {
        using var line = new MemoryStream();
        while (true)
        {
            var next = input.ReadByte();
            if (next < 0 || next == '\n')
            {
                if (next < 0 && line.Length == 0) return null;
                var bytes = line.ToArray();
                if (bytes.Length != 0 && bytes[^1] == '\r') Array.Resize(ref bytes, bytes.Length - 1);
                return bytes;
            }

            if (line.Length >= MaximumInputLineBytes)
                throw new InvalidDataException("MCP input line exceeded its byte limit.");
            line.WriteByte((byte)next);
        }
    }

    private static void HandleLine(byte[] line, Stream output, ref bool initialized)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(StrictUtf8.GetString(line),
                new JsonDocumentOptions { MaxDepth = 32 });
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            WriteError(output, null, -32700, "Parse error");
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || HasDuplicateProperties(root)
                || !root.TryGetProperty("jsonrpc", out var version)
                || version.ValueKind != JsonValueKind.String || version.GetString() != "2.0"
                || !root.TryGetProperty("method", out var method)
                || method.ValueKind != JsonValueKind.String)
            {
                WriteError(output, null, -32600, "Invalid Request");
                return;
            }

            var hasId = root.TryGetProperty("id", out var id);
            if (hasId && !IsBoundedId(id))
            {
                WriteError(output, null, -32600, "Invalid Request");
                return;
            }

            var name = method.GetString();
            if (!hasId)
            {
                // JSON-RPC notifications have no response. In particular, this covers
                // notifications/initialized; no notification can enable a tool.
                return;
            }

            switch (name)
            {
                case "initialize":
                    if (!root.TryGetProperty("params", out var parameters)
                        || parameters.ValueKind != JsonValueKind.Object
                        || !parameters.TryGetProperty("protocolVersion", out var requestedVersion)
                        || requestedVersion.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(requestedVersion.GetString()))
                    {
                        WriteError(output, id, -32602, "Invalid params");
                        return;
                    }
                    initialized = true;
                    WriteResult(output, id, new
                    {
                        protocolVersion = ProtocolVersion,
                        capabilities = new { tools = new { } },
                        serverInfo = new { name = "neobabylon-generated-tool-disabled", version = "0.1.0" }
                    });
                    return;

                case "tools/list":
                    if (!initialized)
                    {
                        WriteError(output, id, -32002, "Not initialized");
                        return;
                    }
                    WriteResult(output, id, new { tools = Array.Empty<object>() });
                    return;

                case "tools/call":
                    // Even a direct call that bypasses discovery is denied. Do not
                    // interpret the supplied name, arguments, or any candidate path.
                    WriteResult(output, id, new
                    {
                        content = new[] { new { type = "text", text = "Generated-tool execution is disabled." } },
                        structuredContent = new { status = "disabled", code = "NEOBABYLON_TOOL_DISABLED" },
                        isError = true
                    });
                    return;

                default:
                    WriteError(output, id, -32601, "Method not found");
                    return;
            }
        }
    }

    private static bool HasDuplicateProperties(JsonElement root)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        return root.EnumerateObject().Any(property => !names.Add(property.Name));
    }

    private static bool IsBoundedId(JsonElement id) => id.ValueKind switch
    {
        JsonValueKind.Number => id.TryGetInt64(out _),
        JsonValueKind.String => id.GetString() is { } value
            && StrictUtf8.GetByteCount(value) <= 256
            && !value.Any(char.IsControl),
        _ => false
    };

    private static void WriteResult(Stream output, JsonElement id, object result) =>
        Write(output, new { jsonrpc = "2.0", id, result });

    private static void WriteError(Stream output, JsonElement? id, int code, string message) =>
        Write(output, new { jsonrpc = "2.0", id, error = new { code, message } });

    private static void Write(Stream output, object response)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(response);
        if (bytes.Length + 1 > MaximumOutputLineBytes)
            throw new InvalidDataException("MCP output line exceeded its byte limit.");
        output.Write(bytes);
        output.WriteByte((byte)'\n');
        output.Flush();
    }
}
