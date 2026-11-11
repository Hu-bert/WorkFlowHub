using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FlowHub.Domain;

namespace FlowHub.Integrations.Http;

/// <summary>Wspólny odczyt JSON z serwerów firmowych. Tylko GET — FlowHub niczego tam nie zmienia.</summary>
public sealed class ApiClient(IHttpClientFactory factory)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    /// <summary>JSON z adresu albo null przy 404. Inne błędy → czytelny komunikat (bez tokenów).</summary>
    public async Task<JsonDocument?> GetJsonAsync(string url, Action<HttpRequestHeaders> authorize, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        authorize(request.Headers);

        using var client = factory.CreateClient();
        client.Timeout = Timeout;
        using var response = await client.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new DomainException($"{new Uri(url).Host} odpowiedział {(int)response.StatusCode} {response.ReasonPhrase} ({Explain(response.StatusCode)}).");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        try
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (JsonException)
        {
            // Typowe przy wygasłym tokenie: serwer zwraca stronę logowania HTML ze statusem 200.
            throw new DomainException($"{new Uri(url).Host} zwrócił coś innego niż JSON — zwykle oznacza to nieważny token (strona logowania).");
        }
    }

    public static Action<HttpRequestHeaders> Basic(string user, string? password) => headers =>
        headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));

    public static Action<HttpRequestHeaders> Bearer(string? token) => headers =>
        headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public static Action<HttpRequestHeaders> Header(string name, string? value) => headers =>
        headers.TryAddWithoutValidation(name, value);

    /// <summary>Adres bazowy bez końcowego ukośnika albo błąd z nazwą brakującego ustawienia.</summary>
    public static string BaseUrl(string? configured, string settingName) =>
        string.IsNullOrWhiteSpace(configured)
            ? throw new DomainException($"Brak adresu serwera — uzupełnij „{settingName}” w Ustawieniach.")
            : configured.Trim().TrimEnd('/');

    private static string Explain(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => "zły albo wygasły token",
        HttpStatusCode.Forbidden => "token nie ma uprawnień",
        _ => "sprawdź adres i ustawienia",
    };
}
