using NeoBabylon.Core;

internal static class AgentPolicyChecks
{
    public static void Run(Action<string, Action> check)
    {
        foreach (var file in new[]
        {
            "MODEL_CAPABILITY_LMSTUDIO_QWEN3_14B.json",
            "MODEL_CAPABILITY_OPENROUTER_STEALTH_SPACE_BUNNY_ALPHA.json",
            "MODEL_CAPABILITY_NVIDIA_GLM_5_3.json"
        })
        {
            var capability = Fixture.Capability(file);
            var prompt = CodexModelCatalogBuilder.Build(capability)["models"]![0]!["base_instructions"]!.GetValue<string>();
            check(capability.ProviderId + " default initiative policy", () =>
            {
                foreach (var required in new[] { "routine prerequisites", "verify", "personal files", "Microsoft VDI", "malware", "not containment" })
                    Fixture.Require(prompt.Contains(required, StringComparison.OrdinalIgnoreCase), "Missing policy contract: " + required);
                Fixture.Require(prompt.Length < 6000, "Default policy unexpectedly became a large tool manual.");
            });
            check(capability.ProviderId + " policy preserves exact model", () =>
                Fixture.Require(CodexModelCatalogBuilder.Build(capability)["models"]![0]!["slug"]!.GetValue<string>() == capability.ModelIdentifier,
                    "A prompt change must not substitute the selected model."));
        }
    }
}
