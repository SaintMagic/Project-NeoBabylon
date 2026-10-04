using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using NeoBabylon.Core;
using NeoBabylon.Host;

internal static class CapabilityVersionChecks
{
    public static async Task RunAsync()
    {
        var sourceRoot = Directory.GetCurrentDirectory();
        var releaseRoot = Path.Combine(sourceRoot, "docs", "release");
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ModelCapabilityRecord Read(string file) => JsonSerializer.Deserialize<ModelCapabilityRecord>(
            File.ReadAllText(Path.Combine(releaseRoot, file)), jsonOptions)!;
        var failures = new List<string>();
        void Check(bool value, string message) { if (!value) failures.Add(message); }
        void Exercise(Action action, string message)
        {
            try { action(); }
            catch (Exception error) { failures.Add(message + ": " + error.GetType().Name); }
        }
        var oldBunny = Read("MODEL_CAPABILITY_OPENROUTER_STEALTH_SPACE_BUNNY_ALPHA.json");
        var bunny = Read("MODEL_CAPABILITY_OPENROUTER_STEALTH_SPACE_BUNNY_ALPHA_20261004.json");
        var oldLing = Read("MODEL_CAPABILITY_OPENROUTER_LING_3_1_FLASH.json");
        var ling = Read("MODEL_CAPABILITY_OPENROUTER_LING_3_1_FLASH_20261004.json");
        Check(oldBunny.ReasoningControls.State == CapabilityState.Unknown && oldLing.ReasoningControls.State == CapabilityState.Unknown,
            "historical records were replaced by current observations");
        var retained = Directory.EnumerateFiles(releaseRoot, "MODEL_CAPABILITY_*.json")
            .Select(path => (Path: path, Record: Read(Path.GetFileName(path)))).ToArray();
        var active = retained.Where(item => ActiveCapabilitySelection.IsSelectable(item.Record, Path.GetFileName(item.Path))).ToArray();
        Check(active.Count(item => item.Record.ModelIdentifier == "stealth/space-bunny-alpha") == 1
            && active.SingleOrDefault(item => Path.GetFileName(item.Path) == "MODEL_CAPABILITY_OPENROUTER_STEALTH_SPACE_BUNNY_ALPHA_20261004.json").Record is not null,
            "fresh Space Bunny selection did not choose exactly the explicit current file");
        Check(active.Count(item => item.Record.ModelIdentifier == "inclusionai/ling-3.1-flash") == 1
            && active.SingleOrDefault(item => Path.GetFileName(item.Path) == "MODEL_CAPABILITY_OPENROUTER_LING_3_1_FLASH_20261004.json").Record is not null,
            "fresh Ling selection did not choose exactly the explicit current file");
        Check(active.All(item => item.Record.ModelIdentifier != "nex-agi/nex-n2.5-pro:free")
            && active.Count(item => item.Record.ProviderId == "nvidia") == 3
            && active.Count(item => item.Record.ProviderId == "lmstudio") == 1
            && active.Any(item => item.Record.ModelIdentifier == "nvidia/nemotron-3-ultra-550b-a55b:free"),
            "current version filtering changed unrelated models or revived NEX");
        Exercise(() =>
        {
            Check(CapabilitySwitchSafety.GetSelectedDefaultReasoningEffort(bunny) == "max", "current Space Bunny default was not exact max");
            foreach (var effort in new[] { "max", "xhigh", "high", "medium", "low" })
                Check(AppServerClient.BuildTurnStartRequest(1, new TurnStartOptions("version-thread", "fixture", ReasoningEffort: effort), bunny, null)
                    .Params["effort"]?.GetValue<string>() == effort, "current Space Bunny lost exact effort " + effort);
            Check(CodexModelCatalogBuilder.Build(bunny)["models"]?[0]?["supported_reasoning_levels"] is JsonArray { Count: 5 },
                "current Space Bunny catalog did not expose its five exact effort levels");
        }, "Space Bunny current controls failed");
        Exercise(() =>
        {
            Check(CapabilitySwitchSafety.GetSelectedDefaultReasoningEffort(ling) is null, "Boolean reasoning gained an invented default effort");
            var model = CodexModelCatalogBuilder.Build(ling)["models"]![0]!;
            Check(model["default_reasoning_level"] is null && model["supported_reasoning_levels"] is JsonArray { Count: 0 },
                "Known Boolean controls became speculative Codex effort levels");
            Check(AppServerProtocol.BuildThreadResumeRequest(1, "version-thread", ToolExecutionPolicy.Unrestricted, ling)
                .Params["config"]?["model_reasoning_effort"] is null, "Boolean resume sent a null/none/medium effort alias");
            Check(!AppServerClient.BuildTurnStartRequest(1, new TurnStartOptions("version-thread", "fixture"), ling, null).Params.ContainsKey("effort"),
                "Boolean omission emitted an effort override");
        }, "Known Boolean metadata was rejected instead of preserved");
        foreach (var effort in new[] { "none", "medium" })
        {
            try
            {
                AppServerClient.BuildTurnStartRequest(1, new TurnStartOptions("version-thread", "fixture", ReasoningEffort: effort), ling, null);
                failures.Add("Boolean reasoning accepted unqualified alias " + effort);
            }
            catch (InvalidDataException error)
            {
                Check(error.Message.Contains("reasoning.enabled", StringComparison.Ordinal)
                    && error.Message.Contains("turn/start", StringComparison.Ordinal), "Boolean override omitted the precise transport blocker");
            }
        }
        var appRoot = Path.Combine(sourceRoot, ".local", "Lab", "Runs", "Context-Reasoning-20261004-version-" + Guid.NewGuid().ToString("N"), "App");
        var workspace = ApplicationRootLayout.Create(sourceRoot, appRoot).FixtureWorkspace;
        await using (var supervisor = new RuntimeSupervisor(sourceRoot, appRoot))
        {
            var bindingRoot = (string)typeof(RuntimeSupervisor).GetField("_threadCapabilityBindingRoot", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(supervisor)!;
            const string threadId = "version-binding-thread";
            var original = ThreadCapabilityBindingStore.Bind(bindingRoot, threadId, workspace, oldBunny);
            var bindingFile = Directory.GetFiles(Path.Combine(bindingRoot, "NeoBabylon", "TaskBindings"), "*.json").Single();
            var originalBytes = File.ReadAllBytes(bindingFile);
            var resolve = typeof(RuntimeSupervisor).GetMethod("ResolveCapabilityBinding", BindingFlags.Instance | BindingFlags.NonPublic)!;
            (string? BlockReason, ModelCapabilityRecord BoundCapability) Resolve(ModelCapabilityRecord selected) =>
                ((string?, ModelCapabilityRecord))resolve.Invoke(supervisor, [threadId, selected, workspace])!;
            var historical = Resolve(bunny);
            Check(historical.BlockReason is null && CapabilityRecordIdentity.Compute(historical.BoundCapability) == original.CapabilityIdentity,
                "opening an old conversation did not resolve its exact retained original version");
            Check(File.ReadAllBytes(bindingFile).AsSpan().SequenceEqual(originalBytes), "passive historical resolution changed the binding");
            ThreadCapabilityBindingStore.RecordExplicitSwitch(bindingRoot, threadId, workspace, bunny);
            var switchedBytes = File.ReadAllBytes(bindingFile);
            var latest = Resolve(oldBunny);
            Check(latest.BlockReason is null && CapabilityRecordIdentity.Compute(latest.BoundCapability) == CapabilityRecordIdentity.Compute(bunny),
                "passive resume fell back to original instead of latest explicit version hash");
            Check(File.ReadAllBytes(bindingFile).AsSpan().SequenceEqual(switchedBytes), "passive latest-version resolution appended/replaced a switch");
            Check(Resolve(bunny).BlockReason is null && ThreadCapabilityBindingStore.Read(bindingRoot, threadId) == original,
                "explicit same-tuple adoption rewrote the immutable original binding");
            var missing = bunny with { ModelVariant = "fixture-missing-retained-version" };
            ThreadCapabilityBindingStore.RecordExplicitSwitch(bindingRoot, threadId, workspace, missing);
            Check(Resolve(oldBunny).BlockReason == "capabilityRecordChanged", "missing latest hash silently authorized the original historical version");
            Check(((ValueTuple<string?, ModelCapabilityRecord>)resolve.Invoke(supervisor, [threadId, bunny, workspace + "-other"])!).Item1 is not null,
                "historical hash resolution weakened workspace ownership");
        }
        if (failures.Count != 0) throw new InvalidOperationException(string.Join("\n", failures));

        // The pinned runtime persists a rollout after its first turn, not thread/start.
        // Seed exactly one deterministic local response, then perform metadata/resume
        // only. No live provider, key, generated tool or compaction is involved.
        var runtimeApp = Path.Combine(sourceRoot, ".local", "Lab", "Runs", "Context-Reasoning-20261004-adoption-" + Guid.NewGuid().ToString("N"), "App");
        await using var providerFixture = await UnrestrictedResponsesFixture.StartAsync(responseSequence:
            [ResponsesSse.AssistantMessage("version-response", "version-message", "Synthetic persisted version history.")]);
        var originalLocal = Read("MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json") with { Endpoint = providerFixture.BaseUrl };
        var nextLocal = originalLocal with { ModelVariant = "isolated-explicit-version-adoption" };
        await using (var supervisor = new RuntimeSupervisor(sourceRoot, runtimeApp))
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var started = await supervisor.StartThreadAsync(originalLocal, timeout.Token);
            var threadId = started["thread"]?["thread"]?["id"]?.GetValue<string>() ?? started["thread"]?["id"]?.GetValue<string>()
                ?? throw new InvalidDataException("Pinned fixture did not return a thread id.");
            var seed = await supervisor.StartTurnAsync("Persist the deterministic local version fixture.", timeout.Token);
            Check(seed["completed"]?.GetValue<bool>() == true, "local fixture seed did not persist a terminal turn");
            var bindingRoot = (string)typeof(RuntimeSupervisor).GetField("_threadCapabilityBindingRoot", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(supervisor)!;
            var originalBinding = ThreadCapabilityBindingStore.Read(bindingRoot, threadId);
            var bindingFile = Directory.GetFiles(Path.Combine(bindingRoot, "NeoBabylon", "TaskBindings"), "*.json").Single();
            var switched = await supervisor.SwitchCapabilityForCurrentThreadAsync(nextLocal, threadId, timeout.Token);
            Check(switched["threadId"]?.GetValue<string>() == threadId && switched["threadPreserved"]?.GetValue<bool>() == true,
                "verified same-tuple adoption created a new chat");
            Check(switched["turns"] is JsonArray { Count: > 0 }, "same-tuple adoption lost persisted transcript/history");
            Check(ThreadCapabilityBindingStore.Read(bindingRoot, threadId) == originalBinding
                && ThreadCapabilityBindingStore.ReadCurrentBinding(bindingRoot, threadId)?.CapabilityIdentity == CapabilityRecordIdentity.Compute(nextLocal),
                "verified same-tuple adoption did not append only the latest exact identity");
            var beforeRejected = File.ReadAllBytes(bindingFile);
            try
            {
                await supervisor.SwitchCapabilityForCurrentThreadAsync(originalLocal, "foreign-thread", timeout.Token);
                failures.Add("stale-thread adoption was accepted");
            }
            catch (InvalidOperationException) { }
            Check(File.ReadAllBytes(bindingFile).AsSpan().SequenceEqual(beforeRejected), "failed adoption changed binding history");
            var resumed = await supervisor.ResumeThreadAsync(nextLocal, threadId, timeout.Token);
            Check(resumed["threadId"]?.GetValue<string>() == threadId
                && resumed["capabilityIdentity"]?.GetValue<string>() == CapabilityRecordIdentity.Compute(nextLocal)
                && resumed["capabilityRecord"]?["modelVariant"]?.GetValue<string>() == "isolated-explicit-version-adoption",
                "passive resume receipt lost the exact historical capability version");
            Check(File.ReadAllBytes(bindingFile).AsSpan().SequenceEqual(beforeRejected), "opening after adoption changed binding history");
            Check(providerFixture.RequestBodies.Count == 1, "metadata adoption/resume unexpectedly called a provider");
        }
        if (failures.Count != 0) throw new InvalidOperationException(string.Join("\n", failures));
        Console.WriteLine("CAPABILITY_VERSIONS=PASS");
        Console.WriteLine("CAPABILITY_VERSION_FIXTURE=" + appRoot);
        Console.WriteLine("CAPABILITY_ADOPTION_FIXTURE=" + runtimeApp);
    }
}
