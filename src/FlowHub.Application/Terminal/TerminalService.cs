using FlowHub.Application.WorkItems;
using FlowHub.Domain;
using FlowHub.Domain.WorkItems;

namespace FlowHub.Application.Terminal;

/// <summary>Gdzie i z jakimi argumentami uruchomić interaktywny Copilot CLI dla zadania.</summary>
public sealed record TerminalLaunch(string WorkingDirectory, IReadOnlyList<string> Arguments);

/// <summary>
/// Terminal w zadaniu: interaktywny Copilot CLI w katalogu repo (albo zadania), opcjonalnie jako
/// kontynuacja sesji kroku. Klient nie podaje ani polecenia, ani ścieżki — tylko id zadania, repo i kroku.
/// O zgodę na narzędzia Copilot pyta w samym terminalu; nigdy nie dodajemy --allow-all.
/// </summary>
public sealed class TerminalService(IWorkItemStore store)
{
    public async Task<TerminalLaunch?> PlanAsync(Guid workItemId, string? repoName, Guid? stepRunId, CancellationToken ct)
    {
        var item = await store.FindAsync(workItemId, ct);
        if (item is null)
        {
            return null;
        }

        var step = stepRunId is { } id ? item.Steps.FirstOrDefault(candidate => candidate.Id == id) : null;
        if (stepRunId is not null && step?.SessionId is null)
        {
            throw new DomainException("Ten krok nie ma sesji Copilot do wznowienia (sesję zapisuje tylko prawdziwy Copilot CLI).");
        }

        var directory = Directory(item, repoName ?? step?.RepoName);
        if (!System.IO.Directory.Exists(directory))
        {
            throw new DomainException($"Katalog {directory} jeszcze nie istnieje — najpierw uruchom flow (folder i klon repo).");
        }

        var arguments = new List<string>();
        if (!string.Equals(directory, item.WorkDirectory, StringComparison.OrdinalIgnoreCase))
        {
            // Sesja w repo widzi też pliki zadania (task.md, context.md, plan.md).
            arguments.AddRange(["--add-dir", item.WorkDirectory]);
        }

        if (step?.SessionId is { } session)
        {
            arguments.Add($"--resume={session}");
        }

        return new TerminalLaunch(directory, arguments);
    }

    /// <summary>Wskazane repo zadania; bez wskazania — pierwsze repo; zadanie bez repo — katalog zadania.</summary>
    private static string Directory(WorkItem item, string? repoName)
    {
        if (repoName is null)
        {
            return item.Repos.FirstOrDefault()?.LocalPath ?? item.WorkDirectory;
        }

        if (repoName.Length == 0)
        {
            return item.WorkDirectory;
        }

        return item.Repos.FirstOrDefault(repo => repo.RepositoryName == repoName)?.LocalPath
            ?? throw new DomainException($"Zadanie nie ma repo '{repoName}'.");
    }
}
