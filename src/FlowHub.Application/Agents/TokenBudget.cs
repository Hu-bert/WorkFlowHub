using FlowHub.Domain.WorkItems;
using Microsoft.Extensions.Options;

namespace FlowHub.Application.Agents;

public enum BudgetState
{
    Ok,

    /// <summary>Zużyto co najmniej 80% — ostrzegamy.</summary>
    Warning,

    /// <summary>Zużyto 100% — zatrzymujemy i pytamy człowieka.</summary>
    Exceeded,
}

/// <summary>Pilnuje budżetu tokenów kroku i całego zadania.</summary>
public sealed class TokenBudget(IOptions<ModelOptions> options)
{
    public const double WarningThreshold = 0.8;

    /// <summary>Plan bez limitu (Copilot Enterprise): budżety są tylko informacją, nic nie zatrzymują.</summary>
    public bool Unlimited => options.Value.Unlimited;

    public long TaskLimit => Unlimited ? 0 : options.Value.TaskTokenBudget;

    public static BudgetState StateOf(long used, long limit)
    {
        if (limit <= 0)
        {
            return BudgetState.Ok;
        }

        if (used >= limit)
        {
            return BudgetState.Exceeded;
        }

        return used >= limit * WarningThreshold ? BudgetState.Warning : BudgetState.Ok;
    }

    public BudgetState StepState(StepRun step) =>
        Unlimited ? BudgetState.Ok : StateOf(step.Usage.TotalTokens, step.TokenBudget);

    public BudgetState TaskState(WorkItem item) => StateOf(item.Usage.TotalTokens, TaskLimit);
}
