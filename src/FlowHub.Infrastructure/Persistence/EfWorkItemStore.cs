using FlowHub.Application.WorkItems;
using FlowHub.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace FlowHub.Infrastructure.Persistence;

public sealed class EfWorkItemStore(FlowHubDbContext db) : IWorkItemStore
{
    private IQueryable<WorkItem> WorkItemsWithChildren =>
        db.WorkItems.Include(item => item.Repos).Include(item => item.Steps).AsSplitQuery();

    public async Task<IReadOnlyList<WorkItem>> ListAsync(CancellationToken ct) =>
        await WorkItemsWithChildren.ToListAsync(ct);

    public Task<WorkItem?> FindAsync(Guid id, CancellationToken ct) =>
        WorkItemsWithChildren.FirstOrDefaultAsync(item => item.Id == id, ct);

    public Task<WorkItem?> FindByStepAsync(Guid stepRunId, CancellationToken ct) =>
        WorkItemsWithChildren.FirstOrDefaultAsync(item => item.Steps.Any(step => step.Id == stepRunId), ct);

    public Task<bool> KeyExistsAsync(string key, CancellationToken ct) =>
        db.WorkItems.AnyAsync(item => item.Key == key, ct);

    public async Task AddAsync(WorkItem workItem, CancellationToken ct)
    {
        db.WorkItems.Add(workItem);
        await db.SaveChangesAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
