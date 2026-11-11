using FlowHub.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace FlowHub.Application.Integrations;

/// <summary>Stan jednego brancha repo w systemach firmowych: PR, buildy, release i wdrożenia.</summary>
public sealed record BranchStatus(
    StatusCell PullRequest,
    StatusCell Build,
    string? Release,
    IReadOnlyList<EnvironmentCell> Environments,
    HealthLevel Level,
    PullRequestStatus? PullRequestDetails,
    IReadOnlyList<BuildStatus> Builds,
    IReadOnlyList<CiChange> Changes,
    RepositoryLinks Links);

/// <summary>
/// Odpytuje TFS, TeamCity i Octopus o branch repo — wspólne dla tabeli repo w zadaniu
/// i dla lokalnych kopii na stronie Repozytoria.
/// </summary>
public sealed class BranchStatusService(
    IPullRequestService pullRequests,
    ICiServer ci,
    IDeployServer deploy,
    IRepositoryLinks links,
    IOptions<RepoStatusOptions> options)
{
    public async Task<BranchStatus> GetAsync(CodeRepository repository, string branch, CancellationToken ct)
    {
        var pullRequest = await pullRequests.GetForBranchAsync(repository, branch, ct);
        var builds = await BuildsAsync(repository, branch, pullRequest, ct);
        var changes = await ci.GetChangesAsync(repository, branch, ct);
        var deployment = await deploy.GetOverviewAsync(repository, branch, builds, ct);

        var pullRequestCell = RepoStatusBuilder.PullRequest(pullRequest);
        var buildCell = RepoStatusBuilder.Build(builds);
        var environments = Visible(deployment?.Environments ?? []).Select(RepoStatusBuilder.Environment).ToList();

        return new BranchStatus(
            pullRequestCell, buildCell, deployment?.Release, environments,
            RepoStatusBuilder.Worst(Known([pullRequestCell, buildCell, .. environments.Select(cell => cell.Status)])),
            pullRequest, builds, changes, links.For(repository, branch));
    }

    /// <summary>„Brak wdrożenia” ani „brak PR” to jeszcze nie problem — do poziomu liczą się tylko znane stany.</summary>
    public static IEnumerable<StatusCell> Known(IEnumerable<StatusCell> cells) =>
        cells.Where(cell => cell.Level != HealthLevel.Unknown);

    /// <summary>
    /// Buildy brancha plus buildy pull requesta: CI często buduje PR na osobnym branchu (np. pull/123/merge),
    /// więc bez tego build „z moimi zmianami” byłby niewidoczny.
    /// </summary>
    private async Task<IReadOnlyList<BuildStatus>> BuildsAsync(CodeRepository repository, string branch, PullRequestStatus? pullRequest, CancellationToken ct)
    {
        var builds = (await ci.GetBuildsWithChangesAsync(repository, branch, ct)).ToList();

        if (pullRequest is not null && options.Value.PullRequestBuildBranch is { Length: > 0 } pattern)
        {
            var pullRequestBranch = pattern.Replace("{id}", pullRequest.Id.ToString());
            if (!string.Equals(pullRequestBranch, branch, StringComparison.OrdinalIgnoreCase))
            {
                builds.AddRange(await ci.GetBuildsWithChangesAsync(repository, pullRequestBranch, ct));
            }
        }

        return builds.DistinctBy(build => (build.Configuration, build.Number)).ToList();
    }

    /// <summary>Tylko środowiska wybrane w Ustawieniach (gdy lista jest pusta — wszystkie).</summary>
    private IEnumerable<EnvironmentDeployment> Visible(IReadOnlyList<EnvironmentDeployment> environments)
    {
        var filter = options.Value.EnvironmentFilter;

        return filter.Count == 0
            ? environments
            : environments.Where(environment => filter.Contains(environment.Environment, StringComparer.OrdinalIgnoreCase));
    }
}