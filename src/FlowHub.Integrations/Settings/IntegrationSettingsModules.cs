using FlowHub.Application.Integrations;
using FlowHub.Application.Settings;
using FlowHub.Integrations.Http;
using FlowHub.Integrations.Jira;
using FlowHub.Integrations.Octopus;
using FlowHub.Integrations.TeamCity;
using FlowHub.Integrations.Tfs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FlowHub.Integrations.Settings;

// Karty na stronie „Ustawienia” dla systemów firmowych. Każda: źródło danych (Atrapa / prawdziwe),
// adres, token (sekret) i test połączenia. Nowy system = nowa klasa tutaj + linia w DependencyInjection.

internal static class Choices
{
    public static SettingChoice Fake { get; } = new(IntegrationMode.Fake, "Atrapa (dane przykładowe)");
}

public sealed class TfsSettingsModule(TfsApi api) : ISettingsModule
{
    public string Id => "tfs";
    public string Title => "TFS / Azure DevOps Server";
    public string Icon => "mdi-source-pull";
    public string Description => "Pull requesty brancha i lista repozytoriów do wyboru. Token: Personal Access Token z prawem odczytu kodu (Code: Read).";
    public bool CanTest => true;

    public IReadOnlyList<SettingField> Fields { get; } =
    [
        new("Integrations:PullRequests", "Źródło pull requestów", SettingKind.Choice, Choices: [Choices.Fake, new(IntegrationMode.Tfs, "TFS (REST API)")]),
        new("Integrations:RepositoryDirectory", "Lista repozytoriów do wyboru", SettingKind.Choice, Choices: [Choices.Fake, new(IntegrationMode.Tfs, "TFS (REST API)")]),
        new("Tfs:CollectionUrl", "Adres kolekcji", Hint: "Np. https://tfs.firma.pl/tfs/DefaultCollection"),
        new("Tfs:Project", "Projekt domyślny", Hint: "Używany do listy repozytoriów. Projekt konkretnego repo jest brany z jego adresu."),
        new("Tfs:ApiVersion", "Wersja API", Hint: "7.0 = Azure DevOps Server 2022 i nowsze (także 25H2); 6.0 = Server 2020."),
        new(TfsOptions.TokenKey, "Token (PAT)", SettingKind.Secret),
    ];

    public async Task<ConnectionTest> TestAsync(CancellationToken ct)
    {
        using var repositories = await api.GetAsync("git/repositories", ct);

        return repositories is null
            ? new ConnectionTest(false, "Nie znaleziono projektu — sprawdź adres kolekcji i nazwę projektu.")
            : new ConnectionTest(true, $"Połączono. Repozytoriów w projekcie: {Json.Items(repositories.RootElement, "value").Count()}.");
    }
}

public sealed class JiraSettingsModule(ApiClient client, JiraTaskSource jira, IOptions<JiraOptions> options) : ISettingsModule
{
    public string Id => "jira";
    public string Title => "Jira";
    public string Icon => "mdi-ticket-outline";
    public string Description => "Przycisk „Pobierz” w nowym zadaniu. Stara Jira bez API: zostaw atrapę i wklejaj treść ręcznie.";
    public bool CanTest => true;

    public IReadOnlyList<SettingField> Fields { get; } =
    [
        new("Integrations:TaskSource", "Źródło zadań", SettingKind.Choice, Choices: [Choices.Fake, new(IntegrationMode.Jira, "Jira (REST API)")]),
        new("Jira:BaseUrl", "Adres Jiry", Hint: "Np. https://jira.firma.pl"),
        new("Jira:Email", "E-mail konta", Hint: "Tylko Jira Cloud. Puste = Jira Server / Data Center (token PAT)."),
        new(JiraOptions.TokenKey, "Token", SettingKind.Secret),
    ];

    public async Task<ConnectionTest> TestAsync(CancellationToken ct)
    {
        var baseUrl = ApiClient.BaseUrl(options.Value.BaseUrl, "Adres Jiry");
        using var me = await client.GetJsonAsync($"{baseUrl}/rest/api/2/myself", jira.Authorize(), ct);

        return me is null
            ? new ConnectionTest(false, "Serwer nie zna adresu /rest/api/2/myself — sprawdź adres Jiry.")
            : new ConnectionTest(true, $"Połączono jako {Json.Text(me.RootElement, "displayName") ?? "?"}.");
    }
}

