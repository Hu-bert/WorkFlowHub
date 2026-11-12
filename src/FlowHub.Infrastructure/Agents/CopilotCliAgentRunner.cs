using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlowHub.Application.Agents;
using FlowHub.Application.Settings;
using FlowHub.Domain.WorkItems;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowHub.Infrastructure.Agents;

/// <summary>
/// Agent przez Copilot CLI w trybie nieinteraktywnym:
/// copilot -p "prompt" --output-format json --agent … --model … --allow-tool … --add-dir …
///
/// Zasady bezpieczeństwa: nigdy --allow-all / --yolo; narzędzia tylko z listy kroku;
/// przed uruchomieniem z `write` lub `shell` pytamy użytkownika o zgodę w UI.
/// Sprawdzone na Copilot CLI 1.0.109. W trybie -p CLI nie pyta o zgodę: narzędzie spoza listy
/// --allow-tool jest odrzucane. Tokeny przychodzą dopiero na końcu (plik --usage-output-file).
/// </summary>
public sealed class CopilotCliAgentRunner(IOptions<AgentOptions> options, ILogger<CopilotCliAgentRunner> logger, IKnownModels? knownModels = null) : IAgentRunner
{
    public async IAsyncEnumerable<AgentEvent> RunAsync(AgentRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        if (RiskyTools(request) is { Count: > 0 } risky
            && !await request.AskPermission(string.Join(", ", risky), $"Sesja Copilot w {request.WorkingDirectory}", ct))
        {
            yield return new AgentEvent.Failed("Odmówiono zgody na narzędzia — krok przerwany.");
            yield break;
        }

        var usageFile = Path.Combine(Path.GetTempPath(), $"flowhub-usage-{Guid.NewGuid():N}.json");
        using var usageCleanup = new FileCleanup(usageFile);
        using var process = Start(request, usageFile);
        if (process is null)
        {
            yield return new AgentEvent.Failed($"Nie udało się uruchomić Copilot CLI ('{options.Value.CliPath}'). Sprawdź instalację i ustawienie Agent:CliPath.");
            yield break;
        }

        // Sprzątanie także wtedy, gdy silnik przerwie sesję wcześniej (np. przekroczony budżet tokenów).
        using var cleanup = new ProcessCleanup(process);
        using var kill = ct.Register(() => Kill(process));
        var errors = process.StandardError.ReadToEndAsync(CancellationToken.None);
        var parser = new CopilotJsonlParser();
        var failed = false;

        while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
        {
            if (parser.Parse(line) is { } agentEvent)
            {
                failed |= agentEvent is AgentEvent.Failed;
                yield return agentEvent;
            }
        }

        await process.WaitForExitAsync(ct);

        var usages = ReadUsage(usageFile);
        knownModels?.Remember(parser.SeenModels.Concat(usages.Select(usage => usage.Model).OfType<string>()));

        foreach (var usage in usages)
        {
            yield return new AgentEvent.Usage(usage, Final: true);
        }

        if (failed)
        {
            yield break;
        }

        yield return process.ExitCode == 0
            ? new AgentEvent.Completed(parser.LastMessage.Trim() is { Length: > 0 } message ? message : "Sesja Copilot zakończona.")
            : new AgentEvent.Failed($"Copilot CLI zakończył się kodem {process.ExitCode}. {(await errors).Trim()}".Trim());
    }

    private static List<string> RiskyTools(AgentRequest request) =>
        request.AllowedTools.Where(tool => tool.StartsWith("write") || tool.StartsWith("shell")).ToList();

    private Process? Start(AgentRequest request, string usageFile)
    {
        var info = CopilotCli.StartInfo(
            options.Value.CliPath,
            request.WorkingDirectory,
            [.. Arguments(request), "--usage-output-file", usageFile]);

        try
        {
            var process = Process.Start(info);
            process?.StandardInput.Close();
            return process;
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            logger.LogError(error, "Nie udało się uruchomić Copilot CLI: {Path}", info.FileName);
            return null;
        }
    }

    private IReadOnlyList<TokenUsage> ReadUsage(string usageFile)
    {
        try
        {
            return File.Exists(usageFile) ? CopilotJsonlParser.ParseUsageFile(File.ReadAllText(usageFile)) : [];
        }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException)
        {
            logger.LogWarning(error, "Nie udało się odczytać zużycia tokenów z {File}", usageFile);
            return [];
        }
    }
    /// <summary>Argumenty CLI — nazwy flag zgodne z docs/copilot-cli.md.</summary>
    public static IEnumerable<string> Arguments(AgentRequest request)
    {
        yield return "-p";
        yield return request.Prompt;
        yield return "--output-format";
        yield return "json";
        yield return "--no-ask-user";
        yield return "--no-auto-update";
        yield return "--no-color";

        if (request.ResumeSessionId is { Length: > 0 } session)
        {
            yield return $"--resume={session}";
        }

        if (request.Agent is { Length: > 0 } agent)
        {
            yield return "--agent";
            yield return agent;
        }

        if (request.Model is { Length: > 0 } model)
        {
            yield return "--model";
            yield return model;
        }

        foreach (var tool in request.AllowedTools)
        {
            yield return "--allow-tool";
            yield return tool;
        }

        // Pliki wynikowe kroku agent musi móc zapisać — zgoda tylko na te konkretne ścieżki, nie na cały zapis.
        foreach (var output in request.ExpectedOutputs)
        {
            yield return "--allow-tool";
            yield return $"write({output})";
        }

        foreach (var directory in request.AdditionalDirectories)
        {
            yield return "--add-dir";
            yield return directory;
        }
    }

    private sealed class FileCleanup(string path) : IDisposable
    {
        public void Dispose()
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Plik tymczasowy — posprząta system.
            }
        }
    }

    private sealed class ProcessCleanup(Process process) : IDisposable
    {
        public void Dispose() => Kill(process);
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Proces zdążył się zakończyć.
        }
    }
}
