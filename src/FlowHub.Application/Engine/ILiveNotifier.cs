using FlowHub.Application.WorkItems;

namespace FlowHub.Application.Engine;

/// <summary>Port: powiadomienia na żywo do UI (implementacja: SignalR).</summary>
public interface ILiveNotifier
{
    /// <summary>Zmienił się stan zadania lub któregoś kroku — UI dostaje cały aktualny obraz.</summary>
    Task WorkItemChangedAsync(WorkItemDetails item, CancellationToken ct);

    /// <summary>Kolejny fragment wyjścia działającego kroku.</summary>
    Task StepOutputAsync(Guid workItemId, Guid stepRunId, string text, CancellationToken ct);
}
