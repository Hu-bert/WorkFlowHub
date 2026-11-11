using FlowHub.Domain.Repositories;

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
    IReadOnlyList<CiChange> Changes);

/// <summary>
/// Odpytuje TFS, TeamCity i Octopus o branch repo — wspólne dla tabeli repo w zadaniu
/// i dla lokalnych kopii na stronie Repozytoria.
/// </summary>
public sealed class BranchStatusService(IPullRequestService pullRequests, ICiServer ci, IDeployServer deploy)
{
    public async Task<BranchStatus> GetAsync(CodeRepository repository, string branch, CancellationToken ct)
    {
        var pullRequest = await pullRequests.GetForBranchAsync(repository, branch, ct);
        var builds = await ci.GetBuildsWithChangesAsync(repository, branch, ct);
        var changes = await ci.GetChangesAsync(repository, branch, ct);
        var deployment = await deploy.GetOverviewAsync(repository, branch, ct);

        var pullRequestCell = RepoStatusBuilder.PullRequest(pullRequest);
        var buildCell = RepoStatusBuilder.Build(builds);
        var environments = deployment?.Environments.Select(RepoStatusBuilder.Environment).ToList() ?? [];

        return new BranchStatus(
            pullRequestCell, buildCell, deployment?.Release, environments,
            RepoStatusBuilder.Worst(Known([pullRequestCell, buildCell, .. environments.Select(cell => cell.Status)])),
            pullRequest, builds, changes);
    }

    /// <summary>„Brak wdrożenia” ani „brak PR” to jeszcze nie problem — do poziomu liczą się tylko znane stany.</summary>
    public static IEnumerable<StatusCell> Known(IEnumerable<StatusCell> cells) =>
        cells.Where(cell => cell.Level != HealthLevel.Unknown);
}
