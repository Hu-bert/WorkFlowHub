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
    IPullRequestService pullRequests,
    ICiServer ci,
    IDeployServer deploy,
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

        var pullRequest = await pullRequests.GetForBranchAsync(repository, repo.Branch, ct);
        var builds = await ci.GetBuildsWithChangesAsync(repository, repo.Branch, ct);
        var changes = await ci.GetChangesAsync(repository, repo.Branch, ct);
        var deployment = await deploy.GetOverviewAsync(repository, repo.Branch, ct);

        var pullRequestCell = RepoStatusBuilder.PullRequest(pullRequest);
        var buildCell = RepoStatusBuilder.Build(builds);
        var environments = deployment?.Environments.Select(RepoStatusBuilder.Environment).ToList() ?? [];

        // „Brak wdrożenia” ani „brak PR” to jeszcze nie problem — do poziomu repo liczą się tylko znane stany.
        var known = new[] { tests, pullRequestCell, buildCell }
            .Concat(environments.Select(cell => cell.Status))
            .Where(cell => cell.Level != HealthLevel.Unknown);

        return new RepoStatus(
            repo.RepositoryName, repo.Branch, repo.LocalPath,
            tests, pullRequestCell, buildCell, deployment?.Release, environments,
            RepoStatusBuilder.Worst(known), pullRequest, builds, changes);
    }

    /// <summary>Ostatni krok skryptowy „per repo” z testami (id kroku zawiera "test").</summary>
    private static StepRun? TestStep(WorkItem item, TaskRepo repo) =>
        item.OrderedSteps.LastOrDefault(step =>
            step.RepoName == repo.RepositoryName && step.StepId.Contains("test", StringComparison.OrdinalIgnoreCase));

    private static RepoStatus Unknown(TaskRepo repo, StatusCell tests)
    {
        var missing = new StatusCell(HealthLevel.Unknown, "Brak w repos.yaml");
        return new RepoStatus(repo.RepositoryName, repo.Branch, repo.LocalPath, tests, missing, missing, null, [], tests.Level, null, [], []);
    }
}
