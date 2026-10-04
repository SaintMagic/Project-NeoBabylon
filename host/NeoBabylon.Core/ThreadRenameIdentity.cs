using System.Text.Json.Nodes;

namespace NeoBabylon.Core;

public static class ThreadRenameIdentity
{
    public static string NormalizeName(string? name)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrEmpty(normalized)
            || normalized.Length > 160
            || normalized.Any(char.IsControl))
        {
            throw new InvalidDataException("A saved thread name must contain 1 to 160 non-control characters.");
        }

        return normalized;
    }

    public static void RequireExactWorkspaceRead(
        JsonObject? thread,
        string requestedThreadId,
        string expectedWorkspace)
    {
        if (thread is null
            || !string.Equals(thread["id"]?.GetValue<string>(), requestedThreadId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("App Server did not confirm the exact requested saved thread identity.");
        }

        var cwd = thread["cwd"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(cwd)
            || !string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(cwd)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedWorkspace)),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The saved thread does not belong to the selected NeoBabylon workspace.");
        }
    }

    public static void RequireRenameConfirmation(JsonObject? thread, string threadId, string expectedName)
    {
        if (thread is null
            || !string.Equals(thread["id"]?.GetValue<string>(), threadId, StringComparison.Ordinal)
            || !string.Equals(thread["name"]?.GetValue<string>(), expectedName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("App Server did not confirm the saved thread name update.");
        }
    }
}
