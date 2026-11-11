using FlowHub.Application.WorkItems;
using FlowHub.Domain.WorkItems;
using Microsoft.Extensions.Logging;

namespace FlowHub.Application.Engine;

/// <summary>Po restarcie aplikacji: kroki, które „działały”, już nie działają — oddajemy decyzję człowiekowi.</summary>
public sealed class StepRecovery(IWorkItemStore store, TimeProvider clock, ILogger<StepRecovery> logger)
{
    public async Task RecoverAsync(CancellationToken ct)
    {
        foreach (var item in await store.ListAsync(ct))
        {
            foreach (var step in item.Steps)
            {
                Recover(item, step);
            }
        }

        await store.SaveAsync(ct);
    }

    private void Recover(WorkItem item, StepRun step)
    {
        if (step.Status == StepStatus.Running)
        {
            step.Fail("Krok przerwany przez restart aplikacji.", clock.GetUtcNow().UtcDateTime);
            logger.LogWarning("Zadanie {Key}: krok {Step} przerwany przez restart", item.Key, step.StepId);
        }
        else if (step.Status == StepStatus.Approved)
        {
            step.Reopen(step.Remark);
        }
    }
}
