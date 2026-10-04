import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { assertQaRunsParent, newQaRun } from "./qa-run-root.mjs";

const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const qaRunsParent = path.join(sourceRoot, ".local", "Lab", "Runs");
const [runRootArg] = process.argv.slice(2);

assert.ok(runRootArg, "usage: node p2-11-live-nex-output.mjs <new .local\\Lab\\Runs run ID>");

assertQaRunsParent(sourceRoot, qaRunsParent);
const { runRoot } = newQaRun(qaRunsParent, runRootArg);
const resultPath = path.join(runRoot, "result.json");

const modelId = "nex-agi/nex-n2.5-pro:free";
const modelVariant = "nex-agi/nex-n2.5-pro-20260907:free";
const routeTag = "nex-agi/fp8";
const requestedMaxOutputTokens = 8192;
const minimumVisibleCharacters = 8_000;
const startedAt = new Date().toISOString();
const result = {
  test: "P2-11 live OpenRouter NEX large-output qualification",
  startedAt,
  sourceRoot,
  runRoot,
  dataHandling: "Response text is not written to disk or stdout; result stores only lengths, timing, status, and SHA-256.",
  outcome: "not_started",
  credential: "process environment only; secret value is never serialized",
  request: {
    api: "OpenRouter Responses API",
    modelId,
    routeTag,
    allowFallbacks: false,
    maxPrice: { prompt: 0, completion: 0 },
    quantizations: ["fp8"],
    maxOutputTokens: requestedMaxOutputTokens,
    tools: [],
    toolChoice: "none",
    automaticRetries: 0,
  },
};

function dateInBratislava() {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Europe/Bratislava",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(new Date());
  const part = (type) => parts.find((item) => item.type === type)?.value;
  return `${part("year")}-${part("month")}-${part("day")}`;
}

async function getJson(url, apiKey, timeoutMs = 30_000) {
  const response = await fetch(url, {
    headers: { Authorization: `Bearer ${apiKey}` },
    signal: AbortSignal.timeout(timeoutMs),
  });
  const body = await response.json().catch(() => null);
  if (!response.ok) {
    const error = body?.error ?? {};
    const message = typeof error.message === "string"
      ? error.message.replace(/sk-or-v1-[A-Za-z0-9-]+/g, "<redacted>").slice(0, 240)
      : "No structured error message";
    throw Object.assign(new Error(message), { httpStatus: response.status, errorCode: error.code ?? null });
  }
  return body;
}

function parseSseEvent(block) {
  const lines = block.split("\n");
  const eventName = lines.find((line) => line.startsWith("event:"))?.slice(6).trim() ?? null;
  const data = lines.filter((line) => line.startsWith("data:")).map((line) => line.slice(5).trimStart()).join("\n");
  if (!data || data === "[DONE]") return null;
  try {
    return { eventName, payload: JSON.parse(data) };
  } catch {
    return { eventName, parseError: true };
  }
}

function responseText(response) {
  if (!Array.isArray(response?.output)) return "";
  return response.output.flatMap((item) => item?.type === "message" && Array.isArray(item.content)
    ? item.content.filter((part) => part?.type === "output_text").map((part) => part.text ?? "")
    : []).join("");
}

function selectRouteMetadata(value) {
  if (!value || typeof value !== "object") return null;
  const selected = {};
  for (const key of ["provider_name", "provider_slug", "endpoint", "endpoint_tag", "model", "generation_id"]) {
    if (typeof value[key] === "string" || typeof value[key] === "number") selected[key] = value[key];
  }
  return Object.keys(selected).length ? selected : null;
}

