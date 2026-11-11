using System.Text;
using System.Text.RegularExpressions;
using FlowHub.Application.Agents;
using FlowHub.Domain;
using Microsoft.Extensions.Options;

namespace FlowHub.Application.Blocks;

public sealed record GenerateBlockRequest(string Kind, string Name, string Description, string? ExamplePath);

/// <summary>Szkic nowego bloczka — NIE jest zapisany; użytkownik ogląda go w edytorze i sam zapisuje.</summary>
public sealed record BlockDraft(string Path, string Content, string ExamplePath, bool Demo);

/// <summary>
/// Generator bloczków: użytkownik opisuje, czego oczekuje, a agent pisze nowy plik analogiczny do
/// istniejącego bloczka tego samego rodzaju (ta sama struktura, nagłówek, język, uniwersalność).
/// Agent nie dostaje żadnych narzędzi i pracuje w pustym katalogu — zwraca tylko tekst.
/// </summary>
public sealed partial class BlockGenerator(IBlockLibrary blocks, IAgentRunner runner, ModelRouter models, IOptions<AgentOptions> agent)
{
    private const int MaxDescriptionLength = 4000;

    /// <summary>Rodzaj bloczka → wzorzec ścieżki nowego pliku.</summary>
    private static readonly Dictionary<string, string> PathPatterns = new()
    {
        ["agents"] = "agents/{0}.agent.md",
        ["skills"] = "skills/{0}/SKILL.md",
        ["prompts"] = "prompts/{0}.md",
        ["scripts"] = "scripts/{0}.ps1",
    };

    public async Task<BlockDraft> GenerateAsync(GenerateBlockRequest request, CancellationToken ct)
    {
        var path = TargetPath(request);
        var description = request.Description?.Trim() ?? "";

        if (description.Length is 0 or > MaxDescriptionLength)
        {
            throw new DomainException($"Opisz, co bloczek ma robić (do {MaxDescriptionLength} znaków).");
        }

        var examplePath = ExamplePath(request);
        var example = blocks.ReadText(examplePath)
            ?? throw new DomainException($"Nie ma bloczka wzorcowego blocks/{examplePath}.");

        if (string.Equals(agent.Value.Runner, AgentRunners.Fake, StringComparison.OrdinalIgnoreCase))
        {
            return new BlockDraft(path, DemoDraft(request, description, example), examplePath, Demo: true);
        }

        var content = await AskAgentAsync(Prompt(request, path, description, examplePath, example), ct);
        return new BlockDraft(path, WithoutCodeFence(content), examplePath, Demo: false);
    }

    private string TargetPath(GenerateBlockRequest request)
    {
        if (!PathPatterns.TryGetValue(request.Kind ?? "", out var pattern))
        {
            throw new DomainException($"Nieznany rodzaj bloczka '{request.Kind}'. Dozwolone: {string.Join(", ", PathPatterns.Keys)}.");
        }

        var name = request.Name?.Trim() ?? "";
        if (!NamePattern().IsMatch(name))
        {
            throw new DomainException("Nazwa bloczka: małe litery, cyfry i myślniki, np. przeglad-bezpieczenstwa.");
        }

        var path = string.Format(pattern, name);
        return blocks.ReadText(path) is null
            ? path
            : throw new DomainException($"Bloczek blocks/{path} już istnieje — wybierz inną nazwę albo popraw istniejący w edytorze.");
    }

    /// <summary>Wzorzec wskazany przez użytkownika (musi być tego samego rodzaju) albo pierwszy z biblioteki.</summary>
    private string ExamplePath(GenerateBlockRequest request)
    {
        var sameKind = blocks.List().Where(block => block.Kind == request.Kind).ToList();

        if (string.IsNullOrWhiteSpace(request.ExamplePath))
        {
            return sameKind.FirstOrDefault()?.Path
                ?? throw new DomainException($"W bibliotece nie ma jeszcze bloczka rodzaju '{request.Kind}', na którym można się wzorować.");
        }

        return sameKind.FirstOrDefault(block => block.Path == request.ExamplePath)?.Path
            ?? throw new DomainException("Wzorzec musi być istniejącym bloczkiem tego samego rodzaju.");
    }

