using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoBabylon.Core;

public sealed record RuntimeLockFile(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("product")] string Product,
    [property: JsonPropertyName("runtime")] RuntimeLockRuntime Runtime);

public sealed record RuntimeLockRuntime(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("sourceRepository")] string SourceRepository,
    [property: JsonPropertyName("sourceRef")] string SourceRef,
    [property: JsonPropertyName("sourceRevision")] string SourceRevision,
    [property: JsonPropertyName("sourceCheckoutRelativePath")] string SourceCheckoutRelativePath,
    [property: JsonPropertyName("appServerBinaryRelativePath")] string AppServerBinaryRelativePath,
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("protocol")] string Protocol,
    [property: JsonPropertyName("execution")] string Execution,
    [property: JsonPropertyName("sourcePatchSha256")] string? SourcePatchSha256 = null);

public sealed record RuntimeIdentity(
    string Version,
    string SourceRevision,
    string? SourcePatchSha256,
    string SourceRef,
    string SourceCheckoutPath,
    string BinaryPath,
    string Sha256,
    string Platform,
    string Protocol,
    string SourceRepository)
{
    public static RuntimeIdentity LoadVerified(string lockPath)
    {
        var fullLockPath = Path.GetFullPath(lockPath);
        if (!File.Exists(fullLockPath))
        {
            throw new FileNotFoundException("NeoBabylon runtime lock is missing.", fullLockPath);
        }

        var lockFile = JsonSerializer.Deserialize<RuntimeLockFile>(File.ReadAllText(fullLockPath), JsonOptions)
            ?? throw new InvalidDataException("NeoBabylon runtime lock is empty.");
        if (lockFile.SchemaVersion != 1 || !string.Equals(lockFile.Product, "NeoBabylon", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Runtime lock schema or product identity is not accepted.");
        }

        var productRoot = Directory.GetParent(fullLockPath)?.Parent?.FullName
            ?? throw new InvalidDataException("Runtime lock has no product root.");
        var binaryPath = Resolve(productRoot, lockFile.Runtime.AppServerBinaryRelativePath);
        if (!File.Exists(binaryPath))
        {
            throw new FileNotFoundException("Pinned Codex App Server binary is missing.", binaryPath);
        }

        var actualHash = ComputeSha256(binaryPath);
        if (!string.Equals(actualHash, lockFile.Runtime.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Pinned Codex App Server hash mismatch: expected {lockFile.Runtime.Sha256}, actual {actualHash}.");
        }

        if (lockFile.Runtime.SourcePatchSha256 is { } sourcePatchSha256
            && (sourcePatchSha256.Length != 64 || sourcePatchSha256.Any(character => !Uri.IsHexDigit(character))))
        {
            throw new InvalidDataException("Runtime source patch SHA-256 is malformed.");
        }

        var sourceCheckoutPath = Resolve(productRoot, lockFile.Runtime.SourceCheckoutRelativePath);
        if (!Directory.Exists(sourceCheckoutPath))
        {
            throw new DirectoryNotFoundException($"Pinned Codex source checkout is missing: {sourceCheckoutPath}");
        }

        return new RuntimeIdentity(
            lockFile.Runtime.Version,
            lockFile.Runtime.SourceRevision,
            lockFile.Runtime.SourcePatchSha256,
            lockFile.Runtime.SourceRef,
            sourceCheckoutPath,
            binaryPath,
            actualHash,
            lockFile.Runtime.Platform,
            lockFile.Runtime.Protocol,
            lockFile.Runtime.SourceRepository);
    }

    private static string Resolve(string productRoot, string relativePath) =>
        Path.GetFullPath(Path.Combine(productRoot, relativePath));

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
