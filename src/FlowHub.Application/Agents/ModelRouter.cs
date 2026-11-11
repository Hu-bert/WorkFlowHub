using FlowHub.Domain;
using FlowHub.Domain.WorkItems;
using Microsoft.Extensions.Options;

namespace FlowHub.Application.Agents;

/// <summary>Modele i ceny (appsettings.json → "Models").</summary>
public sealed class ModelOptions
{
    public const string SectionName = "Models";

    /// <summary>Poziom używany, gdy krok nie podaje modelu. Domyślnie najtańszy.</summary>
    public string DefaultTier { get; init; } = "cheap";

    /// <summary>Tabela routingu: poziom (cheap / standard / premium) → id modelu.</summary>
    public Dictionary<string, string> Tiers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ceny w USD za milion tokenów, per model.</summary>
    public Dictionary<string, ModelPrice> Prices { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Budżet tokenów na całe zadanie. 0 = bez limitu.</summary>
    public long TaskTokenBudget { get; init; }
}

public sealed class ModelPrice
{
    public decimal InputPerMillion { get; init; }
    public decimal OutputPerMillion { get; init; }
    public decimal CacheReadPerMillion { get; init; }
    public decimal CacheWritePerMillion { get; init; }
}

/// <summary>Zamienia „tier:cheap” na konkretny model i wycenia zużycie tokenów.</summary>
public sealed class ModelRouter(IOptions<ModelOptions> options)
{
    public const string TierPrefix = "tier:";

    private const decimal Million = 1_000_000m;

    private ModelOptions Options => options.Value;

    /// <summary>Id modelu dla wpisu z kroku: „tier:x” → z tabeli, konkretne id → bez zmian, brak → poziom domyślny.</summary>
    public string Resolve(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return FromTier(Options.DefaultTier);
        }

        return model.StartsWith(TierPrefix, StringComparison.OrdinalIgnoreCase)
            ? FromTier(model[TierPrefix.Length..].Trim())
            : model.Trim();
    }

    /// <summary>Uzupełnia koszt, jeśli agent go nie podał, a znamy cenę modelu.</summary>
    public TokenUsage Price(TokenUsage usage)
    {
        if (usage.Cost > 0 || usage.Model is null || !Options.Prices.TryGetValue(usage.Model, out var price))
        {
            return usage;
        }

        var cost = usage.InputTokens * price.InputPerMillion
            + usage.OutputTokens * price.OutputPerMillion
            + usage.CacheReadTokens * price.CacheReadPerMillion
            + usage.CacheWriteTokens * price.CacheWritePerMillion;

        return usage with { Cost = cost / Million };
    }

    private string FromTier(string tier) =>
        Options.Tiers.TryGetValue(tier, out var model)
            ? model
            : throw new DomainException(
                $"Nieznany poziom modelu '{tier}'. Dostępne w konfiguracji (Models:Tiers): {string.Join(", ", Options.Tiers.Keys)}.");
}
