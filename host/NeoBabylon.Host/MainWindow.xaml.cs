using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using NeoBabylon.Core;

namespace NeoBabylon.Host;

public partial class MainWindow : Window
{
    private readonly RuntimeSupervisor _supervisor;
    private readonly string _applicationRoot;
    private readonly SemaphoreSlim _candidateReadGate = new(1, 1);
    private readonly SemaphoreSlim _protectedRecordRecoveryGate = new(1, 1);
    private ModelCapabilityRecord _capability;
    private string _capabilityPath;
    private readonly ProviderCredentialAccess _providerCredentials;

    public MainWindow()
    {
        _providerCredentials = AppServerLaunchEnvironment.CaptureProviderCredentialsAndClear();
        InitializeComponent();
        var sourceRoot = LocateSourceRoot();
        var applicationRoot = Environment.GetEnvironmentVariable("NEOBABYLON_APPLICATION_ROOT")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NeoBabylon");
        _applicationRoot = Path.GetFullPath(applicationRoot);
#if NEOBABYLON_APPROVAL_QA
        _capabilityPath = CapabilityRecordPathResolver.ResolveForApprovalQa(
            sourceRoot,
            Environment.GetEnvironmentVariable("NEOBABYLON_MODEL_CAPABILITY_PATH"),
            Path.Combine(applicationRoot, "Data"));
#else
        _capabilityPath = CapabilityRecordPathResolver.Resolve(
            sourceRoot,
            Environment.GetEnvironmentVariable("NEOBABYLON_MODEL_CAPABILITY_PATH"));
#endif
        _capability = LoadCapabilityFromResolvedPath(_capabilityPath);
#if NEOBABYLON_APPROVAL_QA
        _supervisor = new RuntimeSupervisor(sourceRoot, applicationRoot, TimeSpan.FromSeconds(5));
#else
        _supervisor = new RuntimeSupervisor(
            sourceRoot,
            applicationRoot,
            Environment.GetEnvironmentVariable("NEOBABYLON_WINDOWS_SANDBOX_MODE"),
            null,
            _providerCredentials);
#endif
        _supervisor.ContinuingCommandCompleted += PostContinuingCommandCompleted;
        SourceInitialized += (_, _) => WindowChromeTheme.Apply(new WindowInteropHelper(this).Handle, HostAppearance.Dark);
        Loaded += OnLoadedAsync;
        Closed += OnClosedAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            var webViewOptions = new CoreWebView2EnvironmentOptions();
            if (Environment.GetEnvironmentVariable("NEOBABYLON_ENABLE_WEBVIEW2_REMOTE_DEBUG") == "1"
                && int.TryParse(Environment.GetEnvironmentVariable("NEOBABYLON_WEBVIEW2_DEBUG_PORT"), out var debugPort)
                && debugPort is >= 1024 and <= 65535)
            {
                webViewOptions.AdditionalBrowserArguments = $"--remote-debugging-port={debugPort}";
            }

            var webViewEnvironment = await CoreWebView2Environment.CreateAsync(
                options: webViewOptions,
                userDataFolder: _supervisor.WebView2UserDataFolder);
            await Browser.EnsureCoreWebView2Async(webViewEnvironment);
            Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "neobabylon.local",
                Path.Combine(LocateSourceRoot(), "ui", "diagnostic", "dist"),
                CoreWebView2HostResourceAccessKind.Deny);
            Browser.CoreWebView2.WebMessageReceived += OnWebMessageReceivedAsync;
            Browser.CoreWebView2.NavigationStarting += OnNavigationStarting;
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Browser.Source = new Uri("https://neobabylon.local/index.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "NeoBabylon startup failure", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnWebMessageReceivedAsync(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!IsTrustedUiSource(e.Source))
        {
            return;
        }

        string? rawMessage = null;
        string? requestId = null;
        DiagnosticOperation? parsedOperation = null;
        try
        {
            rawMessage = e.WebMessageAsJson;
            var operation = DiagnosticOperation.Parse(rawMessage);
            parsedOperation = operation;
            requestId = operation.RequestId;
            JsonNode result = operation.Name switch
            {
                "getRuntimeStatus" => _supervisor.GetRuntimeStatus(),
                "listProtectedRecordBackups" => await ListProtectedRecordBackupsAsync(operation),
                "restoreProtectedRecordBackup" => await RestoreProtectedRecordBackupAsync(operation),
                "getDiagnostics" => GetDiagnostics(),
                "setAppearance" => SetAppearance(operation.Payload["appearance"]?.GetValue<string>()),
                "addProject" => await AddProjectAsync(),
                "selectProject" => await _supervisor.SelectProjectAsync(
                    operation.Payload["workspacePath"]?.GetValue<string>()
                        ?? throw new InvalidDataException("A saved NeoBabylon project path is required."),
                    addNew: false,
                    CancellationToken.None),
                "listCapabilities" => GetCapabilities(),
                "listGeneratedToolCandidates" => await ListGeneratedToolCandidatesAsync(),
                "readGeneratedToolCandidateFileRange" => await ReadGeneratedToolCandidateFileRangeAsync(operation),
                "recordGeneratedToolReview" => await RecordGeneratedToolDecisionAsync(operation, reject: false),
                "rejectGeneratedToolCandidate" => await RecordGeneratedToolDecisionAsync(operation, reject: true),
                "listGeneratedToolReviewHistory" => await ListGeneratedToolReviewHistoryAsync(operation),
                "listGeneratedToolPreparedBindingToolIds" => await ListGeneratedToolPreparedBindingToolIdsAsync(),
                "listGeneratedToolPreparedBindingHistory" => await HandleGeneratedToolPreparedBindingAsync(operation),
                "readGeneratedToolPreparedBindingCurrent" => await HandleGeneratedToolPreparedBindingAsync(operation),
                "prepareGeneratedToolDisabledBinding" => await HandleGeneratedToolPreparedBindingAsync(operation),
                "revokeGeneratedToolPreparedBinding" => await HandleGeneratedToolPreparedBindingAsync(operation),
                "cleanupGeneratedToolPreparedBinding" => await HandleGeneratedToolPreparedBindingAsync(operation),
                "stageGeneratedToolDisabledMcp" => await StageGeneratedToolDisabledMcpAsync(operation),
                "getGeneratedToolActivationStatus" => await GetGeneratedToolActivationStatusAsync(operation),
                "activateGeneratedTool" => await ActivateGeneratedToolAsync(operation),
                "revokeGeneratedToolActivation" => await RevokeGeneratedToolActivationAsync(operation),
                "listGeneratedToolActivationHistory" => await ListGeneratedToolActivationHistoryAsync(operation),
                "readGeneratedToolReviewComparison" => await ReadGeneratedToolReviewComparisonAsync(operation),
                "listThreads" => await _supervisor.ListThreadsAsync(
                    _capability,
                    operation.Payload["cursor"]?.GetValue<string>(),
                    CancellationToken.None),
                "selectCapability" => await SelectCapabilityAsync(operation),
                "startThread" => await _supervisor.StartThreadAsync(_capability, CancellationToken.None),
                "resumeThread" => await ResumeSelectedThreadAsync(operation),
                "renameSavedThread" => await _supervisor.RenameSavedThreadAsync(
                    _capability,
                    operation.Payload["threadId"]?.GetValue<string>()
                        ?? throw new InvalidDataException("A saved thread identifier is required."),
                    operation.Payload["name"]?.GetValue<string>()
                        ?? throw new InvalidDataException("A saved thread name is required."),
                    CancellationToken.None),
                "forkThread" => await _supervisor.ForkThreadAsync(
                    _capability,
                    operation.Payload["threadId"]?.GetValue<string>()
                        ?? throw new InvalidDataException("A source thread identifier is required."),
                    CancellationToken.None),
                "readOutputRange" => await _supervisor.ReadOutputRangeAsync(
                    operation.Payload["threadId"]?.GetValue<string>()
                        ?? throw new InvalidDataException("An exact App Server thread identifier is required."),
                    operation.Payload["turnId"]?.GetValue<string>()
                        ?? throw new InvalidDataException("An exact App Server turn identifier is required."),
                    operation.Payload["itemId"]?.GetValue<string>()
                        ?? throw new InvalidDataException("An exact App Server item identifier is required."),
                    operation.Payload["offset"]?.GetValue<int>() ?? 0,
                    CancellationToken.None),
                "newTask" => await _supervisor.NewTaskAsync(CancellationToken.None),
                "getThreadUsage" => _supervisor.GetThreadUsage(operation.Payload["threadId"]!.GetValue<string>()),
                "compactContext" => await _supervisor.CompactContextAsync(
                    operation.Payload["threadId"]!.GetValue<string>(),
                    CancellationToken.None,
                    notification => Post(new JsonObject
                    {
                        ["requestId"] = requestId,
                        ["stream"] = true,
                        ["attributedTo"] = "Codex App Server",
                        ["method"] = notification.Method,
                        ["params"] = notification.Params.DeepClone()
                    }),
                    requestId),
                "startTurn" => await _supervisor.StartTurnAsync(
                    operation.Payload["text"]?.GetValue<string>()
                        ?? throw new InvalidDataException("A user-authored task prompt is required."),
                    CancellationToken.None,
                    notification => Post(new JsonObject
                    {
                        ["requestId"] = requestId,
                        ["stream"] = true,
                        ["attributedTo"] = "Codex App Server",
                        ["method"] = notification.Method,
                        ["params"] = notification.Params.DeepClone()
                    }),
                    requestId,
                    operation.Payload["maxOutputTokens"]?.GetValue<int>(),
                    operation.Payload["reasoningEffort"]?.GetValue<string>()),
                "interruptTurn" => await _supervisor.InterruptTurnAsync(CancellationToken.None),
                "stopCommand" => await _supervisor.StopCommandAsync(
                    operation.Payload["itemId"]?.GetValue<string>()
                        ?? throw new InvalidDataException("An exact App Server command item identifier is required."),
                    CancellationToken.None),
                "respondToApproval" => await _supervisor.RespondToApprovalAsync(
                    operation.Payload["approvalRequestId"]?.GetValue<long>()
                        ?? throw new InvalidDataException("An App Server approval request identifier is required."),
                    operation.Payload["approvalInstanceId"]?.GetValue<string>()
                        ?? throw new InvalidDataException("An App Server approval instance identifier is required."),
                    operation.Payload["decision"]?.GetValue<string>()
                        ?? throw new InvalidDataException("An explicit approval decision is required."),
                    operation.Payload["reviewFingerprint"]?.GetValue<string>(),
                    CancellationToken.None),
                _ => throw new InvalidDataException("Unknown named operation.")
            };
            Post(new JsonObject
            {
                ["requestId"] = requestId,
                ["ok"] = true,
                ["result"] = result
            });
        }
        catch (Exception ex)
        {
            if (rawMessage is not null)
            {
                requestId ??= DiagnosticOperation.RequestIdForError(rawMessage);
            }
            var error = new JsonObject
            {
                ["attributedTo"] = "NeoBabylon.Host",
                ["type"] = ex.GetType().Name,
                ["message"] = ex.Message
            };
            if (ex is GeneratedToolStageException stageError)
            {
                error["code"] = stageError.Kind.ToString();
            }
            else if (parsedOperation is { Name: "getGeneratedToolActivationStatus" or "activateGeneratedTool"
                         or "revokeGeneratedToolActivation" or "listGeneratedToolActivationHistory"
                         or "readGeneratedToolReviewComparison" } generatedToolOperation)
            {
                error["type"] = "GeneratedToolHostOperationFailed";
                error["toolId"] = generatedToolOperation.Payload["toolId"]!.GetValue<string>();
                error["message"] = "The host could not complete this generated-tool operation. Reload status or history before deciding whether to retry.";
                if (ex is GeneratedToolActivationHostException activationError)
                {
                    error["code"] = activationError.Code;
                    error["status"] = activationError.Status;
                    error["toolId"] = activationError.ToolId;
                    if (activationError.FailureKind is { } failureKind)
                    {
                        error["failureKind"] = failureKind;
                    }
                }
                else if (ex is ProtectedRecordHostException busyError)
                {
                    error["code"] = busyError.Code;
                    error["status"] = busyError.Status;
                }
                else if (ex is UnauthorizedAccessException)
                {
                    error["code"] = "generatedToolAccessDenied";
                    error["status"] = "failed";
                }
                else if (ex is InvalidDataException or JsonException)
                {
                    error["code"] = "generatedToolDataInvalid";
                    error["status"] = "failed";
                }
                else if (ex is IOException)
                {
                    error["code"] = "generatedToolStorageFailed";
                    error["status"] = "failed";
                }
                else
                {
                    error["code"] = "generatedToolOperationFailed";
                    error["status"] = "failed";
                }
            }
            else if (ex is ProtectedRecordHostException recoveryError)
            {
                error["code"] = recoveryError.Code;
                error["status"] = recoveryError.Status;
                if (recoveryError.RecordKey is { } recordKey)
                {
                    error["recordKey"] = recordKey.ToString();
                    error["recordLabel"] = recordKey == ProtectedDataRecordKey.ForkBookmarks ? "Fork links" : "Projects";
                }
            }
            else if (ex is ProjectRegistryUnavailableException)
            {
                error["code"] = "projectRegistryRecoveryRequired";
                error["status"] = "blocked";
                error["recordKey"] = "Projects";
                error["recordLabel"] = "Projects";
            }
            else if (ex is ProtectedDataRecordException protectedRecordError)
            {
                error["code"] = protectedRecordError.Operation == ProtectedDataRecordOperation.AcquireLock
                    ? "protectedRecordBusy"
                    : protectedRecordError.RecoveryPending
                        ? "protectedRecordRecoveryPending"
                        : protectedRecordError.FailureKind == ProtectedDataRecordFailureKind.AccessDenied
                            ? "protectedRecordAccessDenied"
                            : "protectedRecordStorageFailure";
                error["status"] = protectedRecordError.Operation == ProtectedDataRecordOperation.AcquireLock
                    ? "busy"
                    : protectedRecordError.RecoveryPending ? "recoveryPending" : "failed";
                error["operation"] = protectedRecordError.Operation.ToString();
                error["failureKind"] = protectedRecordError.FailureKind.ToString();
                error["recordCommitConfirmed"] = protectedRecordError.RecordCommitConfirmed;
                error["recordMayHaveChanged"] = protectedRecordError.RecordMayHaveChanged;
                error["recoveryPending"] = protectedRecordError.RecoveryPending;
                if (parsedOperation is { Name: "listProtectedRecordBackups" or "restoreProtectedRecordBackup" })
                {
                    var recordKey = parsedOperation.Payload["recordKey"]!.GetValue<string>();
                    error["recordKey"] = recordKey;
                    error["recordLabel"] = recordKey == "ForkBookmarks" ? "Fork links" : "Projects";
                }
                error["message"] = protectedRecordError.Message;
            }
            else if (parsedOperation is { Name: "listProtectedRecordBackups" or "restoreProtectedRecordBackup" })
            {
                var recordKey = parsedOperation.Payload["recordKey"]!.GetValue<string>();
                error["recordKey"] = recordKey;
                error["recordLabel"] = recordKey == "ForkBookmarks" ? "Fork links" : "Projects";
                error["refreshAction"] = "listProtectedRecordBackups";
                if (ex is InvalidDataException)
                {
                    error["code"] = "protectedBackupInvalidOrIncompatible";
                    error["status"] = "unsupported";
                }
                else if (ex is InvalidOperationException)
                {
                    error["code"] = "protectedRecordStateChanged";
                    error["status"] = "blocked";
                }
                else if (ex is UnauthorizedAccessException)
                {
                    error["code"] = "protectedRecordAccessDenied";
                    error["status"] = "failed";
                }
                else
                {
                    error["code"] = "protectedRecordOperationFailed";
                    error["status"] = "failed";
                }
            }
            Post(new JsonObject
            {
                ["requestId"] = requestId,
                ["ok"] = false,
                ["error"] = error
            });
        }
    }

