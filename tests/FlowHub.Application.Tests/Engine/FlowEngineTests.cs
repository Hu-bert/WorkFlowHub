using FlowHub.Application.Engine;
using FlowHub.Domain;
using FlowHub.Domain.WorkItems;
using Microsoft.Extensions.Logging.Abstractions;
using static FlowHub.Application.Tests.Engine.EngineHarness;

namespace FlowHub.Application.Tests.Engine;

public sealed class FlowEngineTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task Start_proposes_the_first_step_and_waits_for_the_user()
    {
        var engine = new EngineHarness(Script("one"), Script("two"));

        await engine.StartAsync();
        await engine.RunQueuedAsync();

        Assert.Equal(StepStatus.Proposed, engine.Step("one").Status);
        Assert.Equal("Propozycja: one", engine.Step("one").Proposal);
        Assert.Equal(StepStatus.Pending, engine.Step("two").Status);
        Assert.Empty(engine.Scripts.Executed);
    }

    [Fact]
    public async Task Steps_without_approval_run_on_their_own_until_one_needs_the_user()
    {
        var engine = new EngineHarness(Script("auto-1", approval: false), Script("auto-2", approval: false), Script("ask"));

        await engine.StartAsync();
        await engine.RunQueuedAsync();

        Assert.Equal(["auto-1", "auto-2"], engine.Scripts.Executed);
        Assert.Equal(StepStatus.Proposed, engine.Step("ask").Status);
    }

    [Fact]
    public async Task Approved_step_runs_and_the_next_one_is_proposed()
    {
        var engine = new EngineHarness(Script("one"), ManualStep("two"));
        await engine.StartAsync();

        await engine.Service.ApproveAsync(engine.Step("one").Id, None);
        await engine.RunQueuedAsync();

        Assert.Equal(StepStatus.Succeeded, engine.Step("one").Status);
        Assert.Equal("ok", engine.Step("one").Result);
        Assert.Equal(StepStatus.Proposed, engine.Step("two").Status);
    }

    [Fact]
    public async Task Whole_flow_finishes_when_every_step_is_done_or_skipped()
    {
        var engine = new EngineHarness(Script("one"), ManualStep("two"));
        await engine.StartAsync();

        await engine.Service.SkipAsync(engine.Step("one").Id, None);
        await engine.Service.ApproveAsync(engine.Step("two").Id, None);
        await engine.RunQueuedAsync();

        Assert.True(engine.Item.IsFinished);
        Assert.Equal(["two"], engine.Manual.Executed);
    }

    [Fact]
    public async Task Failed_step_stops_the_flow_and_can_be_retried_with_a_remark()
    {
        var engine = new EngineHarness(Script("one"), Script("two"));
        engine.Scripts.OnExecute = (_, _, _) => Task.FromResult(StepOutcome.Failure("Kod wyjścia 1"));
        await engine.StartAsync();
        await engine.Service.ApproveAsync(engine.Step("one").Id, None);
        await engine.RunQueuedAsync();

        Assert.Equal(StepStatus.Failed, engine.Step("one").Status);
        Assert.Equal("Kod wyjścia 1", engine.Step("one").Error);
        Assert.Equal(StepStatus.Pending, engine.Step("two").Status);

        await engine.Service.RetryAsync(engine.Step("one").Id, "Spróbuj z flagą --force", None);

        Assert.Equal(StepStatus.Proposed, engine.Step("one").Status);
        Assert.Equal("Spróbuj z flagą --force", engine.Step("one").Remark);
        Assert.Equal(2, engine.Step("one").Attempt);
    }

    [Fact]
    public async Task Finished_step_can_be_repeated_and_later_steps_wait()
    {
        var engine = new EngineHarness(Script("one"), Script("two"));
        await engine.StartAsync();
        await engine.Service.ApproveAsync(engine.Step("one").Id, None);
        await engine.RunQueuedAsync();

        await engine.Service.RetryAsync(engine.Step("one").Id, "Jeszcze raz", None);

        Assert.Equal("one", engine.Item.CurrentStep!.StepId);
        await Assert.ThrowsAsync<DomainException>(() => engine.Service.ApproveAsync(engine.Step("two").Id, None));
    }

    [Fact]
    public async Task Edited_proposal_model_and_budget_are_kept()
    {
        var engine = new EngineHarness(Script("one"));
        await engine.StartAsync();

        await engine.Service.EditAsync(engine.Step("one").Id, new EditProposalRequest("Moja wersja", "model-x", 5000), None);

        Assert.Equal("Moja wersja", engine.Step("one").Proposal);
        Assert.Equal("model-x", engine.Step("one").Model);
        Assert.Equal(5000, engine.Step("one").TokenBudget);
    }

    [Fact]
    public async Task Exception_in_a_step_becomes_a_failed_step_not_a_crash()
    {
        var engine = new EngineHarness(Script("one"));
        engine.Scripts.OnExecute = (_, _, _) => throw new InvalidOperationException("dysk pełny");
        await engine.StartAsync();
        await engine.Service.ApproveAsync(engine.Step("one").Id, None);

        await engine.RunQueuedAsync();

        Assert.Equal(StepStatus.Failed, engine.Step("one").Status);
        Assert.Contains("dysk pełny", engine.Step("one").Error);
    }

    [Fact]
    public async Task Cancelled_step_is_marked_as_interrupted()
    {
        var engine = new EngineHarness(Script("one"));
        engine.Scripts.OnExecute = async (_, _, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return StepOutcome.Success("nigdy");
        };
        await engine.StartAsync();
        await engine.Service.ApproveAsync(engine.Step("one").Id, None);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await engine.RunQueuedAsync(cancellation.Token);

        Assert.Equal(StepStatus.Failed, engine.Step("one").Status);
        Assert.Equal("Krok przerwany.", engine.Step("one").Error);
    }

    [Fact]
    public async Task Step_output_goes_live_to_the_ui_and_into_the_step_log()
    {
        var engine = new EngineHarness(Script("one"));
        engine.Scripts.OnExecute = async (_, log, ct) =>
        {
            await log.WriteAsync("linia 1\n", ct);
            await log.WriteAsync("linia 2\n", ct);
            return StepOutcome.Success("ok");
        };
        await engine.StartAsync();
        await engine.Service.ApproveAsync(engine.Step("one").Id, None);

        await engine.RunQueuedAsync();

        Assert.Equal(["linia 1\n", "linia 2\n"], engine.Live.Output);
        Assert.Equal("linia 1\nlinia 2\n", engine.Step("one").Log);
    }

    [Fact]
    public async Task Step_kind_without_an_executor_waits_with_a_clear_message()
    {
        var engine = new EngineHarness(new FlowHub.Application.Flows.StepDocument { Id = "check-step", Kind = "Check", Check = "build" });

        await engine.StartAsync();
        await engine.RunQueuedAsync();

        Assert.Equal(StepStatus.Proposed, engine.Step("check-step").Status);
        Assert.Contains("Check nie są jeszcze obsługiwane", engine.Step("check-step").Proposal);
    }

    [Fact]
    public async Task Every_change_is_pushed_to_the_ui()
    {
        var engine = new EngineHarness(Script("one"));
        await engine.StartAsync();
        await engine.Service.ApproveAsync(engine.Step("one").Id, None);
        await engine.RunQueuedAsync();

        var statuses = engine.Live.Changes.Select(change => change.Steps[0].Status).ToList();

        Assert.Equal([StepStatus.Proposed, StepStatus.Approved, StepStatus.Running, StepStatus.Succeeded], statuses);
    }

    [Fact]
    public async Task After_restart_running_steps_fail_and_approved_ones_wait_again()
    {
        var engine = new EngineHarness(Script("one"));
        await engine.StartAsync();
        await engine.Service.ApproveAsync(engine.Step("one").Id, None);
        var recovery = new StepRecovery(engine.Store, TimeProvider.System, NullLogger<StepRecovery>.Instance);

        await recovery.RecoverAsync(None);
        Assert.Equal(StepStatus.Proposed, engine.Step("one").Status);

        engine.Step("one").Approve();
        engine.Step("one").Start(DateTime.UtcNow);
        await recovery.RecoverAsync(None);

        Assert.Equal(StepStatus.Failed, engine.Step("one").Status);
        Assert.Contains("restart", engine.Step("one").Error);
    }
}
