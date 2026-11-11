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

public sealed record BuildStatus(
    string Configuration,
    string Number,
    BuildState State,
    IReadOnlyList<string> FailedTests,
    string Url);

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
