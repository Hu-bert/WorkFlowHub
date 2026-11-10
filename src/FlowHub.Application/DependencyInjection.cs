using Microsoft.Extensions.DependencyInjection;

namespace FlowHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Serwisy aplikacyjne (use case'y) rejestrujemy tutaj od Fazy 2.
        return services;
    }
}
