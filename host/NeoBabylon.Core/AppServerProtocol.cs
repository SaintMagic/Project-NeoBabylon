using System.Text;
using System.Text.Json.Nodes;

namespace NeoBabylon.Core;

public sealed record AppServerRequest(long Id, string Method, JsonObject Params)
{
    public bool OmitParams { get; init; }

    public string ToJsonLine()
    {
        var envelope = new JsonObject
        {
            ["id"] = Id,
            ["method"] = Method
        };
        if (!OmitParams) envelope["params"] = Params;
        return envelope.ToJsonString();
    }
}

public sealed record ThreadStartOptions(
    string Model,
    string ModelProvider,
    string Cwd,
    string Sandbox,
    string ApprovalPolicy,
    IReadOnlyDictionary<string, object> Config);

public sealed record TurnStartOptions(
    string ThreadId,
    string Text,
    ToolExecutionPolicy ToolExecutionPolicy = ToolExecutionPolicy.QualificationWorkspaceWrite,
    string? ReasoningEffort = null);

public sealed record JsonlMessage(
    long? Id,
    string? Method,
    JsonObject? Result,
    JsonObject? Error,
    JsonObject? Params)
{
    public bool IsResponse => Id.HasValue && Method is null;
    public bool IsServerRequest => Id.HasValue && Method is not null;

    public static JsonlMessage Parse(string line)
    {
        var node = JsonNode.Parse(line)?.AsObject() ?? throw new InvalidDataException("App Server emitted a non-object JSONL message.");
        var id = node["id"]?.GetValue<long>();
        var method = node["method"]?.GetValue<string>();
        var result = node.Remove("result", out var resultNode) ? resultNode as JsonObject : null;
        var error = node.Remove("error", out var errorNode) ? errorNode as JsonObject : null;
        var parameters = node.Remove("params", out var paramsNode) ? paramsNode as JsonObject : null;
        return new JsonlMessage(
            id,
            method,
            result,
            error,
            parameters);
    }
}

public static class AppServerProtocol
{
    public const int MaximumApprovalPayloadCharacters = 32_768;
    public const int McpStatusPageLimit = 16;
    private const int McpControlSchemaVersion = 1;
    private const int MaximumMcpStatusResponseBytes = 128 * 1024;
    private const int MaximumMcpToolsPerServer = 64;

    private static readonly HashSet<string> PermissionProfileKeys = new(StringComparer.Ordinal)
    {
        "network",
        "fileSystem"
    };

    public static AppServerRequest BuildInitializeRequest(long id) =>
        new(
            id,
            "initialize",
            new JsonObject
            {
                ["clientInfo"] = new JsonObject
                {
                    ["name"] = "NeoBabylon",
                    ["title"] = "NeoBabylon Diagnostic Host",
                    ["version"] = "0.1.0"
                },
                ["capabilities"] = new JsonObject
                {
                    ["experimentalApi"] = false
                }
            });

    public static string BuildInitializedNotification() =>
        new JsonObject { ["method"] = "initialized", ["params"] = new JsonObject() }.ToJsonString();

    public static AppServerRequest BuildThreadStartRequest(long id, ThreadStartOptions options)
    {
        return new AppServerRequest(
            id,
            "thread/start",
            new JsonObject
            {
                ["model"] = options.Model,
                ["modelProvider"] = options.ModelProvider,
                ["allowProviderModelFallback"] = false,
                ["cwd"] = options.Cwd,
                ["sandbox"] = options.Sandbox,
                ["approvalPolicy"] = options.ApprovalPolicy
            });
    }

    public static AppServerRequest BuildConfigReadRequest(long id, string cwd) =>
        new(
            id,
            "config/read",
            new JsonObject
            {
                ["includeLayers"] = false,
                ["cwd"] = cwd
            });

    public static AppServerRequest BuildMcpServerReloadRequest(long id) =>
        new(id, "config/mcpServer/reload", new JsonObject()) { OmitParams = true };

