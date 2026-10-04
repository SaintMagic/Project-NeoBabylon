using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace NeoBabylon.Core;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CapabilityState
{
    Known,
    Unknown
}

public sealed record CapabilityObservation(
    CapabilityState State,
    JsonNode? Value,
    string EvidenceSource,
    string? Note = null)
{
    public static CapabilityObservation Known<T>(T value, string evidenceSource, string? note = null) =>
        new(CapabilityState.Known, JsonValue.Create(value), evidenceSource, note);

    public static CapabilityObservation Unknown(string evidenceSource, string? note = null) =>
        new(CapabilityState.Unknown, null, evidenceSource, note);

    [JsonIgnore]
    public int? Int32Value => State == CapabilityState.Known && Value is JsonValue value && value.TryGetValue<int>(out var result) ? result : null;

    [JsonIgnore]
    public string? StringValue => State == CapabilityState.Known && Value is JsonValue value && value.TryGetValue<string>(out var result) ? result : null;
}

public sealed record ToolQualification(
    string? OperationId,
    string? State,
    string? ObservedOn = null,
    string? Scope = null,
    string? EvidenceSource = null);

public sealed record ModelCapabilityRecord(
    string ProviderId,
    string ProviderDisplayName,
    string ProviderServerVersion,
    string Endpoint,
    string ModelIdentifier,
    string ModelVariant,
    string Architecture,
    string ParameterCount,
    string ModelSizeBytes,
    CapabilityObservation Quantization,
    CapabilityObservation ContextWindowAdvertised,
    CapabilityObservation ContextWindowEffective,
    CapabilityObservation ReasoningControls,
    CapabilityObservation ToolFunctionCalling,
    CapabilityObservation StructuredOutput,
    CapabilityObservation AgentMetadata)
{
    public CapabilityObservation ProviderRoute { get; init; } = CapabilityObservation.Unknown("not-observed");
    private CapabilityObservation? _maxCompletionTokensAdvertised;

    // Legacy records omit this observation. Keep their serialized bytes unchanged for
    // cap-v1 saved-thread identities while exposing an Unknown observation to callers.
    [JsonIgnore]
    public CapabilityObservation MaxCompletionTokensAdvertised
    {
        get => _maxCompletionTokensAdvertised ?? CapabilityObservation.Unknown("not-observed");
        init => _maxCompletionTokensAdvertised = value;
    }

    [JsonPropertyName("maxCompletionTokensAdvertised")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CapabilityObservation? SerializedMaxCompletionTokensAdvertised
    {
        get => _maxCompletionTokensAdvertised;
        init => _maxCompletionTokensAdvertised = value;
    }

    public CapabilityObservation ApplyPatchToolType { get; init; } = CapabilityObservation.Unknown("not-observed");
    public IReadOnlyList<ToolQualification> ToolQualifications { get; init; } = Array.Empty<ToolQualification>();

    public static ModelCapabilityRecord CreateUnknown(string providerId, string endpoint, string modelIdentifier) =>
        new(
            providerId,
            "Unknown",
            "Unknown",
            endpoint,
            modelIdentifier,
            "Unknown",
            "Unknown",
            "Unknown",
            "Unknown",
            CapabilityObservation.Unknown("not-observed"),
            CapabilityObservation.Unknown("not-observed"),
            CapabilityObservation.Unknown("not-observed"),
            CapabilityObservation.Unknown("not-observed"),
            CapabilityObservation.Unknown("not-observed"),
            CapabilityObservation.Unknown("not-observed"),
            CapabilityObservation.Unknown("not-observed"));
}

public static class CapabilityAdapter
{
    public static IReadOnlyDictionary<string, object> ToCodexConfig(ModelCapabilityRecord record)
    {
        var config = new Dictionary<string, object>(StringComparer.Ordinal);
        if (record.ContextWindowEffective.Int32Value is int effective)
        {
            config["model_context_window"] = effective;
        }
        else if (record.ContextWindowAdvertised.Int32Value is int advertised)
        {
            config["model_context_window"] = advertised;
        }

        return config;
    }
}
