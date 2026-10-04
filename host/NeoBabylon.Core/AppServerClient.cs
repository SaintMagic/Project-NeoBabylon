using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace NeoBabylon.Core;

public sealed record AppServerLaunchOptions(
    string BinaryPath,
    string WorkingDirectory,
    string CodexHome,
    string? BaseUrl,
    string? OpenRouterApiKey = null,
    string? NvidiaSessionToken = null);

public enum ProviderCredentialProvider
{
    OpenRouter,
    Nvidia
}

/// <summary>Opaque host-owned provider credential state. It intentionally exposes no serializable properties.</summary>
public sealed class ProviderCredentialAccess
{
    private readonly string? _openRouterApiKey;
    private readonly string? _nvidiaApiKey;

    internal ProviderCredentialAccess(string? openRouterApiKey, string? nvidiaApiKey)
    {
        _openRouterApiKey = Normalize(openRouterApiKey);
        _nvidiaApiKey = Normalize(nvidiaApiKey);
    }

    public string? GetCredential(ProviderCredentialProvider provider) => provider switch
    {
        ProviderCredentialProvider.OpenRouter => _openRouterApiKey,
        ProviderCredentialProvider.Nvidia => _nvidiaApiKey,
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    public bool HasCredential(ProviderCredentialProvider provider) => GetCredential(provider) is not null;

    public override string ToString() => nameof(ProviderCredentialAccess);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public static class AppServerLaunchEnvironment
{
    public static void RequireCodexHome(JsonObject initializeResponse, string expectedCodexHome)
    {
        var reported = initializeResponse["codexHome"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(reported)
            || !Path.IsPathFullyQualified(reported)
            || !string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(reported)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedCodexHome)),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("App Server resolved a different Codex home; NeoBabylon will not use the ordinary or another runtime state root.");
        }
    }

    public static string? CaptureOpenRouterApiKeyAndClear()
    {
        var credentials = CaptureProviderCredentialsAndClear();
        return credentials.GetCredential(ProviderCredentialProvider.OpenRouter);
    }

    public static ProviderCredentialAccess CaptureProviderCredentialsAndClear()
    {
        var openRouterApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY", EnvironmentVariableTarget.Process);
        var nvidiaApiKey = Environment.GetEnvironmentVariable("NVIDIA_API_KEY", EnvironmentVariableTarget.Process);
        ClearCredentialEnvironmentVariables();
        return new ProviderCredentialAccess(openRouterApiKey, nvidiaApiKey);
    }

    public static void ClearCredentialEnvironmentVariables()
    {
        var names = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process)
            .Keys
            .OfType<string>()
            .Where(IsCredentialVariable)
            .ToArray();
        foreach (var name in names)
        {
            Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.Process);
        }
    }

    public static IReadOnlyDictionary<string, string> Build(AppServerLaunchOptions options)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CODEX_HOME"] = Path.GetFullPath(options.CodexHome),
            ["NEOBABYLON_REQUIRE_CONTAINED_TOOLS"] = "1"
        };
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            environment["CODEX_OSS_BASE_URL"] = options.BaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(options.OpenRouterApiKey))
        {
            environment["OPENROUTER_API_KEY"] = options.OpenRouterApiKey;
        }
        if (!string.IsNullOrWhiteSpace(options.NvidiaSessionToken))
        {
            environment["NEOBABYLON_PROVIDER_SESSION_TOKEN"] = options.NvidiaSessionToken;
        }

        return environment;
    }

    public static bool IsCredentialVariable(string name) =>
        name.Contains("KEY", StringComparison.OrdinalIgnoreCase)
        || name.Contains("SECRET", StringComparison.OrdinalIgnoreCase)
        || name.Contains("TOKEN", StringComparison.OrdinalIgnoreCase);
}