    private static string Prompt(GenerateBlockRequest request, string path, string description, string examplePath, string example) =>
        $"""
        Napisz nowy bloczek biblioteki FlowHub: plik `blocks/{path}` (rodzaj: {request.Kind}, nazwa: {request.Name}).

        Oczekiwania użytkownika:
        {description}

        Zasady:
        - Zachowaj dokładnie strukturę i format pliku wzorcowego (nagłówek/frontmatter, sekcje, styl).
        - Pisz po polsku, krótko i konkretnie.
        - Bloczek ma być uniwersalny — będzie kopiowany do różnych repozytoriów, więc bez założeń o konkretnym repo.
        - Żadnych sekretów, haseł ani adresów firmowych.
        - Nie używaj narzędzi i niczego nie zapisuj. Odpowiedz WYŁĄCZNIE treścią nowego pliku, bez komentarza i bez bloku kodu.

        Plik wzorcowy `blocks/{examplePath}`:
        ---8<---
        {example}
        ---8<---
        """;

    private async Task<string> AskAgentAsync(string prompt, CancellationToken ct)
    {
        // Pusty katalog tymczasowy: agent nie ma czego czytać ani zmieniać.
        var directory = Directory.CreateTempSubdirectory("flowhub-generator-").FullName;

        try
        {
            var request = new AgentRequest
            {
                Prompt = prompt,
                Model = models.Resolve(null),
                WorkingDirectory = directory,
                AskPermission = (_, _, _) => Task.FromResult(false),
            };

            await foreach (var agentEvent in runner.RunAsync(request, ct))
            {
                switch (agentEvent)
                {
                    case AgentEvent.Completed completed when !string.IsNullOrWhiteSpace(completed.Summary):
                        return completed.Summary;

                    case AgentEvent.Failed failed:
                        throw new DomainException($"Agent nie wygenerował bloczka: {failed.Error}");
                }
            }

            throw new DomainException("Agent zakończył pracę bez treści bloczka.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Modele lubią owijać odpowiedź w ```…``` — zdejmujemy taką ramkę.</summary>
    public static string WithoutCodeFence(string content)
    {
        var text = content.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal) || !text.EndsWith("```", StringComparison.Ordinal))
        {
            return text + Environment.NewLine;
        }

        var firstLineEnd = text.IndexOf('\n');
        return firstLineEnd < 0 ? text : text[(firstLineEnd + 1)..^3].TrimEnd() + Environment.NewLine;
    }

    /// <summary>Atrapa: kopia wzorca z podmienioną nazwą i opisem — żeby dało się obejrzeć cały obieg bez Copilota.</summary>
    private static string DemoDraft(GenerateBlockRequest request, string description, string example)
    {
        var oneLine = WhiteSpace().Replace(description, " ");
        var draft = new StringBuilder();
        var comment = request.Kind == "scripts" ? "# " : "> ";

        foreach (var line in example.ReplaceLineEndings("\n").Split('\n'))
        {
            draft.AppendLine(line switch
            {
                _ when line.StartsWith("name:", StringComparison.Ordinal) => $"name: {request.Name}",
                _ when line.StartsWith("description:", StringComparison.Ordinal) => $"description: {oneLine}",
                _ => line,
            });
        }

        return $"{draft.ToString().TrimEnd()}{Environment.NewLine}{Environment.NewLine}"
            + $"{comment}SZKIC DEMONSTRACYJNY (atrapa agenta): to kopia wzorca z nową nazwą. Włącz Copilot CLI w Ustawieniach, żeby treść napisał model.{Environment.NewLine}"
            + $"{comment}Oczekiwania: {oneLine}{Environment.NewLine}";
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();
}
