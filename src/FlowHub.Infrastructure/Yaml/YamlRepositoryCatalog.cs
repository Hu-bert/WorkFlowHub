using FlowHub.Application.Repositories;
using FlowHub.Application.Workspace;
using FlowHub.Domain.Repositories;
using Microsoft.Extensions.Logging;
using YamlDotNet.Core;

namespace FlowHub.Infrastructure.Yaml;

/// <summary>Katalog repo z pliku repos.yaml.</summary>
public sealed class YamlRepositoryCatalog(WorkspacePaths paths, ILogger<YamlRepositoryCatalog> logger) : IRepositoryCatalog
{
    private IReadOnlyList<CodeRepository>? _repositories;

    public IReadOnlyList<CodeRepository> GetAll() => _repositories ??= Load();

    public CodeRepository? Find(string name) =>
        GetAll().FirstOrDefault(repository => string.Equals(repository.Name, name, StringComparison.OrdinalIgnoreCase));

    public void Reload() => _repositories = Load();

    public CodeRepository Save(CodeRepository repository, string? originalName)
    {
        Reload();
        var name = repository.Name.Trim();

        if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new FlowHub.Domain.DomainException("Nazwa repo jest wymagana i nie może zawierać znaków niedozwolonych w nazwie katalogu.");
        }

        if (string.IsNullOrWhiteSpace(repository.Url))
        {
            throw new FlowHub.Domain.DomainException("Adres repo jest wymagany (stamtąd FlowHub klonuje repo do zadania).");
        }

        var others = GetAll().Where(existing => originalName is null || !string.Equals(existing.Name, originalName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (others.Any(existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new FlowHub.Domain.DomainException($"Repo '{name}' już jest w katalogu.");
        }

        var saved = repository with { Name = name, Url = repository.Url.Trim(), DefaultBranch = string.IsNullOrWhiteSpace(repository.DefaultBranch) ? "main" : repository.DefaultBranch.Trim() };
        var position = originalName is null ? -1 : GetAll().ToList().FindIndex(existing => string.Equals(existing.Name, originalName, StringComparison.OrdinalIgnoreCase));
        var all = GetAll().ToList();

        if (position >= 0)
        {
            all[position] = saved;
        }
        else
        {
            all.Add(saved);
        }

        Write(all);
        return saved;
    }

    public bool Remove(string name)
    {
        Reload();
        var remaining = GetAll().Where(existing => !string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (remaining.Count == GetAll().Count)
        {
            return false;
        }

        Write(remaining);
        return true;
    }

    private const string Header =
        """
        # Katalog repozytoriów FlowHub — edytuj w aplikacji (strona Repozytoria) albo ręcznie.
        #   name                   - nazwa repo (nazwa katalogu klonu w zadaniu)
        #   url                    - adres klonu w TFS
        #   defaultBranch          - branch, od którego odchodzi branch zadania
        #   ciBuildConfigurations  - id build configuration w TeamCity
        #   deployProject          - projekt (slug) w Octopus Deploy; puste = nazwa repo
        #   keywords               - słowa, po których FlowHub rozpoznaje repo z treści zadania

        """;

    private void Write(List<CodeRepository> repositories)
    {
        var file = new RepositoriesFile
        {
            Repositories = repositories.Select(repository => new RepositoryEntry
            {
                Name = repository.Name,
                Url = repository.Url,
                DefaultBranch = repository.DefaultBranch,
                CiBuildConfigurations = [.. repository.CiBuildConfigurations],
                DeployProject = string.IsNullOrWhiteSpace(repository.DeployProject) ? null : repository.DeployProject.Trim(),
                Keywords = [.. repository.Keywords],
            }).ToList(),
        };

        File.WriteAllText(paths.RepositoriesFile, Header + (file.Repositories.Count == 0 ? "repositories: []" + Environment.NewLine : YamlFormat.Serializer.Serialize(file)));
        Reload();
    }
    private List<CodeRepository> Load()
    {
        SeedIfMissing();

        if (!File.Exists(paths.RepositoriesFile))
        {
            logger.LogWarning("Brak katalogu repo: {File}", paths.RepositoriesFile);
            return [];
        }

        try
        {
            var file = YamlFormat.Deserializer.Deserialize<RepositoriesFile?>(File.ReadAllText(paths.RepositoriesFile));
            return file?.Repositories.Where(IsComplete).Select(ToDomain).ToList() ?? [];
        }
        catch (YamlException error)
        {
            logger.LogError("repos.yaml: {Error}", YamlFormat.Describe(error));
            return [];
        }
    }

    /// <summary>Pierwsze uruchomienie: katalog użytkownika startuje od pliku z repozytorium FlowHub (także wcześniej edytowanego).</summary>
    private void SeedIfMissing()
    {
        if (File.Exists(paths.RepositoriesFile) || !File.Exists(paths.SeedRepositoriesFile)
            || string.Equals(paths.RepositoriesFile, paths.SeedRepositoriesFile, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(paths.RepositoriesFile)!);
        File.Copy(paths.SeedRepositoriesFile, paths.RepositoriesFile);
        logger.LogInformation("Katalog repo skopiowany do {File}", paths.RepositoriesFile);
    }

    private bool IsComplete(RepositoryEntry entry)
    {
        var complete = !string.IsNullOrWhiteSpace(entry.Name) && !string.IsNullOrWhiteSpace(entry.Url);
        if (!complete)
        {
            logger.LogError("repos.yaml: pominięto wpis bez 'name' lub 'url'.");
        }

        return complete;
    }

    private static CodeRepository ToDomain(RepositoryEntry entry) => new()
    {
        Name = entry.Name!,
        Url = entry.Url!,
        DefaultBranch = entry.DefaultBranch,
        CiBuildConfigurations = entry.CiBuildConfigurations,
        DeployProject = entry.DeployProject,
        Keywords = entry.Keywords,
    };

    private sealed class RepositoriesFile
    {
        public List<RepositoryEntry> Repositories { get; set; } = [];
    }

    private sealed class RepositoryEntry
    {
        public string? Name { get; set; }
        public string? Url { get; set; }
        public string DefaultBranch { get; set; } = "main";
        public List<string> CiBuildConfigurations { get; set; } = [];
        public string? DeployProject { get; set; }
        public List<string> Keywords { get; set; } = [];
    }
}
