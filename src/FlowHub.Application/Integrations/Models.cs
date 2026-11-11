namespace FlowHub.Application.Integrations;

// Modele danych z systemów zewnętrznych. Wspólne dla implementacji Fake i prawdziwych.

public sealed record TaskDetails(string Key, string Title, string Description, string Url);

public enum PullRequestState
{
    Draft,
    Active,
    Completed,
    Abandoned,
}

public enum ReviewerVote
{
    Waiting,
    Approved,
    ApprovedWithSuggestions,
    Rejected,
}

public sealed record Reviewer(string Name, ReviewerVote Vote);

/// <summary>Polityka brancha: build validation, wymagani recenzenci, powiązany work item.</summary>
public sealed record PolicyCheck(string Name, bool Passed);

public sealed record PullRequestStatus(
    int Id,
    string Title,
    PullRequestState State,
    IReadOnlyList<Reviewer> Reviewers,
    int UnresolvedComments,
    IReadOnlyList<PolicyCheck> Policies,
    bool HasConflicts,
    string Url);

public sealed record CiChange(string Revision, string Author, string Comment, DateTime Date);

public enum BuildState
{
    Queued,
    Running,
    Success,
    Failed,
}

/// <summary>StatusText = opis z serwera CI, np. „Tests failed: 24 (24 new), passed: 194”. Branch = branch, na którym poszedł build.</summary>
public sealed record BuildStatus(
    string Configuration,
    string Number,
    BuildState State,
    IReadOnlyList<string> FailedTests,
    string Url,
    string? StatusText = null,
    string? Branch = null);

/// <summary>Build configuration na serwerze CI — do wyboru przy repo w katalogu.</summary>
public sealed record CiConfiguration(string Id, string Name, string? Project);

/// <summary>Projekt na serwerze wdrożeń — do wyboru przy repo w katalogu.</summary>
public sealed record DeployProjectInfo(string Slug, string Name);

public sealed record NamedLink(string Name, string Url);

/// <summary>Skróty do repo w systemach firmowych (null / pusta lista = adres nieznany).</summary>
public sealed record RepositoryLinks(string? Repository, string? PullRequests, IReadOnlyList<NamedLink> Builds, string? Deploy)
{
    public static RepositoryLinks None { get; } = new(null, null, [], null);
}

public enum DeploymentState
{
    None,
    Queued,
    Executing,
    Success,
    Failed,
}

public sealed record EnvironmentDeployment(string Environment, string? Version, DeploymentState State, DateTime? At, string? Url);

/// <summary>Która wersja zawiera zmiany i na których środowiskach jest wdrożona.</summary>
public sealed record DeploymentOverview(string Project, string? Release, IReadOnlyList<EnvironmentDeployment> Environments);
