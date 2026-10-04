using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace NeoBabylon.WorkspaceConsolidationInventory;

internal static class Program
{
    private const uint FileReadAttributes = 0x00000080;
    private const uint ShareAll = 0x00000007;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FsctlGetReparsePoint = 0x000900A8;
    private const int MaxReparseDataBufferSize = 16 * 1024;
    private const int FileIdInfoClass = 18;
    private const int Win32ErrorAlreadyExists = 183;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static int Main(string[] args)
    {
        OutputSession? output = null;
        try
        {
            if (!OperatingSystem.IsWindows())
                throw new InventoryException("This inventory is Windows-only.");

            var options = Options.Parse(args);
            ValidatePaths(options);
            output = OutputSession.Create(options.OutputDirectory);
            RunInventory(options, output);
            Console.WriteLine($"Complete: {output.EntryCount} entries; {output.TotalFileBytes} file-entry bytes; SHA-256 {output.OutputSha256}");
            return 0;
        }
        catch (Exception ex)
        {
            output?.Abort();
            Console.Error.WriteLine($"Inventory failed: {ex.Message}");
            return 2;
        }
        finally
        {
            output?.Dispose();
        }
    }

    private static void ValidatePaths(Options options)
    {
        if (!Path.IsPathFullyQualified(options.SourceRoot) || !Path.IsPathFullyQualified(options.OutputDirectory))
            throw new InventoryException("Source root and output directory must both be fully qualified paths.");

        options.SourceRoot = NormalizeDirectoryPath(options.SourceRoot);
        options.OutputDirectory = NormalizeDirectoryPath(options.OutputDirectory);

        if (!Directory.Exists(options.SourceRoot))
            throw new InventoryException("The explicit source root does not exist or is not a directory.");
        if (IsReparsePoint(options.SourceRoot))
            throw new InventoryException("The source root itself is a reparse point; refusing to resolve it.");

        EnsureNoReparseAncestors(options.SourceRoot, includeSelf: true);
        EnsureNoReparseAncestors(options.OutputDirectory, includeSelf: true);
        EnsureDisjoint(options.SourceRoot, options.OutputDirectory, "output directory");

        if (File.Exists(options.OutputDirectory) || Directory.Exists(options.OutputDirectory))
            throw new InventoryException("Output collision: the requested output path already exists.");

        if (options.ProjectedDestinationRoot is not null)
        {
            if (!Path.IsPathFullyQualified(options.ProjectedDestinationRoot))
                throw new InventoryException("Projected destination root must be fully qualified.");
            options.ProjectedDestinationRoot = NormalizeDirectoryPath(options.ProjectedDestinationRoot);
            if (options.MaxPathLength is null)
                throw new InventoryException("--projected-destination-root requires --max-path-length.");
        }
        else if (options.MaxPathLength is not null)
        {
            throw new InventoryException("--max-path-length requires --projected-destination-root.");
        }
    }

    private static void RunInventory(Options options, OutputSession output)
    {
        var timer = Stopwatch.StartNew();
        var rootMetadata = ReadMetadata(options.SourceRoot);
        if (!rootMetadata.IsDirectory || rootMetadata.IsReparsePoint)
            throw new InventoryException("The source root changed type or became a reparse point before enumeration.");

        Emit(new InventoryRow(".", rootMetadata), options, output, timer);

        // A depth-first stack of live enumerators retains O(depth) traversal state.
        var enumerators = new Stack<DirectoryCursor>();
        try
        {
            enumerators.Push(OpenDirectoryEnumerator(options.SourceRoot, rootMetadata));
            while (enumerators.Count > 0)
            {
                CheckDeadline(options, timer);
                var current = enumerators.Peek();
                if (!current.MoveNext())
                {
                    current.Dispose();
                    enumerators.Pop();
                    continue;
                }

                CheckDeadline(options, timer);
                var path = current.Current;
                var metadata = ReadMetadata(path);
                var row = new InventoryRow(RelativePath(options.SourceRoot, path), metadata);
                Emit(row, options, output, timer);

                if (metadata.IsDirectory && !metadata.IsReparsePoint)
                {
                    // Re-open and validate immediately before obtaining its child enumerator.
                    enumerators.Push(OpenDirectoryEnumerator(path, metadata));
                }
            }

            if (options.TestSyntheticRows > 0)
            {
                for (var i = 0; i < options.TestSyntheticRows; i++)
                {
                    CheckDeadline(options, timer);
                    Emit(InventoryRow.Synthetic(i), options, output, timer);
                }
            }

            CheckDeadline(options, timer);
            output.Complete(options.SourceRoot, options.ProjectedDestinationRoot, timer.Elapsed);
        }
        finally
        {
            while (enumerators.Count > 0)
                enumerators.Pop().Dispose();
        }
    }

