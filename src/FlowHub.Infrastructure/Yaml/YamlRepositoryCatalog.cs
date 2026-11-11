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

    /// <summary>Dopisuje wpis na końcu pliku — komentarze i ręczne formatowanie repos.yaml zostają nietknięte.</summary>
    public CodeRepository Add(CodeRepository repository)
    {
        Reload();
        if (Find(repository.Name) is { } existing)
        {
            return existing;
        }

        var file = paths.RepositoriesFile;
        var text = File.Exists(file) ? File.ReadAllText(file) : "repositories:";
        if (!text.Contains("repositories:", StringComparison.Ordinal))
        {
            text = text.TrimEnd() + Environment.NewLine + "repositories:";
        }

        var entry = string.Join(Environment.NewLine,
            "",
            $"  - name: {Quote(repository.Name)}",
            $"    url: {Quote(repository.Url)}",
            $"    defaultBranch: {Quote(repository.DefaultBranch)}",
            "    ciBuildConfigurations: []",
            "    keywords: []",
            "");

        File.WriteAllText(file, text.TrimEnd() + Environment.NewLine + entry);
        Reload();

        return Find(repository.Name)
            ?? throw new FlowHub.Domain.DomainException($"Nie udało się dopisać repo '{repository.Name}' do repos.yaml — sprawdź składnię pliku.");
    }

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private List<CodeRepository> Load()
    {
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
