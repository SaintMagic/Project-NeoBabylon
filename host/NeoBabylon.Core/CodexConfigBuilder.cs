using System.Text.Json.Nodes;
using System.Text;

namespace NeoBabylon.Core;

public enum ToolExecutionPolicy
{
    QualificationWorkspaceWrite,
    Unrestricted,
    ApprovalQualification
}

// Opt-in config composition only. This is not a callable-tool registration or activation grant.
public sealed record DisabledProductMcpConfig(
    string ApplicationRoot,
    GeneratedToolPreparedBindingRecord Binding,
    string AdapterPath);

// Explicit future opt-in only. The current source-level route gate rejects this option.
public sealed record EnabledGeneratedToolMcpConfig(
    string ApplicationRoot,
    string ToolId,
    string ContentIdentity,
    string ReviewIdentity,
    string BindingRecordSha256,
    string ActivationRecordSha256,
    GeneratedToolNodeRuntimeIdentity NodeRuntime,
    RuntimeIdentity AppServerRuntimeIdentity,
    McpServerStatusEntry? ObservedMcpStatus);

public sealed record GeneratedToolMcpConfigAssessment(
    bool Enabled,
    string? FailureKind,
    IReadOnlyList<string> Blockers,
    GeneratedToolMcpInventoryConfirmation InventoryConfirmation,
    string ServerName,
    GeneratedToolMcpInvocationPlan? InvocationPlan);

public static class CodexConfigBuilder
{
    public const string ProductMcpServerName = "neobabylon_generated_tools";

