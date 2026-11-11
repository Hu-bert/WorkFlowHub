using FlowHub.Application.WorkItems;
using FlowHub.Domain;
using FlowHub.Domain.WorkItems;

namespace FlowHub.Application.Engine;

/// <summary>
/// Przesuwa flow do przodu: bieżący krok dostaje propozycję i czeka na człowieka,
/// a krok bez wymaganej akceptacji od razu trafia do kolejki.
/// </summary>
public sealed class FlowAdvancer(
    IWorkItemStore store,
    StepContextFactory contexts,
    StepExecutorRegistry executors,
    StepQueue queue,
    ILiveNotifier live)
{
    public async Task AdvanceAsync(WorkItem item, CancellationToken ct)
    {
        var step = item.CurrentStep;

        if (step?.Status == StepStatus.Pending)
        {
            var (proposal, ready) = await ProposeAsync(item, step, ct);
            step.Propose(proposal, model: null);

            if (ready && !step.RequiresApproval)
            {
                step.Approve();
            }
        }

        await store.SaveAsync(ct);
        await live.WorkItemChangedAsync(WorkItemMapper.ToDetails(item), ct);

        if (step?.Status == StepStatus.Approved)
        {
            queue.Enqueue(step.Id);
        }
    }

    /// <summary>Gdy propozycji nie da się przygotować, krok czeka na człowieka z opisem problemu.</summary>
    private async Task<(string Proposal, bool Ready)> ProposeAsync(WorkItem item, StepRun step, CancellationToken ct)
    {
        try
        {
            return (await executors.For(step.Kind).ProposeAsync(contexts.Create(item, step), ct), true);
        }
        catch (DomainException error)
        {
            return ($"⚠ Nie można przygotować kroku: {error.Message}", false);
        }
    }
}
