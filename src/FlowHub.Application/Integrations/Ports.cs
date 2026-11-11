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
}

/// <summary>Serwer wdrożeń (Octopus): release ze zmianami i jego stan na środowiskach.</summary>
public interface IDeployServer
{
    Task<DeploymentOverview?> GetOverviewAsync(CodeRepository repository, string branch, CancellationToken ct);
}
