using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowHub.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    // Faza 1: EnsureCreated. Od Fazy 2 (pierwsze encje) przechodzimy na migracje EF Core.
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowHubDbContext>();
        await db.Database.EnsureCreatedAsync(ct);
    }
}
