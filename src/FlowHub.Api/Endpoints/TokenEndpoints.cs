using FlowHub.Application.Agents;
using FlowHub.Application.Tokens;
using Microsoft.Extensions.Options;

namespace FlowHub.Api.Endpoints;

public sealed record ModelTier(string Tier, string Model);

public sealed record ModelsResponse(string DefaultTier, IReadOnlyList<ModelTier> Tiers, long TaskTokenBudget, string AgentRunner);

public static class TokenEndpoints
{
    public static IEndpointRouteBuilder MapTokenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tokens/report", (TokenReportService report, CancellationToken ct) => report.BuildAsync(ct));

        app.MapGet("/api/models", (IOptions<ModelOptions> models, IOptions<AgentOptions> agent) => new ModelsResponse(
            models.Value.DefaultTier,
            models.Value.Tiers.Select(tier => new ModelTier(tier.Key, tier.Value)).ToList(),
            models.Value.TaskTokenBudget,
            agent.Value.Runner));

        return app;
    }
}
