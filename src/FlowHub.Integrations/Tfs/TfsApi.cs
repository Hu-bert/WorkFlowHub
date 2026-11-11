using System.Text.Json;
using FlowHub.Application.Settings;
using FlowHub.Integrations.Http;
using Microsoft.Extensions.Options;

namespace FlowHub.Integrations.Tfs;

/// <summary>Połączenie z TFS / Azure DevOps Server ("Tfs" w ustawieniach; token = sekret "Tfs:Token").</summary>
public sealed class TfsOptions
{
    public const string SectionName = "Tfs";
    public const string TokenKey = "Tfs:Token";

    /// <summary>Adres kolekcji, np. https://tfs.firma.pl/tfs/DefaultCollection.</summary>
    public string? CollectionUrl { get; init; }

    public string? Project { get; init; }

    /// <summary>Wersja REST API serwera: 6.0 = Azure DevOps Server 2020, 7.0 = 2022.</summary>
    public string ApiVersion { get; init; } = "6.0";
}

/// <summary>
/// Odczyty REST z TFS (PAT jako Basic z pustym użytkownikiem).
/// NIE sprawdzone na prawdziwym serwerze — adresy wg dokumentacji Azure DevOps REST API.
/// </summary>
public sealed class TfsApi(ApiClient client, IOptions<TfsOptions> options, ISecretStore secrets)
{
    private TfsOptions Options => options.Value;

    public string CollectionUrl => ApiClient.BaseUrl(Options.CollectionUrl, "Adres kolekcji TFS");

    /// <summary>Adres projektu w przeglądarce — baza linków do repo i pull requestów.</summary>
    public string ProjectUrl => string.IsNullOrWhiteSpace(Options.Project)
        ? CollectionUrl
        : $"{CollectionUrl}/{Uri.EscapeDataString(Options.Project.Trim())}";

    /// <summary>GET {kolekcja}/{projekt}/_apis/{path}. Ścieżka może mieć własne parametry zapytania.</summary>
    public Task<JsonDocument?> GetAsync(string path, CancellationToken ct, string? apiVersion = null) =>
        client.GetJsonAsync(
            $"{ProjectUrl}/_apis/{path}{(path.Contains('?') ? '&' : '?')}api-version={apiVersion ?? Options.ApiVersion}",
            ApiClient.Basic("", secrets.Get(TfsOptions.TokenKey)),
            ct);
}
