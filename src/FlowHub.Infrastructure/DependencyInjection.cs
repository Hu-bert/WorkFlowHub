using FlowHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowHub.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "FlowHub";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<FlowHubDbContext>((provider, options) =>
            options.UseSqlite(ReadConnectionString(provider)));

        return services;
    }

    private static string ReadConnectionString(IServiceProvider provider) =>
        provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
        ?? throw new InvalidOperationException($"Brak connection stringa '{ConnectionStringName}' w konfiguracji.");
}
