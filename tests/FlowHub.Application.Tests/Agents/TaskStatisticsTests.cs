using FlowHub.Application.Agents;
using FlowHub.Domain.Flows;
using FlowHub.Domain.WorkItems;

namespace FlowHub.Application.Tests.Agents;

public sealed class TaskStatisticsTests
{
    [Fact]
    public void Retro_gets_tokens_attempts_errors_and_user_remarks_but_not_itself()
    {
        var item = WorkItem.Create(new NewWorkItem("T-1", "Zadanie", null, "Bug", TaskSourceKind.Manual, null), "work", "todo", DateTime.UtcNow);
        item.AddRepo("Repo.A");
        item.PlanSteps(new FlowDefinition
        {
            Type = "Bug",
            Name = "Błąd",
            Steps =
            [
                new StepDefinition { Id = "plan", Name = "Planowanie", Kind = StepKind.Agent },
                new StepDefinition { Id = "implement", Name = "Realizacja", Kind = StepKind.Agent, PerRepo = true },
                new StepDefinition { Id = "retro", Name = "Retro", Kind = StepKind.Agent },
                new StepDefinition { Id = "later", Name = "Później", Kind = StepKind.Manual },
            ],
        });
        var (plan, implement, retro) = (item.Steps[0], item.Steps[1], item.Steps[2]);

        Run(plan, new TokenUsage { Model = "tani", InputTokens = 1000, OutputTokens = 200, Cost = 0.0123m });
        Run(implement, new TokenUsage { Model = "drogi", InputTokens = 9000, OutputTokens = 1000, Cost = 0.5m }, fail: "Błąd kompilacji\nszczegóły");
        implement.Reopen("Użyj istniejącego serwisu");

        var stats = TaskStatistics.For(item, retro);

        Assert.Contains("| Planowanie | — | Succeeded | 1 | 1200 | 0.0123 | tani |", stats);
        Assert.Contains("| Realizacja | Repo.A | Proposed | 2 | 10000 | 0.5 | drogi |", stats);
        Assert.Contains("Razem: 11200 tokenów, 0.5123 USD.", stats);
        Assert.Contains("- Realizacja: Użyj istniejącego serwisu", stats);
        Assert.Contains("- Realizacja: Błąd kompilacji", stats);
        Assert.DoesNotContain("szczegóły", stats);
        Assert.DoesNotContain("| Retro |", stats);
        Assert.DoesNotContain("Później", stats);
    }

    private static void Run(StepRun step, TokenUsage usage, string? fail = null)
    {
        step.Propose("x", null);
        step.Approve();
        step.Start(DateTime.UtcNow);
        step.AddUsage(usage);

        if (fail is null)
        {
            step.Succeed("ok", DateTime.UtcNow);
        }
        else
        {
            step.Fail(fail, DateTime.UtcNow);
        }
    }
}