    private async Task<JsonObject> ListProtectedRecordBackupsAsync(DiagnosticOperation operation)
    {
        var recordKey = ParseProtectedRecordKey(operation);
        if (!await _protectedRecordRecoveryGate.WaitAsync(0))
        {
            throw new ProtectedRecordHostException(
                "busy", "busy", "Another protected-record recovery operation is in progress.", recordKey);
        }
        try
        {
            return await Task.Run(() => _supervisor.ListProtectedRecordBackups(recordKey));
        }
        finally
        {
            _protectedRecordRecoveryGate.Release();
        }
    }

    private async Task<JsonObject> RestoreProtectedRecordBackupAsync(DiagnosticOperation operation)
    {
        var recordKey = ParseProtectedRecordKey(operation);
        if (!await _protectedRecordRecoveryGate.WaitAsync(0))
        {
            throw new ProtectedRecordHostException(
                "busy", "busy", "Another protected-record recovery operation is in progress.", recordKey);
        }
        try
        {
            var expectedHash = operation.Payload["expectedBackupSha256"]!.GetValue<string>();
            return await Task.Run(() => _supervisor.RestoreProtectedRecordBackup(recordKey, expectedHash));
        }
        finally
        {
            _protectedRecordRecoveryGate.Release();
        }
    }

