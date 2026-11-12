using System.Text.Json;
using FlowHub.Application.Agents;
using FlowHub.Domain.WorkItems;

namespace FlowHub.Infrastructure.Agents;

/// <summary>
/// Zamienia linie JSONL z `copilot -p … --output-format json` na zdarzenia agenta.
///
/// Schemat sprawdzony na Copilot CLI 1.0.109 (2026-11-11): każda linia to
/// {"type": "...", "data": {...}}. Tokenów NIE ma w strumieniu — CLI zapisuje je na końcu
/// do pliku z --usage-output-file (patrz <see cref="ParseUsageFile"/>).
/// Parser jest tolerancyjny: nieznane zdarzenia pomija, a tekst spoza JSON pokazuje w konsoli.
/// </summary>
public sealed class CopilotJsonlParser
{
    private const int ToolDetailMaxLength = 300;

    private readonly Dictionary<string, string> _toolNames = [];
    private bool _sawDelta;

    /// <summary>Modele, które CLI wymieniło w tej sesji (wybrany oraz dostępne dla trybu auto) — do podpowiedzi w Ustawieniach.</summary>
    public HashSet<string> SeenModels { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ostatnia pełna wypowiedź agenta — używana jako podsumowanie kroku.</summary>
    public string LastMessage { get; private set; } = "";

    public AgentEvent? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        if (!TryParseJson(line, out var root))
        {
            return new AgentEvent.MessageDelta(line + Environment.NewLine);
        }

        using (root)
        {
            var type = Text(root.RootElement, "type") ?? "";
            var data = root.RootElement.TryGetProperty("data", out var nested) ? nested : root.RootElement;
            CollectModels(type, data);
            return Map(type, data);
        }
    }

    /// <summary>
    /// Plik z --usage-output-file → zużycie per model. `usage.inputTokens` to całe wejście wszystkich wywołań,
    /// razem z odczytami z cache. Do budżetu liczymy wejście „świeże” (bez odczytów z cache, które są tanie).
    /// </summary>
    public static IReadOnlyList<TokenUsage> ParseUsageFile(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("modelMetrics", out var models) || models.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var result = new List<TokenUsage>();
        foreach (var model in models.EnumerateObject())
        {
            if (!model.Value.TryGetProperty("usage", out var usage))
            {
                continue;
            }

            var cacheRead = Number(usage, "cacheReadTokens");
            var cacheWrite = Number(usage, "cacheWriteTokens");

            result.Add(new TokenUsage
            {
                Model = model.Name,
                InputTokens = Math.Max(0, Number(usage, "inputTokens") - cacheRead),
                OutputTokens = Number(usage, "outputTokens"),
                CacheReadTokens = cacheRead,
                CacheWriteTokens = cacheWrite,
            });
        }

        return result;
    }

    /// <summary>session.auto_mode_resolved: chosenModel + availableModels; session.model_change: newModel.</summary>
    private void CollectModels(string type, JsonElement data)
    {
        if (type is not ("session.auto_mode_resolved" or "session.model_change"))
        {
            return;
        }

        foreach (var name in new[] { Text(data, "chosenModel"), Text(data, "newModel") }.OfType<string>())
        {
            SeenModels.Add(name);
        }

        if (data.TryGetProperty("availableModels", out var available) && available.ValueKind == JsonValueKind.Array)
        {
            foreach (var model in available.EnumerateArray().Where(model => model.ValueKind == JsonValueKind.String))
            {
                SeenModels.Add(model.GetString()!);
            }
        }
    }

    private AgentEvent? Map(string type, JsonElement data) => type switch
    {
        "session.start" => Text(data, "sessionId") is { } id ? new AgentEvent.SessionStarted(id) : null,
        "assistant.message_delta" => Delta(data),
        "assistant.message" => FullMessage(data),
        "tool.execution_start" => ToolStarted(data),
        "tool.execution_complete" => ToolFinished(data),
        "session.error" => new AgentEvent.Failed(Text(data, "message", "error") ?? "Błąd sesji Copilot."),
        _ => null,
    };

    private AgentEvent? Delta(JsonElement data)
    {
        var text = Text(data, "deltaContent");
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        _sawDelta = true;
        return new AgentEvent.MessageDelta(text);
    }

    /// <summary>
    /// Pełna wiadomość. Gdy wcześniej szły delty, tekst jest już w konsoli — domykamy tylko linię.
    /// Wiadomość z samym wywołaniem narzędzia ma pustą treść i jest pomijana.
    /// </summary>
    private AgentEvent? FullMessage(JsonElement data)
    {
        var text = Text(data, "content");
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var alreadyShown = _sawDelta;
        _sawDelta = false;
        LastMessage = text;
        return new AgentEvent.MessageDelta(alreadyShown ? Environment.NewLine : text + Environment.NewLine);
    }

    private AgentEvent ToolStarted(JsonElement data)
    {
        var name = Text(data, "toolName") ?? "tool";
        if (Text(data, "toolCallId") is { } id)
        {
            _toolNames[id] = name;
        }

        var detail = Text(data, "arguments");
        return new AgentEvent.ToolStarted(name, detail?.Length > ToolDetailMaxLength ? detail[..ToolDetailMaxLength] + "…" : detail);
    }

    /// <summary>Zakończenie narzędzia nie niesie nazwy — bierzemy ją z wcześniejszego startu po toolCallId.</summary>
    private AgentEvent ToolFinished(JsonElement data)
    {
        var name = Text(data, "toolCallId") is { } id && _toolNames.Remove(id, out var known)
            ? known
            : Text(data, "toolName") ?? "tool";

        return new AgentEvent.ToolFinished(name, Flag(data, "success") ?? true);
    }

    private static bool TryParseJson(string line, out JsonDocument document)
    {
        document = null!;
        if (!line.TrimStart().StartsWith('{'))
        {
            return false;
        }

        try
        {
            document = JsonDocument.Parse(line);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonElement? Property(JsonElement element, string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null)
            {
                return value;
            }
        }

        return null;
    }

    private static string? Text(JsonElement element, params string[] names) =>
        Property(element, names) is { } value
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText()
            : null;

    private static long Number(JsonElement element, params string[] names) =>
        Property(element, names) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt64(out var number) ? number : 0;

    private static bool? Flag(JsonElement element, params string[] names) =>
        Property(element, names) is { ValueKind: JsonValueKind.True or JsonValueKind.False } value ? value.GetBoolean() : null;
}
