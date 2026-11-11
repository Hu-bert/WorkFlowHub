using FlowHub.Application.Agents;
using FlowHub.Application.Engine;
using FlowHub.Application.Integrations;
using FlowHub.Application.WorkItems;
using Microsoft.AspNetCore.SignalR;

namespace FlowHub.Api.Hubs;

/// <summary>Kanał na żywo do UI. Nazwy zdarzeń są kontraktem z frontendem (web/src/api/live.ts).</summary>
public sealed class LiveHub : Hub
{
    public const string Route = "/hubs/live";

    public const string WorkItemChanged = "workItemChanged";
    public const string StepOutput = "stepOutput";
    public const string PermissionRequested = "permissionRequested";
    public const string PermissionResolved = "permissionResolved";
    public const string RepoStatusChanged = "repoStatusChanged";
}

public sealed class SignalRLiveNotifier(IHubContext<LiveHub> hub) : ILiveNotifier
{
    public Task WorkItemChangedAsync(WorkItemDetails item, CancellationToken ct) =>
        hub.Clients.All.SendAsync(LiveHub.WorkItemChanged, item, ct);

    public Task StepOutputAsync(Guid workItemId, Guid stepRunId, string text, CancellationToken ct) =>
        hub.Clients.All.SendAsync(LiveHub.StepOutput, workItemId, stepRunId, text, ct);

    public Task PermissionRequestedAsync(PermissionRequest request, CancellationToken ct) =>
        hub.Clients.All.SendAsync(LiveHub.PermissionRequested, request, ct);

    public Task PermissionResolvedAsync(PermissionRequest request, CancellationToken ct) =>
        hub.Clients.All.SendAsync(LiveHub.PermissionResolved, request, ct);

    public Task RepoStatusChangedAsync(WorkItemRepoStatus status, CancellationToken ct) =>
        hub.Clients.All.SendAsync(LiveHub.RepoStatusChanged, status, ct);
}
