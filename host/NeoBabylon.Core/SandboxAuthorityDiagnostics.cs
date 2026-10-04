using System.Text.Json.Nodes;

namespace NeoBabylon.Core;

public static class SandboxAuthorityDiagnostics
{
    public static JsonObject RequireUnrestricted(JsonObject effectiveConfig, JsonObject threadStart)
    {
        var report = Build(effectiveConfig, threadStart);
        var config = effectiveConfig["config"] as JsonObject ?? effectiveConfig;
        var configApproval = StringValue(config["approval_policy"]);
        var threadApproval = StringValue(threadStart["approvalPolicy"]);
        if (!string.Equals(report["configSandboxMode"]?.GetValue<string>(), "danger-full-access", StringComparison.Ordinal)
            || !string.Equals(configApproval, "never", StringComparison.Ordinal)
            || !string.Equals(report["effectiveSandboxType"]?.GetValue<string>(), "dangerFullAccess", StringComparison.Ordinal)
            || !string.Equals(threadApproval, "never", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("App Server did not confirm the selected unrestricted execution policy; the thread cannot run tools.");
        }

        report["controllingAuthority"] = "Codex unrestricted execution";
        report["containedToolsQualified"] = false;
        return report;
    }

    public static JsonObject RequireApprovalQualification(JsonObject effectiveConfig, JsonObject threadStart)
    {
        var report = Build(effectiveConfig, threadStart);
        if (!string.Equals(report["configSandboxMode"]?.GetValue<string>(), "read-only", StringComparison.Ordinal)
            || !string.Equals(report["configApprovalPolicy"]?.GetValue<string>(), "on-request", StringComparison.Ordinal)
            || !string.Equals(report["effectiveSandboxType"]?.GetValue<string>(), "readOnly", StringComparison.Ordinal)
            || !string.Equals(report["effectiveApprovalPolicy"]?.GetValue<string>(), "on-request", StringComparison.Ordinal)
            || report["windowsSandboxMode"] is not null)
        {
            throw new InvalidOperationException("App Server did not confirm the fixed QA read-only/on-request profile; approval qualification is blocked.");
        }

        report["controllingAuthority"] = "Codex App Server QA read-only approval qualification";
        report["containedToolsQualified"] = false;
        return report;
    }

    public static JsonObject Build(JsonObject effectiveConfig, JsonObject threadStart)
    {
        var config = effectiveConfig["config"] as JsonObject ?? effectiveConfig;
        var configSandboxMode = StringValue(config["sandbox_mode"]);
        var configApprovalPolicy = StringValue(config["approval_policy"]);
        var windowsSandboxMode = StringValue((config["windows"] as JsonObject)?["sandbox"]);
        var effectiveSandbox = threadStart["sandbox"]?.DeepClone();
        var effectiveSandboxType = StringValue((threadStart["sandbox"] as JsonObject)?["type"]);
        var effectiveApprovalPolicy = StringValue(threadStart["approvalPolicy"]);
        var downgraded = string.Equals(configSandboxMode, "workspace-write", StringComparison.Ordinal)
            && string.Equals(effectiveSandboxType, "readOnly", StringComparison.Ordinal)
            && !string.Equals(windowsSandboxMode, "unelevated", StringComparison.Ordinal);

        return new JsonObject
        {
            ["configSandboxMode"] = configSandboxMode,
            ["configApprovalPolicy"] = configApprovalPolicy,
            ["windowsSandboxMode"] = windowsSandboxMode,
            ["effectiveSandbox"] = effectiveSandbox,
            ["effectiveSandboxType"] = effectiveSandboxType,
            ["effectiveApprovalPolicy"] = effectiveApprovalPolicy,
            ["downgradedByWindowsBackend"] = downgraded,
            ["controllingAuthority"] = windowsSandboxMode switch
            {
                "unelevated" => "Codex Windows restricted-token sandbox",
                _ when downgraded => "Codex Windows backend downgrade because no sandbox backend was selected",
                _ => "Codex effective sandbox projection"
            }
        };
    }

    private static string? StringValue(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
