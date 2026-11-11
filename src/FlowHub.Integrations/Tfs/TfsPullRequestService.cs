using System.Text.Json;
using FlowHub.Application.Integrations;
using FlowHub.Domain;
using FlowHub.Domain.Repositories;

namespace FlowHub.Integrations.Tfs;

/// <summary>Pull request brancha zadania z TFS: stan, głosy, nierozwiązane wątki, polityki.</summary>
public sealed class TfsPullRequestService(TfsApi api) : IPullRequestService
{
    public async Task<PullRequestStatus?> GetForBranchAsync(CodeRepository repository, string branch, CancellationToken ct)
    {
        var repo = Uri.EscapeDataString(repository.Name);
        var source = Uri.EscapeDataString($"refs/heads/{branch}");

        using var list = await api.GetAsync(
            $"git/repositories/{repo}/pullrequests?searchCriteria.sourceRefName={source}&searchCriteria.status=all", ct);

        if (list is null || TfsMapper.NewestPullRequest(list.RootElement) is not { } pullRequest)
        {
            return null;
        }

        var id = pullRequest.GetProperty("pullRequestId").GetInt32();
        using var threads = await api.GetAsync($"git/repositories/{repo}/pullRequests/{id}/threads", ct);

        return TfsMapper.PullRequest(
            pullRequest,
            threads is null ? 0 : TfsMapper.UnresolvedThreads(threads.RootElement),
            await PoliciesAsync(pullRequest, id, ct),
            $"{api.ProjectUrl}/_git/{repo}/pullrequest/{id}");
    }

    /// <summary>Polityki brancha są w API „preview” — gdy serwer ich nie udostępnia, pokazujemy PR bez nich.</summary>
    private async Task<IReadOnlyList<PolicyCheck>> PoliciesAsync(JsonElement pullRequest, int id, CancellationToken ct)
    {
        if (TfsMapper.ProjectId(pullRequest) is not { } projectId)
        {
            return [];
        }

        try
        {
            var artifact = Uri.EscapeDataString($"vstfs:///CodeReview/CodeReviewId/{projectId}/{id}");
            using var evaluations = await api.GetAsync($"policy/evaluations?artifactId={artifact}", ct, apiVersion: "6.0-preview.1");
            return evaluations is null ? [] : TfsMapper.Policies(evaluations.RootElement);
        }
        catch (Exception error) when (error is DomainException or HttpRequestException)
        {
            return [];
        }
    }
}

/// <summary>Czyste mapowanie JSON z TFS na modele FlowHub — testowane na przykładowych odpowiedziach.</summary>
public static class TfsMapper
{
    public static JsonElement? NewestPullRequest(JsonElement list) =>
        Items(list).OrderByDescending(item => item.GetProperty("pullRequestId").GetInt32()).Cast<JsonElement?>().FirstOrDefault();

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
