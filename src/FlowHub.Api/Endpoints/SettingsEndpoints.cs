using FlowHub.Application.Settings;
using FlowHub.Application.Workspace;
using FlowHub.Infrastructure.Settings;

namespace FlowHub.Api.Endpoints;

public sealed record SaveSettingsRequest(Dictionary<string, string?> Values);

public sealed record StorageLocation(string Name, string Path, string Description);

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings");

        // Moduły z polami i wartościami. Sekrety nigdy nie wracają — tylko „ustawiony / nie”.
        group.MapGet("/", (SettingsService settings) => settings.List());

        // Stan połączeń (atrapa / działa / nie działa). ?refresh=true = sprawdź teraz.
        group.MapGet("/status", (bool? refresh, SettingsService settings, CancellationToken ct) => settings.StatusAsync(refresh ?? false, ct));

        // Wartości do wyboru dla pola (środowiska z Octopus, znane modele). Błąd serwera wraca jako czytelny komunikat.
        group.MapGet("/{module}/suggestions", (string module, string key, SettingsService settings, CancellationToken ct) =>
            settings.SuggestAsync(module, key, ct));

        // Gdzie FlowHub trzyma dane na tym komputerze.
        group.MapGet("/storage", (IConfiguration configuration, WorkspacePaths paths) => new StorageLocation[]
        {
            new("Ustawienia", UserSettings.SettingsFile(configuration), "Wartości z tej strony."),
            new("Tokeny (zaszyfrowane)", UserSettings.SecretsFile(configuration), "Czytelne tylko na tym koncie Windows."),
            new("Katalog repo", paths.RepositoriesFile, "Lista repo z adresami oraz nazwami w TeamCity i Octopus."),
            new("Mapy repo", paths.KnowledgeDirectory, "Zapamiętane analizy repozytoriów."),
            new("Zadania (katalogi robocze)", paths.WorkDirectory, "Klony repo i pliki zadań."),
            new("Baza zadań", DatabaseFile(configuration), "Zadania, kroki, tokeny."),
            new("Flow i bloczki", paths.Root, "flows/ i blocks/ — część repozytorium FlowHub."),
        });

        group.MapPut("/{module}", (string module, SaveSettingsRequest request, SettingsService settings) =>
            settings.Save(module, request.Values));

        group.MapPost("/{module}/test", (string module, SettingsService settings, CancellationToken ct) =>
            settings.TestAsync(module, ct));

        return app;
    }

    /// <summary>Ścieżka pliku SQLite z connection stringa („Data Source=…”), względem katalogu aplikacji.</summary>
    private static string DatabaseFile(IConfiguration configuration)
    {
        var source = (configuration.GetConnectionString("FlowHub") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(part => part.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))?["Data Source=".Length..];

        return string.IsNullOrEmpty(source) ? "—" : Path.GetFullPath(source);
    }
}