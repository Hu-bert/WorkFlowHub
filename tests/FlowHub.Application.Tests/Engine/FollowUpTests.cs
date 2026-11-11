using FlowHub.Application.Flows;
using FlowHub.Domain;
using FlowHub.Domain.WorkItems;
using static FlowHub.Application.Tests.Engine.EngineHarness;

namespace FlowHub.Application.Tests.Engine;

/// <summary>Krok ręczny „co dalej?”: polecenie wraca do agenta, a przegląd czeka na nowy wynik.</summary>
public sealed class FollowUpTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private static async Task<EngineHarness> AtReviewAsync()
    {
        var engine = new EngineHarness(Agent("implement"), Script("test", approval: false), ManualStep("review", followUp: "implement"), Script("after"));
        await engine.StartAsync();
        await engine.Service.ApproveAsync(engine.Step("implement").Id, None);
        await engine.RunQueuedAsync();
        return engine;
    }

    [Fact]
    public async Task Follow_up_sends_the_remark_to_the_agent_step_and_returns_to_the_review()
    {
        var engine = await AtReviewAsync();
        Assert.Equal(StepStatus.Proposed, engine.Step("review").Status);

        await engine.Service.FollowUpAsync(engine.Step("review").Id, "Dodaj jeszcze test na sobotę", null, None);

        Assert.Equal(StepStatus.Approved, engine.Step("implement").Status); // bez drugiego zatwierdzania
        Assert.Equal("Dodaj jeszcze test na sobotę", engine.Step("implement").Remark);
        Assert.Equal(2, engine.Step("implement").Attempt);
        Assert.Equal(StepStatus.Pending, engine.Step("test").Status);
        Assert.Equal(StepStatus.Pending, engine.Step("review").Status);

        await engine.RunQueuedAsync();

        Assert.Equal(["implement", "implement"], engine.Agents.Executed);
        Assert.Equal(["test", "test"], engine.Scripts.Executed);
        Assert.Equal(StepStatus.Proposed, engine.Step("review").Status);
        Assert.Equal(StepStatus.Pending, engine.Step("after").Status);
    }

    [Fact]
    public async Task Confirming_the_review_ends_the_loop_and_moves_on()
    {
        var engine = await AtReviewAsync();

        await engine.Service.ApproveAsync(engine.Step("review").Id, None);
        await engine.RunQueuedAsync();

        Assert.Equal(StepStatus.Succeeded, engine.Step("review").Status);
        Assert.Equal(StepStatus.Proposed, engine.Step("after").Status);
        Assert.Equal(["implement"], engine.Agents.Executed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Follow_up_needs_an_instruction(string? remark)
    {
        var engine = await AtReviewAsync();

        await Assert.ThrowsAsync<DomainException>(() => engine.Service.FollowUpAsync(engine.Step("review").Id, remark, null, None));
        Assert.Equal(StepStatus.Succeeded, engine.Step("implement").Status);
    }

    [Fact]
    public async Task Manual_step_without_follow_up_in_the_flow_cannot_send_instructions()
    {
        var engine = new EngineHarness(Agent("implement"), ManualStep("review"));
        await engine.StartAsync();
        await engine.Service.ApproveAsync(engine.Step("implement").Id, None);
        await engine.RunQueuedAsync();

        await Assert.ThrowsAsync<DomainException>(() => engine.Service.FollowUpAsync(engine.Step("review").Id, "popraw", null, None));
    }

    [Theory]
    [InlineData("implement", 0)]
    [InlineData("nie-ma", 1)]
    [InlineData("test", 1)] // skrypt, nie agent
    [InlineData("after", 1)] // późniejszy krok
    public void Follow_up_in_a_flow_must_point_to_an_earlier_agent_step(string followUp, int expectedErrors)
    {
        var flow = new FlowDocument
        {
            Type = "Test",
            Name = "Test",
            Flow = [Agent("implement"), Script("test"), ManualStep("review", followUp), Agent("after")],
        };

        Assert.Equal(expectedErrors, FlowValidator.Validate(flow).Count);
    }
}