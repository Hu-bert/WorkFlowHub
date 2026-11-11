using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using FlowHub.Application.Agents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowHub.Infrastructure.Agents;

/// <summary>
/// Agent przez Copilot CLI w trybie nieinteraktywnym:
/// copilot -p "prompt" --output-format json --agent … --model … --allow-tool … --add-dir …
///
/// Zasady bezpieczeństwa: nigdy --allow-all / --yolo; narzędzia tylko z listy kroku;
/// przed uruchomieniem z `write` lub `shell` pytamy użytkownika o zgodę w UI.
/// NIE przetestowane na prawdziwym CLI (brak dostępu) — patrz CopilotJsonlParser.
/// </summary>
public sealed class CopilotCliAgentRunner(IOptions<AgentOptions> options, ILogger<CopilotCliAgentRunner> logger) : IAgentRunner
{
    public async IAsyncEnumerable<AgentEvent> RunAsync(AgentRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        if (RiskyTools(request) is { Count: > 0 } risky
            && !await request.AskPermission(string.Join(", ", risky), $"Sesja Copilot w {request.WorkingDirectory}", ct))
        {
            yield return new AgentEvent.Failed("Odmówiono zgody na narzędzia — krok przerwany.");
            yield break;
        }

        using var process = Start(request);
        if (process is null)
        {
            yield return new AgentEvent.Failed($"Nie udało się uruchomić Copilot CLI ('{options.Value.CliPath}'). Sprawdź instalację i ustawienie Agent:CliPath.");
            yield break;
        }

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

    private Process? Start(AgentRequest request)
    {
        var info = new ProcessStartInfo(options.Value.CliPath)
        {
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in Arguments(request))
        {
            info.ArgumentList.Add(argument);
        }

        try
        {
            var process = Process.Start(info);
            process?.StandardInput.Close();
            return process;
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            logger.LogError(error, "Nie udało się uruchomić Copilot CLI: {Path}", options.Value.CliPath);
            return null;
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

        foreach (var directory in request.AdditionalDirectories)
        {
            yield return "--add-dir";
            yield return directory;
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Proces zdążył się zakończyć.
        }
    }
}
