using System.Text.RegularExpressions;
using FlowHub.Domain;
using FlowHub.Domain.Repositories;

namespace FlowHub.Application.Repositories;

/// <summary>Jeden działający proces albo usługa znaleziona na maszynie.</summary>
public sealed record RunningProcess(string Kind, string Name, string State, DateTime? StartedAt);

/// <summary>Wynik sprawdzenia jednego wpisu z katalogu repo: czy coś o tej nazwie działa na maszynie.</summary>
public sealed record RemoteProcessState(string Host, string Name, string? Label, bool Running, IReadOnlyList<RunningProcess> Found, string? Error);

/// <summary>Port: odczyt procesów i usług Windows z maszyny (tylko odczyt).</summary>
public interface IRemoteProcessProbe
{
    Task<IReadOnlyList<RunningProcess>> FindAsync(string host, string name, CancellationToken ct);
}

/// <summary>
/// „Czy serwis wstał po wdrożeniu?” — sprawdza procesy / usługi przypisane do repo w katalogu.
/// Błąd jednej maszyny (brak dostępu, wyłączony WinRM) nie przerywa sprawdzania pozostałych.
/// </summary>
public sealed partial class RemoteProcessService(IRemoteProcessProbe probe)
{
    public async Task<IReadOnlyList<RemoteProcessState>> CheckAsync(CodeRepository repository, CancellationToken ct) =>
        await Task.WhenAll(repository.Processes.Select(process => CheckAsync(process, ct)));

    private async Task<RemoteProcessState> CheckAsync(RemoteProcess process, CancellationToken ct)
    {
        try
        {
            Validate(process);
            var found = await probe.FindAsync(process.Host, process.Name, ct);

            // Usługa zatrzymana to „nie działa”, nawet jeśli wpis o niej istnieje.
            var running = found.Any(item => item.Kind == "process" || string.Equals(item.State, "Running", StringComparison.OrdinalIgnoreCase));
            return new RemoteProcessState(process.Host, process.Name, process.Label, running, found, null);
        }
        catch (DomainException error)
        {
            return new RemoteProcessState(process.Host, process.Name, process.Label, false, [], error.Message);
        }
    }

    /// <summary>Host i nazwa trafiają do polecenia systemowego — przepuszczamy tylko bezpieczne znaki.</summary>
    public static void Validate(RemoteProcess process)
    {
        if (!HostPattern().IsMatch(process.Host))
        {
            throw new DomainException($"Nieprawidłowa nazwa maszyny '{process.Host}' (litery, cyfry, kropki i myślniki).");
        }

        if (!NamePattern().IsMatch(process.Name))
        {
            throw new DomainException($"Nieprawidłowa nazwa procesu '{process.Name}' (litery, cyfry, kropki, myślniki, podkreślenia i spacje).");
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9]([A-Za-z0-9.\-]{0,253})$")]
    private static partial Regex HostPattern();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._\- ]{0,120}$")]
    private static partial Regex NamePattern();
}