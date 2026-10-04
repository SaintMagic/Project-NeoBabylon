using System.Text.Json;

namespace NeoBabylon.Core;

public static class CapabilityRecordPathResolver
{
    public static (string Path, ModelCapabilityRecord Record)? FindExactRetainedRecord(
        string sourceRoot, string capabilityIdentity, string providerId, string modelIdentifier)
    {
        var releaseRoot = Path.Combine(sourceRoot, "docs", "release");
        foreach (var path in Directory.EnumerateFiles(releaseRoot, "MODEL_CAPABILITY_*.json"))
        {
            try
            {
                var resolved = Resolve(sourceRoot, path);
                var record = JsonSerializer.Deserialize<ModelCapabilityRecord>(File.ReadAllText(resolved),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (record is not null && record.ProviderId == providerId && record.ModelIdentifier == modelIdentifier
                    && string.Equals(CapabilityRecordIdentity.Compute(record), capabilityIdentity, StringComparison.Ordinal))
                    return (resolved, record);
            }
            catch (Exception error) when (error is IOException or JsonException or InvalidOperationException)
            {
                // An unrelated malformed record cannot replace the exact saved identity.
            }
        }
        return null;
    }

    public static string Resolve(string sourceRoot, string? configuredPath)
    {
        var releaseRoot = Path.GetFullPath(Path.Combine(sourceRoot, "docs", "release"));
        var releasePrefix = releaseRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(releaseRoot, "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json")
            : Path.GetFullPath(configuredPath, Path.GetFullPath(sourceRoot));

        if (!path.StartsWith(releasePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The selected model capability record must be inside NeoBabylon docs/release.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The selected model capability record does not exist.", path);
        }

        return path;
    }

    public static string ResolveForApprovalQa(string sourceRoot, string? configuredPath, string isolatedDataRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return Resolve(sourceRoot, null);
        }

        try
        {
            return Resolve(sourceRoot, configuredPath);
        }
        catch (InvalidOperationException)
        {
            var dataRoot = Path.GetFullPath(isolatedDataRoot);
            var dataPrefix = dataRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(configuredPath, Path.GetFullPath(sourceRoot));
            if (!path.StartsWith(dataPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("An ApprovalQA model capability record must be inside the isolated application Data root.");
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("The selected model capability record does not exist.", path);
            }

            return path;
        }
    }
}
