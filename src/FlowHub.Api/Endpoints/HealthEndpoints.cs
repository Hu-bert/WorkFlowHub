namespace FlowHub.Api.Endpoints;

public sealed record HealthResponse(string Status, string Version);

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => new HealthResponse("ok", AppVersion()));
        return app;
    }

    private static string AppVersion() =>
        typeof(HealthEndpoints).Assembly.GetName().Version?.ToString() ?? "unknown";
}
