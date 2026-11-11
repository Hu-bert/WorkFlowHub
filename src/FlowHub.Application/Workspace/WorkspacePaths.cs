using Microsoft.Extensions.Options;

namespace FlowHub.Application.Workspace;

/// <summary>Gdzie leżą pliki FlowHub (appsettings.json → "Workspace").</summary>
public sealed class WorkspaceOptions
{
    public const string SectionName = "Workspace";

    /// <summary>Katalog główny z flows/, blocks/ i repos.yaml.</summary>
    public string Root { get; set; } = ".";

    /// <summary>Katalog na zadania. Puste = &lt;Root&gt;/work.</summary>
    public string? WorkDirectory { get; set; }
}

/// <summary>Jedyne miejsce, które zna układ katalogów FlowHub.</summary>
public sealed class WorkspacePaths(IOptions<WorkspaceOptions> options)
{
    public string Root { get; } = Path.GetFullPath(options.Value.Root);

    public string FlowsDirectory => Path.Combine(Root, "flows");
    public string BlocksDirectory => Path.Combine(Root, "blocks");
    public string RepositoriesFile => Path.Combine(Root, "repos.yaml");

    /// <summary>Zapamiętane mapy repozytoriów (lokalnie, poza gitem).</summary>
    public string KnowledgeDirectory => Path.Combine(Root, "knowledge");

    public string WorkDirectory { get; } = Path.GetFullPath(
        string.IsNullOrWhiteSpace(options.Value.WorkDirectory)
            ? Path.Combine(options.Value.Root, "work")
            : options.Value.WorkDirectory);
}
