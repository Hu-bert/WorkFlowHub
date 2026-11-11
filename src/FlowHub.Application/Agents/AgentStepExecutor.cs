using FlowHub.Application.Engine;
using FlowHub.Domain.Flows;

namespace FlowHub.Application.Agents;

/// <summary>Krok Agent: jedna sesja Copilot. Strumień zdarzeń trafia do konsoli, tokeny do kroku.</summary>
public sealed class AgentStepExecutor(
    IAgentRunner runner,
    AgentPromptBuilder prompts,
    PermissionBroker permissions,
    ModelRouter models,
    TokenBudget budget) : IStepExecutor
{
    private const int SummaryMaxLength = 2000;

    public StepKind Kind => StepKind.Agent;

    public Task<string> ProposeAsync(StepContext context, CancellationToken ct) =>
        Task.FromResult(prompts.Build(context));

    public async Task<StepOutcome> ExecuteAsync(StepContext context, IStepLog log, CancellationToken ct)
    {
        if (budget.TaskState(context.Item) == BudgetState.Exceeded)
        {
            return StepOutcome.Failure(
                $"Budżet tokenów zadania jest wyczerpany ({context.Item.Usage.TotalTokens:N0} / {budget.TaskLimit:N0}). "
                + "Zwiększ Models:TaskTokenBudget albo pomiń krok.");
        }

        var request = Request(context);
        var session = new AgentSession(context, log, models, budget);
        await log.WriteAsync($"Model: {request.Model}{Environment.NewLine}", ct);

        // Przerwanie pętli (przekroczony budżet) kończy sesję agenta — runner sprząta po sobie.
        await foreach (var agentEvent in runner.RunAsync(request, ct))
        {
            await session.HandleAsync(agentEvent, ct);

            if (session.OverBudget && !session.Finished)
            {
                return StepOutcome.Failure(
                    $"Przekroczono budżet tokenów kroku ({context.Step.Usage.TotalTokens:N0} / {context.Step.TokenBudget:N0}). "
                    + "Zwiększ budżet w propozycji i powtórz krok.");
            }
        }

        var outcome = session.Outcome ?? StepOutcome.Failure("Agent zakończył pracę bez wyniku.");
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
            Model = models.Resolve(step.Model),
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

    private static StepOutcome VerifyOutputs(AgentRequest request, StepOutcome outcome)
    {
        var missing = request.ExpectedOutputs.Where(file => !File.Exists(file)).Select(Path.GetFileName).ToList();

        return missing.Count == 0
            ? outcome
            : StepOutcome.Failure($"Agent nie utworzył oczekiwanych plików: {string.Join(", ", missing)}.");
    }

    /// <summary>Stan jednej sesji: przekłada zdarzenia agenta na log, tokeny i wynik kroku.</summary>
    private sealed class AgentSession(StepContext context, IStepLog log, ModelRouter models, TokenBudget budget)
    {
        private bool _warned;

        public StepOutcome? Outcome { get; private set; }

        public bool OverBudget => budget.StepState(context.Step) == BudgetState.Exceeded;

        /// <summary>Sesja już się skończyła (zużycie przyszło jako podsumowanie) — nie ma czego przerywać.</summary>
        public bool Finished { get; private set; }

        public async Task HandleAsync(AgentEvent agentEvent, CancellationToken ct)
        {
            switch (agentEvent)
            {
                case AgentEvent.SessionStarted started:
                    context.Step.AttachSession(started.SessionId);
                    break;

                case AgentEvent.MessageDelta delta:
                    await log.WriteAsync(delta.Text, ct);
                    break;

                case AgentEvent.ToolStarted tool:
                    await log.WriteAsync($"{Environment.NewLine}🔧 {tool.Tool}{(tool.Detail is null ? "" : $": {tool.Detail}")}{Environment.NewLine}", ct);
                    break;

                case AgentEvent.ToolFinished tool:
                    await log.WriteAsync($"   {(tool.Succeeded ? "✓" : "✗")} {tool.Tool}{Environment.NewLine}", ct);
                    break;

                case AgentEvent.Usage usage:
                    context.Step.AddUsage(models.Price(usage.Tokens));
                    Finished |= usage.Final;
                    await WarnOnceAsync(ct);
                    break;

                case AgentEvent.Completed completed:
                    Outcome = StepOutcome.Success(Shorten(completed.Summary));
                    break;

                case AgentEvent.Failed failed:
                    Outcome = StepOutcome.Failure(failed.Error);
                    break;
            }
        }

        private async Task WarnOnceAsync(CancellationToken ct)
        {
            var state = budget.StepState(context.Step);
            if (_warned || state == BudgetState.Ok || (state == BudgetState.Exceeded && !Finished))
            {
                return;
            }

            _warned = true;
            var what = state == BudgetState.Exceeded ? "Sesja przekroczyła budżet kroku" : "Zużyto ponad 80% budżetu kroku";
            await log.WriteAsync(
                $"{Environment.NewLine}⚠ {what} ({context.Step.Usage.TotalTokens:N0} / {context.Step.TokenBudget:N0} tokenów).{Environment.NewLine}",
                ct);
        }

        private static string Shorten(string summary) =>
            summary.Length <= SummaryMaxLength ? summary : summary[..SummaryMaxLength] + "…";
    }
}
