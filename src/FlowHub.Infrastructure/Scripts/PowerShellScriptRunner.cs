using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using FlowHub.Application.Engine.Scripts;

namespace FlowHub.Infrastructure.Scripts;

/// <summary>Uruchamia skrypt .ps1 w osobnym procesie PowerShell i czyta jego wyjście na żywo (UTF-8).</summary>
public sealed class PowerShellScriptRunner : IScriptRunner
{
    private static readonly string Shell = FindOnPath("pwsh.exe") ?? "powershell.exe";

    public async Task<int> RunAsync(ScriptRequest request, Func<string, Task> onOutputLine, CancellationToken ct)
    {
        using var process = new Process { StartInfo = StartInfo(request) };
        process.Start();
        process.StandardInput.Close();

        var lines = Channel.CreateUnbounded<string>();
        var readers = Task.WhenAll(
            PumpAsync(process.StandardOutput, lines.Writer),
            PumpAsync(process.StandardError, lines.Writer));
        _ = readers.ContinueWith(_ => lines.Writer.TryComplete(), TaskScheduler.Default);

        try
        {
            await foreach (var line in lines.Reader.ReadAllAsync(ct))
            {
                await onOutputLine(line);
            }

            await process.WaitForExitAsync(ct);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }
    }

    private static ProcessStartInfo StartInfo(ScriptRequest request)
    {
        // Konsola skryptu w UTF-8, żeby polskie znaki w wyjściu były czytelne.
        var command = $"[Console]::OutputEncoding = [Text.Encoding]::UTF8; & '{request.ScriptPath.Replace("'", "''")}'; exit $LASTEXITCODE";

        var info = new ProcessStartInfo(Shell)
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

        foreach (var argument in (string[])["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command])
        {
            info.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in request.Environment)
        {
            info.Environment[name] = value;
        }

        return info;
    }

    private static async Task PumpAsync(StreamReader reader, ChannelWriter<string> writer)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            writer.TryWrite(line);
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

    private static string? FindOnPath(string fileName) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory.Trim(), fileName))
            .FirstOrDefault(File.Exists);
}
