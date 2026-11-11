using FlowHub.Application.Integrations;
using FlowHub.Integrations.Fake;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowHub.Integrations;

public static class DependencyInjection
{
    /// <summary>
    /// Rejestruje integracje wg sekcji "Integrations" z appsettings.json.
    /// Prawdziwa implementacja = nowa klasa + jeden wpis w odpowiedniej tabeli poniżej, np.
    /// ["Tfs"] = typeof(TfsPullRequestService), a w konfiguracji "PullRequests": "Tfs".
    /// </summary>
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(IntegrationsOptions.SectionName);
        var options = section.Get<IntegrationsOptions>() ?? new IntegrationsOptions();
        services.Configure<IntegrationsOptions>(section);

        services.AddChosen<ITaskSource>(nameof(options.TaskSource), options.TaskSource, new()
        {
            [IntegrationMode.Fake] = typeof(FakeTaskSource),
        });

        services.AddChosen<IPullRequestService>(nameof(options.PullRequests), options.PullRequests, new()
        {
            [IntegrationMode.Fake] = typeof(FakePullRequestService),
        });

        services.AddChosen<ICiServer>(nameof(options.Ci), options.Ci, new()
        {
            [IntegrationMode.Fake] = typeof(FakeCiServer),
        });

        services.AddChosen<IDeployServer>(nameof(options.Deploy), options.Deploy, new()
        {
            [IntegrationMode.Fake] = typeof(FakeDeployServer),
        });

        return services;
    }

    private static void AddChosen<TPort>(this IServiceCollection services, string setting, string chosen, Dictionary<string, Type> implementations)
        where TPort : class
    {
        var match = implementations.FirstOrDefault(entry => string.Equals(entry.Key, chosen, StringComparison.OrdinalIgnoreCase));

        if (match.Value is null)
        {
            throw new InvalidOperationException(
                $"Integrations:{setting} = '{chosen}' — nie ma takiej implementacji. Dostępne: {string.Join(", ", implementations.Keys)}.");
        }

        services.AddSingleton(typeof(TPort), match.Value);
    }
}
