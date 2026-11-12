namespace FlowHub.Domain.Repositories;

/// <summary>Repozytorium z katalogu repo (repos.yaml) — konfiguracja wspólna dla całej aplikacji.</summary>
public sealed record CodeRepository
{
    public required string Name { get; init; }
    public required string Url { get; init; }
    public string DefaultBranch { get; init; } = "main";

    /// <summary>Build configuration(s) w TeamCity.</summary>
    public IReadOnlyList<string> CiBuildConfigurations { get; init; } = [];

    /// <summary>Projekt w Octopus Deploy.</summary>
    public string? DeployProject { get; init; }

    /// <summary>Słowa kluczowe / moduły do rozpoznawania repo z treści zadania.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>Procesy / usługi Windows tego repo na maszynach — do sprawdzenia po wdrożeniu, czy serwis wstał.</summary>
    public IReadOnlyList<RemoteProcess> Processes { get; init; } = [];
}

/// <summary>Name = początek nazwy procesu albo usługi (tak jak w Menedżerze zadań, bez .exe). Label = np. nazwa środowiska.</summary>
public sealed record RemoteProcess(string Host, string Name, string? Label = null);
