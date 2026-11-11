using FlowHub.Application.Engine;

namespace FlowHub.Api.Hosting;

/// <summary>Usługa w tle: bierze zatwierdzone kroki z kolejki i uruchamia każdy we własnym zakresie DI.</summary>
public sealed class StepWorker(StepQueue queue, IServiceScopeFactory scopes, ILogger<StepWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverAsync(stoppingToken);

        await foreach (var stepRunId in queue.ReadAllAsync(stoppingToken))
        {
            // Kroki różnych zadań mogą działać równolegle — nie czekamy na zakończenie.
            _ = RunAsync(stepRunId, stoppingToken);
        }
    }

    private async Task RecoverAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<StepRecovery>().RecoverAsync(ct);
    }

    private async Task RunAsync(Guid stepRunId, CancellationToken stoppingToken)
    {
        using var cancellation = queue.Track(stepRunId, stoppingToken);

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<StepRunner>().RunAsync(stepRunId, cancellation.Token);
        }
        catch (Exception error)
        {
            logger.LogError(error, "Nie udało się uruchomić kroku {StepRunId}", stepRunId);
        }
        finally
        {
            queue.Untrack(stepRunId);
        }
    }
}
