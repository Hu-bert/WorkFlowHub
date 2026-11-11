using System.Text.Json;
using FlowHub.Application.Integrations;

namespace FlowHub.Integrations.Tfs;

/// <summary>Repozytoria Git projektu w TFS: GET _apis/git/repositories.</summary>
public sealed class TfsRepositoryDirectory(TfsApi api) : IRepositoryDirectory
{
    public async Task<IReadOnlyList<RemoteRepository>> ListAsync(CancellationToken ct)
    {
        using var repositories = await api.GetAsync("git/repositories", ct);
        return repositories is null ? [] : Map(repositories.RootElement);
    }

    public static IReadOnlyList<RemoteRepository> Map(JsonElement repositories) => TfsMapper.Items(repositories)
        .Where(repository => !(repository.TryGetProperty("isDisabled", out var disabled) && disabled.ValueKind == JsonValueKind.True))
        .Select(repository => new RemoteRepository(
            TfsMapper.Text(repository, "name") ?? "",
            TfsMapper.Text(repository, "remoteUrl") ?? "",
            (TfsMapper.Text(repository, "defaultBranch") ?? "refs/heads/main").Replace("refs/heads/", ""),
            TfsMapper.Text(repository, "webUrl")))
        .Where(repository => repository.Name.Length > 0 && repository.Url.Length > 0)
        .OrderBy(repository => repository.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();
}