    public static string Build(
        ModelCapabilityRecord capability,
        string? modelCatalogPath = null,
        string? windowsSandboxMode = null,
        bool deterministicOpenRouterMock = false,
        ToolExecutionPolicy toolExecutionPolicy = ToolExecutionPolicy.QualificationWorkspaceWrite,
        DisabledProductMcpConfig? disabledProductMcp = null,
        EnabledGeneratedToolMcpConfig? enabledGeneratedToolMcp = null,
        string? nvidiaAdapterEndpoint = null)
    {
        if (toolExecutionPolicy is ToolExecutionPolicy.Unrestricted or ToolExecutionPolicy.ApprovalQualification
            && !string.IsNullOrWhiteSpace(windowsSandboxMode))
        {
            throw new InvalidOperationException("This execution policy cannot select a Windows sandbox backend.");
        }

        if (!Enum.IsDefined(toolExecutionPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(toolExecutionPolicy));
        }

        if (deterministicOpenRouterMock
            && !string.Equals(capability.ProviderId, "openrouter", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The deterministic OpenRouter mock configuration requires the OpenRouter provider identity.");
        }

        var lines = new List<string>
        {
            $"model = \"{Escape(capability.ModelIdentifier)}\"",
            $"model_provider = \"{Escape(capability.ProviderId)}\"",
            toolExecutionPolicy == ToolExecutionPolicy.ApprovalQualification
                ? "approval_policy = \"on-request\""
                : "approval_policy = \"never\"",
            toolExecutionPolicy switch
            {
                ToolExecutionPolicy.Unrestricted => "default_permissions = \":danger-full-access\"",
                ToolExecutionPolicy.ApprovalQualification => "default_permissions = \":read-only\"",
                _ => "default_permissions = \":workspace\""
            },
            toolExecutionPolicy switch
            {
                ToolExecutionPolicy.Unrestricted => "sandbox_mode = \"danger-full-access\"",
                ToolExecutionPolicy.ApprovalQualification => "sandbox_mode = \"read-only\"",
                ToolExecutionPolicy.QualificationWorkspaceWrite => "sandbox_mode = \"workspace-write\"",
                _ => throw new ArgumentOutOfRangeException(nameof(toolExecutionPolicy))
            },
            "allow_login_shell = false"
        };
        if (string.Equals(capability.ProviderId, "nvidia", StringComparison.OrdinalIgnoreCase))
        {
            // NVIDIA's adapter translates Responses text/function requests only. Disable
            // Codex's standalone provider-native search tool before provider tables so it
            // cannot emit an unsupported Responses web_search ToolSpec.
            lines.Add("web_search = \"disabled\"");
        }
        if (!string.IsNullOrWhiteSpace(modelCatalogPath))
        {
            lines.Add($"model_catalog_json = \"{EscapeTomlPath(modelCatalogPath)}\"");
        }
        var config = CapabilityAdapter.ToCodexConfig(capability);
        if (config.TryGetValue("model_context_window", out var context) && context is int contextWindow)
        {
            lines.Add($"model_context_window = {contextWindow}");
        }
        if (!string.IsNullOrWhiteSpace(windowsSandboxMode))
        {
            if (!string.Equals(windowsSandboxMode, "unelevated", StringComparison.Ordinal))
            {
                throw new ArgumentException("Only the explicitly qualified unelevated Windows sandbox is supported.", nameof(windowsSandboxMode));
            }

            lines.Add(string.Empty);
            lines.Add("[windows]");
            lines.Add("sandbox = \"unelevated\"");
        }

        if (string.Equals(capability.ProviderId, "openrouter", StringComparison.OrdinalIgnoreCase))
        {
            string? endpointTag = null;
            if (deterministicOpenRouterMock)
            {
                if (!IsLoopbackHttpEndpoint(capability.Endpoint))
                {
                    throw new InvalidOperationException("A deterministic OpenRouter mock may target only an HTTP loopback endpoint.");
                }
            }
            else
            {
                if (!string.Equals(capability.Endpoint.TrimEnd('/'), "https://openrouter.ai/api/v1", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("A live OpenRouter provider route requires the canonical https://openrouter.ai/api/v1 base URL.");
                }

                var providerRoute = capability.ProviderRoute.State == CapabilityState.Known
                    && capability.ProviderRoute.Value is JsonObject route
                    ? route
                    : throw new InvalidOperationException("OpenRouter requires an observed provider endpoint; refusing an unpinned route.");
                var providerSlug = providerRoute["providerSlug"]?.GetValue<string>();
                endpointTag = providerRoute["endpointTag"]?.GetValue<string>();
                if (!IsValidOpenRouterEndpointTag(providerSlug, endpointTag))
                {
                    throw new InvalidOperationException("OpenRouter provider route metadata is incomplete or inconsistent; refusing an unpinned route.");
                }
            }

            lines.Add(string.Empty);
            lines.Add("[model_providers.openrouter]");
            lines.Add("name = \"OpenRouter\"");
            lines.Add($"base_url = \"{Escape(capability.Endpoint)}\"");
            lines.Add("wire_api = \"responses\"");
            lines.Add("env_key = \"OPENROUTER_API_KEY\"");
            lines.Add("requires_openai_auth = false");
            lines.Add("request_max_retries = 0");
            lines.Add("stream_max_retries = 0");
            if (endpointTag is not null)
            {
                lines.Add($"openrouter_provider_endpoint = \"{Escape(endpointTag)}\"");
            }

            lines.Add("http_headers = { \"X-OpenRouter-Metadata\" = \"enabled\" }");
        }
        else if (string.Equals(capability.ProviderId, "nvidia", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(capability.Endpoint.TrimEnd('/'), "https://integrate.api.nvidia.com/v1", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("NVIDIA requires its canonical provider endpoint in the selected capability record.");
            }
            if (!IsTrustedNvidiaAdapterEndpoint(nvidiaAdapterEndpoint))
            {
                throw new InvalidOperationException("NVIDIA requires the trusted loopback Responses adapter endpoint; direct provider routing is disabled.");
            }

            lines.Add(string.Empty);
            lines.Add("[model_providers.nvidia]");
            lines.Add("name = \"NVIDIA NIM via NeoBabylon adapter\"");
            lines.Add($"base_url = \"{Escape(nvidiaAdapterEndpoint!)}\"");
            lines.Add("wire_api = \"responses\"");
            lines.Add("env_key = \"NEOBABYLON_PROVIDER_SESSION_TOKEN\"");
            lines.Add("requires_openai_auth = false");
            lines.Add("request_max_retries = 0");
            lines.Add("stream_max_retries = 0");
            lines.Add("supports_websockets = false");
        }
        else if (nvidiaAdapterEndpoint is not null)
        {
            throw new InvalidOperationException("The NVIDIA adapter endpoint cannot be assigned to a different provider.");
        }

        lines.Add(string.Empty);
        lines.Add("[shell_environment_policy]");
        lines.Add("exclude = [\"OPENROUTER_API_KEY\", \"NVIDIA_API_KEY\", \"NEOBABYLON_PROVIDER_SESSION_TOKEN\"]");
        lines.Add("experimental_use_profile = false");

        if (disabledProductMcp is not null)
        {
            if (enabledGeneratedToolMcp is not null)
            {
                throw new InvalidOperationException("Only one product-generated MCP server configuration may be supplied.");
            }
            var adapterPath = ValidateDisabledProductMcp(disabledProductMcp);
            lines.Add(string.Empty);
            lines.Add($"[mcp_servers.{ProductMcpServerName}]");
            lines.Add($"command = \"{EscapeTomlPath(adapterPath)}\"");
            lines.Add("args = []");
            lines.Add("enabled = false");
            // No tool is advertised even if a future runtime mishandles the disabled flag.
            lines.Add("enabled_tools = []");
            lines.Add("startup_timeout_sec = 10");
            lines.Add("tool_timeout_sec = 30");
        }

        if (enabledGeneratedToolMcp is not null)
        {
            var assessment = AssessEnabledGeneratedToolMcp(
                enabledGeneratedToolMcp, capability, toolExecutionPolicy);
            lines.Add(string.Empty);
            lines.Add($"[mcp_servers.{assessment.ServerName}]");
            if (assessment.Enabled && assessment.InvocationPlan is { } plan)
            {
                lines.Add($"command = \"{EscapeTomlPath(plan.NodeExecutablePath)}\"");
                lines.Add($"args = [\"{EscapeTomlPath(plan.EntryPointPath)}\"]");
                lines.Add("enabled = true");
                lines.Add($"enabled_tools = [\"{Escape(plan.ToolName)}\"]");
                lines.Add("startup_timeout_sec = 10");
                lines.Add("tool_timeout_sec = 30");
            }
            else
            {
                // An observed schema/name mismatch rewrites any prior enabled block to an
                // inert state without retaining a candidate command or advertising a tool.
                lines.Add("enabled = false");
                lines.Add("enabled_tools = []");
            }
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    public static string WriteIsolated(
        string codexHome,
        ModelCapabilityRecord capability,
        string? windowsSandboxMode = null,
        bool deterministicOpenRouterMock = false,
        ToolExecutionPolicy toolExecutionPolicy = ToolExecutionPolicy.QualificationWorkspaceWrite,
        DisabledProductMcpConfig? disabledProductMcp = null,
        EnabledGeneratedToolMcpConfig? enabledGeneratedToolMcp = null,
        string? nvidiaAdapterEndpoint = null)
    {
        var isolatedHome = ValidateIsolatedCodexHome(codexHome);
        var catalogPath = Path.Combine(isolatedHome, "model-catalog.json");
        var path = Path.Combine(isolatedHome, "config.toml");
        var config = Build(capability, catalogPath, windowsSandboxMode,
            deterministicOpenRouterMock, toolExecutionPolicy, disabledProductMcp, enabledGeneratedToolMcp,
            nvidiaAdapterEndpoint);
        var configBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(config);
        Directory.CreateDirectory(isolatedHome);
        GeneratedToolCandidatePath.RejectReparseComponents(isolatedHome);
        GeneratedToolCandidatePath.RejectReparseComponents(path);

        var stagedPath = Path.Combine(isolatedHome, $".config.toml.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(stagedPath, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, bufferSize: 4096, options: FileOptions.WriteThrough))
            {
                stream.Write(configBytes);
                stream.Flush(flushToDisk: true);
            }
            if (!File.ReadAllBytes(stagedPath).AsSpan().SequenceEqual(configBytes))
            {
                throw new IOException("Staged isolated config failed readback verification.");
            }

            // The catalog is still a separate write; config and catalog are not a transaction.
            // Publish only after the stage is complete, without truncating or deleting the old config.
            GeneratedToolCandidatePath.RejectReparseComponents(catalogPath);
            File.WriteAllText(catalogPath, CodexModelCatalogBuilder.Build(capability).ToJsonString());
            GeneratedToolCandidatePath.RejectReparseComponents(isolatedHome);
            GeneratedToolCandidatePath.RejectReparseComponents(path);
            if (Directory.Exists(path))
            {
                throw new IOException("Isolated config target is a directory.");
            }
            if (File.Exists(path))
            {
                File.Replace(stagedPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(stagedPath, path);
            }
        }
        finally
        {
            // Stage cleanup never deletes the published target or masks a publication failure.
            try
            {
                if (File.Exists(stagedPath)) File.Delete(stagedPath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return path;
    }

    private static string ValidateIsolatedCodexHome(string codexHome)
    {
        if (string.IsNullOrWhiteSpace(codexHome) || !Path.IsPathFullyQualified(codexHome))
        {
            throw new InvalidDataException("Isolated Codex home requires an absolute path.");
        }
        var home = Path.TrimEndingDirectorySeparator(Path.GetFullPath(codexHome));
        var dataDirectory = Path.GetDirectoryName(home);
        if (dataDirectory is null || !string.Equals(Path.GetFileName(dataDirectory), "Data",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Isolated Codex home must be directly under application Data.");
        }
        var ordinary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex")));
        if (home.StartsWith(@"\\", StringComparison.Ordinal)
            || string.Equals(home, ordinary, StringComparison.OrdinalIgnoreCase)
            || home.StartsWith(ordinary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Isolated config cannot use the ordinary Codex home or a device path.");
        }
        GeneratedToolCandidatePath.RejectReparseComponents(home);
        return home;
    }

    private static string ValidateDisabledProductMcp(DisabledProductMcpConfig option)
    {
        ArgumentNullException.ThrowIfNull(option.Binding);
        var appDataRoot = GeneratedToolCandidatePath.Root(option.ApplicationRoot);
        var applicationRoot = Path.GetDirectoryName(Path.GetDirectoryName(appDataRoot)!)!;
        var binding = option.Binding;
        var current = GeneratedToolIntegrationStore.ReadCurrent(applicationRoot, binding.ToolId)
            ?? throw new InvalidDataException("Disabled MCP config requires a current prepared binding.");
        if (current != binding
            || current.State != GeneratedToolIntegrationStore.PreparedDisabled
            || current.ActivationState != GeneratedToolIntegrationStore.Disabled
            || current.CallableRoute != GeneratedToolIntegrationStore.NoCallableRoute)
        {
            throw new InvalidDataException("Disabled MCP config requires the exact current disabled preparation.");
        }

        if (string.IsNullOrWhiteSpace(option.AdapterPath)
            || !Path.IsPathFullyQualified(option.AdapterPath)
            || option.AdapterPath.Any(char.IsControl)
            || option.AdapterPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part is "." or ".."))
        {
            throw new InvalidDataException("Product MCP adapter path must be absolute and canonical.");
        }
        var adapterPath = Path.GetFullPath(option.AdapterPath);
        var adaptersRoot = Path.Combine(applicationRoot, "Adapters");
        if (!adapterPath.StartsWith(adaptersRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetExtension(adapterPath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Product MCP adapter must be an executable under the application Adapters directory.");
        }
        GeneratedToolCandidatePath.RejectReparseComponents(adapterPath);
        if (!File.Exists(adapterPath))
        {
            throw new InvalidDataException("Product MCP adapter does not exist.");
        }
        return adapterPath;
    }

    public static GeneratedToolMcpConfigAssessment AssessEnabledGeneratedToolMcp(
        EnabledGeneratedToolMcpConfig option, ModelCapabilityRecord capability,
        ToolExecutionPolicy executionPolicy)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(capability);
        string serverName;
        try
        {
            serverName = GeneratedToolMcpRoute.ServerNameFor(option.ToolId);
        }
        catch (InvalidDataException)
        {
            return new GeneratedToolMcpConfigAssessment(false, "unsupported",
                ["generated-tool-id-invalid"], GeneratedToolMcpInventoryConfirmation.Unknown,
                ProductMcpServerName, null);
        }
        try
        {
            ArgumentNullException.ThrowIfNull(option.NodeRuntime);
            var status = GeneratedToolActivationService.GetGeneratedToolActivationStatus(
                option.ApplicationRoot, option.ToolId, option.NodeRuntime,
                option.AppServerRuntimeIdentity, capability,
                executionPolicy, option.ObservedMcpStatus);
            var history = GeneratedToolActivationService.ListGeneratedToolActivationHistory(
                option.ApplicationRoot, option.ToolId).Records;
            var activation = GeneratedToolActivationStore.FindActive(history);
            var candidate = GeneratedToolCandidateStore.Read(option.ApplicationRoot, option.ToolId);
            var review = candidate is null ? null : GeneratedToolReviewStore.ReadCurrent(option.ApplicationRoot, option.ToolId);
            var binding = GeneratedToolIntegrationStore.ReadCurrent(option.ApplicationRoot, option.ToolId);
            var route = candidate is not null && review is not null
                ? GeneratedToolMcpRoute.Assess(option.ApplicationRoot, candidate, review,
                    option.NodeRuntime, option.AppServerRuntimeIdentity, capability,
                    executionPolicy, option.ObservedMcpStatus)
                : null;
            var plan = route?.InvocationPlan;

            var inventory = route?.InventoryConfirmation ?? GeneratedToolMcpInventoryConfirmation.Unknown;
            if (inventory is GeneratedToolMcpInventoryConfirmation.UnexpectedTools
                or GeneratedToolMcpInventoryConfirmation.SchemaMismatch)
            {
                return new GeneratedToolMcpConfigAssessment(false, "unsupported",
                    route!.InventoryBlockers, inventory, serverName, plan);
            }

            var exactActive = activation is not null
                && activation.RecordSha256 == option.ActivationRecordSha256
                && status.State == "active"
                && status.ContentIdentity == option.ContentIdentity
                && status.ReviewIdentity == option.ReviewIdentity
                && status.BindingRecordSha256 == option.BindingRecordSha256
                && status.ActivationRecordSha256 == option.ActivationRecordSha256
                && status.AppServerBinarySha256 == option.AppServerRuntimeIdentity?.Sha256
                && candidate is not null
                && candidate.ContentIdentity == option.ContentIdentity
                && review is not null
                && review.ReviewIdentity == option.ReviewIdentity
                && binding is not null
                && binding.RecordSha256 == option.BindingRecordSha256
                && binding.State == GeneratedToolIntegrationStore.PreparedDisabled
                && route is not null
                && route.QualificationState == "qualified"
                && route.HostQualificationIdentity is not null
                && plan is not null
                && activation.CandidateContentIdentity == candidate.ContentIdentity
                && activation.ReviewIdentity == review.ReviewIdentity
                && activation.ReviewRecordSha256 == review.RecordSha256
                && activation.ReviewSnapshotIdentity == review.ReviewSnapshotIdentity
                && activation.BindingRecordSha256 == binding.RecordSha256
                && activation.InvocationPlanIdentity == plan.PlanIdentity
                && activation.NodeRuntimeSha256 == option.NodeRuntime.Sha256
                && activation.InputSchemaSha256 == plan.InputSchemaSha256
                && activation.OutputSchemaSha256 == plan.OutputSchemaSha256
                && activation.DependencyIdentity == plan.DependencyIdentity
                && activation.AuthorityIdentity == plan.AuthorityIdentity
                && activation.HostQualificationIdentity == route.HostQualificationIdentity;

            if (!exactActive)
            {
                return new GeneratedToolMcpConfigAssessment(false,
                    status.FailureKind ?? "stale",
                    status.Blockers.Append("active-record-or-invocation-plan-stale").Distinct(StringComparer.Ordinal).ToArray(),
                    route?.InventoryConfirmation ?? GeneratedToolMcpInventoryConfirmation.Unknown,
                    serverName, plan);
            }

            // Unknown means the server has not yet registered or the exact descriptors have
            // not yet arrived. Emit the bootstrap config, but Host must suppress every model
            // turn until a fresh status reports CallableTurnsAllowed=true.
            return new GeneratedToolMcpConfigAssessment(true, null, [], inventory, serverName, plan);
        }
        catch (InvalidDataException)
        {
            return new GeneratedToolMcpConfigAssessment(false, "stale",
                ["active-record-or-invocation-plan-stale"],
                GeneratedToolMcpInventoryConfirmation.Unknown, serverName, null);
        }
    }

    private static bool IsLoopbackHttpEndpoint(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttp
        && uri.IsLoopback
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);

    private static bool IsTrustedNvidiaAdapterEndpoint(string? endpoint) =>
        !string.IsNullOrWhiteSpace(endpoint)
        && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttp
        && uri.IsLoopback
        && uri.Port is >= 1 and <= 65535
        && string.Equals(uri.AbsolutePath.TrimEnd('/'), "/v1", StringComparison.Ordinal)
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);

    private static bool IsValidOpenRouterEndpointTag(string? providerSlug, string? endpointTag)
    {
        if (providerSlug is null || !IsRouteSegment(providerSlug) || endpointTag is null)
        {
            return false;
        }

        // A bare provider slug selects that provider, including future endpoint variants.
        return string.Equals(endpointTag, providerSlug, StringComparison.Ordinal)
            || (endpointTag.StartsWith($"{providerSlug}/", StringComparison.Ordinal)
                && IsRouteSegment(endpointTag[(providerSlug.Length + 1)..]));
    }

    private static bool IsRouteSegment(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string EscapeTomlPath(string value) => Escape(Path.GetFullPath(value).Replace('\\', '/'));
}
