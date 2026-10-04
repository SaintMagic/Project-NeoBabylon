using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NeoBabylon.Core;

public sealed record GeneratedToolNodeRuntimeIdentity(string ExecutablePath, string Version, string Sha256);

public interface IGeneratedToolNodeRuntimeResolver
{
    GeneratedToolNodeRuntimeIdentity? Resolve(string applicationRoot);
}

// The host supplies the package version and hash from its trusted app-private runtime manifest.
// This resolver selects one fixed path and never reads a candidate-controlled command or path.
public sealed class AppPrivateGeneratedToolNodeRuntimeResolver : IGeneratedToolNodeRuntimeResolver
{
    private readonly string _version;
    private readonly string _sha256;

    public AppPrivateGeneratedToolNodeRuntimeResolver(string version, string sha256)
    {
        if (!GeneratedToolMcpRoute.IsSupportedNodeVersion(version)
            || !GeneratedToolMcpRoute.IsLowercaseSha256(sha256))
        {
            throw new ArgumentException("Trusted app-private Node runtime pin is invalid.");
        }
        _version = version;
        _sha256 = sha256;
    }

    public GeneratedToolNodeRuntimeIdentity? Resolve(string applicationRoot)
    {
        if (string.IsNullOrWhiteSpace(applicationRoot) || !Path.IsPathFullyQualified(applicationRoot))
        {
            throw new InvalidDataException("Node runtime resolution requires an absolute application root.");
        }
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(applicationRoot));
        var executable = Path.Combine(root, "Runtimes", "Node", _version, "node.exe");
        GeneratedToolCandidatePath.RejectReparseComponents(executable);
        if (!File.Exists(executable)) return null;
        var actualHash = GeneratedToolMcpRoute.HashNodeExecutable(executable);
        if (!string.Equals(actualHash, _sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("App-private Node runtime hash differs from its trusted host pin.");
        }
        return new GeneratedToolNodeRuntimeIdentity(executable, _version, actualHash);
    }
}

public sealed record GeneratedToolMcpInvocationPlan(
    int SchemaVersion,
    string Invocation,
    string ToolId,
    string ServerName,
    string ToolName,
    string EntryPointRelativePath,
    string ReviewedBundlePath,
    string EntryPointPath,
    string ReviewSnapshotIdentity,
    string NodeExecutablePath,
    string NodeVersion,
    string NodeSha256,
    string InputSchemaJson,
    string InputSchemaSha256,
    string OutputSchemaJson,
    string OutputSchemaSha256,
    string DependencyIdentity,
    string AuthorityIdentity,
    string PlanIdentity);

public sealed record GeneratedToolMcpRouteAssessment(
    string QualificationState,
    string? FailureKind,
    IReadOnlyList<string> Blockers,
    GeneratedToolMcpInvocationPlan? InvocationPlan,
    GeneratedToolAuthority? RequestedAuthority,
    GeneratedToolMcpInventoryConfirmation InventoryConfirmation,
    string? HostQualificationIdentity)
{
    public string? InventoryFailureKind => InventoryConfirmation switch
    {
        GeneratedToolMcpInventoryConfirmation.UnexpectedTools or GeneratedToolMcpInventoryConfirmation.SchemaMismatch => "unsupported",
        GeneratedToolMcpInventoryConfirmation.Unknown => "qualificationPending",
        _ => null
    };

    public IReadOnlyList<string> InventoryBlockers => InventoryConfirmation switch
    {
        GeneratedToolMcpInventoryConfirmation.UnexpectedTools or GeneratedToolMcpInventoryConfirmation.SchemaMismatch
            => ["mcp-tool-inventory-widened-or-schema-mismatch"],
        GeneratedToolMcpInventoryConfirmation.Unknown => ["mcp-tool-schema-unobserved"],
        _ => []
    };
}

public sealed record GeneratedToolRouteQualificationAllowSetEntry(
    string NodeVersion,
    string NodeSha256,
    string AppServerBinarySha256,
    string ProviderId,
    string ModelIdentifier,
    string CapabilityIdentity,
    string RuntimeEvidenceSha256,
    string ModelEvidenceSha256,
    ToolExecutionPolicy ExecutionPolicy);

