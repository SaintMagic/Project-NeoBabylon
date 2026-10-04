using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.Reflection;
using NeoBabylon.Core;
using NeoBabylon.Host;

internal static class ContextReasoningChecks
{
    private static int _fixtureCompactionDispatches;
    public static async Task RunAsync()
    {
        var failures = new List<string>();
        void Check(bool condition, string message) { if (!condition) failures.Add(message); }
        void Rejects(Action action, string message)
        {
            try { action(); failures.Add(message); }
            catch (Exception error) when (error is InvalidDataException or InvalidOperationException) { }
            catch (Exception error) { failures.Add(message + " (wrong exception " + error.GetType().Name + ")"); }
        }
        var known = ModelCapabilityRecord.CreateUnknown("nvidia", "https://integrate.api.nvidia.com/v1", "z-ai/glm-5.3") with
        {
            ReasoningControls = new CapabilityObservation(CapabilityState.Known, new JsonObject
            {
                ["supported_efforts"] = new JsonArray("low", "high", "max"), ["default_effort"] = "max"
            }, "synthetic exact supported effort fixture", null)
        };
        foreach (var effort in new[] { "low", "high", "max" })
        {
            var options = JsonSerializer.Deserialize<TurnStartOptions>(
                "{\"threadId\":\"thread-fixture\",\"text\":\"fixture\",\"reasoningEffort\":\"" + effort + "\"}",
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            var request = AppServerClient.BuildTurnStartRequest(1, options, known, null);
            Check(request.Method == "turn/start" && request.Params["effort"]?.GetValue<string>() == effort,
                "exact supported effort was lost on the wire: " + effort);
        }
        foreach (var value in new[] { "null", "3", "true", "{}", "\"bogus\"", "\"HIGH\"" })
            Rejects(() => DiagnosticOperation.Parse("{\"operation\":\"startTurn\",\"requestId\":\"fixture\",\"text\":\"fixture\",\"reasoningEffort\":" + value + "}"), "invalid effort JSON was accepted: " + value);
        Rejects(() => DiagnosticOperation.Parse("{\"operation\":\"startTurn\",\"requestId\":\"fixture\",\"text\":\"fixture\",\"reasoningEffort\":\"low\",\"reasoningEffort\":\"high\"}"), "duplicate effort was accepted");
        Rejects(() => AppServerClient.BuildTurnStartRequest(1, new TurnStartOptions("thread-fixture", "fixture", ReasoningEffort: "medium"), known, null), "unadvertised effort was accepted");
        Rejects(() => AppServerClient.BuildTurnStartRequest(1, new TurnStartOptions("thread-fixture", "fixture", ReasoningEffort: "low"), known with { ReasoningControls = CapabilityObservation.Unknown("fixture") }, null), "Unknown reasoning allowed an override");
        Check(!AppServerClient.BuildTurnStartRequest(1, new TurnStartOptions("thread-fixture", "fixture"), known, null).Params.ContainsKey("effort"), "omission became a null/default effort override");
        var kimi = known with { ReasoningControls = new CapabilityObservation(CapabilityState.Known, new JsonObject { ["supported_efforts"] = new JsonArray("low", "high", "max"), ["default_effort"] = null }, "fixture", null) };
        Check(CapabilitySwitchSafety.GetSelectedDefaultReasoningEffort(kimi) is null, "Kimi gained an invented default");
        Check(CapabilitySwitchSafety.GetSelectedDefaultReasoningEffort(known) == "max", "known GLM default changed");
        foreach (var operation in new[] { "getThreadUsage", "compactContext" })
        {
            Check(AppServerProtocol.IsNamedHostOperation(operation), operation + " named route is missing");
            try { DiagnosticOperation.Parse("{\"operation\":\"" + operation + "\",\"requestId\":\"fixture\",\"threadId\":\"thread-fixture\"}"); }
            catch (Exception error) { failures.Add(operation + " valid payload rejected: " + error.GetType().Name); }
            Rejects(() => DiagnosticOperation.Parse("{\"operation\":\"" + operation + "\",\"requestId\":\"fixture\",\"threadId\":\"thread-fixture\",\"path\":\"arbitrary\"}"), operation + " accepted arbitrary path");
            Rejects(() => DiagnosticOperation.Parse("{\"operation\":\"" + operation + "\",\"requestId\":\"fixture\",\"threadId\":null}"), operation + " accepted null identity");
            Rejects(() => DiagnosticOperation.Parse("{\"operation\":\"" + operation + "\",\"requestId\":\"fixture\",\"threadId\":\"thread-fixture\",\"threadId\":\"foreign\"}"), operation + " accepted duplicate identity");
        }
        var compactRequest = AppServerProtocol.BuildThreadCompactStartRequest(2, "thread-fixture");
        Check(compactRequest.Method == "thread/compact/start" && compactRequest.Params.Count == 1
            && compactRequest.Params["threadId"]?.GetValue<string>() == "thread-fixture", "compact wire method/params diverged from stable protocol");
        Check(!compactRequest.ToJsonLine().Contains("experimental", StringComparison.Ordinal), "compact enabled experimental API");
        Rejects(() => CapabilitySwitchSafety.RequireManualCompactionEligible("stale", "thread-fixture", true, false), "stale compaction identity accepted");
        Rejects(() => CapabilitySwitchSafety.RequireManualCompactionEligible("thread-fixture", "thread-fixture", true, true), "busy compaction accepted");
        Rejects(() => CapabilitySwitchSafety.RequireManualCompactionEligible("thread-fixture", "thread-fixture", false, false), "unverified client accepted");
        Rejects(() => CapabilitySwitchSafety.RequireNoActiveContinuingCommands(new JsonObject { ["data"] = new JsonArray(new JsonObject()) }), "continuing command accepted");
        CapabilitySwitchSafety.RequireNoActiveContinuingCommands(new JsonObject { ["data"] = new JsonArray() });
        var tracker = new ManualCompactionTracker("thread-fixture");
        Check(!tracker.Finished && tracker.Receipt()["succeeded"] is null, "queued/empty compact reply became success");
        tracker.Observe(Notification("turn/completed", "foreign", "turn-fixture", status: "completed"));
        Check(!tracker.Finished, "wrong-thread terminal completed compaction");
        tracker.Observe(Notification("turn/started", "thread-fixture", "turn-fixture"));
        tracker.Observe(Notification("item/started", "thread-fixture", "turn-fixture", item: true));
        tracker.Observe(Notification("item/completed", "thread-fixture", "turn-fixture", item: true));
        Check(!tracker.Finished, "item completion alone completed compaction");
        tracker.Observe(Notification("turn/completed", "thread-fixture", "turn-fixture", status: "completed"));
        Check(tracker.Finished && tracker.Receipt()["succeeded"]?.GetValue<bool>() == true && tracker.TurnId == "turn-fixture", "correlated compaction completion missing");
        var failed = new ManualCompactionTracker("thread-fixture");
        failed.Observe(Notification("turn/started", "thread-fixture", "failed-turn"));
        failed.Observe(Notification("turn/completed", "thread-fixture", "failed-turn", status: "failed"));
        Check(failed.Finished && failed.Receipt()["succeeded"]?.GetValue<bool>() == false, "terminal compaction failure missing");
        var unproven = new ManualCompactionTracker("thread-fixture");
        unproven.Observe(Notification("turn/started", "thread-fixture", "unproven-turn"));
        unproven.Observe(Notification("turn/completed", "thread-fixture", "unproven-turn", status: "completed"));
        Check(unproven.Receipt()["succeeded"] is null, "ordinary terminal completion became proven compaction success");
        var timedOut = new ManualCompactionTracker("thread-fixture").FinishUnknown("timeout");
        Check(timedOut["outcome"]?.GetValue<string>() == "unknown" && timedOut["succeeded"] is null && timedOut["failure"]?["type"]?.GetValue<string>() == "timeout", "timeout was not typed Unknown");
        var identityLost = new ManualCompactionTracker("thread-fixture");
        identityLost.Observe(Notification("turn/started", "thread-fixture", "first-turn"));
        identityLost.Observe(Notification("turn/started", "thread-fixture", "another-turn"));
        Check(identityLost.Receipt()["outcome"]?.GetValue<string>() == "unknown" && identityLost.Receipt()["failure"]?["type"]?.GetValue<string>() == "identityLost", "changed compaction turn identity was accepted");

        var root = Path.Combine(Directory.GetCurrentDirectory(), ".local", "Lab", "Runs", "Context-Reasoning-20261004", "fixture-" + Guid.NewGuid().ToString("N"), "App", "Data", "CodexHome");
        var sessions = Path.Combine(root, "sessions");
        Directory.CreateDirectory(sessions);
        var path = Path.Combine(sessions, "fixture.jsonl");
        var workspace = Path.Combine(root, "Workspace");
        var meta = new JsonObject { ["type"] = "session_meta", ["payload"] = new JsonObject { ["id"] = "thread-fixture", ["cwd"] = workspace } }.ToJsonString();
        var context = new JsonObject { ["type"] = "turn_context", ["payload"] = new JsonObject { ["model"] = known.ModelIdentifier, ["turn_id"] = "turn-fixture", ["cwd"] = workspace, ["effort"] = "high" } }.ToJsonString();
        const string tokenRecord = "{\"timestamp\":\"2026-10-04T08:00:00Z\",\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{\"total_token_usage\":{\"total_tokens\":1000,\"input_tokens\":800,\"cached_input_tokens\":500,\"cache_write_input_tokens\":20,\"output_tokens\":200,\"reasoning_output_tokens\":40},\"last_token_usage\":{\"total_tokens\":100,\"input_tokens\":80,\"cached_input_tokens\":50,\"output_tokens\":20,\"reasoning_output_tokens\":4},\"model_context_window\":262144}}}";
        File.WriteAllText(path, meta + "\n" + context + "\n" + tokenRecord + "\n");
        var usage = ThreadUsageEvidence.ReadJournal(path, root, "thread-fixture", workspace, known.ModelIdentifier);
        Check(usage.TokenUsage?["total"]?["totalTokens"]?.GetValue<long>() == 1000
            && usage.TokenUsage?["last"]?["totalTokens"]?.GetValue<long>() == 100, "runtime token_count total/last mapping missing");
        Check(usage.TokenUsage?["modelContextWindow"]?.GetValue<long>() == 262144 && !usage.ToResult("thread-fixture").ContainsKey("contextUsed"), "cumulative usage was fabricated as current-context usage");
        Check(usage.TokenUsage?["last"]?["cacheWriteInputTokens"] is null
            && usage.TokenUsage?["last"]?["inputTokens"]?.GetValue<long>() == 80
            && usage.TokenUsage?["total"]?["cacheWriteInputTokens"]?.GetValue<long>() == 20,
            "absent cache-write input fabricated zero or discarded other known counters");
        var explicitZero = JsonNode.Parse(tokenRecord)!;
        explicitZero["payload"]!["info"]!["last_token_usage"]!["cache_write_input_tokens"] = 0;
        File.WriteAllText(path, meta + "\n" + context + "\n" + explicitZero.ToJsonString() + "\n");
        Check(ThreadUsageEvidence.ReadJournal(path, root, "thread-fixture", workspace, known.ModelIdentifier)
            .TokenUsage?["last"]?["cacheWriteInputTokens"]?.GetValue<long>() == 0, "explicit runtime cache-write zero became Unknown");
        File.WriteAllText(path, meta + "\n" + context + "\n" + tokenRecord + "\n");
        Check(usage.ReasoningEffort == "high" && usage.ReasoningEvidenceSource is not null, "observed journal effort missing");
        Check(ThreadUsageEvidence.ReadJournal(path, root, "wrong-thread", workspace, known.ModelIdentifier).TokenUsage is null, "foreign-thread journal accepted");
        Check(ThreadUsageEvidence.ReadJournal(path, root, "thread-fixture", workspace + "-foreign", known.ModelIdentifier).TokenUsage is null, "foreign-workspace journal accepted");
        var switched = ThreadUsageEvidence.ReadJournal(path, root, "thread-fixture", workspace, "different-model");
        Check(switched.ReasoningEffort is null && switched.TokenUsage?["modelContextWindow"] is null
            && switched.UsageModelIdentifier == "z-ai/glm-5.3" && switched.UsageTurnId == "turn-fixture"
            && switched.UsageModelProvider is null && switched.TokenUsage?["total"]?["totalTokens"]?.GetValue<long>() == 1000,
            "old-model usage was relabeled as the new model/context or lost its real provenance");
        Check(ThreadUsageEvidence.ReadJournal(path, root, "thread-fixture", workspace, known.ModelIdentifier, new FileInfo(path).Length).ReasoningEffort is null, "pre-selection effort inherited");
        Check(ThreadUsageEvidence.ReadJournal(path, root, "thread-fixture", workspace, known.ModelIdentifier, new FileInfo(path).Length).TokenUsage?["modelContextWindow"] is null, "pre-selection context window inherited");
        var noEffortContext = JsonNode.Parse(context)!.AsObject();
        noEffortContext["payload"]!["effort"] = null;
        File.WriteAllText(path, meta + "\n" + noEffortContext.ToJsonString() + "\n" + tokenRecord + "\n");
        var noObservedEffort = ThreadUsageEvidence.ReadJournal(path, root, "thread-fixture", workspace, known.ModelIdentifier);
        Check(noObservedEffort.ReasoningEffort is null && noObservedEffort.ReasoningEvidenceSource is not null,
            "fresh observed no-effort context did not supersede older resume effort evidence");
        Check(ThreadUsageEvidence.ReadJournal(Path.Combine(sessions, "missing.jsonl"), root, "thread-fixture", workspace, known.ModelIdentifier).TokenUsage is null, "missing usage invented");
        Check(ThreadUsageEvidence.ReadJournal(path, Path.Combine(root, "foreign-home"), "thread-fixture", workspace, known.ModelIdentifier).TokenUsage is null, "journal path escape accepted");
        File.AppendAllText(path, "{malformed\n");
        Check(ThreadUsageEvidence.ReadJournal(path, root, "thread-fixture", workspace, known.ModelIdentifier).TokenUsage is null, "malformed latest evidence became known usage");
        var padding = new JsonObject { ["type"] = "response_item", ["payload"] = new JsonObject { ["text"] = new string('x', ThreadUsageEvidence.MaximumReadBytes + 1024) } }.ToJsonString();
        var beforeLatestContext = meta + "\n" + padding + "\n";
        var absoluteContextOffset = Encoding.UTF8.GetByteCount(beforeLatestContext);
        File.WriteAllText(path, beforeLatestContext + context + "\n" + tokenRecord + "\n");
        var large = ThreadUsageEvidence.ReadJournal(path, root, "thread-fixture", workspace, known.ModelIdentifier, absoluteContextOffset);
        Check(large.TokenUsage?["total"]?["totalTokens"]?.GetValue<long>() == 1000
            && large.TokenUsage?["modelContextWindow"]?.GetValue<long>() == 262144
            && large.ReasoningEffort == "high" && large.UsageTurnId == "turn-fixture",
            "valid long journal lost bounded-tail counters/context or rebased absolute context offsets");
        Check(ThreadUsageEvidence.ReadJournal(path, root, "wrong-thread", workspace, known.ModelIdentifier).TokenUsage is null,
            "long-journal tail bypassed exact initial session identity");
        File.AppendAllText(path, "{malformed\n");
        Check(ThreadUsageEvidence.ReadJournal(path, root, "thread-fixture", workspace, known.ModelIdentifier).TokenUsage is null,
            "malformed bounded journal tail became known usage");
        File.WriteAllText(path, meta + "\n" + context + "\n" + padding + "\n" + tokenRecord + "\n");
        var missingContextTail = ThreadUsageEvidence.ReadJournal(path, root, "thread-fixture", workspace, known.ModelIdentifier);
        Check(missingContextTail.TokenUsage?["total"]?["totalTokens"]?.GetValue<long>() == 1000
            && missingContextTail.TokenUsage?["modelContextWindow"] is null && missingContextTail.ReasoningEffort is null
            && missingContextTail.UsageModelIdentifier is null && missingContextTail.UsageTurnId is null
            && missingContextTail.ReasoningEvidenceSource is not null,
            "context-free tail lost attributable lifetime totals or invented effective model/context/effort");
        using (var oversized = File.Open(path, FileMode.Create)) oversized.SetLength(ThreadUsageEvidence.MaximumReadBytes + 1L);
        Check(ThreadUsageEvidence.ReadJournal(path, root, "thread-fixture", workspace, known.ModelIdentifier).TokenUsage is null, "invalid oversized header accepted");
        var executable = Path.Combine(AppContext.BaseDirectory, "NeoBabylon.Phase1A.Tests.exe");
        foreach (var mode in new[] { "completed", "failed", "queued", "transport" })
        {
            var fixtureThread = "compact-fixture-" + mode;
            await using var client = AppServerClient.Start(new(executable, Path.Combine(root, mode), Path.Combine(root, mode, "CodexHome"), null));
            await client.InitializeAsync();
            var observed = new List<AppServerNotification>();
            var completion = await client.CompactContextAsync(fixtureThread, TimeSpan.FromMilliseconds(250), CancellationToken.None, observed.Add);
            var receipt = completion.Receipt();
            Check(receipt["outcome"]?.GetValue<string>() == (mode == "completed" ? "completed" : mode == "failed" ? "failed" : "unknown"), "actual client compact wire fixture outcome wrong: " + mode);
            if (mode == "completed") Check(observed.Any(notification => notification.Method == "item/completed") && completion.HasConfirmedTerminal, "compact stream/terminal confirmation missing");
            if (mode == "queued") Check(receipt["succeeded"] is null && receipt["failure"]?["type"]?.GetValue<string>() == "timeout", "actual empty reply became completion");
            if (mode == "transport") Check(receipt["succeeded"] is null && receipt["failure"]?["type"]?.GetValue<string>() == "transportFailure", "actual transport loss became success/failure certainty");
        }
        await using (var backlogClient = AppServerClient.Start(new(executable, Path.Combine(root, "backlog"), Path.Combine(root, "backlog", "CodexHome"), null)))
        {
            await backlogClient.InitializeAsync();
            var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            backlogClient.NotificationObserved += notification =>
            {
                if (notification.Method == "turn/started") published.TrySetResult();
            };
            await backlogClient.RequestResultAsync(id => new AppServerRequest(id, "fixture/compactBacklog",
                new JsonObject { ["threadId"] = "compact-fixture-backlog" }));
            await published.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var observation = await backlogClient.CompactContextAsync("compact-fixture-backlog", TimeSpan.FromSeconds(2), CancellationToken.None);
            var receipt = observation.Receipt();
            Check(receipt["outcome"]?.GetValue<string>() == "unknown" && receipt["succeeded"] is null
                && receipt["requestSent"]?.GetValue<bool>() == false
                && receipt["failure"]?["type"]?.GetValue<string>() == "hostNotificationBacklog"
                && receipt["failure"]?["attributedTo"]?.GetValue<string>() == "NeoBabylon.Host"
                && !observation.HasConfirmedTerminal,
                "saturated pre-dispatch backlog was attributed to Codex or treated as a sent/completed compaction");
            var dispatchCount = await backlogClient.RequestResultAsync(id => new AppServerRequest(id, "fixture/compactDispatchCount", new JsonObject()));
            Check(dispatchCount["count"]?.GetValue<int>() == 0, "compaction RPC dispatched despite unverified residual backlog");
            var reservationApp = Path.Combine(Directory.GetCurrentDirectory(), ".local", "Lab", "Runs",
                "Context-Reasoning-20261004-backlog-" + Guid.NewGuid().ToString("N"), "App");
            await using var reservationHost = new RuntimeSupervisor(Directory.GetCurrentDirectory(), reservationApp);
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            var epoch = Guid.NewGuid();
            var ready = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            typeof(RuntimeSupervisor).GetField("_client", privateInstance)!.SetValue(reservationHost, backlogClient);
            typeof(RuntimeSupervisor).GetField("_clientEpoch", privateInstance)!.SetValue(reservationHost, epoch);
            typeof(RuntimeSupervisor).GetField("_threadId", privateInstance)!.SetValue(reservationHost, "compact-fixture-backlog");
            typeof(RuntimeSupervisor).GetField("_activeTurnReady", privateInstance)!.SetValue(reservationHost, ready);
            var finish = typeof(RuntimeSupervisor).GetMethod("FinishCompactionObservation", privateInstance);
            if (finish is not null)
            {
                var waitingInterrupt = ready.Task.WaitAsync(TimeSpan.FromMilliseconds(250));
                var hostReceipt = (JsonObject)finish.Invoke(reservationHost, [backlogClient, epoch, "compact-fixture-backlog", ready, observation])!;
                Check(hostReceipt["requestSent"]?.GetValue<bool>() == false, "Host lost the verified no-dispatch compaction receipt");
                try { Check(await waitingInterrupt is null, "pre-start rejected compaction gave a waiting interrupt a fabricated turn id"); }
                catch (TimeoutException) { failures.Add("pre-start rejected compaction left a waiting interrupt's ready promise unresolved"); }
                try { await reservationHost.NewTaskAsync(CancellationToken.None); }
                catch (InvalidOperationException) { failures.Add("unsent backlog rejection left the Host operation guard reserved"); }
            }
            else failures.Add("Host does not release a verified unsent compaction reservation");
        }
        var guardApp = Path.Combine(Directory.GetCurrentDirectory(), ".local", "Lab", "Runs",
            "Context-Reasoning-20261004-host-" + Guid.NewGuid().ToString("N"), "App");
        await using (var supervisor = new RuntimeSupervisor(Directory.GetCurrentDirectory(), guardApp))
        {
            Rejects(() => supervisor.GetThreadUsage("stale-thread"), "host usage accepted an unselected identity");
            try { await supervisor.CompactContextAsync("stale-thread", CancellationToken.None); failures.Add("host compact accepted an unselected identity"); }
            catch (InvalidOperationException) { }
            try { await supervisor.StartTurnAsync("synthetic prompt", CancellationToken.None, reasoningEffort: "high"); failures.Add("host turn accepted an unselected capability"); }
            catch (InvalidOperationException) { }
        }
        if (failures.Count != 0) throw new InvalidOperationException(string.Join("\n", failures));
        Console.WriteLine("CONTEXT_REASONING_HOST=PASS");
        Console.WriteLine("CONTEXT_REASONING_FIXTURE=" + root);
    }

    public static async Task<bool> WriteCompactFixtureAsync(JsonObject request)
    {
        _fixtureCompactionDispatches++;
        var thread = request["params"]?["threadId"]?.GetValue<string>() ?? throw new InvalidDataException("Fixture thread missing.");
        if (request["params"]!.AsObject().Count != 1) throw new InvalidDataException("Fixture received non-stable compact parameters.");
        Console.Out.WriteLine(new JsonObject { ["id"] = request["id"]!.DeepClone(), ["result"] = new JsonObject() }.ToJsonString());
        await Console.Out.FlushAsync();
        if (thread.EndsWith("transport", StringComparison.Ordinal)) return false;
        if (thread.EndsWith("queued", StringComparison.Ordinal)) return true;
        foreach (var notification in new[]
        {
            Notification("turn/started", thread, thread + "-turn"),
            Notification("item/started", thread, thread + "-turn", item: true),
            Notification("item/completed", thread, thread + "-turn", item: true),
            Notification("turn/completed", thread, thread + "-turn", status: thread.EndsWith("failed", StringComparison.Ordinal) ? "failed" : "completed")
        })
            Console.Out.WriteLine(new JsonObject { ["method"] = notification.Method, ["params"] = notification.Params.DeepClone() }.ToJsonString());
        await Console.Out.FlushAsync();
        return true;
    }

    public static async Task WriteBacklogFixtureAsync(JsonObject request)
    {
        var isSeed = request["method"]?.GetValue<string>() == "fixture/compactBacklog";
        Console.Out.WriteLine(new JsonObject
        {
            ["id"] = request["id"]!.DeepClone(),
            ["result"] = isSeed ? new JsonObject() : new JsonObject { ["count"] = _fixtureCompactionDispatches }
        }.ToJsonString());
        await Console.Out.FlushAsync();
        if (!isSeed) return;
        const string thread = "compact-fixture-backlog";
        for (var index = 0; index < 128; index++)
            Console.Out.WriteLine(new JsonObject { ["method"] = "thread/status/changed", ["params"] = new JsonObject { ["threadId"] = thread } }.ToJsonString());
        foreach (var notification in new[]
        {
            Notification("turn/started", thread, "stale-compaction-turn"),
            Notification("item/started", thread, "stale-compaction-turn", item: true),
            Notification("item/completed", thread, "stale-compaction-turn", item: true),
            Notification("turn/completed", thread, "stale-compaction-turn", status: "completed")
        })
            Console.Out.WriteLine(new JsonObject { ["method"] = notification.Method, ["params"] = notification.Params.DeepClone() }.ToJsonString());
        await Console.Out.FlushAsync();
    }

    private static AppServerNotification Notification(string method, string threadId, string turnId, bool item = false, string? status = null)
    {
        var parameters = new JsonObject { ["threadId"] = threadId };
        if (method.StartsWith("turn/", StringComparison.Ordinal)) parameters["turn"] = new JsonObject { ["id"] = turnId, ["status"] = status };
        else parameters["turnId"] = turnId;
        if (item) parameters["item"] = new JsonObject { ["type"] = "contextCompaction", ["id"] = "compact-item" };
        return new(method, parameters);
    }
}
