namespace FlowHub.Domain.Flows;

/// <summary>Typ zadania i jego flow (jeden plik YAML).</summary>
public sealed record FlowDefinition
{
    /// <summary>Klucz typu zadania, np. "Bug".</summary>
    public required string Type { get; init; }

    public required string Name { get; init; }
    public int Version { get; init; } = 1;
    public required IReadOnlyList<StepDefinition> Steps { get; init; }
}