public sealed record DiagnosticOperation(string Name, string RequestId, JsonObject Payload)
{
    public static string? RequestIdForError(string json)
    {
        // Correlation only: this does not validate or authorize a named host operation.
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            var requestIds = document.RootElement.EnumerateObject()
                .Where(property => property.Name == "requestId")
                .Take(2)
                .ToArray();
            if (requestIds.Length != 1 || requestIds[0].Value.ValueKind != JsonValueKind.String)
            {
                return null;
            }
            var requestId = requestIds[0].Value.GetString();
            return string.IsNullOrWhiteSpace(requestId)
                || requestId.Length > 128
                || requestId.Any(char.IsControl)
                ? null
                : requestId;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static DiagnosticOperation Parse(string json)
    {
        // Validate duplicates/types before JsonNode builds its property dictionary.
        using (var document = JsonDocument.Parse(json))
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Diagnostic message must be a JSON object.");
            var properties = document.RootElement.EnumerateObject().ToArray();
            var operations = properties.Where(property => property.Name == "operation").ToArray();
            if (operations.Length != 1 || operations[0].Value.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("One exact named operation is required.");
            var operation = operations[0].Value.GetString();
            if (operation is "startTurn" or "getThreadUsage" or "compactContext")
            {
                var allowed = operation == "startTurn"
                    ? new HashSet<string>(StringComparer.Ordinal) { "operation", "requestId", "text", "maxOutputTokens", "reasoningEffort" }
                    : new HashSet<string>(StringComparer.Ordinal) { "operation", "requestId", "threadId" };
                if (properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length
                    || properties.Any(property => !allowed.Contains(property.Name)))
                    throw new InvalidDataException("This named operation accepts only its declared fields, without duplicates.");
                foreach (var field in operation == "startTurn" ? new[] { "requestId", "text" } : new[] { "requestId", "threadId" })
                {
                    var value = properties.SingleOrDefault(property => property.Name == field);
                    var maximum = field == "threadId" ? 512 : 128;
                    if (value.Name is null || value.Value.ValueKind != JsonValueKind.String)
                        throw new InvalidDataException("This named operation requires string identity/prompt fields.");
                    if (field != "text" && (string.IsNullOrWhiteSpace(value.Value.GetString())
                        || value.Value.GetString()!.Length > maximum || value.Value.GetString()!.Any(char.IsControl)))
                        throw new InvalidDataException("This named operation requires bounded non-control identity fields.");
                }
            }
            var efforts = properties.Where(property => property.Name == "reasoningEffort").ToArray();
            if (efforts.Length != 0 && (operation != "startTurn" || efforts.Length != 1
                || efforts[0].Value.ValueKind != JsonValueKind.String
                || !CapabilitySwitchSafety.IsPinnedReasoningEffort(efforts[0].Value.GetString())))
                throw new InvalidDataException("Only startTurn accepts one exact supported reasoning effort string; omit it for inherited runtime state.");
        }
        var objectNode = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidDataException("Diagnostic message must be a JSON object.");
        var name = objectNode["operation"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name) || !AppServerProtocol.IsNamedHostOperation(name))
        {
            throw new InvalidDataException("Diagnostic bridge accepts only a named NeoBabylon operation.");
        }

        using (var document = JsonDocument.Parse(json))
        {
            var outputCaps = document.RootElement.EnumerateObject()
                .Where(property => property.Name == "maxOutputTokens")
                .ToArray();
            if (outputCaps.Length != 0
                && (name != "startTurn"
                    || outputCaps.Length != 1
                    || outputCaps[0].Value.ValueKind != JsonValueKind.Number
                    || !outputCaps[0].Value.TryGetInt32(out var maxOutputTokens)
                    || maxOutputTokens <= 0))
            {
                throw new InvalidDataException("Only startTurn accepts one positive integer maxOutputTokens override.");
            }
        }

        if (name == "renameSavedThread")
        {
            using var document = JsonDocument.Parse(json);
            var properties = document.RootElement.EnumerateObject().ToArray();
            var expected = new HashSet<string>(StringComparer.Ordinal)
                { "operation", "requestId", "threadId", "name" };
            if (properties.Length != expected.Count
                || properties.Select(property => property.Name).ToHashSet(StringComparer.Ordinal).Count != expected.Count
                || properties.Any(property => !expected.Contains(property.Name)))
            {
                throw new InvalidDataException("Saved thread rename accepts only operation, requestId, threadId, and name.");
            }

            string RequiredRenameString(string field, int maximumLength)
            {
                var value = properties.Single(property => property.Name == field).Value;
                if (value.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException($"Saved thread rename requires a string {field}.");
                }
                var text = value.GetString();
                if (string.IsNullOrWhiteSpace(text) || text.Length > maximumLength || text.Any(char.IsControl))
                {
                    throw new InvalidDataException($"Saved thread rename requires a bounded {field}.");
                }
                return text;
            }

            if (RequiredRenameString("operation", name.Length) != name)
            {
                throw new InvalidDataException("Saved thread rename requires its exact named operation.");
            }
            var renameRequestId = RequiredRenameString("requestId", 128);
            var threadId = RequiredRenameString("threadId", 512);
            var threadName = ThreadRenameIdentity.NormalizeName(RequiredRenameString("name", 4096));
            objectNode["name"] = threadName;
            return new DiagnosticOperation(name, renameRequestId, objectNode);
        }

        if (name == "selectCapability")
        {
            using var document = JsonDocument.Parse(json);
            var properties = document.RootElement.EnumerateObject().ToArray();
            var expected = new HashSet<string>(StringComparer.Ordinal)
                { "operation", "requestId", "providerId", "modelIdentifier", "threadId" };
            var required = new HashSet<string>(StringComparer.Ordinal)
                { "operation", "requestId", "providerId", "modelIdentifier" };
            if (properties.Length < required.Count || properties.Length > expected.Count
                || properties.Select(property => property.Name).ToHashSet(StringComparer.Ordinal).Count != properties.Length
                || properties.Any(property => !expected.Contains(property.Name))
                || required.Any(field => properties.All(property => property.Name != field)))
            {
                throw new InvalidDataException("Capability selection accepts only providerId, modelIdentifier, and an optional threadId.");
            }

            string RequiredSelectionString(string field, int maximumLength)
            {
                var value = properties.Single(property => property.Name == field).Value;
                if (value.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException($"Capability selection requires a string {field}.");
                var text = value.GetString();
                if (string.IsNullOrWhiteSpace(text) || text.Length > maximumLength || text.Any(char.IsControl))
                    throw new InvalidDataException($"Capability selection requires a bounded {field}.");
                return text;
            }

            var selectionRequestId = RequiredSelectionString("requestId", 128);
            RequiredSelectionString("providerId", 128);
            RequiredSelectionString("modelIdentifier", 256);
            var threadProperty = properties.SingleOrDefault(property => property.Name == "threadId");
            if (threadProperty.Name is not null && threadProperty.Value.ValueKind != JsonValueKind.Null)
            {
                if (threadProperty.Value.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException("Capability selection threadId must be a saved thread identifier or null.");
                var threadId = threadProperty.Value.GetString();
                if (string.IsNullOrWhiteSpace(threadId) || threadId.Length > 512 || threadId.Any(char.IsControl))
                    throw new InvalidDataException("Capability selection threadId must be a bounded saved thread identifier or null.");
            }
            return new DiagnosticOperation(name, selectionRequestId, objectNode);
        }

        if (name is "listGeneratedToolCandidates" or "listGeneratedToolPreparedBindingToolIds")
        {
            using var document = JsonDocument.Parse(json);
            var properties = document.RootElement.EnumerateObject().ToArray();
            if (properties.Length != 2
                || properties.Count(property => property.Name == "operation") != 1
                || properties.Count(property => property.Name == "requestId") != 1
                || properties.Single(property => property.Name == "operation").Value.ValueKind != JsonValueKind.String
                || properties.Single(property => property.Name == "operation").Value.GetString() != name
                || properties.Single(property => property.Name == "requestId").Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException("Generated tool listing accepts only operation and requestId.");
            }

            var listRequestId = properties.Single(property => property.Name == "requestId").Value.GetString();
            if (string.IsNullOrWhiteSpace(listRequestId)
                || listRequestId.Length > 128
                || listRequestId.Any(char.IsControl))
            {
                throw new InvalidDataException("Generated tool listing requires a bounded request identifier.");
            }
            return new DiagnosticOperation(name, listRequestId, objectNode);
        }

        if (name == "readGeneratedToolCandidateFileRange")
        {
            using var document = JsonDocument.Parse(json);
            var properties = document.RootElement.EnumerateObject().ToArray();
            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                "operation", "requestId", "toolId", "contentIdentity", "path", "offset"
            };
            if (properties.Length != expected.Count
                || properties.Select(property => property.Name).ToHashSet(StringComparer.Ordinal).Count != expected.Count
                || properties.Any(property => !expected.Contains(property.Name)))
            {
                throw new InvalidDataException("Candidate file inspection accepts only its six named fields.");
            }

            string RequiredString(string field, int maximumLength)
            {
                var value = properties.Single(property => property.Name == field).Value;
                if (value.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException($"Candidate file inspection requires a string {field}.");
                }
                var text = value.GetString();
                if (string.IsNullOrWhiteSpace(text) || text.Length > maximumLength || text.Any(char.IsControl))
                {
                    throw new InvalidDataException($"Candidate file inspection requires a bounded {field}.");
                }
                return text;
            }

            if (RequiredString("operation", name.Length) != name)
            {
                throw new InvalidDataException("Candidate file inspection requires its exact named operation.");
            }
            var fileRequestId = RequiredString("requestId", 128);
            var toolId = RequiredString("toolId", 80);
            if (!char.IsAsciiLetterOrDigit(toolId[0])
                || toolId.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            {
                throw new InvalidDataException("Candidate file inspection requires a bounded tool identity.");
            }
            const string identityPrefix = "candidate-v1:sha256:";
            var identity = RequiredString("contentIdentity", identityPrefix.Length + 64);
            if (!identity.StartsWith(identityPrefix, StringComparison.Ordinal)
                || identity.Length != identityPrefix.Length + 64
                || identity[identityPrefix.Length..].Any(character =>
                    !(character >= '0' && character <= '9') && !(character >= 'a' && character <= 'f')))
            {
                throw new InvalidDataException("Candidate file inspection requires an exact content identity.");
            }
            RequiredString("path", 1024);
            var offset = properties.Single(property => property.Name == "offset").Value;
            if (offset.ValueKind != JsonValueKind.Number || !offset.TryGetInt32(out var start) || start < 0)
            {
                throw new InvalidDataException("Candidate file inspection requires a nonnegative integer offset.");
            }
            return new DiagnosticOperation(name, fileRequestId, objectNode);
        }

        if (name is "recordGeneratedToolReview" or "rejectGeneratedToolCandidate"
            or "listGeneratedToolReviewHistory")
        {
            using var document = JsonDocument.Parse(json);
            var properties = document.RootElement.EnumerateObject().ToArray();
            var isDecision = name != "listGeneratedToolReviewHistory";
            var expected = isDecision
                ? new HashSet<string>(StringComparer.Ordinal)
                    { "operation", "requestId", "toolId", "contentIdentity", "note" }
                : new HashSet<string>(StringComparer.Ordinal)
                    { "operation", "requestId", "toolId" };
            if (properties.Length != expected.Count
                || properties.Select(property => property.Name).ToHashSet(StringComparer.Ordinal).Count != expected.Count
                || properties.Any(property => !expected.Contains(property.Name)))
            {
                throw new InvalidDataException("Generated tool review accepts only its named fields.");
            }

            string RequiredReviewString(string field, int maximumLength)
            {
                var value = properties.Single(property => property.Name == field).Value;
                if (value.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException($"Generated tool review requires a string {field}.");
                }
                var text = value.GetString();
                if (string.IsNullOrWhiteSpace(text) || text.Length > maximumLength || text.Any(char.IsControl))
                {
                    throw new InvalidDataException($"Generated tool review requires a bounded {field}.");
                }
                return text;
            }

            if (RequiredReviewString("operation", name.Length) != name)
            {
                throw new InvalidDataException("Generated tool review requires its exact named operation.");
            }
            var reviewRequestId = RequiredReviewString("requestId", 128);
            var toolId = RequiredReviewString("toolId", 80);
            static bool IsLowercaseAlphanumeric(char character) =>
                (character >= 'a' && character <= 'z') || (character >= '0' && character <= '9');
            if (!IsLowercaseAlphanumeric(toolId[0])
                || toolId.Any(character => !IsLowercaseAlphanumeric(character) && character != '-'))
            {
                throw new InvalidDataException("Generated tool review requires a bounded tool identity.");
            }
            if (isDecision)
            {
                const string identityPrefix = "candidate-v1:sha256:";
                var identity = RequiredReviewString("contentIdentity", identityPrefix.Length + 64);
                if (!identity.StartsWith(identityPrefix, StringComparison.Ordinal)
                    || identity.Length != identityPrefix.Length + 64
                    || identity[identityPrefix.Length..].Any(character =>
                        !(character >= '0' && character <= '9') && !(character >= 'a' && character <= 'f')))
                {
                    throw new InvalidDataException("Generated tool review requires an exact content identity.");
                }
                RequiredReviewString("note", 2048);
            }
            return new DiagnosticOperation(name, reviewRequestId, objectNode);
        }

        if (name is "listGeneratedToolPreparedBindingHistory" or "readGeneratedToolPreparedBindingCurrent"
            or "prepareGeneratedToolDisabledBinding" or "revokeGeneratedToolPreparedBinding"
            or "cleanupGeneratedToolPreparedBinding" or "stageGeneratedToolDisabledMcp")
        {
            using var document = JsonDocument.Parse(json);
            var properties = document.RootElement.EnumerateObject().ToArray();
            var isRead = name is "listGeneratedToolPreparedBindingHistory" or "readGeneratedToolPreparedBindingCurrent";
            var isDenial = name is "revokeGeneratedToolPreparedBinding" or "cleanupGeneratedToolPreparedBinding";
            var isStage = name == "stageGeneratedToolDisabledMcp";
            var expected = new HashSet<string>(StringComparer.Ordinal) { "operation", "requestId", "toolId" };
            if (!isRead)
            {
                expected.UnionWith(["contentIdentity", "reviewIdentity"]);
                if (!isStage) expected.Add("note");
            }
            if (isDenial || isStage)
            {
                expected.Add("currentRecordSha256");
            }
            if (properties.Length != expected.Count
                || properties.Select(property => property.Name).ToHashSet(StringComparer.Ordinal).Count != expected.Count
                || properties.Any(property => !expected.Contains(property.Name)))
            {
                throw new InvalidDataException("Prepared binding accepts only its exact named fields.");
            }

            string RequiredBindingString(string field, int maximumLength)
            {
                var value = properties.Single(property => property.Name == field).Value;
                if (value.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException($"Prepared binding requires a string {field}.");
                }
                var text = value.GetString();
                if (string.IsNullOrWhiteSpace(text) || text.Length > maximumLength || text.Any(char.IsControl))
                {
                    throw new InvalidDataException($"Prepared binding requires a bounded {field}.");
                }
                return text;
            }

            static bool IsLowercaseAlphanumeric(char character) =>
                (character >= 'a' && character <= 'z') || (character >= '0' && character <= '9');
            static bool IsLowercaseSha256(string value) =>
                value.Length == 64 && value.All(character =>
                    (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));
            static bool IsIdentity(string value, string prefix) =>
                value.StartsWith(prefix, StringComparison.Ordinal)
                && IsLowercaseSha256(value[prefix.Length..]);

            if (RequiredBindingString("operation", name.Length) != name)
            {
                throw new InvalidDataException("Prepared binding requires its exact named operation.");
            }
            var bindingRequestId = RequiredBindingString("requestId", 128);
            var toolId = RequiredBindingString("toolId", 80);
            if (!IsLowercaseAlphanumeric(toolId[0])
                || toolId.Any(character => !IsLowercaseAlphanumeric(character) && character != '-'))
            {
                throw new InvalidDataException("Prepared binding requires a bounded tool identity.");
            }
            if (!isRead)
            {
                var contentIdentity = RequiredBindingString("contentIdentity", "candidate-v1:sha256:".Length + 64);
                var reviewIdentity = RequiredBindingString("reviewIdentity", "review-v1:sha256:".Length + 64);
                if (!IsIdentity(contentIdentity, "candidate-v1:sha256:")
                    || !IsIdentity(reviewIdentity, "review-v1:sha256:"))
                {
                    throw new InvalidDataException("Prepared binding requires exact candidate and review identities.");
                }
                if ((isDenial || isStage) && !IsLowercaseSha256(RequiredBindingString("currentRecordSha256", 64)))
                {
                    throw new InvalidDataException("Prepared binding operation requires an exact current record hash.");
                }
                if (!isStage) RequiredBindingString("note", 2048);
            }
            return new DiagnosticOperation(name, bindingRequestId, objectNode);
        }

        if (name is "getGeneratedToolActivationStatus" or "activateGeneratedTool"
            or "revokeGeneratedToolActivation" or "listGeneratedToolActivationHistory"
            or "readGeneratedToolReviewComparison")
        {
            using var document = JsonDocument.Parse(json);
            var properties = document.RootElement.EnumerateObject().ToArray();
            var isActivation = name == "activateGeneratedTool";
            var isRevocation = name == "revokeGeneratedToolActivation";
            var isComparison = name == "readGeneratedToolReviewComparison";
            var isMutation = isActivation || isRevocation;
            var expected = new HashSet<string>(StringComparer.Ordinal)
                { "operation", "requestId", "toolId" };
            if (isActivation)
            {
                expected.UnionWith(["contentIdentity", "reviewIdentity", "expectedBindingRecordSha256", "note"]);
            }
            else if (isRevocation)
            {
                expected.UnionWith(["expectedActivationRecordSha256", "note"]);
            }
            else if (isComparison)
            {
                expected.Add("contentIdentity");
            }

            if (properties.Length != expected.Count
                || properties.Select(property => property.Name).ToHashSet(StringComparer.Ordinal).Count != expected.Count
                || properties.Any(property => !expected.Contains(property.Name)))
            {
                throw new InvalidDataException("Generated tool activation accepts only its exact named fields.");
            }

            string RequiredActivationString(string field, int maximumLength)
            {
                var value = properties.Single(property => property.Name == field).Value;
                if (value.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException($"Generated tool activation requires a string {field}.");
                }
                var text = value.GetString();
                if (string.IsNullOrWhiteSpace(text) || text.Length > maximumLength || text.Any(char.IsControl))
                {
                    throw new InvalidDataException($"Generated tool activation requires a bounded {field}.");
                }
                return text;
            }

            static bool IsLowercaseAlphanumeric(char character) =>
                character is >= 'a' and <= 'z' or >= '0' and <= '9';
            static bool IsLowercaseSha256(string value) =>
                value.Length == 64 && value.All(character =>
                    character is >= '0' and <= '9' or >= 'a' and <= 'f');
            static bool IsIdentity(string value, string prefix) =>
                value.StartsWith(prefix, StringComparison.Ordinal)
                && IsLowercaseSha256(value[prefix.Length..]);

            if (RequiredActivationString("operation", name.Length) != name)
            {
                throw new InvalidDataException("Generated tool activation requires its exact named operation.");
            }
            var activationRequestId = RequiredActivationString("requestId", 128);
            var toolId = RequiredActivationString("toolId", 80);
            if (!IsLowercaseAlphanumeric(toolId[0])
                || toolId.Any(character => !IsLowercaseAlphanumeric(character) && character != '-'))
            {
                throw new InvalidDataException("Generated tool activation requires a bounded tool identity.");
            }

            if (isActivation || isComparison)
            {
                var contentIdentity = RequiredActivationString("contentIdentity", "candidate-v1:sha256:".Length + 64);
                if (!IsIdentity(contentIdentity, "candidate-v1:sha256:"))
                {
                    throw new InvalidDataException("Generated tool activation requires an exact candidate content identity.");
                }
            }
            if (isActivation)
            {
                var reviewIdentity = RequiredActivationString("reviewIdentity", "review-v1:sha256:".Length + 64);
                if (!IsIdentity(reviewIdentity, "review-v1:sha256:"))
                {
                    throw new InvalidDataException("Generated tool activation requires an exact review identity.");
                }
                var bindingHash = RequiredActivationString("expectedBindingRecordSha256", 64);
                if (!IsLowercaseSha256(bindingHash))
                {
                    throw new InvalidDataException("Generated tool activation requires the exact prepared binding record hash.");
                }
            }
            if (isRevocation)
            {
                var activationHash = RequiredActivationString("expectedActivationRecordSha256", 64);
                if (!IsLowercaseSha256(activationHash))
                {
                    throw new InvalidDataException("Generated tool revocation requires the exact activation record hash.");
                }
            }
            if (isMutation)
            {
                RequiredActivationString("note", 2048);
            }

            return new DiagnosticOperation(name, activationRequestId, objectNode);
        }

        if (name is "listProtectedRecordBackups" or "restoreProtectedRecordBackup")
        {
            using var document = JsonDocument.Parse(json);
            var properties = document.RootElement.EnumerateObject().ToArray();
            var isRestore = name == "restoreProtectedRecordBackup";
            var expected = isRestore
                ? new HashSet<string>(StringComparer.Ordinal)
                    { "operation", "requestId", "recordKey", "expectedBackupSha256" }
                : new HashSet<string>(StringComparer.Ordinal)
                    { "operation", "requestId", "recordKey" };
            if (properties.Length != expected.Count
                || properties.Select(property => property.Name).ToHashSet(StringComparer.Ordinal).Count != expected.Count
                || properties.Any(property => !expected.Contains(property.Name)))
            {
                throw new InvalidDataException("Protected-record backup accepts only its exact named fields.");
            }

            string RequiredBackupString(string field, int maximumLength)
            {
                var value = properties.Single(property => property.Name == field).Value;
                if (value.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException($"Protected-record backup requires a string {field}.");
                }
                var text = value.GetString();
                if (string.IsNullOrWhiteSpace(text) || text.Length > maximumLength || text.Any(char.IsControl))
                {
                    throw new InvalidDataException($"Protected-record backup requires a bounded {field}.");
                }
                return text;
            }

            if (RequiredBackupString("operation", name.Length) != name)
            {
                throw new InvalidDataException("Protected-record backup requires its exact named operation.");
            }
            var backupRequestId = RequiredBackupString("requestId", 128);
            var recordKey = RequiredBackupString("recordKey", 32);
            if (recordKey is not ("Projects" or "ForkBookmarks"))
            {
                throw new InvalidDataException("Protected-record backup accepts only Projects or ForkBookmarks.");
            }
            if (isRestore)
            {
                var expectedBackupSha256 = RequiredBackupString("expectedBackupSha256", 64);
                if (expectedBackupSha256.Length != 64
                    || expectedBackupSha256.Any(character =>
                        character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
                {
                    throw new InvalidDataException("Protected-record restore requires an exact lowercase SHA-256 backup identity.");
                }
            }

            return new DiagnosticOperation(name, backupRequestId, objectNode);
        }

        var requestId = objectNode["requestId"]?.GetValue<string>() ?? string.Empty;
        return new DiagnosticOperation(name, requestId, objectNode);
    }
}

public sealed record AppServerNotification(string Method, JsonObject Params);

public sealed record TurnObservation(
    IReadOnlyList<AppServerNotification> Notifications,
    bool Terminal,
    string? Status,
    string? Failure,
    JsonNode? FailureDetails = null,
    bool NotificationsTruncated = false)
{
    public bool Completed => Terminal && Status == "completed";
    public bool Interrupted => Terminal && Status == "interrupted";

    public JsonObject? ToHostFailure()
    {
        if (string.IsNullOrWhiteSpace(Failure))
        {
            return null;
        }

        var result = new JsonObject
        {
            ["type"] = "appServerTurn",
            ["attributedTo"] = "Codex App Server",
            ["message"] = Failure
        };
        if (FailureDetails is not null)
        {
            result["details"] = FailureDetails.DeepClone();
        }

        return result;
    }

    private AppServerNotification? FinalAssistantCompletion => Notifications.LastOrDefault(notification =>
        notification.Method == "item/completed"
        && notification.Params["item"]?["type"]?.GetValue<string>() == "agentMessage"
        && !string.IsNullOrWhiteSpace(notification.Params["item"]?["id"]?.GetValue<string>()));

    private string? FinalAssistantTruncationItemId => Notifications
        .Where(notification => notification.Method == "neobabylon/outputTruncated"
            && notification.Params["itemType"]?.GetValue<string>() == "agentMessage")
        .Select(notification => notification.Params["itemId"]?.GetValue<string>())
        .LastOrDefault(id => !string.IsNullOrWhiteSpace(id));

    public string? FinalAssistantText => FinalAssistantCompletion?.Params["item"]?["text"]?.GetValue<string>();

    public string? FinalAssistantItemId =>
        FinalAssistantCompletion?.Params["item"]?["id"]?.GetValue<string>() ?? FinalAssistantTruncationItemId;

    public JsonNode? FinalAssistantDisplay
    {
        get
        {
            var itemId = FinalAssistantItemId;
            if (itemId is null)
            {
                return null;
            }

            var completedDisplay = FinalAssistantCompletion?.Params["item"]?["neoBabylonDisplay"];
            if (completedDisplay is not null)
            {
                return completedDisplay.DeepClone();
            }

            return Notifications
                .Where(notification => notification.Method == "neobabylon/outputTruncated"
                    && notification.Params["itemType"]?.GetValue<string>() == "agentMessage"
                    && notification.Params["itemId"]?.GetValue<string>() == itemId)
                .Select(notification => notification.Params["neoBabylonDisplay"])
                .LastOrDefault()?.DeepClone();
        }
    }
}

public sealed class AppServerRpcException : Exception
{
    public AppServerRpcException(string message) : base(message)
    {
    }
}

public sealed class AppServerClient : IAsyncDisposable
{
    public static AppServerRequest BuildTurnStartRequest(
        long id,
        TurnStartOptions options,
        ModelCapabilityRecord capability,
        int? maxOutputTokens)
    {
        CapabilitySwitchSafety.RequireSupportedReasoningEffort(capability, options.ReasoningEffort);
        var request = AppServerProtocol.BuildTurnStartRequest(id, options);
        request.Params["model"] = capability.ModelIdentifier;
        if (maxOutputTokens is not int requested)
        {
            return request;
        }

        if (requested <= 0)
        {
            throw new InvalidDataException("maxOutputTokens must be a positive integer.");
        }

        var advertised = capability.MaxCompletionTokensAdvertised;
        if (advertised.State == CapabilityState.Known)
        {
            if (advertised.Int32Value is not int maximum || maximum <= 0)
            {
                throw new InvalidDataException("The advertised maximum completion-token capability is invalid; the override cannot be verified.");
            }

            if (requested > maximum)
            {
                throw new InvalidDataException("maxOutputTokens exceeds the selected model's advertised maximum completion tokens.");
            }
        }

        request.Params["maxOutputTokens"] = requested;
        return request;
    }

    private enum ApprovalResponseState
    {
        AwaitingDecision,
        ResponseCommitted,
        OutcomeUnknown,
        Resolved
    }

    private sealed class FileChangeReviewState
    {
        public object Gate { get; } = new();
        public bool ItemStartedObserved { get; set; }
        public bool Invalidated { get; set; }
        public FileChangeApprovalReview? Review { get; set; }
        public PendingApproval? Pending { get; set; }
    }

    private sealed class PendingApproval(
        JsonlMessage request,
        CancellationTokenSource timeoutCancellation,
        FileChangeReviewState? fileChangeReviewState)
    {
        private readonly object _gate = fileChangeReviewState?.Gate ?? new object();

        public JsonlMessage Request { get; } = request;
        public CancellationTokenSource TimeoutCancellation { get; } = timeoutCancellation;
        public string ApprovalInstanceId { get; } = Guid.NewGuid().ToString("N");
        public FileChangeReviewState? FileChangeReviewState { get; } = fileChangeReviewState;
        public ApprovalResponseState ResponseState { get; set; } = ApprovalResponseState.AwaitingDecision;
        public string? CommittedDecision { get; set; }

        public object Gate => _gate;
    }

    private readonly record struct FileChangeReviewKey(string ThreadId, string TurnId, string ItemId);

    private readonly Process _process;
    private readonly StreamWriter _input;
    private readonly Task<string> _stderrTask;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonlMessage>> _pending = new();
    private readonly ConcurrentDictionary<long, PendingApproval> _pendingApprovals = new();
    private readonly ConcurrentDictionary<FileChangeReviewKey, FileChangeReviewState> _fileChangeReviews = new();
    private readonly Channel<AppServerNotification> _notifications = Channel.CreateBounded<AppServerNotification>(
        new BoundedChannelOptions(128)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly SemaphoreSlim _notificationPublishLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _readLoop;
    private readonly TimeSpan _approvalTimeout;
    private long _nextRequestId;
    private int _disposed;

    private AppServerClient(Process process, TimeSpan approvalTimeout)
    {
        _process = process;
        _approvalTimeout = approvalTimeout;
        _input = process.StandardInput;
        _stderrTask = process.StandardError.ReadToEndAsync();
        _readLoop = ReadLoopAsync();
    }

    public bool HasExited => _process.HasExited;
    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;
    public int ProcessId => _process.Id;
    public long ProcessStartTimeUtcTicks => _process.StartTime.ToUniversalTime().Ticks;

    public event Action<AppServerNotification>? NotificationObserved;

    public static AppServerClient Start(AppServerLaunchOptions options, TimeSpan? approvalTimeout = null)
    {
        var timeout = approvalTimeout ?? TimeSpan.FromMinutes(5);
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(nameof(approvalTimeout), "Approval timeout must be positive and no longer than one day.");
        }

        if (!File.Exists(options.BinaryPath))
        {
            throw new FileNotFoundException("Pinned App Server binary is missing.", options.BinaryPath);
        }

        Directory.CreateDirectory(options.WorkingDirectory);
        Directory.CreateDirectory(options.CodexHome);
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.GetFullPath(options.BinaryPath),
            WorkingDirectory = Path.GetFullPath(options.WorkingDirectory),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var name in startInfo.Environment.Keys.ToArray())
        {
            if (AppServerLaunchEnvironment.IsCredentialVariable(name))
            {
                startInfo.Environment.Remove(name);
            }
        }
        startInfo.ArgumentList.Add("--listen");
        startInfo.ArgumentList.Add("stdio://");
        foreach (var pair in AppServerLaunchEnvironment.Build(options))
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("Pinned App Server process did not start.");
        }

        return new AppServerClient(process, timeout);
    }

    public async Task<JsonObject> InitializeAsync(CancellationToken cancellationToken = default)
    {
        var response = await RequestAsync(AppServerProtocol.BuildInitializeRequest(NextId()), cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, "initialize");
        await SendRawJsonLineAsync(AppServerProtocol.BuildInitializedNotification(), cancellationToken).ConfigureAwait(false);
        return response.Result ?? new JsonObject();
    }

    public async Task<JsonObject> RequestResultAsync(AppServerRequest request, CancellationToken cancellationToken = default)
    {
        var response = await RequestAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, request.Method);
        return response.Result ?? new JsonObject();
    }

    public Task<JsonObject> RequestResultAsync(
        Func<long, AppServerRequest> buildRequest,
        CancellationToken cancellationToken = default) =>
        RequestResultAsync(buildRequest(NextId()), cancellationToken);

    public Task<McpServerReloadReceipt> ReloadMcpServersAsync(
        CancellationToken cancellationToken = default, TimeSpan? timeout = null) =>
        RequestMcpControlAsync(
            AppServerProtocol.BuildMcpServerReloadRequest,
            AppServerProtocol.ParseMcpServerReloadResponse,
            cancellationToken, timeout);

    public Task<McpServerStatusPage> ListMcpServerStatusAsync(
        string? threadId = null, string? cursor = null,
        CancellationToken cancellationToken = default, TimeSpan? timeout = null)
    {
        return RequestMcpControlAsync(
            id => AppServerProtocol.BuildMcpServerStatusListRequest(id, threadId, cursor),
            AppServerProtocol.ParseMcpServerStatusListResponse,
            cancellationToken, timeout);
    }

    private async Task<T> RequestMcpControlAsync<T>(
        Func<long, AppServerRequest> buildRequest,
        Func<JsonObject?, T> parseResponse,
        CancellationToken cancellationToken,
        TimeSpan? timeout)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(10);
        if (effectiveTimeout <= TimeSpan.Zero || effectiveTimeout > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(timeout), "MCP control timeout must be positive and at most 30 seconds.");

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(effectiveTimeout);
        JsonlMessage response;
        try
        {
            response = await RequestAsync(buildRequest(NextId()), bounded.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new McpControlException(McpControlFailureKind.Cancelled, "MCP control request was cancelled.", exception);
        }
        catch (OperationCanceledException exception) when (bounded.IsCancellationRequested)
        {
            throw new McpControlException(McpControlFailureKind.Timeout, "MCP control request timed out.", exception);
        }
        catch (OperationCanceledException exception)
        {
            throw new McpControlException(McpControlFailureKind.Transport, "MCP control transport stopped.", exception);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException
            or InvalidOperationException or InvalidDataException or System.Text.Json.JsonException)
        {
            throw new McpControlException(McpControlFailureKind.Transport, "MCP control transport failed.", exception);
        }

        if (response.Error is not null)
            throw new McpControlException(McpControlFailureKind.RpcRejected, "App Server rejected the MCP control request.");
        try
        {
            return parseResponse(response.Result);
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException
            or System.Text.Json.JsonException)
        {
            throw new McpControlException(McpControlFailureKind.InvalidResponse,
                "App Server returned an invalid MCP control response.", exception);
        }
    }

    public async Task<JsonObject> ResolveApprovalAsync(
        long requestId,
        string approvalInstanceId,
        string expectedThreadId,
        string expectedTurnId,
        string decision,
        string? reviewFingerprint = null,
        CancellationToken cancellationToken = default)
    {
        if (!_pendingApprovals.TryGetValue(requestId, out var pending))
        {
            throw new InvalidOperationException("The App Server approval is no longer pending.");
        }

        var request = pending.Request;
        var requestThreadId = request.Params?["threadId"]?.GetValue<string>();
        var requestTurnId = request.Params?["turnId"]?.GetValue<string>();
        string response;
        lock (pending.Gate)
        {
            if (string.IsNullOrWhiteSpace(approvalInstanceId)
                || !string.Equals(pending.ApprovalInstanceId, approvalInstanceId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The App Server approval instance is stale or does not match the current request.");
            }

            if (string.IsNullOrWhiteSpace(expectedThreadId)
                || !string.Equals(requestThreadId, expectedThreadId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The approval does not belong to the active App Server thread.");
            }

            if (string.IsNullOrWhiteSpace(expectedTurnId)
                || !string.Equals(requestTurnId, expectedTurnId, StringComparison.Ordinal)
                || request.Params is not JsonObject parameters
                || !AppServerProtocol.HasValidApprovalIdentity(parameters))
            {
                throw new InvalidOperationException("The approval does not belong to the active App Server turn or lacks its exact item identity.");
            }

            if (pending.ResponseState != ApprovalResponseState.AwaitingDecision)
            {
                throw new InvalidOperationException("The App Server approval already has a committed response or is no longer resolvable.");
            }

            var reviewState = pending.FileChangeReviewState;
            var review = reviewState is { Invalidated: false } ? reviewState.Review : null;
            response = AppServerProtocol.BuildApprovalServerRequestResponse(request, decision, review, reviewFingerprint);
            pending.ResponseState = ApprovalResponseState.ResponseCommitted;
            pending.CommittedDecision = decision;
        }

        CancelTimeout(pending.TimeoutCancellation);
        try
        {
            await SendRawJsonLineAsync(response, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            lock (pending.Gate)
            {
                pending.ResponseState = ApprovalResponseState.OutcomeUnknown;
            }

            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }

            throw;
        }

        return new JsonObject
        {
            ["attributedTo"] = "Codex App Server",
            ["eventType"] = "approvalResolved",
            ["approvalRequestId"] = requestId,
            ["approvalInstanceId"] = pending.ApprovalInstanceId,
            ["turnId"] = expectedTurnId,
            ["decision"] = decision
        };
    }

    public async Task<TurnObservation> WaitForTurnCompletionAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        string? expectedThreadId = null,
        string? expectedTurnId = null,
        Action<AppServerNotification>? notificationObserver = null)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var notifications = new BoundedTurnNotificationAccumulator();
        var outputProjection = new AppServerNotificationProjection();

        TurnObservation Finish(bool terminal, string? status, string? failure, JsonNode? failureDetails = null)
        {
            foreach (var summary in outputProjection.Complete(turnCompleted: status == "completed"))
            {
                notifications.Add(summary);
                notificationObserver?.Invoke(summary);
            }

            return new TurnObservation(
                notifications.Snapshot(),
                terminal,
                status,
                failure,
                failureDetails,
                notifications.Truncated);
        }

        while (true)
        {
            try
            {
                var notification = await _notifications.Reader.ReadAsync(timeoutSource.Token).ConfigureAwait(false);
                if (notification.Method == "neobabylon/approvalRequested"
                    && expectedThreadId is not null
                    && expectedTurnId is not null
                    && !ApprovalMatchesActiveTurn(notification, expectedThreadId, expectedTurnId))
                {
                    notification = await DenyPendingApprovalAsync(notification, "approvalThreadOrTurnMismatch").ConfigureAwait(false);
                }

                if (!MatchesExpectedTurn(notification, expectedThreadId, expectedTurnId))
                {
                    continue;
                }

                var projectedNotification = outputProjection.Project(notification);
                if (projectedNotification is not null)
                {
                    notifications.Add(projectedNotification);
                    notificationObserver?.Invoke(projectedNotification);
                }

                if (notification.Method == "turn/completed")
                {
                    var threadId = notification.Params["threadId"]?.GetValue<string>();
                    var turn = notification.Params["turn"] as JsonObject;
                    var turnId = turn?["id"]?.GetValue<string>();
                    var status = turn?["status"]?.GetValue<string>();

                    if (expectedThreadId is not null && threadId is null
                        || expectedTurnId is not null && turnId is null)
                    {
                        return Finish(
                            true,
                            status,
                            "App Server emitted a terminal turn event without the thread/turn identity needed to correlate it.");
                    }

                    if (expectedThreadId is not null && threadId != expectedThreadId
                        || expectedTurnId is not null && turnId != expectedTurnId)
                    {
                        continue;
                    }

                    var failure = status switch
                    {
                        "completed" or "interrupted" => null,
                        "failed" => turn?["error"]?["message"]?.GetValue<string>()
                            ?? turn?["error"]?.ToJsonString()
                            ?? "App Server reported a failed turn without an error detail.",
                        _ => $"App Server emitted a terminal turn event with unrecognized status '{status ?? "Unknown"}'."
                    };
                    var failureDetails = status == "failed" ? turn?["error"]?.DeepClone() : null;
                    return Finish(true, status, failure, failureDetails);
                }

                if (notification.Method is "error" or "turn/failed" or "turn/aborted")
                {
                    var interrupted = notification.Method == "turn/aborted";
                    var failureDetails = interrupted
                        ? null
                        : notification.Params["error"]?.DeepClone() ?? notification.Params.DeepClone();
                    return Finish(
                        true,
                        interrupted ? "interrupted" : "failed",
                        interrupted ? null : ExtractFailureMessage(notification.Params),
                        failureDetails);
                }
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return Finish(
                    false,
                    null,
                    $"Turn did not complete within {timeout:g}; partial notifications were retained.");
            }
            catch (ChannelClosedException ex)
            {
                var cause = ex.InnerException is null ? string.Empty : $" ({ex.InnerException.GetType().Name})";
                return Finish(
                    false,
                    null,
                    $"Pinned Codex App Server protocol stream closed before a terminal turn event{cause}.");
            }
        }
    }

    public async Task<ManualCompactionTracker> CompactContextAsync(
        string threadId,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<AppServerNotification>? notificationObserver = null,
        Action<string>? turnStarted = null)
    {
        var tracker = new ManualCompactionTracker(threadId);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(timeout);
        // This named operation is called only under the host's exclusive turn reservation.
        // Drop residual notifications from prior completed/resumed turns before dispatch.
        var discarded = 0;
        while (discarded < 128 && _notifications.Reader.TryRead(out _)) discarded++;
        // A saturated drain cannot prove quiescence: the bounded channel may
        // still have a blocked publisher, even if its next write has not landed.
        if (discarded == 128 || _notifications.Reader.TryPeek(out _) || _notificationPublishLock.CurrentCount == 0)
        {
            tracker.RejectBeforeDispatch();
            return tracker;
        }
        var projection = new AppServerNotificationProjection();
        try
        {
            var queued = await RequestResultAsync(
                id => AppServerProtocol.BuildThreadCompactStartRequest(id, threadId), bounded.Token).ConfigureAwait(false);
            if (queued.Count != 0)
            {
                tracker.FinishUnknown("unexpectedReply");
                return tracker;
            }
            // The stable empty RPC reply means queued, not completed.
            while (!tracker.Finished)
            {
                var notification = await _notifications.Reader.ReadAsync(bounded.Token).ConfigureAwait(false);
                if (tracker.Observe(notification))
                {
                    if (notification.Method == "turn/started" && tracker.TurnId is string turnId) turnStarted?.Invoke(turnId);
                    var projected = projection.Project(notification);
                    if (projected is not null) notificationObserver?.Invoke(projected);
                }
            }
        }
        catch (AppServerRpcException) { tracker.RejectRequest(); }
        catch (OperationCanceledException) { tracker.FinishUnknown(cancellationToken.IsCancellationRequested ? "cancelled" : "timeout"); }
        catch (Exception error) when (error is IOException or ChannelClosedException or InvalidOperationException)
        {
            tracker.FinishUnknown("transportFailure");
        }
        return tracker;
    }

    private static string ExtractFailureMessage(JsonObject parameters)
    {
        var error = parameters["error"] as JsonObject ?? parameters;
        return error["message"]?.GetValue<string>() ?? error.ToJsonString();
    }

    private static bool MatchesExpectedTurn(
        AppServerNotification notification,
        string? expectedThreadId,
        string? expectedTurnId)
    {
        if (expectedThreadId is null && expectedTurnId is null)
        {
            return true;
        }

        if (!notification.Method.StartsWith("item/", StringComparison.Ordinal)
            && notification.Method is not ("turn/started" or "turn/diff/updated"))
        {
            return true;
        }

        var threadId = notification.Params["threadId"]?.GetValue<string>();
        var turnId = notification.Params["turnId"]?.GetValue<string>()
            ?? notification.Params["turn"]?["id"]?.GetValue<string>();
        return (expectedThreadId is null || string.Equals(threadId, expectedThreadId, StringComparison.Ordinal))
            && (expectedTurnId is null || string.Equals(turnId, expectedTurnId, StringComparison.Ordinal));
    }

    public async Task<string> GetStandardErrorAsync() => await _stderrTask.ConfigureAwait(false);

    private async Task<JsonlMessage> RequestAsync(AppServerRequest request, CancellationToken cancellationToken)
    {
        var pending = new TaskCompletionSource<JsonlMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(request.Id, pending))
        {
            throw new InvalidOperationException($"Duplicate App Server request id {request.Id}.");
        }

        try
        {
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _input.WriteLineAsync(request.ToJsonLine().AsMemory(), cancellationToken).ConfigureAwait(false);
                await _input.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }

            return await pending.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _pending.TryRemove(request.Id, out _);
            throw;
        }
    }

    private async Task SendRawJsonLineAsync(string jsonLine, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _input.WriteLineAsync(jsonLine).ConfigureAwait(false);
            await _input.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoopAsync()
    {
        Exception? failure = null;
        try
        {
            while (await _process.StandardOutput.ReadLineAsync(_lifetime.Token).ConfigureAwait(false) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var message = JsonlMessage.Parse(line);
                if (message.IsResponse && message.Id is long id && _pending.TryRemove(id, out var pending))
                {
                    pending.TrySetResult(message);
                }
                else if (message.IsServerRequest && message.Id is long requestId && message.Method is string requestMethod)
                {
                    var isKnownApproval = AppServerProtocol.IsInteractiveApprovalRequest(message);
                    var approvalPayloadLength = message.Params?.ToJsonString().Length ?? 0;
                    var payloadCanBeReviewed = approvalPayloadLength <= AppServerProtocol.MaximumApprovalPayloadCharacters;
                    var identityCanBeBound = message.Params is JsonObject approvalParams
                        && AppServerProtocol.HasValidApprovalIdentity(approvalParams);
                    var fileChangeReviewState = requestMethod == "item/fileChange/requestApproval"
                        ? GetFileChangeReviewStateForApproval(message)
                        : null;
                    var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                    var pendingApproval = new PendingApproval(message, timeoutCancellation, fileChangeReviewState);
                    if (isKnownApproval
                        && payloadCanBeReviewed
                        && identityCanBeBound
                        && _pendingApprovals.TryAdd(requestId, pendingApproval))
                    {
                        if (fileChangeReviewState is not null)
                        {
                            lock (fileChangeReviewState.Gate)
                            {
                                fileChangeReviewState.Pending = pendingApproval;
                            }
                        }

                        _ = ExpirePendingApprovalAsync(requestId, pendingApproval);
                        var notificationParams = new JsonObject
                        {
                            ["requestId"] = requestId,
                            ["approvalInstanceId"] = pendingApproval.ApprovalInstanceId,
                            ["method"] = requestMethod,
                            ["params"] = message.Params?.DeepClone() ?? new JsonObject()
                        };
                        if (fileChangeReviewState is not null)
                        {
                            lock (fileChangeReviewState.Gate)
                            {
                                notificationParams["reviewInvalidated"] = fileChangeReviewState.Invalidated;
                                if (!fileChangeReviewState.Invalidated && fileChangeReviewState.Review is not null)
                                {
                                    notificationParams["reviewPreview"] = fileChangeReviewState.Review.ToJsonObject();
                                }
                            }
                        }

                        await PublishNotificationAsync(
                            new AppServerNotification("neobabylon/approvalRequested", notificationParams)).ConfigureAwait(false);
                    }
                    else
                    {
                        timeoutCancellation.Dispose();
                        var reason = !isKnownApproval
                            ? "unsupportedAuthorityRequest"
                            : !payloadCanBeReviewed
                                ? "approvalPayloadTooLarge"
                                : !identityCanBeBound
                                    ? "approvalIdentityInvalid"
                                    : "duplicateRequestId";
                        var response = AppServerProtocol.BuildFailClosedServerRequestResponse(message);
                        await SendRawJsonLineAsync(response, _lifetime.Token).ConfigureAwait(false);
                        await PublishNotificationAsync(
                            new AppServerNotification(
                                "neobabylon/serverRequestDenied",
                                new JsonObject
                                {
                                    ["id"] = requestId,
                                    ["method"] = requestMethod,
                                    ["response"] = JsonNode.Parse(response),
                                    ["reason"] = reason
                                })).ConfigureAwait(false);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(message.Method))
                {
                    var parameters = message.Params ?? new JsonObject();
                    var reviewInvalidation = UpdateFileChangeReview(message.Method, parameters);
                    if (reviewInvalidation is not null)
                    {
                        await PublishNotificationAsync(reviewInvalidation).ConfigureAwait(false);
                    }

                    if (message.Method == "serverRequest/resolved"
                        && TryGetLong(parameters["requestId"], out var resolvedRequestId)
                        && _pendingApprovals.TryRemove(resolvedRequestId, out var resolvedApproval))
                    {
                        lock (resolvedApproval.Gate)
                        {
                            resolvedApproval.ResponseState = ApprovalResponseState.Resolved;
                            if (resolvedApproval.FileChangeReviewState is { } resolvedReview
                                && ReferenceEquals(resolvedReview.Pending, resolvedApproval))
                            {
                                resolvedReview.Pending = null;
                            }
                        }

                        CancelTimeout(resolvedApproval.TimeoutCancellation);
                        RemoveFileChangeReview(resolvedApproval.Request);
                    }

                    if (message.Method == "turn/completed")
                    {
                        var threadId = parameters["threadId"]?.GetValue<string>();
                        var turnId = parameters["turn"]?["id"]?.GetValue<string>();
                        if (threadId is not null && turnId is not null)
                        {
                            RetirePendingApprovals(threadId, turnId);
                            RemoveFileChangeReviews(threadId, turnId);
                        }
                    }

                    await PublishNotificationAsync(new AppServerNotification(message.Method, parameters)).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            foreach (var pending in _pendingApprovals.Values)
            {
                lock (pending.Gate)
                {
                    pending.ResponseState = ApprovalResponseState.Resolved;
                }
                CancelTimeout(pending.TimeoutCancellation);
            }
            _pendingApprovals.Clear();
            failure ??= new EndOfStreamException("Pinned App Server closed its JSONL output.");
            foreach (var pair in _pending)
            {
                pair.Value.TrySetException(failure);
            }
            _notifications.Writer.TryComplete(failure);
        }
    }

    private async Task ExpirePendingApprovalAsync(long requestId, PendingApproval pending)
    {
        try
        {
            await Task.Delay(_approvalTimeout, pending.TimeoutCancellation.Token).ConfigureAwait(false);
            string response;
            lock (pending.Gate)
            {
                if (pending.ResponseState != ApprovalResponseState.AwaitingDecision)
                {
                    return;
                }

                pending.ResponseState = ApprovalResponseState.ResponseCommitted;
                pending.CommittedDecision = "failClosed";
                response = AppServerProtocol.BuildFailClosedServerRequestResponse(pending.Request);
            }

            await _notificationPublishLock.WaitAsync(_lifetime.Token).ConfigureAwait(false);
            try
            {
                try
                {
                    await SendRawJsonLineAsync(response, _lifetime.Token).ConfigureAwait(false);
                    await WriteNotificationCoreAsync(new AppServerNotification(
                        "neobabylon/approvalTimedOut",
                        new JsonObject
                        {
                            ["requestId"] = requestId,
                            ["method"] = pending.Request.Method,
                            ["threadId"] = pending.Request.Params?["threadId"]?.DeepClone(),
                            ["turnId"] = pending.Request.Params?["turnId"]?.DeepClone(),
                            ["response"] = JsonNode.Parse(response),
                            ["decision"] = "failClosed"
                        })).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lock (pending.Gate)
                    {
                        pending.ResponseState = ApprovalResponseState.OutcomeUnknown;
                    }

                    if (!_process.HasExited)
                    {
                        _process.Kill(entireProcessTree: true);
                    }

                    await WriteNotificationCoreAsync(new AppServerNotification(
                        "neobabylon/approvalTimeoutFailed",
                        new JsonObject
                        {
                            ["requestId"] = requestId,
                            ["method"] = pending.Request.Method,
                            ["threadId"] = pending.Request.Params?["threadId"]?.DeepClone(),
                            ["turnId"] = pending.Request.Params?["turnId"]?.DeepClone(),
                            ["message"] = ex.Message,
                            ["serverTerminated"] = true
                        })).ConfigureAwait(false);
                }
            }
            finally
            {
                _notificationPublishLock.Release();
            }
        }
        catch (OperationCanceledException) when (pending.TimeoutCancellation.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            pending.TimeoutCancellation.Dispose();
        }
    }

    private async Task PublishNotificationAsync(AppServerNotification notification)
    {
        try
        {
            await _notificationPublishLock.WaitAsync(_lifetime.Token).ConfigureAwait(false);
            try
            {
                var observers = NotificationObserved;
                if (observers is not null)
                {
                    foreach (Action<AppServerNotification> observer in observers.GetInvocationList())
                    {
                        try
                        {
                            observer(notification);
                        }
                        catch (Exception error)
                        {
                            Trace.TraceError($"An App Server notification observer failed: {error.GetType().Name}.");
                        }
                    }
                }

                await WriteNotificationCoreAsync(notification).ConfigureAwait(false);
            }
            finally
            {
                _notificationPublishLock.Release();
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
    }

    private async Task WriteNotificationCoreAsync(AppServerNotification notification)
    {
        try
        {
            await _notifications.Writer.WriteAsync(notification, _lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (ChannelClosedException)
        {
        }
    }

    private async Task<AppServerNotification> DenyPendingApprovalAsync(AppServerNotification notification, string reason)
    {
        var parameters = notification.Params;
        var requestIdValue = parameters["requestId"];
        var requestMethod = parameters["method"]?.GetValue<string>() ?? "unknown";
        if (!TryGetLong(requestIdValue, out var requestId)
            || !_pendingApprovals.TryRemove(requestId, out var pending))
        {
            return new AppServerNotification(
                "neobabylon/serverRequestDenied",
                new JsonObject
                {
                    ["id"] = requestIdValue?.DeepClone(),
                    ["method"] = requestMethod,
                    ["reason"] = reason
                });
        }

        string response;
        lock (pending.Gate)
        {
            if (pending.ResponseState != ApprovalResponseState.AwaitingDecision)
            {
                return new AppServerNotification(
                    "neobabylon/serverRequestDenied",
                    new JsonObject
                    {
                        ["id"] = requestId,
                        ["method"] = requestMethod,
                        ["reason"] = "approvalResponseAlreadyCommitted"
                    });
            }

            pending.ResponseState = ApprovalResponseState.ResponseCommitted;
            pending.CommittedDecision = "failClosed";
            response = AppServerProtocol.BuildFailClosedServerRequestResponse(pending.Request);
        }

        CancelTimeout(pending.TimeoutCancellation);
        await SendRawJsonLineAsync(response, _lifetime.Token).ConfigureAwait(false);
        return new AppServerNotification(
            "neobabylon/serverRequestDenied",
            new JsonObject
            {
                ["id"] = requestId,
                ["method"] = requestMethod,
                ["response"] = JsonNode.Parse(response),
                ["reason"] = reason
            });
    }

    private static bool ApprovalMatchesActiveTurn(AppServerNotification notification, string threadId, string turnId)
    {
        var approvalParameters = notification.Params["params"] as JsonObject;
        return approvalParameters is not null
            && string.Equals(approvalParameters["threadId"]?.GetValue<string>(), threadId, StringComparison.Ordinal)
            && string.Equals(approvalParameters["turnId"]?.GetValue<string>(), turnId, StringComparison.Ordinal)
            && AppServerProtocol.HasValidApprovalIdentity(approvalParameters);
    }

    private FileChangeReviewState? GetFileChangeReviewStateForApproval(JsonlMessage request)
    {
        if (request.Method != "item/fileChange/requestApproval"
            || request.Params is not JsonObject parameters
            || !FileChangeApprovalReviewProjector.TryGetRequestIdentity(parameters, out var identity))
        {
            return null;
        }

        var key = new FileChangeReviewKey(identity.ThreadId, identity.TurnId, identity.ItemId);
        return _fileChangeReviews.GetOrAdd(key, _ => new FileChangeReviewState
        {
            ItemStartedObserved = true,
            Invalidated = true
        });
    }

    private AppServerNotification? UpdateFileChangeReview(string method, JsonObject parameters)
    {
        if (method == "item/started"
            && FileChangeApprovalReviewProjector.TryGetIdentity(parameters, out var itemIdentity))
        {
            var key = new FileChangeReviewKey(itemIdentity.ThreadId, itemIdentity.TurnId, itemIdentity.ItemId);
            var state = _fileChangeReviews.GetOrAdd(key, _ => new FileChangeReviewState());
            var candidate = FileChangeApprovalReviewProjector.ProjectItemStarted(parameters);
            lock (state.Gate)
            {
                if (!state.ItemStartedObserved)
                {
                    state.ItemStartedObserved = true;
                    state.Review = candidate;
                    state.Invalidated = candidate is null;
                    return null;
                }

                if (candidate is null
                    || state.Review is null
                    || !string.Equals(candidate.Fingerprint, state.Review.Fingerprint, StringComparison.Ordinal))
                {
                    return InvalidateFileChangeReview(state, "itemStartedSnapshotChanged");
                }

                return null;
            }
        }

        if (method == "item/fileChange/patchUpdated"
            && FileChangeApprovalReviewProjector.TryGetPatchUpdatedIdentity(parameters, out var patchIdentity))
        {
            var key = new FileChangeReviewKey(patchIdentity.ThreadId, patchIdentity.TurnId, patchIdentity.ItemId);
            if (!_fileChangeReviews.TryGetValue(key, out var state))
            {
                return null;
            }

            lock (state.Gate)
            {
                // patchUpdated is only a cumulative model-argument snapshot; the frozen item/started review remains the approval authority.
                if (!state.ItemStartedObserved)
                {
                    return null;
                }

                var candidate = FileChangeApprovalReviewProjector.ProjectPatchUpdated(parameters);
                if (candidate is null
                    || state.Review is null
                    || !string.Equals(candidate.Fingerprint, state.Review.Fingerprint, StringComparison.Ordinal))
                {
                    return InvalidateFileChangeReview(state, "patchUpdatedSnapshotChanged");
                }

                return null;
            }
        }

        return null;
    }

    private static AppServerNotification? InvalidateFileChangeReview(FileChangeReviewState state, string reason)
    {
        if (state.Invalidated)
        {
            return null;
        }

        state.Invalidated = true;
        var pending = state.Pending;
        if (pending is null || pending.ResponseState != ApprovalResponseState.AwaitingDecision)
        {
            return null;
        }

        var identity = state.Review?.Identity;
        return new AppServerNotification(
            "neobabylon/approvalReviewInvalidated",
            new JsonObject
            {
                ["requestId"] = pending.Request.Id,
                ["approvalInstanceId"] = pending.ApprovalInstanceId,
                ["threadId"] = identity?.ThreadId,
                ["turnId"] = identity?.TurnId,
                ["itemId"] = identity?.ItemId,
                ["previewFingerprint"] = state.Review?.Fingerprint,
                ["reason"] = reason
            });
    }

    private void RemoveFileChangeReview(JsonlMessage request)
    {
        if (request.Method == "item/fileChange/requestApproval"
            && request.Params is JsonObject parameters
            && FileChangeApprovalReviewProjector.TryGetRequestIdentity(parameters, out var identity))
        {
            var key = new FileChangeReviewKey(identity.ThreadId, identity.TurnId, identity.ItemId);
            if (_fileChangeReviews.TryRemove(key, out var state))
            {
                lock (state.Gate)
                {
                    state.Pending = null;
                }
            }
        }
    }

    private void RemoveFileChangeReviews(string threadId, string turnId)
    {
        foreach (var key in _fileChangeReviews.Keys)
        {
            if (string.Equals(key.ThreadId, threadId, StringComparison.Ordinal)
                && string.Equals(key.TurnId, turnId, StringComparison.Ordinal))
            {
                _fileChangeReviews.TryRemove(key, out _);
            }
        }
    }

    private void RetirePendingApprovals(string threadId, string turnId)
    {
        foreach (var pair in _pendingApprovals)
        {
            var requestThreadId = pair.Value.Request.Params?["threadId"]?.GetValue<string>();
            var requestTurnId = pair.Value.Request.Params?["turnId"]?.GetValue<string>();
            if (string.Equals(requestThreadId, threadId, StringComparison.Ordinal)
                && string.Equals(requestTurnId, turnId, StringComparison.Ordinal)
                && _pendingApprovals.TryRemove(pair.Key, out var retired))
            {
                lock (retired.Gate)
                {
                    retired.ResponseState = ApprovalResponseState.Resolved;
                    if (retired.FileChangeReviewState is { } reviewState
                        && ReferenceEquals(reviewState.Pending, retired))
                    {
                        reviewState.Pending = null;
                    }
                }

                CancelTimeout(retired.TimeoutCancellation);
            }
        }
    }

    private static void CancelTimeout(CancellationTokenSource cancellation)
    {
        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static bool TryGetLong(JsonNode? node, out long value)
    {
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<long>(out value))
        {
            return true;
        }

        if (node is JsonValue stringValue
            && stringValue.TryGetValue<string>(out var text)
            && long.TryParse(text, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private long NextId() => Interlocked.Increment(ref _nextRequestId);

    private static void EnsureSuccess(JsonlMessage response, string method)
    {
        if (response.Error is not null)
        {
            throw new AppServerRpcException($"App Server {method} failed: {response.Error.ToJsonString()}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _lifetime.Cancel();
        try
        {
            _input.Close();
        }
        catch (ObjectDisposedException)
        {
        }

        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync().ConfigureAwait(false);
        }

        try
        {
            await _readLoop.ConfigureAwait(false);
        }
        catch
        {
        }

        _writeLock.Dispose();
        _lifetime.Dispose();
        _process.Dispose();
    }
}
