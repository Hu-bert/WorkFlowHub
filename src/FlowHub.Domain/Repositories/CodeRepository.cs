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
}
