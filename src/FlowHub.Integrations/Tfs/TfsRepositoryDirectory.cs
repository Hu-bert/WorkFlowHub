using System.Text.Json;
using FlowHub.Application.Integrations;
using FlowHub.Integrations.Http;

namespace FlowHub.Integrations.Tfs;

/// <summary>Repozytoria Git projektu w TFS: GET _apis/git/repositories.</summary>
public sealed class TfsRepositoryDirectory(TfsApi api) : IRepositoryDirectory
{
    public async Task<IReadOnlyList<RemoteRepository>> ListAsync(CancellationToken ct)
    {
        using var repositories = await api.GetAsync("git/repositories", ct);
        return repositories is null ? [] : Map(repositories.RootElement);
    }

    public static IReadOnlyList<RemoteRepository> Map(JsonElement repositories) => Json.Items(repositories, "value")
        .Where(repository => !(repository.TryGetProperty("isDisabled", out var disabled) && disabled.ValueKind == JsonValueKind.True))
        .Select(repository => new RemoteRepository(
            Json.Text(repository, "name") ?? "",
            Json.Text(repository, "remoteUrl") ?? "",
            (Json.Text(repository, "defaultBranch") ?? "refs/heads/main").Replace("refs/heads/", "")))
        .Where(repository => repository.Name.Length > 0 && repository.Url.Length > 0)
        .OrderBy(repository => repository.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();
}