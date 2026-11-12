using System.Text.Json;
using FlowHub.Api.Endpoints;
using FlowHub.Application.Integrations;
using FlowHub.Application.Settings;
using FlowHub.Domain.Repositories;
using FlowHub.Integrations;
using FlowHub.Integrations.Octopus;
using FlowHub.Integrations.TeamCity;
using FlowHub.Integrations.Tfs;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FlowHub.Api.Tests;

/// <summary>
/// Mapowanie odpowiedzi TeamCity, Octopus i statusów TFS. Przykłady JSON są wg dokumentacji —
/// NIE pochodzą z prawdziwych serwerów.
/// </summary>
public sealed class CompanySystemsTests(FlowHubApiFactory factory) : IClassFixture<FlowHubApiFactory>
{
    [Fact]
    public void TeamCity_build_keeps_number_branch_and_the_status_text_with_test_counts()
    {
        using var build = JsonDocument.Parse("""
            { "id": 8134, "number": "ZR-3925-2 #644", "status": "FAILURE", "state": "finished",
              "statusText": "Tests failed: 24 (24 new), passed: 194", "branchName": "pull/36816/merge",
              "webUrl": "https://tc/buildConfiguration/Kcf_Production/8134", "buildTypeId": "Kcf_Production",
              "testOccurrences": { "count": 218, "passed": 194, "failed": 24 } }
            """);

        var status = TeamCityMapper.Build(build.RootElement, "Kcf_Production", ["Tests.A", "Tests.B"]);

        Assert.Equal(("Kcf_Production", "ZR-3925-2 #644", BuildState.Failed, "pull/36816/merge"), (status.Configuration, status.Number, status.State, status.Branch));
        Assert.Equal("Tests failed: 24 (24 new), passed: 194", status.StatusText);
        Assert.Equal(2, status.FailedTests.Count);
    }

    [Fact]
    public void TeamCity_build_filters_go_from_the_fullest_to_the_simplest_and_never_mix_state_with_running()
    {
        var locators = TeamCityCiServer.BuildLocators("Kcf_Production", "ZR-3925-2");

        Assert.Equal(3, locators.Count);
        Assert.All(locators, locator => Assert.Contains("buildType:(id:Kcf_Production)", locator));
        Assert.All(locators, locator => Assert.DoesNotContain("running:", locator)); // serwer 2026.1 odpowiedział na to 400
        Assert.Equal("buildType:(id:Kcf_Production),branch:ZR-3925-2,count:1", locators[^1]);
    }

    [Fact]
    public void Server_error_text_is_shown_without_html_and_cut_to_a_readable_length()
    {
        var html = "<html><body><h1>Bad Request</h1>\n<p>Locator dimension 'running' is unknown.</p></body></html>";

        Assert.Equal("Bad Request Locator dimension 'running' is unknown.", FlowHub.Integrations.Http.ApiClient.Summarize(html));
        Assert.Equal(401, FlowHub.Integrations.Http.ApiClient.Summarize(new string('x', 2000)).Length);
    }

    [Theory]
    [InlineData("queued", null, BuildState.Queued)]
    [InlineData("running", "SUCCESS", BuildState.Running)]
    [InlineData("running", "FAILURE", BuildState.Failed)]
    [InlineData("finished", "SUCCESS", BuildState.Success)]
    [InlineData("finished", "UNKNOWN", BuildState.Failed)]
    public void TeamCity_state_and_status_become_one_build_state(string state, string? status, BuildState expected)
    {
        Assert.Equal(expected, TeamCityMapper.State(state, status));
    }

    [Fact]
    public void TeamCity_changes_use_the_compact_date_format()
    {
        using var changes = JsonDocument.Parse("""
            { "change": [ { "version": "c1046c4fa016a774253fb3b93e6cc8bd12c159e5", "username": "h.grzesiak", "comment": "Poprawka\n\nszczegóły", "date": "20261110T151059+0100" } ] }
            """);

        var change = Assert.Single(TeamCityMapper.Changes(changes.RootElement));

        Assert.Equal(("c1046c4f", "h.grzesiak", "Poprawka"), (change.Revision, change.Author, change.Comment));
        Assert.Equal(new DateTime(2026, 11, 10, 14, 10, 59, DateTimeKind.Utc), change.Date);
    }

