using FlowHub.Application.Repositories;

namespace FlowHub.Api.Endpoints;

public sealed record SuggestRepositoriesRequest(string Text);

public static class RepositoryEndpoints
{
    public static IEndpointRouteBuilder MapRepositoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/repositories");

        group.MapGet("/", (IRepositoryCatalog catalog) => catalog.GetAll());

        group.MapPost("/reload", (IRepositoryCatalog catalog) =>
        {
            catalog.Reload();
            return catalog.GetAll();
        });

        group.MapPost("/suggest", (SuggestRepositoriesRequest request, IRepositoryCatalog catalog) =>
            RepositoryMatcher.Suggest(request.Text, catalog.GetAll()));

        return app;
    }
}
