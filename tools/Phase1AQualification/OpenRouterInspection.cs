using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using NeoBabylon.Core;

namespace NeoBabylon.Phase1AQualification;

public static class OpenRouterInspection
{
    private const string ModelsEndpoint = "https://openrouter.ai/api/v1/models";

    public static async Task<JsonObject> ReadAndValidateAsync(
        ModelCapabilityRecord record,
        DateTimeOffset? observedAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var models = await client.GetFromJsonAsync<JsonObject>(ModelsEndpoint, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("OpenRouter returned an empty model catalog.");
        var model = models["data"]?.AsArray()
            .FirstOrDefault(candidate => candidate?["id"]?.GetValue<string>() == record.ModelIdentifier)
            ?? throw new InvalidDataException("The exact selected OpenRouter model is no longer present; no replacement is permitted.");

        var parts = record.ModelIdentifier.Split('/', 2);
        if (parts.Length != 2)
        {
            throw new InvalidDataException("The selected OpenRouter model identifier is not in author/model form.");
        }

        var endpointPath = $"https://openrouter.ai/api/v1/models/{Uri.EscapeDataString(parts[0])}/{Uri.EscapeDataString(parts[1])}/endpoints";
        var endpointResponse = await client.GetFromJsonAsync<JsonObject>(endpointPath, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("OpenRouter returned an empty endpoint catalog.");
        return ValidateSnapshot(record, model, endpointResponse, observedAtUtc ?? DateTimeOffset.UtcNow);
    }

    public static JsonObject ValidateSnapshot(
        ModelCapabilityRecord record,
        JsonNode model,
        JsonNode endpointResponse,
        DateTimeOffset observedAtUtc)
    {
        var endpointSegments = record.ModelIdentifier.Split('/', 2);
        if (endpointSegments.Length != 2)
        {
            throw new InvalidDataException("The selected OpenRouter model identifier is not in author/model form.");
        }

        var endpointPath = $"https://openrouter.ai/api/v1/models/{Uri.EscapeDataString(endpointSegments[0])}/{Uri.EscapeDataString(endpointSegments[1])}/endpoints";
        var metadata = record.AgentMetadata.StringValue
            ?? throw new InvalidDataException("OpenRouter route validation requires the authoritative agent-metadata record.");
        var modelId = RequiredString(model, "id");
        var canonicalSlug = RequiredString(model, "canonical_slug");
        var advertisedContext = RequiredInt(model, "context_length");
        var expiryText = RequiredString(model, "expiration_date");
        if (modelId != record.ModelIdentifier || canonicalSlug != record.ModelVariant)
        {
            throw new InvalidDataException("OpenRouter's current model identity differs from the selected record; no model fallback is permitted.");
        }

        if (record.ContextWindowAdvertised.Int32Value != advertisedContext)
        {
            throw new InvalidDataException("OpenRouter's advertised context differs from the selected capability record.");
        }

        if (!DateOnly.TryParseExact(expiryText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiry)
            || expiry <= DateOnly.FromDateTime(observedAtUtc.UtcDateTime))
        {
            throw new InvalidDataException("The selected OpenRouter free route is expired or its expiration date is unknown.");
        }

        var recordExpiry = MetadataValue(metadata, "expirationDate");
        var recordProvider = MetadataValue(metadata, "provider");
        var recordMaxCompletion = MetadataValue(metadata, "maxCompletionTokens");
        if (expiryText != recordExpiry)
        {
            throw new InvalidDataException("OpenRouter's free-route expiration differs from the capability record.");
        }

        var modelReasoning = model["reasoning"] as JsonObject
            ?? throw new InvalidDataException("OpenRouter did not report reasoning capability metadata.");
        if (record.ReasoningControls.Value is not JsonObject expectedReasoning
            || !JsonNode.DeepEquals(modelReasoning, expectedReasoning))
        {
            throw new InvalidDataException("OpenRouter reasoning controls/defaults differ from the capability record.");
        }

        RequireZeroPrice(model["pricing"], "model catalog");
        var requiredModelParameters = new[]
        {
            "reasoning", "reasoning_effort", "include_reasoning", "tools", "tool_choice", "response_format", "structured_outputs"
        };
        RequireParameters(model["supported_parameters"], requiredModelParameters, "model catalog");

        var routeData = endpointResponse["data"] as JsonObject
            ?? throw new InvalidDataException("OpenRouter's endpoint catalog omitted its data object.");
        if (RequiredString(routeData, "id") != record.ModelIdentifier)
        {
            throw new InvalidDataException("OpenRouter's endpoint catalog resolved to a different model ID.");
        }

        var endpointArray = routeData["endpoints"]?.AsArray()
            ?? throw new InvalidDataException("OpenRouter's endpoint catalog omitted its endpoint list.");
        if (endpointArray.Count != 1)
        {
            throw new InvalidDataException("The selected free model no longer has exactly one listed provider endpoint; route pinning is unavailable, so inference was not started.");
        }

        var endpoint = endpointArray[0] as JsonObject
            ?? throw new InvalidDataException("OpenRouter returned an invalid endpoint entry.");
        var providerName = RequiredString(endpoint, "provider_name");
        var quantization = RequiredString(endpoint, "quantization");
        var endpointTag = RequiredString(endpoint, "tag");
        var expectedRoute = record.ProviderRoute.State == CapabilityState.Known
            ? record.ProviderRoute.Value as JsonObject
            : null;
        if (expectedRoute is null)
        {
            throw new InvalidDataException("OpenRouter route validation requires an observed provider endpoint; no inference was started.");
        }

        var expectedProviderSlug = RequiredString(expectedRoute, "providerSlug");
        var expectedEndpointTag = RequiredString(expectedRoute, "endpointTag");
        var endpointModel = RequiredString(endpoint, "model_id");
        var endpointContext = RequiredInt(endpoint, "context_length");
        var maxCompletion = RequiredInt(endpoint, "max_completion_tokens");
        var expectedProvider = recordProvider ?? "Unknown";
        var expectedQuantization = record.Quantization.StringValue ?? "Unknown";
        if (endpointModel != record.ModelIdentifier
            || providerName != expectedProvider
            || quantization != expectedQuantization
            || endpointTag != expectedEndpointTag
            || !endpointTag.StartsWith($"{expectedProviderSlug}/", StringComparison.Ordinal)
            || endpointContext != advertisedContext
            || maxCompletion.ToString(CultureInfo.InvariantCulture) != recordMaxCompletion)
        {
            throw new InvalidDataException("OpenRouter's unique endpoint identity/capabilities differ from the selected record.");
        }

        RequireZeroPrice(endpoint["pricing"], "provider endpoint");
        RequireParameters(endpoint["supported_parameters"], requiredModelParameters, "provider endpoint");
        if (endpoint["supports_tool_choice"]?["auto"]?.GetValue<bool>() != true
            || endpoint["supports_tool_choice"]?["function"]?.GetValue<bool>() != true)
        {
            throw new InvalidDataException("The unique OpenRouter endpoint no longer advertises ordinary function tool calls.");
        }

        if (record.ToolFunctionCalling.StringValue != "tool_use")
        {
            throw new InvalidDataException("The authoritative capability record no longer marks tool calling as observed.");
        }

        var route = new JsonObject
        {
            ["endpointCount"] = endpointArray.Count,
            ["providerName"] = providerName,
            ["quantization"] = quantization,
            ["providerSlug"] = expectedProviderSlug,
            ["tag"] = endpointTag,
            ["pricePrompt"] = RequiredString(endpoint["pricing"]!, "prompt"),
            ["priceCompletion"] = RequiredString(endpoint["pricing"]!, "completion"),
            ["statusValue"] = endpoint["status"]?.DeepClone(),
            ["statusSemantics"] = "Unknown",
            ["actualResponseProviderAttestation"] = "Unknown; public endpoint catalog lists one current candidate"
        };
        var modelEvidence = new JsonObject
        {
            ["id"] = modelId,
            ["canonicalSlug"] = canonicalSlug,
            ["contextWindowAdvertised"] = advertisedContext,
            ["expirationDate"] = expiryText,
            ["reasoning"] = modelReasoning.DeepClone(),
            ["supportedParameters"] = model["supported_parameters"]?.DeepClone(),
            ["architecture"] = model["architecture"]?.DeepClone(),
            ["pricing"] = model["pricing"]?.DeepClone()
        };
        return new JsonObject
        {
            ["source"] = new JsonArray(
                JsonValue.Create(ModelsEndpoint),
                JsonValue.Create(endpointPath)),
            ["observedAtUtc"] = observedAtUtc,
            ["providerServerVersion"] = "Unknown",
            ["endpoint"] = record.Endpoint,
            ["model"] = modelEvidence,
            ["route"] = route,
            ["routePolicy"] = $"Pinned Codex Responses request to {expectedEndpointTag} with provider fallback disabled.",
            ["noInferenceFallbackConfigured"] = true
        };
    }

    private static string RequiredString(JsonNode node, string property) =>
        node[property]?.GetValue<string>()
        ?? throw new InvalidDataException($"OpenRouter metadata is missing string field `{property}`.");

    private static int RequiredInt(JsonNode node, string property) =>
        node[property]?.GetValue<int>()
        ?? throw new InvalidDataException($"OpenRouter metadata is missing integer field `{property}`.");

    private static void RequireZeroPrice(JsonNode? pricing, string source)
    {
        if (pricing is not JsonObject values
            || !IsZero(values["prompt"])
            || !IsZero(values["completion"]))
        {
            throw new InvalidDataException($"The selected OpenRouter {source} is no longer advertised at zero prompt/completion price.");
        }
    }

    private static bool IsZero(JsonNode? value)
    {
        var text = value?.ToString();
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) && price == 0;
    }

    private static void RequireParameters(JsonNode? node, IReadOnlyCollection<string> required, string source)
    {
        var values = node?.AsArray().Select(value => value?.GetValue<string>()).ToHashSet(StringComparer.Ordinal)
            ?? throw new InvalidDataException($"OpenRouter {source} omitted supported_parameters.");
        var missing = required.Where(parameter => !values.Contains(parameter)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidDataException($"OpenRouter {source} no longer advertises required parameters: {string.Join(", ", missing)}.");
        }
    }

    private static string? MetadataValue(string metadata, string key) =>
        metadata
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(part => part.StartsWith($"{key}=", StringComparison.OrdinalIgnoreCase))?
            .Split('=', 2)[1];
}
