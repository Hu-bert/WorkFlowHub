using System.Text.Json;
using FlowHub.Application.Settings;
using FlowHub.Domain.Repositories;
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

    /// <summary>Wersja REST API: 6.0 = Azure DevOps Server 2020, 7.0 = Server 2022 i nowsze.</summary>
    public string ApiVersion { get; init; } = "7.0";
}

/// <summary>Gdzie w TFS leży repo: adres projektu (z kolekcją) i nazwa repo.</summary>
public sealed record TfsRepository(string ProjectUrl, string Name)
{
    /// <summary>Adres repo w przeglądarce.</summary>
    public string WebUrl => $"{ProjectUrl}/_git/{Uri.EscapeDataString(Name)}";

    /// <summary>Ścieżka API repo (względem _apis/).</summary>
    public string ApiPath => $"git/repositories/{Uri.EscapeDataString(Name)}";
}

/// <summary>Odczyty REST z TFS (PAT jako Basic z pustym użytkownikiem).</summary>
public sealed class TfsApi(ApiClient client, IOptions<TfsOptions> options, ISecretStore secrets)
{
    private const string GitSegment = "/_git/";

    private TfsOptions Options => options.Value;

    public string CollectionUrl => ApiClient.BaseUrl(Options.CollectionUrl, "Adres kolekcji TFS");

    /// <summary>Adres projektu z Ustawień — używany, gdy adres repo nie wskazuje projektu.</summary>
    public string ProjectUrl => string.IsNullOrWhiteSpace(Options.Project)
        ? CollectionUrl
        : $"{CollectionUrl}/{Uri.EscapeDataString(Options.Project.Trim())}";

    /// <summary>
    /// Repo bywają w różnych projektach kolekcji, więc projekt i nazwę bierzemy z adresu repo
    /// (…/Kolekcja/Projekt/_git/Repo). Adres spoza skonfigurowanej kolekcji ignorujemy — token idzie tylko do niej.
    /// </summary>
    public TfsRepository Locate(CodeRepository repository)
    {
        var url = repository.Url.Trim();
        var git = url.IndexOf(GitSegment, StringComparison.OrdinalIgnoreCase);

        if (git > 0 && url.StartsWith(CollectionUrl + "/", StringComparison.OrdinalIgnoreCase))
        {
            var name = Uri.UnescapeDataString(url[(git + GitSegment.Length)..].Split('/', '?')[0]);
            return new TfsRepository(url[..git].TrimEnd('/'), name);
        }

        return new TfsRepository(ProjectUrl, repository.Name);
    }

    /// <summary>Wersja API dla zasobów „preview” (np. polityki): 7.0 → 7.0-preview.1.</summary>
    public string PreviewVersion => $"{Options.ApiVersion}-preview.1";

    /// <summary>Statusy pull requesta były „preview” do wersji 6.x włącznie.</summary>
    public string PullRequestStatusesVersion =>
        Version.TryParse(Options.ApiVersion, out var version) && version.Major >= 7 ? Options.ApiVersion : PreviewVersion;

    /// <summary>GET {projekt z Ustawień}/_apis/{path}.</summary>
    public Task<JsonDocument?> GetAsync(string path, CancellationToken ct, string? apiVersion = null) =>
        GetAsync(ProjectUrl, path, ct, apiVersion);

    /// <summary>GET {projectUrl}/_apis/{path}. Ścieżka może mieć własne parametry zapytania.</summary>
    public Task<JsonDocument?> GetAsync(string projectUrl, string path, CancellationToken ct, string? apiVersion = null) =>
        client.GetJsonAsync(
            $"{projectUrl}/_apis/{path}{(path.Contains('?') ? '&' : '?')}api-version={apiVersion ?? Options.ApiVersion}",
            ApiClient.Basic("", secrets.Get(TfsOptions.TokenKey)),
            ct);
}