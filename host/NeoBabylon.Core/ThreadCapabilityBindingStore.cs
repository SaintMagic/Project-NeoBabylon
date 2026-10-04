using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoBabylon.Core;

public sealed record ThreadCapabilityBinding(
    string ThreadId,
    string Workspace,
    string CapabilityIdentity,
    string ProviderId,
    string ModelIdentifier,
    DateTimeOffset CreatedAtUtc);

public sealed record ThreadCapabilitySwitch(
    string CapabilityIdentity,
    string ProviderId,
    string ModelIdentifier,
    DateTimeOffset SwitchedAtUtc);

public sealed record ThreadCapabilitySelection(
    string Workspace,
    string CapabilityIdentity,
    string ProviderId,
    string ModelIdentifier);

public static class CapabilityRecordIdentity
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ModelCapabilityRecord Freeze(ModelCapabilityRecord capability)
    {
        ArgumentNullException.ThrowIfNull(capability);
        var serialized = Serialize(capability);
        return JsonSerializer.Deserialize<ModelCapabilityRecord>(serialized, JsonOptions)
            ?? throw new InvalidDataException("NeoBabylon could not freeze the selected capability record.");
    }

    public static string Serialize(ModelCapabilityRecord capability) =>
        JsonSerializer.Serialize(capability, JsonOptions);

    public static string Compute(ModelCapabilityRecord capability) =>
        ComputeSerialized(Serialize(capability));

    public static string ComputeSerialized(string serializedCapabilityRecord)
    {
        ArgumentNullException.ThrowIfNull(serializedCapabilityRecord);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(serializedCapabilityRecord));
        return $"cap-v1:sha256:{Convert.ToHexString(digest).ToLowerInvariant()}";
    }
}

