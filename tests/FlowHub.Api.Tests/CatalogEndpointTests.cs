using FlowHub.Api.Endpoints;
using FlowHub.Application.Board;
using FlowHub.Application.Repositories;
using FlowHub.Domain.Repositories;

namespace FlowHub.Api.Tests;

/// <summary>Pilnuje plików z repo: flows/*.yaml, repos.yaml i kolumn tablicy z appsettings.json.</summary>
public sealed class CatalogEndpointTests(FlowHubApiFactory factory) : IClassFixture<FlowHubApiFactory>
{
    [Fact]
    public async Task All_flow_files_in_the_repo_are_valid()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync<FlowsResponse>("/api/flows");

        Assert.Empty(response.Problems);
        Assert.Equal(["Bug", "Business", "Spike", "Technical"], response.Flows.Select(flow => flow.Type).Order());
    }

    [Fact]
    public async Task Repository_catalog_is_loaded_and_suggests_by_keywords()
    {
        using var client = factory.CreateClient();

        var repositories = await client.GetAsync<List<CodeRepository>>("/api/repositories");
        var response = await client.PostAsync("/api/repositories/suggest", new SuggestRepositoriesRequest("Zła faktura w portalu"));
        var suggestions = await response.ReadAsync<List<RepositorySuggestion>>();

        Assert.Contains(repositories, repository => repository.Name == "Billing.Api");
        Assert.Equal(["Billing.Api", "Customer.Portal"], suggestions.Select(s => s.Name).Order());
    }

    [Fact]
    public async Task Board_has_columns_from_configuration()
    {
        using var client = factory.CreateClient();

        var columns = await client.GetAsync<List<BoardColumn>>("/api/board");

        Assert.Equal("todo", columns[0].Key);
        Assert.Contains(columns, column => column.Key == "waiting-deploy");
    }
}
