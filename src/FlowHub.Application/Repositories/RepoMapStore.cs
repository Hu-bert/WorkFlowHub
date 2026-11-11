using System.Text.Json;
using FlowHub.Application.Workspace;
using FlowHub.Domain;

namespace FlowHub.Application.Repositories;

/// <summary>Zapamiętana „mapa repo” — wynik analizy technologicznej, używany ponownie w kolejnych zadaniach.</summary>
public sealed record RepoMap(string Repository, string Content, DateTime SavedAt, string? Commit, string? TaskKey);

/// <summary>
/// Mapy repozytoriów trzymane lokalnie w katalogu knowledge/ (poza gitem FlowHub): &lt;repo&gt;.md + &lt;repo&gt;.json.
/// Dzięki nim analiza kolejnego zadania aktualizuje gotową mapę, zamiast skanować repo od zera — mniej tokenów i czasu.
/// </summary>
public sealed class RepoMapStore(WorkspacePaths paths, TimeProvider clock)
{
    private sealed record Meta(DateTime SavedAt, string? Commit, string? TaskKey);

    public RepoMap? Find(string repository)
    {
        var file = ContentFile(repository);
        if (!File.Exists(file))
        {
            return null;
        }

        var meta = ReadMeta(repository) ?? new Meta(File.GetLastWriteTimeUtc(file), null, null);
        return new RepoMap(repository, File.ReadAllText(file), meta.SavedAt, meta.Commit, meta.TaskKey);
    }

    /// <summary>commit = stan repo, którego dotyczy mapa (od niego następna analiza liczy zmiany).</summary>
    public RepoMap Save(string repository, string content, string? commit, string? taskKey)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new DomainException("Mapa repo nie może być pusta — żeby zacząć od zera, usuń ją.");
        }

        Directory.CreateDirectory(paths.KnowledgeDirectory);
        var meta = new Meta(clock.GetUtcNow().UtcDateTime, commit, taskKey);

        File.WriteAllText(ContentFile(repository), content);
        File.WriteAllText(MetaFile(repository), JsonSerializer.Serialize(meta));

        return new RepoMap(repository, content, meta.SavedAt, commit, taskKey);
    }

    /// <summary>Zapis poprawionej treści z aplikacji — commit i zadanie zostają te same.</summary>
    public RepoMap Edit(string repository, string content)
    {
        var existing = Find(repository);
        return Save(repository, content, existing?.Commit, existing?.TaskKey);
    }

    public bool Delete(string repository)
    {
        var existed = File.Exists(ContentFile(repository));
        File.Delete(ContentFile(repository));
        File.Delete(MetaFile(repository));
        return existed;
    }

    private Meta? ReadMeta(string repository)
    {
        try
        {
            return File.Exists(MetaFile(repository)) ? JsonSerializer.Deserialize<Meta>(File.ReadAllText(MetaFile(repository))) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string ContentFile(string repository) => Path.Combine(paths.KnowledgeDirectory, $"{Safe(repository)}.md");

    private string MetaFile(string repository) => Path.Combine(paths.KnowledgeDirectory, $"{Safe(repository)}.json");

    /// <summary>Nazwa repo jest nazwą pliku — żadnych ścieżek ani „..”.</summary>
    private static string Safe(string repository) =>
        repository.Length > 0 && repository == Path.GetFileName(repository) && repository.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && repository != ".."
            ? repository
            : throw new DomainException($"Nieprawidłowa nazwa repo '{repository}'.");
}
