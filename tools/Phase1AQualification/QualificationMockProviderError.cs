namespace NeoBabylon.Phase1AQualification;

public static class QualificationMockProviderError
{
    private const string ErrorArgument = "--mock-provider-error";

    public static int? ResolveStatusCode(IReadOnlyList<string> arguments, bool mockOnly, bool openRouterMode)
    {
        var matches = arguments
            .Select((value, index) => (value, index))
            .Where(item => string.Equals(item.value, ErrorArgument, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length == 0)
        {
            return null;
        }

        if (matches.Length != 1 || matches[0].index + 1 >= arguments.Count)
        {
            throw new InvalidDataException("--mock-provider-error requires exactly one supported status code.");
        }

        if (!mockOnly)
        {
            throw new InvalidDataException("--mock-provider-error is allowed only together with --mock-only.");
        }

        if (openRouterMode)
        {
            throw new InvalidDataException("--mock-provider-error currently supports the local LM Studio fixture only.");
        }

        if (arguments.Any(argument => string.Equals(argument, "--mock-probe", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("--mock-provider-error cannot be combined with --mock-probe.");
        }

        if (!string.Equals(arguments[matches[0].index + 1], "429", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unsupported mock provider status code. Supported status code: 429.");
        }

        return 429;
    }
}
