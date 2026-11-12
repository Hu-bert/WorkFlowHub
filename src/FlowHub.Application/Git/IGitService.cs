namespace FlowHub.Application.Git;

/// <summary>Zmiany w repo zadania względem brancha domyślnego (razem z niezacommitowanymi).</summary>
public sealed record RepoDiff(string Repo, string BaseBranch, string Stat, string Patch, IReadOnlyList<string> UntrackedFiles, bool Truncated);

public sealed record GitCommit(string Hash, string Author, DateTimeOffset Date, string Subject);

/// <summary>Stan lokalnej kopii repo: skąd pochodzi, na jakim jest branchu i co ostatnio commitowano.</summary>
public sealed record GitWorkingCopy(string? RemoteUrl, string Branch, IReadOnlyList<GitCommit> RecentCommits, int UncommittedFiles);

/// <summary>Port: odczyt stanu repozytorium Git (tylko odczyt — FlowHub nigdy nie robi push).</summary>
public interface IGitService
{
    Task<RepoDiff> DiffAsync(string repo, string repositoryPath, string baseBranch, CancellationToken ct);

    Task<GitWorkingCopy> DescribeAsync(string repositoryPath, CancellationToken ct);

    /// <summary>git clone do wskazanego (jeszcze nieistniejącego) katalogu. Logowanie do serwera robi Git (credential manager).</summary>
    Task CloneAsync(string url, string targetPath, CancellationToken ct);
}