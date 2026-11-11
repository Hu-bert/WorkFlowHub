using FlowHub.Application.Agents;
using FlowHub.Domain;
using FlowHub.Domain.WorkItems;
using Microsoft.Extensions.Options;

namespace FlowHub.Application.Tests.Agents;

public sealed class ModelRouterTests
{
    private static readonly ModelRouter Router = new(Options.Create(new ModelOptions
    {
        DefaultTier = "cheap",
        Tiers = { ["cheap"] = "model-tani", ["premium"] = "model-drogi" },
        Prices =
        {
            ["model-tani"] = new ModelPrice { InputPerMillion = 0.5m, OutputPerMillion = 2m, CacheReadPerMillion = 0.05m },
        },
    }));

    [Theory]
    [InlineData("tier:cheap", "model-tani")]
    [InlineData("TIER:Premium", "model-drogi")]
    [InlineData("gpt-x", "gpt-x")]
    [InlineData(null, "model-tani")]
    [InlineData("  ", "model-tani")]
    public void Resolves_tiers_keeps_explicit_models_and_defaults_to_the_cheapest(string? model, string expected)
    {
        Assert.Equal(expected, Router.Resolve(model));
    }

    [Fact]
    public void Unknown_tier_lists_the_configured_ones()
    {
        var error = Assert.Throws<DomainException>(() => Router.Resolve("tier:mega"));

        Assert.Contains("cheap, premium", error.Message);
    }

    [Fact]
    public void Prices_usage_from_the_table_per_million_tokens()
    {
        var priced = Router.Price(new TokenUsage { Model = "model-tani", InputTokens = 2_000_000, OutputTokens = 500_000, CacheReadTokens = 1_000_000 });

        Assert.Equal(1m + 1m + 0.05m, priced.Cost);
    }

    [Fact]
    public void Keeps_the_cost_reported_by_the_agent_and_skips_unknown_models()
    {
        var reported = Router.Price(new TokenUsage { Model = "model-tani", InputTokens = 1_000_000, Cost = 9m });
        var unknown = Router.Price(new TokenUsage { Model = "nieznany", InputTokens = 1_000_000 });

        Assert.Equal(9m, reported.Cost);
        Assert.Equal(0m, unknown.Cost);
    }

    [Theory]
    [InlineData(0, 1000, BudgetState.Ok)]
    [InlineData(799, 1000, BudgetState.Ok)]
    [InlineData(800, 1000, BudgetState.Warning)]
    [InlineData(1000, 1000, BudgetState.Exceeded)]
    [InlineData(5000, 0, BudgetState.Ok)]
    public void Budget_warns_at_80_percent_and_is_exceeded_at_100(long used, long limit, BudgetState expected)
    {
        Assert.Equal(expected, TokenBudget.StateOf(used, limit));
    }
}
