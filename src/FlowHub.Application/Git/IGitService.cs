namespace FlowHub.Application.Git;

/// <summary>Zmiany w repo zadania względem brancha domyślnego (razem z niezacommitowanymi).</summary>
public sealed record RepoDiff(string Repo, string BaseBranch, string Stat, string Patch, IReadOnlyList<string> UntrackedFiles, bool Truncated);

/// <summary>Port: odczyt stanu repozytorium Git (tylko odczyt — FlowHub nigdy nie robi push).</summary>
public interface IGitService
{
    Task<RepoDiff> DiffAsync(string repo, string repositoryPath, string baseBranch, CancellationToken ct);
}
