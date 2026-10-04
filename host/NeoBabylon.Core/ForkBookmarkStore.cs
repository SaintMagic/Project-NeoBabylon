using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoBabylon.Core;

public sealed record ForkBookmark(
    string ThreadId,
    string ForkedFromId,
    string ModelProvider,
    string ModelIdentifier,
    string Workspace,
    DateTimeOffset CreatedAtUtc);

public static class ForkBookmarkStore
{
    private const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static IReadOnlyList<ForkBookmark> Read(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var bytes = ProtectedDataRecordFile.Read(fullPath, value => Parse(value));
        if (bytes is null)
        {
            return [];
        }

        return Parse(bytes);
    }

    private static IReadOnlyList<ForkBookmark> Parse(byte[] bytes)
    {
        var document = JsonSerializer.Deserialize<ForkBookmarkDocument>(bytes, JsonOptions)
            ?? throw new InvalidDataException("NeoBabylon fork bookmark file is empty or invalid.");
        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException($"NeoBabylon fork bookmark schema version {document.SchemaVersion} is unsupported; this build supports version {CurrentSchemaVersion}. The saved record was preserved.");
        }

        if (!string.Equals(document.Product, "NeoBabylon", StringComparison.Ordinal)
            || document.Forks is null)
        {
            throw new InvalidDataException("NeoBabylon fork bookmark schema or product identity is not accepted.");
        }

        var bookmarks = new List<ForkBookmark>(document.Forks.Length);
        var byThreadId = new Dictionary<string, ForkBookmark>(StringComparer.Ordinal);
        foreach (var stored in document.Forks)
        {
            var bookmark = NormalizeAndValidate(stored);
            if (byThreadId.TryGetValue(bookmark.ThreadId, out var existing))
            {
                if (existing != bookmark)
                {
                    throw new InvalidDataException("NeoBabylon fork bookmark file contains conflicting records for one App Server thread.");
                }

                continue;
            }

            byThreadId.Add(bookmark.ThreadId, bookmark);
            bookmarks.Add(bookmark);
        }

        return bookmarks;
    }

    internal static void ValidateProtectedRecord(byte[] bytes) => _ = Parse(bytes);

    public static void Record(string path, ForkBookmark bookmark)
    {
        var fullPath = Path.GetFullPath(path);
        var normalized = NormalizeAndValidate(bookmark);
        var current = Read(fullPath).ToList();
        var existing = current.FirstOrDefault(item => string.Equals(item.ThreadId, normalized.ThreadId, StringComparison.Ordinal));
        if (existing is not null)
        {
            if (existing != normalized)
            {
                throw new InvalidDataException("NeoBabylon refused to replace a fork bookmark with conflicting thread identity.");
            }

            return;
        }

        current.Add(normalized);
        var document = new ForkBookmarkDocument(CurrentSchemaVersion, "NeoBabylon", current.ToArray());
        var replacement = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(document, JsonOptions));
        ProtectedDataRecordFile.Replace(fullPath, replacement, value => Parse(value), value => Parse(value));
    }

    private static ForkBookmark NormalizeAndValidate(ForkBookmark? bookmark)
    {
        if (bookmark is null)
        {
            throw new InvalidDataException("NeoBabylon fork bookmark file contains a null record.");
        }

        if (string.IsNullOrWhiteSpace(bookmark.ThreadId)
            || string.IsNullOrWhiteSpace(bookmark.ForkedFromId)
            || string.Equals(bookmark.ThreadId, bookmark.ForkedFromId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(bookmark.ModelProvider)
            || string.IsNullOrWhiteSpace(bookmark.ModelIdentifier)
            || string.IsNullOrWhiteSpace(bookmark.Workspace))
        {
            throw new InvalidDataException("NeoBabylon fork bookmark is missing a required thread or capability identity.");
        }

        return bookmark with
        {
            Workspace = Path.GetFullPath(bookmark.Workspace),
            CreatedAtUtc = bookmark.CreatedAtUtc.ToUniversalTime()
        };
    }

    private sealed record ForkBookmarkDocument(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("product")] string Product,
        [property: JsonPropertyName("forks")] ForkBookmark[] Forks);
}
