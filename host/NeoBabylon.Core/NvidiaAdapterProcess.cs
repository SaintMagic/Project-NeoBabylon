using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace NeoBabylon.Core;

public sealed class NvidiaAdapterProcess : IAsyncDisposable
{
    private static readonly string[] AllowedModels =
    [
        "deepseek-ai/deepseek-v4.1-flash",
        "z-ai/glm-5.3",
        "moonshotai/kimi-k3"
    ];

    private readonly Process _process;
    private readonly Task _stderrDrain;
    private readonly string _modelIdentifier;
    private readonly string _executablePath;
    private readonly string _bundleSha256;
    private bool _disposed;

    private NvidiaAdapterProcess(Process process, Task stderrDrain, string endpoint, string sessionToken, ModelCapabilityRecord capability, string executablePath, string bundleSha256)
    {
        _process = process;
        _stderrDrain = stderrDrain;
        Endpoint = endpoint;
        SessionToken = sessionToken;
        _modelIdentifier = capability.ModelIdentifier;
        _executablePath = executablePath;
        _bundleSha256 = bundleSha256;
    }

    public string Endpoint { get; }
    [JsonIgnore]
    public string SessionToken { get; }

    public static async Task<NvidiaAdapterProcess> StartAsync(
        string sourceRoot,
        string applicationRoot,
        ModelCapabilityRecord capability,
        string apiKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capability);
        if (!string.Equals(capability.ProviderId, "nvidia", StringComparison.Ordinal)
            || !AllowedModels.Contains(capability.ModelIdentifier, StringComparer.Ordinal)
            || !Uri.TryCreate(capability.Endpoint, UriKind.Absolute, out var providerEndpoint)
            || providerEndpoint.Scheme != Uri.UriSchemeHttps
            || providerEndpoint.Host != "integrate.api.nvidia.com"
            || providerEndpoint.AbsolutePath.TrimEnd('/') != "/v1")
            throw new InvalidOperationException("The selected NVIDIA capability tuple is not supported.");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("NVIDIA API key is required.", nameof(apiKey));

