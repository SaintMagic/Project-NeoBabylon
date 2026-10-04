namespace NeoBabylon.Phase1AQualification;

public static class QualificationMockProbe
{
    private const string ProbeArgument = "--mock-probe";

    public static string? ResolveCommand(IReadOnlyList<string> arguments, bool mockOnly, bool openRouterMode)
    {
        var matches = arguments
            .Select((value, index) => (value, index))
            .Where(item => string.Equals(item.value, ProbeArgument, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length == 0)
        {
            return null;
        }

        if (matches.Length != 1 || matches[0].index + 1 >= arguments.Count)
        {
            throw new InvalidDataException("--mock-probe requires exactly one supported probe name.");
        }

        if (!mockOnly)
        {
            throw new InvalidDataException("--mock-probe is allowed only together with --mock-only.");
        }

        var probeName = arguments[matches[0].index + 1];
        if (openRouterMode)
        {
            if (string.Equals(probeName, "openrouter-key-exclusion", StringComparison.OrdinalIgnoreCase))
            {
                return "if (Test-Path Env:OPENROUTER_API_KEY) { Write-Output NB_OPENROUTER_KEY_PRESENT } else { Write-Output NB_OPENROUTER_KEY_ABSENT }; cmd.exe /d /c ver";
            }

            throw new InvalidDataException("OpenRouter mock mode supports only the openrouter-key-exclusion probe.");
        }

        if (!string.Equals(probeName, "workspace-write", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(probeName, "parent-delete", StringComparison.OrdinalIgnoreCase))
            {
                return "Write-Output PROBE_DELETE_ATTEMPTED; cmd.exe /d /c del ..\\delete-canary.txt; if ($LASTEXITCODE -ne 0) { Write-Output PROBE_DELETE_DENIED; exit 1 }; Write-Output PROBE_OUTSIDE_DELETE_OK; cmd.exe /d /c ver";
            }

            if (string.Equals(probeName, "parent-write", StringComparison.OrdinalIgnoreCase))
            {
                return "Set-Content -LiteralPath ..\\outside-write.txt -Value NEOBABYLON_OUTSIDE_PROBE -ErrorAction Stop; Write-Output PROBE_OUTSIDE_WRITE_OK; cmd.exe /d /c ver";
            }

            throw new InvalidDataException($"Unsupported mock probe '{probeName}'. Supported probes: workspace-write, parent-write, parent-delete.");
        }

        return "Set-Content -LiteralPath .neobabylon-sandbox-probe.txt -Value NEOBABYLON_SANDBOX_PROBE -ErrorAction Stop; Write-Output PROBE_OK; cmd.exe /d /c ver";
    }
}
