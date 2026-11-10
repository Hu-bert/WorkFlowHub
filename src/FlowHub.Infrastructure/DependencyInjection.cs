using FlowHub.Application.Flows;
using FlowHub.Application.Repositories;
using FlowHub.Application.WorkItems;
using FlowHub.Infrastructure.Persistence;
using FlowHub.Infrastructure.Yaml;
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

        services.AddScoped<IWorkItemStore, EfWorkItemStore>();
        services.AddSingleton<IFlowStore, YamlFlowStore>();
        services.AddSingleton<IRepositoryCatalog, YamlRepositoryCatalog>();

        return services;
    }

    private static string ReadConnectionString(IServiceProvider provider) =>
        provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
        ?? throw new InvalidOperationException($"Brak connection stringa '{ConnectionStringName}' w konfiguracji.");
}
