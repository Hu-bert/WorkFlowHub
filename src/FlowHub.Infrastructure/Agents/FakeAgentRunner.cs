using System.Runtime.CompilerServices;
using FlowHub.Application.Agents;
using FlowHub.Domain.WorkItems;
using Microsoft.Extensions.Options;

namespace FlowHub.Infrastructure.Agents;

/// <summary>
/// Agent-atrapa: udaje sesję Copilot (strumień tekstu, narzędzia, prośba o zgodę, tokeny)
/// i tworzy oczekiwane pliki. Dzięki niemu cały flow działa bez dostępu do Copilota.
/// </summary>
public sealed class FakeAgentRunner(IOptions<AgentOptions> options) : IAgentRunner
{
    private const string DemoChange = "- Zmiana demonstracyjna FlowHub (FakeAgentRunner).";

    private TimeSpan Delay => TimeSpan.FromMilliseconds(options.Value.FakeDelayMs);

    public async IAsyncEnumerable<AgentEvent> RunAsync(AgentRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        var written = 0;

        await foreach (var delta in SayAsync($"[Fake] agent: {request.Agent ?? "domyślny"}, model: {request.Model ?? "domyślny"}. Czytam zadanie i pliki wejściowe.", ct))
        {
            yield return delta;
        }

        yield return new AgentEvent.ToolStarted("read", "task.md");
        await Task.Delay(Delay, ct);
        yield return new AgentEvent.ToolFinished("read", true);

        if (request.AllowedTools.Contains("write"))
        {
            if (!await request.AskPermission("write", $"Zapis plików w repo: {request.WorkingDirectory}", ct))
            {
                yield return new AgentEvent.Failed("Odmówiono zgody na zapis plików — krok przerwany.");
                yield break;
            }

            yield return new AgentEvent.ToolStarted("write", "README.md");
            written += ChangeRepository(request.WorkingDirectory);
            yield return new AgentEvent.ToolFinished("write", true);
        }

        foreach (var output in request.ExpectedOutputs)
        {
            yield return new AgentEvent.ToolStarted("write", Path.GetFileName(output));
            written += WriteDocument(output);
            await Task.Delay(Delay, ct);
            yield return new AgentEvent.ToolFinished("write", true);
        }

        var summary = Summary(request);
        await foreach (var delta in SayAsync(summary, ct))
        {
            yield return delta;
        }

        yield return new AgentEvent.Usage(Usage(request, written));
        yield return new AgentEvent.Completed(summary);
    }

    /// <summary>Tekst słowo po słowie — tak jak przychodzi z prawdziwego modelu.</summary>
    private async IAsyncEnumerable<AgentEvent> SayAsync(string text, [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var word in text.Split(' '))
        {
            await Task.Delay(Delay, ct);
            yield return new AgentEvent.MessageDelta(word + " ");
        }

        yield return new AgentEvent.MessageDelta(Environment.NewLine);
    }

    private static int WriteDocument(string path)
    {
        var content = FakeDocuments.For(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return content.Length;
    }

    /// <summary>Mała zmiana w repo, żeby krok „realizacja” zostawił prawdziwy diff.</summary>
    private static int ChangeRepository(string repositoryPath)
    {
        if (!Directory.Exists(repositoryPath))
        {
            return 0;
        }

        File.AppendAllText(Path.Combine(repositoryPath, "README.md"), Environment.NewLine + DemoChange + Environment.NewLine);
        return DemoChange.Length;
    }

    private static string Summary(AgentRequest request) =>
        request.ExpectedOutputs.Count == 0
            ? "Krok wykonany (tryb demonstracyjny)."
            : $"Utworzono: {string.Join(", ", request.ExpectedOutputs.Select(Path.GetFileName))} (tryb demonstracyjny).";

    /// <summary>Szacunek: ok. 4 znaki na token + stały narzut promptu systemowego.</summary>
    private static TokenUsage Usage(AgentRequest request, int writtenCharacters) => new()
    {
        Model = request.Model ?? "fake-model",
        InputTokens = 1500 + request.Prompt.Length / 4,
        OutputTokens = 200 + writtenCharacters / 4,
        CacheReadTokens = 1000,
    };
}
