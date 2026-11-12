using System.Net;
using FlowHub.Api.Endpoints;
using FlowHub.Application.Workspace;
using FlowHub.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace FlowHub.Api.Tests;

/// <summary>Dane użytkownika poza repozytorium, listy podpowiedzi w Ustawieniach i klonowanie repo.</summary>
public sealed class UserDataTests(FlowHubApiFactory factory) : IClassFixture<FlowHubApiFactory>
{
    [Fact]
    public async Task Repo_catalog_lives_next_to_the_settings_and_starts_from_the_file_shipped_with_the_app()
    {
        var repositories = await factory.CreateClient().GetAsync<List<CodeRepository>>("/api/repositories");
        var paths = factory.Services.GetRequiredService<WorkspacePaths>();

        Assert.Contains(repositories, repository => repository.Name == "Shared.Lib");
        Assert.Equal(Path.Combine(factory.SettingsDirectory, "repos.yaml"), paths.RepositoriesFile);
        Assert.True(File.Exists(paths.RepositoriesFile));
        Assert.StartsWith(factory.SettingsDirectory, paths.KnowledgeDirectory);
    }

    [Fact]
    public async Task Settings_page_can_tell_where_everything_is_stored()
    {
        var storage = await factory.CreateClient().GetAsync<List<StorageLocation>>("/api/settings/storage");

        Assert.Contains(storage, location => location.Path.EndsWith("settings.json"));
        Assert.Contains(storage, location => location.Path.EndsWith("secrets.json"));
        Assert.Contains(storage, location => location.Path.EndsWith("repos.yaml"));
        Assert.Contains(storage, location => location.Path.EndsWith("test.db"));
        Assert.All(storage, location => Assert.True(Path.IsPathFullyQualified(location.Path), location.Path));
    }

    [Fact]
    public async Task Environments_and_models_can_be_picked_from_a_list_instead_of_typed()
    {
        var client = factory.CreateClient();

        var environments = await client.GetAsync<List<string>>("/api/settings/octopus/suggestions?key=RepoStatus:Environments");
        var models = await client.GetAsync<List<string>>("/api/settings/models/suggestions?key=Models:Tiers:cheap");
        var notSuggestable = await client.GetAsync("/api/settings/octopus/suggestions?key=Octopus:BaseUrl", TestContext.Current.CancellationToken);

        Assert.Equal(["DEV", "TEST", "UAT", "PROD"], environments);
        Assert.Equal("auto", models[0]);
        Assert.Equal(HttpStatusCode.BadRequest, notSuggestable.StatusCode);
    }

    [Theory]
    [InlineData("Shared.Lib", @"C:\gdziekolwiek", "nie nadaje się do klonowania")] // adres demo://
    [InlineData("Shared.Lib", "wzgledna", "pełną ścieżkę")]
    [InlineData("Shared.Lib", null, "pełną ścieżkę")] // brak folderu repozytoriów w Ustawieniach
    public async Task Clone_refuses_demo_addresses_and_unclear_targets(string repo, string? directory, string expected)
    {
        var response = await factory.CreateClient().PostAsync($"/api/repositories/{repo}/clone", new CloneRequest(directory));
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expected, body);
    }
}