async function run() {
  const apiKey = process.env.OPENROUTER_API_KEY;
  if (!apiKey) {
    result.outcome = "blocked";
    result.blocker = "OPENROUTER_API_KEY is not available to this process; no model request was made.";
    return;
  }

  const [catalog, endpointCatalog] = await Promise.all([
    getJson("https://openrouter.ai/api/v1/models", apiKey),
    getJson(`https://openrouter.ai/api/v1/models/${modelId.replace(":", "%3A")}/endpoints`, apiKey),
  ]);
  const models = (catalog.data ?? []).filter((model) => model.id === modelId);
  const endpoints = endpointCatalog.data?.endpoints ?? [];
  const selectedEndpoints = endpoints.filter((endpoint) => endpoint.tag === routeTag);
  const selectedModel = models.length === 1 ? models[0] : null;
  const selectedEndpoint = selectedEndpoints.length === 1 ? selectedEndpoints[0] : null;
  const localDate = dateInBratislava();

  result.preflight = {
    observedAt: new Date().toISOString(),
    localDate: `${localDate} Europe/Bratislava`,
    modelMatchCount: models.length,
    endpointCount: endpoints.length,
    exactEndpointMatchCount: selectedEndpoints.length,
    model: selectedModel ? {
      id: selectedModel.id,
      expirationDate: selectedModel.expiration_date ?? null,
      contextLength: selectedModel.context_length ?? null,
      maxCompletionTokens: selectedModel.top_provider?.max_completion_tokens ?? null,
      promptPrice: selectedModel.pricing?.prompt ?? null,
      completionPrice: selectedModel.pricing?.completion ?? null,
      supportedParameters: selectedModel.supported_parameters ?? null,
    } : null,
    endpoint: selectedEndpoint ? {
      providerName: selectedEndpoint.provider_name ?? null,
      tag: selectedEndpoint.tag ?? null,
      rawStatus: selectedEndpoint.status ?? null,
      statusSemantics: "Unknown",
      contextLength: selectedEndpoint.context_length ?? null,
      maxCompletionTokens: selectedEndpoint.max_completion_tokens ?? null,
      promptPrice: selectedEndpoint.pricing?.prompt ?? null,
      completionPrice: selectedEndpoint.pricing?.completion ?? null,
      quantization: selectedEndpoint.quantization ?? null,
      endpointExpirationDate: selectedEndpoint.endpoint_expiration_date ?? null,
    } : null,
    userKeyRestriction: "User-stated free-only; not independently inspectable.",
  };

  const preflightOk = models.length === 1
    && endpoints.length === 1
    && selectedEndpoints.length === 1
    && selectedModel?.id === modelId
    && selectedEndpoint?.provider_name === "Nex AGI"
    && selectedEndpoint?.context_length === 262144
    && selectedEndpoint?.max_completion_tokens === 235929
    && selectedEndpoint?.quantization === "fp8"
    && selectedEndpoint?.pricing?.prompt === "0"
    && selectedEndpoint?.pricing?.completion === "0"
    && selectedModel?.expiration_date >= localDate;

  if (!preflightOk) {
    result.outcome = "blocked-preflight";
    result.blocker = "The current catalog no longer matches the accepted exact NEX free route, capability, and zero-price constraints; no model request was made.";
    return;
  }

  const prompt = [
    "This is a bounded response-size test. Write a substantial, self-contained technical handbook about evidence quality and repeatable QA for desktop agent applications.",
    "Produce 24 numbered sections. Each section must contain two distinct paragraphs of at least 45 words each. Avoid filler and do not repeat earlier material.",
    "Return plain text only. Do not use tools, make network requests, or perform any action. Continue until all sections are complete or the explicit output-token limit stops the response.",
  ].join("\n\n");
  result.request.promptSha256 = createHash("sha256").update(prompt).digest("hex");

  const body = {
    model: modelId,
    input: prompt,
    max_output_tokens: requestedMaxOutputTokens,
    stream: true,
    tools: [],
    tool_choice: "none",
    provider: {
      only: [routeTag],
      allow_fallbacks: false,
      quantizations: ["fp8"],
      max_price: { prompt: 0, completion: 0 },
    },
  };

  const requestStarted = Date.now();
  const request = await fetch("https://openrouter.ai/api/v1/responses", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${apiKey}`,
      "Content-Type": "application/json",
      "X-OpenRouter-Metadata": "enabled",
    },
    body: JSON.stringify(body),
    signal: AbortSignal.timeout(15 * 60_000),
  });

  result.httpStatus = request.status;
  result.request.requestId = request.headers.get("x-request-id") ?? null;
  result.request.generationId = request.headers.get("x-openrouter-generation-id") ?? null;
  if (!request.ok) {
    const errorBody = await request.json().catch(() => null);
    const error = errorBody?.error ?? {};
    result.outcome = "provider-error";
    result.providerError = {
      code: error.code ?? null,
      message: typeof error.message === "string"
        ? error.message.replace(/sk-or-v1-[A-Za-z0-9-]+/g, "<redacted>").slice(0, 240)
        : "No structured error message",
    };
    return;
  }

  let output = "";
  let finalResponse = null;
  let routeMetadata = null;
  let streamError = null;
  let firstTextAt = null;
  let eventCount = 0;
  let outputDeltaCount = 0;
  let incompleteDetail = null;
  let buffer = "";
  const reader = request.body.getReader();
  const decoder = new TextDecoder();

  function consume(block) {
    if (!block.trim()) return;
    const parsed = parseSseEvent(block);
    if (!parsed) return;
    eventCount++;
    if (parsed.parseError) {
      streamError = "The SSE stream contained a non-JSON event.";
      return;
    }
    const event = parsed.payload;
    const type = event.type ?? parsed.eventName;
    if (type === "response.output_text.delta" && typeof event.delta === "string") {
      if (firstTextAt === null) firstTextAt = Date.now();
      output += event.delta;
      outputDeltaCount++;
      if (output.length >= minimumVisibleCharacters && (output.length - event.delta.length) < minimumVisibleCharacters) {
        console.log(`LIVE_NEX_PROGRESS visibleChars=${output.length}`);
      }
    } else if (type === "response.completed") {
      finalResponse = event.response ?? event;
      routeMetadata = selectRouteMetadata(finalResponse.openrouter_metadata ?? event.openrouter_metadata);
    } else if (type === "response.incomplete") {
      finalResponse = event.response ?? event;
      incompleteDetail = finalResponse.incomplete_details ?? event.incomplete_details ?? null;
      routeMetadata = selectRouteMetadata(finalResponse.openrouter_metadata ?? event.openrouter_metadata);
    } else if (type === "response.failed" || type === "error") {
      streamError = event.error?.message ?? event.message ?? type;
    }
  }

  try {
    for (;;) {
      const { value, done } = await reader.read();
      buffer += decoder.decode(value, { stream: !done }).replace(/\r\n/g, "\n");
      let separator;
      while ((separator = buffer.indexOf("\n\n")) >= 0) {
        consume(buffer.slice(0, separator));
        buffer = buffer.slice(separator + 2);
      }
      if (done) break;
    }
    if (buffer.trim()) consume(buffer);
  } finally {
    reader.releaseLock();
  }

  if (!output && finalResponse) output = responseText(finalResponse);
  const finishedAt = Date.now();
  const resolvedModel = finalResponse?.model ?? null;
  const modelMatches = resolvedModel === modelId || resolvedModel === modelVariant;
  const outputItemTypes = Array.isArray(finalResponse?.output)
    ? [...new Set(finalResponse.output.map((item) => item?.type ?? "Unknown"))]
    : [];
  const generationId = routeMetadata?.generation_id
    ?? result.request.generationId
    ?? (typeof finalResponse?.id === "string" && finalResponse.id.startsWith("gen-") ? finalResponse.id : null);

  result.response = {
    id: finalResponse?.id ?? null,
    status: finalResponse?.status ?? (incompleteDetail ? "incomplete" : null),
    incompleteDetails: incompleteDetail,
    resolvedModel,
    modelMatchesAcceptedAliasOrVariant: modelMatches,
    outputItemTypes,
    outputChars: output.length,
    outputUtf8Bytes: Buffer.byteLength(output, "utf8"),
    outputSha256: createHash("sha256").update(output).digest("hex"),
    usage: finalResponse?.usage ?? null,
    eventCount,
    outputDeltaCount,
    timeToFirstTextMs: firstTextAt === null ? null : firstTextAt - requestStarted,
    totalElapsedMs: finishedAt - requestStarted,
    routeMetadata,
    routeTagPostHocAttested: routeMetadata?.endpoint_tag === routeTag || routeMetadata?.endpoint === routeTag,
    streamError,
  };

  if (generationId) {
    try {
      const generation = await getJson(
        `https://openrouter.ai/api/v1/generation?id=${encodeURIComponent(generationId)}`,
        apiKey,
        30_000,
      );
      const data = generation.data ?? {};
      result.generationAttestation = {
        generationId,
        providerName: data.provider_name ?? null,
        model: data.model ?? null,
        totalCost: data.total_cost ?? null,
        isByok: data.is_byok ?? null,
        streamed: data.streamed ?? null,
        tokensPrompt: data.tokens_prompt ?? null,
        tokensCompletion: data.tokens_completion ?? null,
        nativeTokensPrompt: data.native_tokens_prompt ?? null,
        nativeTokensCompletion: data.native_tokens_completion ?? null,
        nativeTokensReasoning: data.native_tokens_reasoning ?? null,
        latencyMs: data.latency ?? null,
      };
    } catch (error) {
      result.generationAttestation = {
        generationId,
        lookupStatus: error.httpStatus ?? null,
        lookup: "unavailable; no repeat request was made",
      };
    }
  } else {
    result.generationAttestation = "No generation identifier was exposed by the response headers or metadata.";
  }

  const generation = result.generationAttestation;
  const providerAttested = generation && typeof generation === "object"
    ? generation.providerName === "Nex AGI"
      && (generation.model === modelId || generation.model === modelVariant)
      && generation.totalCost === 0
      && generation.isByok === false
    : false;
  const outputLargeEnough = output.length >= minimumVisibleCharacters;
  const completed = result.response.status === "completed";
  result.outcome = !streamError && modelMatches && outputLargeEnough && completed && providerAttested
    ? "passed"
    : "partial-or-failed";
  result.acceptance = {
    providerAttested,
    outputLargeEnough,
    completed,
    minimumVisibleCharacters,
    note: "Provider endpoint tag is separately request-pinned; do not infer post-hoc tag attestation unless routeTagPostHocAttested is true.",
  };
}

try {
  await run();
} catch (error) {
  result.outcome = "error";
  result.error = {
    name: error?.name ?? "Error",
    message: typeof error?.message === "string"
      ? error.message.replace(/sk-or-v1-[A-Za-z0-9-]+/g, "<redacted>").slice(0, 400)
      : "Unknown error",
    httpStatus: error?.httpStatus ?? null,
    code: error?.errorCode ?? null,
  };
} finally {
  result.finishedAt = new Date().toISOString();
  writeFileSync(resultPath, `${JSON.stringify(result, null, 2)}\n`, { encoding: "utf8", flag: "wx" });
  console.log(`P2_11_LIVE_NEX ${JSON.stringify({ outcome: result.outcome, resultPath, preflight: result.preflight ?? null, response: result.response ?? null, generationAttestation: result.generationAttestation ?? null, blocker: result.blocker ?? result.error ?? result.providerError ?? null })}`);
}