    public static AppServerRequest BuildMcpServerStatusListRequest(
        long id, string? threadId = null, string? cursor = null)
    {
        if (threadId is not null && !IsBoundedMcpName(threadId, 128))
            throw new ArgumentException("MCP status requires a bounded thread id.", nameof(threadId));
        if (cursor is not null && (cursor.Length is 0 or > 20 || cursor.Any(character => character is < '0' or > '9')))
            throw new ArgumentException("MCP status cursor must be a bounded decimal offset.", nameof(cursor));

        var parameters = new JsonObject
        {
            ["limit"] = McpStatusPageLimit,
            ["detail"] = "toolsAndAuthOnly"
        };
        if (threadId is not null) parameters["threadId"] = threadId;
        if (cursor is not null) parameters["cursor"] = cursor;
        return new AppServerRequest(id, "mcpServerStatus/list", parameters);
    }

    public static McpServerReloadReceipt ParseMcpServerReloadResponse(JsonObject? result)
    {
        if (result is null || result.Count != 0)
            throw new InvalidDataException("App Server MCP reload returned an unexpected response shape.");
        return new McpServerReloadReceipt(McpControlSchemaVersion);
    }

    public static McpServerStatusPage ParseMcpServerStatusListResponse(JsonObject? result)
    {
        if (result is null || Encoding.UTF8.GetByteCount(result.ToJsonString()) > MaximumMcpStatusResponseBytes
            || result["data"] is not JsonArray data || data.Count > McpStatusPageLimit)
        {
            throw new InvalidDataException("App Server MCP status response is missing or exceeds its page bound.");
        }

        string? nextCursor = null;
        if (result["nextCursor"] is not null)
        {
            nextCursor = RequireMcpString(result["nextCursor"], "nextCursor", 20);
            if (nextCursor.Any(character => character is < '0' or > '9'))
                throw new InvalidDataException("App Server MCP status cursor is not a decimal offset.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var servers = new List<McpServerStatusEntry>(data.Count);
        foreach (var item in data)
        {
            if (item is not JsonObject server)
                throw new InvalidDataException("App Server MCP status entry is not an object.");
            var name = RequireMcpString(server["name"], "name", 128);
            if (!seen.Add(name))
                throw new InvalidDataException("App Server MCP status page contains a duplicate server.");
            McpServerRuntimeStatus? runtimeStatus = server["runtimeStatus"] is null
                ? null
                : ParseMcpRuntimeStatus(RequireMcpString(server["runtimeStatus"], "runtimeStatus", 32));
            var authStatus = ParseMcpAuthStatus(RequireMcpString(server["authStatus"], "authStatus", 32));
            if (server["tools"] is not JsonObject tools || tools.Count > MaximumMcpToolsPerServer)
                throw new InvalidDataException("App Server MCP tool inventory is malformed or exceeds its bound.");
            var toolNames = new List<string>(tools.Count);
            foreach (var tool in tools)
            {
                if (!IsBoundedMcpName(tool.Key, 128) || tool.Value is not JsonObject)
                    throw new InvalidDataException("App Server MCP tool inventory contains an invalid entry.");
                toolNames.Add(tool.Key);
            }
            IReadOnlyList<McpServerToolDescriptor>? toolDescriptors;
            try
            {
                // Keep the exact schemas when the pinned response provides them.
                // A names-only or incomplete inventory is still useful status, but
                // it must remain Unknown for any future callable-route decision.
                toolDescriptors = GeneratedToolMcpStatus.ParseToolDescriptors(tools);
            }
            catch (InvalidDataException)
            {
                toolDescriptors = null;
            }
            var toolsError = server["toolsError"] is null
                ? null
                : RequireMcpString(server["toolsError"], "toolsError", 1024);
            servers.Add(new McpServerStatusEntry(name, runtimeStatus, authStatus, toolNames, toolsError, toolDescriptors));
        }
        return new McpServerStatusPage(McpControlSchemaVersion, servers, nextCursor);
    }

    private static string RequireMcpString(JsonNode? node, string field, int maximumLength)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text)
            || !IsBoundedMcpName(text, maximumLength))
            throw new InvalidDataException($"App Server MCP {field} is invalid or exceeds its bound.");
        return text;
    }

    private static bool IsBoundedMcpName(string? text, int maximumLength) =>
        !string.IsNullOrWhiteSpace(text) && text.Length <= maximumLength && !text.Any(char.IsControl);

    private static McpServerRuntimeStatus ParseMcpRuntimeStatus(string value) => value switch
    {
        "notStarted" => McpServerRuntimeStatus.NotStarted,
        "starting" => McpServerRuntimeStatus.Starting,
        "connected" => McpServerRuntimeStatus.Connected,
        "authenticationRequired" => McpServerRuntimeStatus.AuthenticationRequired,
        "failed" => McpServerRuntimeStatus.Failed,
        "cancelled" => McpServerRuntimeStatus.Cancelled,
        "disabled" => McpServerRuntimeStatus.Disabled,
        _ => throw new InvalidDataException("App Server returned an unknown MCP runtime status.")
    };

    private static McpServerAuthStatus ParseMcpAuthStatus(string value) => value switch
    {
        "unknown" => McpServerAuthStatus.Unknown,
        "unsupported" => McpServerAuthStatus.Unsupported,
        "notLoggedIn" => McpServerAuthStatus.NotLoggedIn,
        "bearerToken" => McpServerAuthStatus.BearerToken,
        "oAuth" => McpServerAuthStatus.OAuth,
        _ => throw new InvalidDataException("App Server returned an unknown MCP auth status.")
    };

    public static AppServerRequest BuildThreadListRequest(
        long id,
        bool useStateDbOnly = false,
        string? cursor = null,
        string? cwd = null)
    {
        var parameters = new JsonObject
        {
            ["limit"] = 50,
            ["sortKey"] = "updated_at",
            ["sortDirection"] = "desc",
            ["archived"] = false,
            // An omitted provider filter defaults to App Server's current provider.
            // Empty explicitly includes all providers within the supplied workspace.
            ["modelProviders"] = new JsonArray(),
            ["useStateDbOnly"] = useStateDbOnly
        };
        if (cursor is not null) parameters["cursor"] = cursor;
        if (cwd is not null) parameters["cwd"] = cwd;
        return new AppServerRequest(id, "thread/list", parameters);
    }

    public static AppServerRequest BuildThreadResumeRequest(
        long id,
        string threadId,
        ToolExecutionPolicy toolExecutionPolicy = ToolExecutionPolicy.QualificationWorkspaceWrite,
        string? model = null,
        string? modelProvider = null,
        string? reasoningEffort = null,
        bool includeReasoningEffortOverride = false)
    {
        if (toolExecutionPolicy == ToolExecutionPolicy.ApprovalQualification)
        {
            throw new InvalidOperationException("The QA approval-qualification profile is limited to fresh threads.");
        }

        var parameters = new JsonObject
        {
            ["threadId"] = threadId,
            ["excludeTurns"] = true
        };
        if (model is not null) parameters["model"] = model;
        if (modelProvider is not null) parameters["modelProvider"] = modelProvider;
        if (model is not null && modelProvider is not null && includeReasoningEffortOverride
            && reasoningEffort is not null)
        {
            parameters["config"] = new JsonObject
            {
                ["model_reasoning_effort"] = reasoningEffort
            };
        }
        if (toolExecutionPolicy == ToolExecutionPolicy.Unrestricted)
        {
            parameters["sandbox"] = "danger-full-access";
            parameters["approvalPolicy"] = "never";
        }

        return new(
            id,
            "thread/resume",
            parameters);
    }

    public static AppServerRequest BuildThreadResumeRequest(
        long id,
        string threadId,
        ToolExecutionPolicy toolExecutionPolicy,
        ModelCapabilityRecord capability) =>
        BuildThreadResumeRequest(
            id, threadId, toolExecutionPolicy,
            capability.ModelIdentifier,
            capability.ProviderId,
            CapabilitySwitchSafety.GetSelectedDefaultReasoningEffort(capability),
            includeReasoningEffortOverride: true);

    public static AppServerRequest BuildThreadReadRequest(long id, string threadId, bool includeTurns) =>
        new(
            id,
            "thread/read",
            new JsonObject
            {
                ["threadId"] = threadId,
                ["includeTurns"] = includeTurns
            });

    public static AppServerRequest BuildThreadNameSetRequest(long id, string threadId, string name) =>
        new(
            id,
            "thread/name/set",
            new JsonObject
            {
                ["threadId"] = threadId,
                ["name"] = ThreadRenameIdentity.NormalizeName(name)
            });

    public static AppServerRequest BuildThreadForkRequest(
        long id,
        string threadId,
        ToolExecutionPolicy toolExecutionPolicy = ToolExecutionPolicy.QualificationWorkspaceWrite)
    {
        if (toolExecutionPolicy == ToolExecutionPolicy.ApprovalQualification)
        {
            throw new InvalidOperationException("The QA approval-qualification profile is limited to fresh threads.");
        }

        var parameters = new JsonObject
        {
            ["threadId"] = threadId,
            ["excludeTurns"] = true
        };
        if (toolExecutionPolicy == ToolExecutionPolicy.Unrestricted)
        {
            parameters["sandbox"] = "danger-full-access";
            parameters["approvalPolicy"] = "never";
        }

        return new(
            id,
            "thread/fork",
            parameters);
    }

    public static AppServerRequest BuildThreadTurnsListRequest(long id, string threadId) =>
        new(
            id,
            "thread/turns/list",
            new JsonObject
            {
                ["threadId"] = threadId,
                ["limit"] = 20,
                ["sortDirection"] = "desc",
                ["itemsView"] = "full"
            });

    public static AppServerRequest BuildThreadItemsListRequest(
        long id,
        string threadId,
        string turnId,
        string? cursor = null) =>
        new(
            id,
            "thread/items/list",
            new JsonObject
            {
                ["threadId"] = threadId,
                ["turnId"] = turnId,
                ["limit"] = 1,
                ["sortDirection"] = "desc",
                ["cursor"] = cursor
            });

    public static AppServerRequest BuildTurnStartRequest(long id, TurnStartOptions options)
    {
        var parameters = new JsonObject
        {
            ["threadId"] = options.ThreadId,
            ["input"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = options.Text
                }
            }
        };
        if (options.ToolExecutionPolicy == ToolExecutionPolicy.Unrestricted)
        {
            parameters["sandboxPolicy"] = new JsonObject { ["type"] = "dangerFullAccess" };
            parameters["approvalPolicy"] = "never";
        }
        else if (options.ToolExecutionPolicy == ToolExecutionPolicy.ApprovalQualification)
        {
            parameters["sandboxPolicy"] = new JsonObject { ["type"] = "readOnly" };
            parameters["approvalPolicy"] = "on-request";
        }
        if (options.ReasoningEffort is not null)
        {
            if (!CapabilitySwitchSafety.IsPinnedReasoningEffort(options.ReasoningEffort))
                throw new InvalidDataException("A supported exact reasoning effort string is required.");
            parameters["effort"] = options.ReasoningEffort;
        }

        return new(
            id,
            "turn/start",
            parameters);
    }

    public static AppServerRequest BuildTurnInterruptRequest(long id, string threadId, string turnId) =>
        new(
            id,
            "turn/interrupt",
            new JsonObject
            {
                ["threadId"] = threadId,
                ["turnId"] = turnId
            });

    public static AppServerRequest BuildThreadCompactStartRequest(long id, string threadId) =>
        new(id, "thread/compact/start", new JsonObject { ["threadId"] = threadId });

    public static AppServerRequest BuildCommandExecutionListRequest(long id, string threadId) =>
        new(
            id,
            "thread/commandExecution/list",
            new JsonObject { ["threadId"] = threadId });

    public static AppServerRequest BuildCommandExecutionStopRequest(
        long id,
        string threadId,
        string itemId,
        string expectedProcessId) =>
        new(
            id,
            "thread/commandExecution/stop",
            new JsonObject
            {
                ["threadId"] = threadId,
                ["itemId"] = itemId,
                ["expectedProcessId"] = expectedProcessId
            });

    public static string BuildFailClosedServerRequestResponse(JsonlMessage request)
    {
        if (!request.IsServerRequest || request.Id is not long id || request.Method is not string method)
        {
            throw new InvalidOperationException("A fail-closed response requires a server request.");
        }

        var response = new JsonObject { ["id"] = id };
        switch (method)
        {
            case "item/commandExecution/requestApproval":
            case "item/fileChange/requestApproval":
                response["result"] = new JsonObject { ["decision"] = "decline" };
                break;
            case "item/permissions/requestApproval":
                response["result"] = new JsonObject
                {
                    ["permissions"] = new JsonObject(),
                    ["scope"] = "turn"
                };
                break;
            default:
                response["error"] = new JsonObject
                {
                    ["code"] = -32001,
                    ["message"] = $"NeoBabylon has no handler for authority-bearing App Server request '{method}'."
                };
                break;
        }

        return response.ToJsonString();
    }

    public static bool IsInteractiveApprovalRequest(JsonlMessage request) => request.IsServerRequest
        && request.Method is "item/commandExecution/requestApproval"
            or "item/fileChange/requestApproval"
            or "item/permissions/requestApproval";

    public static string BuildApprovalServerRequestResponse(
        JsonlMessage request,
        string decision,
        FileChangeApprovalReview? fileChangeReview = null,
        string? reviewFingerprint = null)
    {
        if (!IsInteractiveApprovalRequest(request) || request.Id is not long id || request.Method is not string method)
        {
            throw new InvalidOperationException("Only a recognized App Server approval request can be answered interactively.");
        }

        var parameters = request.Params ?? new JsonObject();
        if (parameters.ToJsonString().Length > MaximumApprovalPayloadCharacters)
        {
            throw new InvalidOperationException("The approval payload is too large to review safely.");
        }

        var result = new JsonObject();
        switch (method)
        {
            case "item/commandExecution/requestApproval":
                if (decision is not ("accept" or "decline" or "cancel"))
                {
                    throw new InvalidOperationException("Only one-shot command approval decisions are supported.");
                }

                if (decision == "accept" && !HasValidApprovalIdentity(parameters))
                {
                    throw new InvalidOperationException("The command approval is missing its App Server thread, turn, or item identity.");
                }

                if (decision == "accept"
                    && (parameters["availableDecisions"] is not JsonArray availableDecisions
                        || !availableDecisions.Any(value => value is JsonValue stringValue
                            && stringValue.TryGetValue<string>(out var advertisedDecision)
                            && string.Equals(advertisedDecision, decision, StringComparison.Ordinal))))
                {
                    throw new InvalidOperationException("The selected command approval decision was not advertised by App Server.");
                }

                if (decision == "accept"
                    && (string.IsNullOrWhiteSpace(parameters["command"]?.GetValue<string>())
                        || parameters["additionalPermissions"] is JsonObject additionalPermissions
                            && additionalPermissions.Count > 0))
                {
                    throw new InvalidOperationException("This command request contains missing or experimental authority details and cannot be approved.");
                }

                result["decision"] = decision;
                break;

            case "item/fileChange/requestApproval":
                if (decision is not ("accept" or "decline" or "cancel"))
                {
                    throw new InvalidOperationException("Only one-shot file-change approval decisions are supported.");
                }

                if (decision == "accept" && !string.IsNullOrWhiteSpace(parameters["grantRoot"]?.GetValue<string>()))
                {
                    throw new InvalidOperationException("Persistent write-root expansion is not supported by this approval surface.");
                }

                if (decision == "accept" && !HasValidApprovalIdentity(parameters))
                {
                    throw new InvalidOperationException("The file-change approval is missing its App Server thread, turn, or item identity.");
                }

                if (decision == "accept"
                    && (fileChangeReview is null
                        || !fileChangeReview.MatchesRequest(parameters)
                        || !string.Equals(fileChangeReview.Fingerprint, reviewFingerprint, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException("File-change approval requires the exact host-held preview and matching fingerprint.");
                }

                result["decision"] = decision;
                break;

            case "item/permissions/requestApproval":
                if (decision == "deny")
                {
                    result["permissions"] = new JsonObject();
                    result["scope"] = "turn";
                    break;
                }

                if (decision != "grantRequestedForTurn"
                    || parameters["permissions"] is not JsonObject permissions
                    || !HasValidApprovalIdentity(parameters)
                    || !IsKnownPermissionProfile(permissions))
                {
                    throw new InvalidOperationException("Only a recognized permission profile copied from the original request can be granted for this turn.");
                }

                result["permissions"] = permissions.DeepClone();
                result["scope"] = "turn";
                break;
        }

        return new JsonObject
        {
            ["id"] = id,
            ["result"] = result
        }.ToJsonString();
    }

    public static bool HasValidApprovalIdentity(JsonObject parameters) =>
        !string.IsNullOrWhiteSpace(parameters["threadId"]?.GetValue<string>())
        && !string.IsNullOrWhiteSpace(parameters["turnId"]?.GetValue<string>())
        && !string.IsNullOrWhiteSpace(parameters["itemId"]?.GetValue<string>())
        && parameters["startedAtMs"] is JsonValue timestamp
        && timestamp.TryGetValue<long>(out var startedAtMs)
        && startedAtMs >= 0;

    private static bool IsKnownPermissionProfile(JsonObject permissions)
    {
        if (permissions.Count == 0 || permissions.Any(pair => !PermissionProfileKeys.Contains(pair.Key)))
        {
            return false;
        }

        if (permissions["network"] is JsonNode networkNode && networkNode is not JsonObject
            || permissions["fileSystem"] is JsonNode fileSystemNode && fileSystemNode is not JsonObject)
        {
            return false;
        }

        if (permissions["network"] is JsonObject network)
        {
            if (network.Any(pair => pair.Key != "enabled"))
            {
                return false;
            }

            if (network["enabled"] is JsonNode enabledNode
                && (enabledNode is not JsonValue enabledValue || !enabledValue.TryGetValue<bool>(out _)))
            {
                return false;
            }
        }

        if (permissions["fileSystem"] is JsonObject fileSystem)
        {
            var allowedFileSystemKeys = new HashSet<string>(StringComparer.Ordinal)
            {
                "read",
                "write",
                "globScanMaxDepth",
                "entries"
            };
            if (fileSystem.Any(pair => !allowedFileSystemKeys.Contains(pair.Key)))
            {
                return false;
            }

            foreach (var key in new[] { "read", "write" })
            {
                if (fileSystem[key] is JsonArray paths && paths.Any(path => path is not JsonValue value || !value.TryGetValue<string>(out _)))
                {
                    return false;
                }
            }

            if (fileSystem["globScanMaxDepth"] is JsonNode depthNode
                && (depthNode is not JsonValue depthValue || !depthValue.TryGetValue<int>(out var depth) || depth < 1))
            {
                return false;
            }

            if (fileSystem["entries"] is JsonArray entries && entries.Any(entry => !IsKnownFileSystemEntry(entry)))
            {
                return false;
            }

            if (fileSystem.Count == 0)
            {
                return false;
            }
        }

        return permissions.Any(pair => pair.Value is JsonObject profile && profile.Count > 0);
    }

    private static bool IsKnownFileSystemEntry(JsonNode? entry)
    {
        if (entry is not JsonObject record
            || record.Count != 2
            || record["access"]?.GetValue<string>() is not ("read" or "write" or "deny")
            || record["path"] is not JsonObject path)
        {
            return false;
        }

        var type = path["type"]?.GetValue<string>();
        return type switch
        {
            "path" => path.Count == 2 && !string.IsNullOrWhiteSpace(path["path"]?.GetValue<string>()),
            "globPattern" => path.Count == 2 && !string.IsNullOrWhiteSpace(path["pattern"]?.GetValue<string>()),
            "special" => IsKnownSpecialFileSystemPath(path),
            _ => false
        };
    }

    private static bool IsKnownSpecialFileSystemPath(JsonObject path)
    {
        if (path.Count != 2 || path["value"] is not JsonObject value || value["kind"]?.GetValue<string>() is not string kind)
        {
            return false;
        }

        return kind switch
        {
            "root" or "minimal" or "tmpdir" or "slash_tmp" => value.Count == 1,
            "project_roots" => value.Count is 1 or 2
                && (value["subpath"] is null || value["subpath"] is JsonValue subpath && subpath.TryGetValue<string>(out _)),
            _ => false
        };
    }

    public static bool IsNamedHostOperation(string operation) => operation switch
    {
        "getRuntimeStatus" or "startThread" or "startTurn" or "interruptTurn" or "stopCommand" or "getDiagnostics"
            or "respondToApproval" or "listCapabilities" or "selectCapability" or "listThreads"
            or "resumeThread" or "forkThread" or "renameSavedThread" or "newTask" or "setAppearance"
            or "addProject" or "selectProject" or "readOutputRange"
            or "getThreadUsage" or "compactContext"
            or "listGeneratedToolCandidates" or "readGeneratedToolCandidateFileRange"
            or "recordGeneratedToolReview" or "rejectGeneratedToolCandidate"
            or "listGeneratedToolReviewHistory"
            or "listGeneratedToolPreparedBindingToolIds"
            or "listGeneratedToolPreparedBindingHistory" or "readGeneratedToolPreparedBindingCurrent"
            or "prepareGeneratedToolDisabledBinding" or "revokeGeneratedToolPreparedBinding"
            or "cleanupGeneratedToolPreparedBinding" or "stageGeneratedToolDisabledMcp"
            or "getGeneratedToolActivationStatus" or "activateGeneratedTool"
            or "revokeGeneratedToolActivation" or "listGeneratedToolActivationHistory"
            or "readGeneratedToolReviewComparison" or "listProtectedRecordBackups"
            or "restoreProtectedRecordBackup" => true,
        _ => false
    };
}
