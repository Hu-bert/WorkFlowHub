using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlowHub.Api.Tests;

/// <summary>
/// Uruchamia API w pamięci z osobną, tymczasową bazą SQLite i katalogiem zadań.
/// Flow i repos.yaml są prawdziwe (z repo) — testy pilnują, że pliki w repo są poprawne.
/// </summary>
public sealed class FlowHubApiFactory : WebApplicationFactory<Program>
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"flowhub-test-{Guid.NewGuid():N}");

    public string WorkDirectory => Path.Combine(_tempDirectory, "work");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_tempDirectory);

        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:FlowHub", $"Data Source={Path.Combine(_tempDirectory, "test.db")};Pooling=False");
        builder.UseSetting("Workspace:WorkDirectory", WorkDirectory);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        try
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Plik bazy bywa jeszcze chwilę zablokowany — katalog tymczasowy posprząta system.
        }
    }
}

internal static class HttpClientJsonExtensions
{
    public static async Task<T> GetAsync<T>(this HttpClient client, string url)
    {
        var value = await client.GetFromJsonAsync<T>(url, FlowHubApiFactory.Json, TestContext.Current.CancellationToken);
        return value ?? throw new InvalidOperationException($"Pusta odpowiedź z {url}");
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(FlowHubApiFactory.Json, TestContext.Current.CancellationToken);
        return value ?? throw new InvalidOperationException("Pusta odpowiedź");
    }

    public static Task<HttpResponseMessage> PostAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, FlowHubApiFactory.Json, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> PutAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, FlowHubApiFactory.Json, TestContext.Current.CancellationToken);
}
