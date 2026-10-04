using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NeoBabylon.Core;

internal static class P5_02DataMigrationChecks
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static void BackupReadbackAndRestoreMissing()
    {
        WithRegistry((root, layout, registry) =>
        {
            var project = Path.Combine(root, "project");
            Directory.CreateDirectory(project);
            registry.Save(registry.Read());
            var original = File.ReadAllBytes(registry.RegistryPath);
            var hash = Digest(original);
            registry.AddAndSelect(project);

            var backupRoot = Path.Combine(layout.DataRoot, "Backups", "NeoBabylon");
            var backup = Directory.EnumerateFiles(backupRoot, hash + ".bin", SearchOption.AllDirectories).Single();
            Require(File.ReadAllBytes(backup).SequenceEqual(original), "pre-replacement backup did not preserve exact bytes");

            File.Delete(registry.RegistryPath); // Only the isolated fixture record is removed.
            RequireThrows<InvalidDataException>(() => registry.Read());
            RequireThrows<InvalidDataException>(() => registry.Save(new WorkspaceProjectState(
                layout.FixtureWorkspace, [new WorkspaceProject(layout.FixtureWorkspace, "NeoBabylon")])));
            ProtectedDataRecordFile.RestoreMissingFromBackup(registry.RegistryPath, hash, ValidateRegistryFixture);
            Require(File.ReadAllBytes(registry.RegistryPath).SequenceEqual(original), "verified restore changed original bytes");
            Require(registry.Read().SelectedWorkspace == layout.FixtureWorkspace, "restored registry did not select its original workspace");
        });
    }

    public static void InterruptedReplacementRecoversDeterministically()
    {
        WithRegistry((root, layout, registry) =>
        {
            var project = Path.Combine(root, "project");
            Directory.CreateDirectory(project);
            registry.Save(registry.Read());
            var original = File.ReadAllBytes(registry.RegistryPath);
            registry.AddAndSelect(project);
            var replacement = File.ReadAllBytes(registry.RegistryPath);
            var journalPath = registry.RegistryPath + ".migration.json";
            var journal = JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                product = "NeoBabylon",
                originalSha256 = Digest(original),
                replacementSha256 = Digest(replacement)
            });

            File.WriteAllBytes(registry.RegistryPath, original);
            File.WriteAllText(journalPath, journal, Utf8NoBom);
            Require(registry.Read().SelectedWorkspace == layout.FixtureWorkspace,
                "interrupted pre-commit state did not retain original selection");
            Require(!File.Exists(journalPath), "pre-commit journal was not resolved");

            File.Delete(registry.RegistryPath);
            File.WriteAllText(journalPath, journal, Utf8NoBom);
            Require(registry.Read().SelectedWorkspace == layout.FixtureWorkspace,
                "missing interrupted record was not restored from verified backup");
            Require(File.ReadAllBytes(registry.RegistryPath).SequenceEqual(original),
                "interruption recovery did not restore exact original bytes");

            File.WriteAllBytes(registry.RegistryPath, replacement);
            File.WriteAllText(journalPath, journal, Utf8NoBom);
            Require(registry.Read().SelectedWorkspace == Path.GetFullPath(project),
                "completed replacement was not recognized after interruption");
            Require(!File.Exists(journalPath), "completed replacement journal was not resolved");
        });
    }

    public static void BackupPathObstructionAndFutureSchemaPreserveBytes()
    {
        WithRegistry((root, layout, registry) =>
        {
            var project = Path.Combine(root, "project");
            Directory.CreateDirectory(project);
            registry.Save(registry.Read());
            var original = File.ReadAllBytes(registry.RegistryPath);
            var backupParent = Path.Combine(layout.DataRoot, "Backups");
            Directory.CreateDirectory(backupParent);
            var obstruction = Path.Combine(backupParent, "NeoBabylon");
            File.WriteAllText(obstruction, "fixture obstruction", Encoding.UTF8);
            RequireThrows<IOException>(() => registry.AddAndSelect(project));
            Require(File.ReadAllBytes(registry.RegistryPath).SequenceEqual(original),
                "backup storage failure rewrote the original registry");
            File.Delete(obstruction);

            var journalPath = registry.RegistryPath + ".migration.json";
            File.WriteAllText(journalPath, "{invalid", Utf8NoBom);
            RequireThrows<JsonException>(() => registry.Read());
            Require(File.ReadAllBytes(registry.RegistryPath).SequenceEqual(original),
                "corrupt recovery journal rewrote the original registry");
            File.Delete(journalPath);

            var future = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 999,
                product = "NeoBabylon",
                selectedWorkspace = layout.FixtureWorkspace,
                projects = new[] { new { workspacePath = layout.FixtureWorkspace, name = "NeoBabylon" } }
            });
            File.WriteAllBytes(registry.RegistryPath, future);
            RequireThrows<InvalidDataException>(() => registry.Read());
            RequireThrows<InvalidDataException>(() => registry.Save(new WorkspaceProjectState(
                layout.FixtureWorkspace, [new WorkspaceProject(layout.FixtureWorkspace, "NeoBabylon")])));
            Require(File.ReadAllBytes(registry.RegistryPath).SequenceEqual(future),
                "downgrade attempt rewrote unsupported schema bytes");
        });
    }

    public static void InjectedWriteAndBackupFailuresPreserveBytesAndRecoveryArtifacts()
    {
        foreach (var permissionDenied in new[] { false, true })
        foreach (var checkpoint in new[]
        {
            ProtectedDataWriteCheckpoint.BackupStageWrite,
            ProtectedDataWriteCheckpoint.ReplacementStageWrite,
            ProtectedDataWriteCheckpoint.RecordCommit
        })
        {
            WithRegistry((_, layout, _) =>
            {
                var path = Path.Combine(layout.DataRoot, "NeoBabylon", "fixture-fault-record.json");
                var original = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"product\":\"NeoBabylon\",\"value\":\"old\"}");
                var replacement = Encoding.UTF8.GetBytes("{\"schemaVersion\":2,\"product\":\"NeoBabylon\",\"value\":\"new\"}");
                ProtectedDataRecordFile.Replace(path, original, bytes => ValidateVersion(bytes, 1),
                    bytes => ValidateVersion(bytes, 1));
                var journalPath = path + ".migration.json";
                var backupRoot = Path.Combine(layout.DataRoot, "Backups", "NeoBabylon");
                var injected = false;
                Exception? observed = null;
                try
                {
                    ProtectedDataRecordFile.Replace(path, replacement, bytes => ValidateVersion(bytes, 1),
                        bytes => ValidateVersion(bytes, 2), phase =>
                        {
                            if (phase != checkpoint) return;
                            injected = true;
                            if (permissionDenied) throw new UnauthorizedAccessException("fixture access denied");
                            throw new IOException("fixture disk full", unchecked((int)0x80070070));
                        });
                }
                catch (UnauthorizedAccessException exception) when (permissionDenied) { observed = exception; }
                catch (IOException exception) when (!permissionDenied) { observed = exception; }

                Require(injected && observed is not null, $"{checkpoint}: expected injected storage failure was not observed");
                if (!permissionDenied)
                    Require(observed!.HResult == unchecked((int)0x80070070),
                        $"{checkpoint}: IOException did not model Windows disk full");
                Require(File.ReadAllBytes(path).SequenceEqual(original),
                    $"{checkpoint}: failed replacement changed original bytes");

                var backups = Directory.Exists(backupRoot)
                    ? Directory.EnumerateFiles(backupRoot, Digest(original) + ".bin", SearchOption.AllDirectories).ToArray()
                    : Array.Empty<string>();
                if (checkpoint == ProtectedDataWriteCheckpoint.BackupStageWrite)
                    Require(backups.Length == 0, "failed backup write published a backup");
                else
                    Require(backups.Length == 1 && File.ReadAllBytes(backups[0]).SequenceEqual(original),
                        $"{checkpoint}: verified backup was missing or changed");

                if (checkpoint == ProtectedDataWriteCheckpoint.RecordCommit)
                {
                    using var journal = JsonDocument.Parse(File.ReadAllBytes(journalPath));
                    var entry = journal.RootElement;
                    Require(entry.GetProperty("schemaVersion").GetInt32() == 1
                        && entry.GetProperty("product").GetString() == "NeoBabylon"
                        && entry.GetProperty("originalSha256").GetString() == Digest(original)
                        && entry.GetProperty("replacementSha256").GetString() == Digest(replacement),
                        "commit failure did not retain a valid pending migration journal");
                }
                else Require(!File.Exists(journalPath), $"{checkpoint}: unpublished journal survived failure");

                Require(!Directory.EnumerateFiles(layout.DataRoot, "*.stage-*", SearchOption.AllDirectories).Any(),
                    $"{checkpoint}: incomplete stage was not cleaned up");
                Require(ProtectedDataRecordFile.Read(path, bytes => ValidateVersion(bytes, 1))!
                    .SequenceEqual(original), $"{checkpoint}: recovery reset or changed the original record");
                Require(!File.Exists(journalPath), $"{checkpoint}: recovery did not resolve the journal");
            });
        }
    }

    public static void ForkBookmarksBackUpBeforeReplacement()
    {
        WithRegistry((root, layout, _) =>
        {
            var path = Path.Combine(layout.DataRoot, "NeoBabylon", "fork-bookmarks.json");
            var workspace = layout.FixtureWorkspace;
            ForkBookmarkStore.Record(path, new ForkBookmark(
                "fork-1", "parent-1", "lmstudio", "fixture-model", workspace,
                DateTimeOffset.Parse("2026-09-26T08:00:00Z")));
            var original = File.ReadAllBytes(path);
            ForkBookmarkStore.Record(path, new ForkBookmark(
                "fork-2", "parent-2", "lmstudio", "fixture-model", workspace,
                DateTimeOffset.Parse("2026-09-26T09:00:00Z")));
            var backupRoot = Path.Combine(layout.DataRoot, "Backups", "NeoBabylon");
            var backup = Directory.EnumerateFiles(backupRoot, Digest(original) + ".bin", SearchOption.AllDirectories).Single();
            Require(File.ReadAllBytes(backup).SequenceEqual(original),
                "fork bookmark replacement did not verify an exact pre-replacement backup");
            Require(ForkBookmarkStore.Read(path).Count == 2, "fork bookmark replacement lost an existing record");
        });
    }

    public static void ExplicitVersionTransitionPreservesSourceBytes()
    {
        WithRegistry((root, layout, _) =>
        {
            var path = Path.Combine(layout.DataRoot, "NeoBabylon", "fixture-versioned-record.json");
            var versionOne = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"product\":\"NeoBabylon\",\"value\":\"old\"}");
            var versionTwo = Encoding.UTF8.GetBytes("{\"schemaVersion\":2,\"product\":\"NeoBabylon\",\"value\":\"new\"}");
            ProtectedDataRecordFile.Replace(path, versionOne, bytes => ValidateVersion(bytes, 1),
                bytes => ValidateVersion(bytes, 1));
            ProtectedDataRecordFile.Replace(path, versionTwo, bytes => ValidateVersion(bytes, 1),
                bytes => ValidateVersion(bytes, 2));
            Require(File.ReadAllBytes(path).SequenceEqual(versionTwo), "explicit schema transition did not publish the target bytes");
            var backupRoot = Path.Combine(layout.DataRoot, "Backups", "NeoBabylon");
            var backup = Directory.EnumerateFiles(backupRoot, Digest(versionOne) + ".bin", SearchOption.AllDirectories).Single();
            Require(File.ReadAllBytes(backup).SequenceEqual(versionOne),
                "explicit schema transition did not retain exact pre-migration bytes");
        });
    }

    private static void WithRegistry(Action<string, ApplicationRootLayout, WorkspaceProjectRegistry> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-P5-02-Tests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var application = Path.Combine(root, "app");
        Directory.CreateDirectory(source);
        try
        {
            var layout = ApplicationRootLayout.Create(source, application);
            check(root, layout, new WorkspaceProjectRegistry(layout.DataRoot, layout.FixtureWorkspace));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void ValidateRegistryFixture(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        Require(document.RootElement.GetProperty("schemaVersion").GetInt32() == 1
            && document.RootElement.GetProperty("product").GetString() == "NeoBabylon",
            "backup did not contain a supported NeoBabylon registry");
    }

    private static void ValidateVersion(byte[] bytes, int expected)
    {
        using var document = JsonDocument.Parse(bytes);
        Require(document.RootElement.GetProperty("schemaVersion").GetInt32() == expected
            && document.RootElement.GetProperty("product").GetString() == "NeoBabylon",
            "versioned fixture record has the wrong schema or product identity");
    }

    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RequireThrows<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
