using FlowHub.Application.Engine;
using FlowHub.Application.Repositories;
using FlowHub.Domain;
using FlowHub.Domain.Flows;
using FlowHub.Domain.Repositories;

namespace FlowHub.Application.Integrations;

/// <summary>
/// Krok Check: odczyt stanu z zewnątrz — bez AI. Dostępne sprawdzenia (pole `check` w YAML):
/// pull-request, build, deployment. Nowe sprawdzenie = nowa gałąź w <see cref="RunAsync"/>.
/// </summary>
public sealed class CheckStepExecutor(
    IRepositoryCatalog repositories,
    IPullRequestService pullRequests,
    ICiServer ci,
    IDeployServer deploy) : IStepExecutor
{
    public const string PullRequestCheck = "pull-request";
    public const string BuildCheck = "build";
    public const string DeploymentCheck = "deployment";

    public StepKind Kind => StepKind.Check;

    public Task<string> ProposeAsync(StepContext context, CancellationToken ct)
    {
        var repo = context.Repo?.RepositoryName ?? "repo";
        var branch = context.Repo?.Branch ?? "branch zadania";

        return Task.FromResult(context.Definition.Check switch
        {
            PullRequestCheck => $"Sprawdzę pull request dla brancha {branch} w repo {repo}.{Environment.NewLine}Jeśli PR jeszcze nie istnieje — utwórz go, zanim zatwierdzisz ten krok.",
            BuildCheck => $"Sprawdzę buildy z brancha {branch} w repo {repo}.",
            DeploymentCheck => $"Sprawdzę, gdzie wdrożona jest wersja z brancha {branch} (repo {repo}).",
            var other => $"Sprawdzenie „{other}”.",
        });
    }

    public async Task<StepOutcome> ExecuteAsync(StepContext context, IStepLog log, CancellationToken ct)
    {
        var repo = context.Repo ?? throw new DomainException("Krok Check musi być „per repo” (perRepo: true).");
        var repository = repositories.Find(repo.RepositoryName)
            ?? throw new DomainException($"Repo '{repo.RepositoryName}' nie istnieje w katalogu (repos.yaml).");

        var outcome = await RunAsync(context, repository, repo.Branch, ct);
        await log.WriteAsync(outcome.Message + Environment.NewLine, ct);
        return outcome;
    }

    private async Task<StepOutcome> RunAsync(StepContext context, CodeRepository repository, string branch, CancellationToken ct) =>
        context.Definition.Check switch
        {
            PullRequestCheck => await CheckPullRequestAsync(context, repository, branch, ct),
            BuildCheck => CheckBuilds(await ci.GetBuildsWithChangesAsync(repository, branch, ct)),
            DeploymentCheck => CheckDeployment(await deploy.GetOverviewAsync(repository, branch, ct)),
            var other => StepOutcome.Failure($"Nieznane sprawdzenie '{other}'. Dostępne: {PullRequestCheck}, {BuildCheck}, {DeploymentCheck}."),
        };

    /// <summary>Zapisuje pr-&lt;repo&gt;.md w katalogu zadania — link do PR przyda się kolejnym krokom (np. wiadomość na Teams).</summary>
    private async Task<StepOutcome> CheckPullRequestAsync(StepContext context, CodeRepository repository, string branch, CancellationToken ct)
    {
        var pr = await pullRequests.GetForBranchAsync(repository, branch, ct);
        if (pr is null)
        {
            return StepOutcome.Failure($"Nie znalazłem PR dla brancha {branch} w repo {repository.Name}. Utwórz PR i powtórz krok.");
        }

        var cell = RepoStatusBuilder.PullRequest(pr);
        var note = $"# PR: {repository.Name}{Environment.NewLine}{Environment.NewLine}- Tytuł: {pr.Title}{Environment.NewLine}- Link: {pr.Url}{Environment.NewLine}- Stan: {pr.State} ({cell.Text}){Environment.NewLine}";

        Directory.CreateDirectory(context.Item.WorkDirectory);
        await File.WriteAllTextAsync(Path.Combine(context.Item.WorkDirectory, $"pr-{repository.Name}.md"), note, ct);

        return StepOutcome.Success($"PR #{pr.Id}: {pr.State}, {cell.Text}. {pr.Url}");
    }

    private static StepOutcome CheckBuilds(IReadOnlyList<BuildStatus> builds)
    {
        var cell = RepoStatusBuilder.Build(builds);
        var text = $"Build: {cell.Text}. {cell.Url}".Trim();

        return cell.Level == HealthLevel.Ok ? StepOutcome.Success(text) : StepOutcome.Failure(text);
    }

    private static StepOutcome CheckDeployment(DeploymentOverview? overview)
    {
        if (overview is null)
        {
            return StepOutcome.Failure("Brak danych o wdrożeniach dla tego repo.");
        }

        var environments = overview.Environments.Select(env => $"{env.Environment}: {RepoStatusBuilder.Environment(env).Status.Text}");
        var text = $"Release {overview.Release ?? "—"}. {string.Join(", ", environments)}";

        return overview.Environments.Any(env => env.State == DeploymentState.Failed)
            ? StepOutcome.Failure(text)
            : StepOutcome.Success(text);
    }
}
