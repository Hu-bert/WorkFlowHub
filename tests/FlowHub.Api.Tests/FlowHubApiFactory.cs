using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlowHub.Api.Tests;

/// <summary>Uruchamia API w pamięci z osobną, tymczasową bazą SQLite.</summary>
public sealed class FlowHubApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"flowhub-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:FlowHub", $"Data Source={_databasePath};Pooling=False");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        File.Delete(_databasePath);
    }
}
