using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowHub.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>Tworzy bazę i nakłada brakujące migracje przy starcie aplikacji.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowHubDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