    private static ProtectedDataRecordKey ParseProtectedRecordKey(DiagnosticOperation operation) =>
        operation.Payload["recordKey"]?.GetValue<string>() switch
        {
            "Projects" => ProtectedDataRecordKey.Projects,
            "ForkBookmarks" => ProtectedDataRecordKey.ForkBookmarks,
            _ => throw new InvalidDataException("Protected-record recovery accepts only Projects or ForkBookmarks.")
        };

    private async Task<JsonObject> GetGeneratedToolActivationStatusAsync(DiagnosticOperation operation)
    {
        var toolId = operation.Payload["toolId"]!.GetValue<string>();
        if (!await _candidateReadGate.WaitAsync(0))
        {
            throw new GeneratedToolActivationHostException(
                "busy", "busy", toolId, "Another generated-tool source decision is in progress.");
        }
        try
        {
            var capability = _capability;
            return await _supervisor.GetGeneratedToolActivationStatusAsync(
                toolId, capability, CancellationToken.None);
        }
        finally
        {
            _candidateReadGate.Release();
        }
    }

    private async Task<JsonObject> ActivateGeneratedToolAsync(DiagnosticOperation operation)
    {
        var toolId = operation.Payload["toolId"]!.GetValue<string>();
        if (!await _candidateReadGate.WaitAsync(0))
        {
            throw new GeneratedToolActivationHostException(
                "busy", "busy", toolId, "Another generated-tool source decision is in progress.");
        }
        try
        {
            var capability = _capability;
            var contentIdentity = operation.Payload["contentIdentity"]!.GetValue<string>();
            var reviewIdentity = operation.Payload["reviewIdentity"]!.GetValue<string>();
            var bindingRecordSha256 = operation.Payload["expectedBindingRecordSha256"]!.GetValue<string>();
            var note = operation.Payload["note"]!.GetValue<string>();
            return await Task.Run(() => _supervisor.ActivateGeneratedTool(
                toolId, contentIdentity, reviewIdentity, bindingRecordSha256, note, capability));
        }
        finally
        {
            _candidateReadGate.Release();
        }
    }

