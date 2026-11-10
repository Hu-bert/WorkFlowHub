using FlowHub.Application.Repositories;
using FlowHub.Domain.Repositories;

namespace FlowHub.Application.Tests;

public sealed class RepositoryMatcherTests
{
    private static readonly CodeRepository[] Repositories =
    [
        new() { Name = "Billing.Api", Url = "x", Keywords = ["faktura", "płatność"] },
        new() { Name = "Customer.Portal", Url = "x", Keywords = ["portal", "logowanie"] },
        new() { Name = "Shared.Lib", Url = "x", Keywords = ["kontrakt"] },
    ];

    [Fact]
    public void Finds_repos_by_keywords_ignoring_case_and_polish_letters()
    {
        var suggestions = RepositoryMatcher.Suggest("Zła PLATNOSC po zmianie na Portal klienta", Repositories);

        Assert.Equal(["Billing.Api", "Customer.Portal"], suggestions.Select(s => s.Name).Order());
    }

    [Fact]
    public void Best_match_comes_first()
    {
        var suggestions = RepositoryMatcher.Suggest("Faktura i płatność nie działają po logowaniu", Repositories);

        Assert.Equal("Billing.Api", suggestions[0].Name);
        Assert.Equal(2, suggestions[0].MatchedKeywords.Count);
    }

    [Fact]
    public void Repo_name_in_the_text_also_matches()
    {
        var suggestion = Assert.Single(RepositoryMatcher.Suggest("Podbij wersję w Shared.Lib", Repositories));

        Assert.Equal("Shared.Lib", suggestion.Name);
    }

    [Fact]
    public void No_keywords_means_no_suggestions()
    {
        Assert.Empty(RepositoryMatcher.Suggest("Zupełnie inny temat", Repositories));
    }
}
