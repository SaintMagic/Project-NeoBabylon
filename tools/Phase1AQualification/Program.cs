using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NeoBabylon.Core;
using NeoBabylon.Phase1AQualification;

var sourceRoot = LocateSourceRoot();
var openRouterMode = args.Contains("--openrouter", StringComparer.OrdinalIgnoreCase);
var qualificationPhase = openRouterMode ? "1B" : "1A";
var configuredApplicationRoot = Environment.GetEnvironmentVariable("NEOBABYLON_APPLICATION_ROOT");
var useGeneratedApplicationRoot = string.IsNullOrWhiteSpace(configuredApplicationRoot);
var applicationRoot = useGeneratedApplicationRoot
    ? Path.Combine(ReserveDefaultRunRoot(sourceRoot), "App")
    : configuredApplicationRoot!;
if (useGeneratedApplicationRoot)
{
    Environment.SetEnvironmentVariable("NEOBABYLON_APPLICATION_ROOT", applicationRoot, EnvironmentVariableTarget.Process);
}
var artifactPath = openRouterMode
    ? Path.Combine(sourceRoot, "artifacts", "phase1b", "openrouter", "qualification.json")
    : Path.Combine(sourceRoot, "artifacts", "phase1a", "lmstudio", "qualification.json");
var evidence = new JsonObject
{
    ["schemaVersion"] = 1,
    ["phase"] = qualificationPhase,
    ["providerTarget"] = openRouterMode ? "openrouter" : "lmstudio",
    ["startedAtUtc"] = DateTimeOffset.UtcNow,
    ["sourceRepositoryRoot"] = sourceRoot,
    ["applicationRoot"] = Path.GetFullPath(applicationRoot),
    ["mockRunsBeforeLive"] = true
};
var mockOnly = args.Contains("--mock-only", StringComparer.OrdinalIgnoreCase);
var windowsSandboxMode = args.Contains("--windows-sandbox-unelevated", StringComparer.OrdinalIgnoreCase)
    ? "unelevated"
    : null;
var toolExecutionPolicy = args.Contains("--unrestricted-tools", StringComparer.OrdinalIgnoreCase)
    ? ToolExecutionPolicy.Unrestricted
    : ToolExecutionPolicy.QualificationWorkspaceWrite;

