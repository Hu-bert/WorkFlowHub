using FlowHub.Application.Agents;
using FlowHub.Application.Integrations;
using FlowHub.Application.WorkItems;

namespace FlowHub.Application.Engine;

/// <summary>Port: powiadomienia na żywo do UI (implementacja: SignalR).</summary>
public interface ILiveNotifier
{
    /// <summary>Zmienił się stan zadania lub któregoś kroku — UI dostaje cały aktualny obraz.</summary>
    Task WorkItemChangedAsync(WorkItemDetails item, CancellationToken ct);

    /// <summary>Kolejny fragment wyjścia działającego kroku.</summary>
    Task StepOutputAsync(Guid workItemId, Guid stepRunId, string text, CancellationToken ct);

    /// <summary>Agent prosi o zgodę na narzędzie — UI ma zapytać użytkownika.</summary>
    Task PermissionRequestedAsync(PermissionRequest request, CancellationToken ct);

    /// <summary>Prośba o zgodę już nie czeka (decyzja albo przerwanie kroku).</summary>
    Task PermissionResolvedAsync(PermissionRequest request, CancellationToken ct);

    /// <summary>Odświeżono statusy repo zadania (PR, buildy, wdrożenia).</summary>
    Task RepoStatusChangedAsync(WorkItemRepoStatus status, CancellationToken ct);
}
