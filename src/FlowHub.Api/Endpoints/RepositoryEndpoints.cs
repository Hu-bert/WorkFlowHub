using FlowHub.Application.Integrations;
using FlowHub.Application.Repositories;
using FlowHub.Domain;
using FlowHub.Domain.Repositories;
using FlowHub.Integrations.Http;

namespace FlowHub.Api.Endpoints;

public sealed record SuggestRepositoriesRequest(string Text);

public sealed record AddRemoteRepositoryRequest(string Name);

public sealed record AddLocalRepositoryRequest(string Path);

public sealed record RepoMapContent(string Content);

/// <summary>Wynik diagnostyki: zapytania do serwerów i to, co z nich wyszło.</summary>
public sealed record Diagnosis(IReadOnlyList<ApiCall> Calls, string? Error, string? PullRequest, string? Build, string? Release);

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
        group.MapGet("/{name}/branch-status", async (
            string name, string branch, bool? refresh, IRepositoryCatalog catalog, BranchStatusService branches, BranchStatusCache cache, CancellationToken ct) =>
            catalog.Find(name) is { } repository
                ? Results.Ok(await cache.GetAsync(repository, branch, refresh ?? false, () => branches.GetAsync(repository, branch, ct)))
                : Results.NotFound());

        // Edycja katalogu repo w aplikacji (zapis do repos.yaml).
        group.MapPost("/", (CodeRepository repository, IRepositoryCatalog catalog) => catalog.Save(repository, null));

        group.MapPut("/{name}", (string name, CodeRepository repository, IRepositoryCatalog catalog) =>
            catalog.Find(name) is null ? Results.NotFound() : Results.Ok(catalog.Save(repository, name)));

        group.MapDelete("/{name}", (string name, IRepositoryCatalog catalog) =>
            catalog.Remove(name) ? Results.NoContent() : Results.NotFound());

        // Diagnostyka: te same zapytania co wyżej, ale z listą adresów i odpowiedzi serwerów (bez tokenów).
        group.MapGet("/{name}/diagnose", async (string name, string branch, IRepositoryCatalog catalog, BranchStatusService branches, CancellationToken ct) =>
        {
            if (catalog.Find(name) is not { } repository)
            {
                return Results.NotFound();
            }

            var calls = ApiClient.StartTrace();
            try
            {
                var status = await branches.GetAsync(repository, branch, ct);
                return Results.Ok(new Diagnosis(calls, null, status.PullRequest.Text, status.Build.Text, status.Release));
            }
            catch (DomainException error)
            {
                return Results.Ok(new Diagnosis(calls, error.Message, null, null, null));
            }
        });

        // Podpowiedzi do mapowania repo: build configuration z CI i projekty z serwera wdrożeń.
        group.MapGet("/ci-configurations", (ICiServer ci, CancellationToken ct) => ci.ListConfigurationsAsync(ct));
        group.MapGet("/deploy-projects", (IDeployServer deploy, CancellationToken ct) => deploy.ListProjectsAsync(ct));

        // Repozytoria widoczne na serwerze kodu (TFS) — do wyboru przy nowym zadaniu.
        group.MapGet("/remote", (IRepositoryDirectory directory, CancellationToken ct) => directory.ListAsync(ct));

        // Wybrane repo z serwera trafia do katalogu repos.yaml. Adres bierzemy z serwera, nie od klienta.
        group.MapPost("/remote", async (AddRemoteRepositoryRequest request, IRepositoryDirectory directory, IRepositoryCatalog catalog, CancellationToken ct) =>
        {
            var remote = (await directory.ListAsync(ct)).FirstOrDefault(repository => repository.Name == request.Name)
                ?? throw new DomainException($"Na serwerze nie ma repo '{request.Name}'.");

            return catalog.Find(remote.Name) ?? catalog.Save(new CodeRepository { Name = remote.Name, Url = remote.Url, DefaultBranch = remote.DefaultBranch }, null);
        });

        // Mapa repo: zapamiętany wynik analizy technologicznej, używany ponownie w kolejnych zadaniach.
        group.MapGet("/{name}/map", (string name, RepoMapStore maps) => maps.Find(name) is { } map ? Results.Ok(map) : Results.NotFound());

        group.MapPut("/{name}/map", (string name, RepoMapContent body, RepoMapStore maps) => maps.Edit(name, body.Content));

        group.MapDelete("/{name}/map", (string name, RepoMapStore maps) => maps.Delete(name) ? Results.NoContent() : Results.NotFound());

        // Lokalne kopie repo z folderu ustawionego w Ustawieniach (np. C:\tfs).
        group.MapGet("/local", (bool? refresh, LocalRepositoryService local, CancellationToken ct) => local.ScanAsync(ct, refresh ?? false));

        group.MapPost("/local", (AddLocalRepositoryRequest request, LocalRepositoryService local, CancellationToken ct) =>
            local.AddToCatalogAsync(request.Path, ct));

        return app;
    }
}