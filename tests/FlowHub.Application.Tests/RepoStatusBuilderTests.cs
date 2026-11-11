using FlowHub.Application.Integrations;

namespace FlowHub.Application.Tests;

public sealed class RepoStatusBuilderTests
{
    private static PullRequestStatus Pr(
        PullRequestState state = PullRequestState.Active,
        bool conflicts = false,
        int comments = 0,
        bool policiesPass = true,
        params ReviewerVote[] votes) => new(
            1, "PR", state,
            votes.Select((vote, index) => new Reviewer($"R{index}", vote)).ToList(),
            comments,
            [new PolicyCheck("Build validation", policiesPass)],
            conflicts,
            "https://tfs/pr/1");

    [Fact]
    public void Pull_request_cell_shows_votes_and_the_most_important_problem()
    {
        Assert.Equal((HealthLevel.Unknown, "Brak PR"), Cell(RepoStatusBuilder.PullRequest(null)));
        Assert.Equal((HealthLevel.Ok, "Completed"), Cell(RepoStatusBuilder.PullRequest(Pr(PullRequestState.Completed))));
        Assert.Equal((HealthLevel.Failed, "Konflikty scalania"), Cell(RepoStatusBuilder.PullRequest(Pr(conflicts: true, votes: ReviewerVote.Approved))));
        Assert.Equal((HealthLevel.Failed, "Odrzucony · 1/2 akcept."), Cell(RepoStatusBuilder.PullRequest(Pr(votes: [ReviewerVote.Approved, ReviewerVote.Rejected]))));
        Assert.Equal((HealthLevel.Warning, "1/2 akcept. · 3 koment."), Cell(RepoStatusBuilder.PullRequest(Pr(comments: 3, votes: [ReviewerVote.Approved, ReviewerVote.Waiting]))));
        Assert.Equal((HealthLevel.Pending, "2/3 akcept."), Cell(RepoStatusBuilder.PullRequest(Pr(votes: [ReviewerVote.Approved, ReviewerVote.ApprovedWithSuggestions, ReviewerVote.Waiting]))));
        Assert.Equal((HealthLevel.Pending, "0/1 akcept. · polityki"), Cell(RepoStatusBuilder.PullRequest(Pr(policiesPass: false, votes: ReviewerVote.Waiting))));
    }

    [Fact]
    public void Build_cell_shows_the_worst_build()
    {
        var ok = new BuildStatus("A", "10", BuildState.Success, [], "u1");
        var running = new BuildStatus("B", "11", BuildState.Running, [], "u2");
        var failed = new BuildStatus("C", "12", BuildState.Failed, ["T1", "T2"], "u3");

        Assert.Equal((HealthLevel.Unknown, "Brak buildu"), Cell(RepoStatusBuilder.Build([])));
        Assert.Equal((HealthLevel.Ok, "#10"), Cell(RepoStatusBuilder.Build([ok])));
        Assert.Equal((HealthLevel.Pending, "#11 · trwa"), Cell(RepoStatusBuilder.Build([ok, running])));
        Assert.Equal((HealthLevel.Failed, "#12 · 2 testów"), Cell(RepoStatusBuilder.Build([ok, running, failed])));
        Assert.Equal("u3", RepoStatusBuilder.Build([ok, failed]).Url);
    }

    [Fact]
    public void Worst_status_wins()
    {
        Assert.Equal(HealthLevel.Failed, RepoStatusBuilder.Worst([new(HealthLevel.Ok, ""), new(HealthLevel.Failed, ""), new(HealthLevel.Pending, "")]));
        Assert.Equal(HealthLevel.Pending, RepoStatusBuilder.Worst([new(HealthLevel.Ok, ""), new(HealthLevel.Pending, "")]));
        Assert.Equal(HealthLevel.Ok, RepoStatusBuilder.Worst([new(HealthLevel.Ok, "")]));
        Assert.Equal(HealthLevel.Unknown, RepoStatusBuilder.Worst([]));
    }

    [Fact]
    public void Summary_counts_repos_deployed_to_the_ready_environment()
    {
        RepoStatus Repo(string name, HealthLevel dev) => new(
            name, "b", "p", new(HealthLevel.Ok, ""), new(HealthLevel.Ok, ""), new(HealthLevel.Ok, ""), "1.0",
            [new EnvironmentCell("DEV", new StatusCell(dev, "")), new EnvironmentCell("TEST", new StatusCell(HealthLevel.Unknown, "—"))],
            HealthLevel.Ok, null, [], []);

        var summary = RepoStatusBuilder.Summary([Repo("A", HealthLevel.Ok), Repo("B", HealthLevel.Ok), Repo("C", HealthLevel.Pending)], "dev");

        Assert.Equal("2 z 3 repo gotowe na dev", summary);
    }

    private static (HealthLevel, string) Cell(StatusCell cell) => (cell.Level, cell.Text);
}
