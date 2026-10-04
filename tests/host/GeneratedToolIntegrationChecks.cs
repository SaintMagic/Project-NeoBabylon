using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using NeoBabylon.Core;
using NeoBabylon.Host;

// Focused generated-tool binding and disabled-config checks; execution is deferred.
internal static class GeneratedToolIntegrationChecks
{
    public static void ExplicitStageUsesOnlyCurrentBindingAndKeepsTheAdapterDisabled()
    {
        WithCandidate((applicationRoot, candidate) =>
        {
            var sourceRoot = Path.Combine(Path.GetDirectoryName(applicationRoot)!, "source");
            var runtimeDirectory = Path.Combine(sourceRoot, "runtime");
            Directory.CreateDirectory(runtimeDirectory);
            var fakeBinary = Path.Combine(runtimeDirectory, "app-server.exe");
            File.WriteAllBytes(fakeBinary, [1, 2, 3, 4]);
            var runtimeHash = Hash(File.ReadAllBytes(fakeBinary));
            File.WriteAllText(Path.Combine(runtimeDirectory, "runtime-lock.json"),
                JsonSerializer.Serialize(new RuntimeLockFile(1, "NeoBabylon",
                    new RuntimeLockRuntime("Codex App Server", "test", "fixture", "test",
                        new string('a', 40), ".", "runtime/app-server.exe", "test", runtimeHash,
                        "app-server-v2", "fixture"))));

            var review = GeneratedToolReviewStore.RecordReview(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, "Reviewed fixture.");
            var prepared = GeneratedToolIntegrationStore.PrepareDisabledBinding(applicationRoot,
                candidate.Manifest.ToolId, candidate.ContentIdentity, review.ReviewIdentity,
                "Prepare disabled binding.");
            var adapter = Path.Combine(applicationRoot, "Adapters", "NeoBabylon.GeneratedToolMcp.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(adapter)!);
            var capability = ModelCapabilityRecord.CreateUnknown(
                "lmstudio", "http://127.0.0.1:1234/v1", "fixture-model");
            var configPath = Path.Combine(applicationRoot, "Data", "CodexHome", "config.toml");
            var supervisor = new RuntimeSupervisor(sourceRoot, applicationRoot);
            try
            {
                RequireThrows<GeneratedToolStageException>(() => supervisor.StageGeneratedToolDisabledMcp(
                    capability, candidate.Manifest.ToolId, candidate.ContentIdentity,
                    review.ReviewIdentity, new string('0', 64)));
                Require(!File.Exists(configPath), "stale stage wrote isolated config");
                RequireThrows<GeneratedToolStageException>(() => supervisor.StageGeneratedToolDisabledMcp(
                    capability, candidate.Manifest.ToolId, candidate.ContentIdentity,
                    review.ReviewIdentity, prepared.RecordSha256));
                Require(!File.Exists(configPath), "missing adapter published isolated config");
                File.WriteAllBytes(adapter, [5, 6, 7, 8]);
                var staged = supervisor.StageGeneratedToolDisabledMcp(capability,
                    candidate.Manifest.ToolId, candidate.ContentIdentity, review.ReviewIdentity,
                    prepared.RecordSha256);
                Require(staged["runtimeConfirmation"]?.GetValue<string>() == "Unknown"
                    && staged["state"]?.GetValue<string>() == "staged-disabled",
                    "disk staging claimed runtime confirmation or callable activation");
                var config = File.ReadAllText(configPath);
                Require(config.Contains("[mcp_servers.neobabylon_generated_tools]", StringComparison.Ordinal)
                    && config.Contains("enabled = false", StringComparison.Ordinal)
                    && config.Contains("enabled_tools = []", StringComparison.Ordinal)
                    && config.Contains(adapter.Replace('\\', '/'), StringComparison.Ordinal),
                    "stage did not publish the fixed disabled product adapter");
                RequireThrows<GeneratedToolStageException>(() => supervisor.StageGeneratedToolDisabledMcp(
                    capability, candidate.Manifest.ToolId, candidate.ContentIdentity,
                    review.ReviewIdentity, prepared.RecordSha256));
            }
            finally
            {
                supervisor.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        });
    }

    public static void DisabledMcpConfigIsExplicitAndBoundToCurrentPreparation()
    {
        WithCandidate((applicationRoot, candidate) =>
        {
            var capability = ModelCapabilityRecord.CreateUnknown(
                "lmstudio", "http://127.0.0.1:1234/v1", "fixture-model");
            var unchanged = CodexConfigBuilder.Build(capability);
            Require(!unchanged.Contains("[mcp_servers.", StringComparison.Ordinal),
                "default isolated config gained an MCP server");

            var review = GeneratedToolReviewStore.RecordReview(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, "Reviewed fixture.");
            var prepared = GeneratedToolIntegrationStore.PrepareDisabledBinding(applicationRoot,
                candidate.Manifest.ToolId, candidate.ContentIdentity, review.ReviewIdentity,
                "Prepare disabled binding.");
            var adapter = Path.Combine(applicationRoot, "Adapters", "NeoBabylon MCP.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(adapter)!);
            File.WriteAllBytes(adapter, [1, 2, 3]);
            var option = new DisabledProductMcpConfig(applicationRoot, prepared, adapter);

            var config = CodexConfigBuilder.Build(capability, disabledProductMcp: option);
            Require(config.Contains("[mcp_servers.neobabylon_generated_tools]", StringComparison.Ordinal)
                && config.Contains($"command = \"{adapter.Replace('\\', '/')}\"", StringComparison.Ordinal)
                && config.Contains("enabled = false", StringComparison.Ordinal)
                && config.Contains("enabled_tools = []", StringComparison.Ordinal)
                && config.Contains("startup_timeout_sec = 10", StringComparison.Ordinal)
                && config.Contains("tool_timeout_sec = 30", StringComparison.Ordinal),
                "explicit MCP stanza is not disabled and bounded");
            Require(config.Split("[mcp_servers.", StringSplitOptions.None).Length == 2,
                "more than one product MCP server was emitted");
            Require(!config.Contains("[mcp_servers.neobabylon_generated_tools.env]", StringComparison.Ordinal)
                && !config.Contains("env_vars", StringComparison.Ordinal),
                "MCP stanza gained credential environment configuration");
            Require(config.StartsWith(unchanged, StringComparison.Ordinal),
                "explicit option modified the existing isolated config content");

            var codexHome = Path.Combine(applicationRoot, "Data", "ConfigFixture");
            CodexConfigBuilder.WriteIsolated(codexHome, capability, disabledProductMcp: option);
            Require(File.ReadAllText(Path.Combine(codexHome, "config.toml")) ==
                CodexConfigBuilder.Build(capability, Path.Combine(codexHome, "model-catalog.json"),
                    disabledProductMcp: option), "isolated write dropped the explicit disabled option");

            RequireThrows<InvalidDataException>(() => CodexConfigBuilder.Build(capability,
                disabledProductMcp: option with { AdapterPath = Path.Combine(Path.GetTempPath(), "outside.exe") }));
            RequireThrows<InvalidDataException>(() => CodexConfigBuilder.Build(capability,
                disabledProductMcp: option with { AdapterPath = "relative.exe" }));
            RequireThrows<InvalidDataException>(() => CodexConfigBuilder.Build(capability,
                disabledProductMcp: option with { Binding = prepared with { RecordSha256 = new string('0', 64) } }));
            var failedHome = Path.Combine(applicationRoot, "Data", "FailedConfigFixture");
            RequireThrows<InvalidDataException>(() => CodexConfigBuilder.WriteIsolated(failedHome, capability,
                disabledProductMcp: option with { Binding = prepared with { RecordSha256 = new string('0', 64) } }));
            Require(!Directory.Exists(failedHome), "invalid opt-in binding created isolated config output");

            GeneratedToolIntegrationStore.Revoke(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, review.ReviewIdentity, prepared.RecordSha256, "Revoke fixture.");
            RequireThrows<InvalidDataException>(() => CodexConfigBuilder.Build(capability,
                disabledProductMcp: option));
            Require(CodexConfigBuilder.Build(capability) == unchanged,
                "default config changed after a rejected explicit option");
        });
    }

    public static void BindingIdsRemainDiscoverableWhenCandidateBytesAreInvalid()
    {
        WithCandidate((applicationRoot, candidate) =>
        {
            var review = GeneratedToolReviewStore.RecordReview(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, "Reviewed fixture.");
            var prepared = GeneratedToolIntegrationStore.PrepareDisabledBinding(applicationRoot,
                candidate.Manifest.ToolId, candidate.ContentIdentity, review.ReviewIdentity,
                "Prepare disabled binding.");
            File.WriteAllText(Path.Combine(candidate.CandidatePath, "tool.js"), "corrupt candidate");
            Require(GeneratedToolIntegrationStore.ListToolIds(applicationRoot)
                .SequenceEqual([candidate.Manifest.ToolId]),
                "binding discovery depended on valid candidate bytes or exposed staging");
            Require(GeneratedToolIntegrationStore.History(applicationRoot, candidate.Manifest.ToolId)
                .SequenceEqual([prepared]), "binding history was unavailable after candidate corruption");
            var revoked = GeneratedToolIntegrationStore.Revoke(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, review.ReviewIdentity, prepared.RecordSha256,
                "Deny changed candidate.");
            GeneratedToolIntegrationStore.Cleanup(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, review.ReviewIdentity, revoked.RecordSha256,
                "Tombstone changed candidate.");
            Require(GeneratedToolIntegrationStore.ListToolIds(applicationRoot)
                .SequenceEqual([candidate.Manifest.ToolId]),
                "denial history vanished from recovery discovery");
            Directory.Delete(candidate.CandidatePath, recursive: true);
            Require(GeneratedToolIntegrationStore.ListToolIds(applicationRoot)
                .SequenceEqual([candidate.Manifest.ToolId]),
                "binding discovery depended on a candidate directory existing");
            Require(GeneratedToolIntegrationStore.History(applicationRoot, candidate.Manifest.ToolId).Count == 3,
                "binding history depended on a candidate directory existing");
            var invalidEntry = Path.Combine(applicationRoot, "Data", "GeneratedTools",
                "PreparedBindings", "invalid id");
            Directory.CreateDirectory(invalidEntry);
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.ListToolIds(applicationRoot));
        });
    }

    public static void PreparationBindsCurrentReviewWithoutMakingTheCandidateCallable()
    {
        WithCandidate((applicationRoot, candidate) =>
        {
            var review = GeneratedToolReviewStore.RecordReview(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, "Reviewed the fixture envelope.");
            var prepared = GeneratedToolIntegrationStore.PrepareDisabledBinding(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, review.ReviewIdentity, "Prepare an inert local binding.");
            Require(prepared.State == "prepared-disabled" && prepared.ActivationState == "disabled"
                && prepared.CallableRoute == "none", "preparation gained a callable route");
            Require(prepared.CandidateContentIdentity == candidate.ContentIdentity
                && prepared.ReviewIdentity == review.ReviewIdentity
                && prepared.ReviewInterfaceVersion == review.ReviewInterfaceVersion
                && prepared.ReviewRecordSha256 == review.RecordSha256,
                "preparation was not bound to the exact candidate and review");
            Require(GeneratedToolIntegrationStore.ReadCurrent(applicationRoot, candidate.Manifest.ToolId) == prepared,
                "prepared-disabled state did not survive a fresh read");
            Require(GeneratedToolCandidateStore.Read(applicationRoot, candidate.Manifest.ToolId)?.State == "unapproved",
                "preparation changed candidate approval state");
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.PrepareDisabledBinding(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, review.ReviewIdentity,
                "Duplicate preparation."));
        });
    }

    public static void PreparationRejectsStaleRejectedAndCorruptReviewEvidence()
    {
        WithCandidate((applicationRoot, candidate) =>
        {
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.PrepareDisabledBinding(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity,
                "review-v1:sha256:" + new string('0', 64), "No review exists."));
        });

        WithCandidate((applicationRoot, candidate) =>
        {
            var rejected = GeneratedToolReviewStore.RecordRejection(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, "Rejected fixture.");
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.PrepareDisabledBinding(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, rejected.ReviewIdentity,
                "Must not prepare rejection."));
            Require(GeneratedToolIntegrationStore.History(applicationRoot, candidate.Manifest.ToolId).Count == 0,
                "rejected preparation wrote a binding");
        });

