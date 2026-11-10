using System.Globalization;
using System.Text;
using FlowHub.Domain.Repositories;

namespace FlowHub.Application.Repositories;

public sealed record RepositorySuggestion(string Name, IReadOnlyList<string> MatchedKeywords);

/// <summary>Rozpoznaje repo z treści zadania po słowach kluczowych — bez modelu, 0 tokenów.</summary>
public static class RepositoryMatcher
{
    public static IReadOnlyList<RepositorySuggestion> Suggest(string text, IEnumerable<CodeRepository> repositories)
    {
        var normalizedText = Normalize(text);

        return repositories
            .Select(repository => new RepositorySuggestion(repository.Name, MatchedKeywords(repository, normalizedText)))
            .Where(suggestion => suggestion.MatchedKeywords.Count > 0)
            .OrderByDescending(suggestion => suggestion.MatchedKeywords.Count)
            .ToList();
    }

    private static List<string> MatchedKeywords(CodeRepository repository, string normalizedText) =>
        repository.Keywords
            .Append(repository.Name)
            .Where(keyword => keyword.Length > 1 && normalizedText.Contains(Normalize(keyword)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Małe litery bez polskich znaków, żeby "płatność" pasowała do "platnosc".</summary>
    private static string Normalize(string text)
    {
        var decomposed = text.ToLowerInvariant().Replace('ł', 'l').Normalize(NormalizationForm.FormD);
        var letters = decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        return new string(letters.ToArray());
    }
}
