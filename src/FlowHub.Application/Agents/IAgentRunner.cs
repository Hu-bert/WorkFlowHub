using FlowHub.Domain.WorkItems;

namespace FlowHub.Application.Agents;

/// <summary>Pytanie agenta o zgodę na ryzykowne narzędzie. Zwraca true, gdy użytkownik pozwolił.</summary>
public delegate Task<bool> AskPermission(string tool, string detail, CancellationToken ct);

/// <summary>Jedno uruchomienie agenta = jedna sesja Copilot dla jednego kroku flow.</summary>
public sealed record AgentRequest
{
    public required string Prompt { get; init; }

    /// <summary>Nazwa custom agenta (.github/agents/&lt;nazwa&gt;.agent.md) albo null.</summary>
    public string? Agent { get; init; }

    /// <summary>Id modelu albo null = domyślny.</summary>
    public string? Model { get; init; }

    /// <summary>Katalog roboczy sesji (repo zadania).</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Dodatkowe katalogi z dostępem (katalog zadania).</summary>
    public IReadOnlyList<string> AdditionalDirectories { get; init; } = [];

    /// <summary>Narzędzia dozwolone bez pytania, np. read, write, shell(git:*).</summary>
    public IReadOnlyList<string> AllowedTools { get; init; } = [];

    /// <summary>Pliki, które agent ma utworzyć (pełne ścieżki).</summary>
    public IReadOnlyList<string> ExpectedOutputs { get; init; } = [];

    public required AskPermission AskPermission { get; init; }
}

/// <summary>Zdarzenia sesji agenta — wspólne dla Fake, Copilot CLI i (w przyszłości) SDK.</summary>
public abstract record AgentEvent
{
    /// <summary>Id sesji Copilot — pozwala wznowić ją w terminalu (copilot --resume=id).</summary>
    public sealed record SessionStarted(string SessionId) : AgentEvent;

    public sealed record MessageDelta(string Text) : AgentEvent;

    public sealed record ToolStarted(string Tool, string? Detail) : AgentEvent;

    public sealed record ToolFinished(string Tool, bool Succeeded) : AgentEvent;

    /// <summary>
    /// Zużycie tokenów. Final = podsumowanie po zakończeniu sesji (Copilot CLI podaje tokeny dopiero na końcu) —
    /// wtedy nie ma już czego przerywać, więc przekroczony budżet kończy się ostrzeżeniem, a nie błędem.
    /// </summary>
    public sealed record Usage(TokenUsage Tokens, bool Final = false) : AgentEvent;

    public sealed record Completed(string Summary) : AgentEvent;

    public sealed record Failed(string Error) : AgentEvent;
}

/// <summary>Port: uruchamia agenta i oddaje strumień zdarzeń. Silnik flow i UI znają tylko ten interfejs.</summary>
public interface IAgentRunner
{
    IAsyncEnumerable<AgentEvent> RunAsync(AgentRequest request, CancellationToken ct);
}
