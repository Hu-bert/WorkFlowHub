using System.Net;
using FlowHub.Api.Endpoints;
using FlowHub.Application.Flows;
using FlowHub.Application.Workspace;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FlowHub.Api.Tests;

/// <summary>Edytor flow i bloczków — na kopii katalogów flows/ i blocks/, żeby testy nie zmieniały plików w repo.</summary>
public sealed class FlowEditorTests : IClassFixture<FlowHubApiFactory>, IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("flowhub-editor-").FullName;
    private readonly WebApplicationFactory<Program> _factory;

    public FlowEditorTests(FlowHubApiFactory factory)
    {
        var real = factory.Services.GetRequiredService<WorkspacePaths>();
        Copy(real.FlowsDirectory, Path.Combine(_root, "flows"));
        Copy(real.BlocksDirectory, Path.Combine(_root, "blocks"));
        File.Copy(real.SeedRepositoriesFile, Path.Combine(_root, "repos.yaml"));

        _factory = factory.WithWebHostBuilder(builder => builder.UseSetting("Workspace:Root", _root));
    }

    public void Dispose()
    {
        _factory.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private static void Copy(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }

    [Fact]
    public async Task Reordered_flow_is_saved_to_yaml_and_used_from_then_on()
    {
        using var client = _factory.CreateClient();
        var document = await client.GetAsync<FlowDocument>("/api/flows/Spike/document");
        var originalOrder = document.Flow.Select(step => step.Id).ToList();

        (document.Flow[^1], document.Flow[^2]) = (document.Flow[^2], document.Flow[^1]);
        document.Flow.Add(new StepDocument { Id = "podsumowanie", Name = "Podsumowanie dla zespołu", Kind = "Manual", Checklist = ["Wysłane"] });
        var response = await (await client.PutAsync("/api/flows", document)).ReadAsync<SaveFlowResponse>();

        var reloaded = (await client.GetAsync<FlowsResponse>("/api/flows")).Flows.Single(flow => flow.Type == "Spike");

        Assert.True(response.Saved);
        Assert.Equal([.. originalOrder[..^2], originalOrder[^1], originalOrder[^2], "podsumowanie"], reloaded.Steps.Select(step => step.Id));
        Assert.Contains("Podsumowanie dla zespołu", File.ReadAllText(Path.Combine(_root, "flows", "spike.yaml")));
    }

    [Fact]
    public async Task Invalid_flow_is_not_saved_and_errors_are_readable()
    {
        using var client = _factory.CreateClient();
        var document = await client.GetAsync<FlowDocument>("/api/flows/Bug/document");
        var before = File.ReadAllText(Path.Combine(_root, "flows", "bug.yaml"));

        document.Flow[0].Script = null;
        document.Flow[1].Id = document.Flow[0].Id;
        var response = await (await client.PutAsync("/api/flows", document)).ReadAsync<SaveFlowResponse>();

        Assert.False(response.Saved);
        Assert.Contains(response.Errors, error => error.Contains("brak pola 'script'"));
        Assert.Contains(response.Errors, error => error.Contains("powtarza się"));
        Assert.Equal(before, File.ReadAllText(Path.Combine(_root, "flows", "bug.yaml")));
    }

    [Fact]
    public async Task New_task_type_becomes_a_new_yaml_file()
    {
        using var client = _factory.CreateClient();
        var document = new FlowDocument
        {
            Type = "Hotfix",
            Name = "Pilna poprawka",
            Flow = [new StepDocument { Id = "create-folder", Name = "Folder", Kind = "Script", Script = "scripts/create-folder.ps1" }],
        };

        var response = await (await client.PutAsync("/api/flows", document)).ReadAsync<SaveFlowResponse>();
        var flows = await client.GetAsync<FlowsResponse>("/api/flows");

        Assert.True(response.Saved);
        Assert.True(File.Exists(Path.Combine(_root, "flows", "hotfix.yaml")));
        Assert.Contains(flows.Flows, flow => flow is { Type: "Hotfix", Name: "Pilna poprawka" });
        Assert.Empty(flows.Problems);
    }

    [Fact]
    public async Task Block_content_can_be_read_and_edited_but_only_inside_the_library()
    {
        using var client = _factory.CreateClient();

        var prompt = await client.GetAsync<BlockContent>("/api/blocks/content?path=prompts/planowanie.md");
        var saved = await client.PutAsync("/api/blocks/content", new BlockContent("prompts/planowanie.md", prompt.Content + "\nZawsze pytaj o zależności między repo."));
        var script = await client.PutAsync("/api/blocks/content", new BlockContent("scripts/nowy.ps1", "Write-Output 'zażółć'"));
        var outside = await client.PutAsync("/api/blocks/content", new BlockContent("../flows/bug.yaml", "x"));
        var wrongKind = await client.PutAsync("/api/blocks/content", new BlockContent("prompts/wirus.exe", "x"));

        Assert.Contains("Ułóż plan realizacji", prompt.Content);
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        Assert.EndsWith("zależności między repo.", File.ReadAllText(Path.Combine(_root, "blocks", "prompts", "planowanie.md")));
        Assert.Equal(HttpStatusCode.NoContent, script.StatusCode);
        Assert.Equal([0xEF, 0xBB, 0xBF], File.ReadAllBytes(Path.Combine(_root, "blocks", "scripts", "nowy.ps1"))[..3]);
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongKind.StatusCode);
    }
}