    private Task<JsonObject> RevokeGeneratedToolActivationAsync(DiagnosticOperation operation)
    {
        var toolId = operation.Payload["toolId"]!.GetValue<string>();
        var expectedActivationRecordSha256 = operation.Payload["expectedActivationRecordSha256"]!.GetValue<string>();
        var note = operation.Payload["note"]!.GetValue<string>();
        var capability = _capability;
        return Task.Run(() => _supervisor.RevokeGeneratedToolActivation(
            toolId, expectedActivationRecordSha256, note, capability));
    }

    private Task<JsonObject> ListGeneratedToolActivationHistoryAsync(DiagnosticOperation operation)
    {
        var toolId = operation.Payload["toolId"]!.GetValue<string>();
        return Task.Run(() => _supervisor.ListGeneratedToolActivationHistory(toolId));
    }

    private async Task<JsonObject> ReadGeneratedToolReviewComparisonAsync(DiagnosticOperation operation)
    {
        var toolId = operation.Payload["toolId"]!.GetValue<string>();
        if (!await _candidateReadGate.WaitAsync(0))
        {
            throw new GeneratedToolActivationHostException(
                "busy", "busy", toolId, "Another generated-tool source decision is in progress.");
        }
        try
        {
            var contentIdentity = operation.Payload["contentIdentity"]!.GetValue<string>();
            return await Task.Run(() => _supervisor.ReadGeneratedToolReviewComparison(toolId, contentIdentity));
        }
        finally
        {
            _candidateReadGate.Release();
        }
    }

