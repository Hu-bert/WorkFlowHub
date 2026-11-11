using FlowHub.Api.Endpoints;
using FlowHub.Application.Workspace;
using FlowHub.Domain.Flows;
using Microsoft.Extensions.DependencyInjection;

namespace FlowHub.Api.Tests;

/// <summary>Pilnuje biblioteki blocks/: flow nie może wskazywać nieistniejących plików, a skrypty muszą mieć BOM.</summary>
public sealed class BlocksLibraryTests(FlowHubApiFactory factory) : IClassFixture<FlowHubApiFactory>
{
    private string BlocksDirectory => factory.Services.GetRequiredService<WorkspacePaths>().BlocksDirectory;

    [Fact]
    public async Task Every_script_used_by_a_flow_exists()
    {
        using var client = factory.CreateClient();
        var flows = (await client.GetAsync<FlowsResponse>("/api/flows")).Flows;

        var missing = flows
            .SelectMany(flow => flow.Steps.Where(step => step.Kind == StepKind.Script).Select(step => step.Script!))
            .Distinct()
            .Where(script => !File.Exists(Path.Combine(BlocksDirectory, script)));

        Assert.Empty(missing);
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
