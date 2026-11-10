using FlowHub.Domain.WorkItems;

namespace FlowHub.Application.WorkItems;

/// <summary>Port: trwałość zadań. Zadanie zawsze wczytywane razem z repo i krokami.</summary>
public interface IWorkItemStore
{
    Task<IReadOnlyList<WorkItem>> ListAsync(CancellationToken ct);

    Task<WorkItem?> FindAsync(Guid id, CancellationToken ct);

    Task<WorkItem?> FindByStepAsync(Guid stepRunId, CancellationToken ct);

    Task<bool> KeyExistsAsync(string key, CancellationToken ct);

    Task AddAsync(WorkItem workItem, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}
