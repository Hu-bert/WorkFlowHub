using FlowHub.Domain.Flows;

namespace FlowHub.Application.Engine;

/// <summary>Krok Manual: checklista dla człowieka. „Zatwierdź” = potwierdzam, że zrobione.</summary>
public sealed class ManualStepExecutor : IStepExecutor
{
    public StepKind Kind => StepKind.Manual;

    public Task<string> ProposeAsync(StepContext context, CancellationToken ct)
    {
        var checklist = context.Definition.Checklist;

        var text = checklist.Count == 0
            ? "Krok ręczny — zatwierdź, gdy będzie zrobione."
            : string.Join(Environment.NewLine, checklist.Select(point => $"☐ {point}"));

        return Task.FromResult(text);
    }

    public Task<StepOutcome> ExecuteAsync(StepContext context, IStepLog log, CancellationToken ct) =>
        Task.FromResult(StepOutcome.Success("Potwierdzone ręcznie."));
}
