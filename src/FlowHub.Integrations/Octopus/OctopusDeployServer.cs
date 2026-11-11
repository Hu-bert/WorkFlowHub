using System.Net.Http.Headers;
using System.Text.Json;
using FlowHub.Application.Integrations;
using FlowHub.Application.Settings;
using FlowHub.Domain.Repositories;
using FlowHub.Integrations.Http;
using Microsoft.Extensions.Options;

namespace FlowHub.Integrations.Octopus;

/// <summary>Połączenie z Octopus Deploy ("Octopus" w ustawieniach; klucz = sekret "Octopus:ApiKey").</summary>
public sealed class OctopusOptions
{
    public const string SectionName = "Octopus";
    public const string ApiKeyKey = "Octopus:ApiKey";

    public string? BaseUrl { get; init; }

    /// <summary>Id przestrzeni, np. Spaces-1. Puste = adresy bez przestrzeni (przestrzeń domyślna).</summary>
    public string? Space { get; init; }
}

/// <summary>
/// Release i wdrożenia z Octopus Deploy (GET /api/…, nagłówek X-Octopus-ApiKey).
/// Projekt bierzemy z katalogu repo (pole „Octopus”; domyślnie nazwa repo). Adresy i pola wg dokumentacji
/// i kodu oficjalnego klienta; NIE sprawdzone na prawdziwym serwerze.
/// </summary>
public sealed class OctopusDeployServer(ApiClient client, IOptions<OctopusOptions> options, ISecretStore secrets) : IDeployServer
{
    /// <summary>Ile ostatnich release przeglądamy, szukając tego z brancha.</summary>
    private const int ReleaseHistory = 30;

    private string BaseUrl => ApiClient.BaseUrl(options.Value.BaseUrl, "Adres Octopus");

    private string Api => string.IsNullOrWhiteSpace(options.Value.Space) ? $"{BaseUrl}/api" : $"{BaseUrl}/api/{options.Value.Space.Trim()}";

    public async Task<DeploymentOverview?> GetOverviewAsync(CodeRepository repository, string branch, IReadOnlyList<BuildStatus> builds, CancellationToken ct)
    {
        var slug = repository.DeployProject ?? repository.Name;
        using var project = await GetAsync($"projects/{Uri.EscapeDataString(slug)}", ct);
        if (project is null || Json.Text(project.RootElement, "Id") is not { } projectId)
        {
            return null;
        }

        using var progression = await GetAsync($"progression/{projectId}?releaseHistoryCount={ReleaseHistory}", ct);
        if (progression is null)
        {
            return null;
        }

        var portal = $"{BaseUrl}/app#/{Json.Text(project.RootElement, "SpaceId")}/projects/{Json.Text(project.RootElement, "Slug") ?? slug}";
        var isDefaultBranch = string.Equals(branch, repository.DefaultBranch, StringComparison.OrdinalIgnoreCase);

        return OctopusMapper.Overview(progression.RootElement, Json.Text(project.RootElement, "Name") ?? slug, branch, builds, isDefaultBranch, portal);
    }

    public async Task<IReadOnlyList<DeployProjectInfo>> ListProjectsAsync(CancellationToken ct)
    {
        using var projects = await GetAsync("projects/all", ct);

        return projects is null
            ? []
            : Json.Array(projects.RootElement)
                .Where(project => !Json.Flag(project, "IsDisabled"))
                .Select(project => new DeployProjectInfo(Json.Text(project, "Slug") ?? "", Json.Text(project, "Name") ?? ""))
                .Where(project => project.Slug.Length > 0)
                .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    public async Task<IReadOnlyList<string>> ListEnvironmentsAsync(CancellationToken ct)
    {
        using var environments = await GetAsync("environments/all", ct);

        return environments is null
            ? []
            : Json.Array(environments.RootElement)
                .OrderBy(environment => Json.Number(environment, "SortOrder"))
                .Select(environment => Json.Text(environment, "Name") ?? "?")
                .ToList();
    }

    /// <summary>Nazwa zalogowanego użytkownika — do testu połączenia (adres bez przestrzeni).</summary>
    public async Task<string?> CurrentUserAsync(CancellationToken ct)
    {
        using var me = await client.GetJsonAsync($"{BaseUrl}/api/users/me", Authorize, ct);
        return me is null ? null : Json.Text(me.RootElement, "DisplayName") ?? Json.Text(me.RootElement, "Username") ?? "?";
    }

    private Task<JsonDocument?> GetAsync(string path, CancellationToken ct) => client.GetJsonAsync($"{Api}/{path}", Authorize, ct);

    private Action<HttpRequestHeaders> Authorize => ApiClient.Header("X-Octopus-ApiKey", secrets.Get(OctopusOptions.ApiKeyKey));
}

public static class OctopusMapper
{
    /// <summary>
    /// Z „progression” projektu: środowiska oraz release, który powstał z brancha, i jego wdrożenia.
    /// Gdy żaden release nie pasuje do brancha, środowiska są puste („—”) — nie pokazujemy cudzego release.
    /// </summary>
    public static DeploymentOverview Overview(
        JsonElement progression, string project, string branch, IReadOnlyList<BuildStatus> builds, bool isDefaultBranch, string portalUrl)
    {
        var environments = Json.Items(progression, "Environments")
            .Select(environment => (Id: Json.Text(environment, "Id") ?? "", Name: Json.Text(environment, "Name") ?? "?"))
            .ToList();

        var releases = Json.Items(progression, "Releases").Cast<JsonElement?>().ToList();
        var match = releases.FirstOrDefault(entry => Matches(Json.Object(entry!.Value, "Release"), branch, builds))
            ?? (isDefaultBranch ? releases.FirstOrDefault() : null);

        var version = match is { } found ? Json.Text(Json.Object(found, "Release"), "Version") : null;

        return new DeploymentOverview(
            project,
            version,
            environments.Select(environment => Deployment(match, environment.Id, environment.Name, version, portalUrl)).ToList());
    }

