namespace FlowHub.Domain.WorkItems;

/// <summary>Jedyne miejsce, które wie, jakie przejścia statusu kroku są dozwolone.</summary>
public static class StepStatusTransitions
{
    private static readonly Dictionary<StepStatus, StepStatus[]> Allowed = new()
    {
        [StepStatus.Pending] = [StepStatus.Proposed, StepStatus.Skipped],
        [StepStatus.Proposed] = [StepStatus.Approved, StepStatus.Skipped, StepStatus.Pending], // Pending: wcześniejszy krok wraca do poprawki
        [StepStatus.Approved] = [StepStatus.Running, StepStatus.Proposed],
        [StepStatus.Running] = [StepStatus.Succeeded, StepStatus.Failed],
        [StepStatus.Succeeded] = [StepStatus.Proposed, StepStatus.Pending], // „Powtórz z uwagą” / ponowne przejście po poprawce
        [StepStatus.Failed] = [StepStatus.Proposed, StepStatus.Skipped, StepStatus.Pending],
        [StepStatus.Skipped] = [StepStatus.Proposed, StepStatus.Pending],
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
