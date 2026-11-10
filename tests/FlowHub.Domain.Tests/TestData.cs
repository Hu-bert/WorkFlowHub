using FlowHub.Domain.Flows;
using FlowHub.Domain.WorkItems;

namespace FlowHub.Domain.Tests;

internal static class TestData
{
    public static readonly DateTime Now = new(2026, 11, 11, 8, 0, 0, DateTimeKind.Utc);

    public static WorkItem WorkItem(string key = "PROJ-1", string title = "Błąd w fakturze") =>
        Domain.WorkItems.WorkItem.Create(
            new NewWorkItem(key, title, "Opis", "Bug", TaskSourceKind.Manual, null),
            workRoot: "work",
            boardColumn: "todo",
            Now);

    public static StepDefinition Step(string id, StepKind kind = StepKind.Script, bool perRepo = false) => new()
    {
        Id = id,
        Name = id,
        Kind = kind,
        PerRepo = perRepo,
        TokenBudget = 1000,
    };

    public static FlowDefinition Flow(params StepDefinition[] steps) => new()
    {
        Type = "Bug",
        Name = "Błąd",
        Steps = steps,
    };

    public static StepRun StepRun(StepKind kind = StepKind.Agent) =>
        Domain.WorkItems.StepRun.Create(Guid.NewGuid(), Step("step", kind), order: 1, repoName: null);
}
