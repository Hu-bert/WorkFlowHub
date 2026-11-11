using System.Text;
using FlowHub.Application.WorkItems;
using FlowHub.Domain.WorkItems;

namespace FlowHub.Api.Tests;

/// <summary>Flow od początku do pierwszego kroku wymagającego akceptacji — z prawdziwymi skryptami PowerShell.</summary>
public sealed class FlowRunTests(FlowHubApiFactory factory) : IClassFixture<FlowHubApiFactory>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task Started_flow_prepares_the_folder_repo_and_branch_then_waits_for_the_user()
    {
        using var client = factory.CreateClient();
        var created = await (await client.PostAsync("/api/work-items", new CreateWorkItemRequest(
            "RUN-1", "Zażółć fakturę", "Opis z polskimi znakami: żółć", "Bug", TaskSourceKind.Manual, null, ["Billing.Api"]))).ReadAsync<WorkItemDetails>();

        await client.PostAsync($"/api/work-items/{created.Summary.Id}/start", new { });
        var item = await WaitUntilAsync(client, created.Summary.Id, details => details.Summary.WaitsForUser);

        var scriptSteps = item.Steps.Where(step => step.Order <= 4).ToList();
        Assert.All(scriptSteps, step => Assert.True(step.Status == StepStatus.Succeeded, $"{step.StepId}: {step.Error}\n{step.Log}"));
        Assert.Equal("analyze-tech", item.Steps.First(step => step.Status == StepStatus.Proposed).StepId);

        var repoPath = item.Repos[0].LocalPath;
        Assert.True(Directory.Exists(Path.Combine(repoPath, ".git")));
        Assert.Equal("ref: refs/heads/feature/RUN-1-zazolc-fakture", File.ReadAllText(Path.Combine(repoPath, ".git", "HEAD")).Trim());
        Assert.Contains("żółć", File.ReadAllText(Path.Combine(item.WorkDirectory, "task.md"), Encoding.UTF8));
        Assert.Contains("Katalog zadania", scriptSteps[0].Log);
    }

    private static async Task<WorkItemDetails> WaitUntilAsync(HttpClient client, Guid id, Func<WorkItemDetails, bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;

        while (true)
        {
            var item = await client.GetAsync<WorkItemDetails>($"/api/work-items/{id}");
            if (condition(item))
            {
                return item;
            }

            if (DateTime.UtcNow > deadline)
            {
                var states = string.Join(", ", item.Steps.Select(step => $"{step.StepId}={step.Status}"));
                throw new TimeoutException($"Flow nie doszedł do oczekiwanego stanu: {states}");
            }

            await Task.Delay(250, TestContext.Current.CancellationToken);
        }
    }
}
