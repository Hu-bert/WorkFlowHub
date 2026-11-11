using System.Net.Http.Headers;
using System.Text.Json;
using FlowHub.Application.Integrations;
using FlowHub.Application.Settings;
using FlowHub.Integrations.Http;
using Microsoft.Extensions.Options;

namespace FlowHub.Integrations.Jira;

/// <summary>Połączenie z nową Jirą ("Jira" w ustawieniach; token = sekret "Jira:Token").</summary>
public sealed class JiraOptions
{
    public const string SectionName = "Jira";
    public const string TokenKey = "Jira:Token";

    public string? BaseUrl { get; init; }

    /// <summary>Jira Cloud: e-mail konta (logowanie Basic e-mail + token API). Puste = Jira Server / Data Center (token PAT jako Bearer).</summary>
    public string? Email { get; init; }
}

/// <summary>
/// Treść zadania z Jiry: GET /rest/api/2/issue/{klucz}.
/// NIE sprawdzone na prawdziwym serwerze — adres i pola wg dokumentacji Jira REST API v2.
/// </summary>
public sealed class JiraTaskSource(ApiClient client, IOptions<JiraOptions> options, ISecretStore secrets) : ITaskSource
{
    public async Task<TaskDetails?> GetAsync(string key, CancellationToken ct)
    {
        var baseUrl = ApiClient.BaseUrl(options.Value.BaseUrl, "Adres Jiry");

        using var issue = await client.GetJsonAsync(
            $"{baseUrl}/rest/api/2/issue/{Uri.EscapeDataString(key)}?fields=summary,description", Authorize(), ct);

        return issue is null ? null : Map(issue.RootElement, key, baseUrl);
    }

    public Action<HttpRequestHeaders> Authorize()
    {
        var token = secrets.Get(JiraOptions.TokenKey);

        return string.IsNullOrWhiteSpace(options.Value.Email)
            ? ApiClient.Bearer(token)
            : ApiClient.Basic(options.Value.Email.Trim(), token);
    }

    public static TaskDetails Map(JsonElement issue, string key, string baseUrl)
    {
        var fields = issue.TryGetProperty("fields", out var value) ? value : default;

        return new TaskDetails(
            Text(issue, "key") ?? key,
            Text(fields, "summary") ?? "",
            Text(fields, "description") ?? "",
            $"{baseUrl}/browse/{Text(issue, "key") ?? key}");
    }

    private static string? Text(JsonElement element, string property) => Json.Text(element, property);
}
