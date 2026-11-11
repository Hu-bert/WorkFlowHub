using FlowHub.Application.Agents;
using FlowHub.Application.Blocks;
using FlowHub.Application.WorkItems;

namespace FlowHub.Api.Endpoints;

public sealed record PermissionDecision(bool Allow);

public sealed record FileContent(string Name, string Content);

public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        // Prośby agenta o zgodę na narzędzia.
        app.MapGet("/api/work-items/{id:guid}/permissions", (Guid id, PermissionBroker broker) => broker.PendingFor(id));

        app.MapPost("/api/permissions/{id:guid}", (Guid id, PermissionDecision decision, PermissionBroker broker) =>
            broker.Decide(id, decision.Allow) ? Results.NoContent() : Results.NotFound());

        // Pliki wynikowe zadania.
        app.MapGet("/api/work-items/{id:guid}/files", async (Guid id, WorkItemFiles files, CancellationToken ct) =>
            await files.ListAsync(id, ct) is { } list ? Results.Ok(list) : Results.NotFound());

        app.MapGet("/api/work-items/{id:guid}/files/{name}", async (Guid id, string name, WorkItemFiles files, CancellationToken ct) =>
            await files.ReadAsync(id, name, ct) is { } content ? Results.Ok(new FileContent(name, content)) : Results.NotFound());

        // Edycja pliku .md zadania z poziomu aplikacji.
        app.MapPut("/api/work-items/{id:guid}/files/{name}", async (Guid id, string name, FileContent file, WorkItemFiles files, CancellationToken ct) =>
            await files.WriteAsync(id, name, file.Content, ct) ? Results.NoContent() : Results.NotFound());

        // Biblioteka bloczków.
        app.MapGet("/api/blocks", (IBlockLibrary blocks) => blocks.List());

        return app;
    }
}
