using FlowHub.Domain.WorkItems;

namespace FlowHub.Domain.Tests;

public sealed class StepRunTests
{
    [Fact]
    public void Goes_from_proposal_to_success()
    {
        var step = TestData.StepRun();

        step.Propose("Zrób analizę", "model-a");
        step.Approve();
        step.Start(TestData.Now);
        step.Succeed("Gotowe", TestData.Now.AddMinutes(2));

        Assert.Equal(StepStatus.Succeeded, step.Status);
        Assert.Equal("Gotowe", step.Result);
        Assert.Equal(TimeSpan.FromMinutes(2), step.Duration);
        Assert.True(step.IsFinished);
    }

    [Fact]
    public void Cannot_start_without_approval()
    {
        var step = TestData.StepRun();
        step.Propose("Zrób analizę", null);

        Assert.Throws<DomainException>(() => step.Start(TestData.Now));
    }

    [Fact]
    public void Proposal_can_be_edited_only_before_approval()
    {
        var step = TestData.StepRun();
        step.Propose("Wersja 1", "model-a");

        step.EditProposal("Wersja 2", "model-b", 5000);
        step.Approve();

        Assert.Equal("Wersja 2", step.Proposal);
        Assert.Equal("model-b", step.Model);
        Assert.Equal(5000, step.TokenBudget);
        Assert.Throws<DomainException>(() => step.EditProposal("Wersja 3", null, null));
    }

    [Fact]
    public void Reopen_keeps_the_remark_and_counts_the_attempt()
    {
        var step = TestData.StepRun();
        step.Propose("Zrób", null);
        step.Approve();
        step.Start(TestData.Now);
        step.Fail("Błąd kompilacji", TestData.Now);

        step.Reopen("  Użyj innej biblioteki  ");

        Assert.Equal(StepStatus.Proposed, step.Status);
        Assert.Equal("Użyj innej biblioteki", step.Remark);
        Assert.Equal(2, step.Attempt);
    }

    [Fact]
    public void Usage_adds_up_and_detects_exceeded_budget()
    {
        var step = TestData.StepRun();

        step.AddUsage(new TokenUsage { Model = "m", InputTokens = 600, OutputTokens = 100, Cost = 0.01m });
        Assert.False(step.IsOverBudget);

        step.AddUsage(new TokenUsage { Model = "m", InputTokens = 250, OutputTokens = 50, Cost = 0.02m });

        Assert.Equal(1000, step.Usage.TotalTokens);
        Assert.Equal(0.03m, step.Usage.Cost);
        Assert.True(step.IsOverBudget);
    }

    [Fact]
    public void Starting_again_clears_the_previous_result()
    {
        var step = TestData.StepRun();
        step.Propose("Zrób", null);
        step.Approve();
        step.Start(TestData.Now);
        step.AppendLog("log 1");
        step.AddUsage(new TokenUsage { InputTokens = 10 });
        step.Succeed("Wynik 1", TestData.Now);
        step.Reopen(null);
        step.Approve();

        step.Start(TestData.Now);

        Assert.Null(step.Result);
        Assert.Equal("", step.Log);
        Assert.Equal(0, step.Usage.TotalTokens);
    }
}
