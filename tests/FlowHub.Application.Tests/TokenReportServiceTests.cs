using FlowHub.Application.Tests.Engine;
using FlowHub.Application.Tokens;
using FlowHub.Domain.Flows;
using FlowHub.Domain.WorkItems;

namespace FlowHub.Application.Tests;

public sealed class TokenReportServiceTests
{
    private static readonly DateTime Day1 = new(2026, 11, 10, 10, 0, 0, DateTimeKind.Local);

    private static WorkItem Item(string key, string type, params (string Step, string Model, long Input, long Output, decimal Cost, int Day)[] runs)
    {
        var item = WorkItem.Create(new NewWorkItem(key, $"Zadanie {key}", null, type, TaskSourceKind.Manual, null), "work", "todo", DateTime.UtcNow);
        item.AddRepo("Repo.A");
        item.PlanSteps(new FlowDefinition
        {
            Type = type,
            Name = type,
            Steps = runs.Select(run => new StepDefinition { Id = run.Step, Name = run.Step, Kind = StepKind.Agent }).ToList(),
        });

        foreach (var (step, run) in item.Steps.Zip(runs))
        {
            step.Propose("x", null);
            step.Approve();
            step.Start(Day1.AddDays(run.Day).ToUniversalTime());
            step.AddUsage(new TokenUsage { Model = run.Model, InputTokens = run.Input, OutputTokens = run.Output, Cost = run.Cost });
            step.Succeed("ok", Day1.AddDays(run.Day).ToUniversalTime());
        }

        return item;
    }

    [Fact]
    public async Task Report_sums_usage_by_model_type_item_step_and_day()
    {
        var store = new InMemoryWorkItemStore();
        store.Items.Add(Item("A-1", "Bug", ("plan", "tani", 1000, 100, 0.01m, 0), ("implement", "drogi", 5000, 900, 0.50m, 1)));
        store.Items.Add(Item("A-2", "Business", ("plan", "tani", 2000, 200, 0.02m, 1)));

        var report = await new TokenReportService(store).BuildAsync(CancellationToken.None);

        Assert.Equal((9200, 0.53m, 3), (report.Total.TotalTokens, report.Total.Cost, report.Total.Runs));
        Assert.Equal(["drogi", "tani"], report.ByModel.Select(row => row.Label));
        Assert.Equal(3300, report.ByModel[1].TotalTokens);
        Assert.Equal(["Bug", "Business"], report.ByType.Select(row => row.Label));
        Assert.StartsWith("A-1", report.ByWorkItem[0].Label);
        Assert.Equal(("implement", 5900L), (report.ByStep[0].Label, report.ByStep[0].TotalTokens));
        Assert.Equal(["2026-11-10", "2026-11-11"], report.ByDay.Select(row => row.Label));
        Assert.Equal(8100, report.ByDay[1].TotalTokens);
    }

    [Fact]
    public async Task Steps_without_tokens_are_left_out()
    {
        var store = new InMemoryWorkItemStore();
        store.Items.Add(Item("A-1", "Bug", ("script", "x", 0, 0, 0m, 0)));

        var report = await new TokenReportService(store).BuildAsync(CancellationToken.None);

        Assert.Equal(0, report.Total.Runs);
        Assert.Empty(report.ByModel);
    }
}
