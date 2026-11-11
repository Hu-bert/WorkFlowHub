using FlowHub.Application.Git;
using FlowHub.Application.Integrations;
using FlowHub.Application.Repositories;
using FlowHub.Application.WorkItems;

namespace FlowHub.Api.Endpoints;

/// <summary>Kropka statusu repo na karcie zadania (lista / kanban).</summary>
public sealed record RepoLevel(string Repo, HealthLevel Level);

public static class IntegrationEndpoints
{
    public static IEndpointRouteBuilder MapIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        // Treść zadania z trackera (przycisk „Pobierz” w nowym zadaniu).
        app.MapGet("/api/task-source/{key}", async (string key, ITaskSource source, CancellationToken ct) =>
            await source.GetAsync(key, ct) is { } task ? Results.Ok(task) : Results.NotFound());

        // Tabela repo w zadaniu. ?refresh=true = „Odśwież teraz”.
        app.MapGet("/api/work-items/{id:guid}/repo-status", async (Guid id, bool? refresh, RepoStatusService statuses, CancellationToken ct) =>
            await statuses.GetAsync(id, refresh ?? false, ct) is { } status ? Results.Ok(status) : Results.NotFound());

        // Kropki per repo dla wszystkich zadań (dashboard, kanban).
        app.MapGet("/api/repo-status/levels", async (WorkItemService items, RepoStatusService statuses, CancellationToken ct) =>
        {
            var levels = new Dictionary<Guid, IReadOnlyList<RepoLevel>>();

            foreach (var item in await items.ListAsync(ct))
            {
                if (await statuses.GetAsync(item.Id, refresh: false, ct) is { } status)
                {
                    levels[item.Id] = status.Repos.Select(repo => new RepoLevel(repo.Repo, repo.Level)).ToList();
                }
            }

            return levels;
        });

        // Diff repo zadania względem brancha domyślnego.
        app.MapGet("/api/work-items/{id:guid}/repos/{repo}/diff", async (
            Guid id, string repo, WorkItemService items, IRepositoryCatalog catalog, IGitService git, CancellationToken ct) =>
        {
            var taskRepo = (await items.GetAsync(id, ct))?.Repos.FirstOrDefault(candidate => candidate.Name == repo);
            if (taskRepo is null)
            {
                return Results.NotFound();
            }

            var baseBranch = catalog.Find(repo)?.DefaultBranch ?? "main";
            return Results.Ok(await git.DiffAsync(repo, taskRepo.LocalPath, baseBranch, ct));
        });

        return app;
    }
}
