using FlowHub.Application.Engine;
using FlowHub.Application.Engine.Scripts;
using FlowHub.Application.Repositories;
using FlowHub.Application.Workspace;
using FlowHub.Domain;
using FlowHub.Domain.Flows;
using FlowHub.Domain.Repositories;
using FlowHub.Domain.WorkItems;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FlowHub.Application.Tests.Engine;

public sealed class StepExecutorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("flowhub-exec-").FullName;
    private readonly IScriptRunner _runner = Substitute.For<IScriptRunner>();
    private readonly IStepLog _log = Substitute.For<IStepLog>();
    private readonly ScriptStepExecutor _executor;

    public StepExecutorTests()
    {
        var paths = new WorkspacePaths(Options.Create(new WorkspaceOptions { Root = _root }));
        Directory.CreateDirectory(Path.Combine(paths.BlocksDirectory, "scripts"));
        File.WriteAllText(Path.Combine(paths.BlocksDirectory, "scripts", "ok.ps1"), "");

        var repositories = Substitute.For<IRepositoryCatalog>();
        repositories.Find("Repo.A").Returns(new CodeRepository { Name = "Repo.A", Url = "demo://Repo.A", DefaultBranch = "develop" });

        _executor = new ScriptStepExecutor(_runner, new StepEnvironment(paths, repositories), paths);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static StepContext Context(string script, bool perRepo = false)
    {
        var definition = new StepDefinition { Id = "step", Name = "Krok", Kind = StepKind.Script, Script = script, PerRepo = perRepo };
        var item = WorkItem.Create(
            new NewWorkItem("T-1", "Tytuł", "Opis zadania", "Test", TaskSourceKind.Manual, null), "work", "todo", DateTime.UtcNow);
        item.AddRepo("Repo.A");
        item.PlanSteps(new FlowDefinition { Type = "Test", Name = "Test", Steps = [definition] });

        return new StepContext(item, item.Steps[0], definition);
    }

    private void ScriptPrints(int exitCode, params string[] lines) =>
        _runner.RunAsync(Arg.Any<ScriptRequest>(), Arg.Any<Func<string, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                foreach (var line in lines)
                {
                    await call.Arg<Func<string, Task>>()(line);
                }

                return exitCode;
            });

    [Fact]
    public async Task Successful_script_returns_its_last_lines_as_the_summary()
    {
        ScriptPrints(0, "Klonuję...", "", "Repo gotowe");

        var outcome = await _executor.ExecuteAsync(Context("scripts/ok.ps1"), _log, CancellationToken.None);

        Assert.True(outcome.Succeeded);
        Assert.Equal($"Klonuję...{Environment.NewLine}Repo gotowe", outcome.Message);
        await _log.Received(3).WriteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_zero_exit_code_fails_the_step_with_the_output_tail()
    {
        ScriptPrints(3, "fatal: repository not found");

        var outcome = await _executor.ExecuteAsync(Context("scripts/ok.ps1"), _log, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("kodem 3", outcome.Message);
        Assert.Contains("repository not found", outcome.Message);
    }

    [Fact]
    public async Task Script_gets_the_task_and_repo_context_as_environment_variables()
    {
        ScriptRequest? request = null;
        _runner.RunAsync(Arg.Do<ScriptRequest>(r => request = r), Arg.Any<Func<string, Task>>(), Arg.Any<CancellationToken>()).Returns(0);

        await _executor.ExecuteAsync(Context("scripts/ok.ps1", perRepo: true), _log, CancellationToken.None);

        Assert.Equal("T-1", request!.Environment["FLOWHUB_TASK_KEY"]);
        Assert.Equal("Opis zadania", request.Environment["FLOWHUB_TASK_DESCRIPTION"]);
        Assert.Equal("Repo.A", request.Environment["FLOWHUB_REPO_NAME"]);
        Assert.Equal("demo://Repo.A", request.Environment["FLOWHUB_REPO_URL"]);
        Assert.Equal("develop", request.Environment["FLOWHUB_DEFAULT_BRANCH"]);
        Assert.Equal("feature/T-1-tytul", request.Environment["FLOWHUB_BRANCH"]);
    }

    [Theory]
    [InlineData("../outside.ps1", "wychodzi poza katalog blocks")]
    [InlineData("scripts/missing.ps1", "Nie ma skryptu")]
    public async Task Script_must_exist_inside_the_blocks_directory(string script, string expectedError)
    {
        var error = await Assert.ThrowsAsync<DomainException>(
            () => _executor.ExecuteAsync(Context(script), _log, CancellationToken.None));

        Assert.Contains(expectedError, error.Message);
    }

    [Fact]
    public async Task Manual_step_shows_the_checklist_and_succeeds_when_confirmed()
    {
        var manual = new ManualStepExecutor();
        var context = Context("x") with
        {
            Definition = new StepDefinition { Id = "m", Name = "M", Kind = StepKind.Manual, Checklist = ["Sprawdź plan", "Wybierz model"] },
        };

        var proposal = await manual.ProposeAsync(context, CancellationToken.None);
        var outcome = await manual.ExecuteAsync(context, _log, CancellationToken.None);

        Assert.Equal($"☐ Sprawdź plan{Environment.NewLine}☐ Wybierz model", proposal);
        Assert.True(outcome.Succeeded);
    }
}
