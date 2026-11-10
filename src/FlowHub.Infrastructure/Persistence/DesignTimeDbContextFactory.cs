using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FlowHub.Infrastructure.Persistence;

/// <summary>Tylko dla narzędzia `dotnet ef` (tworzenie migracji) — aplikacja z tego nie korzysta.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FlowHubDbContext>
{
    public FlowHubDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<FlowHubDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
