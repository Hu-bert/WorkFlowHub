using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FlowHub.Application.Repositories;
using FlowHub.Domain;

namespace FlowHub.Infrastructure.Processes;

/// <summary>
/// Procesy i usługi Windows przez CIM (Get-CimInstance) — lokalnie albo na zdalnej maszynie przez WinRM,
/// na koncie, na którym działa FlowHub. Tylko odczyt. Host i nazwa są wcześniej sprawdzone (bezpieczne znaki)
/// i przekazane jako argumenty skryptu, nie wklejone w jego treść.
/// </summary>
public sealed class WindowsProcessProbe : IRemoteProcessProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private const string Script =
        """
        param([string]$TargetHost, [string]$Name)
        $ErrorActionPreference = 'Stop'
        [Console]::OutputEncoding = [Text.Encoding]::UTF8
        # Błąd jako jedna czytelna linia (z polskimi znakami), nie cały zrzut PowerShella.
        trap { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }
        $local = $TargetHost -in @('localhost', '.', $env:COMPUTERNAME)
        $target = @{}
        if (-not $local) { $target['ComputerName'] = $TargetHost; $target['OperationTimeoutSec'] = 20 }
        $pattern = $Name.Replace('_', '[_]') + '%'
        $found = @()
        Get-CimInstance Win32_Process -Filter "Name LIKE '$pattern'" @target | ForEach-Object {
            $found += [pscustomobject]@{ kind = 'process'; name = $_.Name; state = 'Running'; startedAt = if ($_.CreationDate) { $_.CreationDate.ToUniversalTime().ToString('o') } else { $null } }
        }
        Get-CimInstance Win32_Service -Filter "Name LIKE '$pattern' OR DisplayName LIKE '$pattern'" @target | ForEach-Object {
            $found += [pscustomobject]@{ kind = 'service'; name = $_.DisplayName; state = $_.State; startedAt = $null }
        }
        ConvertTo-Json -InputObject @($found) -Compress
        """;

    public async Task<IReadOnlyList<RunningProcess>> FindAsync(string host, string name, CancellationToken ct)
    {
        var scriptFile = Path.Combine(Path.GetTempPath(), $"flowhub-process-{Guid.NewGuid():N}.ps1");
        await File.WriteAllTextAsync(scriptFile, Script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), ct);

        try
        {
            var (exitCode, output, errors) = await RunAsync(scriptFile, host, name, ct);

            return exitCode == 0
                ? Parse(output)
                : throw new DomainException($"{host}: {Explain(errors)}");
        }
        finally
        {
            File.Delete(scriptFile);
        }
    }

    public static IReadOnlyList<RunningProcess> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Select(item => new RunningProcess(
                item.GetProperty("kind").GetString() ?? "process",
                item.GetProperty("name").GetString() ?? "?",
                item.GetProperty("state").GetString() ?? "?",
                item.TryGetProperty("startedAt", out var started) && started.ValueKind == JsonValueKind.String && DateTime.TryParse(started.GetString(), out var date)
                    ? date.ToUniversalTime()
                    : null))
            .ToList();
    }

    private static async Task<(int ExitCode, string Output, string Errors)> RunAsync(string scriptFile, string host, string name, CancellationToken ct)
    {
        var info = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in (string[])["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", scriptFile, "-TargetHost", host, "-Name", name])
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new DomainException("Nie udało się uruchomić PowerShella.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);

        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errors = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            return (process.ExitCode, await output, await errors);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            throw new DomainException($"{host}: brak odpowiedzi w {Timeout.TotalSeconds:0} s.");
        }
    }

    /// <summary>Z długiego błędu PowerShella zostawiamy pierwszą linię i podpowiedź, co zwykle jest przyczyną.</summary>
    private static string Explain(string errors)
    {
        var first = errors.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "nieznany błąd";

        return first.Contains("WinRM", StringComparison.OrdinalIgnoreCase) || first.Contains("WS-Management", StringComparison.OrdinalIgnoreCase)
            ? $"{first} (sprawdź, czy na maszynie działa WinRM i czy Twoje konto ma do niej dostęp)"
            : first;
    }
}
