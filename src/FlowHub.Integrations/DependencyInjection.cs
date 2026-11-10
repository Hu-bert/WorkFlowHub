using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowHub.Integrations;

public static class DependencyInjection
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IntegrationsOptions>(configuration.GetSection(IntegrationsOptions.SectionName));

        // Implementacje (Fake / prawdziwe) rejestrujemy tutaj wg IntegrationsOptions od Fazy 7.
        return services;
    }
}
