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

    public long TaskLimit => options.Value.TaskTokenBudget;

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

    public static BudgetState StepState(StepRun step) => StateOf(step.Usage.TotalTokens, step.TokenBudget);

    public BudgetState TaskState(WorkItem item) => StateOf(item.Usage.TotalTokens, TaskLimit);
}
