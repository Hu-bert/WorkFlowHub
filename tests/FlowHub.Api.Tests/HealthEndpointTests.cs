using FlowHub.Api.Endpoints;

namespace FlowHub.Api.Tests;

public sealed class HealthEndpointTests(FlowHubApiFactory factory) : IClassFixture<FlowHubApiFactory>
{
    [Fact]
    public async Task Health_returns_ok()
    {
        using var client = factory.CreateClient();

        var health = await client.GetAsync<HealthResponse>("/api/health");

        Assert.Equal("ok", health.Status);
    }
}
