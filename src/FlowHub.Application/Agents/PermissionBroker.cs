using System.Collections.Concurrent;
using FlowHub.Application.Engine;

namespace FlowHub.Application.Agents;

public sealed record PermissionRequest(Guid Id, Guid WorkItemId, Guid StepRunId, string Tool, string Detail);

/// <summary>
/// Pośrednik próśb o zgodę: agent czeka, UI pokazuje pytanie, decyzja użytkownika wznawia agenta.
/// </summary>
public sealed class PermissionBroker(ILiveNotifier live)
{
    private sealed record Pending(PermissionRequest Request, TaskCompletionSource<bool> Decision);

    private readonly ConcurrentDictionary<Guid, Pending> _pending = new();

    public IReadOnlyList<PermissionRequest> PendingFor(Guid workItemId) =>
        _pending.Values.Select(pending => pending.Request).Where(request => request.WorkItemId == workItemId).ToList();

    public async Task<bool> AskAsync(Guid workItemId, Guid stepRunId, string tool, string detail, CancellationToken ct)
    {
        var request = new PermissionRequest(Guid.NewGuid(), workItemId, stepRunId, tool, detail);
        var pending = new Pending(request, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        _pending[request.Id] = pending;

        try
        {
            await live.PermissionRequestedAsync(request, CancellationToken.None);
            return await pending.Decision.Task.WaitAsync(ct);
        }
        finally
        {
            _pending.TryRemove(request.Id, out _);
            await live.PermissionResolvedAsync(request, CancellationToken.None);
        }
    }

    /// <summary>Decyzja użytkownika. Zwraca false, gdy prośba już nie czeka.</summary>
    public bool Decide(Guid requestId, bool allow) =>
        _pending.TryGetValue(requestId, out var pending) && pending.Decision.TrySetResult(allow);
}
