using FlowHub.Application.Agents;
using FlowHub.Application.Engine;
using FlowHub.Application.Flows;
using FlowHub.Application.WorkItems;
using FlowHub.Domain.Flows;
using FlowHub.Domain.WorkItems;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlowHub.Application.Tests.Engine;

/// <summary>Prawdziwy silnik flow z pamięciowym magazynem i podstawionym wykonawcą kroków.</summary>
internal sealed class EngineHarness
{
    public InMemoryWorkItemStore Store { get; } = new();
    public RecordingNotifier Live { get; } = new();
    public StepQueue Queue { get; } = new();
    public FakeExecutor Scripts { get; } = new(StepKind.Script);
    public FakeExecutor Manual { get; } = new(StepKind.Manual);
    public FakeExecutor Agents { get; } = new(StepKind.Agent);
    public StepService Service { get; }
    public WorkItem Item { get; }

    private readonly StepRunner _runner;

    public EngineHarness(params StepDocument[] steps)
    {
        var flowStore = Substitute.For<IFlowStore>();
        flowStore.LoadAll().Returns([new FlowFile("test.yaml", new FlowDocument { Type = "Test", Name = "Test", Flow = [.. steps] }, null)]);
        var flows = new FlowCatalog(flowStore, NullLogger<FlowCatalog>.Instance);

        var contexts = new StepContextFactory(flows);
        var executors = new StepExecutorRegistry([Scripts, Manual, Agents]);
        var advancer = new FlowAdvancer(Store, contexts, executors, Queue, Live);

        Service = new StepService(Store, advancer, Queue, contexts);
        _runner = new StepRunner(Store, contexts, executors, advancer, Live, TimeProvider.System, NullLogger<StepRunner>.Instance);

        Item = WorkItem.Create(
            new NewWorkItem("T-1", "Zadanie testowe", null, "Test", TaskSourceKind.Manual, null), "work", "todo", DateTime.UtcNow);
        Item.AddRepo("Repo.A");
        Item.PlanSteps(flows.Find("Test")!);
        Store.Items.Add(Item);
    }

    public static StepDocument Script(string id, bool approval = true) =>
        new() { Id = id, Kind = "Script", Script = "x.ps1", RequiresApproval = approval };

    public static StepDocument ManualStep(string id, string? followUp = null) => new() { Id = id, Kind = "Manual", FollowUp = followUp };

    public static StepDocument Agent(string id) => new() { Id = id, Kind = "Agent", Agent = "a" };

    public StepRun Step(string stepId) => Item.Steps.Single(step => step.StepId == stepId);

    public Task StartAsync() => Service.StartFlowAsync(Item.Id, CancellationToken.None);

    /// <summary>Wykonuje wszystko, co czeka w kolejce (to, co w aplikacji robi usługa w tle).</summary>
    public async Task RunQueuedAsync(CancellationToken ct = default)
    {
        while (Queue.TryDequeue(out var stepRunId))
        {
            await _runner.RunAsync(stepRunId, ct);
        }
    }
}

internal sealed class InMemoryWorkItemStore : IWorkItemStore
{
    public List<WorkItem> Items { get; } = [];
    public int Saves { get; private set; }

    public Task<IReadOnlyList<WorkItem>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<WorkItem>>(Items);

    public Task<WorkItem?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(item => item.Id == id));

    public Task<WorkItem?> FindByStepAsync(Guid stepRunId, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(item => item.Steps.Any(step => step.Id == stepRunId)));

    public Task<bool> KeyExistsAsync(string key, CancellationToken ct) => Task.FromResult(Items.Any(item => item.Key == key));

    public Task AddAsync(WorkItem workItem, CancellationToken ct)
    {
        Items.Add(workItem);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken ct)
    {
        Saves++;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingNotifier : ILiveNotifier
{
    public List<WorkItemDetails> Changes { get; } = [];
    public List<string> Output { get; } = [];

    public Task WorkItemChangedAsync(WorkItemDetails item, CancellationToken ct)
    {
        Changes.Add(item);
        return Task.CompletedTask;
    }

    public Task StepOutputAsync(Guid workItemId, Guid stepRunId, string text, CancellationToken ct)
    {
        Output.Add(text);
        return Task.CompletedTask;
    }

    public List<PermissionRequest> Permissions { get; } = [];

    public Task PermissionRequestedAsync(PermissionRequest request, CancellationToken ct)
    {
        Permissions.Add(request);
        return Task.CompletedTask;
    }

    public Task PermissionResolvedAsync(PermissionRequest request, CancellationToken ct)
    {
        Permissions.Remove(request);
        return Task.CompletedTask;
    }

    public Task RepoStatusChangedAsync(FlowHub.Application.Integrations.WorkItemRepoStatus status, CancellationToken ct) =>
        Task.CompletedTask;
}

internal sealed class FakeExecutor(StepKind kind) : IStepExecutor
{
    public StepKind Kind => kind;
    public List<string> Executed { get; } = [];
    public Func<StepContext, IStepLog, CancellationToken, Task<StepOutcome>> OnExecute { get; set; } =
        (_, _, _) => Task.FromResult(StepOutcome.Success("ok"));

    public Task<string> ProposeAsync(StepContext context, CancellationToken ct) =>
        Task.FromResult($"Propozycja: {context.Definition.Id}");

    public Task<StepOutcome> ExecuteAsync(StepContext context, IStepLog log, CancellationToken ct)
    {
        Executed.Add(context.Step.StepId);
        return OnExecute(context, log, ct);
    }
}
