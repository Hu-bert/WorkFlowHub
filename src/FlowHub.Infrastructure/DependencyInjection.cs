using FlowHub.Application.Agents;
using FlowHub.Application.Blocks;
using FlowHub.Application.Engine.Scripts;
using FlowHub.Application.Flows;
using FlowHub.Application.Repositories;
using FlowHub.Application.WorkItems;
using FlowHub.Infrastructure.Agents;
using FlowHub.Infrastructure.Blocks;
using FlowHub.Infrastructure.Persistence;
using FlowHub.Infrastructure.Scripts;
using FlowHub.Infrastructure.Yaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        services.AddSingleton<IBlockLibrary, FileBlockLibrary>();
        services.AddSingleton<IScriptRunner, PowerShellScriptRunner>();

        return services.AddAgentRunner();
    }

    /// <summary>Agent wybierany w konfiguracji: Agent:Runner = Fake | CopilotCli. Nowy runner = nowa linia w switchu.</summary>
    private static IServiceCollection AddAgentRunner(this IServiceCollection services)
    {
        services.AddSingleton<FakeAgentRunner>();
        services.AddSingleton<CopilotCliAgentRunner>();

        services.AddSingleton<IAgentRunner>(provider =>
        {
            var runner = provider.GetRequiredService<IOptions<AgentOptions>>().Value.Runner;

            return runner.ToLowerInvariant() switch
            {
                "fake" => provider.GetRequiredService<FakeAgentRunner>(),
                "copilotcli" => provider.GetRequiredService<CopilotCliAgentRunner>(),
                _ => throw new InvalidOperationException($"Nieznany Agent:Runner '{runner}'. Dozwolone: Fake, CopilotCli."),
            };
        });

        return services;
    }

    private static string ReadConnectionString(IServiceProvider provider) =>
        provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
        ?? throw new InvalidOperationException($"Brak connection stringa '{ConnectionStringName}' w konfiguracji.");
}