    private static DirectoryCursor OpenDirectoryEnumerator(string directory, EntryMetadata expected)
    {
        SafeFileHandle? directoryHandle = null;
        try
        {
            // Omitting FILE_SHARE_DELETE pins this directory object at its inspected path until
            // its enumerator is disposed. OPEN_REPARSE_POINT makes the type check non-following.
            directoryHandle = NativeMethods.CreateFile(
                directory,
                FileReadAttributes,
                0x00000003, // FILE_SHARE_READ | FILE_SHARE_WRITE
                IntPtr.Zero,
                OpenExisting,
                FileFlagBackupSemantics | FileFlagOpenReparsePoint,
                IntPtr.Zero);
            if (directoryHandle.IsInvalid)
                throw NativeFailure($"Could not pin directory without following reparse points '{directory}'");
            if (!NativeMethods.GetFileInformationByHandle(directoryHandle, out var info))
                throw NativeFailure($"Could not verify directory before enumeration '{directory}'");
            if ((info.FileAttributes & (uint)FileAttributes.Directory) == 0 ||
                (info.FileAttributes & (uint)FileAttributes.ReparsePoint) != 0)
                throw new InventoryException($"Directory became a reparse point or changed type before enumeration: '{directory}'");
            unsafe
            {
                if (!NativeMethods.GetFileInformationByHandleEx(directoryHandle, FileIdInfoClass, out var currentId, (uint)sizeof(NativeMethods.FileIdInfo)))
                    throw NativeFailure($"Could not revalidate directory identity before enumeration '{directory}'");
                var currentFileId = new byte[16];
                byte* source = currentId.FileId;
                fixed (byte* destination = currentFileId)
                    Buffer.MemoryCopy(source, destination, 16, 16);
                if (currentId.VolumeSerialNumber.ToString("X16") != expected.VolumeSerial ||
                    Convert.ToHexString(currentFileId).ToLowerInvariant() != expected.FileId)
                    throw new InventoryException($"Directory identity changed before enumeration: '{directory}'");
            }

            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = false,
                IgnoreInaccessible = false,
                ReturnSpecialDirectories = false,
                AttributesToSkip = 0,
                BufferSize = 4096
            };
            var enumerator = Directory.EnumerateFileSystemEntries(directory, "*", enumerationOptions).GetEnumerator();
            return new DirectoryCursor(directoryHandle, enumerator);
        }
        catch (Exception ex) when (IsFilesystemFailure(ex))
        {
            directoryHandle?.Dispose();
            throw new InventoryException($"Could not enumerate directory '{directory}': {ex.Message}", ex);
        }
        catch
        {
            directoryHandle?.Dispose();
            throw;
        }
    }

    private static void Emit(InventoryRow row, Options options, OutputSession output, Stopwatch timer)
    {
        CheckDeadline(options, timer);
        if (options.TestFailAfterRows is long failAfter && output.EntryCount >= failAfter)
            throw new InventoryException("Test-only injected interruption; scan is incomplete.");

        if (options.ProjectedDestinationRoot is not null && options.MaxPathLength is int maxLength)
        {
            var relative = row.relativePath == "." ? string.Empty : row.relativePath;
            var projected = Path.GetFullPath(Path.Combine(options.ProjectedDestinationRoot, relative));
            if (projected.Length > maxLength)
                throw new InventoryException($"Projected path exceeds the configured limit ({projected.Length} > {maxLength}): {row.relativePath}");
        }

        output.Write(row);
    }

    private static void CheckDeadline(Options options, Stopwatch timer)
    {
        if (options.MaximumDuration is TimeSpan duration && timer.Elapsed >= duration)
            throw new InventoryException($"Maximum duration exceeded after {timer.Elapsed.TotalSeconds:F3} seconds; inventory is incomplete.");
    }

    private static EntryMetadata ReadMetadata(string path)
    {
        using var handle = NativeMethods.CreateFile(
            path,
            FileReadAttributes,
            ShareAll,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint,
            IntPtr.Zero);
        if (handle.IsInvalid)
            throw NativeFailure($"Could not open entry without following reparse points '{path}'");

        if (!NativeMethods.GetFileInformationByHandle(handle, out var basic))
            throw NativeFailure($"Could not read entry metadata '{path}'");

        NativeMethods.FileIdInfo idInfo;
        unsafe
        {
            if (!NativeMethods.GetFileInformationByHandleEx(handle, FileIdInfoClass, out idInfo, (uint)sizeof(NativeMethods.FileIdInfo)))
                throw NativeFailure($"Could not read lossless file identity '{path}'");
        }

        var attributes = (FileAttributes)basic.FileAttributes;
        var isReparse = (attributes & FileAttributes.ReparsePoint) != 0;
        var reparseTag = (string?)null;
        var reparseLength = (int?)null;
        var reparseHash = (string?)null;

        if (isReparse)
        {
            var raw = ReadRawReparseData(handle, path);
            var tag = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0, 4));
            var declaredDataLength = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(4, 2));
            if (raw.Length != 8 + declaredDataLength)
                throw new InventoryException($"Malformed reparse response length for '{path}': returned {raw.Length} bytes, header declares {8 + declaredDataLength}.");
            reparseTag = $"0x{tag:X8}";
            reparseLength = raw.Length;
            reparseHash = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
        }

        var idHex = new byte[16];
        unsafe
        {
            byte* source = idInfo.FileId;
            fixed (byte* destination = idHex)
                Buffer.MemoryCopy(source, destination, 16, 16);
        }

        var size = ((ulong)basic.FileSizeHigh << 32) | basic.FileSizeLow;
        var creation = ToUtc(basic.CreationTime);
        var access = ToUtc(basic.LastAccessTime);
        var write = ToUtc(basic.LastWriteTime);

        return new EntryMetadata(
            IsDirectory: (attributes & FileAttributes.Directory) != 0,
            IsReparsePoint: isReparse,
            Kind: isReparse ? ((attributes & FileAttributes.Directory) != 0 ? "directory-reparse-point" : "file-reparse-point") :
                ((attributes & FileAttributes.Directory) != 0 ? "directory" : "file"),
            Length: (attributes & FileAttributes.Directory) != 0 ? 0UL : size,
            Attributes: (uint)attributes,
            CreationUtc: creation.ToString("O"),
            LastAccessUtc: access.ToString("O"),
            LastWriteUtc: write.ToString("O"),
            VolumeSerial: idInfo.VolumeSerialNumber.ToString("X16"),
            FileId: Convert.ToHexString(idHex).ToLowerInvariant(),
            HardLinkCount: basic.NumberOfLinks,
            ReparseTag: reparseTag,
            ReparseDataLength: reparseLength,
            ReparsePayloadSha256: reparseHash);
    }

    private static byte[] ReadRawReparseData(SafeFileHandle handle, string path)
    {
        var buffer = new byte[MaxReparseDataBufferSize];
        if (!NativeMethods.DeviceIoControl(handle, FsctlGetReparsePoint, IntPtr.Zero, 0, buffer, (uint)buffer.Length, out var bytesReturned, IntPtr.Zero))
            throw NativeFailure($"Could not read exact reparse payload for '{path}'");
        if (bytesReturned < 8 || bytesReturned > buffer.Length)
            throw new InventoryException($"Invalid FSCTL_GET_REPARSE_POINT length {bytesReturned} for '{path}'.");
        Array.Resize(ref buffer, checked((int)bytesReturned));
        return buffer;
    }

    private static string RelativePath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InventoryException($"Enumerated path escaped the source root: '{path}'.");
        return relative.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private static string NormalizeDirectoryPath(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        while (full.Length > root.Length && Path.EndsInDirectorySeparator(full))
            full = full[..^1];
        return full;
    }

    private static void EnsureDisjoint(string source, string output, string description)
    {
        if (IsSameOrChild(source, output) || IsSameOrChild(output, source))
            throw new InventoryException($"The {description} overlaps the source tree; paths must be disjoint.");
    }

    private static bool IsSameOrChild(string parent, string candidate)
    {
        if (string.Equals(parent, candidate, StringComparison.OrdinalIgnoreCase))
            return true;
        var prefix = Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureNoReparseAncestors(string path, bool includeSelf)
    {
        var current = includeSelf ? path : Directory.GetParent(path)?.FullName;
        while (!string.IsNullOrEmpty(current))
        {
            if ((Directory.Exists(current) || File.Exists(current)) && IsReparsePoint(current))
                throw new InventoryException($"A path component is a reparse point; refusing ambiguous resolution: '{current}'.");
            var parent = Directory.GetParent(current);
            current = parent?.FullName;
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    private static DateTime ToUtc(NativeMethods.FileTime time)
    {
        var value = ((long)time.High << 32) | time.Low;
        return DateTime.FromFileTimeUtc(value);
    }

    private static bool IsFilesystemFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or System.Security.SecurityException;

    private static InventoryException NativeFailure(string message) =>
        new($"{message}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");

    private sealed class OutputSession : IDisposable
    {
        private readonly string _directory;
        private readonly string _partialPath;
        private readonly string _streamPath;
        private readonly string _manifestPath;
        private readonly FileStream _stream;
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private bool _complete;
        private bool _aborted;

        public long EntryCount { get; private set; }
        public ulong TotalFileBytes { get; private set; }
        public string OutputSha256 { get; private set; } = string.Empty;

        private OutputSession(string directory)
        {
            _directory = directory;
            _partialPath = Path.Combine(directory, "inventory.ndjson.partial");
            _streamPath = Path.Combine(directory, "inventory.ndjson");
            _manifestPath = Path.Combine(directory, "completion.json");

            if (!NativeMethods.CreateDirectory(directory, IntPtr.Zero))
            {
                var error = Marshal.GetLastWin32Error();
                throw new InventoryException(error == Win32ErrorAlreadyExists
                    ? "Output collision: the requested output path already exists."
                    : $"Could not create output directory: {new Win32Exception(error).Message}");
            }

            try
            {
                _stream = new FileStream(_partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan);
            }
            catch
            {
                try { Directory.Delete(directory); } catch { }
                throw;
            }
        }

        public static OutputSession Create(string directory) => new(directory);

        public void Write(InventoryRow row)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(row, JsonOptions);
            var line = new byte[json.Length + 1];
            Buffer.BlockCopy(json, 0, line, 0, json.Length);
            line[^1] = (byte)'\n';
            _stream.Write(line);
            _hash.AppendData(line);
            EntryCount++;
            if (row.kind is "file" or "file-reparse-point")
                TotalFileBytes = checked(TotalFileBytes + row.length);
        }

        public void Complete(string sourceRoot, string? projectedDestinationRoot, TimeSpan elapsed)
        {
            _stream.Flush(flushToDisk: true);
            OutputSha256 = Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant();
            _stream.Dispose();
            File.Move(_partialPath, _streamPath);

            var manifest = new CompletionManifest(
                Complete: true,
                SourceRoot: sourceRoot,
                ProjectedDestinationRoot: projectedDestinationRoot,
                EntryCount: EntryCount,
                TotalFileEntryBytes: TotalFileBytes,
                OutputFile: "inventory.ndjson",
                OutputSha256: OutputSha256,
                CompletedUtc: DateTime.UtcNow,
                ElapsedMilliseconds: (long)elapsed.TotalMilliseconds);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
            using (var manifestStream = new FileStream(_manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                manifestStream.Write(bytes);
                manifestStream.Flush(flushToDisk: true);
            }
            _complete = true;
        }

        public void Abort()
        {
            if (_complete || _aborted) return;
            _aborted = true;
            try { _stream.Dispose(); } catch { }
            try { if (File.Exists(_partialPath)) File.Delete(_partialPath); } catch { }
            try { if (File.Exists(_streamPath)) File.Delete(_streamPath); } catch { }
            try { if (File.Exists(_manifestPath)) File.Delete(_manifestPath); } catch { }
            try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: false); } catch { }
        }

        public void Dispose()
        {
            if (!_complete) Abort();
            _stream.Dispose();
            _hash.Dispose();
        }
    }

    private sealed class DirectoryCursor : IDisposable
    {
        private readonly SafeFileHandle _directoryHandle;
        private readonly IEnumerator<string> _enumerator;

        public DirectoryCursor(SafeFileHandle directoryHandle, IEnumerator<string> enumerator)
        {
            _directoryHandle = directoryHandle;
            _enumerator = enumerator;
        }

        public string Current => _enumerator.Current;
        public bool MoveNext() => _enumerator.MoveNext();

        public void Dispose()
        {
            try { _enumerator.Dispose(); }
            finally { _directoryHandle.Dispose(); }
        }
    }

    private sealed class Options
    {
        public required string SourceRoot { get; set; }
        public required string OutputDirectory { get; set; }
        public TimeSpan? MaximumDuration { get; private set; }
        public string? ProjectedDestinationRoot { get; set; }
        public int? MaxPathLength { get; private set; }
        public bool TestMode { get; private set; }
        public int TestSyntheticRows { get; private set; }
        public long? TestFailAfterRows { get; private set; }

        public static Options Parse(string[] args)
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < args.Length; i++)
            {
                var key = args[i];
                if (!key.StartsWith("--", StringComparison.Ordinal))
                    throw new InventoryException($"Unexpected argument '{key}'.");
                if (key is "--test-mode")
                {
                    if (!flags.Add(key)) throw new InventoryException($"Duplicate option '{key}'.");
                    continue;
                }
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new InventoryException($"Missing value for '{key}'.");
                if (!values.TryAdd(key, args[++i]))
                    throw new InventoryException($"Duplicate option '{key}'.");
            }

            string Required(string key) => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value!
                : throw new InventoryException($"Required option missing: {key}");

            var options = new Options { SourceRoot = Required("--source-root"), OutputDirectory = Required("--output-dir") };
            if (values.TryGetValue("--max-duration-seconds", out var durationText))
            {
                if (!double.TryParse(durationText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds) ||
                    !double.IsFinite(seconds) || seconds <= 0)
                    throw new InventoryException("--max-duration-seconds must be a finite positive number.");
                options.MaximumDuration = TimeSpan.FromSeconds(seconds);
            }
            if (values.TryGetValue("--projected-destination-root", out var projected)) options.ProjectedDestinationRoot = projected;
            if (values.TryGetValue("--max-path-length", out var maxPathText))
            {
                if (!int.TryParse(maxPathText, out var maxPath) || maxPath < 1 || maxPath > 32767)
                    throw new InventoryException("--max-path-length must be from 1 through 32767.");
                options.MaxPathLength = maxPath;
            }

            options.TestMode = flags.Contains("--test-mode");
            if (values.TryGetValue("--test-synthetic-rows", out var syntheticText))
            {
                if (!options.TestMode || !int.TryParse(syntheticText, out var synthetic) || synthetic < 0 || synthetic > 2_000_000)
                    throw new InventoryException("--test-synthetic-rows is a test-mode-only integer from 0 through 2000000.");
                options.TestSyntheticRows = synthetic;
            }
            if (values.TryGetValue("--test-fail-after-rows", out var failText))
            {
                if (!options.TestMode || !long.TryParse(failText, out var failAfter) || failAfter < 0)
                    throw new InventoryException("--test-fail-after-rows is a test-mode-only nonnegative integer.");
                options.TestFailAfterRows = failAfter;
            }
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "--source-root", "--output-dir", "--max-duration-seconds", "--projected-destination-root", "--max-path-length",
                "--test-synthetic-rows", "--test-fail-after-rows"
            };
            var unknown = values.Keys.FirstOrDefault(key => !known.Contains(key));
            if (unknown is not null) throw new InventoryException($"Unknown option '{unknown}'.");
            if (!options.TestMode && (values.ContainsKey("--test-synthetic-rows") || values.ContainsKey("--test-fail-after-rows")))
                throw new InventoryException("Test-only options require --test-mode.");
            if (options.TestMode && options.TestSyntheticRows == 0 && !options.TestFailAfterRows.HasValue)
                throw new InventoryException("--test-mode requires a test-only operation.");
            return options;
        }
    }

    private sealed class InventoryRow
    {
        public string relativePath { get; }
        public string kind { get; }
        public ulong length { get; }
        public string creationTimeUtc { get; }
        public string lastAccessTimeUtc { get; }
        public string lastWriteTimeUtc { get; }
        public uint attributes { get; }
        public string volumeSerial { get; }
        public string fileId { get; }
        public uint hardLinkCount { get; }
        public string? reparseTag { get; }
        public int? reparseDataLength { get; }
        public string? reparsePayloadSha256 { get; }
        public string? testRow { get; }

        public InventoryRow(string relativePath, EntryMetadata metadata)
        {
            this.relativePath = relativePath;
            kind = metadata.Kind;
            length = metadata.Length;
            creationTimeUtc = metadata.CreationUtc;
            lastAccessTimeUtc = metadata.LastAccessUtc;
            lastWriteTimeUtc = metadata.LastWriteUtc;
            attributes = metadata.Attributes;
            volumeSerial = metadata.VolumeSerial;
            fileId = metadata.FileId;
            hardLinkCount = metadata.HardLinkCount;
            reparseTag = metadata.ReparseTag;
            reparseDataLength = metadata.ReparseDataLength;
            reparsePayloadSha256 = metadata.ReparsePayloadSha256;
        }

        private InventoryRow(string relativePath, EntryMetadata metadata, string? testRow)
        {
            this.relativePath = relativePath;
            kind = metadata.Kind;
            length = metadata.Length;
            creationTimeUtc = metadata.CreationUtc;
            lastAccessTimeUtc = metadata.LastAccessUtc;
            lastWriteTimeUtc = metadata.LastWriteUtc;
            attributes = metadata.Attributes;
            volumeSerial = metadata.VolumeSerial;
            fileId = metadata.FileId;
            hardLinkCount = metadata.HardLinkCount;
            reparseTag = metadata.ReparseTag;
            reparseDataLength = metadata.ReparseDataLength;
            reparsePayloadSha256 = metadata.ReparsePayloadSha256;
            this.testRow = testRow;
        }

        public static InventoryRow Synthetic(int index) => new($"__synthetic__/{index:D6}", EntryMetadata.Synthetic, "stream-stress");
    }

    private sealed record CompletionManifest(
        bool Complete,
        string SourceRoot,
        string? ProjectedDestinationRoot,
        long EntryCount,
        ulong TotalFileEntryBytes,
        string OutputFile,
        string OutputSha256,
        DateTime CompletedUtc,
        long ElapsedMilliseconds);

    private sealed record EntryMetadata(
        bool IsDirectory,
        bool IsReparsePoint,
        string Kind,
        ulong Length,
        uint Attributes,
        string CreationUtc,
        string LastAccessUtc,
        string LastWriteUtc,
        string VolumeSerial,
        string FileId,
        uint HardLinkCount,
        string? ReparseTag,
        int? ReparseDataLength,
        string? ReparsePayloadSha256)
    {
        public static EntryMetadata Synthetic { get; } = new(
            false, false, "test-row", 0, 0, "", "", "", "", "", 0, null, null, null);
    }

    private sealed class InventoryException : Exception
    {
        public InventoryException(string message) : base(message) { }
        public InventoryException(string message, Exception inner) : base(message, inner) { }
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct FileTime { public uint Low; public uint High; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public FileTime CreationTime;
            public FileTime LastAccessTime;
            public FileTime LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct FileIdInfo
        {
            public ulong VolumeSerialNumber;
            public fixed byte FileId[16];
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
        internal static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
            uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation information);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetFileInformationByHandleEx")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern unsafe bool GetFileInformationByHandleEx(SafeFileHandle file, int informationClass, out FileIdInfo information, uint bufferSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeviceIoControl(SafeFileHandle device, uint controlCode, IntPtr inputBuffer, uint inputBufferSize,
            [Out] byte[] outputBuffer, uint outputBufferSize, out uint bytesReturned, IntPtr overlapped);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateDirectoryW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateDirectory(string path, IntPtr securityAttributes);
    }
}
