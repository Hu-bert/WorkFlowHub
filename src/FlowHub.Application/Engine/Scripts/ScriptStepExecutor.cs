using FlowHub.Application.Workspace;
using FlowHub.Domain;
using FlowHub.Domain.Flows;

namespace FlowHub.Application.Engine.Scripts;

/// <summary>Krok Script: skrypt PowerShell z blocks/ — bez AI, 0 tokenów.</summary>
public sealed class ScriptStepExecutor(IScriptRunner runner, StepEnvironment environment, WorkspacePaths paths) : IStepExecutor
{
    private const int SummaryLines = 12;

    public StepKind Kind => StepKind.Script;

    public Task<string> ProposeAsync(StepContext context, CancellationToken ct)
    {
        var lines = new List<string> { $"Skrypt: {context.Definition.Script}" };

        if (context.Repo is { } repo)
        {
            lines.Add($"Repo: {repo.RepositoryName}");
            lines.Add($"Branch: {repo.Branch}");
            lines.Add($"Katalog: {repo.LocalPath}");
        }
        else
        {
            lines.Add($"Katalog: {context.Item.WorkDirectory}");
        }

        return Task.FromResult(string.Join(Environment.NewLine, lines));
    }

    public async Task<StepOutcome> ExecuteAsync(StepContext context, IStepLog log, CancellationToken ct)
    {
        var request = new ScriptRequest(ScriptPath(context.Definition), WorkingDirectory(context), environment.For(context));
        var lastLines = new Queue<string>();

        var exitCode = await runner.RunAsync(request, async line =>
        {
            Remember(lastLines, line);
            await log.WriteAsync(line + Environment.NewLine, ct);
        }, ct);

        var summary = string.Join(Environment.NewLine, lastLines);

        return exitCode == 0
            ? StepOutcome.Success(summary.Length > 0 ? summary : "Skrypt zakończony.")
            : StepOutcome.Failure($"Skrypt zakończył się kodem {exitCode}.{Environment.NewLine}{summary}".TrimEnd());
    }

    /// <summary>Skrypt musi leżeć w blocks/ — ścieżka z flow nie może wyjść poza ten katalog.</summary>
    private string ScriptPath(StepDefinition definition)
    {
        var blocks = Path.GetFullPath(paths.BlocksDirectory) + Path.DirectorySeparatorChar;
        var script = Path.GetFullPath(Path.Combine(blocks, definition.Script ?? ""));

        if (!script.StartsWith(blocks, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException($"Skrypt '{definition.Script}' wychodzi poza katalog blocks/.");
        }

        return File.Exists(script)
            ? script
            : throw new DomainException($"Nie ma skryptu blocks/{definition.Script}.");
    }

    private string WorkingDirectory(StepContext context)
    {
        string?[] candidates = [context.Repo?.LocalPath, context.Item.WorkDirectory];
        return candidates.FirstOrDefault(Directory.Exists) ?? paths.Root;
    }

    private static void Remember(Queue<string> lastLines, string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        lastLines.Enqueue(line);
        if (lastLines.Count > SummaryLines)
        {
            lastLines.Dequeue();
        }
    }
}
