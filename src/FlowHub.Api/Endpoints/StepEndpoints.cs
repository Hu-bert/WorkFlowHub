using FlowHub.Application.Engine;
using FlowHub.Application.WorkItems;

namespace FlowHub.Api.Endpoints;

public sealed record RetryStepRequest(string? Remark);

public static class StepEndpoints
{
    public static IEndpointRouteBuilder MapStepEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/work-items/{id:guid}/start", async (Guid id, StepService steps, CancellationToken ct) =>
            OkOrNotFound(await steps.StartFlowAsync(id, ct)));

        var group = app.MapGroup("/api/steps/{id:guid}");

        group.MapPut("/proposal", async (Guid id, EditProposalRequest request, StepService steps, CancellationToken ct) =>
            OkOrNotFound(await steps.EditAsync(id, request, ct)));

        group.MapPost("/approve", async (Guid id, StepService steps, CancellationToken ct) =>
            OkOrNotFound(await steps.ApproveAsync(id, ct)));

        group.MapPost("/skip", async (Guid id, StepService steps, CancellationToken ct) =>
            OkOrNotFound(await steps.SkipAsync(id, ct)));

        group.MapPost("/retry", async (Guid id, RetryStepRequest request, StepService steps, CancellationToken ct) =>
            OkOrNotFound(await steps.RetryAsync(id, request.Remark, ct)));

        group.MapPost("/cancel", (Guid id, StepService steps) =>
            steps.Cancel(id) ? Results.Accepted() : Results.NotFound());

        return app;
    }

    private static IResult OkOrNotFound(WorkItemDetails? item) => item is null ? Results.NotFound() : Results.Ok(item);
}
