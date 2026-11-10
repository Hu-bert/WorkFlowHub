using Microsoft.EntityFrameworkCore;

namespace FlowHub.Infrastructure.Persistence;

public sealed class FlowHubDbContext(DbContextOptions<FlowHubDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FlowHubDbContext).Assembly);
    }
}
