using System.Text;
using System.Text.Json.Nodes;

namespace NeoBabylon.Core;

public sealed record ThreadHistoryCursor(string? ScanCursor, string? StateDbCursor)
{
    public static string? CreateNext(
        string workspace,
        string providerId,
        string modelIdentifier,
        string? scanCursor,
        string? stateDbCursor)
    {
        ValidatePart(scanCursor);
        ValidatePart(stateDbCursor);
        if (scanCursor is null && stateDbCursor is null) return null;
        var payload = new JsonObject
        {
            ["version"] = 1,
            ["workspace"] = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspace)),
            ["providerId"] = providerId,
            ["modelIdentifier"] = modelIdentifier,
            ["scanCursor"] = scanCursor,
            ["stateDbCursor"] = stateDbCursor
        };
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload.ToJsonString()))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static ThreadHistoryCursor Decode(
        string token,
        string expectedWorkspace,
        string expectedProviderId,
        string expectedModelIdentifier)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096)
        {
            throw new InvalidOperationException("The saved-history page cursor is invalid.");
        }

        try
        {
            var encoded = token.Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
            var payload = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded))) as JsonObject;
            var workspace = payload?["workspace"]?.GetValue<string>();
            var providerId = payload?["providerId"]?.GetValue<string>();
            var modelIdentifier = payload?["modelIdentifier"]?.GetValue<string>();
            var scanCursor = payload?["scanCursor"]?.GetValue<string>();
            var stateDbCursor = payload?["stateDbCursor"]?.GetValue<string>();
            if (payload?["version"]?.GetValue<int>() != 1
                || string.IsNullOrWhiteSpace(workspace)
                || !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspace)),
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedWorkspace)), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(providerId, expectedProviderId, StringComparison.Ordinal)
                || !string.Equals(modelIdentifier, expectedModelIdentifier, StringComparison.Ordinal)
                || scanCursor is null && stateDbCursor is null)
            {
                throw new InvalidOperationException("The saved-history page cursor does not match the selected project/provider/model.");
            }
            ValidatePart(scanCursor);
            ValidatePart(stateDbCursor);
            return new ThreadHistoryCursor(scanCursor, stateDbCursor);
        }
        catch (Exception exception) when (exception is FormatException or System.Text.Json.JsonException
            or ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidOperationException("The saved-history page cursor is invalid.", exception);
        }
    }

    private static void ValidatePart(string? cursor)
    {
        if (cursor is not null && (string.IsNullOrWhiteSpace(cursor) || cursor.Length > 2048))
        {
            throw new InvalidOperationException("The upstream saved-history cursor is invalid.");
        }
    }
}
