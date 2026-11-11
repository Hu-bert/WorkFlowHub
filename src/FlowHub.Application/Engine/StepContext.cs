using FlowHub.Application.Flows;
using FlowHub.Domain;
using FlowHub.Domain.Flows;
using FlowHub.Domain.WorkItems;

namespace FlowHub.Application.Engine;

/// <summary>Wszystko, czego wykonawca potrzebuje, żeby zaproponować i wykonać krok.</summary>
public sealed record StepContext(WorkItem Item, StepRun Step, StepDefinition Definition)
{
    /// <summary>Repo zadania, którego dotyczy krok (tylko kroki „per repo”).</summary>
    public TaskRepo? Repo => Item.Repos.FirstOrDefault(repo => repo.RepositoryName == Step.RepoName);
}

public sealed record StepOutcome(bool Succeeded, string Message)
{
    public static StepOutcome Success(string summary) => new(true, summary);

    public static StepOutcome Failure(string error) => new(false, error);
}

public sealed class StepContextFactory(FlowCatalog flows)
{
    public StepContext Create(WorkItem item, StepRun step)
    {
        var definition = flows.Find(item.Type)?.Steps.FirstOrDefault(candidate => candidate.Id == step.StepId)
            ?? throw new DomainException(
                $"Krok '{step.StepId}' nie istnieje już w flow '{item.Type}'. Popraw plik w flows/ albo pomiń krok.");

        return new StepContext(item, step, definition);
    }
}
