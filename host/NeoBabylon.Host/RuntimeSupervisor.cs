using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NeoBabylon.Core;

namespace NeoBabylon.Host;

public enum GeneratedToolStageFailureKind
{
    Busy,
    AlreadyStaged,
    InvalidBinding,
    WriteFailed,
    RuntimeInventoryUnsafe
}

public sealed class GeneratedToolStageException : Exception
{
    public GeneratedToolStageFailureKind Kind { get; }

    public GeneratedToolStageException(GeneratedToolStageFailureKind kind, string message)
        : base(message) => Kind = kind;
}

public sealed class ProtectedRecordHostException : Exception
{
    public string Code { get; }
    public string Status { get; }
    public ProtectedDataRecordKey? RecordKey { get; }

    public ProtectedRecordHostException(
        string code,
        string status,
        string message,
        ProtectedDataRecordKey? recordKey = null)
        : base(message)
    {
        Code = code;
        Status = status;
        RecordKey = recordKey;
    }
}

public sealed class ProjectRegistryUnavailableException : InvalidOperationException
{
    public ProjectRegistryUnavailableException()
        : base("The saved Projects record is unresolved. Execution and project changes are blocked until verified recovery completes.")
    {
    }
}

public sealed class GeneratedToolActivationHostException : InvalidOperationException
{
    public string Code { get; }
    public string Status { get; }
    public string ToolId { get; }
    public string? FailureKind { get; }

    public GeneratedToolActivationHostException(
        string code,
        string status,
        string toolId,
        string message,
        string? failureKind = null)
        : base(message)
    {
        Code = code;
        Status = status;
        ToolId = toolId;
        FailureKind = failureKind;
    }
}

