using FlowHub.Application.Flows;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FlowHub.Application.Tests;

public sealed class FlowCatalogTests
{
    private static FlowDocument Flow(string type) => new()
    {
        Type = type,
        Name = type,
        Flow = [new StepDocument { Id = "confirm", Kind = "Manual" }],
    };

    private static FlowCatalog CatalogWith(params FlowFile[] files)
    {
        var store = Substitute.For<IFlowStore>();
        store.LoadAll().Returns(files);
        return new FlowCatalog(store, NullLogger<FlowCatalog>.Instance);
    }

    [Fact]
    public void Broken_file_does_not_block_the_valid_ones()
    {
        var catalog = CatalogWith(
            new FlowFile("bug.yaml", Flow("Bug"), null),
            new FlowFile("broken.yaml", null, "Błąd składni YAML (linia 3)"),
            new FlowFile("empty.yaml", new FlowDocument(), null));

        Assert.Equal(["Bug"], catalog.Flows.Select(flow => flow.Type));
        Assert.Equal(["broken.yaml", "empty.yaml"], catalog.Problems.Select(problem => problem.FileName));
        Assert.Contains("linia 3", catalog.Problems[0].Errors[0]);
    }

    [Fact]
    public void Same_type_in_two_files_is_a_problem()
    {
        var catalog = CatalogWith(
            new FlowFile("bug.yaml", Flow("Bug"), null),
            new FlowFile("bug-copy.yaml", Flow("bug"), null));

        Assert.Single(catalog.Flows);
        Assert.Contains("już zdefiniowany", Assert.Single(catalog.Problems).Errors[0]);
    }

    [Fact]
    public void Find_ignores_case_and_reload_reads_files_again()
    {
        var store = Substitute.For<IFlowStore>();
        store.LoadAll().Returns([new FlowFile("bug.yaml", Flow("Bug"), null)]);
        var catalog = new FlowCatalog(store, NullLogger<FlowCatalog>.Instance);

        Assert.NotNull(catalog.Find("bug"));
        Assert.Null(catalog.Find("Spike"));

        store.LoadAll().Returns([new FlowFile("spike.yaml", Flow("Spike"), null)]);
        catalog.Reload();

        Assert.NotNull(catalog.Find("Spike"));
    }
}