public sealed record GeneratedToolHostQualificationMatch(
    string QualificationIdentity,
    string NodeVersion,
    string NodeSha256,
    string AppServerBinarySha256,
    string ProviderId,
    string ModelIdentifier,
    string CapabilityIdentity,
    string RuntimeEvidenceSha256,
    string ModelEvidenceSha256,
    ToolExecutionPolicy ExecutionPolicy);

public static class GeneratedToolRouteQualificationGate
{
    // Route acceptance is source-controlled. The exact runtime/provider/model/authority
    // evidence allow-set is intentionally empty in this build and cannot be populated
    // from user data or environment.
    public const bool RouteAccepted = false;
    private static readonly IReadOnlyList<GeneratedToolRouteQualificationAllowSetEntry> CompiledAllowSet =
        Array.AsReadOnly(Array.Empty<GeneratedToolRouteQualificationAllowSetEntry>());

    public static IReadOnlyList<GeneratedToolRouteQualificationAllowSetEntry> QualifiedRuntimeModelAllowSet => CompiledAllowSet;

    public static IReadOnlyList<string> Blockers
    {
        get
        {
            var blockers = new List<string>();
            if (!RouteAccepted) blockers.Add("route-not-accepted");
            if (CompiledAllowSet.Count == 0) blockers.Add("runtime-model-evidence-allow-set-empty");
            return blockers;
        }
    }

    public static IReadOnlyList<string> BlockersFor(
        GeneratedToolNodeRuntimeIdentity? runtime, RuntimeIdentity? appServerRuntimeIdentity,
        ModelCapabilityRecord? capability,
        ToolExecutionPolicy executionPolicy)
    {
        var blockers = new List<string>();
        if (!RouteAccepted) blockers.Add("route-not-accepted");
        if (executionPolicy != ToolExecutionPolicy.Unrestricted)
        {
            blockers.Add("effective-authority-not-danger-full-access-never");
        }
        if (runtime is null) blockers.Add("app-private-node-runtime-unavailable");
        if (!GeneratedToolMcpRoute.IsTrustedAppServerIdentity(appServerRuntimeIdentity))
        {
            blockers.Add("app-server-runtime-identity-unavailable");
        }
        if (capability is null) blockers.Add("selected-provider-model-unavailable");
        if (runtime is not null && appServerRuntimeIdentity is not null && capability is not null
            && FindExactMatch(runtime, appServerRuntimeIdentity, capability, executionPolicy) is null)
        {
            blockers.Add("runtime-provider-model-evidence-not-qualified");
        }
        return blockers.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static GeneratedToolHostQualificationMatch? FindExactMatch(
        GeneratedToolNodeRuntimeIdentity? runtime, RuntimeIdentity? appServerRuntimeIdentity,
        ModelCapabilityRecord? capability,
        ToolExecutionPolicy executionPolicy)
    {
        if (!RouteAccepted || executionPolicy != ToolExecutionPolicy.Unrestricted
            || runtime is null || appServerRuntimeIdentity is null
            || !GeneratedToolMcpRoute.IsTrustedAppServerIdentity(appServerRuntimeIdentity)
            || capability is null
            || !GeneratedToolMcpRoute.IsSupportedNodeVersion(runtime.Version)
            || !GeneratedToolMcpRoute.IsLowercaseSha256(runtime.Sha256))
        {
            return null;
        }
        var capabilityIdentity = CapabilityRecordIdentity.Compute(capability);
        foreach (var entry in CompiledAllowSet)
        {
            const string capabilityPrefix = "cap-v1:sha256:";
            if (!GeneratedToolMcpRoute.IsSupportedNodeVersion(entry.NodeVersion)
                || !GeneratedToolMcpRoute.IsLowercaseSha256(entry.NodeSha256)
                || !GeneratedToolMcpRoute.IsLowercaseSha256(entry.AppServerBinarySha256)
                || !GeneratedToolMcpRoute.IsLowercaseSha256(entry.RuntimeEvidenceSha256)
                || !GeneratedToolMcpRoute.IsLowercaseSha256(entry.ModelEvidenceSha256)
                || entry.ExecutionPolicy != ToolExecutionPolicy.Unrestricted
                || !entry.CapabilityIdentity.StartsWith(capabilityPrefix, StringComparison.Ordinal)
                || !GeneratedToolMcpRoute.IsLowercaseSha256(entry.CapabilityIdentity[capabilityPrefix.Length..]))
            {
                continue;
            }
            if (entry.NodeVersion == runtime.Version && entry.NodeSha256 == runtime.Sha256
                && entry.AppServerBinarySha256 == appServerRuntimeIdentity.Sha256
                && entry.ProviderId == capability.ProviderId
                && entry.ModelIdentifier == capability.ModelIdentifier
                && entry.CapabilityIdentity == capabilityIdentity
                && entry.ExecutionPolicy == executionPolicy)
            {
                const string effectiveAuthority = "danger-full-access/never";
                var identityText = string.Join('\n', entry.NodeVersion, entry.NodeSha256,
                    entry.AppServerBinarySha256,
                    entry.ProviderId, entry.ModelIdentifier, entry.CapabilityIdentity,
                    entry.RuntimeEvidenceSha256, entry.ModelEvidenceSha256, effectiveAuthority);
                var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identityText))).ToLowerInvariant();
                return new GeneratedToolHostQualificationMatch(identity, entry.NodeVersion,
                    entry.NodeSha256, entry.AppServerBinarySha256,
                    entry.ProviderId, entry.ModelIdentifier,
                    entry.CapabilityIdentity, entry.RuntimeEvidenceSha256, entry.ModelEvidenceSha256,
                    entry.ExecutionPolicy);
            }
        }
        return null;
    }
}

