using FlowHub.Domain.WorkItems;

namespace FlowHub.Application.Integrations;

/// <summary>Poziom statusu. Kolejność ma znaczenie: większa wartość = gorzej („najgorszy status wygrywa”).</summary>
public enum HealthLevel
{
    Ok = 0,
    Unknown = 1,
    Pending = 2,
    Warning = 3,
    Failed = 4,
}

/// <summary>Jedna komórka tabeli repo: poziom (kolor + ikona), tekst i link.</summary>
public sealed record StatusCell(HealthLevel Level, string Text, string? Url = null);

public sealed record EnvironmentCell(string Environment, StatusCell Status);

/// <summary>Stan jednego repo w zadaniu — wiersz tabeli + szczegóły do rozwinięcia.</summary>
public sealed record RepoStatus(
    string Repo,
    string Branch,
    string LocalPath,
    StatusCell Tests,
    StatusCell PullRequest,
    StatusCell Build,
    string? Release,
    IReadOnlyList<EnvironmentCell> Environments,
    HealthLevel Level,
    PullRequestStatus? PullRequestDetails,
    IReadOnlyList<BuildStatus> Builds,
    IReadOnlyList<CiChange> Changes);

public sealed record WorkItemRepoStatus(
    Guid WorkItemId,
    DateTime CheckedAt,
    string Summary,
    HealthLevel Level,
    IReadOnlyList<RepoStatus> Repos);

/// <summary>Zamienia surowe dane z integracji na komórki tabeli. Czyste funkcje — bez I/O.</summary>
public static class RepoStatusBuilder
{
    public static StatusCell Tests(StepRun? testStep) => testStep?.Status switch
    {
        StepStatus.Succeeded => new StatusCell(HealthLevel.Ok, "Przeszły"),
        StepStatus.Failed => new StatusCell(HealthLevel.Failed, "Nie przeszły"),
        StepStatus.Running or StepStatus.Approved => new StatusCell(HealthLevel.Pending, "W trakcie"),
        StepStatus.Skipped => new StatusCell(HealthLevel.Warning, "Pominięte"),
        _ => new StatusCell(HealthLevel.Unknown, "Nie uruchomiono"),
    };

    public static StatusCell PullRequest(PullRequestStatus? pr)
    {
        if (pr is null)
        {
            return new StatusCell(HealthLevel.Unknown, "Brak PR");
        }

        var approved = pr.Reviewers.Count(reviewer => reviewer.Vote is ReviewerVote.Approved or ReviewerVote.ApprovedWithSuggestions);
        var votes = $"{approved}/{pr.Reviewers.Count} akcept.";

        return pr switch
        {
            { State: PullRequestState.Completed } => new StatusCell(HealthLevel.Ok, "Completed", pr.Url),
            { State: PullRequestState.Abandoned } => new StatusCell(HealthLevel.Failed, "Abandoned", pr.Url),
            { HasConflicts: true } => new StatusCell(HealthLevel.Failed, "Konflikty scalania", pr.Url),
            _ when pr.Reviewers.Any(reviewer => reviewer.Vote == ReviewerVote.Rejected) => new StatusCell(HealthLevel.Failed, $"Odrzucony · {votes}", pr.Url),
            { State: PullRequestState.Draft } => new StatusCell(HealthLevel.Pending, $"Draft · {votes}", pr.Url),
            _ when pr.UnresolvedComments > 0 => new StatusCell(HealthLevel.Warning, $"{votes} · {pr.UnresolvedComments} koment.", pr.Url),
            _ when pr.Policies.Any(policy => !policy.Passed) => new StatusCell(HealthLevel.Pending, $"{votes} · polityki", pr.Url),
            _ => new StatusCell(HealthLevel.Pending, votes, pr.Url),
        };
    }

    /// <summary>Wiele buildów → pokazujemy najgorszy.</summary>
    public static StatusCell Build(IReadOnlyList<BuildStatus> builds)
    {
        if (builds.Count == 0)
        {
            return new StatusCell(HealthLevel.Unknown, "Brak buildu");
        }

        var worst = builds.MaxBy(build => (int)Level(build.State))!;
        var text = worst.State switch
        {
            BuildState.Failed when worst.FailedTests.Count > 0 => $"#{worst.Number} · {worst.FailedTests.Count} testów",
            BuildState.Failed => $"#{worst.Number} · błąd",
            BuildState.Running => $"#{worst.Number} · trwa",
            BuildState.Queued => $"#{worst.Number} · w kolejce",
            _ => $"#{worst.Number}",
        };

        return new StatusCell(Level(worst.State), text, worst.Url);
    }

    public static EnvironmentCell Environment(EnvironmentDeployment deployment) => new(
        deployment.Environment,
        deployment.State switch
        {
            DeploymentState.Success => new StatusCell(HealthLevel.Ok, deployment.Version ?? "OK", deployment.Url),
            DeploymentState.Failed => new StatusCell(HealthLevel.Failed, "Błąd wdrożenia", deployment.Url),
            DeploymentState.Executing => new StatusCell(HealthLevel.Pending, "Wdrażanie", deployment.Url),
            DeploymentState.Queued => new StatusCell(HealthLevel.Pending, "W kolejce", deployment.Url),
            _ => new StatusCell(HealthLevel.Unknown, "—"),
        });

    /// <summary>Poziom repo = najgorszy z testów, PR, buildu i wdrożeń. „Nie wdrożono jeszcze” nie jest problemem.</summary>
    public static HealthLevel Worst(IEnumerable<StatusCell> cells) =>
        cells.Select(cell => cell.Level).DefaultIfEmpty(HealthLevel.Unknown).Max();

    public static string Summary(IReadOnlyList<RepoStatus> repos, string readyEnvironment)
    {
        var ready = repos.Count(repo => repo.Environments.Any(cell =>
            string.Equals(cell.Environment, readyEnvironment, StringComparison.OrdinalIgnoreCase) && cell.Status.Level == HealthLevel.Ok));

        return $"{ready} z {repos.Count} repo gotowe na {readyEnvironment}";
    }

    private static HealthLevel Level(BuildState state) => state switch
    {
        BuildState.Success => HealthLevel.Ok,
        BuildState.Failed => HealthLevel.Failed,
        _ => HealthLevel.Pending,
    };
}
