using FlowHub.Application.Integrations;
using FlowHub.Application.Settings;
using FlowHub.Domain;
using FlowHub.Integrations.Fake;
using FlowHub.Integrations.Http;
using FlowHub.Integrations.Jira;
using FlowHub.Integrations.Settings;
using FlowHub.Integrations.Tfs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FlowHub.Integrations;

public static class DependencyInjection
{
    /// <summary>
    /// Rejestruje integracje. Która implementacja działa, decyduje sekcja "Integrations" (strona Ustawienia) —
    /// wybór jest czytany przy każdym użyciu, więc przełączenie Atrapa ↔ prawdziwa nie wymaga restartu.
    /// Nowa implementacja = nowa klasa + jeden wpis w odpowiedniej tabeli poniżej.
    /// </summary>
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IntegrationsOptions>(configuration.GetSection(IntegrationsOptions.SectionName));
        services.Configure<TfsOptions>(configuration.GetSection(TfsOptions.SectionName));
        services.Configure<JiraOptions>(configuration.GetSection(JiraOptions.SectionName));

        services.AddHttpClient();
        services.AddSingleton<ApiClient>();
        services.AddSingleton<TfsApi>();

        services.AddChosen<ITaskSource>(nameof(IntegrationsOptions.TaskSource), options => options.TaskSource, new()
        {
            [IntegrationMode.Fake] = typeof(FakeTaskSource),
            [IntegrationMode.Jira] = typeof(JiraTaskSource),
        });

        services.AddChosen<IPullRequestService>(nameof(IntegrationsOptions.PullRequests), options => options.PullRequests, new()
        {
            [IntegrationMode.Fake] = typeof(FakePullRequestService),
            [IntegrationMode.Tfs] = typeof(TfsPullRequestService),
        });

        services.AddChosen<IRepositoryDirectory>(nameof(IntegrationsOptions.RepositoryDirectory), options => options.RepositoryDirectory, new()
        {
            [IntegrationMode.Fake] = typeof(FakeRepositoryDirectory),
            [IntegrationMode.Tfs] = typeof(TfsRepositoryDirectory),
        });

        services.AddChosen<ICiServer>(nameof(IntegrationsOptions.Ci), options => options.Ci, new()
        {
            [IntegrationMode.Fake] = typeof(FakeCiServer),
        });

        services.AddChosen<IDeployServer>(nameof(IntegrationsOptions.Deploy), options => options.Deploy, new()
        {
            [IntegrationMode.Fake] = typeof(FakeDeployServer),
        });

        // Karty na stronie „Ustawienia”.
        services.AddSingleton<ISettingsModule, TfsSettingsModule>();
        services.AddSingleton<ISettingsModule, JiraSettingsModule>();
        services.AddSingleton<ISettingsModule, TeamCitySettingsModule>();
        services.AddSingleton<ISettingsModule, OctopusSettingsModule>();

        return services;
    }

    private static void AddChosen<TPort>(
        this IServiceCollection services,
        string setting,
        Func<IntegrationsOptions, string> chosen,
        Dictionary<string, Type> implementations)
        where TPort : class
    {
        foreach (var implementation in implementations.Values)
        {
            services.AddSingleton(implementation);
        }

        services.AddTransient(provider =>
        {
            var name = chosen(provider.GetRequiredService<IOptions<IntegrationsOptions>>().Value);
            var match = implementations.FirstOrDefault(entry => string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase));

            return match.Value is null
                ? throw new DomainException($"Integrations:{setting} = '{name}' — nie ma takiej implementacji. Dostępne: {string.Join(", ", implementations.Keys)}.")
                : (TPort)provider.GetRequiredService(match.Value);
        });
    }
}