        WithCandidate((applicationRoot, candidate) =>
        {
            var review = GeneratedToolReviewStore.RecordReview(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, "Reviewed fixture.");
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.PrepareDisabledBinding(
                applicationRoot, candidate.Manifest.ToolId,
                "candidate-v1:sha256:" + new string('0', 64), review.ReviewIdentity, "Stale candidate."));
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.PrepareDisabledBinding(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity,
                "review-v1:sha256:" + new string('0', 64), "Stale review."));
            File.WriteAllText(Path.Combine(candidate.CandidatePath, "tool.js"), "changed without review");
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.PrepareDisabledBinding(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, review.ReviewIdentity,
                "Changed bytes."));
            Require(GeneratedToolIntegrationStore.History(applicationRoot, candidate.Manifest.ToolId).Count == 0,
                "stale preparation wrote a binding");
        });

        WithCandidate((applicationRoot, candidate) =>
        {
            var review = GeneratedToolReviewStore.RecordReview(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, "Reviewed fixture.");
            var reviewPath = Path.Combine(applicationRoot, "Data", "GeneratedTools", "Reviews",
                candidate.Manifest.ToolId, "0000000001.json");
            File.AppendAllText(reviewPath, "corrupt");
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.PrepareDisabledBinding(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, review.ReviewIdentity,
                "Corrupt review."));
            Require(GeneratedToolIntegrationStore.History(applicationRoot, candidate.Manifest.ToolId).Count == 0,
                "corrupt review evidence was bypassed");
        });
    }

    public static void RevocationAndCleanupAreDurableDenialsWithoutDeletingEvidence()
    {
        WithCandidate((applicationRoot, candidate) =>
        {
            var review = GeneratedToolReviewStore.RecordReview(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, "Reviewed fixture.");
            var prepared = GeneratedToolIntegrationStore.PrepareDisabledBinding(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, review.ReviewIdentity, "Prepare disabled binding.");
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.Cleanup(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, review.ReviewIdentity,
                prepared.RecordSha256, "Cleanup needs revocation."));
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.Revoke(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, review.ReviewIdentity,
                new string('0', 64), "Stale binding."));
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.Revoke(
                applicationRoot, candidate.Manifest.ToolId,
                "candidate-v1:sha256:" + new string('0', 64), review.ReviewIdentity,
                prepared.RecordSha256, "Wrong candidate identity."));
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.Revoke(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity,
                "review-v1:sha256:" + new string('0', 64),
                prepared.RecordSha256, "Wrong review identity."));

            var entryPoint = Path.Combine(candidate.CandidatePath, "tool.js");
            var originalBytes = File.ReadAllBytes(entryPoint);
            File.WriteAllText(entryPoint, "changed after preparation");
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.ReadCurrent(
                applicationRoot, candidate.Manifest.ToolId));
            var revoked = GeneratedToolIntegrationStore.Revoke(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, review.ReviewIdentity, prepared.RecordSha256,
                "Deny this binding despite changed candidate bytes.");
            Require(revoked.State == "revoked" && revoked.ActivationState == "disabled"
                && revoked.CallableRoute == "none"
                && revoked.PreviousRecordSha256 == prepared.RecordSha256,
                "revocation did not append a durable denial");
            Require(GeneratedToolIntegrationStore.ReadCurrent(applicationRoot, candidate.Manifest.ToolId) == revoked,
                "revocation needed valid candidate bytes to remain visible");
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.Revoke(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, review.ReviewIdentity,
                prepared.RecordSha256, "Duplicate revocation."));
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.Cleanup(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, review.ReviewIdentity,
                prepared.RecordSha256, "Stale cleanup."));

            var cleaned = GeneratedToolIntegrationStore.Cleanup(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, review.ReviewIdentity, revoked.RecordSha256,
                "Tombstone the local binding only.");
            Require(cleaned.State == "cleaned" && cleaned.ActivationState == "disabled"
                && cleaned.CallableRoute == "none"
                && cleaned.PreviousRecordSha256 == revoked.RecordSha256,
                "cleanup did not append a disabled tombstone");
            Require(GeneratedToolIntegrationStore.History(applicationRoot, candidate.Manifest.ToolId)
                .Select(item => item.State).SequenceEqual(["prepared-disabled", "revoked", "cleaned"]),
                "binding history lost a transition");
            Require(File.Exists(Path.Combine(candidate.CandidatePath, "tool.js"))
                && GeneratedToolReviewStore.History(applicationRoot, candidate.Manifest.ToolId).SequenceEqual([review]),
                "cleanup deleted candidate or review evidence");
            Require(File.ReadAllText(entryPoint) == "changed after preparation",
                "cleanup rewrote candidate evidence");
            File.WriteAllBytes(entryPoint, originalBytes);
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.PrepareDisabledBinding(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, review.ReviewIdentity,
                "Do not reuse a revoked identity."));
        });
    }

    public static void CorruptBindingHistoryFailsClosed()
    {
        WithCandidate((applicationRoot, candidate) =>
        {
            var review = GeneratedToolReviewStore.RecordReview(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, "Reviewed fixture.");
            var prepared = GeneratedToolIntegrationStore.PrepareDisabledBinding(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, review.ReviewIdentity, "Prepare disabled binding.");
            var recordPath = Path.Combine(applicationRoot, "Data", "GeneratedTools", "PreparedBindings",
                candidate.Manifest.ToolId, "0000000001.json");
            File.AppendAllText(recordPath, "corrupt");
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.ReadCurrent(
                applicationRoot, candidate.Manifest.ToolId));
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.Revoke(
                applicationRoot, candidate.Manifest.ToolId, candidate.ContentIdentity, review.ReviewIdentity,
                prepared.RecordSha256, "Do not skip corrupt history."));
            Require(File.Exists(recordPath), "corrupt binding evidence was erased");
        });
    }

    public static void HardLinkedBindingHistoryFailsClosed()
    {
        WithCandidate((applicationRoot, candidate) =>
        {
            var review = GeneratedToolReviewStore.RecordReview(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, "Reviewed fixture.");
            GeneratedToolIntegrationStore.PrepareDisabledBinding(applicationRoot, candidate.Manifest.ToolId,
                candidate.ContentIdentity, review.ReviewIdentity, "Prepare disabled binding.");
            var recordPath = Path.Combine(applicationRoot, "Data", "GeneratedTools", "PreparedBindings",
                candidate.Manifest.ToolId, "0000000001.json");
            var outside = Path.Combine(Path.GetDirectoryName(applicationRoot)!, "outside-binding.json");
            File.Move(recordPath, outside);
            if (!CreateHardLink(recordPath, outside, IntPtr.Zero))
            {
                throw new InvalidOperationException("Hard-link fixture could not be established.");
            }
            RequireThrows<InvalidDataException>(() => GeneratedToolIntegrationStore.History(
                applicationRoot, candidate.Manifest.ToolId));
        });
    }

    private static void WithCandidate(Action<string, GeneratedToolCandidate> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-PreparedBinding-Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var applicationRoot = Path.Combine(root, "app");
        Directory.CreateDirectory(sourceRoot);
        try
        {
            ApplicationRootLayout.Create(sourceRoot, applicationRoot);
            var files = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.OrdinalIgnoreCase)
            {
                ["tool.js"] = Encoding.UTF8.GetBytes("export function invoke() { return 'fixture'; }"),
                ["evidence/search.txt"] = Encoding.UTF8.GetBytes("Declared search."),
                ["evidence/composition.txt"] = Encoding.UTF8.GetBytes("Declared composition."),
                ["evidence/authority.txt"] = Encoding.UTF8.GetBytes("Declared authority."),
                ["evidence/gap.txt"] = Encoding.UTF8.GetBytes("Declared gap.")
            };
            GeneratedToolEvidence Evidence(string kind, string id, string path) =>
                new(kind, id, "unverified-declaration", "Creator declaration", path, Hash(files[path].Span));
            var manifest = new GeneratedToolCandidateManifest(
                1, "NeoBabylon", "fixture-tool",
                Path.Combine(applicationRoot, "Data", "GeneratedTools", "Candidates", "fixture-tool"),
                "revision-1", "Fixture tool", "Exercise disabled binding", "Return fixture value", "Declared capability gap",
                new GeneratedToolOrigin("task-9", "session-9", DateTimeOffset.Parse("2026-09-27T00:00:00Z"),
                    "fixture creator", "fixture provider", "fixture model",
                    "cap-v1:sha256:" + Hash(Encoding.UTF8.GetBytes("capability")), "gap-1"),
                "tool.js", new GeneratedToolContract("manual only", "{}", "{}"),
                new GeneratedToolAuthority(["read-only"], ["fixture workspace"], [], ["fixture input"],
                    "deny", "read-only", "never", Hash(files["evidence/authority.txt"].Span)),
                [],
                [
                    Evidence("existing-tool-search", "search-1", "evidence/search.txt"),
                    Evidence("composition-attempt", "composition-1", "evidence/composition.txt"),
                    Evidence("current-task-authority", "authority-1", "evidence/authority.txt"),
                    Evidence("capability-gap", "gap-1", "evidence/gap.txt")
                ],
                files.Select(file => new GeneratedToolFile(file.Key, Hash(file.Value.Span))).ToArray(),
                "unapproved");
            check(applicationRoot, GeneratedToolCandidateStore.Create(applicationRoot, manifest, files));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string newFileName, string existingFileName, IntPtr reserved);

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
