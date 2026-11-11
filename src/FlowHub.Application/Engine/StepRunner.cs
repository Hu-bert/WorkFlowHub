using FlowHub.Application.WorkItems;
using FlowHub.Domain;
using FlowHub.Domain.WorkItems;
using Microsoft.Extensions.Logging;

namespace FlowHub.Application.Engine;

/// <summary>Wykonuje jeden zatwierdzony krok: start → wykonawca → wynik → następny krok.</summary>
public sealed class StepRunner(
    IWorkItemStore store,
    StepContextFactory contexts,
    StepExecutorRegistry executors,
    FlowAdvancer advancer,
    ILiveNotifier live,
    TimeProvider clock,
    ILogger<StepRunner> logger)
{
    private static readonly TimeSpan LogSaveInterval = TimeSpan.FromSeconds(2);

    public async Task RunAsync(Guid stepRunId, CancellationToken ct)
    {
        var item = await store.FindByStepAsync(stepRunId, CancellationToken.None);
        var step = item?.Steps.FirstOrDefault(candidate => candidate.Id == stepRunId);

        if (item is null || step is null || step.Status != StepStatus.Approved)
        {
            return;
        }

        step.Start(Now);
        await SaveAndNotifyAsync(item);

        var outcome = await ExecuteAsync(item, step, ct);

        if (outcome.Succeeded)
        {
            step.Succeed(outcome.Message, Now);
        }
        else
        {
            step.Fail(outcome.Message, Now);
        }

        // Wynik zapisujemy zawsze — także gdy krok przerwano albo aplikacja się zamyka.
        await advancer.AdvanceAsync(item, CancellationToken.None);
    }

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private async Task<StepOutcome> ExecuteAsync(WorkItem item, StepRun step, CancellationToken ct)
    {
        try
        {
            var log = new StepLog(item, step, store, live, clock, LogSaveInterval);
            return await executors.For(step.Kind).ExecuteAsync(contexts.Create(item, step), log, ct);
        }
        catch (OperationCanceledException)
        {
            return StepOutcome.Failure("Krok przerwany.");
        }
        catch (DomainException error)
        {
            return StepOutcome.Failure(error.Message);
        }
        catch (Exception error)
        {
            logger.LogError(error, "Krok {Step} zadania {Key} zakończył się wyjątkiem", step.StepId, item.Key);
            return StepOutcome.Failure($"Nieoczekiwany błąd: {error.Message}");
        }
    }

    private async Task SaveAndNotifyAsync(WorkItem item)
    {
        await store.SaveAsync(CancellationToken.None);
        await live.WorkItemChangedAsync(WorkItemMapper.ToDetails(item), CancellationToken.None);
    }

    /// <summary>Dopisuje wyjście do logu kroku, wysyła je do UI i co chwilę zapisuje do bazy.</summary>
    private sealed class StepLog(
        WorkItem item,
        StepRun step,
        IWorkItemStore store,
        ILiveNotifier live,
        TimeProvider clock,
        TimeSpan saveInterval) : IStepLog
    {
        private DateTimeOffset _lastSave = clock.GetUtcNow();

        public async Task WriteAsync(string text, CancellationToken ct = default)
        {
            step.AppendLog(text);
            await live.StepOutputAsync(item.Id, step.Id, text, CancellationToken.None);

            if (clock.GetUtcNow() - _lastSave >= saveInterval)
            {
                _lastSave = clock.GetUtcNow();
                await store.SaveAsync(CancellationToken.None);
            }
        }
    }
}
