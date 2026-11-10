using FlowHub.Domain.Flows;

namespace FlowHub.Domain.WorkItems;

public enum TaskSourceKind
{
    /// <summary>Treść wpisana ręcznie (np. stara Jira bez API).</summary>
    Manual,

    /// <summary>Pobrane z trackera przez API.</summary>
    Tracker,
}

/// <summary>Zadanie programistyczne prowadzone przez flow. Korzeń agregatu: repo i kroki należą do niego.</summary>
public sealed class WorkItem
{
    private readonly List<TaskRepo> _repos = [];
    private readonly List<StepRun> _steps = [];

    private WorkItem()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Klucz z trackera, np. "PROJ-123".</summary>
    public string Key { get; private set; } = "";

    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";

    /// <summary>Typ zadania = klucz flow z YAML.</summary>
    public string Type { get; private set; } = "";

    public TaskSourceKind Source { get; private set; }
    public string? SourceUrl { get; private set; }

    /// <summary>Katalog zadania: work/&lt;KEY&gt;-&lt;slug&gt;.</summary>
    public string WorkDirectory { get; private set; } = "";

    /// <summary>Klucz kolumny na tablicy kanban.</summary>
    public string BoardColumn { get; private set; } = "";

    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<TaskRepo> Repos => _repos;
    public IReadOnlyList<StepRun> Steps => _steps;

    public IEnumerable<StepRun> OrderedSteps => _steps.OrderBy(step => step.Order);
    public StepRun? CurrentStep => OrderedSteps.FirstOrDefault(step => !step.IsFinished);
    public bool IsFinished => _steps.Count > 0 && CurrentStep is null;
    public TokenUsage Usage => _steps.Aggregate(TokenUsage.Empty, (sum, step) => sum.Add(step.Usage));

    public static WorkItem Create(NewWorkItem data, string workRoot, string boardColumn, DateTime now)
    {
        Require(data.Key, "Klucz zadania");
        Require(data.Title, "Tytuł zadania");
        Require(data.Type, "Typ zadania");

        return new WorkItem
        {
            Id = Guid.NewGuid(),
            Key = data.Key.Trim(),
            Title = data.Title.Trim(),
            Description = data.Description?.Trim() ?? "",
            Type = data.Type,
            Source = data.Source,
            SourceUrl = data.SourceUrl,
            WorkDirectory = Path.Combine(workRoot, WorkItemNaming.FolderName(data.Key.Trim(), data.Title)),
            BoardColumn = boardColumn,
            CreatedAt = now,
        };
    }

    /// <summary>Dodaje repo do zadania. Pierwsze repo jest główne, kolejne zależne. Ten sam branch we wszystkich.</summary>
    public TaskRepo AddRepo(string repositoryName)
    {
        if (_repos.Any(repo => repo.RepositoryName == repositoryName))
        {
            throw new DomainException($"Repo '{repositoryName}' jest już w zadaniu.");
        }

        var repo = TaskRepo.Create(
            Id,
            repositoryName,
            WorkItemNaming.BranchName(Key, Title),
            Path.Combine(WorkDirectory, repositoryName),
            _repos.Count == 0 ? RepoRole.Main : RepoRole.Dependent);

        _repos.Add(repo);
        return repo;
    }

    /// <summary>Tworzy kroki zadania z definicji flow. Krok „per repo” powstaje osobno dla każdego repo.</summary>
    public void PlanSteps(FlowDefinition flow)
    {
        if (_steps.Count > 0)
        {
            throw new DomainException("Kroki zadania są już zaplanowane.");
        }

        foreach (var definition in flow.Steps)
        {
            foreach (var repoName in RepoNamesFor(definition))
            {
                _steps.Add(StepRun.Create(Id, definition, _steps.Count + 1, repoName));
            }
        }
    }

    public void MoveTo(string boardColumn)
    {
        Require(boardColumn, "Kolumna tablicy");
        BoardColumn = boardColumn;
    }

    public StepRun GetStep(Guid stepRunId) =>
        _steps.FirstOrDefault(step => step.Id == stepRunId)
        ?? throw new DomainException("Nie ma takiego kroku w zadaniu.");

    private IEnumerable<string?> RepoNamesFor(StepDefinition definition) =>
        definition.PerRepo ? _repos.Select(repo => (string?)repo.RepositoryName) : [null];

    private static void Require(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{fieldName}: pole wymagane.");
        }
    }
}

/// <summary>Dane wejściowe nowego zadania.</summary>
public sealed record NewWorkItem(
    string Key,
    string Title,
    string? Description,
    string Type,
    TaskSourceKind Source,
    string? SourceUrl);
