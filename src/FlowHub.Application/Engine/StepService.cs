using FlowHub.Application.WorkItems;
using FlowHub.Domain;
using FlowHub.Domain.WorkItems;

namespace FlowHub.Application.Engine;

public sealed record EditProposalRequest(string Proposal, string? Model, int? TokenBudget);

/// <summary>Decyzje człowieka o krokach: start flow, edycja, zatwierdzenie, pominięcie, powtórka, przerwanie.</summary>
public sealed class StepService(IWorkItemStore store, FlowAdvancer advancer, StepQueue queue)
{
    public async Task<WorkItemDetails?> StartFlowAsync(Guid workItemId, CancellationToken ct)
    {
        var item = await store.FindAsync(workItemId, ct);
        if (item is null)
        {
            return null;
        }

        await advancer.AdvanceAsync(item, ct);
        return WorkItemMapper.ToDetails(item);
    }

    public Task<WorkItemDetails?> EditAsync(Guid stepRunId, EditProposalRequest request, CancellationToken ct) =>
        ChangeAsync(stepRunId, step => step.EditProposal(request.Proposal, request.Model, request.TokenBudget), ct);

    public Task<WorkItemDetails?> ApproveAsync(Guid stepRunId, CancellationToken ct) =>
        ChangeAsync(stepRunId, step => step.Approve(), ct);

    public Task<WorkItemDetails?> SkipAsync(Guid stepRunId, CancellationToken ct) =>
        ChangeAsync(stepRunId, step => step.Skip(), ct);

    /// <summary>„Powtórz z uwagą”: krok wraca do propozycji; kolejne kroki poczekają.</summary>
    public Task<WorkItemDetails?> RetryAsync(Guid stepRunId, string? remark, CancellationToken ct) =>
        ChangeAsync(stepRunId, step => step.Reopen(remark), ct);

    public bool Cancel(Guid stepRunId) => queue.Cancel(stepRunId);

    private async Task<WorkItemDetails?> ChangeAsync(Guid stepRunId, Action<StepRun> change, CancellationToken ct)
    {
        var item = await store.FindByStepAsync(stepRunId, ct);
        if (item is null)
        {
            return null;
        }

        var step = item.GetStep(stepRunId);
        EnsureIsCurrent(item, step);
        change(step);

        await advancer.AdvanceAsync(item, ct);
        return WorkItemMapper.ToDetails(item);
    }

    /// <summary>Kroki idą po kolei — decyzję można podjąć tylko o bieżącym albo już zakończonym kroku.</summary>
    private static void EnsureIsCurrent(WorkItem item, StepRun step)
    {
        var current = item.CurrentStep;

        if (current is not null && step.Order > current.Order)
        {
            throw new DomainException($"Najpierw dokończ krok „{current.Name}”.");
        }

        if (item.Steps.Any(other => other.Status is StepStatus.Running or StepStatus.Approved && other.Id != step.Id))
        {
            throw new DomainException("Inny krok tego zadania właśnie działa — poczekaj albo go przerwij.");
        }
    }
}