public sealed class RuntimeSupervisor : IAsyncDisposable
{
    private const int MaximumObservedCommandItemsPerTurn = 256;
    private static readonly Regex TrustedNodeVersionPattern = new(
        "^v[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.-]+)?$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private sealed record GeneratedToolMcpObservation(bool Available, McpServerStatusEntry? Entry);

    private static readonly JsonSerializerOptions HostProjectionJsonOptions = CreateHostProjectionJsonOptions();

    private sealed record ContinuingCommandIdentity(
        string ThreadId,
        string TurnId,
        string ItemId,
        string ProcessId,
        string Command,
        Guid ClientEpoch,
        string? BridgeRequestId = null);

    private readonly string _sourceRoot;
    private readonly string _applicationRoot;
    private readonly RuntimeIdentity _identity;
    private readonly ApplicationRootLayout _layout;
    private readonly RuntimeDataLease _dataLease;
    private readonly WorkspaceProjectRegistry _projectRegistry;
    private readonly string _forkBookmarkPath;
    private readonly string _threadCapabilityBindingRoot;
    private readonly string? _openRouterApiKey;
    private readonly ProviderCredentialAccess? _providerCredentials;
    private NvidiaAdapterProcess? _nvidiaAdapter;
    private readonly ToolExecutionPolicy _toolExecutionPolicy;
    private readonly TimeSpan _approvalTimeout;
    private readonly object _turnStateLock = new();
    // Shared reservation for thread/task, project, protected-record recovery, and future generated-tool lifecycle transitions.
    private readonly Dictionary<(string ThreadId, string ItemId), ContinuingCommandIdentity> _continuingCommands = [];
    private InterruptedCommandCompletionTracker? _commandCompletionTracker;
    private AppServerClient? _client;
    private Guid _clientEpoch;
    private ModelCapabilityRecord? _activeCapability;
    private string? _activeCapabilityRecordSnapshot;
    private string? _activeSessionPath;
    private long _currentContextEvidenceOffset = long.MaxValue;
    private string? _observedReasoningEffort;
    private string? _reasoningEvidenceSource;
    private string? _threadId;
    private bool _threadRequiresResume;
    private string? _activeTurnId;
    private long _turnGeneration;
    private TaskCompletionSource<string?>? _activeTurnReady;
    private sealed class CompactionReservation(Guid clientEpoch, string threadId, TaskCompletionSource<string?> ready)
    {
        public Guid ClientEpoch { get; } = clientEpoch;
        public string ThreadId { get; } = threadId;
        public TaskCompletionSource<string?> Ready { get; } = ready;
        public string? TurnId { get; set; }
    }
    private CompactionReservation? _compactionReservation;
    private bool _threadStartInProgress;
    private DisabledProductMcpConfig? _stagedDisabledProductMcp;
    private EnabledGeneratedToolMcpConfig? _registeredGeneratedToolMcp;
    private string? _disabledGeneratedToolIdAwaitingConfirmation;
    private readonly ProtectedDataRecordRecoveryService _protectedRecordRecoveryService;
    private WorkspaceProjectState? _projectState;
    private Exception? _projectRegistryReadFailure;

    public event Action<string, AppServerNotification>? ContinuingCommandCompleted;

#if NEOBABYLON_APPROVAL_QA
    private (string ThreadId, string ItemId, string ProcessId)? _approvalQaNextFailedCommandStop;

    public (int ProcessId, long StartTimeUtcTicks)? PinnedAppServerProcessIdentity =>
        _client is { HasExited: false } client
            ? (client.ProcessId, client.ProcessStartTimeUtcTicks)
            : null;

    // QA diagnostics only: observe a failed initialize before disposal hides the child process.
    public Action<AppServerClient>? ApprovalQaInitializeFailureObserver { get; set; }

    public void RequireOpenRouterCredentialForTurnForApprovalQaTest(ModelCapabilityRecord capability) =>
        RequireOpenRouterCredentialForTurn(capability);

    public void ReturnFailedForNextCommandStopForApprovalQaTest(
        string threadId,
        string itemId)
    {
        if (string.IsNullOrWhiteSpace(threadId)
            || string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException("The QA typed-failure fixture requires the exact thread and item identity.");
        }

        lock (_turnStateLock)
        {
            if (!_continuingCommands.TryGetValue((threadId, itemId), out var commandIdentity)
                || commandIdentity.ClientEpoch != _clientEpoch
                || !string.Equals(_threadId, threadId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A current host-verified command identity is required for the QA typed-failure fixture.");
            }

            if (_approvalQaNextFailedCommandStop is not null)
            {
                throw new InvalidOperationException("A QA typed-failure fixture is already queued.");
            }

            _approvalQaNextFailedCommandStop = (threadId, itemId, commandIdentity.ProcessId);
        }
    }

    private bool TryReturnQaFailedCommandStop(AppServerRequest request, out JsonObject response)
    {
        (string ThreadId, string ItemId, string ProcessId)? expected;
        lock (_turnStateLock)
        {
            expected = _approvalQaNextFailedCommandStop;
            _approvalQaNextFailedCommandStop = null;
        }

        if (expected is null)
        {
            response = null!;
            return false;
        }

        var identity = expected.Value;
        if (!string.Equals(request.Method, "thread/commandExecution/stop", StringComparison.Ordinal)
            || !string.Equals(request.Params["threadId"]?.GetValue<string>(), identity.ThreadId, StringComparison.Ordinal)
            || !string.Equals(request.Params["itemId"]?.GetValue<string>(), identity.ItemId, StringComparison.Ordinal)
            || !string.Equals(request.Params["expectedProcessId"]?.GetValue<string>(), identity.ProcessId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The QA typed-failure fixture was not bound to the exact command stop request.");
        }

        response = new JsonObject { ["status"] = "failed" };
        return true;
    }

    public void RegisterCommandIdentityForApprovalQaTest(
        string threadId,
        string turnId,
        string itemId,
        string processId,
        string command)
    {
        lock (_turnStateLock)
        {
            if (_client is null || _clientEpoch == Guid.Empty)
            {
                throw new InvalidOperationException("A current QA App Server instance is required to seed a command identity.");
            }

            _continuingCommands[(threadId, itemId)] = new ContinuingCommandIdentity(
                threadId,
                turnId,
                itemId,
                processId,
                command,
                _clientEpoch);
        }
    }

    public void RemoveCommandIdentityForApprovalQaTest(string threadId, string itemId)
    {
        lock (_turnStateLock)
        {
            _continuingCommands.Remove((threadId, itemId));
        }
    }
#endif

    public RuntimeSupervisor(
        string sourceRoot,
        string applicationRoot,
        string? windowsSandboxMode = null,
        string? openRouterApiKey = null,
        ProviderCredentialAccess? providerCredentials = null)
        : this(
            sourceRoot,
            applicationRoot,
            windowsSandboxMode,
            openRouterApiKey,
            providerCredentials,
            ToolExecutionPolicy.Unrestricted,
            TimeSpan.FromMinutes(5))
    {
    }

#if NEOBABYLON_APPROVAL_QA
    internal RuntimeSupervisor(string sourceRoot, string applicationRoot, TimeSpan approvalTimeout)
        : this(
            sourceRoot,
            applicationRoot,
            null,
            null,
            null,
            ToolExecutionPolicy.ApprovalQualification,
            approvalTimeout)
    {
    }
#endif

    private RuntimeSupervisor(
        string sourceRoot,
        string applicationRoot,
        string? windowsSandboxMode,
        string? openRouterApiKey,
        ProviderCredentialAccess? providerCredentials,
        ToolExecutionPolicy toolExecutionPolicy,
        TimeSpan approvalTimeout)
    {
        if (!string.IsNullOrWhiteSpace(windowsSandboxMode))
        {
            throw new InvalidOperationException("NeoBabylon now selects unrestricted tools explicitly. Remove NEOBABYLON_WINDOWS_SANDBOX_MODE; the unqualified Windows sandbox cannot be selected by the desktop host.");
        }

        _sourceRoot = Path.GetFullPath(sourceRoot);
        _applicationRoot = Path.GetFullPath(applicationRoot);
        _toolExecutionPolicy = toolExecutionPolicy;
        _approvalTimeout = approvalTimeout;
        _identity = RuntimeIdentity.LoadVerified(Path.Combine(_sourceRoot, "runtime", "runtime-lock.json"));
        _layout = ApplicationRootLayout.Create(_sourceRoot, _applicationRoot);
        _protectedRecordRecoveryService = new ProtectedDataRecordRecoveryService(_layout.DataRoot);
        _projectRegistry = new WorkspaceProjectRegistry(_layout.DataRoot, _layout.FixtureWorkspace);
        // Claim the app-owned mutable Data root before reading protected records.
        _dataLease = RuntimeDataLease.Acquire(_layout.DataRoot);
        try
        {
            _projectState = _projectRegistry.Read();
        }
        catch (Exception exception) when (IsProjectRegistryRecoveryFailure(exception))
        {
            // Keep the window and fixed-key recovery surface available. Never
            // synthesize a default after a failed read or overwrite the record.
            _projectRegistryReadFailure = exception;
        }
        _forkBookmarkPath = Path.Combine(_layout.DataRoot, "NeoBabylon", "fork-bookmarks.json");
        _threadCapabilityBindingRoot = _layout.DataRoot;
        _openRouterApiKey = string.IsNullOrWhiteSpace(openRouterApiKey) ? null : openRouterApiKey;
        _providerCredentials = providerCredentials;
    }

    public JsonObject GetNvidiaAdapterDiagnostics() => _nvidiaAdapter?.GetDiagnostics()
        ?? new JsonObject { ["status"] = "notStarted" };

    public JsonObject GetRuntimeStatus()
    {
        WorkspaceProjectState? projectState;
        Exception? projectRegistryFailure;
        string? activeThreadId;
        string? activeProviderId;
        string? activeModelIdentifier;
        bool threadRequiresResume;
        lock (_turnStateLock)
        {
            projectState = _projectState;
            projectRegistryFailure = _projectRegistryReadFailure;
            activeThreadId = _threadId;
            activeProviderId = _activeCapability?.ProviderId;
            activeModelIdentifier = _activeCapability?.ModelIdentifier;
            threadRequiresResume = _threadRequiresResume;
        }

        var projects = new JsonArray();
        foreach (var project in projectState?.Projects ?? [])
        {
            projects.Add(new JsonObject
            {
                ["name"] = project.Name,
                ["workspacePath"] = project.WorkspacePath,
                ["available"] = Directory.Exists(project.WorkspacePath)
            });
        }

        return new JsonObject
        {
            ["attributedTo"] = "NeoBabylon.Host",
            ["runtime"] = new JsonObject
            {
                ["kind"] = _identity.SourceRepository,
                ["version"] = _identity.Version,
                ["sourceRef"] = _identity.SourceRef,
                ["sourceRevision"] = _identity.SourceRevision,
                ["binaryPath"] = _identity.BinaryPath,
                ["sha256"] = _identity.Sha256,
                ["protocol"] = _identity.Protocol
            },
            ["sourceRepositoryRoot"] = _layout.SourceRepositoryRoot,
            ["applicationRoot"] = _layout.ApplicationRoot,
            ["dataRoot"] = _layout.DataRoot,
            ["webView2UserDataFolder"] = _layout.WebView2UserDataFolder,
            ["codexHome"] = _layout.CodexHome,
            ["fixtureWorkspace"] = _layout.FixtureWorkspace,
            ["selectedWorkspace"] = projectState?.SelectedWorkspace,
            ["currentThreadId"] = activeThreadId,
            ["currentProviderId"] = activeProviderId,
            ["currentModelIdentifier"] = activeModelIdentifier,
            ["currentThreadRequiresResume"] = threadRequiresResume,
            ["selectedWorkspaceAvailable"] = projectState is null ? null : Directory.Exists(projectState.SelectedWorkspace),
            ["projects"] = projects,
            ["projectRegistryResolved"] = projectState is not null,
            ["executionBlocked"] = projectState is null,
            ["executionBlockedReason"] = projectState is null ? "projectRegistryRecoveryRequired" : null,
            ["recoveryBlock"] = projectState is null
                ? BuildProjectRegistryRecoveryBlock(projectRegistryFailure)
                : null,
            ["ordinaryCodexRootUsed"] = ApplicationRootLayout.IsOrdinaryCodexRoot(_layout.CodexHome),
            ["executionPolicy"] = new JsonObject
            {
                ["requestedToolPolicy"] = _toolExecutionPolicy == ToolExecutionPolicy.ApprovalQualification
                    ? "approval-qualification"
                    : "unrestricted",
                ["requestedWindowsSandboxMode"] = null,
                ["configSandboxMode"] = _toolExecutionPolicy == ToolExecutionPolicy.ApprovalQualification
                    ? "read-only"
                    : "danger-full-access",
                ["approvalPolicy"] = _toolExecutionPolicy == ToolExecutionPolicy.ApprovalQualification
                    ? "on-request"
                    : "never",
                ["containedToolsQualified"] = false,
                ["effectiveSandbox"] = null,
                ["selectionSource"] = _toolExecutionPolicy == ToolExecutionPolicy.ApprovalQualification
                    ? "Separate ApprovalQA build configuration"
                    : "NeoBabylon product policy",
                ["approvalTimeoutSeconds"] = _toolExecutionPolicy == ToolExecutionPolicy.ApprovalQualification
                    ? _approvalTimeout.TotalSeconds
                    : null
            }
        };
    }

    public string WebView2UserDataFolder => _layout.WebView2UserDataFolder;
    public string SelectedWorkspace => RequireProjectState().SelectedWorkspace;

    private WorkspaceProjectState RequireProjectState()
    {
        lock (_turnStateLock)
        {
            return _projectState ?? throw new ProjectRegistryUnavailableException();
        }
    }

    private static bool IsProjectRegistryRecoveryFailure(Exception exception) =>
        exception is IOException or InvalidDataException or System.Text.Json.JsonException
            or ArgumentException or NotSupportedException;

    private static JsonObject BuildProjectRegistryRecoveryBlock(Exception? exception)
    {
        var protectedRecordFailure = exception as ProtectedDataRecordException;
        var failureKind = protectedRecordFailure?.FailureKind.ToString()
            ?? (exception is System.Text.Json.JsonException or InvalidDataException ? "InvalidRecord" : "StorageFailure");
        var code = protectedRecordFailure?.RecoveryPending == true
            ? "projectRecordRecoveryPending"
            : protectedRecordFailure is not null
                ? "projectRecordReadFailed"
                : "projectRecordUnresolved";

        return new JsonObject
        {
            ["code"] = code,
            ["recordKey"] = "Projects",
            ["recordLabel"] = "Projects",
            ["blocksExecution"] = true,
            ["blocksProjectWrites"] = true,
            ["diagnosticType"] = exception?.GetType().Name ?? "Unknown",
            ["failureKind"] = failureKind,
            ["operation"] = protectedRecordFailure?.Operation.ToString(),
            ["message"] = exception?.Message
                ?? "The saved Projects record could not be loaded. No default was substituted; recover only from a verified compatible backup."
        };
    }

    public JsonObject ListProtectedRecordBackups(ProtectedDataRecordKey recordKey)
    {
        var recovery = _protectedRecordRecoveryService.ListVerifiedBackups(recordKey);
        var status = recovery.JournalExists
            ? "recoveryPending"
            : recovery.TargetExists
                ? "recordPresent"
                : recovery.CanRestoreMissing
                    ? "restoreAvailable"
                    : recovery.VerifiedBackups.Count == 0
                        ? "noVerifiedBackups"
                        : recovery.VerifiedBackups.Any(backup => backup.CanRestore)
                            ? "restoreUnavailable"
                            : "noCompatibleBackups";
        WorkspaceProjectState? projectState;
        lock (_turnStateLock) projectState = _projectState;

        return new JsonObject
        {
            ["attributedTo"] = "NeoBabylon.Host",
            ["recordKey"] = recordKey.ToString(),
            ["recordLabel"] = GetProtectedRecordLabel(recordKey),
            ["status"] = status,
            ["liveRecordExists"] = recovery.TargetExists,
            ["pendingJournal"] = recovery.JournalExists,
            ["canRestoreMissing"] = recovery.CanRestoreMissing,
            ["projectRegistryResolved"] = projectState is not null,
            ["backups"] = SerializeProtectedBackups(recovery.VerifiedBackups)
        };
    }

    public JsonObject RestoreProtectedRecordBackup(ProtectedDataRecordKey recordKey, string expectedBackupSha256)
    {
        lock (_turnStateLock)
        {
            if (_activeTurnReady is not null || _threadStartInProgress || _client is not null || _threadId is not null)
            {
                throw new ProtectedRecordHostException(
                    "busy", "busy",
                    "Protected-record recovery is unavailable while an App Server thread, turn, or runtime transition is owned.",
                    recordKey);
            }

            // This is the same reservation used by thread/task/project transitions
            // and by future generated-tool activation/revocation configuration changes.
            _threadStartInProgress = true;
        }

        try
        {
            var current = _protectedRecordRecoveryService.ListVerifiedBackups(recordKey);
            var selected = current.VerifiedBackups.FirstOrDefault(backup =>
                string.Equals(backup.Sha256, expectedBackupSha256, StringComparison.Ordinal));
            if (selected is null)
            {
                throw new ProtectedRecordHostException(
                    "staleBackupHash", "stale",
                    "The selected verified backup is no longer present. Refresh the backup list before retrying.",
                    recordKey);
            }
            if (!selected.CanRestore)
            {
                throw new ProtectedRecordHostException(
                    "backupIncompatible", "unsupported",
                    "The selected backup does not match the current protected-record schema; it was not restored.",
                    recordKey);
            }
            if (current.JournalExists)
            {
                throw new ProtectedRecordHostException(
                    "pendingJournal", "recoveryPending",
                    "A protected-record recovery journal is present. The explicit missing-record restore will not run over it.",
                    recordKey);
            }
            if (current.TargetExists)
            {
                throw new ProtectedRecordHostException(
                    "liveRecordPresent", "blocked",
                    "The protected record already exists. Restore is missing-only and will not overwrite it.",
                    recordKey);
            }
            if (!current.CanRestoreMissing)
            {
                throw new ProtectedRecordHostException(
                    "noCompatibleBackup", "unavailable",
                    "No verified backup is currently eligible for missing-record recovery.",
                    recordKey);
            }

            _protectedRecordRecoveryService.RestoreMissing(recordKey, expectedBackupSha256);
            var projectsReloaded = false;
            if (recordKey == ProtectedDataRecordKey.Projects)
            {
                try
                {
                    var restoredProjects = _projectRegistry.Read();
                    lock (_turnStateLock)
                    {
                        _projectState = restoredProjects;
                        _projectRegistryReadFailure = null;
                    }
                    projectsReloaded = true;
                }
                catch (Exception exception) when (IsProjectRegistryRecoveryFailure(exception))
                {
                    lock (_turnStateLock)
                    {
                        _projectState = null;
                        _projectRegistryReadFailure = exception;
                    }
                }
            }

            return new JsonObject
            {
                ["attributedTo"] = "NeoBabylon.Host",
                ["recordKey"] = recordKey.ToString(),
                ["recordLabel"] = GetProtectedRecordLabel(recordKey),
                ["status"] = projectsReloaded || recordKey != ProtectedDataRecordKey.Projects
                    ? "restored"
                    : "restoredButRecordUnresolved",
                ["restoredBackupSha256"] = expectedBackupSha256,
                ["backupRetained"] = true,
                ["liveRecordExists"] = true,
                ["pendingJournal"] = false,
                ["canRestoreMissing"] = false,
                ["backups"] = SerializeProtectedBackups(current.VerifiedBackups),
                ["projectsReloaded"] = recordKey == ProtectedDataRecordKey.Projects && projectsReloaded,
                ["refreshRequired"] = true,
                ["refreshAction"] = recordKey == ProtectedDataRecordKey.Projects ? "getRuntimeStatus" : "listThreads",
                ["reopenGuidance"] = recordKey == ProtectedDataRecordKey.Projects
                    ? projectsReloaded
                        ? "The Projects record was restored and reloaded. Refresh runtime status and the project list."
                        : "The verified backup was restored and retained, but Projects could not be reloaded. Review runtime recovery status before starting work."
                    : "Refresh Fork links in NeoBabylon to reload the restored record. Normal Codex history is unaffected.",
                ["codexHistoryAffected"] = false,
                ["runtimeStatus"] = GetRuntimeStatus()
            };
        }
        finally
        {
            lock (_turnStateLock)
            {
                _threadStartInProgress = false;
            }
        }
    }

    public async Task<JsonObject> GetGeneratedToolActivationStatusAsync(
        string toolId,
        ModelCapabilityRecord selectedCapability,
        CancellationToken cancellationToken)
    {
        selectedCapability = CapabilityRecordIdentity.Freeze(selectedCapability);
        var observation = await GetGeneratedToolMcpObservationAsync(toolId, cancellationToken)
            .ConfigureAwait(false);
        var nodeRuntime = ResolveTrustedGeneratedToolNodeRuntime(selectedCapability);
        var status = GeneratedToolActivationService.GetGeneratedToolActivationStatus(
            _applicationRoot, toolId, nodeRuntime, _identity, selectedCapability,
            _toolExecutionPolicy, observation.Available ? observation.Entry : null);
        return SerializeHostProjection(status);
    }

    public JsonObject ActivateGeneratedTool(
        string toolId,
        string contentIdentity,
        string reviewIdentity,
        string expectedBindingRecordSha256,
        string note,
        ModelCapabilityRecord selectedCapability)
    {
        ReserveGeneratedToolLifecycleChange("activate");
        try
        {
            selectedCapability = CapabilityRecordIdentity.Freeze(selectedCapability);
            if (_stagedDisabledProductMcp is not null)
            {
                throw new GeneratedToolActivationHostException(
                    "productMcpRouteConflict", "blocked", toolId,
                    "A prepared-disabled MCP route is staged. Clear that host selection before activation.");
            }

            var nodeRuntime = ResolveTrustedGeneratedToolNodeRuntime(selectedCapability);
            var result = GeneratedToolActivationService.ActivateGeneratedTool(
                _applicationRoot, toolId, contentIdentity, reviewIdentity,
                expectedBindingRecordSha256, note, nodeRuntime, _identity, selectedCapability,
                _toolExecutionPolicy, observedMcpStatus: null);

            if (result.Record?.Event == "activated")
            {
                var option = CreateEnabledGeneratedToolMcpConfig(_applicationRoot, result.Status, nodeRuntime, _identity);
                var assessment = CodexConfigBuilder.AssessEnabledGeneratedToolMcp(
                    option, selectedCapability, _toolExecutionPolicy);
                if (!assessment.Enabled)
                {
                    var revoked = GeneratedToolActivationService.RevokeGeneratedToolActivation(
                        _applicationRoot, toolId, result.Record.RecordSha256,
                        "Host rejected generated-tool MCP registration after activation.");
                    WriteDisabledGeneratedToolConfig(selectedCapability);
                    return BuildGeneratedToolCommandResult(
                        revoked.FailureKind ?? assessment.FailureKind ?? "registrationRejected",
                        revoked.Status, revoked.Record,
                        new JsonObject
                        {
                            ["enabled"] = false,
                            ["state"] = "inert",
                            ["inventoryConfirmation"] = assessment.InventoryConfirmation.ToString(),
                            ["blockers"] = JsonSerializer.SerializeToNode(assessment.Blockers)
                        },
                        result.Record);
                }

                try
                {
                    var configPath = CodexConfigBuilder.WriteIsolated(
                        _layout.CodexHome, selectedCapability,
                        toolExecutionPolicy: _toolExecutionPolicy,
                        enabledGeneratedToolMcp: option);
                    return BuildGeneratedToolCommandResult(
                        result.FailureKind, result.Status, result.Record,
                        new JsonObject
                        {
                            ["enabled"] = true,
                            ["state"] = "bootstrapRegistered",
                            ["inventoryConfirmation"] = assessment.InventoryConfirmation.ToString(),
                            ["serverName"] = assessment.ServerName,
                            ["callableTurnsAllowed"] = false,
                            ["configPath"] = configPath
                        });
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                                   or InvalidDataException or InvalidOperationException
                                                   or ArgumentException or JsonException)
                {
                    var revoked = GeneratedToolActivationService.RevokeGeneratedToolActivation(
                        _applicationRoot, toolId, result.Record.RecordSha256,
                        "Host isolated config publication failed; activation was revoked fail-closed.");
                    try { WriteDisabledGeneratedToolConfig(selectedCapability); }
                    catch (Exception cleanupException) when (cleanupException is IOException or UnauthorizedAccessException
                                                              or InvalidDataException or InvalidOperationException
                                                              or ArgumentException or JsonException)
                    {
                        throw new GeneratedToolActivationHostException(
                            "registrationDisableFailed", "blocked", toolId,
                            "Activation was revoked, but the isolated MCP config could not be rewritten. No runtime client was started.",
                            revoked.FailureKind ?? "stale");
                    }
                    throw new GeneratedToolActivationHostException(
                        "registrationFailed", "failed", toolId,
                        "Activation was revoked because isolated MCP config publication failed.",
                        revoked.FailureKind ?? "stale");
                }
            }

            var disabledConfigPath = WriteDisabledGeneratedToolConfig(selectedCapability);
            return BuildGeneratedToolCommandResult(
                result.FailureKind, result.Status, result.Record,
                new JsonObject
                {
                    ["enabled"] = false,
                    ["state"] = "inert",
                    ["configPath"] = disabledConfigPath
                });
        }
        finally
        {
            ReleaseGeneratedToolLifecycleChange();
        }
    }

    public JsonObject RevokeGeneratedToolActivation(
        string toolId,
        string expectedActivationRecordSha256,
        string note,
        ModelCapabilityRecord selectedCapability)
    {
        ReserveGeneratedToolLifecycleChange("revoke");
        try
        {
            // Core deliberately resolves this from append-only activation history
            // only; candidate/review/runtime corruption cannot block recovery revoke.
            var result = GeneratedToolActivationService.RevokeGeneratedToolActivation(
                _applicationRoot, toolId, expectedActivationRecordSha256, note);
            if (result.Record?.Event == "revoked")
            {
                selectedCapability = CapabilityRecordIdentity.Freeze(selectedCapability);
                var configPath = WriteDisabledGeneratedToolConfig(selectedCapability);
                return BuildGeneratedToolCommandResult(result.FailureKind, result.Status, result.Record,
                    new JsonObject
                    {
                        ["enabled"] = false,
                        ["state"] = "inert",
                        ["configPath"] = configPath,
                        ["refreshRequired"] = true,
                        ["refreshAction"] = "startThread"
                    });
            }

            return BuildGeneratedToolCommandResult(
                result.FailureKind, result.Status, result.Record,
                new JsonObject { ["enabled"] = false, ["state"] = "unchanged-stale-request" });
        }
        finally
        {
            ReleaseGeneratedToolLifecycleChange();
        }
    }

    public JsonObject ListGeneratedToolActivationHistory(string toolId)
    {
        var history = GeneratedToolActivationService.ListGeneratedToolActivationHistory(_applicationRoot, toolId);
        return new JsonObject
        {
            ["attributedTo"] = "NeoBabylon.Host",
            ["toolId"] = history.ToolId,
            ["records"] = JsonSerializer.SerializeToNode(
                history.Records, HostProjectionJsonOptions)
        };
    }

    public JsonObject ReadGeneratedToolReviewComparison(string toolId, string contentIdentity)
    {
        var comparison = GeneratedToolReviewStore.ReadComparison(_applicationRoot, toolId, contentIdentity);
        return new JsonObject
        {
            ["attributedTo"] = "NeoBabylon.Host",
            ["comparison"] = JsonSerializer.SerializeToNode(
                comparison, HostProjectionJsonOptions)
        };
    }

    private async Task<McpServerStatusEntry?> GetObservedGeneratedToolMcpStatusAsync(
        string toolId,
        CancellationToken cancellationToken)
    {
        AppServerClient? client;
        string? threadId;
        lock (_turnStateLock)
        {
            if (_threadStartInProgress || _activeTurnReady is not null)
            {
                return null;
            }
            client = _client;
            threadId = _threadId;
        }
        if (client is null || client.HasExited || string.IsNullOrWhiteSpace(threadId)) return null;

        var observation = await ObserveGeneratedToolMcpStatusAsync(
            client, threadId, toolId, cancellationToken).ConfigureAwait(false);
        return observation.Available ? observation.Entry : null;
    }

    private async Task<GeneratedToolMcpObservation> GetGeneratedToolMcpObservationAsync(
        string toolId,
        CancellationToken cancellationToken)
    {
        AppServerClient? client;
        string? threadId;
        lock (_turnStateLock)
        {
            if (_threadStartInProgress || _activeTurnReady is not null)
            {
                return new GeneratedToolMcpObservation(false, null);
            }
            client = _client;
            threadId = _threadId;
        }
        if (client is null || client.HasExited || string.IsNullOrWhiteSpace(threadId))
        {
            return new GeneratedToolMcpObservation(false, null);
        }
        return await ObserveGeneratedToolMcpStatusAsync(client, threadId, toolId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<GeneratedToolMcpObservation> ObserveGeneratedToolMcpStatusAsync(
        AppServerClient client,
        string threadId,
        string toolId,
        CancellationToken cancellationToken)
    {
        if (client.HasExited || string.IsNullOrWhiteSpace(threadId))
        {
            return new GeneratedToolMcpObservation(false, null);
        }

        var expectedServer = GeneratedToolMcpRoute.ServerNameFor(toolId);
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        McpServerStatusEntry? observed = null;
        try
        {
            for (var pageIndex = 0; pageIndex < 8; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = await client.ListMcpServerStatusAsync(
                    threadId, cursor, cancellationToken, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                foreach (var entry in page.Servers.Where(entry =>
                             string.Equals(entry.Name, expectedServer, StringComparison.Ordinal)))
                {
                    if (observed is not null) return new GeneratedToolMcpObservation(false, null);
                    observed = entry;
                }
                if (page.NextCursor is null) return new GeneratedToolMcpObservation(true, observed);
                if (!seenCursors.Add(page.NextCursor)) return new GeneratedToolMcpObservation(false, null);
                cursor = page.NextCursor;
            }
        }
        catch (Exception exception) when (exception is McpControlException or InvalidDataException)
        {
            return new GeneratedToolMcpObservation(false, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new GeneratedToolMcpObservation(false, null);
        }
        return new GeneratedToolMcpObservation(false, null);
    }

    private async Task RequireGeneratedToolMcpCallableForTurnAsync(
        AppServerClient client,
        string threadId,
        ModelCapabilityRecord capability,
        CancellationToken cancellationToken)
    {
        EnabledGeneratedToolMcpConfig? option;
        string? disabledToolId;
        lock (_turnStateLock)
        {
            option = _registeredGeneratedToolMcp;
            disabledToolId = _disabledGeneratedToolIdAwaitingConfirmation;
        }

        if (disabledToolId is not null)
        {
            var disabledObservation = await ObserveGeneratedToolMcpStatusAsync(
                client, threadId, disabledToolId, cancellationToken).ConfigureAwait(false);
            if (!disabledObservation.Available
                || disabledObservation.Entry is not null
                    && disabledObservation.Entry.RuntimeStatus != McpServerRuntimeStatus.Disabled)
            {
                throw new GeneratedToolActivationHostException(
                    "generatedToolDisableUnconfirmed", "blocked", disabledToolId,
                    "The generated-tool MCP config was reloaded, but the current App Server has not confirmed the server disabled. No model turn was sent.");
            }
            lock (_turnStateLock)
            {
                if (_disabledGeneratedToolIdAwaitingConfirmation == disabledToolId)
                    _disabledGeneratedToolIdAwaitingConfirmation = null;
            }
            return;
        }

        if (option is null) return;

        var observation = await ObserveGeneratedToolMcpStatusAsync(
            client, threadId, option.ToolId, cancellationToken).ConfigureAwait(false);
        var currentStatus = GeneratedToolActivationService.GetGeneratedToolActivationStatus(
            _applicationRoot, option.ToolId, option.NodeRuntime, _identity, capability,
            _toolExecutionPolicy, observation.Available ? observation.Entry : null);
        var observedOption = option with
        {
            ObservedMcpStatus = observation.Available ? observation.Entry : null
        };
        var configAssessment = CodexConfigBuilder.AssessEnabledGeneratedToolMcp(
            observedOption, capability, _toolExecutionPolicy);
        if (currentStatus.CallableTurnsAllowed && configAssessment.Enabled)
        {
            return;
        }

        if (currentStatus.InventoryConfirmation is GeneratedToolMcpInventoryConfirmation.UnexpectedTools
            or GeneratedToolMcpInventoryConfirmation.SchemaMismatch
            || configAssessment.InventoryConfirmation is GeneratedToolMcpInventoryConfirmation.UnexpectedTools
                or GeneratedToolMcpInventoryConfirmation.SchemaMismatch)
        {
            await DisableAndRevokeGeneratedToolRouteAsync(
                client, threadId, capability, option, observation, cancellationToken).ConfigureAwait(false);
            throw new GeneratedToolActivationHostException(
                "generatedToolMcpDescriptorMismatch", "unsupported", option.ToolId,
                "The generated-tool MCP descriptors differ from the reviewed exact schemas. Registration was disabled and revoked; the attempted model turn was not replayed.",
                currentStatus.CallableFailureKind ?? "unsupported");
        }

        throw new GeneratedToolActivationHostException(
            "generatedToolMcpQualificationPending", "blocked", option.ToolId,
            "A fresh exact MCP tool descriptor is not available for the active generated-tool route. No model turn was sent.",
            currentStatus.CallableFailureKind ?? "qualificationPending");
    }

    private async Task DisableAndRevokeGeneratedToolRouteAsync(
        AppServerClient client,
        string threadId,
        ModelCapabilityRecord capability,
        EnabledGeneratedToolMcpConfig option,
        GeneratedToolMcpObservation mismatchObservation,
        CancellationToken cancellationToken)
    {
        if (!mismatchObservation.Available || mismatchObservation.Entry is null)
        {
            throw new GeneratedToolActivationHostException(
                "generatedToolMcpDisableUnavailable", "blocked", option.ToolId,
                "The mismatched MCP descriptor was not available for a safe disabled-config publication.");
        }

        var inertOption = option with { ObservedMcpStatus = mismatchObservation.Entry };
        try
        {
            CodexConfigBuilder.WriteIsolated(
                _layout.CodexHome, capability,
                toolExecutionPolicy: _toolExecutionPolicy,
                enabledGeneratedToolMcp: inertOption);
            await client.ReloadMcpServersAsync(cancellationToken, TimeSpan.FromSeconds(10))
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                           or InvalidDataException or InvalidOperationException
                                           or ArgumentException or JsonException or McpControlException)
        {
            throw new GeneratedToolActivationHostException(
                "generatedToolMcpDisableFailed", "blocked", option.ToolId,
                "The mismatched generated-tool MCP route could not be safely disabled and reloaded. No model turn was sent.",
                "unsupported");
        }

        GeneratedToolActivationCommandResult revoke;
        try
        {
            revoke = GeneratedToolActivationService.RevokeGeneratedToolActivation(
                _applicationRoot, option.ToolId, option.ActivationRecordSha256,
                "Host revoked activation after an exact MCP descriptor mismatch.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                           or InvalidDataException or InvalidOperationException
                                           or ArgumentException or JsonException)
        {
            throw new GeneratedToolActivationHostException(
                "generatedToolMcpRevokeFailed", "blocked", option.ToolId,
                "MCP registration was made inert, but append-only activation revocation could not be confirmed. No model turn was sent.",
                "stale");
        }

        if (revoke.Record?.Event != "revoked"
            && !string.Equals(revoke.Status.State, "revoked", StringComparison.Ordinal))
        {
            throw new GeneratedToolActivationHostException(
                "generatedToolMcpRevokeFailed", "blocked", option.ToolId,
                "MCP registration was made inert, but the exact active activation could not be revoked. No model turn was sent.",
                revoke.FailureKind ?? "stale");
        }

        lock (_turnStateLock)
        {
            _registeredGeneratedToolMcp = null;
            _disabledGeneratedToolIdAwaitingConfirmation = option.ToolId;
        }

        var confirmation = await ObserveGeneratedToolMcpStatusAsync(
            client, threadId, option.ToolId, cancellationToken).ConfigureAwait(false);
        if (confirmation.Available
            && (confirmation.Entry is null
                || confirmation.Entry.RuntimeStatus == McpServerRuntimeStatus.Disabled))
        {
            lock (_turnStateLock)
            {
                if (_disabledGeneratedToolIdAwaitingConfirmation == option.ToolId)
                    _disabledGeneratedToolIdAwaitingConfirmation = null;
            }
        }
    }

    private void ReserveGeneratedToolLifecycleChange(string operation)
    {
        lock (_turnStateLock)
        {
            if (_activeTurnReady is not null || _threadStartInProgress || _client is not null || _threadId is not null)
            {
                throw new ProtectedRecordHostException(
                    "busy", "busy",
                    $"Generated-tool {operation} is unavailable while an App Server thread, turn, or runtime transition is owned.");
            }
            _threadStartInProgress = true;
        }
    }

    private void ReleaseGeneratedToolLifecycleChange()
    {
        lock (_turnStateLock)
        {
            _threadStartInProgress = false;
        }
    }

    private static JsonObject SerializeHostProjection<T>(T projection) =>
        JsonSerializer.SerializeToNode(projection, HostProjectionJsonOptions) as JsonObject
            ?? throw new InvalidDataException("NeoBabylon Core returned an unexpected host projection.");

    private static JsonSerializerOptions CreateHostProjectionJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static JsonObject BuildGeneratedToolCommandResult(
        string? failureKind,
        GeneratedToolActivationStatusProjection status,
        GeneratedToolActivationRecord? record,
        JsonObject registration,
        GeneratedToolActivationRecord? priorRecord = null) =>
        new()
        {
            ["attributedTo"] = "NeoBabylon.Host",
            ["failureKind"] = failureKind,
            ["status"] = SerializeHostProjection(status),
            ["record"] = JsonSerializer.SerializeToNode(record, HostProjectionJsonOptions),
            ["registration"] = registration,
            ["priorRecord"] = JsonSerializer.SerializeToNode(priorRecord, HostProjectionJsonOptions)
        };

    private GeneratedToolNodeRuntimeIdentity? ResolveTrustedGeneratedToolNodeRuntime(
        ModelCapabilityRecord selectedCapability)
    {
        // The lock location is source-owned. Its contents cannot redirect the
        // resolver: only the exact app-private relative path for the pinned
        // version is accepted, and the resolver independently hashes node.exe.
        try
        {
            var lockPath = Path.GetFullPath(Path.Combine(
                _sourceRoot, "runtime", "generated-tool-node-lock.json"));
            if (!File.Exists(lockPath)) return null;
            var fileInfo = new FileInfo(lockPath);
            if (fileInfo.Length is <= 0 or > 4096) return null;
            var json = File.ReadAllBytes(lockPath);
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !HasExactObjectProperties(root, "schemaVersion", "kind", "version", "sha256", "applicationRelativePath")
                || root.GetProperty("schemaVersion").ValueKind != JsonValueKind.Number
                || root.GetProperty("schemaVersion").GetInt32() != 1
                || root.GetProperty("kind").ValueKind != JsonValueKind.String
                || root.GetProperty("kind").GetString() != "NodeJS"
                || root.GetProperty("version").ValueKind != JsonValueKind.String
                || root.GetProperty("sha256").ValueKind != JsonValueKind.String
                || root.GetProperty("applicationRelativePath").ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var version = root.GetProperty("version").GetString();
            var sha256 = root.GetProperty("sha256").GetString();
            if (version is null || sha256 is null
                || !TrustedNodeVersionPattern.IsMatch(version)
                || !IsLowercaseSha256(sha256)
                || root.GetProperty("applicationRelativePath").GetString()
                    != $"Runtimes/Node/{version}/node.exe")
            {
                return null;
            }

            return new AppPrivateGeneratedToolNodeRuntimeResolver(version, sha256)
                .Resolve(_applicationRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                          or InvalidDataException or JsonException or ArgumentException
                                          or InvalidOperationException or OverflowException)
        {
            // Missing, malformed, mismatched, or unavailable pins remain an
            // unknown route. No system Node or mutable override is consulted.
            return null;
        }
    }

    private static bool HasExactObjectProperties(JsonElement value, params string[] expected)
    {
        var properties = value.EnumerateObject().ToArray();
        return properties.Length == expected.Length
            && properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() == expected.Length
            && properties.All(property => expected.Contains(property.Name, StringComparer.Ordinal));
    }

    private static bool IsLowercaseSha256(string value) =>
        value.Length == 64
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private EnabledGeneratedToolMcpConfig? FindActiveGeneratedToolMcpConfig(
        ModelCapabilityRecord capability)
    {
        if (_toolExecutionPolicy != ToolExecutionPolicy.Unrestricted || _stagedDisabledProductMcp is not null)
        {
            return null;
        }

        var nodeRuntime = ResolveTrustedGeneratedToolNodeRuntime(capability);
        if (nodeRuntime is null) return null;

        var active = new List<EnabledGeneratedToolMcpConfig>();
        foreach (var toolId in GeneratedToolIntegrationStore.ListToolIds(_applicationRoot))
        {
            var status = GeneratedToolActivationService.GetGeneratedToolActivationStatus(
                _applicationRoot, toolId, nodeRuntime, _identity, capability, _toolExecutionPolicy,
                observedMcpStatus: null);
            if (!string.Equals(status.State, "active", StringComparison.Ordinal)) continue;
            if (status.ContentIdentity is null || status.ReviewIdentity is null
                || status.BindingRecordSha256 is null || status.ActivationRecordSha256 is null)
            {
                throw new InvalidDataException("An active generated-tool record is missing an exact identity.");
            }

            active.Add(new EnabledGeneratedToolMcpConfig(
                _applicationRoot, toolId, status.ContentIdentity, status.ReviewIdentity,
                status.BindingRecordSha256, status.ActivationRecordSha256, nodeRuntime,
                _identity,
                ObservedMcpStatus: null));
        }

        if (active.Count != 1) return null;
        var option = active[0];
        var assessment = CodexConfigBuilder.AssessEnabledGeneratedToolMcp(
            option, capability, _toolExecutionPolicy);
        return assessment.Enabled ? option : null;
    }

    private string WriteDisabledGeneratedToolConfig(ModelCapabilityRecord capability) =>
        CodexConfigBuilder.WriteIsolated(
            _layout.CodexHome,
            capability,
            toolExecutionPolicy: _toolExecutionPolicy);

    private static EnabledGeneratedToolMcpConfig CreateEnabledGeneratedToolMcpConfig(
        string applicationRoot,
        GeneratedToolActivationStatusProjection status,
        GeneratedToolNodeRuntimeIdentity? nodeRuntime,
        RuntimeIdentity appServerRuntimeIdentity)
    {
        if (nodeRuntime is null || status.ContentIdentity is null || status.ReviewIdentity is null
            || status.BindingRecordSha256 is null || status.ActivationRecordSha256 is null)
        {
            throw new InvalidDataException("Core reported an activated route without complete trusted identities.");
        }
        return new EnabledGeneratedToolMcpConfig(
            applicationRoot,
            status.ToolId,
            status.ContentIdentity,
            status.ReviewIdentity,
            status.BindingRecordSha256,
            status.ActivationRecordSha256,
            nodeRuntime,
            appServerRuntimeIdentity,
            ObservedMcpStatus: null);
    }

    private static string GetProtectedRecordLabel(ProtectedDataRecordKey recordKey) => recordKey switch
    {
        ProtectedDataRecordKey.Projects => "Projects",
        ProtectedDataRecordKey.ForkBookmarks => "Fork links",
        _ => throw new ArgumentOutOfRangeException(nameof(recordKey))
    };

    private static JsonArray SerializeProtectedBackups(IReadOnlyList<ProtectedDataBackupMetadata> backups)
    {
        var result = new JsonArray();
        foreach (var backup in backups)
        {
            result.Add(new JsonObject
            {
                ["sha256"] = backup.Sha256,
                ["byteLength"] = backup.ByteLength,
                ["lastWriteTimeUtc"] = backup.LastWriteTimeUtc,
                ["canRestore"] = backup.CanRestore
            });
        }
        return result;
    }

    public void RequireProjectChangeAllowed()
    {
        _ = RequireProjectState();
        lock (_turnStateLock)
        {
            if (_activeTurnReady is not null || _threadStartInProgress)
            {
                throw new InvalidOperationException("Finish the current App Server operation before changing projects.");
            }
        }
    }

    public async Task<JsonObject> SelectProjectAsync(string folder, bool addNew, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = RequireProjectState();
        lock (_turnStateLock)
        {
            if (_activeTurnReady is not null || _threadStartInProgress)
            {
                throw new InvalidOperationException("Finish the current App Server operation before changing projects.");
            }

            _threadStartInProgress = true;
        }

        try
        {
            var candidate = addNew
                ? _projectRegistry.PrepareAddAndSelect(folder)
                : _projectRegistry.PrepareSelectExisting(folder);
            var changed = !string.Equals(RequireProjectState().SelectedWorkspace, candidate.SelectedWorkspace, StringComparison.OrdinalIgnoreCase);
            var previousThreadClosed = changed && _threadId is not null;
            if (changed) await DisposeClientAsync().ConfigureAwait(false);
            _projectRegistry.Save(candidate);
            lock (_turnStateLock)
            {
                _projectState = candidate;
                _projectRegistryReadFailure = null;
            }
            var status = GetRuntimeStatus();
            status["previousThreadClosed"] = previousThreadClosed;
            status["persistedThreadDataPreserved"] = true;
            return status;
        }
        finally
        {
            lock (_turnStateLock)
            {
                _threadStartInProgress = false;
            }
        }
    }

    public async Task<bool> ResetThreadForCapabilityChangeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_turnStateLock)
        {
            if (_activeTurnReady is not null || _threadStartInProgress)
            {
                throw new InvalidOperationException("Finish the current App Server operation before changing the selected model.");
            }

            _threadStartInProgress = true;
        }

        try
        {
            var hadActiveSession = _client is not null;
            await DisposeClientAsync().ConfigureAwait(false);
            return hadActiveSession;
        }
        finally
        {
            lock (_turnStateLock)
            {
                _threadStartInProgress = false;
            }
        }
    }

    public async Task<JsonObject> NewTaskAsync(CancellationToken cancellationToken)
    {
        string? previousThreadId;
        lock (_turnStateLock)
        {
            if (_activeTurnReady is not null || _threadStartInProgress)
            {
                throw new InvalidOperationException("Finish the current App Server operation before starting a new task.");
            }

            _threadStartInProgress = true;
            previousThreadId = _threadId;
        }

        try
        {
            await DisposeClientAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new JsonObject
            {
                ["attributedTo"] = "NeoBabylon.Host",
                ["previousThreadId"] = previousThreadId,
                ["persistedThreadDataPreserved"] = true
            };
        }
        finally
        {
            lock (_turnStateLock)
            {
                _threadStartInProgress = false;
            }
        }
    }

    public async Task<JsonObject> ListThreadsAsync(
        ModelCapabilityRecord capability,
        string? cursor,
        CancellationToken cancellationToken)
    {
        capability = CapabilityRecordIdentity.Freeze(capability);
        _ = RequireProjectState();
        lock (_turnStateLock)
        {
            if (_activeTurnReady is not null || _threadStartInProgress)
            {
                throw new InvalidOperationException("Finish the current App Server operation before refreshing saved threads.");
            }

            _threadStartInProgress = true;
        }

        try
        {
            var pageCursor = cursor is null ? null : ThreadHistoryCursor.Decode(
                cursor, RequireProjectState().SelectedWorkspace, capability.ProviderId, capability.ModelIdentifier);
            var client = await EnsureClientAsync(capability, requireProviderCredential: false, cancellationToken).ConfigureAwait(false);
            var result = await ListThreadsWithStateDbAsync(client, capability, cancellationToken, pageCursor).ConfigureAwait(false);
            var threads = new JsonArray();
            if (result["data"] is JsonArray data)
            {
                foreach (var threadNode in data.OfType<JsonObject>())
                {
                    var projected = new JsonObject
                    {
                        ["id"] = threadNode["id"]?.DeepClone(),
                        ["name"] = threadNode["name"]?.DeepClone(),
                        ["preview"] = threadNode["preview"]?.DeepClone(),
                        ["forkedFromId"] = threadNode["forkedFromId"]?.DeepClone(),
                        ["modelProvider"] = threadNode["modelProvider"]?.DeepClone(),
                        ["model"] = threadNode["model"]?.DeepClone(),
                        ["updatedAt"] = threadNode["updatedAt"]?.DeepClone(),
                        ["status"] = threadNode["status"]?.DeepClone(),
                        ["cwd"] = threadNode["cwd"]?.DeepClone()
                    };
                    var listedThreadId = threadNode["id"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(listedThreadId))
                    {
                        try
                        {
                            var latestSelection = ThreadCapabilityBindingStore.ReadCurrentBinding(
                                _threadCapabilityBindingRoot, listedThreadId);
                            if (latestSelection is { } selected
                                && string.Equals(selected.Workspace, RequireProjectState().SelectedWorkspace, StringComparison.OrdinalIgnoreCase))
                            {
                                projected["modelProvider"] = selected.ProviderId;
                                projected["model"] = selected.ModelIdentifier;
                                projected["capabilityIdentity"] = selected.CapabilityIdentity;
                            }
                        }
                        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
                        {
                            // Keep App Server's observed metadata; a corrupt private sidecar
                            // must not make otherwise readable saved history disappear.
                        }
                    }
                    threads.Add(projected);
                }
            }

            return new JsonObject
            {
                ["attributedTo"] = "Codex App Server",
                ["selectedProviderId"] = capability.ProviderId,
                ["selectedModelIdentifier"] = capability.ModelIdentifier,
                ["threads"] = threads,
                ["nextCursor"] = result["nextCursor"]?.DeepClone(),
                ["historyQuery"] = result["historyQuery"]?.DeepClone()
            };
        }
        finally
        {
            lock (_turnStateLock)
            {
                _threadStartInProgress = false;
            }
        }
    }

    public async Task<JsonObject> SwitchCapabilityForCurrentThreadAsync(
        ModelCapabilityRecord capability,
        string? requestedThreadId,
        CancellationToken cancellationToken)
    {
        capability = CapabilityRecordIdentity.Freeze(capability);
        _ = CapabilitySwitchSafety.GetSelectedDefaultReasoningEffort(capability);
        if (requestedThreadId is not null
            && (string.IsNullOrWhiteSpace(requestedThreadId)
                || requestedThreadId.Length > 512
                || requestedThreadId.Any(char.IsControl)))
        {
            throw new InvalidDataException("The selected saved thread identifier is invalid.");
        }

        var openRouterKey = _providerCredentials?.GetCredential(ProviderCredentialProvider.OpenRouter)
            ?? _openRouterApiKey;
        var nvidiaKey = _providerCredentials?.GetCredential(ProviderCredentialProvider.Nvidia);
        if (string.Equals(capability.ProviderId, "openrouter", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(openRouterKey))
        {
            throw new InvalidOperationException("The selected OpenRouter credential is unavailable; the current conversation was not changed.");
        }
        if (string.Equals(capability.ProviderId, "nvidia", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(nvidiaKey))
        {
            throw new InvalidOperationException("The selected NVIDIA credential is unavailable; the current conversation was not changed.");
        }

        lock (_turnStateLock)
        {
            if (_activeTurnReady is not null || _threadStartInProgress)
            {
                throw new InvalidOperationException("Finish the current App Server operation before changing models.");
            }
            if (_threadId is not null && requestedThreadId is not null
                && !string.Equals(_threadId, requestedThreadId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Model changes apply only to the currently selected conversation.");
            }
            _threadStartInProgress = true;
        }

        var originalThreadId = _threadId;
        var threadId = originalThreadId ?? requestedThreadId;
        var originalCapability = _activeCapability;
        var originalSnapshot = _activeCapabilityRecordSnapshot;
        var originalSessionPath = _activeSessionPath;
        var originalClient = _client is { HasExited: false } liveClient ? liveClient : null;
        var targetAdapter = (NvidiaAdapterProcess?)null;
        var targetInstalled = false;
        try
        {
            if (threadId is null)
            {
                var hadClient = _client is not null;
                await DisposeClientAsync().ConfigureAwait(false);
                return new JsonObject
                {
                    ["attributedTo"] = "NeoBabylon.Host",
                    ["threadId"] = null,
                    ["threadPreserved"] = false,
                    ["runtimeWasReleased"] = hadClient,
                    ["persistedThreadDataPreserved"] = true
                };
            }

            var currentClient = originalClient;
            if (currentClient is null)
            {
                if (string.Equals(capability.ProviderId, "nvidia", StringComparison.OrdinalIgnoreCase))
                {
                    targetAdapter = await NvidiaAdapterProcess.StartAsync(
                        _sourceRoot, _applicationRoot, capability, nvidiaKey!, cancellationToken).ConfigureAwait(false);
                }
                currentClient = await EnsureClientAsync(
                    capability, requireProviderCredential: false, cancellationToken,
                    allowExplicitCapabilitySwitch: true, preparedNvidiaAdapter: targetAdapter).ConfigureAwait(false);
                targetAdapter = null; // EnsureClientAsync now owns and disposes it with the client.
                targetInstalled = true;
            }

            var currentRead = await currentClient.RequestResultAsync(
                id => AppServerProtocol.BuildThreadReadRequest(id, threadId, includeTurns: false),
                cancellationToken).ConfigureAwait(false);
            ThreadRenameIdentity.RequireExactWorkspaceRead(
                currentRead["thread"] as JsonObject, threadId, RequireProjectState().SelectedWorkspace);

            var requestedSnapshot = CapabilityRecordIdentity.Serialize(capability);
            var alreadySelected = string.Equals(_activeCapabilityRecordSnapshot, requestedSnapshot, StringComparison.Ordinal)
                && !_threadRequiresResume
                && ThreadCapabilityBindingStore.MatchesCurrent(
                    _threadCapabilityBindingRoot, threadId, capability, RequireProjectState().SelectedWorkspace);
            if (alreadySelected)
            {
                _threadId = threadId;
                return BuildCapabilitySwitchReceipt(capability, threadId, currentRead["thread"] as JsonObject, []);
            }

            if (originalClient is not null)
            {
                var commandState = await originalClient.RequestResultAsync(
                    id => AppServerProtocol.BuildCommandExecutionListRequest(id, threadId),
                    cancellationToken).ConfigureAwait(false);
                CapabilitySwitchSafety.RequireNoActiveContinuingCommands(commandState);
                lock (_turnStateLock)
                {
                    foreach (var stale in _continuingCommands
                                 .Where(pair => string.Equals(pair.Key.ThreadId, threadId, StringComparison.Ordinal)
                                     && pair.Value.ClientEpoch == _clientEpoch)
                                 .Select(pair => pair.Key).ToArray())
                    {
                        _continuingCommands.Remove(stale);
                    }
                }
            }

            if (originalClient is not null)
            {
                if (string.Equals(capability.ProviderId, "nvidia", StringComparison.OrdinalIgnoreCase))
                {
                    targetAdapter = await NvidiaAdapterProcess.StartAsync(
                        _sourceRoot, _applicationRoot, capability, nvidiaKey!, cancellationToken).ConfigureAwait(false);
                }
                await DisposeClientAsync(preserveSelectedThread: true).ConfigureAwait(false);
            }

            var selectedClient = targetInstalled
                ? currentClient
                : await EnsureClientAsync(
                    capability, requireProviderCredential: false, cancellationToken,
                    allowExplicitCapabilitySwitch: true, preparedNvidiaAdapter: targetAdapter).ConfigureAwait(false);
            targetAdapter = null;
            targetInstalled = true;
            var resumed = await selectedClient.RequestResultAsync(
                id => AppServerProtocol.BuildThreadResumeRequest(
                    id, threadId, ToolExecutionPolicy.Unrestricted, capability),
                cancellationToken).ConfigureAwait(false);
            // thread/resume accepts explicit model/provider overrides, while the
            // returned thread metadata can continue to describe its creation model.
            // The successful typed request confirms the selected tuple; verify the
            // stable thread/workspace identity independently and persist the override.
            var resumedRead = await selectedClient.RequestResultAsync(
                id => AppServerProtocol.BuildThreadReadRequest(id, threadId, includeTurns: false),
                cancellationToken).ConfigureAwait(false);
            ThreadResumeIdentity.RequireExactThreadWorkspace(
                resumedRead["thread"] as JsonObject, threadId, RequireProjectState().SelectedWorkspace);

            var turnsResponse = await selectedClient.RequestResultAsync(
                id => AppServerProtocol.BuildThreadTurnsListRequest(id, threadId), cancellationToken).ConfigureAwait(false);
            var turns = turnsResponse["data"] as JsonArray ?? new JsonArray();
            var transcript = ThreadTranscriptProjector.Project(turns, maxCharacters: 120_000);
            var savedReviews = ThreadSavedChangeProjector.Project(turns, maxCharacters: 250_000);
            var savedOutputs = ThreadSavedOutputProjector.Project(turns);

            ThreadCapabilityBindingStore.RecordExplicitSwitch(
                _threadCapabilityBindingRoot, threadId, RequireProjectState().SelectedWorkspace, capability);
            _threadId = threadId;
            _threadRequiresResume = false;
            _activeCapability = capability;
            _activeCapabilityRecordSnapshot = requestedSnapshot;
            _activeSessionPath = resumedRead["thread"]?["path"]?.GetValue<string>();
            CaptureSelectedThreadUsageState(resumed, "thread/resume");
            return BuildCapabilitySwitchReceipt(capability, threadId, resumedRead["thread"] as JsonObject, transcript.Turns,
                savedReviews, savedOutputs.Outputs, savedOutputs.Truncated, transcript.Truncated);
        }
        catch
        {
            if (targetAdapter is not null)
            {
                await targetAdapter.DisposeAsync().ConfigureAwait(false);
                targetAdapter = null;
            }

            var originalRuntimeStillCurrent = originalClient is not null
                && ReferenceEquals(_client, originalClient)
                && string.Equals(_activeCapabilityRecordSnapshot, originalSnapshot, StringComparison.Ordinal);
            if (!originalRuntimeStillCurrent && originalCapability is not null && threadId is not null)
            {
                try
                {
                    await DisposeClientAsync(preserveSelectedThread: true).ConfigureAwait(false);
                    var rollbackClient = await EnsureClientAsync(
                        originalCapability, requireProviderCredential: false, cancellationToken,
                        allowExplicitCapabilitySwitch: true).ConfigureAwait(false);
                    var rollback = await rollbackClient.RequestResultAsync(
                        id => AppServerProtocol.BuildThreadResumeRequest(
                            id, threadId, ToolExecutionPolicy.Unrestricted, originalCapability),
                        cancellationToken).ConfigureAwait(false);
                    var rollbackIdentity = new JsonObject
                    {
                        ["modelProvider"] = rollback["modelProvider"]?.DeepClone(),
                        ["model"] = rollback["model"]?.DeepClone(),
                        ["cwd"] = rollback["cwd"]?.DeepClone()
                    };
                    ThreadResumeIdentity.RequireExactMatch(
                        rollbackIdentity, originalCapability, RequireProjectState().SelectedWorkspace);
                    _threadId = threadId;
                    _threadRequiresResume = false;
                    _activeCapability = originalCapability;
                    _activeCapabilityRecordSnapshot = originalSnapshot;
                    _activeSessionPath = rollback["thread"]?["path"]?.GetValue<string>() ?? originalSessionPath;
                    CaptureSelectedThreadUsageState(rollback, "thread/resume");
                }
                catch
                {
                    await DisposeClientAsync(preserveSelectedThread: true).ConfigureAwait(false);
                    _threadId = threadId;
                    _threadRequiresResume = true;
                    _activeCapability = originalCapability;
                    _activeCapabilityRecordSnapshot = originalSnapshot;
                    _activeSessionPath = originalSessionPath;
                }
            }
            else if (targetInstalled && originalCapability is null)
            {
                await DisposeClientAsync().ConfigureAwait(false);
                _threadId = originalThreadId;
            }

            throw new InvalidOperationException(
                "The selected model could not be confirmed for this conversation. Its saved history and thread identity were preserved; no new task was started.");
        }
        finally
        {
            lock (_turnStateLock)
            {
                _threadStartInProgress = false;
            }
        }
    }

    private static JsonObject BuildCapabilitySwitchReceipt(
        ModelCapabilityRecord capability,
        string threadId,
        JsonObject? thread,
        JsonArray turns,
        JsonArray? savedReviews = null,
        JsonArray? savedOutputs = null,
        bool savedOutputsTruncated = false,
        bool historyTruncated = false) => new()
    {
        ["attributedTo"] = "Codex App Server",
        ["threadId"] = threadId,
        ["threadPreserved"] = true,
        ["persistedThreadDataPreserved"] = true,
        ["model"] = capability.ModelIdentifier,
        ["modelProvider"] = capability.ProviderId,
        ["thread"] = thread?.DeepClone(),
        ["turns"] = turns,
        ["savedReviews"] = savedReviews?.DeepClone() ?? new JsonArray(),
        ["savedOutputs"] = savedOutputs?.DeepClone() ?? new JsonArray(),
        ["savedOutputsTruncated"] = savedOutputsTruncated,
        ["historyTruncated"] = historyTruncated
    };

    public async Task<JsonObject> RenameSavedThreadAsync(
        ModelCapabilityRecord capability,
        string threadId,
        string name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(threadId) || threadId.Length > 512 || threadId.Any(char.IsControl))
        {
            throw new InvalidDataException("A bounded saved App Server thread identifier is required.");
        }
        var normalizedName = ThreadRenameIdentity.NormalizeName(name);
        var workspace = RequireProjectState().SelectedWorkspace;
        lock (_turnStateLock)
        {
            if (_activeTurnReady is not null || _threadStartInProgress)
            {
                throw new InvalidOperationException("Finish the current App Server operation before renaming a saved thread.");
            }

            _threadStartInProgress = true;
        }

        try
        {
            capability = CapabilityRecordIdentity.Freeze(capability);
            // A live client is already bound to the isolated CodexHome. Reuse it even if
            // the selected capability differs from the history record: rename is metadata,
            // not resume or execution. It does not inspect or rewrite capability bindings.
            var client = _client is { HasExited: false } existingClient
                ? existingClient
                : await EnsureClientAsync(capability, requireProviderCredential: false, cancellationToken).ConfigureAwait(false);
            var read = await client.RequestResultAsync(
                id => AppServerProtocol.BuildThreadReadRequest(id, threadId, includeTurns: false),
                cancellationToken).ConfigureAwait(false);
            var thread = read["thread"] as JsonObject;
            ThreadRenameIdentity.RequireExactWorkspaceRead(thread, threadId, workspace);

            _ = await client.RequestResultAsync(
                id => AppServerProtocol.BuildThreadNameSetRequest(id, threadId, normalizedName),
                cancellationToken).ConfigureAwait(false);
            var confirmedRead = await client.RequestResultAsync(
                id => AppServerProtocol.BuildThreadReadRequest(id, threadId, includeTurns: false),
                cancellationToken).ConfigureAwait(false);
            var confirmedThread = confirmedRead["thread"] as JsonObject;
            ThreadRenameIdentity.RequireExactWorkspaceRead(confirmedThread, threadId, workspace);
            ThreadRenameIdentity.RequireRenameConfirmation(confirmedThread, threadId, normalizedName);

            return new JsonObject
            {
                ["attributedTo"] = "Codex App Server",
                ["threadId"] = threadId,
                ["name"] = normalizedName
            };
        }
        finally
        {
            lock (_turnStateLock)
            {
                _threadStartInProgress = false;
            }
        }
    }

    public Task<JsonObject> ResumeThreadAsync(
        ModelCapabilityRecord capability,
        string threadId,
        CancellationToken cancellationToken) =>
        ResumeThreadCoreAsync(capability, threadId, cancellationToken, reservedByTurn: false);

    private async Task<JsonObject> ResumeThreadCoreAsync(
        ModelCapabilityRecord capability,
        string threadId,
        CancellationToken cancellationToken,
        bool reservedByTurn)
    {
        _ = RequireProjectState();
        if (_toolExecutionPolicy == ToolExecutionPolicy.ApprovalQualification)
        {
            throw new InvalidOperationException("The ApprovalQA host accepts fresh threads only; saved-task resume is disabled.");
        }

        capability = CapabilityRecordIdentity.Freeze(capability);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            throw new InvalidOperationException("A saved App Server thread identifier is required.");
        }

        if (!reservedByTurn)
        {
            lock (_turnStateLock)
            {
                if (_activeTurnReady is not null || _threadStartInProgress)
                {
                    throw new InvalidOperationException("Finish the current App Server operation before resuming a saved thread.");
                }

                _threadStartInProgress = true;
            }
        }

        try
        {
            var (bindingBlockReason, boundCapability) = ResolveCapabilityBinding(
                threadId, capability, RequireProjectState().SelectedWorkspace);
            capability = boundCapability;
            if (bindingBlockReason is null && _client is { HasExited: false } priorClient && _threadId is not null
                && !string.Equals(_activeCapabilityRecordSnapshot, CapabilityRecordIdentity.Serialize(capability), StringComparison.Ordinal))
            {
                var commands = await priorClient.RequestResultAsync(
                    id => AppServerProtocol.BuildCommandExecutionListRequest(id, _threadId), cancellationToken).ConfigureAwait(false);
                CapabilitySwitchSafety.RequireNoActiveContinuingCommands(commands, "opening another saved capability");
            }
            // Resolving a verified historical record changes the private client's
            // configuration, not any saved binding. Missing records remain read-only.
            var client = bindingBlockReason is not null && _client is { HasExited: false } historyClient
                ? historyClient
                : await EnsureClientAsync(capability, requireProviderCredential: false, cancellationToken,
                    allowExplicitCapabilitySwitch: true).ConfigureAwait(false);
            var savedRead = await client.RequestResultAsync(
                id => AppServerProtocol.BuildThreadReadRequest(id, threadId, includeTurns: false),
                cancellationToken).ConfigureAwait(false);
            ThreadRenameIdentity.RequireExactWorkspaceRead(
                savedRead["thread"] as JsonObject, threadId, RequireProjectState().SelectedWorkspace);
            if (bindingBlockReason is not null)
            {
                return await ReadThreadHistoryOnlyAsync(
                    client, savedRead, threadId, bindingBlockReason, cancellationToken).ConfigureAwait(false);
            }

            var effectiveConfig = await client.RequestResultAsync(
                id => AppServerProtocol.BuildConfigReadRequest(id, RequireProjectState().SelectedWorkspace),
                cancellationToken).ConfigureAwait(false);
            var resumed = await client.RequestResultAsync(
                id => AppServerProtocol.BuildThreadResumeRequest(
                    id, threadId, ToolExecutionPolicy.Unrestricted, capability),
                cancellationToken).ConfigureAwait(false);
            var executionAuthority = SandboxAuthorityDiagnostics.RequireUnrestricted(effectiveConfig, resumed);
            var explicitlySwitched = ThreadCapabilityBindingStore.HasExplicitSwitch(_threadCapabilityBindingRoot, threadId);
            JsonObject? resumedReadThread = null;
            if (explicitlySwitched)
            {
                var resumedRead = await client.RequestResultAsync(
                    id => AppServerProtocol.BuildThreadReadRequest(id, threadId, includeTurns: false),
                    cancellationToken).ConfigureAwait(false);
                resumedReadThread = resumedRead["thread"] as JsonObject;
                ThreadResumeIdentity.RequireExactThreadWorkspace(
                    resumedReadThread, threadId, RequireProjectState().SelectedWorkspace);
            }
            else
            {
                var resumedIdentity = new JsonObject
                {
                    ["modelProvider"] = resumed["modelProvider"]?.DeepClone(),
                    ["model"] = resumed["model"]?.DeepClone(),
                    ["cwd"] = resumed["cwd"]?.DeepClone()
                };
                ThreadResumeIdentity.RequireExactMatch(resumedIdentity, capability, RequireProjectState().SelectedWorkspace);
            }
            var mcpConfirmation = await RequireSafeProductInventoryAsync(client, threadId,
                cancellationToken).ConfigureAwait(false);

            var turns = await client.RequestResultAsync(
                id => AppServerProtocol.BuildThreadTurnsListRequest(id, threadId),
                cancellationToken).ConfigureAwait(false);
            var sourceTurns = turns["data"] as JsonArray ?? new JsonArray();
            var transcript = ThreadTranscriptProjector.Project(
                sourceTurns,
                maxCharacters: 120_000);
            var savedReviews = ThreadSavedChangeProjector.Project(sourceTurns, maxCharacters: 250_000);
            var savedOutputs = ThreadSavedOutputProjector.Project(sourceTurns);

            _threadId = threadId;
            _threadRequiresResume = false;
            _activeCapability = capability;
            _activeSessionPath = (resumedReadThread ?? resumed["thread"] as JsonObject)?["path"]?.GetValue<string>();
            CaptureSelectedThreadUsageState(resumed, "thread/resume");
            var response = new JsonObject
            {
                ["attributedTo"] = "Codex App Server",
                ["thread"] = (resumedReadThread ?? resumed["thread"] as JsonObject)?.DeepClone(),
                ["model"] = explicitlySwitched ? JsonValue.Create(capability.ModelIdentifier) : resumed["model"]?.DeepClone(),
                ["modelProvider"] = explicitlySwitched ? JsonValue.Create(capability.ProviderId) : resumed["modelProvider"]?.DeepClone(),
                ["cwd"] = resumed["cwd"]?.DeepClone(),
                ["turns"] = transcript.Turns,
                ["savedReviews"] = savedReviews,
                ["savedOutputs"] = savedOutputs.Outputs,
                ["savedOutputsTruncated"] = savedOutputs.Truncated,
                ["historyTruncated"] = transcript.Truncated,
                ["threadId"] = threadId,
                ["capabilityRecord"] = JsonSerializer.SerializeToNode(capability, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                ["capabilityIdentity"] = CapabilityRecordIdentity.Compute(capability),
                ["capabilityRecordPath"] = CapabilityRecordPathResolver.FindExactRetainedRecord(
                    _sourceRoot, CapabilityRecordIdentity.Compute(capability), capability.ProviderId, capability.ModelIdentifier)?.Path,
                ["executionAuthority"] = executionAuthority,
                ["executionEligible"] = true,
                ["executionBlockedReason"] = null
            };
            if (mcpConfirmation is not null)
            {
                response["generatedToolMcpRuntimeConfirmation"] = mcpConfirmation.Value.ToString();
            }
            return response;
        }
        finally
        {
            if (!reservedByTurn)
            {
                lock (_turnStateLock)
                {
                    _threadStartInProgress = false;
                }
            }
        }
    }

    private async Task<JsonObject> ReadThreadHistoryOnlyAsync(
        AppServerClient client,
        JsonObject savedRead,
        string threadId,
        string bindingBlockReason,
        CancellationToken cancellationToken)
    {
        var turns = await client.RequestResultAsync(
            id => AppServerProtocol.BuildThreadTurnsListRequest(id, threadId),
            cancellationToken).ConfigureAwait(false);
        var sourceTurns = turns["data"] as JsonArray ?? new JsonArray();
        var transcript = ThreadTranscriptProjector.Project(sourceTurns, maxCharacters: 120_000);
        var savedReviews = ThreadSavedChangeProjector.Project(sourceTurns, maxCharacters: 250_000);
        var savedOutputs = ThreadSavedOutputProjector.Project(sourceTurns);
        var thread = savedRead["thread"] as JsonObject
            ?? throw new InvalidDataException("Codex App Server did not return the saved thread metadata.");

        return new JsonObject
        {
            ["attributedTo"] = "Codex App Server",
            ["thread"] = thread.DeepClone(),
            ["modelProvider"] = thread["modelProvider"]?.DeepClone(),
            ["model"] = thread["model"]?.DeepClone(),
            ["cwd"] = thread["cwd"]?.DeepClone(),
            ["turns"] = transcript.Turns,
            ["savedReviews"] = savedReviews,
            ["savedOutputs"] = savedOutputs.Outputs,
            ["savedOutputsTruncated"] = savedOutputs.Truncated,
            ["historyTruncated"] = transcript.Truncated,
            ["threadId"] = threadId,
            ["executionEligible"] = false,
            ["executionBlockedReason"] = bindingBlockReason
        };
    }

    public async Task<JsonObject> ForkThreadAsync(
        ModelCapabilityRecord capability,
        string sourceThreadId,
        CancellationToken cancellationToken)
    {
        _ = RequireProjectState();
        if (_toolExecutionPolicy == ToolExecutionPolicy.ApprovalQualification)
        {
            throw new InvalidOperationException("The ApprovalQA host accepts fresh threads only; saved-task fork is disabled.");
        }

        capability = CapabilityRecordIdentity.Freeze(capability);
        if (string.IsNullOrWhiteSpace(sourceThreadId))
        {
            throw new InvalidOperationException("A source App Server thread identifier is required.");
        }

        lock (_turnStateLock)
        {
            if (_activeTurnReady is not null || _threadStartInProgress)
            {
                throw new InvalidOperationException("Finish the current App Server operation before forking a saved thread.");
            }

            if (!string.Equals(_threadId, sourceThreadId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Only the currently open NeoBabylon thread can be forked.");
            }

            _threadStartInProgress = true;
        }

        try
        {
            var (bindingBlockReason, boundCapability) = ResolveCapabilityBinding(
                sourceThreadId, capability, RequireProjectState().SelectedWorkspace);
            capability = boundCapability;
            if (bindingBlockReason is not null)
            {
                throw new InvalidOperationException(DescribeCapabilityBindingBlock(bindingBlockReason));
            }

            var client = await EnsureClientAsync(capability, requireProviderCredential: false, cancellationToken).ConfigureAwait(false);
            var savedRead = await client.RequestResultAsync(
                id => AppServerProtocol.BuildThreadReadRequest(id, sourceThreadId, includeTurns: false),
                cancellationToken).ConfigureAwait(false);
            ThreadRenameIdentity.RequireExactWorkspaceRead(
                savedRead["thread"] as JsonObject, sourceThreadId, RequireProjectState().SelectedWorkspace);
            var effectiveConfig = await client.RequestResultAsync(
                id => AppServerProtocol.BuildConfigReadRequest(id, RequireProjectState().SelectedWorkspace),
                cancellationToken).ConfigureAwait(false);
            var forked = await client.RequestResultAsync(
                id => AppServerProtocol.BuildThreadForkRequest(id, sourceThreadId, ToolExecutionPolicy.Unrestricted),
                cancellationToken).ConfigureAwait(false);
            var executionAuthority = SandboxAuthorityDiagnostics.RequireUnrestricted(effectiveConfig, forked);
            var forkedThreadId = ThreadForkIdentity.RequireNewExactMatch(
                forked,
                sourceThreadId,
                capability,
                RequireProjectState().SelectedWorkspace);
            var mcpConfirmation = await RequireSafeProductInventoryAsync(client, forkedThreadId,
                cancellationToken).ConfigureAwait(false);
            ThreadCapabilityBindingStore.Bind(
                _threadCapabilityBindingRoot,
                forkedThreadId,
                RequireProjectState().SelectedWorkspace,
                capability);
            var bookmarkPersisted = true;
            string? bookmarkFailure = null;
            try
            {
                ForkBookmarkStore.Record(
                    _forkBookmarkPath,
                    new ForkBookmark(
                        forkedThreadId,
                        sourceThreadId,
                        capability.ProviderId,
                        capability.ModelIdentifier,
                        RequireProjectState().SelectedWorkspace,
                        DateTimeOffset.UtcNow));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                bookmarkPersisted = false;
                bookmarkFailure = exception.Message;
            }

            var turns = await client.RequestResultAsync(
                id => AppServerProtocol.BuildThreadTurnsListRequest(id, forkedThreadId),
                cancellationToken).ConfigureAwait(false);
            var sourceTurns = turns["data"] as JsonArray ?? new JsonArray();
            var transcript = ThreadTranscriptProjector.Project(
                sourceTurns,
                maxCharacters: 120_000);
            var savedReviews = ThreadSavedChangeProjector.Project(sourceTurns, maxCharacters: 250_000);
            var savedOutputs = ThreadSavedOutputProjector.Project(sourceTurns);

            _threadId = forkedThreadId;
            _threadRequiresResume = false;
            _activeCapability = capability;
            _activeSessionPath = forked["thread"]?["path"]?.GetValue<string>();
            CaptureSelectedThreadUsageState(forked, "thread/fork");
            var response = new JsonObject
            {
                ["attributedTo"] = "Codex App Server",
                ["forkedFromThreadId"] = sourceThreadId,
                ["thread"] = forked["thread"]?.DeepClone(),
                ["model"] = forked["model"]?.DeepClone(),
                ["modelProvider"] = forked["modelProvider"]?.DeepClone(),
                ["cwd"] = forked["cwd"]?.DeepClone(),
                ["turns"] = transcript.Turns,
                ["savedReviews"] = savedReviews,
                ["savedOutputs"] = savedOutputs.Outputs,
                ["savedOutputsTruncated"] = savedOutputs.Truncated,
                ["historyTruncated"] = transcript.Truncated,
                ["threadId"] = forkedThreadId,
                ["executionEligible"] = true,
                ["executionBlockedReason"] = null,
                ["executionAuthority"] = executionAuthority,
                ["bookmarkPersisted"] = bookmarkPersisted,
                ["bookmarkFailure"] = bookmarkFailure
            };
            if (mcpConfirmation is not null)
            {
                response["generatedToolMcpRuntimeConfirmation"] = mcpConfirmation.Value.ToString();
            }
            return response;
        }
        finally
        {
            lock (_turnStateLock)
            {
                _threadStartInProgress = false;
            }
        }
    }

    // Selection is process-local and is never an activation or a model-visible tool grant.
    public JsonObject StageGeneratedToolDisabledMcp(ModelCapabilityRecord capability,
        string toolId, string contentIdentity, string reviewIdentity, string currentRecordSha256)
    {
        _ = RequireProjectState();
        lock (_turnStateLock)
        {
            if (_activeTurnReady is not null || _threadStartInProgress || _client is not null || _threadId is not null)
            {
                throw new GeneratedToolStageException(GeneratedToolStageFailureKind.Busy,
                    "Close the current App Server task before staging a disabled binding.");
            }
            if (_stagedDisabledProductMcp is not null)
            {
                throw new GeneratedToolStageException(GeneratedToolStageFailureKind.AlreadyStaged,
                    "A disabled binding is already staged in this host process.");
            }
            _threadStartInProgress = true;
        }

        try
        {
            GeneratedToolPreparedBindingRecord current;
            try
            {
                current = GeneratedToolIntegrationStore.ReadCurrent(_applicationRoot, toolId)
                    ?? throw new InvalidDataException("No current prepared binding exists.");
                if (current.State != GeneratedToolIntegrationStore.PreparedDisabled
                    || current.ActivationState != GeneratedToolIntegrationStore.Disabled
                    || current.CallableRoute != GeneratedToolIntegrationStore.NoCallableRoute
                    || current.ToolId != toolId
                    || current.CandidateContentIdentity != contentIdentity
                    || current.ReviewIdentity != reviewIdentity
                    || current.RecordSha256 != currentRecordSha256)
                {
                    throw new InvalidDataException("The prepared binding identity or state changed.");
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException
                                       or InvalidOperationException or ArgumentException or JsonException)
            {
                throw new GeneratedToolStageException(GeneratedToolStageFailureKind.InvalidBinding,
                    "The current prepared-disabled binding could not be verified.");
            }

            var option = new DisabledProductMcpConfig(_applicationRoot, current,
                Path.Combine(_applicationRoot, "Adapters", "NeoBabylon.GeneratedToolMcp.exe"));
            try
            {
                CodexConfigBuilder.WriteIsolated(_layout.CodexHome,
                    CapabilityRecordIdentity.Freeze(capability), toolExecutionPolicy: _toolExecutionPolicy,
                    disabledProductMcp: option);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException
                                       or InvalidOperationException or ArgumentException or JsonException)
            {
                throw new GeneratedToolStageException(GeneratedToolStageFailureKind.WriteFailed,
                    "The isolated disabled MCP configuration could not be published; no binding was selected.");
            }

            lock (_turnStateLock)
            {
                _stagedDisabledProductMcp = option;
            }
            return new JsonObject
            {
                ["attributedTo"] = "NeoBabylon.Host",
                ["state"] = "staged-disabled",
                ["toolId"] = toolId,
                ["currentRecordSha256"] = current.RecordSha256,
                ["runtimeConfirmation"] = GeneratedToolMcpConfirmation.Unknown.ToString()
            };
        }
        finally
        {
            lock (_turnStateLock)
            {
                _threadStartInProgress = false;
            }
        }
    }

    public async Task<JsonObject> StartThreadAsync(ModelCapabilityRecord capability, CancellationToken cancellationToken)
    {
        capability = CapabilityRecordIdentity.Freeze(capability);
        _ = RequireProjectState();
        lock (_turnStateLock)
        {
            if (_activeTurnReady is not null || _threadStartInProgress)
            {
                throw new InvalidOperationException("The current App Server operation must finish before starting a new thread.");
            }

            _threadStartInProgress = true;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(capability.ModelIdentifier) || capability.ModelIdentifier == "Unknown")
            {
                throw new InvalidOperationException("A verified model identifier is required; no fallback is permitted.");
            }

            if (_toolExecutionPolicy == ToolExecutionPolicy.ApprovalQualification
                && !IsQualificationLoopbackEndpoint(capability))
            {
                throw new InvalidOperationException("ApprovalQA accepts only a local LM Studio capability pointed at a loopback HTTP Responses fixture.");
            }

            await DisposeClientAsync().ConfigureAwait(false);
            var configPath = Path.Combine(_layout.CodexHome, "config.toml");
            var client = await EnsureClientAsync(capability, requireProviderCredential: true, cancellationToken).ConfigureAwait(false);
            var effectiveConfig = await client.RequestResultAsync(
                id => AppServerProtocol.BuildConfigReadRequest(id, RequireProjectState().SelectedWorkspace),
                cancellationToken).ConfigureAwait(false);

            var options = new ThreadStartOptions(
                capability.ModelIdentifier,
                capability.ProviderId,
                RequireProjectState().SelectedWorkspace,
                _toolExecutionPolicy == ToolExecutionPolicy.ApprovalQualification ? "read-only" : "danger-full-access",
                _toolExecutionPolicy == ToolExecutionPolicy.ApprovalQualification ? "on-request" : "never",
                CapabilityAdapter.ToCodexConfig(capability));
            var result = await client.RequestResultAsync(
                id => AppServerProtocol.BuildThreadStartRequest(id, options),
                cancellationToken).ConfigureAwait(false);
            var executionAuthority = _toolExecutionPolicy == ToolExecutionPolicy.ApprovalQualification
                ? SandboxAuthorityDiagnostics.RequireApprovalQualification(effectiveConfig, result)
                : SandboxAuthorityDiagnostics.RequireUnrestricted(effectiveConfig, result);
            var startedThreadId = ExtractThreadId(result);
            ThreadResumeIdentity.RequireExactMatch(result, capability, RequireProjectState().SelectedWorkspace);
            var mcpConfirmation = await RequireSafeProductInventoryAsync(client, startedThreadId,
                cancellationToken).ConfigureAwait(false);
            ThreadCapabilityBindingStore.Bind(
                _threadCapabilityBindingRoot,
                startedThreadId,
                RequireProjectState().SelectedWorkspace,
                capability);
            _threadId = startedThreadId;
            _threadRequiresResume = false;
            _activeCapability = capability;
            _activeSessionPath = result["thread"]?["path"]?.GetValue<string>();
            CaptureSelectedThreadUsageState(result, "thread/start");
            var generatedToolRegistration = await VerifyGeneratedToolRegistrationAfterThreadStartAsync(
                client, startedThreadId, capability, cancellationToken).ConfigureAwait(false);
            var catalogPath = Path.Combine(Path.GetDirectoryName(configPath)!, "model-catalog.json");
            var response = new JsonObject
            {
                ["attributedTo"] = "NeoBabylon.Host",
                ["model"] = capability.ModelIdentifier,
                ["modelProvider"] = capability.ProviderId,
                ["capabilityConfig"] = JsonSerializer.SerializeToNode(CapabilityAdapter.ToCodexConfig(capability)),
                ["isolatedConfigPath"] = configPath,
                ["modelCatalogPath"] = catalogPath,
                ["codexModelCatalog"] = CodexModelCatalogBuilder.Build(capability),
                ["effectiveConfig"] = effectiveConfig,
                ["executionAuthority"] = executionAuthority,
                ["executionEligible"] = true,
                ["executionBlockedReason"] = null,
                ["thread"] = result
            };
            if (generatedToolRegistration is not null)
            {
                response["generatedToolRegistration"] = generatedToolRegistration;
                if (generatedToolRegistration["blocksModelTurns"]?.GetValue<bool>() == true)
                {
                    response["executionEligible"] = false;
                    response["executionBlockedReason"] = "generatedToolMcpQualificationPending";
                }
            }
            if (mcpConfirmation is not null)
            {
                response["generatedToolMcpRuntimeConfirmation"] = mcpConfirmation.Value.ToString();
            }
            return response;
        }
        finally
        {
            lock (_turnStateLock)
            {
                _threadStartInProgress = false;
            }
        }
    }

    private async Task<GeneratedToolMcpConfirmation?> RequireSafeProductInventoryAsync(
        AppServerClient client, string threadId, CancellationToken cancellationToken)
    {
        if (_stagedDisabledProductMcp is null) return null;
        var confirmation = await ConfirmDisabledProductMcpAsync(client, threadId,
            cancellationToken).ConfigureAwait(false);
        if (confirmation == GeneratedToolMcpConfirmation.UnexpectedTools)
        {
            await DisposeClientAsync().ConfigureAwait(false);
            throw new GeneratedToolStageException(GeneratedToolStageFailureKind.RuntimeInventoryUnsafe,
                "The product MCP inventory was not empty; the App Server client was stopped.");
        }
        return confirmation;
    }

    private async Task<JsonObject?> VerifyGeneratedToolRegistrationAfterThreadStartAsync(
        AppServerClient client,
        string threadId,
        ModelCapabilityRecord capability,
        CancellationToken cancellationToken)
    {
        EnabledGeneratedToolMcpConfig? option;
        lock (_turnStateLock) option = _registeredGeneratedToolMcp;
        if (option is null) return null;

        var observation = await ObserveGeneratedToolMcpStatusAsync(
            client, threadId, option.ToolId, cancellationToken).ConfigureAwait(false);
        var status = GeneratedToolActivationService.GetGeneratedToolActivationStatus(
            _applicationRoot, option.ToolId, option.NodeRuntime, _identity, capability,
            _toolExecutionPolicy, observation.Available ? observation.Entry : null);
        var observedOption = option with
        {
            ObservedMcpStatus = observation.Available ? observation.Entry : null
        };
        var assessment = CodexConfigBuilder.AssessEnabledGeneratedToolMcp(
            observedOption, capability, _toolExecutionPolicy);

        if (status.CallableTurnsAllowed && assessment.Enabled)
        {
            return new JsonObject
            {
                ["toolId"] = option.ToolId,
                ["state"] = "verified",
                ["inventoryConfirmation"] = status.InventoryConfirmation.ToString(),
                ["callableTurnsAllowed"] = true,
                ["blocksModelTurns"] = false
            };
        }

        if (status.InventoryConfirmation is GeneratedToolMcpInventoryConfirmation.UnexpectedTools
            or GeneratedToolMcpInventoryConfirmation.SchemaMismatch
            || assessment.InventoryConfirmation is GeneratedToolMcpInventoryConfirmation.UnexpectedTools
                or GeneratedToolMcpInventoryConfirmation.SchemaMismatch)
        {
            await DisableAndRevokeGeneratedToolRouteAsync(
                client, threadId, capability, option, observation, cancellationToken).ConfigureAwait(false);
            return new JsonObject
            {
                ["toolId"] = option.ToolId,
                ["state"] = "revokedAfterDescriptorMismatch",
                ["failureKind"] = "unsupported",
                ["inventoryConfirmation"] = status.InventoryConfirmation.ToString(),
                ["callableTurnsAllowed"] = false,
                ["blocksModelTurns"] = _disabledGeneratedToolIdAwaitingConfirmation is not null
            };
        }

        return new JsonObject
        {
            ["toolId"] = option.ToolId,
            ["state"] = "awaitingExactDescriptors",
            ["failureKind"] = status.CallableFailureKind ?? "qualificationPending",
            ["inventoryConfirmation"] = status.InventoryConfirmation.ToString(),
            ["blockers"] = JsonSerializer.SerializeToNode(status.CallableBlockers),
            ["callableTurnsAllowed"] = false,
            ["blocksModelTurns"] = true
        };
    }

    private static async Task<GeneratedToolMcpConfirmation> ConfirmDisabledProductMcpAsync(
        AppServerClient client, string threadId, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        McpServerStatusEntry? productStatus = null;
        try
        {
            for (var pageIndex = 0; pageIndex < 8; pageIndex++)
            {
                var page = await client.ListMcpServerStatusAsync(threadId, cursor,
                    deadline.Token, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                foreach (var entry in page.Servers.Where(entry =>
                             entry.Name == CodexConfigBuilder.ProductMcpServerName))
                {
                    if (entry.ToolNames.Count != 0) return GeneratedToolMcpConfirmation.UnexpectedTools;
                    if (productStatus is not null) return GeneratedToolMcpConfirmation.Unknown;
                    productStatus = entry;
                }
                if (page.NextCursor is null) return GeneratedToolMcpStatus.Evaluate(productStatus);
                if (!seenCursors.Add(page.NextCursor)) return GeneratedToolMcpConfirmation.Unknown;
                cursor = page.NextCursor;
            }
        }
        catch (McpControlException)
        {
            return GeneratedToolMcpConfirmation.Unknown;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return GeneratedToolMcpConfirmation.Unknown;
        }
        return GeneratedToolMcpConfirmation.Unknown;
    }

    private async Task<JsonObject> ListThreadsWithStateDbAsync(
        AppServerClient client,
        ModelCapabilityRecord capability,
        CancellationToken cancellationToken,
        ThreadHistoryCursor? cursor = null)
    {
        var scanAndRepair = cursor is null || cursor.ScanCursor is not null
            ? await client.RequestResultAsync(
                id => AppServerProtocol.BuildThreadListRequest(id, cursor: cursor?.ScanCursor, cwd: RequireProjectState().SelectedWorkspace),
                cancellationToken).ConfigureAwait(false)
            : new JsonObject { ["data"] = new JsonArray() };
        var stateDbOnly = cursor is null || cursor.StateDbCursor is not null
            ? await client.RequestResultAsync(
                id => AppServerProtocol.BuildThreadListRequest(id, useStateDbOnly: true, cursor: cursor?.StateDbCursor,
                    cwd: RequireProjectState().SelectedWorkspace),
                cancellationToken).ConfigureAwait(false)
            : new JsonObject { ["data"] = new JsonArray() };

        var scannedItems = scanAndRepair["data"] as JsonArray ?? new JsonArray();
        var indexedItems = stateDbOnly["data"] as JsonArray ?? new JsonArray();
        var bookmarkedItems = new JsonArray();
        var unresolvedBookmarkCount = 0;
        var eligibleBookmarks = (cursor is null ? ForkBookmarkStore.Read(_forkBookmarkPath) : [])
            .Where(bookmark => string.Equals(bookmark.Workspace, RequireProjectState().SelectedWorkspace, StringComparison.OrdinalIgnoreCase));
        foreach (var bookmark in eligibleBookmarks)
        {
            try
            {
                var read = await client.RequestResultAsync(
                    id => AppServerProtocol.BuildThreadReadRequest(id, bookmark.ThreadId, includeTurns: false),
                    cancellationToken).ConfigureAwait(false);
                if (read["thread"] is not JsonObject thread)
                {
                    unresolvedBookmarkCount++;
                    continue;
                }

                ForkBookmarkIdentity.RequireExactMatch(thread, bookmark);
                bookmarkedItems.Add(new JsonObject
                {
                    ["id"] = thread["id"]?.DeepClone(),
                    ["preview"] = thread["preview"]?.DeepClone(),
                    ["forkedFromId"] = thread["forkedFromId"]?.DeepClone(),
                    ["modelProvider"] = thread["modelProvider"]?.DeepClone(),
                    ["model"] = thread["model"]?.DeepClone(),
                    ["updatedAt"] = thread["updatedAt"]?.DeepClone(),
                    ["status"] = thread["status"]?.DeepClone(),
                    ["cwd"] = thread["cwd"]?.DeepClone()
                });
            }
            catch (AppServerRpcException)
            {
                unresolvedBookmarkCount++;
            }
            catch (InvalidOperationException)
            {
                unresolvedBookmarkCount++;
            }
        }

        var mergedThreads = ThreadListProjector.MergeScanAndStateDb(scannedItems, indexedItems);
        var allKnownThreads = ThreadListProjector.MergeScanAndStateDb(mergedThreads, bookmarkedItems);
        var selectedThreads = ThreadListProjector.FilterByWorkspace(allKnownThreads, RequireProjectState().SelectedWorkspace);
        var nextCursor = ThreadHistoryCursor.CreateNext(
            RequireProjectState().SelectedWorkspace,
            capability.ProviderId,
            capability.ModelIdentifier,
            scanAndRepair["nextCursor"]?.GetValue<string>(),
            stateDbOnly["nextCursor"]?.GetValue<string>());
        return new JsonObject
        {
            ["data"] = selectedThreads,
            ["nextCursor"] = nextCursor,
            ["historyQuery"] = new JsonObject
            {
                ["scanAndRepairCount"] = scannedItems.Count,
                ["stateDbOnlyCount"] = indexedItems.Count,
                ["verifiedForkBookmarkCount"] = bookmarkedItems.Count,
                ["unresolvedForkBookmarkCount"] = unresolvedBookmarkCount,
                ["omittedOtherWorkspaceOrUnknownCount"] = allKnownThreads.Count - selectedThreads.Count
            }
        };
    }

    private string? GetCapabilityBindingBlockReason(
        string threadId,
        ModelCapabilityRecord capability,
        string workspace)
    {
        var resolved = ResolveCapabilityBinding(threadId, capability, workspace);
        return resolved.BlockReason ?? (string.Equals(CapabilityRecordIdentity.Compute(capability),
            CapabilityRecordIdentity.Compute(resolved.BoundCapability), StringComparison.Ordinal) ? null : "capabilityRecordChanged");
    }

    private (string? BlockReason, ModelCapabilityRecord BoundCapability) ResolveCapabilityBinding(
        string threadId,
        ModelCapabilityRecord capability,
        string workspace)
    {
        ThreadCapabilitySelection? binding;
        try
        {
            binding = ThreadCapabilityBindingStore.ReadCurrentBinding(_threadCapabilityBindingRoot, threadId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            return ("capabilityBindingUnavailable", capability);
        }

        if (binding is null)
        {
            return ("capabilityBindingMissing", capability);
        }

        if (!string.Equals(binding.Workspace, Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspace)), StringComparison.OrdinalIgnoreCase))
            return ("capabilityBindingUnavailable", capability);

        bool Matches(ModelCapabilityRecord candidate) =>
            string.Equals(binding.CapabilityIdentity, CapabilityRecordIdentity.Compute(candidate), StringComparison.Ordinal)
            && string.Equals(binding.ProviderId, candidate.ProviderId, StringComparison.Ordinal)
            && string.Equals(binding.ModelIdentifier, candidate.ModelIdentifier, StringComparison.Ordinal);

        if (Matches(capability))
        {
            return (null, capability);
        }

        try
        {
            var retained = CapabilityRecordPathResolver.FindExactRetainedRecord(
                _sourceRoot, binding.CapabilityIdentity, binding.ProviderId, binding.ModelIdentifier);
            if (retained is { } exact) return (null, exact.Record);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            return ("capabilityBindingUnavailable", capability);
        }

        // A Known maximum changes execution policy; a prior binding without it
        // remains history-only. Reconcile legacy JSON shapes only while the
        // current observation is still genuinely Unknown or absent.
        var selectedMaximum = capability.MaxCompletionTokensAdvertised;
        if (selectedMaximum.State != CapabilityState.Unknown || selectedMaximum.Value is not null)
        {
            return ("capabilityRecordChanged", capability);
        }

        // The older record omitted this observation entirely.
        if (capability.SerializedMaxCompletionTokensAdvertised is not null)
        {
            var legacyCapability = capability with { SerializedMaxCompletionTokensAdvertised = null };
            if (Matches(legacyCapability))
            {
                return (null, legacyCapability);
            }
        }

        // The first P2-11 implementation serialized a default Unknown even when
        // the JSON omitted this field. Preserve bindings created in that interval.
        var transitionalCapability = capability with
        {
            SerializedMaxCompletionTokensAdvertised = CapabilityObservation.Unknown("not-observed")
        };
        if (Matches(transitionalCapability))
        {
            return (null, transitionalCapability);
        }

        return ("capabilityRecordChanged", capability);
    }

    private static string DescribeCapabilityBindingBlock(string reason) => reason switch
    {
        "capabilityBindingMissing" => "The original capability identity for this saved task is unavailable. Its history is read-only; start a new task to execute under the selected record.",
        "capabilityBindingUnavailable" => "The saved task's capability identity could not be verified. Its history is read-only; the binding was not changed.",
        _ => "The saved task was created under a different capability record. Its history is read-only; select a matching record or start a new task."
    };

    private async Task<AppServerClient> EnsureClientAsync(
        ModelCapabilityRecord capability,
        bool requireProviderCredential,
        CancellationToken cancellationToken,
        bool allowExplicitCapabilitySwitch = false,
        NvidiaAdapterProcess? preparedNvidiaAdapter = null)
    {
        var selectedWorkspace = RequireProjectState().SelectedWorkspace;
        if (!Directory.Exists(selectedWorkspace))
        {
            throw new DirectoryNotFoundException($"The selected NeoBabylon project is unavailable: {selectedWorkspace}. Choose an available project before starting App Server.");
        }

        if (string.IsNullOrWhiteSpace(capability.ModelIdentifier) || capability.ModelIdentifier == "Unknown")
        {
            throw new InvalidOperationException("A verified model identifier is required; no fallback is permitted.");
        }

        var isOpenRouter = string.Equals(capability.ProviderId, "openrouter", StringComparison.OrdinalIgnoreCase);
        var isNvidia = string.Equals(capability.ProviderId, "nvidia", StringComparison.OrdinalIgnoreCase);
        var isLmStudio = string.Equals(capability.ProviderId, "lmstudio", StringComparison.OrdinalIgnoreCase);
        if (!isOpenRouter && !isNvidia && !isLmStudio)
        {
            throw new InvalidOperationException("The selected provider is not supported by this host; no provider fallback is permitted.");
        }

        var openRouterApiKey = _providerCredentials?.GetCredential(ProviderCredentialProvider.OpenRouter)
            ?? _openRouterApiKey;
        var nvidiaApiKey = _providerCredentials?.GetCredential(ProviderCredentialProvider.Nvidia);
        if (requireProviderCredential && isOpenRouter && openRouterApiKey is null)
        {
            throw new InvalidOperationException("OPENROUTER_API_KEY is required for the selected OpenRouter provider; no credential or provider fallback is permitted.");
        }
        if (isNvidia && nvidiaApiKey is null)
        {
            throw new InvalidOperationException("NVIDIA_API_KEY is required for the selected NVIDIA provider; no credential or provider fallback is permitted.");
        }

        var requestedRecordSnapshot = CapabilityRecordIdentity.Serialize(capability);
        var sameCapabilityRecord = string.Equals(
            _activeCapabilityRecordSnapshot, requestedRecordSnapshot, StringComparison.Ordinal);
        if (!allowExplicitCapabilitySwitch
            && _threadId is not null && _activeCapabilityRecordSnapshot is not null && !sameCapabilityRecord)
        {
            throw new InvalidOperationException("The selected task is bound to a different capability record. Start a new task or explicitly change the selected model before resuming.");
        }

        if (_client is { HasExited: true })
        {
            var preserveSelectedThread = _threadId is not null
                && _activeCapability is not null
                && sameCapabilityRecord;
            await DisposeClientAsync(preserveSelectedThread).ConfigureAwait(false);
        }

        if (_client is not null && _activeCapability is not null && sameCapabilityRecord)
        {
            return _client;
        }

        if (_client is not null)
        {
            await DisposeClientAsync().ConfigureAwait(false);
        }

        if (_toolExecutionPolicy == ToolExecutionPolicy.ApprovalQualification
            && !IsQualificationLoopbackEndpoint(capability))
        {
            throw new InvalidOperationException("ApprovalQA accepts only a local LM Studio capability pointed at a loopback HTTP Responses fixture.");
        }

        if (isNvidia)
        {
            try
            {
                _nvidiaAdapter = preparedNvidiaAdapter ?? await NvidiaAdapterProcess.StartAsync(
                    _sourceRoot, _applicationRoot, capability, nvidiaApiKey!, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                _nvidiaAdapter = null;
                throw new InvalidOperationException("The private NVIDIA adapter could not be started for the selected model; no provider fallback was attempted.");
            }
        }

        EnabledGeneratedToolMcpConfig? enabledGeneratedToolMcp;
        AppServerClient client;
        try
        {
            enabledGeneratedToolMcp = FindActiveGeneratedToolMcpConfig(capability);
            CodexConfigBuilder.WriteIsolated(
                _layout.CodexHome,
                capability,
                toolExecutionPolicy: _toolExecutionPolicy,
                disabledProductMcp: _stagedDisabledProductMcp,
                enabledGeneratedToolMcp: enabledGeneratedToolMcp,
                nvidiaAdapterEndpoint: isNvidia ? _nvidiaAdapter!.Endpoint : null);
            var providerBaseUrl = isLmStudio ? capability.Endpoint : null;
            var providerApiKey = isOpenRouter ? openRouterApiKey : null;
            var providerSessionToken = isNvidia ? _nvidiaAdapter!.SessionToken : null;
            client = AppServerClient.Start(new AppServerLaunchOptions(
                _identity.BinaryPath,
                RequireProjectState().SelectedWorkspace,
                _layout.CodexHome,
                providerBaseUrl,
                providerApiKey,
                providerSessionToken),
                _approvalTimeout);
        }
        catch
        {
            if (_nvidiaAdapter is { } abandonedAdapter)
            {
                _nvidiaAdapter = null;
                await abandonedAdapter.DisposeAsync().ConfigureAwait(false);
            }
            throw;
        }
        var clientEpoch = Guid.NewGuid();
        lock (_turnStateLock)
        {
            _client = client;
            _clientEpoch = clientEpoch;
            _continuingCommands.Clear();
            _registeredGeneratedToolMcp = enabledGeneratedToolMcp;
            _disabledGeneratedToolIdAwaitingConfirmation = null;
        }
        client.NotificationObserved += notification =>
        {
            ObserveCompactionTerminalNotification(client, clientEpoch, notification);
            ObserveCommandCompletionNotification(client, clientEpoch, notification);
        };
        try
        {
            var initializeResponse = await client.InitializeAsync(cancellationToken).ConfigureAwait(false);
            AppServerLaunchEnvironment.RequireCodexHome(initializeResponse, _layout.CodexHome);
            _activeCapability = capability;
            _activeCapabilityRecordSnapshot = requestedRecordSnapshot;
            return client;
        }
        catch
        {
#if NEOBABYLON_APPROVAL_QA
            try
            {
                ApprovalQaInitializeFailureObserver?.Invoke(client);
            }
            catch
            {
                // Diagnostics must never replace the original initialization failure.
            }
#endif
            await DisposeClientAsync(_threadRequiresResume).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<JsonObject> StartTurnAsync(
        string text,
        CancellationToken cancellationToken,
        Action<AppServerNotification>? notificationObserver = null,
        string? bridgeRequestId = null,
        int? maxOutputTokens = null,
        string? reasoningEffort = null)
    {
        _ = RequireProjectState();
        lock (_turnStateLock)
        {
            if (_activeTurnReady is not null || _threadStartInProgress)
            {
                throw new InvalidOperationException("Finish the current App Server operation before starting a turn.");
            }

            var capability = _activeCapability
                ?? throw new InvalidOperationException("Start a pinned thread before starting a turn.");
            CapabilitySwitchSafety.RequireSupportedReasoningEffort(capability, reasoningEffort);

            _threadStartInProgress = true;
        }

        try
        {
            return await StartTurnCoreAsync(text, cancellationToken, notificationObserver, bridgeRequestId, maxOutputTokens, reasoningEffort).ConfigureAwait(false);
        }
        finally
        {
            lock (_turnStateLock)
            {
                _threadStartInProgress = false;
            }
        }
    }

    private void CaptureSelectedThreadUsageState(JsonObject response, string method)
    {
        _currentContextEvidenceOffset = SessionJournalToolEvidenceReader.CaptureOffset(_activeSessionPath, _layout.CodexHome) ?? long.MaxValue;
        _observedReasoningEffort = response["reasoningEffort"] is JsonValue value && value.TryGetValue<string>(out var effort)
            && CapabilitySwitchSafety.IsPinnedReasoningEffort(effort) ? effort : null;
        _reasoningEvidenceSource = _observedReasoningEffort is null ? null : "Codex App Server " + method + ".reasoningEffort";
    }

    public JsonObject GetThreadUsage(string requestedThreadId)
    {
        var workspace = RequireProjectState().SelectedWorkspace;
        AppServerClient client;
        Guid epoch;
        ModelCapabilityRecord capability;
        string? path, observedEffort, reasoningSource;
        long contextOffset;
        lock (_turnStateLock)
        {
            if (!string.Equals(_threadId, requestedThreadId, StringComparison.Ordinal)
                || _threadRequiresResume || _client is null || _client.HasExited || _clientEpoch == Guid.Empty
                || _threadStartInProgress && _activeTurnReady is null)
                throw new InvalidOperationException("Usage is limited to the exact selected verified App Server thread.");
            client = _client;
            epoch = _clientEpoch;
            capability = _activeCapability ?? throw new InvalidOperationException("The selected capability is unavailable.");
            path = _activeSessionPath;
            contextOffset = _currentContextEvidenceOffset;
            observedEffort = _observedReasoningEffort;
            reasoningSource = _reasoningEvidenceSource;
        }
        var block = GetCapabilityBindingBlockReason(requestedThreadId, capability, workspace);
        if (block is not null) throw new InvalidOperationException(DescribeCapabilityBindingBlock(block));
        var usage = ThreadUsageEvidence.ReadJournal(path, _layout.CodexHome, requestedThreadId, workspace, capability.ModelIdentifier, contextOffset);
        // A typed thread/start/resume response is actual runtime state, not the requested control.
        if (usage.ReasoningEvidenceSource is null && observedEffort is not null)
            usage = usage with { ReasoningEffort = observedEffort, ReasoningEvidenceSource = reasoningSource };
        lock (_turnStateLock)
        {
            if (!ReferenceEquals(_client, client) || _clientEpoch != epoch || _threadId != requestedThreadId
                || _threadRequiresResume || client.HasExited || !string.Equals(RequireProjectState().SelectedWorkspace, workspace, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The selected thread identity changed during the usage read.");
        }
        return usage.ToResult(requestedThreadId);
    }

    public async Task<JsonObject> CompactContextAsync(
        string requestedThreadId,
        CancellationToken cancellationToken,
        Action<AppServerNotification>? notificationObserver = null,
        string? bridgeRequestId = null)
    {
        var workspace = RequireProjectState().SelectedWorkspace;
        AppServerClient client;
        ModelCapabilityRecord capability;
        Guid epoch;
        lock (_turnStateLock)
        {
            CapabilitySwitchSafety.RequireManualCompactionEligible(requestedThreadId, _threadId,
                !_threadRequiresResume && _client is { HasExited: false } && _clientEpoch != Guid.Empty,
                _threadStartInProgress || _activeTurnReady is not null);
            client = _client!;
            capability = _activeCapability ?? throw new InvalidOperationException("The selected capability is unavailable.");
            epoch = _clientEpoch;
            _threadStartInProgress = true;
        }
        TaskCompletionSource<string?>? ready = null;
        try
        {
            var block = GetCapabilityBindingBlockReason(requestedThreadId, capability, workspace);
            if (block is not null) throw new InvalidOperationException(DescribeCapabilityBindingBlock(block));
            RequireOpenRouterCredentialForTurn(capability);
            if (string.Equals(capability.ProviderId, "nvidia", StringComparison.OrdinalIgnoreCase)
                && _providerCredentials?.GetCredential(ProviderCredentialProvider.Nvidia) is null)
                throw new InvalidOperationException("The selected NVIDIA credential is unavailable; no compaction request was sent.");
            var read = await client.RequestResultAsync(
                id => AppServerProtocol.BuildThreadReadRequest(id, requestedThreadId, includeTurns: false), cancellationToken).ConfigureAwait(false);
            ThreadRenameIdentity.RequireExactWorkspaceRead(read["thread"] as JsonObject, requestedThreadId, workspace);
            var commands = await client.RequestResultAsync(
                id => AppServerProtocol.BuildCommandExecutionListRequest(id, requestedThreadId), cancellationToken).ConfigureAwait(false);
            CapabilitySwitchSafety.RequireNoActiveContinuingCommands(commands, "compacting context");
            lock (_turnStateLock)
            {
                if (!ReferenceEquals(_client, client) || _clientEpoch != epoch || _threadId != requestedThreadId
                    || _threadRequiresResume || client.HasExited || _activeTurnReady is not null
                    || !string.Equals(RequireProjectState().SelectedWorkspace, workspace, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The selected thread identity changed before compaction could start.");
                ready = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
                _activeTurnReady = ready;
                _activeTurnId = null;
                _compactionReservation = new CompactionReservation(epoch, requestedThreadId, ready);
                _turnGeneration++;
            }
            var observation = await client.CompactContextAsync(requestedThreadId, TimeSpan.FromMinutes(10), cancellationToken,
                notificationObserver, turnId =>
                {
                    lock (_turnStateLock)
                        if (ReferenceEquals(_activeTurnReady, ready) && ReferenceEquals(_client, client) && _clientEpoch == epoch)
                            _activeTurnId = turnId;
                    ready.TrySetResult(turnId);
                }).ConfigureAwait(false);
            return FinishCompactionObservation(client, epoch, requestedThreadId, ready, observation);
        }
        finally
        {
            lock (_turnStateLock) _threadStartInProgress = false;
        }
    }

    private JsonObject FinishCompactionObservation(
        AppServerClient client, Guid epoch, string requestedThreadId,
        TaskCompletionSource<string?> ready, ManualCompactionTracker observation)
    {
        lock (_turnStateLock)
        {
            if (!ReferenceEquals(_client, client) || _clientEpoch != epoch || _threadId != requestedThreadId)
                return new ManualCompactionTracker(requestedThreadId).FinishUnknown("identityLost");
        }
        if (observation.RequestNotSent || observation.HasConfirmedTerminal || client.HasExited)
        {
            // A rejection before turn/started must also release interruptTurn's
            // existing waiter, not just remove the active reservation reference.
            ready.TrySetResult(observation.TurnId);
            ClearActiveTurn(ready);
        }
        else ready.TrySetResult(observation.TurnId); // Sent-but-Unknown retains the guard; no speculative reset/kill.
        return observation.Receipt();
    }

    private void ObserveCompactionTerminalNotification(AppServerClient client, Guid epoch, AppServerNotification notification)
    {
        lock (_turnStateLock)
        {
            var reservation = _compactionReservation;
            if (reservation is null || reservation.ClientEpoch != epoch || !ReferenceEquals(_client, client)
                || _clientEpoch != epoch || !ReferenceEquals(_activeTurnReady, reservation.Ready)
                || notification.Params["threadId"] is not JsonValue threadValue
                || !threadValue.TryGetValue<string>(out var threadId) || threadId != reservation.ThreadId) return;
            var turn = notification.Params["turn"] as JsonObject;
            var turnId = turn?["id"] is JsonValue turnValue && turnValue.TryGetValue<string>(out var id) ? id : null;
            if (string.IsNullOrWhiteSpace(turnId)) return;
            if (notification.Method == "turn/started" && reservation.TurnId is null)
            {
                reservation.TurnId = turnId;
                _activeTurnId = turnId;
                reservation.Ready.TrySetResult(turnId);
            }
            else if (notification.Method == "turn/completed" && reservation.TurnId == turnId)
            {
                // A late terminal event ends the exclusive operation even after an Unknown timeout.
                // It does not retroactively turn the returned Unknown receipt into a success claim.
                _activeTurnReady = null;
                _activeTurnId = null;
                _compactionReservation = null;
            }
        }
    }

    public async Task<JsonObject> ReadOutputRangeAsync(
        string requestedThreadId,
        string turnId,
        string itemId,
        int offset,
        CancellationToken cancellationToken)
    {
        _ = RequireProjectState();
        AppServerClient client;
        ModelCapabilityRecord capability;
        lock (_turnStateLock)
        {
            if (_activeTurnReady is not null || _threadStartInProgress)
            {
                throw new InvalidOperationException("Finish the current App Server operation before inspecting saved output.");
            }

            if (!string.Equals(_threadId, requestedThreadId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Output inspection is limited to the exact active App Server thread.");
            }

            if (_threadRequiresResume || _client is null || _client.HasExited)
            {
                throw new InvalidOperationException("The active App Server thread must be resumed before inspecting saved output.");
            }

            client = _client;
            capability = _activeCapability
                ?? throw new InvalidOperationException("The selected provider/model record is unavailable for the active thread.");
            _threadStartInProgress = true;
        }

        try
        {
            var bindingBlockReason = GetCapabilityBindingBlockReason(
                requestedThreadId, capability, RequireProjectState().SelectedWorkspace);
            if (bindingBlockReason is not null)
            {
                throw new InvalidOperationException(DescribeCapabilityBindingBlock(bindingBlockReason));
            }

            const int maximumItemPages = 256;
            string? cursor = null;
            for (var pageIndex = 0; pageIndex < maximumItemPages; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await client.RequestResultAsync(
                    id => AppServerProtocol.BuildThreadItemsListRequest(id, requestedThreadId, turnId, cursor),
                    cancellationToken).ConfigureAwait(false);
                var data = response["data"] as JsonArray
                    ?? throw new InvalidDataException("App Server item history did not contain an item array.");
                var page = ThreadItemOutputRangeProjector.TryProject(
                    data,
                    requestedThreadId,
                    turnId,
                    itemId,
                    offset,
                    ThreadItemOutputRangeProjector.MaximumPageCharacters);
                if (page is not null)
                {
                    return new JsonObject
                    {
                        ["attributedTo"] = "Codex App Server",
                        ["threadId"] = page.ThreadId,
                        ["turnId"] = page.TurnId,
                        ["itemId"] = page.ItemId,
                        ["itemType"] = page.ItemType,
                        ["offset"] = page.Offset,
                        ["totalCharacters"] = page.TotalCharacters,
                        ["text"] = page.Text,
                        ["nextOffset"] = page.NextOffset,
                        ["hasMore"] = page.HasMore,
                        ["upstreamTruncated"] = page.UpstreamTruncated
                    };
                }

                cursor = response["nextCursor"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(cursor))
                {
                    break;
                }
            }

            throw new InvalidDataException("The requested output item was not found within the bounded App Server history scan.");
        }
        finally
        {
            lock (_turnStateLock)
            {
                _threadStartInProgress = false;
            }
        }
    }

    private void RequireOpenRouterCredentialForTurn(ModelCapabilityRecord capability)
    {
        var openRouterApiKey = _providerCredentials?.GetCredential(ProviderCredentialProvider.OpenRouter)
            ?? _openRouterApiKey;
        if (string.Equals(capability.ProviderId, "openrouter", StringComparison.OrdinalIgnoreCase)
            && openRouterApiKey is null)
        {
            throw new InvalidOperationException("OPENROUTER_API_KEY is required before a provider turn can start; no credential or provider fallback is permitted.");
        }
    }

    private async Task<JsonObject> StartTurnCoreAsync(
        string text,
        CancellationToken cancellationToken,
        Action<AppServerNotification>? notificationObserver,
        string? bridgeRequestId,
        int? maxOutputTokens,
        string? reasoningEffort)
    {
        if (_client is { HasExited: true } && _activeCapability is not null && _threadId is not null)
        {
            await EnsureClientAsync(_activeCapability, requireProviderCredential: true, cancellationToken).ConfigureAwait(false);
        }

        if (_threadRequiresResume && _activeCapability is not null && _threadId is not null)
        {
            await ResumeThreadCoreAsync(_activeCapability, _threadId, cancellationToken, reservedByTurn: true).ConfigureAwait(false);
        }

        if (_client is null || string.IsNullOrWhiteSpace(_threadId))
        {
            throw new InvalidOperationException("Start a pinned thread before starting a turn.");
        }

        var client = _client;
        var threadId = _threadId;
        var capability = _activeCapability
            ?? throw new InvalidOperationException("The selected provider/model record is unavailable for the active thread.");
        var bindingBlockReason = GetCapabilityBindingBlockReason(threadId, capability, RequireProjectState().SelectedWorkspace);
        if (bindingBlockReason is not null)
        {
            throw new InvalidOperationException(DescribeCapabilityBindingBlock(bindingBlockReason));
        }

        RequireOpenRouterCredentialForTurn(capability);

        await RequireGeneratedToolMcpCallableForTurnAsync(
            client, threadId, capability, cancellationToken).ConfigureAwait(false);
        var ready = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var journalStartOffset = SessionJournalToolEvidenceReader.CaptureOffset(_activeSessionPath, _layout.CodexHome);
        Guid clientEpoch;
        long turnGeneration;
        lock (_turnStateLock)
        {
            if (_activeTurnReady is not null)
            {
                throw new InvalidOperationException("An App Server turn is already active or awaiting its terminal event.");
            }

            if (!ReferenceEquals(_client, client) || _clientEpoch == Guid.Empty)
            {
                throw new InvalidOperationException("The selected App Server instance changed before the turn could start.");
            }

            _activeTurnReady = ready;
            _activeTurnId = null;
            _turnGeneration++;
            clientEpoch = _clientEpoch;
            turnGeneration = _turnGeneration;
        }

        string turnId;
        try
        {
            var startResult = await client.RequestResultAsync(
                id =>
                {
                    var request = AppServerClient.BuildTurnStartRequest(
                        id, new TurnStartOptions(threadId, text, _toolExecutionPolicy, reasoningEffort), capability, maxOutputTokens);
                    if (reasoningEffort is not null)
                    {
                        // A requested override is not observed effective effort. Await fresh journal state.
                        _observedReasoningEffort = null;
                        _reasoningEvidenceSource = null;
                        _currentContextEvidenceOffset = journalStartOffset ?? long.MaxValue;
                    }
                    return request;
                },
                cancellationToken).ConfigureAwait(false);
            turnId = ExtractTurnId(startResult);
            lock (_turnStateLock)
            {
                if (ReferenceEquals(_activeTurnReady, ready))
                {
                    _activeTurnId = turnId;
                }
            }
            ready.TrySetResult(turnId);
        }
        catch
        {
            ready.TrySetResult(null);
            ClearActiveTurn(ready);
            throw;
        }

        var activeCommandItems = new Dictionary<string, string>(StringComparer.Ordinal);
        var commandTrackingIncomplete = false;
        void ObserveTurnNotification(AppServerNotification notification)
        {
            var item = notification.Params["item"] as JsonObject;
            if (item?["type"]?.GetValue<string>() == "commandExecution"
                && item["id"]?.GetValue<string>() is string itemId
                && !string.IsNullOrWhiteSpace(itemId))
            {
                if (notification.Method == "item/started")
                {
                    if (activeCommandItems.ContainsKey(itemId)
                        || activeCommandItems.Count < MaximumObservedCommandItemsPerTurn)
                    {
                        activeCommandItems[itemId] = item["command"]?.GetValue<string>() ?? "Command execution";
                    }
                    else
                    {
                        commandTrackingIncomplete = true;
                    }
                }
                else if (notification.Method == "item/completed")
                {
                    activeCommandItems.Remove(itemId);
                }
            }

            notificationObserver?.Invoke(notification);
        }

        var observation = await client.WaitForTurnCompletionAsync(
            TimeSpan.FromMinutes(10),
            cancellationToken,
            expectedThreadId: threadId,
            expectedTurnId: turnId,
            notificationObserver: ObserveTurnNotification).ConfigureAwait(false);
        if (observation.Terminal || client.HasExited)
        {
            ClearActiveTurn(ready);
        }

        var notificationToolDiagnostics = TurnDiagnostics.Extract(observation.Notifications);
        var journalEvidence = SessionJournalToolEvidenceReader.ReadAppended(
            _activeSessionPath,
            _layout.CodexHome,
            journalStartOffset);
        var journalToolDiagnostics = TurnDiagnostics.ExtractSessionJournal(journalEvidence.Calls);
        var toolDiagnostics = TurnDiagnostics.Combine(notificationToolDiagnostics, journalToolDiagnostics);
        var hostFailure = BuildHostFailure(observation, toolDiagnostics);
        var toolOutcomeStatus = TurnDiagnostics.OutcomeStatus(toolDiagnostics);
        var continuingCommandEvidence = observation.Interrupted
            ? await BuildContinuingCommandEvidenceAsync(
                client,
                threadId,
                turnId,
                activeCommandItems,
                commandTrackingIncomplete,
                clientEpoch,
                turnGeneration,
                bridgeRequestId,
                cancellationToken).ConfigureAwait(false)
            : new JsonObject
            {
                ["status"] = "notApplicable",
                ["commands"] = new JsonArray()
            };

        return new JsonObject
        {
            ["attributedTo"] = observation.Completed || observation.Interrupted
                ? string.Equals(capability.ProviderId, "openrouter", StringComparison.OrdinalIgnoreCase)
                    ? "Codex App Server → OpenRouter requested model; actual endpoint provider is unverified"
                    : $"Codex App Server → {capability.ProviderDisplayName} → selected model"
                : "NeoBabylon.Host",
            ["eventType"] = observation.Completed
                ? TurnDiagnostics.HasFailure(toolDiagnostics) ? "turnCompletedWithToolFailure" : "turnCompleted"
                : observation.Interrupted ? "turnInterrupted" : "turnFailure",
            ["terminal"] = observation.Terminal,
            ["turnStatus"] = observation.Status,
            ["completed"] = observation.Completed,
            ["interrupted"] = observation.Interrupted,
            ["failure"] = hostFailure,
            ["toolDiagnostics"] = toolDiagnostics,
            ["toolFailureObserved"] = TurnDiagnostics.HasFailure(toolDiagnostics),
            ["toolOutcomeStatus"] = toolOutcomeStatus,
            ["toolEvidenceReadStatus"] = journalEvidence.Status,
            ["toolEvidenceReadFailure"] = journalEvidence.Failure,
            ["assistantText"] = observation.FinalAssistantText,
            ["assistantItemId"] = observation.FinalAssistantItemId,
            ["assistantDisplay"] = observation.FinalAssistantDisplay,
            ["threadId"] = threadId,
            ["turnId"] = turnId,
            ["commandExecutionDiscoveryStatus"] = continuingCommandEvidence["status"]?.DeepClone(),
            ["commandExecutionDiscoveryFailure"] = continuingCommandEvidence["failure"]?.DeepClone(),
            ["continuingCommands"] = continuingCommandEvidence["commands"]?.DeepClone() ?? new JsonArray(),
            ["additionalActiveCommandsMayBeOmitted"] = continuingCommandEvidence["additionalActiveCommandsMayBeOmitted"]?.GetValue<bool>() == true,
            ["notificationHistoryTruncated"] = observation.NotificationsTruncated,
            ["modelContextEvidence"] = new JsonObject
            {
                ["providerContextWindow"] = capability.ContextWindowEffective.Int32Value,
                ["sessionContextWindow"] = journalEvidence.ModelContextWindow,
                ["sessionContextStatus"] = journalEvidence.ModelContextWindow is null ? "unknown" : "observed",
                ["sessionContextEvidenceSource"] = "isolated App Server session journal task_started.model_context_window"
            }
        };
    }

    private async Task<JsonObject> BuildContinuingCommandEvidenceAsync(
        AppServerClient client,
        string threadId,
        string turnId,
        IReadOnlyDictionary<string, string> activeCommandItems,
        bool commandTrackingIncomplete,
        Guid clientEpoch,
        long turnGeneration,
        string? bridgeRequestId,
        CancellationToken cancellationToken)
    {
        var commands = new JsonArray();
        var evidence = new JsonObject
        {
            ["status"] = commandTrackingIncomplete ? "partial" : "observed",
            ["commands"] = commands
        };
        if (commandTrackingIncomplete)
        {
            evidence["additionalActiveCommandsMayBeOmitted"] = true;
        }
        if (activeCommandItems.Count == 0)
        {
            return evidence;
        }

        var completionTracker = new InterruptedCommandCompletionTracker(
            threadId,
            turnId,
            activeCommandItems.Keys);
        lock (_turnStateLock)
        {
            _commandCompletionTracker = completionTracker;
        }

        try
        {
            var response = await client.RequestResultAsync(
                id => AppServerProtocol.BuildCommandExecutionListRequest(id, threadId),
                cancellationToken).ConfigureAwait(false);
            var data = response["data"] as JsonArray
                ?? throw new InvalidDataException("App Server command listing omitted its data array.");
            var activeByItemId = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            foreach (var value in data.OfType<JsonObject>())
            {
                var listedItemId = value["itemId"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(listedItemId))
                {
                    activeByItemId[listedItemId] = value;
                }
            }

            lock (_turnStateLock)
            {
                var changedIdentityFields = new List<string>();
                if (!ReferenceEquals(_client, client))
                {
                    changedIdentityFields.Add("AppServerInstance");
                }
                if (_clientEpoch != clientEpoch)
                {
                    changedIdentityFields.Add("AppServerEpoch");
                }
                if (!string.Equals(_threadId, threadId, StringComparison.Ordinal))
                {
                    changedIdentityFields.Add("SelectedThread");
                }
                if (_threadRequiresResume)
                {
                    changedIdentityFields.Add("ThreadRequiresResume");
                }
                if (_activeTurnReady is not null)
                {
                    changedIdentityFields.Add("AnotherTurnActive");
                }
                if (_turnGeneration != turnGeneration)
                {
                    changedIdentityFields.Add("TurnGeneration");
                }

                if (changedIdentityFields.Count > 0)
                {
                    evidence["status"] = "unavailable";
                    evidence["failure"] = new JsonObject
                    {
                        ["attributedTo"] = "NeoBabylon.Host",
                        ["type"] = "CommandDiscoveryIdentityChanged",
                        ["message"] = $"The selected App Server thread or instance changed while active command state was being checked ({string.Join(", ", changedIdentityFields)})."
                    };
                    foreach (var (itemId, command) in activeCommandItems)
                    {
                        commands.Add(new JsonObject
                        {
                            ["itemId"] = itemId,
                            ["command"] = command,
                            ["commandStopState"] = "unknown",
                            ["commandStopAvailable"] = false,
                            ["commandStopDetail"] = "The thread, turn, or App Server instance changed during command discovery; no Stop action is offered without a current exact identity."
                        });
                    }
                    return evidence;
                }

                foreach (var (itemId, observedCommand) in activeCommandItems)
                {
                    if (completionTracker.HasCompleted(itemId))
                    {
                        if (_continuingCommands.TryGetValue((threadId, itemId), out var priorIdentity)
                            && string.Equals(priorIdentity.TurnId, turnId, StringComparison.Ordinal)
                            && priorIdentity.ClientEpoch == clientEpoch)
                        {
                            _continuingCommands.Remove((threadId, itemId));
                        }

                        commands.Add(new JsonObject
                        {
                            ["itemId"] = itemId,
                            ["command"] = observedCommand,
                            ["commandStopState"] = "notReportedActive",
                            ["commandStopAvailable"] = false,
                            ["commandStopDetail"] = "Codex App Server reported this command complete while active-command discovery was pending. No Stop action is offered."
                        });
                        continue;
                    }

                    if (!activeByItemId.TryGetValue(itemId, out var activeCommand))
                    {
                        commands.Add(new JsonObject
                        {
                            ["itemId"] = itemId,
                            ["command"] = observedCommand,
                            ["commandStopState"] = "notReportedActive",
                            ["commandStopAvailable"] = false,
                            ["commandStopDetail"] = "Codex App Server no longer reports this command as active. Whether it exited normally or stopped another way is unconfirmed; final output may be unavailable."
                        });
                        continue;
                    }

                    var processId = activeCommand["processId"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(processId) || !int.TryParse(processId, out _))
                    {
                        commands.Add(new JsonObject
                        {
                            ["itemId"] = itemId,
                            ["command"] = observedCommand,
                            ["commandStopState"] = "unknown",
                            ["commandStopAvailable"] = false,
                            ["commandStopDetail"] = "Codex App Server did not return a valid identity for this active command."
                        });
                        continue;
                    }

                    _continuingCommands[(threadId, itemId)] = new ContinuingCommandIdentity(
                        threadId,
                        turnId,
                        itemId,
                    processId,
                    observedCommand,
                    clientEpoch,
                    bridgeRequestId);
                    commands.Add(new JsonObject
                    {
                        ["itemId"] = itemId,
                        ["command"] = observedCommand,
                        ["commandStopState"] = "running",
                        ["commandStopAvailable"] = true,
                        ["commandStopDetail"] = "This command is still running after the turn was interrupted. Stop is separate from stopping model generation."
                    });
                }
            }

            return evidence;
        }
        catch (Exception error)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            evidence["status"] = "unavailable";
            evidence["failure"] = new JsonObject
            {
                ["attributedTo"] = "NeoBabylon.Host",
                ["type"] = error.GetType().Name,
                ["message"] = "Codex App Server did not confirm which interrupted command items are still running."
            };
            foreach (var (itemId, command) in activeCommandItems)
            {
                commands.Add(new JsonObject
                {
                    ["itemId"] = itemId,
                    ["command"] = command,
                    ["commandStopState"] = "unknown",
                    ["commandStopAvailable"] = false,
                    ["commandStopDetail"] = "Running state is unknown; no stop action is offered without a verified process identity."
                });
            }
            return evidence;
        }
        finally
        {
            lock (_turnStateLock)
            {
                if (ReferenceEquals(_commandCompletionTracker, completionTracker))
                {
                    _commandCompletionTracker = null;
                }
            }
        }
    }

    private void ObserveCommandCompletionNotification(
        AppServerClient client,
        Guid clientEpoch,
        AppServerNotification notification)
    {
        if (notification.Method != "item/completed"
            || notification.Params["item"]?["type"]?.GetValue<string>() != "commandExecution"
            || notification.Params["threadId"]?.GetValue<string>() is not { Length: > 0 } threadId
            || notification.Params["turnId"]?.GetValue<string>() is not { Length: > 0 } turnId
            || notification.Params["item"]?["id"]?.GetValue<string>() is not { Length: > 0 } itemId)
        {
            return;
        }

        ContinuingCommandIdentity? completedIdentity = null;
        lock (_turnStateLock)
        {
            if (!ReferenceEquals(_client, client) || _clientEpoch != clientEpoch)
            {
                return;
            }

            _commandCompletionTracker?.Observe(notification);
            if (_continuingCommands.TryGetValue((threadId, itemId), out var identity)
                && string.Equals(identity.TurnId, turnId, StringComparison.Ordinal)
                && identity.ClientEpoch == clientEpoch)
            {
                _continuingCommands.Remove((threadId, itemId));
                completedIdentity = identity;
            }
        }

        if (completedIdentity?.BridgeRequestId is { Length: > 0 } bridgeRequestId)
        {
            var projected = new AppServerNotificationProjection().Project(notification);
            if (projected is not null)
            {
                ContinuingCommandCompleted?.Invoke(bridgeRequestId, projected);
            }
        }
    }

    public async Task<JsonObject> StopCommandAsync(string itemId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new InvalidDataException("An exact App Server command item identifier is required.");
        }

        string? threadId;
        AppServerClient? client;
        ContinuingCommandIdentity? identity;
        lock (_turnStateLock)
        {
            threadId = _threadId;
            var matchingIdentities = _continuingCommands.Values
                .Where(candidate => string.Equals(candidate.ItemId, itemId, StringComparison.Ordinal))
                .ToArray();
            if (matchingIdentities.Length != 1)
            {
                return CommandStopHostFailure(
                    threadId,
                    null,
                    itemId,
                    matchingIdentities.Length == 0 ? "not_found" : "identity_mismatch",
                    "CommandIdentityUnavailable",
                    matchingIdentities.Length == 0
                        ? "NeoBabylon has no verified active-command identity for this item."
                        : "More than one thread has this command item identifier; NeoBabylon will not guess which one to stop.");
            }

            identity = matchingIdentities[0];
            if (threadId is null || !string.Equals(threadId, identity.ThreadId, StringComparison.Ordinal))
            {
                return CommandStopHostFailure(
                    threadId,
                    identity.TurnId,
                    itemId,
                    "identity_mismatch",
                    "SelectedThreadChanged",
                    "The selected thread changed after this command action was rendered. No command in the new thread was targeted.");
            }

            client = _client;
            if (_threadRequiresResume
                || client is null
                || client.HasExited
                || identity.ClientEpoch != _clientEpoch)
            {
                if (_continuingCommands.TryGetValue((identity.ThreadId, identity.ItemId), out var current)
                    && ReferenceEquals(current, identity))
                {
                    _continuingCommands.Remove((identity.ThreadId, identity.ItemId));
                }
                return CommandStopHostFailure(
                    identity.ThreadId,
                    identity.TurnId,
                    itemId,
                    "unknown",
                    "AppServerUnavailable",
                    "The App Server instance that owned this command is no longer current. The old Stop identity was invalidated.");
            }
        }

        try
        {
            AppServerRequest BuildStopRequest(long id) => AppServerProtocol.BuildCommandExecutionStopRequest(
                id,
                identity.ThreadId,
                identity.ItemId,
                identity.ProcessId);
#if NEOBABYLON_APPROVAL_QA
            var qaFailedResponse = BuildStopRequest(0);
            var response = TryReturnQaFailedCommandStop(qaFailedResponse, out var injectedResponse)
                ? injectedResponse
                : await client.RequestResultAsync(BuildStopRequest, cancellationToken).ConfigureAwait(false);
#else
            var response = await client.RequestResultAsync(BuildStopRequest, cancellationToken).ConfigureAwait(false);
#endif
            var status = response["status"]?.GetValue<string>();
            if (status is not ("stopped" or "already_exited" or "not_found" or "identity_mismatch" or "failed"))
            {
                status = "failed";
            }

            var serverStillCurrent = false;
            var retryAvailable = false;
            lock (_turnStateLock)
            {
                serverStillCurrent = ReferenceEquals(_client, client) && _clientEpoch == identity.ClientEpoch;
                retryAvailable = status == "failed"
                    && serverStillCurrent
                    && !_threadRequiresResume
                    && string.Equals(_threadId, identity.ThreadId, StringComparison.Ordinal)
                    && _continuingCommands.TryGetValue((identity.ThreadId, identity.ItemId), out var currentIdentity)
                    && ReferenceEquals(currentIdentity, identity);
                if (status is "stopped" or "already_exited" or "not_found" or "identity_mismatch"
                    || !serverStillCurrent
                    || (status == "failed" && !retryAvailable))
                {
                    if (_continuingCommands.TryGetValue((identity.ThreadId, identity.ItemId), out var current)
                        && ReferenceEquals(current, identity))
                    {
                        _continuingCommands.Remove((identity.ThreadId, identity.ItemId));
                    }
                }
            }

            if (status == "failed" && !serverStillCurrent)
            {
                return CommandStopHostFailure(
                    identity.ThreadId,
                    identity.TurnId,
                    identity.ItemId,
                    "unknown",
                    "AppServerReplaced",
                    "The old App Server did not confirm Stop before it was replaced. This command identity cannot be retried against the new instance.");
            }

            return new JsonObject
            {
                ["attributedTo"] = "Codex App Server",
                ["eventType"] = "commandExecutionStopResult",
                ["threadId"] = identity.ThreadId,
                ["turnId"] = identity.TurnId,
                ["itemId"] = identity.ItemId,
                ["status"] = status,
                ["commandStopAvailable"] = retryAvailable,
                ["hostOperation"] = "NeoBabylon.Host"
            };
        }
        catch (Exception error)
        {
            bool retryAvailable;
            lock (_turnStateLock)
            {
                retryAvailable = ReferenceEquals(_client, client)
                    && _clientEpoch == identity.ClientEpoch
                    && !_threadRequiresResume
                    && _continuingCommands.TryGetValue((identity.ThreadId, identity.ItemId), out var current)
                    && ReferenceEquals(current, identity);
                if (!retryAvailable
                    && _continuingCommands.TryGetValue((identity.ThreadId, identity.ItemId), out current)
                    && ReferenceEquals(current, identity))
                {
                    _continuingCommands.Remove((identity.ThreadId, identity.ItemId));
                }
            }

            return CommandStopHostFailure(
                identity.ThreadId,
                identity.TurnId,
                identity.ItemId,
                "unknown",
                error.GetType().Name,
                retryAvailable
                    ? "Codex App Server did not confirm the stop request; the command may still be running. Retry is limited to the same App Server instance."
                    : "Codex App Server did not confirm the stop request, and its original instance is no longer current. This Stop identity was invalidated.",
                retryAvailable);
        }
    }

    private static JsonObject CommandStopHostFailure(
        string? threadId,
        string? turnId,
        string itemId,
        string status,
        string failureType,
        string message,
        bool commandStopAvailable = false) => new()
    {
        ["attributedTo"] = "NeoBabylon.Host",
        ["eventType"] = "commandExecutionStopResult",
        ["threadId"] = threadId,
        ["turnId"] = turnId,
        ["itemId"] = itemId,
        ["status"] = status,
        ["commandStopAvailable"] = commandStopAvailable,
        ["failure"] = new JsonObject
        {
            ["attributedTo"] = "NeoBabylon.Host",
            ["type"] = failureType,
            ["message"] = message
        }
    };

    public async Task<JsonObject> InterruptTurnAsync(CancellationToken cancellationToken)
    {
        var client = _client ?? throw new InvalidOperationException("Start a pinned App Server thread before requesting interruption.");
        var threadId = _threadId ?? throw new InvalidOperationException("Start a pinned App Server thread before requesting interruption.");
        TaskCompletionSource<string?>? ready;
        string? turnId;
        lock (_turnStateLock)
        {
            ready = _activeTurnReady;
            turnId = _activeTurnId;
        }

        if (ready is null)
        {
            throw new InvalidOperationException("There is no active App Server turn to interrupt.");
        }

        turnId ??= await ready.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(turnId))
        {
            throw new InvalidOperationException("The App Server did not accept a turn that can be interrupted.");
        }

        await client.RequestResultAsync(
            id => AppServerProtocol.BuildTurnInterruptRequest(id, threadId, turnId),
            cancellationToken).ConfigureAwait(false);
        return new JsonObject
        {
            ["attributedTo"] = "Codex App Server",
            ["eventType"] = "turnInterruptionRequested",
            ["threadId"] = threadId,
            ["turnId"] = turnId,
            ["terminal"] = false
        };
    }

    public async Task<JsonObject> RespondToApprovalAsync(
        long approvalRequestId,
        string approvalInstanceId,
        string decision,
        string? reviewFingerprint,
        CancellationToken cancellationToken)
    {
        var client = _client
            ?? throw new InvalidOperationException("There is no active App Server to receive the approval decision.");
        var threadId = _threadId
            ?? throw new InvalidOperationException("There is no active App Server thread for this approval.");
        string? turnId;
        lock (_turnStateLock)
        {
            turnId = _activeTurnId;
        }

        if (string.IsNullOrWhiteSpace(turnId))
        {
            throw new InvalidOperationException("There is no active App Server turn for this approval.");
        }

        return await client.ResolveApprovalAsync(
            approvalRequestId,
            approvalInstanceId,
            threadId,
            turnId,
            decision,
            reviewFingerprint,
            cancellationToken).ConfigureAwait(false);
    }

    private static bool IsQualificationLoopbackEndpoint(ModelCapabilityRecord capability)
    {
        return string.Equals(capability.ProviderId, "lmstudio", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(capability.Endpoint, UriKind.Absolute, out var endpoint)
            && endpoint.Scheme == Uri.UriSchemeHttp
            && endpoint.IsLoopback
            && endpoint.AbsolutePath.TrimEnd('/').Equals("/v1", StringComparison.Ordinal)
            && string.IsNullOrEmpty(endpoint.UserInfo)
            && string.IsNullOrEmpty(endpoint.Query)
            && string.IsNullOrEmpty(endpoint.Fragment);
    }

    private static JsonObject? BuildHostFailure(TurnObservation observation, JsonArray toolDiagnostics)
    {
        var toolFailure = toolDiagnostics
            .OfType<JsonObject>()
            .FirstOrDefault(item => item["succeeded"]?.GetValue<bool>() == false);
        if (toolFailure is not null)
        {
            return new JsonObject
            {
                ["type"] = "toolExecution",
                ["attributedTo"] = "Codex App Server",
                ["message"] = toolFailure["failure"]?["message"]?.DeepClone(),
                ["details"] = toolFailure.DeepClone()
            };
        }

        return observation.ToHostFailure();
    }

    private static string ExtractThreadId(JsonObject result)
    {
        var id = result["thread"]?["id"]?.GetValue<string>() ?? result["threadId"]?.GetValue<string>();
        return !string.IsNullOrWhiteSpace(id)
            ? id
            : throw new InvalidDataException("App Server thread/start returned no thread id.");
    }

    private static string ExtractTurnId(JsonObject result)
    {
        var id = result["turn"]?["id"]?.GetValue<string>();
        return !string.IsNullOrWhiteSpace(id)
            ? id
            : throw new InvalidDataException("App Server turn/start returned no runtime-owned turn id.");
    }

    private void ClearActiveTurn(TaskCompletionSource<string?> ready)
    {
        lock (_turnStateLock)
        {
            if (ReferenceEquals(_activeTurnReady, ready))
            {
                _activeTurnReady = null;
                _activeTurnId = null;
                _compactionReservation = null;
            }
        }
    }

    private async Task DisposeClientAsync(bool preserveSelectedThread = false)
    {
        TaskCompletionSource<string?>? ready;
        AppServerClient? client;
        NvidiaAdapterProcess? nvidiaAdapter;
        lock (_turnStateLock)
        {
            ready = _activeTurnReady;
            _activeTurnReady = null;
            _activeTurnId = null;
            _compactionReservation = null;
            client = _client;
            _client = null;
            nvidiaAdapter = _nvidiaAdapter;
            _nvidiaAdapter = null;
            _clientEpoch = Guid.Empty;
            _continuingCommands.Clear();

            if (preserveSelectedThread && _threadId is not null && _activeCapability is not null)
            {
                _threadRequiresResume = true;
            }
            else
            {
                _threadId = null;
                _threadRequiresResume = false;
                _activeCapability = null;
                _activeCapabilityRecordSnapshot = null;
            }
            _activeSessionPath = null;
            _currentContextEvidenceOffset = long.MaxValue;
            _observedReasoningEffort = null;
            _reasoningEvidenceSource = null;
        }
        ready?.TrySetResult(null);
        if (client is not null)
        {
            try { await client.DisposeAsync().ConfigureAwait(false); }
            finally
            {
                if (nvidiaAdapter is not null) await nvidiaAdapter.DisposeAsync().ConfigureAwait(false);
            }
        }
        else if (nvidiaAdapter is not null)
        {
            await nvidiaAdapter.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeClientAsync().ConfigureAwait(false);
        _dataLease.Dispose();
    }
}
