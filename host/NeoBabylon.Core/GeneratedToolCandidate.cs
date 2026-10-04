using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace NeoBabylon.Core;

// Candidate metadata describes proposed behavior. It is never an execution grant.
public sealed record GeneratedToolCandidateManifest(
    int SchemaVersion,
    string Product,
    string ToolId,
    string CandidatePath,
    string Revision,
    string Name,
    string Purpose,
    string ProposedBehavior,
    string MissingCapability,
    GeneratedToolOrigin Origin,
    string EntryPoint,
    GeneratedToolContract Contract,
    GeneratedToolAuthority Authority,
    IReadOnlyList<GeneratedToolDependency> Dependencies,
    IReadOnlyList<GeneratedToolEvidence> Evidence,
    IReadOnlyList<GeneratedToolFile> Files,
    string ReviewState);

public sealed record GeneratedToolOrigin(
    string TaskId,
    string SessionId,
    DateTimeOffset CreatedAtUtc,
    string Creator,
    string Provider,
    string Model,
    string CapabilityIdentity,
    string GapEvidenceId);

public sealed record GeneratedToolContract(string Invocation, string InputSchema, string OutputSchema);

public sealed record GeneratedToolAuthority(
    IReadOnlyList<string> RequiredPermissions,
    IReadOnlyList<string> FileScopes,
    IReadOnlyList<string> NetworkDestinations,
    IReadOnlyList<string> DataFlows,
    string DenialBehavior,
    string PermissionMode,
    string ApprovalPolicy,
    string EvidenceSha256);

public sealed record GeneratedToolDependency(string Name, string Version, string Source, string Sha256);
public sealed record GeneratedToolEvidence(string Kind, string EvidenceId, string Outcome, string Description, string Path, string Sha256);
public sealed record GeneratedToolFile(string Path, string Sha256);

public sealed record GeneratedToolCandidate(
    GeneratedToolCandidateManifest Manifest,
    string CandidatePath,
    string ContentIdentity,
    string State);

public static class GeneratedToolCandidateValidator
{
    public const int SchemaVersion = 1;
    public const string ProductIdentity = "NeoBabylon";
    public const string Unapproved = "unapproved";
    public const string UnverifiedDeclaration = "unverified-declaration";

