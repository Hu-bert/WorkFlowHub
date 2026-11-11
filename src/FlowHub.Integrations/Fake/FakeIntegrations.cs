using FlowHub.Application.Integrations;
using FlowHub.Domain.Repositories;
using static FlowHub.Integrations.Fake.FakeScenarios;

namespace FlowHub.Integrations.Fake;

/// <summary>Tracker-atrapa: zwraca przykładowe zadanie dla dowolnego klucza.</summary>
public sealed class FakeTaskSource : ITaskSource
{
    public Task<TaskDetails?> GetAsync(string key, CancellationToken ct) => Task.FromResult<TaskDetails?>(new TaskDetails(
        key,
        "Zła stawka VAT na fakturze po zmianie płatności",
        """
        Po zmianie metody płatności w portalu klienta faktura dostaje stawkę VAT z poprzedniej metody.

        Kroki:
        1. Utwórz zamówienie z płatnością kartą.
        2. Zmień płatność na przelew.
        3. Wystaw fakturę.

        Oczekiwane: stawka zgodna z aktualną metodą płatności.
        Jest: stawka z pierwszej metody.
        """,
        $"https://jira.example.local/browse/{key}"));
}

/// <summary>PR-y w TFS — atrapa.</summary>
public sealed class FakePullRequestService : IPullRequestService
{
    public Task<PullRequestStatus?> GetForBranchAsync(CodeRepository repository, string branch, CancellationToken ct)
    {
        var id = Number(repository.Name + branch, 100, 9000);
        var url = $"{TfsUrl}/{repository.Name}/pullrequest/{id}";
        var title = $"{branch.Replace("feature/", "")}";

        PullRequestStatus pr = For(repository.Name) switch
        {
            FakeScenario.AllGreen => new(id, title, PullRequestState.Completed,
                [new("Anna Nowak", ReviewerVote.Approved), new("Piotr Zieliński", ReviewerVote.Approved)],
                0, Policies(build: true, reviewers: true), false, url),

            FakeScenario.Broken => new(id, title, PullRequestState.Active,
                [new("Anna Nowak", ReviewerVote.Waiting), new("Piotr Zieliński", ReviewerVote.Rejected)],
                3, Policies(build: false, reviewers: false), true, url),

            _ => new(id, title, PullRequestState.Active,
                [new("Anna Nowak", ReviewerVote.Approved), new("Piotr Zieliński", ReviewerVote.ApprovedWithSuggestions), new("Ewa Kowalska", ReviewerVote.Waiting)],
                0, Policies(build: true, reviewers: false), false, url),
        };

        return Task.FromResult<PullRequestStatus?>(pr);
    }

    private static PolicyCheck[] Policies(bool build, bool reviewers) =>
    [
        new("Build validation", build),
        new("Wymagani recenzenci", reviewers),
        new("Powiązany work item", true),
    ];
}

/// <summary>TeamCity — atrapa.</summary>
public sealed class FakeCiServer : ICiServer
{
    private static readonly DateTime BaseDate = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    public Task<IReadOnlyList<CiChange>> GetChangesAsync(CodeRepository repository, string branch, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CiChange>>(
        [
            new(Revision(repository.Name, 1), "Ty", "Poprawka wyliczania stawki", BaseDate),
            new(Revision(repository.Name, 2), "Ty", "Test regresyjny", BaseDate.AddHours(2)),
        ]);

    public Task<IReadOnlyList<BuildStatus>> GetBuildsWithChangesAsync(CodeRepository repository, string branch, CancellationToken ct)
    {
        var scenario = For(repository.Name);

        IReadOnlyList<BuildStatus> builds = repository.CiBuildConfigurations
            .DefaultIfEmpty($"{repository.Name}_Build")
            .Select((configuration, index) => Build(configuration, scenario, failing: scenario == FakeScenario.Broken && index == 0))
            .ToList();

        return Task.FromResult(builds);
    }

    private static BuildStatus Build(string configuration, FakeScenario scenario, bool failing)
    {
        var number = Number(configuration, 300, 5000).ToString();
        var url = $"{TeamCityUrl}/buildConfiguration/{configuration}/{number}";

        return failing
            ? new(configuration, number, BuildState.Failed, ["Checkout.Tests.VatRate_AfterPaymentChange", "Checkout.Tests.Invoice_Totals"], url)
            : new(configuration, number, scenario == FakeScenario.Broken ? BuildState.Running : BuildState.Success, [], url);
    }

    private static string Revision(string name, int index) => Number(name + index, 0x100000, 0xFFFFFF).ToString("x7");
}

/// <summary>Octopus — atrapa. Środowiska: DEV, TEST, UAT, PROD.</summary>
public sealed class FakeDeployServer : IDeployServer
{
    private static readonly DateTime BaseDate = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    public Task<DeploymentOverview?> GetOverviewAsync(CodeRepository repository, string branch, CancellationToken ct)
    {
        var project = repository.DeployProject ?? repository.Name;
        var release = $"{Number(project, 1, 4)}.{Number(project + "minor", 0, 12)}.{Number(branch, 0, 30)}";

        DeploymentState[] states = For(repository.Name) switch
        {
            FakeScenario.AllGreen => [DeploymentState.Success, DeploymentState.Success, DeploymentState.None, DeploymentState.None],
            FakeScenario.Broken => [DeploymentState.None, DeploymentState.None, DeploymentState.None, DeploymentState.None],
            _ => [DeploymentState.Success, DeploymentState.Executing, DeploymentState.None, DeploymentState.None],
        };

        string[] environments = ["DEV", "TEST", "UAT", "PROD"];
        var deployments = environments.Zip(states, (environment, state) => new EnvironmentDeployment(
            environment,
            state == DeploymentState.None ? null : release,
            state,
            state == DeploymentState.None ? null : BaseDate,
            state == DeploymentState.None ? null : $"{OctopusUrl}/app#/projects/{project}/releases/{release}")).ToList();

        var anyDeployed = states.Any(state => state != DeploymentState.None);
        return Task.FromResult<DeploymentOverview?>(new DeploymentOverview(project, anyDeployed ? release : null, deployments));
    }
}

/// <summary>Lista repo z serwera — atrapa: repo z katalogu demo i kilka, których w katalogu jeszcze nie ma.</summary>
public sealed class FakeRepositoryDirectory : IRepositoryDirectory
{
    private static readonly string[] Names =
        ["Billing.Api", "Shared.Lib", "Customer.Portal", "Orders.Service", "Notifications.Worker", "Reporting.Web"];

    public Task<IReadOnlyList<RemoteRepository>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RemoteRepository>>(Names
            .Select(name => new RemoteRepository(name, $"demo://{name}", "main", $"{TfsUrl}/{name}"))
            .ToList());
}
