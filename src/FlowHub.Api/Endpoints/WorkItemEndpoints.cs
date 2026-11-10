using FlowHub.Application.Board;
using FlowHub.Application.WorkItems;
using Microsoft.Extensions.Options;

namespace FlowHub.Api.Endpoints;

public sealed record MoveWorkItemRequest(string Column);

public static class WorkItemEndpoints
{
    public static IEndpointRouteBuilder MapWorkItemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/board", (IOptions<BoardOptions> board) => board.Value.Columns);

        var group = app.MapGroup("/api/work-items");

        group.MapGet("/", (WorkItemService service, CancellationToken ct) => service.ListAsync(ct));

        group.MapGet("/{id:guid}", async (Guid id, WorkItemService service, CancellationToken ct) =>
            await service.GetAsync(id, ct) is { } item ? Results.Ok(item) : Results.NotFound());

        group.MapPost("/", async (CreateWorkItemRequest request, WorkItemService service, CancellationToken ct) =>
        {
            var item = await service.CreateAsync(request, ct);
            return Results.Created($"/api/work-items/{item.Summary.Id}", item);
        });

        group.MapPut("/{id:guid}/column", async (Guid id, MoveWorkItemRequest request, WorkItemService service, CancellationToken ct) =>
            await service.MoveAsync(id, request.Column, ct) ? Results.NoContent() : Results.NotFound());

        return app;
    }
}
