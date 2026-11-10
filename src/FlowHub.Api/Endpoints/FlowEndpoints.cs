using FlowHub.Application.Flows;
using FlowHub.Domain.Flows;

namespace FlowHub.Api.Endpoints;

public sealed record FlowsResponse(IReadOnlyList<FlowDefinition> Flows, IReadOnlyList<FlowProblem> Problems);

public static class FlowEndpoints
{
    public static IEndpointRouteBuilder MapFlowEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/flows");

        group.MapGet("/", (FlowCatalog catalog) => new FlowsResponse(catalog.Flows, catalog.Problems));

        group.MapPost("/reload", (FlowCatalog catalog) =>
        {
            catalog.Reload();
            return new FlowsResponse(catalog.Flows, catalog.Problems);
        });

        return app;
    }
}
