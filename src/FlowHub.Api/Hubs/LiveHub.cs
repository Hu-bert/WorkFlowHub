using Microsoft.AspNetCore.SignalR;

namespace FlowHub.Api.Hubs;

/// <summary>Kanał na żywo do UI: zdarzenia agenta, statusy kroków (od Fazy 3–4).</summary>
public sealed class LiveHub : Hub
{
    public const string Route = "/hubs/live";
}
