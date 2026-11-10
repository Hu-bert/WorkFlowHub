using FlowHub.Application.Board;
using FlowHub.Application.Flows;
using FlowHub.Application.Repositories;
using FlowHub.Application.WorkItems;
using FlowHub.Application.Workspace;
using FlowHub.Domain;
using FlowHub.Domain.Repositories;
using FlowHub.Domain.WorkItems;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FlowHub.Application.Tests;

public sealed class WorkItemServiceTests
{
    private readonly IWorkItemStore _store = Substitute.For<IWorkItemStore>();
    private readonly WorkItemService _service;

    public WorkItemServiceTests()
    {
        var flowStore = Substitute.For<IFlowStore>();
        flowStore.LoadAll().Returns([new FlowFile("bug.yaml", BugFlow(), null)]);

        var repositories = Substitute.For<IRepositoryCatalog>();
        repositories.Find("Billing.Api").Returns(new CodeRepository { Name = "Billing.Api", Url = "x" });

        var board = Options.Create(new BoardOptions
        {
            Columns = [new BoardColumn { Key = "todo" }, new BoardColumn { Key = "done" }],
        });

        _service = new WorkItemService(
            _store,
            new FlowCatalog(flowStore, NullLogger<FlowCatalog>.Instance),
            repositories,
            board,
            new WorkspacePaths(Options.Create(new WorkspaceOptions { Root = Path.GetTempPath() })),
            TimeProvider.System);
    }

    private static FlowDocument BugFlow() => new()
    {
        Type = "Bug",
        Name = "Błąd",
        Flow =
        [
            new StepDocument { Id = "create-folder", Kind = "Script", Script = "s.ps1" },
            new StepDocument { Id = "clone-repo", Kind = "Script", Script = "s.ps1", PerRepo = true },
        ],
    };

    private static CreateWorkItemRequest Request(string type = "Bug", params string[] repositories) =>
        new("PROJ-1", "Błąd w fakturze", "Opis", type, TaskSourceKind.Manual, null, repositories);

    [Fact]
    public async Task Creates_item_in_the_first_column_with_planned_steps()
    {
        var created = await _service.CreateAsync(Request("Bug", "Billing.Api"), CancellationToken.None);

        Assert.Equal("todo", created.Summary.BoardColumn);
        Assert.Equal(["create-folder", "clone-repo"], created.Steps.Select(step => step.StepId));
        Assert.Equal("Billing.Api", created.Steps[1].RepoName);
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), created.WorkDirectory);
        await _store.Received(1).AddAsync(Arg.Any<WorkItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_unknown_type_unknown_repo_and_missing_repo()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(Request("Nope", "Billing.Api"), CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(Request("Bug", "Unknown.Repo"), CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(Request("Bug"), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_a_key_that_already_exists()
    {
        _store.KeyExistsAsync("PROJ-1", Arg.Any<CancellationToken>()).Returns(true);

        var error = await Assert.ThrowsAsync<DomainException>(
            () => _service.CreateAsync(Request("Bug", "Billing.Api"), CancellationToken.None));

        Assert.Contains("już istnieje", error.Message);
    }

    [Fact]
    public async Task Moves_item_only_to_an_existing_column()
    {
        var item = WorkItem.Create(
            new NewWorkItem("PROJ-1", "Tytuł", null, "Bug", TaskSourceKind.Manual, null), "work", "todo", DateTime.UtcNow);
        _store.FindAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);

        Assert.True(await _service.MoveAsync(item.Id, "done", CancellationToken.None));
        Assert.Equal("done", item.BoardColumn);

        await Assert.ThrowsAsync<DomainException>(() => _service.MoveAsync(item.Id, "nowhere", CancellationToken.None));
        Assert.False(await _service.MoveAsync(Guid.NewGuid(), "done", CancellationToken.None));
    }
}
