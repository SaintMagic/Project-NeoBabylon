using System.Text.Json.Nodes;

namespace NeoBabylon.Core;

public static class ForkBookmarkIdentity
{
    public static void RequireExactMatch(JsonObject thread, ForkBookmark bookmark)
    {
        var threadId = thread["id"]?.GetValue<string>();
        var sourceThreadId = thread["forkedFromId"]?.GetValue<string>();
        var provider = thread["modelProvider"]?.GetValue<string>();
        var model = thread["model"]?.GetValue<string>();
        var cwd = thread["cwd"]?.GetValue<string>();

        if (!string.Equals(threadId, bookmark.ThreadId, StringComparison.Ordinal)
            || !string.Equals(sourceThreadId, bookmark.ForkedFromId, StringComparison.Ordinal)
            || !string.Equals(provider, bookmark.ModelProvider, StringComparison.Ordinal)
            || !string.Equals(model, bookmark.ModelIdentifier, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(cwd)
            || !string.Equals(Path.GetFullPath(cwd), bookmark.Workspace, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A persisted fork bookmark does not match the App Server thread identity; the history entry was not exposed.");
        }
    }
}
