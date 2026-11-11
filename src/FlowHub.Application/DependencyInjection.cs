using FlowHub.Application.Agents;
using FlowHub.Application.Blocks;
using FlowHub.Application.Engine;
using FlowHub.Application.Engine.Scripts;
using FlowHub.Application.Flows;
using FlowHub.Application.Integrations;
using FlowHub.Application.Repositories;
using FlowHub.Application.Settings;
using FlowHub.Application.Terminal;
using FlowHub.Application.Tokens;
using FlowHub.Application.WorkItems;
using FlowHub.Application.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace FlowHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<WorkspacePaths>();
        services.AddSingleton<FlowCatalog>();
        services.AddScoped<WorkItemService>();
        services.AddScoped<WorkItemFiles>();
        services.AddScoped<TerminalService>();
        services.AddScoped<BlockGenerator>();
        services.AddScoped<TokenReportService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<ISettingsModule, ModelsSettingsModule>();

        return services.AddFlowEngine();
    }

    private static IServiceCollection AddFlowEngine(this IServiceCollection services)
    {
        services.AddSingleton<StepQueue>();
        services.AddSingleton<StepContextFactory>();
        services.AddSingleton<StepEnvironment>();
        services.AddScoped<StepExecutorRegistry>();
        services.AddScoped<FlowAdvancer>();
        services.AddScoped<StepService>();
        services.AddScoped<StepRunner>();
        services.AddScoped<StepRecovery>();

        // Wykonawcy kroków. Nowy rodzaj kroku = nowa linia tutaj.
        services.AddScoped<IStepExecutor, ScriptStepExecutor>();
        services.AddScoped<IStepExecutor, ManualStepExecutor>();
        services.AddScoped<IStepExecutor, AgentStepExecutor>();
        services.AddScoped<IStepExecutor, CheckStepExecutor>();

        services.AddSingleton<RepoStatusCache>();
        services.AddScoped<RepoStatusService>();
        services.AddScoped<BranchStatusService>();
        services.AddSingleton<BranchStatusCache>();
        services.AddSingleton<LocalRepositoryService>();
        services.AddSingleton<RepoMapStore>();
        services.AddSingleton<ISettingsModule, LocalRepositoriesSettingsModule>();

        services.AddSingleton<AgentPromptBuilder>();
        services.AddSingleton<PermissionBroker>();
        services.AddSingleton<ModelRouter>();
        services.AddSingleton<TokenBudget>();

        return services;
    }
}
