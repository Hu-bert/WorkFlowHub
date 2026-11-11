using FlowHub.Domain.Flows;
using FlowHub.Domain.WorkItems;

namespace FlowHub.Application.WorkItems;

public sealed record CreateWorkItemRequest(
    string Key,
    string Title,
    string? Description,
    string Type,
    TaskSourceKind Source,
    string? SourceUrl,
    IReadOnlyList<string> Repositories);

public sealed record TokenUsageDto(
    string? Model,
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    long TotalTokens,
    decimal Cost);

public sealed record TaskRepoDto(string Name, string Branch, string LocalPath, RepoRole Role);

public sealed record StepRunDto(
    Guid Id,
    string StepId,
    string Name,
    StepKind Kind,
    int Order,
    string? RepoName,
    StepStatus Status,
    bool RequiresApproval,
    string? Proposal,
    string? Model,
    int TokenBudget,
    string? Result,
    string? Error,
    string? Remark,
    string Log,
    int Attempt,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    TokenUsageDto Usage,
    string? SessionId);

/// <summary>Zadanie na liście / karcie kanban.</summary>
public sealed record WorkItemSummary(
    Guid Id,
    string Key,
    string Title,
    string Type,
    string BoardColumn,
    DateTime CreatedAt,
    string? CurrentStepName,
    StepStatus? CurrentStepStatus,
    bool WaitsForUser,
    int StepsDone,
    int StepsTotal,
    IReadOnlyList<string> Repositories,
    TokenUsageDto Usage);

public sealed record WorkItemDetails(
    WorkItemSummary Summary,
    string Description,
    TaskSourceKind Source,
    string? SourceUrl,
    string WorkDirectory,
    IReadOnlyList<TaskRepoDto> Repos,
    IReadOnlyList<StepRunDto> Steps);
