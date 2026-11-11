using System.Text;
using FlowHub.Application.Blocks;
using FlowHub.Application.Engine;

namespace FlowHub.Application.Agents;

/// <summary>
/// Buduje prompt kroku Agent z szablonu (blocks/prompts) i kontekstu zadania.
/// Kontekst przekazujemy plikami (wejścia/wyjścia), nie całą rozmową — krótszy prompt, mniej tokenów.
/// </summary>
public sealed class AgentPromptBuilder(IBlockLibrary blocks)
{
    public string Build(StepContext context)
    {
        var template = context.Definition.Prompt is { } path ? blocks.ReadText(path) : null;
        var prompt = new StringBuilder(Fill(string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template, context).Trim());

        AppendFiles(prompt, "Pliki wejściowe (przeczytaj zamiast skanować repo od nowa)", InputFiles(context));
        AppendFiles(prompt, "Pliki wynikowe (utwórz dokładnie te pliki)", OutputFiles(context));

        return prompt.ToString();
    }

    /// <summary>Pełne ścieżki plików, które krok ma utworzyć w katalogu zadania.</summary>
    public IReadOnlyList<string> OutputFiles(StepContext context) =>
        context.Definition.Outputs
            .Select(output => Path.Combine(context.Item.WorkDirectory, RepoName(output, context)))
            .ToList();

    private const string DefaultTemplate =
        """
        Wykonaj krok „{{step.name}}” dla zadania {{task.key}}: {{task.title}}.
        Treść zadania jest w pliku {{task.file}}.
        """;

    private IReadOnlyList<string> InputFiles(StepContext context) =>
        context.Definition.Inputs.SelectMany(input => ResolveInput(RepoName(input, context), context)).ToList();

    /// <summary>templates/… bierzemy z blocks/, resztę (także wzorce z *) z katalogu zadania.</summary>
    private IEnumerable<string> ResolveInput(string input, StepContext context)
    {
        if (input.StartsWith("templates/", StringComparison.OrdinalIgnoreCase))
        {
            return [blocks.FullPath(input)];
        }

        var workDirectory = context.Item.WorkDirectory;

        if (!input.Contains('*'))
        {
            return [Path.Combine(workDirectory, input)];
        }

        return Directory.Exists(workDirectory)
            ? Directory.EnumerateFiles(workDirectory, input).Order(StringComparer.OrdinalIgnoreCase)
            : [];
    }

    private static string RepoName(string pattern, StepContext context) =>
        pattern.Replace("{repo}", context.Step.RepoName ?? "repo");

    private static string Fill(string template, StepContext context)
    {
        var item = context.Item;
        var repo = context.Repo ?? item.Repos.FirstOrDefault();

        var values = new Dictionary<string, string?>
        {
            ["task.key"] = item.Key,
            ["task.title"] = item.Title,
            ["task.type"] = item.Type,
            ["task.file"] = Path.Combine(item.WorkDirectory, "task.md"),
            ["work.dir"] = item.WorkDirectory,
            ["step.name"] = context.Step.Name,
            ["repo.name"] = repo?.RepositoryName,
            ["repo.path"] = repo?.LocalPath,
            ["repo.branch"] = repo?.Branch,
            ["repos"] = string.Join(", ", item.Repos.Select(taskRepo => $"{taskRepo.RepositoryName} ({taskRepo.LocalPath})")),
            ["stats"] = TaskStatistics.For(item, context.Step),
        };

        return values.Aggregate(template, (text, pair) => text.Replace("{{" + pair.Key + "}}", pair.Value ?? ""));
    }

    private static void AppendFiles(StringBuilder prompt, string title, IReadOnlyList<string> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        prompt.AppendLine().AppendLine().AppendLine($"{title}:");
        foreach (var file in files)
        {
            prompt.AppendLine($"- {file}");
        }
    }
}
