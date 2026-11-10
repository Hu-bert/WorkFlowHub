using System.Globalization;
using System.Text;

namespace FlowHub.Domain.WorkItems;

/// <summary>Nazwy katalogu i brancha zadania budowane z klucza i tytułu.</summary>
public static class WorkItemNaming
{
    private const int MaxSlugLength = 40;

    public static string FolderName(string key, string title) => $"{key}-{Slug(title)}".TrimEnd('-');

    public static string BranchName(string key, string title) => $"feature/{FolderName(key, title)}";

    public static string Slug(string text)
    {
        var slug = new StringBuilder();

        foreach (var c in RemoveDiacritics(text).ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        var result = slug.ToString().Trim('-');
        return result.Length <= MaxSlugLength ? result : result[..MaxSlugLength].TrimEnd('-');
    }

    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Replace('ł', 'l').Replace('Ł', 'L').Normalize(NormalizationForm.FormD);
        var letters = decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        return new string(letters.ToArray());
    }
}