public sealed class TeamCitySettingsModule(TeamCityCiServer teamCity) : ISettingsModule
{
    public string Id => "teamcity";
    public string Title => "TeamCity / buildy";
    public string Icon => "mdi-hammer-wrench";

    public string Description =>
        "Skąd brać buildy brancha. „Statusy z TFS” czyta wyniki, które TeamCity zapisuje przy commicie i pull requeście — działa bez mapowania. "
        + "„TeamCity” daje więcej (testy, zmiany), ale wymaga id build configuration przy repo w katalogu.";

    public bool CanTest => true;

    public IReadOnlyList<SettingField> Fields { get; } =
    [
        new("Integrations:Ci", "Źródło buildów", SettingKind.Choice, Choices:
        [
            Choices.Fake,
            new(IntegrationMode.Tfs, "Statusy buildów z TFS (wymaga tylko połączenia z TFS)"),
            new(IntegrationMode.TeamCity, "TeamCity (REST API)"),
        ]),
        new("TeamCity:BaseUrl", "Adres TeamCity", Hint: "Np. https://teamcity.firma.pl — używany też do linków „przejdź do TeamCity”."),
        new(TeamCityOptions.TokenKey, "Token dostępu", SettingKind.Secret),
        new("RepoStatus:PullRequestBuildBranch", "Branch builda pull requesta", Hint: "Jak CI nazywa branch PR; {id} = numer PR. Np. pull/{id}/merge. Puste = nie szukaj buildów PR."),
    ];

    public async Task<ConnectionTest> TestAsync(CancellationToken ct) =>
        await teamCity.ServerVersionAsync(ct) is { } version
            ? new ConnectionTest(true, $"Połączono. TeamCity {version}.")
            : new ConnectionTest(false, "Serwer nie zna adresu /app/rest/server — sprawdź adres TeamCity.");
}

public sealed class OctopusSettingsModule(OctopusDeployServer octopus, IServiceProvider services) : ISettingsModule
{
    public string Id => "octopus";
    public string Title => "Octopus Deploy / środowiska";
    public string Icon => "mdi-rocket-launch-outline";

    public string Description =>
        "Release i wdrożenia. Projekt Octopus ustawiasz przy repo w katalogu (domyślnie = nazwa repo). "
        + "Tu wybierasz też, które środowiska pokazywać i które liczy się jako „gotowe”.";

    public bool CanTest => true;

    public IReadOnlyList<SettingField> Fields { get; } =
    [
        new("Integrations:Deploy", "Źródło wdrożeń", SettingKind.Choice, Choices: [Choices.Fake, new(IntegrationMode.Octopus, "Octopus (REST API)")]),
        new("Octopus:BaseUrl", "Adres Octopus", Hint: "Np. https://octopus.firma.pl — używany też do linków „przejdź do Octopus”."),
        new("Octopus:Space", "Przestrzeń (Space)", Hint: "Id przestrzeni, np. Spaces-1. Puste = przestrzeń domyślna."),
        new(OctopusOptions.ApiKeyKey, "Klucz API", SettingKind.Secret),
        new("RepoStatus:ReadyEnvironment", "Środowisko domyślne", Hint: "Od niego repo liczy się jako „gotowe”. Lista pochodzi z serwera wdrożeń.", Suggest: true),
        new("RepoStatus:Environments", "Pokazywane środowiska", Hint: "Wybierz z listy z serwera. Puste = wszystkie.", Suggest: true, Multiple: true),
    ];

    public async Task<ConnectionTest> TestAsync(CancellationToken ct)
    {
        if (await octopus.CurrentUserAsync(ct) is not { } user)
        {
            return new ConnectionTest(false, "Serwer nie zna adresu /api/users/me — sprawdź adres Octopus.");
        }

        var environments = await octopus.ListEnvironmentsAsync(ct);
        return new ConnectionTest(true, $"Połączono jako {user}. Środowiska: {(environments.Count == 0 ? "brak" : string.Join(", ", environments))}.");
    }

    /// <summary>Środowiska z aktualnie wybranego źródła wdrożeń (atrapa albo Octopus).</summary>
    public Task<IReadOnlyList<string>> SuggestAsync(string key, CancellationToken ct) =>
        services.GetRequiredService<IDeployServer>().ListEnvironmentsAsync(ct);
}
