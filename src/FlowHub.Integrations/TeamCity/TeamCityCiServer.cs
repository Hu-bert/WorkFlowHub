using System.Globalization;
using System.Text.Json;
using FlowHub.Application.Integrations;
using FlowHub.Application.Settings;
using FlowHub.Domain;
using FlowHub.Domain.Repositories;
using FlowHub.Integrations.Http;
using Microsoft.Extensions.Options;

namespace FlowHub.Integrations.TeamCity;

/// <summary>Połączenie z TeamCity ("TeamCity" w ustawieniach; token = sekret "TeamCity:Token").</summary>
public sealed class TeamCityOptions
{
    public const string SectionName = "TeamCity";
    public const string TokenKey = "TeamCity:Token";

    public string? BaseUrl { get; init; }
}

/// <summary>
/// Buildy z TeamCity przez REST API (GET /app/rest/…, nagłówek Authorization: Bearer).
/// Build configuration bierzemy z katalogu repo (pole „TeamCity”). Adresy i pola wg dokumentacji REST API;
/// NIE sprawdzone na prawdziwym serwerze.
/// </summary>
public sealed class TeamCityCiServer(ApiClient client, IOptions<TeamCityOptions> options, ISecretStore secrets) : ICiServer
{
    private const string BuildFields = "build(id,number,status,state,statusText,branchName,webUrl,buildTypeId,testOccurrences(count,passed,failed))";

    private string Api => ApiClient.BaseUrl(options.Value.BaseUrl, "Adres TeamCity") + "/app/rest";

    public async Task<IReadOnlyList<BuildStatus>> GetBuildsWithChangesAsync(CodeRepository repository, string branch, CancellationToken ct)
    {
        var builds = new List<BuildStatus>();

        foreach (var configuration in repository.CiBuildConfigurations)
        {
            using var latest = await LatestBuildAsync(configuration, branch, ct);
            if (latest is null || Json.Items(latest.RootElement, "build").Cast<JsonElement?>().FirstOrDefault() is not { } build)
            {
                continue;
            }

            var failedTests = Json.Number(Json.Object(build, "testOccurrences"), "failed") > 0
                ? await FailedTestsAsync(Json.Text(build, "id")!, ct)
                : [];

            builds.Add(TeamCityMapper.Build(build, configuration, failedTests));
        }

        return builds;
    }

    /// <summary>Zmiany (commity) z najnowszego builda brancha — TeamCity nie filtruje zmian po branchu, tylko po buildzie.</summary>
    public async Task<IReadOnlyList<CiChange>> GetChangesAsync(CodeRepository repository, string branch, CancellationToken ct)
    {
        foreach (var configuration in repository.CiBuildConfigurations)
        {
            using var latest = await LatestBuildAsync(configuration, branch, ct);
            if (latest is null || Json.Items(latest.RootElement, "build").Cast<JsonElement?>().FirstOrDefault() is not { } build)
            {
                continue;
            }

            using var changes = await GetAsync(
                $"changes?locator={Uri.EscapeDataString($"build:(id:{Json.Text(build, "id")})")}&fields=change(version,username,comment,date)", ct);

            return changes is null ? [] : TeamCityMapper.Changes(changes.RootElement);
        }

        return [];
    }