    private static readonly string[] RequiredDeclarationKinds =
        ["existing-tool-search", "composition-attempt", "current-task-authority", "capability-gap"];

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9-]{0,79}$", RegexOptions.CultureInvariant);
    private static readonly Regex HashPattern = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    private static readonly Regex CredentialPattern = new(
        @"-----BEGIN (?:[A-Z ]*PRIVATE KEY)-----|\b(?:Bearer\s+[A-Za-z0-9._~+/-]{8,}|Basic\s+[A-Za-z0-9+/=]{8,}|(?:password|passwd|api[_-]?key|access[_-]?token|refresh[_-]?token|client[_-]?secret|secret|authorization)\s*[:=]\s*[^\s,;]{4,}|(?:sk-[A-Za-z0-9_-]{12,}|gh[psu]_[A-Za-z0-9]{12,}|github_pat_[A-Za-z0-9_]{12,}))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex CredentialAssignmentPattern = new(
        @"\b(?:password|passwd|api[_-]?key|access[_-]?token|refresh[_-]?token|client[_-]?secret|secret|authorization|credential|private[_-]?key)\b\s*['""`]?\s*[:=]\s*(?:""[^""\r\n]+""|'[^'\r\n]+'|`[^`\r\n]+`|[^\s,;}\]]{4,})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex SplitBearerAssignmentPattern = new(
        @"(?:\[\s*['""`]authorization['""`]\s*\]|(?<![\w])['""`]?authorization['""`]?)\s*[:=]\s*['""`]Bearer\s*['""`]\s*\+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static void Validate(GeneratedToolCandidateManifest manifest, string applicationRoot)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var root = GeneratedToolCandidatePath.Root(applicationRoot);
        ValidateAtPath(manifest, GeneratedToolCandidatePath.Candidate(root, manifest.ToolId));
    }

    internal static void ValidateAtPath(GeneratedToolCandidateManifest manifest, string candidatePath)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.SchemaVersion != SchemaVersion
            || !string.Equals(manifest.Product, ProductIdentity, StringComparison.Ordinal)
            || !string.Equals(manifest.ReviewState, Unapproved, StringComparison.Ordinal)
            || !IdPattern.IsMatch(manifest.ToolId ?? ""))
        {
            throw new InvalidDataException("Generated tool candidate has an unsupported schema or invalid tool ID.");
        }

        if (string.IsNullOrWhiteSpace(manifest.CandidatePath)
            || !Path.IsPathFullyQualified(manifest.CandidatePath)
            || manifest.CandidatePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part is "." or "..")
            || !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(manifest.CandidatePath)),
                candidatePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Generated tool manifest path must identify its own candidate directory.");
        }

        Require(manifest.Revision, "revision");
        Require(manifest.Name, "name");
        Require(manifest.Purpose, "purpose");
        Require(manifest.ProposedBehavior, "proposed behavior");
        Require(manifest.MissingCapability, "missing capability");
        if (manifest.Origin is null || manifest.Origin.CreatedAtUtc == default
            || manifest.Origin.CreatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException("Generated tool candidate is missing its origin.");
        }

        Require(manifest.Origin.TaskId, "origin task");
        Require(manifest.Origin.SessionId, "origin session");
        Require(manifest.Origin.Creator, "creator identity");
        Require(manifest.Origin.Provider, "origin provider");
        Require(manifest.Origin.Model, "origin model");
        Require(manifest.Origin.GapEvidenceId, "capability gap evidence");
        const string capabilityPrefix = "cap-v1:sha256:";
        if (manifest.Origin.CapabilityIdentity is null
            || !manifest.Origin.CapabilityIdentity.StartsWith(capabilityPrefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Generated tool candidate is missing a capability identity.");
        }
        RequireHash(manifest.Origin.CapabilityIdentity[capabilityPrefix.Length..]);
        if (manifest.Contract is null)
        {
            throw new InvalidDataException("Generated tool candidate is missing its invocation contract.");
        }

        Require(manifest.Contract.Invocation, "invocation contract");
        Require(manifest.Contract.InputSchema, "input contract");
        Require(manifest.Contract.OutputSchema, "output contract");
        if (manifest.Authority is null
            || manifest.Authority.RequiredPermissions is null || manifest.Authority.RequiredPermissions.Count is 0 or > 32
            || manifest.Authority.FileScopes is null || manifest.Authority.FileScopes.Count > 32
            || manifest.Authority.NetworkDestinations is null || manifest.Authority.NetworkDestinations.Count > 32
            || manifest.Authority.DataFlows is null || manifest.Authority.DataFlows.Count is 0 or > 32)
        {
            throw new InvalidDataException("Generated tool candidate is missing explicit permissions or data flow.");
        }

        Require(manifest.Authority.DenialBehavior, "permission denial behavior");
        if (manifest.Authority.PermissionMode is not ("workspace-write" or "danger-full-access" or "read-only")
            || manifest.Authority.ApprovalPolicy is not ("never" or "on-request"))
        {
            throw new InvalidDataException("Generated tool candidate has invalid selected-task authority.");
        }
        RequireHash(manifest.Authority.EvidenceSha256);
        foreach (var value in manifest.Authority.RequiredPermissions) Require(value, "permission");
        foreach (var value in manifest.Authority.FileScopes) Require(value, "file scope");
        foreach (var value in manifest.Authority.NetworkDestinations) Require(value, "network destination");
        foreach (var value in manifest.Authority.DataFlows) Require(value, "data flow");

        if (manifest.Dependencies is null || manifest.Dependencies.Count > 32
            || manifest.Evidence is null || manifest.Evidence.Count is 0 or > 32
            || manifest.Files is null || manifest.Files.Count is 0 or > 256)
        {
            throw new InvalidDataException("Generated tool candidate needs an explicit bounded file, dependency, and evidence inventory.");
        }

        foreach (var dependency in manifest.Dependencies)
        {
            if (dependency is null) throw new InvalidDataException("Generated tool candidate has a null dependency.");
            Require(dependency.Name, "dependency name");
            Require(dependency.Version, "dependency version");
            Require(dependency.Source, "dependency source");
            RequireHash(dependency.Sha256);
        }

        var evidenceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var evidence in manifest.Evidence)
        {
            if (evidence is null) throw new InvalidDataException("Generated tool candidate has null evidence.");
            Require(evidence.Kind, "evidence kind");
            Require(evidence.EvidenceId, "evidence identity");
            Require(evidence.Outcome, "evidence outcome");
            Require(evidence.Description, "evidence description");
            if (!evidenceIds.Add(evidence.EvidenceId)
                || !string.Equals(evidence.Outcome, UnverifiedDeclaration, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Generated tool evidence must have a unique identity and remain an unverified declaration.");
            }
            if (!string.IsNullOrEmpty(evidence.Path))
            {
                GeneratedToolCandidatePath.ResolveFile(candidatePath, evidence.Path);
                RequireHash(evidence.Sha256);
            }
            else if (!string.IsNullOrEmpty(evidence.Sha256))
            {
                throw new InvalidDataException("Generated tool evidence hash requires a file path.");
            }
        }

        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            if (file is null) throw new InvalidDataException("Generated tool candidate has a null file.");
            GeneratedToolCandidatePath.ResolveFile(candidatePath, file.Path);
            if (string.Equals(file.Path, "tool.json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(file.Path, "candidate-state.json", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Generated tool inventory cannot include its own metadata files.");
            }
            RequireHash(file.Sha256);
            if (!files.Add(file.Path.Replace('/', '\\')))
            {
                throw new InvalidDataException("Generated tool candidate lists the same file twice.");
            }
        }
        if (files.Any(path => files.Any(other => other.StartsWith(path + "\\", StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidDataException("Generated tool candidate file paths cannot also name directories.");
        }

        GeneratedToolCandidatePath.ResolveFile(candidatePath, manifest.EntryPoint);
        if (!files.Contains(manifest.EntryPoint.Replace('/', '\\')))
        {
            throw new InvalidDataException("Generated tool entry point is not in the file inventory.");
        }

        foreach (var evidence in manifest.Evidence.Where(item => !string.IsNullOrEmpty(item.Path)))
        {
            var file = manifest.Files.FirstOrDefault(item => string.Equals(
                item.Path.Replace('/', '\\'), evidence.Path.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase));
            if (file is null || !string.Equals(file.Sha256, evidence.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Generated tool evidence path/hash is not in the file inventory.");
            }
        }

        // All four records are creator-supplied declarations, not host attestations or execution grants.
        if (RequiredDeclarationKinds.Any(kind => !manifest.Evidence.Any(item =>
                string.Equals(item.Kind, kind, StringComparison.Ordinal)
                && !string.IsNullOrEmpty(item.Path)))
            || !manifest.Evidence.Any(item =>
                string.Equals(item.Kind, "capability-gap", StringComparison.Ordinal)
                && string.Equals(item.EvidenceId, manifest.Origin.GapEvidenceId, StringComparison.Ordinal)
                && !string.IsNullOrEmpty(item.Path))
            || !manifest.Evidence.Any(item =>
                string.Equals(item.Kind, "current-task-authority", StringComparison.Ordinal)
                && !string.IsNullOrEmpty(item.Path)
                && string.Equals(item.Sha256, manifest.Authority.EvidenceSha256, StringComparison.Ordinal)))
        {
            throw new InvalidDataException("Generated tool candidate lacks required hashed declarations or exact gap/authority bindings.");
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        if (bytes.Length > 64 * 1024)
        {
            throw new InvalidDataException("Generated tool manifest exceeds the 64 KiB limit.");
        }

        using var json = JsonDocument.Parse(bytes);
        RejectCredentialText(json.RootElement);
    }

    internal static void RequireHash(string? hash)
    {
        if (hash is null || !HashPattern.IsMatch(hash))
        {
            throw new InvalidDataException("Generated tool candidate requires lowercase SHA-256 hashes.");
        }
    }

    internal static void RejectDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Generated tool JSON contains a duplicate property.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray()) RejectDuplicateProperties(item);
        }
    }

    internal static void RejectCredentialText(string text)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(text));
        RejectCredentialText(json.RootElement);
    }

    internal static void RejectCredentialText(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                if (Regex.IsMatch(property.Name, @"^(?:password|passwd|apiKey|api_key|accessToken|refreshToken|clientSecret|credential|privateKey)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                {
                    throw new InvalidDataException("Generated tool manifest may not contain credential fields.");
                }

                RejectCredentialText(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray()) RejectCredentialText(item);
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString() ?? "";
            if (CredentialPattern.IsMatch(text) || CredentialAssignmentPattern.IsMatch(text)
                || SplitBearerAssignmentPattern.IsMatch(text)
                || (Uri.TryCreate(text, UriKind.Absolute, out var uri)
                    && (!string.IsNullOrEmpty(uri.UserInfo)
                        || Regex.IsMatch(uri.Query, @"[?&](?:api[_-]?key|access[_-]?token|password|client[_-]?secret)=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))))
            {
                throw new InvalidDataException("Generated tool manifest appears to contain credential material.");
            }
        }
    }

    private static void Require(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || value.Any(char.IsControl))
        {
            throw new InvalidDataException($"Generated tool candidate requires a valid {field}.");
        }
    }
}

internal static class GeneratedToolCandidatePath
{
    internal static string Root(string applicationRoot)
    {
        if (string.IsNullOrWhiteSpace(applicationRoot) || !Path.IsPathFullyQualified(applicationRoot)
            || applicationRoot.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "." or ".."))
        {
            throw new InvalidDataException("Generated tool store requires an explicit absolute application root.");
        }

        var app = Path.TrimEndingDirectorySeparator(Path.GetFullPath(applicationRoot));
        if (string.Equals(app, Path.GetPathRoot(app), StringComparison.OrdinalIgnoreCase)
            || !Directory.Exists(app))
        {
            throw new InvalidDataException("Generated tool store requires an existing application root.");
        }
        var ordinaryCodexRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex")));
        if (string.Equals(app, ordinaryCodexRoot, StringComparison.OrdinalIgnoreCase)
            || app.StartsWith(ordinaryCodexRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Generated tool store cannot use the ordinary Codex root.");
        }

        var root = Path.Combine(app, "Data", "GeneratedTools");
        RejectReparseComponents(root);
        if (!Directory.Exists(Path.Combine(app, "Data")))
        {
            throw new InvalidDataException("Generated tool store requires an existing application Data root.");
        }
        return root;
    }

    internal static string Candidate(string root, string toolId)
    {
        if (string.IsNullOrEmpty(toolId)
            || !Regex.IsMatch(toolId, "^[a-z0-9][a-z0-9-]{0,79}$", RegexOptions.CultureInvariant)
            || Regex.IsMatch(toolId, @"^(?:con|prn|aux|nul|com[1-9]|lpt[1-9])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            throw new InvalidDataException("Generated tool candidate ID is invalid.");
        }

        var path = Path.Combine(root, "Candidates", toolId);
        RejectReparseComponents(path);
        return path;
    }

    internal static string ResolveFile(string candidate, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Length > 1024
            || Path.IsPathRooted(relative) || relative.Contains(':')
            || relative.Any(char.IsControl))
        {
            throw new InvalidDataException("Generated tool file path must be relative to the candidate.");
        }

        var parts = relative.Split('/', '\\');
        if (parts.Length > 16 || parts.Any(part => part.Length is < 1 or > 128
            || !char.IsAsciiLetterOrDigit(part[0]) || part.EndsWith('.')
            || part.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.'))
            || Regex.IsMatch(part, @"^(?:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
        {
            throw new InvalidDataException("Generated tool file path contains an invalid component.");
        }

        var full = Path.GetFullPath(Path.Combine(parts.Prepend(candidate).ToArray()));
        if (!full.StartsWith(candidate + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Generated tool file path escapes the candidate root.");
        }

        RejectReparseComponents(full);
        return full;
    }

    internal static void RejectReparseComponents(string path)
    {
        var full = Path.GetFullPath(path);
        var current = Path.GetPathRoot(full) ?? throw new InvalidDataException("Generated tool path has no root.");
        RejectReparse(current);
        foreach (var part in full[current.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (part.Length == 0) continue;
            current = Path.Combine(current, part);
            RejectReparse(current);
        }
    }

    private static void RejectReparse(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Generated tool path traverses a reparse point.");
            }
        }
        catch (FileNotFoundException)
        {
            if (new FileInfo(path).LinkTarget is not null || new DirectoryInfo(path).LinkTarget is not null)
            {
                throw new InvalidDataException("Generated tool path traverses a broken reparse point.");
            }
        }
        catch (DirectoryNotFoundException) { }
    }
}
