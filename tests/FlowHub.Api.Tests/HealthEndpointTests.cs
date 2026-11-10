using System.Net.Http.Json;
using FlowHub.Api.Endpoints;

namespace FlowHub.Api.Tests;

public sealed class HealthEndpointTests(FlowHubApiFactory factory) : IClassFixture<FlowHubApiFactory>
{
    [Fact]
    public async Task Health_returns_ok()
    {
        using var client = factory.CreateClient();

        var health = await client.GetFromJsonAsync<HealthResponse>("/api/health", TestContext.Current.CancellationToken);

        Assert.Equal("ok", health?.Status);
    }
}