    /// <summary>
    /// Release pasuje do brancha, gdy: build information wskazuje ten branch, referencja Git kończy się nim,
    /// wersja zawiera nazwę brancha albo wersja jest w numerze builda z tego brancha (np. build „1.30.0 #643” → release 1.30.0).
    /// </summary>
    public static bool Matches(JsonElement? release, string branch, IReadOnlyList<BuildStatus> builds)
    {
        if (release is not { } value || Json.Text(value, "Version") is not { Length: > 0 } version)
        {
            return false;
        }

        var gitRef = Json.Text(Json.Object(value, "VersionControlReference"), "GitRef");
        var fromBuildInformation = Json.Items(value, "BuildInformation").Any(build => SameBranch(Json.Text(build, "Branch"), branch));

        return fromBuildInformation
            || SameBranch(gitRef, branch)
            || version.Contains(Slug(branch), StringComparison.OrdinalIgnoreCase)
            || builds.Any(build => build.Number.Split(' ', '#').Any(token => token.Length > 0 && string.Equals(token, version, StringComparison.OrdinalIgnoreCase)));
    }

    private static EnvironmentDeployment Deployment(JsonElement? release, string environmentId, string name, string? version, string portalUrl)
    {
        var deployments = release is { } entry ? Json.Object(entry, "Deployments") : null;
        var items = deployments is { } all && all.TryGetProperty(environmentId, out var list) ? Json.Array(list).ToList() : [];
        if (items.Count == 0)
        {
            return new EnvironmentDeployment(name, null, DeploymentState.None, null, null);
        }

        JsonElement current = items.FirstOrDefault(item => Json.Flag(item, "IsCurrent"), items[0]);
        var deployedVersion = Json.Text(current, "ReleaseVersion") ?? version;
        DateTime? at = DateTime.TryParse(Json.Text(current, "CompletedTime") ?? Json.Text(current, "QueueTime"), out var date) ? date.ToUniversalTime() : null;

        return new EnvironmentDeployment(
            name, deployedVersion, State(Json.Text(current, "State")), at,
            $"{portalUrl}/deployments/releases/{deployedVersion}/deployments/{Json.Text(current, "DeploymentId")}");
    }

    /// <summary>TaskState z Octopus: Queued, Executing, Failed, Canceled, TimedOut, Success, Cancelling.</summary>
    public static DeploymentState State(string? state) => state switch
    {
        "Success" => DeploymentState.Success,
        "Queued" => DeploymentState.Queued,
        "Executing" or "Cancelling" => DeploymentState.Executing,
        "Failed" or "TimedOut" or "Canceled" => DeploymentState.Failed,
        _ => DeploymentState.None,
    };

    private static bool SameBranch(string? reference, string branch) =>
        reference is { Length: > 0 }
        && (string.Equals(reference, branch, StringComparison.OrdinalIgnoreCase)
            || reference.EndsWith("/" + branch, StringComparison.OrdinalIgnoreCase));

    /// <summary>Nazwa brancha tak, jak trafia do numeru wersji: feature/ZR-1_opis → feature-ZR-1-opis.</summary>
    private static string Slug(string branch) =>
        new(branch.Select(character => char.IsLetterOrDigit(character) || character == '.' ? character : '-').ToArray());
}
