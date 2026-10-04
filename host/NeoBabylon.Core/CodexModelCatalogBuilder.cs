using System.Text.Json.Nodes;

namespace NeoBabylon.Core;

/// Builds the stable Codex model-catalog shape from the authoritative provider
/// capability record. Values that are not provider observations are explicit
/// adapter constraints rather than copied GPT-family metadata.
public static class CodexModelCatalogBuilder
{
    private const string NeoBabylonBaseInstructions =
        "You are a model connected through NeoBabylon and Codex App Server. Follow the user's request, use only tools presented by the host, and report tool results accurately.";

    public static JsonObject Build(ModelCapabilityRecord capability)
    {
        if (string.IsNullOrWhiteSpace(capability.ModelIdentifier) || capability.ModelIdentifier == "Unknown")
        {
            throw new InvalidOperationException("A Codex catalog requires a verified model identifier.");
        }

        var context = capability.ContextWindowEffective.Int32Value
            ?? capability.ContextWindowAdvertised.Int32Value
            ?? throw new InvalidOperationException("A Codex catalog requires an observed context window.");

        if (capability.ToolFunctionCalling.StringValue is not "tool_use")
        {
            throw new InvalidOperationException("The Codex shell tool requires observed tool/function calling support.");
        }

        var agentMetadata = capability.AgentMetadata.StringValue;
        if (agentMetadata is null)
        {
            throw new InvalidOperationException("The Codex catalog requires observed agent metadata and input modalities.");
        }

        var inputModalities = BuildInputModalities(agentMetadata);
        var (defaultReasoningLevel, supportedReasoningLevels) = BuildReasoningLevels(capability.ReasoningControls);
        int? maxCompletionTokens = null;
        if (capability.MaxCompletionTokensAdvertised.State == CapabilityState.Known)
        {
            maxCompletionTokens = capability.MaxCompletionTokensAdvertised.Int32Value;
            if (maxCompletionTokens is null || maxCompletionTokens <= 0)
            {
                throw new InvalidOperationException("Known advertised completion-token maximum must be a positive integer.");
            }
        }

        var model = new JsonObject
        {
            ["slug"] = capability.ModelIdentifier,
            ["display_name"] = $"{capability.ProviderDisplayName} {capability.ModelIdentifier}",
            ["description"] = $"{capability.ProviderDisplayName} model qualified by NeoBabylon.",
            ["default_reasoning_level"] = defaultReasoningLevel,
            ["supported_reasoning_levels"] = supportedReasoningLevels,
            ["shell_type"] = "unified_exec",
            ["visibility"] = "none",
            ["supported_in_api"] = true,
            ["priority"] = 99,
            ["additional_speed_tiers"] = new JsonArray(),
            ["service_tiers"] = new JsonArray(),
            ["default_service_tier"] = null,
            ["availability_nux"] = null,
            ["upgrade"] = null,
            ["model_messages"] = null,
            ["base_instructions"] = NeoBabylonBaseInstructions,
            ["include_skills_usage_instructions"] = false,
            ["include_plugin_usage_instructions"] = false,
            ["include_apps_usage_instructions"] = false,
            ["supports_reasoning_summary_parameter"] = false,
            ["default_reasoning_summary"] = "none",
            ["support_verbosity"] = false,
            ["default_verbosity"] = null,
            ["apply_patch_tool_type"] = BuildApplyPatchToolType(capability.ApplyPatchToolType),
            ["web_search_tool_type"] = "text",
            ["truncation_policy"] = new JsonObject
            {
                ["mode"] = "bytes",
                ["limit"] = 10000
            },
            ["supports_image_detail_original"] = false,
            ["context_window"] = context,
            ["max_context_window"] = context,
            ["auto_compact_token_limit"] = null,
            ["comp_hash"] = null,
            ["effective_context_window_percent"] = 95,
            ["experimental_supported_tools"] = new JsonArray(),
            ["input_modalities"] = inputModalities,
            ["supports_search_tool"] = false,
            ["supports_experimental_context"] = false,
            ["use_responses_lite"] = false,
            ["node_repl_auto_review_required"] = false,
            ["node_repl_disabled"] = true,
            ["auto_review_model_override"] = null,
            ["model_specialty"] = null,
            ["tool_mode"] = null,
            ["multi_agent_version"] = null,
            ["multi_agent_reasoning_effort"] = null
        };

        if (maxCompletionTokens is int maximum)
        {
            model["max_completion_tokens"] = maximum;
        }

        return new JsonObject { ["models"] = new JsonArray(model) };
    }

    private static JsonArray BuildInputModalities(string metadata)
    {
        var explicitModalities = metadata
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(part => part.StartsWith("inputModalities=", StringComparison.OrdinalIgnoreCase));
        if (explicitModalities is not null)
        {
            var values = explicitModalities["inputModalities=".Length..]
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (values.Length == 0 || values.Any(value => value is not ("text" or "image")) || !values.Contains("text", StringComparer.Ordinal))
            {
                throw new InvalidOperationException("The Codex catalog only maps an observed text modality and optional image input.");
            }

            return new JsonArray(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        }

        if (HasMetadataValue(metadata, "vision", "False"))
        {
            return new JsonArray(JsonValue.Create("text"));
        }

        if (HasMetadataValue(metadata, "vision", "True"))
        {
            return new JsonArray(JsonValue.Create("text"), JsonValue.Create("image"));
        }

        throw new InvalidOperationException("The Codex catalog requires observed input modalities.");
    }

    private static JsonNode? BuildApplyPatchToolType(CapabilityObservation observation)
    {
        if (observation.State == CapabilityState.Unknown)
        {
            return null;
        }

        var toolType = observation.StringValue;
        if (toolType is not ("freeform" or "function"))
        {
            throw new InvalidOperationException("Known apply_patch tool metadata must be either freeform or function.");
        }

        return JsonValue.Create(toolType);
    }

    private static (JsonNode? Default, JsonArray Supported) BuildReasoningLevels(CapabilityObservation observation)
    {
        if (observation.State == CapabilityState.Unknown)
        {
            return (null, new JsonArray());
        }
        if (CapabilitySwitchSafety.HasBooleanReasoningControls(observation))
        {
            // The provider capability remains Known; the pinned Codex effort wire
            // cannot express its Boolean enabled control without an unqualified alias.
            return (null, new JsonArray());
        }

        if (observation.Value is not JsonObject controls
            || controls["supported_efforts"] is not JsonArray efforts)
        {
            throw new InvalidOperationException("Known reasoning controls must include an observed supported_efforts array.");
        }

        var supported = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in efforts)
        {
            var effort = value?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(effort) || !seen.Add(effort))
            {
                throw new InvalidOperationException("Provider reasoning efforts must be non-empty, unique strings.");
            }

            supported.Add(new JsonObject
            {
                ["effort"] = effort,
                ["description"] = $"Provider-reported reasoning effort: {effort}."
            });
        }

        var defaultEffort = controls["default_effort"]?.GetValue<string>();
        if (defaultEffort is not null && !seen.Contains(defaultEffort))
        {
            throw new InvalidOperationException("The provider default reasoning effort is not present in its supported_efforts list.");
        }

        return (defaultEffort is null ? null : JsonValue.Create(defaultEffort), supported);
    }

    private static bool HasMetadataValue(string metadata, string key, string expected) =>
        metadata
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(part => part.StartsWith($"{key}=", StringComparison.OrdinalIgnoreCase)
                && string.Equals(part[(key.Length + 1)..], expected, StringComparison.OrdinalIgnoreCase));
}
