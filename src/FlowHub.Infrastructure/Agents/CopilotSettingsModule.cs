using System.Diagnostics;
using FlowHub.Application.Agents;
using FlowHub.Application.Settings;
using Microsoft.Extensions.Options;

namespace FlowHub.Infrastructure.Agents;

/// <summary>Ustawienia: czy kroki Agent uruchamia atrapa, czy prawdziwy Copilot CLI.</summary>
public sealed class CopilotSettingsModule(IOptions<AgentOptions> options) : ISettingsModule
{
    public string Id => "copilot";

    public string Title => "GitHub Copilot CLI";

    public string Icon => "mdi-robot-outline";

    public string Description =>
        "Agent kroków flow i terminal w zadaniu. Logowanie robisz raz w zwykłym terminalu: copilot login. FlowHub nigdy nie używa --allow-all.";

    public bool CanTest => true;

    public IReadOnlyList<SettingField> Fields { get; } =
    [
        new("Agent:Runner", "Agent kroków", SettingKind.Choice, "Atrapa nie zużywa tokenów i nie potrzebuje Copilota.",
        [
            new(AgentRunners.Fake, "Atrapa (Fake)"),
            new(AgentRunners.CopilotCli, "Copilot CLI"),
        ]),
        new("Agent:CliPath", "Polecenie CLI", Hint: "Nazwa z PATH (copilot) albo pełna ścieżka do copilot.exe."),
    ];

    public async Task<ConnectionTest> TestAsync(CancellationToken ct)
    {
        var info = CopilotCli.StartInfo(options.Value.CliPath, Environment.CurrentDirectory, ["--version"]);

        try
        {
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Proces się nie uruchomił.");
            process.StandardInput.Close();
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            var version = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            return process.ExitCode == 0
                ? new ConnectionTest(true, $"{version} ({info.FileName})")
                : new ConnectionTest(false, $"Copilot CLI zakończył się kodem {process.ExitCode}.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new ConnectionTest(false, $"Nie znaleziono polecenia '{info.FileName}'. Zainstaluj Copilot CLI albo podaj pełną ścieżkę.");
        }
    }
}
