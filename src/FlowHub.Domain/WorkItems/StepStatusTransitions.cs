namespace FlowHub.Domain.WorkItems;

/// <summary>Jedyne miejsce, które wie, jakie przejścia statusu kroku są dozwolone.</summary>
public static class StepStatusTransitions
{
    private static readonly Dictionary<StepStatus, StepStatus[]> Allowed = new()
    {
        [StepStatus.Pending] = [StepStatus.Proposed, StepStatus.Skipped],
        [StepStatus.Proposed] = [StepStatus.Approved, StepStatus.Skipped],
        [StepStatus.Approved] = [StepStatus.Running, StepStatus.Proposed],
        [StepStatus.Running] = [StepStatus.Succeeded, StepStatus.Failed],
        [StepStatus.Succeeded] = [StepStatus.Proposed], // „Powtórz z uwagą”
        [StepStatus.Failed] = [StepStatus.Proposed, StepStatus.Skipped],
        [StepStatus.Skipped] = [StepStatus.Proposed],
    };

    public static bool IsAllowed(StepStatus from, StepStatus to) => Allowed[from].Contains(to);

    public static void EnsureAllowed(StepStatus from, StepStatus to)
    {
        if (!IsAllowed(from, to))
        {
            throw new DomainException($"Niedozwolona zmiana statusu kroku: {from} → {to}.");
        }
    }
}
