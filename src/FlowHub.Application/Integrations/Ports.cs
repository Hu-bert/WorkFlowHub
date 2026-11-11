using FlowHub.Domain.Repositories;

namespace FlowHub.Application.Integrations;

// Porty do systemów zewnętrznych. Każdy ma implementację Fake (FlowHub.Integrations/Fake)
// i miejsce na prawdziwą — wybór w appsettings.json → "Integrations".
//
// Mapowanie zmian między systemami:
//   repo (repos.yaml) → build configuration w TeamCity + projekt w Octopus,
//   a dalej po nazwie brancha zadania: branch → PR (TFS) → buildy (TeamCity) → release i wdrożenia (Octopus).

/// <summary>Tracker zadań (nowa Jira przez API; stara Jira = ręczne wpisanie w UI).</summary>
public interface ITaskSource
{
    Task<TaskDetails?> GetAsync(string key, CancellationToken ct);
}

/// <summary>Pull requesty w TFS / Azure DevOps Server.</summary>
public interface IPullRequestService
{
    Task<PullRequestStatus?> GetForBranchAsync(CodeRepository repository, string branch, CancellationToken ct);
}

/// <summary>Serwer CI (TeamCity): zmiany z brancha i buildy, które je zawierają.</summary>
public interface ICiServer
{
    Task<IReadOnlyList<CiChange>> GetChangesAsync(CodeRepository repository, string branch, CancellationToken ct);

    Task<IReadOnlyList<BuildStatus>> GetBuildsWithChangesAsync(CodeRepository repository, string branch, CancellationToken ct);

    /// <summary>Build configuration dostępne na serwerze — podpowiedzi przy mapowaniu repo.</summary>
    Task<IReadOnlyList<CiConfiguration>> ListConfigurationsAsync(CancellationToken ct);
}

/// <summary>Serwer wdrożeń (Octopus): release ze zmianami i jego stan na środowiskach.</summary>
public interface IDeployServer
{
    /// <summary>builds = buildy tego brancha z CI; ich numery pomagają znaleźć release, który z nich powstał.</summary>
    Task<DeploymentOverview?> GetOverviewAsync(CodeRepository repository, string branch, IReadOnlyList<BuildStatus> builds, CancellationToken ct);

    Task<IReadOnlyList<DeployProjectInfo>> ListProjectsAsync(CancellationToken ct);

    /// <summary>Nazwy środowisk w kolejności z serwera.</summary>
    Task<IReadOnlyList<string>> ListEnvironmentsAsync(CancellationToken ct);
}

/// <summary>Adresy stron repo w TFS, TeamCity i Octopus — do przycisków „przejdź do…”.</summary>
public interface IRepositoryLinks
{
    RepositoryLinks For(CodeRepository repository, string? branch);
}

/// <summary>Repozytorium widoczne na serwerze (TFS) — do wyboru przy zadaniu i dodania do katalogu repos.yaml.</summary>
public sealed record RemoteRepository(string Name, string Url, string DefaultBranch, string? WebUrl);

/// <summary>Lista repozytoriów z serwera kodu (TFS / Azure DevOps Server).</summary>
public interface IRepositoryDirectory
{
    Task<IReadOnlyList<RemoteRepository>> ListAsync(CancellationToken ct);
}
