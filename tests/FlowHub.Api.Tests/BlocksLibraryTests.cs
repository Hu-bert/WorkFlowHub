using FlowHub.Api.Endpoints;
using FlowHub.Application.Blocks;
using FlowHub.Application.Workspace;
using FlowHub.Domain.Flows;
using Microsoft.Extensions.DependencyInjection;

namespace FlowHub.Api.Tests;

/// <summary>Pilnuje biblioteki blocks/: flow nie może wskazywać nieistniejących plików, a skrypty muszą mieć BOM.</summary>
public sealed class BlocksLibraryTests(FlowHubApiFactory factory) : IClassFixture<FlowHubApiFactory>
{
    private string BlocksDirectory => factory.Services.GetRequiredService<WorkspacePaths>().BlocksDirectory;

    private async Task<List<StepDefinition>> AllStepsAsync()
    {
        using var client = factory.CreateClient();
        return (await client.GetAsync<FlowsResponse>("/api/flows")).Flows.SelectMany(flow => flow.Steps).ToList();
    }

    [Fact]
    public async Task Every_script_agent_and_prompt_used_by_a_flow_exists()
    {
        var steps = await AllStepsAsync();

        var referenced = steps.Select(step => step.Script)
            .Concat(steps.Select(step => step.Prompt))
            .Concat(steps.Where(step => step.Agent is not null).Select(step => $"agents/{step.Agent}.agent.md"))
            .Concat(steps.SelectMany(step => step.Inputs).Where(input => input.StartsWith("templates/")))
            .OfType<string>()
            .Distinct();

        Assert.DoesNotContain(referenced, path => !File.Exists(Path.Combine(BlocksDirectory, path)));
    }

    [Fact]
    public async Task Blocks_endpoint_lists_agents_skills_prompts_scripts_and_templates()
    {
        using var client = factory.CreateClient();

        var blocks = await client.GetAsync<List<BlockFile>>("/api/blocks");

        Assert.Contains(blocks, block => block is { Kind: "agents", Name: "retro" });
        Assert.Contains(blocks, block => block is { Kind: "skills", Name: "retro", Path: "skills/retro/SKILL.md" });
        Assert.Contains(blocks, block => block is { Kind: "scripts", Name: "clone-repo" });
        Assert.Contains(blocks, block => block is { Kind: "templates", Name: "mail" });
    }

    [Fact]
    public void Every_agent_and_skill_has_the_frontmatter_copilot_requires()
    {
        var files = Directory.EnumerateFiles(Path.Combine(BlocksDirectory, "agents"), "*.agent.md")
            .Concat(Directory.EnumerateFiles(Path.Combine(BlocksDirectory, "skills"), "SKILL.md", SearchOption.AllDirectories));

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.True(text.StartsWith("---"), $"{file}: brak frontmattera");
            Assert.True(text.Contains("\nname: ") && text.Contains("\ndescription: "), $"{file}: brak name/description");
        }
    }

    [Fact]
    public void Every_script_is_saved_as_utf8_with_bom()
    {
        // Windows PowerShell 5.1 czyta plik bez BOM jako ANSI i psuje polskie znaki.
        var withoutBom = Directory.EnumerateFiles(BlocksDirectory, "*.ps1", SearchOption.AllDirectories)
            .Where(path => !File.ReadAllBytes(path).AsSpan().StartsWith<byte>([0xEF, 0xBB, 0xBF]))
            .Select(Path.GetFileName);

        Assert.Empty(withoutBom);
    }
}
