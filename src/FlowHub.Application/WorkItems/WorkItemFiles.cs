using FlowHub.Domain;

namespace FlowHub.Application.WorkItems;

public sealed record WorkItemFile(string Name, long Size, DateTime ModifiedAt);

/// <summary>Pliki wynikowe zadania (task.md, context-*.md, plan.md, …) z katalogu zadania.</summary>
public sealed class WorkItemFiles(IWorkItemStore store)
{
    private const int MaxFileSize = 1_000_000;

    public async Task<IReadOnlyList<WorkItemFile>?> ListAsync(Guid workItemId, CancellationToken ct)
    {
        var item = await store.FindAsync(workItemId, ct);
        if (item is null)
        {
            return null;
        }

        if (!Directory.Exists(item.WorkDirectory))
        {
            return [];
        }

        return new DirectoryInfo(item.WorkDirectory)
            .EnumerateFiles("*.md")
            .OrderBy(file => file.LastWriteTimeUtc)
            .Select(file => new WorkItemFile(file.Name, file.Length, file.LastWriteTimeUtc))
            .ToList();
    }

    public async Task<string?> ReadAsync(Guid workItemId, string name, CancellationToken ct)
    {
        var item = await store.FindAsync(workItemId, ct);

        // Tylko nazwa pliku z katalogu zadania — żadnych podkatalogów ani „..”.
        if (item is null || name != Path.GetFileName(name))
        {
            return null;
        }

        var file = new FileInfo(Path.Combine(item.WorkDirectory, name));
        if (!file.Exists)
        {
            return null;
        }

        return file.Length <= MaxFileSize
            ? await File.ReadAllTextAsync(file.FullName, ct)
            : throw new DomainException($"Plik {name} jest za duży do podglądu.");
    }

    /// <summary>Zapisuje poprawiony plik .md zadania (np. plan.md przed realizacją). false = nie ma takiego zadania.</summary>
    public async Task<bool> WriteAsync(Guid workItemId, string name, string content, CancellationToken ct)
    {
        var item = await store.FindAsync(workItemId, ct);
        if (item is null)
        {
            return false;
        }

        // Tylko plik .md bezpośrednio w katalogu zadania — żadnych podkatalogów ani „..”.
        if (name != Path.GetFileName(name) || !name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Edytować można tylko pliki .md z katalogu zadania.");
        }

        if (content.Length > MaxFileSize)
        {
            throw new DomainException($"Plik {name} jest za duży.");
        }

        if (!Directory.Exists(item.WorkDirectory))
        {
            throw new DomainException("Katalog zadania jeszcze nie istnieje — najpierw uruchom flow.");
        }

        await File.WriteAllTextAsync(Path.Combine(item.WorkDirectory, name), content, ct);
        return true;
    }
}
