using System.Text.Json;
using FlowHub.Application.Integrations;
using FlowHub.Domain;
using FlowHub.Domain.Repositories;

namespace FlowHub.Integrations.Tfs;

/// <summary>Pull request brancha z TFS: stan, głosy, nierozwiązane wątki, polityki.</summary>
public sealed class TfsPullRequestService(TfsApi api) : IPullRequestService
{
    public async Task<PullRequestStatus?> GetForBranchAsync(CodeRepository repository, string branch, CancellationToken ct)
    {
        var repo = api.Locate(repository);
        using var found = await FindAsync(repo, branch, ct);

        if (found is null || TfsMapper.NewestPullRequest(found.RootElement, repo.Name) is not { } pullRequest)
        {
            return null;
        }

        var id = pullRequest.GetProperty("pullRequestId").GetInt32();
        using var threads = await api.GetAsync(repo.ProjectUrl, $"{repo.ApiPath}/pullRequests/{id}/threads", ct);

        return TfsMapper.PullRequest(
            pullRequest,
            threads is null ? 0 : TfsMapper.UnresolvedThreads(threads.RootElement),
            await PoliciesAsync(repo, pullRequest, id, ct),
            $"{repo.WebUrl}/pullrequest/{id}");
    }

    /// <summary>
    /// Szukamy po branchu źródłowym w repo; gdy serwer nie zna repo po nazwie (404), pytamy cały projekt
    /// i wybieramy PR z tego repo. Branch „pull/123/merge” (tak CI nazywa build PR) to po prostu PR nr 123.
    /// </summary>
    private async Task<JsonDocument?> FindAsync(TfsRepository repo, string branch, CancellationToken ct)
    {
        if (TfsMapper.PullRequestIdFromBranch(branch) is { } id)
        {
            var single = await api.GetAsync(repo.ProjectUrl, $"{repo.ApiPath}/pullrequests/{id}", ct);
            return single is null ? null : TfsMapper.AsList(single);
        }

        var criteria = $"searchCriteria.sourceRefName={Uri.EscapeDataString($"refs/heads/{branch}")}&searchCriteria.status=all";

        return await api.GetAsync(repo.ProjectUrl, $"{repo.ApiPath}/pullrequests?{criteria}", ct)
            ?? await api.GetAsync(repo.ProjectUrl, $"git/pullrequests?{criteria}", ct);
    }

    /// <summary>Polityki brancha są w API „preview” — gdy serwer ich nie udostępnia, pokazujemy PR bez nich.</summary>
    private async Task<IReadOnlyList<PolicyCheck>> PoliciesAsync(TfsRepository repo, JsonElement pullRequest, int id, CancellationToken ct)
    {
        if (TfsMapper.ProjectId(pullRequest) is not { } projectId)
        {
            return [];
        }

        try
        {
            var artifact = Uri.EscapeDataString($"vstfs:///CodeReview/CodeReviewId/{projectId}/{id}");
            using var evaluations = await api.GetAsync(repo.ProjectUrl, $"policy/evaluations?artifactId={artifact}", ct, api.PreviewVersion);
            return evaluations is null ? [] : TfsMapper.Policies(evaluations.RootElement);
        }
        catch (DomainException)
        {
            return [];
        }
    }
}

