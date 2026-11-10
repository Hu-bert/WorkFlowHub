using FlowHub.Domain.WorkItems;

namespace FlowHub.Domain.Tests;

public sealed class StepStatusTransitionsTests
{
    [Theory]
    [InlineData(StepStatus.Pending, StepStatus.Proposed)]
    [InlineData(StepStatus.Proposed, StepStatus.Approved)]
    [InlineData(StepStatus.Proposed, StepStatus.Skipped)]
    [InlineData(StepStatus.Approved, StepStatus.Running)]
    [InlineData(StepStatus.Running, StepStatus.Succeeded)]
    [InlineData(StepStatus.Running, StepStatus.Failed)]
    [InlineData(StepStatus.Failed, StepStatus.Proposed)]
    [InlineData(StepStatus.Succeeded, StepStatus.Proposed)]
    public void Allows_the_happy_path_and_retries(StepStatus from, StepStatus to)
    {
        Assert.True(StepStatusTransitions.IsAllowed(from, to));
    }

    [Theory]
    [InlineData(StepStatus.Pending, StepStatus.Running)]
    [InlineData(StepStatus.Pending, StepStatus.Succeeded)]
    [InlineData(StepStatus.Proposed, StepStatus.Running)]
    [InlineData(StepStatus.Running, StepStatus.Approved)]
    [InlineData(StepStatus.Succeeded, StepStatus.Running)]
    public void Blocks_shortcuts_that_skip_approval(StepStatus from, StepStatus to)
    {
        var error = Assert.Throws<DomainException>(() => StepStatusTransitions.EnsureAllowed(from, to));

        Assert.Contains(from.ToString(), error.Message);
    }

    [Fact]
    public void Every_status_has_defined_transitions()
    {
        foreach (var status in Enum.GetValues<StepStatus>())
        {
            Assert.False(StepStatusTransitions.IsAllowed(status, status));
        }
    }
}