    private async Task<JsonObject> ListGeneratedToolCandidatesAsync()
    {
        if (!await _candidateReadGate.WaitAsync(0))
        {
            throw new InvalidOperationException("Generated tool candidate listing is already in progress.");
        }
        try
        {
            return await Task.Run(ListGeneratedToolCandidates);
        }
        finally
        {
            _candidateReadGate.Release();
        }
    }

    private async Task<JsonObject> ReadGeneratedToolCandidateFileRangeAsync(DiagnosticOperation operation)
    {
        if (!await _candidateReadGate.WaitAsync(0))
        {
            throw new InvalidOperationException("Generated tool candidate inspection is already in progress.");
        }
        try
        {
            var toolId = operation.Payload["toolId"]!.GetValue<string>();
            var identity = operation.Payload["contentIdentity"]!.GetValue<string>();
            var path = operation.Payload["path"]!.GetValue<string>();
            var offset = operation.Payload["offset"]!.GetValue<int>();
            var range = await Task.Run(() => GeneratedToolCandidateStore.ReadFileRange(
                _applicationRoot, toolId, identity, path, offset));
            return new JsonObject
            {
                ["attributedTo"] = "NeoBabylon.Host",
                ["toolId"] = toolId,
                ["contentIdentity"] = identity,
                ["path"] = path,
                ["offset"] = offset,
                ["text"] = range.Text,
                ["total"] = range.Total,
                ["next"] = range.Next,
                ["hasMore"] = range.HasMore
            };
        }
        finally
        {
            _candidateReadGate.Release();
        }
    }

