using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoBabylon.Core;

public sealed record WorkspaceProject(string WorkspacePath, string Name);
public sealed record WorkspaceProjectState(string SelectedWorkspace, IReadOnlyList<WorkspaceProject> Projects);

public sealed class WorkspaceProjectRegistry
{
    private const int SchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _dataRoot;
    private readonly string _fixtureWorkspace;

    public WorkspaceProjectRegistry(string dataRoot, string fixtureWorkspace)
    {
        _dataRoot = Normalize(dataRoot);
        _fixtureWorkspace = Normalize(fixtureWorkspace);
        RegistryPath = Path.Combine(_dataRoot, "NeoBabylon", "projects.json");
    }

    public string RegistryPath { get; }

    public WorkspaceProjectState Read()
    {
        var bytes = ProtectedDataRecordFile.Read(RegistryPath, value => Parse(value));
        if (bytes is null)
        {
            return new WorkspaceProjectState(_fixtureWorkspace, [new WorkspaceProject(_fixtureWorkspace, "NeoBabylon")]);
        }

        return Parse(bytes);
    }

    private static WorkspaceProjectState Parse(byte[] bytes)
    {
        var document = JsonSerializer.Deserialize<WorkspaceProjectDocument>(bytes, JsonOptions)
            ?? throw new InvalidDataException("NeoBabylon project registry is empty or invalid.");
        if (document.SchemaVersion != SchemaVersion)
        {
            throw new InvalidDataException($"NeoBabylon project registry schema version {document.SchemaVersion} is unsupported; this build supports version {SchemaVersion}. The saved record was preserved.");
        }

        if (!string.Equals(document.Product, "NeoBabylon", StringComparison.Ordinal)
            || document.Projects is null
            || document.Projects.Length == 0)
        {
            throw new InvalidDataException("NeoBabylon project registry schema or identity is not accepted.");
        }

        var projects = new List<WorkspaceProject>(document.Projects.Length);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in document.Projects)
        {
            if (project is null || string.IsNullOrWhiteSpace(project.Name))
            {
                throw new InvalidDataException("NeoBabylon project registry contains an unnamed project.");
            }

            var path = Normalize(project.WorkspacePath);
            if (!seen.Add(path))
            {
                throw new InvalidDataException("NeoBabylon project registry contains duplicate workspace paths.");
            }

            projects.Add(new WorkspaceProject(path, project.Name));
        }

        var selected = Normalize(document.SelectedWorkspace);
        if (!seen.Contains(selected))
        {
            throw new InvalidDataException("NeoBabylon project registry selected path is not in its project list.");
        }

        return new WorkspaceProjectState(selected, projects);
    }

    internal static void ValidateProtectedRecord(byte[] bytes) => _ = Parse(bytes);

    public WorkspaceProjectState AddAndSelect(string folder)
    {
        var result = PrepareAddAndSelect(folder);
        Save(result);
        return result;
    }

    public WorkspaceProjectState PrepareAddAndSelect(string folder)
    {
        var selected = RequireExistingFolder(folder);
        if (IsWithinOrEqual(_dataRoot, selected))
        {
            throw new InvalidOperationException("NeoBabylon application Data cannot be selected as a user project.");
        }

        var current = Read();
        var projects = current.Projects.ToList();
        if (!projects.Any(project => string.Equals(project.WorkspacePath, selected, StringComparison.OrdinalIgnoreCase)))
        {
            projects.Add(new WorkspaceProject(selected, new DirectoryInfo(selected).Name));
        }

        var result = new WorkspaceProjectState(selected, projects);
        return result;
    }

    public WorkspaceProjectState SelectExisting(string folder)
    {
        var result = PrepareSelectExisting(folder);
        Save(result);
        return result;
    }

    public WorkspaceProjectState PrepareSelectExisting(string folder)
    {
        var selected = Normalize(folder);
        var current = Read();
        if (!current.Projects.Any(project => string.Equals(project.WorkspacePath, selected, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("The requested workspace is not in the saved NeoBabylon project list. Use Add project to choose a folder.");
        }

        RequireExistingFolder(selected);
        return current with { SelectedWorkspace = selected };
    }

    public void Save(WorkspaceProjectState state) => Write(state);

    private void Write(WorkspaceProjectState state)
    {
        var document = new WorkspaceProjectDocument(SchemaVersion, "NeoBabylon", state.SelectedWorkspace, state.Projects.ToArray());
        var replacement = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(document, JsonOptions));
        ProtectedDataRecordFile.Replace(RegistryPath, replacement, value => Parse(value), value => Parse(value));
    }

    private static string RequireExistingFolder(string folder)
    {
        var path = Normalize(folder);
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"Selected NeoBabylon project folder is unavailable: {path}");
        return path;
    }

    private static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new InvalidDataException("NeoBabylon project workspace must be an absolute folder path.");
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static bool IsWithinOrEqual(string parent, string candidate) =>
        string.Equals(parent, candidate, StringComparison.OrdinalIgnoreCase)
        || candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private sealed record WorkspaceProjectDocument(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("product")] string Product,
        [property: JsonPropertyName("selectedWorkspace")] string SelectedWorkspace,
        [property: JsonPropertyName("projects")] WorkspaceProject[] Projects);
}
