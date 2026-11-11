using FlowHub.Application.Engine;
using FlowHub.Application.WorkItems;
using Microsoft.AspNetCore.SignalR;

namespace FlowHub.Api.Hubs;

/// <summary>Kanał na żywo do UI. Nazwy zdarzeń są kontraktem z frontendem (web/src/api/live.ts).</summary>
public sealed class LiveHub : Hub
{
    public const string Route = "/hubs/live";

    public const string WorkItemChanged = "workItemChanged";
    public const string StepOutput = "stepOutput";
}

public sealed class SignalRLiveNotifier(IHubContext<LiveHub> hub) : ILiveNotifier
{
    public Task WorkItemChangedAsync(WorkItemDetails item, CancellationToken ct) =>
        hub.Clients.All.SendAsync(LiveHub.WorkItemChanged, item, ct);

    public Task StepOutputAsync(Guid workItemId, Guid stepRunId, string text, CancellationToken ct) =>
        hub.Clients.All.SendAsync(LiveHub.StepOutput, workItemId, stepRunId, text, ct);
}