    private async Task<JsonObject> RecordGeneratedToolDecisionAsync(DiagnosticOperation operation, bool reject)
    {
        if (!await _candidateReadGate.WaitAsync(0))
        {
            throw new InvalidOperationException("Generated tool candidate review is already in progress.");
        }
        try
        {
            var toolId = operation.Payload["toolId"]!.GetValue<string>();
            var identity = operation.Payload["contentIdentity"]!.GetValue<string>();
            var note = operation.Payload["note"]!.GetValue<string>();
            return await Task.Run(() =>
            {
                var record = reject
                    ? GeneratedToolReviewStore.RecordRejection(_applicationRoot, toolId, identity, note)
                    : GeneratedToolReviewStore.RecordReview(_applicationRoot, toolId, identity, note);
                return new JsonObject
                {
                    ["attributedTo"] = "NeoBabylon.Host",
                    ["record"] = JsonSerializer.SerializeToNode(record, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                };
            });
        }
        finally
        {
            _candidateReadGate.Release();
        }
    }

    private async Task<JsonObject> ListGeneratedToolReviewHistoryAsync(DiagnosticOperation operation)
    {
        if (!await _candidateReadGate.WaitAsync(0))
        {
            throw new InvalidOperationException("Generated tool candidate review is already in progress.");
        }
        try
        {
            var toolId = operation.Payload["toolId"]!.GetValue<string>();
            return await Task.Run(() =>
            {
                var history = GeneratedToolReviewStore.History(_applicationRoot, toolId);
                return new JsonObject
                {
                    ["attributedTo"] = "NeoBabylon.Host",
                    ["toolId"] = toolId,
                    ["history"] = JsonSerializer.SerializeToNode(history, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                };
            });
        }
        finally
        {
            _candidateReadGate.Release();
        }
    }

    private async Task<JsonObject> ListGeneratedToolPreparedBindingToolIdsAsync()
    {
        if (!await _candidateReadGate.WaitAsync(0))
        {
            throw new InvalidOperationException("Generated tool candidate operation is already in progress.");
        }
        try
        {
            return await Task.Run(() => new JsonObject
            {
                ["attributedTo"] = "NeoBabylon.Host",
                ["toolIds"] = JsonSerializer.SerializeToNode(
                    GeneratedToolIntegrationStore.ListToolIds(_applicationRoot),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
            });
        }
        finally
        {
            _candidateReadGate.Release();
        }
    }

    private async Task<JsonObject> HandleGeneratedToolPreparedBindingAsync(DiagnosticOperation operation)
    {
        if (!await _candidateReadGate.WaitAsync(0))
        {
            throw new InvalidOperationException("Generated tool candidate operation is already in progress.");
        }
        try
        {
            return await Task.Run(() =>
            {
                var toolId = operation.Payload["toolId"]!.GetValue<string>();
                var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
                if (operation.Name == "listGeneratedToolPreparedBindingHistory")
                {
                    var history = GeneratedToolIntegrationStore.History(_applicationRoot, toolId);
                    return new JsonObject
                    {
                        ["attributedTo"] = "NeoBabylon.Host",
                        ["toolId"] = toolId,
                        ["history"] = JsonSerializer.SerializeToNode(history, jsonOptions)
                    };
                }
                if (operation.Name == "readGeneratedToolPreparedBindingCurrent")
                {
                    var current = GeneratedToolIntegrationStore.ReadCurrent(_applicationRoot, toolId);
                    return new JsonObject
                    {
                        ["attributedTo"] = "NeoBabylon.Host",
                        ["toolId"] = toolId,
                        ["record"] = JsonSerializer.SerializeToNode(current, jsonOptions)
                    };
                }

                var identity = operation.Payload["contentIdentity"]!.GetValue<string>();
                var reviewIdentity = operation.Payload["reviewIdentity"]!.GetValue<string>();
                var note = operation.Payload["note"]!.GetValue<string>();
                var record = operation.Name switch
                {
                    "prepareGeneratedToolDisabledBinding" => GeneratedToolIntegrationStore.PrepareDisabledBinding(
                        _applicationRoot, toolId, identity, reviewIdentity, note),
                    "revokeGeneratedToolPreparedBinding" => GeneratedToolIntegrationStore.Revoke(
                        _applicationRoot, toolId, identity, reviewIdentity,
                        operation.Payload["currentRecordSha256"]!.GetValue<string>(), note),
                    "cleanupGeneratedToolPreparedBinding" => GeneratedToolIntegrationStore.Cleanup(
                        _applicationRoot, toolId, identity, reviewIdentity,
                        operation.Payload["currentRecordSha256"]!.GetValue<string>(), note),
                    _ => throw new InvalidDataException("Unknown prepared binding operation.")
                };
                return new JsonObject
                {
                    ["attributedTo"] = "NeoBabylon.Host",
                    ["record"] = JsonSerializer.SerializeToNode(record, jsonOptions)
                };
            });
        }
        finally
        {
            _candidateReadGate.Release();
        }
    }

    private async Task<JsonObject> StageGeneratedToolDisabledMcpAsync(DiagnosticOperation operation)
    {
        if (!await _candidateReadGate.WaitAsync(0))
        {
            throw new GeneratedToolStageException(GeneratedToolStageFailureKind.Busy,
                "Another generated-tool candidate operation is in progress.");
        }
        try
        {
            var capability = _capability;
            return await Task.Run(() => _supervisor.StageGeneratedToolDisabledMcp(
                capability,
                operation.Payload["toolId"]!.GetValue<string>(),
                operation.Payload["contentIdentity"]!.GetValue<string>(),
                operation.Payload["reviewIdentity"]!.GetValue<string>(),
                operation.Payload["currentRecordSha256"]!.GetValue<string>()));
        }
        finally
        {
            _candidateReadGate.Release();
        }
    }

    private JsonObject ListGeneratedToolCandidates() => GeneratedToolCandidateHostProjection.List(_applicationRoot);

    private JsonObject GetDiagnostics()
    {
        var result = _supervisor.GetRuntimeStatus();
        result["capabilityRecord"] = JsonSerializer.SerializeToNode(
            _capability,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        result["capabilityRecordPath"] = _capabilityPath;
        var selectedProvider = _capability.ProviderId.ToLowerInvariant();
        result["providerCredentialCaptured"] = selectedProvider switch
        {
            "openrouter" => _providerCredentials.HasCredential(ProviderCredentialProvider.OpenRouter),
            "nvidia" => _providerCredentials.HasCredential(ProviderCredentialProvider.Nvidia),
            _ => false
        };
        result["providerTransport"] = selectedProvider switch
        {
            "nvidia" => "Responses via app-private NVIDIA Chat Completions adapter",
            "openrouter" => "Responses via OpenRouter",
            _ => "Provider-native Responses"
        };
        result["providerFallbackAllowed"] = false;
        result["providerToolScope"] = selectedProvider == "nvidia"
            ? "adapter text/function tools only; standalone provider-native web/search disabled; no tool fallback"
            : "unchanged provider capability policy";
        result["nvidiaAdapter"] = _supervisor.GetNvidiaAdapterDiagnostics();
#if NEOBABYLON_APPROVAL_QA
        var appServerIdentity = _supervisor.PinnedAppServerProcessIdentity;
        result["approvalQaAppServerIdentity"] = appServerIdentity is { } identity
            ? new JsonObject
            {
                ["processId"] = identity.ProcessId,
                ["startTimeUtcTicks"] = identity.StartTimeUtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }
            : null;
#endif
        return result;
    }

    private JsonObject SetAppearance(string? value)
    {
        var appearance = HostAppearanceParser.Parse(value);
        if (!WindowChromeTheme.Apply(new WindowInteropHelper(this).Handle, appearance))
        {
            throw new InvalidOperationException("Windows could not apply the selected title-bar appearance.");
        }

        Background = new SolidColorBrush(appearance == HostAppearance.Dark
            ? Color.FromRgb(28, 28, 28)
            : Color.FromRgb(255, 255, 255));
        return new JsonObject { ["appearance"] = appearance == HostAppearance.Dark ? "dark" : "light" };
    }

    private async Task<JsonObject> AddProjectAsync()
    {
        _supervisor.RequireProjectChangeAllowed();
        var dialog = new OpenFolderDialog
        {
            Title = "Add a NeoBabylon project folder",
            Multiselect = false,
            InitialDirectory = Directory.Exists(_supervisor.SelectedWorkspace)
                ? _supervisor.SelectedWorkspace
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) != true)
        {
            return new JsonObject { ["attributedTo"] = "NeoBabylon.Host", ["cancelled"] = true };
        }

        return await _supervisor.SelectProjectAsync(dialog.FolderName, addNew: true, CancellationToken.None);
    }

    private JsonObject GetCapabilities()
    {
        var sourceRoot = LocateSourceRoot();
        var releaseRoot = Path.Combine(sourceRoot, "docs", "release");
        var records = new JsonArray();
        var rejected = new JsonArray();
        foreach (var path in Directory.EnumerateFiles(releaseRoot, "MODEL_CAPABILITY_*.json")
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var resolved = CapabilityRecordPathResolver.Resolve(sourceRoot, path);
                var record = LoadCapability(sourceRoot, resolved);
                if (string.IsNullOrWhiteSpace(record.ModelIdentifier) || record.ModelIdentifier == "Unknown")
                {
                    rejected.Add(Path.GetFileName(path));
                    continue;
                }
                if (!ActiveCapabilitySelection.IsSelectable(record, Path.GetFileName(path))) continue;

                records.Add(new JsonObject
                {
                    ["sourceFile"] = Path.GetFileName(path),
                    ["capabilityRecord"] = JsonSerializer.SerializeToNode(
                        record,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web))
                });
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
            {
                rejected.Add(new JsonObject
                {
                    ["sourceFile"] = Path.GetFileName(path),
                    ["reason"] = ex.GetType().Name
                });
            }
        }

        return new JsonObject
        {
            ["attributedTo"] = "NeoBabylon.Host",
            ["selectedProviderId"] = _capability.ProviderId,
            ["selectedModelIdentifier"] = _capability.ModelIdentifier,
            ["records"] = records,
            ["rejectedRecords"] = rejected
        };
    }

    private async Task<JsonObject> SelectCapabilityAsync(DiagnosticOperation operation)
    {
        var providerId = operation.Payload["providerId"]?.GetValue<string>();
        var modelIdentifier = operation.Payload["modelIdentifier"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(providerId) || string.IsNullOrWhiteSpace(modelIdentifier))
        {
            throw new InvalidDataException("Exact provider and model identifiers are required; no fallback is permitted.");
        }

        var sourceRoot = LocateSourceRoot();
        var releaseRoot = Path.Combine(sourceRoot, "docs", "release");
        foreach (var path in Directory.EnumerateFiles(releaseRoot, "MODEL_CAPABILITY_*.json"))
        {
            var resolved = CapabilityRecordPathResolver.Resolve(sourceRoot, path);
            var record = LoadCapability(sourceRoot, resolved);
            if (!string.Equals(record.ProviderId, providerId, StringComparison.Ordinal)
                || !string.Equals(record.ModelIdentifier, modelIdentifier, StringComparison.Ordinal))
            {
                continue;
            }

            ActiveCapabilitySelection.RequireSelectable(record);
            if (!ActiveCapabilitySelection.IsSelectable(record, Path.GetFileName(path))) continue;
            var switchResult = await _supervisor.SwitchCapabilityForCurrentThreadAsync(
                record,
                operation.Payload["threadId"]?.GetValue<string>(),
                CancellationToken.None);
            _capability = record;
            _capabilityPath = resolved;
            return new JsonObject
            {
                ["attributedTo"] = "NeoBabylon.Host",
                ["selectedProviderId"] = record.ProviderId,
                ["selectedModelIdentifier"] = record.ModelIdentifier,
                ["capabilityRecordPath"] = resolved,
                ["threadId"] = switchResult["threadId"]?.DeepClone(),
                ["threadPreserved"] = switchResult["threadPreserved"]?.DeepClone(),
                ["persistedThreadDataPreserved"] = true,
                ["runtimeSelection"] = switchResult,
                ["capabilityRecord"] = JsonSerializer.SerializeToNode(
                    record,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
            };
        }

        throw new InvalidOperationException("The exact provider/model pair has no canonical capability record under NeoBabylon docs/release.");
    }

    private async Task<JsonObject> ResumeSelectedThreadAsync(DiagnosticOperation operation)
    {
        var result = await _supervisor.ResumeThreadAsync(_capability,
            operation.Payload["threadId"]?.GetValue<string>()
                ?? throw new InvalidDataException("A saved thread identifier is required."), CancellationToken.None);
        if (result["executionEligible"]?.GetValue<bool>() == true && result["capabilityRecord"] is JsonObject record)
        {
            _capability = record.Deserialize<ModelCapabilityRecord>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new InvalidDataException("The exact resumed capability record is unavailable.");
            if (result["capabilityRecordPath"]?.GetValue<string>() is { } path)
                _capabilityPath = CapabilityRecordPathResolver.Resolve(LocateSourceRoot(), path);
        }
        return result;
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        e.Cancel = !IsTrustedUiSource(e.Uri);
    }

    private static bool IsTrustedUiSource(string source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, "neobabylon.local", StringComparison.OrdinalIgnoreCase);

    private void Post(JsonObject message)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => Post(message));
            return;
        }

        Browser.CoreWebView2.PostWebMessageAsJson(message.ToJsonString());
    }

