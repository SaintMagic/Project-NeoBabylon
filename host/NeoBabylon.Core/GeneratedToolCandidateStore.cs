using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace NeoBabylon.Core;

public sealed class GeneratedToolCandidateListLimitException : IOException
{
    public GeneratedToolCandidateListLimitException(string message) : base(message) { }
}

// Positions are UTF-16 code-unit offsets, matching JavaScript string indexing.
public sealed record GeneratedToolCandidateFileRange(string Text, int Total, int Next, bool HasMore);

// Stores unapproved, reviewable data only. It does not register, load, or execute tools.
public static class GeneratedToolCandidateStore
{
    private const int MaximumManifestBytes = 64 * 1024;
    private const int MaximumFileBytes = 1024 * 1024;
    private const int MaximumTotalFileBytes = 16 * 1024 * 1024;
    private const int MaximumListedCandidates = 64;
    private const int MaximumListedFiles = 512;
    private const int MaximumListedFileBytes = 64 * 1024 * 1024;
    private const int MaximumFileRangeCharacters = 32768;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static GeneratedToolCandidate Create(
        string applicationRoot,
        GeneratedToolCandidateManifest manifest,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> files)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(files);
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, GeneratedToolCandidateValidator.JsonOptions);
        if (manifestBytes.Length > MaximumManifestBytes)
        {
            throw new InvalidDataException("Generated tool candidate manifest exceeds its size limit.");
        }
        // Freeze caller-owned collections before checking and writing their hash inventory.
        manifest = JsonSerializer.Deserialize<GeneratedToolCandidateManifest>(manifestBytes,
            GeneratedToolCandidateValidator.JsonOptions)
            ?? throw new InvalidDataException("Generated tool candidate manifest is empty.");
        GeneratedToolCandidateValidator.Validate(manifest, applicationRoot);
        if (files.Count != manifest.Files.Count)
        {
            throw new InvalidDataException("Generated tool candidate file inventory does not match supplied files.");
        }
        var root = GeneratedToolCandidatePath.Root(applicationRoot);
        var candidateRoot = GeneratedToolCandidatePath.Candidate(root, manifest.ToolId);
        var supplied = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, bytes) in files)
        {
            GeneratedToolCandidatePath.ResolveFile(candidateRoot, path);
            if (bytes.Length is 0 or > MaximumFileBytes)
            {
                throw new InvalidDataException("Generated tool candidate file is empty or too large.");
            }
            if (!supplied.TryAdd(path.Replace('/', '\\'), bytes.ToArray()))
            {
                throw new InvalidDataException("Generated tool candidate supplies the same file twice.");
            }
        }
        if (supplied.Count != manifest.Files.Count)
        {
            throw new InvalidDataException("Generated tool candidate file inventory does not match supplied files.");
        }

        var totalBytes = 0;
        foreach (var file in manifest.Files)
        {
            if (!supplied.TryGetValue(file.Path.Replace('/', '\\'), out var bytes))
            {
                throw new InvalidDataException("Generated tool candidate is missing an inventoried file.");
            }
            ValidateFile(bytes, file.Sha256);
            totalBytes = checked(totalBytes + bytes.Length);
            if (totalBytes > MaximumTotalFileBytes)
            {
                throw new InvalidDataException("Generated tool candidate exceeds its total file limit.");
            }
        }

        if (Directory.Exists(candidateRoot) || File.Exists(candidateRoot))
        {
            throw new InvalidDataException("Generated tool candidate identity already exists.");
        }

        var stagingRoot = Path.Combine(root, ".staging");
        Directory.CreateDirectory(stagingRoot);
        GeneratedToolCandidatePath.RejectReparseComponents(stagingRoot);
        var staging = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        GeneratedToolCandidatePath.RejectReparseComponents(staging);

        // A failed write is never listed as a candidate; do not delete unknown filesystem data.
        foreach (var file in manifest.Files)
        {
            var target = GeneratedToolCandidatePath.ResolveFile(staging, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            GeneratedToolCandidatePath.RejectReparseComponents(target);
            WriteNewFile(target, supplied[file.Path.Replace('/', '\\')]);
        }
        WriteNewFile(Path.Combine(staging, "tool.json"), manifestBytes);

        var candidatesRoot = Path.Combine(root, "Candidates");
        Directory.CreateDirectory(candidatesRoot);
        GeneratedToolCandidatePath.RejectReparseComponents(candidatesRoot);
        if (Directory.Exists(candidateRoot) || File.Exists(candidateRoot))
        {
            throw new InvalidDataException("Generated tool candidate identity already exists.");
        }
        Directory.Move(staging, candidateRoot);
        return Read(applicationRoot, manifest.ToolId)
            ?? throw new InvalidDataException("Generated tool candidate was not readable after storage.");
    }

    public static GeneratedToolCandidate? Read(string applicationRoot, string toolId)
        => ReadCore(applicationRoot, toolId, null);

    public static GeneratedToolCandidateFileRange ReadFileRange(
        string applicationRoot, string toolId, string contentIdentity, string relativePath, int offset)
    {
        if (offset < 0)
        {
            throw new InvalidDataException("Candidate file inspection requires a nonnegative offset.");
        }
        var candidate = Read(applicationRoot, toolId)
            ?? throw new InvalidDataException("Generated tool candidate does not exist.");
        if (!string.Equals(candidate.ContentIdentity, contentIdentity, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Generated tool candidate content identity is stale.");
        }
        var file = candidate.Manifest.Files.FirstOrDefault(item =>
            string.Equals(item.Path, relativePath, StringComparison.Ordinal))
            ?? throw new InvalidDataException("Candidate file inspection requires an exact inventoried path.");
        var path = GeneratedToolCandidatePath.ResolveFile(candidate.CandidatePath, file.Path);
        var bytes = ReadBounded(path, MaximumFileBytes);
        ValidateFile(bytes, file.Sha256);
        var content = StrictUtf8.GetString(bytes);
        if (offset > content.Length
            || offset > 0 && offset < content.Length
                && char.IsHighSurrogate(content[offset - 1]) && char.IsLowSurrogate(content[offset]))
        {
            throw new InvalidDataException("Candidate file inspection offset is outside a valid text boundary.");
        }
        var next = Math.Min(content.Length, offset + MaximumFileRangeCharacters);
        if (next < content.Length && char.IsHighSurrogate(content[next - 1])
            && char.IsLowSurrogate(content[next]))
        {
            next--;
        }
        return new GeneratedToolCandidateFileRange(content[offset..next], content.Length, next,
            next < content.Length);
    }

    private static GeneratedToolCandidate? ReadCore(string applicationRoot, string toolId, ListBudget? listBudget)
    {
        var root = GeneratedToolCandidatePath.Root(applicationRoot);
        var candidateRoot = GeneratedToolCandidatePath.Candidate(root, toolId);
        var candidatesRoot = Path.Combine(root, "Candidates");
        if (File.Exists(candidatesRoot))
        {
            throw new InvalidDataException("Generated tool candidates root is not a directory.");
        }
        if (!Directory.Exists(candidateRoot))
        {
            if (File.Exists(candidateRoot)) throw new InvalidDataException("Generated tool candidate path is not a directory.");
            return null;
        }
        var manifestPath = Path.Combine(candidateRoot, "tool.json");
        GeneratedToolCandidatePath.RejectReparseComponents(manifestPath);
        if (!File.Exists(manifestPath))
        {
            throw new InvalidDataException("Generated tool candidate manifest is missing.");
        }

        var manifestBytes = ReadBounded(manifestPath, MaximumManifestBytes);
        GeneratedToolCandidateManifest manifest;
        try
        {
            using var document = JsonDocument.Parse(manifestBytes);
            GeneratedToolCandidateValidator.RejectDuplicateProperties(document.RootElement);
            manifest = JsonSerializer.Deserialize<GeneratedToolCandidateManifest>(manifestBytes,
                GeneratedToolCandidateValidator.JsonOptions)
                ?? throw new InvalidDataException("Generated tool candidate manifest is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Generated tool candidate manifest is malformed.", exception);
        }
        GeneratedToolCandidateValidator.Validate(manifest, applicationRoot);
        if (!string.Equals(manifest.ToolId, toolId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Generated tool candidate manifest has a different directory identity.");
        }

        listBudget?.RequireFileSlots(manifest.Files.Count);
        RejectUnlistedEntries(candidateRoot, manifest.Files);
        var totalBytes = 0;
        foreach (var file in manifest.Files)
        {
            var path = GeneratedToolCandidatePath.ResolveFile(candidateRoot, file.Path);
            if (!File.Exists(path))
            {
                throw new InvalidDataException("Generated tool candidate is missing an inventoried file.");
            }
            var bytes = ReadBounded(path, MaximumFileBytes, listBudget);
            listBudget?.ConsumeFile();
            totalBytes = checked(totalBytes + bytes.Length);
            if (totalBytes > MaximumTotalFileBytes)
            {
                throw new InvalidDataException("Generated tool candidate exceeds its total file limit.");
            }
            ValidateFile(bytes, file.Sha256);
        }

        var identity = "candidate-v1:sha256:"
            + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
                manifest, GeneratedToolCandidateValidator.JsonOptions))).ToLowerInvariant();
        return new GeneratedToolCandidate(manifest, candidateRoot, identity,
            GeneratedToolCandidateValidator.Unapproved);
    }

    public static IReadOnlyList<GeneratedToolCandidate> List(string applicationRoot)
    {
        var root = GeneratedToolCandidatePath.Root(applicationRoot);
        var candidatesRoot = Path.Combine(root, "Candidates");
        GeneratedToolCandidatePath.RejectReparseComponents(candidatesRoot);
        if (File.Exists(candidatesRoot))
        {
            throw new InvalidDataException("Generated tool candidates root is not a directory.");
        }
        if (!Directory.Exists(candidatesRoot)) return [];
        var budget = new ListBudget();
        var candidates = new List<GeneratedToolCandidate>();
        foreach (var entry in Directory.EnumerateFileSystemEntries(candidatesRoot))
        {
            if (candidates.Count >= MaximumListedCandidates)
            {
                throw new GeneratedToolCandidateListLimitException("Generated tool candidate listing exceeds its candidate count limit.");
            }
            GeneratedToolCandidatePath.RejectReparseComponents(entry);
            var candidate = ReadCore(applicationRoot, Path.GetFileName(entry), budget)
                ?? throw new InvalidDataException("Generated tool candidate entry is not readable.");
            candidates.Add(candidate);
        }
        return candidates.OrderBy(candidate => candidate.Manifest.Origin.CreatedAtUtc).ToArray();
    }

    private static void ValidateFile(ReadOnlySpan<byte> bytes, string expectedSha256)
    {
        if (bytes.Length is 0 or > MaximumFileBytes)
        {
            throw new InvalidDataException("Generated tool candidate file is empty or too large.");
        }
        try
        {
            GeneratedToolCandidateValidator.RejectCredentialText(StrictUtf8.GetString(bytes));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("Generated tool candidate files must be UTF-8 text.", exception);
        }
        var actualSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Generated tool candidate file hash does not match its manifest.");
        }
    }

    private static byte[] ReadBounded(string path, int maximumBytes, ListBudget? listBudget = null)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        RequireSingleLink(stream.SafeFileHandle);
        if (stream.Length > maximumBytes)
        {
            throw new InvalidDataException("Generated tool candidate file exceeds its size limit.");
        }
        listBudget?.RequireBytes(stream.Length);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = stream.Read(chunk)) != 0)
        {
            if (buffer.Length + count > maximumBytes)
            {
                throw new InvalidDataException("Generated tool candidate file exceeds its size limit.");
            }
            listBudget?.RequireBytes(buffer.Length + count);
            buffer.Write(chunk, 0, count);
        }
        RequireSingleLink(stream.SafeFileHandle);
        listBudget?.ConsumeBytes(buffer.Length);
        return buffer.ToArray();
    }

    private static void WriteNewFile(string path, ReadOnlySpan<byte> bytes)
    {
        GeneratedToolCandidatePath.RejectReparseComponents(path);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 4096, FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
        RequireSingleLink(stream.SafeFileHandle);
    }

    private static void RequireSingleLink(SafeFileHandle handle)
    {
        if (!OperatingSystem.IsWindows()
            || !GetFileInformationByHandle(handle, out var information)
            || information.NumberOfLinks == 0)
        {
            throw new InvalidDataException("Generated tool candidate file link count could not be established.");
        }
        if (information.NumberOfLinks != 1)
        {
            throw new InvalidDataException("Generated tool candidate file has multiple hard links.");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleFileInformation information);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public uint CreationTimeLow;
        public uint CreationTimeHigh;
        public uint LastAccessTimeLow;
        public uint LastAccessTimeHigh;
        public uint LastWriteTimeLow;
        public uint LastWriteTimeHigh;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    private sealed class ListBudget
    {
        private int _remainingFiles = MaximumListedFiles;
        private long _remainingBytes = MaximumListedFileBytes;

        public void RequireFileSlots(int count)
        {
            if (count > _remainingFiles)
            {
                throw new GeneratedToolCandidateListLimitException("Generated tool candidate listing exceeds its aggregate file count limit.");
            }
        }

        public void ConsumeFile() => _remainingFiles--;

        public void RequireBytes(long count)
        {
            if (count > _remainingBytes)
            {
                throw new GeneratedToolCandidateListLimitException("Generated tool candidate listing exceeds its aggregate file byte limit.");
            }
        }

        public void ConsumeBytes(long count) => _remainingBytes -= count;
    }

    private static void RejectUnlistedEntries(string candidateRoot, IReadOnlyList<GeneratedToolFile> files)
    {
        var expectedFiles = new HashSet<string>(files.Select(file => file.Path.Replace('/', '\\')),
            StringComparer.OrdinalIgnoreCase) { "tool.json" };
        var expectedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var parts = file.Path.Replace('/', '\\').Split('\\');
            for (var count = 1; count < parts.Length; count++)
            {
                expectedDirectories.Add(string.Join("\\", parts.Take(count)));
            }
        }

        var pending = new Stack<string>();
        pending.Push(candidateRoot);
        while (pending.Count != 0)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                GeneratedToolCandidatePath.RejectReparseComponents(entry);
                var relative = Path.GetRelativePath(candidateRoot, entry).Replace('/', '\\');
                if (Directory.Exists(entry))
                {
                    if (!expectedDirectories.Contains(relative))
                    {
                        throw new InvalidDataException("Generated tool candidate has an unlisted directory.");
                    }
                    pending.Push(entry);
                }
                else if (!File.Exists(entry) || !expectedFiles.Contains(relative))
                {
                    throw new InvalidDataException("Generated tool candidate has an unlisted file.");
                }
            }
        }
    }
}
