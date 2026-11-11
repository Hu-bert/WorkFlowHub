using FlowHub.Application.Integrations;
using FlowHub.Application.Repositories;
using FlowHub.Domain;
using FlowHub.Domain.Repositories;

namespace FlowHub.Api.Endpoints;

public sealed record SuggestRepositoriesRequest(string Text);

public sealed record AddRemoteRepositoryRequest(string Name);

public sealed record AddLocalRepositoryRequest(string Path);

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

        // Stan brancha repo w TFS / TeamCity / Octopus (dla lokalnych kopii na stronie Repozytoria).
        group.MapGet("/{name}/branch-status", async (string name, string branch, IRepositoryCatalog catalog, BranchStatusService branches, CancellationToken ct) =>
            catalog.Find(name) is { } repository ? Results.Ok(await branches.GetAsync(repository, branch, ct)) : Results.NotFound());

        // Repozytoria widoczne na serwerze kodu (TFS) — do wyboru przy nowym zadaniu.
        group.MapGet("/remote", (IRepositoryDirectory directory, CancellationToken ct) => directory.ListAsync(ct));

        // Wybrane repo z serwera trafia do katalogu repos.yaml. Adres bierzemy z serwera, nie od klienta.
        group.MapPost("/remote", async (AddRemoteRepositoryRequest request, IRepositoryDirectory directory, IRepositoryCatalog catalog, CancellationToken ct) =>
        {
            var remote = (await directory.ListAsync(ct)).FirstOrDefault(repository => repository.Name == request.Name)
                ?? throw new DomainException($"Na serwerze nie ma repo '{request.Name}'.");

            return catalog.Add(new CodeRepository { Name = remote.Name, Url = remote.Url, DefaultBranch = remote.DefaultBranch });
        });

        // Lokalne kopie repo z folderu ustawionego w Ustawieniach (np. C:\tfs).
        group.MapGet("/local", (LocalRepositoryService local, CancellationToken ct) => local.ScanAsync(ct));

        group.MapPost("/local", (AddLocalRepositoryRequest request, LocalRepositoryService local, CancellationToken ct) =>
            local.AddToCatalogAsync(request.Path, ct));

        return app;
    }
}