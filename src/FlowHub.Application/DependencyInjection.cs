using FlowHub.Application.Agents;
using FlowHub.Application.Engine;
using FlowHub.Application.Engine.Scripts;
using FlowHub.Application.Flows;
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
        services.AddScoped<TokenReportService>();

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

        services.AddSingleton<AgentPromptBuilder>();
        services.AddSingleton<PermissionBroker>();
        services.AddSingleton<ModelRouter>();
        services.AddSingleton<TokenBudget>();

        return services;
    }
}
