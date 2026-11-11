using FlowHub.Application.Engine;
using FlowHub.Domain.Flows;

namespace FlowHub.Application.Agents;

/// <summary>Krok Agent: jedna sesja Copilot. Strumień zdarzeń trafia do konsoli, tokeny do kroku.</summary>
public sealed class AgentStepExecutor(IAgentRunner runner, AgentPromptBuilder prompts, PermissionBroker permissions) : IStepExecutor
{
    private const int SummaryMaxLength = 2000;

    public StepKind Kind => StepKind.Agent;

    public Task<string> ProposeAsync(StepContext context, CancellationToken ct) =>
        Task.FromResult(prompts.Build(context));

    public async Task<StepOutcome> ExecuteAsync(StepContext context, IStepLog log, CancellationToken ct)
    {
        var request = Request(context);
        StepOutcome? outcome = null;

        await foreach (var agentEvent in runner.RunAsync(request, ct))
        {
            outcome = await HandleAsync(agentEvent, context, log, ct) ?? outcome;
        }

        outcome ??= StepOutcome.Failure("Agent zakończył pracę bez wyniku.");
        return outcome.Succeeded ? VerifyOutputs(request, outcome) : outcome;
    }

    private AgentRequest Request(StepContext context)
    {
        var item = context.Item;
        var step = context.Step;

        // Sesja startuje w repo (tam są skopiowani agenci i skille), a katalog zadania jest dodatkowo dostępny.
        var repo = context.Repo ?? item.Repos.FirstOrDefault();

        return new AgentRequest
        {
            Prompt = PromptWithRemark(context),
            Agent = context.Definition.Agent,
            Model = step.Model,
            WorkingDirectory = repo?.LocalPath ?? item.WorkDirectory,
            AdditionalDirectories = [item.WorkDirectory],
            AllowedTools = context.Definition.AllowedTools,
            ExpectedOutputs = prompts.OutputFiles(context),
            AskPermission = (tool, detail, token) => permissions.AskAsync(item.Id, step.Id, tool, detail, token),
        };
    }

    /// <summary>Prompt to zatwierdzona (być może edytowana) propozycja + uwaga z „Powtórz z uwagą”.</summary>
    private string PromptWithRemark(StepContext context)
    {
        var prompt = context.Step.Proposal ?? prompts.Build(context);

        return context.Step.Remark is { } remark
            ? $"{prompt}{Environment.NewLine}{Environment.NewLine}Uwaga użytkownika do tej próby: {remark}"
            : prompt;
    }

    private static async Task<StepOutcome?> HandleAsync(AgentEvent agentEvent, StepContext context, IStepLog log, CancellationToken ct)
    {
        switch (agentEvent)
        {
            case AgentEvent.MessageDelta delta:
                await log.WriteAsync(delta.Text, ct);
                return null;

            case AgentEvent.ToolStarted tool:
                await log.WriteAsync($"{Environment.NewLine}🔧 {tool.Tool}{(tool.Detail is null ? "" : $": {tool.Detail}")}{Environment.NewLine}", ct);
                return null;

            case AgentEvent.ToolFinished tool:
                await log.WriteAsync($"   {(tool.Succeeded ? "✓" : "✗")} {tool.Tool}{Environment.NewLine}", ct);
                return null;

            case AgentEvent.Usage usage:
                context.Step.AddUsage(usage.Tokens);
                return null;

            case AgentEvent.Completed completed:
                return StepOutcome.Success(Shorten(completed.Summary));

            case AgentEvent.Failed failed:
                return StepOutcome.Failure(failed.Error);

            default:
                return null;
        }
    }

    private static StepOutcome VerifyOutputs(AgentRequest request, StepOutcome outcome)
    {
        var missing = request.ExpectedOutputs.Where(file => !File.Exists(file)).Select(Path.GetFileName).ToList();

        return missing.Count == 0
            ? outcome
            : StepOutcome.Failure($"Agent nie utworzył oczekiwanych plików: {string.Join(", ", missing)}.");
    }

    private static string Shorten(string summary) =>
        summary.Length <= SummaryMaxLength ? summary : summary[..SummaryMaxLength] + "…";
}