try
{
    var mockProbeCommand = QualificationMockProbe.ResolveCommand(args, mockOnly, openRouterMode);
    var mockProviderErrorStatusCode = QualificationMockProviderError.ResolveStatusCode(args, mockOnly, openRouterMode);
    var offlineOpenRouterMock = openRouterMode && mockOnly && mockProbeCommand is not null;
    var identity = RuntimeIdentity.LoadVerified(Path.Combine(sourceRoot, "runtime", "runtime-lock.json"));
    var layout = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
    evidence["runtime"] = JsonSerializer.SerializeToNode(identity, QualificationJson.Options);
    evidence["roots"] = new JsonObject
    {
        ["sourceRepositoryRoot"] = layout.SourceRepositoryRoot,
        ["applicationRoot"] = layout.ApplicationRoot,
        ["dataRoot"] = layout.DataRoot,
        ["codexHome"] = layout.CodexHome,
        ["fixtureWorkspace"] = layout.FixtureWorkspace,
        ["ordinaryCodexRootUsed"] = ApplicationRootLayout.IsOrdinaryCodexRoot(layout.CodexHome)
    };

    LmStudioInspection? lmStudio = null;
    ModelCapabilityRecord capability;
    if (openRouterMode)
    {
        var recordPath = CapabilityRecordPathResolver.Resolve(
            sourceRoot,
            Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_OPENROUTER_LING_3_1_FLASH_20261004.json"));
        capability = JsonSerializer.Deserialize<ModelCapabilityRecord>(
            await File.ReadAllTextAsync(recordPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("The authoritative OpenRouter capability record could not be loaded.");
        evidence["capabilityRecordPath"] = recordPath;
        evidence["capabilityRecord"] = JsonSerializer.SerializeToNode(capability, QualificationJson.Options);
        if (offlineOpenRouterMock)
        {
            evidence["providerInspection"] = new JsonObject
            {
                ["provider"] = "openrouter",
                ["mode"] = "offline deterministic mock probe",
                ["liveInspectionPerformed"] = false
            };
            Console.WriteLine("Offline OpenRouter mock qualification: using the accepted capability record; no provider inspection, model load, inference, or external request.");
        }
        else
        {
            evidence["providerInspection"] = await OpenRouterInspection.ReadAndValidateAsync(capability);
            Console.WriteLine($"OpenRouter public route snapshot verified: {capability.ModelIdentifier}; catalog context {capability.ContextWindowAdvertised.Int32Value?.ToString() ?? "Unknown"}; reasoning {capability.ReasoningControls.Value?.ToJsonString() ?? "Unknown"}");
        }
    }
    else if (mockOnly && (mockProbeCommand is not null || mockProviderErrorStatusCode is not null))
    {
        var recordPath = CapabilityRecordPathResolver.Resolve(
            sourceRoot,
            Path.Combine(sourceRoot, "docs", "release", "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json"));
        capability = JsonSerializer.Deserialize<ModelCapabilityRecord>(
            await File.ReadAllTextAsync(recordPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("The accepted LM Studio capability record could not be loaded for the offline mock qualification.");
        evidence["capabilityRecordPath"] = recordPath;
        evidence["capabilityRecord"] = JsonSerializer.SerializeToNode(capability, QualificationJson.Options);
        evidence["providerInspection"] = new JsonObject
        {
            ["provider"] = "lmstudio",
            ["mode"] = "offline deterministic mock probe",
            ["liveInspectionPerformed"] = false
        };
        Console.WriteLine("Offline mock qualification: using the accepted LM Studio capability record; no provider inspection, model load, or inference.");
    }
    else
    {
        lmStudio = await LmStudioInspection.ReadAsync();
        evidence["providerInspection"] = lmStudio.Inspection;
        capability = lmStudio.Capability;
        Console.WriteLine($"LM Studio {lmStudio.ServerVersion}; candidate {capability.ModelIdentifier} {capability.ModelVariant}; advertised context {capability.ContextWindowAdvertised.Int32Value?.ToString() ?? "Unknown"}; tool use {capability.ToolFunctionCalling.StringValue ?? "Unknown"}");
    }

    var mockRoot = Path.Combine(applicationRoot, "Mock");
    var mockToolCommand = openRouterMode ? mockProbeCommand ?? "cmd.exe /d /c ver" : mockProbeCommand;
    var mockResult = await RunMockQualificationAsync(
        sourceRoot,
        mockRoot,
        identity,
        capability,
        windowsSandboxMode,
        mockToolCommand,
        mockProviderErrorStatusCode,
        toolExecutionPolicy);
    evidence["mock"] = mockResult;
    var mockQualificationPassed = mockResult["toolRoundTrip"]?.GetValue<bool>() == true
        || mockResult["expectedProviderErrorObserved"]?.GetValue<bool>() == true;
    var mockResultLabel = mockResult["expectedProviderErrorObserved"]?.GetValue<bool>() == true
        ? "429 qualification"
        : "tool round trip";
    Console.WriteLine($"Mock Responses {mockResultLabel}: {(mockQualificationPassed ? "PASS" : "FAIL")}");
    if (mockOnly)
    {
        evidence["completedAtUtc"] = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
        await File.WriteAllTextAsync(artifactPath, evidence.ToJsonString(QualificationJson.Options));
        Console.WriteLine($"Evidence: {artifactPath}");
        return mockQualificationPassed ? 0 : 2;
    }

    if (mockResult["toolRoundTrip"]?.GetValue<bool>() != true)
    {
        throw new InvalidOperationException("The deterministic mock Responses path failed; live provider inference was not started.");
    }

    string? openRouterApiKey = null;
    if (openRouterMode)
    {
        openRouterApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY", EnvironmentVariableTarget.Process);
        if (string.IsNullOrWhiteSpace(openRouterApiKey))
        {
            throw new InvalidOperationException("OPENROUTER_API_KEY is not available to the qualification process; live inference was not started.");
        }

        evidence["credentialSource"] = "OPENROUTER_API_KEY process environment; value never recorded";
        evidence["capabilityRecord"] = JsonSerializer.SerializeToNode(capability, QualificationJson.Options);
    }
    else
    {
        var loadResult = await lmStudio!.LoadSelectedModelAsync();
        evidence["modelLoad"] = loadResult;
        capability = lmStudio.RefreshCapability(loadResult);
        evidence["capabilityRecord"] = JsonSerializer.SerializeToNode(capability, QualificationJson.Options);
    }

    var liveRoot = Path.Combine(applicationRoot, "Live");
    var liveResult = await RunLiveQualificationAsync(
        liveRoot,
        identity,
        capability,
        windowsSandboxMode,
        openRouterMode,
        openRouterApiKey,
        toolExecutionPolicy);
    evidence["live"] = liveResult;
    evidence["completedAtUtc"] = DateTimeOffset.UtcNow;
    Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
    await File.WriteAllTextAsync(artifactPath, evidence.ToJsonString(QualificationJson.Options));
    Console.WriteLine($"Live Responses path: {(liveResult["toolRoundTrip"]?.GetValue<bool>() == true ? "PASS" : "FAIL")}");
    Console.WriteLine($"Evidence: {artifactPath}");
    return liveResult["toolRoundTrip"]?.GetValue<bool>() == true ? 0 : 2;
}
catch (Exception ex)
{
    evidence["failure"] = new JsonObject
    {
        ["attributedTo"] = "NeoBabylon.Phase1AQualification",
        ["type"] = ex.GetType().FullName,
        ["message"] = ex.Message,
        ["stack"] = ex.StackTrace
    };
    Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
    await File.WriteAllTextAsync(artifactPath, evidence.ToJsonString(QualificationJson.Options));
    Console.Error.WriteLine($"Phase {qualificationPhase} qualification failed: {ex.Message}");
    Console.Error.WriteLine($"Evidence: {artifactPath}");
    return 1;
}

static async Task<JsonObject> RunMockQualificationAsync(
    string sourceRoot,
    string applicationRoot,
    RuntimeIdentity identity,
    ModelCapabilityRecord capability,
    string? windowsSandboxMode,
    string? mockToolCommand,
    int? mockProviderErrorStatusCode,
    ToolExecutionPolicy toolExecutionPolicy)
{
    var layout = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
    using var dataLease = RuntimeDataLease.Acquire(layout.DataRoot);
    var toolCommand = mockToolCommand ?? "cmd.exe /d /c ver";
    await using var mockServer = await MockResponsesServer.StartAsync(toolCommand, mockProviderErrorStatusCode);
    var mockCapability = capability with { Endpoint = mockServer.BaseUrl };
    var isOpenRouter = string.Equals(capability.ProviderId, "openrouter", StringComparison.OrdinalIgnoreCase);
    if (isOpenRouter)
    {
        mockCapability = mockCapability with
        {
            ProviderRoute = CapabilityObservation.Unknown(
                "deterministic-local-mock",
                "The local Responses fixture does not represent or attest a live OpenRouter provider endpoint.")
        };
    }

    var configPath = CodexConfigBuilder.WriteIsolated(
        layout.CodexHome,
        mockCapability,
        windowsSandboxMode,
        deterministicOpenRouterMock: isOpenRouter,
        toolExecutionPolicy: toolExecutionPolicy);
    await using var client = AppServerClient.Start(new AppServerLaunchOptions(
        identity.BinaryPath,
        layout.FixtureWorkspace,
        layout.CodexHome,
        string.Equals(capability.ProviderId, "openrouter", StringComparison.OrdinalIgnoreCase) ? null : mockServer.BaseUrl,
        string.Equals(capability.ProviderId, "openrouter", StringComparison.OrdinalIgnoreCase) ? "phase1b-mock-secret" : null));
    await client.InitializeAsync();
    var effectiveConfig = await client.RequestResultAsync(AppServerProtocol.BuildConfigReadRequest(2, layout.FixtureWorkspace));
    var threadResponse = await client.RequestResultAsync(AppServerProtocol.BuildThreadStartRequest(
        3,
        new ThreadStartOptions(
            mockCapability.ModelIdentifier,
            mockCapability.ProviderId,
            layout.FixtureWorkspace,
            toolExecutionPolicy == ToolExecutionPolicy.Unrestricted ? "danger-full-access" : "workspace-write",
            "never",
            CapabilityAdapter.ToCodexConfig(mockCapability))));
    var executionAuthority = toolExecutionPolicy == ToolExecutionPolicy.Unrestricted
        ? SandboxAuthorityDiagnostics.RequireUnrestricted(effectiveConfig, threadResponse)
        : SandboxAuthorityDiagnostics.Build(effectiveConfig, threadResponse);
    var threadId = ExtractThreadId(threadResponse);
    await client.RequestResultAsync(AppServerProtocol.BuildTurnStartRequest(
        4,
        new TurnStartOptions(
            threadId,
            "Use the available command tool to perform the requested diagnostic operation.",
            toolExecutionPolicy)));
    var observation = await client.WaitForTurnCompletionAsync(TimeSpan.FromMinutes(2));
    var journalPath = threadResponse["thread"]?["path"]?.GetValue<string>()
        ?? throw new InvalidDataException("The mock App Server thread returned no isolated session journal path.");
    var journalEvidence = SessionJournalToolEvidenceReader.ReadAppended(journalPath, layout.CodexHome, 0);
    var journalToolDiagnostics = TurnDiagnostics.ExtractSessionJournal(journalEvidence.Calls);
    var typedToolFailure = journalToolDiagnostics
        .OfType<JsonObject>()
        .FirstOrDefault(item => item["succeeded"]?.GetValue<bool>() == false)?["failure"]?.DeepClone();
    var toolOutputRequestSeen = mockServer.RequestBodies.Any(body => body.Contains("function_call_output", StringComparison.Ordinal));
    var toolOutput = mockServer.RequestBodies
        .Select(ExtractFunctionCallOutput)
        .LastOrDefault(output => !string.IsNullOrWhiteSpace(output))
        ?? string.Empty;
    const string expectedMarker = "Microsoft Windows";
    var toolSucceeded = toolOutput.Contains(expectedMarker, StringComparison.OrdinalIgnoreCase)
        && !toolOutput.Contains("failed", StringComparison.OrdinalIgnoreCase);
    var effectiveConfigObject = effectiveConfig["config"] as JsonObject ?? effectiveConfig;
    var selectedModel = capability.ModelIdentifier;
    var selectedProvider = capability.ProviderId;
    var effectiveModel = effectiveConfigObject["model"]?.GetValue<string>();
    var effectiveProvider = effectiveConfigObject["model_provider"]?.GetValue<string>();
    var requestModels = mockServer.RequestBodies
        .Select(body => JsonNode.Parse(body)?["model"]?.GetValue<string>())
        .Where(model => !string.IsNullOrWhiteSpace(model))
        .ToArray();
    var modelFallbackObserved = requestModels.Any(model => !string.Equals(model, selectedModel, StringComparison.Ordinal));
    var providerFallbackObserved = !string.Equals(effectiveProvider, selectedProvider, StringComparison.Ordinal);
    var credentialExcludedFromTools = effectiveConfigObject["shell_environment_policy"]?["exclude"] is JsonArray exclusions
        && exclusions.Any(value => value?.GetValue<string>() == "OPENROUTER_API_KEY");
    var toolEnvironmentCredentialObservation = !isOpenRouter
        ? null
        : toolOutput.Contains("NB_OPENROUTER_KEY_ABSENT", StringComparison.Ordinal)
            ? "absent"
            : toolOutput.Contains("NB_OPENROUTER_KEY_PRESENT", StringComparison.Ordinal)
                ? "present"
                : "unknown";
    var requestSummaries = new JsonArray();
    foreach (var requestBody in mockServer.RequestBodies)
    {
        var request = JsonNode.Parse(requestBody)?.AsObject();
        if (request is null)
        {
            continue;
        }

        requestSummaries.Add(new JsonObject
        {
            ["model"] = request["model"]?.DeepClone(),
            ["reasoning"] = request["reasoning"]?.DeepClone(),
            ["toolChoice"] = request["tool_choice"]?.DeepClone(),
            ["toolsCount"] = request["tools"]?.AsArray().Count,
            ["providerRoutingRequested"] = request["provider"] is not null
        });
    }
    var appServerFailureEvidence = string.Join(
        Environment.NewLine,
        new[] { observation.Failure }
            .Concat(observation.Notifications.Select(notification => notification.Params.ToJsonString()))
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    const string mockRateLimitMessage = "mock rate limit";
    var appServerError = observation.Notifications
        .Where(notification => notification.Method == "error")
        .Select(notification => notification.Params["error"] as JsonObject)
        .FirstOrDefault(error => error?["codexErrorInfo"]?["responseTooManyFailedAttempts"] is JsonObject);
    var appServerRateLimitError = appServerError?["codexErrorInfo"]?["responseTooManyFailedAttempts"] as JsonObject;
    var appServerErrorType = appServerRateLimitError is null ? null : "responseTooManyFailedAttempts";
    int? appServerStatusCode = appServerRateLimitError?["httpStatusCode"] is JsonValue statusValue
        && statusValue.TryGetValue<int>(out var parsedStatusCode)
            ? parsedStatusCode
            : null;
    var appServerErrorMessage = appServerError?["message"]?.GetValue<string>();
    var appServerErrorVisible = mockProviderErrorStatusCode == 429
        && appServerStatusCode == 429
        && appServerErrorType == "responseTooManyFailedAttempts"
        && appServerFailureEvidence.Contains("429", StringComparison.Ordinal);
    var observed429 = mockServer.ResponseStatusCodes.Contains(429);
    var noToolCallObserved = !toolOutputRequestSeen
        && !observation.Notifications.Any(notification =>
            notification.Params.ToJsonString().Contains("exec_command", StringComparison.Ordinal)
            || notification.Params.ToJsonString().Contains("function_call", StringComparison.Ordinal));
    var selectedModelStayedEffective = string.Equals(effectiveModel, selectedModel, StringComparison.Ordinal)
        && requestModels.Length > 0
        && requestModels.All(model => string.Equals(model, selectedModel, StringComparison.Ordinal));
    var noProviderFallbackObserved = string.Equals(effectiveProvider, selectedProvider, StringComparison.Ordinal);
    var expectedProviderErrorObserved = mockProviderErrorStatusCode == 429
        && observed429
        && mockServer.ResponseStatusCodes.All(status => status == 429)
        && observation.Terminal
        && observation.Status == "failed"
        && appServerErrorVisible
        && noToolCallObserved
        && selectedModelStayedEffective
        && noProviderFallbackObserved;
    var providerFailure = mockProviderErrorStatusCode is null
        ? null
        : new JsonObject
        {
            ["statusCode"] = mockServer.ResponseStatusCodes.FirstOrDefault(status => status == mockProviderErrorStatusCode.Value),
            ["fixtureResponseMessage"] = mockRateLimitMessage,
            ["fixtureErrorType"] = "rate_limit_error",
            ["fixtureErrorCode"] = "rate_limit_exceeded",
            ["httpStatusCode"] = appServerStatusCode,
            ["appServerErrorType"] = appServerErrorType,
            ["appServerErrorMessage"] = appServerErrorMessage,
            ["appServerError"] = appServerError?.DeepClone(),
            ["appServerErrorVisible"] = appServerErrorVisible,
            ["appServerFailure"] = observation.Failure,
            ["effectiveModel"] = effectiveModel,
            ["effectiveProvider"] = effectiveProvider,
            ["modelFallbackObserved"] = modelFallbackObserved,
            ["providerFallbackObserved"] = providerFallbackObserved
        };
    var attributedTo = mockProviderErrorStatusCode == 429 && observed429
        ? "Codex App Server → deterministic mock Responses"
        : observation.Completed
            ? "Codex App Server → deterministic mock Responses"
            : "NeoBabylon.Phase1AQualification";
    return new JsonObject
    {
        ["attributedTo"] = attributedTo,
        ["completed"] = observation.Completed,
        ["toolRoundTrip"] = observation.Completed && toolOutputRequestSeen && toolSucceeded,
        ["ordinaryTool"] = "exec_command",
        ["toolCommand"] = toolCommand,
        ["toolOutputRequestSeen"] = toolOutputRequestSeen,
        ["noToolCallObserved"] = noToolCallObserved,
        ["toolSucceeded"] = toolSucceeded,
        ["toolOutputEvidence"] = toolOutput,
        ["typedToolFailure"] = typedToolFailure,
        ["toolEvidenceReadStatus"] = journalEvidence.Status,
        ["providerCredentialExcludedByEffectiveConfig"] = isOpenRouter ? credentialExcludedFromTools : null,
        ["toolEnvironmentCredentialObservation"] = toolEnvironmentCredentialObservation,
        ["failure"] = observation.Failure,
        ["expectedProviderErrorObserved"] = expectedProviderErrorObserved,
        ["providerFailure"] = providerFailure,
        ["notificationMethods"] = NotificationMethods(observation),
        ["interestingNotifications"] = InterestingNotifications(observation),
        ["threadStart"] = threadResponse,
        ["isolatedConfigPath"] = configPath,
        ["modelCatalogPath"] = Path.Combine(Path.GetDirectoryName(configPath)!, "model-catalog.json"),
        ["codexModelCatalog"] = CodexModelCatalogBuilder.Build(mockCapability),
        ["executionAuthority"] = executionAuthority,
        ["effectiveConfig"] = effectiveConfig,
        ["responsesEndpoint"] = mockServer.BaseUrl,
        ["capabilityConfigSent"] = JsonSerializer.SerializeToNode(CapabilityAdapter.ToCodexConfig(mockCapability), QualificationJson.Options),
        ["responsesRequestCount"] = mockServer.RequestBodies.Count,
        ["responsesRequestSummaries"] = requestSummaries,
        ["responsesHttpStatusCodes"] = new JsonArray(
            mockServer.ResponseStatusCodes.Select(status => (JsonNode?)JsonValue.Create(status)).ToArray())
    };
}

static async Task<JsonObject> RunLiveQualificationAsync(
    string applicationRoot,
    RuntimeIdentity identity,
    ModelCapabilityRecord capability,
    string? windowsSandboxMode,
    bool openRouterMode,
    string? openRouterApiKey,
    ToolExecutionPolicy toolExecutionPolicy)
{
    var sourceRoot = LocateSourceRoot();
    var layout = ApplicationRootLayout.Create(sourceRoot, applicationRoot);
    using var dataLease = RuntimeDataLease.Acquire(layout.DataRoot);
    var configPath = CodexConfigBuilder.WriteIsolated(
        layout.CodexHome,
        capability,
        windowsSandboxMode,
        toolExecutionPolicy: toolExecutionPolicy);
    await using var client = AppServerClient.Start(new AppServerLaunchOptions(
        identity.BinaryPath,
        layout.FixtureWorkspace,
        layout.CodexHome,
        openRouterMode ? null : capability.Endpoint,
        openRouterMode ? openRouterApiKey : null));
    await client.InitializeAsync();
    var effectiveConfig = await client.RequestResultAsync(AppServerProtocol.BuildConfigReadRequest(2, layout.FixtureWorkspace));
    var threadResponse = await client.RequestResultAsync(AppServerProtocol.BuildThreadStartRequest(
        3,
        new ThreadStartOptions(
            capability.ModelIdentifier,
            capability.ProviderId,
            layout.FixtureWorkspace,
            toolExecutionPolicy == ToolExecutionPolicy.Unrestricted ? "danger-full-access" : "workspace-write",
            "never",
            CapabilityAdapter.ToCodexConfig(capability))));
    var executionAuthority = toolExecutionPolicy == ToolExecutionPolicy.Unrestricted
        ? SandboxAuthorityDiagnostics.RequireUnrestricted(effectiveConfig, threadResponse)
        : SandboxAuthorityDiagnostics.Build(effectiveConfig, threadResponse);
    var threadId = ExtractThreadId(threadResponse);
    const string liveCommand = "cmd.exe /d /c ver";
    var prompt = $"Use exec_command with this exact Windows command and no PowerShell: {liveCommand}. Do not substitute another shell or command. Then report whether it succeeded.";
    await client.RequestResultAsync(AppServerProtocol.BuildTurnStartRequest(
        4,
        new TurnStartOptions(threadId, prompt, toolExecutionPolicy)));
    var observation = await client.WaitForTurnCompletionAsync(TimeSpan.FromMinutes(10));
    await client.DisposeAsync();
    var sessionJournalRead = SessionJournalEvidenceReader.ReadToolEvidence(layout.CodexHome);
    var sessionEvidence = sessionJournalRead.Evidence;
    var notificationJson = observation.Notifications.Select(item => item.Params.ToJsonString()).ToArray();
    var sessionToolCallObserved = sessionEvidence["functionCallObserved"]?.GetValue<bool>() == true;
    var sessionToolOutputObserved = sessionEvidence["functionCallOutputObserved"]?.GetValue<bool>() == true;
    var sessionToolOutput = sessionEvidence["functionCallOutput"]?.GetValue<string>() ?? string.Empty;
    var ordinaryToolObserved = sessionToolCallObserved || notificationJson.Any(item => item.Contains("exec_command", StringComparison.Ordinal));
    var forbiddenFullHostObserved = notificationJson.Any(item => item.Contains("thread/shellCommand", StringComparison.Ordinal));
    var toolFailureObserved = sessionToolOutput.Contains("exec_command failed", StringComparison.OrdinalIgnoreCase)
        || sessionToolOutput.Contains("blocked by policy", StringComparison.OrdinalIgnoreCase)
        || sessionToolOutput.Contains("rejected", StringComparison.OrdinalIgnoreCase)
        || notificationJson.Any(item => item.Contains("exec_command failed", StringComparison.OrdinalIgnoreCase) || item.Contains("rejected", StringComparison.OrdinalIgnoreCase));
    var effectiveConfigObject = effectiveConfig["config"] as JsonObject ?? effectiveConfig;
    var expectedContext = CapabilityAdapter.ToCodexConfig(capability).TryGetValue("model_context_window", out var context) ? context : null;
    var effectiveModel = effectiveConfigObject["model"]?.GetValue<string>();
    var effectiveProvider = effectiveConfigObject["model_provider"]?.GetValue<string>();
    var configuredContext = effectiveConfigObject["model_context_window"]?.GetValue<int>();
    var sessionContext = sessionEvidence["modelContextWindow"]?.GetValue<int>();
    var catalog = CodexModelCatalogBuilder.Build(capability);
    var catalogModel = catalog["models"]?.AsArray().FirstOrDefault()?.AsObject();
    var catalogContext = catalogModel?["context_window"]?.GetValue<int>();
    var catalogEffectivePercent = catalogModel?["effective_context_window_percent"]?.GetValue<int>();
    var expectedEffectiveContext = catalogContext is int catalogContextValue && catalogEffectivePercent is int percent
        ? (int?)(catalogContextValue * percent / 100)
        : null;
    var configuredContextObserved = expectedContext is int expectedContextValue && configuredContext == expectedContextValue;
    var effectiveContextObserved = expectedEffectiveContext is int expectedEffectiveContextValue && sessionContext == expectedEffectiveContextValue;
    var effectiveModelMatches = effectiveModel == capability.ModelIdentifier;
    var effectiveProviderMatches = effectiveProvider == capability.ProviderId;
    var codexModelMetadataFallbackObserved = notificationJson.Any(item =>
        item.Contains("Model metadata", StringComparison.OrdinalIgnoreCase)
        && item.Contains("fallback metadata", StringComparison.OrdinalIgnoreCase));
    var modelMatches = threadResponse["model"]?.GetValue<string>() == capability.ModelIdentifier;
    var providerMatches = threadResponse["modelProvider"]?.GetValue<string>() == capability.ProviderId;
    return new JsonObject
    {
        ["attributedTo"] = ordinaryToolObserved
            ? openRouterMode
                ? "Codex App Server → OpenRouter requested model; actual endpoint provider not attested by the observed response"
                : "Codex App Server → LM Studio → selected real model"
            : "NeoBabylon.Phase1AQualification",
        ["actualResponseProviderAttestation"] = openRouterMode
            ? "Unknown; observed Responses stream did not identify an independently verifiable provider endpoint"
            : null,
        ["completed"] = observation.Completed,
        ["toolRoundTrip"] = ordinaryToolObserved && sessionToolOutputObserved && !forbiddenFullHostObserved && !toolFailureObserved,
        ["toolRoundTripCompleted"] = ordinaryToolObserved && sessionToolOutputObserved,
        ["toolRoundTripTurnCompleted"] = observation.Completed && ordinaryToolObserved && sessionToolOutputObserved,
        ["ordinaryToolObserved"] = ordinaryToolObserved,
        ["toolCommand"] = liveCommand,
        ["toolFailureObserved"] = toolFailureObserved,
        ["toolOutput"] = sessionToolOutput,
        ["forbiddenFullHostObserved"] = forbiddenFullHostObserved,
        ["failure"] = observation.Failure,
        ["sessionEvidenceReadFailure"] = sessionJournalRead.Failure is null
            ? null
            : new JsonObject
            {
                ["attributedTo"] = "NeoBabylon.Phase1AQualification",
                ["type"] = sessionJournalRead.Failure.GetType().FullName,
                ["message"] = sessionJournalRead.Failure.Message
            },
        ["notificationMethods"] = NotificationMethods(observation),
        ["interestingNotifications"] = InterestingNotifications(observation),
        ["sessionToolEvidence"] = sessionEvidence,
        ["threadStart"] = threadResponse,
        ["isolatedConfigPath"] = configPath,
        ["modelCatalogPath"] = Path.Combine(Path.GetDirectoryName(configPath)!, "model-catalog.json"),
        ["codexModelCatalog"] = catalog,
        ["executionAuthority"] = executionAuthority,
        ["effectiveConfig"] = effectiveConfig,
        ["capabilityConfigSent"] = JsonSerializer.SerializeToNode(CapabilityAdapter.ToCodexConfig(capability), QualificationJson.Options),
        ["codexModelMatchesProviderRecord"] = modelMatches,
        ["codexProviderMatchesProviderRecord"] = providerMatches,
        ["codexEffectiveModel"] = effectiveModel,
        ["codexEffectiveProvider"] = effectiveProvider,
        ["codexConfiguredContext"] = configuredContext,
        ["codexEffectiveContext"] = sessionContext,
        ["codexEffectiveModelMatchesProviderRecord"] = effectiveModelMatches,
        ["codexEffectiveProviderMatchesProviderRecord"] = effectiveProviderMatches,
        ["codexConfiguredContextObserved"] = configuredContextObserved,
        ["codexEffectiveContextObserved"] = effectiveContextObserved,
        ["codexEffectiveContextExpected"] = expectedEffectiveContext,
        ["codexModelMetadataFallbackObserved"] = codexModelMetadataFallbackObserved,
        ["providerModelMetadataMismatch"] = !modelMatches || !providerMatches || !effectiveModelMatches || !effectiveProviderMatches || !configuredContextObserved || !effectiveContextObserved || codexModelMetadataFallbackObserved
    };
}

static JsonArray NotificationMethods(TurnObservation observation) =>
    new(observation.Notifications.Select(notification => JsonValue.Create(notification.Method)).ToArray());

static JsonArray InterestingNotifications(TurnObservation observation)
{
    var result = new JsonArray();
    foreach (var notification in observation.Notifications)
    {
        var text = notification.Params.ToJsonString();
        if (text.Contains("exec_command", StringComparison.Ordinal) || text.Contains("function_call", StringComparison.Ordinal) || text.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            result.Add(new JsonObject
            {
                ["method"] = notification.Method,
                ["params"] = notification.Params.DeepClone()
            });
        }
    }

    return result;
}

static string ExtractThreadId(JsonObject result)
{
    var id = result["thread"]?["id"]?.GetValue<string>() ?? result["threadId"]?.GetValue<string>();
    return !string.IsNullOrWhiteSpace(id)
        ? id
        : throw new InvalidDataException("App Server thread/start returned no thread id.");
}

static string ExtractFunctionCallOutput(string body)
{
    try
    {
        var root = JsonNode.Parse(body)?.AsObject();
        var input = root?["input"]?.AsArray();
        var output = input?
            .OfType<JsonObject>()
            .FirstOrDefault(item => item["type"]?.GetValue<string>() == "function_call_output")?
            ["output"];
        return output is null ? string.Empty : output.ToJsonString();
    }
    catch (JsonException)
    {
        return string.Empty;
    }
}

static string LocateSourceRoot()
{
    var configured = Environment.GetEnvironmentVariable("NEOBABYLON_SOURCE_ROOT");
    if (!string.IsNullOrWhiteSpace(configured))
    {
        return Path.GetFullPath(configured);
    }

    var candidates = new[] { new DirectoryInfo(Directory.GetCurrentDirectory()), new DirectoryInfo(AppContext.BaseDirectory) };
    foreach (var initial in candidates)
    {
        for (var current = initial; current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "runtime", "runtime-lock.json")))
            {
                return current.FullName;
            }
        }
    }

    throw new DirectoryNotFoundException("NeoBabylon source root was not found.");
}

static string ReserveDefaultRunRoot(string sourceRoot)
{
    var runsParent = Path.Combine(sourceRoot, ".local", "Lab", "Runs");
    RequireDirectExistingDirectoryPath(runsParent);
    for (var attempt = 0; attempt < 16; attempt++)
    {
        var runId = $"{DateTimeOffset.UtcNow:yyyyMMdd'T'HHmmss'Z'}-{Guid.NewGuid():N}";
        var runRoot = Path.Combine(runsParent, runId);
        if (Directory.Exists(runRoot) || File.Exists(runRoot))
        {
            continue;
        }

        RequireDirectExistingDirectoryPath(runsParent);
        Directory.CreateDirectory(runRoot);
        RequireDirectExistingDirectoryPath(runRoot);
        var claimPath = Path.Combine(runRoot, ".qualification-root-claim");
        try
        {
            using var claim = new FileStream(claimPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            return runRoot;
        }
        catch (IOException) when (File.Exists(claimPath))
        {
            // A concurrent runner claimed this ID first; its App root is not ours.
        }
    }

    throw new IOException("Could not reserve a unique NeoBabylon qualification run root.");
}

static void RequireDirectExistingDirectoryPath(string path)
{
    var fullPath = Path.GetFullPath(path);
    var root = Path.GetPathRoot(fullPath)
        ?? throw new InvalidOperationException("The qualification run root has no filesystem root.");
    var current = root;
    CheckComponent(current);
    foreach (var component in fullPath[root.Length..].Split(
        [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
        StringSplitOptions.RemoveEmptyEntries))
    {
        current = Path.Combine(current, component);
        CheckComponent(current);
    }

    static void CheckComponent(string componentPath)
    {
        var directory = new DirectoryInfo(componentPath);
        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException(
                $"The qualification Lab Runs path must already contain direct directories: {componentPath}");
        }

        if (directory.LinkTarget is not null
            || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                $"The qualification Lab Runs path must not traverse a reparse point: {componentPath}");
        }
    }
}

sealed class LmStudioInspection
{
    private const string SelectedModelKey = "qwen/qwen3-14b";
    private const string ProviderEndpoint = "http://127.0.0.1:1234/v1";
    private JsonObject _apiModel;
    private readonly JsonObject _catalogModel;
    private readonly JsonObject _inspection;

    private LmStudioInspection(JsonObject apiModel, JsonObject catalogModel, JsonObject inspection, string serverVersion)
    {
        _apiModel = apiModel;
        _catalogModel = catalogModel;
        _inspection = inspection;
        ServerVersion = serverVersion;
        Capability = BuildCapability(null);
    }

    public string ServerVersion { get; }
    public JsonObject Inspection => _inspection;
    public ModelCapabilityRecord Capability { get; private set; }

    public static async Task<LmStudioInspection> ReadAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var apiJson = await http.GetStringAsync("http://127.0.0.1:1234/api/v0/models");
        var apiRoot = JsonNode.Parse(apiJson)?.AsObject() ?? throw new InvalidDataException("LM Studio model API returned no object.");
        var apiModels = apiRoot["data"]?.AsArray() ?? throw new InvalidDataException("LM Studio model API returned no data array.");
        var apiModel = apiModels.OfType<JsonObject>().FirstOrDefault(model => model["id"]?.GetValue<string>() == SelectedModelKey)
            ?? throw new InvalidOperationException($"The explicitly selected LM Studio model {SelectedModelKey} is not advertised; no fallback is permitted.");
        if (apiModel["type"]?.GetValue<string>() != "llm" ||
            !(apiModel["capabilities"]?.AsArray().Any(item => item?.GetValue<string>() == "tool_use") ?? false))
        {
            throw new InvalidOperationException("The selected LM Studio model is not an advertised tool-use LLM; no fallback is permitted.");
        }

        var lmsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lmstudio", "bin", "lms.exe");
        var catalogOutput = await QualificationRuntime.RunProcessAsync(lmsPath, ["ls", "--json"], TimeSpan.FromSeconds(30));
        var catalog = JsonNode.Parse(catalogOutput.Stdout)?.AsArray() ?? throw new InvalidDataException("lms ls --json returned no array.");
        var catalogModel = catalog.OfType<JsonObject>().FirstOrDefault(model => model["modelKey"]?.GetValue<string>() == SelectedModelKey)
            ?? throw new InvalidOperationException($"The explicitly selected LM Studio catalog entry {SelectedModelKey} is missing; no fallback is permitted.");

        var lmStudioPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "LM Studio", "LM Studio.exe");
        if (!File.Exists(lmStudioPath) || !File.Exists(lmsPath))
        {
            throw new FileNotFoundException("LM Studio executable or lms CLI is unavailable; no provider substitution is permitted.");
        }

        var fileVersion = FileVersionInfo.GetVersionInfo(lmStudioPath);
        var cliVersion = await QualificationRuntime.RunProcessAsync(lmsPath, ["--version"], TimeSpan.FromSeconds(30));
        var serverStatus = await QualificationRuntime.RunProcessAsync(lmsPath, ["server", "status"], TimeSpan.FromSeconds(30));
        var serverVersion = fileVersion.ProductVersion ?? fileVersion.FileVersion ?? "Unknown";
        var inspection = new JsonObject
        {
            ["provider"] = "lmstudio",
            ["displayName"] = "LM Studio",
            ["serverVersion"] = serverVersion,
            ["fileVersion"] = fileVersion.FileVersion ?? "Unknown",
            ["executable"] = lmStudioPath,
            ["lmsExecutable"] = lmsPath,
            ["lmsCliVersion"] = CombineProcessOutput(cliVersion),
            ["serverStatusEvidence"] = CombineProcessOutput(serverStatus),
            ["modelApiEndpoint"] = "http://127.0.0.1:1234/api/v0/models",
            ["providerEndpoint"] = ProviderEndpoint,
            ["advertisedModel"] = apiModel.DeepClone(),
            ["catalogModel"] = catalogModel.DeepClone(),
            ["reasoningControls"] = "Unknown: not advertised by the inspected LM Studio model metadata",
            ["structuredOutput"] = "Unknown: not advertised by the inspected LM Studio model metadata"
        };
        return new LmStudioInspection(apiModel, catalogModel, inspection, serverVersion);
    }

    private static string CombineProcessOutput((int ExitCode, string Stdout, string Stderr) result) =>
        string.Join(Environment.NewLine, new[] { result.Stdout.Trim(), result.Stderr.Trim() }.Where(value => value.Length > 0));

    public async Task<JsonObject> LoadSelectedModelAsync()
    {
        var lmsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".lmstudio", "bin", "lms.exe");
        var before = await QualificationRuntime.RunProcessAsync(lmsPath, ["ps", "--json"], TimeSpan.FromSeconds(30));
        var beforeModels = JsonNode.Parse(before.Stdout)?.AsArray() ?? new JsonArray();
        var alreadyLoaded = beforeModels.OfType<JsonObject>().FirstOrDefault(model =>
            model["modelKey"]?.GetValue<string>() == SelectedModelKey &&
            model["identifier"]?.GetValue<string>() == "phase1a-qwen3-14b");
        var loadCommand = "already-loaded: phase1a-qwen3-14b";
        var result = (ExitCode: 0, Stdout: "already loaded", Stderr: string.Empty);
        if (alreadyLoaded is null)
        {
            loadCommand = "lms load qwen/qwen3-14b --context-length 32768 --identifier phase1a-qwen3-14b -y";
            result = await QualificationRuntime.RunProcessAsync(
                lmsPath,
                ["load", SelectedModelKey, "--context-length", "32768", "--identifier", "phase1a-qwen3-14b", "-y"],
                TimeSpan.FromMinutes(15));
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"LM Studio refused to load {SelectedModelKey}: {result.Stderr.Trim()}");
            }
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var apiJson = await http.GetStringAsync("http://127.0.0.1:1234/api/v0/models");
        var apiRoot = JsonNode.Parse(apiJson)?.AsObject() ?? throw new InvalidDataException("LM Studio model API returned no object after load.");
        var ps = await QualificationRuntime.RunProcessAsync(lmsPath, ["ps", "--json"], TimeSpan.FromSeconds(30));
        var psNode = JsonNode.Parse(ps.Stdout)?.AsArray() ?? new JsonArray();
        var loaded = psNode.OfType<JsonObject>().FirstOrDefault(model =>
            model["modelKey"]?.GetValue<string>() == SelectedModelKey || model["identifier"]?.GetValue<string>() == "phase1a-qwen3-14b");
        if (loaded is null)
        {
            throw new InvalidDataException("The explicitly selected LM Studio model was not loaded after qualification.");
        }

        var loadedIdentifier = loaded["identifier"]?.GetValue<string>() ?? SelectedModelKey;
        var apiModel = apiRoot["data"]?.AsArray().OfType<JsonObject>().FirstOrDefault(model =>
            model["id"]?.GetValue<string>() == loadedIdentifier && model["state"]?.GetValue<string>() == "loaded")
            ?? throw new InvalidDataException($"LM Studio did not advertise loaded identifier {loadedIdentifier} after load.");
        _apiModel = apiModel;
        return new JsonObject
        {
            ["command"] = loadCommand,
            ["stdout"] = result.Stdout.Trim(),
            ["apiModelAfterLoad"] = apiModel.DeepClone(),
            ["lmsPsAfterLoad"] = loaded?.DeepClone() ?? new JsonObject { ["state"] = "Unknown" },
            ["exitCode"] = result.ExitCode
        };
    }

    public ModelCapabilityRecord RefreshCapability(JsonObject loadResult)
    {
        var loaded = loadResult["lmsPsAfterLoad"] as JsonObject;
        Capability = BuildCapability(loaded);
        return Capability;
    }

    private ModelCapabilityRecord BuildCapability(JsonObject? loaded)
    {
        var capabilities = _apiModel["capabilities"]?.AsArray().Select(item => item?.GetValue<string>()).Where(value => value is not null).ToArray() ?? [];
        var trainedForToolUse = _catalogModel["trainedForToolUse"]?.GetValue<bool>();
        var effectiveContext = GetInt(loaded, "contextLength", "context_length", "loaded_context_length")
            ?? GetInt(_apiModel, "loaded_context_length", "loadedContextLength");
        var advertisedContext = GetInt(_apiModel, "max_context_length", "maxContextLength");
        return new ModelCapabilityRecord(
            "lmstudio",
            "LM Studio",
            ServerVersion,
            ProviderEndpoint,
            _apiModel["id"]?.GetValue<string>() ?? SelectedModelKey,
            _catalogModel["selectedVariant"]?.GetValue<string>() ?? "Unknown",
            _catalogModel["architecture"]?.GetValue<string>() ?? _apiModel["arch"]?.GetValue<string>() ?? "Unknown",
            _catalogModel["paramsString"]?.GetValue<string>() ?? "Unknown",
            _catalogModel["sizeBytes"]?.ToJsonString() ?? "Unknown",
            CapabilityObservation.Known(_catalogModel["quantization"]?["name"]?.GetValue<string>() ?? _apiModel["quantization"]?.GetValue<string>() ?? "Unknown", "LM Studio lms ls --json and /api/v0/models"),
            advertisedContext is int advertised
                ? CapabilityObservation.Known(advertised, "LM Studio GET /api/v0/models:max_context_length")
                : CapabilityObservation.Unknown("LM Studio GET /api/v0/models"),
            effectiveContext is int effective
                ? CapabilityObservation.Known(effective, "LM Studio lms ps --json/API after lms load")
                : CapabilityObservation.Unknown("LM Studio lms ps --json/API after lms load", "The inspected metadata did not expose an effective loaded context field."),
            CapabilityObservation.Unknown("LM Studio /api/v0/models and lms ls --json", "Reasoning controls/levels were not advertised."),
            capabilities.Contains("tool_use", StringComparer.Ordinal)
                ? CapabilityObservation.Known("tool_use", "LM Studio GET /api/v0/models:capabilities")
                : CapabilityObservation.Unknown("LM Studio GET /api/v0/models:capabilities"),
            CapabilityObservation.Unknown("LM Studio /api/v0/models", "Structured-output support was not advertised; it is not inferred from tool use."),
            CapabilityObservation.Known(
                $"type={_apiModel["type"]?.GetValue<string>() ?? "Unknown"}; state={_apiModel["state"]?.GetValue<string>() ?? "Unknown"}; trainedForToolUse={trainedForToolUse?.ToString() ?? "Unknown"}; vision={_catalogModel["vision"]?.GetValue<bool>().ToString() ?? "Unknown"}; capabilities={string.Join(",", capabilities)}",
                "LM Studio GET /api/v0/models and lms ls --json"))
        {
            ApplyPatchToolType = CapabilityObservation.Unknown(
                "LM Studio GET /api/v0/models:capabilities",
                "Generic tool_use was observed; the Codex apply_patch wire format was not advertised or live-qualified.")
        };
    }

    private static int? GetInt(JsonObject? node, params string[] names)
    {
        foreach (var name in names)
        {
            if (node?[name] is JsonValue value && value.TryGetValue<int>(out var result))
            {
                return result;
            }
        }

        return null;
    }
}

