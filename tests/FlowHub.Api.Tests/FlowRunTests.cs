using System.Text;
using FlowHub.Api.Endpoints;
using FlowHub.Application.Agents;
using FlowHub.Application.WorkItems;
using FlowHub.Domain.Flows;
using FlowHub.Domain.WorkItems;

namespace FlowHub.Api.Tests;

/// <summary>Flow na prawdziwych plikach z repo: skrypty PowerShell + agent Fake (bez Copilota).</summary>
public sealed class FlowRunTests(FlowHubApiFactory factory) : IClassFixture<FlowHubApiFactory>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    [Fact]
    public async Task Started_flow_prepares_the_folder_repo_and_branch_then_waits_for_the_user()
    {
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "RUN-1", "Zażółć fakturę", "Bug");

        await client.PostAsync($"/api/work-items/{created.Summary.Id}/start", new { });
        var item = await WaitUntilAsync(client, created.Summary.Id, details => details.Summary.WaitsForUser);

        var scriptSteps = item.Steps.Where(step => step.Order <= 4).ToList();
        Assert.All(scriptSteps, step => Assert.True(step.Status == StepStatus.Succeeded, $"{step.StepId}: {step.Error}\n{step.Log}"));

        var proposed = item.Steps.First(step => step.Status == StepStatus.Proposed);
        Assert.Equal("analyze-tech", proposed.StepId);
        Assert.Contains("Przeanalizuj repozytorium **Billing.Api**", proposed.Proposal);
        Assert.Contains("context-Billing.Api.md", proposed.Proposal);

        var repoPath = item.Repos[0].LocalPath;
        Assert.Equal("ref: refs/heads/feature/RUN-1-zazolc-fakture", File.ReadAllText(Path.Combine(repoPath, ".git", "HEAD")).Trim());
        Assert.Contains("żółć", File.ReadAllText(Path.Combine(item.WorkDirectory, "task.md"), Encoding.UTF8));
        Assert.True(File.Exists(Path.Combine(repoPath, ".github", "agents", "planowanie.agent.md")));
        Assert.True(File.Exists(Path.Combine(repoPath, ".github", "skills", "retro", "SKILL.md")));
        Assert.Contains("/.github/agents/planowanie.agent.md", File.ReadAllText(Path.Combine(repoPath, ".git", "info", "exclude")));
    }

    [Fact]
    public async Task Whole_bug_flow_runs_to_the_end_with_the_fake_agent()
    {
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "RUN-2", "Pełny przebieg", "Bug");
        var id = created.Summary.Id;
        await client.PostAsync($"/api/work-items/{id}/start", new { });

        var item = await DriveToTheEndAsync(client, id);

        Assert.All(item.Steps, step => Assert.True(step.Status == StepStatus.Succeeded, $"{step.StepId}: {step.Status} {step.Error}"));
        Assert.Equal(item.Summary.StepsTotal, item.Summary.StepsDone);

        var files = await client.GetAsync<List<WorkItemFile>>($"/api/work-items/{id}/files");
        Assert.Superset(
            new HashSet<string> { "task.md", "context-Billing.Api.md", "root-cause.md", "plan.md", "summary-Billing.Api.md", "architecture-notes.md", "teams.md", "retro.md" },
            files.Select(file => file.Name).ToHashSet());

        var plan = await client.GetAsync<FileContent>($"/api/work-items/{id}/files/plan.md");
        Assert.Contains("Definition of Done", plan.Content);

        var agentSteps = item.Steps.Where(step => step.Kind == StepKind.Agent).ToList();
        Assert.All(agentSteps, step => Assert.True(step.Usage.TotalTokens > 0, step.StepId));
        Assert.True(item.Summary.Usage.TotalTokens > 5000);
        Assert.Contains("Zmiana demonstracyjna", File.ReadAllText(Path.Combine(item.Repos[0].LocalPath, "README.md")));
    }

    [Fact]
    public async Task Denied_permission_fails_the_step_instead_of_writing_files()
    {
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "RUN-3", "Odmowa zgody", "Bug");
        var id = created.Summary.Id;
        await client.PostAsync($"/api/work-items/{id}/start", new { });

        var item = await DriveToTheEndAsync(client, id, allowPermissions: false, stopOnFailure: true);

        var failed = item.Steps.Single(step => step.Status == StepStatus.Failed);
        Assert.Equal("implement", failed.StepId);
        Assert.Contains("Odmówiono zgody", failed.Error);
        Assert.DoesNotContain("Zmiana demonstracyjna", File.ReadAllText(Path.Combine(item.Repos[0].LocalPath, "README.md")));
    }

    private static async Task<WorkItemDetails> CreateAsync(HttpClient client, string key, string title, string type)
    {
        var response = await client.PostAsync("/api/work-items", new CreateWorkItemRequest(
            key, title, "Opis z polskimi znakami: żółć", type, TaskSourceKind.Manual, null, ["Billing.Api"]));
        return await response.ReadAsync<WorkItemDetails>();
    }

    /// <summary>Gra rolę użytkownika: zatwierdza każdy krok i odpowiada na prośby o zgodę.</summary>
    private static async Task<WorkItemDetails> DriveToTheEndAsync(
        HttpClient client, Guid id, bool allowPermissions = true, bool stopOnFailure = false)
    {
        var deadline = DateTime.UtcNow + Timeout;

        while (true)
        {
            var item = await client.GetAsync<WorkItemDetails>($"/api/work-items/{id}");
            var current = item.Steps.FirstOrDefault(step => step.Status is not (StepStatus.Succeeded or StepStatus.Skipped));

            if (current is null || (stopOnFailure && current.Status == StepStatus.Failed))
            {
                return item;
            }

            Assert.True(current.Status != StepStatus.Failed, $"{current.StepId}: {current.Error}\n{current.Log}");
            Assert.True(DateTime.UtcNow < deadline, $"Flow utknął na kroku {current.StepId} ({current.Status})");

            if (current.Status == StepStatus.Proposed)
            {
                await client.PostAsync($"/api/steps/{current.Id}/approve", new { });
            }

            foreach (var permission in await client.GetAsync<List<PermissionRequest>>($"/api/work-items/{id}/permissions"))
            {
                await client.PostAsync($"/api/permissions/{permission.Id}", new PermissionDecision(allowPermissions));
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
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

            Assert.True(DateTime.UtcNow < deadline, "Flow nie doszedł do oczekiwanego stanu: "
                + string.Join(", ", item.Steps.Select(step => $"{step.StepId}={step.Status}")));
            await Task.Delay(250, TestContext.Current.CancellationToken);
        }
    }
}
