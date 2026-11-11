using FlowHub.Application.WorkItems;
using FlowHub.Domain;
using FlowHub.Domain.WorkItems;

namespace FlowHub.Application.Engine;

public sealed record EditProposalRequest(string Proposal, string? Model, int? TokenBudget);

/// <summary>Decyzje człowieka o krokach: start flow, edycja, zatwierdzenie, pominięcie, powtórka, przerwanie.</summary>
public sealed class StepService(IWorkItemStore store, FlowAdvancer advancer, StepQueue queue, StepContextFactory contexts)
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

    /// <summary>
    /// „Co dalej?” z kroku ręcznego: wskazany w flow krok agenta (followUp) rusza ponownie z poleceniem użytkownika,
    /// a kroki po nim — razem z tym krokiem ręcznym — czekają od nowa. Pętla trwa, aż użytkownik potwierdzi krok ręczny.
    /// </summary>
    public async Task<WorkItemDetails?> FollowUpAsync(Guid stepRunId, string? remark, string? repoName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(remark))
        {
            throw new DomainException("Napisz, co agent ma zrobić dalej.");
        }

        var item = await store.FindByStepAsync(stepRunId, ct);
        if (item is null)
        {
            return null;
        }

        var review = item.GetStep(stepRunId);
        EnsureIsCurrent(item, review);

        var targetId = contexts.Create(item, review).Definition.FollowUp
            ?? throw new DomainException($"Krok „{review.Name}” nie ma ustawionego kroku agenta (followUp w flow).");

        var targets = item.OrderedSteps
            .Where(step => step.StepId == targetId && (string.IsNullOrEmpty(repoName) || step.RepoName == repoName))
            .ToList();

        if (targets.Count == 0)
        {
            throw new DomainException("Nie znaleziono kroku agenta, do którego wysłać polecenie.");
        }

        // Wszystko po pierwszym poprawianym kroku (aż do tego kroku ręcznego) musi przejść jeszcze raz.
        foreach (var step in item.OrderedSteps.Where(step => step.Order > targets[0].Order && step.Order <= review.Order && !targets.Contains(step)))
        {
            step.ResetToPending();
        }

        foreach (var target in targets)
        {
            // Użytkownik właśnie zlecił tę pracę — nie każemy mu zatwierdzać jej drugi raz.
            target.Reopen(remark);
            target.Approve();
        }

        await advancer.AdvanceAsync(item, ct);
        return WorkItemMapper.ToDetails(item);
    }

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
