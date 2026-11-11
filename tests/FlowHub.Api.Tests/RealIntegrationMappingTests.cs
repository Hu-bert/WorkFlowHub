using System.Text.Json;
using FlowHub.Application.Integrations;
using FlowHub.Integrations.Jira;
using FlowHub.Integrations.Tfs;

namespace FlowHub.Api.Tests;

/// <summary>
/// Mapowanie odpowiedzi TFS i Jiry. Przykłady JSON są wg dokumentacji REST API —
/// NIE pochodzą z prawdziwego serwera (brak dostępu), więc w pracy warto je porównać.
/// </summary>
public sealed class RealIntegrationMappingTests
{
    private const string PullRequests = """
        { "count": 2, "value": [
          { "pullRequestId": 41, "title": "Stary", "status": "abandoned" },
          { "pullRequestId": 57, "title": "Poprawka VAT", "status": "active", "isDraft": false, "mergeStatus": "conflicts",
            "repository": { "name": "Billing.Api", "project": { "id": "6ce954b1-ce1f-45d1-b94d-e6bf2464ba2c" } },
            "reviewers": [
              { "displayName": "Anna Nowak", "vote": 10 },
              { "displayName": "Piotr Zieliński", "vote": 5 },
              { "displayName": "Ewa Kowalska", "vote": 0 },
              { "displayName": "Jan Lis", "vote": -10 }
            ] }
        ] }
        """;

    [Fact]
    public void Newest_pull_request_of_the_branch_is_mapped_with_votes_and_conflicts()
    {
        using var list = JsonDocument.Parse(PullRequests);
        var newest = TfsMapper.NewestPullRequest(list.RootElement)!.Value;

        var pr = TfsMapper.PullRequest(newest, 2, [new PolicyCheck("Build", true)], "https://tfs/pr/57");

        Assert.Equal((57, "Poprawka VAT", PullRequestState.Active, true, 2), (pr.Id, pr.Title, pr.State, pr.HasConflicts, pr.UnresolvedComments));
        Assert.Equal(
            [ReviewerVote.Approved, ReviewerVote.ApprovedWithSuggestions, ReviewerVote.Waiting, ReviewerVote.Rejected],
            pr.Reviewers.Select(reviewer => reviewer.Vote));
        Assert.Equal("6ce954b1-ce1f-45d1-b94d-e6bf2464ba2c", TfsMapper.ProjectId(newest));
    }

    [Theory]
    [InlineData("""{ "pullRequestId": 1, "status": "completed" }""", PullRequestState.Completed)]
    [InlineData("""{ "pullRequestId": 1, "status": "active", "isDraft": true }""", PullRequestState.Draft)]
    public void Pull_request_state_is_recognised(string json, PullRequestState expected)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Equal(expected, TfsMapper.PullRequest(document.RootElement, 0, [], "").State);
    }

    [Fact]
    public void Only_open_comment_threads_count_as_unresolved()
    {
        using var threads = JsonDocument.Parse("""
            { "value": [
              { "status": "active" }, { "status": "pending" }, { "status": "fixed" }, { "status": "closed" },
              { "status": "active", "isDeleted": true }, { "comments": [] }
            ] }
            """);

        Assert.Equal(2, TfsMapper.UnresolvedThreads(threads.RootElement));
    }

    [Fact]
    public void Policies_show_name_and_whether_they_passed()
    {
        using var evaluations = JsonDocument.Parse("""
            { "value": [
              { "status": "approved", "configuration": { "type": { "displayName": "Build" } } },
              { "status": "rejected", "configuration": { "type": { "displayName": "Minimum number of reviewers" } } },
              { "status": "notApplicable", "configuration": { "type": { "displayName": "Work item linking" } } }
            ] }
            """);

        Assert.Equal(
            [new PolicyCheck("Build", true), new PolicyCheck("Minimum number of reviewers", false)],
            TfsMapper.Policies(evaluations.RootElement));
    }

    [Fact]
    public void Repositories_from_tfs_skip_disabled_ones_and_strip_the_ref_prefix()
    {
        using var repositories = JsonDocument.Parse("""
            { "value": [
              { "name": "Shared.Lib", "remoteUrl": "https://tfs/col/P/_git/Shared.Lib", "webUrl": "https://tfs/web", "defaultBranch": "refs/heads/develop" },
              { "name": "Billing.Api", "remoteUrl": "https://tfs/col/P/_git/Billing.Api" },
              { "name": "Stare", "remoteUrl": "https://tfs/col/P/_git/Stare", "isDisabled": true }
            ] }
            """);

        var list = TfsRepositoryDirectory.Map(repositories.RootElement);

        Assert.Equal(["Billing.Api", "Shared.Lib"], list.Select(repository => repository.Name));
        Assert.Equal(["main", "develop"], list.Select(repository => repository.DefaultBranch));
    }

    [Fact]
    public void Jira_issue_becomes_task_details_with_a_browse_link()
    {
        using var issue = JsonDocument.Parse("""{ "key": "PAY-12", "fields": { "summary": "Zła stawka VAT", "description": "Kroki…" } }""");

        var task = JiraTaskSource.Map(issue.RootElement, "pay-12", "https://jira.firma.pl");

        Assert.Equal(new TaskDetails("PAY-12", "Zła stawka VAT", "Kroki…", "https://jira.firma.pl/browse/PAY-12"), task);
    }
}