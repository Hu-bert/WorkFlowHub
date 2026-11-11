using FlowHub.Application.Blocks;
using FlowHub.Application.Flows;
using FlowHub.Domain.Flows;

namespace FlowHub.Api.Endpoints;

public sealed record FlowsResponse(IReadOnlyList<FlowDefinition> Flows, IReadOnlyList<FlowProblem> Problems);

public sealed record SaveFlowResponse(bool Saved, IReadOnlyList<string> Errors);

public sealed record BlockContent(string Path, string Content);

public static class FlowEndpoints
{
    public static IEndpointRouteBuilder MapFlowEndpoints(this IEndpointRouteBuilder app)
    {
        var flows = app.MapGroup("/api/flows");

        flows.MapGet("/", (FlowCatalog catalog) => new FlowsResponse(catalog.Flows, catalog.Problems));

        flows.MapPost("/reload", (FlowCatalog catalog) =>
        {
            catalog.Reload();
            return new FlowsResponse(catalog.Flows, catalog.Problems);
        });

        // Edytor flow: dokument do edycji, walidacja „na sucho” i zapis do YAML.
        flows.MapGet("/{type}/document", (string type, FlowCatalog catalog) =>
            catalog.FindDocument(type) is { } document ? Results.Ok(document) : Results.NotFound());

        flows.MapPost("/validate", (FlowDocument document) =>
            new SaveFlowResponse(false, FlowValidator.Validate(document)));

        flows.MapPut("/", (FlowDocument document, FlowCatalog catalog) =>
        {
            var errors = catalog.Save(document);
            return new SaveFlowResponse(errors.Count == 0, errors);
        });

        // Edytor bloczków: treść pliku z blocks/.
        var blocks = app.MapGroup("/api/blocks");

        blocks.MapGet("/content", (string path, IBlockLibrary library) =>
            library.ReadText(path) is { } content ? Results.Ok(new BlockContent(path, content)) : Results.NotFound());

        blocks.MapPut("/content", (BlockContent block, IBlockLibrary library) =>
        {
            library.WriteText(block.Path, block.Content);
            return Results.NoContent();
        });

        return app;
    }
}
