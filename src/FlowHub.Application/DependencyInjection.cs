using FlowHub.Application.Flows;
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

        return services;
    }
}