public static class GeneratedToolMcpRoute
{
    public const string SupportedInvocation = "stdio-mcp-node-v1";
    public const int InvocationPlanSchemaVersion = 1;
    private static readonly Regex NodeVersionPattern = new(
        "^v[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.-]+)?$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex DependencyNamePattern = new(
        "^[a-z0-9][a-z0-9._-]{0,127}$", RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex DependencyVersionPattern = new(
        "^[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.-]+)?(?:\\+[0-9A-Za-z.-]+)?$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private const int MaximumPackageJsonBytes = 64 * 1024;
    private const long MaximumNodeRuntimeBytes = 512L * 1024 * 1024;

    public static GeneratedToolMcpRouteAssessment Assess(
        string applicationRoot,
        GeneratedToolCandidate candidate,
        GeneratedToolReviewRecord review,
        GeneratedToolNodeRuntimeIdentity? nodeRuntime,
        RuntimeIdentity? appServerRuntimeIdentity,
        ModelCapabilityRecord? selectedCapability,
        ToolExecutionPolicy executionPolicy,
        McpServerStatusEntry? observedMcpStatus)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(review);
        var manifest = candidate.Manifest;
        if (executionPolicy != ToolExecutionPolicy.Unrestricted)
        {
            return new GeneratedToolMcpRouteAssessment("unsupported", "unsupported",
                ["effective-authority-not-danger-full-access-never"], null, manifest.Authority,
                GeneratedToolMcpInventoryConfirmation.Unknown, null);
        }
        if (!string.Equals(review.Decision, "reviewed", StringComparison.Ordinal)
            || !string.Equals(review.CandidateContentIdentity, candidate.ContentIdentity, StringComparison.Ordinal)
            || review.ReviewSnapshotIdentity is null)
        {
            return Failure("stale", "stale", "review-or-snapshot-stale");
        }

        if (!string.Equals(manifest.Contract.Invocation, SupportedInvocation, StringComparison.Ordinal)
            || !string.Equals(Path.GetExtension(manifest.EntryPoint), ".mjs", StringComparison.Ordinal))
        {
            return Failure("unsupported", "unsupported", "unsupported-invocation-contract");
        }

        string inputSchema;
        string outputSchema;
        try
        {
            inputSchema = GeneratedToolJsonSchema.Canonicalize(manifest.Contract.InputSchema);
            outputSchema = GeneratedToolJsonSchema.Canonicalize(manifest.Contract.OutputSchema);
        }
        catch (InvalidDataException)
        {
            return Failure("unsupported", "unsupported", "unsupported-or-malformed-json-schema");
        }

        GeneratedToolReviewedBundleInfo bundle;
        try
        {
            bundle = GeneratedToolReviewStore.ReadReviewedBundle(applicationRoot, manifest.ToolId, review);
        }
        catch (InvalidDataException)
        {
            return Failure("stale", "stale", "reviewed-bundle-missing-or-changed");
        }
        if (!string.Equals(bundle.Manifest.EntryPoint, manifest.EntryPoint, StringComparison.Ordinal)
            || !string.Equals(bundle.Manifest.Contract.Invocation, manifest.Contract.Invocation, StringComparison.Ordinal)
            || !string.Equals(GeneratedToolJsonSchema.Canonicalize(bundle.Manifest.Contract.InputSchema), inputSchema, StringComparison.Ordinal)
            || !string.Equals(GeneratedToolJsonSchema.Canonicalize(bundle.Manifest.Contract.OutputSchema), outputSchema, StringComparison.Ordinal))
        {
            return Failure("stale", "stale", "reviewed-invocation-plan-changed");
        }

        string dependencyIdentity;
        try
        {
            dependencyIdentity = ValidateReviewedDependencies(bundle);
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException
                                          or InvalidOperationException or KeyNotFoundException)
        {
            return Failure("unsupported", "unsupported", "dependency-material-unsupported");
        }

        var serverName = ServerNameFor(manifest.ToolId);
        var inventory = GeneratedToolMcpStatus.EvaluateCallableInventory(
            observedMcpStatus, serverName, manifest.ToolId, inputSchema, outputSchema);
        if (nodeRuntime is null)
        {
            var blockers = GeneratedToolRouteQualificationGate.BlockersFor(
                nodeRuntime, appServerRuntimeIdentity, selectedCapability, executionPolicy);
            return new GeneratedToolMcpRouteAssessment("pending", "qualificationPending", blockers,
                null, manifest.Authority, inventory, null);
        }

        try
        {
            ValidateNodeRuntime(applicationRoot, nodeRuntime);
        }
        catch (InvalidDataException)
        {
            return Failure("stale", "stale", "app-private-node-runtime-identity-stale");
        }

        var inputHash = Digest(Encoding.UTF8.GetBytes(inputSchema));
        var outputHash = Digest(Encoding.UTF8.GetBytes(outputSchema));
        var authorityIdentity = Digest(JsonSerializer.SerializeToUtf8Bytes(
            manifest.Authority, GeneratedToolCandidateValidator.JsonOptions));
        var entryPoint = GeneratedToolCandidatePath.ResolveFile(bundle.BundlePath, manifest.EntryPoint);
        var planIdentity = ComputePlanIdentity(manifest.ToolId, serverName,
            review.ReviewSnapshotIdentity, entryPoint, nodeRuntime, inputHash, outputHash,
            dependencyIdentity, authorityIdentity);
        var plan = new GeneratedToolMcpInvocationPlan(
            InvocationPlanSchemaVersion, SupportedInvocation, manifest.ToolId, serverName,
            manifest.ToolId, manifest.EntryPoint, bundle.BundlePath, entryPoint,
            review.ReviewSnapshotIdentity, Path.GetFullPath(nodeRuntime.ExecutablePath),
            nodeRuntime.Version, nodeRuntime.Sha256, inputSchema, inputHash,
            outputSchema, outputHash, dependencyIdentity, authorityIdentity, planIdentity);

        var hostQualification = GeneratedToolRouteQualificationGate.FindExactMatch(
            nodeRuntime, appServerRuntimeIdentity, selectedCapability, executionPolicy);
        var gateBlockers = GeneratedToolRouteQualificationGate.BlockersFor(
            nodeRuntime, appServerRuntimeIdentity, selectedCapability, executionPolicy);
        return hostQualification is null
            ? new GeneratedToolMcpRouteAssessment("pending", "qualificationPending", gateBlockers,
                plan, manifest.Authority, inventory, null)
            : new GeneratedToolMcpRouteAssessment("qualified", null, [], plan, manifest.Authority,
                inventory, hostQualification.QualificationIdentity);
    }

    public static string ServerNameFor(string toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId) || toolId.Length > 80
            || toolId.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new InvalidDataException("Generated tool ID is invalid for a fixed MCP server name.");
        }
        return "neobabylon_generated_" + toolId;
    }

    public static void ValidateNodeRuntime(string applicationRoot, GeneratedToolNodeRuntimeIdentity runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (!IsSupportedNodeVersion(runtime.Version) || !IsLowercaseSha256(runtime.Sha256)
            || string.IsNullOrWhiteSpace(runtime.ExecutablePath)
            || !Path.IsPathFullyQualified(runtime.ExecutablePath)
            || runtime.ExecutablePath.Any(char.IsControl)
            || runtime.ExecutablePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part is "." or ".."))
        {
            throw new InvalidDataException("App-private Node runtime identity is malformed.");
        }
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(applicationRoot));
        var expected = Path.GetFullPath(Path.Combine(root, "Runtimes", "Node", runtime.Version, "node.exe"));
        var actual = Path.GetFullPath(runtime.ExecutablePath);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Node executable is not the fixed app-private runtime path.");
        }
        GeneratedToolCandidatePath.RejectReparseComponents(actual);
        if (!File.Exists(actual))
        {
            throw new InvalidDataException("App-private Node executable is missing.");
        }
        var digest = HashNodeExecutable(actual);
        if (!string.Equals(digest, runtime.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("App-private Node executable hash changed.");
        }
    }

    internal static bool IsSupportedNodeVersion(string? version) =>
        version is not null && NodeVersionPattern.IsMatch(version);

    internal static bool IsTrustedAppServerIdentity(RuntimeIdentity? identity) =>
        identity is not null
        && !string.IsNullOrWhiteSpace(identity.Version)
        && !string.IsNullOrWhiteSpace(identity.SourceRevision)
        && IsLowercaseSha256(identity.Sha256)
        && !string.IsNullOrWhiteSpace(identity.BinaryPath)
        && Path.IsPathFullyQualified(identity.BinaryPath)
        && !identity.BinaryPath.Any(char.IsControl);

    internal static bool IsLowercaseSha256(string? value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static string HashNodeExecutable(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaximumNodeRuntimeBytes)
        {
            throw new InvalidDataException("App-private Node runtime is empty or exceeds its hash bound.");
        }
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static GeneratedToolMcpRouteAssessment Failure(
        string state, string failureKind, string blocker) =>
        new(state, failureKind, [blocker], null, null, GeneratedToolMcpInventoryConfirmation.Unknown, null);

    private static string ValidateReviewedDependencies(GeneratedToolReviewedBundleInfo bundle)
    {
        var dependencies = bundle.Manifest.Dependencies;
        var dependencyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filesUnderNodeModules = bundle.Manifest.Files
            .Where(file => file.Path.StartsWith("node_modules/", StringComparison.OrdinalIgnoreCase)
                || file.Path.StartsWith("node_modules\\", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (dependencies.Count > 32)
        {
            throw new InvalidDataException("Generated tool dependency count exceeds its callable-route bound.");
        }
        foreach (var dependency in dependencies)
        {
            if (!DependencyNamePattern.IsMatch(dependency.Name)
                || !DependencyVersionPattern.IsMatch(dependency.Version)
                || !IsLowercaseSha256(dependency.Sha256)
                || !dependencyNames.Add(dependency.Name))
            {
                throw new InvalidDataException("Generated tool dependency declaration is unsupported.");
            }
            var prefix = "node_modules/" + dependency.Name + "/";
            var packageFiles = filesUnderNodeModules.Where(file =>
                    NormalizePath(file.Path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => NormalizePath(file.Path), StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (packageFiles.Length == 0)
            {
                throw new InvalidDataException("Generated tool dependency bytes are not present in the reviewed bundle.");
            }
            var digestInput = new StringBuilder();
            foreach (var file in packageFiles)
            {
                digestInput.Append(NormalizePath(file.Path)[prefix.Length..])
                    .Append('\0').Append(file.Sha256).Append('\n');
            }
            if (!string.Equals(Digest(Encoding.UTF8.GetBytes(digestInput.ToString())), dependency.Sha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("Generated tool dependency hash does not match its reviewed files.");
            }
            var packageJson = packageFiles.FirstOrDefault(file =>
                string.Equals(NormalizePath(file.Path)[prefix.Length..], "package.json", StringComparison.OrdinalIgnoreCase));
            if (packageJson is null)
            {
                throw new InvalidDataException("Generated tool dependency package.json is missing from the reviewed bundle.");
            }
            var packagePath = GeneratedToolCandidatePath.ResolveFile(bundle.BundlePath, packageJson.Path);
            var packageBytes = ReadBounded(packagePath, MaximumPackageJsonBytes);
            using var packageDocument = JsonDocument.Parse(packageBytes);
            GeneratedToolCandidateValidator.RejectDuplicateProperties(packageDocument.RootElement);
            if (packageDocument.RootElement.ValueKind != JsonValueKind.Object
                || !string.Equals(packageDocument.RootElement.GetProperty("name").GetString(), dependency.Name, StringComparison.Ordinal)
                || !string.Equals(packageDocument.RootElement.GetProperty("version").GetString(), dependency.Version, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Generated tool dependency package identity differs from its declaration.");
            }
            foreach (var propertyName in new[] { "dependencies", "optionalDependencies", "peerDependencies" })
            {
                if (!packageDocument.RootElement.TryGetProperty(propertyName, out var required)) continue;
                if (required.ValueKind != JsonValueKind.Object
                    || required.EnumerateObject().Any(item => !dependencyNames.Contains(item.Name)))
                {
                    throw new InvalidDataException("Generated tool dependency material references an undeclared package.");
                }
            }
        }

        if (filesUnderNodeModules.Any(file => !dependencies.Any(dependency =>
                NormalizePath(file.Path).StartsWith("node_modules/" + dependency.Name + "/", StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidDataException("Reviewed bundle contains undeclared dependency files.");
        }
        return Digest(Encoding.UTF8.GetBytes(string.Join('\n', dependencies
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => $"{item.Name}\0{item.Version}\0{item.Sha256}"))));
    }

    private static string ComputePlanIdentity(
        string toolId, string serverName, string snapshotIdentity, string entryPoint,
        GeneratedToolNodeRuntimeIdentity runtime, string inputHash, string outputHash,
        string dependencyIdentity, string authorityIdentity)
    {
        var value = string.Join('\n', SupportedInvocation, toolId, serverName, toolId,
            snapshotIdentity, Path.GetFullPath(entryPoint), Path.GetFullPath(runtime.ExecutablePath),
            runtime.Version, runtime.Sha256, inputHash, outputHash, dependencyIdentity, authorityIdentity);
        return Digest(Encoding.UTF8.GetBytes(value));
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static string Digest(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static byte[] ReadBounded(string path, int maximumBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 || stream.Length > maximumBytes)
        {
            throw new InvalidDataException("Reviewed dependency file is empty or exceeds its bound.");
        }
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}

public static class GeneratedToolJsonSchema
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.Ordinal)
    {
        "object", "array", "string", "number", "integer", "boolean", "null"
    };
    private static readonly HashSet<string> SupportedKeywords = new(StringComparer.Ordinal)
    {
        "type", "properties", "required", "additionalProperties", "items", "enum",
        "title", "description", "minLength", "maxLength", "minimum", "maximum"
    };

    public static string Canonicalize(string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson)
            || Encoding.UTF8.GetByteCount(schemaJson) > 64 * 1024)
        {
            throw new InvalidDataException("Generated tool JSON Schema is empty or exceeds its bound.");
        }
        try
        {
            using var document = JsonDocument.Parse(schemaJson, new JsonDocumentOptions { MaxDepth = 24 });
            GeneratedToolCandidateValidator.RejectDuplicateProperties(document.RootElement);
            ValidateNode(document.RootElement, 0, isRoot: true);
            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output)) WriteCanonical(document.RootElement, writer);
            return Encoding.UTF8.GetString(output.ToArray());
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Generated tool JSON Schema is malformed.", exception);
        }
    }

    private static void ValidateNode(JsonElement schema, int depth, bool isRoot)
    {
        if (depth > 16 || schema.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Generated tool JSON Schema has an unsupported shape or depth.");
        }
        foreach (var property in schema.EnumerateObject())
        {
            if (!SupportedKeywords.Contains(property.Name))
            {
                throw new InvalidDataException("Generated tool JSON Schema uses an unsupported keyword.");
            }
        }
        if (!schema.TryGetProperty("type", out var typeElement)
            || typeElement.ValueKind != JsonValueKind.String
            || !SupportedTypes.Contains(typeElement.GetString() ?? ""))
        {
            throw new InvalidDataException("Generated tool JSON Schema requires one supported type.");
        }
        var type = typeElement.GetString()!;
        if (isRoot && type != "object")
        {
            throw new InvalidDataException("Generated tool callable input/output schemas must be objects.");
        }

        if (schema.TryGetProperty("properties", out var properties))
        {
            if (type != "object" || properties.ValueKind != JsonValueKind.Object
                || properties.EnumerateObject().Count() > 64)
            {
                throw new InvalidDataException("Generated tool JSON Schema has invalid object properties.");
            }
            foreach (var property in properties.EnumerateObject()) ValidateNode(property.Value, depth + 1, false);
        }
        if (schema.TryGetProperty("required", out var required))
        {
            if (type != "object" || required.ValueKind != JsonValueKind.Array
                || !schema.TryGetProperty("properties", out properties))
            {
                throw new InvalidDataException("Generated tool JSON Schema has an invalid required list.");
            }
            var propertyNames = properties.EnumerateObject().Select(property => property.Name)
                .ToHashSet(StringComparer.Ordinal);
            var requiredNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in required.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String || !requiredNames.Add(item.GetString()!)
                    || !propertyNames.Contains(item.GetString()!))
                {
                    throw new InvalidDataException("Generated tool JSON Schema has an invalid required property.");
                }
            }
        }
        if (schema.TryGetProperty("additionalProperties", out var additionalProperties)
            && (type != "object" || additionalProperties.ValueKind != JsonValueKind.False))
        {
            throw new InvalidDataException("Generated tool JSON Schema must explicitly reject additional properties when specified.");
        }
        if (type == "object" && (!schema.TryGetProperty("additionalProperties", out additionalProperties)
                                  || additionalProperties.ValueKind != JsonValueKind.False))
        {
            throw new InvalidDataException("Generated tool object schemas must set additionalProperties to false.");
        }
        if (type == "array")
        {
            if (!schema.TryGetProperty("items", out var items))
            {
                throw new InvalidDataException("Generated tool array schema requires an items schema.");
            }
            ValidateNode(items, depth + 1, false);
        }
        else if (schema.TryGetProperty("items", out _))
        {
            throw new InvalidDataException("Generated tool items keyword is valid only for arrays.");
        }
        if (schema.TryGetProperty("enum", out var enumValues)
            && (enumValues.ValueKind != JsonValueKind.Array || enumValues.GetArrayLength() is 0 or > 64))
        {
            throw new InvalidDataException("Generated tool JSON Schema enum is empty or exceeds its bound.");
        }
        foreach (var textKey in new[] { "title", "description" })
        {
            if (schema.TryGetProperty(textKey, out var text)
                && (text.ValueKind != JsonValueKind.String || text.GetString()!.Length > 1024))
            {
                throw new InvalidDataException("Generated tool JSON Schema text metadata is invalid.");
            }
        }
        ValidateIntegerBound(schema, "minLength");
        ValidateIntegerBound(schema, "maxLength");
        ValidateNumberBound(schema, "minimum");
        ValidateNumberBound(schema, "maximum");
    }

    private static void ValidateIntegerBound(JsonElement schema, string key)
    {
        if (schema.TryGetProperty(key, out var value)
            && (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number)
                || number < 0 || number > 1_000_000))
        {
            throw new InvalidDataException("Generated tool JSON Schema has an invalid length bound.");
        }
    }

    private static void ValidateNumberBound(JsonElement schema, string key)
    {
        if (schema.TryGetProperty(key, out var value)
            && (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out _)))
        {
            throw new InvalidDataException("Generated tool JSON Schema has an invalid numeric bound.");
        }
    }

    private static void WriteCanonical(JsonElement value, Utf8JsonWriter writer)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) WriteCanonical(item, writer);
                writer.WriteEndArray();
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }
}
