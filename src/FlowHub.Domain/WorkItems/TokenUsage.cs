namespace FlowHub.Domain.WorkItems;

/// <summary>Zużycie tokenów i koszt (suma dla kroku).</summary>
public sealed record TokenUsage
{
    public static readonly TokenUsage Empty = new();

    public string? Model { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long CacheReadTokens { get; init; }
    public long CacheWriteTokens { get; init; }
    public decimal Cost { get; init; }

    /// <summary>Tokeny liczone do budżetu: wejście + wyjście.</summary>
    public long TotalTokens => InputTokens + OutputTokens;

    public TokenUsage Add(TokenUsage other) => new()
    {
        Model = other.Model ?? Model,
        InputTokens = InputTokens + other.InputTokens,
        OutputTokens = OutputTokens + other.OutputTokens,
        CacheReadTokens = CacheReadTokens + other.CacheReadTokens,
        CacheWriteTokens = CacheWriteTokens + other.CacheWriteTokens,
        Cost = Cost + other.Cost,
    };
}
