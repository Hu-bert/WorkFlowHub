using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FlowHub.Domain;

namespace FlowHub.Integrations.Http;

/// <summary>Jedno zapytanie do serwera firmowego — do diagnostyki („co FlowHub zapytał i co dostał”). Bez tokenów.</summary>
public sealed record ApiCall(string Url, string Result, long Milliseconds);

/// <summary>Wspólny odczyt JSON z serwerów firmowych. Tylko GET — FlowHub niczego tam nie zmienia.</summary>
public sealed class ApiClient(IHttpClientFactory factory)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);
    private static readonly AsyncLocal<List<ApiCall>?> Trace = new();

    /// <summary>Włącza zapis zapytań wykonanych w tym wywołaniu (diagnostyka). Zwraca listę, która będzie się wypełniać.</summary>
    public static List<ApiCall> StartTrace() => Trace.Value = [];

    /// <summary>JSON z adresu albo null przy 404. Inne błędy → czytelny komunikat (bez tokenów).</summary>
    public async Task<JsonDocument?> GetJsonAsync(string url, Action<HttpRequestHeaders> authorize, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var document = await SendAsync(url, authorize, ct);
            Record(url, document is null ? "404 — nie znaleziono" : "200 OK", watch);
            return document;
        }
        catch (Exception error) when (error is DomainException or HttpRequestException or TaskCanceledException)
        {
            Record(url, error.Message, watch);
            throw error is DomainException ? error : new DomainException($"{Host(url)}: brak połączenia ({error.Message}).");
        }
    }

    private async Task<JsonDocument?> SendAsync(string url, Action<HttpRequestHeaders> authorize, CancellationToken ct)
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
            var details = Summarize(await response.Content.ReadAsStringAsync(ct));
            throw new DomainException(
                $"{Host(url)} odpowiedział {(int)response.StatusCode} {response.ReasonPhrase} ({Explain(response.StatusCode)}).{(details.Length == 0 ? "" : $" Serwer: {details}")}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        try
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (JsonException)
        {
            // Typowe przy wygasłym tokenie: serwer zwraca stronę logowania HTML ze statusem 200.
            throw new DomainException($"{Host(url)} zwrócił coś innego niż JSON — zwykle oznacza to nieważny token (strona logowania).");
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

    private static void Record(string url, string result, Stopwatch watch) =>
        Trace.Value?.Add(new ApiCall(url, result, watch.ElapsedMilliseconds));

    private static string Host(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;

    /// <summary>
    /// Początek odpowiedzi błędu, bez znaczników HTML — serwery (np. TeamCity) piszą w niej, co dokładnie
    /// im się nie podoba w zapytaniu. To treść od serwera, nie nasze dane: nie ma w niej tokenu.
    /// </summary>
    public static string Summarize(string body)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(body, "<[^>]+>", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

        return text.Length <= MaxErrorDetails ? text : text[..MaxErrorDetails] + "…";
    }

    private const int MaxErrorDetails = 400;

    private static string Explain(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => "zły albo wygasły token",
        HttpStatusCode.Forbidden => "token nie ma uprawnień",
        HttpStatusCode.BadRequest => "serwer nie rozumie zapytania",
        _ => "sprawdź adres i ustawienia",
    };
}

/// <summary>Krótkie odczyty pól JSON, wspólne dla klientów.</summary>
internal static class Json
{
    public static IEnumerable<JsonElement> Items(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray()
            : [];

    public static IEnumerable<JsonElement> Array(JsonElement element) =>
        element.ValueKind == JsonValueKind.Array ? element.EnumerateArray() : [];

    public static JsonElement? Object(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : null;

    public static string? Text(JsonElement? element, string property) =>
        element is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var text)
            ? text.ValueKind switch
            {
                JsonValueKind.String => text.GetString(),
                JsonValueKind.Number => text.GetRawText(),
                _ => null,
            }
            : null;

    public static int Number(JsonElement? element, string property) =>
        element is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var number) && number.TryGetInt32(out var result)
            ? result
            : 0;

    public static bool Flag(JsonElement? element, string property) =>
        element is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var flag) && flag.ValueKind == JsonValueKind.True;
}