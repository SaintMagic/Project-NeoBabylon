using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NeoBabylon.Core;
using NeoBabylon.Host;

internal static class GeneratedToolCandidateChecks
{
    private const string Unverified = "unverified-declaration";
    private static readonly JsonSerializerOptions ManifestJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static void RejectsCredentialAssignmentsOnCreateAndRead()
    {
        foreach (var source in new[]
        {
            "{\"password\":\"hunter2\"}",
            "const apiKey = \"hunter2\";",
            "const clientSecret = 'hunter2';",
            "const headers = { [\"Authorization\"]: \"Bearer \" + \"abc123456789\" };"
        })
        {
            WithFixture(applicationRoot =>
            {
                var files = Files(source);
                var manifest = Manifest(applicationRoot, files);
                RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.Create(applicationRoot, manifest, files));
                WriteCandidate(manifest, files);
                RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.Read(applicationRoot, manifest.ToolId));
            });
        }

        WithFixture(applicationRoot =>
        {
            var files = Files();
            var baseline = Manifest(applicationRoot, files);
            var credentialInManifest = baseline with { ProposedBehavior = "{\"password\":\"hunter2\"}" };
            RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.Create(applicationRoot, credentialInManifest, files));
            WriteCandidate(credentialInManifest, files);
            RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.Read(applicationRoot, credentialInManifest.ToolId));
        });

        WithFixture(applicationRoot =>
        {
            var files = Files();
            var baseline = Manifest(applicationRoot, files);
            var credentialInManifest = baseline with
            {
                ProposedBehavior = "headers = { ['Authorization']: 'Bearer ' + 'abc123456789' };"
            };
            RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.Create(applicationRoot, credentialInManifest, files));
            WriteCandidate(credentialInManifest, files);
            RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.Read(applicationRoot, credentialInManifest.ToolId));
        });
    }

    public static void RejectsHardLinkedCandidateFilesOnRead()
    {
        foreach (var relative in new[] { "tool.js", "tool.json" })
        {
            WithFixture(applicationRoot =>
            {
                var files = Files();
                var manifest = Manifest(applicationRoot, files);
                WriteCandidate(manifest, files);
                var candidateFile = Path.Combine(manifest.CandidatePath, relative);
                var externalFile = Path.Combine(Path.GetDirectoryName(applicationRoot)!, "outside-" + relative);
                File.Move(candidateFile, externalFile);
                if (!CreateHardLink(candidateFile, externalFile, IntPtr.Zero))
                {
                    throw new InvalidOperationException("Hard-link fixture could not be established.");
                }
                RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.Read(applicationRoot, manifest.ToolId));
                RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.List(applicationRoot));
            });
        }
    }

    public static void RejectsUnboundedCandidateListing()
    {
        WithFixture(applicationRoot =>
        {
            var files = Files();
            for (var index = 0; index < 65; index++)
            {
                var manifest = Manifest(applicationRoot, files, $"fixture-{index}");
                WriteCandidate(manifest, files);
            }
            RequireThrows<GeneratedToolCandidateListLimitException>(() => GeneratedToolCandidateStore.List(applicationRoot));
            Require(GeneratedToolCandidateStore.Read(applicationRoot, "fixture-0") is not null,
                "listing limit incorrectly disabled a valid individual read");
        });

        WithFixture(applicationRoot =>
        {
            for (var index = 0; index < 5; index++)
            {
                var files = Files();
                for (var extra = 0; extra < 100; extra++)
                {
                    files[$"extra/{extra:D3}.txt"] = Encoding.UTF8.GetBytes("Creator-supplied fixture text.");
                }
                WriteCandidate(Manifest(applicationRoot, files, $"fixture-{index}"), files);
            }
            RequireThrows<GeneratedToolCandidateListLimitException>(() => GeneratedToolCandidateStore.List(applicationRoot));
        });
    }

    public static void ReadsOnlyBoundedInventoriedFilePages()
    {
        WithFixture(applicationRoot =>
        {
            var files = Files(new string('a', 32767) + "😀tail");
            var manifest = Manifest(applicationRoot, files);
            var candidate = GeneratedToolCandidateStore.Create(applicationRoot, manifest, files);

            var first = GeneratedToolCandidateStore.ReadFileRange(applicationRoot, manifest.ToolId,
                candidate.ContentIdentity, "tool.js", 0);
            Require(first.Text == new string('a', 32767) && first.Total == 32773
                && first.Next == 32767 && first.HasMore, "first page split a surrogate pair or returned wrong bounds");

            var last = GeneratedToolCandidateStore.ReadFileRange(applicationRoot, manifest.ToolId,
                candidate.ContentIdentity, "tool.js", first.Next);
            Require(last.Text == "😀tail" && last.Total == 32773 && last.Next == 32773
                && !last.HasMore, "last page did not preserve UTF-8-decoded text and position");

            var end = GeneratedToolCandidateStore.ReadFileRange(applicationRoot, manifest.ToolId,
                candidate.ContentIdentity, "tool.js", last.Next);
            Require(end.Text == "" && end.Total == 32773 && end.Next == 32773 && !end.HasMore,
                "end-of-file page was not empty and terminal");

            foreach (var offset in new[] { -1, 32768, 32774 })
            {
                RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.ReadFileRange(
                    applicationRoot, manifest.ToolId, candidate.ContentIdentity, "tool.js", offset));
            }
        });
    }

    public static void HostListingProjectsValidatedContractAsInertStrings()
    {
        WithFixture(applicationRoot =>
        {
            var files = Files();
            var manifest = Manifest(applicationRoot, files) with
            {
                Contract = new GeneratedToolContract(
                    "manual-only proposed invocation", "{\"type\":\"object\"}", "{\"type\":\"string\"}")
            };
            var candidate = GeneratedToolCandidateStore.Create(applicationRoot, manifest, files);
            var listing = GeneratedToolCandidateHostProjection.List(applicationRoot);
            var row = (listing["candidates"] as JsonArray)?[0] as JsonObject
                ?? throw new InvalidOperationException("Host listing omitted the validated candidate.");
            var contract = row["contract"] as JsonObject
                ?? throw new InvalidOperationException("Host listing omitted the content-bound contract.");
            Require(listing["attributedTo"]?.GetValue<string>() == "NeoBabylon.Host"
                && row["contentIdentity"]?.GetValue<string>() == candidate.ContentIdentity
                && row["state"]?.GetValue<string>() == "unapproved",
                "host listing lost its validated candidate identity or attribution");
            Require(contract.Count == 3
                && contract["invocation"]?.GetValue<string>() == "manual-only proposed invocation"
                && contract["inputSchema"]?.GetValue<string>() == "{\"type\":\"object\"}"
                && contract["outputSchema"]?.GetValue<string>() == "{\"type\":\"string\"}",
                "host listing did not preserve the bounded contract as three inert strings");
        });
    }

    public static void RejectsStaleIdentityAndUninventoriedOrChangedFiles()
    {
        WithFixture(applicationRoot =>
        {
            var files = Files();
            var manifest = Manifest(applicationRoot, files);
            var candidate = GeneratedToolCandidateStore.Create(applicationRoot, manifest, files);
            foreach (var path in new[]
            {
                "tool.json", "unlisted.txt", "TOOL.JS", "../outside.txt",
                "evidence/../tool.js", "C:\\outside\\tool.js"
            })
            {
                RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.ReadFileRange(
                    applicationRoot, manifest.ToolId, candidate.ContentIdentity, path, 0));
            }

            var changedManifest = manifest with { Revision = "revision-2" };
            WriteCandidate(changedManifest, files);
            RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.ReadFileRange(
                applicationRoot, manifest.ToolId, candidate.ContentIdentity, "tool.js", 0));

            var changedCandidate = GeneratedToolCandidateStore.Read(applicationRoot, manifest.ToolId)!;
            File.WriteAllText(Path.Combine(manifest.CandidatePath, "tool.js"), "changed without manifest update");
            RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.ReadFileRange(
                applicationRoot, manifest.ToolId, changedCandidate.ContentIdentity, "tool.js", 0));
        });
    }

    public static void RejectsHardLinkedFileInspection()
    {
        WithFixture(applicationRoot =>
        {
            var files = Files();
            var manifest = Manifest(applicationRoot, files);
            var candidate = GeneratedToolCandidateStore.Create(applicationRoot, manifest, files);
            var candidateFile = Path.Combine(manifest.CandidatePath, "tool.js");
            var externalFile = Path.Combine(Path.GetDirectoryName(applicationRoot)!, "outside-tool.js");
            File.Move(candidateFile, externalFile);
            if (!CreateHardLink(candidateFile, externalFile, IntPtr.Zero))
            {
                throw new InvalidOperationException("Hard-link fixture could not be established.");
            }
            RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.ReadFileRange(
                applicationRoot, manifest.ToolId, candidate.ContentIdentity, "tool.js", 0));
        });
    }

    public static void RejectsReparseFileInspection()
    {
        WithFixture(applicationRoot =>
        {
            var files = Files();
            var manifest = Manifest(applicationRoot, files);
            var candidate = GeneratedToolCandidateStore.Create(applicationRoot, manifest, files);
            var candidateFile = Path.Combine(manifest.CandidatePath, "tool.js");
            var externalFile = Path.Combine(Path.GetDirectoryName(applicationRoot)!, "outside-tool.js");
            File.Move(candidateFile, externalFile);
            File.CreateSymbolicLink(candidateFile, externalFile);
            Require((File.GetAttributes(candidateFile) & FileAttributes.ReparsePoint) != 0,
                "symbolic-link fixture was not a reparse point");
            RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.ReadFileRange(
                applicationRoot, manifest.ToolId, candidate.ContentIdentity, "tool.js", 0));
        });
    }

    public static void RejectsOversizedFileInspection()
    {
        WithFixture(applicationRoot =>
        {
            var files = Files();
            var manifest = Manifest(applicationRoot, files);
            var candidate = GeneratedToolCandidateStore.Create(applicationRoot, manifest, files);
            File.WriteAllBytes(Path.Combine(manifest.CandidatePath, "tool.js"), new byte[1024 * 1024 + 1]);
            RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.ReadFileRange(
                applicationRoot, manifest.ToolId, candidate.ContentIdentity, "tool.js", 0));
        });
    }

    public static void RequiresFourDistinctHashedDeclarationsOnCreateAndRead()
    {
        foreach (var missingKind in new[]
        {
            "existing-tool-search", "composition-attempt", "current-task-authority", "capability-gap"
        })
        {
            WithFixture(applicationRoot =>
            {
                var files = Files();
                var baseline = Manifest(applicationRoot, files);
                var invalid = baseline with { Evidence = baseline.Evidence.Where(item => item.Kind != missingKind).ToArray() };
                RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.Create(applicationRoot, invalid, files));
                WriteCandidate(invalid, files);
                RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.Read(applicationRoot, invalid.ToolId));
            });
        }

        WithFixture(applicationRoot =>
        {
            var files = Files();
            var baseline = Manifest(applicationRoot, files);
            var unboundGap = baseline with { Origin = baseline.Origin with { GapEvidenceId = "search-1" } };
            RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.Create(applicationRoot, unboundGap, files));
            WriteCandidate(unboundGap, files);
            RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.Read(applicationRoot, unboundGap.ToolId));
        });
        WithFixture(applicationRoot =>
        {
            var files = Files();
            var baseline = Manifest(applicationRoot, files);
            var searchHash = baseline.Evidence.Single(item => item.Kind == "existing-tool-search").Sha256;
            var unboundAuthority = baseline with { Authority = baseline.Authority with { EvidenceSha256 = searchHash } };
            RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.Create(applicationRoot, unboundAuthority, files));
            WriteCandidate(unboundAuthority, files);
            RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.Read(applicationRoot, unboundAuthority.ToolId));
        });
        WithFixture(applicationRoot =>
        {
            var files = Files();
            var baseline = Manifest(applicationRoot, files);
            var unhashedSearch = baseline with
            {
                Evidence = baseline.Evidence.Select(item => item.Kind == "existing-tool-search"
                    ? item with { Path = "", Sha256 = "" } : item).ToArray()
            };
            RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.Create(applicationRoot, unhashedSearch, files));
            WriteCandidate(unhashedSearch, files);
            RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.Read(applicationRoot, unhashedSearch.ToolId));
        });
    }

    public static void KeepsEvidenceDeclarativeAndCandidateUnapproved()
    {
        WithFixture(applicationRoot =>
        {
            var files = Files();
            var baseline = Manifest(applicationRoot, files);
            var claimedVerification = baseline with
            {
                Evidence = baseline.Evidence.Select(item => item.Kind == "capability-gap"
                    ? item with { Outcome = "verified" } : item).ToArray()
            };
            RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.Create(applicationRoot, claimedVerification, files));
            WriteCandidate(claimedVerification, files);
            RequireThrows<InvalidDataException>(() => GeneratedToolCandidateStore.Read(applicationRoot, claimedVerification.ToolId));
        });

        WithFixture(applicationRoot =>
        {
            var files = Files();
            var manifest = Manifest(applicationRoot, files);
            var created = GeneratedToolCandidateStore.Create(applicationRoot, manifest, files);
            Require(created.State == "unapproved", "candidate creation changed its review state");
            var reopened = GeneratedToolCandidateStore.Read(applicationRoot, manifest.ToolId);
            Require(reopened is not null && reopened.State == "unapproved", "candidate did not reopen as unapproved");
            Require(reopened!.Manifest.Evidence.All(item => item.Outcome == Unverified),
                "reopened candidate evidence claimed independent verification");
        });
    }

    private static Dictionary<string, ReadOnlyMemory<byte>> Files(string source = "export function invoke() { return 'fixture'; }") =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["tool.js"] = Encoding.UTF8.GetBytes(source),
            ["evidence/search.txt"] = Encoding.UTF8.GetBytes("Creator declares existing-tool search was attempted."),
            ["evidence/composition.txt"] = Encoding.UTF8.GetBytes("Creator declares composition was attempted."),
            ["evidence/authority.txt"] = Encoding.UTF8.GetBytes("Creator declares selected task authority."),
            ["evidence/gap.txt"] = Encoding.UTF8.GetBytes("Creator declares a remaining capability gap.")
        };

    private static GeneratedToolCandidateManifest Manifest(string applicationRoot,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> files, string toolId = "fixture-tool")
    {
        GeneratedToolEvidence Evidence(string kind, string id, string path) =>
            new(kind, id, Unverified, "Creator declaration; not independently verified", path, Hash(files[path].ToArray()));

        var evidence = new[]
        {
            Evidence("existing-tool-search", "search-1", "evidence/search.txt"),
            Evidence("composition-attempt", "composition-1", "evidence/composition.txt"),
            Evidence("current-task-authority", "authority-1", "evidence/authority.txt"),
            Evidence("capability-gap", "gap-1", "evidence/gap.txt")
        };
        return new GeneratedToolCandidateManifest(
            1, "NeoBabylon", toolId,
            Path.Combine(applicationRoot, "Data", "GeneratedTools", "Candidates", toolId),
            "revision-1", "Fixture tool", "Exercise candidate validation", "Return a fixture value",
            "Creator-declared missing capability",
            new GeneratedToolOrigin("task-1", "session-1", DateTimeOffset.Parse("2026-09-27T00:00:00Z"),
                "fixture creator", "fixture provider", "fixture model", "cap-v1:sha256:" + Hash("capability"), "gap-1"),
            "tool.js", new GeneratedToolContract("manual only", "{}", "{}"),
            new GeneratedToolAuthority(["read-only"], ["fixture workspace"], [], ["fixture input only"],
                "deny", "read-only", "never", Hash(files["evidence/authority.txt"].ToArray())),
            [], evidence,
            files.Select(item => new GeneratedToolFile(item.Key, Hash(item.Value.ToArray()))).ToArray(),
            "unapproved");
    }

    private static void WriteCandidate(GeneratedToolCandidateManifest manifest,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> files)
    {
        Directory.CreateDirectory(manifest.CandidatePath);
        foreach (var (path, content) in files)
        {
            var target = Path.Combine(manifest.CandidatePath, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, content.ToArray());
        }
        File.WriteAllBytes(Path.Combine(manifest.CandidatePath, "tool.json"),
            JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestJson));
    }

    private static void WithFixture(Action<string> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Task8-Tests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var application = Path.Combine(root, "app");
        Directory.CreateDirectory(source);
        try
        {
            ApplicationRootLayout.Create(source, application);
            check(application);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string newFileName, string existingFileName, IntPtr reserved);

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

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