    private void PostContinuingCommandCompleted(string requestId, AppServerNotification notification) => Post(new JsonObject
    {
        ["requestId"] = requestId,
        ["stream"] = true,
        ["attributedTo"] = "Codex App Server",
        ["method"] = notification.Method,
        ["params"] = notification.Params.DeepClone()
    });

    private async void OnClosedAsync(object? sender, EventArgs e)
    {
        _supervisor.ContinuingCommandCompleted -= PostContinuingCommandCompleted;
        await _supervisor.DisposeAsync();
    }

    private static string LocateSourceRoot()
    {
        var configured = Environment.GetEnvironmentVariable("NEOBABYLON_SOURCE_ROOT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && current is not null; i++, current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "runtime", "runtime-lock.json")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("NeoBabylon source root was not found; set NEOBABYLON_SOURCE_ROOT.");
    }

    private static ModelCapabilityRecord LoadCapability(string sourceRoot, string? configuredPath = null)
    {
        var path = CapabilityRecordPathResolver.Resolve(
            sourceRoot,
            configuredPath ?? Environment.GetEnvironmentVariable("NEOBABYLON_MODEL_CAPABILITY_PATH"));
        return LoadCapabilityFromResolvedPath(path);
    }

    private static ModelCapabilityRecord LoadCapabilityFromResolvedPath(string path)
    {
        var record = JsonSerializer.Deserialize<ModelCapabilityRecord>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return record ?? throw new InvalidDataException($"The authoritative model-capability record could not be read: {path}");
    }
}

// Read-only projection of already validated candidate data for the trusted renderer.
public static class GeneratedToolCandidateHostProjection
{
    public static JsonObject List(string applicationRoot)
    {
        var records = new JsonArray();
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var candidate in GeneratedToolCandidateStore.List(applicationRoot))
        {
            if (candidate.State != GeneratedToolCandidateValidator.Unapproved
                || candidate.Manifest.ReviewState != GeneratedToolCandidateValidator.Unapproved)
            {
                throw new InvalidDataException("Generated tool listing contained a candidate outside unapproved review state.");
            }

            var manifest = candidate.Manifest;
            records.Add(new JsonObject
            {
                ["toolId"] = manifest.ToolId,
                ["revision"] = manifest.Revision,
                ["name"] = manifest.Name,
                ["purpose"] = manifest.Purpose,
                ["proposedBehavior"] = manifest.ProposedBehavior,
                ["missingCapability"] = manifest.MissingCapability,
                ["origin"] = JsonSerializer.SerializeToNode(manifest.Origin, jsonOptions),
                ["contract"] = JsonSerializer.SerializeToNode(manifest.Contract, jsonOptions),
                ["permissions"] = JsonSerializer.SerializeToNode(manifest.Authority, jsonOptions),
                ["dependencies"] = JsonSerializer.SerializeToNode(manifest.Dependencies, jsonOptions),
                ["evidence"] = JsonSerializer.SerializeToNode(manifest.Evidence, jsonOptions),
                ["entryPoint"] = manifest.EntryPoint,
                ["files"] = JsonSerializer.SerializeToNode(manifest.Files, jsonOptions),
                ["actualPath"] = candidate.CandidatePath,
                ["contentIdentity"] = candidate.ContentIdentity,
                ["state"] = candidate.State
            });
        }

        return new JsonObject
        {
            ["attributedTo"] = "NeoBabylon.Host",
            ["candidates"] = records
        };
    }
}