public static class ThreadCapabilityBindingStore
{
    private const int CurrentSchemaVersion = 2;
    private const string ProductIdentity = "NeoBabylon";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static ThreadCapabilityBinding? Read(string dataRoot, string threadId)
    {
        var path = ResolvePath(dataRoot, threadId);
        if (!File.Exists(path))
        {
            return null;
        }

        var document = ReadDocument(path);
        if (document.Binding is null) return null;
        var binding = NormalizeAndValidate(document.Binding);
        if (!string.Equals(binding.ThreadId, threadId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("NeoBabylon task capability binding does not match its thread identity.");
        }

        return binding;
    }

    public static ThreadCapabilityBinding Bind(
        string dataRoot,
        string threadId,
        string workspace,
        ModelCapabilityRecord capability)
    {
        var binding = NormalizeAndValidate(new ThreadCapabilityBinding(
            threadId,
            workspace,
            CapabilityRecordIdentity.Compute(capability),
            capability.ProviderId,
            capability.ModelIdentifier,
            DateTimeOffset.UtcNow));
        var path = ResolvePath(dataRoot, threadId);
        var existing = Read(dataRoot, threadId);
        if (existing is not null)
        {
            if (SameIdentity(existing, binding))
            {
                return existing;
            }

            throw new InvalidDataException("NeoBabylon refused to replace a task's existing capability binding.");
        }

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidDataException("NeoBabylon task capability binding path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var document = new BindingDocument(CurrentSchemaVersion, ProductIdentity, binding, [], threadId, binding.Workspace);
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(document, JsonOptions), new UTF8Encoding(false));
            File.Move(temporaryPath, path);
            return binding;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static bool Matches(ThreadCapabilityBinding binding, ModelCapabilityRecord capability, string workspace)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(capability);
        return string.Equals(binding.CapabilityIdentity, CapabilityRecordIdentity.Compute(capability), StringComparison.Ordinal)
            && string.Equals(binding.ProviderId, capability.ProviderId, StringComparison.Ordinal)
            && string.Equals(binding.ModelIdentifier, capability.ModelIdentifier, StringComparison.Ordinal)
            && string.Equals(binding.Workspace, NormalizeWorkspace(workspace), StringComparison.OrdinalIgnoreCase);
    }

    public static void RecordExplicitSwitch(
        string dataRoot,
        string threadId,
        string workspace,
        ModelCapabilityRecord capability)
    {
        var path = ResolvePath(dataRoot, threadId);
        var document = File.Exists(path)
            ? ReadDocument(path)
            : new BindingDocument(CurrentSchemaVersion, ProductIdentity, null, [], threadId, NormalizeWorkspace(workspace));
        if (document.ThreadId is not null && !string.Equals(document.ThreadId, threadId, StringComparison.Ordinal))
            throw new InvalidDataException("NeoBabylon capability-switch history does not match the saved thread.");
        var original = document.Binding is null ? null : NormalizeAndValidate(document.Binding);
        var normalizedWorkspace = NormalizeWorkspace(workspace);
        var boundWorkspace = original?.Workspace ?? document.Workspace;
        if (string.IsNullOrWhiteSpace(boundWorkspace)
            || !string.Equals(NormalizeWorkspace(boundWorkspace), normalizedWorkspace, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A saved thread cannot be switched from a different workspace.");
        }

        var next = new ThreadCapabilitySwitch(
            CapabilityRecordIdentity.Compute(capability),
            capability.ProviderId,
            capability.ModelIdentifier,
            DateTimeOffset.UtcNow);
        var switches = (document.CapabilitySwitches ?? []).Select(NormalizeAndValidate).ToList();
        if ((switches.Count > 0 && SameCapability(switches[^1], next))
            || (switches.Count == 0 && original is not null && SameCapability(original, next)))
        {
            return;
        }

        switches.Add(next);
        WriteDocument(path, document with
        {
            SchemaVersion = CurrentSchemaVersion,
            Binding = original,
            CapabilitySwitches = switches,
            ThreadId = threadId,
            Workspace = normalizedWorkspace
        });
    }

    public static bool MatchesCurrent(
        string dataRoot,
        string threadId,
        ModelCapabilityRecord capability,
        string workspace)
    {
        var selected = ReadCurrentBinding(dataRoot, threadId);
        return selected is not null
            && string.Equals(selected.Workspace, NormalizeWorkspace(workspace), StringComparison.OrdinalIgnoreCase)
            && string.Equals(selected.CapabilityIdentity, CapabilityRecordIdentity.Compute(capability), StringComparison.Ordinal)
            && string.Equals(selected.ProviderId, capability.ProviderId, StringComparison.Ordinal)
            && string.Equals(selected.ModelIdentifier, capability.ModelIdentifier, StringComparison.Ordinal);
    }

    public static (string ProviderId, string ModelIdentifier)? ReadCurrentSelection(string dataRoot, string threadId)
    {
        var current = ReadCurrentBinding(dataRoot, threadId);
        return current is null ? null : (current.ProviderId, current.ModelIdentifier);
    }

    // A read-only view of the latest explicit selection, never a replacement
    // for the immutable original binding returned by Read.
    public static ThreadCapabilitySelection? ReadCurrentBinding(string dataRoot, string threadId)
    {
        var path = ResolvePath(dataRoot, threadId);
        if (!File.Exists(path)) return null;
        var document = ReadDocument(path);
        if (document.ThreadId is not null && !string.Equals(document.ThreadId, threadId, StringComparison.Ordinal))
            throw new InvalidDataException("NeoBabylon capability history does not match its saved thread.");
        var original = document.Binding is null ? null : NormalizeAndValidate(document.Binding);
        if (original is not null && !string.Equals(original.ThreadId, threadId, StringComparison.Ordinal))
            throw new InvalidDataException("NeoBabylon task capability binding does not match its thread identity.");
        var workspace = NormalizeWorkspace(original?.Workspace ?? document.Workspace!);
        var switches = (document.CapabilitySwitches ?? []).Select(NormalizeAndValidate).ToArray();
        if (switches.Length > 0)
        {
            var latest = switches[^1];
            return new(workspace, latest.CapabilityIdentity, latest.ProviderId, latest.ModelIdentifier);
        }
        return original is null
            ? null
            : new(workspace, original.CapabilityIdentity, original.ProviderId, original.ModelIdentifier);
    }

    public static bool HasExplicitSwitch(string dataRoot, string threadId)
    {
        var path = ResolvePath(dataRoot, threadId);
        if (!File.Exists(path)) return false;
        var document = ReadDocument(path);
        if (document.ThreadId is not null && !string.Equals(document.ThreadId, threadId, StringComparison.Ordinal))
            throw new InvalidDataException("NeoBabylon capability history does not match its saved thread.");
        return (document.CapabilitySwitches?.Count ?? 0) > 0;
    }

    private static BindingDocument ReadDocument(string path)
    {
        BindingDocument document;
        try
        {
            document = JsonSerializer.Deserialize<BindingDocument>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidDataException("NeoBabylon task capability binding is empty or invalid.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("NeoBabylon task capability binding is not valid JSON.", exception);
        }

        if (document.SchemaVersion is not (1 or CurrentSchemaVersion)
            || !string.Equals(document.Product, ProductIdentity, StringComparison.Ordinal)
            || (document.SchemaVersion == 1 && document.Binding is null)
            || (document.Binding is null
                && (string.IsNullOrWhiteSpace(document.ThreadId) || string.IsNullOrWhiteSpace(document.Workspace)))
            || (document.CapabilitySwitches is not null && document.SchemaVersion != CurrentSchemaVersion))
        {
            throw new InvalidDataException("NeoBabylon task capability binding schema or product identity is not accepted.");
        }

        return document;
    }

    private static void WriteDocument(string path, BindingDocument document)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidDataException("NeoBabylon task capability binding path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(document, JsonOptions), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporaryPath, path, null);
            else File.Move(temporaryPath, path);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static ThreadCapabilitySwitch NormalizeAndValidate(ThreadCapabilitySwitch item)
    {
        if (string.IsNullOrWhiteSpace(item.CapabilityIdentity)
            || !IsCapabilityIdentity(item.CapabilityIdentity)
            || string.IsNullOrWhiteSpace(item.ProviderId)
            || string.IsNullOrWhiteSpace(item.ModelIdentifier)
            || item.SwitchedAtUtc == default)
        {
            throw new InvalidDataException("NeoBabylon task capability switch history is invalid.");
        }
        return item with { SwitchedAtUtc = item.SwitchedAtUtc.ToUniversalTime() };
    }

    private static bool SameCapability(ThreadCapabilityBinding original, ThreadCapabilitySwitch next) =>
        string.Equals(original.CapabilityIdentity, next.CapabilityIdentity, StringComparison.Ordinal)
        && string.Equals(original.ProviderId, next.ProviderId, StringComparison.Ordinal)
        && string.Equals(original.ModelIdentifier, next.ModelIdentifier, StringComparison.Ordinal);

    private static bool SameCapability(ThreadCapabilitySwitch left, ThreadCapabilitySwitch right) =>
        string.Equals(left.CapabilityIdentity, right.CapabilityIdentity, StringComparison.Ordinal)
        && string.Equals(left.ProviderId, right.ProviderId, StringComparison.Ordinal)
        && string.Equals(left.ModelIdentifier, right.ModelIdentifier, StringComparison.Ordinal);

    private static string ResolvePath(string dataRoot, string threadId)
    {
        if (string.IsNullOrWhiteSpace(threadId) || threadId.Length > 512)
        {
            throw new InvalidDataException("NeoBabylon task capability binding requires a valid App Server thread identifier.");
        }

        var root = Path.GetFullPath(dataRoot);
        var fileId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(threadId))).ToLowerInvariant();
        return Path.Combine(root, "NeoBabylon", "TaskBindings", $"{fileId}.json");
    }

    private static ThreadCapabilityBinding NormalizeAndValidate(ThreadCapabilityBinding binding)
    {
        if (string.IsNullOrWhiteSpace(binding.ThreadId)
            || binding.ThreadId.Length > 512
            || string.IsNullOrWhiteSpace(binding.ProviderId)
            || string.IsNullOrWhiteSpace(binding.ModelIdentifier)
            || !IsCapabilityIdentity(binding.CapabilityIdentity)
            || binding.CreatedAtUtc == default)
        {
            throw new InvalidDataException("NeoBabylon task capability binding is missing a required identity field.");
        }

        return binding with
        {
            Workspace = NormalizeWorkspace(binding.Workspace),
            CreatedAtUtc = binding.CreatedAtUtc.ToUniversalTime()
        };
    }

        private static string NormalizeWorkspace(string workspace)
    {
        if (string.IsNullOrWhiteSpace(workspace))
        {
            throw new InvalidDataException("NeoBabylon task capability binding is missing its workspace identity.");
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspace));
    }

    private static bool IsCapabilityIdentity(string value)
    {
        const string prefix = "cap-v1:sha256:";
        if (value is null || value.Length != prefix.Length + 64 || !value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        return value.AsSpan(prefix.Length).ToArray().All(Uri.IsHexDigit);
    }

    private static bool SameIdentity(ThreadCapabilityBinding left, ThreadCapabilityBinding right) =>
        string.Equals(left.ThreadId, right.ThreadId, StringComparison.Ordinal)
        && string.Equals(left.Workspace, right.Workspace, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.CapabilityIdentity, right.CapabilityIdentity, StringComparison.Ordinal)
        && string.Equals(left.ProviderId, right.ProviderId, StringComparison.Ordinal)
        && string.Equals(left.ModelIdentifier, right.ModelIdentifier, StringComparison.Ordinal);

    private sealed record BindingDocument(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("product")] string Product,
        [property: JsonPropertyName("binding")] ThreadCapabilityBinding? Binding,
        [property: JsonPropertyName("capabilitySwitches")] List<ThreadCapabilitySwitch>? CapabilitySwitches = null,
        [property: JsonPropertyName("threadId")] string? ThreadId = null,
        [property: JsonPropertyName("workspace")] string? Workspace = null);
}
