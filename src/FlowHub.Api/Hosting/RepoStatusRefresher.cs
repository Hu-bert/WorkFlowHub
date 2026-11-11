using FlowHub.Application.Engine;
using FlowHub.Application.Integrations;
using FlowHub.Application.WorkItems;
using Microsoft.Extensions.Options;

namespace FlowHub.Api.Hosting;

/// <summary>Usługa w tle: co 1–2 minuty odświeża statusy repo (PR, buildy, wdrożenia) niezakończonych zadań.</summary>
public sealed class RepoStatusRefresher(
    IServiceScopeFactory scopes,
    ILiveNotifier live,
    IOptions<RepoStatusOptions> options,
    ILogger<RepoStatusRefresher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(15, options.Value.RefreshSeconds)));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RefreshAllAsync(stoppingToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // Serwer integracji chwilowo niedostępny — spróbujemy w następnym cyklu.
                logger.LogWarning(error, "Nie udało się odświeżyć statusów repo");
            }
        }
    }

    private async Task RefreshAllAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IWorkItemStore>();
        var statuses = scope.ServiceProvider.GetRequiredService<RepoStatusService>();

        foreach (var item in (await store.ListAsync(ct)).Where(item => !item.IsFinished))
        {
            await live.RepoStatusChangedAsync(await statuses.RefreshAsync(item, ct), ct);
        }
    }
}
