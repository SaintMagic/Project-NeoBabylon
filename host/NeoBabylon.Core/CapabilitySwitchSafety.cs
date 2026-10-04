using System.Text.Json.Nodes;

namespace NeoBabylon.Core;

public static class CapabilitySwitchSafety
{
    private static readonly HashSet<string> PinnedReasoningEfforts = new(StringComparer.Ordinal)
    {
        "none", "minimal", "low", "medium", "high", "xhigh", "max", "ultra", "persistent"
    };

    public static bool IsPinnedReasoningEffort(string? effort) => effort is not null && PinnedReasoningEfforts.Contains(effort);

    public static void RequireSupportedReasoningEffort(ModelCapabilityRecord capability, string? effort)
    {
        if (effort is null) return; // Omission inherits runtime state; it is not a reset.
        if (HasBooleanReasoningControls(capability.ReasoningControls))
            throw new InvalidDataException("The selected model advertises Boolean reasoning.enabled controls, but pinned turn/start supports effort only. No qualified Boolean transport mapping is available.");
        if (!IsPinnedReasoningEffort(effort) || capability.ReasoningControls.State != CapabilityState.Known)
            throw new InvalidDataException("The exact selected model does not advertise this reasoning effort; no override or mapping is permitted.");
        _ = GetSelectedDefaultReasoningEffort(capability);
        var supported = (JsonArray)((JsonObject)capability.ReasoningControls.Value!)["supported_efforts"]!;
        if (!supported.Any(item => string.Equals(item?.GetValue<string>(), effort, StringComparison.Ordinal)))
            throw new InvalidDataException("The exact selected model does not advertise this reasoning effort; no override or mapping is permitted.");
    }

    public static string? GetSelectedDefaultReasoningEffort(ModelCapabilityRecord capability)
    {
        if (capability.ReasoningControls.State == CapabilityState.Unknown) return null;
        if (HasBooleanReasoningControls(capability.ReasoningControls)) return null;
        if (capability.ReasoningControls.Value is not JsonObject controls
            || controls["supported_efforts"] is not JsonArray supported)
        {
            throw new InvalidDataException("Known reasoning controls must include their supported effort list.");
        }

        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in supported)
        {
            var effort = item?.GetValue<string>();
            if (effort is null || !PinnedReasoningEfforts.Contains(effort) || !values.Add(effort))
                throw new InvalidDataException("The selected capability contains an unsupported or duplicate reasoning effort.");
        }

        var defaultEffort = controls["default_effort"]?.GetValue<string>();
        if (defaultEffort is not null && !values.Contains(defaultEffort))
            throw new InvalidDataException("The selected capability default effort is not in its supported effort list.");
        return defaultEffort;
    }

    public static bool HasBooleanReasoningControls(CapabilityObservation observation)
    {
        if (observation.State != CapabilityState.Known || observation.Value is not JsonObject controls
            || controls["control_type"] is not JsonValue type || !type.TryGetValue<string>(out var value)
            || value != "enabled") return false;
        if (controls["mandatory"] is not JsonValue mandatory || !mandatory.TryGetValue<bool>(out _)
            || controls["default_enabled"] is not JsonValue defaultEnabled || !defaultEnabled.TryGetValue<bool>(out _)
            || controls.ContainsKey("supported_efforts") || controls.ContainsKey("default_effort"))
            throw new InvalidDataException("Known Boolean reasoning controls require Boolean flags without native effort levels/defaults.");
        return true;
    }

    public static void RequireNoActiveContinuingCommands(JsonObject? commandList, string operation = "changing models")
    {
        if (commandList?["data"] is not JsonArray data)
            throw new InvalidOperationException($"Command state could not be verified; stop or wait for any continuing command before {operation}.");
        if (data.Count != 0)
            throw new InvalidOperationException($"A command is still running in this conversation. Stop it or wait for it to exit before {operation}.");
    }

    public static void RequireManualCompactionEligible(
        string requestedThreadId, string? selectedThreadId, bool verifiedClient, bool busy)
    {
        if (busy) throw new InvalidOperationException("Finish the current App Server operation before compacting context.");
        if (string.IsNullOrWhiteSpace(requestedThreadId)
            || !string.Equals(requestedThreadId, selectedThreadId, StringComparison.Ordinal))
            throw new InvalidOperationException("Compaction is limited to the exact selected App Server thread.");
        if (!verifiedClient)
            throw new InvalidOperationException("Resume the selected verified App Server thread before compacting context.");
    }
}