/// <summary>Czyste mapowanie JSON z TFS na modele FlowHub — testowane na przykładowych odpowiedziach.</summary>
public static class TfsMapper
{
    /// <summary>Najnowszy PR z listy; repositoryName zawęża wynik zapytania o cały projekt do jednego repo.</summary>
    public static JsonElement? NewestPullRequest(JsonElement list, string? repositoryName = null) =>
        Items(list)
            .Where(item => repositoryName is null || RepositoryName(item) is not { } name || string.Equals(name, repositoryName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.GetProperty("pullRequestId").GetInt32())
            .Cast<JsonElement?>()
            .FirstOrDefault();

    /// <summary>„pull/36816/merge” albo „refs/pull/36816/merge” → 36816.</summary>
    public static int? PullRequestIdFromBranch(string branch)
    {
        var parts = branch.Replace("refs/", "", StringComparison.OrdinalIgnoreCase).Split('/');
        return parts.Length >= 2 && parts[0].Equals("pull", StringComparison.OrdinalIgnoreCase) && int.TryParse(parts[1], out var id) ? id : null;
    }

    /// <summary>Pojedynczy PR opakowany jak lista — żeby dalej obsługiwać go tak samo.</summary>
    public static JsonDocument AsList(JsonDocument single)
    {
        using (single)
        {
            return JsonDocument.Parse($"{{\"value\":[{single.RootElement.GetRawText()}]}}");
        }
    }

    public static PullRequestStatus PullRequest(JsonElement pullRequest, int unresolvedThreads, IReadOnlyList<PolicyCheck> policies, string url) => new(
        pullRequest.GetProperty("pullRequestId").GetInt32(),
        Text(pullRequest, "title") ?? "",
        State(pullRequest),
        Items(pullRequest, "reviewers").Select(Reviewer).ToList(),
        unresolvedThreads,
        policies,
        string.Equals(Text(pullRequest, "mergeStatus"), "conflicts", StringComparison.OrdinalIgnoreCase),
        url);

    /// <summary>Wątki otwarte: status "active" lub "pending" (wątki systemowe nie mają statusu).</summary>
    public static int UnresolvedThreads(JsonElement threads) => Items(threads).Count(thread =>
        Text(thread, "status") is "active" or "pending"
        && !(thread.TryGetProperty("isDeleted", out var deleted) && deleted.ValueKind == JsonValueKind.True));

    public static IReadOnlyList<PolicyCheck> Policies(JsonElement evaluations) => Items(evaluations)
        .Where(evaluation => Text(evaluation, "status") is not (null or "notApplicable"))
        .Select(evaluation => new PolicyCheck(PolicyName(evaluation), Text(evaluation, "status") == "approved"))
        .ToList();

    public static string? ProjectId(JsonElement pullRequest) =>
        pullRequest.TryGetProperty("repository", out var repository) && repository.TryGetProperty("project", out var project)
            ? Text(project, "id")
            : null;

    private static string? RepositoryName(JsonElement pullRequest) =>
        pullRequest.TryGetProperty("repository", out var repository) ? Text(repository, "name") : null;

    private static PullRequestState State(JsonElement pullRequest) => Text(pullRequest, "status")?.ToLowerInvariant() switch
    {
        "completed" => PullRequestState.Completed,
        "abandoned" => PullRequestState.Abandoned,
        _ when pullRequest.TryGetProperty("isDraft", out var draft) && draft.ValueKind == JsonValueKind.True => PullRequestState.Draft,
        _ => PullRequestState.Active,
    };

    /// <summary>Głosy TFS: 10 = approved, 5 = approved with suggestions, 0 = brak, -5 = waiting for author, -10 = rejected.</summary>
    private static Reviewer Reviewer(JsonElement reviewer)
    {
        var vote = reviewer.TryGetProperty("vote", out var value) && value.TryGetInt32(out var number) ? number : 0;

        return new Reviewer(Text(reviewer, "displayName") ?? "?", vote switch
        {
            10 => ReviewerVote.Approved,
            5 => ReviewerVote.ApprovedWithSuggestions,
            -10 => ReviewerVote.Rejected,
            _ => ReviewerVote.Waiting,
        });
    }

    private static string PolicyName(JsonElement evaluation) =>
        evaluation.TryGetProperty("configuration", out var configuration)
        && configuration.TryGetProperty("type", out var type)
        && Text(type, "displayName") is { } name
            ? name
            : "Polityka";

    /// <summary>Listy w TFS są opakowane: { "count": n, "value": [...] }.</summary>
    public static IEnumerable<JsonElement> Items(JsonElement element, string property = "value") =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray()
            : [];

    public static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}