    private const string Progression = """
        { "Environments": [ { "Id": "Environments-1", "Name": "DEV" }, { "Id": "Environments-2", "Name": "TEST" } ],
          "Releases": [
            { "Release": { "Id": "Releases-9", "Version": "1.31.0" },
              "Deployments": { "Environments-1": [ { "State": "Success", "ReleaseVersion": "1.31.0", "DeploymentId": "Deployments-90", "IsCurrent": true, "CompletedTime": "2026-11-10T12:00:00+00:00" } ] } },
            { "Release": { "Id": "Releases-8", "Version": "1.30.0", "BuildInformation": [ { "Branch": "refs/heads/ZR-3925-2" } ] },
              "Deployments": {
                "Environments-1": [ { "State": "Failed", "ReleaseVersion": "1.30.0", "DeploymentId": "Deployments-80", "IsCurrent": false },
                                    { "State": "Executing", "ReleaseVersion": "1.30.0", "DeploymentId": "Deployments-81", "IsCurrent": true } ] } }
          ] }
        """;

    [Fact]
    public void Octopus_shows_the_release_built_from_the_branch_and_where_it_is_deployed()
    {
        using var progression = JsonDocument.Parse(Progression);

        var overview = OctopusMapper.Overview(progression.RootElement, "Kcf", "ZR-3925-2", [], isDefaultBranch: false, "https://octo/app#/Spaces-1/projects/kcf");

        Assert.Equal("1.30.0", overview.Release);
        Assert.Equal(["DEV", "TEST"], overview.Environments.Select(environment => environment.Environment));
        Assert.Equal((DeploymentState.Executing, "1.30.0"), (overview.Environments[0].State, overview.Environments[0].Version));
        Assert.EndsWith("/deployments/releases/1.30.0/deployments/Deployments-81", overview.Environments[0].Url);
        Assert.Equal(DeploymentState.None, overview.Environments[1].State);
    }

    [Fact]
    public void Octopus_release_can_be_matched_by_the_version_in_the_build_number()
    {
        using var progression = JsonDocument.Parse(Progression);
        BuildStatus[] builds = [new("Kcf_Production", "1.31.0 #650", BuildState.Success, [], "u")];

        var overview = OctopusMapper.Overview(progression.RootElement, "Kcf", "ZR-4000", builds, isDefaultBranch: false, "p");

        Assert.Equal("1.31.0", overview.Release);
    }

    [Fact]
    public void Octopus_never_shows_someone_elses_release_for_a_feature_branch_but_shows_the_latest_for_the_default_branch()
    {
        using var progression = JsonDocument.Parse(Progression);

        var feature = OctopusMapper.Overview(progression.RootElement, "Kcf", "ZR-9999", [], isDefaultBranch: false, "p");
        var master = OctopusMapper.Overview(progression.RootElement, "Kcf", "master", [], isDefaultBranch: true, "p");

        Assert.Null(feature.Release);
        Assert.All(feature.Environments, environment => Assert.Equal(DeploymentState.None, environment.State));
        Assert.Equal("1.31.0", master.Release);
    }

    [Fact]
    public void Tfs_statuses_become_builds_one_per_context_with_a_link_to_the_ci_build()
    {
        using var statuses = JsonDocument.Parse("""
            { "count": 3, "value": [
              { "state": "pending", "description": "Build started", "context": { "name": "Production", "genre": "TeamCity" }, "creationDate": "2026-11-10T09:00:00Z", "targetUrl": "https://tc/viewLog.html?buildId=643" },
              { "state": "failed", "description": "Tests failed: 24, passed: 194", "context": { "name": "Production", "genre": "TeamCity" }, "creationDate": "2026-11-10T09:05:00Z", "targetUrl": "https://tc/viewLog.html?buildId=644" },
              { "state": "notApplicable", "context": { "name": "Inny" } }
            ] }
            """);

        var build = Assert.Single(TfsStatusMapper.Builds(statuses.RootElement, "ZR-3925-2"));

        Assert.Equal(("TeamCity/Production", "644", BuildState.Failed, "Tests failed: 24, passed: 194"), (build.Configuration, build.Number, build.State, build.StatusText));
        Assert.Equal("https://tc/viewLog.html?buildId=644", build.Url);
    }

