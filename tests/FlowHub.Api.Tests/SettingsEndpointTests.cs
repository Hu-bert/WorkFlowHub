using System.Net;
using System.Text.Json;
using FlowHub.Application.Integrations;
using FlowHub.Application.Settings;
using FlowHub.Integrations.Fake;
using FlowHub.Integrations.Jira;
using Microsoft.Extensions.DependencyInjection;

namespace FlowHub.Api.Tests;

public sealed class SettingsEndpointTests(FlowHubApiFactory factory) : IClassFixture<FlowHubApiFactory>
{
    private sealed record Body(Dictionary<string, string?> Values);

    private sealed record Models(bool Unlimited, long TaskTokenBudget);

    [Fact]
    public async Task Modules_for_copilot_models_local_repos_and_company_systems_are_listed()
    {
        var modules = await factory.CreateClient().GetAsync<List<SettingsModuleView>>("/api/settings");

        Assert.Equal(["models", "repositories", "copilot", "tfs", "jira", "teamcity", "octopus"], modules.Select(module => module.Id));
        Assert.All(modules, module => Assert.NotEmpty(module.Fields));
    }

    [Fact]
    public async Task Saved_setting_works_immediately_without_restart()
    {
        var client = factory.CreateClient();
        Assert.False((await client.GetAsync<Models>("/api/models")).Unlimited);

        var response = await client.PutAsync("/api/settings/models", new Body(new() { ["Models:Plan"] = "Unlimited" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await client.GetAsync<Models>("/api/models")).Unlimited);
    }

    [Fact]
    public async Task Switching_the_task_source_changes_the_implementation_in_use()
    {
        var client = factory.CreateClient();
        Assert.IsType<FakeTaskSource>(factory.Services.GetRequiredService<ITaskSource>());

        await client.PutAsync("/api/settings/jira", new Body(new() { ["Integrations:TaskSource"] = "Jira" }));
        Assert.IsType<JiraTaskSource>(factory.Services.GetRequiredService<ITaskSource>());

        await client.PutAsync("/api/settings/jira", new Body(new() { ["Integrations:TaskSource"] = "Fake" }));
        Assert.IsType<FakeTaskSource>(factory.Services.GetRequiredService<ITaskSource>());
    }

    [Fact]
    public async Task Token_is_stored_encrypted_and_never_returned()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsync("/api/settings/tfs", new Body(new() { ["Tfs:Token"] = "bardzo-tajny-pat" }));
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var listed = await (await client.GetAsync("/api/settings", TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("bardzo-tajny-pat", body + listed);
        Assert.Equal("bardzo-tajny-pat", factory.Services.GetRequiredService<ISecretStore>().Get("Tfs:Token"));
        Assert.DoesNotContain("bardzo-tajny-pat", File.ReadAllText(Path.Combine(factory.SettingsDirectory, "secrets.json")));

        using var modules = JsonDocument.Parse(listed);
        var token = modules.RootElement.EnumerateArray().Single(module => module.GetProperty("id").GetString() == "tfs")
            .GetProperty("fields").EnumerateArray().Single(field => field.GetProperty("field").GetProperty("key").GetString() == "Tfs:Token");
        Assert.True(token.GetProperty("hasValue").GetBoolean());
    }

    [Fact]
    public async Task Connection_test_without_an_address_explains_what_is_missing()
    {
        var response = await factory.CreateClient().PostAsync("/api/settings/teamcity/test", new { });
        var result = await response.ReadAsync<ConnectionTest>();

        Assert.False(result.Ok);
        Assert.Contains("Adres TeamCity", result.Message);
    }

    [Fact]
    public async Task Invalid_choice_is_rejected()
    {
        var response = await factory.CreateClient().PutAsync("/api/settings/copilot", new Body(new() { ["Agent:Runner"] = "Magia" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}