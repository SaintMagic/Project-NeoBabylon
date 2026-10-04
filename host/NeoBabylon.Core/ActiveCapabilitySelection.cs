namespace NeoBabylon.Core;

// New user selection only. Historical capability loading and bindings remain unfiltered.
public static class ActiveCapabilitySelection
{
    private static readonly IReadOnlyDictionary<(string Provider, string Model), string> CurrentFiles =
        new Dictionary<(string, string), string>
        {
            [("openrouter", "stealth/space-bunny-alpha")] = "MODEL_CAPABILITY_OPENROUTER_STEALTH_SPACE_BUNNY_ALPHA_20261004.json",
            [("openrouter", "inclusionai/ling-3.1-flash")] = "MODEL_CAPABILITY_OPENROUTER_LING_3_1_FLASH_20261004.json"
        };

    public static bool IsSelectable(ModelCapabilityRecord capability, string sourceFile) =>
        IsSelectable(capability)
        && (!CurrentFiles.TryGetValue((capability.ProviderId, capability.ModelIdentifier), out var currentFile)
            || string.Equals(Path.GetFileName(sourceFile), currentFile, StringComparison.Ordinal));

    public static bool IsSelectable(ModelCapabilityRecord capability)
    {
        ArgumentNullException.ThrowIfNull(capability);
        return !(string.Equals(capability.ProviderId, "openrouter", StringComparison.Ordinal)
            && string.Equals(capability.ModelIdentifier, "nex-agi/nex-n2.5-pro:free", StringComparison.Ordinal));
    }

    public static void RequireSelectable(ModelCapabilityRecord capability)
    {
        if (!IsSelectable(capability))
            throw new InvalidOperationException("This provider/model is retired from new selection; its saved history and original capability binding remain available.");
    }
}
