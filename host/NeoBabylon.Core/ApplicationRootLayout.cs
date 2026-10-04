namespace NeoBabylon.Core;

public sealed record ApplicationRootLayout(
    string SourceRepositoryRoot,
    string ApplicationRoot,
    string DataRoot,
    string CodexHome,
    string FixtureWorkspace)
{
    public string WebView2UserDataFolder => Path.Combine(DataRoot, "WebView2");

    public static ApplicationRootLayout Create(string sourceRepositoryRoot, string applicationRoot)
    {
        var source = Normalize(sourceRepositoryRoot);
        var app = Normalize(applicationRoot);
        if (IsWithinOrEqual(source, app))
        {
            if (ContainsTraversal(applicationRoot) || !IsAllowedLocalApplicationRoot(source, app))
            {
                throw new InvalidOperationException("The NeoBabylon application root must be outside the source repository or an exact approved .local application root.");
            }

            RejectExistingReparseComponents(app);
        }

        var data = Path.Combine(app, "Data");
        var codexHome = Path.Combine(data, "CodexHome");
        if (IsOrdinaryCodexRoot(app) || IsOrdinaryCodexRoot(codexHome))
        {
            throw new InvalidOperationException("The NeoBabylon runtime state must not use the ordinary Codex data/configuration root.");
        }

        var fixtureWorkspace = Path.Combine(data, "Workspace");
        if (IsWithinOrEqual(source, app))
        {
            // Validate both branches before either creation can follow an existing junction.
            RejectExistingReparseComponents(codexHome);
            RejectExistingReparseComponents(fixtureWorkspace);
        }

        Directory.CreateDirectory(codexHome);
        Directory.CreateDirectory(fixtureWorkspace);
        return new ApplicationRootLayout(source, app, data, codexHome, fixtureWorkspace);
    }

    public static bool IsOrdinaryCodexRoot(string candidate)
    {
        var normalized = Normalize(candidate);
        var ordinary = Normalize(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex"));
        return string.Equals(normalized, ordinary, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool ContainsTraversal(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(component => component is "." or "..");

    private static bool IsAllowedLocalApplicationRoot(string source, string app)
    {
        var components = Path.GetRelativePath(source, app)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (components.Length == 2
            && SameComponent(components[0], ".local")
            && SameComponent(components[1], "App"))
        {
            return true;
        }

        return components.Length == 5
            && SameComponent(components[0], ".local")
            && SameComponent(components[1], "Lab")
            && SameComponent(components[2], "Runs")
            && IsValidRunId(components[3])
            && SameComponent(components[4], "App");
    }

    private static bool SameComponent(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsValidRunId(string runId) =>
        runId.Length is >= 1 and <= 80
        && IsAsciiAlphanumeric(runId[0])
        && IsAsciiAlphanumeric(runId[^1])
        && runId.All(character => IsAsciiAlphanumeric(character) || character == '-');

    private static bool IsAsciiAlphanumeric(char character) =>
        character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';

    private static void RejectExistingReparseComponents(string app)
    {
        var root = Path.GetPathRoot(app)
            ?? throw new InvalidOperationException("The application root has no filesystem root.");
        var current = root;
        RejectReparsePoint(current);
        foreach (var component in app[root.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, component);
            RejectReparsePoint(current);
        }
    }

    private static void RejectReparsePoint(string path)
    {
        try
        {
            if (new DirectoryInfo(path).LinkTarget is not null
                || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("The NeoBabylon application root must not traverse a reparse point.");
            }
        }
        catch (FileNotFoundException)
        {
            // New directories do not have attributes yet.
        }
        catch (DirectoryNotFoundException)
        {
            // New directories do not have attributes yet.
        }
    }

    private static bool IsWithinOrEqual(string parent, string candidate)
    {
        if (string.Equals(parent, candidate, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
