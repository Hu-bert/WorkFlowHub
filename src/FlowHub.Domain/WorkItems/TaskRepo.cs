namespace FlowHub.Domain.WorkItems;

public enum RepoRole
{
    Main,
    Dependent,
}

/// <summary>Repo w zadaniu: branch zadania i lokalna ścieżka klonu.</summary>
public sealed class TaskRepo
{
    private TaskRepo()
    {
    }

    public Guid Id { get; private set; }
    public Guid WorkItemId { get; private set; }

    /// <summary>Nazwa z katalogu repo (repos.yaml).</summary>
    public string RepositoryName { get; private set; } = "";

    public string Branch { get; private set; } = "";
    public string LocalPath { get; private set; } = "";
    public RepoRole Role { get; private set; }

    public static TaskRepo Create(Guid workItemId, string repositoryName, string branch, string localPath, RepoRole role) => new()
    {
        Id = Guid.NewGuid(),
        WorkItemId = workItemId,
        RepositoryName = repositoryName,
        Branch = branch,
        LocalPath = localPath,
        Role = role,
    };
}
