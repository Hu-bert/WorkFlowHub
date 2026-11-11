using FlowHub.Infrastructure.Persistence;
using FlowHub.Integrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FlowHub.Api.Tests;

public sealed class DependencyInjectionTests(FlowHubApiFactory factory) : IClassFixture<FlowHubApiFactory>
{
    [Fact]
    public void Database_context_is_registered()
    {
        using var scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<FlowHubDbContext>());
    }

    [Fact]
    public void Integrations_default_to_fake()
    {
        var options = factory.Services.GetRequiredService<IOptions<IntegrationsOptions>>().Value;

        Assert.All(
            [options.TaskSource, options.PullRequests, options.Ci, options.Deploy, options.Docs],
            mode => Assert.Equal(IntegrationMode.Fake, mode));
    }
}
