using System.Net;
using FlowHub.Api.Endpoints;
using FlowHub.Application.Git;
using FlowHub.Application.Integrations;
using FlowHub.Application.WorkItems;
using FlowHub.Domain.WorkItems;
using FlowHub.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowHub.Api.Tests;

/// <summary>Integracje Fake: tabela repo (PR, build, wdrożenia), tracker i diff.</summary>
public sealed class IntegrationEndpointTests(FlowHubApiFactory factory) : IClassFixture<FlowHubApiFactory>
{
    private static async Task<WorkItemDetails> CreateAsync(HttpClient client, string key, params string[] repositories) =>
        await (await client.PostAsync("/api/work-items", new CreateWorkItemRequest(
            key, "Tabela repo", null, "Technical", TaskSourceKind.Manual, null, repositories))).ReadAsync<WorkItemDetails>();

    [Fact]
    public async Task Repo_table_shows_each_repo_and_the_worst_status_wins()
    {
        using var client = factory.CreateClient();
        var item = await CreateAsync(client, "INT-1", "Billing.Api", "Shared.Lib", "Customer.Portal");

        var status = await client.GetAsync<WorkItemRepoStatus>($"/api/work-items/{item.Summary.Id}/repo-status");
        var levels = await client.GetAsync<Dictionary<Guid, List<RepoLevel>>>("/api/repo-status/levels");

        Assert.Equal("2 z 3 repo gotowe na DEV", status.Summary);
        Assert.Equal(HealthLevel.Failed, status.Level);

        var billing = status.Repos.Single(repo => repo.Repo == "Billing.Api");
        Assert.Equal("2/3 akcept. · polityki", billing.PullRequest.Text);
        Assert.Equal(HealthLevel.Ok, billing.Build.Level);
        Assert.Equal(["DEV", "TEST", "UAT", "PROD"], billing.Environments.Select(cell => cell.Environment));
        Assert.Equal(HealthLevel.Pending, billing.Level);
        Assert.NotNull(billing.Release);

        Assert.Equal(HealthLevel.Ok, status.Repos.Single(repo => repo.Repo == "Shared.Lib").Level);

        var portal = status.Repos.Single(repo => repo.Repo == "Customer.Portal");
        Assert.Equal("Konflikty scalania", portal.PullRequest.Text);
        Assert.Equal(2, portal.Builds.Single(build => build.State == BuildState.Failed).FailedTests.Count);
        Assert.Equal(HealthLevel.Failed, portal.Level);

        Assert.Equal(3, levels[item.Summary.Id].Count);
    }

    [Fact]
    public async Task Tracker_returns_task_details_for_a_key()
    {
        using var client = factory.CreateClient();

        var task = await client.GetAsync<TaskDetails>("/api/task-source/PROJ-55");

        Assert.Equal("PROJ-55", task.Key);
        Assert.Contains("stawka VAT", task.Title);
        Assert.EndsWith("/PROJ-55", task.Url);
    }

    [Fact]
    public async Task Diff_of_a_repo_that_is_not_cloned_yet_is_a_clear_error()
    {
        using var client = factory.CreateClient();
        var item = await CreateAsync(client, "INT-2", "Billing.Api");

        var response = await client.GetAsync($"/api/work-items/{item.Summary.Id}/repos/Billing.Api/diff", TestContext.Current.CancellationToken);
        var unknownRepo = await client.GetAsync($"/api/work-items/{item.Summary.Id}/repos/Nie.Ma/diff", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("nie jest jeszcze sklonowane", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.NotFound, unknownRepo.StatusCode);
    }

    [Fact]
    public void Unknown_integration_name_fails_at_startup_with_the_list_of_allowed_ones()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Integrations:Ci"] = "Jenkins" })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddIntegrations(configuration));

        Assert.Contains("Integrations:Ci = 'Jenkins'", error.Message);
        Assert.Contains("Fake", error.Message);
    }

    [Fact]
    public async Task Diff_shows_changes_made_in_the_task_repo()
    {
        using var client = factory.CreateClient();
        var item = await CreateAsync(client, "INT-3", "Shared.Lib");
        await client.PostAsync($"/api/work-items/{item.Summary.Id}/start", new { });
        var repoPath = item.Repos[0].LocalPath;
        await WaitForAsync(() => File.Exists(Path.Combine(repoPath, ".github", "agents", "retro.agent.md")));

        File.AppendAllText(Path.Combine(repoPath, "README.md"), "\nNowa linia w README\n");
        File.WriteAllText(Path.Combine(repoPath, "nowy-plik.txt"), "x");

        var diff = await client.GetAsync<RepoDiff>($"/api/work-items/{item.Summary.Id}/repos/Shared.Lib/diff");

        Assert.Contains("+Nowa linia w README", diff.Patch);
        Assert.Contains("README.md", diff.Stat);
        Assert.Equal(["nowy-plik.txt"], diff.UntrackedFiles); // bloczki FlowHub są w .git/info/exclude
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 240 && !condition(); attempt++)
        {
            await Task.Delay(250, TestContext.Current.CancellationToken);
        }

        Assert.True(condition(), "Nie doczekałem się przygotowania repo.");
    }
}
