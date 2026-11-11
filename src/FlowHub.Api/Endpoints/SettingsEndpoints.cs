using FlowHub.Application.Settings;

namespace FlowHub.Api.Endpoints;

public sealed record SaveSettingsRequest(Dictionary<string, string?> Values);

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings");

        // Moduły z polami i wartościami. Sekrety nigdy nie wracają — tylko „ustawiony / nie”.
        group.MapGet("/", (SettingsService settings) => settings.List());

        // Stan połączeń (atrapa / działa / nie działa). ?refresh=true = sprawdź teraz.
        group.MapGet("/status", (bool? refresh, SettingsService settings, CancellationToken ct) => settings.StatusAsync(refresh ?? false, ct));

        group.MapPut("/{module}", (string module, SaveSettingsRequest request, SettingsService settings) =>
            settings.Save(module, request.Values));

        group.MapPost("/{module}/test", (string module, SettingsService settings, CancellationToken ct) =>
            settings.TestAsync(module, ct));

        return app;
    }
}