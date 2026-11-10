using System.Net;
using FlowHub.Api.Endpoints;
using FlowHub.Application.WorkItems;
using FlowHub.Domain.WorkItems;
using Microsoft.AspNetCore.Mvc;

namespace FlowHub.Api.Tests;

public sealed class WorkItemEndpointTests(FlowHubApiFactory factory) : IClassFixture<FlowHubApiFactory>
{
    private static CreateWorkItemRequest NewBug(string key, params string[] repositories) =>
        new(key, "Zła stawka VAT na fakturze", "Opis błędu", "Bug", TaskSourceKind.Manual, null, repositories);

    [Fact]
    public async Task Created_item_is_saved_with_steps_for_each_repo()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/work-items", NewBug("API-1", "Billing.Api", "Shared.Lib"));
        var created = await response.ReadAsync<WorkItemDetails>();
        var loaded = await client.GetAsync<WorkItemDetails>($"/api/work-items/{created.Summary.Id}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, loaded.Repos.Count);
        Assert.Equal(2, loaded.Steps.Count(step => step.StepId == "clone-repo"));
        Assert.All(loaded.Steps, step => Assert.Equal(StepStatus.Pending, step.Status));
        Assert.StartsWith(factory.WorkDirectory, loaded.WorkDirectory);
        Assert.Equal(DateTimeKind.Utc, loaded.Summary.CreatedAt.Kind);
    }

    [Fact]
    public async Task Broken_rule_returns_400_with_a_readable_message()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/work-items", NewBug("API-2", "Nie.Ma.Takiego"));
        var problem = await response.ReadAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Nie.Ma.Takiego", problem.Detail);
    }

    [Fact]
    public async Task Item_can_be_moved_on_the_board()
    {
        using var client = factory.CreateClient();
        var created = await (await client.PostAsync("/api/work-items", NewBug("API-3", "Billing.Api"))).ReadAsync<WorkItemDetails>();

        var moved = await client.PutAsync($"/api/work-items/{created.Summary.Id}/column", new MoveWorkItemRequest("waiting-tests"));
        var list = await client.GetAsync<List<WorkItemSummary>>("/api/work-items");

        Assert.Equal(HttpStatusCode.NoContent, moved.StatusCode);
        Assert.Equal("waiting-tests", list.Single(item => item.Key == "API-3").BoardColumn);
    }

    [Fact]
    public async Task Unknown_item_returns_404()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/work-items/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
