using System.Text.Json.Nodes;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NeoBabylon.Core;
using NeoBabylon.Host;
using NeoBabylon.Phase1AQualification;
using System.Threading.Channels;
using System.Diagnostics;

if (args is ["--listen", "stdio://"])
{
    return await RunFakeAppServerAsync();
}

if (args is ["--probe-provider-credentials-and-nvidia-config"])
{
    await RunProviderCredentialAndNvidiaConfigChecksAsync();
    return 0;
}

if (args is ["--probe-development-credential-records"])
{
    RunDevelopmentCredentialRecordChecks();
    return 0;
}

if (args is ["--probe-rename-saved-thread"])
{
    RunRenameAndCapabilitySwitchChecks();
    return 0;
}

if (args is ["--probe-same-thread-capability-switch-fixture"])
{
    await RunSameThreadCapabilitySwitchFixtureAsync();
    return 0;
}

if (args is ["--probe-cross-provider-thread-history"])
{
    await RunCrossProviderThreadHistoryChecksAsync();
    return 0;
}

if (args is ["--probe-ling-active-selection"])
{
    RunLingActiveSelectionChecks();
    return 0;
}

if (args is ["--probe-context-reasoning-host"])
{
    await ContextReasoningChecks.RunAsync();
    return 0;
}

if (args is ["--probe-capability-versions"])
{
    await CapabilityVersionChecks.RunAsync();
    return 0;
}

if (args is ["--prepare-patch-review-qa", var outputRoot])
{
    await RunPinnedPatchDiffQualificationAsync(outputRoot);
    return 0;
}

if (args is ["--prepare-history-pagination-qa", var historyQaApplicationRoot, var historyQaManifestPath])
{
    await PrepareHistoryPaginationQaAsync(historyQaApplicationRoot, historyQaManifestPath);
    return 0;
}

if (args is ["--prepare-p2-11-native-output-qa", var outputApplicationRoot, var outputWorkspace,
    var outputCapabilityPath, var outputManifestPath])
{
    await PrepareP2_11NativeOutputQaAsync(
        outputApplicationRoot, outputWorkspace, outputCapabilityPath, outputManifestPath);
    return 0;
}

if (args is ["--probe-function-patch", var candidateLockPath])
{
    await RunPinnedPatchDiffQualificationAsync(
        patchFormat: PatchFixtureFormat.Function, runtimeLockOverride: candidateLockPath);
    return 0;
}

if (args is ["--probe-effective-context-compaction"])
{
    await RunEffectiveContextCompactionQualificationAsync();
    return 0;
}

if (args is ["--probe-readonly-qualification-cleanup"])
{
    await AssertReadOnlyQualificationCleanupAsync();
    await AssertQualificationCleanupRejectsReparsePointAsync();
    return 0;
}

if (args is ["--probe-openrouter-route-tag"])
{
    AssertOpenRouterRouteTagValidation();
    return 0;
}

if (args is ["--probe-repeated-interruption-recovery"])
{
    await RunRepeatedInterruptionRecoveryAsync();
    return 0;
}

if (args is ["--probe-unrestricted-command-interruption"])
{
    await RunUnrestrictedCommandInterruptionAsync();
    return 0;
}

if (args is ["--probe-exited-supervisor-client-recovery"])
{
    await RunExitedSupervisorClientRecoveryAsync();
    return 0;
}

if (args is ["--probe-paused-turn-recovery"])
{
    await RunPausedTurnRecoveryAsync();
    return 0;
}

if (args is ["--probe-recovery-diagnostic-contract"])
{
    await AssertRecoveryDiagnosticContractAsync();
    return 0;
}

if (args is ["--probe-model-switch-rebind"])
{
    await RunModelSwitchRebindAsync();
    return 0;
}

if (args is ["--probe-context-change-invalidation"])
{
    await RunContextChangeInvalidationAsync();
    return 0;
}

if (args is ["--probe-cross-project-operation-ordering"])
{
    await RunCrossProjectOperationOrderingAsync();
    return 0;
}

if (args is ["--probe-patch-review-resume", var applicationRoot, var threadId])
{
    await ProbeSavedPatchResumeAsync(applicationRoot, threadId);
    return 0;
}

if (args is ["--probe-patch-supervisor", var supervisorApplicationRoot, var supervisorThreadId])
{
    await ProbePatchSupervisorAsync(supervisorApplicationRoot, supervisorThreadId);
    return 0;
}

if (args is ["--probe-live-openrouter-patch", var liveApplicationRoot])
{
    await ProbeLiveOpenRouterPatchAsync(liveApplicationRoot);
    return 0;
}

if (args is ["--probe-live-openrouter-patch", var candidateApplicationRoot, var liveCandidateLockPath])
{
    await ProbeLiveOpenRouterPatchAsync(candidateApplicationRoot, liveCandidateLockPath);
    return 0;
}

var applicationRootBoundaryOnly = args is ["--application-root-boundary-only"];
var failures = new List<string>();

void Check(string name, Action assertion)
{
    if (applicationRootBoundaryOnly && !name.StartsWith("application root", StringComparison.Ordinal))
    {
        return;
    }

    try
    {
        assertion();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
        Console.WriteLine($"FAIL {name}: {ex}");
    }
}

Check("runtime lock verifies exact binary identity", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Phase1A-Tests", Guid.NewGuid().ToString("N"));
    var runtimeDirectory = Path.Combine(root, "runtime");
    Directory.CreateDirectory(runtimeDirectory);
    var binary = Path.Combine(runtimeDirectory, "app-server.exe");
    File.WriteAllBytes(binary, [1, 2, 3, 4]);
    var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(binary))).ToLowerInvariant();
    var lockPath = Path.Combine(runtimeDirectory, "runtime-lock.json");
    File.WriteAllText(lockPath, $$"""
    {
      "schemaVersion": 1,
      "product": "NeoBabylon",
      "runtime": {
        "kind": "Codex App Server",
        "version": "test",
        "sourceRepository": "https://example.invalid/codex.git",
        "sourceRef": "test",
        "sourceRevision": "0123456789012345678901234567890123456789",
        "sourcePatchSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "sourceCheckoutRelativePath": ".",
        "appServerBinaryRelativePath": "runtime/app-server.exe",
        "platform": "test",
        "sha256": "{{hash}}",
        "protocol": "stable",
        "execution": "test"
      }
    }
    """);
    var identity = RuntimeIdentity.LoadVerified(lockPath);
    Assert(identity.Version == "test", "runtime version not read");
    Assert(identity.SourcePatchSha256 == new string('a', 64), "source patch digest was not retained");
    Assert(identity.Sha256 == hash, "runtime hash not retained");
    Assert(identity.BinaryPath == Path.GetFullPath(binary), "runtime path not resolved");
});

Check("application root rejects repository-local state and isolates Codex home", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Phase1A-Tests", Guid.NewGuid().ToString("N"));
    var repo = Path.Combine(root, "repo");
    Directory.CreateDirectory(repo);
    AssertThrows<InvalidOperationException>(() => ApplicationRootLayout.Create(repo, Path.Combine(repo, "Data")));
    var app = Path.Combine(root, "application");
    var layout = ApplicationRootLayout.Create(repo, app);
    Assert(layout.ApplicationRoot != layout.SourceRepositoryRoot, "application root equals source root");
    Assert(layout.DataRoot == Path.Combine(app, "Data"), "data root is not under application root");
    Assert(layout.WebView2UserDataFolder == Path.Combine(layout.DataRoot, "WebView2"), "WebView2 user data is not isolated under application Data");
    Assert(!ApplicationRootLayout.IsOrdinaryCodexRoot(layout.CodexHome), "ordinary Codex root selected");
});

Check("application root accepts only exact local App and validated Lab Runs App roots", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-ApplicationRoot-Tests", Guid.NewGuid().ToString("N"));
    var repo = Path.Combine(root, "repo");
    Directory.CreateDirectory(repo);

    var ordinaryApp = Path.Combine(repo, ".local", "App");
    var ordinary = ApplicationRootLayout.Create(repo, ordinaryApp);
    Assert(ordinary.ApplicationRoot == Path.GetFullPath(ordinaryApp), "exact local App root was not selected");
    Assert(Directory.Exists(ordinary.CodexHome) && Directory.Exists(ordinary.FixtureWorkspace),
        "exact local App did not initialize its isolated state");

    var qaApp = Path.Combine(repo, ".local", "Lab", "Runs", "qa-01", "App");
    var qa = ApplicationRootLayout.Create(repo.ToUpperInvariant(), qaApp);
    Assert(qa.ApplicationRoot == Path.GetFullPath(qaApp), "validated QA App root was not selected");
    Assert(Directory.Exists(qa.CodexHome), "validated QA App did not initialize its isolated state");

    var caseVariant = ApplicationRootLayout.Create(repo, Path.Combine(repo, ".LOCAL", "lab", "runs", "Qa-01", "app"));
    Assert(Directory.Exists(caseVariant.CodexHome), "case-insensitive Windows path components were not accepted");

    var singleId = ApplicationRootLayout.Create(repo, Path.Combine(repo, ".local", "Lab", "Runs", "a", "App"));
    Assert(Directory.Exists(singleId.CodexHome), "one-character alphanumeric run id was rejected");
    var maxId = ApplicationRootLayout.Create(repo, Path.Combine(repo, ".local", "Lab", "Runs", new string('a', 80), "App"));
    Assert(Directory.Exists(maxId.CodexHome), "80-character alphanumeric run id was rejected");
});

Check("application root rejects all other source descendants before creating Data", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-ApplicationRoot-Tests", Guid.NewGuid().ToString("N"));
    var repo = Path.Combine(root, "repo");
    Directory.CreateDirectory(repo);
    var rejected = new[]
    {
        repo,
        Path.Combine(repo, "Data"),
        Path.Combine(repo, ".local"),
        Path.Combine(repo, ".local", "Lab"),
        Path.Combine(repo, ".local", "Lab", "NeoBabylon-Data"),
        Path.Combine(repo, ".local", "Lab", "ProbeSources", "probe-01"),
        Path.Combine(repo, ".local", "Runtime", "NeoBabylon-Runtime"),
        Path.Combine(repo, ".local", "QA"),
        Path.Combine(repo, ".local-prefix", "App"),
        Path.Combine(repo, ".local", "Lab", "Runs", "qa-01"),
        Path.Combine(repo, ".local", "Lab", "Runs", "qa-01", "App", "child"),
        Path.Combine(repo, ".local", "Lab", "Runs", "-bad", "App"),
        Path.Combine(repo, ".local", "Lab", "Runs", "bad-", "App"),
        Path.Combine(repo, ".local", "Lab", "Runs", "bad_id", "App"),
        Path.Combine(repo, ".local", "Lab", "Runs", "é", "App"),
        Path.Combine(repo, ".local", "Lab", "Runs", new string('a', 81), "App"),
        Path.Combine(repo, ".local", "App", "..", ".."),
        Path.Combine(repo, ".local", "Lab", "Runs", "qa-01", "child", "..", "App")
    };
    foreach (var candidate in rejected)
    {
        var data = Path.Combine(candidate, "Data");
        Assert(!Directory.Exists(data), $"rejected candidate already had Data: {candidate}");
        AssertThrows<InvalidOperationException>(() => ApplicationRootLayout.Create(repo, candidate));
        Assert(!Directory.Exists(data), $"rejected candidate created Data: {candidate}");
    }

    var siblingPrefix = Path.Combine(root, "repo-sibling", "App");
    var outside = ApplicationRootLayout.Create(repo, siblingPrefix);
    Assert(Directory.Exists(outside.CodexHome), "outside-source sibling-prefix behavior changed");
});

Check("application root rejects a local junction redirect before touching its outside canary", () =>
{
    Assert(OperatingSystem.IsWindows(), "junction fixture requires Windows");
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-ApplicationRoot-Junction-Tests", Guid.NewGuid().ToString("N"));
    var repo = Path.Combine(root, "repo");
    var canary = Path.Combine(root, "outside-canary");
    var junction = Path.Combine(repo, ".local");
    Directory.CreateDirectory(repo);
    Directory.CreateDirectory(canary);
    using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/d /c mklink /J \"{junction}\" \"{canary}\"")
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    }) ?? throw new InvalidOperationException("junction fixture unavailable: cmd.exe did not start");
    Assert(process.WaitForExit(10_000), "junction fixture unavailable: mklink timed out");
    Assert(process.ExitCode == 0, $"junction fixture unavailable: {process.StandardError.ReadToEnd()} {process.StandardOutput.ReadToEnd()}");
    Assert((File.GetAttributes(junction) & FileAttributes.ReparsePoint) != 0,
        "junction fixture unavailable: .local is not a reparse point");
    try
    {
        AssertThrows<InvalidOperationException>(() => ApplicationRootLayout.Create(repo, Path.Combine(junction, "App")));
        Assert(!Directory.Exists(Path.Combine(canary, "App", "Data")), "junction target received application Data");
    }
    finally
    {
        if (Directory.Exists(junction) && (File.GetAttributes(junction) & FileAttributes.ReparsePoint) != 0)
        {
            Directory.Delete(junction);
        }
        if (!Directory.Exists(junction) && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
});

foreach (var redirectedComponent in new[] { "Data", "CodexHome", "Workspace" })
{
    Check($"application root rejects an existing {redirectedComponent} junction before creating state", () =>
    {
        Assert(OperatingSystem.IsWindows(), "junction fixture requires Windows");
        var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-ApplicationRoot-ChildJunction-Tests", Guid.NewGuid().ToString("N"));
        var repo = Path.Combine(root, "repo");
        var app = Path.Combine(repo, ".local", "Lab", "Runs", "qa-child-01", "App");
        var data = Path.Combine(app, "Data");
        var canary = Path.Combine(root, "outside-canary");
        var junction = redirectedComponent switch
        {
            "Data" => data,
            "CodexHome" => Path.Combine(data, "CodexHome"),
            _ => Path.Combine(data, "Workspace")
        };
        Directory.CreateDirectory(Path.GetDirectoryName(junction)!);
        Directory.CreateDirectory(canary);
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/d /c mklink /J \"{junction}\" \"{canary}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException($"{redirectedComponent} junction fixture unavailable: cmd.exe did not start");
        Assert(process.WaitForExit(10_000), $"{redirectedComponent} junction fixture unavailable: mklink timed out");
        Assert(process.ExitCode == 0,
            $"{redirectedComponent} junction fixture unavailable: {process.StandardError.ReadToEnd()} {process.StandardOutput.ReadToEnd()}");
        Assert((File.GetAttributes(junction) & FileAttributes.ReparsePoint) != 0,
            $"{redirectedComponent} junction fixture unavailable: path is not a reparse point");
        try
        {
            var rejected = false;
            try
            {
                _ = ApplicationRootLayout.Create(repo, app);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }

            Assert(!Directory.EnumerateFileSystemEntries(canary).Any(),
                $"{redirectedComponent} junction redirected application writes into the outside canary");
            if (redirectedComponent == "CodexHome")
            {
                Assert(!Directory.Exists(Path.Combine(data, "Workspace")),
                    "Workspace was created before the CodexHome redirect was rejected");
            }
            if (redirectedComponent == "Workspace")
            {
                Assert(!Directory.Exists(Path.Combine(data, "CodexHome")),
                    "CodexHome was created before the Workspace redirect was rejected");
            }
            Assert(rejected, $"{redirectedComponent} junction was accepted as application state");
        }
        finally
        {
            if (Directory.Exists(junction) && (File.GetAttributes(junction) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(junction);
            }
            if (!Directory.Exists(junction) && Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    });
}

Check("application root rejects the ordinary Codex home itself without creating state", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-ApplicationRoot-Tests", Guid.NewGuid().ToString("N"));
    var repo = Path.Combine(root, "repo");
    Directory.CreateDirectory(repo);
    var ordinaryCodexHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
    var candidateData = Path.Combine(ordinaryCodexHome, "Data");
    var hadCandidateData = Directory.Exists(candidateData);
    AssertThrows<InvalidOperationException>(() => ApplicationRootLayout.Create(repo, ordinaryCodexHome));
    Assert(Directory.Exists(candidateData) == hadCandidateData, "ordinary Codex home gained Data state");
});

Check("thread capability binding preserves workspace roots and contains only identity metadata", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-TaskBinding-Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var capabilityPath = Path.Combine(Directory.GetCurrentDirectory(), "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json");
        var capability = JsonSerializer.Deserialize<ModelCapabilityRecord>(
            File.ReadAllText(capabilityPath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("The LM Studio capability fixture is empty.");
        var workspaceRoot = Path.GetPathRoot(Environment.SystemDirectory)
            ?? throw new InvalidDataException("The test system directory has no filesystem root.");
        const string threadId = "root-workspace-binding-test";

        var binding = ThreadCapabilityBindingStore.Bind(root, threadId, workspaceRoot, capability);
        Assert(binding.Workspace == Path.GetFullPath(workspaceRoot), "normalizing a filesystem-root workspace changed its identity");
        Assert(ThreadCapabilityBindingStore.Matches(binding, capability, workspaceRoot), "the persisted exact capability binding did not match");
        Assert(!ThreadCapabilityBindingStore.Matches(binding, capability with { Endpoint = capability.Endpoint + "/changed" }, workspaceRoot),
            "the complete capability identity did not detect an endpoint change");

        var fileId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(threadId))).ToLowerInvariant();
        var content = File.ReadAllText(Path.Combine(root, "NeoBabylon", "TaskBindings", $"{fileId}.json"));
        Assert(content.Contains("capabilityIdentity", StringComparison.Ordinal), "the sidecar omitted the capability identity");
        Assert(!content.Contains(capability.Endpoint, StringComparison.OrdinalIgnoreCase), "the sidecar copied the raw provider endpoint");
        Assert(!content.Contains("ApiKey", StringComparison.OrdinalIgnoreCase), "the sidecar contains credential-shaped metadata");
        AssertThrows<InvalidDataException>(() => ThreadCapabilityBindingStore.Bind(
            root, threadId, workspaceRoot, capability with { Endpoint = capability.Endpoint + "/changed" }));
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
});

Check("legacy capability without completion maximum keeps its serialized binding identity", () =>
{
    var capabilityPath = Path.Combine(Directory.GetCurrentDirectory(), "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json");
    var capability = JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(capabilityPath), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The legacy LM Studio capability record is empty.");
    var legacyShape = new
    {
        capability.ProviderId,
        capability.ProviderDisplayName,
        capability.ProviderServerVersion,
        capability.Endpoint,
        capability.ModelIdentifier,
        capability.ModelVariant,
        capability.Architecture,
        capability.ParameterCount,
        capability.ModelSizeBytes,
        capability.Quantization,
        capability.ContextWindowAdvertised,
        capability.ContextWindowEffective,
        capability.ReasoningControls,
        capability.ToolFunctionCalling,
        capability.StructuredOutput,
        capability.AgentMetadata,
        capability.ProviderRoute,
        capability.ApplyPatchToolType,
        capability.ToolQualifications
    };
    var expectedSerialized = JsonSerializer.Serialize(legacyShape, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    var actualSerialized = CapabilityRecordIdentity.Serialize(capability);
    Assert(actualSerialized == expectedSerialized, "the optional completion maximum changed legacy capability serialization");
    var workspace = Path.GetFullPath(Directory.GetCurrentDirectory());
    var binding = new ThreadCapabilityBinding(
        "legacy-capability-thread", workspace, CapabilityRecordIdentity.ComputeSerialized(expectedSerialized),
        capability.ProviderId, capability.ModelIdentifier, DateTimeOffset.UtcNow);
    Assert(ThreadCapabilityBindingStore.Matches(binding, capability, workspace), "the pre-existing capability binding no longer matches on resume");
    Assert(CapabilityRecordIdentity.Compute(CapabilityRecordIdentity.Freeze(capability)) == binding.CapabilityIdentity,
        "freezing a legacy capability changed its saved-thread identity");
    var transitional = capability with
    {
        SerializedMaxCompletionTokensAdvertised = CapabilityObservation.Unknown("not-observed")
    };
    var transitionalSerialized = CapabilityRecordIdentity.Serialize(transitional);
    Assert(JsonNode.Parse(transitionalSerialized)?["maxCompletionTokensAdvertised"]?["state"]?.GetValue<string>() == "Unknown",
        "the transitional default Unknown capability could not be reconstructed");
    Assert(CapabilityRecordIdentity.Compute(transitional) != binding.CapabilityIdentity,
        "a transitional default-Unknown binding was confused with the pre-field identity");
});

Check("project registry persists selected folders without replacing isolated runtime data", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Projects-Tests", Guid.NewGuid().ToString("N"));
    var source = Path.Combine(root, "source");
    var app = Path.Combine(root, "app");
    var project = Path.Combine(root, "sample-project");
    Directory.CreateDirectory(source);
    Directory.CreateDirectory(project);
    var layout = ApplicationRootLayout.Create(source, app);
    var registry = new WorkspaceProjectRegistry(layout.DataRoot, layout.FixtureWorkspace);
    var first = registry.Read();
    Assert(first.SelectedWorkspace == layout.FixtureWorkspace && first.Projects.Count == 1, "first-run local workspace was not selected");
    Assert(!File.Exists(registry.RegistryPath), "first-run read unexpectedly wrote a registry");

    var preview = registry.PrepareAddAndSelect(project);
    Assert(!File.Exists(registry.RegistryPath), "preparing a project switch wrote state before the active thread could close");
    registry.Save(preview);
    var added = registry.Read();
    Assert(added.SelectedWorkspace == Path.GetFullPath(project), "selected project cwd was not recorded");
    Assert(added.Projects.Count == 2 && added.Projects[1].Name == "sample-project", "project list was not retained");
    Assert(registry.Read().SelectedWorkspace == added.SelectedWorkspace, "selected project did not persist across registry reload");
    Assert(registry.AddAndSelect(project).Projects.Count == 2, "adding the same folder duplicated the project");
    Assert(registry.SelectExisting(layout.FixtureWorkspace).SelectedWorkspace == layout.FixtureWorkspace, "the original workspace could not be restored");
    Assert(registry.Read().Projects.Count == 2, "switching deleted an existing project");

    AssertThrows<InvalidOperationException>(() => registry.SelectExisting(Path.Combine(root, "unknown")));
    AssertThrows<DirectoryNotFoundException>(() => registry.AddAndSelect(Path.Combine(root, "missing")));
    AssertThrows<InvalidOperationException>(() => registry.AddAndSelect(Path.Combine(layout.DataRoot, "Workspace")));
    Assert(registry.Read().SelectedWorkspace == layout.FixtureWorkspace, "a rejected choice changed the selected workspace");
    Assert(Directory.Exists(layout.CodexHome), "project changes removed isolated runtime data");
});

Check("project registry rejects malformed and unsupported data without rewriting evidence", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Projects-Invalid-Tests", Guid.NewGuid().ToString("N"));
    var source = Path.Combine(root, "source");
    var app = Path.Combine(root, "app");
    Directory.CreateDirectory(source);
    try
    {
        var layout = ApplicationRootLayout.Create(source, app);
        var registry = new WorkspaceProjectRegistry(layout.DataRoot, layout.FixtureWorkspace);
        Directory.CreateDirectory(Path.GetDirectoryName(registry.RegistryPath)!);

        const string malformed = "{not-json";
        File.WriteAllText(registry.RegistryPath, malformed, new UTF8Encoding(false));
        var malformedBytes = File.ReadAllBytes(registry.RegistryPath);
        AssertThrows<System.Text.Json.JsonException>(() => registry.Read());
        Assert(File.ReadAllBytes(registry.RegistryPath).SequenceEqual(malformedBytes),
            "reading malformed project-registry data rewrote the original bytes");

        var unsupported = JsonSerializer.Serialize(new
        {
            schemaVersion = 999,
            product = "NeoBabylon",
            selectedWorkspace = Path.Combine(root, "project"),
            projects = new[] { new { workspacePath = Path.Combine(root, "project"), name = "project" } }
        });
        File.WriteAllText(registry.RegistryPath, unsupported, new UTF8Encoding(false));
        var unsupportedBytes = File.ReadAllBytes(registry.RegistryPath);
        AssertThrows<InvalidDataException>(() => registry.Read());
        Assert(File.ReadAllBytes(registry.RegistryPath).SequenceEqual(unsupportedBytes),
            "reading unsupported project-registry schema rewrote the original bytes");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
});

Check("P5-02 protected records verify backup readback and restore only a missing fixture record",
    P5_02DataMigrationChecks.BackupReadbackAndRestoreMissing);
Check("P5-02 protected registry replacement resolves interrupted old, missing, and committed states",
    P5_02DataMigrationChecks.InterruptedReplacementRecoversDeterministically);
Check("P5-02 backup obstruction and future schema preserve original record bytes",
    P5_02DataMigrationChecks.BackupPathObstructionAndFutureSchemaPreserveBytes);
Check("P5-02 injected disk-full and access-denied backup/write failures preserve bytes and recovery artifacts",
    P5_02DataMigrationChecks.InjectedWriteAndBackupFailuresPreserveBytesAndRecoveryArtifacts);
Check("P5-02 fork bookmark replacement backs up existing navigation records",
    P5_02DataMigrationChecks.ForkBookmarksBackUpBeforeReplacement);
Check("P5-02 explicit fixture schema transition retains exact source bytes",
    P5_02DataMigrationChecks.ExplicitVersionTransitionPreservesSourceBytes);

Check("Task 8 candidate rejects common JSON and source credential assignments on create and read",
    GeneratedToolCandidateChecks.RejectsCredentialAssignmentsOnCreateAndRead);
Check("Task 8 candidate rejects observed hard-linked inventory and manifest files",
    GeneratedToolCandidateChecks.RejectsHardLinkedCandidateFilesOnRead);
Check("Task 8 candidate listing fails visibly above its aggregate count bound",
    GeneratedToolCandidateChecks.RejectsUnboundedCandidateListing);
Check("Task 9 host listing exposes the validated contract as inert read-only strings",
    GeneratedToolCandidateChecks.HostListingProjectsValidatedContractAsInertStrings);
Check("Task 9 candidate file inspection returns bounded UTF-8 text pages without splitting surrogates",
    GeneratedToolCandidateChecks.ReadsOnlyBoundedInventoriedFilePages);
Check("Task 9 candidate file inspection rejects stale identity, unlisted paths, and changed content",
    GeneratedToolCandidateChecks.RejectsStaleIdentityAndUninventoriedOrChangedFiles);
Check("Task 9 candidate file inspection rejects hard-linked files",
    GeneratedToolCandidateChecks.RejectsHardLinkedFileInspection);
Check("Task 9 candidate file inspection rejects reparse-point files",
    GeneratedToolCandidateChecks.RejectsReparseFileInspection);
Check("Task 9 candidate file inspection enforces the existing one MiB file cap",
    GeneratedToolCandidateChecks.RejectsOversizedFileInspection);
Check("Task 8 candidate requires four hashed declared evidence kinds and exact gap/authority bindings",
    GeneratedToolCandidateChecks.RequiresFourDistinctHashedDeclarationsOnCreateAndRead);
Check("Task 8 candidate evidence remains declarative and survives restart only as unapproved",
    GeneratedToolCandidateChecks.KeepsEvidenceDeclarativeAndCandidateUnapproved);

Check("Task 9 review binds current candidate content and declared runtime interface without activation",
    GeneratedToolReviewChecks.ReviewBindsCurrentContentAndInterfaceWithoutActivatingCandidate);
Check("Task 9 rejection history survives mutation and a changed identity needs a new decision",
    GeneratedToolReviewChecks.RejectionHistorySurvivesCandidateMutationAndNewIdentityNeedsNewReview);
Check("Task 9 corrupt review history fails closed without replacing evidence",
    GeneratedToolReviewChecks.CorruptHistoryFailsClosedWithoutReplacingEvidence);
Check("Task 9 disabled preparation binds exact current review without a callable route",
    GeneratedToolIntegrationChecks.PreparationBindsCurrentReviewWithoutMakingTheCandidateCallable);
Check("Task 9 disabled preparation rejects stale, rejected, and corrupt review evidence",
    GeneratedToolIntegrationChecks.PreparationRejectsStaleRejectedAndCorruptReviewEvidence);
Check("Task 9 revoke and cleanup append disabled denials without deleting evidence",
    GeneratedToolIntegrationChecks.RevocationAndCleanupAreDurableDenialsWithoutDeletingEvidence);
Check("Task 9 corrupt prepared binding history fails closed",
    GeneratedToolIntegrationChecks.CorruptBindingHistoryFailsClosed);
Check("Task 9 hard-linked prepared binding history fails closed",
    GeneratedToolIntegrationChecks.HardLinkedBindingHistoryFailsClosed);
Check("Task 7 disabled product MCP config binds the exact current preparation",
    GeneratedToolIntegrationChecks.DisabledMcpConfigIsExplicitAndBoundToCurrentPreparation);
Check("Task 7 explicit stage publishes only one current disabled product adapter",
    GeneratedToolIntegrationChecks.ExplicitStageUsesOnlyCurrentBindingAndKeepsTheAdapterDisabled);
Check("Task 7 disabled MCP confirmation requires typed disabled empty inventory", () =>
{
    Assert(GeneratedToolMcpStatus.Evaluate(new McpServerStatusEntry(
            CodexConfigBuilder.ProductMcpServerName, McpServerRuntimeStatus.Disabled,
            McpServerAuthStatus.Unsupported, [], null)) == GeneratedToolMcpConfirmation.ConfirmedDisabledEmpty,
        "disabled empty inventory was not confirmed");
    Assert(GeneratedToolMcpStatus.Evaluate(new McpServerStatusEntry(
            CodexConfigBuilder.ProductMcpServerName, null,
            McpServerAuthStatus.Unsupported, [], null)) == GeneratedToolMcpConfirmation.Unknown,
        "threadless status was misreported as confirmed disabled");
    Assert(GeneratedToolMcpStatus.Evaluate(new McpServerStatusEntry(
            CodexConfigBuilder.ProductMcpServerName, McpServerRuntimeStatus.Disabled,
            McpServerAuthStatus.Unsupported, [], "inventory unavailable")) == GeneratedToolMcpConfirmation.Unknown,
        "status with a tools error was misreported as confirmed");
    Assert(GeneratedToolMcpStatus.Evaluate(new McpServerStatusEntry(
            CodexConfigBuilder.ProductMcpServerName, McpServerRuntimeStatus.Connected,
            McpServerAuthStatus.Unsupported, ["unexpected"], null)) == GeneratedToolMcpConfirmation.UnexpectedTools,
        "nonempty generated-tool inventory did not fail closed");
    Assert(GeneratedToolMcpStatus.Evaluate(null) == GeneratedToolMcpConfirmation.Unknown,
        "missing server status was misreported as confirmed");
});
Check("Task 9 binding recovery IDs survive invalid candidate bytes",
    GeneratedToolIntegrationChecks.BindingIdsRemainDiscoverableWhenCandidateBytesAreInvalid);

Check("saved-thread listing includes only the selected project cwd", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Projects-Tests", Guid.NewGuid().ToString("N"));
    var first = Path.Combine(root, "first");
    var second = Path.Combine(root, "second");
    var threads = new JsonArray(
        new JsonObject { ["id"] = "first-1", ["cwd"] = first + Path.DirectorySeparatorChar },
        new JsonObject { ["id"] = "second-1", ["cwd"] = second },
        new JsonObject { ["id"] = "unknown-cwd" });
    var visible = ThreadListProjector.FilterByWorkspace(threads, first);
    Assert(visible.Count == 1 && visible[0]?["id"]?.GetValue<string>() == "first-1", "other-project or unknown-cwd thread leaked into the selected project");
    Assert(threads.Count == 3, "project filtering mutated App Server history data");
});

Check("runtime data lease rejects a second active owner", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Phase1B-Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    using var first = RuntimeDataLease.Acquire(root);
    AssertThrows<InvalidOperationException>(() => RuntimeDataLease.Acquire(root));
    first.Dispose();
    using var second = RuntimeDataLease.Acquire(root);
});

Check("qualification preserves session evidence when the journal is temporarily locked", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Phase1B-Tests", Guid.NewGuid().ToString("N"));
    var codexHome = Path.Combine(root, "CodexHome");
    var sessions = Path.Combine(codexHome, "sessions");
    Directory.CreateDirectory(sessions);
    var journal = Path.Combine(sessions, "rollout-test.jsonl");
    File.WriteAllText(journal, "{\"payload\":{\"type\":\"task_started\",\"model_context_window\":31129}}\n");

    try
    {
        using (new FileStream(journal, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var unavailable = SessionJournalEvidenceReader.ReadToolEvidence(codexHome, 2, TimeSpan.FromMilliseconds(1));
            Assert(unavailable.Failure is IOException, "journal lock was not returned as an I/O evidence failure");
            Assert(unavailable.Evidence["readStatus"]?.GetValue<string>() == "unavailable", "unavailable journal status was not retained");
            Assert(unavailable.Evidence["sessionPath"]?.GetValue<string>() == journal, "locked journal path was not retained");
        }

        var readable = SessionJournalEvidenceReader.ReadToolEvidence(codexHome);
        Assert(readable.Failure is null, "released journal remained unreadable");
        Assert(readable.Evidence["readStatus"]?.GetValue<string>() == "read", "readable journal status was not retained");
        Assert(readable.Evidence["modelContextWindow"]?.GetValue<int>() == 31129, "session evidence was not parsed after the lock cleared");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
});

Check("capability mapping preserves unknowns and maps only observed context", () =>
{
    var record = ModelCapabilityRecord.CreateUnknown("lmstudio", "http://127.0.0.1:1234/v1", "qwen/qwen3-14b");
    record = record with
    {
        ContextWindowAdvertised = CapabilityObservation.Known(32768, "test"),
        ToolFunctionCalling = CapabilityObservation.Known("tool_use", "test")
    };
    var config = CapabilityAdapter.ToCodexConfig(record);
    Assert(config.TryGetValue("model_context_window", out var context) && context is int value && value == 32768, "context was not mapped");
    Assert(!config.ContainsKey("model_auto_compact_token_limit"), "auto-compaction was invented");
    Assert(record.StructuredOutput.State == CapabilityState.Unknown, "unknown structured output was not preserved");
});

Check("model capability serialization preserves exact tool qualification evidence", () =>
{
    var releaseRoot = Path.Combine(Directory.GetCurrentDirectory(), "docs", "release");
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    foreach (var fileName in new[]
    {
        "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json",
        "MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json"
    })
    {
        var source = JsonNode.Parse(File.ReadAllText(Path.Combine(releaseRoot, fileName)))!.AsObject();
        var record = source.Deserialize<ModelCapabilityRecord>(options)
            ?? throw new InvalidDataException($"{fileName} did not deserialize.");
        var roundTrip = JsonSerializer.SerializeToNode(record, options)!.AsObject();
        Assert(JsonNode.DeepEquals(source["toolQualifications"], roundTrip["toolQualifications"]),
            $"{fileName} dropped or changed selected-tuple tool evidence");
    }

    var unknown = ModelCapabilityRecord.CreateUnknown("lmstudio", "http://127.0.0.1:1234/v1", "unknown-model");
    var unknownJson = JsonSerializer.SerializeToNode(unknown, options)!.AsObject();
    Assert(unknownJson["toolQualifications"]?.AsArray().Count == 0,
        "a missing qualification record must remain an empty list, not a fabricated capability");
});

Check("Codex catalog mapping pins observed model metadata without GPT defaults", () =>
{
    var record = ModelCapabilityRecord.CreateUnknown("lmstudio", "http://127.0.0.1:1234/v1", "phase1a-qwen3-14b") with
    {
        ProviderDisplayName = "LM Studio",
        ProviderServerVersion = "0.4.25.0",
        ModelVariant = "qwen/qwen3-14b@q4_k_m",
        Architecture = "qwen3",
        ParameterCount = "14B",
        ModelSizeBytes = "9001931568",
        ContextWindowAdvertised = CapabilityObservation.Known(32768, "test"),
        ContextWindowEffective = CapabilityObservation.Known(32768, "test"),
        ToolFunctionCalling = CapabilityObservation.Known("tool_use", "test"),
        AgentMetadata = CapabilityObservation.Known("type=llm; vision=False; parallel=4", "test")
    };

    var catalog = CodexModelCatalogBuilder.Build(record);
    var model = catalog["models"]![0]!.AsObject();
    Assert(model["slug"]?.GetValue<string>() == "phase1a-qwen3-14b", "catalog slug was not pinned");
    Assert(model["context_window"]?.GetValue<int>() == 32768, "observed context was not mapped");
    Assert(model["max_context_window"]?.GetValue<int>() == 32768, "maximum context was not pinned to observed context");
    Assert(model["supported_reasoning_levels"]?.AsArray().Count == 0, "unknown reasoning levels were invented");
    Assert(model["input_modalities"]?.AsArray().Count == 1 && model["input_modalities"]![0]!.GetValue<string>() == "text", "vision=false was not enforced");
    Assert(model["parallel"] is null, "LM Studio parallel metadata was projected into an unsupported catalog field");
    Assert(model["parallel_tool_calls"] is null, "LM Studio parallel metadata was conflated with Codex parallel tool calls");
    Assert(model["shell_type"]?.GetValue<string>() == "unified_exec", "observed tool use was not mapped to the Codex shell tool");
    Assert(model["apply_patch_tool_type"] is null, "unknown patch format was inferred for the LM Studio record");
    Assert(model["apply_patch_tool_type"] is null, "unknown patch format was inferred for the LM Studio record");
    Assert(model["supports_reasoning_summary_parameter"]?.GetValue<bool>() == false, "unsupported reasoning summary was enabled");
});

Check("OpenRouter observed reasoning and image input reach its Codex catalog entry", () =>
{
    var record = ModelCapabilityRecord.CreateUnknown(
        "openrouter",
        "https://openrouter.ai/api/v1",
        "nex-agi/nex-n2.5-pro:free") with
    {
        ProviderDisplayName = "OpenRouter",
        ModelVariant = "nex-agi/nex-n2.5-pro-20260907",
        Architecture = "Qwen3",
        Quantization = CapabilityObservation.Known("fp8", "OpenRouter model endpoints API"),
        ContextWindowAdvertised = CapabilityObservation.Known(262144, "OpenRouter models API"),
        ContextWindowEffective = CapabilityObservation.Known(262144, "test App Server effective state"),
        ReasoningControls = new CapabilityObservation(
            CapabilityState.Known,
            JsonNode.Parse("{\"supported_efforts\":[\"high\",\"medium\",\"none\"],\"default_effort\":\"high\"}"),
            "OpenRouter models API"),
        ToolFunctionCalling = CapabilityObservation.Known("tool_use", "OpenRouter model endpoints API"),
        StructuredOutput = CapabilityObservation.Known("response_format+structured_outputs", "OpenRouter model endpoints API"),
        AgentMetadata = CapabilityObservation.Known(
            "type=llm; state=available; provider=Nex AGI; vision=True; inputModalities=text,image; outputModalities=text; maxCompletionTokens=235929; expirationDate=2026-09-25",
            "OpenRouter models and model endpoints APIs")
    };

    var model = CodexModelCatalogBuilder.Build(record)["models"]![0]!.AsObject();
    var reasoning = model["supported_reasoning_levels"]!.AsArray();
    Assert(model["default_reasoning_level"]?.GetValue<string>() == "high", "provider default reasoning effort was not retained");
    Assert(reasoning.Count == 3, "advertised reasoning levels were dropped");
    Assert(reasoning[0]?["effort"]?.GetValue<string>() == "high" && reasoning[1]?["effort"]?.GetValue<string>() == "medium" && reasoning[2]?["effort"]?.GetValue<string>() == "none", "reasoning levels differ from the provider record");
    var modalities = model["input_modalities"]!.AsArray();
    Assert(modalities.Count == 2 && modalities[0]?.GetValue<string>() == "text" && modalities[1]?.GetValue<string>() == "image", "advertised input modalities were not mapped");
    Assert(model["context_window"]?.GetValue<int>() == 262144, "advertised context was not mapped");
    Assert(!model["description"]!.GetValue<string>().Contains("local model", StringComparison.OrdinalIgnoreCase), "remote provider record was mislabeled as a local model");
});

Check("Codex catalog maps only a positive observed completion maximum", () =>
{
    var record = ModelCapabilityRecord.CreateUnknown("openrouter", "https://openrouter.ai/api/v1", "stealth/space-bunny-alpha") with
    {
        ContextWindowAdvertised = CapabilityObservation.Known(1_000_000, "test"),
        ToolFunctionCalling = CapabilityObservation.Known("tool_use", "test"),
        AgentMetadata = CapabilityObservation.Known("inputModalities=text,image; providerInputModalities=text,image,video; outputModalities=text; maxCompletionTokens=999", "test")
    };
    var unknown = CodexModelCatalogBuilder.Build(record)["models"]![0]!.AsObject();
    Assert(!unknown.ContainsKey("max_completion_tokens"), "an untyped free-text completion maximum was mapped into the model catalog");

    record = record with { MaxCompletionTokensAdvertised = CapabilityObservation.Known(524_288, "test") };
    var known = CodexModelCatalogBuilder.Build(record)["models"]![0]!.AsObject();
    Assert(known["max_completion_tokens"]?.GetValue<int>() == 524_288, "advertised completion maximum did not reach the runtime model field");
    Assert(known["context_window"]?.GetValue<int>() == 1_000_000, "completion maximum changed model context");
    Assert(known["input_modalities"]!.AsArray().Count == 2, "unsupported video was mapped into Codex input modalities");

    record = record with { MaxCompletionTokensAdvertised = CapabilityObservation.Known(0, "test") };
    AssertThrows<InvalidOperationException>(() => CodexModelCatalogBuilder.Build(record));
});

Check("Stealth OpenRouter capability record maps the advertised completion maximum without reasoning levels", () =>
{
    var recordPath = Path.Combine(Directory.GetCurrentDirectory(), "docs", "release", "MODEL_CAPABILITY_OPENROUTER_STEALTH_SPACE_BUNNY_ALPHA.json");
    var record = JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(recordPath), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The Stealth capability record is empty.");
    var model = CodexModelCatalogBuilder.Build(record)["models"]![0]!.AsObject();
    Assert(model["slug"]?.GetValue<string>() == "stealth/space-bunny-alpha", "Stealth model ID changed");
    Assert(model["max_completion_tokens"]?.GetValue<int>() == 524_288, "Stealth completion maximum was not mapped");
    Assert(model["supported_reasoning_levels"]?.AsArray().Count == 0, "unknown Stealth reasoning levels were invented");
    var modalities = model["input_modalities"]!.AsArray();
    Assert(modalities.Count == 2 && modalities[0]?.GetValue<string>() == "text" && modalities[1]?.GetValue<string>() == "image",
        "Stealth catalog input modalities must exclude video");
});

Check("live-qualified NEX patch format reaches the Codex model catalog", () =>
{
    var recordPath = Path.Combine(Directory.GetCurrentDirectory(), "docs", "release", "MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json");
    var record = JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(recordPath), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The canonical OpenRouter capability record is empty.");
    var model = CodexModelCatalogBuilder.Build(record)["models"]![0]!.AsObject();
    Assert(model["apply_patch_tool_type"]?.GetValue<string>() == "function", "the live-qualified function patch format did not reach the Codex model catalog");
    Assert(model["max_completion_tokens"]?.GetValue<int>() == 235_929, "NEX advertised completion maximum was not mapped from the typed observation");
    var serialized = CapabilityRecordIdentity.Serialize(record);
    Assert(JsonNode.Parse(serialized)?["maxCompletionTokensAdvertised"]?["value"]?.GetValue<int>() == 235_929,
        "the typed NEX maximum was not preserved in the capability snapshot");
    var legacy = record with { SerializedMaxCompletionTokensAdvertised = null };
    var workspace = Path.GetFullPath(Directory.GetCurrentDirectory());
    var legacyBinding = new ThreadCapabilityBinding(
        "legacy-nex-thread", workspace, CapabilityRecordIdentity.Compute(legacy),
        legacy.ProviderId, legacy.ModelIdentifier, DateTimeOffset.UtcNow);
    Assert(!ThreadCapabilityBindingStore.Matches(legacyBinding, record, workspace),
        "the new typed NEX record was incorrectly treated as the old saved-thread identity");
    Assert(ThreadCapabilityBindingStore.Matches(legacyBinding, legacy, workspace),
        "the original NEX record without a typed maximum no longer has its saved-thread identity");
});

Check("Nemotron typed completion maximum reaches the catalog and bounds an explicit override", () =>
{
    var recordPath = Path.Combine(Directory.GetCurrentDirectory(), "docs", "release", "MODEL_CAPABILITY_OPENROUTER_NEMOTRON_3_ULTRA_550B_A55B_FREE.json");
    var record = JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(recordPath), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The Nemotron capability record is empty.");
    var model = CodexModelCatalogBuilder.Build(record)["models"]![0]!.AsObject();
    Assert(model["max_completion_tokens"]?.GetValue<int>() == 65_536, "Nemotron advertised completion maximum was not mapped from the typed observation");
    var options = new TurnStartOptions("nemotron-fixture-thread", "fixture prompt");
    Assert(AppServerClient.BuildTurnStartRequest(1, options, record, 65_536).Params["maxOutputTokens"]?.GetValue<int>() == 65_536,
        "the advertised maximum was rejected as an explicit override");
    AssertThrows<InvalidDataException>(() => AppServerClient.BuildTurnStartRequest(2, options, record, 65_537));
});

Check("Codex model catalog rejects an unsupported known patch format", () =>
{
    var recordPath = Path.Combine(Directory.GetCurrentDirectory(), "docs", "release", "MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json");
    var source = JsonNode.Parse(File.ReadAllText(recordPath))!.AsObject();
    source["applyPatchToolType"] = new JsonObject
    {
        ["state"] = "Known",
        ["value"] = "invented",
        ["evidenceSource"] = "test fixture"
    };
    var record = source.Deserialize<ModelCapabilityRecord>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The controlled capability record is empty.");
    AssertThrows<InvalidOperationException>(() => CodexModelCatalogBuilder.Build(record));
});

Check("OpenRouter isolated config uses Responses, the exact endpoint, and child-shell credential exclusion", () =>
{
    var record = ModelCapabilityRecord.CreateUnknown(
        "openrouter",
        "https://openrouter.ai/api/v1",
        "nex-agi/nex-n2.5-pro:free") with
    {
        ProviderRoute = new CapabilityObservation(
            CapabilityState.Known,
            JsonNode.Parse("""{"providerName":"Nex AGI","providerSlug":"nex-agi","endpointTag":"nex-agi/fp8","quantization":"fp8","expirationDate":"2026-09-25"}"""),
            "OpenRouter providers and model endpoint APIs (2026-09-23)")
    };
    var config = CodexConfigBuilder.Build(record);
    Assert(config.Contains("model = \"nex-agi/nex-n2.5-pro:free\"", StringComparison.Ordinal), "exact free model ID was not pinned");
    Assert(config.Contains("model_provider = \"openrouter\"", StringComparison.Ordinal), "OpenRouter provider ID was not pinned");
    Assert(config.Contains("[model_providers.openrouter]", StringComparison.Ordinal), "custom OpenRouter provider was not defined");
    Assert(config.Contains("base_url = \"https://openrouter.ai/api/v1\"", StringComparison.Ordinal), "OpenRouter endpoint was not pinned");
    Assert(config.Contains("wire_api = \"responses\"", StringComparison.Ordinal), "Responses wire API was not explicit");
    Assert(config.Contains("env_key = \"OPENROUTER_API_KEY\"", StringComparison.Ordinal), "credential was not referenced through its environment variable name");
    Assert(config.Contains("openrouter_provider_endpoint = \"nex-agi/fp8\"", StringComparison.Ordinal), "the exact observed OpenRouter provider endpoint was not pinned");
    Assert(config.Contains("http_headers = { \"X-OpenRouter-Metadata\" = \"enabled\" }", StringComparison.Ordinal), "OpenRouter route response metadata was not enabled");
    Assert(config.Contains("[shell_environment_policy]", StringComparison.Ordinal) && config.Contains("exclude = [\"OPENROUTER_API_KEY\"]", StringComparison.Ordinal), "provider credential was not excluded from child commands");
    Assert(!config.Contains("sk-or-", StringComparison.Ordinal), "credential material was written to config");
});

Check("OpenRouter route tags accept a base provider slug or one valid provider variant", AssertOpenRouterRouteTagValidation);

static void AssertOpenRouterRouteTagValidation()
{
    static ModelCapabilityRecord RouteRecord(string providerSlug, string endpointTag,
        string endpoint = "https://openrouter.ai/api/v1") =>
        ModelCapabilityRecord.CreateUnknown("openrouter", endpoint, "nvidia/nemotron-3-ultra-550b-a55b:free") with
        {
            ProviderRoute = new CapabilityObservation(
                CapabilityState.Known,
                new JsonObject { ["providerSlug"] = providerSlug, ["endpointTag"] = endpointTag },
                "controlled OpenRouter route fixture")
        };

    foreach (var endpointTag in new[] { "nvidia", "nvidia/fp8" })
    {
        var config = CodexConfigBuilder.Build(RouteRecord("nvidia", endpointTag));
        Assert(config.Contains($"openrouter_provider_endpoint = \"{endpointTag}\"", StringComparison.Ordinal),
            $"OpenRouter did not retain the observed provider route tag {endpointTag}");
    }
    Assert(CodexConfigBuilder.Build(RouteRecord("nex-agi", "nex-agi/fp8"))
        .Contains("openrouter_provider_endpoint = \"nex-agi/fp8\"", StringComparison.Ordinal),
        "the existing provider/variant route was rejected");
    Assert(CodexConfigBuilder.Build(RouteRecord("_nvidia-", "_nvidia-/_fp8-"))
        .Contains("openrouter_provider_endpoint = \"_nvidia-/_fp8-\"", StringComparison.Ordinal),
        "the pinned route-segment grammar rejected allowed edge punctuation");

    foreach (var (providerSlug, endpointTag) in new[]
    {
        ("nvidia", "other"),
        ("nvidia", "nvidia-other/fp8"),
        ("nvidia", "nvidia/"),
        ("nvidia", "nvidia//fp8"),
        ("nvidia", "nvidia/fp8/other"),
        ("nvidia", " nvidia"),
        ("nvidia", "nvidia "),
        ("nvidia", "nvidia/fp 8"),
        ("nvidia", "nvidia/\tfp8"),
        ("nvidia", "nvidia\\fp8"),
        ("nvidia", "nvidia/.."),
        ("nvidia", "nvidia/fp.8"),
        ("nvid ia", "nvid ia"),
        ("nvidia/other", "nvidia/other")
    })
    {
        AssertThrows<InvalidOperationException>(() => CodexConfigBuilder.Build(RouteRecord(providerSlug, endpointTag)));
    }

    var unknownRoute = RouteRecord("nvidia", "nvidia") with { ProviderRoute = CapabilityObservation.Unknown("controlled fixture") };
    AssertThrows<InvalidOperationException>(() => CodexConfigBuilder.Build(unknownRoute));
    AssertThrows<InvalidOperationException>(() => CodexConfigBuilder.Build(RouteRecord(
        "nvidia", "nvidia", "http://127.0.0.1:4567/v1")));
}

Check("OpenRouter config refuses an unknown provider route", () =>
{
    var record = ModelCapabilityRecord.CreateUnknown(
        "openrouter",
        "https://openrouter.ai/api/v1",
        "nex-agi/nex-n2.5-pro:free");
    AssertThrows<InvalidOperationException>(() => CodexConfigBuilder.Build(record));
});

Check("OpenRouter live route pin rejects a noncanonical base URL", () =>
{
    var record = ModelCapabilityRecord.CreateUnknown(
        "openrouter",
        "http://127.0.0.1:4567/v1",
        "nex-agi/nex-n2.5-pro:free") with
    {
        ProviderRoute = new CapabilityObservation(
            CapabilityState.Known,
            JsonNode.Parse("""{"providerName":"Nex AGI","providerSlug":"nex-agi","endpointTag":"nex-agi/fp8"}"""),
            "test")
    };

    AssertThrows<InvalidOperationException>(() => CodexConfigBuilder.Build(record));
});

Check("deterministic OpenRouter mock is loopback-only and omits the live route pin", () =>
{
    var record = ModelCapabilityRecord.CreateUnknown(
        "openrouter",
        "http://127.0.0.1:4567/v1",
        "nex-agi/nex-n2.5-pro:free");
    var config = CodexConfigBuilder.Build(record, deterministicOpenRouterMock: true);

    Assert(config.Contains("base_url = \"http://127.0.0.1:4567/v1\"", StringComparison.Ordinal), "mock base URL was not retained");
    Assert(config.Contains("wire_api = \"responses\"", StringComparison.Ordinal), "mock Responses wire was not retained");
    Assert(!config.Contains("openrouter_provider_endpoint", StringComparison.Ordinal), "mock was mislabeled with a live provider route");
    AssertThrows<InvalidOperationException>(() => CodexConfigBuilder.Build(
        record with { Endpoint = "http://example.com:4567/v1" },
        deterministicOpenRouterMock: true));
    AssertThrows<InvalidOperationException>(() => CodexConfigBuilder.Build(record));
});

Check("host capability selection is limited to canonical release records", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Phase1B-Tests", Guid.NewGuid().ToString("N"));
    var release = Path.Combine(root, "docs", "release");
    Directory.CreateDirectory(release);
    var defaultRecord = Path.Combine(release, "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json");
    var selectedRecord = Path.Combine(release, "MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json");
    var outsideRecord = Path.Combine(root, "untrusted.json");
    var isolatedDataRoot = Path.Combine(root, "application", "Data");
    var isolatedRecord = Path.Combine(isolatedDataRoot, "Fixtures", "model-capability.json");
    File.WriteAllText(defaultRecord, "{}");
    File.WriteAllText(selectedRecord, "{}");
    File.WriteAllText(outsideRecord, "{}");
    Directory.CreateDirectory(Path.GetDirectoryName(isolatedRecord)!);
    File.WriteAllText(isolatedRecord, "{}");

    try
    {
        Assert(CapabilityRecordPathResolver.Resolve(root, null) == defaultRecord, "default provider record changed");
        Assert(CapabilityRecordPathResolver.Resolve(root, selectedRecord) == selectedRecord, "selected canonical provider record was not resolved");
        AssertThrows<InvalidOperationException>(() => CapabilityRecordPathResolver.Resolve(root, outsideRecord));
        Assert(CapabilityRecordPathResolver.ResolveForApprovalQa(root, isolatedRecord, isolatedDataRoot) == isolatedRecord,
            "ApprovalQA could not load a capability fixture from isolated application Data");
        AssertThrows<InvalidOperationException>(() => CapabilityRecordPathResolver.ResolveForApprovalQa(root, outsideRecord, isolatedDataRoot));
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
});

Check("OpenRouter route inspection fails closed unless the exact free endpoint is unique", () =>
{
    var record = ModelCapabilityRecord.CreateUnknown(
        "openrouter",
        "https://openrouter.ai/api/v1",
        "nex-agi/nex-n2.5-pro:free") with
    {
        ModelVariant = "nex-agi/nex-n2.5-pro-20260907",
        Quantization = CapabilityObservation.Known("fp8", "OpenRouter endpoints API"),
        ContextWindowAdvertised = CapabilityObservation.Known(262144, "OpenRouter models API"),
        ReasoningControls = new CapabilityObservation(
            CapabilityState.Known,
            JsonNode.Parse("{\"mandatory\":false,\"supported_efforts\":[\"high\",\"medium\",\"none\"],\"default_effort\":\"high\"}"),
            "OpenRouter models API"),
        ToolFunctionCalling = CapabilityObservation.Known("tool_use", "OpenRouter endpoints API"),
        StructuredOutput = CapabilityObservation.Known("response_format+structured_outputs+json_schema", "OpenRouter model capability page"),
        AgentMetadata = CapabilityObservation.Known(
            "provider=Nex AGI; maxCompletionTokens=235929; expirationDate=2026-09-25",
            "OpenRouter models and endpoints APIs"),
        ProviderRoute = new CapabilityObservation(
            CapabilityState.Known,
            JsonNode.Parse("""{"providerName":"Nex AGI","providerSlug":"nex-agi","endpointTag":"nex-agi/fp8","quantization":"fp8","expirationDate":"2026-09-25"}"""),
            "OpenRouter providers and model endpoint APIs")
    };
    var model = JsonNode.Parse("""
    {"id":"nex-agi/nex-n2.5-pro:free","canonical_slug":"nex-agi/nex-n2.5-pro-20260907","context_length":262144,"expiration_date":"2026-09-25","reasoning":{"mandatory":false,"supported_efforts":["high","medium","none"],"default_effort":"high"},"supported_parameters":["include_reasoning","logprobs","max_tokens","reasoning","reasoning_effort","response_format","structured_outputs","temperature","tool_choice","tools","top_k","top_logprobs","top_p"],"pricing":{"prompt":"0","completion":"0"},"architecture":{"modality":"text+image->text","input_modalities":["text","image"],"output_modalities":["text"],"tokenizer":"Qwen3","instruct_type":null}}
    """)!;
    var endpoints = JsonNode.Parse("""
    {"data":{"id":"nex-agi/nex-n2.5-pro:free","endpoints":[{"name":"Nex AGI | nex-agi/nex-n2.5-pro-20260907:free","model_id":"nex-agi/nex-n2.5-pro:free","model_name":"Nex AGI: Nex-N2.5-Pro","context_length":262144,"pricing":{"prompt":"0","completion":"0","discount":0},"provider_name":"Nex AGI","tag":"nex-agi/fp8","quantization":"fp8","max_completion_tokens":235929,"max_prompt_tokens":null,"supported_parameters":["reasoning","include_reasoning","top_p","top_k","temperature","max_tokens","logprobs","top_logprobs","tools","tool_choice","response_format","structured_outputs","reasoning_effort"],"supports_tool_choice":{"none":true,"auto":true,"required":true,"function":true},"status":0,"uptime_last_30m":98.22279223158667,"uptime_last_5m":96.57768651608487,"uptime_last_1d":99.37200646409055,"supports_implicit_caching":false,"supports_voice_cloning":false,"latency_last_30m":null,"throughput_last_30m":null}]}}
    """)!;

    var snapshot = OpenRouterInspection.ValidateSnapshot(record, model, endpoints, DateTimeOffset.Parse("2026-09-22T00:00:00Z"));
    Assert(snapshot["route"]?["endpointCount"]?.GetValue<int>() == 1, "unique endpoint count was not retained");
    Assert(snapshot["route"]?["providerName"]?.GetValue<string>() == "Nex AGI", "exact provider route was not retained");
    Assert(snapshot["route"]?["tag"]?.GetValue<string>() == "nex-agi/fp8", "exact endpoint tag was not retained");
    Assert(snapshot["routePolicy"]?.GetValue<string>()?.Contains("provider fallback disabled", StringComparison.Ordinal) == true, "route fallback policy was not recorded");

    var withFallback = endpoints.DeepClone();
    withFallback["data"]!["endpoints"]!.AsArray().Add(endpoints["data"]!["endpoints"]![0]!.DeepClone());
    AssertThrows<InvalidDataException>(() => OpenRouterInspection.ValidateSnapshot(record, model, withFallback, DateTimeOffset.Parse("2026-09-22T00:00:00Z")));

    var paid = endpoints.DeepClone();
    paid["data"]!["endpoints"]![0]!["pricing"]!["prompt"] = "0.01";
    AssertThrows<InvalidDataException>(() => OpenRouterInspection.ValidateSnapshot(record, model, paid, DateTimeOffset.Parse("2026-09-22T00:00:00Z")));

    var routeChanged = endpoints.DeepClone();
    routeChanged["data"]!["endpoints"]![0]!["tag"] = "different-provider/fp8";
    AssertThrows<InvalidDataException>(() => OpenRouterInspection.ValidateSnapshot(record, model, routeChanged, DateTimeOffset.Parse("2026-09-22T00:00:00Z")));
});

Check("Windows sandbox qualification is explicit and opt in", () =>
{
    var record = ModelCapabilityRecord.CreateUnknown("lmstudio", "http://127.0.0.1:1234/v1", "phase1a-qwen3-14b") with
    {
        ContextWindowEffective = CapabilityObservation.Known(32768, "test"),
        ToolFunctionCalling = CapabilityObservation.Known("tool_use", "test"),
        AgentMetadata = CapabilityObservation.Known("vision=False", "test")
    };
    var config = CodexConfigBuilder.Build(record, "C:\\fixture\\model-catalog.json", "unelevated");
    Assert(config.Contains("[windows]", StringComparison.Ordinal), "Windows sandbox section was not emitted");
    Assert(config.Contains("sandbox = \"unelevated\"", StringComparison.Ordinal), "Windows sandbox mode was not pinned");
    Assert(config.IndexOf("model_context_window = 32768", StringComparison.Ordinal) < config.IndexOf("[windows]", StringComparison.Ordinal), "context was emitted inside the Windows table");
});

Check("Local provider isolated config disables login shells and profile loading", () =>
{
    var record = ModelCapabilityRecord.CreateUnknown("lmstudio", "http://127.0.0.1:1234/v1", "phase1a-qwen3-14b");
    var config = CodexConfigBuilder.Build(record);
    Assert(config.Contains("allow_login_shell = false", StringComparison.Ordinal), "local provider still permits the default login shell");
    Assert(config.Contains("[shell_environment_policy]", StringComparison.Ordinal)
        && config.Contains("experimental_use_profile = false", StringComparison.Ordinal), "local provider did not disable shell profile loading");
});

Check("isolated config replacement preserves old bytes when Windows denies publication", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-AtomicConfig-Tests", Guid.NewGuid().ToString("N"));
    var sourceRoot = Path.Combine(root, "source");
    var applicationRoot = Path.Combine(root, "app");
    Directory.CreateDirectory(sourceRoot);
    try
    {
        var layout = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
        var oldCapability = ModelCapabilityRecord.CreateUnknown(
            "lmstudio", "http://127.0.0.1:1234/v1", "atomic-old-model");
        var nextCapability = oldCapability with { ModelIdentifier = "atomic-next-model" };
        var configPath = CodexConfigBuilder.WriteIsolated(layout.CodexHome, oldCapability);
        var oldBytes = File.ReadAllBytes(configPath);
        Assert(Encoding.UTF8.GetString(oldBytes).Contains("model = \"atomic-old-model\"", StringComparison.Ordinal),
            "old config fixture was not published");
        var outsideData = Path.Combine(applicationRoot, "OtherHome");
        AssertThrows<InvalidDataException>(() => CodexConfigBuilder.WriteIsolated(outsideData, nextCapability));
        Assert(!Directory.Exists(outsideData), "non-Data home was created by isolated config writer");

        var publicationFailed = false;
        using (var heldTarget = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            try
            {
                CodexConfigBuilder.WriteIsolated(layout.CodexHome, nextCapability);
            }
            catch (IOException)
            {
                publicationFailed = true;
            }
            catch (UnauthorizedAccessException)
            {
                publicationFailed = true;
            }
        }
        Assert(publicationFailed, "exclusive target lock did not deny config publication");
        Assert(File.ReadAllBytes(configPath).SequenceEqual(oldBytes),
            "failed replacement changed the existing config bytes");
        Assert(Directory.EnumerateFiles(layout.CodexHome).All(file =>
                Path.GetFileName(file) is "config.toml" or "model-catalog.json"),
            "failed replacement left a staged config in the isolated home");

        CodexConfigBuilder.WriteIsolated(layout.CodexHome, nextCapability);
        var published = File.ReadAllText(configPath);
        Assert(published.Contains("model = \"atomic-next-model\"", StringComparison.Ordinal)
            && !published.Contains("model = \"atomic-old-model\"", StringComparison.Ordinal),
            "successful replacement did not publish the complete new config");
        Assert(File.ReadAllBytes(configPath).SequenceEqual(Encoding.UTF8.GetBytes(
                CodexConfigBuilder.Build(nextCapability, Path.Combine(layout.CodexHome, "model-catalog.json")))),
            "atomic publication changed the default config bytes");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
});

Check("unrestricted host policy is explicit in isolated Codex config", () =>
{
    var record = ModelCapabilityRecord.CreateUnknown("lmstudio", "http://127.0.0.1:1234/v1", "phase1a-qwen3-14b");
    var config = CodexConfigBuilder.Build(record, toolExecutionPolicy: ToolExecutionPolicy.Unrestricted);
    Assert(config.Contains("default_permissions = \":danger-full-access\"", StringComparison.Ordinal), "full-access permission profile was not selected");
    Assert(config.Contains("sandbox_mode = \"danger-full-access\"", StringComparison.Ordinal), "full-access sandbox mode was not selected");
    Assert(config.Contains("approval_policy = \"never\"", StringComparison.Ordinal), "approval policy changed without an explicit choice");
    Assert(!config.Contains("[windows]", StringComparison.Ordinal), "unqualified Windows sandbox backend was selected");
    Assert(config.Contains("experimental_use_profile = false", StringComparison.Ordinal), "unrestricted tools re-enabled shell profile loading");
    AssertThrows<InvalidOperationException>(() => CodexConfigBuilder.Build(
        record,
        windowsSandboxMode: "unelevated",
        toolExecutionPolicy: ToolExecutionPolicy.Unrestricted));
});

Check("approval qualification policy pins a bounded QA-only App Server profile", () =>
{
    var hasPolicy = Enum.TryParse<ToolExecutionPolicy>("ApprovalQualification", out var policy);
    Assert(hasPolicy, "the explicit approval-qualification test profile is missing");

    var record = ModelCapabilityRecord.CreateUnknown("lmstudio", "http://127.0.0.1:1234/v1", "approval-qualification-fixture");
    var config = CodexConfigBuilder.Build(record, toolExecutionPolicy: policy);
    Assert(config.Contains("sandbox_mode = \"read-only\"", StringComparison.Ordinal), "QA did not select the read-only baseline");
    Assert(config.Contains("default_permissions = \":read-only\"", StringComparison.Ordinal), "QA did not select the read-only permission profile");
    Assert(config.Contains("approval_policy = \"on-request\"", StringComparison.Ordinal), "QA did not select on-request approvals");
    Assert(!config.Contains("danger-full-access", StringComparison.Ordinal), "QA authority was widened to unrestricted");

    var threadStart = AppServerProtocol.BuildThreadStartRequest(1, new ThreadStartOptions(
        record.ModelIdentifier,
        record.ProviderId,
        "C:\\fixture",
        "read-only",
        "on-request",
        CapabilityAdapter.ToCodexConfig(record)));
    Assert(threadStart.Params["sandbox"]?.GetValue<string>() == "read-only", "QA thread did not pin read-only authority");
    Assert(threadStart.Params["approvalPolicy"]?.GetValue<string>() == "on-request", "QA thread did not pin on-request");

    var turn = AppServerProtocol.BuildTurnStartRequest(2, new TurnStartOptions("qa-thread", "fixture", policy));
    Assert(turn.Params["sandboxPolicy"]?["type"]?.GetValue<string>() == "readOnly", "QA turn did not pin readOnly");
    Assert(turn.Params["approvalPolicy"]?.GetValue<string>() == "on-request", "QA turn did not pin on-request");
    AssertThrows<InvalidOperationException>(() => AppServerProtocol.BuildThreadResumeRequest(3, "qa-thread", policy));
    AssertThrows<InvalidOperationException>(() => AppServerProtocol.BuildThreadForkRequest(4, "qa-thread", policy));
});

Check("sandbox authority reports requested and effective policy separately", () =>
{
    var config = new JsonObject
    {
        ["config"] = new JsonObject
        {
            ["sandbox_mode"] = "workspace-write",
            ["windows"] = new JsonObject { ["sandbox"] = "unelevated" }
        }
    };
    var thread = new JsonObject
    {
        ["sandbox"] = new JsonObject
        {
            ["type"] = "workspaceWrite",
            ["networkAccess"] = false
        }
    };
    var report = SandboxAuthorityDiagnostics.Build(config, thread);
    Assert(report["configSandboxMode"]?.GetValue<string>() == "workspace-write", "configured sandbox was not retained");
    Assert(report["windowsSandboxMode"]?.GetValue<string>() == "unelevated", "Windows sandbox selector was not retained");
    Assert(report["effectiveSandbox"]?["type"]?.GetValue<string>() == "workspaceWrite", "effective sandbox was not retained");
    Assert(report["downgradedByWindowsBackend"]?.GetValue<bool>() == false, "explicit restricted-token policy was incorrectly marked downgraded");
});

Check("unrestricted tools require matching effective App Server authority", () =>
{
    var config = new JsonObject
    {
        ["config"] = new JsonObject
        {
            ["sandbox_mode"] = "danger-full-access",
            ["approval_policy"] = "never"
        }
    };
    var thread = new JsonObject
    {
        ["sandbox"] = new JsonObject { ["type"] = "dangerFullAccess" },
        ["approvalPolicy"] = "never"
    };
    var report = SandboxAuthorityDiagnostics.RequireUnrestricted(config, thread);
    Assert(report["effectiveSandboxType"]?.GetValue<string>() == "dangerFullAccess", "effective authority was not retained");
    Assert(report["controllingAuthority"]?.GetValue<string>() == "Codex unrestricted execution", "unrestricted execution was mislabeled as contained");
    AssertThrows<InvalidOperationException>(() => SandboxAuthorityDiagnostics.RequireUnrestricted(
        config,
        new JsonObject
        {
            ["sandbox"] = new JsonObject { ["type"] = "readOnly" },
            ["approvalPolicy"] = "never"
        }));
    AssertThrows<InvalidOperationException>(() => SandboxAuthorityDiagnostics.RequireUnrestricted(
        new JsonObject { ["config"] = new JsonObject { ["sandbox_mode"] = "workspace-write", ["approval_policy"] = "never" } },
        thread));
    AssertThrows<InvalidOperationException>(() => SandboxAuthorityDiagnostics.RequireUnrestricted(
        config,
        new JsonObject { ["sandbox"] = new JsonObject { ["type"] = "dangerFullAccess" }, ["approvalPolicy"] = "on-request" }));
});

Check("QA approval authority requires matching read-only and on-request state", () =>
{
    var config = new JsonObject
    {
        ["config"] = new JsonObject
        {
            ["sandbox_mode"] = "read-only",
            ["approval_policy"] = "on-request"
        }
    };
    var thread = new JsonObject
    {
        ["sandbox"] = new JsonObject { ["type"] = "readOnly" },
        ["approvalPolicy"] = "on-request"
    };
    var report = SandboxAuthorityDiagnostics.RequireApprovalQualification(config, thread);
    Assert(report["effectiveSandboxType"]?.GetValue<string>() == "readOnly", "QA effective sandbox was not retained");
    Assert(report["effectiveApprovalPolicy"]?.GetValue<string>() == "on-request", "QA effective approval policy was not retained");
    Assert(report["containedToolsQualified"]?.GetValue<bool>() == false, "QA authority was mislabeled as containment-qualified");
    AssertThrows<InvalidOperationException>(() => SandboxAuthorityDiagnostics.RequireApprovalQualification(
        config,
        new JsonObject { ["sandbox"] = new JsonObject { ["type"] = "workspaceWrite" }, ["approvalPolicy"] = "on-request" }));
    AssertThrows<InvalidOperationException>(() => SandboxAuthorityDiagnostics.RequireApprovalQualification(
        config,
        new JsonObject { ["sandbox"] = new JsonObject { ["type"] = "readOnly" }, ["approvalPolicy"] = "never" }));
});

Check("unrestricted resume and every turn carry explicit authority overrides", () =>
{
    var resumed = AppServerProtocol.BuildThreadResumeRequest(4, "saved-thread", ToolExecutionPolicy.Unrestricted);
    Assert(resumed.Params["sandbox"]?.GetValue<string>() == "danger-full-access", "resume relied on persisted sandbox state");
    Assert(resumed.Params["approvalPolicy"]?.GetValue<string>() == "never", "resume relied on persisted approval state");
    var forked = AppServerProtocol.BuildThreadForkRequest(6, "saved-thread", ToolExecutionPolicy.Unrestricted);
    Assert(forked.Params["sandbox"]?.GetValue<string>() == "danger-full-access", "fork relied on inherited sandbox state");
    Assert(forked.Params["approvalPolicy"]?.GetValue<string>() == "never", "fork relied on inherited approval state");
    var turn = AppServerProtocol.BuildTurnStartRequest(5, new TurnStartOptions(
        "saved-thread", "ordinary task", ToolExecutionPolicy.Unrestricted));
    Assert(turn.Params["sandboxPolicy"]?["type"]?.GetValue<string>() == "dangerFullAccess", "turn omitted the full-access override");
    Assert(turn.Params["approvalPolicy"]?.GetValue<string>() == "never", "turn omitted the approval override");
    Assert(turn.Params["input"]?[0]?["text"]?.GetValue<string>() == "ordinary task", "turn input was altered by policy selection");
});

Check("pinned App Server confirms isolated unrestricted thread without provider inference", () =>
    RunUnrestrictedAppServerPolicyAsync().GetAwaiter().GetResult());

Check("qualification cleanup removes read-only pack files inside its exact test root", () =>
    AssertReadOnlyQualificationCleanupAsync().GetAwaiter().GetResult());
Check("qualification cleanup rejects a junction inside its exact test root", () =>
    AssertQualificationCleanupRejectsReparsePointAsync().GetAwaiter().GetResult());

Check("pinned App Server attributes partial output from a failed ordinary tool without retry or fallback", () =>
    RunUnrestrictedCommandPartialFailureAsync().GetAwaiter().GetResult());
Check("pinned App Server separately stops an identity-bound active command and reports transport uncertainty", () =>
    RunUnrestrictedCommandInterruptionAsync().GetAwaiter().GetResult());
Check("pinned App Server maps observed context into effective state and emits automatic compaction", () =>
    RunEffectiveContextCompactionQualificationAsync().GetAwaiter().GetResult());

Check("pinned App Server emits a tracked patch diff and saves patch-item history under a test-only catalog", () =>
    RunPinnedPatchDiffQualificationAsync().GetAwaiter().GetResult());
Check("pinned App Server executes function-form patch through tracked review", () =>
    RunPinnedPatchDiffQualificationAsync(patchFormat: PatchFixtureFormat.Function).GetAwaiter().GetResult());

Check("pinned App Server preserves interrupted-provider turn state without automatic replay", () =>
    RunPausedTurnRecoveryAsync().GetAwaiter().GetResult());
Check("pinned App Server repeatedly interrupts active generations and leaves no child or replay", () =>
    RunRepeatedInterruptionRecoveryAsync().GetAwaiter().GetResult());
Check("runtime supervisor switches exact model records without losing saved task history", () =>
    RunModelSwitchRebindAsync().GetAwaiter().GetResult());
Check("pinned App Server leaves an empty unlisted thread unavailable for resume after restart", () =>
    RunEmptyThreadResumeAsync().GetAwaiter().GetResult());

Check("runtime supervisor rebinds the exact saved task after App Server exit without replay", () =>
    RunExitedSupervisorClientRecoveryAsync().GetAwaiter().GetResult());
Check("project-scoped tasks resume only under their exact capability record after host restart", () =>
    RunCrossProjectRecoveryAsync().GetAwaiter().GetResult());
Check("cross-project history, turn, and switch operations preserve exact project/model identity", () =>
    RunCrossProjectOperationOrderingAsync().GetAwaiter().GetResult());
Check("saved task rejects changed context or newly Known cap while retaining Unknown-cap compatibility", () =>
    RunContextChangeInvalidationAsync().GetAwaiter().GetResult());

Check("protocol uses named operations and rejects full-host shell authority", () =>
{
    var request = AppServerProtocol.BuildThreadStartRequest(
        7,
        new ThreadStartOptions(
            "qwen/qwen3-14b",
            "lmstudio",
            "C:\\fixture",
            "workspace-write",
            "never",
            new Dictionary<string, object> { ["model_context_window"] = 32768 }));
    Assert(request.Method == "thread/start", "wrong method");
    Assert(request.Params["model"]?.GetValue<string>() == "qwen/qwen3-14b", "model was not pinned");
    Assert(request.Params["modelProvider"]?.GetValue<string>() == "lmstudio", "provider was not pinned");
    Assert(request.Params["config"] is null, "ignored thread config override was sent");
    var configRead = AppServerProtocol.BuildConfigReadRequest(8, "C:\\fixture");
    Assert(configRead.Method == "config/read" && configRead.Params["cwd"]?.GetValue<string>() == "C:\\fixture", "effective config read was not prepared");
    Assert(AppServerProtocol.IsNamedHostOperation("getRuntimeStatus"), "named host operation missing");
    var interrupt = AppServerProtocol.BuildTurnInterruptRequest(9, "thread-1", "turn-1");
    Assert(interrupt.Method == "turn/interrupt", "interrupt operation used the wrong App Server method");
    Assert(interrupt.Params["threadId"]?.GetValue<string>() == "thread-1", "interrupt thread id was not preserved");
    Assert(interrupt.Params["turnId"]?.GetValue<string>() == "turn-1", "interrupt turn id was not preserved");
    Assert(AppServerProtocol.IsNamedHostOperation("interruptTurn"), "named interrupt host operation missing");
    var commandList = AppServerProtocol.BuildCommandExecutionListRequest(14, "thread-1");
    Assert(commandList.Method == "thread/commandExecution/list"
        && commandList.Params["threadId"]?.GetValue<string>() == "thread-1",
        "active command listing was not scoped to the exact thread");
    var commandStop = AppServerProtocol.BuildCommandExecutionStopRequest(
        15, "thread-1", "call-exact", "1000");
    Assert(commandStop.Method == "thread/commandExecution/stop"
        && commandStop.Params["threadId"]?.GetValue<string>() == "thread-1"
        && commandStop.Params["itemId"]?.GetValue<string>() == "call-exact"
        && commandStop.Params["expectedProcessId"]?.GetValue<string>() == "1000",
        "command stop did not bind the exact App Server item and observed process identity");
    Assert(AppServerProtocol.IsNamedHostOperation("stopCommand"), "named command stop operation missing");
    var uiCommandStop = DiagnosticOperation.Parse(
        "{\"operation\":\"stopCommand\",\"requestId\":\"stop-exact\",\"itemId\":\"call-exact\"}");
    Assert(uiCommandStop.Name == "stopCommand" && uiCommandStop.Payload["itemId"]?.GetValue<string>() == "call-exact"
        && uiCommandStop.Payload["processId"] is null && uiCommandStop.Payload["expectedProcessId"] is null,
        "renderer supplied process identity instead of the host binding it to its observed command");
    var listThreads = AppServerProtocol.BuildThreadListRequest(10);
    Assert(listThreads.Method == "thread/list", "thread history did not use the pinned App Server method");
    Assert(listThreads.Params["limit"]?.GetValue<int>() == 50, "thread history page is not bounded");
    Assert(listThreads.Params["archived"]?.GetValue<bool>() == false, "archived thread policy was not explicit");
    Assert(listThreads.Params["modelProviders"] is JsonArray { Count: 0 }, "history defaulted to the current provider instead of all providers");
    var stateDbThreads = AppServerProtocol.BuildThreadListRequest(101, useStateDbOnly: true);
    Assert(stateDbThreads.Params["useStateDbOnly"]?.GetValue<bool>() == true, "state DB-only thread listing was not explicit");
    Assert(stateDbThreads.Params["modelProviders"] is JsonArray { Count: 0 }, "state DB history defaulted to the current provider");
    var olderProjectThreads = AppServerProtocol.BuildThreadListRequest(103, cursor: "opaque-next", cwd: "D:\\QA\\Project");
    Assert(olderProjectThreads.Params["cursor"]?.GetValue<string>() == "opaque-next", "older saved-history page cursor was lost");
    Assert(olderProjectThreads.Params["cwd"]?.GetValue<string>() == "D:\\QA\\Project", "saved-history page was not scoped to the selected project");
    Assert(olderProjectThreads.Params["limit"]?.GetValue<int>() == 50, "saved-history pagination lost its bounded page size");
    var metadataRead = AppServerProtocol.BuildThreadReadRequest(102, "thread-fork", includeTurns: false);
    Assert(metadataRead.Method == "thread/read", "branch metadata did not use the pinned App Server method");
    Assert(metadataRead.Params["threadId"]?.GetValue<string>() == "thread-fork", "branch metadata read used a different thread id");
    Assert(metadataRead.Params["includeTurns"]?.GetValue<bool>() == false, "branch discovery requested conversation history");
    var resumeThread = AppServerProtocol.BuildThreadResumeRequest(11, "thread-exact");
    Assert(resumeThread.Method == "thread/resume", "thread resume used the wrong App Server method");
    Assert(resumeThread.Params["threadId"]?.GetValue<string>() == "thread-exact", "thread resume identity was not pinned");
    Assert(resumeThread.Params["model"] is null && resumeThread.Params["modelProvider"] is null, "resume silently overrode persisted model identity");
    Assert(resumeThread.Params["excludeTurns"]?.GetValue<bool>() == true, "thread resume requested unbounded history");
    var turns = AppServerProtocol.BuildThreadTurnsListRequest(12, "thread-exact");
    Assert(turns.Method == "thread/turns/list" && turns.Params["limit"]?.GetValue<int>() == 20, "thread transcript page is not bounded");
    Assert(turns.Params["itemsView"]?.GetValue<string>() == "full", "thread transcript did not request renderable message items");
    Assert(AppServerProtocol.IsNamedHostOperation("forkThread"), "named thread fork operation missing");
    var fork = AppServerProtocol.BuildThreadForkRequest(13, "thread-exact");
    Assert(fork.Method == "thread/fork", "thread fork used the wrong App Server method");
    Assert(fork.Params["threadId"]?.GetValue<string>() == "thread-exact", "thread fork identity was not pinned");
    Assert(fork.Params["excludeTurns"]?.GetValue<bool>() == true, "thread fork did not use the bounded transcript path");
    Assert(fork.Params.Count == 2, "thread fork overrode source provider, model, workspace, or authority");
    Assert(AppServerProtocol.IsNamedHostOperation("listThreads"), "named thread listing operation missing");
    Assert(AppServerProtocol.IsNamedHostOperation("resumeThread"), "named thread resume operation missing");
    Assert(AppServerProtocol.IsNamedHostOperation("newTask"), "named new-task operation missing");
    Assert(!AppServerProtocol.IsNamedHostOperation("thread/shellCommand"), "full-host shell operation exposed");
});

Check("thread list reconciliation preserves scan order and adds persisted fork-only entries", () =>
{
    var scanned = new JsonArray
    {
        new JsonObject { ["id"] = "parent", ["preview"] = "original preview" },
        new JsonObject { ["id"] = "duplicate", ["preview"] = "scan-and-repair copy" },
        new JsonObject { ["id"] = "fork", ["preview"] = "branch's first prompt", ["forkedFromId"] = null }
    };
    var indexed = new JsonArray
    {
        new JsonObject { ["id"] = "duplicate", ["preview"] = "state-only copy" },
        new JsonObject { ["id"] = "fork", ["preview"] = "", ["forkedFromId"] = "parent" }
    };

    var merged = ThreadListProjector.MergeScanAndStateDb(scanned, indexed);
    Assert(merged.Count == 3, "the fork was not represented exactly once");
    Assert(merged[0]?["id"]?.GetValue<string>() == "parent", "scan order was not preserved");
    Assert(merged[1]?["preview"]?.GetValue<string>() == "scan-and-repair copy", "scan-and-repair metadata did not take precedence");
    Assert(merged[2]?["id"]?.GetValue<string>() == "fork", "scan order was not preserved for the fork");
    Assert(merged[2]?["preview"]?.GetValue<string>() == "branch's first prompt", "the scan preview was not preserved");
    Assert(merged[2]?["forkedFromId"]?.GetValue<string>() == "parent", "verified branch ancestry did not fill missing scan metadata");
    Assert(indexed.Count == 2, "merging mutated the App Server response");
});

Check("thread history merge sorts scan and state-db entries newest first", () =>
{
    var scanned = new JsonArray
    {
        new JsonObject { ["id"] = "scan-old", ["preview"] = "Older scan", ["updatedAt"] = "2026-09-24T08:00:00.000Z" },
        new JsonObject { ["id"] = "scan-new", ["preview"] = "Newest scan", ["updatedAt"] = "2026-09-24T12:00:00.000Z" },
        new JsonObject { ["id"] = "overlap", ["preview"] = "Scan metadata", ["updatedAt"] = "2026-09-24T11:00:00.000Z" }
    };
    var indexed = new JsonArray
    {
        new JsonObject { ["id"] = "index-middle", ["preview"] = "Middle index", ["updatedAt"] = "2026-09-24T10:00:00.000Z" },
        new JsonObject { ["id"] = "overlap", ["preview"] = "Index metadata", ["updatedAt"] = "2026-09-24T11:00:00.000Z" },
        new JsonObject { ["id"] = "index-unknown", ["preview"] = "No timestamp" }
    };

    var merged = ThreadListProjector.MergeScanAndStateDb(scanned, indexed);
    var ids = merged.OfType<JsonObject>().Select(thread => thread["id"]?.GetValue<string>()).ToArray();
    Assert(ids.SequenceEqual(["scan-new", "overlap", "index-middle", "scan-old", "index-unknown"]),
        $"scan and state-db history was not globally sorted: {string.Join(", ", ids)}");
    Assert(merged[1]?["preview"]?.GetValue<string>() == "Scan metadata",
        "sorting changed scan-and-repair precedence for duplicate thread metadata");
});

Check("fork bookmarks persist navigation identity without conversation content", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Fork-Bookmark-Tests", Guid.NewGuid().ToString("N"));
    var path = Path.Combine(root, "Data", "NeoBabylon", "fork-bookmarks.json");
    var bookmark = new ForkBookmark(
        "thread-fork",
        "thread-parent",
        "lmstudio",
        "phase1a-qwen3-14b",
        Path.Combine(root, "Workspace"),
        DateTimeOffset.Parse("2026-09-23T08:00:00Z"));

    ForkBookmarkStore.Record(path, bookmark);
    ForkBookmarkStore.Record(path, bookmark);
    var loaded = ForkBookmarkStore.Read(path);
    Assert(loaded.Count == 1, "re-recording one fork created duplicate bookmarks");
    Assert(loaded[0] == bookmark, "fork navigation identity did not round-trip exactly");
    var json = File.ReadAllText(path);
    Assert(json.Contains("\"schemaVersion\": 1", StringComparison.Ordinal), "bookmark schema version was not explicit");
    Assert(!json.Contains("turns", StringComparison.OrdinalIgnoreCase), "bookmark duplicated App Server conversation history");
});

Check("fork bookmark exposure requires an exact App Server identity", () =>
{
    var workspace = Path.Combine(Path.GetTempPath(), "NeoBabylon-Bookmark-Workspace");
    var bookmark = new ForkBookmark(
        "thread-fork",
        "thread-parent",
        "lmstudio",
        "phase1a-qwen3-14b",
        workspace,
        DateTimeOffset.Parse("2026-09-23T08:00:00Z"));
    var thread = new JsonObject
    {
        ["id"] = "thread-fork",
        ["forkedFromId"] = "thread-parent",
        ["modelProvider"] = "lmstudio",
        ["model"] = "phase1a-qwen3-14b",
        ["cwd"] = workspace
    };

    ForkBookmarkIdentity.RequireExactMatch(thread, bookmark);
    var wrongParent = (JsonObject)thread.DeepClone();
    wrongParent["forkedFromId"] = "another-parent";
    AssertThrows<InvalidOperationException>(() => ForkBookmarkIdentity.RequireExactMatch(wrongParent, bookmark));
    var wrongModel = (JsonObject)thread.DeepClone();
    wrongModel["model"] = "Unknown";
    AssertThrows<InvalidOperationException>(() => ForkBookmarkIdentity.RequireExactMatch(wrongModel, bookmark));
});

Check("thread resume requires an exact provider/model and isolated workspace match", () =>
{
    var capability = ModelCapabilityRecord.CreateUnknown("lmstudio", "http://127.0.0.1:1234/v1", "phase1a-qwen3-14b");
    var cwd = Path.Combine(Path.GetTempPath(), "NeoBabylon-Isolated-Workspace");
    var listedThread = new JsonObject
    {
        ["modelProvider"] = "lmstudio",
        ["model"] = "phase1a-qwen3-14b",
        ["cwd"] = cwd
    };
    ThreadResumeIdentity.RequireExactMatch(listedThread, capability, cwd);
    var readThread = (JsonObject)listedThread.DeepClone();
    readThread["id"] = "older-thread";
    ThreadResumeIdentity.RequireExactRead(readThread, "older-thread", capability, cwd);
    AssertThrows<InvalidOperationException>(() => ThreadResumeIdentity.RequireExactRead(readThread, "another-thread", capability, cwd));
    AssertThrows<InvalidOperationException>(() => ThreadResumeIdentity.RequireExactRead(null, "older-thread", capability, cwd));
    AssertThrows<InvalidOperationException>(() => ThreadResumeIdentity.RequireExactMatch(
        new JsonObject { ["modelProvider"] = "openrouter", ["model"] = capability.ModelIdentifier, ["cwd"] = cwd },
        capability,
        cwd));
    AssertThrows<InvalidOperationException>(() => ThreadResumeIdentity.RequireExactMatch(
        new JsonObject { ["modelProvider"] = capability.ProviderId, ["model"] = "Unknown", ["cwd"] = cwd },
        capability,
        cwd));
    AssertThrows<InvalidOperationException>(() => ThreadResumeIdentity.RequireExactMatch(
        new JsonObject { ["modelProvider"] = capability.ProviderId, ["model"] = capability.ModelIdentifier, ["cwd"] = Path.GetTempPath() },
        capability,
        cwd));
});

Check("saved-history pagination binds both upstream cursors to the selected identity", () =>
{
    var workspace = Path.Combine(Path.GetTempPath(), "NeoBabylon-History-Project");
    var token = ThreadHistoryCursor.CreateNext(workspace, "lmstudio", "phase1a-qwen3-14b", "scan-next", "db-next");
    Assert(token is not null, "a nonterminal history page lost its continuation");
    var decoded = ThreadHistoryCursor.Decode(token!, workspace, "lmstudio", "phase1a-qwen3-14b");
    Assert(decoded.ScanCursor == "scan-next" && decoded.StateDbCursor == "db-next", "independent history streams were conflated");
    Assert(ThreadHistoryCursor.CreateNext(workspace, "lmstudio", "phase1a-qwen3-14b", null, null) is null,
        "terminal history pages still exposed a continuation");
    AssertThrows<InvalidOperationException>(() => ThreadHistoryCursor.Decode(token!, Path.GetTempPath(), "lmstudio", "phase1a-qwen3-14b"));
    AssertThrows<InvalidOperationException>(() => ThreadHistoryCursor.Decode(token!, workspace, "openrouter", "phase1a-qwen3-14b"));
    AssertThrows<InvalidOperationException>(() => ThreadHistoryCursor.Decode(token!, workspace, "lmstudio", "other-model"));
    AssertThrows<InvalidOperationException>(() => ThreadHistoryCursor.Decode("not-a-valid-cursor", workspace, "lmstudio", "phase1a-qwen3-14b"));
});

Check("thread fork accepts only a new exact provider/model/workspace identity", () =>
{
    const string sourceThreadId = "thread-source";
    var capability = ModelCapabilityRecord.CreateUnknown("lmstudio", "http://127.0.0.1:1234/v1", "phase1a-qwen3-14b");
    var workspace = Path.Combine(Path.GetTempPath(), "NeoBabylon-Fork-Workspace");
    var helperType = typeof(ModelCapabilityRecord).Assembly.GetType("NeoBabylon.Core.ThreadForkIdentity");
    Assert(helperType is not null, "thread fork identity validator is missing");
    var helper = helperType!.GetMethod("RequireNewExactMatch");
    Assert(helper is not null, "thread fork identity validation method is missing");

    string RequireNewThread(JsonObject forkResult)
    {
        try
        {
            return (string)(helper!.Invoke(null, [forkResult, sourceThreadId, capability, workspace])
                ?? throw new InvalidOperationException("thread fork identity validator returned no identifier"));
        }
        catch (System.Reflection.TargetInvocationException exception)
            when (exception.InnerException is InvalidOperationException invalidOperation)
        {
            throw invalidOperation;
        }
    }

    JsonObject ForkResult(string id, string provider, string model, string cwd) => new()
    {
        ["thread"] = new JsonObject { ["id"] = id },
        ["modelProvider"] = provider,
        ["model"] = model,
        ["cwd"] = cwd
    };

    Assert(RequireNewThread(ForkResult("thread-forked", "lmstudio", "phase1a-qwen3-14b", workspace)) == "thread-forked",
        "forked thread identifier was not returned");
    AssertThrows<InvalidOperationException>(() => RequireNewThread(ForkResult(sourceThreadId, "lmstudio", "phase1a-qwen3-14b", workspace)));
    AssertThrows<InvalidOperationException>(() => RequireNewThread(ForkResult("thread-forked", "openrouter", "phase1a-qwen3-14b", workspace)));
    AssertThrows<InvalidOperationException>(() => RequireNewThread(ForkResult("thread-forked", "lmstudio", "Unknown", workspace)));
    AssertThrows<InvalidOperationException>(() => RequireNewThread(ForkResult("thread-forked", "lmstudio", "phase1a-qwen3-14b", Path.GetTempPath())));
    AssertThrows<InvalidOperationException>(() => RequireNewThread(ForkResult("", "lmstudio", "phase1a-qwen3-14b", workspace)));
});

Check("saved-thread transcript projection excludes tool payloads and caps rendered history", () =>
{
    var source = JsonNode.Parse("""
    [
      {"id":"turn-newest","items":[
        {"id":"user-1","type":"userMessage","content":[{"type":"text","text":"Hello"},{"type":"image","url":"local-image"}]},
        {"id":"agent-1","type":"agentMessage","text":"World"},
        {"id":"tool-1","type":"commandExecution","command":"private command","aggregatedOutput":"private output"}
      ]}
    ]
    """)!.AsArray();
    var projected = ThreadTranscriptProjector.Project(source, 100);
    var json = projected.Turns.ToJsonString();
    Assert(!projected.Truncated, "short transcript was unexpectedly truncated");
    Assert(json.Contains("Hello", StringComparison.Ordinal) && json.Contains("World", StringComparison.Ordinal), "user/assistant text was not projected");
    Assert(json.Contains("hasNonTextContent", StringComparison.Ordinal), "unsupported attachment presence was hidden");
    Assert(!json.Contains("private command", StringComparison.Ordinal) && !json.Contains("private output", StringComparison.Ordinal), "tool details crossed the trusted host boundary");

    var capped = ThreadTranscriptProjector.Project(source, 5);
    Assert(capped.Truncated, "oversized transcript was not marked as truncated");
    var cappedItems = capped.Turns[0]?["items"] as JsonArray;
    var displayedCharacters = cappedItems?.OfType<JsonObject>()
        .Sum(item => item["text"]?.GetValue<string>()?.Length ?? 0) ?? int.MaxValue;
    Assert(displayedCharacters <= 5, "projected history exceeded its text display budget");
    Assert(cappedItems?[1]?["neoBabylonDisplay"]?["omittedCharacters"]?.GetValue<int>() == 5,
        "saved assistant output lost its exact omitted-character metadata");
});

Check("saved transcript preserves latest persisted turn outcome without tool data", () =>
{
    var source = JsonNode.Parse("""
    [
      {"id":"turn-newest","status":"interrupted","items":[]},
      {"id":"turn-older","status":"completed","items":[
        {"id":"user-older","type":"userMessage","content":[{"type":"text","text":"Earlier request"}]},
        {"id":"tool-older","type":"commandExecution","aggregatedOutput":"private output"}
      ]}
    ]
    """)!.AsArray();
    var projected = ThreadTranscriptProjector.Project(source, 100);
    Assert(projected.Turns[0]?["status"]?.GetValue<string>() == "interrupted",
        "an itemless latest interrupted turn was mistaken for an older completed turn");
    Assert(projected.Turns[1]?["status"]?.GetValue<string>() == "completed",
        "the older completed turn lost its status");
    Assert(!projected.Turns.ToJsonString().Contains("private output", StringComparison.Ordinal),
        "tool data crossed the host transcript boundary");
});

Check("saved transcript detects only the pinned App Server omission marker", () =>
{
    var source = JsonNode.Parse("""
    [{"id":"turn-markers","items":[
      {"id":"assistant-exact","type":"agentMessage","text":"head\n... 7 bytes omitted ...\ntail"},
      {"id":"assistant-loose","type":"agentMessage","text":"head ... arbitrary bytes omitted ... tail"}
    ]}]
    """)!.AsArray();
    var projected = ThreadTranscriptProjector.Project(source, 20_000);
    var items = projected.Turns[0]!["items"]!.AsArray();
    Assert(items[0]?["neoBabylonDisplay"]?["upstreamTruncated"]?.GetValue<bool>() == true,
        "the exact pinned omission marker was not identified");
    Assert(items[1]?["neoBabylonDisplay"] is null,
        "a loose non-App-Server omission phrase was reported as upstream truncation");
});

Check("saved transcript retains bounded readable reasoning items with turn identity", () =>
{
    var source = JsonNode.Parse("""
    [{"id":"turn-reasoning","items":[
      {"id":"reasoning-content","type":"reasoning","summary":["summary"],"content":[" first\n","second "],"encryptedContent":"opaque"},
      {"id":"reasoning-summary","type":"reasoning","summary":["visible summary"],"content":[],"encryptedContent":"opaque"},
      {"id":"reasoning-empty","type":"reasoning","summary":[],"content":[],"encryptedContent":"opaque"},
      {"id":"assistant-1","type":"agentMessage","text":"answer"}
    ]}]
    """)!.AsArray();
    var projected = ThreadTranscriptProjector.Project(source, 100);
    var items = projected.Turns[0]!["items"]!.AsArray();
    Assert(items.Count == 3, "saved transcript did not filter empty reasoning while retaining answer");
    Assert(items[0]?["type"]?.GetValue<string>() == "reasoning"
        && items[0]?["content"]?[0]?.GetValue<string>() == " first\n"
        && items[0]?["id"]?.GetValue<string>() == "reasoning-content",
        "readable reasoning content or its exact item identity was altered");
    Assert(items[1]?["summary"]?.AsArray().Count == 1 && items[1]?["content"]?.AsArray().Count == 0,
        "summary-only reasoning was not retained in the selected summary channel");
    Assert(!projected.Turns.ToJsonString().Contains("opaque", StringComparison.Ordinal),
        "encrypted reasoning crossed the saved transcript boundary");

    var manyParts = new JsonArray(Enumerable.Range(0, 1_000)
        .Select(_ => (JsonNode?)JsonValue.Create("")).Append(JsonValue.Create("hidden tail")).ToArray());
    var oversizedParts = new JsonArray(new JsonObject
    {
        ["id"] = "turn-many-parts",
        ["items"] = new JsonArray(new JsonObject
        {
            ["id"] = "reasoning-many-parts",
            ["type"] = "reasoning",
            ["summary"] = new JsonArray(),
            ["content"] = manyParts
        })
    });
    var omitted = ThreadTranscriptProjector.Project(oversizedParts, 100);
    Assert(omitted.Truncated && omitted.Turns.Count == 0,
        "saved transcript copied unbounded reasoning parts or created a fake empty section");
});

Check("live output projection bounds assistant and tool payloads with explicit omission counts", () =>
{
    var projector = new AppServerNotificationProjection();
    var assistantText = new string('a', AppServerNotificationProjection.AssistantDisplayLimit + 7);
    var assistant = projector.Project(new AppServerNotification("item/completed", new JsonObject
    {
        ["item"] = new JsonObject
        {
            ["id"] = "assistant-large",
            ["type"] = "agentMessage",
            ["text"] = assistantText
        }
    }));
    Assert(assistant is not null, "assistant completion was dropped");
    Assert(assistant!.Params["item"]?["text"]?.GetValue<string>()?.Length == AppServerNotificationProjection.AssistantDisplayLimit,
        "assistant completion crossed its display budget");
    Assert(assistant.Params["item"]?["neoBabylonDisplay"]?["omittedCharacters"]?.GetValue<int>() == 7,
        "assistant omission count was not exact");

    var toolOutput = new string('t', AppServerNotificationProjection.ToolOutputDisplayLimit + 11);
    var tool = projector.Project(new AppServerNotification("item/completed", new JsonObject
    {
        ["item"] = new JsonObject
        {
            ["id"] = "tool-large",
            ["type"] = "commandExecution",
            ["status"] = "failed",
            ["aggregatedOutput"] = toolOutput
        }
    }));
    Assert(tool?.Params["item"]?["aggregatedOutput"]?.GetValue<string>()?.Length == AppServerNotificationProjection.ToolOutputDisplayLimit,
        "tool output crossed its display budget");
    Assert(tool?.Params["item"]?["status"]?.GetValue<string>() == "failed",
        "output projection erased the tool failure outcome");
    Assert(tool?.Params["item"]?["neoBabylonDisplay"]?["omittedCharacters"]?.GetValue<int>() == 11,
        "tool output omission count was not exact");

    var compaction = projector.Project(new AppServerNotification("item/completed", new JsonObject
    {
        ["threadId"] = "thread-1",
        ["turnId"] = "turn-1",
        ["item"] = new JsonObject
        {
            ["id"] = "compaction-1",
            ["type"] = "contextCompaction",
            ["status"] = "completed",
            ["summary"] = "large private summary payload"
        }
    }));
    Assert(compaction?.Params["item"]?["id"]?.GetValue<string>() == "compaction-1"
        && compaction.Params["item"]?["status"]?.GetValue<string>() == "completed"
        && compaction.Params["item"]?["summary"] is null,
        "context-compaction completion identity was dropped or its payload was not minimized");

    var streamedProjector = new AppServerNotificationProjection();
    var delta = streamedProjector.Project(new AppServerNotification("item/agentMessage/delta", new JsonObject
    {
        ["itemId"] = "assistant-partial",
        ["delta"] = new string('p', AppServerNotificationProjection.AssistantDisplayLimit + 5)
    }));
    Assert(delta?.Params["delta"]?.GetValue<string>()?.Length == AppServerNotificationProjection.AssistantDisplayLimit,
        "assistant deltas were not bounded before reaching the renderer");
    Assert(streamedProjector.Project(new AppServerNotification("item/agentMessage/delta", new JsonObject
    {
        ["itemId"] = "assistant-partial",
        ["delta"] = "tail"
    })) is null, "overflow assistant text was forwarded after reaching the display budget");
    var terminalSummary = streamedProjector.Complete(turnCompleted: false).Single();
    Assert(terminalSummary.Method == "neobabylon/outputTruncated"
        && terminalSummary.Params["neoBabylonDisplay"]?["omittedCharacters"]?.GetValue<int>() == 9
        && terminalSummary.Params["neoBabylonDisplay"]?["sourceRetained"]?.GetValue<bool>() == false,
        "terminal partial-output metadata did not report the exact omission or persistence state");
});

Check("live reasoning projection preserves notification identity, indexed text, bounds, and completion authority", () =>
{
    var projector = new AppServerNotificationProjection();
    var delta = projector.Project(new AppServerNotification("item/reasoning/summaryTextDelta", new JsonObject
    {
        ["threadId"] = "thread-reasoning",
        ["turnId"] = "turn-reasoning",
        ["itemId"] = "reasoning-live",
        ["summaryIndex"] = 3,
        ["delta"] = "  summary\\n"
    }));
    Assert(delta?.Params["threadId"]?.GetValue<string>() == "thread-reasoning"
        && delta.Params["turnId"]?.GetValue<string>() == "turn-reasoning"
        && delta.Params["itemId"]?.GetValue<string>() == "reasoning-live"
        && delta.Params["summaryIndex"]?.GetValue<int>() == 3
        && delta.Params["delta"]?.GetValue<string>() == "  summary\\n",
        "reasoning delta identity, indexed field, or whitespace changed");

    var completion = projector.Project(new AppServerNotification("item/completed", new JsonObject
    {
        ["threadId"] = "thread-reasoning",
        ["turnId"] = "turn-reasoning",
        ["item"] = new JsonObject
        {
            ["id"] = "reasoning-live",
            ["type"] = "reasoning",
            ["summary"] = new JsonArray(new string('s', 120_000)),
            ["content"] = new JsonArray(new string('c', 120_007)),
            ["encryptedContent"] = "opaque secret payload"
        }
    }));
    Assert(completion?.Params["item"]?["content"]?[0]?.GetValue<string>()?.Length
            == AppServerNotificationProjection.AssistantDisplayLimit,
        "completed reasoning crossed the existing bounded display budget");
    Assert(completion!.Params["item"]?["neoBabylonDisplay"]?["omittedCharacters"]?.GetValue<int>() == 7
        && completion.Params["item"]?["neoBabylonDisplay"]?["sourceRetained"]?.GetValue<bool>() == true,
        "completed reasoning omission notice did not report exact retained source");
    Assert(!completion.Params.ToJsonString().Contains("opaque secret payload", StringComparison.Ordinal),
        "opaque encrypted reasoning crossed the UI projection");
    Assert(projector.Project(new AppServerNotification("item/reasoning/textDelta", new JsonObject
        {
            ["threadId"] = "thread-reasoning",
            ["turnId"] = "turn-reasoning",
            ["itemId"] = "reasoning-live",
            ["contentIndex"] = 0,
            ["delta"] = "stale trailing delta"
        })) is null,
        "a late streamed delta was allowed to override completed reasoning");

    var manyParts = projector.Project(new AppServerNotification("item/completed", new JsonObject
    {
        ["threadId"] = "thread-reasoning",
        ["turnId"] = "turn-many-parts",
        ["item"] = new JsonObject
        {
            ["id"] = "reasoning-many-parts",
            ["type"] = "reasoning",
            ["summary"] = new JsonArray(),
            ["content"] = new JsonArray(Enumerable.Range(0, 1_000)
                .Select(_ => (JsonNode?)JsonValue.Create("")).ToArray())
        }
    }));
    Assert(manyParts?.Params["item"]?["content"]?.AsArray().Count == 128
        && manyParts.Params["item"]?["neoBabylonDisplay"]?["omittedParts"]?.GetValue<bool>() == true,
        "reasoning part-count bounds or the omission notice were missing");

    var overflow = new AppServerNotificationProjection();
    Assert(overflow.Project(new AppServerNotification("item/reasoning/textDelta", new JsonObject
        {
            ["threadId"] = "thread-overflow",
            ["turnId"] = "turn-overflow",
            ["itemId"] = "reasoning-overflow",
            ["contentIndex"] = 0,
            ["delta"] = new string('x', AppServerNotificationProjection.AssistantDisplayLimit)
        })) is not null,
        "the exact-limit reasoning chunk was not displayed");
    Assert(overflow.Project(new AppServerNotification("item/reasoning/textDelta", new JsonObject
        {
            ["threadId"] = "thread-overflow",
            ["turnId"] = "turn-overflow",
            ["itemId"] = "reasoning-overflow",
            ["contentIndex"] = 0,
            ["delta"] = "omitted"
        })) is null,
        "a fully omitted reasoning chunk crossed the display limit");
    var omitted = overflow.Complete(turnCompleted: false).Single(notification =>
        notification.Method == "neobabylon/reasoningTruncated");
    Assert(omitted.Params["threadId"]?.GetValue<string>() == "thread-overflow"
        && omitted.Params["turnId"]?.GetValue<string>() == "turn-overflow"
        && omitted.Params["itemId"]?.GetValue<string>() == "reasoning-overflow"
        && omitted.Params["neoBabylonDisplay"]?["omittedCharacters"]?.GetValue<int>() == 7
        && omitted.Params["neoBabylonDisplay"]?["sourceRetained"]?.GetValue<bool>() == false,
        "fully omitted reasoning did not produce exact identity-bound retention evidence");
});

Check("saved App Server item output exposes bounded, identity-checked ranges", () =>
{
    var page = JsonNode.Parse("""
    {"data":[
      {"turnId":"turn-1","item":{"id":"assistant-1","type":"agentMessage","text":"abcdefghij"}},
      {"turnId":"turn-1","item":{"id":"tool-1","type":"commandExecution","aggregatedOutput":"0123456789"}},
      {"turnId":"turn-1","item":{"id":"reasoning-1","type":"reasoning","content":["first ","second"],"summary":["unused summary"]}}
    ],"nextCursor":null}
    """)!.AsObject();
    var assistant = ThreadItemOutputRangeProjector.Project(page, "thread-1", "turn-1", "assistant-1", offset: 3, maximumCharacters: 4);
    Assert(assistant.Text == "defg" && assistant.TotalCharacters == 10 && assistant.NextOffset == 7 && assistant.HasMore,
        "assistant item range was not bounded or positioned exactly");
    var tool = ThreadItemOutputRangeProjector.Project(page, "thread-1", "turn-1", "tool-1", offset: 6, maximumCharacters: 10);
    Assert(tool.Text == "6789" && !tool.HasMore, "tool output tail range was not reconstructed exactly");
    var reasoning = ThreadItemOutputRangeProjector.Project(page, "thread-1", "turn-1", "reasoning-1", offset: 6, maximumCharacters: 4);
    Assert(reasoning.ThreadId == "thread-1" && reasoning.TurnId == "turn-1"
        && reasoning.ItemType == "reasoning" && reasoning.Text == "seco"
        && reasoning.TotalCharacters == 12 && reasoning.NextOffset == 10 && reasoning.HasMore,
        "reasoning inspection did not select ordered content or preserve its reasoning item type");
    var summaryPage = JsonNode.Parse("""
    [{"turnId":"turn-1","item":{"id":"reasoning-summary","type":"reasoning","content":[],"summary":["summary ","only"]}}]
    """)!.AsArray();
    var summary = ThreadItemOutputRangeProjector.TryProject(summaryPage, "thread-1", "turn-1", "reasoning-summary", 0, 32);
    Assert(summary?.Text == "summary only" && summary.ItemType == "reasoning",
        "summary-only reasoning was not available through the bounded range projector");
    var excessiveParts = new JsonArray(Enumerable.Range(0, 129).Select(_ => (JsonNode?)JsonValue.Create("")).ToArray());
    excessiveParts[128] = JsonValue.Create("hidden");
    var boundedParts = JsonNode.Parse("""[{"turnId":"turn-1","item":{"id":"reasoning-many","type":"reasoning","content":[],"summary":[]}}]""")!.AsArray();
    ((JsonObject)boundedParts[0]!["item"]!)!["content"] = excessiveParts;
    var cappedParts = ThreadItemOutputRangeProjector.Project(
        new JsonObject { ["data"] = boundedParts }, "thread-1", "turn-1", "reasoning-many", 0, 10);
    Assert(cappedParts.Text.Length == 0 && cappedParts.OmittedParts,
        "reasoning range did not truthfully mark parts omitted by its bounded projection");
    AssertThrows<InvalidDataException>(() => ThreadItemOutputRangeProjector.Project(page, "thread-1", "other-turn", "assistant-1", 0, 10));
    AssertThrows<ArgumentOutOfRangeException>(() => ThreadItemOutputRangeProjector.Project(page, "thread-1", "turn-1", "assistant-1", 0, 0));
});

Check("saved tool outputs are restored with a bounded preview and the correct source markers", () =>
{
    var source = JsonNode.Parse($$"""
    [{"id":"turn-output","items":[
      {"id":"tool-large","type":"commandExecution","status":"completed","command":"cmd /c ver","aggregatedOutput":"{{new string('o', AppServerNotificationProjection.ToolOutputDisplayLimit + 33)}}"},
      {"id":"tool-upstream","type":"commandExecution","status":"completed","aggregatedOutput":"head\n... 7 bytes omitted ...\ntail"}
    ]}]
    """)!.AsArray();
    var projected = ThreadSavedOutputProjector.Project(source);
    Assert(projected.Outputs.Count == 2 && !projected.Truncated,
        "saved tool outcomes were not included in history reconstruction");
    Assert(projected.Outputs[0]?["itemId"]?.GetValue<string>() == "tool-large"
        && projected.Outputs[0]?["turnId"]?.GetValue<string>() == "turn-output"
        && projected.Outputs[0]?["text"]?.GetValue<string>()?.Length == AppServerNotificationProjection.ToolOutputDisplayLimit,
        "saved tool output was not identity-bound and bounded");
    Assert(projected.Outputs[0]?["neoBabylonDisplay"]?["omittedCharacters"]?.GetValue<int>() == 33
        && projected.Outputs[0]?["neoBabylonDisplay"]?["sourceRetained"]?.GetValue<bool>() == true,
        "saved output lost its exact display omission count");
    Assert(projected.Outputs[1]?["neoBabylonDisplay"]?["upstreamTruncated"]?.GetValue<bool>() == true,
        "an upstream App Server omission marker was hidden");

    var turns = new JsonArray();
    for (var turnIndex = 39; turnIndex >= 0; turnIndex--)
    {
        turns.Add(new JsonObject
        {
            ["id"] = $"turn-{turnIndex:D2}",
            ["items"] = new JsonArray
            {
                new JsonObject { ["id"] = $"tool-{turnIndex:D2}-0", ["type"] = "commandExecution", ["status"] = "completed", ["output"] = "old" },
                new JsonObject { ["id"] = $"tool-{turnIndex:D2}-1", ["type"] = "commandExecution", ["status"] = "completed", ["output"] = "new" }
            }
        });
    }
    var capped = ThreadSavedOutputProjector.Project(turns);
    Assert(capped.Truncated && capped.Outputs.Count == 64,
        "saved activity count cap was not explicit");
    Assert(capped.Outputs[0]?["itemId"]?.GetValue<string>() == "tool-08-0"
        && capped.Outputs[63]?["itemId"]?.GetValue<string>() == "tool-39-1",
        "saved-output cap did not keep the newest 64 activities in chronological display order");
});

Check("output-range inspection uses a named host operation and one-item App Server pages", () =>
{
    var request = AppServerProtocol.BuildThreadItemsListRequest(17, "thread-1", "turn-2", "cursor-3");
    Assert(request.Method == "thread/items/list", "output inspection did not use App Server's paginated item API");
    Assert(request.Params["threadId"]?.GetValue<string>() == "thread-1"
        && request.Params["turnId"]?.GetValue<string>() == "turn-2"
        && request.Params["cursor"]?.GetValue<string>() == "cursor-3"
        && request.Params["limit"]?.GetValue<int>() == 1,
        "output inspection did not bind the exact turn and small item page");
    Assert(AppServerProtocol.IsNamedHostOperation("readOutputRange"),
        "the bridge did not allow the explicit output-range operation");
    Assert(!AppServerProtocol.IsNamedHostOperation("thread/items/list"),
        "raw App Server RPC was exposed as a renderer operation");
});

Check("saved file-change review projects exact patch items without leaking command output or partial diffs", () =>
{
    var source = JsonNode.Parse("""
    [
      {"id":"turn-2","status":"completed","items":[
        {"id":"patch-2","type":"fileChange","status":"completed","changes":[
          {"path":"note.txt","kind":{"type":"update","move_path":null},"diff":"-before\n+after"}
        ]},
        {"id":"shell-2","type":"commandExecution","aggregatedOutput":"private command output"}
      ]},
      {"id":"turn-1","status":"completed","items":[
        {"id":"patch-1","type":"fileChange","status":"completed","changes":[
          {"path":"broken.txt","kind":{"type":"update"},"diff":123}
        ]}
      ]}
    ]
    """)!.AsArray();
    var projected = ThreadSavedChangeProjector.Project(source, 100);
    Assert(projected.Count == 2, "saved patch turns were not projected");
    Assert(projected[0]?["turnId"]?.GetValue<string>() == "turn-2"
        && projected[0]?["status"]?.GetValue<string>() == "available", "latest patch turn lost its identity/status");
    Assert(projected[0]?["changes"]?[0]?["diff"]?.GetValue<string>() == "-before\n+after", "saved patch diff was changed");
    Assert(projected[1]?["status"]?.GetValue<string>() == "unavailable"
        && projected[1]?["changes"] is null, "malformed saved patch appeared reviewable");
    Assert(!projected.ToJsonString().Contains("private command output", StringComparison.Ordinal), "command output crossed the review boundary");

    var capped = ThreadSavedChangeProjector.Project(source, 5);
    Assert(capped[0]?["status"]?.GetValue<string>() == "oversized"
        && capped[0]?["changes"] is null, "an oversized saved patch leaked partial review content");
});

Check("saved file-change review rejects an untyped move destination", () =>
{
    var source = JsonNode.Parse("""
    [{"id":"move-turn","items":[{"type":"fileChange","status":"completed","changes":[
      {"path":"old.txt","kind":{"type":"update","move_path":123},"diff":"-old\n+new"}
    ]}]}]
    """)!.AsArray();
    var projected = ThreadSavedChangeProjector.Project(source, 100);
    Assert(projected[0]?["status"]?.GetValue<string>() == "unavailable"
        && projected[0]?["changes"] is null, "a malformed move destination was silently omitted from review");
});

Check("JSONL messages distinguish responses from notifications", () =>
{
    var response = JsonlMessage.Parse("{\"id\":4,\"result\":{\"ok\":true}}");
    var notification = JsonlMessage.Parse("{\"method\":\"turn/completed\",\"params\":{\"threadId\":\"t\"}}");
    Assert(response.Id == 4 && response.IsResponse, "response not parsed");
    Assert(notification.Method == "turn/completed" && !notification.IsResponse, "notification not parsed");
});

Check("App Server exit during a turn returns failure and preserves partial events", () =>
{
    RunUnexpectedServerExitAsync().GetAwaiter().GetResult();
});

Check("malformed App Server JSON closes the turn with a typed diagnostic and preserves partial events", () =>
{
    RunMalformedServerMessageAsync().GetAwaiter().GetResult();
});

Check("unknown informational App Server notifications are preserved without disrupting the turn", () =>
{
    RunUnknownNotificationAsync().GetAwaiter().GetResult();
});

Check("command completion received while a delayed active-command list is pending invalidates discovery", () =>
{
    RunCommandListCompletionRaceAsync().GetAwaiter().GetResult();
});

Check("App Server exit before a request response fails the pending request with EOF", () =>
{
    RunBrokenPipeAsync().GetAwaiter().GetResult();
});

Check("interrupted turn is terminal without being reported as success", () =>
{
    RunInterruptedTurnAsync().GetAwaiter().GetResult();
});

Check("large App Server assistant and tool output are bounded, attributed, and recover exact omission counts", () =>
{
    RunLargeOutputProjectionAsync().GetAwaiter().GetResult();
});

Check("App Server provider failure exposes its readable error message", () =>
{
    RunProviderErrorNotificationAsync().GetAwaiter().GetResult();
});

Check("server approval requests fail closed with schema-valid denials", () =>
{
    var commandRequest = JsonlMessage.Parse("{\"id\":9,\"method\":\"item/commandExecution/requestApproval\",\"params\":{\"command\":\"ver\"}}");
    Assert(commandRequest.IsServerRequest, "server request was not classified");
    var commandResponse = JsonNode.Parse(AppServerProtocol.BuildFailClosedServerRequestResponse(commandRequest))!.AsObject();
    Assert(commandResponse["id"]?.GetValue<long>() == 9, "server request id was not retained");
    Assert(commandResponse["result"]?["decision"]?.GetValue<string>() == "decline", "command approval was not declined");

    var permissionRequest = JsonlMessage.Parse("{\"id\":10,\"method\":\"item/permissions/requestApproval\",\"params\":{}}");
    var permissionResponse = JsonNode.Parse(AppServerProtocol.BuildFailClosedServerRequestResponse(permissionRequest))!.AsObject();
    Assert(permissionResponse["result"]?["permissions"] is JsonObject permissions && permissions.Count == 0, "permissions were not denied");

    var unknownRequest = JsonlMessage.Parse("{\"id\":11,\"method\":\"mcpServer/elicitation/request\",\"params\":{}}");
    var unknownResponse = JsonNode.Parse(AppServerProtocol.BuildFailClosedServerRequestResponse(unknownRequest))!.AsObject();
    Assert(unknownResponse["error"]?["code"]?.GetValue<int>() == -32001, "unknown authority request did not fail closed");
});

Check("approval responses are one-shot and permissions are copied from the runtime request", () =>
{
    var commandRequest = JsonlMessage.Parse("""
        {"id":41,"method":"item/commandExecution/requestApproval","params":{"threadId":"thread-1","turnId":"turn-1","itemId":"cmd-1","startedAtMs":1,"command":"cmd.exe /d /c ver","cwd":"D:\\\\workspace","availableDecisions":["accept","acceptForSession","decline","cancel"]}}
        """);
    var commandResponse = JsonNode.Parse(AppServerProtocol.BuildApprovalServerRequestResponse(commandRequest, "accept"))!.AsObject();
    Assert(commandResponse["result"]?["decision"]?.GetValue<string>() == "accept", "one-shot command approval was not serialized");
    AssertThrows<InvalidOperationException>(() => AppServerProtocol.BuildApprovalServerRequestResponse(commandRequest, "acceptForSession"));

    var unadvertisedCommand = JsonlMessage.Parse("""
        {"id":42,"method":"item/commandExecution/requestApproval","params":{"threadId":"thread-1","turnId":"turn-1","itemId":"cmd-2","startedAtMs":1,"command":"ver","availableDecisions":["decline","cancel"]}}
        """);
    AssertThrows<InvalidOperationException>(() => AppServerProtocol.BuildApprovalServerRequestResponse(unadvertisedCommand, "accept"));
    Assert(JsonNode.Parse(AppServerProtocol.BuildApprovalServerRequestResponse(unadvertisedCommand, "decline"))!["result"]?["decision"]?.GetValue<string>() == "decline",
        "fail-closed denial depended on an advertised allow choice");

    var commandWithoutDecisionMetadata = JsonlMessage.Parse("""
        {"id":48,"method":"item/commandExecution/requestApproval","params":{"threadId":"thread-1","turnId":"turn-1","itemId":"cmd-3","startedAtMs":1,"command":"ver","cwd":"D:\\\\workspace"}}
        """);
    AssertThrows<InvalidOperationException>(() => AppServerProtocol.BuildApprovalServerRequestResponse(commandWithoutDecisionMetadata, "accept"));

    var fileChangeRequest = JsonlMessage.Parse("""
        {"id":43,"method":"item/fileChange/requestApproval","params":{"threadId":"thread-1","turnId":"turn-1","itemId":"file-1","startedAtMs":1,"reason":"write requested file"}}
        """);
    var exactFileChangeReview = FileChangeApprovalReviewProjector.ProjectItemStarted(JsonNode.Parse("""
        {"threadId":"thread-1","turnId":"turn-1","item":{"type":"fileChange","id":"file-1","status":"inProgress","changes":[{"path":"D:\\\\workspace\\\\approved.txt","kind":{"type":"add"},"diff":"+approved\\n"}]}}
        """)!.AsObject())!;
    AssertThrows<InvalidOperationException>(() => AppServerProtocol.BuildApprovalServerRequestResponse(fileChangeRequest, "accept"));
    var fileChangeResponse = JsonNode.Parse(AppServerProtocol.BuildApprovalServerRequestResponse(
        fileChangeRequest, "accept", exactFileChangeReview, exactFileChangeReview.Fingerprint))!.AsObject();
    Assert(fileChangeResponse["result"]?.AsObject().Count == 1
        && fileChangeResponse["result"]?["decision"]?.GetValue<string>() == "accept",
        "file-change approval response was not limited to the exact one-shot decision");
    AssertThrows<InvalidOperationException>(() => AppServerProtocol.BuildApprovalServerRequestResponse(
        fileChangeRequest, "accept", exactFileChangeReview, new string('0', 64)));
    var mismatchedFileReview = exactFileChangeReview with
    {
        Identity = exactFileChangeReview.Identity with { ItemId = "different-item" }
    };
    AssertThrows<InvalidOperationException>(() => AppServerProtocol.BuildApprovalServerRequestResponse(
        fileChangeRequest, "accept", mismatchedFileReview, mismatchedFileReview.Fingerprint));
    Assert(JsonNode.Parse(AppServerProtocol.BuildApprovalServerRequestResponse(fileChangeRequest, "decline"))!["result"]?["decision"]?.GetValue<string>() == "decline",
        "file-change denial did not remain available without a diff preview");

    var rootExpansion = JsonlMessage.Parse("""
        {"id":44,"method":"item/fileChange/requestApproval","params":{"threadId":"thread-1","turnId":"turn-1","itemId":"file-2","startedAtMs":1,"grantRoot":"D:\\\\outside"}}
        """);
    AssertThrows<InvalidOperationException>(() => AppServerProtocol.BuildApprovalServerRequestResponse(rootExpansion, "accept"));

    var permissionRequest = JsonlMessage.Parse("""
        {"id":45,"method":"item/permissions/requestApproval","params":{"threadId":"thread-1","turnId":"turn-1","itemId":"perm-1","startedAtMs":1,"cwd":"D:\\\\workspace","permissions":{"network":{"enabled":true},"fileSystem":{"write":["D:\\\\workspace\\\\out.txt"]}}}}
        """);
    var permissionResponse = JsonNode.Parse(AppServerProtocol.BuildApprovalServerRequestResponse(permissionRequest, "grantRequestedForTurn"))!.AsObject();
    Assert(JsonNode.DeepEquals(permissionResponse["result"]?["permissions"], permissionRequest.Params?["permissions"]), "granted profile differed from the original request");
    Assert(permissionResponse["result"]?["scope"]?.GetValue<string>() == "turn", "permission grant was not restricted to this turn");
    AssertThrows<InvalidOperationException>(() => AppServerProtocol.BuildApprovalServerRequestResponse(permissionRequest, "acceptForSession"));

    var unsupportedPermissions = JsonlMessage.Parse("""
        {"id":46,"method":"item/permissions/requestApproval","params":{"threadId":"thread-1","turnId":"turn-1","itemId":"perm-2","startedAtMs":1,"cwd":"D:\\\\workspace","permissions":{"futureCapability":{"enabled":true}}}}
        """);
    AssertThrows<InvalidOperationException>(() => AppServerProtocol.BuildApprovalServerRequestResponse(unsupportedPermissions, "grantRequestedForTurn"));
});

Check("App Server approval requests wait for explicit host decisions", () =>
{
    RunInteractiveApprovalFlowAsync().GetAwaiter().GetResult();
});
Check("pending App Server approvals expire fail-closed and reject late choices", () =>
{
    RunApprovalTimeoutAsync().GetAwaiter().GetResult();
});

Check("turn diagnostics expose a successful command outcome", () =>
{
    var diagnostics = TurnDiagnostics.Extract([
        new AppServerNotification(
            "item/completed",
            new JsonObject
            {
                ["item"] = new JsonObject
                {
                    ["type"] = "commandExecution",
                    ["id"] = "item-1",
                    ["command"] = "ver",
                    ["status"] = "completed",
                    ["exitCode"] = 0,
                    ["aggregatedOutput"] = "Microsoft Windows"
                }
            })
    ]);
    Assert(diagnostics.Count == 1, "successful command was not summarized");
    var item = diagnostics[0]!.AsObject();
    Assert(item["succeeded"]?.GetValue<bool>() == true, "successful command was not marked successful");
    Assert(item["output"]?.GetValue<string>() == "Microsoft Windows", "command output was not retained");
    Assert(item["eventType"]?.GetValue<string>() == "toolOutcome", "successful tool event was not typed");
});

Check("turn diagnostics expose a failed command outcome", () =>
{
    var diagnostics = TurnDiagnostics.Extract([
        new AppServerNotification(
            "item/completed",
            new JsonObject
            {
                ["item"] = new JsonObject
                {
                    ["type"] = "commandExecution",
                    ["id"] = "item-2",
                    ["command"] = "cmd.exe /d /c ver",
                    ["status"] = "failed",
                    ["exitCode"] = 1,
                    ["aggregatedOutput"] = "blocked by policy"
                }
            })
    ]);
    Assert(diagnostics.Count == 1, "failed command was not summarized");
    var item = diagnostics[0]!.AsObject();
    Assert(item["succeeded"]?.GetValue<bool>() == false, "failed command was not marked failed");
    Assert(item["failure"]?["type"]?.GetValue<string>() == "toolExecution", "command failure was not typed");
    Assert(item["failure"]?["message"]?.GetValue<string>() == "blocked by policy", "command failure was not retained");
});

Check("session journal tool outcomes are paired, typed, and retain normal tool output", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Phase1B-Tests", Guid.NewGuid().ToString("N"));
    var codexHome = Path.Combine(root, "CodexHome");
    var sessions = Path.Combine(codexHome, "sessions");
    Directory.CreateDirectory(sessions);
    var journal = Path.Combine(sessions, "rollout-test.jsonl");
    var records = new JsonArray
    {
        JournalPayload("function_call", "fc-fail", "call-fail", "exec_command", "{\"cmd\":\"cmd.exe /d /c ver\"}"),
        JournalPayload("function_call", "fc-success", "call-success", "exec_command", "{\"cmd\":\"cmd.exe /d /c ver\"}"),
        JournalPayload("function_call", "fc-unknown", "call-unknown", "exec_command", "{\"cmd\":\"cmd.exe /d /c ver\"}"),
        JournalPayload("function_call_output", "fco-success", "call-success", null, "Chunk ID: c1\nProcess exited with code 0\nOutput:\nOPENAI_API_KEY=sk-test-value-must-not-leak"),
        JournalPayload("function_call_output", "fco-fail", "call-fail", null, "exec_command failed: CreateProcess { message: \\\"UnsupportedOperation(\\\"windows unelevated restricted-token sandbox cannot enforce split writable root sets directly; refusing to run unsandboxed\\\")\\\" }")
    };
    File.WriteAllText(journal, string.Join(Environment.NewLine, records.Select(record => record!.ToJsonString())) + Environment.NewLine);

    var read = SessionJournalEvidenceReader.ReadToolEvidence(codexHome);
    var diagnostics = read.Evidence["toolDiagnostics"] as JsonArray;
    Assert(diagnostics?.Count == 3, "journal function calls were not all converted into typed diagnostics");
    var outcomes = diagnostics!.OfType<JsonObject>().ToDictionary(
        item => item["callId"]?.GetValue<string>() ?? "",
        item => item,
        StringComparer.Ordinal);
    Assert(outcomes["call-fail"]["itemId"]?.GetValue<string>() == "fc-fail", "failed function output was not paired with its call item");
    Assert(outcomes["call-fail"]["succeeded"]?.GetValue<bool>() == false, "sandbox rejection was not reported as a typed tool failure");
    Assert(outcomes["call-fail"]["failure"]?["type"]?.GetValue<string>() == "toolExecution", "journal failure did not receive a structured toolExecution type");
    Assert(outcomes["call-success"]["itemId"]?.GetValue<string>() == "fc-success", "successful function output was not paired with its call item");
    Assert(outcomes["call-success"]["succeeded"]?.GetValue<bool>() == true, "exit code zero was not reported as success");
    Assert(outcomes["call-success"]["arguments"]?.GetValue<string>() == "{\"cmd\":\"cmd.exe /d /c ver\"}", "successful tool arguments were not retained exactly");
    Assert(outcomes["call-success"]["outputItemId"]?.GetValue<string>() == "fco-success", "normal output lost its exact output-item identity");
    Assert(outcomes["call-success"]["output"]?.GetValue<string>() == "Chunk ID: c1\nProcess exited with code 0\nOutput:\nOPENAI_API_KEY=sk-test-value-must-not-leak", "normal tool output was not retained exactly");
    Assert(outcomes["call-success"]["sourceRetained"]?.GetValue<bool>() == true, "retained output identity was not marked");
    Assert(outcomes["call-unknown"]["outcome"]?.GetValue<string>() == "unknown", "missing function output was not left Unknown");
    Assert(outcomes["call-unknown"]["output"] is null && outcomes["call-unknown"]["outputItemId"] is null, "missing output gained fabricated content or identity");
});

Check("App Server session journal preserves its effective context window", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Phase1B-Tests", Guid.NewGuid().ToString("N"));
    var codexHome = Path.Combine(root, "CodexHome");
    var sessions = Path.Combine(codexHome, "sessions");
    Directory.CreateDirectory(sessions);
    var journal = Path.Combine(sessions, "rollout-context.jsonl");
    File.WriteAllText(journal, "{\"payload\":{\"type\":\"task_started\",\"model_context_window\":249036}}\n");

    var read = SessionJournalToolEvidenceReader.ReadAppended(journal, codexHome, 0);
    Assert(read.ModelContextWindow == 249036, "effective session context was not retained from task_started");
    Assert(read.Status == "read", "effective context evidence status was not readable");
});

static JsonObject JournalPayload(string type, string itemId, string callId, string? name, string? value) => new()
{
    ["payload"] = new JsonObject
    {
        ["type"] = type,
        ["id"] = itemId,
        ["call_id"] = callId,
        ["name"] = name,
        [type == "function_call" ? "arguments" : "output"] = value
    }
};

Check("App Server launch environment is explicitly isolated", () =>
{
    const string isolatedCodexHome = "C:\\NeoBabylon\\.local\\Lab\\Runs\\launch-env-01\\App\\Data\\CodexHome";
    var env = AppServerLaunchEnvironment.Build(
        new AppServerLaunchOptions(
            "C:\\NeoBabylon\\.local\\Runtime\\NeoBabylon-Runtime\\bin\\codex-app-server.exe",
            "C:\\fixture",
            isolatedCodexHome,
            "http://127.0.0.1:1234/v1"));
    Assert(env["CODEX_HOME"] == isolatedCodexHome, "CODEX_HOME not isolated");
    Assert(env["CODEX_OSS_BASE_URL"] == "http://127.0.0.1:1234/v1", "provider endpoint not pinned");
    Assert(env["NEOBABYLON_REQUIRE_CONTAINED_TOOLS"] == "1", "NeoBabylon App Server child can launch unqualified legacy tools");
    Assert(!env.ContainsKey("OPENAI_API_KEY"), "credential fallback was introduced");
});

Check("App Server initialize must resolve the exact NeoBabylon Codex home", () =>
{
    var expected = Path.GetFullPath("C:\\NeoBabylon\\.local\\Lab\\Runs\\initialize-01\\App\\Data\\CodexHome");
    AppServerLaunchEnvironment.RequireCodexHome(
        new JsonObject { ["codexHome"] = expected }, expected);
    AssertThrows<InvalidOperationException>(() => AppServerLaunchEnvironment.RequireCodexHome(
        new JsonObject { ["codexHome"] = "C:\\Users\\Martin\\.codex" }, expected));
    AssertThrows<InvalidOperationException>(() => AppServerLaunchEnvironment.RequireCodexHome(
        new JsonObject(), expected));
});

Check("App Server receives only its explicit provider credential", () =>
    RunProviderCredentialHandoffAsync().GetAwaiter().GetResult());

Check("diagnostic bridge accepts only named operations", () =>
{
    var operation = DiagnosticOperation.Parse("{\"operation\":\"getDiagnostics\",\"requestId\":\"r1\"}");
    Assert(operation.Name == "getDiagnostics" && operation.RequestId == "r1", "named operation was not parsed");
    var interruptOperation = DiagnosticOperation.Parse("{\"operation\":\"interruptTurn\",\"requestId\":\"r2\"}");
    Assert(interruptOperation.Name == "interruptTurn", "named interrupt operation was not parsed");
    AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse("{\"method\":\"thread/shellCommand\"}"));
});

Check("Task 9 candidate file bridge accepts only exact bounded read fields", () =>
{
    const string identity = "candidate-v1:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    var operation = DiagnosticOperation.Parse(
        "{\"operation\":\"readGeneratedToolCandidateFileRange\",\"requestId\":\"file-1\","
        + "\"toolId\":\"fixture-tool\",\"contentIdentity\":\"" + identity + "\",\"path\":\"tool.js\",\"offset\":0}");
    Assert(operation.Name == "readGeneratedToolCandidateFileRange"
        && operation.RequestId == "file-1"
        && operation.Payload["toolId"]?.GetValue<string>() == "fixture-tool"
        && operation.Payload["contentIdentity"]?.GetValue<string>() == identity
        && operation.Payload["path"]?.GetValue<string>() == "tool.js"
        && operation.Payload["offset"]?.GetValue<int>() == 0,
        "candidate file read fields were not preserved exactly");

    foreach (var invalid in new[]
    {
        "{\"operation\":\"readGeneratedToolCandidateFileRange\",\"requestId\":\"x\",\"toolId\":\"fixture-tool\",\"contentIdentity\":\"" + identity + "\",\"path\":\"tool.js\",\"offset\":0,\"mode\":\"write\"}",
        "{\"operation\":\"readGeneratedToolCandidateFileRange\",\"requestId\":\"x\",\"toolId\":\"fixture-tool\",\"toolId\":\"other\",\"contentIdentity\":\"" + identity + "\",\"path\":\"tool.js\",\"offset\":0}",
        "{\"operation\":\"readGeneratedToolCandidateFileRange\",\"requestId\":\"x\",\"toolId\":\"fixture-tool\",\"contentIdentity\":\"" + identity + "\",\"path\":\"tool.js\",\"offset\":-1}",
        "{\"operation\":\"readGeneratedToolCandidateFileRange\",\"requestId\":\"x\",\"toolId\":\"fixture-tool\",\"contentIdentity\":\"" + identity + "\",\"path\":\"tool.js\",\"offset\":1.5}",
        "{\"operation\":\"readGeneratedToolCandidateFileRange\",\"requestId\":\"x\",\"toolId\":\"fixture-tool\",\"contentIdentity\":\"stale\",\"path\":\"tool.js\",\"offset\":0}"
    })
    {
        AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(invalid));
    }
});

Check("Task 9 review bridge accepts only exact non-executing decision and history fields", () =>
{
    const string identity = "candidate-v1:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    foreach (var name in new[] { "recordGeneratedToolReview", "rejectGeneratedToolCandidate" })
    {
        Assert(AppServerProtocol.IsNamedHostOperation(name), "named review decision operation is unavailable");
        var valid = "{\"operation\":\"" + name + "\",\"requestId\":\"review-1\","
            + "\"toolId\":\"fixture-tool\",\"contentIdentity\":\"" + identity + "\",\"note\":\"Reviewed locally\"}";
        var operation = DiagnosticOperation.Parse(valid);
        Assert(operation.Name == name && operation.RequestId == "review-1"
            && operation.Payload["toolId"]?.GetValue<string>() == "fixture-tool"
            && operation.Payload["contentIdentity"]?.GetValue<string>() == identity
            && operation.Payload["note"]?.GetValue<string>() == "Reviewed locally",
            "review decision fields were not preserved exactly");

        foreach (var invalid in new[]
        {
            valid.Replace("\"note\":\"Reviewed locally\"", "\"note\":\" \""),
            valid.Replace("\"note\":\"Reviewed locally\"", "\"note\":\"line\\nbreak\""),
            valid.Replace("\"note\":\"Reviewed locally\"", "\"note\":\"" + new string('n', 2049) + "\""),
            valid.Replace(",\"note\":\"Reviewed locally\"", ""),
            valid.Replace("\"requestId\":\"review-1\"", "\"requestId\":\"\""),
            valid.Replace("\"requestId\":\"review-1\"", "\"requestId\":7"),
            valid.Replace("\"requestId\":\"review-1\"", "\"requestId\":\"" + new string('r', 129) + "\""),
            valid.Replace("\"requestId\":\"review-1\"", "\"requestId\":\"review-1\",\"requestId\":\"again\""),
            valid.Replace("\"toolId\":\"fixture-tool\"", "\"toolId\":\"../fixture\""),
            valid.Replace("\"toolId\":\"fixture-tool\"", "\"toolId\":\"Fixture-tool\""),
            valid.Replace(identity, "candidate-v1:sha256:BAD"),
            valid.Replace("\"note\":\"Reviewed locally\"", "\"note\":null"),
            valid.Replace("\"note\":\"Reviewed locally\"", "\"note\":\"Reviewed locally\",\"note\":\"again\""),
            valid.Replace("\"note\":\"Reviewed locally\"", "\"note\":\"Reviewed locally\",\"path\":\"tool.js\""),
            valid.Replace("\"note\":\"Reviewed locally\"", "\"note\":\"Reviewed locally\",\"permissions\":[]"),
            valid.Replace("\"note\":\"Reviewed locally\"", "\"note\":\"Reviewed locally\",\"activate\":true"),
            valid.Replace("\"note\":\"Reviewed locally\"", "\"note\":\"Reviewed locally\",\"runtimeInterfaceVersion\":\"caller\""),
            valid.Replace("\"note\":\"Reviewed locally\"", "\"note\":\"Reviewed locally\",\"reviewInterfaceVersion\":\"caller\""),
            valid.Replace("\"note\":\"Reviewed locally\"", "\"note\":\"Reviewed locally\",\"decision\":\"execute\"")
        })
        {
            AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(invalid));
        }
    }

    Assert(AppServerProtocol.IsNamedHostOperation("listGeneratedToolReviewHistory"), "named review history operation is unavailable");
    var history = DiagnosticOperation.Parse(
        "{\"operation\":\"listGeneratedToolReviewHistory\",\"requestId\":\"history-1\",\"toolId\":\"fixture-tool\"}");
    Assert(history.RequestId == "history-1" && history.Payload["toolId"]?.GetValue<string>() == "fixture-tool",
        "review history fields were not preserved exactly");
    foreach (var invalid in new[]
    {
        "{\"operation\":\"listGeneratedToolReviewHistory\",\"requestId\":\"history-1\"}",
        "{\"operation\":\"listGeneratedToolReviewHistory\",\"requestId\":\"\",\"toolId\":\"fixture-tool\"}",
        "{\"operation\":\"listGeneratedToolReviewHistory\",\"requestId\":\"history-1\",\"toolId\":null}",
        "{\"operation\":\"listGeneratedToolReviewHistory\",\"requestId\":\"history-1\",\"toolId\":\"fixture-tool\",\"toolId\":\"other\"}",
        "{\"operation\":\"listGeneratedToolReviewHistory\",\"requestId\":\"history-1\",\"toolId\":\"fixture-tool\",\"contentIdentity\":\"" + identity + "\"}",
        "{\"operation\":\"listGeneratedToolReviewHistory\",\"requestId\":\"history-1\",\"toolId\":\"fixture-tool\",\"note\":\"not read-only\"}"
    })
    {
        AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(invalid));
    }
    Assert(!AppServerProtocol.IsNamedHostOperation("generatedToolReviewRpc"), "generic review RPC was exposed");
});

Check("Task 9 malformed review errors retain only a unique bounded request identifier", () =>
{
    const string identity = "candidate-v1:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    foreach (var name in new[] { "recordGeneratedToolReview", "rejectGeneratedToolCandidate" })
    {
        var rejectedNote = "{\"operation\":\"" + name + "\",\"requestId\":\"review-error-1\","
            + "\"toolId\":\"fixture-tool\",\"contentIdentity\":\"" + identity + "\",\"note\":\"line\\nbreak\"}";
        Assert(DiagnosticOperation.RequestIdForError(rejectedNote) == "review-error-1",
            "a rejected review note lost its valid host error correlation identifier");
        AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(rejectedNote));

        var duplicateId = rejectedNote.Replace("\"requestId\":\"review-error-1\"",
            "\"requestId\":\"review-error-1\",\"requestId\":\"other\"");
        Assert(DiagnosticOperation.RequestIdForError(duplicateId) is null,
            "a duplicate request identifier was used to correlate a rejected review");
        AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(duplicateId));

        foreach (var invalidId in new[]
        {
            rejectedNote.Replace("\"requestId\":\"review-error-1\"", "\"requestId\":7"),
            rejectedNote.Replace("\"requestId\":\"review-error-1\"", "\"requestId\":\"line\\nbreak\""),
            rejectedNote.Replace("\"requestId\":\"review-error-1\"", "\"requestId\":\"" + new string('r', 129) + "\"")
        })
        {
            Assert(DiagnosticOperation.RequestIdForError(invalidId) is null,
                "an invalid request identifier was used for host error correlation");
            AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(invalidId));
        }
    }

    var unnamed = "{\"operation\":\"thread/shellCommand\",\"requestId\":\"invalid-operation\"}";
    Assert(DiagnosticOperation.RequestIdForError(unnamed) == "invalid-operation",
        "error correlation unexpectedly depended on accepting an operation");
    AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(unnamed));
    Assert(DiagnosticOperation.RequestIdForError("{\"requestId\":\"unfinished\"") is null,
        "malformed JSON unexpectedly supplied an error correlation identifier");
});

Check("Task 9 prepared binding bridge accepts only exact disabled lifecycle fields", () =>
{
    const string recoveryName = "listGeneratedToolPreparedBindingToolIds";
    Assert(AppServerProtocol.IsNamedHostOperation(recoveryName),
        "named prepared binding recovery listing is unavailable");
    var recovery = DiagnosticOperation.Parse(
        "{\"operation\":\"" + recoveryName + "\",\"requestId\":\"binding-recovery-1\"}");
    Assert(recovery.Name == recoveryName && recovery.RequestId == "binding-recovery-1"
        && recovery.Payload.Count == 2, "recovery listing accepted unexpected fields");
    AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(
        "{\"operation\":\"" + recoveryName + "\",\"requestId\":\"binding-recovery-1\",\"toolId\":\"fixture-tool\"}"));
    AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(
        "{\"operation\":\"" + recoveryName + "\",\"requestId\":\"binding-recovery-1\",\"requestId\":\"other\"}"));
    AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(
        "{\"operation\":\"" + recoveryName + "\",\"requestId\":\"line\\nbreak\"}"));

    const string contentIdentity = "candidate-v1:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const string reviewIdentity = "review-v1:sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    const string currentRecordSha256 = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    const string fields = "\"requestId\":\"binding-1\",\"toolId\":\"fixture-tool\"";
    const string stageName = "stageGeneratedToolDisabledMcp";
    var stage = "{\"operation\":\"" + stageName + "\"," + fields
        + ",\"contentIdentity\":\"" + contentIdentity + "\",\"reviewIdentity\":\""
        + reviewIdentity + "\",\"currentRecordSha256\":\"" + currentRecordSha256 + "\"}";
    Assert(AppServerProtocol.IsNamedHostOperation(stageName), "named disabled MCP stage is unavailable");
    var stageOperation = DiagnosticOperation.Parse(stage);
    Assert(stageOperation.Name == stageName && stageOperation.RequestId == "binding-1"
        && stageOperation.Payload.Count == 6
        && stageOperation.Payload["currentRecordSha256"]?.GetValue<string>() == currentRecordSha256,
        "disabled MCP stage lost its exact binding identity");
    foreach (var invalid in new[]
    {
        stage.Replace("\"requestId\":\"binding-1\"", "\"requestId\":\"binding-1\",\"requestId\":\"other\""),
        stage.Replace("\"requestId\":\"binding-1\"", "\"requestId\":\"line\\nbreak\""),
        stage.Replace("\"toolId\":\"fixture-tool\"", "\"toolId\":\"../fixture\""),
        stage.Replace(contentIdentity, "candidate-v1:sha256:BAD"),
        stage.Replace(reviewIdentity, "review-v1:sha256:BAD"),
        stage.Replace(currentRecordSha256, "BAD"),
        stage.Replace("}", ",\"adapterPath\":\"C:/outside.exe\"}"),
        stage.Replace("}", ",\"enabled\":true}"),
        stage.Replace("}", ",\"note\":\"caller instruction\"}")
    })
    {
        AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(invalid));
    }
    Assert(DiagnosticOperation.RequestIdForError(stage.Replace(currentRecordSha256, "BAD")) == "binding-1",
        "malformed stage lost valid request correlation");
    Assert(DiagnosticOperation.RequestIdForError(stage.Replace("\"requestId\":\"binding-1\"",
        "\"requestId\":\"binding-1\",\"requestId\":\"other\"")) is null,
        "duplicate stage request ID was used for error correlation");
    foreach (var name in new[] { "listGeneratedToolPreparedBindingHistory", "readGeneratedToolPreparedBindingCurrent" })
    {
        Assert(AppServerProtocol.IsNamedHostOperation(name), "named prepared binding read is unavailable");
        var valid = "{\"operation\":\"" + name + "\"," + fields + "}";
        var operation = DiagnosticOperation.Parse(valid);
        Assert(operation.Name == name && operation.RequestId == "binding-1"
            && operation.Payload["toolId"]?.GetValue<string>() == "fixture-tool",
            "prepared binding read fields were not preserved");
        AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(
            "{\"operation\":\"" + name + "\"," + fields + ",\"contentIdentity\":\"" + contentIdentity + "\"}"));
        AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(
            valid.Replace("\"toolId\":\"fixture-tool\"", "\"toolId\":\"../fixture\"")));
        AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(
            valid.Replace("\"requestId\":\"binding-1\"", "\"requestId\":\"binding-1\",\"requestId\":\"other\"")));
    }

    foreach (var name in new[]
    {
        "prepareGeneratedToolDisabledBinding", "revokeGeneratedToolPreparedBinding", "cleanupGeneratedToolPreparedBinding"
    })
    {
        Assert(AppServerProtocol.IsNamedHostOperation(name), "named prepared binding transition is unavailable");
        var transitionFields = fields + ",\"contentIdentity\":\"" + contentIdentity
            + "\",\"reviewIdentity\":\"" + reviewIdentity + "\"";
        if (name != "prepareGeneratedToolDisabledBinding")
        {
            transitionFields += ",\"currentRecordSha256\":\"" + currentRecordSha256 + "\"";
        }
        var valid = "{\"operation\":\"" + name + "\"," + transitionFields + ",\"note\":\"Local disabled transition\"}";
        var operation = DiagnosticOperation.Parse(valid);
        Assert(operation.Name == name && operation.RequestId == "binding-1"
            && operation.Payload["contentIdentity"]?.GetValue<string>() == contentIdentity
            && operation.Payload["reviewIdentity"]?.GetValue<string>() == reviewIdentity
            && operation.Payload["note"]?.GetValue<string>() == "Local disabled transition",
            "prepared binding transition lost an exact identity or note");
        Assert(operation.Payload.ContainsKey("currentRecordSha256") == (name != "prepareGeneratedToolDisabledBinding"),
            "prepared binding transition accepted the wrong current-record identity shape");

        foreach (var invalid in new[]
        {
            valid.Replace("\"note\":\"Local disabled transition\"", "\"note\":\" \""),
            valid.Replace("\"note\":\"Local disabled transition\"", "\"note\":\"line\\nbreak\""),
            valid.Replace("\"note\":\"Local disabled transition\"", "\"note\":\"" + new string('n', 2049) + "\""),
            valid.Replace("\"requestId\":\"binding-1\"", "\"requestId\":\"binding-1\",\"requestId\":\"other\""),
            valid.Replace("\"requestId\":\"binding-1\"", "\"requestId\":7"),
            valid.Replace("\"toolId\":\"fixture-tool\"", "\"toolId\":\"../fixture\""),
            valid.Replace(contentIdentity, "candidate-v1:sha256:BAD"),
            valid.Replace(reviewIdentity, "review-v1:sha256:BAD"),
            valid.Replace("\"note\":\"Local disabled transition\"", "\"note\":\"Local disabled transition\",\"path\":\"tool.js\""),
            valid.Replace("\"note\":\"Local disabled transition\"", "\"note\":\"Local disabled transition\",\"activate\":true"),
            valid.Replace("\"note\":\"Local disabled transition\"", "\"note\":\"Local disabled transition\",\"reviewInterfaceVersion\":\"caller\"")
        })
        {
            AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(invalid));
        }
        if (name == "prepareGeneratedToolDisabledBinding")
        {
            AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(
                valid.Replace("\"note\":\"Local disabled transition\"",
                    "\"currentRecordSha256\":\"" + currentRecordSha256 + "\",\"note\":\"Local disabled transition\"")));
        }
        else
        {
            Assert(operation.Payload["currentRecordSha256"]?.GetValue<string>() == currentRecordSha256,
                "prepared binding denial lost the current record hash");
            AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(valid.Replace(currentRecordSha256, "BAD")));
            AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(
                valid.Replace("\"currentRecordSha256\":\"" + currentRecordSha256 + "\",", "")));
        }
    }

    var malformed = "{\"operation\":\"prepareGeneratedToolDisabledBinding\"," + fields
        + ",\"contentIdentity\":\"" + contentIdentity + "\",\"reviewIdentity\":\"" + reviewIdentity
        + "\",\"note\":\"line\\nbreak\"}";
    Assert(DiagnosticOperation.RequestIdForError(malformed) == "binding-1",
        "malformed prepared binding error lost its bounded request correlation");
    AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(malformed));
    var duplicateId = malformed.Replace("\"requestId\":\"binding-1\"",
        "\"requestId\":\"binding-1\",\"requestId\":\"other\"");
    Assert(DiagnosticOperation.RequestIdForError(duplicateId) is null,
        "duplicate prepared binding request ID was used for error correlation");
    AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(duplicateId));
    Assert(!AppServerProtocol.IsNamedHostOperation("activateGeneratedToolCandidate")
        && !AppServerProtocol.IsNamedHostOperation("generatedToolIntegrationRpc"),
        "prepared binding exposed activation or generic RPC");
});

Check("only startTurn accepts an optional positive integer output cap", () =>
{
    var omitted = DiagnosticOperation.Parse("{\"operation\":\"startTurn\",\"requestId\":\"turn-1\",\"text\":\"fixture\"}");
    Assert(!omitted.Payload.ContainsKey("maxOutputTokens"), "omitted output cap was inserted by the bridge");
    var explicitCap = DiagnosticOperation.Parse("{\"operation\":\"startTurn\",\"requestId\":\"turn-2\",\"text\":\"fixture\",\"maxOutputTokens\":512}");
    Assert(explicitCap.Payload["maxOutputTokens"]?.GetValue<int>() == 512, "positive integer output cap was not preserved");
    foreach (var invalid in new[] { "0", "-1", "1.5", "\"512\"", "null", "2147483648" })
    {
        AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(
            $"{{\"operation\":\"startTurn\",\"requestId\":\"turn-invalid\",\"text\":\"fixture\",\"maxOutputTokens\":{invalid}}}"));
    }
    AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(
        "{\"operation\":\"startTurn\",\"requestId\":\"turn-duplicate\",\"text\":\"fixture\",\"maxOutputTokens\":128,\"maxOutputTokens\":256}"));
    AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(
        "{\"operation\":\"getDiagnostics\",\"requestId\":\"not-a-turn\",\"maxOutputTokens\":512}"));
});

Check("App Server turn request carries only an explicit cap bounded by advertised completion tokens", () =>
{
    var capability = ModelCapabilityRecord.CreateUnknown("lmstudio", "http://127.0.0.1:1234/v1", "fixture") with
    {
        MaxCompletionTokensAdvertised = CapabilityObservation.Known(512, "test")
    };
    var options = new TurnStartOptions("fixture-thread", "fixture prompt");
    var omitted = AppServerClient.BuildTurnStartRequest(1, options, capability, null);
    Assert(!omitted.Params.ContainsKey("maxOutputTokens"), "omitted cap overrode the runtime default");
    var explicitCap = AppServerClient.BuildTurnStartRequest(2, options, capability, 512);
    Assert(explicitCap.Method == "turn/start" && explicitCap.Params["maxOutputTokens"]?.GetValue<int>() == 512,
        "the known maximum was not serialized on turn/start");
    AssertThrows<InvalidDataException>(() => AppServerClient.BuildTurnStartRequest(3, options, capability, 513));
    AssertThrows<InvalidDataException>(() => AppServerClient.BuildTurnStartRequest(4, options, capability, 0));
    var unknown = capability with { MaxCompletionTokensAdvertised = CapabilityObservation.Unknown("test") };
    Assert(AppServerClient.BuildTurnStartRequest(5, options, unknown, 1024).Params["maxOutputTokens"]?.GetValue<int>() == 1024,
        "an unknown advertised maximum incorrectly blocked an explicit positive override");
    var malformed = capability with { MaxCompletionTokensAdvertised = CapabilityObservation.Known("not-an-integer", "test") };
    AssertThrows<InvalidDataException>(() => AppServerClient.BuildTurnStartRequest(6, options, malformed, 128));
});

Check("native caption appearance accepts only dark and light choices", () =>
{
    Assert(HostAppearanceParser.Parse("dark") == HostAppearance.Dark, "dark appearance was not accepted");
    Assert(HostAppearanceParser.Parse("light") == HostAppearance.Light, "light appearance was not accepted");
    AssertThrows<InvalidDataException>(() => HostAppearanceParser.Parse("system"));
    AssertThrows<InvalidDataException>(() => HostAppearanceParser.Parse(""));
    Assert(AppServerProtocol.IsNamedHostOperation("setAppearance"), "appearance bridge is not a named host operation");
    var operation = DiagnosticOperation.Parse("{\"operation\":\"setAppearance\",\"requestId\":\"appearance-1\",\"appearance\":\"dark\"}");
    Assert(operation.Payload["appearance"]?.GetValue<string>() == "dark", "appearance bridge payload was not preserved");
});

Check("project selection uses named host operations, not a renderer-supplied cwd", () =>
{
    Assert(AppServerProtocol.IsNamedHostOperation("addProject"), "native folder-picker operation is unavailable");
    Assert(AppServerProtocol.IsNamedHostOperation("selectProject"), "saved-project selection operation is unavailable");
    Assert(!AppServerProtocol.IsNamedHostOperation("setWorkingDirectory"), "renderer gained an arbitrary cwd setter");
    var selection = DiagnosticOperation.Parse("{\"operation\":\"selectProject\",\"requestId\":\"project-1\",\"workspacePath\":\"D:\\\\Projects\\\\Alpha\"}");
    Assert(selection.Payload["workspacePath"]?.GetValue<string>() == "D:\\Projects\\Alpha", "project selection path was not parsed");
});

Check("diagnostic bridge accepts model catalog operations without adding generic RPC", () =>
{
    var list = DiagnosticOperation.Parse("{\"operation\":\"listCapabilities\",\"requestId\":\"models-1\"}");
    Assert(list.Name == "listCapabilities", "named model catalog operation was rejected");
    var select = DiagnosticOperation.Parse("{\"operation\":\"selectCapability\",\"requestId\":\"models-2\",\"providerId\":\"openrouter\",\"modelIdentifier\":\"nex-agi/nex-n2.5-pro:free\"}");
    Assert(select.Payload["modelIdentifier"]?.GetValue<string>() == "nex-agi/nex-n2.5-pro:free", "exact model identity was not retained");
    var fork = DiagnosticOperation.Parse("{\"operation\":\"forkThread\",\"requestId\":\"threads-fork-1\",\"threadId\":\"thread-source\"}");
    Assert(fork.Name == "forkThread" && fork.Payload["threadId"]?.GetValue<string>() == "thread-source", "named thread fork operation was rejected");
    AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse("{\"operation\":\"thread/shellCommand\",\"requestId\":\"unsafe\"}"));
});

Check("deterministic Responses fixture emits the pinned function-call shape", () =>
{
    var stream = ResponsesSse.FunctionCall(
        "mock-response-1",
        "mock-call-1",
        "exec_command",
        "{\"cmd\":\"echo fixture\"}");
    Assert(stream.Contains("event: response.output_item.done", StringComparison.Ordinal), "function-call event missing");
    Assert(stream.Contains("\"name\":\"exec_command\"", StringComparison.Ordinal), "ordinary tool name missing");
    Assert(stream.Contains("event: response.completed", StringComparison.Ordinal), "completion event missing");
});

Check("isolated Codex config materializes only known capability state", () =>
{
    var known = ModelCapabilityRecord.CreateUnknown("lmstudio", "http://127.0.0.1:1234/v1", "qwen/qwen3-14b") with
    {
        ContextWindowAdvertised = CapabilityObservation.Known(32768, "test")
    };
    var text = CodexConfigBuilder.Build(known);
    Assert(text.Contains("model_context_window = 32768", StringComparison.Ordinal), "observed context was not written");
    Assert(!text.Contains("model_auto_compact_token_limit", StringComparison.Ordinal), "auto-compaction was invented");
    var unknown = ModelCapabilityRecord.CreateUnknown("lmstudio", "http://127.0.0.1:1234/v1", "qwen/qwen3-14b");
    Assert(!CodexConfigBuilder.Build(unknown).Contains("model_context_window", StringComparison.Ordinal), "unknown context was written as a default");
});

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}

return 0;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static string NewQaRunRoot(string sourceRoot, string purpose) =>
    Path.Combine(sourceRoot, ".local", "Lab", "Runs", $"{purpose}-{Guid.NewGuid():N}");

static bool IsExactQaRunApplicationRoot(string sourceRoot, string applicationRoot)
{
    var runsRoot = Path.Combine(sourceRoot, ".local", "Lab", "Runs");
    var components = Path.GetRelativePath(runsRoot, applicationRoot)
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    return components.Length == 2
        && !string.IsNullOrEmpty(components[0])
        && string.Equals(components[1], "App", StringComparison.OrdinalIgnoreCase);
}

static void AssertThrows<T>(Action action) where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        return;
    }

    throw new InvalidOperationException($"expected {typeof(T).Name}");
}

static async Task AssertThrowsAsync<T>(Func<Task> action) where T : Exception
{
    try
    {
        await action().ConfigureAwait(false);
    }
    catch (T)
    {
        return;
    }

    throw new InvalidOperationException($"expected {typeof(T).Name}");
}

static async Task RunUnrestrictedAppServerPolicyAsync()
{
    var sourceRoot = Directory.GetCurrentDirectory();
    var identity = RuntimeIdentity.LoadVerified(Path.Combine(sourceRoot, "runtime", "runtime-lock.json"));
    var testParent = NewQaRunRoot(sourceRoot, "unrestricted");
    var applicationRoot = Path.Combine(testParent, "App");
    var layout = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
    await using var mock = await UnrestrictedResponsesFixture.StartAsync();
    var capability = (JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The pinned LM Studio capability record is empty.")) with { Endpoint = mock.BaseUrl };

    try
    {
        CodexConfigBuilder.WriteIsolated(layout.CodexHome, capability, toolExecutionPolicy: ToolExecutionPolicy.Unrestricted);
        await using var client = AppServerClient.Start(new AppServerLaunchOptions(
            identity.BinaryPath,
            layout.FixtureWorkspace,
            layout.CodexHome,
            mock.BaseUrl));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var initialized = await client.InitializeAsync(timeout.Token);
        AppServerLaunchEnvironment.RequireCodexHome(initialized, layout.CodexHome);
        var config = await client.RequestResultAsync(
            id => AppServerProtocol.BuildConfigReadRequest(id, layout.FixtureWorkspace), timeout.Token);
        var thread = await client.RequestResultAsync(
            id => AppServerProtocol.BuildThreadStartRequest(id, new ThreadStartOptions(
                capability.ModelIdentifier,
                capability.ProviderId,
                layout.FixtureWorkspace,
                "danger-full-access",
                "never",
                CapabilityAdapter.ToCodexConfig(capability))), timeout.Token);
        var authority = SandboxAuthorityDiagnostics.RequireUnrestricted(config, thread);
        Assert(authority["effectiveSandboxType"]?.GetValue<string>() == "dangerFullAccess", "pinned App Server did not select full access");
        Assert(thread["cwd"]?.GetValue<string>() == layout.FixtureWorkspace, "thread escaped the isolated fixture workspace");
        var threadId = thread["thread"]?["id"]?.GetValue<string>()
            ?? throw new InvalidDataException("Pinned App Server did not return a thread id.");
        await client.RequestResultAsync(
            id => AppServerProtocol.BuildTurnStartRequest(id, new TurnStartOptions(
                threadId, "Run the fixture diagnostic command.", ToolExecutionPolicy.Unrestricted)), timeout.Token);
        var observation = await client.WaitForTurnCompletionAsync(TimeSpan.FromSeconds(60), timeout.Token);
        Assert(observation.Completed, $"unrestricted fixture turn did not complete: {observation.Failure}");
        Assert(mock.RequestBodies.Any(body => body.Contains("function_call_output", StringComparison.Ordinal)
            && body.Contains("Microsoft Windows", StringComparison.OrdinalIgnoreCase)),
            "pinned App Server did not complete the real full-access command round trip");
        var scopedHistory = await client.RequestResultAsync(
            id => AppServerProtocol.BuildThreadListRequest(id, cwd: layout.FixtureWorkspace), timeout.Token);
        Assert(scopedHistory["data"] is JsonArray scopedRows
            && scopedRows.OfType<JsonObject>().Any(item => item["id"]?.GetValue<string>() == threadId),
            "pinned App Server did not list the saved thread under its exact project cwd");
        var savedRead = await client.RequestResultAsync(
            id => AppServerProtocol.BuildThreadReadRequest(id, threadId, includeTurns: false), timeout.Token);
        ThreadResumeIdentity.RequireExactRead(
            savedRead["thread"] as JsonObject, threadId, capability, layout.FixtureWorkspace);
        var resumed = await client.RequestResultAsync(
            id => AppServerProtocol.BuildThreadResumeRequest(id, threadId, ToolExecutionPolicy.Unrestricted), timeout.Token);
        var resumedAuthority = SandboxAuthorityDiagnostics.RequireUnrestricted(config, resumed);
        Assert(resumedAuthority["effectiveSandboxType"]?.GetValue<string>() == "dangerFullAccess", "resumed thread lost full access");
        var forked = await client.RequestResultAsync(
            id => AppServerProtocol.BuildThreadForkRequest(id, threadId, ToolExecutionPolicy.Unrestricted), timeout.Token);
        var forkedAuthority = SandboxAuthorityDiagnostics.RequireUnrestricted(config, forked);
        Assert(forkedAuthority["effectiveSandboxType"]?.GetValue<string>() == "dangerFullAccess", "forked thread lost full access");
        var forkedThreadId = forked["thread"]?["id"]?.GetValue<string>()
            ?? throw new InvalidDataException("The pinned App Server did not return a fork id.");
        await client.RequestResultAsync(
            id => AppServerProtocol.BuildTurnStartRequest(id, new TurnStartOptions(
                forkedThreadId, "Run the second fixture diagnostic command.", ToolExecutionPolicy.Unrestricted)), timeout.Token);
        var forkedObservation = await client.WaitForTurnCompletionAsync(TimeSpan.FromSeconds(60), timeout.Token);
        Assert(forkedObservation.Completed, $"second fixture turn did not complete: {forkedObservation.Failure}");
        var firstPage = await client.RequestResultAsync(id => new AppServerRequest(id, "thread/list", new JsonObject
        {
            ["limit"] = 1,
            ["cwd"] = layout.FixtureWorkspace,
            ["archived"] = false,
            ["sortKey"] = "updated_at",
            ["sortDirection"] = "desc"
        }), timeout.Token);
        var upstreamCursor = firstPage["nextCursor"]?.GetValue<string>();
        Assert(!string.IsNullOrWhiteSpace(upstreamCursor), "the pinned App Server did not expose an older-history cursor");
        var secondPage = await client.RequestResultAsync(id => new AppServerRequest(id, "thread/list", new JsonObject
        {
            ["limit"] = 1,
            ["cursor"] = upstreamCursor,
            ["cwd"] = layout.FixtureWorkspace,
            ["archived"] = false,
            ["sortKey"] = "updated_at",
            ["sortDirection"] = "desc"
        }), timeout.Token);
        var firstListedId = firstPage["data"]?[0]?["id"]?.GetValue<string>();
        var secondListedId = secondPage["data"]?[0]?["id"]?.GetValue<string>();
        Assert(firstListedId is not null && secondListedId is not null && firstListedId != secondListedId,
            "the pinned App Server did not advance to a distinct saved-history page");
        var indexedFirstPage = await client.RequestResultAsync(id => new AppServerRequest(id, "thread/list", new JsonObject
        {
            ["limit"] = 1,
            ["cwd"] = layout.FixtureWorkspace,
            ["archived"] = false,
            ["sortKey"] = "updated_at",
            ["sortDirection"] = "desc",
            ["useStateDbOnly"] = true
        }), timeout.Token);
        var indexedCursor = indexedFirstPage["nextCursor"]?.GetValue<string>();
        Assert(!string.IsNullOrWhiteSpace(indexedCursor), "state DB-only saved history did not expose its independent cursor");
        var indexedSecondPage = await client.RequestResultAsync(id => new AppServerRequest(id, "thread/list", new JsonObject
        {
            ["limit"] = 1,
            ["cursor"] = indexedCursor,
            ["cwd"] = layout.FixtureWorkspace,
            ["archived"] = false,
            ["sortKey"] = "updated_at",
            ["sortDirection"] = "desc",
            ["useStateDbOnly"] = true
        }), timeout.Token);
        Assert(indexedFirstPage["data"]?[0]?["id"]?.GetValue<string>() != indexedSecondPage["data"]?[0]?["id"]?.GetValue<string>(),
            "state DB-only saved history did not advance to a distinct page");
    }
    finally
    {
        var exactRoot = Path.GetFullPath(applicationRoot);
        if (Directory.Exists(exactRoot)
            && exactRoot.StartsWith(testParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var file in Directory.EnumerateFiles(exactRoot, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            }))
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                }
            }
            await DeleteTemporaryQualificationRootWithRetryAsync(exactRoot, testParent);
        }
    }
}

static async Task RunUnrestrictedCommandPartialFailureAsync()
{
    const string marker = "NEOBABYLON_P3_02_PARTIAL_FAILURE_MARKER";
    var sourceRoot = Directory.GetCurrentDirectory();
    var identity = RuntimeIdentity.LoadVerified(Path.Combine(sourceRoot, "runtime", "runtime-lock.json"));
    var qaParent = Path.Combine(sourceRoot, ".local", "Lab", "Runs");
    var testParent = Path.Combine(qaParent, $"P3-02-command-failure-{Guid.NewGuid():N}");
    var applicationRoot = Path.Combine(testParent, "App");
    var layout = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
    Directory.CreateDirectory(layout.FixtureWorkspace);
    var scriptPath = Path.Combine(layout.FixtureWorkspace, "p3-02-fail.cmd");
    File.WriteAllText(scriptPath, $"@echo off\r\necho {marker}\r\nexit /b 23\r\n", Encoding.ASCII);
    Assert(!scriptPath.Any(char.IsWhiteSpace), "the isolated command fixture path unexpectedly contains whitespace");
    var command = $"cmd.exe /d /c {scriptPath}; exit $LASTEXITCODE";
    var responses = new[]
    {
        ResponsesSse.FunctionCall(
            "p3-command-failure-response",
            "p3-command-failure-call",
            "exec_command",
            new JsonObject { ["cmd"] = command }.ToJsonString()),
        ResponsesSse.AssistantMessage(
            "p3-command-failure-followup",
            "p3-command-failure-followup-message",
            "The command result was received.")
    };
    await using var fixture = await UnrestrictedResponsesFixture.StartAsync(responseSequence: responses);

    try
    {
        var sourceCapability = JsonSerializer.Deserialize<ModelCapabilityRecord>(
            File.ReadAllText(Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("The pinned LM Studio capability record is empty.");
        var capability = sourceCapability with { Endpoint = fixture.BaseUrl };
        CodexConfigBuilder.WriteIsolated(
            layout.CodexHome, capability, toolExecutionPolicy: ToolExecutionPolicy.Unrestricted);

        await using var client = AppServerClient.Start(new AppServerLaunchOptions(
            identity.BinaryPath,
            layout.FixtureWorkspace,
            layout.CodexHome,
            fixture.BaseUrl));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var initialized = await client.InitializeAsync(timeout.Token);
        AppServerLaunchEnvironment.RequireCodexHome(initialized, layout.CodexHome);
        var config = await client.RequestResultAsync(
            id => AppServerProtocol.BuildConfigReadRequest(id, layout.FixtureWorkspace), timeout.Token);
        var thread = await client.RequestResultAsync(
            id => AppServerProtocol.BuildThreadStartRequest(id, new ThreadStartOptions(
                capability.ModelIdentifier,
                capability.ProviderId,
                layout.FixtureWorkspace,
                "danger-full-access",
                "never",
                CapabilityAdapter.ToCodexConfig(capability))), timeout.Token);
        var authority = SandboxAuthorityDiagnostics.RequireUnrestricted(config, thread);
        Assert(authority["effectiveSandboxType"]?.GetValue<string>() == "dangerFullAccess",
            "the pinned runtime did not report the selected unrestricted authority");
        var threadId = thread["thread"]?["id"]?.GetValue<string>()
            ?? throw new InvalidDataException("The pinned App Server did not return a thread id.");
        var turnStart = await client.RequestResultAsync(
            id => AppServerProtocol.BuildTurnStartRequest(id, new TurnStartOptions(
                threadId, "Run the fixed partial-output failure fixture.", ToolExecutionPolicy.Unrestricted)), timeout.Token);
        var turnId = turnStart["turn"]?["id"]?.GetValue<string>()
            ?? throw new InvalidDataException("The pinned App Server did not return a turn id.");
        var observation = await client.WaitForTurnCompletionAsync(
            TimeSpan.FromSeconds(60), timeout.Token, threadId, turnId);
        Assert(observation.Completed,
            $"the deterministic provider follow-up did not complete after the failed tool: {observation.Failure ?? "<no failure detail>"}");

        var completed = observation.Notifications.Single(notification =>
            notification.Method == "item/completed"
            && notification.Params["item"]?["type"]?.GetValue<string>() == "commandExecution");
        var item = completed.Params["item"] as JsonObject
            ?? throw new InvalidDataException("The completed command event did not contain its item.");
        var itemId = item["id"]?.GetValue<string>()
            ?? throw new InvalidDataException("The failed command item had no identity.");
        Assert(completed.Params["threadId"]?.GetValue<string>() == threadId
            && completed.Params["turnId"]?.GetValue<string>() == turnId,
            "the failed command item lost its exact thread/turn attribution");
        var output = item["aggregatedOutput"]?.GetValue<string>() ?? string.Empty;
        var exitCode = item["exitCode"]?.GetValue<int>();
        Assert(output.Contains(marker, StringComparison.Ordinal),
            "the failed command's partial stdout marker was not retained on its terminal item");
        Assert(exitCode == 23,
            $"the failed command's nonzero exit code was not retained (actual={exitCode?.ToString() ?? "Unknown"}; output={output})");

        var diagnostic = TurnDiagnostics.Extract(observation.Notifications)
            .OfType<JsonObject>()
            .Single(outcome => outcome["itemId"]?.GetValue<string>() == itemId);
        Assert(diagnostic["outcome"]?.GetValue<string>() == "failed"
            && diagnostic["succeeded"]?.GetValue<bool>() == false
            && diagnostic["failure"]?["type"]?.GetValue<string>() == "toolExecution"
            && diagnostic["attributedTo"]?.GetValue<string>() == "Codex App Server"
            && diagnostic["output"]?.GetValue<string>()?.Contains(marker, StringComparison.Ordinal) == true,
            "host diagnostics did not preserve a typed, attributed failure with its partial output");

        var requests = fixture.RequestBodies;
        Assert(requests.Count == 2,
            $"the fixed tool call must produce one result request and no retry/fallback (providerRequests={requests.Count})");
        Assert(requests[1].Contains("\"type\":\"function_call_output\"", StringComparison.Ordinal)
            && requests[1].Contains("p3-command-failure-call", StringComparison.Ordinal)
            && requests[1].Contains(marker, StringComparison.Ordinal)
            && requests[1].Contains($"Process exited with code {exitCode}", StringComparison.Ordinal),
            "the exact failed call and partial output were not returned to the configured local Responses fixture");

        Console.WriteLine($"P3_02_PARTIAL_TOOL_FAILURE runtime={identity.Version} item={itemId} requestedExitCode=23 reportedExitCode={exitCode} partialOutputRetained=true outcome=failed attributedTo=Codex-App-Server providerRequests={requests.Count} retryOrFallback=false");
    }
    finally
    {
        var exactRoot = Path.GetFullPath(applicationRoot);
        if (Directory.Exists(exactRoot)
            && exactRoot.StartsWith(testParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var file in Directory.EnumerateFiles(exactRoot, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            }))
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                }
            }

            await DeleteTemporaryQualificationRootWithRetryAsync(exactRoot, testParent);
        }

        if (Directory.Exists(testParent) && !Directory.EnumerateFileSystemEntries(testParent).Any())
        {
            Directory.Delete(testParent);
        }
    }
}

static async Task RunUnrestrictedCommandInterruptionAsync()
{
    const string marker = "NEOBABYLON_P3_02_COMMAND_INTERRUPTED_MARKER";
    const string lateMarker = "NEOBABYLON_P3_02_COMMAND_MUST_NOT_FINISH";
    const string transportMarker = "NEOBABYLON_P3_02_TRANSPORT_COMMAND_MARKER";
    const string transportLateMarker = "NEOBABYLON_P3_02_TRANSPORT_COMMAND_MUST_NOT_FINISH";
    var sourceRoot = Directory.GetCurrentDirectory();
    var identity = RuntimeIdentity.LoadVerified(Path.Combine(sourceRoot, "runtime", "runtime-lock.json"));
    var qaParent = Path.Combine(sourceRoot, ".local", "Lab", "Runs");
    var testParent = Path.Combine(qaParent, $"P3-02-command-interruption-{Guid.NewGuid():N}");
    var applicationRoot = Path.Combine(testParent, "App");
    var layout = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
    Directory.CreateDirectory(layout.FixtureWorkspace);

    var scriptPath = Path.Combine(layout.FixtureWorkspace, "p3-02-interrupt.ps1");
    var startedPath = Path.Combine(layout.FixtureWorkspace, "command-started.txt");
    var processIdentityPath = Path.Combine(layout.FixtureWorkspace, "command-process.txt");
    var latePath = Path.Combine(layout.FixtureWorkspace, "command-finished.txt");
    var transportScriptPath = Path.Combine(layout.FixtureWorkspace, "p3-02-transport-interrupt.ps1");
    var transportStartedPath = Path.Combine(layout.FixtureWorkspace, "transport-command-started.txt");
    var transportProcessIdentityPath = Path.Combine(layout.FixtureWorkspace, "transport-command-process.txt");
    var transportLatePath = Path.Combine(layout.FixtureWorkspace, "transport-command-finished.txt");
    Assert(!new[] { scriptPath, startedPath, processIdentityPath, latePath, transportScriptPath,
        transportStartedPath, transportProcessIdentityPath, transportLatePath }.Any(path =>
        path.Any(char.IsWhiteSpace) || path.Contains('\'')),
        "the isolated PowerShell command paths must not require shell escaping");

    var script = $"[Console]::Out.WriteLine('{marker}'); [Console]::Out.Flush(); $process = Get-Process -Id $PID; $identity = [string]$PID + '|' + [string]$process.StartTime.ToUniversalTime().Ticks; [System.IO.File]::WriteAllText('{processIdentityPath}', $identity); [System.IO.File]::WriteAllText('{startedPath}', 'started'); Start-Sleep -Seconds 60; [Console]::Out.WriteLine('{lateMarker}'); [System.IO.File]::WriteAllText('{latePath}', 'late');";
    File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    var command = $"& '{scriptPath}'";
    var transportScript = $"[Console]::Out.WriteLine('{transportMarker}'); [Console]::Out.Flush(); $process = Get-Process -Id $PID; $identity = [string]$PID + '|' + [string]$process.StartTime.ToUniversalTime().Ticks; [System.IO.File]::WriteAllText('{transportProcessIdentityPath}', $identity); [System.IO.File]::WriteAllText('{transportStartedPath}', 'started'); Start-Sleep -Seconds 60; [Console]::Out.WriteLine('{transportLateMarker}'); [System.IO.File]::WriteAllText('{transportLatePath}', 'late');";
    File.WriteAllText(transportScriptPath, transportScript, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    var transportCommand = $"& '{transportScriptPath}'";
    var responses = new[]
    {
        ResponsesSse.FunctionCall(
            "p3-command-interrupt-response",
            "p3-command-interrupt-call",
            "exec_command",
            new JsonObject
            {
                ["cmd"] = command,
                ["workdir"] = layout.FixtureWorkspace
            }.ToJsonString()),
        ResponsesSse.FunctionCall(
            "p3-command-transport-response",
            "p3-command-transport-call",
            "exec_command",
            new JsonObject
            {
                ["cmd"] = transportCommand,
                ["workdir"] = layout.FixtureWorkspace
            }.ToJsonString()),
        ResponsesSse.AssistantMessage(
            "p3-command-transport-followup",
            "p3-command-transport-followup-message",
            "This response must not be requested after App Server transport is lost.")
    };
    await using var fixture = await UnrestrictedResponsesFixture.StartAsync(responseSequence: responses);
    var ownedCommandProcesses = new List<(int ProcessId, long StartTimeUtcTicks)>();

    try
    {
        var sourceCapability = JsonSerializer.Deserialize<ModelCapabilityRecord>(
            File.ReadAllText(Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("The pinned LM Studio capability record is empty.");
        var capability = sourceCapability with { Endpoint = fixture.BaseUrl };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await using var supervisor = new RuntimeSupervisor(sourceRoot, applicationRoot);
        var startedThread = await supervisor.StartThreadAsync(capability, timeout.Token);
        var threadId = startedThread["thread"]?["thread"]?["id"]?.GetValue<string>()
            ?? throw new InvalidDataException("The pinned App Server did not return a thread id.");

        var turnTask = supervisor.StartTurnAsync(
            "Run the one isolated command and wait until I stop it.", timeout.Token);
        await fixture.WaitForFirstRequestAsync(TimeSpan.FromSeconds(20));
        var startDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (!File.Exists(startedPath) && !turnTask.IsCompleted && DateTime.UtcNow < startDeadline)
        {
            await Task.Delay(50, timeout.Token);
        }

        if (!File.Exists(startedPath) && turnTask.IsCompleted)
        {
            var earlyOutcome = await turnTask;
            throw new InvalidOperationException($"The fixture command ended before the interrupt probe: {earlyOutcome.ToJsonString()}");
        }

        Assert(File.Exists(startedPath), "the fixed command did not enter its controlled wait before the test deadline");
        Assert(fixture.RequestBodies.Count == 1, "the first tool call did not use exactly one fixture request before interruption");
        await Task.Delay(500, timeout.Token);

        var interrupt = await supervisor.InterruptTurnAsync(timeout.Token);
        Assert(interrupt["eventType"]?.GetValue<string>() == "turnInterruptionRequested"
            && interrupt["attributedTo"]?.GetValue<string>() == "Codex App Server"
            && interrupt["threadId"]?.GetValue<string>() == threadId
            && interrupt["terminal"]?.GetValue<bool>() == false,
            "the stop request was not attributed to the exact active App Server thread");

        var outcome = await turnTask.WaitAsync(TimeSpan.FromSeconds(20));
        Assert(outcome["terminal"]?.GetValue<bool>() == true
            && outcome["interrupted"]?.GetValue<bool>() == true
            && outcome["turnStatus"]?.GetValue<string>() == "interrupted"
            && outcome["eventType"]?.GetValue<string>() == "turnInterrupted"
            && outcome["threadId"]?.GetValue<string>() == threadId
            && outcome["turnId"]?.GetValue<string>() == interrupt["turnId"]?.GetValue<string>(),
            $"the active command did not finish as the attributed interrupted turn: {outcome.ToJsonString()}");

        var processParts = File.ReadAllText(processIdentityPath).Split('|');
        if (processParts.Length != 2
            || !int.TryParse(processParts[0], out var commandProcessId)
            || !long.TryParse(processParts[1], out var commandStartTicks))
        {
            throw new InvalidDataException("The controlled command did not record its exact shell-process identity.");
        }
        ownedCommandProcesses.Add((commandProcessId, commandStartTicks));

        Assert(outcome["commandExecutionDiscoveryStatus"]?.GetValue<string>() == "observed",
            $"the host did not complete exact interrupted-command discovery: {outcome.ToJsonString()}");
        Assert(outcome["commandExecutionDiscoveryFailure"] is null,
            "the host returned a discovery failure alongside a current, observed command identity");
        var continuingCommands = (outcome["continuingCommands"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
        var continuingCommand = continuingCommands.SingleOrDefault(item => item["commandStopState"]?.GetValue<string>() == "running")
            ?? throw new InvalidDataException("The exact still-running command was not exposed to the diagnostic host.");
        var commandItemId = continuingCommand["itemId"]?.GetValue<string>()
            ?? throw new InvalidDataException("The continuing command omitted its App Server item identity.");
        Assert(continuingCommand["commandStopAvailable"]?.GetValue<bool>() == true,
            "the host did not expose Stop for the command with an observed process identity");

        const string collidingThreadId = "p3-command-stop-colliding-thread";
        supervisor.RegisterCommandIdentityForApprovalQaTest(
            collidingThreadId,
            "p3-command-stop-colliding-turn",
            commandItemId,
            "987654",
            "must not be stopped from the wrong thread");
        var ambiguousStop = await supervisor.StopCommandAsync(commandItemId, timeout.Token);
        Assert(ambiguousStop["attributedTo"]?.GetValue<string>() == "NeoBabylon.Host"
            && ambiguousStop["status"]?.GetValue<string>() == "identity_mismatch"
            && ambiguousStop["commandStopAvailable"]?.GetValue<bool>() == false
            && IsSameProcessRunning(commandProcessId, commandStartTicks),
            "a colliding item ID across threads was not rejected without touching either command");
        supervisor.RemoveCommandIdentityForApprovalQaTest(collidingThreadId, commandItemId);

        using var cancelledStopRequest = new CancellationTokenSource();
        cancelledStopRequest.Cancel();
        var sameServerUncertainty = await supervisor.StopCommandAsync(commandItemId, cancelledStopRequest.Token);
        Assert(sameServerUncertainty["attributedTo"]?.GetValue<string>() == "NeoBabylon.Host"
            && sameServerUncertainty["status"]?.GetValue<string>() == "unknown"
            && sameServerUncertainty["commandStopAvailable"]?.GetValue<bool>() == true
            && IsSameProcessRunning(commandProcessId, commandStartTicks),
            "a cancelled Stop request did not preserve a retry limited to the same live App Server instance");

        supervisor.ReturnFailedForNextCommandStopForApprovalQaTest(
            threadId,
            commandItemId);
        var typedFailedStop = await supervisor.StopCommandAsync(commandItemId, timeout.Token);
        var commandAliveAfterTypedFailedStop = IsSameProcessRunning(commandProcessId, commandStartTicks);
        Assert(typedFailedStop["eventType"]?.GetValue<string>() == "commandExecutionStopResult"
            && typedFailedStop["attributedTo"]?.GetValue<string>() == "Codex App Server"
            && typedFailedStop["threadId"]?.GetValue<string>() == threadId
            && typedFailedStop["turnId"]?.GetValue<string>() == outcome["turnId"]?.GetValue<string>()
            && typedFailedStop["itemId"]?.GetValue<string>() == commandItemId
            && typedFailedStop["status"]?.GetValue<string>() == "failed"
            && typedFailedStop["commandStopAvailable"]?.GetValue<bool>() == true
            && commandAliveAfterTypedFailedStop,
            $"a typed failed stop did not preserve a retryable exact identity: {typedFailedStop.ToJsonString()}");

        var stopResult = await supervisor.StopCommandAsync(commandItemId, timeout.Token);
        Assert(stopResult["eventType"]?.GetValue<string>() == "commandExecutionStopResult"
            && stopResult["attributedTo"]?.GetValue<string>() == "Codex App Server"
            && stopResult["threadId"]?.GetValue<string>() == threadId
            && stopResult["turnId"]?.GetValue<string>() == outcome["turnId"]?.GetValue<string>()
            && stopResult["itemId"]?.GetValue<string>() == commandItemId
            && stopResult["status"]?.GetValue<string>() == "stopped"
            && stopResult["processId"] is null && stopResult["expectedProcessId"] is null,
            $"the exact command stop was not attributed or exposed safely: {stopResult.ToJsonString()}");

        var stopDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (IsSameProcessRunning(commandProcessId, commandStartTicks) && DateTime.UtcNow < stopDeadline)
        {
            await Task.Delay(50, timeout.Token);
        }
        var commandProcessAliveAfterStop = IsSameProcessRunning(commandProcessId, commandStartTicks);
        Assert(!commandProcessAliveAfterStop, "the exact command was still alive after App Server confirmed Stop");
        var lateMarkerObserved = File.Exists(latePath);

        var duplicateStop = await supervisor.StopCommandAsync(commandItemId, timeout.Token);
        Assert(duplicateStop["eventType"]?.GetValue<string>() == "commandExecutionStopResult"
            && duplicateStop["attributedTo"]?.GetValue<string>() == "NeoBabylon.Host"
            && duplicateStop["status"]?.GetValue<string>() == "not_found",
            $"a duplicate stop did not fail closed after the first exact stop: {duplicateStop.ToJsonString()}");

        var toolDiagnostics = (outcome["toolDiagnostics"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
        var commandDiagnostic = toolDiagnostics.SingleOrDefault(item => item["itemType"]?.GetValue<string>() == "commandExecution");
        var commandOutput = commandDiagnostic?["output"]?.GetValue<string>();
        var partialOutputRetained = commandOutput?.Contains(marker, StringComparison.Ordinal) == true;
        Assert(fixture.RequestBodies.Count == 1,
            $"interrupted command output was automatically continued or retried (providerRequests={fixture.RequestBodies.Count})");

        var transportTurnTask = supervisor.StartTurnAsync(
            "Run the second isolated command and wait until the App Server disconnect is tested.", timeout.Token);
        var secondRequestDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (fixture.RequestBodies.Count < 2 && !transportTurnTask.IsCompleted && DateTime.UtcNow < secondRequestDeadline)
        {
            await Task.Delay(50, timeout.Token);
        }
        Assert(fixture.RequestBodies.Count == 2, "the second deterministic command turn did not reach the loopback provider");
        var secondCommandDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (!File.Exists(transportStartedPath) && !transportTurnTask.IsCompleted && DateTime.UtcNow < secondCommandDeadline)
        {
            await Task.Delay(50, timeout.Token);
        }
        Assert(File.Exists(transportStartedPath), "the transport-failure command did not enter its controlled wait");
        var secondCommandParts = File.ReadAllText(transportProcessIdentityPath).Split('|');
        if (secondCommandParts.Length != 2
            || !int.TryParse(secondCommandParts[0], out var transportProcessId)
            || !long.TryParse(secondCommandParts[1], out var transportStartTicks))
        {
            throw new InvalidDataException("The transport-failure command did not record its exact shell-process identity.");
        }
        ownedCommandProcesses.Add((transportProcessId, transportStartTicks));
        var secondInterrupt = await supervisor.InterruptTurnAsync(timeout.Token);
        var secondOutcome = await transportTurnTask.WaitAsync(TimeSpan.FromSeconds(20));
        Assert(secondOutcome["interrupted"]?.GetValue<bool>() == true
            && secondOutcome["turnId"]?.GetValue<string>() == secondInterrupt["turnId"]?.GetValue<string>(),
            $"the second command turn was not interrupted before the transport-failure probe: {secondOutcome.ToJsonString()}");
        var transportContinuingCommand = ((secondOutcome["continuingCommands"] as JsonArray)?.OfType<JsonObject>() ?? [])
            .SingleOrDefault(item => item["commandStopState"]?.GetValue<string>() == "running")
            ?? throw new InvalidDataException("the second continuing command lacked an identity-bound Stop action");
        var transportItemId = transportContinuingCommand["itemId"]?.GetValue<string>()
            ?? throw new InvalidDataException("the transport-failure command omitted its item identity");

        var appServerIdentity = supervisor.PinnedAppServerProcessIdentity
            ?? throw new InvalidDataException("the QA build did not expose the exact App Server process identity");
        using (var appServerProcess = Process.GetProcessById(appServerIdentity.ProcessId))
        {
            Assert(appServerProcess.StartTime.ToUniversalTime().Ticks == appServerIdentity.StartTimeUtcTicks,
                "the App Server process identity changed before the controlled transport-failure test");
            appServerProcess.Kill();
            Assert(appServerProcess.WaitForExit(5_000), "the isolated App Server did not exit after its exact process was stopped");
        }

        var transportFailure = await supervisor.StopCommandAsync(transportItemId, timeout.Token);
        Assert(transportFailure["eventType"]?.GetValue<string>() == "commandExecutionStopResult"
            && transportFailure["attributedTo"]?.GetValue<string>() == "NeoBabylon.Host"
            && transportFailure["status"]?.GetValue<string>() == "unknown"
            && transportFailure["commandStopAvailable"]?.GetValue<bool>() == false
            && transportFailure["failure"]?["type"] is not null,
            $"App Server replacement did not invalidate the old command identity: {transportFailure.ToJsonString()}");
        var replacementThreads = await supervisor.ListThreadsAsync(capability, null, timeout.Token);
        Assert(replacementThreads["threads"] is JsonArray,
            "the replacement App Server did not return its thread list before the stale-stop check");
        var replacementServerIdentity = supervisor.PinnedAppServerProcessIdentity
            ?? throw new InvalidDataException("the QA host did not start a replacement App Server instance");
        Assert(replacementServerIdentity != appServerIdentity,
            "the App Server process identity did not change after the old server was terminated");
        var staleStopAfterReplacement = await supervisor.StopCommandAsync(transportItemId, timeout.Token);
        Assert(staleStopAfterReplacement["attributedTo"]?.GetValue<string>() == "NeoBabylon.Host"
            && staleStopAfterReplacement["status"]?.GetValue<string>() == "not_found"
            && staleStopAfterReplacement["commandStopAvailable"]?.GetValue<bool>() == false,
            "an old server's command identity survived into the replacement App Server instance");
        Assert(fixture.RequestBodies.Count == 2,
            $"the interrupted command or transport failure triggered a provider continuation/fallback (providerRequests={fixture.RequestBodies.Count})");

        var resultPath = Path.Combine(testParent, "result.json");
        var result = new JsonObject
        {
            ["passed"] = true,
            ["probe"] = "P3-02 exact command stop and transport-failure qualification",
            ["runtimeVersion"] = identity.Version,
            ["runtimeRevision"] = identity.SourceRevision,
            ["runtimeSha256"] = identity.Sha256,
            ["turnStatus"] = outcome["turnStatus"]?.DeepClone(),
            ["turnEventType"] = outcome["eventType"]?.DeepClone(),
            ["commandProcessAliveOneSecondAfterStop"] = commandProcessAliveAfterStop,
            ["commandProcessId"] = commandProcessId,
            ["commandStopStatus"] = stopResult["status"]?.DeepClone(),
            ["typedFailedStopStatus"] = typedFailedStop["status"]?.DeepClone(),
            ["typedFailedStopRetryAvailable"] = typedFailedStop["commandStopAvailable"]?.DeepClone(),
            ["commandAliveAfterTypedFailedStop"] = commandAliveAfterTypedFailedStop,
            ["ambiguousItemStopStatus"] = ambiguousStop["status"]?.DeepClone(),
            ["sameServerUncertainStopStatus"] = sameServerUncertainty["status"]?.DeepClone(),
            ["duplicateStopStatus"] = duplicateStop["status"]?.DeepClone(),
            ["transportFailureStatus"] = transportFailure["status"]?.DeepClone(),
            ["transportFailureRetryAvailable"] = transportFailure["commandStopAvailable"]?.DeepClone(),
            ["staleStopAfterReplacementStatus"] = staleStopAfterReplacement["status"]?.DeepClone(),
            ["serverEpochChanged"] = replacementServerIdentity != appServerIdentity,
            ["transportCommandAliveAtFailure"] = IsSameProcessRunning(transportProcessId, transportStartTicks),
            ["partialOutputRetainedInTurnDiagnostics"] = partialOutputRetained,
            ["partialOutput"] = commandOutput,
            ["lateMarkerObservedBeforeCleanup"] = lateMarkerObserved,
            ["providerRequestCount"] = fixture.RequestBodies.Count,
            ["continuationOrFallback"] = false,
            ["applicationRoot"] = applicationRoot,
            ["dataRoot"] = layout.DataRoot,
            ["ordinaryCodexRootUsed"] = false
        };
        File.WriteAllText(resultPath, result.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"P3_02_COMMAND_STOP_PROBE runtime={identity.Version} ambiguous={ambiguousStop["status"]?.GetValue<string>()} sameServerRetry={sameServerUncertainty["commandStopAvailable"]?.GetValue<bool>()} typedFailed={typedFailedStop["status"]?.GetValue<string>()} typedFailedRetry={typedFailedStop["commandStopAvailable"]?.GetValue<bool>()} aliveAfterTypedFailure={commandAliveAfterTypedFailedStop} stop={stopResult["status"]?.GetValue<string>()} duplicate={duplicateStop["status"]?.GetValue<string>()} replacedServer={transportFailure["commandStopAvailable"]?.GetValue<bool>()} staleRetry={staleStopAfterReplacement["status"]?.GetValue<string>()} partialOutputRetained={partialOutputRetained} providerRequests={fixture.RequestBodies.Count} result={resultPath}");
    }
    finally
    {
        foreach (var (processId, startTimeUtcTicks) in ownedCommandProcesses)
        {
            if (IsSameProcessRunning(processId, startTimeUtcTicks))
            {
                using var commandProcess = Process.GetProcessById(processId);
                if (commandProcess.StartTime.ToUniversalTime().Ticks == startTimeUtcTicks)
                {
                    commandProcess.Kill();
                    commandProcess.WaitForExit(5_000);
                }
            }
        }

        var exactRoot = Path.GetFullPath(applicationRoot);
        var parentPrefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(testParent)) + Path.DirectorySeparatorChar;
        if (Directory.Exists(exactRoot)
            && exactRoot.StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var file in Directory.EnumerateFiles(exactRoot, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            }))
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                }
            }

            await DeleteTemporaryQualificationRootWithRetryAsync(exactRoot, testParent);
        }

        if (Directory.Exists(testParent) && !Directory.EnumerateFileSystemEntries(testParent).Any())
        {
            Directory.Delete(testParent);
        }
    }

    static bool IsSameProcessRunning(int processId, long startTimeUtcTicks)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.StartTime.ToUniversalTime().Ticks == startTimeUtcTicks;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

static async Task RunEffectiveContextCompactionQualificationAsync()
{
    var sourceRoot = Directory.GetCurrentDirectory();
    var identity = RuntimeIdentity.LoadVerified(Path.Combine(sourceRoot, "runtime", "runtime-lock.json"));
    var testParent = NewQaRunRoot(sourceRoot, "context-compaction");
    var applicationRoot = Path.Combine(testParent, "App");
    var layout = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
    var responses = new[]
    {
        UnrestrictedResponsesFixture.AssistantMessageWithTokens("context-response-1", "context-message-1", "First fixture reply.", 500),
        UnrestrictedResponsesFixture.AssistantMessageWithTokens("context-response-2", "context-message-2", "Second fixture reply.", 1900),
        UnrestrictedResponsesFixture.AssistantMessageWithTokens("context-response-3", "context-message-3", "Local compaction summary.", 200),
        UnrestrictedResponsesFixture.AssistantMessageWithTokens("context-response-4", "context-message-4", "Final fixture reply.", 120)
    };
    await using var fixture = await UnrestrictedResponsesFixture.StartAsync(responseSequence: responses);

    try
    {
        var sourceCapability = JsonSerializer.Deserialize<ModelCapabilityRecord>(
            File.ReadAllText(Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("The pinned LM Studio capability record is empty.");
        var capability = sourceCapability with
        {
            Endpoint = fixture.BaseUrl,
            ContextWindowAdvertised = CapabilityObservation.Known(2048, "test-only context-compaction fixture; not provider metadata"),
            ContextWindowEffective = CapabilityObservation.Known(2048, "test-only context-compaction fixture; not provider metadata")
        };
        CodexConfigBuilder.WriteIsolated(
            layout.CodexHome, capability, toolExecutionPolicy: ToolExecutionPolicy.Unrestricted);

        var catalogPath = Path.Combine(layout.CodexHome, "model-catalog.json");
        var catalog = JsonNode.Parse(File.ReadAllText(catalogPath))?.AsObject()
            ?? throw new InvalidDataException("The isolated test model catalog is malformed.");
        var model = catalog["models"]?[0] as JsonObject
            ?? throw new InvalidDataException("The isolated test model catalog has no model entry.");
        Assert(model["context_window"]?.GetValue<int>() == 2048, "the effective context did not reach the Codex model catalog");
        Assert(model["max_context_window"]?.GetValue<int>() == 2048, "the test model maximum context did not match the selected capability");
        Assert(model["effective_context_window_percent"]?.GetValue<int>() == 95, "the Codex effective-context buffer was unexpectedly changed");
        Assert(model["auto_compact_token_limit"] is null, "NeoBabylon invented an explicit auto-compaction limit");
        Assert(File.ReadAllText(Path.Combine(layout.CodexHome, "config.toml"))
            .Contains("model_context_window = 2048", StringComparison.Ordinal),
            "the selected effective context did not reach the isolated Codex configuration");

        await using var client = AppServerClient.Start(new AppServerLaunchOptions(
            identity.BinaryPath, layout.FixtureWorkspace, layout.CodexHome, fixture.BaseUrl));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var initialized = await client.InitializeAsync(timeout.Token);
        AppServerLaunchEnvironment.RequireCodexHome(initialized, layout.CodexHome);
        var effectiveConfig = await client.RequestResultAsync(
            id => AppServerProtocol.BuildConfigReadRequest(id, layout.FixtureWorkspace), timeout.Token);
        var thread = await client.RequestResultAsync(
            id => AppServerProtocol.BuildThreadStartRequest(id, new ThreadStartOptions(
                capability.ModelIdentifier,
                capability.ProviderId,
                layout.FixtureWorkspace,
                "danger-full-access",
                "never",
                CapabilityAdapter.ToCodexConfig(capability))), timeout.Token);
        var authority = SandboxAuthorityDiagnostics.RequireUnrestricted(effectiveConfig, thread);
        Assert(authority["effectiveSandboxType"]?.GetValue<string>() == "dangerFullAccess",
            "the deterministic context test did not use the explicitly selected unrestricted authority");
        var threadId = thread["thread"]?["id"]?.GetValue<string>()
            ?? throw new InvalidDataException("Pinned App Server did not return a context test thread id.");

        TurnObservation? lastTurn = null;
        for (var index = 0; index < 3; index++)
        {
            var started = await client.RequestResultAsync(
                id => AppServerProtocol.BuildTurnStartRequest(id, new TurnStartOptions(
                    threadId, $"Short deterministic context qualification turn {index + 1}.",
                    ToolExecutionPolicy.Unrestricted)), timeout.Token);
            var turnId = started["turn"]?["id"]?.GetValue<string>()
                ?? throw new InvalidDataException("Pinned App Server did not return a context test turn id.");
            lastTurn = await client.WaitForTurnCompletionAsync(
                TimeSpan.FromSeconds(50), timeout.Token, threadId, turnId);
            Assert(lastTurn.Completed, $"context fixture turn {index + 1} did not complete: {lastTurn.Failure ?? "unknown"}");
        }

        var thirdTurn = lastTurn ?? throw new InvalidDataException("The deterministic context test did not run its final turn.");
        var startedCompaction = thirdTurn.Notifications.FirstOrDefault(notification =>
            notification.Method == "item/started" && IsContextCompactionNotification(notification));
        var completedCompaction = thirdTurn.Notifications.FirstOrDefault(notification =>
            notification.Method == "item/completed" && IsContextCompactionNotification(notification));
        Assert(startedCompaction is not null, "crossing the 1843-token derived threshold emitted no compaction-start event");
        Assert(completedCompaction is not null, "the context compaction emitted no completion event");
        var compactionId = startedCompaction!.Params["item"]?["id"]?.GetValue<string>();
        Assert(!string.IsNullOrWhiteSpace(compactionId)
            && compactionId == completedCompaction!.Params["item"]?["id"]?.GetValue<string>(),
            "context-compaction lifecycle events did not refer to the same item");
        Assert(fixture.RequestBodies.Count == 4,
            $"expected three user turns plus one local compaction request, got {fixture.RequestBodies.Count} Responses requests");

        var sessionDirectory = Path.Combine(layout.CodexHome, "sessions");
        var journalContextWindow = Directory.Exists(sessionDirectory)
            ? Directory.EnumerateFiles(sessionDirectory, "*.jsonl", SearchOption.AllDirectories)
                .Select(path => SessionJournalToolEvidenceReader.ReadAppended(path, layout.CodexHome, 0).ModelContextWindow)
                .FirstOrDefault(contextWindow => contextWindow is not null)
            : null;
        Assert(journalContextWindow == 1945,
            $"App Server's task_started journal reported usable context {journalContextWindow?.ToString() ?? "Unknown"}, expected 1945 after its 95% context buffer");
        Console.WriteLine($"CONTEXT_COMPACTION_QA catalogContext=2048 codexUsableContext={journalContextWindow} derivedThreshold=1843 secondResponseUsage=1900 compactionItem={compactionId} providerRequests={fixture.RequestBodies.Count} runtimeSha={identity.Sha256}");
    }
    finally
    {
        var exactRoot = Path.GetFullPath(applicationRoot);
        if (Directory.Exists(exactRoot)
            && exactRoot.StartsWith(testParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            await DeleteTemporaryQualificationRootWithRetryAsync(exactRoot, testParent);
        }
    }
}

static async Task DeleteTemporaryQualificationRootWithRetryAsync(string exactRoot, string testParent)
{
    var normalizedRoot = Path.GetFullPath(exactRoot);
    var normalizedParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(testParent));
    var sourceRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
    var localRoot = Path.Combine(sourceRoot, ".local");
    var labRoot = Path.Combine(localRoot, "Lab");
    var runsRoot = Path.Combine(labRoot, "Runs");
    var relativeParts = Path.GetRelativePath(runsRoot, normalizedRoot)
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    var runName = relativeParts[0];
    var suffixStart = runName.LastIndexOf('-') + 1;
    var runRoot = Path.Combine(runsRoot, runName);
    var isGeneratedRun = suffixStart > 1
        && Guid.TryParseExact(runName[suffixStart..], "N", out _);
    var isRunRoot = relativeParts.Length == 1
        && string.Equals(normalizedRoot, runRoot, StringComparison.OrdinalIgnoreCase)
        && string.Equals(normalizedParent, runsRoot, StringComparison.OrdinalIgnoreCase);
    var isApplicationRoot = relativeParts.Length == 2
        && string.Equals(relativeParts[1], "App", StringComparison.OrdinalIgnoreCase)
        && string.Equals(normalizedRoot, Path.Combine(runRoot, "App"), StringComparison.OrdinalIgnoreCase)
        && (string.Equals(normalizedParent, runsRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedParent, runRoot, StringComparison.OrdinalIgnoreCase));
    if (!isGeneratedRun || (!isRunRoot && !isApplicationRoot))
    {
        throw new InvalidOperationException("Refusing to remove a path outside an exact generated qualification run root.");
    }

    var retryDelays = new[] { 250, 500, 1000, 2000, 4000 };
    for (var attempt = 0; ; attempt++)
    {
        try
        {
            foreach (var path in new[] { sourceRoot, localRoot, labRoot, runsRoot, runRoot, normalizedRoot })
            {
                if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException("Refusing to remove a qualification root through a redirected directory.");
                }
            }
            if (Directory.Exists(normalizedRoot))
            {
                var pending = new Stack<string>();
                pending.Push(normalizedRoot);
                while (pending.Count > 0)
                {
                    foreach (var entry in Directory.EnumerateFileSystemEntries(pending.Pop()))
                    {
                        var attributes = File.GetAttributes(entry);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            throw new InvalidOperationException("Refusing to remove a qualification root containing a redirected entry.");
                        }
                        if ((attributes & FileAttributes.Directory) != 0)
                        {
                            pending.Push(entry);
                        }
                        else if ((attributes & FileAttributes.ReadOnly) != 0)
                        {
                            File.SetAttributes(entry, attributes & ~FileAttributes.ReadOnly);
                        }
                    }
                }
                Directory.Delete(normalizedRoot, recursive: true);
            }
            return;
        }
        catch (IOException) when (attempt < retryDelays.Length)
        {
            await Task.Delay(retryDelays[attempt]);
        }
        catch (UnauthorizedAccessException) when (attempt < retryDelays.Length)
        {
            await Task.Delay(retryDelays[attempt]);
        }
    }
}

static async Task AssertReadOnlyQualificationCleanupAsync()
{
    var testParent = NewQaRunRoot(Directory.GetCurrentDirectory(), "readonly-cleanup");
    var exactRoot = Path.Combine(testParent, "App");
    var packDirectory = Path.Combine(exactRoot, "Data", "CodexHome", ".tmp", "plugins-clone-fixture",
        ".git", "objects", "pack");
    var packFile = Path.Combine(packDirectory, "tmp_pack_fixture");
    var indexFile = Path.Combine(packDirectory, "tmp_idx_fixture");
    var packIndexFile = Path.Combine(packDirectory, "pack-fixture.idx");
    var siblingCanary = Path.Combine(testParent, "outside-app-canary.txt");
    Directory.CreateDirectory(packDirectory);
    File.WriteAllText(packFile, "pack fixture");
    File.WriteAllText(indexFile, "index fixture");
    File.WriteAllText(packIndexFile, "pack index fixture");
    File.WriteAllText(siblingCanary, "keep sibling");
    File.SetAttributes(packFile, File.GetAttributes(packFile) | FileAttributes.ReadOnly);
    File.SetAttributes(indexFile, File.GetAttributes(indexFile) | FileAttributes.ReadOnly);
    File.SetAttributes(packIndexFile, File.GetAttributes(packIndexFile) | FileAttributes.ReadOnly);

    try
    {
        Assert((File.GetAttributes(packFile) & FileAttributes.ReadOnly) != 0
            && (File.GetAttributes(indexFile) & FileAttributes.ReadOnly) != 0
            && (File.GetAttributes(packIndexFile) & FileAttributes.ReadOnly) != 0,
            "the cleanup fixture did not create read-only files");
        await DeleteTemporaryQualificationRootWithRetryAsync(exactRoot, testParent);
        Assert(!Directory.Exists(exactRoot), "qualification cleanup left its exact application root");
        Assert(File.ReadAllText(siblingCanary) == "keep sibling",
            "qualification cleanup changed a sibling outside its exact application root");
    }
    finally
    {
        foreach (var file in new[] { packFile, indexFile, packIndexFile })
        {
            if (File.Exists(file))
            {
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }
        }
        if (Directory.Exists(exactRoot))
        {
            await DeleteTemporaryQualificationRootWithRetryAsync(exactRoot, testParent);
        }
        if (File.Exists(siblingCanary))
        {
            File.Delete(siblingCanary);
        }
        if (Directory.Exists(testParent) && !Directory.EnumerateFileSystemEntries(testParent).Any())
        {
            Directory.Delete(testParent);
        }
    }
}

static async Task AssertQualificationCleanupRejectsReparsePointAsync()
{
    var testParent = NewQaRunRoot(Directory.GetCurrentDirectory(), "cleanup-reparse");
    var exactRoot = Path.Combine(testParent, "App");
    var target = Path.Combine(testParent, "redirect-target");
    var canary = Path.Combine(target, "canary.txt");
    var junction = Path.Combine(exactRoot, "redirect");
    Directory.CreateDirectory(exactRoot);
    Directory.CreateDirectory(target);
    File.WriteAllText(canary, "keep target");
    try
    {
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/d /c mklink /J \"{junction}\" \"{target}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("cleanup junction fixture unavailable: cmd.exe did not start");
        Assert(process.WaitForExit(10_000), "cleanup junction fixture unavailable: mklink timed out");
        Assert(process.ExitCode == 0,
            $"cleanup junction fixture unavailable: {process.StandardError.ReadToEnd()} {process.StandardOutput.ReadToEnd()}");
        Assert((File.GetAttributes(junction) & FileAttributes.ReparsePoint) != 0,
            "cleanup junction fixture did not create a reparse point");
        await AssertThrowsAsync<InvalidOperationException>(() =>
            DeleteTemporaryQualificationRootWithRetryAsync(exactRoot, testParent));
        Assert(Directory.Exists(exactRoot) && File.ReadAllText(canary) == "keep target",
            "qualification cleanup changed a redirected target");
    }
    finally
    {
        if (Directory.Exists(junction) && (File.GetAttributes(junction) & FileAttributes.ReparsePoint) != 0)
        {
            Directory.Delete(junction);
        }
        if (Directory.Exists(exactRoot))
        {
            await DeleteTemporaryQualificationRootWithRetryAsync(exactRoot, testParent);
        }
        if (File.Exists(canary))
        {
            File.Delete(canary);
        }
        if (Directory.Exists(target) && !Directory.EnumerateFileSystemEntries(target).Any())
        {
            Directory.Delete(target);
        }
        if (Directory.Exists(testParent) && !Directory.EnumerateFileSystemEntries(testParent).Any())
        {
            Directory.Delete(testParent);
        }
    }
}

static bool IsContextCompactionNotification(AppServerNotification notification)
{
    var itemType = notification.Params["item"]?["type"]?.GetValue<string>();
    return string.Equals(
        itemType?.Replace("_", string.Empty, StringComparison.Ordinal),
        "contextCompaction",
        StringComparison.OrdinalIgnoreCase);
}

static async Task RunPinnedPatchDiffQualificationAsync(
    string? preservedApplicationRoot = null,
    PatchFixtureFormat patchFormat = PatchFixtureFormat.Freeform,
    string? runtimeLockOverride = null)
{
    var sourceRoot = Directory.GetCurrentDirectory();
    var identity = RuntimeIdentity.LoadVerified(runtimeLockOverride
        ?? Path.Combine(sourceRoot, "runtime", "runtime-lock.json"));
    var testParent = Path.Combine(sourceRoot, ".local", "Lab", "Runs");
    var applicationRoot = preservedApplicationRoot is null
        ? Path.Combine(NewQaRunRoot(sourceRoot, "patch-diff"), "App")
        : Path.GetFullPath(preservedApplicationRoot);
    if (preservedApplicationRoot is not null && (Directory.Exists(applicationRoot) || File.Exists(applicationRoot)))
    {
        throw new IOException("The requested patch-review QA application root already exists.");
    }
    var layout = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
    var trackedFile = Path.Combine(layout.FixtureWorkspace, "tracked.txt");
    File.WriteAllText(trackedFile, "before\n");
    const string patch = "*** Begin Patch\n*** Update File: tracked.txt\n@@\n-before\n+after\n*** End Patch";
    await using var fixture = await UnrestrictedResponsesFixture.StartAsync(patchText: patch, patchFormat: patchFormat);
    var capability = (JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The pinned LM Studio capability record is empty.")) with
    {
        Endpoint = fixture.BaseUrl,
        ApplyPatchToolType = CapabilityObservation.Known(
            patchFormat == PatchFixtureFormat.Function ? "function" : "freeform",
            "Deterministic patch-format test fixture")
    };

    try
    {
        CodexConfigBuilder.WriteIsolated(layout.CodexHome, capability, toolExecutionPolicy: ToolExecutionPolicy.Unrestricted);
        var catalogPath = Path.Combine(layout.CodexHome, "model-catalog.json");
        var testCatalog = JsonNode.Parse(File.ReadAllText(catalogPath))!.AsObject();
        Assert(testCatalog["models"]![0]!["apply_patch_tool_type"]?.GetValue<string>()
            == (patchFormat == PatchFixtureFormat.Function ? "function" : "freeform"),
            "Codex config did not receive the test capability record's patch format");
        await using var client = AppServerClient.Start(new AppServerLaunchOptions(
            identity.BinaryPath, layout.FixtureWorkspace, layout.CodexHome, fixture.BaseUrl));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var initialized = await client.InitializeAsync(timeout.Token);
        AppServerLaunchEnvironment.RequireCodexHome(initialized, layout.CodexHome);
        var thread = await client.RequestResultAsync(
            id => AppServerProtocol.BuildThreadStartRequest(id, new ThreadStartOptions(
                capability.ModelIdentifier, capability.ProviderId, layout.FixtureWorkspace,
                "danger-full-access", "never", CapabilityAdapter.ToCodexConfig(capability))), timeout.Token);
        var threadId = thread["thread"]?["id"]?.GetValue<string>()
            ?? throw new InvalidDataException("Pinned App Server did not return a patch test thread id.");
        var startedTurn = await client.RequestResultAsync(
            id => AppServerProtocol.BuildTurnStartRequest(id, new TurnStartOptions(
                threadId, "Apply the isolated fixture patch.", ToolExecutionPolicy.Unrestricted)), timeout.Token);
        var turnId = startedTurn["turn"]?["id"]?.GetValue<string>()
            ?? throw new InvalidDataException("Pinned App Server did not return a patch test turn id.");
        var observation = await client.WaitForTurnCompletionAsync(TimeSpan.FromSeconds(35), timeout.Token);
        Assert(observation.Completed, $"pinned patch fixture turn did not complete: {observation.Failure}");
        Assert(File.ReadAllText(trackedFile) == "after\n", "pinned App Server did not apply the isolated tracked patch");
        Assert(observation.Notifications.Any(notification => notification.Method == "turn/diff/updated"
            && notification.Params["threadId"]?.GetValue<string>() == threadId
            && notification.Params["turnId"]?.GetValue<string>() == turnId
            && notification.Params["diff"]?.GetValue<string>().Contains("after", StringComparison.Ordinal) == true),
            "pinned App Server omitted the attributed turn diff event");
        var saved = await client.RequestResultAsync(
            id => AppServerProtocol.BuildThreadTurnsListRequest(id, threadId), timeout.Token);
        var reviews = ThreadSavedChangeProjector.Project(saved["data"] as JsonArray ?? new JsonArray(), 250_000);
        Assert(reviews.Count > 0 && reviews[0]?["turnId"]?.GetValue<string>() == turnId
            && reviews[0]?["status"]?.GetValue<string>() == "available"
            && reviews[0]?["changes"]?[0]?["diff"]?.GetValue<string>().Contains("after", StringComparison.Ordinal) == true,
            "pinned App Server did not preserve a reviewable saved patch item");
        var expectedOutputType = patchFormat == PatchFixtureFormat.Function ? "function_call_output" : "custom_tool_call_output";
        Assert(fixture.RequestBodies.Any(body => body.Contains($"\"type\":\"{expectedOutputType}\"", StringComparison.Ordinal)),
            "pinned App Server did not send the patch-tool result back to the deterministic provider");
        Assert(fixture.RequestBodies.Count == 2, "the deterministic provider received a replayed or missing patch round trip");
        var providerRequest = JsonNode.Parse(fixture.RequestBodies[0])?.AsObject()
            ?? throw new InvalidDataException("The pinned App Server sent a malformed deterministic provider request.");
        var advertisedTools = providerRequest["tools"] as JsonArray ?? new JsonArray();
        var expectedToolType = patchFormat == PatchFixtureFormat.Function ? "function" : "custom";
        Assert(advertisedTools.Count(tool => tool?["name"]?.GetValue<string>() == "apply_patch"
            && tool?["type"]?.GetValue<string>() == expectedToolType) == 1,
            "the pinned App Server accepted a synthetic patch call without actually advertising apply_patch to the model");
        Console.WriteLine($"PATCH_EVIDENCE runtimeSha={identity.Sha256} turnDiffEvents={observation.Notifications.Count(n => n.Method == "turn/diff/updated")} savedReview=available providerRequests={fixture.RequestBodies.Count} testCatalog={patchFormat}");
        if (preservedApplicationRoot is not null)
        {
            Console.WriteLine($"PATCH_QA_ROOT={applicationRoot}");
            Console.WriteLine($"PATCH_QA_THREAD={threadId}");
        }
    }
    finally
    {
        var exactRoot = Path.GetFullPath(applicationRoot);
        if (preservedApplicationRoot is null && Directory.Exists(exactRoot)
            && exactRoot.StartsWith(testParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            await DeleteTemporaryQualificationRootWithRetryAsync(exactRoot, testParent);
        }
    }
}

static async Task PrepareHistoryPaginationQaAsync(string requestedApplicationRoot, string requestedManifestPath)
{
    var sourceRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
    var applicationRoot = Path.GetFullPath(requestedApplicationRoot);
    var manifestPath = Path.GetFullPath(requestedManifestPath);
    var qaParent = Path.GetDirectoryName(applicationRoot)
        ?? throw new InvalidOperationException("The history QA application root must have a parent directory.");
    var qaParentPrefix = Path.TrimEndingDirectorySeparator(qaParent) + Path.DirectorySeparatorChar;
    if (!IsExactQaRunApplicationRoot(sourceRoot, applicationRoot)
        || !manifestPath.StartsWith(qaParentPrefix, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("History QA state and manifest must be under one isolated .local/Lab/Runs/<id> parent with an exact App root.");
    }

    if (Directory.Exists(applicationRoot) || File.Exists(applicationRoot) || File.Exists(manifestPath))
    {
        throw new IOException("The requested history QA application root and manifest must be new paths.");
    }

    var layout = ApplicationRootLayout.Create(sourceRoot, requestedApplicationRoot);
    const int taskCount = 56;
    var workspace = Path.Combine(qaParent, "workspace");
    var emptyWorkspace = Path.Combine(qaParent, "empty-workspace");
    Directory.CreateDirectory(workspace);
    Directory.CreateDirectory(emptyWorkspace);
    var releaseRoot = Path.Combine(sourceRoot, "docs", "release");
    const string overflowAssistantPrefix = "P2-01 TRANSCRIPT OVERFLOW START: ";
    var overflowAssistantReply = overflowAssistantPrefix
        + new string('x', 120_512)
        + " :P2-01 TRANSCRIPT OVERFLOW END";
    var capabilityRecordPath = Path.Combine(releaseRoot, $"MODEL_CAPABILITY_HISTORY_QA_{Guid.NewGuid():N}.json");
    await using var fixture = await UnrestrictedResponsesFixture.StartAsync(responseSequence:
        Enumerable.Range(0, taskCount)
            .Select(index => ResponsesSse.AssistantMessage(
                $"history-response-{index:D3}",
                $"history-assistant-{index:D3}",
                index == taskCount - 1 ? overflowAssistantReply : $"Saved fixture response {index:D3}."))
            .ToArray());

    var capability = JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(Path.Combine(releaseRoot, "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The canonical LM Studio capability record is empty.");
    capability = capability with { Endpoint = fixture.BaseUrl };

    try
    {
        File.WriteAllText(capabilityRecordPath,
            JsonSerializer.Serialize(capability, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var created = new List<(string Id, string Prompt)>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await using (var projectSetup = new RuntimeSupervisor(sourceRoot, applicationRoot))
        {
            await projectSetup.SelectProjectAsync(workspace, addNew: true, timeout.Token);
            await projectSetup.SelectProjectAsync(emptyWorkspace, addNew: true, timeout.Token);
            await projectSetup.SelectProjectAsync(workspace, addNew: false, timeout.Token);
        }

        CodexConfigBuilder.WriteIsolated(
            layout.CodexHome,
            capability,
            toolExecutionPolicy: ToolExecutionPolicy.Unrestricted);
        var identity = RuntimeIdentity.LoadVerified(Path.Combine(sourceRoot, "runtime", "runtime-lock.json"));
        await using (var client = AppServerClient.Start(new AppServerLaunchOptions(
            identity.BinaryPath,
            workspace,
            layout.CodexHome,
            capability.Endpoint)))
        {
            var initialized = await client.InitializeAsync(timeout.Token);
            AppServerLaunchEnvironment.RequireCodexHome(initialized, layout.CodexHome);
            var effectiveConfig = await client.RequestResultAsync(
                id => AppServerProtocol.BuildConfigReadRequest(id, workspace), timeout.Token);
            for (var index = 0; index < taskCount; index++)
            {
                var started = await client.RequestResultAsync(
                    id => AppServerProtocol.BuildThreadStartRequest(id, new ThreadStartOptions(
                        capability.ModelIdentifier,
                        capability.ProviderId,
                        workspace,
                        "danger-full-access",
                        "never",
                        CapabilityAdapter.ToCodexConfig(capability))), timeout.Token);
                _ = SandboxAuthorityDiagnostics.RequireUnrestricted(effectiveConfig, started);
                ThreadResumeIdentity.RequireExactMatch(started, capability, workspace);
                var threadId = started["thread"]?["id"]?.GetValue<string>()
                    ?? throw new InvalidDataException($"History fixture task {index:D3} has no thread id.");
                var prompt = $"HISTORY P2-01 {index:D3}";
                ThreadCapabilityBindingStore.Bind(layout.DataRoot, threadId, workspace, capability);
                var startedTurn = await client.RequestResultAsync(
                    id => AppServerProtocol.BuildTurnStartRequest(id, new TurnStartOptions(
                        threadId, prompt, ToolExecutionPolicy.Unrestricted)), timeout.Token);
                var turnId = startedTurn["turn"]?["id"]?.GetValue<string>()
                    ?? throw new InvalidDataException($"History fixture task {index:D3} has no turn id.");
                var turn = await client.WaitForTurnCompletionAsync(
                    TimeSpan.FromSeconds(60), timeout.Token, expectedThreadId: threadId, expectedTurnId: turnId);
                Assert(turn.Completed,
                    $"history fixture task {index:D3} did not complete: {turn.Failure ?? turn.Status ?? "unknown failure"}");
                if (index == taskCount - 1)
                {
                    Assert(turn.FinalAssistantText?.Length == AppServerNotificationProjection.AssistantDisplayLimit,
                        "the oversized history fixture did not retain the bounded notification preview");
                    Assert(turn.FinalAssistantDisplay?["originalCharacters"]?.GetValue<int>() == overflowAssistantReply.Length
                            && turn.FinalAssistantDisplay?["omittedCharacters"]?.GetValue<int>()
                                == overflowAssistantReply.Length - AppServerNotificationProjection.AssistantDisplayLimit
                            && turn.FinalAssistantDisplay?["sourceRetained"]?.GetValue<bool>() == true,
                        "the oversized notification did not disclose exact display truncation while retaining App Server history as source");
                }
                else
                {
                    Assert(turn.FinalAssistantText == $"Saved fixture response {index:D3}.",
                        $"history fixture task {index:D3} did not preserve its attributed response");
                }
                created.Add((threadId, prompt));
                if ((index + 1) % 8 == 0 || index + 1 == taskCount)
                {
                    Console.WriteLine($"HISTORY_PAGINATION_QA_PROGRESS={index + 1}/{taskCount}");
                }
            }
        }

        Assert(fixture.RequestBodies.Count == taskCount,
            $"history fixture received {fixture.RequestBodies.Count} provider requests for {taskCount} authored tasks");

        int firstPageCount;
        int secondPageCount;
        string[] firstPageIds;
        string[] secondPageIds;
        await using (var verifier = new RuntimeSupervisor(sourceRoot, applicationRoot))
        {
            var firstPage = await verifier.ListThreadsAsync(capability, null, timeout.Token);
            firstPageCount = (firstPage["threads"] as JsonArray)?.Count ?? 0;
            firstPageIds = (firstPage["threads"] as JsonArray)?.OfType<JsonObject>()
                .Select(thread => thread["id"]?.GetValue<string>() ?? "").ToArray() ?? [];
            var cursor = firstPage["nextCursor"]?.GetValue<string>();
            Assert(!string.IsNullOrWhiteSpace(cursor), "the first WPF history page did not expose older tasks");
            Assert(!firstPageIds.Contains(created[0].Id, StringComparer.Ordinal),
                "the oldest fixture task unexpectedly appeared on the first history page");

            var secondPage = await verifier.ListThreadsAsync(capability, cursor, timeout.Token);
            secondPageCount = (secondPage["threads"] as JsonArray)?.Count ?? 0;
            secondPageIds = (secondPage["threads"] as JsonArray)?.OfType<JsonObject>()
                .Select(thread => thread["id"]?.GetValue<string>() ?? "").ToArray() ?? [];
            Assert(secondPageIds.Contains(created[0].Id, StringComparer.Ordinal),
                "loading one older page did not return the oldest fixture task");
            var observedIds = firstPageIds.Concat(secondPageIds).ToHashSet(StringComparer.Ordinal);
            Assert(observedIds.Count == taskCount,
                $"the first two cursor pages exposed {observedIds.Count} unique tasks, expected {taskCount}");
        }

        Assert(fixture.RequestBodies.Count == taskCount,
            "history list/resume issued an unexpected provider request");

        var manifest = new JsonObject
        {
            ["applicationRoot"] = applicationRoot,
            ["workspace"] = workspace,
            ["emptyWorkspace"] = emptyWorkspace,
            ["capabilityRecordPath"] = capabilityRecordPath,
            ["oldestThreadId"] = created[0].Id,
            ["oldestPrompt"] = created[0].Prompt,
            ["newestPrompt"] = created[^1].Prompt,
            ["assistantReply"] = "Saved fixture response 000.",
            ["overflowThreadId"] = created[^1].Id,
            ["overflowPrompt"] = created[^1].Prompt,
            ["overflowAssistantPrefix"] = overflowAssistantPrefix,
            ["overflowAssistantCharacters"] = overflowAssistantReply.Length,
            ["transcriptProjectionLimit"] = 120_000,
            ["expectedThreadCount"] = taskCount,
            ["firstPageCount"] = firstPageCount,
            ["secondPageCount"] = secondPageCount,
            ["firstPageIds"] = JsonSerializer.SerializeToNode(firstPageIds),
            ["secondPageIds"] = JsonSerializer.SerializeToNode(secondPageIds),
            ["promptByThreadId"] = JsonSerializer.SerializeToNode(
                created.ToDictionary(item => item.Id, item => item.Prompt, StringComparer.Ordinal)),
            ["providerRequestsBeforeNativeOpen"] = fixture.RequestBodies.Count,
            ["sourceRoot"] = sourceRoot,
            ["runtimeSha256"] = RuntimeIdentity.LoadVerified(Path.Combine(sourceRoot, "runtime", "runtime-lock.json")).Sha256
        };
        File.WriteAllText(manifestPath, manifest.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"HISTORY_PAGINATION_QA_READY={manifest.ToJsonString()}");
        await Console.In.ReadLineAsync();
        Console.WriteLine($"HISTORY_PAGINATION_QA_PROVIDER_REQUESTS={fixture.RequestBodies.Count}");
    }
    finally
    {
        var releasePrefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(releaseRoot)) + Path.DirectorySeparatorChar;
        if (capabilityRecordPath.StartsWith(releasePrefix, StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(capabilityRecordPath).StartsWith("MODEL_CAPABILITY_HISTORY_QA_", StringComparison.Ordinal)
            && File.Exists(capabilityRecordPath))
        {
            File.Delete(capabilityRecordPath);
        }
    }
}

static async Task PrepareP2_11NativeOutputQaAsync(
    string requestedApplicationRoot,
    string requestedWorkspace,
    string requestedCapabilityPath,
    string requestedManifestPath)
{
    var sourceRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
    var applicationRoot = Path.GetFullPath(requestedApplicationRoot);
    var workspace = Path.GetFullPath(requestedWorkspace);
    var capabilityPath = Path.GetFullPath(requestedCapabilityPath);
    var manifestPath = Path.GetFullPath(requestedManifestPath);
    var qaParent = Path.GetDirectoryName(applicationRoot)
        ?? throw new InvalidOperationException("The P2-11 application root must have a parent directory.");
    var releaseRoot = Path.GetFullPath(Path.Combine(sourceRoot, "docs", "release"));

    static bool IsWithin(string parent, string candidate)
    {
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(candidate).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    if (!IsExactQaRunApplicationRoot(sourceRoot, applicationRoot)
        || !IsWithin(qaParent, workspace) || !IsWithin(qaParent, manifestPath)
        || !IsWithin(releaseRoot, capabilityPath)
        || !Path.GetFileName(capabilityPath).StartsWith("MODEL_CAPABILITY_P2_11_NATIVE_", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("P2-11 native QA state, workspace, and manifest must stay in one isolated .local/Lab/Runs/<id> parent with an exact App root; only a uniquely named synthetic capability may be created under docs/release.");
    }

    if (Directory.Exists(applicationRoot) || Directory.Exists(workspace)
        || !File.Exists(capabilityPath) || File.Exists(manifestPath))
    {
        throw new IOException("P2-11 native QA requires new application/workspace paths, an existing capability record, and a new manifest path.");
    }

    var capability = JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(capabilityPath), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The P2-11 synthetic capability record is empty.");
    if (!string.Equals(capability.ProviderId, "lmstudio", StringComparison.OrdinalIgnoreCase)
        || !Uri.TryCreate(capability.Endpoint, UriKind.Absolute, out var endpoint)
        || endpoint.Scheme != Uri.UriSchemeHttp
        || !endpoint.IsLoopback
        || !string.Equals(capability.ModelVariant, "P2-11 deterministic loopback fixture; no model weights", StringComparison.Ordinal))
    {
        throw new InvalidDataException("P2-11 native QA accepts only its explicitly synthetic loopback capability record.");
    }

    var layout = ApplicationRootLayout.Create(sourceRoot, requestedApplicationRoot);
    Directory.CreateDirectory(workspace);
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    await using (var projectSetup = new RuntimeSupervisor(sourceRoot, applicationRoot))
    {
        await projectSetup.SelectProjectAsync(workspace, addNew: true, timeout.Token);
    }

    CodexConfigBuilder.WriteIsolated(
        layout.CodexHome,
        capability,
        toolExecutionPolicy: ToolExecutionPolicy.Unrestricted);

    var identity = RuntimeIdentity.LoadVerified(Path.Combine(sourceRoot, "runtime", "runtime-lock.json"));
    var manifest = new JsonObject
    {
        ["applicationRoot"] = applicationRoot,
        ["workspace"] = workspace,
        ["capabilityRecordPath"] = capabilityPath,
        ["endpoint"] = capability.Endpoint,
        ["modelIdentifier"] = capability.ModelIdentifier,
        ["sourceRoot"] = sourceRoot,
        ["runtimeVersion"] = identity.Version,
        ["runtimeRevision"] = identity.SourceRevision,
        ["runtimeSha256"] = identity.Sha256,
        ["runtimeBinaryPath"] = identity.BinaryPath
    };
    File.WriteAllText(manifestPath, manifest.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"P2_11_NATIVE_QA_READY={manifest.ToJsonString()}");
}

static async Task ProbeSavedPatchResumeAsync(string applicationRoot, string threadId)
{
    var sourceRoot = Directory.GetCurrentDirectory();
    var identity = RuntimeIdentity.LoadVerified(Path.Combine(sourceRoot, "runtime", "runtime-lock.json"));
    var layout = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
    var capability = JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The canonical LM Studio capability record is empty.");
    CodexConfigBuilder.WriteIsolated(layout.CodexHome, capability, toolExecutionPolicy: ToolExecutionPolicy.Unrestricted);
    await using var client = AppServerClient.Start(new AppServerLaunchOptions(
        identity.BinaryPath, layout.FixtureWorkspace, layout.CodexHome, capability.Endpoint));
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
    await client.InitializeAsync(timeout.Token);
    Console.WriteLine("RESUME_QA initialized");
    var read = await client.RequestResultAsync(
        id => AppServerProtocol.BuildThreadReadRequest(id, threadId, includeTurns: false), timeout.Token);
    ThreadResumeIdentity.RequireExactRead(read["thread"] as JsonObject, threadId, capability, layout.FixtureWorkspace);
    Console.WriteLine("RESUME_QA threadRead=ok");
    var config = await client.RequestResultAsync(
        id => AppServerProtocol.BuildConfigReadRequest(id, layout.FixtureWorkspace), timeout.Token);
    Console.WriteLine("RESUME_QA configRead=ok");
    var resumed = await client.RequestResultAsync(
        id => AppServerProtocol.BuildThreadResumeRequest(id, threadId, ToolExecutionPolicy.Unrestricted), timeout.Token);
    SandboxAuthorityDiagnostics.RequireUnrestricted(config, resumed);
    Console.WriteLine("RESUME_QA threadResume=ok");
    var turns = await client.RequestResultAsync(
        id => AppServerProtocol.BuildThreadTurnsListRequest(id, threadId), timeout.Token);
    var reviews = ThreadSavedChangeProjector.Project(turns["data"] as JsonArray ?? new JsonArray(), 250_000);
    Assert(reviews.Count > 0 && reviews[0]?["status"]?.GetValue<string>() == "available",
        "the saved patch review was unavailable after App Server restart");
    Console.WriteLine($"RESUME_QA savedReviews={reviews.Count}");
}

static async Task ProbePatchSupervisorAsync(string applicationRoot, string threadId)
{
    var sourceRoot = Directory.GetCurrentDirectory();
    var capability = JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The canonical LM Studio capability record is empty.");
    await using var supervisor = new RuntimeSupervisor(sourceRoot, applicationRoot);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
    var listed = await supervisor.ListThreadsAsync(capability, null, timeout.Token);
    Console.WriteLine($"SUPERVISOR_QA listed={((JsonArray?)listed["threads"])?.Count ?? 0}");
    var resumed = await supervisor.ResumeThreadAsync(capability, threadId, timeout.Token);
    Assert(resumed["savedReviews"] is JsonArray reviews && reviews.Count > 0
        && reviews[0]?["status"]?.GetValue<string>() == "available",
        "the host supervisor did not project the saved patch review");
    var serialized = resumed.ToJsonString();
    Console.WriteLine("SUPERVISOR_QA savedReview=available");
    Console.WriteLine($"SUPERVISOR_QA responseBytes={Encoding.UTF8.GetByteCount(serialized)}");
    var listedAgain = await supervisor.ListThreadsAsync(capability, null, timeout.Token);
    Console.WriteLine($"SUPERVISOR_QA relisted={((JsonArray?)listedAgain["threads"])?.Count ?? 0}");
}

static async Task ProbeLiveOpenRouterPatchAsync(string applicationRoot, string? runtimeLockOverride = null)
{
    var sourceRoot = Directory.GetCurrentDirectory();
    var exactRoot = Path.GetFullPath(applicationRoot);
    if (Directory.Exists(exactRoot) || File.Exists(exactRoot))
    {
        throw new IOException("The requested live-patch QA application root already exists.");
    }

    var identity = RuntimeIdentity.LoadVerified(runtimeLockOverride
        ?? Path.Combine(sourceRoot, "runtime", "runtime-lock.json"));
    var capability = JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The canonical OpenRouter capability record is empty.");
    var route = await OpenRouterInspection.ReadAndValidateAsync(capability);
    var apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY", EnvironmentVariableTarget.Process);
    AppServerLaunchEnvironment.ClearCredentialEnvironmentVariables();
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        throw new InvalidOperationException("The live patch probe requires an OpenRouter key in its process environment; no provider request was made.");
    }

    var layout = ApplicationRootLayout.Create(sourceRoot, exactRoot);
    var trackedFile = Path.Combine(layout.FixtureWorkspace, "tracked.txt");
    File.WriteAllText(trackedFile, "before\n");
    CodexConfigBuilder.WriteIsolated(layout.CodexHome, capability, toolExecutionPolicy: ToolExecutionPolicy.Unrestricted);
    var catalogPath = Path.Combine(layout.CodexHome, "model-catalog.json");
    var testCatalog = JsonNode.Parse(File.ReadAllText(catalogPath))!.AsObject();
    Assert(testCatalog["models"]![0]!["apply_patch_tool_type"]?.GetValue<string>() == "function",
        "the authoritative OpenRouter capability record did not select function-form apply_patch");
    Console.WriteLine($"LIVE_PATCH_QA routePreflight=ok endpointCount={route["route"]?["endpointCount"]?.GetValue<int>() ?? 0} model={capability.ModelIdentifier} runtimeSha={identity.Sha256} capabilityCatalog=function-only");

    await using var client = AppServerClient.Start(new AppServerLaunchOptions(
        identity.BinaryPath, layout.FixtureWorkspace, layout.CodexHome, null, apiKey));
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
    var initialized = await client.InitializeAsync(timeout.Token);
    AppServerLaunchEnvironment.RequireCodexHome(initialized, layout.CodexHome);
    var thread = await client.RequestResultAsync(
        id => AppServerProtocol.BuildThreadStartRequest(id, new ThreadStartOptions(
            capability.ModelIdentifier, capability.ProviderId, layout.FixtureWorkspace,
            "danger-full-access", "never", CapabilityAdapter.ToCodexConfig(capability))), timeout.Token);
    var threadId = thread["thread"]?["id"]?.GetValue<string>()
        ?? throw new InvalidDataException("Pinned App Server did not return a live patch thread id.");
    var startedTurn = await client.RequestResultAsync(
        id => AppServerProtocol.BuildTurnStartRequest(id, new TurnStartOptions(
            threadId,
            "Use the apply_patch tool to change only tracked.txt in the current working directory: replace its single line before with after. Do not run a shell command or touch another path. Then briefly report the result.",
            ToolExecutionPolicy.Unrestricted)), timeout.Token);
    var turnId = startedTurn["turn"]?["id"]?.GetValue<string>()
        ?? throw new InvalidDataException("Pinned App Server did not return a live patch turn id.");
    var observation = await client.WaitForTurnCompletionAsync(
        TimeSpan.FromMinutes(3), timeout.Token, threadId, turnId);
    var itemTypes = observation.Notifications
        .Where(notification => notification.Method is "item/started" or "item/completed")
        .Select(notification => notification.Params["item"]?["type"]?.GetValue<string>() ?? "Unknown")
        .Distinct(StringComparer.Ordinal)
        .OrderBy(value => value, StringComparer.Ordinal);
    Console.WriteLine($"LIVE_PATCH_QA thread={threadId} turn={turnId} terminal={observation.Terminal} status={observation.Status ?? "Unknown"} itemTypes={string.Join(',', itemTypes)} diffEvents={observation.Notifications.Count(notification => notification.Method == "turn/diff/updated")} trackedAfter={File.ReadAllText(trackedFile).Trim()} root={layout.ApplicationRoot}");
    if (!observation.Completed)
    {
        throw new InvalidOperationException($"The live patch turn did not complete: {observation.Failure ?? "Unknown"}");
    }

    var turns = await client.RequestResultAsync(
        id => AppServerProtocol.BuildThreadTurnsListRequest(id, threadId), timeout.Token);
    var reviews = ThreadSavedChangeProjector.Project(turns["data"] as JsonArray ?? new JsonArray(), 250_000);
    Assert(File.ReadAllText(trackedFile) == "after\n", "the real NEX model did not apply the requested isolated patch");
    Assert(reviews.Count > 0 && reviews[0]?["status"]?.GetValue<string>() == "available",
        "the real NEX patch was not saved as a reviewable App Server file-change item");
    Console.WriteLine($"LIVE_PATCH_QA result=pass savedReviews={reviews.Count}");
}

static StartupFailureSnapshot SnapshotStartupFailure(string phase, AppServerClient client) => new(
    phase,
    client.ProcessId,
    client.ProcessStartTimeUtcTicks,
    client.ExitCode,
    client.GetStandardErrorAsync());

static StartupFailureSnapshot? TrySnapshotStartupFailure(string phase, AppServerClient client)
{
    try
    {
        return SnapshotStartupFailure(phase, client);
    }
    catch
    {
        return null;
    }
}

static string ClassifyRedactedStderrLine(string line)
{
    // Only fixed labels leave the QA process. Raw stderr can contain credentials, URLs, or user paths.
    if (line.Contains("database is locked", StringComparison.OrdinalIgnoreCase)) return "sqlite.database_locked";
    if (line.Contains("disk i/o error", StringComparison.OrdinalIgnoreCase)) return "sqlite.disk_io_error";
    if (line.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
        || line.Contains("access is denied", StringComparison.OrdinalIgnoreCase)) return "filesystem.access_denied";
    if (line.Contains("no such file or directory", StringComparison.OrdinalIgnoreCase)) return "filesystem.missing";
    if (line.Contains("panicked", StringComparison.OrdinalIgnoreCase)
        || line.Contains("panic", StringComparison.OrdinalIgnoreCase)) return "process.panic";
    if (line.Contains("error", StringComparison.OrdinalIgnoreCase)) return "error.unspecified";
    if (line.Contains("warn", StringComparison.OrdinalIgnoreCase)) return "warning.unspecified";
    return "redacted";
}

static async Task<string> WriteRecoveryFailureArtifactAsync(
    string sourceRoot,
    string testParent,
    string phase,
    Exception failure,
    StartupFailureSnapshot? startup,
    (int ProcessId, long StartTimeUtcTicks)? previousProcess)
{
    var applicationRoot = Path.Combine(testParent, "App");
    Assert(IsExactQaRunApplicationRoot(sourceRoot, applicationRoot),
        "recovery diagnostics must stay inside an exact isolated Lab/Runs QA root");
    var stderrStatus = startup is null ? "not-captured" : "captured";
    var stderrLength = 0;
    var categories = new List<string>();
    if (startup is not null)
    {
        try
        {
            var stderr = await startup.StandardError.WaitAsync(TimeSpan.FromSeconds(2));
            stderrLength = stderr.Length;
            if (stderr.Length == 0) stderrStatus = "captured-empty";
            foreach (var line in stderr.AsSpan(0, Math.Min(stderr.Length, 8192)).ToString()
                .Split('\n').Take(16).Where(line => line.Length > 0))
            {
                categories.Add(ClassifyRedactedStderrLine(line));
            }
            if (stderr.Length > 8192) stderrStatus = "captured-input-truncated";
        }
        catch (TimeoutException)
        {
            stderrStatus = "read-timeout";
        }
        catch (Exception)
        {
            stderrStatus = "read-error";
        }
    }

    var report = new
    {
        schemaVersion = 1,
        testPhase = phase,
        exceptionType = failure.GetType().FullName,
        previousProcess = previousProcess is null ? null : new
        {
            processId = previousProcess.Value.ProcessId,
            startTimeUtcTicks = previousProcess.Value.StartTimeUtcTicks
        },
        failedStartup = startup is null ? null : new
        {
            phase = startup.Phase,
            processId = startup.ProcessId,
            startTimeUtcTicks = startup.StartTimeUtcTicks,
            exitCode = startup.ExitCode
        },
        stderr = new
        {
            status = stderrStatus,
            inputCharacters = stderrLength,
            categories
        }
    };
    Directory.CreateDirectory(testParent);
    var path = Path.Combine(testParent, "recovery-failure.json");
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    return path;
}

static async Task AssertRecoveryDiagnosticContractAsync()
{
    Assert(typeof(RuntimeSupervisor).GetProperty("ApprovalQaInitializeFailureObserver") is not null,
        "the ApprovalQA host build did not expose its failure-only initialize observer");
    var sourceRoot = Directory.GetCurrentDirectory();
    var testParent = NewQaRunRoot(sourceRoot, "recovery-diagnostic-contract");
    Directory.CreateDirectory(Path.Combine(testParent, "App"));
    const string plantedKey = "sk-planted-secret-123456789";
    const string plantedPath = "C:\\Users\\SecretUser\\credential-file.txt";
    var snapshot = new StartupFailureSnapshot(
        "replacement-initialize", 12345, 987654321, 17,
        Task.FromResult($"ERROR database is locked {plantedKey}\nAuthorization: Bearer {plantedKey}\n{plantedPath}\n"
            + string.Concat(Enumerable.Repeat("warning with redacted detail\n", 30))
            + new string('x', 9000)));
    var artifact = await WriteRecoveryFailureArtifactAsync(
        sourceRoot, testParent, "replacement-initialize", new EndOfStreamException("do not persist messages"),
        snapshot, (12344, 987654320));
    var json = File.ReadAllText(artifact);
    Assert(json.Contains("sqlite.database_locked", StringComparison.Ordinal), "the diagnostic lost the fixed SQLite category");
    Assert(json.Contains("captured-input-truncated", StringComparison.Ordinal)
        && (JsonNode.Parse(json)?["stderr"]?["categories"] as JsonArray)?.Count == 16,
        "the diagnostic did not bound classified stderr");
    Assert(json.Contains("\"processId\": 12345", StringComparison.Ordinal)
        && json.Contains("\"exitCode\": 17", StringComparison.Ordinal),
        "the diagnostic lost replacement process identity or exit code");
    Assert(!json.Contains(plantedKey, StringComparison.Ordinal)
        && !json.Contains(plantedPath, StringComparison.Ordinal)
        && !json.Contains("Authorization", StringComparison.Ordinal)
        && !json.Contains("do not persist messages", StringComparison.Ordinal),
        "the diagnostic persisted unredacted stderr or exception text");
    Assert(Directory.Exists(Path.Combine(testParent, "App")), "the diagnostic did not retain the failed QA root");
    Console.WriteLine($"RECOVERY_DIAGNOSTIC_CONTRACT result=pass artifact={artifact}");
}

static async Task RunPausedTurnRecoveryAsync()
{
    var sourceRoot = Directory.GetCurrentDirectory();
    var identity = RuntimeIdentity.LoadVerified(Path.Combine(sourceRoot, "runtime", "runtime-lock.json"));
    var testParent = NewQaRunRoot(sourceRoot, "paused-turn");
    var applicationRoot = Path.Combine(testParent, "App");
    var layout = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
    await using var mock = await UnrestrictedResponsesFixture.StartAsync(pauseFirstResponse: true);
    var capability = (JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The pinned LM Studio capability record is empty.")) with { Endpoint = mock.BaseUrl };
    var phase = "setup";
    var completed = false;
    StartupFailureSnapshot? startupFailure = null;
    (int ProcessId, long StartTimeUtcTicks)? previousProcess = null;

    try
    {
        CodexConfigBuilder.WriteIsolated(layout.CodexHome, capability, toolExecutionPolicy: ToolExecutionPolicy.Unrestricted);
        string threadId;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await using (var activeClient = AppServerClient.Start(new AppServerLaunchOptions(
            identity.BinaryPath, layout.FixtureWorkspace, layout.CodexHome, mock.BaseUrl)))
        {
            phase = "active-initialize";
            var initialized = await activeClient.InitializeAsync(timeout.Token);
            previousProcess = (activeClient.ProcessId, activeClient.ProcessStartTimeUtcTicks);
            AppServerLaunchEnvironment.RequireCodexHome(initialized, layout.CodexHome);
            phase = "active-thread-start";
            var started = await activeClient.RequestResultAsync(
                id => AppServerProtocol.BuildThreadStartRequest(id, new ThreadStartOptions(
                    capability.ModelIdentifier, capability.ProviderId, layout.FixtureWorkspace,
                    "danger-full-access", "never", CapabilityAdapter.ToCodexConfig(capability))), timeout.Token);
            threadId = started["thread"]?["id"]?.GetValue<string>()
                ?? throw new InvalidDataException("Pinned App Server did not return a thread id.");
            phase = "active-turn-start";
            var turn = await activeClient.RequestResultAsync(
                id => AppServerProtocol.BuildTurnStartRequest(id, new TurnStartOptions(
                    threadId, "Wait for the isolated test response.", ToolExecutionPolicy.Unrestricted)), timeout.Token);
            Assert(turn["turn"]?["status"]?.GetValue<string>() == "inProgress",
                "the paused test turn was not active before App Server exit");
            await mock.WaitForFirstRequestAsync(TimeSpan.FromSeconds(30));
            Assert(mock.RequestBodies.Count == 1, "the paused provider fixture did not receive exactly one request");
        }

        phase = "replacement-start";
        mock.ReleaseFirstResponse();
        await using var recoveryClient = AppServerClient.Start(new AppServerLaunchOptions(
            identity.BinaryPath, layout.FixtureWorkspace, layout.CodexHome, mock.BaseUrl));
        phase = "replacement-initialize";
        JsonObject recoveryInitialized;
        try
        {
            recoveryInitialized = await recoveryClient.InitializeAsync(timeout.Token);
        }
        catch
        {
            startupFailure = TrySnapshotStartupFailure(phase, recoveryClient);
            throw;
        }
        AppServerLaunchEnvironment.RequireCodexHome(recoveryInitialized, layout.CodexHome);
        phase = "replacement-thread-read";
        var read = await recoveryClient.RequestResultAsync(
            id => AppServerProtocol.BuildThreadReadRequest(id, threadId, includeTurns: false), timeout.Token);
        ThreadResumeIdentity.RequireExactRead(
            read["thread"] as JsonObject, threadId, capability, layout.FixtureWorkspace);
        phase = "replacement-turns-list-before-resume";
        var storedTurns = await recoveryClient.RequestResultAsync(
            id => AppServerProtocol.BuildThreadTurnsListRequest(id, threadId), timeout.Token);
        var beforeResume = string.Join(",", (storedTurns["data"] as JsonArray ?? new JsonArray())
            .OfType<JsonObject>().Select(turn => turn["status"]?.GetValue<string>() ?? "<missing>"));
        phase = "replacement-thread-resume";
        var resumed = await recoveryClient.RequestResultAsync(
            id => AppServerProtocol.BuildThreadResumeRequest(id, threadId, ToolExecutionPolicy.Unrestricted), timeout.Token);
        ThreadResumeIdentity.RequireExactMatch(new JsonObject
        {
            ["modelProvider"] = resumed["modelProvider"]?.DeepClone(),
            ["model"] = resumed["model"]?.DeepClone(),
            ["cwd"] = resumed["cwd"]?.DeepClone()
        }, capability, layout.FixtureWorkspace);
        phase = "replacement-turns-list-after-resume";
        var afterResume = await recoveryClient.RequestResultAsync(
            id => AppServerProtocol.BuildThreadTurnsListRequest(id, threadId), timeout.Token);
        var afterStatuses = string.Join(",", (afterResume["data"] as JsonArray ?? new JsonArray())
            .OfType<JsonObject>().Select(turn => turn["status"]?.GetValue<string>() ?? "<missing>"));
        Assert(mock.RequestBodies.Count == 1,
            "reading or resuming a paused turn replayed a provider request without user action");
        Console.WriteLine($"OBSERVE paused-turn recovery: stored=[{beforeResume}], resumed=[{afterStatuses}], providerRequests={mock.RequestBodies.Count}");
        completed = true;
    }
    catch (Exception ex)
    {
        var artifact = await WriteRecoveryFailureArtifactAsync(
            sourceRoot, testParent, phase, ex, startupFailure, previousProcess);
        Console.Error.WriteLine($"RECOVERY_QA_FAILURE artifact={artifact}");
        throw;
    }
    finally
    {
        mock.ReleaseFirstResponse();
        var exactRoot = Path.GetFullPath(applicationRoot);
        if (completed && Directory.Exists(exactRoot)
            && exactRoot.StartsWith(testParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var file in Directory.EnumerateFiles(exactRoot, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            }))
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                }
            }
            await DeleteTemporaryQualificationRootWithRetryAsync(exactRoot, testParent);
        }
    }
}

static async Task RunRepeatedInterruptionRecoveryAsync()
{
    const int attempts = 2;
    var sourceRoot = Directory.GetCurrentDirectory();
    var identity = RuntimeIdentity.LoadVerified(Path.Combine(sourceRoot, "runtime", "runtime-lock.json"));
    var testParent = Path.Combine(sourceRoot, ".local", "Lab", "Runs");
    var capabilityPath = Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json");
    var baseCapability = JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(capabilityPath),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The pinned LM Studio capability record is empty.");
    var response = ResponsesSse.AssistantMessage(
        "cancel-response", "cancel-message", "This delayed fixture response must not be replayed.");

    for (var attempt = 1; attempt <= attempts; attempt++)
    {
        var applicationRoot = Path.Combine(NewQaRunRoot(sourceRoot, "repeated-interruption"), "App");
        var layout = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
        var capabilityFixture = await UnrestrictedResponsesFixture.StartAsync(
            pauseFirstResponse: true,
            responseSequence: [response]);
        var capability = baseCapability with { Endpoint = capabilityFixture.BaseUrl };
        string threadId;
        (int ProcessId, long StartTimeUtcTicks) processIdentity;

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await using (var supervisor = new RuntimeSupervisor(sourceRoot, applicationRoot))
            {
                var started = await supervisor.StartThreadAsync(capability, timeout.Token);
                threadId = started["thread"]?["thread"]?["id"]?.GetValue<string>()
                    ?? throw new InvalidDataException("The isolated supervisor did not return a thread id.");
                processIdentity = supervisor.PinnedAppServerProcessIdentity
                    ?? throw new InvalidOperationException("The isolated supervisor did not expose its active App Server process identity.");

                var turnTask = supervisor.StartTurnAsync(
                    $"Hold the isolated cancellation probe at the provider boundary (attempt {attempt}).",
                    timeout.Token);
                await capabilityFixture.WaitForFirstRequestAsync(TimeSpan.FromSeconds(20));
                Assert(!turnTask.IsCompleted,
                    $"attempt {attempt}: the active generation completed before the stop request");
                Assert(capabilityFixture.RequestBodies.Count == 1,
                    $"attempt {attempt}: the provider fixture did not receive exactly one request before stop");

                var interrupt = await supervisor.InterruptTurnAsync(timeout.Token);
                Assert(interrupt["eventType"]?.GetValue<string>() == "turnInterruptionRequested"
                    && interrupt["attributedTo"]?.GetValue<string>() == "Codex App Server",
                    $"attempt {attempt}: the stop request was not attributed to App Server");
                capabilityFixture.ReleaseFirstResponse();

                var outcome = await turnTask.WaitAsync(TimeSpan.FromSeconds(30));
                Assert(outcome["terminal"]?.GetValue<bool>() == true
                    && outcome["interrupted"]?.GetValue<bool>() == true
                    && outcome["turnStatus"]?.GetValue<string>() == "interrupted"
                    && outcome["eventType"]?.GetValue<string>() == "turnInterrupted",
                    $"attempt {attempt}: cancellation did not produce a typed interrupted terminal outcome");
                Assert(capabilityFixture.RequestBodies.Count == 1,
                    $"attempt {attempt}: cancellation caused a repeated provider request");
            }

            AssertPinnedAppServerExited(processIdentity,
                $"attempt {attempt}: the App Server process survived supervisor disposal");

            (int ProcessId, long StartTimeUtcTicks) recoveredProcessIdentity;
            await using (var recoveredSupervisor = new RuntimeSupervisor(sourceRoot, applicationRoot))
            {
                var recovered = await recoveredSupervisor.ResumeThreadAsync(capability, threadId, timeout.Token);
                Assert(recovered["executionEligible"]?.GetValue<bool>() == true
                    && recovered["threadId"]?.GetValue<string>() == threadId,
                    $"attempt {attempt}: the interrupted saved thread did not resume under its exact capability binding");
                recoveredProcessIdentity = recoveredSupervisor.PinnedAppServerProcessIdentity
                    ?? throw new InvalidOperationException("The recovery supervisor did not expose its active App Server process identity.");
                Assert(capabilityFixture.RequestBodies.Count == 1,
                    $"attempt {attempt}: history resume blindly replayed the interrupted provider request");
            }

            AssertPinnedAppServerExited(recoveredProcessIdentity,
                $"attempt {attempt}: the recovered App Server process survived supervisor disposal");
            Console.WriteLine($"INTERRUPTION_RECOVERY attempt={attempt}/{attempts} terminal=interrupted providerRequests=1 resumedWithoutReplay=true pinnedAppServerExited=true");
        }
        finally
        {
            capabilityFixture.ReleaseFirstResponse();
            await capabilityFixture.DisposeAsync();
            var exactRoot = Path.GetFullPath(applicationRoot);
            var parentPrefix = Path.TrimEndingDirectorySeparator(testParent) + Path.DirectorySeparatorChar;
            if (Directory.Exists(exactRoot)
                && exactRoot.StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                await DeleteTemporaryQualificationRootWithRetryAsync(exactRoot, testParent);
            }
        }
    }

    static bool IsSameProcessRunning((int ProcessId, long StartTimeUtcTicks) identity)
    {
        try
        {
            using var process = Process.GetProcessById(identity.ProcessId);
            return process.StartTime.ToUniversalTime().Ticks == identity.StartTimeUtcTicks;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    static void AssertPinnedAppServerExited((int ProcessId, long StartTimeUtcTicks) identity, string message)
    {
        Assert(!IsSameProcessRunning(identity),
            $"{message}; pinned PID={identity.ProcessId}, startTimeUtcTicks={identity.StartTimeUtcTicks}");
    }
}

static async Task RunModelSwitchRebindAsync()
{
    var sourceRoot = Directory.GetCurrentDirectory();
    var testParent = NewQaRunRoot(sourceRoot, "model-switch");
    var applicationRoot = Path.Combine(testParent, "App");
    var capabilityPath = Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json");
    var baseCapability = JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(capabilityPath),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The pinned LM Studio capability record is empty.");
    var prompts = new[]
    {
        "Model A initial task before explicit switch.",
        "Model B task after explicit switch.",
        "Model A user-authored continuation after exact resume."
    };
    await using var mock = await UnrestrictedResponsesFixture.StartAsync(responseSequence:
    [
        ResponsesSse.AssistantMessage("model-a-response", "model-a-message", "Model A fixture reply."),
        ResponsesSse.AssistantMessage("model-b-response", "model-b-message", "Model B fixture reply."),
        ResponsesSse.AssistantMessage("model-a-resume-response", "model-a-resume-message", "Model A resumed fixture reply.")
    ]);
    var modelA = baseCapability with { Endpoint = mock.BaseUrl };
    var modelB = modelA with { ModelIdentifier = "fixture/alternate-model" };

    try
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await using var supervisor = new RuntimeSupervisor(sourceRoot, applicationRoot);

        var modelAStart = await supervisor.StartThreadAsync(modelA, timeout.Token);
        var modelAThreadId = modelAStart["thread"]?["thread"]?["id"]?.GetValue<string>()
            ?? throw new InvalidDataException("Model A thread identifier is missing.");
        Assert(modelAStart["model"]?.GetValue<string>() == modelA.ModelIdentifier,
            "the initial thread did not report the exact selected model A identity");
        var firstTurn = await supervisor.StartTurnAsync(prompts[0], timeout.Token);
        Assert(firstTurn["completed"]?.GetValue<bool>() == true,
            "the model A fixture turn did not complete");
        Assert(mock.RequestBodies.Count == 1,
            "the initial model A turn did not make exactly one provider request");

        Assert(await supervisor.ResetThreadForCapabilityChangeAsync(timeout.Token),
            "explicit model selection did not close the prior active runtime session");
        var staleTurnRejected = false;
        try
        {
            await supervisor.StartTurnAsync("This must not continue the prior model A thread.", timeout.Token);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Start a pinned thread", StringComparison.Ordinal))
        {
            staleTurnRejected = true;
        }
        Assert(staleTurnRejected,
            "a turn continued on the old thread after the explicit model-selection reset");
        Assert(mock.RequestBodies.Count == 1,
            "a turn attempted to contact the provider before a new model B thread was started");

        var modelBStart = await supervisor.StartThreadAsync(modelB, timeout.Token);
        var modelBThreadId = modelBStart["thread"]?["thread"]?["id"]?.GetValue<string>()
            ?? throw new InvalidDataException("Model B thread identifier is missing.");
        Assert(modelBThreadId != modelAThreadId,
            "switching model reused the prior model A thread identity");
        Assert(modelBStart["model"]?.GetValue<string>() == modelB.ModelIdentifier,
            "the switched thread did not report the exact selected model B identity");
        var secondTurn = await supervisor.StartTurnAsync(prompts[1], timeout.Token);
        Assert(secondTurn["completed"]?.GetValue<bool>() == true,
            "the model B fixture turn did not complete");
        Assert(mock.RequestBodies.Count == 2,
            "the model B turn did not make exactly one additional provider request");

        Assert(await supervisor.ResetThreadForCapabilityChangeAsync(timeout.Token),
            "switching back to the original model did not close the model B runtime session");
        var restoredModelA = await supervisor.ResumeThreadAsync(modelA, modelAThreadId, timeout.Token);
        Assert(restoredModelA["executionEligible"]?.GetValue<bool>() == true
            && restoredModelA["threadId"]?.GetValue<string>() == modelAThreadId
            && restoredModelA["model"]?.GetValue<string>() == modelA.ModelIdentifier,
            "the original task did not resume under its exact saved provider/model identity");
        var restoredTranscript = restoredModelA["turns"]?.ToJsonString() ?? string.Empty;
        Assert(restoredTranscript.Contains(prompts[0], StringComparison.Ordinal)
            && restoredTranscript.Contains("Model A fixture reply.", StringComparison.Ordinal),
            "explicit model switching did not preserve Model A's runtime-owned transcript");
        var thirdTurn = await supervisor.StartTurnAsync(prompts[2], timeout.Token);
        Assert(thirdTurn["completed"]?.GetValue<bool>() == true,
            "the user-authored continuation did not complete after exact Model A resume");
        Assert(mock.RequestBodies.Count == 3,
            "model switching or resume automatically replayed a previous provider request");

        var requestModels = mock.RequestBodies
            .Select(body => JsonNode.Parse(body)?["model"]?.GetValue<string>())
            .ToArray();
        Assert(requestModels.SequenceEqual([modelA.ModelIdentifier, modelB.ModelIdentifier, modelA.ModelIdentifier]),
            $"provider requests did not follow the selected model sequence: {string.Join(",", requestModels.Select(value => value ?? "<missing>"))}");
        for (var index = 0; index < prompts.Length; index++)
        {
            Assert(mock.RequestBodies[index].Contains(prompts[index], StringComparison.Ordinal),
                $"provider request {index + 1} did not correspond to its explicit user-authored prompt");
        }

        Console.WriteLine($"MODEL_SWITCH_QA modelSequence={string.Join("→", requestModels)} distinctThreads=true historyPreserved=true staleTurnRejected=true providerRequests={mock.RequestBodies.Count}");
    }
    finally
    {
        var exactRoot = Path.GetFullPath(applicationRoot);
        var parentPrefix = Path.TrimEndingDirectorySeparator(testParent) + Path.DirectorySeparatorChar;
        if (Directory.Exists(exactRoot)
            && exactRoot.StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var file in Directory.EnumerateFiles(exactRoot, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            }))
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                }
            }
            await DeleteTemporaryQualificationRootWithRetryAsync(exactRoot, testParent);
        }
    }
}

static async Task RunEmptyThreadResumeAsync()
{
    var sourceRoot = Directory.GetCurrentDirectory();
    var testParent = Path.Combine(sourceRoot, ".local", "Lab", "Runs");
    var testRoot = NewQaRunRoot(sourceRoot, "empty-thread-resume");
    var applicationRoot = Path.Combine(testRoot, "App");
    var layout = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
    var capability = JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The pinned LM Studio capability record is empty.");

    try
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        string threadId;
        bool listedBeforeResume;
        await using (var firstSupervisor = new RuntimeSupervisor(sourceRoot, applicationRoot))
        {
            var started = await firstSupervisor.StartThreadAsync(capability, timeout.Token);
            threadId = started["thread"]?["thread"]?["id"]?.GetValue<string>()
                ?? throw new InvalidDataException("The pinned App Server did not return an empty thread id.");
            var listing = await firstSupervisor.ListThreadsAsync(capability, null, timeout.Token);
            listedBeforeResume = (listing["threads"] as JsonArray ?? new JsonArray())
                .OfType<JsonObject>()
                .Any(thread => thread["id"]?.GetValue<string>() == threadId);
        }

        var exactReadRejected = false;
        await using (var restartedSupervisor = new RuntimeSupervisor(sourceRoot, applicationRoot))
        {
            try
            {
                await restartedSupervisor.ResumeThreadAsync(capability, threadId, timeout.Token);
            }
            catch (AppServerRpcException ex) when (ex.Message.Contains("thread not loaded", StringComparison.OrdinalIgnoreCase))
            {
                exactReadRejected = true;
            }
        }

        Assert(!listedBeforeResume, "the empty thread unexpectedly appeared in saved history");
        Assert(exactReadRejected, "the empty unlisted thread was unexpectedly readable after host restart");
        Console.WriteLine("EMPTY_THREAD_RESUME listedBeforeResume=false threadRead=not-loaded turnCount=0");
    }
    finally
    {
        var exactRoot = Path.GetFullPath(testRoot);
        if (Directory.Exists(exactRoot)
            && exactRoot.StartsWith(testParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var file in Directory.EnumerateFiles(exactRoot, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            }))
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                }
            }
            await DeleteTemporaryQualificationRootWithRetryAsync(exactRoot, testParent);
        }
    }
}

static async Task RunExitedSupervisorClientRecoveryAsync()
{
    var sourceRoot = Directory.GetCurrentDirectory();
    var identity = RuntimeIdentity.LoadVerified(Path.Combine(sourceRoot, "runtime", "runtime-lock.json"));
    var testParent = NewQaRunRoot(sourceRoot, "dead-client");
    var applicationRoot = Path.Combine(testParent, "App");
    var layout = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
    await using var mock = await UnrestrictedResponsesFixture.StartAsync(pauseFirstResponse: true);
    await using var changedRoute = await UnrestrictedResponsesFixture.StartAsync();
    var capability = (JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The pinned LM Studio capability record is empty.")) with { Endpoint = mock.BaseUrl };
    var marker = Path.Combine(layout.DataRoot, "dead-client-recovery-marker.txt");
    File.WriteAllText(marker, "keep isolated state");
    var phase = "setup";
    var completed = false;
    StartupFailureSnapshot? startupFailure = null;
    (int ProcessId, long StartTimeUtcTicks)? previousProcess = null;

    try
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        (int ProcessId, long StartTimeUtcTicks) finalIdentity;
        await using (var supervisor = new RuntimeSupervisor(sourceRoot, applicationRoot))
        {
            // The observer exists only in the ApprovalQA host build; keep the test source usable elsewhere.
            typeof(RuntimeSupervisor).GetProperty("ApprovalQaInitializeFailureObserver")?.SetValue(
                supervisor,
                (Action<AppServerClient>)(client => startupFailure = TrySnapshotStartupFailure(phase, client)));
            phase = "initial-supervisor-thread-start";
            var started = await supervisor.StartThreadAsync(capability, timeout.Token);
            var threadId = started["thread"]?["thread"]?["id"]?.GetValue<string>()
                ?? throw new InvalidDataException("the supervisor did not return a pinned thread id");
            var firstTurnTask = supervisor.StartTurnAsync("Run the isolated fixture diagnostic.", timeout.Token);
            await mock.WaitForFirstRequestAsync(TimeSpan.FromSeconds(15));
            var listingRejectedDuringTurn = false;
            try
            {
                await supervisor.ListThreadsAsync(capability, null, timeout.Token);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("operation", StringComparison.OrdinalIgnoreCase))
            {
                listingRejectedDuringTurn = true;
            }
            mock.ReleaseFirstResponse();
            var firstTurn = await firstTurnTask;
            Assert(listingRejectedDuringTurn,
                "a history refresh touched the App Server while a turn owned the active execution binding");
            Assert(firstTurn["completed"]?.GetValue<bool>() == true,
                "the first fixture turn did not complete before the controlled App Server exit");
            Assert(mock.RequestBodies.Count == 2,
                "the first fixture turn did not complete one tool round trip before the controlled exit");
            var originalIdentity = supervisor.PinnedAppServerProcessIdentity
                ?? throw new InvalidOperationException("the isolated supervisor did not expose its active App Server process identity");
            previousProcess = originalIdentity;
            using var child = Process.GetProcessById(originalIdentity.ProcessId);
            Assert(child.StartTime.ToUniversalTime().Ticks == originalIdentity.StartTimeUtcTicks,
                "the proposed termination target no longer has the supervisor-owned process identity");
            Assert(string.Equals(Path.GetFullPath(child.MainModule?.FileName ?? string.Empty), identity.BinaryPath,
                StringComparison.OrdinalIgnoreCase), "the proposed termination target is not the exact locked App Server binary");
            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(timeout.Token);

            phase = "first-replacement-list-threads";
            var recovered = await supervisor.ListThreadsAsync(capability, null, timeout.Token);
            Assert(recovered["selectedModelIdentifier"]?.GetValue<string>() == capability.ModelIdentifier,
                "the supervisor did not recover the selected model after App Server exit");
            var replacementIdentity = supervisor.PinnedAppServerProcessIdentity
                ?? throw new InvalidOperationException("the supervisor did not expose its recovered App Server process identity");
            Assert(replacementIdentity != originalIdentity,
                "the recovered supervisor reused the dead App Server process identity");
            Assert(File.ReadAllText(marker) == "keep isolated state", "relaunch erased isolated application data");
            Assert((recovered["threads"] as JsonArray)?.OfType<JsonObject>()
                .Any(thread => thread["id"]?.GetValue<string>() == threadId) == true,
                "the relaunched App Server did not retain the exact saved task");

            phase = "first-replacement-continuation";
            var continuation = await supervisor.StartTurnAsync("Continue this saved task after the exit.", timeout.Token);
            Assert(continuation["completed"]?.GetValue<bool>() == true,
                "a new user-authored turn did not complete in the exact recovered task");
            Assert(mock.RequestBodies.Count == 3,
                "recovery replayed a prior provider request or skipped the new user-authored request");
            Assert(mock.RequestBodies[2].Contains("Continue this saved task after the exit.", StringComparison.Ordinal),
                "the third request was not the new user-authored continuation");

            using var replacement = Process.GetProcessById(replacementIdentity.ProcessId);
            Assert(replacement.StartTime.ToUniversalTime().Ticks == replacementIdentity.StartTimeUtcTicks,
                "the second termination target no longer has the supervisor-owned process identity");
            Assert(string.Equals(Path.GetFullPath(replacement.MainModule?.FileName ?? string.Empty), identity.BinaryPath,
                StringComparison.OrdinalIgnoreCase), "the second termination target is not the locked App Server binary");
            replacement.Kill(entireProcessTree: true);
            await replacement.WaitForExitAsync(timeout.Token);

            previousProcess = replacementIdentity;
            phase = "second-replacement-direct-continuation";
            var directContinuation = await supervisor.StartTurnAsync("Continue directly after a second exit.", timeout.Token);
            Assert(directContinuation["completed"]?.GetValue<bool>() == true,
                "a new turn did not safely rebind the saved task when App Server exited without a history refresh");
            Assert(mock.RequestBodies.Count == 4
                && mock.RequestBodies[3].Contains("Continue directly after a second exit.", StringComparison.Ordinal),
                "direct recovery replayed a previous provider request or lost the new prompt");

            var thirdIdentity = supervisor.PinnedAppServerProcessIdentity
                ?? throw new InvalidOperationException("the supervisor did not expose its second recovered App Server process identity");
            Assert(thirdIdentity != replacementIdentity,
                "the second recovery reused the prior App Server process identity");
            using (var thirdChild = Process.GetProcessById(thirdIdentity.ProcessId))
            {
                Assert(thirdChild.StartTime.ToUniversalTime().Ticks == thirdIdentity.StartTimeUtcTicks,
                    "the third termination target no longer has the supervisor-owned process identity");
                Assert(string.Equals(Path.GetFullPath(thirdChild.MainModule?.FileName ?? string.Empty), identity.BinaryPath,
                    StringComparison.OrdinalIgnoreCase), "the failed-relaunch target is not the locked App Server binary");
                thirdChild.Kill(entireProcessTree: true);
                await thirdChild.WaitForExitAsync(timeout.Token);
            }

            previousProcess = thirdIdentity;
            phase = "cancelled-replacement-list-threads";
            using var cancelledRelaunch = new CancellationTokenSource();
            cancelledRelaunch.Cancel();
            var relaunchWasCancelled = false;
            try
            {
                await supervisor.ListThreadsAsync(capability, null, cancelledRelaunch.Token);
            }
            catch (OperationCanceledException)
            {
                relaunchWasCancelled = true;
            }
            Assert(relaunchWasCancelled, "the controlled failed-relaunch probe did not cancel initialization");

            phase = "post-cancellation-replacement-continuation";
            var afterFailedRelaunch = await supervisor.StartTurnAsync(
                "Continue the selected task after a failed replacement initialization.", timeout.Token);
            Assert(afterFailedRelaunch["completed"]?.GetValue<bool>() == true,
                "failed replacement initialization erased the selected saved task");
            Assert(mock.RequestBodies.Count == 5
                && mock.RequestBodies[4].Contains("Continue the selected task after a failed replacement initialization.", StringComparison.Ordinal),
                "failed replacement initialization replayed an old prompt or skipped the new prompt");
            finalIdentity = supervisor.PinnedAppServerProcessIdentity
                ?? throw new InvalidOperationException("the supervisor did not expose the final recovered App Server process identity");

            phase = "changed-route-rejection";
            var differentEndpoint = capability with { Endpoint = changedRoute.BaseUrl };
            var rejectedChangedRecord = false;
            try
            {
                await supervisor.ResumeThreadAsync(differentEndpoint, threadId, timeout.Token);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("capability record", StringComparison.OrdinalIgnoreCase))
            {
                rejectedChangedRecord = true;
            }
            Assert(rejectedChangedRecord,
                "a live task was rebound to a different capability record with the same provider and model identifiers");
            Assert(changedRoute.RequestBodies.Count == 0,
                "the changed endpoint received a provider request during rejected cross-record binding");
        }

        Assert(!IsRuntimeAppServerRunning(finalIdentity),
            $"the final recovered App Server process survived supervisor disposal; pinned PID={finalIdentity.ProcessId}, startTimeUtcTicks={finalIdentity.StartTimeUtcTicks}");
        completed = true;
    }
    catch (Exception ex)
    {
        var artifact = await WriteRecoveryFailureArtifactAsync(
            sourceRoot, testParent, phase, ex, startupFailure, previousProcess);
        Console.Error.WriteLine($"RECOVERY_QA_FAILURE artifact={artifact}");
        throw;
    }
    finally
    {
        var exactRoot = Path.GetFullPath(applicationRoot);
        if (completed && Directory.Exists(exactRoot)
            && exactRoot.StartsWith(testParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var file in Directory.EnumerateFiles(exactRoot, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            }))
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                }
            }
            await DeleteTemporaryQualificationRootWithRetryAsync(exactRoot, testParent);
        }
    }
}

static bool IsRuntimeAppServerRunning((int ProcessId, long StartTimeUtcTicks) identity)
{
    try
    {
        using var process = Process.GetProcessById(identity.ProcessId);
        return process.StartTime.ToUniversalTime().Ticks == identity.StartTimeUtcTicks;
    }
    catch (ArgumentException)
    {
        return false;
    }
    catch (InvalidOperationException)
    {
        return false;
    }
}

static async Task RunCrossProjectRecoveryAsync()
{
    var sourceRoot = Directory.GetCurrentDirectory();
    var testParent = Path.Combine(sourceRoot, ".local", "Lab", "Runs");
    var testRoot = NewQaRunRoot(sourceRoot, "cross-project");
    var applicationRoot = Path.Combine(testRoot, "App");
    var alphaWorkspace = Path.Combine(testRoot, "alpha");
    var betaWorkspace = Path.Combine(testRoot, "beta");
    _ = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
    Directory.CreateDirectory(alphaWorkspace);
    Directory.CreateDirectory(betaWorkspace);
    await using var mock = await UnrestrictedResponsesFixture.StartAsync();
    await using var changedRoute = await UnrestrictedResponsesFixture.StartAsync();
    var capability = (JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The pinned LM Studio capability record is empty.")) with { Endpoint = mock.BaseUrl };

    try
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        string alphaId;
        string betaId;
        await using (var supervisor = new RuntimeSupervisor(sourceRoot, applicationRoot))
        {
            await supervisor.SelectProjectAsync(alphaWorkspace, addNew: true, timeout.Token);
            var alphaStart = await supervisor.StartThreadAsync(capability, timeout.Token);
            alphaId = alphaStart["thread"]?["thread"]?["id"]?.GetValue<string>()
                ?? throw new InvalidDataException("Alpha thread identifier is missing.");
            Assert((await supervisor.StartTurnAsync("Alpha project initial prompt.", timeout.Token))["completed"]?.GetValue<bool>() == true,
                "Alpha project did not complete its initial mock-provider turn");

            var betaSelection = await supervisor.SelectProjectAsync(betaWorkspace, addNew: true, timeout.Token);
            Assert(betaSelection["previousThreadClosed"]?.GetValue<bool>() == true,
                "selecting Beta did not close Alpha's active client binding");
            var betaStart = await supervisor.StartThreadAsync(capability, timeout.Token);
            betaId = betaStart["thread"]?["thread"]?["id"]?.GetValue<string>()
                ?? throw new InvalidDataException("Beta thread identifier is missing.");
            Assert(betaId != alphaId, "the two projects reused one runtime thread identity");
            Assert((await supervisor.StartTurnAsync("Beta project initial prompt.", timeout.Token))["completed"]?.GetValue<bool>() == true,
                "Beta project did not complete its initial mock-provider turn");

            var betaRows = (await supervisor.ListThreadsAsync(capability, null, timeout.Token))["threads"] as JsonArray;
            Assert(betaRows?.OfType<JsonObject>().Any(row => row["id"]?.GetValue<string>() == betaId) == true
                && betaRows.OfType<JsonObject>().All(row => row["id"]?.GetValue<string>() != alphaId),
                "Beta history included Alpha or omitted its own saved task");
            var rejectedAlpha = false;
            try { await supervisor.ResumeThreadAsync(capability, alphaId, timeout.Token); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("workspace does not match", StringComparison.OrdinalIgnoreCase))
            {
                rejectedAlpha = true;
            }
            Assert(rejectedAlpha, "Beta workspace accepted Alpha's saved task");
            Assert((await supervisor.StartTurnAsync("Beta after rejected Alpha resume.", timeout.Token))["completed"]?.GetValue<bool>() == true,
                "rejected cross-project resume displaced the selected Beta task");

            await supervisor.SelectProjectAsync(alphaWorkspace, addNew: false, timeout.Token);
            var alphaRows = (await supervisor.ListThreadsAsync(capability, null, timeout.Token))["threads"] as JsonArray;
            Assert(alphaRows?.OfType<JsonObject>().Any(row => row["id"]?.GetValue<string>() == alphaId) == true
                && alphaRows.OfType<JsonObject>().All(row => row["id"]?.GetValue<string>() != betaId),
                "Alpha history included Beta or omitted its own saved task");
            var alphaResumed = await supervisor.ResumeThreadAsync(capability, alphaId, timeout.Token);
            Assert(alphaResumed["threadId"]?.GetValue<string>() == alphaId
                && string.Equals(alphaResumed["cwd"]?.GetValue<string>(), alphaWorkspace, StringComparison.OrdinalIgnoreCase),
                "Alpha resume did not return the exact saved task and workspace");
            Assert((await supervisor.StartTurnAsync("Alpha project continuation.", timeout.Token))["completed"]?.GetValue<bool>() == true,
                "Alpha project continuation failed after selecting back");
        }

        await using (var restarted = new RuntimeSupervisor(sourceRoot, applicationRoot))
        {
            Assert(string.Equals(restarted.SelectedWorkspace, alphaWorkspace, StringComparison.OrdinalIgnoreCase),
                "the selected Alpha project did not persist across host restart");
            await restarted.SelectProjectAsync(betaWorkspace, addNew: false, timeout.Token);
            var betaRows = (await restarted.ListThreadsAsync(capability, null, timeout.Token))["threads"] as JsonArray;
            Assert(betaRows?.OfType<JsonObject>().Any(row => row["id"]?.GetValue<string>() == betaId) == true
                && betaRows.OfType<JsonObject>().All(row => row["id"]?.GetValue<string>() != alphaId),
                "Beta history after restart leaked Alpha or lost its saved task");
            var betaResumed = await restarted.ResumeThreadAsync(capability, betaId, timeout.Token);
            Assert(betaResumed["threadId"]?.GetValue<string>() == betaId
                && string.Equals(betaResumed["cwd"]?.GetValue<string>(), betaWorkspace, StringComparison.OrdinalIgnoreCase),
                "Beta resume after restart did not restore its exact saved task and workspace");
            Assert((await restarted.StartTurnAsync("Beta after host restart.", timeout.Token))["completed"]?.GetValue<bool>() == true,
                "Beta continuation after restart failed");
        }
        Assert(mock.RequestBodies.Count == 6,
            "cross-project selection or restart replayed a prior provider request or skipped a new prompt");
        Assert(mock.RequestBodies[5].Contains("Beta after host restart.", StringComparison.Ordinal),
            "the final provider request was not the separately authored Beta continuation");

        var changedCapability = capability with { Endpoint = changedRoute.BaseUrl };
        await using (var changedRecordSupervisor = new RuntimeSupervisor(sourceRoot, applicationRoot))
        {
            var historyOnly = await changedRecordSupervisor.ResumeThreadAsync(changedCapability, betaId, timeout.Token);
            Assert(historyOnly["executionEligible"]?.GetValue<bool>() == false,
                "a saved task was marked executable after host restart under a changed endpoint with the same provider/model identity");
            Assert((historyOnly["turns"] as JsonArray)?.Count > 0,
                "blocking execution under a changed capability record hid the saved conversation history");

            var startWasBlocked = false;
            try
            {
                await changedRecordSupervisor.StartTurnAsync("This must stay read-only under a changed capability record.", timeout.Token);
            }
            catch (InvalidOperationException)
            {
                startWasBlocked = true;
            }
            Assert(startWasBlocked,
                "a history-only task accepted a new turn under the changed capability record");

            var originalHistory = (await changedRecordSupervisor.ListThreadsAsync(capability, null, timeout.Token))["threads"] as JsonArray;
            Assert(originalHistory?.OfType<JsonObject>().Any(row => row["id"]?.GetValue<string>() == betaId) == true,
                "rejecting the changed capability record removed the saved task from its original history");

            var bindingFileId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(betaId)))
                .ToLowerInvariant();
            var bindingPath = Path.Combine(applicationRoot, "Data", "NeoBabylon", "TaskBindings", $"{bindingFileId}.json");
            File.Delete(bindingPath);
            var missingBinding = await changedRecordSupervisor.ResumeThreadAsync(capability, betaId, timeout.Token);
            Assert(missingBinding["executionEligible"]?.GetValue<bool>() == false
                && missingBinding["executionBlockedReason"]?.GetValue<string>() == "capabilityBindingMissing"
                && (missingBinding["turns"] as JsonArray)?.Count > 0,
                "a missing task capability binding did not preserve history in an explicitly read-only state");
            Assert(!File.Exists(bindingPath), "resuming a task with a missing capability binding silently backfilled it");

            const string corruptBinding = "{not-json";
            File.WriteAllText(bindingPath, corruptBinding);
            var unavailableBinding = await changedRecordSupervisor.ResumeThreadAsync(capability, betaId, timeout.Token);
            Assert(unavailableBinding["executionEligible"]?.GetValue<bool>() == false
                && unavailableBinding["executionBlockedReason"]?.GetValue<string>() == "capabilityBindingUnavailable"
                && (unavailableBinding["turns"] as JsonArray)?.Count > 0,
                "a corrupt task capability binding did not preserve history in an explicitly read-only state");
            Assert(File.ReadAllText(bindingPath) == corruptBinding,
                "resuming a task with a corrupt capability binding rewrote the evidence");
        }
        Assert(changedRoute.RequestBodies.Count == 0,
            "the changed provider endpoint received an inference request while the saved task binding was rejected");
        Assert(mock.RequestBodies.Count == 6,
            "history-only resume for a missing or corrupt binding issued an inference request");
    }
    finally
    {
        var exactRoot = Path.GetFullPath(testRoot);
        if (Directory.Exists(exactRoot)
            && exactRoot.StartsWith(testParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var file in Directory.EnumerateFiles(exactRoot, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            }))
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
            Directory.Delete(exactRoot, recursive: true);
        }
    }
}

static async Task RunCrossProjectOperationOrderingAsync()
{
    var sourceRoot = Directory.GetCurrentDirectory();
    var testParent = Path.Combine(sourceRoot, ".local", "Lab", "Runs");
    var testRoot = NewQaRunRoot(sourceRoot, "cross-project-ordering");
    var applicationRoot = Path.Combine(testRoot, "App");
    var alphaWorkspace = Path.Combine(testRoot, "alpha");
    var betaWorkspace = Path.Combine(testRoot, "beta");
    _ = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
    Directory.CreateDirectory(alphaWorkspace);
    Directory.CreateDirectory(betaWorkspace);

    var prompts = new[]
    {
        "P2-05 Alpha active turn before competing history and switch.",
        "P2-05 Alpha authored after history scan.",
        "P2-05 Beta initial authored turn.",
        "P2-05 Beta deliberate continuation after host restart.",
        "P2-05 Alpha deliberate continuation after returning to its project."
    };
    var responses = prompts.Select((prompt, index) => ResponsesSse.AssistantMessage(
        $"p2-05-response-{index + 1}", $"p2-05-message-{index + 1}", $"Deterministic reply for {prompt}"))
        .ToArray();
    await using var mock = await UnrestrictedResponsesFixture.StartAsync(
        pauseFirstResponse: true, responseSequence: responses);
    var baseCapability = (JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The pinned test capability is empty.")) with { Endpoint = mock.BaseUrl };
    var alphaCapability = baseCapability with { ModelIdentifier = "p2-05-project-model" };
    // Keep the capability record identical so the cross-project-resume check
    // reaches the workspace-binding guard rather than the separate model-change guard.
    var betaCapability = alphaCapability;

    try
    {
        Directory.CreateDirectory(testParent);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        string alphaThreadId;
        string betaThreadId;

        await using (var supervisor = new RuntimeSupervisor(sourceRoot, applicationRoot))
        {
            await supervisor.SelectProjectAsync(alphaWorkspace, addNew: true, timeout.Token);
            var alphaStart = await supervisor.StartThreadAsync(alphaCapability, timeout.Token);
            alphaThreadId = alphaStart["thread"]?["thread"]?["id"]?.GetValue<string>()
                ?? throw new InvalidDataException("The Alpha project thread id is missing.");

            var alphaActiveTurn = supervisor.StartTurnAsync(prompts[0], timeout.Token);
            await mock.WaitForFirstRequestAsync(TimeSpan.FromSeconds(20));
            Assert(mock.RequestBodies.Count == 1 && mock.RequestBodies[0].Contains(prompts[0], StringComparison.Ordinal),
                "the controlled Alpha turn did not reach the loopback fixture exactly once");

            var activeHistoryRejected = false;
            try { await supervisor.ListThreadsAsync(alphaCapability, null, timeout.Token); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("operation", StringComparison.OrdinalIgnoreCase))
            {
                activeHistoryRejected = true;
            }
            var activeProjectSwitchRejected = false;
            try { await supervisor.SelectProjectAsync(betaWorkspace, addNew: true, timeout.Token); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("operation", StringComparison.OrdinalIgnoreCase))
            {
                activeProjectSwitchRejected = true;
            }
            Assert(activeHistoryRejected && activeProjectSwitchRejected,
                "an active Alpha turn allowed history access or a project switch to interleave");
            Assert(string.Equals(supervisor.SelectedWorkspace, alphaWorkspace, StringComparison.OrdinalIgnoreCase),
                "a rejected project switch changed the selected Alpha workspace");
            Assert(mock.RequestBodies.Count == 1,
                "the rejected active-turn operations issued an unsolicited provider request");

            mock.ReleaseFirstResponse();
            Assert((await alphaActiveTurn)["completed"]?.GetValue<bool>() == true,
                "the held Alpha fixture turn did not complete after its response was released");

            var alphaHistoryTask = supervisor.ListThreadsAsync(alphaCapability, null, timeout.Token);
            var startDuringHistoryRejected = false;
            try { await supervisor.StartTurnAsync("P2-05 rejected turn must not be sent during Alpha history scan.", timeout.Token); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("operation", StringComparison.OrdinalIgnoreCase))
            {
                startDuringHistoryRejected = true;
            }
            Assert(startDuringHistoryRejected,
                "a new turn was accepted while the Alpha history operation owned the supervisor");
            Assert(mock.RequestBodies.Count == 1,
                "a turn rejected during history listing reached the provider fixture");

            var alphaHistory = await alphaHistoryTask;
            var alphaRows = alphaHistory["threads"] as JsonArray ?? new JsonArray();
            Assert(alphaRows.OfType<JsonObject>().Any(row => row["id"]?.GetValue<string>() == alphaThreadId)
                && alphaRows.OfType<JsonObject>().All(row => row["cwd"]?.GetValue<string>() == alphaWorkspace),
                "the Alpha history scan did not remain attributed to its exact workspace");
            Assert((await supervisor.StartTurnAsync(prompts[1], timeout.Token))["completed"]?.GetValue<bool>() == true,
                "the deliberately authored Alpha turn did not start after the history scan completed");
            Assert(mock.RequestBodies.Count == 2 && mock.RequestBodies[1].Contains(prompts[1], StringComparison.Ordinal),
                "the post-scan Alpha turn did not create exactly its one planned provider request");

            await supervisor.SelectProjectAsync(betaWorkspace, addNew: true, timeout.Token);
            var betaStart = await supervisor.StartThreadAsync(betaCapability, timeout.Token);
            betaThreadId = betaStart["thread"]?["thread"]?["id"]?.GetValue<string>()
                ?? throw new InvalidDataException("The Beta project thread id is missing.");
            Assert(betaThreadId != alphaThreadId, "Alpha and Beta reused one App Server thread identity");
            Assert((await supervisor.StartTurnAsync(prompts[2], timeout.Token))["completed"]?.GetValue<bool>() == true,
                "the Beta project turn did not complete");

            var betaHistory = await supervisor.ListThreadsAsync(betaCapability, null, timeout.Token);
            var betaRows = betaHistory["threads"] as JsonArray ?? new JsonArray();
            Assert(betaRows.OfType<JsonObject>().Any(row => row["id"]?.GetValue<string>() == betaThreadId)
                && betaRows.OfType<JsonObject>().All(row => row["id"]?.GetValue<string>() != alphaThreadId
                    && string.Equals(row["cwd"]?.GetValue<string>(), betaWorkspace, StringComparison.OrdinalIgnoreCase)),
                "the Beta project history leaked Alpha or lost Beta's exact workspace");

            var crossProjectResumeRejected = false;
            try { await supervisor.ResumeThreadAsync(alphaCapability, alphaThreadId, timeout.Token); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("workspace does not match", StringComparison.OrdinalIgnoreCase))
            {
                crossProjectResumeRejected = true;
            }
            Assert(crossProjectResumeRejected,
                "the selected Beta project accepted Alpha's saved task for execution");
            Assert(string.Equals(supervisor.SelectedWorkspace, betaWorkspace, StringComparison.OrdinalIgnoreCase),
                "rejecting an Alpha resume changed the selected Beta project");
        }

        await using (var restarted = new RuntimeSupervisor(sourceRoot, applicationRoot))
        {
            Assert(string.Equals(restarted.SelectedWorkspace, betaWorkspace, StringComparison.OrdinalIgnoreCase),
                "the selected Beta workspace did not persist across host restart");
            var betaHistory = await restarted.ListThreadsAsync(betaCapability, null, timeout.Token);
            var betaRows = betaHistory["threads"] as JsonArray ?? new JsonArray();
            Assert(betaRows.OfType<JsonObject>().Any(row => row["id"]?.GetValue<string>() == betaThreadId)
                && betaRows.OfType<JsonObject>().All(row => row["id"]?.GetValue<string>() != alphaThreadId),
                "Beta history after restart leaked or lost its project-scoped saved task");
            var betaResume = await restarted.ResumeThreadAsync(betaCapability, betaThreadId, timeout.Token);
            Assert(betaResume["threadId"]?.GetValue<string>() == betaThreadId
                && string.Equals(betaResume["cwd"]?.GetValue<string>(), betaWorkspace, StringComparison.OrdinalIgnoreCase),
                "Beta resume after restart did not restore the exact thread and workspace");
            Assert((await restarted.StartTurnAsync(prompts[3], timeout.Token))["completed"]?.GetValue<bool>() == true,
                "the deliberate Beta continuation after restart failed");
        }

        await using (var alphaReturn = new RuntimeSupervisor(sourceRoot, applicationRoot))
        {
            await alphaReturn.SelectProjectAsync(alphaWorkspace, addNew: false, timeout.Token);
            var alphaHistory = await alphaReturn.ListThreadsAsync(alphaCapability, null, timeout.Token);
            var alphaRows = alphaHistory["threads"] as JsonArray ?? new JsonArray();
            Assert(alphaRows.OfType<JsonObject>().Any(row => row["id"]?.GetValue<string>() == alphaThreadId)
                && alphaRows.OfType<JsonObject>().All(row => row["id"]?.GetValue<string>() != betaThreadId),
                "returning to Alpha after restart did not preserve its separate history");
            var alphaResume = await alphaReturn.ResumeThreadAsync(alphaCapability, alphaThreadId, timeout.Token);
            Assert(alphaResume["threadId"]?.GetValue<string>() == alphaThreadId
                && string.Equals(alphaResume["cwd"]?.GetValue<string>(), alphaWorkspace, StringComparison.OrdinalIgnoreCase),
                "Alpha resume after returning did not restore the exact project and task");
            Assert((await alphaReturn.StartTurnAsync(prompts[4], timeout.Token))["completed"]?.GetValue<bool>() == true,
                "the deliberate Alpha continuation after returning to its project failed");
        }

        var requests = mock.RequestBodies;
        Assert(requests.Count == prompts.Length,
            $"operation interleaving replayed, skipped, or duplicated an authored request; expected {prompts.Length}, observed {requests.Count}");
        for (var index = 0; index < prompts.Length; index++)
        {
            var expectedModel = alphaCapability.ModelIdentifier;
            Assert(requests[index].Contains(prompts[index], StringComparison.Ordinal)
                && requests[index].Contains(expectedModel, StringComparison.Ordinal),
                $"provider request {index + 1} did not preserve its authored prompt and exact project capability");
        }
        Console.WriteLine("P2_05_HOST_INTERLEAVING passed list/start and start/list orderings; workspace/model bindings and request count remained exact.");
    }
    finally
    {
        var exactRoot = Path.GetFullPath(testRoot);
        if (Directory.Exists(exactRoot)
            && exactRoot.StartsWith(testParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            await DeleteTemporaryQualificationRootWithRetryAsync(exactRoot, testParent);
        }
    }
}

static async Task RunContextChangeInvalidationAsync()
{
    var sourceRoot = Directory.GetCurrentDirectory();
    var testParent = NewQaRunRoot(sourceRoot, "context-change");
    var applicationRoot = Path.Combine(testParent, "App");
    _ = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
    await using var mock = await UnrestrictedResponsesFixture.StartAsync();
    var capability = (JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The pinned LM Studio capability record is empty.")) with { Endpoint = mock.BaseUrl };
    var originalContext = capability.ContextWindowEffective.Int32Value
        ?? throw new InvalidDataException("The pinned test capability has no effective context value.");
    var changedContext = originalContext == 16_384 ? 16_383 : 16_384;
    var changedCapability = capability with
    {
        ContextWindowEffective = capability.ContextWindowEffective with
        {
            Value = JsonValue.Create(changedContext)
        }
    };
    var knownMaximumCapability = capability with
    {
        MaxCompletionTokensAdvertised = CapabilityObservation.Known(8_192, "focused saved-thread cap regression")
    };
    string threadId;
    var bindingPath = string.Empty;

    try
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using (var supervisor = new RuntimeSupervisor(sourceRoot, applicationRoot))
        {
            var started = await supervisor.StartThreadAsync(capability, timeout.Token);
            threadId = started["thread"]?["thread"]?["id"]?.GetValue<string>()
                ?? throw new InvalidDataException("The context-invalidation probe did not receive a thread id.");
            var turn = await supervisor.StartTurnAsync("Create one saved turn for the isolated context-invalidation probe.", timeout.Token);
            Assert(turn["completed"]?.GetValue<bool>() == true,
                "the initial loopback turn did not complete before the host restart");
        }

        Assert(mock.RequestBodies.Count == 2
            && mock.RequestBodies.Any(body => body.Contains("function_call_output", StringComparison.Ordinal)),
            $"the initial saved turn did not complete one loopback tool round trip; requests={mock.RequestBodies.Count}");
        var actualBindingId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(threadId)))
            .ToLowerInvariant();
        bindingPath = Path.Combine(applicationRoot, "Data", "NeoBabylon", "TaskBindings", $"{actualBindingId}.json");
        Assert(File.Exists(bindingPath), "the completed thread did not persist its capability binding before restart");
        var originalBinding = File.ReadAllBytes(bindingPath);

        await using (var restarted = new RuntimeSupervisor(sourceRoot, applicationRoot))
        {
            var historyOnly = await restarted.ResumeThreadAsync(changedCapability, threadId, timeout.Token);
            Assert(historyOnly["executionEligible"]?.GetValue<bool>() == false
                && historyOnly["executionBlockedReason"]?.GetValue<string>() == "capabilityRecordChanged",
                "a changed effective context alone did not block executable resume after host restart");
            Assert((historyOnly["turns"] as JsonArray)?.Count > 0,
                "the context mismatch hid the saved transcript instead of preserving it as history-only");

            var knownMaximumHistoryOnly = await restarted.ResumeThreadAsync(knownMaximumCapability, threadId, timeout.Token);
            Assert(knownMaximumHistoryOnly["executionEligible"]?.GetValue<bool>() == false
                && knownMaximumHistoryOnly["executionBlockedReason"]?.GetValue<string>() == "capabilityRecordChanged",
                "adding a Known completion maximum silently resumed a thread bound without one");
            Assert((knownMaximumHistoryOnly["turns"] as JsonArray)?.Count > 0,
                "the Known completion-maximum mismatch hid saved history instead of making it read-only");

            var continuationBlocked = false;
            try
            {
                await restarted.StartTurnAsync("This continuation must stay blocked after a Known-cap mismatch.", timeout.Token,
                    maxOutputTokens: 8_193);
            }
            catch (InvalidOperationException)
            {
                continuationBlocked = true;
            }
            Assert(continuationBlocked,
                "a history-only task accepted an above-maximum override after the capability change");

            var stillUnknown = capability with
            {
                MaxCompletionTokensAdvertised = CapabilityObservation.Unknown("current record remains Unknown")
            };
            var unknownResume = await restarted.ResumeThreadAsync(stillUnknown, threadId, timeout.Token);
            Assert(unknownResume["executionEligible"]?.GetValue<bool>() == true
                && unknownResume["threadId"]?.GetValue<string>() == threadId,
                "an exact legacy binding did not remain executable when the current maximum was still Unknown");
        }

        Assert(File.ReadAllBytes(bindingPath).SequenceEqual(originalBinding),
            "a saved-thread capability check silently rewrote the original binding");
        Assert(mock.RequestBodies.Count == 2,
            "saved-thread capability checks issued another provider request");
        Assert(changedCapability.Endpoint == capability.Endpoint
            && changedCapability.ModelIdentifier == capability.ModelIdentifier
            && changedCapability.ProviderId == capability.ProviderId
            && changedCapability.ContextWindowEffective.Int32Value == changedContext
            && changedContext != originalContext,
            "the context-invalidation fixture changed fields other than effective context");
        Console.WriteLine($"CONTEXT_RESTART_INVALIDATION originalContext={originalContext} changedContext={changedContext} contextExecutionEligible=false knownMaximumExecutionEligible=false unknownMaximumExecutionEligible=true historyRetained=true bindingUnchanged=true toolRoundTripRequests={mock.RequestBodies.Count}");
    }
    finally
    {
        var exactRoot = Path.GetFullPath(applicationRoot);
        var parentPrefix = Path.TrimEndingDirectorySeparator(testParent) + Path.DirectorySeparatorChar;
        if (Directory.Exists(exactRoot)
            && exactRoot.StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await DeleteTemporaryQualificationRootWithRetryAsync(exactRoot, testParent);
        }
    }
}


static async Task RunUnexpectedServerExitAsync()
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Phase1B-Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var executable = Environment.ProcessPath
        ?? throw new InvalidOperationException("The test executable path is unavailable.");
    Assert(File.Exists(executable), "the test executable could not be relaunched as a fake App Server");

    try
    {
        await using var client = AppServerClient.Start(new AppServerLaunchOptions(
            executable,
            root,
            Path.Combine(root, "CodexHome"),
            null));
        await client.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await client.RequestResultAsync(new AppServerRequest(2, "turn/start", new JsonObject()))
            .WaitAsync(TimeSpan.FromSeconds(5));

        var observation = await client.WaitForTurnCompletionAsync(TimeSpan.FromSeconds(5));
        Assert(!observation.Completed, "an exited App Server was reported as a completed turn");
        Assert(observation.Notifications.Any(notification => notification.Method == "turn/started"), "partial turn events were discarded");
        Assert(observation.Notifications.Any(notification => notification.Method == "item/agentMessage/delta"
            && notification.Params["delta"]?.GetValue<string>() == "Partial output before App Server exit."),
            "partial assistant output was not retained when the App Server exited");
        Assert(observation.Failure?.Contains("before a terminal turn event", StringComparison.Ordinal) == true, "server exit was not distinguished from a successful or timed-out turn");
        Assert(observation.ToHostFailure()?["attributedTo"]?.GetValue<string>() == "Codex App Server",
            "child-exit failure did not preserve App Server attribution");
        Assert(client.HasExited && client.ExitCode == 23, "the fake App Server did not exit with its expected status");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static async Task RunMalformedServerMessageAsync()
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Phase1B-Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var executable = Environment.ProcessPath
        ?? throw new InvalidOperationException("The test executable path is unavailable.");
    AppServerClient? client = null;

    try
    {
        client = AppServerClient.Start(new AppServerLaunchOptions(
            executable,
            Path.GetTempPath(),
            Path.Combine(root, "CodexHome"),
            null));
        await client.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await client.RequestResultAsync(AppServerProtocol.BuildTurnStartRequest(
            2,
            new TurnStartOptions("fake-malformed-thread", "test malformed stream")))
            .WaitAsync(TimeSpan.FromSeconds(5));

        var observation = await client.WaitForTurnCompletionAsync(
            TimeSpan.FromSeconds(5),
            expectedThreadId: "fake-malformed-thread",
            expectedTurnId: "fake-malformed-turn");
        Assert(!observation.Completed, "malformed protocol output was reported as a completed turn");
        Assert(observation.Notifications.Any(notification => notification.Method == "turn/started"), "partial turn events were discarded");
        Assert(observation.Failure?.Contains("JsonReaderException", StringComparison.Ordinal) == true, $"malformed JSON was not identified in the stream-closure diagnostic: {observation.Failure ?? "<none>"}");
        Assert(SpinWait.SpinUntil(() => client.HasExited, TimeSpan.FromSeconds(5)),
            "fake App Server did not exit after malformed protocol output");
    }
    finally
    {
        if (client is not null)
        {
            await client.DisposeAsync();
        }

        Directory.Delete(root, recursive: true);
    }
}

static async Task RunUnknownNotificationAsync()
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Phase1B-Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var executable = Environment.ProcessPath
        ?? throw new InvalidOperationException("The test executable path is unavailable.");
    AppServerClient? client = null;

    try
    {
        client = AppServerClient.Start(new AppServerLaunchOptions(
            executable,
            Path.GetTempPath(),
            Path.Combine(root, "CodexHome"),
            null));
        await client.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await client.RequestResultAsync(AppServerProtocol.BuildTurnStartRequest(
            2,
            new TurnStartOptions("fake-unknown-thread", "test unknown notification")))
            .WaitAsync(TimeSpan.FromSeconds(5));

        var observation = await client.WaitForTurnCompletionAsync(
            TimeSpan.FromSeconds(5),
            expectedThreadId: "fake-unknown-thread",
            expectedTurnId: "fake-unknown-turn");
        Assert(observation.Completed && observation.Status == "completed", "an unknown informational event disrupted the terminal turn result");
        Assert(observation.Notifications.Any(notification => notification.Method == "future/progress"
            && notification.Params["traceId"]?.GetValue<string>() == "trace-1"), "the unknown informational notification was not preserved");
        Assert(SpinWait.SpinUntil(() => client.HasExited, TimeSpan.FromSeconds(5)), "fake App Server did not exit after the completed turn");
    }
    finally
    {
        if (client is not null)
        {
            await client.DisposeAsync();
        }

        Directory.Delete(root, recursive: true);
    }
}

static async Task RunCommandListCompletionRaceAsync()
{
    const string threadId = "fake-command-list-race-thread";
    const string turnId = "fake-command-list-race-turn";
    const string itemId = "fake-command-list-race-item";
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Phase1B-Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var executable = Environment.ProcessPath
        ?? throw new InvalidOperationException("The test executable path is unavailable.");
    AppServerClient? client = null;

    try
    {
        client = AppServerClient.Start(new AppServerLaunchOptions(
            executable,
            root,
            Path.Combine(root, "CodexHome"),
            null));
        await client.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var startResponse = await client.RequestResultAsync(AppServerProtocol.BuildTurnStartRequest(
            2,
            new TurnStartOptions(threadId, "create then complete one command")))
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert(startResponse["turn"]?["id"]?.GetValue<string>() == turnId,
            "the fake command-list race turn identity was not returned");
        var observation = await client.WaitForTurnCompletionAsync(
            TimeSpan.FromSeconds(5),
            expectedThreadId: threadId,
            expectedTurnId: turnId);
        Assert(observation.Interrupted, "the fake command-list race turn did not end interrupted");

        var completionTracker = new InterruptedCommandCompletionTracker(threadId, turnId, [itemId]);
        client.NotificationObserved += completionTracker.Observe;
        var response = await client.RequestResultAsync(
            AppServerProtocol.BuildCommandExecutionListRequest(3, threadId))
            .WaitAsync(TimeSpan.FromSeconds(5));
        var listed = (response["data"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
        Assert(listed.Any(item => item["itemId"]?.GetValue<string>() == itemId),
            "the controlled delayed-list fixture did not return its stale active snapshot");
        Assert(completionTracker.HasCompleted(itemId),
            "the command-completion notification was not observed while the list response was pending");
        Assert(!completionTracker.MayPublishRunningBinding(itemId),
            "a command completed during list discovery was still eligible for a running Stop binding");
        client.NotificationObserved -= completionTracker.Observe;
    }
    finally
    {
        if (client is not null)
        {
            await client.DisposeAsync();
        }

        Directory.Delete(root, recursive: true);
    }
}

static async Task RunBrokenPipeAsync()
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Phase1B-Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var executable = Environment.ProcessPath
        ?? throw new InvalidOperationException("The test executable path is unavailable.");
    AppServerClient? client = null;

    try
    {
        client = AppServerClient.Start(new AppServerLaunchOptions(
            executable,
            Path.GetTempPath(),
            Path.Combine(root, "CodexHome"),
            null));
        await client.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Exception? failure = null;
        try
        {
            await client.RequestResultAsync(new AppServerRequest(2, "test/brokenPipe", new JsonObject()))
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (EndOfStreamException ex)
        {
            failure = ex;
        }

        Assert(failure is not null, "the pending request succeeded or timed out instead of receiving an EOF failure");
        Assert(SpinWait.SpinUntil(() => client.HasExited, TimeSpan.FromSeconds(5)), "fake App Server did not exit after closing its response stream");
    }
    finally
    {
        if (client is not null)
        {
            await client.DisposeAsync();
        }

        Directory.Delete(root, recursive: true);
    }
}

static async Task RunLargeOutputProjectionAsync()
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Large-Output-Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var executable = Environment.ProcessPath
        ?? throw new InvalidOperationException("The test executable path is unavailable.");

    try
    {
        await using var client = AppServerClient.Start(new AppServerLaunchOptions(
            executable,
            root,
            Path.Combine(root, "CodexHome"),
            null));
        await client.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));
        const string threadId = "fake-large-output-thread";
        var started = await client.RequestResultAsync(AppServerProtocol.BuildTurnStartRequest(
            2,
            new TurnStartOptions(threadId, "exercise bounded output"))).WaitAsync(TimeSpan.FromSeconds(5));
        var turnId = started["turn"]?["id"]?.GetValue<string>();
        var streamed = new List<AppServerNotification>();
        var observation = await client.WaitForTurnCompletionAsync(
            TimeSpan.FromSeconds(15),
            expectedThreadId: threadId,
            expectedTurnId: turnId,
            notificationObserver: streamed.Add);

        Assert(observation.Completed, $"large-output fixture did not complete: {observation.Failure ?? observation.Status ?? "unknown"}");
        Assert(observation.FinalAssistantText?.Length == AppServerNotificationProjection.AssistantDisplayLimit,
            "final assistant text exceeded its display budget or lost the bounded preview");
        Assert(observation.FinalAssistantDisplay?["omittedCharacters"]?.GetValue<int>() == 4_880_000,
            "final assistant item did not report the exact number of display-omitted characters");
        var visibleDeltas = streamed.Where(notification => notification.Method == "item/agentMessage/delta").ToArray();
        Assert(visibleDeltas.Sum(notification => notification.Params["delta"]?.GetValue<string>()?.Length ?? 0)
                == AppServerNotificationProjection.AssistantDisplayLimit,
            "assistant streaming did not stop exactly at the renderer character budget");
        Assert(observation.Notifications.Count(notification => notification.Method == "item/agentMessage/delta") == 1,
            "turn observation retained each delta instead of a single bounded aggregate");
        var tools = TurnDiagnostics.Extract(observation.Notifications);
        var tool = tools.OfType<JsonObject>().SingleOrDefault(item => item["itemType"]?.GetValue<string>() == "commandExecution");
        Assert(tool?["output"]?.GetValue<string>()?.Length == AppServerNotificationProjection.ToolOutputDisplayLimit,
            "completed tool output exceeded its display budget");
        Assert(tool?["neoBabylonDisplay"]?["omittedCharacters"]?.GetValue<int>() == 1_960_000,
            "completed tool output did not report its exact display omission count");
        Assert(observation.Notifications.Count < 10 && !observation.NotificationsTruncated,
            "a large stream retained an unbounded event history or falsely reported retention loss");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static async Task RunInterruptedTurnAsync()
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Phase1B-Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var executable = Environment.ProcessPath
        ?? throw new InvalidOperationException("The test executable path is unavailable.");

    try
    {
        await using var client = AppServerClient.Start(new AppServerLaunchOptions(
            executable,
            root,
            Path.Combine(root, "CodexHome"),
            null));
        await client.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var started = await client.RequestResultAsync(AppServerProtocol.BuildTurnStartRequest(
            2,
            new TurnStartOptions("fake-thread", "test interruption"))).WaitAsync(TimeSpan.FromSeconds(5));
        var turnId = started["turn"]?["id"]?.GetValue<string>();
        Assert(turnId == "fake-turn", "turn/start response did not expose its runtime-owned turn id");

        var streamedNotifications = new List<string>();
        var observationTask = client.WaitForTurnCompletionAsync(
            TimeSpan.FromSeconds(5),
            expectedThreadId: "fake-thread",
            expectedTurnId: turnId,
            notificationObserver: notification => streamedNotifications.Add(notification.Method));
        var interrupt = await client.RequestResultAsync(new AppServerRequest(
            3,
            "turn/interrupt",
            new JsonObject
            {
                ["threadId"] = "fake-thread",
                ["turnId"] = turnId
            })).WaitAsync(TimeSpan.FromSeconds(5));
        Assert(interrupt.Count == 0, "turn/interrupt response was not accepted");

        var observation = await observationTask;
        Assert(observation.Terminal, "interrupted turn was not recognized as terminal");
        Assert(observation.Interrupted, "interrupted turn status was not distinguished");
        Assert(!observation.Completed, "an interrupted turn was reported as completed successfully");
        Assert(observation.Failure is null, "a user-requested interruption was mislabeled as a failure");
        Assert(observation.Notifications.Any(notification =>
            notification.Method == "turn/completed"
            && notification.Params["turn"]?["status"]?.GetValue<string>() == "interrupted"),
            "interrupted terminal evidence was not retained");
        Assert(observation.Notifications.Any(notification => notification.Method == "item/agentMessage/delta"
            && notification.Params["delta"]?.GetValue<string>() == "Partial answer before cancellation."),
            "the interrupted observation discarded the assistant delta received before cancellation");
        Assert(streamedNotifications.SequenceEqual(["turn/started", "item/agentMessage/delta", "turn/completed"]),
            "App Server notifications were not streamed in order before terminal completion");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static async Task RunProviderErrorNotificationAsync()
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Phase1B-Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var executable = Environment.ProcessPath
        ?? throw new InvalidOperationException("The test executable path is unavailable.");

    try
    {
        await using var client = AppServerClient.Start(new AppServerLaunchOptions(
            executable,
            root,
            Path.Combine(root, "CodexHome"),
            null));
        await client.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var started = await client.RequestResultAsync(new AppServerRequest(
            2,
            "turn/start",
            new JsonObject { ["threadId"] = "fake-error-thread" })).WaitAsync(TimeSpan.FromSeconds(5));
        var turnId = started["turn"]?["id"]?.GetValue<string>();
        Assert(turnId == "fake-error-turn", "provider-error fixture turn did not start");

        var observation = await client.WaitForTurnCompletionAsync(
            TimeSpan.FromSeconds(5),
            expectedThreadId: "fake-error-thread",
            expectedTurnId: turnId).WaitAsync(TimeSpan.FromSeconds(5));
        Assert(observation.Failure == "429 Too Many Requests", "provider error notification was flattened to raw JSON instead of its readable message");
        Assert(observation.FailureDetails?["codexErrorInfo"]?["responseTooManyFailedAttempts"]?["httpStatusCode"]?.GetValue<int>() == 429,
            "native App Server provider error details were not retained");

        var hostFailure = observation.ToHostFailure();
        Assert(hostFailure?["type"]?.GetValue<string>() == "appServerTurn", "host failure did not retain its typed category");
        Assert(hostFailure?["attributedTo"]?.GetValue<string>() == "Codex App Server", "host failure lost its source attribution");
        Assert(hostFailure?["message"]?.GetValue<string>() == "429 Too Many Requests", "host failure message was not human-readable");
        Assert(hostFailure?["details"]?["codexErrorInfo"]?["responseTooManyFailedAttempts"]?["httpStatusCode"]?.GetValue<int>() == 429,
            "typed host failure did not carry the provider error details");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static async Task RunInteractiveApprovalFlowAsync()
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Phase1B-Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var executable = Environment.ProcessPath
        ?? throw new InvalidOperationException("The test executable path is unavailable.");
    var notifications = Channel.CreateUnbounded<AppServerNotification>();
    AppServerClient? client = null;

    try
    {
        var activeClient = client = AppServerClient.Start(new AppServerLaunchOptions(
            executable,
            Directory.GetCurrentDirectory(),
            Path.Combine(root, "CodexHome"),
            null));
        await activeClient.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var started = await activeClient.RequestResultAsync(new AppServerRequest(
            2,
            "turn/start",
            new JsonObject { ["threadId"] = "fake-approval-thread" })).WaitAsync(TimeSpan.FromSeconds(5));
        var turnId = started["turn"]?["id"]?.GetValue<string>();
        Assert(turnId == "fake-approval-turn", "approval test turn did not start");

        var observationTask = activeClient.WaitForTurnCompletionAsync(
            TimeSpan.FromSeconds(5),
            expectedThreadId: "fake-approval-thread",
            expectedTurnId: turnId,
            notificationObserver: notification => notifications.Writer.TryWrite(notification));

        foreach (var (requestId, method, decision) in new[]
        {
            (41L, "item/commandExecution/requestApproval", "accept"),
            (42L, "item/fileChange/requestApproval", "accept"),
            (45L, "item/fileChange/requestApproval", "decline"),
            (43L, "item/permissions/requestApproval", "grantRequestedForTurn")
        })
        {
            AppServerNotification approval;
            while (true)
            {
                approval = await notifications.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                if (approval.Method is "turn/started" or "serverRequest/resolved")
                {
                    continue;
                }

                if (approval.Method == "item/started"
                    && approval.Params["item"]?["id"]?.GetValue<string>() is "file-1" or "file-2")
                {
                    Assert(approval.Params["threadId"]?.GetValue<string>() == "fake-approval-thread"
                        && approval.Params["turnId"]?.GetValue<string>() == "fake-approval-turn",
                        "pre-approval file-change item lost its exact thread/turn identity");
                    continue;
                }
                if (approval.Method == "item/fileChange/patchUpdated")
                {
                    continue;
                }
                break;
            }
            Assert(approval.Method == "neobabylon/approvalRequested", $"{method} did not create a typed pending-approval event");
            Assert(approval.Params["requestId"]?.GetValue<long>() == requestId, "pending approval request id changed");
            Assert(approval.Params["method"]?.GetValue<string>() == method, "pending approval method changed");
            var approvalInstanceId = approval.Params["approvalInstanceId"]?.GetValue<string>();
            Assert(!string.IsNullOrWhiteSpace(approvalInstanceId), "approval event did not carry its opaque host-issued instance id");
            string? previewFingerprint = null;
            if (requestId is 42 or 45)
            {
                var preview = approval.Params["reviewPreview"] as JsonObject
                    ?? throw new InvalidOperationException("exact pre-approval file-change item was not projected into the approval event");
                var expectedItemId = requestId == 42 ? "file-1" : "file-2";
                var expectedPath = requestId == 42
                    ? "D:\\workspace\\approved.txt"
                    : "D:\\workspace\\changed-after-preview.txt";
                var expectedDiff = requestId == 42 ? "+approved\n" : "+original\n";
                Assert(preview["threadId"]?.GetValue<string>() == "fake-approval-thread"
                    && preview["turnId"]?.GetValue<string>() == "fake-approval-turn"
                    && preview["itemId"]?.GetValue<string>() == expectedItemId,
                    "file-change preview was not bound to the exact App Server request identity");
                Assert(preview["changes"]?[0]?["path"]?.GetValue<string>() == expectedPath
                    && preview["changes"]?[0]?["diff"]?.GetValue<string>() == expectedDiff,
                    "file-change approval preview differed from the App Server item started before the request");
                previewFingerprint = preview["fingerprint"]?.GetValue<string>();
                Assert(!string.IsNullOrWhiteSpace(previewFingerprint), "file-change preview fingerprint was missing");

                if (requestId == 45)
                {
                    var invalidation = await notifications.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                    Assert(invalidation.Method == "neobabylon/approvalReviewInvalidated",
                        "a changed App Server patch snapshot did not invalidate the exact pending file-change review");
                    Assert(invalidation.Params["requestId"]?.GetValue<long>() == requestId
                        && invalidation.Params["approvalInstanceId"]?.GetValue<string>() == approval.Params["approvalInstanceId"]?.GetValue<string>()
                        && invalidation.Params["threadId"]?.GetValue<string>() == "fake-approval-thread"
                        && invalidation.Params["turnId"]?.GetValue<string>() == "fake-approval-turn"
                        && invalidation.Params["itemId"]?.GetValue<string>() == expectedItemId,
                        "file-change review invalidation was not bound to the exact pending approval instance");

                    await AssertThrowsAsync<InvalidOperationException>(() => activeClient.ResolveApprovalAsync(
                        requestId, approvalInstanceId!, "fake-approval-thread", "fake-approval-turn", "accept", previewFingerprint));
                }

            }
            Assert(!activeClient.HasExited, "App Server exited instead of waiting for a human decision");
            await AssertThrowsAsync<InvalidOperationException>(() => activeClient.ResolveApprovalAsync(
                requestId, "stale-approval-instance", "fake-approval-thread", "fake-approval-turn", decision, previewFingerprint));
            await AssertThrowsAsync<InvalidOperationException>(() => activeClient.ResolveApprovalAsync(
                requestId, approvalInstanceId!, "fake-approval-thread", "stale-turn", decision, previewFingerprint));
            if (requestId == 42)
            {
                await AssertThrowsAsync<InvalidOperationException>(() => activeClient.ResolveApprovalAsync(
                    requestId, approvalInstanceId!, "fake-approval-thread", "fake-approval-turn", decision, new string('0', 64)));
            }
            await activeClient.ResolveApprovalAsync(
                    requestId, approvalInstanceId!, "fake-approval-thread", "fake-approval-turn", decision, previewFingerprint)
                .WaitAsync(TimeSpan.FromSeconds(5));
            if (requestId == 42)
            {
                await AssertThrowsAsync<InvalidOperationException>(() => activeClient.ResolveApprovalAsync(
                    requestId, approvalInstanceId!, "fake-approval-thread", "fake-approval-turn", decision, previewFingerprint));
            }
        }

        AppServerNotification denied;
        do
        {
            denied = await notifications.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        } while (denied.Method == "serverRequest/resolved");
        Assert(denied.Method == "neobabylon/serverRequestDenied", "unknown authority request was not surfaced as a typed denial");
        Assert(denied.Params["response"]?["error"]?["code"]?.GetValue<int>() == -32001, "unknown request did not fail closed with an error");

        var observation = await observationTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert(observation.Completed, "turn did not complete after approval responses");
        Assert(observation.Notifications.Any(notification => notification.Method == "neobabylon/approvalReviewInvalidated"
            && notification.Params["requestId"]?.GetValue<long>() == 45),
            "the exact changed file-change preview did not produce a typed invalidation");
        Assert(SpinWait.SpinUntil(() => activeClient.HasExited, TimeSpan.FromSeconds(5)), "fake App Server did not exit after the approval test turn");
        Assert(activeClient.HasExited && activeClient.ExitCode == 0, $"fake App Server protocol assertion returned exit code {activeClient.ExitCode?.ToString() ?? "still running"}");
    }
    finally
    {
        if (client is not null) await client.DisposeAsync();
        Directory.Delete(root, recursive: true);
    }
}

static async Task RunApprovalTimeoutAsync()
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Approval-Timeout-Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var executable = Environment.ProcessPath
        ?? throw new InvalidOperationException("The test executable path is unavailable.");
    AppServerClient? client = null;

    try
    {
        var activeClient = client = AppServerClient.Start(new AppServerLaunchOptions(
            executable,
            Directory.GetCurrentDirectory(),
            Path.Combine(root, "CodexHome"),
            null), TimeSpan.FromMilliseconds(300));
        await activeClient.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var started = await activeClient.RequestResultAsync(new AppServerRequest(
            2,
            "turn/start",
            new JsonObject { ["threadId"] = "fake-approval-timeout-thread" })).WaitAsync(TimeSpan.FromSeconds(5));
        var turnId = started["turn"]?["id"]?.GetValue<string>();
        Assert(turnId == "fake-approval-timeout-turn", "timeout test turn did not start");

        var observation = await activeClient.WaitForTurnCompletionAsync(
            TimeSpan.FromSeconds(5),
            expectedThreadId: "fake-approval-timeout-thread",
            expectedTurnId: turnId).WaitAsync(TimeSpan.FromSeconds(6));
        Assert(observation.Completed, "the timeout-denied command did not reach a terminal completed state");
        var timeout = observation.Notifications.Single(notification => notification.Method == "neobabylon/approvalTimedOut");
        Assert(timeout.Params["requestId"]?.GetValue<long>() == 51, "the timeout event lost its request identity");
        Assert(timeout.Params["response"]?["result"]?["decision"]?.GetValue<string>() == "decline",
            "the timeout did not send the schema-valid command denial");
        Assert(observation.Notifications.Any(notification => notification.Method == "serverRequest/resolved"
            && notification.Params["requestId"]?.GetValue<long>() == 51),
            "the approval lifecycle did not observe serverRequest/resolved");
        var expiredApprovalInstanceId = observation.Notifications
            .Single(notification => notification.Method == "neobabylon/approvalRequested"
                && notification.Params["requestId"]?.GetValue<long>() == 51)
            .Params["approvalInstanceId"]?.GetValue<string>()
            ?? throw new InvalidOperationException("timeout approval instance id was missing");
        await AssertThrowsAsync<InvalidOperationException>(() => activeClient.ResolveApprovalAsync(
            51, expiredApprovalInstanceId, "fake-approval-timeout-thread", "fake-approval-timeout-turn", "accept"));
        Assert(SpinWait.SpinUntil(() => activeClient.HasExited, TimeSpan.FromSeconds(5)), "timeout test App Server did not exit after the response");
        Assert(activeClient.ExitCode == 0, $"timeout test App Server rejected the single fail-closed response with exit code {activeClient.ExitCode}");
    }
    finally
    {
        if (client is not null) await client.DisposeAsync();
        Directory.Delete(root, recursive: true);
    }
}

static async Task RunProviderCredentialHandoffAsync()
{
    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Phase1B-Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var executable = Environment.ProcessPath
        ?? throw new InvalidOperationException("The test executable path is unavailable.");
    var previousOpenRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY", EnvironmentVariableTarget.Process);
    var previousOpenAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.Process);

    try
    {
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "ambient-must-not-inherit", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "shared-key-must-not-inherit", EnvironmentVariableTarget.Process);
        await using var client = AppServerClient.Start(new AppServerLaunchOptions(
            executable,
            root,
            Path.Combine(root, "CodexHome"),
            null,
            "explicit-child-only"));
        var result = await client.RequestResultAsync(new AppServerRequest(1, "test/credentialState", new JsonObject()))
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert(result["openRouterExplicit"]?.GetValue<bool>() == true, "explicit provider credential was not passed to App Server");
        Assert(result["openRouterAmbient"]?.GetValue<bool>() == false, "ambient provider credential was not replaced");
        Assert(result["openAiAmbient"]?.GetValue<bool>() == false, "ordinary Codex credential leaked into App Server");
        Assert(result["containedToolsRequired"]?.GetValue<bool>() == true, "App Server process did not receive NeoBabylon containment marker");
    }
    finally
    {
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", previousOpenRouterKey, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", previousOpenAiKey, EnvironmentVariableTarget.Process);
        Directory.Delete(root, recursive: true);
    }
}

static async Task<int> RunFakeAppServerAsync()
{
    while (await Console.In.ReadLineAsync() is { } line)
    {
        var request = JsonNode.Parse(line)?.AsObject();
        var method = request?["method"]?.GetValue<string>();
        var id = request?["id"]?.DeepClone();
        if (method == "initialize")
        {
            Console.Out.WriteLine(new JsonObject
            {
                ["id"] = id,
                ["result"] = new JsonObject()
            }.ToJsonString());
            await Console.Out.FlushAsync();
        }
        else if (method == "test/credentialState")
        {
            Console.Out.WriteLine(new JsonObject
            {
                ["id"] = id,
                ["result"] = new JsonObject
                {
                    ["openRouterExplicit"] = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") == "explicit-child-only",
                    ["openRouterAmbient"] = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") == "ambient-must-not-inherit",
                    ["openAiAmbient"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY") == "shared-key-must-not-inherit",
                    ["containedToolsRequired"] = Environment.GetEnvironmentVariable("NEOBABYLON_REQUIRE_CONTAINED_TOOLS") == "1"
                }
            }.ToJsonString());
            await Console.Out.FlushAsync();
        }
        else if (method == "test/brokenPipe")
        {
            return 0;
        }
        else if (method == "thread/compact/start")
        {
            if (!await ContextReasoningChecks.WriteCompactFixtureAsync(request!)) return 0;
        }
        else if (method is "fixture/compactBacklog" or "fixture/compactDispatchCount")
        {
            await ContextReasoningChecks.WriteBacklogFixtureAsync(request!);
        }
        else if (method == "turn/start" && request?["params"]?["threadId"]?.GetValue<string>() == "fake-large-output-thread")
        {
            const string threadId = "fake-large-output-thread";
            const string turnId = "fake-large-output-turn";
            const int deltaCharacters = 2_000;
            const int deltaCount = 2_500;
            var assistantText = new string('a', deltaCharacters * deltaCount);
            var toolOutput = new string('t', 2_000_000);
            Console.Out.WriteLine(new JsonObject
            {
                ["id"] = id,
                ["result"] = new JsonObject
                {
                    ["turn"] = new JsonObject { ["id"] = turnId, ["status"] = "inProgress", ["items"] = new JsonArray() }
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "turn/started",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turn"] = new JsonObject { ["id"] = turnId, ["status"] = "inProgress", ["items"] = new JsonArray() }
                }
            }.ToJsonString());
            for (var index = 0; index < deltaCount; index++)
            {
                Console.Out.WriteLine(new JsonObject
                {
                    ["method"] = "item/agentMessage/delta",
                    ["params"] = new JsonObject
                    {
                        ["threadId"] = threadId,
                        ["turnId"] = turnId,
                        ["itemId"] = "large-assistant-item",
                        ["delta"] = new string('a', deltaCharacters)
                    }
                }.ToJsonString());
            }
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "item/completed",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turnId"] = turnId,
                    ["item"] = new JsonObject
                    {
                        ["id"] = "large-assistant-item",
                        ["type"] = "agentMessage",
                        ["text"] = assistantText
                    }
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "item/completed",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turnId"] = turnId,
                    ["item"] = new JsonObject
                    {
                        ["id"] = "large-tool-item",
                        ["type"] = "commandExecution",
                        ["status"] = "completed",
                        ["command"] = "synthetic command",
                        ["aggregatedOutput"] = toolOutput,
                        ["exitCode"] = 0
                    }
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "turn/completed",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turn"] = new JsonObject { ["id"] = turnId, ["status"] = "completed", ["items"] = new JsonArray() }
                }
            }.ToJsonString());
            await Console.Out.FlushAsync();
        }
        else if (method == "turn/start" && request?["params"]?["threadId"]?.GetValue<string>() == "fake-error-thread")
        {
            const string threadId = "fake-error-thread";
            const string turnId = "fake-error-turn";
            Console.Out.WriteLine(new JsonObject
            {
                ["id"] = id,
                ["result"] = new JsonObject
                {
                    ["turn"] = new JsonObject
                    {
                        ["id"] = turnId,
                        ["status"] = "inProgress",
                        ["items"] = new JsonArray()
                    }
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "error",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turnId"] = turnId,
                    ["error"] = new JsonObject
                    {
                        ["message"] = "429 Too Many Requests",
                        ["codexErrorInfo"] = new JsonObject
                        {
                            ["responseTooManyFailedAttempts"] = new JsonObject
                            {
                                ["httpStatusCode"] = 429
                            }
                        }
                    }
                }
            }.ToJsonString());
            await Console.Out.FlushAsync();
        }
        else if (method == "turn/start" && request?["params"]?["threadId"]?.GetValue<string>() == "fake-malformed-thread")
        {
            Console.Out.WriteLine(new JsonObject
            {
                ["id"] = id,
                ["result"] = new JsonObject
                {
                    ["turn"] = new JsonObject
                    {
                        ["id"] = "fake-malformed-turn",
                        ["status"] = "inProgress",
                        ["items"] = new JsonArray()
                    }
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "turn/started",
                ["params"] = new JsonObject
                {
                    ["threadId"] = "fake-malformed-thread",
                    ["turn"] = new JsonObject
                    {
                        ["id"] = "fake-malformed-turn",
                        ["status"] = "inProgress",
                        ["items"] = new JsonArray()
                    }
                }
            }.ToJsonString());
            Console.Out.WriteLine("{malformed-json");
            await Console.Out.FlushAsync();
            return 0;
        }
        else if (method == "turn/start" && request?["params"]?["threadId"]?.GetValue<string>() == "fake-unknown-thread")
        {
            const string threadId = "fake-unknown-thread";
            const string turnId = "fake-unknown-turn";
            Console.Out.WriteLine(new JsonObject
            {
                ["id"] = id,
                ["result"] = new JsonObject
                {
                    ["turn"] = new JsonObject
                    {
                        ["id"] = turnId,
                        ["status"] = "inProgress",
                        ["items"] = new JsonArray()
                    }
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "turn/started",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turn"] = new JsonObject { ["id"] = turnId, ["status"] = "inProgress", ["items"] = new JsonArray() }
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "future/progress",
                ["params"] = new JsonObject { ["threadId"] = threadId, ["turnId"] = turnId, ["traceId"] = "trace-1" }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "turn/completed",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turn"] = new JsonObject { ["id"] = turnId, ["status"] = "completed", ["items"] = new JsonArray() }
                }
            }.ToJsonString());
            await Console.Out.FlushAsync();
            return 0;
        }
        else if (method == "turn/start" && request?["params"]?["threadId"]?.GetValue<string>() == "fake-thread")
        {
            Console.Out.WriteLine(new JsonObject
            {
                ["id"] = id,
                ["result"] = new JsonObject
                {
                    ["turn"] = new JsonObject
                    {
                        ["id"] = "fake-turn",
                        ["status"] = "inProgress",
                        ["items"] = new JsonArray()
                    }
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "turn/started",
                ["params"] = new JsonObject
                {
                    ["threadId"] = "fake-thread",
                    ["turn"] = new JsonObject
                    {
                        ["id"] = "fake-turn",
                        ["status"] = "inProgress",
                        ["items"] = new JsonArray()
                    }
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "item/agentMessage/delta",
                ["params"] = new JsonObject
                {
                    ["threadId"] = "fake-thread",
                    ["turnId"] = "fake-turn",
                    ["itemId"] = "fake-partial-message",
                    ["delta"] = "Partial answer before cancellation."
                }
            }.ToJsonString());
            await Console.Out.FlushAsync();
        }
        else if (method == "turn/start" && request?["params"]?["threadId"]?.GetValue<string>() == "fake-command-list-race-thread")
        {
            const string threadId = "fake-command-list-race-thread";
            const string turnId = "fake-command-list-race-turn";
            const string itemId = "fake-command-list-race-item";
            Console.Out.WriteLine(new JsonObject
            {
                ["id"] = id,
                ["result"] = new JsonObject
                {
                    ["turn"] = new JsonObject { ["id"] = turnId, ["status"] = "inProgress", ["items"] = new JsonArray() }
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "turn/started",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turn"] = new JsonObject { ["id"] = turnId, ["status"] = "inProgress", ["items"] = new JsonArray() }
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "item/started",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turnId"] = turnId,
                    ["item"] = new JsonObject
                    {
                        ["id"] = itemId,
                        ["type"] = "commandExecution",
                        ["command"] = "controlled delayed list command",
                        ["status"] = "inProgress"
                    }
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "turn/completed",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turn"] = new JsonObject { ["id"] = turnId, ["status"] = "interrupted", ["items"] = new JsonArray() }
                }
            }.ToJsonString());
            await Console.Out.FlushAsync();
        }
        else if (method == "thread/commandExecution/list"
            && request?["params"]?["threadId"]?.GetValue<string>() == "fake-command-list-race-thread")
        {
            const string threadId = "fake-command-list-race-thread";
            const string turnId = "fake-command-list-race-turn";
            const string itemId = "fake-command-list-race-item";
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "item/completed",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turnId"] = turnId,
                    ["item"] = new JsonObject
                    {
                        ["id"] = itemId,
                        ["type"] = "commandExecution",
                        ["command"] = "controlled delayed list command",
                        ["status"] = "completed",
                        ["exitCode"] = 0
                    }
                }
            }.ToJsonString());
            await Console.Out.FlushAsync();
            await Task.Delay(100);
            Console.Out.WriteLine(new JsonObject
            {
                ["id"] = id,
                ["result"] = new JsonObject
                {
                    ["data"] = new JsonArray(new JsonObject
                    {
                        ["itemId"] = itemId,
                        ["processId"] = "4242",
                        ["command"] = "controlled delayed list command"
                    })
                }
            }.ToJsonString());
            await Console.Out.FlushAsync();
        }
        else if (method == "turn/interrupt")
        {
            var parameters = request?["params"]?.AsObject();
            if (parameters?["threadId"]?.GetValue<string>() != "fake-thread"
                || parameters["turnId"]?.GetValue<string>() != "fake-turn")
            {
                return 25;
            }

            Console.Out.WriteLine(new JsonObject
            {
                ["id"] = id,
                ["result"] = new JsonObject()
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "turn/completed",
                ["params"] = new JsonObject
                {
                    ["threadId"] = "fake-thread",
                    ["turn"] = new JsonObject
                    {
                        ["id"] = "fake-turn",
                        ["status"] = "interrupted",
                        ["items"] = new JsonArray(),
                        ["error"] = null
                    }
                }
            }.ToJsonString());
            await Console.Out.FlushAsync();
        }
        else if (method == "turn/start" && request?["params"]?["threadId"]?.GetValue<string>() == "fake-approval-timeout-thread")
        {
            const string threadId = "fake-approval-timeout-thread";
            const string turnId = "fake-approval-timeout-turn";
            Console.Out.WriteLine(new JsonObject
            {
                ["id"] = id,
                ["result"] = new JsonObject
                {
                    ["turn"] = new JsonObject { ["id"] = turnId, ["status"] = "inProgress", ["items"] = new JsonArray() }
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "turn/started",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turn"] = new JsonObject { ["id"] = turnId, ["status"] = "inProgress", ["items"] = new JsonArray() }
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["id"] = 51,
                ["method"] = "item/commandExecution/requestApproval",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turnId"] = turnId,
                    ["itemId"] = "timeout-command",
                    ["startedAtMs"] = 1,
                    ["command"] = "cmd.exe /d /c ver",
                    ["cwd"] = "D:\\workspace",
                    ["availableDecisions"] = new JsonArray("accept", "decline", "cancel")
                }
            }.ToJsonString());
            await Console.Out.FlushAsync();

            var responseLine = await Console.In.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var response = responseLine is null ? null : JsonNode.Parse(responseLine)?.AsObject();
            if (response is null
                || response["id"]?.GetValue<long>() != 51
                || response["result"]?["decision"]?.GetValue<string>() != "decline")
            {
                return 31;
            }

            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "serverRequest/resolved",
                ["params"] = new JsonObject { ["threadId"] = threadId, ["requestId"] = 51 }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "turn/completed",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turn"] = new JsonObject { ["id"] = turnId, ["status"] = "completed", ["items"] = new JsonArray() }
                }
            }.ToJsonString());
            await Console.Out.FlushAsync();
            return 0;
        }
        else if (method == "turn/start" && request?["params"]?["threadId"]?.GetValue<string>() == "fake-approval-thread")
        {
            const string threadId = "fake-approval-thread";
            const string turnId = "fake-approval-turn";
            Console.Out.WriteLine(new JsonObject
            {
                ["id"] = id,
                ["result"] = new JsonObject
                {
                    ["turn"] = new JsonObject
                    {
                        ["id"] = turnId,
                        ["status"] = "inProgress",
                        ["items"] = new JsonArray()
                    }
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "turn/started",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turn"] = new JsonObject { ["id"] = turnId, ["status"] = "inProgress", ["items"] = new JsonArray() }
                }
            }.ToJsonString());
            await Console.Out.FlushAsync();

            async Task<bool> ReadExpectedResponseAsync(long expectedId, Func<JsonObject, bool> predicate)
            {
                var line = await Console.In.ReadLineAsync();
                var response = line is null ? null : JsonNode.Parse(line)?.AsObject();
                return response?["id"]?.GetValue<long>() == expectedId && predicate(response);
            }

            async Task SendServerRequestAsync(long requestId, string methodName, JsonObject parameters)
            {
                Console.Out.WriteLine(new JsonObject
                {
                    ["id"] = requestId,
                    ["method"] = methodName,
                    ["params"] = parameters
                }.ToJsonString());
                await Console.Out.FlushAsync();
            }

            async Task SendServerRequestResolvedAsync(long requestId)
            {
                Console.Out.WriteLine(new JsonObject
                {
                    ["method"] = "serverRequest/resolved",
                    ["params"] = new JsonObject { ["threadId"] = threadId, ["requestId"] = requestId }
                }.ToJsonString());
                await Console.Out.FlushAsync();
            }

            await SendServerRequestAsync(41, "item/commandExecution/requestApproval", new JsonObject
            {
                ["threadId"] = threadId,
                ["turnId"] = turnId,
                ["itemId"] = "cmd-1",
                ["startedAtMs"] = 1,
                ["command"] = "cmd.exe /d /c ver",
                ["cwd"] = "D:\\workspace",
                ["availableDecisions"] = new JsonArray("accept", "acceptForSession", "decline", "cancel")
            });
            if (!await ReadExpectedResponseAsync(41, response => response["result"]?["decision"]?.GetValue<string>() == "accept")) return 26;
            await SendServerRequestResolvedAsync(41);

            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "item/fileChange/patchUpdated",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turnId"] = turnId,
                    ["itemId"] = "file-1",
                    ["changes"] = new JsonArray(new JsonObject
                    {
                        ["path"] = "D:\\workspace\\approved.txt",
                        ["kind"] = "add",
                        ["diff"] = "+approved\n"
                    })
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "item/started",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turnId"] = turnId,
                    ["item"] = new JsonObject
                    {
                        ["type"] = "fileChange",
                        ["id"] = "file-1",
                        ["status"] = "inProgress",
                        ["changes"] = new JsonArray(new JsonObject
                        {
                            ["path"] = "D:\\workspace\\approved.txt",
                            ["kind"] = new JsonObject { ["type"] = "add" },
                            ["diff"] = "+approved\n"
                        })
                    }
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "item/fileChange/patchUpdated",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turnId"] = turnId,
                    ["itemId"] = "file-1",
                    ["changes"] = new JsonArray(new JsonObject
                    {
                        ["path"] = "D:\\workspace\\approved.txt",
                        ["kind"] = "add",
                        ["diff"] = "+approved\n"
                    })
                }
            }.ToJsonString());
            await Console.Out.FlushAsync();

            await SendServerRequestAsync(42, "item/fileChange/requestApproval", new JsonObject
            {
                ["threadId"] = threadId,
                ["turnId"] = turnId,
                ["itemId"] = "file-1",
                ["startedAtMs"] = 1,
                ["reason"] = "Write requested file"
            });
            if (!await ReadExpectedResponseAsync(42, response => response["result"]?.AsObject().Count == 1
                && response["result"]?["decision"]?.GetValue<string>() == "accept")) return 27;
            await SendServerRequestResolvedAsync(42);

            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "item/started",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turnId"] = turnId,
                    ["item"] = new JsonObject
                    {
                        ["type"] = "fileChange",
                        ["id"] = "file-2",
                        ["status"] = "inProgress",
                        ["changes"] = new JsonArray(new JsonObject
                        {
                            ["path"] = "D:\\workspace\\changed-after-preview.txt",
                            ["kind"] = new JsonObject { ["type"] = "add" },
                            ["diff"] = "+original\n"
                        })
                    }
                }
            }.ToJsonString());
            await Console.Out.FlushAsync();
            await SendServerRequestAsync(45, "item/fileChange/requestApproval", new JsonObject
            {
                ["threadId"] = threadId,
                ["turnId"] = turnId,
                ["itemId"] = "file-2",
                ["startedAtMs"] = 2,
                ["reason"] = "Write requested file"
            });
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "item/fileChange/patchUpdated",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turnId"] = turnId,
                    ["itemId"] = "file-2",
                    ["changes"] = new JsonArray(new JsonObject
                    {
                        ["path"] = "D:\\workspace\\changed-after-preview.txt",
                        ["kind"] = "add",
                        ["diff"] = "+changed\n"
                    })
                }
            }.ToJsonString());
            await Console.Out.FlushAsync();
            if (!await ReadExpectedResponseAsync(45, response => response["result"]?["decision"]?.GetValue<string>() == "decline")) return 30;
            await SendServerRequestResolvedAsync(45);

            var permissionProfile = new JsonObject
            {
                ["network"] = new JsonObject { ["enabled"] = true },
                ["fileSystem"] = new JsonObject { ["write"] = new JsonArray("D:\\workspace\\out.txt") }
            };
            await SendServerRequestAsync(43, "item/permissions/requestApproval", new JsonObject
            {
                ["threadId"] = threadId,
                ["turnId"] = turnId,
                ["itemId"] = "perm-1",
                ["startedAtMs"] = 1,
                ["cwd"] = "D:\\workspace",
                ["reason"] = "Write the requested output",
                ["permissions"] = permissionProfile.DeepClone()
            });
            if (!await ReadExpectedResponseAsync(43, response =>
                JsonNode.DeepEquals(response["result"]?["permissions"], permissionProfile)
                && response["result"]?["scope"]?.GetValue<string>() == "turn")) return 28;
            await SendServerRequestResolvedAsync(43);

            await SendServerRequestAsync(44, "mcpServer/elicitation/request", new JsonObject { ["prompt"] = "unknown request" });
            if (!await ReadExpectedResponseAsync(44, response => response["error"]?["code"]?.GetValue<int>() == -32001)) return 29;

            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "turn/completed",
                ["params"] = new JsonObject
                {
                    ["threadId"] = threadId,
                    ["turn"] = new JsonObject { ["id"] = turnId, ["status"] = "completed", ["items"] = new JsonArray() }
                }
            }.ToJsonString());
            await Console.Out.FlushAsync();
            return 0;
        }
        else if (method == "turn/start")
        {
            Console.Out.WriteLine(new JsonObject
            {
                ["id"] = id,
                ["result"] = new JsonObject
                {
                    ["turn"] = new JsonObject
                    {
                        ["id"] = "fake-turn-exit",
                        ["status"] = "inProgress",
                        ["items"] = new JsonArray()
                    }
                }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "turn/started",
                ["params"] = new JsonObject { ["turnId"] = "fake-turn" }
            }.ToJsonString());
            Console.Out.WriteLine(new JsonObject
            {
                ["method"] = "item/agentMessage/delta",
                ["params"] = new JsonObject
                {
                    ["turnId"] = "fake-turn-exit",
                    ["itemId"] = "fake-exit-partial-message",
                    ["delta"] = "Partial output before App Server exit."
                }
            }.ToJsonString());
            await Console.Out.FlushAsync();
            return 23;
        }
    }

    return 0;
}

static void RunRenameAndCapabilitySwitchChecks()
{
    Assert(AppServerProtocol.IsNamedHostOperation("renameSavedThread"),
        "the exact renameSavedThread host route is not exposed");
    var parsed = DiagnosticOperation.Parse(
        "{\"operation\":\"renameSavedThread\",\"requestId\":\"rename-1\",\"threadId\":\"thread-fixture\",\"name\":\"  Reviewed title  \"}");
    Assert(parsed.Payload["name"]?.GetValue<string>() == "Reviewed title",
        "the host did not return the bounded trimmed thread name");
    foreach (var invalid in new[]
    {
        "{\"operation\":\"renameSavedThread\",\"requestId\":\"r\",\"threadId\":\"t\",\"name\":\"  \"}",
        "{\"operation\":\"renameSavedThread\",\"requestId\":\"r\",\"threadId\":\"t\",\"name\":\"bad\\nname\"}",
        "{\"operation\":\"renameSavedThread\",\"requestId\":\"r\",\"threadId\":\"t\",\"name\":\"" + new string('x', 161) + "\"}",
        "{\"operation\":\"renameSavedThread\",\"requestId\":\"r\",\"threadId\":\"t\",\"name\":\"ok\",\"providerId\":\"nvidia\"}"
    })
    {
        AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(invalid));
    }

    var request = AppServerProtocol.BuildThreadNameSetRequest(41, "thread-fixture", "Reviewed title");
    Assert(request.Id == 41 && request.Method == "thread/name/set"
        && request.Params.Count == 2
        && request.Params["threadId"]?.GetValue<string>() == "thread-fixture"
        && request.Params["name"]?.GetValue<string>() == "Reviewed title",
        "the typed thread/name/set route did not emit its exact two-field payload");

    var differentCapabilityHistory = new JsonObject
    {
        ["id"] = "thread-fixture",
        ["modelProvider"] = "historical-provider",
        ["model"] = "historical-model",
        ["cwd"] = "C:\\NeoBabylon\\fixture"
    };
    ThreadRenameIdentity.RequireExactWorkspaceRead(
        differentCapabilityHistory, "thread-fixture", "C:\\NeoBabylon\\fixture");
    AssertThrows<InvalidOperationException>(() => ThreadRenameIdentity.RequireExactWorkspaceRead(
        differentCapabilityHistory, "foreign-thread", "C:\\NeoBabylon\\fixture"));
    AssertThrows<InvalidOperationException>(() => ThreadRenameIdentity.RequireExactWorkspaceRead(
        differentCapabilityHistory, "thread-fixture", "C:\\NeoBabylon\\other"));
    AssertThrows<InvalidOperationException>(() => ThreadRenameIdentity.RequireRenameConfirmation(
        new JsonObject { ["id"] = "thread-fixture", ["name"] = "Old title" },
        "thread-fixture", "Reviewed title"));
    ThreadRenameIdentity.RequireRenameConfirmation(
        new JsonObject { ["id"] = "thread-fixture", ["name"] = "Reviewed title" },
        "thread-fixture", "Reviewed title");

    var nullableThreadSelection = DiagnosticOperation.Parse(
        "{\"operation\":\"selectCapability\",\"requestId\":\"select-1\",\"providerId\":\"openrouter\",\"modelIdentifier\":\"stealth/space-bunny-alpha\",\"threadId\":null}");
    Assert(nullableThreadSelection.Payload.ContainsKey("threadId") && nullableThreadSelection.Payload["threadId"] is null,
        "the capability selection allowlist did not preserve nullable threadId");
    var exactThreadSelection = DiagnosticOperation.Parse(
        "{\"operation\":\"selectCapability\",\"requestId\":\"select-2\",\"providerId\":\"openrouter\",\"modelIdentifier\":\"stealth/space-bunny-alpha\",\"threadId\":\"thread-fixture\"}");
    Assert(exactThreadSelection.Payload["threadId"]?.GetValue<string>() == "thread-fixture",
        "the capability selection allowlist dropped the exact preserved thread id");
    AssertThrows<InvalidDataException>(() => DiagnosticOperation.Parse(
        "{\"operation\":\"selectCapability\",\"requestId\":\"select-3\",\"providerId\":\"openrouter\",\"modelIdentifier\":\"stealth/space-bunny-alpha\",\"threadId\":\"thread-fixture\",\"forceNewTask\":true}"));

    var releaseRoot = Path.Combine(Directory.GetCurrentDirectory(), "docs", "release");
    ModelCapabilityRecord ReadCapability(string fileName) => JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(Path.Combine(releaseRoot, fileName)), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException($"Capability fixture {fileName} is empty.");
    var originalA = ReadCapability("MODEL_CAPABILITY_OPENROUTER_STEALTH_SPACE_BUNNY_ALPHA.json");
    var switchedB = ReadCapability("MODEL_CAPABILITY_NVIDIA_GLM_5_3.json");
    var switchBackA = originalA;
    var glmResume = AppServerProtocol.BuildThreadResumeRequest(51, "thread-fixture", ToolExecutionPolicy.Unrestricted, switchedB);
    Assert(glmResume.Params["model"]?.GetValue<string>() == "z-ai/glm-5.3"
        && glmResume.Params["modelProvider"]?.GetValue<string>() == "nvidia"
        && glmResume.Params["config"]?["model_reasoning_effort"]?.GetValue<string>() == "max",
        "GLM resume did not carry its exact model/provider and recorded max reasoning default");
    var kimiResume = AppServerProtocol.BuildThreadResumeRequest(52, "thread-fixture", ToolExecutionPolicy.Unrestricted,
        ReadCapability("MODEL_CAPABILITY_NVIDIA_KIMI_K3.json"));
    Assert(!kimiResume.Params.ContainsKey("config"),
        "Kimi's absent reasoning default emitted a synthetic reasoning override");
    var deepseekResume = AppServerProtocol.BuildThreadResumeRequest(53, "thread-fixture", ToolExecutionPolicy.Unrestricted,
        ReadCapability("MODEL_CAPABILITY_NVIDIA_DEEPSEEK_V4_1_FLASH.json"));
    Assert(!deepseekResume.Params.ContainsKey("config"),
        "Unknown DeepSeek reasoning controls emitted an unsupported synthetic reasoning override");

    CapabilitySwitchSafety.RequireNoActiveContinuingCommands(new JsonObject { ["data"] = new JsonArray() });
    AssertThrows<InvalidOperationException>(() => CapabilitySwitchSafety.RequireNoActiveContinuingCommands(
        new JsonObject { ["data"] = new JsonArray(new JsonObject { ["itemId"] = "active-command" }) }));
    AssertThrows<InvalidOperationException>(() => CapabilitySwitchSafety.RequireNoActiveContinuingCommands(new JsonObject()));

    var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-Capability-Switch-Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        const string threadId = "switch-a-b-a-thread";
        var workspace = Path.GetPathRoot(Environment.SystemDirectory)
            ?? throw new InvalidDataException("The test system directory has no filesystem root.");
        var originalBinding = ThreadCapabilityBindingStore.Bind(root, threadId, workspace, originalA);
        ThreadCapabilityBindingStore.RecordExplicitSwitch(root, threadId, workspace, switchedB);
        Assert(ThreadCapabilityBindingStore.MatchesCurrent(root, threadId, switchedB, workspace)
            && !ThreadCapabilityBindingStore.MatchesCurrent(root, threadId, originalA, workspace),
            "the B transition did not become the sole current capability selection");
        ThreadCapabilityBindingStore.RecordExplicitSwitch(root, threadId, workspace, switchBackA);
        Assert(ThreadCapabilityBindingStore.MatchesCurrent(root, threadId, switchBackA, workspace)
            && !ThreadCapabilityBindingStore.MatchesCurrent(root, threadId, switchedB, workspace),
            "A→B→A history incorrectly authorized an earlier non-current tuple");
        Assert(ThreadCapabilityBindingStore.Read(root, threadId) == originalBinding,
            "explicit switches rewrote the immutable original thread binding");
        var latest = ThreadCapabilityBindingStore.ReadCurrentSelection(root, threadId);
        Assert(latest == (switchBackA.ProviderId, switchBackA.ModelIdentifier),
            "reopening the sidecar did not expose the latest explicitly selected tuple");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    var nvidiaEnvironment = AppServerLaunchEnvironment.Build(new AppServerLaunchOptions(
        "C:\\runtime\\codex.exe", "C:\\fixture", "C:\\isolated\\CodexHome",
        null, NvidiaSessionToken: "synthetic-session-token"));
    Assert(nvidiaEnvironment["NEOBABYLON_PROVIDER_SESSION_TOKEN"] == "synthetic-session-token"
        && !nvidiaEnvironment.ContainsKey("NVIDIA_API_KEY")
        && !nvidiaEnvironment.ContainsKey("OPENROUTER_API_KEY")
        && !nvidiaEnvironment.ContainsKey("CODEX_OSS_BASE_URL"),
        "NVIDIA App Server launch environment must carry only its adapter session token, never provider keys or OSS base URL");
    Console.WriteLine("RENAME_AND_CAPABILITY_SWITCH_CHECKS=PASS");
}

static async Task RunSameThreadCapabilitySwitchFixtureAsync()
{
    var sourceRoot = Directory.GetCurrentDirectory();
    var runId = $"Nvidia-20261004-switch-{Guid.NewGuid():N}";
    var applicationRoot = Path.Combine(sourceRoot, ".local", "Lab", "Runs", runId, "App");
    var capability = JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The local fixture capability is empty.");
    capability = capability with
    {
        ReasoningControls = new CapabilityObservation(
            CapabilityState.Known,
            JsonNode.Parse("{\"supported_efforts\":[\"low\",\"high\"],\"default_effort\":\"high\"}"),
            "synthetic-resume-fixture")
    };
    var nextCapability = capability with
    {
        ModelIdentifier = "neobabylon-fixture-next-model",
        ModelVariant = "fixture-next-model",
        ReasoningControls = CapabilityObservation.Unknown("synthetic-resume-fixture", "selected model exposes no bounded effort mapping")
    };
    await using var fixture = await UnrestrictedResponsesFixture.StartAsync(responseSequence:
    [
        ResponsesSse.AssistantMessage("switch-fixture-response", "switch-fixture-message", "Fixture turn persisted."),
        ResponsesSse.AssistantMessage("switch-fixture-response-2", "switch-fixture-message-2", "Unknown-reasoning model fixture reply.")
    ]);
    capability = capability with { Endpoint = fixture.BaseUrl };
    nextCapability = nextCapability with { Endpoint = fixture.BaseUrl };
    try
    {
        await using var supervisor = new RuntimeSupervisor(sourceRoot, applicationRoot);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var started = await supervisor.StartThreadAsync(capability, timeout.Token);
        var threadId = started["thread"]?["thread"]?["id"]?.GetValue<string>()
            ?? started["thread"]?["id"]?.GetValue<string>()
            ?? throw new InvalidDataException("The isolated App Server did not return a thread id.");
        var completedTurn = await supervisor.StartTurnAsync("Persist a local deterministic fixture turn.", timeout.Token);
        Assert(completedTurn["completed"]?.GetValue<bool>() == true,
            "the isolated deterministic fixture turn did not complete");
        var firstProviderRequest = JsonNode.Parse(fixture.RequestBodies[0]) as JsonObject;
        Assert(firstProviderRequest?["reasoning"]?["effort"]?.GetValue<string>() == "high",
            "the fixture did not seed a persisted high reasoning effort before switching");

        var switched = await supervisor.SwitchCapabilityForCurrentThreadAsync(
            nextCapability, threadId, timeout.Token);
        Assert(switched["threadId"]?.GetValue<string>() == threadId
            && switched["threadPreserved"]?.GetValue<bool>() == true
            && switched["modelProvider"]?.GetValue<string>() == nextCapability.ProviderId
            && switched["model"]?.GetValue<string>() == nextCapability.ModelIdentifier
            && switched["turns"] is JsonArray,
            "the isolated capability switch did not preserve and confirm the exact App Server thread");
        var nextTurn = await supervisor.StartTurnAsync("Check the selected model's default reasoning behavior.", timeout.Token);
        Assert(nextTurn["completed"]?.GetValue<bool>() == true,
            "the deterministic post-switch fixture turn did not complete");
        var nextProviderRequest = JsonNode.Parse(fixture.RequestBodies[1]) as JsonObject;
        Assert(nextProviderRequest?["reasoning"]?["effort"] is null,
            "the Unknown target serialized an inherited or synthetic reasoning effort to the provider");

        var openRouter = JsonSerializer.Deserialize<ModelCapabilityRecord>(
            File.ReadAllText(Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_OPENROUTER_STEALTH_SPACE_BUNNY_ALPHA.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("The OpenRouter missing-key fixture is empty.");
        await AssertThrowsAsync<InvalidOperationException>(() =>
            supervisor.SwitchCapabilityForCurrentThreadAsync(openRouter, threadId, timeout.Token));
        var afterMissingCredential = supervisor.GetRuntimeStatus();
        Assert(afterMissingCredential["currentThreadId"]?.GetValue<string>() == threadId
            && afterMissingCredential["currentProviderId"]?.GetValue<string>() == nextCapability.ProviderId
            && afterMissingCredential["currentModelIdentifier"]?.GetValue<string>() == nextCapability.ModelIdentifier,
            "a missing credential changed the active thread or model tuple");

        var renamed = await supervisor.RenameSavedThreadAsync(nextCapability, threadId, "Fixture persisted title", timeout.Token);
        Assert(renamed["threadId"]?.GetValue<string>() == threadId
            && renamed["name"]?.GetValue<string>() == "Fixture persisted title",
            "thread/name/set did not return the runtime-confirmed exact identity and title");
        await AssertThrowsAsync<AppServerRpcException>(() =>
            supervisor.RenameSavedThreadAsync(nextCapability, "00000000-0000-7000-8000-000000000001", "Unknown fixture", timeout.Token));

        var readBack = await supervisor.ResumeThreadAsync(nextCapability, threadId, timeout.Token);
        Assert(readBack["threadId"]?.GetValue<string>() == threadId
            && readBack["thread"]?["name"]?.GetValue<string>() == "Fixture persisted title",
            "the name did not persist on the exact App Server thread after a later read/resume");
        var listed = await supervisor.ListThreadsAsync(nextCapability, null, timeout.Token);
        var row = (listed["threads"] as JsonArray)?.OfType<JsonObject>()
            .SingleOrDefault(item => item["id"]?.GetValue<string>() == threadId);
        Assert(row?["name"]?.GetValue<string>() == "Fixture persisted title"
            && row["modelProvider"]?.GetValue<string>() == nextCapability.ProviderId
            && row["model"]?.GetValue<string>() == nextCapability.ModelIdentifier,
            "saved history listing did not expose the persisted name and latest explicit capability tuple");

        var switchedBack = await supervisor.SwitchCapabilityForCurrentThreadAsync(capability, threadId, timeout.Token);
        Assert(switchedBack["threadId"]?.GetValue<string>() == threadId
            && supervisor.GetRuntimeStatus()["currentThreadId"]?.GetValue<string>() == threadId,
            "switching the same conversation back changed its persistent thread identity");
        Console.WriteLine("SAME_THREAD_CAPABILITY_SWITCH_FIXTURE=PASS");
        Console.WriteLine($"SAME_THREAD_FIXTURE_HISTORY_TURNS={((switchedBack["turns"] as JsonArray)?.Count ?? -1)}");
    }
    finally
    {
        var safeRunsRoot = Path.GetFullPath(Path.Combine(sourceRoot, ".local", "Lab", "Runs"));
        var exactRoot = Path.GetFullPath(applicationRoot);
        if (!exactRoot.StartsWith(safeRunsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The isolated fixture cleanup target escaped its task run directory.");
        if (Directory.Exists(exactRoot))
        {
            foreach (var file in Directory.EnumerateFiles(exactRoot, "*", SearchOption.AllDirectories))
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
            Directory.Delete(exactRoot, recursive: true);
        }
    }
}

static void RunLingActiveSelectionChecks()
{
    var sourceRoot = Directory.GetCurrentDirectory();
    var releaseRoot = Path.Combine(sourceRoot, "docs", "release");
    var historicalPath = Path.Combine(releaseRoot, "MODEL_CAPABILITY_OPENROUTER_NEX_N2_5_PRO_FREE.json");
    var historicalBytes = File.ReadAllBytes(historicalPath);
    var historical = JsonSerializer.Deserialize<ModelCapabilityRecord>(historicalBytes, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The historical NEX capability record is empty.");
    var ling = JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(Path.Combine(releaseRoot, "MODEL_CAPABILITY_OPENROUTER_LING_3_1_FLASH.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The canonical Ling capability record is empty.");
    var failures = new List<string>();
    void Require(bool condition, string message) { if (!condition) failures.Add(message); }
    Require(ActiveCapabilitySelection.IsSelectable(ling), "Ling was excluded from new selection");
    Require(!ActiveCapabilitySelection.IsSelectable(historical), "historical NEX is still offered for new selection");
    try
    {
        ActiveCapabilitySelection.RequireSelectable(historical);
        failures.Add("explicit new NEX selection was not rejected");
    }
    catch (InvalidOperationException) { }
    ActiveCapabilitySelection.RequireSelectable(ling);
    Require(ling.ReasoningControls.State == CapabilityState.Unknown && ling.ReasoningControls.Value is null,
        "active selection invented Ling reasoning controls");
    foreach (var path in Directory.EnumerateFiles(releaseRoot, "MODEL_CAPABILITY_*.json"))
    {
        var record = JsonSerializer.Deserialize<ModelCapabilityRecord>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("A catalog capability fixture is empty.");
        if (record.ProviderId == historical.ProviderId && record.ModelIdentifier == historical.ModelIdentifier) continue;
        Require(ActiveCapabilitySelection.IsSelectable(record), "an unrelated canonical model was retired");
    }
    foreach (var provider in new[] { "lmstudio", "nvidia" })
        Require(ActiveCapabilitySelection.IsSelectable(historical with { ProviderId = provider }), "retirement leaked to another provider");
    Require(ActiveCapabilitySelection.IsSelectable(historical with { ModelIdentifier = "nex-agi/another-model" }),
        "retirement leaked to another model");

    var dataRoot = Path.Combine(sourceRoot, ".local", "Lab", "Runs", "Ling-20261004-selection-" + Guid.NewGuid().ToString("N"), "App", "Data");
    var workspace = Path.Combine(dataRoot, "Workspace");
    var original = ThreadCapabilityBindingStore.Bind(dataRoot, "ling-selection-historical-fixture", workspace, historical);
    var read = ThreadCapabilityBindingStore.Read(dataRoot, original.ThreadId);
    Require(read == original && ThreadCapabilityBindingStore.Matches(read!, historical, workspace)
        && ThreadCapabilityBindingStore.MatchesCurrent(dataRoot, original.ThreadId, historical, workspace),
        "new-selection retirement changed the readable/matching historical NEX binding");
    Require(File.ReadAllBytes(historicalPath).AsSpan().SequenceEqual(historicalBytes), "the historical NEX JSON bytes changed");
    Assert(failures.Count == 0, string.Join("; ", failures));
    Console.WriteLine("LING_ACTIVE_SELECTION=PASS");
    Console.WriteLine("LING_SELECTION_FIXTURE=Ling active, NEX retired from new selection only, other models unchanged, historical binding intact");
}

static async Task RunCrossProviderThreadHistoryChecksAsync()
{
    var sourceRoot = Directory.GetCurrentDirectory();
    var runsRoot = Path.GetFullPath(Path.Combine(sourceRoot, ".local", "Lab", "Runs"));
    var fixtureRoot = Path.Combine(runsRoot, "Nvidia-20261004-history-" + Guid.NewGuid().ToString("N"));
    var applicationRoot = Path.Combine(fixtureRoot, "App");
    var layout = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
    var foreignWorkspace = Path.Combine(layout.DataRoot, "OtherWorkspace");
    Directory.CreateDirectory(foreignWorkspace);
    foreach (var stateDbOnly in new[] { false, true })
    {
        var request = AppServerProtocol.BuildThreadListRequest(
            71, useStateDbOnly: stateDbOnly, cursor: "history-page", cwd: layout.FixtureWorkspace);
        Assert(request.Method == "thread/list"
            && request.Params["modelProviders"] is JsonArray { Count: 0 }
            && request.Params["cwd"]?.GetValue<string>() == layout.FixtureWorkspace
            && request.Params["useStateDbOnly"]?.GetValue<bool>() == stateDbOnly
            && request.Params["cursor"]?.GetValue<string>() == "history-page",
            "scan/state DB history must explicitly include all providers while retaining the exact workspace and cursor");
    }

    await using var fixture = await UnrestrictedResponsesFixture.StartAsync(responseSequence:
    [
        ResponsesSse.AssistantMessage("history-or-response", "history-or-message", "Persisted OpenRouter fixture."),
        ResponsesSse.AssistantMessage("history-foreign-response", "history-foreign-message", "Persisted foreign workspace fixture."),
        ResponsesSse.AssistantMessage("history-local-response", "history-local-message", "Persisted local provider fixture.")
    ]);
    var sourceCapability = JsonSerializer.Deserialize<ModelCapabilityRecord>(
        File.ReadAllText(Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("The pinned local history fixture capability is empty.");
    var localCapability = sourceCapability with
    {
        Endpoint = fixture.BaseUrl,
        ModelIdentifier = "history-local-fixture-model"
    };
    var otherCapability = localCapability with { ProviderId = "openrouter", ModelIdentifier = "stealth/space-bunny-alpha" };
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
    try
    {
        var identity = RuntimeIdentity.LoadVerified(Path.Combine(sourceRoot, "runtime", "runtime-lock.json"));
        CodexConfigBuilder.WriteIsolated(layout.CodexHome, otherCapability,
            deterministicOpenRouterMock: true, toolExecutionPolicy: ToolExecutionPolicy.Unrestricted);
        string otherProviderId;
        string branchId;
        string foreignThreadId;
        await using (var client = AppServerClient.Start(new AppServerLaunchOptions(
                         identity.BinaryPath, layout.FixtureWorkspace, layout.CodexHome, null,
                         OpenRouterApiKey: "synthetic-history-key")))
        {
            var initialized = await client.InitializeAsync(timeout.Token);
            AppServerLaunchEnvironment.RequireCodexHome(initialized, layout.CodexHome);
            async Task<string> PersistThreadAsync(string workspace)
            {
                var started = await client.RequestResultAsync(
                    id => AppServerProtocol.BuildThreadStartRequest(id, new ThreadStartOptions(
                        otherCapability.ModelIdentifier, otherCapability.ProviderId, workspace,
                        "danger-full-access", "never", CapabilityAdapter.ToCodexConfig(otherCapability))), timeout.Token);
                var threadId = started["thread"]?["id"]?.GetValue<string>()
                    ?? throw new InvalidDataException("The pinned history fixture did not create an exact thread id.");
                var turn = await client.RequestResultAsync(id => AppServerProtocol.BuildTurnStartRequest(id,
                    new TurnStartOptions(threadId, "Persist deterministic history.", ToolExecutionPolicy.Unrestricted)), timeout.Token);
                var turnId = turn["turn"]?["id"]?.GetValue<string>()
                    ?? throw new InvalidDataException("The history fixture turn did not expose an id.");
                var observation = await client.WaitForTurnCompletionAsync(
                    TimeSpan.FromSeconds(30), timeout.Token, threadId, turnId);
                Assert(observation.Completed, "the deterministic history fixture turn did not complete");
                return threadId;
            }

            otherProviderId = await PersistThreadAsync(layout.FixtureWorkspace);
            var forked = await client.RequestResultAsync(
                id => AppServerProtocol.BuildThreadForkRequest(id, otherProviderId, ToolExecutionPolicy.Unrestricted), timeout.Token);
            branchId = forked["thread"]?["id"]?.GetValue<string>()
                ?? throw new InvalidDataException("The pinned history fixture fork did not expose an id.");
            var bookmark = new ForkBookmark(branchId, otherProviderId, otherCapability.ProviderId,
                otherCapability.ModelIdentifier, layout.FixtureWorkspace, DateTimeOffset.UtcNow);
            var branchRead = await client.RequestResultAsync(
                id => AppServerProtocol.BuildThreadReadRequest(id, branchId, includeTurns: false), timeout.Token);
            ForkBookmarkIdentity.RequireExactMatch(branchRead["thread"] as JsonObject
                ?? throw new InvalidDataException("The fork read did not expose native thread metadata."), bookmark);
            ForkBookmarkStore.Record(Path.Combine(layout.DataRoot, "NeoBabylon", "fork-bookmarks.json"), bookmark);
            foreignThreadId = await PersistThreadAsync(foreignWorkspace);
        }

        await using var supervisor = new RuntimeSupervisor(sourceRoot, applicationRoot);
        var localStarted = await supervisor.StartThreadAsync(localCapability, timeout.Token);
        var localThreadId = localStarted["thread"]?["thread"]?["id"]?.GetValue<string>()
            ?? localStarted["thread"]?["id"]?.GetValue<string>()
            ?? throw new InvalidDataException("The local history fixture did not create an exact thread id.");
        var localTurn = await supervisor.StartTurnAsync("Persist the current-provider history fixture.", timeout.Token);
        Assert(localTurn["completed"]?.GetValue<bool>() == true, "the local history fixture turn did not complete");
        var listed = await supervisor.ListThreadsAsync(localCapability, null, timeout.Token);
        var rows = listed["threads"] as JsonArray ?? throw new InvalidDataException("The history listing was missing.");
        var ids = rows.OfType<JsonObject>().Select(row => row["id"]?.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        Assert(ids.Contains(otherProviderId) && ids.Contains(branchId) && ids.Contains(localThreadId)
            && !ids.Contains(foreignThreadId),
            "mixed-provider history or a verified branch disappeared, or foreign workspace history became visible");
        Assert(rows.OfType<JsonObject>().Single(row => row["id"]?.GetValue<string>() == otherProviderId)
                ["modelProvider"]?.GetValue<string>() == "openrouter"
            && listed["historyQuery"]?["verifiedForkBookmarkCount"]?.GetValue<int>() == 1,
            "cross-provider native metadata or verified bookmark supplementation was lost");
        var status = supervisor.GetRuntimeStatus();
        Assert(status["currentThreadId"]?.GetValue<string>() == localThreadId
            && status["currentProviderId"]?.GetValue<string>() == localCapability.ProviderId
            && status["currentModelIdentifier"]?.GetValue<string>() == localCapability.ModelIdentifier,
            "history listing silently selected another thread or provider tuple");
        Console.WriteLine("CROSS_PROVIDER_THREAD_HISTORY=PASS");
        Console.WriteLine("HISTORY_FIXTURE=two providers, verified cross-provider branch, foreign workspace excluded, selection unchanged");
    }
    finally
    {
        var exactRoot = Path.GetFullPath(fixtureRoot);
        if (!exactRoot.StartsWith(runsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("History fixture cleanup escaped its isolated run directory.");
        if (Directory.Exists(exactRoot))
        {
            foreach (var file in Directory.EnumerateFiles(exactRoot, "*", SearchOption.AllDirectories))
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
            Directory.Delete(exactRoot, recursive: true);
        }
    }
}

static async Task RunProviderCredentialAndNvidiaConfigChecksAsync()
{
    var previousOpenRouter = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY", EnvironmentVariableTarget.Process);
    var previousNvidia = Environment.GetEnvironmentVariable("NVIDIA_API_KEY", EnvironmentVariableTarget.Process);
    var previousOpenAi = Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.Process);
    var previousAccessToken = Environment.GetEnvironmentVariable("NEOBABYLON_TEST_ACCESS_TOKEN", EnvironmentVariableTarget.Process);
    try
    {
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "synthetic-or-key", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("NVIDIA_API_KEY", "synthetic-nvidia-key", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "synthetic-shared-key", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("NEOBABYLON_TEST_ACCESS_TOKEN", "synthetic-token", EnvironmentVariableTarget.Process);
        var captured = AppServerLaunchEnvironment.CaptureProviderCredentialsAndClear();
        Assert(captured.GetCredential(ProviderCredentialProvider.OpenRouter) == "synthetic-or-key", "startup did not capture OpenRouter");
        Assert(captured.GetCredential(ProviderCredentialProvider.Nvidia) == "synthetic-nvidia-key", "startup did not capture NVIDIA");
        Assert(!JsonSerializer.Serialize(captured).Contains("synthetic-", StringComparison.Ordinal), "opaque credential state serialized secret data");
        Assert(Environment.GetEnvironmentVariable("OPENROUTER_API_KEY", EnvironmentVariableTarget.Process) is null
            && Environment.GetEnvironmentVariable("NVIDIA_API_KEY", EnvironmentVariableTarget.Process) is null
            && Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.Process) is null
            && Environment.GetEnvironmentVariable("NEOBABYLON_TEST_ACCESS_TOKEN", EnvironmentVariableTarget.Process) is null,
            "a credential-like process environment variable survived startup capture");

        var sourceRoot = Directory.GetCurrentDirectory();
        var fixtureParent = Path.GetFullPath(Path.Combine(sourceRoot, ".local", "Lab", "Runs"));
        var fixtureRoot = Path.Combine(fixtureParent, "Nvidia-20261004-credential-gate-" + Guid.NewGuid().ToString("N"));
        var fixtureApp = Path.Combine(fixtureRoot, "App");
        var openRouterCapability = ModelCapabilityRecord.CreateUnknown(
            "openrouter", "https://openrouter.ai/api/v1", "stealth/space-bunny-alpha");
        try
        {
            await using (var supervisor = new RuntimeSupervisor(sourceRoot, fixtureApp,
                             providerCredentials: captured))
            {
                supervisor.RequireOpenRouterCredentialForTurnForApprovalQaTest(openRouterCapability);
            }

            Environment.SetEnvironmentVariable("NVIDIA_API_KEY", "synthetic-nvidia-only", EnvironmentVariableTarget.Process);
            var nvidiaOnly = AppServerLaunchEnvironment.CaptureProviderCredentialsAndClear();
            await using (var legacy = new RuntimeSupervisor(sourceRoot, fixtureApp,
                             openRouterApiKey: "synthetic-legacy-key", providerCredentials: nvidiaOnly))
            {
                legacy.RequireOpenRouterCredentialForTurnForApprovalQaTest(openRouterCapability);
            }
            await using (var missing = new RuntimeSupervisor(sourceRoot, fixtureApp,
                             providerCredentials: nvidiaOnly))
            {
                AssertThrows<InvalidOperationException>(() =>
                    missing.RequireOpenRouterCredentialForTurnForApprovalQaTest(openRouterCapability));
            }
        }
        finally
        {
            var exactRoot = Path.GetFullPath(fixtureRoot);
            if (!exactRoot.StartsWith(fixtureParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The credential-gate fixture cleanup escaped its isolated run directory.");
            if (Directory.Exists(exactRoot)) Directory.Delete(exactRoot, recursive: true);
        }

        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "synthetic-legacy-or", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("NVIDIA_API_KEY", "synthetic-legacy-nvidia", EnvironmentVariableTarget.Process);
        Assert(AppServerLaunchEnvironment.CaptureOpenRouterApiKeyAndClear() == "synthetic-legacy-or",
            "legacy OpenRouter capture changed its API contract");
        Assert(Environment.GetEnvironmentVariable("NVIDIA_API_KEY", EnvironmentVariableTarget.Process) is null,
            "legacy capture did not clear NVIDIA environment data");

        var childEnvironment = AppServerLaunchEnvironment.Build(new AppServerLaunchOptions(
            "C:\\fixture\\app-server.exe", "C:\\fixture", "C:\\fixture\\App\\Data\\CodexHome", null,
            NvidiaSessionToken: "synthetic-adapter-session"));
        Assert(childEnvironment["NEOBABYLON_PROVIDER_SESSION_TOKEN"] == "synthetic-adapter-session"
            && !childEnvironment.ContainsKey("NVIDIA_API_KEY")
            && !childEnvironment.ContainsKey("OPENROUTER_API_KEY"),
            "NVIDIA adapter handoff exposed a provider key or omitted its ephemeral token");

        const string canonicalEndpoint = "https://integrate.api.nvidia.com/v1";
        const string adapterEndpoint = "http://127.0.0.1:43127/v1";
        var record = ModelCapabilityRecord.CreateUnknown("nvidia", canonicalEndpoint, "z-ai/glm-5.3");
        var config = CodexConfigBuilder.Build(record, nvidiaAdapterEndpoint: adapterEndpoint);
        Assert(config.Contains("model = \"z-ai/glm-5.3\"", StringComparison.Ordinal)
            && config.Contains("[model_providers.nvidia]", StringComparison.Ordinal), "NVIDIA selected model/provider identity is missing");
        Assert(config.Contains($"base_url = \"{adapterEndpoint}\"", StringComparison.Ordinal)
            && !config.Contains(canonicalEndpoint, StringComparison.Ordinal), "NVIDIA config bypasses the trusted local adapter");
        Assert(config.Contains("wire_api = \"responses\"", StringComparison.Ordinal)
            && config.Contains("env_key = \"NEOBABYLON_PROVIDER_SESSION_TOKEN\"", StringComparison.Ordinal)
            && config.Contains("requires_openai_auth = false", StringComparison.Ordinal), "NVIDIA adapter auth/protocol config is incomplete");
        Assert(config.Contains("request_max_retries = 0", StringComparison.Ordinal)
            && config.Contains("stream_max_retries = 0", StringComparison.Ordinal)
            && config.Contains("supports_websockets = false", StringComparison.Ordinal), "NVIDIA retries/WebSockets were not fail-closed");
        Assert(config.IndexOf("web_search = \"disabled\"", StringComparison.Ordinal) >= 0
            && config.IndexOf("web_search = \"disabled\"", StringComparison.Ordinal)
                < config.IndexOf("[model_providers.nvidia]", StringComparison.Ordinal),
            "NVIDIA standalone provider-native web/search must be disabled at top level before provider tables");
        Assert(!CodexConfigBuilder.Build(ModelCapabilityRecord.CreateUnknown(
                "lmstudio", "http://127.0.0.1:1234/v1", "fixture-local-model"))
                .Contains("web_search = \"disabled\"", StringComparison.Ordinal),
            "NVIDIA adapter web/search restriction leaked into other provider config");
        Assert(config.Contains("exclude = [\"OPENROUTER_API_KEY\", \"NVIDIA_API_KEY\", \"NEOBABYLON_PROVIDER_SESSION_TOKEN\"]", StringComparison.Ordinal),
            "provider credentials or session token can reach child commands");
        AssertThrows<InvalidOperationException>(() => CodexConfigBuilder.Build(record));
        AssertThrows<InvalidOperationException>(() => CodexConfigBuilder.Build(record,
            nvidiaAdapterEndpoint: "https://integrate.api.nvidia.com/v1"));
        Console.WriteLine("PASS provider capture, captured/legacy/missing OpenRouter turn credential gate, credential exclusion, legacy API, and NVIDIA local adapter config");
    }
    finally
    {
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", previousOpenRouter, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("NVIDIA_API_KEY", previousNvidia, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", previousOpenAi, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("NEOBABYLON_TEST_ACCESS_TOKEN", previousAccessToken, EnvironmentVariableTarget.Process);
    }
}

static void RunDevelopmentCredentialRecordChecks()
{
    var repositoryRoot = Directory.GetCurrentDirectory();
    var parent = Path.GetFullPath(Path.Combine(repositoryRoot, ".local", "Lab", "Runs", "Nvidia-20261004"));
    Directory.CreateDirectory(parent);
    var testRoot = Path.Combine(parent, "credential-record-check-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(testRoot);
    try
    {
        static string QuotePowerShell(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
        var modulePath = Path.Combine(repositoryRoot, "scripts", "DevelopmentCredentialStore.psm1");
        var testScript = "$ErrorActionPreference='Stop'; Import-Module " + QuotePowerShell(modulePath) + " -Force; "
            + "$root=" + QuotePowerShell(testRoot) + "; "
            + "if ($null -ne (Read-DevelopmentCredential -Provider nvidia -CredentialRoot $root)) { throw 'missing record was not absent' }; "
            + "[IO.File]::WriteAllText((Join-Path $root 'nvidia.json'), '{\"schemaVersion\":1,\"provider\":\"nvidia\",\"protection\":\"WindowsCurrentUserDPAPI\",\"ciphertext\":\"invalid\"}'); "
            + "try { $null=Read-DevelopmentCredential -Provider nvidia -CredentialRoot $root; throw 'invalid record was accepted' } "
            + "catch { if ($_.Exception.Message -notlike '*invalid or cannot be decrypted*') { throw } }; "
            + "$value='synthetic-record-fixture'; $secure=ConvertTo-SecureString -String $value -AsPlainText -Force; "
            + "$cipher=ConvertFrom-SecureString -SecureString $secure; $secure.Dispose(); "
            + "$record=@{schemaVersion=1;provider='nvidia';protection='WindowsCurrentUserDPAPI';ciphertext=$cipher} | ConvertTo-Json -Compress; "
            + "[IO.File]::WriteAllText((Join-Path $root 'nvidia.json'), $record); "
            + "$actual=Read-DevelopmentCredential -Provider nvidia -CredentialRoot $root; "
            + "if ($actual -cne $value) { throw 'DPAPI record round trip failed' }; "
            + "$info=[Diagnostics.ProcessStartInfo]::new(); $info.Environment.Clear(); "
            + "$info.Environment['OPENROUTER_API_KEY']='synthetic'; $info.Environment['NVIDIA_API_KEY']='synthetic'; "
            + "$info.Environment['CUSTOM_SECRET_VALUE']='synthetic'; $info.Environment['ACCESS_TOKEN']='synthetic'; $info.Environment['PATH']='preserved'; "
            + "Remove-DevelopmentCredentialEnvironment $info; "
            + "if (@($info.Environment.Keys | Where-Object { $_ -match '(?i)(KEY|SECRET|TOKEN)' }).Count -ne 0 -or $info.Environment['PATH'] -cne 'preserved') { throw 'build child environment filtering failed' }; "
            + "'PASS missing, invalid, DPAPI record behavior, and build child credential exclusion'";
        var startInfo = new ProcessStartInfo("pwsh")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(testScript);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("PowerShell focused credential check did not start.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0 || !stdout.Contains("PASS missing, invalid, DPAPI record behavior, and build child credential exclusion", StringComparison.Ordinal))
        {
            _ = stderr;
            throw new InvalidOperationException("PowerShell focused credential-record check failed; raw process output is withheld.");
        }
        Console.WriteLine("PASS missing, invalid, DPAPI record behavior, and build child credential exclusion (synthetic value; output withheld)");
    }
    finally
    {
        var safePrefix = Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar;
        var fullTestRoot = Path.GetFullPath(testRoot);
        if (fullTestRoot.StartsWith(safePrefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullTestRoot))
        {
            Directory.Delete(fullTestRoot, recursive: true);
        }
    }
}

file sealed record StartupFailureSnapshot(
    string Phase,
    int ProcessId,
    long StartTimeUtcTicks,
    int? ExitCode,
    Task<string> StandardError);
