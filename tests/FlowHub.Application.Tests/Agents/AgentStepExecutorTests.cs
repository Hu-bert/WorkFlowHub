using System.Runtime.CompilerServices;
using FlowHub.Application.Agents;
using FlowHub.Application.Blocks;
using FlowHub.Application.Engine;
using FlowHub.Application.Tests.Engine;
using FlowHub.Domain.Flows;
using FlowHub.Domain.WorkItems;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FlowHub.Application.Tests.Agents;

public sealed class AgentStepExecutorTests : IDisposable
{
    private readonly string _workRoot = Directory.CreateTempSubdirectory("flowhub-agent-").FullName;
    private readonly IBlockLibrary _blocks = Substitute.For<IBlockLibrary>();
    private readonly ScriptedAgentRunner _runner = new();
    private readonly RecordingNotifier _live = new();
    private readonly List<string> _log = [];
    private readonly IStepLog _stepLog = Substitute.For<IStepLog>();
    private readonly AgentStepExecutor _executor;

    public AgentStepExecutorTests()
    {
        _blocks.FullPath(Arg.Any<string>()).Returns(call => Path.Combine("blocks", call.Arg<string>()));
        _stepLog.WriteAsync(Arg.Do<string>(_log.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _executor = Executor(new PermissionBroker(_live));
    }

    public void Dispose() => Directory.Delete(_workRoot, recursive: true);

    private static readonly IOptions<ModelOptions> Models = Options.Create(new ModelOptions
    {
        DefaultTier = "cheap",
        TaskTokenBudget = 100_000,
        Tiers = { ["cheap"] = "model-tani", ["standard"] = "model-sredni" },
        Prices = { ["model-tani"] = new ModelPrice { InputPerMillion = 1m, OutputPerMillion = 10m } },
    });

    private AgentStepExecutor Executor(PermissionBroker broker) =>
        new(_runner, new AgentPromptBuilder(_blocks), broker, new ModelRouter(Models), new TokenBudget(Models));

    private StepContext Context(StepDefinition? definition = null)
    {
        definition ??= new StepDefinition { Id = "plan", Name = "Planowanie", Kind = StepKind.Agent, Agent = "planowanie" };
        var item = WorkItem.Create(
            new NewWorkItem("T-1", "Zła stawka", "Opis", "Bug", TaskSourceKind.Manual, null), _workRoot, "todo", DateTime.UtcNow);
        item.AddRepo("Repo.A");
        item.PlanSteps(new FlowDefinition { Type = "Bug", Name = "Błąd", Steps = [definition] });
        Directory.CreateDirectory(item.WorkDirectory);

        return new StepContext(item, item.Steps[0], definition);
    }

    [Fact]
    public async Task Agent_events_become_console_output_token_usage_and_the_result()
    {
        var context = Context();
        _runner.Events =
        [
            new AgentEvent.MessageDelta("Czytam "),
            new AgentEvent.MessageDelta("zadanie."),
            new AgentEvent.ToolStarted("read", "task.md"),
            new AgentEvent.ToolFinished("read", true),
            new AgentEvent.Usage(new TokenUsage { Model = "m1", InputTokens = 1000, OutputTokens = 200 }),
            new AgentEvent.Usage(new TokenUsage { Model = "m1", InputTokens = 500, OutputTokens = 100 }),
            new AgentEvent.Completed("Plan gotowy."),
        ];

        var outcome = await _executor.ExecuteAsync(context, _stepLog, CancellationToken.None);

        Assert.True(outcome.Succeeded);
        Assert.Equal("Plan gotowy.", outcome.Message);
        Assert.Equal(1800, context.Step.Usage.TotalTokens);
        Assert.StartsWith("Model: model-tani", string.Concat(_log));
        Assert.Contains("Czytam zadanie.", string.Concat(_log));
        Assert.Contains("🔧 read: task.md", string.Concat(_log));
        Assert.Contains("✓ read", string.Concat(_log));
    }

    [Fact]
    public async Task Agent_gets_the_approved_prompt_with_the_users_remark()
    {
        var context = Context();
        context.Step.Propose("Mój poprawiony prompt", "model-x");
        context.Step.Approve();
        context.Step.Start(DateTime.UtcNow);
        context.Step.Fail("x", DateTime.UtcNow);
        context.Step.Reopen("Najpierw biblioteka");
        _runner.Events = [new AgentEvent.Completed("ok")];

        await _executor.ExecuteAsync(context, _stepLog, CancellationToken.None);

        var request = _runner.LastRequest!;
        Assert.StartsWith("Mój poprawiony prompt", request.Prompt);
        Assert.EndsWith("Uwaga użytkownika do tej próby: Najpierw biblioteka", request.Prompt);
        Assert.Equal("planowanie", request.Agent);
        Assert.Equal("model-x", request.Model);
        Assert.Equal(context.Item.Repos[0].LocalPath, request.WorkingDirectory);
        Assert.Equal([context.Item.WorkDirectory], request.AdditionalDirectories);
    }

    [Fact]
    public async Task Step_fails_when_the_agent_did_not_create_the_expected_file()
    {
        var context = Context(new StepDefinition { Id = "plan", Name = "Plan", Kind = StepKind.Agent, Agent = "a", Outputs = ["plan.md"] });
        _runner.Events = [new AgentEvent.Completed("Gotowe")];

        var missing = await _executor.ExecuteAsync(context, _stepLog, CancellationToken.None);
        File.WriteAllText(Path.Combine(context.Item.WorkDirectory, "plan.md"), "# Plan");
        var present = await _executor.ExecuteAsync(context, _stepLog, CancellationToken.None);

        Assert.False(missing.Succeeded);
        Assert.Contains("plan.md", missing.Message);
        Assert.True(present.Succeeded);
    }

    [Fact]
    public async Task Failed_event_or_no_result_fails_the_step()
    {
        _runner.Events = [new AgentEvent.MessageDelta("…"), new AgentEvent.Failed("Limit zapytań")];
        var failed = await _executor.ExecuteAsync(Context(), _stepLog, CancellationToken.None);

        _runner.Events = [new AgentEvent.MessageDelta("…")];
        var silent = await _executor.ExecuteAsync(Context(), _stepLog, CancellationToken.None);

        Assert.Equal("Limit zapytań", failed.Message);
        Assert.False(silent.Succeeded);
        Assert.Contains("bez wyniku", silent.Message);
    }

    [Fact]
    public async Task Permission_question_waits_for_the_users_decision()
    {
        var context = Context();
        var broker = new PermissionBroker(_live);
        var executor = Executor(broker);
        _runner.AskFor = ("write", "Zapis w repo");
        _runner.Events = [new AgentEvent.Completed("ok")];

        var running = executor.ExecuteAsync(context, _stepLog, CancellationToken.None);
        var request = await WaitForAsync(() => _live.Permissions.FirstOrDefault());

        Assert.False(running.IsCompleted);
        Assert.Equal("write", request.Tool);
        Assert.Equal(context.Step.Id, request.StepRunId);
        Assert.Single(broker.PendingFor(context.Item.Id));

        Assert.True(broker.Decide(request.Id, allow: true));
        await running;

        Assert.True(_runner.PermissionGranted);
        Assert.Empty(broker.PendingFor(context.Item.Id));
        Assert.False(broker.Decide(request.Id, allow: true));
    }

    [Fact]
    public async Task Tier_is_resolved_to_a_model_and_usage_is_priced()
    {
        var context = Context(new StepDefinition { Id = "plan", Name = "Plan", Kind = StepKind.Agent, Agent = "a", Model = "tier:standard" });
        _runner.Events =
        [
            new AgentEvent.Usage(new TokenUsage { Model = "model-tani", InputTokens = 1_000_000, OutputTokens = 100_000 }),
            new AgentEvent.Completed("ok"),
        ];

        await _executor.ExecuteAsync(context, _stepLog, CancellationToken.None);

        Assert.Equal("model-sredni", _runner.LastRequest!.Model);
        Assert.Equal(2.0m, context.Step.Usage.Cost);
    }

    [Fact]
    public async Task Step_warns_at_80_percent_and_stops_at_100_percent_of_its_budget()
    {
        var context = Context(new StepDefinition { Id = "plan", Name = "Plan", Kind = StepKind.Agent, Agent = "a", TokenBudget = 1000 });
        _runner.Events =
        [
            new AgentEvent.Usage(new TokenUsage { InputTokens = 850 }),
            new AgentEvent.MessageDelta("pracuję dalej"),
            new AgentEvent.Usage(new TokenUsage { InputTokens = 200 }),
            new AgentEvent.MessageDelta("tego już nie powinno być"),
            new AgentEvent.Completed("ok"),
        ];

        var outcome = await _executor.ExecuteAsync(context, _stepLog, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("Przekroczono budżet tokenów kroku", outcome.Message);
        Assert.Contains("80% budżetu", string.Concat(_log));
        Assert.Contains("pracuję dalej", string.Concat(_log));
        Assert.DoesNotContain("tego już nie powinno być", string.Concat(_log));
    }

    [Fact]
    public async Task Agent_does_not_start_when_the_task_budget_is_used_up()
    {
        var context = Context();
        context.Step.AddUsage(new TokenUsage { InputTokens = 100_000 });

        var outcome = await _executor.ExecuteAsync(context, _stepLog, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("Budżet tokenów zadania jest wyczerpany", outcome.Message);
        Assert.Null(_runner.LastRequest);
    }

    private static async Task<T> WaitForAsync<T>(Func<T?> probe) where T : class
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (probe() is { } value)
            {
                return value;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Nie doczekałem się prośby o zgodę.");
    }

    private sealed class ScriptedAgentRunner : IAgentRunner
    {
        public IReadOnlyList<AgentEvent> Events { get; set; } = [];
        public (string Tool, string Detail)? AskFor { get; set; }
        public AgentRequest? LastRequest { get; private set; }
        public bool? PermissionGranted { get; private set; }

        public async IAsyncEnumerable<AgentEvent> RunAsync(AgentRequest request, [EnumeratorCancellation] CancellationToken ct)
        {
            LastRequest = request;

            if (AskFor is { } ask)
            {
                PermissionGranted = await request.AskPermission(ask.Tool, ask.Detail, ct);
            }

            foreach (var agentEvent in Events)
            {
                yield return agentEvent;
            }
        }
    }
}
