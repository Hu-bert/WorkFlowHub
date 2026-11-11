using FlowHub.Domain.WorkItems;

namespace FlowHub.Application.WorkItems;

public static class WorkItemMapper
{
    public static WorkItemSummary ToSummary(WorkItem item)
    {
        var current = item.CurrentStep;

        return new WorkItemSummary(
            item.Id,
            item.Key,
            item.Title,
            item.Type,
            item.BoardColumn,
            item.CreatedAt,
            current?.Name,
            current?.Status,
            WaitsForUser(current),
            item.Steps.Count(step => step.IsFinished),
            item.Steps.Count,
            item.Repos.Select(repo => repo.RepositoryName).ToList(),
            ToDto(item.Usage));
    }

    public static WorkItemDetails ToDetails(WorkItem item) => new(
        ToSummary(item),
        item.Description,
        item.Source,
        item.SourceUrl,
        item.WorkDirectory,
        item.Repos.Select(repo => new TaskRepoDto(repo.RepositoryName, repo.Branch, repo.LocalPath, repo.Role)).ToList(),
        item.OrderedSteps.Select(ToDto).ToList());

    public static StepRunDto ToDto(StepRun step) => new(
        step.Id,
        step.StepId,
        step.Name,
        step.Kind,
        step.Order,
        step.RepoName,
        step.Status,
        step.RequiresApproval,
        step.Proposal,
        step.Model,
        step.TokenBudget,
        step.Result,
        step.Error,
        step.Remark,
        step.Log,
        step.Attempt,
        step.StartedAt,
        step.FinishedAt,
        ToDto(step.Usage),
        step.SessionId);

    public static TokenUsageDto ToDto(TokenUsage usage) => new(
        usage.Model,
        usage.InputTokens,
        usage.OutputTokens,
        usage.CacheReadTokens,
        usage.CacheWriteTokens,
        usage.TotalTokens,
        usage.Cost);

    /// <summary>Zadanie czeka na człowieka, gdy bieżący krok czeka na decyzję albo się nie udał.</summary>
    private static bool WaitsForUser(StepRun? current) =>
        current?.Status is StepStatus.Proposed or StepStatus.Failed;
}
