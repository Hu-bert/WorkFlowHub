using FlowHub.Application.Settings;
using FlowHub.Integrations.Http;
using FlowHub.Integrations.Jira;
using FlowHub.Integrations.Tfs;
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
    public string Description => "Pull requesty brancha zadania i lista repozytoriów do wyboru. Token: Personal Access Token z prawem odczytu kodu.";
    public bool CanTest => true;

    public IReadOnlyList<SettingField> Fields { get; } =
    [
        new("Integrations:PullRequests", "Źródło pull requestów", SettingKind.Choice, Choices: [Choices.Fake, new(IntegrationMode.Tfs, "TFS (REST API)")]),
        new("Integrations:RepositoryDirectory", "Lista repozytoriów do wyboru", SettingKind.Choice, Choices: [Choices.Fake, new(IntegrationMode.Tfs, "TFS (REST API)")]),
        new("Tfs:CollectionUrl", "Adres kolekcji", Hint: "Np. https://tfs.firma.pl/tfs/DefaultCollection"),
        new("Tfs:Project", "Projekt"),
        new("Tfs:ApiVersion", "Wersja API", Hint: "6.0 = Azure DevOps Server 2020, 7.0 = 2022."),
        new(TfsOptions.TokenKey, "Token (PAT)", SettingKind.Secret),
    ];

    public async Task<ConnectionTest> TestAsync(CancellationToken ct)
    {
        using var repositories = await api.GetAsync("git/repositories", ct);

        return repositories is null
            ? new ConnectionTest(false, "Nie znaleziono projektu — sprawdź adres kolekcji i nazwę projektu.")
            : new ConnectionTest(true, $"Połączono. Repozytoriów w projekcie: {TfsMapper.Items(repositories.RootElement).Count()}.");
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
            : new ConnectionTest(true, $"Połączono jako {TfsMapper.Text(me.RootElement, "displayName") ?? "?"}.");
    }
}

public sealed class TeamCitySettingsModule(ApiClient client, ISettingsStore settings, ISecretStore secrets) : ISettingsModule
{
    public const string TokenKey = "TeamCity:Token";

    public string Id => "teamcity";
    public string Title => "TeamCity";
    public string Icon => "mdi-hammer-wrench";
    public string Description => "Buildy i zmiany z brancha zadania. Na razie dane pochodzą z atrapy — tutaj możesz już zapisać adres i token oraz sprawdzić połączenie.";
    public bool CanTest => true;

    public IReadOnlyList<SettingField> Fields { get; } =
    [
        new("Integrations:Ci", "Źródło buildów", SettingKind.Choice, "Prawdziwy klient TeamCity: do dopisania po sprawdzeniu API na serwerze.", [Choices.Fake]),
        new("TeamCity:BaseUrl", "Adres TeamCity", Hint: "Np. https://teamcity.firma.pl"),
        new(TokenKey, "Token dostępu", SettingKind.Secret),
    ];

    public async Task<ConnectionTest> TestAsync(CancellationToken ct)
    {
        var baseUrl = ApiClient.BaseUrl(settings.Get("TeamCity:BaseUrl"), "Adres TeamCity");
        using var server = await client.GetJsonAsync($"{baseUrl}/app/rest/server", ApiClient.Bearer(secrets.Get(TokenKey)), ct);

        return server is null
            ? new ConnectionTest(false, "Serwer nie zna adresu /app/rest/server — sprawdź adres TeamCity.")
            : new ConnectionTest(true, $"Połączono. TeamCity {TfsMapper.Text(server.RootElement, "version") ?? "?"}.");
    }
}

public sealed class OctopusSettingsModule(ApiClient client, ISettingsStore settings, ISecretStore secrets) : ISettingsModule
{
    public const string ApiKeyKey = "Octopus:ApiKey";

    public string Id => "octopus";
    public string Title => "Octopus Deploy";
    public string Icon => "mdi-rocket-launch-outline";
    public string Description => "Release i wdrożenia na środowiska. Na razie dane pochodzą z atrapy — tutaj możesz już zapisać adres i klucz oraz sprawdzić połączenie.";
    public bool CanTest => true;

    public IReadOnlyList<SettingField> Fields { get; } =
    [
        new("Integrations:Deploy", "Źródło wdrożeń", SettingKind.Choice, "Prawdziwy klient Octopus: do dopisania po sprawdzeniu API na serwerze.", [Choices.Fake]),
        new("Octopus:BaseUrl", "Adres Octopus", Hint: "Np. https://octopus.firma.pl"),
        new(ApiKeyKey, "Klucz API", SettingKind.Secret),
    ];

    public async Task<ConnectionTest> TestAsync(CancellationToken ct)
    {
        var baseUrl = ApiClient.BaseUrl(settings.Get("Octopus:BaseUrl"), "Adres Octopus");
        using var me = await client.GetJsonAsync($"{baseUrl}/api/users/me", ApiClient.Header("X-Octopus-ApiKey", secrets.Get(ApiKeyKey)), ct);

        return me is null
            ? new ConnectionTest(false, "Serwer nie zna adresu /api/users/me — sprawdź adres Octopus.")
            : new ConnectionTest(true, $"Połączono jako {TfsMapper.Text(me.RootElement, "DisplayName") ?? TfsMapper.Text(me.RootElement, "Username") ?? "?"}.");
    }
}
