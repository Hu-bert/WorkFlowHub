using FlowHub.Application.Agents;
using FlowHub.Application.Blocks;
using FlowHub.Application.Engine.Scripts;
using FlowHub.Application.Flows;
using FlowHub.Application.Git;
using FlowHub.Infrastructure.Git;
using FlowHub.Application.Repositories;
using FlowHub.Application.Settings;
using FlowHub.Application.WorkItems;
using FlowHub.Domain;
using FlowHub.Infrastructure.Settings;
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
        services.AddSingleton<IGitService, GitCliService>();

        return services.AddAgentRunner().AddSettings();
    }

    /// <summary>
    /// Agent wybierany w ustawieniach: Agent:Runner = Fake | CopilotCli — przy każdym uruchomieniu kroku,
    /// więc przełączenie nie wymaga restartu. Nowy runner = nowa linia AddKeyedSingleton.
    /// </summary>
    private static IServiceCollection AddAgentRunner(this IServiceCollection services)
    {
        services.AddKeyedSingleton<IAgentRunner, FakeAgentRunner>(AgentRunners.Fake.ToLowerInvariant());
        services.AddKeyedSingleton<IAgentRunner, CopilotCliAgentRunner>(AgentRunners.CopilotCli.ToLowerInvariant());

        services.AddTransient<IAgentRunner>(provider =>
        {
            var runner = provider.GetRequiredService<IOptions<AgentOptions>>().Value.Runner;

            return provider.GetKeyedService<IAgentRunner>(runner.ToLowerInvariant())
                ?? throw new DomainException($"Nieznany Agent:Runner '{runner}'. Dozwolone: {AgentRunners.Fake}, {AgentRunners.CopilotCli}.");
        });

        return services;
    }

    private static IServiceCollection AddSettings(this IServiceCollection services)
    {
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<ISecretStore, ProtectedSecretStore>();
        services.AddSingleton<ISettingsModule, CopilotSettingsModule>();

        return services;
    }
    private static string ReadConnectionString(IServiceProvider provider) =>
        provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
        ?? throw new InvalidOperationException($"Brak connection stringa '{ConnectionStringName}' w konfiguracji.");
}
