using System.Text.Json;
using FlowHub.Application.Integrations;
using FlowHub.Domain.Repositories;
using FlowHub.Integrations.Http;

namespace FlowHub.Integrations.Tfs;

/// <summary>
/// Buildy odczytane z TFS zamiast z serwera CI: TeamCity (i każdy inny system) zapisuje wynik builda
/// jako „status” przy commicie i przy pull requeście — to, co widać w zakładce Status / w sekcji Pipelines PR.
/// Nie wymaga mapowania build configuration ani tokenu do TeamCity.
/// </summary>
public sealed class TfsStatusCiServer(TfsApi api) : ICiServer
{
    public async Task<IReadOnlyList<BuildStatus>> GetBuildsWithChangesAsync(CodeRepository repository, string branch, CancellationToken ct)
    {
        var repo = api.Locate(repository);

        if (TfsMapper.PullRequestIdFromBranch(branch) is { } pullRequestId)
        {
            using var onPullRequest = await api.GetAsync(
                repo.ProjectUrl, $"{repo.ApiPath}/pullRequests/{pullRequestId}/statuses", ct, api.PullRequestStatusesVersion);
            return onPullRequest is null ? [] : TfsStatusMapper.Builds(onPullRequest.RootElement, branch);
        }

        if (await HeadCommitAsync(repo, branch, ct) is not { } commit)
        {
            return [];
        }

        using var onCommit = await api.GetAsync(repo.ProjectUrl, $"{repo.ApiPath}/commits/{commit}/statuses?latestOnly=true", ct);
        return onCommit is null ? [] : TfsStatusMapper.Builds(onCommit.RootElement, branch);
    }

    public async Task<IReadOnlyList<CiChange>> GetChangesAsync(CodeRepository repository, string branch, CancellationToken ct)
    {
        if (TfsMapper.PullRequestIdFromBranch(branch) is not null)
        {
            return [];
        }

        var repo = api.Locate(repository);
        using var commits = await api.GetAsync(
            repo.ProjectUrl, $"{repo.ApiPath}/commits?searchCriteria.itemVersion.version={Uri.EscapeDataString(branch)}&$top=10", ct);

        return commits is null ? [] : TfsStatusMapper.Changes(commits.RootElement);
    }

    /// <summary>Statusy w TFS nie są przypisane do build configuration, więc nie ma czego podpowiadać.</summary>
    public Task<IReadOnlyList<CiConfiguration>> ListConfigurationsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CiConfiguration>>([]);

    /// <summary>Ostatni commit brancha. Filtr refs dopasowuje „zaczyna się od”, więc wybieramy dokładną nazwę.</summary>
    private async Task<string?> HeadCommitAsync(TfsRepository repo, string branch, CancellationToken ct)
    {
        using var refs = await api.GetAsync(repo.ProjectUrl, $"{repo.ApiPath}/refs?filter={Uri.EscapeDataString($"heads/{branch}")}", ct);

        return refs is null ? null : TfsStatusMapper.HeadCommit(refs.RootElement, branch);
    }
}

public static class TfsStatusMapper
{
    public static string? HeadCommit(JsonElement refs, string branch) =>
        Json.Items(refs, "value")
            .Where(reference => string.Equals(Json.Text(reference, "name"), $"refs/heads/{branch}", StringComparison.OrdinalIgnoreCase))
            .Select(reference => Json.Text(reference, "objectId"))
            .FirstOrDefault();

    /// <summary>Jeden build na każdy kontekst (genre/name) — najnowszy status wygrywa. „notApplicable” pomijamy.</summary>
    public static IReadOnlyList<BuildStatus> Builds(JsonElement statuses, string branch) =>
        Json.Items(statuses, "value")
            .Where(status => Json.Text(status, "state") is not (null or "notApplicable"))
            .GroupBy(Context)
            .Select(group => group.OrderByDescending(status => Json.Text(status, "creationDate")).First())
            .Select(status => new BuildStatus(
                Context(status),
                Number(status),
                State(Json.Text(status, "state")!),
                [],
                Json.Text(status, "targetUrl") ?? "",
                Json.Text(status, "description"),
                branch))
            .ToList();

    public static IReadOnlyList<CiChange> Changes(JsonElement commits) =>
        Json.Items(commits, "value")
            .Select(commit =>
            {
                var author = commit.TryGetProperty("author", out var value) ? value : default;
                var id = Json.Text(commit, "commitId") ?? "";

                return new CiChange(
                    id.Length > 8 ? id[..8] : id,
                    Json.Text(author, "name") ?? "?",
                    (Json.Text(commit, "comment") ?? "").Split('\n')[0],
                    DateTime.TryParse(Json.Text(author, "date"), out var date) ? date.ToUniversalTime() : default);
            })
            .ToList();

    private static string Context(JsonElement status)
    {
        var context = status.TryGetProperty("context", out var value) ? value : default;
        var name = Json.Text(context, "name") ?? "status";

        return Json.Text(context, "genre") is { Length: > 0 } genre ? $"{genre}/{name}" : name;
    }

    /// <summary>Numer builda bierzemy z adresu (ostatni segment z cyframi), bo status go nie ma w osobnym polu.</summary>
    private static string Number(JsonElement status)
    {
        var url = Json.Text(status, "targetUrl") ?? "";
        var segment = url.Split('/', '?', '=', '&', ':').LastOrDefault(part => part.Length > 0 && part.All(char.IsDigit));

        return segment ?? "?";
    }

    private static BuildState State(string state) => state.ToLowerInvariant() switch
    {
        "succeeded" => BuildState.Success,
        "failed" or "error" => BuildState.Failed,
        "pending" => BuildState.Running,
        _ => BuildState.Queued,
    };
}