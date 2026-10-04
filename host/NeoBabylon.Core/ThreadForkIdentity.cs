using System.Text.Json.Nodes;

namespace NeoBabylon.Core;

public static class ThreadForkIdentity
{
    public static string RequireNewExactMatch(
        JsonObject forkResult,
        string sourceThreadId,
        ModelCapabilityRecord capability,
        string expectedWorkspace)
    {
        if (string.IsNullOrWhiteSpace(sourceThreadId))
        {
            throw new InvalidOperationException("A source App Server thread identifier is required; fork is blocked.");
        }

        ThreadResumeIdentity.RequireExactMatch(forkResult, capability, expectedWorkspace);

        var forkedThreadId = forkResult["thread"]?["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(forkedThreadId)
            || string.Equals(forkedThreadId, sourceThreadId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Codex App Server did not return a distinct forked thread identifier.");
        }

        return forkedThreadId;
    }
}
