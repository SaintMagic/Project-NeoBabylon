using System.Text.Json.Nodes;
using NeoBabylon.Core;

internal static class ToolOutputChecks
{
    public static void Run(Action<string, Action> check)
    {
        check("structured result survives saved projection and paging", () =>
        {
            var item = JsonNode.Parse("""{"id":"i","type":"mcpToolCall","status":"completed","result":{"content":[{"type":"text","text":"answer"}]}}""")!.AsObject();
            var saved = Saved(item);
            Fixture.Require(saved["text"]?.GetValue<string>() == item["result"]!.ToJsonString(), "Structured result lost.");
            Fixture.Require(Page(item).Text == saved["text"]?.GetValue<string>(), "Saved and paged representation disagree.");
        });
        check("structured failure is visible and remains failed after reload", () =>
        {
            var item = JsonNode.Parse("""{"id":"i","type":"mcpToolCall","status":"completed","error":{"message":"permission denied"}}""")!.AsObject();
            var saved = Saved(item);
            Fixture.Require(saved["outcome"]?.GetValue<string>() == "failed", "Structured failure misclassified.");
            Fixture.Require(Page(item).Text.Contains("permission denied"), "Error-only item is not inspectable.");
            var projected = Project(item);
            Fixture.Require(TurnDiagnostics.HasFailure(TurnDiagnostics.Extract([projected])), "Live structured error reported successful.");
        });
        check("large structured results cannot bypass display bound", () =>
        {
            var result = new JsonObject { ["text"] = new string('x', 100_000) };
            var item = new JsonObject { ["id"] = "i", ["type"] = "mcpToolCall", ["status"] = "completed", ["result"] = result };
            var original = item.ToJsonString();
            var projected = Project(item).Params["item"]!.AsObject();
            Fixture.Require(projected["result"] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length <= AppServerNotificationProjection.ToolOutputDisplayLimit, "Structured result bypassed bound.");
            Fixture.Require(projected["neoBabylonDisplay"]?["displayTruncated"]?.GetValue<bool>() == true, "Omission not disclosed.");
            Fixture.Require(item.ToJsonString() == original, "Source journal item was mutated.");
            var saved = Saved(item);
            Fixture.Require(saved["text"]?.GetValue<string>()?.Length == AppServerNotificationProjection.ToolOutputDisplayLimit, "Saved preview bound differs.");
            Fixture.Require(Page(item, 40_000).Text.Length > 0, "Omitted source is not pageable.");
        });
        check("dynamic content and explicit unsuccessful result survive projection", () =>
        {
            var item = JsonNode.Parse("""{"id":"i","type":"dynamicToolCall","status":"completed","success":false,"contentItems":[{"type":"inputText","text":"failed validation"}]}""")!.AsObject();
            var saved = Saved(item);
            Fixture.Require(saved["text"]?.GetValue<string>()?.Contains("failed validation") == true, "Dynamic content lost.");
            Fixture.Require(saved["outcome"]?.GetValue<string>() == "failed", "Explicit unsuccessful result ignored.");
            Fixture.Require(TurnDiagnostics.HasFailure(TurnDiagnostics.Extract([Project(item)])), "Live unsuccessful result ignored.");
        });
        check("saved command preserves exit code and duration", () =>
        {
            var item = JsonNode.Parse("""{"id":"i","type":"commandExecution","status":"completed","error":"","aggregatedOutput":"output","exitCode":7,"durationMs":123}""")!.AsObject();
            var saved = Saved(item);
            Fixture.Require(saved["text"]?.GetValue<string>() == "output", "Empty error shadows real output.");
            Fixture.Require(saved["outcome"]?.GetValue<string>() == "failed", "Nonzero exit misclassified.");
            Fixture.Require(saved["exitCode"]?.GetValue<int>() == 7 && saved["durationMs"]?.GetValue<int>() == 123, "Execution metadata lost.");
            Fixture.Require(TurnDiagnostics.Extract([Project(item)])[0]?["durationMs"]?.GetValue<int>() == 123, "Terminal diagnostics lost duration.");
        });
        check("session journal retains exact synthetic normal output and leaves missing output unknown", () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "NeoBabylon-ProductParity-Journal-" + Guid.NewGuid().ToString("N"));
            var codexHome = Path.Combine(root, "CodexHome");
            var sessions = Path.Combine(codexHome, "sessions");
            Directory.CreateDirectory(sessions);
            var journal = Path.Combine(sessions, "rollout-test.jsonl");
            const string expectedArguments = "{\"cmd\":\"synthetic-command\"}";
            const string expectedOutput = "Chunk ID: fixture\nProcess exited with code 0\nOutput:\nSYNTHETIC_NORMAL_OUTPUT";
            try
            {
                var records = new JsonArray
                {
                    new JsonObject { ["payload"] = new JsonObject { ["type"] = "function_call", ["id"] = "call-item", ["call_id"] = "call-1", ["name"] = "exec_command", ["arguments"] = expectedArguments } },
                    new JsonObject { ["payload"] = new JsonObject { ["type"] = "function_call", ["id"] = "missing-item", ["call_id"] = "call-2", ["name"] = "exec_command", ["arguments"] = expectedArguments } },
                    new JsonObject { ["payload"] = new JsonObject { ["type"] = "function_call_output", ["id"] = "output-item", ["call_id"] = "call-1", ["output"] = expectedOutput } }
                };
                File.WriteAllText(journal, string.Join(Environment.NewLine, records.Select(record => record!.ToJsonString())) + Environment.NewLine);

                var read = SessionJournalToolEvidenceReader.ReadAppended(journal, codexHome, 0);
                Fixture.Require(read.Status == "read", "Synthetic journal was not read successfully.");
                var diagnostics = TurnDiagnostics.ExtractSessionJournal(read.Calls).OfType<JsonObject>().ToDictionary(
                    item => item["callId"]!.GetValue<string>(), StringComparer.Ordinal);
                var normal = diagnostics["call-1"];
                Fixture.Require(normal["itemId"]?.GetValue<string>() == "call-item", "Call item identity was not paired.");
                Fixture.Require(normal["arguments"]?.GetValue<string>() == expectedArguments, "Arguments were not retained exactly.");
                Fixture.Require(normal["outputItemId"]?.GetValue<string>() == "output-item", "Output item identity was not retained.");
                Fixture.Require(normal["output"]?.GetValue<string>() == expectedOutput, "Normal output was not retained exactly.");
                Fixture.Require(normal["outcome"]?.GetValue<string>() == "succeeded", "Synthetic successful result was misclassified.");
                var missing = diagnostics["call-2"];
                Fixture.Require(missing["outcome"]?.GetValue<string>() == "unknown", "Missing output was not left Unknown.");
                Fixture.Require(missing["output"] is null && missing["outputItemId"] is null, "Missing output gained fabricated content or identity.");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        });
    }

    private static JsonObject Saved(JsonObject item) => ThreadSavedOutputProjector.Project(
        new JsonArray(new JsonObject { ["id"] = "t", ["items"] = new JsonArray(item.DeepClone()) })).Outputs[0]!.AsObject();
    private static AppServerNotification Project(JsonObject item) => new AppServerNotificationProjection().Project(
        new AppServerNotification("item/completed", new JsonObject { ["threadId"] = "thread", ["turnId"] = "t", ["item"] = item.DeepClone() }))!;
    private static ThreadItemOutputRange Page(JsonObject item, int offset = 0) => ThreadItemOutputRangeProjector.Project(
        new JsonObject { ["data"] = new JsonArray(new JsonObject { ["turnId"] = "t", ["item"] = item.DeepClone() }) }, "thread", "t", "i", offset, 32_768);
}