    public async Task<IReadOnlyList<CiConfiguration>> ListConfigurationsAsync(CancellationToken ct)
    {
        using var types = await GetAsync("buildTypes?fields=buildType(id,name,projectName)", ct);

        return types is null
            ? []
            : Json.Items(types.RootElement, "buildType")
                .Select(type => new CiConfiguration(Json.Text(type, "id") ?? "", Json.Text(type, "name") ?? "", Json.Text(type, "projectName")))
                .Where(type => type.Id.Length > 0)
                .OrderBy(type => type.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    /// <summary>Wersja serwera — do testu połączenia.</summary>
    public async Task<string?> ServerVersionAsync(CancellationToken ct)
    {
        using var server = await GetAsync("server", ct);
        return server is null ? null : Json.Text(server.RootElement, "version") ?? "?";
    }

    /// <summary>
    /// Najnowszy build konfiguracji na branchu. Różne wersje TeamCity różnie traktują filtry (prawdziwy serwer
    /// odrzucił kodem 400 połączenie state:any z running:any), więc próbujemy od najpełniejszego do najprostszego.
    /// </summary>
    private async Task<JsonDocument?> LatestBuildAsync(string configuration, string branch, CancellationToken ct)
    {
        DomainException? rejected = null;

        foreach (var locator in BuildLocators(configuration, branch))
        {
            try
            {
                return await GetAsync($"builds?locator={Uri.EscapeDataString(locator)}&fields={BuildFields}", ct);
            }
            catch (DomainException error) when (error.Message.Contains(" 400 "))
            {
                rejected = error;
            }
        }

        throw rejected!;
    }

    /// <summary>
    /// 1) także buildy trwające i w kolejce; 2) bez filtra stanu (defaultFilter:false i tak nie odcina trwających);
    /// 3) najprostszy: sama nazwa brancha tak, jak pokazuje ją TeamCity.
    /// </summary>
    public static IReadOnlyList<string> BuildLocators(string configuration, string branch) =>
    [
        $"defaultFilter:false,buildType:(id:{configuration}),branch:(name:{branch}),state:any,count:1",
        $"defaultFilter:false,buildType:(id:{configuration}),branch:(name:{branch}),count:1",
        $"buildType:(id:{configuration}),branch:{branch},count:1",
    ];

    private async Task<IReadOnlyList<string>> FailedTestsAsync(string buildId, CancellationToken ct)
    {
        var locator = $"build:(id:{buildId}),status:FAILURE,count:25";
        using var tests = await GetAsync($"testOccurrences?locator={Uri.EscapeDataString(locator)}&fields=testOccurrence(name)", ct);

        return tests is null
            ? []
            : Json.Items(tests.RootElement, "testOccurrence").Select(test => Json.Text(test, "name") ?? "?").ToList();
    }

    private Task<JsonDocument?> GetAsync(string path, CancellationToken ct) =>
        client.GetJsonAsync($"{Api}/{path}", ApiClient.Bearer(secrets.Get(TeamCityOptions.TokenKey)), ct);
}

public static class TeamCityMapper
{
    public static BuildStatus Build(JsonElement build, string configuration, IReadOnlyList<string> failedTests) => new(
        Json.Text(build, "buildTypeId") ?? configuration,
        Json.Text(build, "number") ?? "?",
        State(Json.Text(build, "state"), Json.Text(build, "status")),
        failedTests,
        Json.Text(build, "webUrl") ?? "",
        Json.Text(build, "statusText"),
        Json.Text(build, "branchName"));

    public static IReadOnlyList<CiChange> Changes(JsonElement changes) =>
        Json.Items(changes, "change")
            .Select(change =>
            {
                var version = Json.Text(change, "version") ?? "";

                return new CiChange(
                    version.Length > 8 ? version[..8] : version,
                    Json.Text(change, "username") ?? "?",
                    (Json.Text(change, "comment") ?? "").Trim().Split('\n')[0],
                    Date(Json.Text(change, "date")));
            })
            .ToList();

    /// <summary>state: queued / running / finished; status: SUCCESS / FAILURE / UNKNOWN (anulowany).</summary>
    public static BuildState State(string? state, string? status) => state?.ToLowerInvariant() switch
    {
        "queued" => BuildState.Queued,
        "running" when status != "FAILURE" => BuildState.Running,
        _ => status == "SUCCESS" ? BuildState.Success : BuildState.Failed,
    };

    /// <summary>Daty TeamCity: 20240905T151059+0200.</summary>
    public static DateTime Date(string? text) =>
        DateTimeOffset.TryParseExact(text, "yyyyMMdd'T'HHmmsszzz", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.UtcDateTime
            : default;
}