    [Fact]
    public void Tfs_head_commit_is_taken_from_the_exact_branch_not_from_one_with_the_same_prefix()
    {
        using var refs = JsonDocument.Parse("""
            { "value": [ { "name": "refs/heads/ZR-3925-2", "objectId": "bbb" }, { "name": "refs/heads/ZR-3925", "objectId": "aaa" } ] }
            """);

        Assert.Equal("aaa", TfsStatusMapper.HeadCommit(refs.RootElement, "ZR-3925"));
    }

    [Theory]
    [InlineData("pull/36816/merge", 36816)]
    [InlineData("refs/pull/12/merge", 12)]
    [InlineData("ZR-3925-2", null)]
    [InlineData("feature/pull/x", null)]
    public void Pull_request_build_branch_names_the_pull_request(string branch, int? expected)
    {
        Assert.Equal(expected, TfsMapper.PullRequestIdFromBranch(branch));
    }

    [Fact]
    public void Repository_in_another_project_of_the_collection_is_located_from_its_address()
    {
        var api = new TfsApi(null!, Options.Create(new TfsOptions { CollectionUrl = "https://tfs.firma.pl/tfs/Col", Project = "Domyslny" }), Substitute.For<ISecretStore>());

        var fromUrl = api.Locate(new CodeRepository { Name = "kcf", Url = "https://tfs.firma.pl/tfs/Col/Inny%20Projekt/_git/SystemMonitor.Tasks.KcfMonitoring" });
        var foreign = api.Locate(new CodeRepository { Name = "Obce", Url = "https://github.com/ktos/_git/Obce" });

        Assert.Equal(("https://tfs.firma.pl/tfs/Col/Inny%20Projekt", "SystemMonitor.Tasks.KcfMonitoring"), (fromUrl.ProjectUrl, fromUrl.Name));
        Assert.Equal(("https://tfs.firma.pl/tfs/Col/Domyslny", "Obce"), (foreign.ProjectUrl, foreign.Name)); // token nie idzie poza kolekcję
    }

    [Fact]
    public void Links_lead_to_the_repo_branch_build_configuration_and_deploy_project()
    {
        var links = new RepositoryLinksBuilder(
            Options.Create(new TeamCityOptions { BaseUrl = "https://tc.firma.pl/" }),
            Options.Create(new OctopusOptions { BaseUrl = "https://octo.firma.pl", Space = "Spaces-1" }));
        var repository = new CodeRepository { Name = "Kcf", Url = "https://tfs.firma.pl/tfs/Col/P/_git/Kcf", CiBuildConfigurations = ["Kcf_Production"], DeployProject = "kcf" };

        var result = links.For(repository, "ZR-3925-2");

        Assert.Equal("https://tfs.firma.pl/tfs/Col/P/_git/Kcf?version=GBZR-3925-2", result.Repository);
        Assert.Equal("https://tfs.firma.pl/tfs/Col/P/_git/Kcf/pullrequests", result.PullRequests);
        Assert.Equal(new NamedLink("Kcf_Production", "https://tc.firma.pl/buildConfiguration/Kcf_Production?mode=builds&branch=ZR-3925-2"), Assert.Single(result.Builds));
        Assert.Equal("https://octo.firma.pl/app#/Spaces-1/projects/kcf", result.Deploy);
    }

    [Fact]
    public async Task Connection_status_says_which_integrations_run_on_sample_data()
    {
        var statuses = await factory.CreateClient().GetAsync<List<ConnectionStatus>>("/api/settings/status");

        Assert.All(
            statuses.Where(status => status.Id is "tfs" or "jira" or "teamcity" or "octopus"),
            status => Assert.Equal(ConnectionState.Fake, status.State));
    }

    [Fact]
    public async Task Diagnosis_reports_the_outcome_and_the_requests_made()
    {
        var diagnosis = await factory.CreateClient().GetAsync<Diagnosis>("/api/repositories/Shared.Lib/diagnose?branch=main");

        Assert.Null(diagnosis.Error);
        Assert.Equal("Completed", diagnosis.PullRequest);
        Assert.Empty(diagnosis.Calls); // atrapy nie wykonują zapytań
    }
}