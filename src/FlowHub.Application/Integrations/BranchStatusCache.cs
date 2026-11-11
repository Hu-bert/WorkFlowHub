using System.Collections.Concurrent;
using FlowHub.Application.Repositories;
using FlowHub.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace FlowHub.Application.Integrations;

public sealed record CachedBranchStatus(BranchStatus Status, DateTime CheckedAt);

/// <summary>
/// Pamięć statusów brancha (PR, build, wdrożenia) dla strony Repozytoria — żeby każde wejście na stronę
/// nie odpytywało serwerów od nowa. Czas życia: Repositories:RefreshMinutes.
/// </summary>
public sealed class BranchStatusCache(IOptions<LocalRepositoriesOptions> options, TimeProvider clock)
{
    private readonly ConcurrentDictionary<(string Repo, string Branch), CachedBranchStatus> _entries = new();

    public async Task<CachedBranchStatus> GetAsync(
        CodeRepository repository, string branch, bool refresh, Func<Task<BranchStatus>> load)
    {
        var key = (repository.Name.ToLowerInvariant(), branch);
        var now = clock.GetUtcNow().UtcDateTime;
        var lifetime = TimeSpan.FromMinutes(Math.Max(0, options.Value.RefreshMinutes));

        if (!refresh && _entries.TryGetValue(key, out var cached) && now - cached.CheckedAt < lifetime)
        {
            return cached;
        }

        return _entries[key] = new CachedBranchStatus(await load(), now);
    }
}