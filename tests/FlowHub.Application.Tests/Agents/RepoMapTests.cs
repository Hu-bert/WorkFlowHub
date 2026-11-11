using FlowHub.Application.Agents;
using FlowHub.Application.Blocks;
using FlowHub.Application.Engine;
using FlowHub.Application.Git;
using FlowHub.Application.Repositories;
using FlowHub.Application.Workspace;
using FlowHub.Domain;
using FlowHub.Domain.Flows;
using FlowHub.Domain.WorkItems;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FlowHub.Application.Tests.Agents;

/// <summary>Mapa repo: analiza zapisuje wynik per repo, a następne zadanie dostaje go do aktualizacji.</summary>
public sealed class RepoMapTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"flowhub-map-{Guid.NewGuid():N}");
    private readonly RepoMapStore _maps;
    private readonly IGitService _git = Substitute.For<IGitService>();
    private readonly ScriptedRunner _runner = new();
    private readonly List<string> _log = [];

    private static readonly StepDefinition Analyze = new()
    {
        Id = "analyze-tech", Name = "Analiza", Kind = StepKind.Agent, Agent = "a", PerRepo = true, Remember = true, Outputs = ["context-{repo}.md"],
    };

    public RepoMapTests()
    {
        _maps = new RepoMapStore(new WorkspacePaths(Options.Create(new WorkspaceOptions { Root = _root })), TimeProvider.System);
        _git.DescribeAsync(default!, default).ReturnsForAnyArgs(new GitWorkingCopy(null, "main", [new GitCommit("abc1234", "Ja", DateTimeOffset.UtcNow, "Start")], 0));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private (AgentStepExecutor Executor, AgentPromptBuilder Prompts, StepContext Context) Arrange(string key, StepDefinition? definition = null)
    {
        definition ??= Analyze;
        var item = WorkItem.Create(new NewWorkItem(key, "Zadanie", null, "Test", TaskSourceKind.Manual, null), Path.Combine(_root, "work"), "todo", DateTime.UtcNow);
        var repo = item.AddRepo("Shared.Lib");
        Directory.CreateDirectory(repo.LocalPath);
        item.PlanSteps(new FlowDefinition { Type = "Test", Name = "Test", Steps = [definition] });

        var models = Options.Create(new ModelOptions { Tiers = { ["cheap"] = "auto" } });
        var blocks = Substitute.For<IBlockLibrary>();
        blocks.ReadText(Arg.Any<string>()).Returns((string?)null);
        var prompts = new AgentPromptBuilder(blocks, _maps);
        var executor = new AgentStepExecutor(_runner, prompts, new PermissionBroker(Substitute.For<ILiveNotifier>()), new ModelRouter(models), new TokenBudget(models), _maps, _git);

        return (executor, prompts, new StepContext(item, item.Steps[0], definition));
    }

    private IStepLog Log()
    {
        var log = Substitute.For<IStepLog>();
        log.WriteAsync(Arg.Do<string>(_log.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return log;
    }

    [Fact]
    public async Task First_analysis_is_saved_as_the_repo_map_with_the_commit_it_describes()
    {
        var (executor, prompts, context) = Arrange("T-1");
        _runner.WriteToOutput = "# Shared.Lib\nStack: .NET";
        var promptBefore = prompts.Build(context);

        var outcome = await executor.ExecuteAsync(context, Log(), CancellationToken.None);

        var map = _maps.Find("Shared.Lib");
        Assert.True(outcome.Succeeded);
        Assert.Equal(("# Shared.Lib\nStack: .NET", "abc1234", "T-1"), (map?.Content, map?.Commit, map?.TaskKey));
        Assert.DoesNotContain("ma już zapisaną mapę", promptBefore); // pierwsza analiza — od zera
        Assert.Contains(_log, line => line.Contains("Zapisano mapę repo Shared.Lib"));
    }

    [Fact]
    public async Task Next_task_starts_from_the_saved_map_and_is_told_to_update_it_instead_of_starting_over()
    {
        _maps.Save("Shared.Lib", "# Shared.Lib\nStara mapa", "abc1234", "T-1");
        var (executor, prompts, context) = Arrange("T-2");
        var output = Path.Combine(context.Item.WorkDirectory, "context-Shared.Lib.md");
        _runner.OnRun = () => _runner.SeenBeforeRun = File.Exists(output) ? File.ReadAllText(output) : null;
        _runner.WriteToOutput = "# Shared.Lib\nZaktualizowana mapa";

        var prompt = prompts.Build(context);
        await executor.ExecuteAsync(context, Log(), CancellationToken.None);

        Assert.Contains("NIE analizuj repo od zera", prompt);
        Assert.Contains("git log abc1234..HEAD", prompt);
        Assert.Contains("zadanie T-1", prompt);
        Assert.Equal("# Shared.Lib\nStara mapa", _runner.SeenBeforeRun); // agent zastał starą mapę w pliku wynikowym
        Assert.Equal(("# Shared.Lib\nZaktualizowana mapa", "T-2"), (_maps.Find("Shared.Lib")?.Content, _maps.Find("Shared.Lib")?.TaskKey));
    }

    [Fact]
    public async Task Failed_analysis_does_not_overwrite_the_saved_map()
    {
        _maps.Save("Shared.Lib", "dobra mapa", "abc1234", "T-1");
        var (executor, _, context) = Arrange("T-3");
        _runner.Fail = true;

        await executor.ExecuteAsync(context, Log(), CancellationToken.None);

        Assert.Equal("dobra mapa", _maps.Find("Shared.Lib")?.Content);
    }

    [Fact]
    public async Task Steps_without_the_remember_flag_save_nothing()
    {
        var (executor, _, context) = Arrange("T-4", Analyze with { Remember = false });
        _runner.WriteToOutput = "treść";

        await executor.ExecuteAsync(context, Log(), CancellationToken.None);

        Assert.Null(_maps.Find("Shared.Lib"));
    }

    [Fact]
    public void Map_can_be_corrected_and_removed_and_its_name_cannot_escape_the_folder()
    {
        _maps.Save("Shared.Lib", "v1", "abc1234", "T-1");

        var edited = _maps.Edit("Shared.Lib", "v2");

        Assert.Equal(("v2", "abc1234", "T-1"), (edited.Content, edited.Commit, edited.TaskKey));
        Assert.True(_maps.Delete("Shared.Lib"));
        Assert.Null(_maps.Find("Shared.Lib"));
        Assert.Throws<DomainException>(() => _maps.Find(@"..\..\sekret"));
        Assert.Throws<DomainException>(() => _maps.Edit("Shared.Lib", "  "));
    }

    private sealed class ScriptedRunner : IAgentRunner
    {
        public string? WriteToOutput { get; set; }
        public bool Fail { get; set; }
        public Action? OnRun { get; set; }
        public string? SeenBeforeRun { get; set; }

        public async IAsyncEnumerable<AgentEvent> RunAsync(AgentRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            OnRun?.Invoke();
            await Task.Yield();

            if (Fail)
            {
                yield return new AgentEvent.Failed("błąd");
                yield break;
            }

            if (WriteToOutput is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(request.ExpectedOutputs[0])!);
                File.WriteAllText(request.ExpectedOutputs[0], WriteToOutput);
            }

            yield return new AgentEvent.Completed("gotowe");
        }
    }
}