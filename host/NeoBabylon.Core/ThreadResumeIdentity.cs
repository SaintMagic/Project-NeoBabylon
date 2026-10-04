using System.Text.Json.Nodes;

namespace NeoBabylon.Core;

public static class ThreadResumeIdentity
{
    public static void RequireExactThreadWorkspace(JsonObject? thread, string requestedThreadId, string expectedWorkspace)
    {
        ThreadRenameIdentity.RequireExactWorkspaceRead(thread, requestedThreadId, expectedWorkspace);
    }

    public static void RequireExactRead(
        JsonObject? thread,
        string requestedThreadId,
        ModelCapabilityRecord capability,
        string expectedWorkspace)
    {
        if (thread is null || !string.Equals(thread["id"]?.GetValue<string>(), requestedThreadId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("App Server did not return the exact requested saved thread identity.");
        }
        RequireExactMatch(thread, capability, expectedWorkspace);
    }

    public static void RequireExactMatch(JsonObject thread, ModelCapabilityRecord capability, string expectedWorkspace)
    {
        var provider = thread["modelProvider"]?.GetValue<string>();
        var model = thread["model"]?.GetValue<string>();
        var cwd = thread["cwd"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(cwd))
        {
            throw new InvalidOperationException("The saved thread does not expose a complete provider/model/workspace identity; opening or forking is blocked.");
        }

        if (!string.Equals(provider, capability.ProviderId, StringComparison.Ordinal)
            || !string.Equals(model, capability.ModelIdentifier, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The saved thread belongs to a different or unknown provider/model pair. Select its exact capability record before opening or forking it.");
        }

        var actualWorkspace = Path.GetFullPath(cwd);
        var allowedWorkspace = Path.GetFullPath(expectedWorkspace);
        if (!string.Equals(actualWorkspace, allowedWorkspace, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The saved thread workspace does not match the currently selected NeoBabylon project; opening or forking is blocked.");
        }
    }
}
