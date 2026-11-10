using FlowHub.Domain.Flows;
using FlowHub.Domain.WorkItems;

namespace FlowHub.Domain.Tests;

public sealed class WorkItemTests
{
    [Fact]
    public void Folder_and_branch_come_from_key_and_title()
    {
        var item = TestData.WorkItem("PROJ-123", "Błąd w fakturze: zła stawka VAT!");

        Assert.Equal(Path.Combine("work", "PROJ-123-blad-w-fakturze-zla-stawka-vat"), item.WorkDirectory);

        var repo = item.AddRepo("Billing.Api");
        Assert.Equal("feature/PROJ-123-blad-w-fakturze-zla-stawka-vat", repo.Branch);
        Assert.Equal(Path.Combine(item.WorkDirectory, "Billing.Api"), repo.LocalPath);
    }

    [Fact]
    public void First_repo_is_main_and_all_share_one_branch()
    {
        var item = TestData.WorkItem();

        var first = item.AddRepo("Billing.Api");
        var second = item.AddRepo("Shared.Lib");

        Assert.Equal(RepoRole.Main, first.Role);
        Assert.Equal(RepoRole.Dependent, second.Role);
        Assert.Equal(first.Branch, second.Branch);
        Assert.Throws<DomainException>(() => item.AddRepo("Billing.Api"));
    }

    [Fact]
    public void Per_repo_step_is_planned_once_for_each_repo()
    {
        var item = TestData.WorkItem();
        item.AddRepo("Billing.Api");
        item.AddRepo("Shared.Lib");

        item.PlanSteps(TestData.Flow(
            TestData.Step("create-folder"),
            TestData.Step("clone-repo", perRepo: true),
            TestData.Step("plan", StepKind.Agent)));

        Assert.Equal(
            ["create-folder:", "clone-repo:Billing.Api", "clone-repo:Shared.Lib", "plan:"],
            item.OrderedSteps.Select(step => $"{step.StepId}:{step.RepoName}"));
        Assert.Equal([1, 2, 3, 4], item.OrderedSteps.Select(step => step.Order));
    }

    [Fact]
    public void Current_step_is_the_first_unfinished_one()
    {
        var item = TestData.WorkItem();
        item.AddRepo("Billing.Api");
        item.PlanSteps(TestData.Flow(TestData.Step("one"), TestData.Step("two")));

        item.CurrentStep!.Propose("x", null);
        item.CurrentStep.Skip();

        Assert.Equal("two", item.CurrentStep!.StepId);
        Assert.False(item.IsFinished);
    }

    [Fact]
    public void Steps_cannot_be_planned_twice()
    {
        var item = TestData.WorkItem();
        item.AddRepo("Billing.Api");
        var flow = TestData.Flow(TestData.Step("one"));
        item.PlanSteps(flow);

        Assert.Throws<DomainException>(() => item.PlanSteps(flow));
    }

    [Theory]
    [InlineData("", "Tytuł")]
    [InlineData("PROJ-1", " ")]
    public void Key_and_title_are_required(string key, string title)
    {
        Assert.Throws<DomainException>(() => TestData.WorkItem(key, title));
    }

    [Theory]
    [InlineData("Zażółć gęślą jaźń", "zazolc-gesla-jazn")]
    [InlineData("  API -- v2 (nowe)  ", "api-v2-nowe")]
    [InlineData("Łódź", "lodz")]
    public void Slug_is_ascii_lowercase_with_hyphens(string title, string expected)
    {
        Assert.Equal(expected, WorkItemNaming.Slug(title));
    }

    [Fact]
    public void Slug_is_cut_to_a_safe_length()
    {
        var slug = WorkItemNaming.Slug(new string('a', 30) + " " + new string('b', 30));

        Assert.True(slug.Length <= 40);
        Assert.False(slug.EndsWith('-'));
    }
}