sealed class MockResponsesServer : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _loop;
    private readonly List<string> _requestBodies = [];
    private readonly List<int> _responseStatusCodes = [];
    private int _requestCount;
    private readonly int? _providerErrorStatusCode;

    private MockResponsesServer(HttpListener listener, string baseUrl, string command, int? providerErrorStatusCode)
    {
        _listener = listener;
        BaseUrl = baseUrl;
        Command = command;
        _providerErrorStatusCode = providerErrorStatusCode;
        _loop = RunAsync();
    }

    public string BaseUrl { get; }
    private string Command { get; }
    public IReadOnlyList<string> RequestBodies => _requestBodies;
    public IReadOnlyList<int> ResponseStatusCodes => _responseStatusCodes;

    public static async Task<MockResponsesServer> StartAsync(string command, int? providerErrorStatusCode)
    {
        var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        var prefix = $"http://127.0.0.1:{port}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        await Task.Yield();
        return new MockResponsesServer(listener, $"http://127.0.0.1:{port}/v1", command, providerErrorStatusCode);
    }

    private async Task RunAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException) when (_cancellation.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException) when (_cancellation.IsCancellationRequested)
            {
                return;
            }

            await HandleAsync(context).ConfigureAwait(false);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync().ConfigureAwait(false);
        lock (_requestBodies)
        {
            _requestBodies.Add(body);
        }

        if (!string.Equals(context.Request.Url?.AbsolutePath, "/v1/responses", StringComparison.Ordinal) || context.Request.HttpMethod != "POST")
        {
            context.Response.StatusCode = 404;
            context.Response.Close();
            return;
        }

        var requestNumber = Interlocked.Increment(ref _requestCount);
        var statusCode = _providerErrorStatusCode ?? 200;
        lock (_responseStatusCodes)
        {
            _responseStatusCodes.Add(statusCode);
        }

        var response = statusCode == 429
            ? JsonSerializer.Serialize(new
            {
                error = new
                {
                    message = "mock rate limit",
                    type = "rate_limit_error",
                    code = "rate_limit_exceeded"
                }
            })
            : requestNumber == 1
                ? ResponsesSse.FunctionCall("mock-response-1", "mock-call-1", "exec_command", JsonSerializer.Serialize(new { cmd = Command }))
                : ResponsesSse.AssistantMessage("mock-response-2", "mock-message-2", "Mock tool round trip completed.");
        var bytes = Encoding.UTF8.GetBytes(response);
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = statusCode == 429 ? "application/json" : "text/event-stream";
        context.Response.ContentEncoding = Encoding.UTF8;
        context.Response.ContentLength64 = bytes.Length;
        if (statusCode == 429)
        {
            context.Response.Headers[HttpResponseHeader.RetryAfter] = "0";
        }
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.Close();
    }

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        _listener.Stop();
        _listener.Close();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch
        {
        }

        _cancellation.Dispose();
    }
}

static class QualificationRuntime
{
    public static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(timeout);
        await process.WaitForExitAsync(cancellation.Token).ConfigureAwait(false);
        return (process.ExitCode, await stdoutTask.ConfigureAwait(false), await stderrTask.ConfigureAwait(false));
    }
}

static class QualificationJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