        var projectPath = Path.GetFullPath(Path.Combine(sourceRoot, "host", "NeoBabylon.NvidiaAdapter", "NeoBabylon.NvidiaAdapter.csproj"));
        if (!File.Exists(projectPath))
            throw new FileNotFoundException("NVIDIA adapter project is missing.", projectPath);
        var output = Path.GetFullPath(Path.Combine(applicationRoot, "Build", "bin", "NeoBabylon.NvidiaAdapter", "release"));
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(output))
            throw new DirectoryNotFoundException("The launcher must build the NVIDIA adapter before provider selection.");
        var executable = Path.Combine(output, "NeoBabylon.NvidiaAdapter.exe");
        if (!File.Exists(executable))
            throw new InvalidOperationException("NVIDIA adapter publish did not produce its expected apphost.");
        var bundleHash = await ComputeBundleHash(output, cancellationToken).ConfigureAwait(false);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = output,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        CopyRuntimeEnvironment(startInfo.Environment);
        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
            throw new InvalidOperationException("NVIDIA adapter process could not be started.");
        var stderrDrain = DrainSanitizedStderr(process.StandardError);
        try
        {
            var efforts = ReadSupportedEfforts(capability.ReasoningControls);
            var maxTokens = capability.MaxCompletionTokensAdvertised.State == CapabilityState.Known
                ? capability.MaxCompletionTokensAdvertised.Int32Value ?? throw new InvalidOperationException("Known NVIDIA completion-token maximum is invalid.")
                : 32768;
            var bootstrap = new JsonObject
            {
                ["apiKey"] = apiKey,
                ["modelIdentifier"] = capability.ModelIdentifier,
                ["sessionToken"] = token,
                ["supportedReasoningEfforts"] = efforts,
                ["maxCompletionTokens"] = maxTokens
            };
            await process.StandardInput.WriteLineAsync(bootstrap.ToJsonString()).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            var readyTask = process.StandardOutput.ReadLineAsync(cancellationToken).AsTask();
            var readyLine = await readyTask.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
            if (readyLine is null || readyLine.Length > 2048)
                throw new InvalidOperationException("NVIDIA adapter did not return readiness metadata.");
            var ready = JsonNode.Parse(readyLine)?.AsObject() ?? throw new InvalidOperationException("NVIDIA adapter readiness metadata was invalid.");
            var readyNames = ready.Select(pair => pair.Key).Order(StringComparer.Ordinal).ToArray();
            if (!readyNames.SequenceEqual(new[] { "endpoint", "model", "provider" }, StringComparer.Ordinal)
                || ready["provider"]?.GetValue<string>() != "nvidia"
                || ready["model"]?.GetValue<string>() != capability.ModelIdentifier
                || !Uri.TryCreate(ready["endpoint"]?.GetValue<string>(), UriKind.Absolute, out var endpoint)
                || endpoint.Scheme != Uri.UriSchemeHttp
                || endpoint.Host != "127.0.0.1"
                || endpoint.Port is < 1 or > 65535
                || endpoint.AbsolutePath != "/v1")
                throw new InvalidOperationException("NVIDIA adapter readiness tuple did not match the selected model.");
            return new NvidiaAdapterProcess(process, stderrDrain, endpoint.ToString().TrimEnd('/'), token, capability, executable, bundleHash);
        }
        catch
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await stderrDrain.ConfigureAwait(false);
            process.Dispose();
            throw;
        }
    }

    public JsonObject GetDiagnostics() => new()
    {
        ["provider"] = "nvidia",
        ["model"] = _modelIdentifier,
        ["endpoint"] = Endpoint,
        ["processId"] = _process.Id,
        ["processExited"] = _process.HasExited,
        ["executable"] = _executablePath,
        ["bundleSha256"] = _bundleSha256,
        ["supportedScope"] = "text_and_function_tools_only",
        ["maximumRequestBodyBytes"] = 2 * 1024 * 1024,
        ["encryptedReasoningUnavailable"] = true,
        ["reasoningReplay"] = "bounded_in_memory_session_only; adapter_restart_requires_complete_assistant_reasoning_history"
    };

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (!_process.HasExited)
        {
            try { _process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }
        await _process.WaitForExitAsync().ConfigureAwait(false);
        await _stderrDrain.ConfigureAwait(false);
        _process.Dispose();
    }

    private static async Task<string> ComputeBundleHash(string directory, CancellationToken cancellationToken)
    {
        var files = new[]
        {
            "NeoBabylon.NvidiaAdapter.exe",
            "NeoBabylon.NvidiaAdapter.dll",
            "NeoBabylon.NvidiaAdapter.deps.json",
            "NeoBabylon.NvidiaAdapter.runtimeconfig.json"
        };
        using var aggregate = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var name in files)
        {
            var path = Path.Combine(directory, name);
            if (!File.Exists(path))
                throw new InvalidOperationException("The launcher-built NVIDIA adapter bundle is incomplete.");
            aggregate.AppendData(System.Text.Encoding.UTF8.GetBytes(name + "\0"));
            await using var stream = File.OpenRead(path);
            var digest = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            aggregate.AppendData(digest);
        }
        return Convert.ToHexString(aggregate.GetHashAndReset()).ToLowerInvariant();
    }

    private static JsonArray ReadSupportedEfforts(CapabilityObservation observation)
    {
        var result = new JsonArray();
        if (observation.State != CapabilityState.Known) return result;
        if (observation.Value is not JsonObject controls || controls["supported_efforts"] is not JsonArray efforts)
            throw new InvalidOperationException("Known NVIDIA reasoning controls do not contain supported_efforts.");
        foreach (var effort in efforts)
        {
            var value = effort?.GetValue<string>();
            if (value is not ("low" or "high" or "max"))
                throw new InvalidOperationException("The bounded NVIDIA adapter accepts only explicitly recorded low, high, and max reasoning efforts.");
            result.Add(value);
        }
        return result;
    }

    private static async Task DrainSanitizedStderr(StreamReader stderr)
    {
        while (await stderr.ReadLineAsync().ConfigureAwait(false) is not null) { }
    }

    private static void CopyRuntimeEnvironment(IDictionary<string, string?> destination)
    {
        destination.Clear();
        foreach (var name in new[] { "SystemRoot", "WINDIR", "PATH", "TEMP", "TMP", "USERPROFILE", "DOTNET_ROOT", "DOTNET_ROOT_X64" })
            if (Environment.GetEnvironmentVariable(name) is { } value)
                destination[name] = value;
    }
}
