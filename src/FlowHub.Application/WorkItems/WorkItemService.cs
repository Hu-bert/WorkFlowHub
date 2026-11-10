using FlowHub.Application.Board;
using FlowHub.Application.Flows;
using FlowHub.Application.Repositories;
using FlowHub.Application.Workspace;
using FlowHub.Domain;
using FlowHub.Domain.Flows;
using FlowHub.Domain.WorkItems;
using Microsoft.Extensions.Options;

namespace FlowHub.Application.WorkItems;

/// <summary>Use case'y zadań: lista, szczegóły, utworzenie, przeniesienie na tablicy.</summary>
public sealed class WorkItemService(
    IWorkItemStore store,
    FlowCatalog flows,
    IRepositoryCatalog repositories,
    IOptions<BoardOptions> board,
    WorkspacePaths workspace,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<WorkItemSummary>> ListAsync(CancellationToken ct)
    {
        var items = await store.ListAsync(ct);
        return items.OrderByDescending(item => item.CreatedAt).Select(WorkItemMapper.ToSummary).ToList();
    }

    public async Task<WorkItemDetails?> GetAsync(Guid id, CancellationToken ct)
    {
        var item = await store.FindAsync(id, ct);
        return item is null ? null : WorkItemMapper.ToDetails(item);
    }

    public async Task<WorkItemDetails> CreateAsync(CreateWorkItemRequest request, CancellationToken ct)
    {
        var flow = FlowFor(request.Type);
        await EnsureKeyIsFreeAsync(request.Key, ct);

        var item = WorkItem.Create(
            new NewWorkItem(request.Key, request.Title, request.Description, flow.Type, request.Source, request.SourceUrl),
            workspace.WorkDirectory,
            board.Value.DefaultColumn,
            clock.GetUtcNow().UtcDateTime);

        foreach (var repositoryName in KnownRepositories(request.Repositories))
        {
            item.AddRepo(repositoryName);
        }

        item.PlanSteps(flow);
        await store.AddAsync(item, ct);
        return WorkItemMapper.ToDetails(item);
    }

    public async Task<bool> MoveAsync(Guid id, string boardColumn, CancellationToken ct)
    {
        if (!board.Value.HasColumn(boardColumn))
        {
            throw new DomainException($"Nie ma kolumny '{boardColumn}' na tablicy.");
        }

        var item = await store.FindAsync(id, ct);
        if (item is null)
        {
            return false;
        }

        item.MoveTo(boardColumn);
        await store.SaveAsync(ct);
        return true;
    }

    private FlowDefinition FlowFor(string type) =>
        flows.Find(type) ?? throw new DomainException($"Nie ma typu zadania '{type}'. Sprawdź katalog flows/.");

    private async Task EnsureKeyIsFreeAsync(string key, CancellationToken ct)
    {
        if (await store.KeyExistsAsync(key.Trim(), ct))
        {
            throw new DomainException($"Zadanie '{key}' już istnieje.");
        }
    }

    private IEnumerable<string> KnownRepositories(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
        {
            throw new DomainException("Wybierz co najmniej jedno repozytorium.");
        }

        return names.Select(name => repositories.Find(name)?.Name
            ?? throw new DomainException($"Repo '{name}' nie istnieje w katalogu (repos.yaml)."));
    }
}
