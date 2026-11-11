using System.Collections.Concurrent;
using FlowHub.Application.Repositories;
using FlowHub.Application.WorkItems;
using FlowHub.Domain.Repositories;
using FlowHub.Domain.WorkItems;
using Microsoft.Extensions.Options;

namespace FlowHub.Application.Integrations;

/// <summary>Ustawienia odświeżania statusów (appsettings.json → "RepoStatus").</summary>
public sealed class RepoStatusOptions
{
    public const string SectionName = "RepoStatus";

    /// <summary>Co ile sekund odświeżać statusy aktywnych zadań w tle.</summary>
    public int RefreshSeconds { get; init; } = 120;

    /// <summary>Środowisko, od którego repo liczy się jako „gotowe” w podsumowaniu zadania.</summary>
    public string ReadyEnvironment { get; init; } = "DEV";

    /// <summary>Pokazywane środowiska, po przecinku (np. „DEV, TEST”). Puste = wszystkie z serwera wdrożeń.</summary>
    public string? Environments { get; init; }

    /// <summary>
    /// Nazwa brancha, na którym CI buduje pull request ({id} = numer PR), np. „pull/{id}/merge”.
    /// Buildy z tego brancha są doliczane do brancha zadania. Puste = nie szukaj.
    /// </summary>
    public string? PullRequestBuildBranch { get; init; } = "pull/{id}/merge";

    public IReadOnlyList<string> EnvironmentFilter =>
        (Environments ?? "").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>Pamięć ostatnich statusów — żeby nie odpytywać serwerów przy każdym otwarciu strony.</summary>
public sealed class RepoStatusCache
{
    private readonly ConcurrentDictionary<Guid, WorkItemRepoStatus> _statuses = new();

    public WorkItemRepoStatus? Find(Guid workItemId) => _statuses.GetValueOrDefault(workItemId);

    public void Store(WorkItemRepoStatus status) => _statuses[status.WorkItemId] = status;
}

/// <summary>Stan wszystkich repo zadania: testy lokalne + PR + buildy + wdrożenia.</summary>
public sealed class RepoStatusService(
    IWorkItemStore store,
    IRepositoryCatalog repositories,
    BranchStatusService branches,
    RepoStatusCache cache,
    IOptions<RepoStatusOptions> options,
    TimeProvider clock)
{
    /// <summary>Status z pamięci; z serwerów tylko gdy go brakuje albo użytkownik kliknął „Odśwież teraz”.</summary>
    public async Task<WorkItemRepoStatus?> GetAsync(Guid workItemId, bool refresh, CancellationToken ct)
    {
        if (!refresh && cache.Find(workItemId) is { } cached)
        {
            return cached;
        }

        var item = await store.FindAsync(workItemId, ct);
        return item is null ? null : await RefreshAsync(item, ct);
    }

    public async Task<WorkItemRepoStatus> RefreshAsync(WorkItem item, CancellationToken ct)
    {
        var repos = new List<RepoStatus>();
        foreach (var repo in item.Repos)
        {
            repos.Add(await ForRepoAsync(item, repo, ct));
        }

        var status = new WorkItemRepoStatus(
            item.Id,
            clock.GetUtcNow().UtcDateTime,
            RepoStatusBuilder.Summary(repos, options.Value.ReadyEnvironment),
            repos.Select(repo => repo.Level).DefaultIfEmpty(HealthLevel.Unknown).Max(),
            repos);

        cache.Store(status);
        return status;
    }

    private async Task<RepoStatus> ForRepoAsync(WorkItem item, TaskRepo repo, CancellationToken ct)
    {
        var tests = RepoStatusBuilder.Tests(TestStep(item, repo));

        if (repositories.Find(repo.RepositoryName) is not { } repository)
        {
            return Unknown(repo, tests);
        }

        var branch = await branches.GetAsync(repository, repo.Branch, ct);
        var known = BranchStatusService.Known([tests, branch.PullRequest, branch.Build, .. branch.Environments.Select(cell => cell.Status)]);

        return new RepoStatus(
            repo.RepositoryName, repo.Branch, repo.LocalPath,
            tests, branch.PullRequest, branch.Build, branch.Release, branch.Environments,
            RepoStatusBuilder.Worst(known), branch.PullRequestDetails, branch.Builds, branch.Changes, branch.Links);
    }

    /// <summary>Ostatni krok skryptowy „per repo” z testami (id kroku zawiera "test").</summary>
    private static StepRun? TestStep(WorkItem item, TaskRepo repo) =>
        item.OrderedSteps.LastOrDefault(step =>
            step.RepoName == repo.RepositoryName && step.StepId.Contains("test", StringComparison.OrdinalIgnoreCase));

    private static RepoStatus Unknown(TaskRepo repo, StatusCell tests)
    {
        var missing = new StatusCell(HealthLevel.Unknown, "Brak w repos.yaml");
        return new RepoStatus(repo.RepositoryName, repo.Branch, repo.LocalPath, tests, missing, missing, null, [], tests.Level, null, [], [], RepositoryLinks.None);
    }
}
