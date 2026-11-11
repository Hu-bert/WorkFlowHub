using FlowHub.Application.Git;
using FlowHub.Application.Settings;
using FlowHub.Domain;
using FlowHub.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace FlowHub.Application.Repositories;

/// <summary>Gdzie na dysku leżą Twoje kopie repozytoriów ("Repositories" w ustawieniach).</summary>
public sealed class LocalRepositoriesOptions
{
    public const string SectionName = "Repositories";

    /// <summary>Folder z repozytoriami, np. C:\tfs. Repo może leżeć bezpośrednio w nim albo poziom niżej (folder brancha).</summary>
    public string? ScanRoot { get; init; }
}

/// <summary>Lokalna kopia repo znaleziona w folderze. CatalogName = dopasowany wpis z repos.yaml (albo null).</summary>
public sealed record LocalRepository(
    string Path,
    string Folder,
    string Name,
    string? RemoteUrl,
    string Branch,
    IReadOnlyList<GitCommit> RecentCommits,
    int UncommittedFiles,
    string? CatalogName);

public sealed record LocalRepositoryScan(string? Root, IReadOnlyList<LocalRepository> Repositories);

/// <summary>
/// Przegląda folder z repozytoriami (tylko odczyt): co tam jest, na jakim branchu i z jakimi commitami.
/// Układ jest dowolny — repo leży luzem w folderze albo w podfolderze nazwanym np. od brancha.
/// </summary>
public sealed class LocalRepositoryService(IOptions<LocalRepositoriesOptions> options, IGitService git, IRepositoryCatalog catalog)
{
    /// <summary>Folder główny → repo → repo w folderze brancha. Głębiej nie szukamy (szybko i przewidywalnie).</summary>
    private const int MaxDepth = 2;

    public async Task<LocalRepositoryScan> ScanAsync(CancellationToken ct)
    {
        var root = options.Value.ScanRoot?.Trim();
        if (string.IsNullOrEmpty(root))
        {
            return new LocalRepositoryScan(null, []);
        }

        if (!Directory.Exists(root))
        {
            throw new DomainException($"Folder repozytoriów '{root}' nie istnieje — popraw go w Ustawieniach.");
        }

        var repositories = new List<LocalRepository>();
        foreach (var path in FindRepositories(root, MaxDepth).Order(StringComparer.OrdinalIgnoreCase))
        {
            repositories.Add(await DescribeAsync(root, path, ct));
        }

        return new LocalRepositoryScan(root, repositories);
    }

    /// <summary>Lokalne repo z folderu → wpis katalogu (nazwa i adres z origin). Nie ufa danym od klienta — skanuje sam.</summary>
    public async Task<CodeRepository> AddToCatalogAsync(string path, CancellationToken ct)
    {
        var scan = await ScanAsync(ct);
        var local = scan.Repositories.FirstOrDefault(repository => string.Equals(repository.Path, path, StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainException("Tego repo nie ma w folderze repozytoriów.");

        if (local.CatalogName is not null)
        {
            return catalog.Find(local.CatalogName)!;
        }

        if (local.RemoteUrl is null)
        {
            throw new DomainException($"Repo {local.Name} nie ma zdalnego adresu (origin) — nie da się go sklonować do zadania.");
        }

        return catalog.Add(new CodeRepository { Name = local.Name, Url = local.RemoteUrl });
    }

    private async Task<LocalRepository> DescribeAsync(string root, string path, CancellationToken ct)
    {
        var copy = await git.DescribeAsync(path, ct);
        var name = NameFromUrl(copy.RemoteUrl) ?? System.IO.Path.GetFileName(path);

        return new LocalRepository(
            path,
            System.IO.Path.GetRelativePath(root, path),
            name,
            copy.RemoteUrl,
            copy.Branch,
            copy.RecentCommits,
            copy.UncommittedFiles,
            Match(name, copy.RemoteUrl)?.Name);
    }

    /// <summary>Dopasowanie do katalogu: po adresie zdalnym, a gdy się różni (np. inny protokół) — po nazwie.</summary>
    private CodeRepository? Match(string name, string? remoteUrl) =>
        catalog.GetAll().FirstOrDefault(repository => remoteUrl is not null && SameUrl(repository.Url, remoteUrl))
        ?? catalog.Find(name);

    private static IEnumerable<string> FindRepositories(string directory, int depth)
    {
        if (IsRepository(directory))
        {
            return [directory];
        }

        if (depth == 0)
        {
            return [];
        }

        try
        {
            return Directory.EnumerateDirectories(directory)
                .Where(child => !System.IO.Path.GetFileName(child).StartsWith('.'))
                .SelectMany(child => FindRepositories(child, depth - 1))
                .ToList();
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>.git jest katalogiem w zwykłym klonie, a plikiem w worktree.</summary>
    private static bool IsRepository(string directory)
    {
        var git = System.IO.Path.Combine(directory, ".git");
        return Directory.Exists(git) || File.Exists(git);
    }

    public static string? NameFromUrl(string? url)
    {
        var last = url?.TrimEnd('/').Split('/', '\\', ':').LastOrDefault();
        if (string.IsNullOrWhiteSpace(last))
        {
            return null;
        }

        return last.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? last[..^4] : last;
    }

    private static bool SameUrl(string first, string second) =>
        string.Equals(Normalize(first), Normalize(second), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string url)
    {
        var trimmed = url.Trim().TrimEnd('/');
        return trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? trimmed[..^4] : trimmed;
    }
}

/// <summary>Ustawienia: folder z lokalnymi kopiami repozytoriów.</summary>
public sealed class LocalRepositoriesSettingsModule(LocalRepositoryService repositories) : ISettingsModule
{
    public string Id => "repositories";

    public string Title => "Repozytoria lokalne";

    public string Icon => "mdi-folder-search-outline";

    public string Description =>
        "Folder, w którym trzymasz kopie repozytoriów (luzem albo w podfolderach, np. per branch). Strona Repozytoria pokaże je z branchem, commitami oraz statusem PR, buildu i wdrożeń. FlowHub tylko czyta.";

    public bool CanTest => true;

    public IReadOnlyList<SettingField> Fields { get; } =
    [
        new($"{LocalRepositoriesOptions.SectionName}:{nameof(LocalRepositoriesOptions.ScanRoot)}", "Folder repozytoriów", SettingKind.Folder, @"Np. C:\tfs"),
    ];

    public async Task<ConnectionTest> TestAsync(CancellationToken ct)
    {
        var scan = await repositories.ScanAsync(ct);

        return scan.Root is null
            ? new ConnectionTest(false, "Nie ustawiono folderu.")
            : new ConnectionTest(true, $"Znaleziono repozytoriów: {scan.Repositories.Count} (w katalogu repos.yaml: {scan.Repositories.Count(repository => repository.CatalogName is not null)}).");
    }
}
