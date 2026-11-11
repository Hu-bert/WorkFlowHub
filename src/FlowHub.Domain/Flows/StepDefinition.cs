namespace FlowHub.Domain.Flows;

/// <summary>Bloczek flow: opis jednego kroku (z YAML).</summary>
public sealed record StepDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required StepKind Kind { get; init; }

    /// <summary>Script: ścieżka skryptu względem blocks/.</summary>
    public string? Script { get; init; }

    /// <summary>Agent: nazwa custom agenta Copilot (blocks/agents).</summary>
    public string? Agent { get; init; }

    /// <summary>Agent: szablon promptu względem blocks/.</summary>
    public string? Prompt { get; init; }

    /// <summary>Check: nazwa sprawdzenia (np. pull-request, build, deployment).</summary>
    public string? Check { get; init; }

    /// <summary>Manual: punkty do odhaczenia.</summary>
    public IReadOnlyList<string> Checklist { get; init; } = [];

    /// <summary>
    /// Manual: id wcześniejszego kroku Agent, do którego można stąd wysłać polecenie „co dalej”
    /// (krok agenta rusza ponownie z tą uwagą, a ten krok czeka na jego wynik).
    /// </summary>
    public string? FollowUp { get; init; }

    /// <summary>Model lub poziom: "tier:cheap", "tier:standard", "tier:premium" albo konkretne id.</summary>
    public string? Model { get; init; }

    public int TokenBudget { get; init; }
    public bool RequiresApproval { get; init; } = true;

    /// <summary>Krok uruchamiany osobno dla każdego repo zadania.</summary>
    public bool PerRepo { get; init; }

    /// <summary>
    /// Agent „per repo”: plik wynikowy jest zapamiętywany dla repo (mapa repo) i w kolejnych zadaniach
    /// agent dostaje go do aktualizacji zamiast pisać od zera.
    /// </summary>
    public bool Remember { get; init; }

    public IReadOnlyList<string> AllowedTools { get; init; } = [];
    public IReadOnlyList<string> Inputs { get; init; } = [];
    public IReadOnlyList<string> Outputs { get; init; } = [];
}
