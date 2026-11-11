using System.Text.Json;
using FlowHub.Application.Agents;
using FlowHub.Domain.WorkItems;

namespace FlowHub.Infrastructure.Agents;

/// <summary>
/// Zamienia linie JSONL z `copilot --output-format json` na zdarzenia agenta.
///
/// UWAGA: schemat zdarzeń przyjęty na podstawie nazw z dokumentacji Copilot SDK
/// (assistant.message_delta, tool.execution_start, …) — NIE zweryfikowany na prawdziwym CLI.
/// Parser jest tolerancyjny: nieznane zdarzenia pomija, a tekst spoza JSON pokazuje w konsoli.
/// Po pierwszym uruchomieniu w pracy porównaj z docs/copilot-cli.md i popraw nazwy pól tutaj.
/// </summary>
public sealed class CopilotJsonlParser
{
    private bool _sawDelta;

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
            return Map(type, data);
        }
    }

    private AgentEvent? Map(string type, JsonElement data) => type switch
    {
        "assistant.message_delta" => Delta(data),
        "assistant.message" => FullMessage(data),
        "tool.execution_start" => new AgentEvent.ToolStarted(Text(data, "toolName", "name") ?? "tool", Text(data, "arguments", "input")),
        "tool.execution_complete" => new AgentEvent.ToolFinished(Text(data, "toolName", "name") ?? "tool", Flag(data, "success") ?? true),
        "assistant.usage" => new AgentEvent.Usage(Usage(data)),
        "session.error" => new AgentEvent.Failed(Text(data, "message", "error") ?? "Błąd sesji Copilot."),
        _ => null,
    };

    private AgentEvent? Delta(JsonElement data)
    {
        var text = Text(data, "deltaContent", "delta", "content");
        if (text is null)
        {
            return null;
        }

        _sawDelta = true;
        LastMessage += text;
        return new AgentEvent.MessageDelta(text);
    }

    /// <summary>Pełna wiadomość: gdy wcześniej szły delty, tekst jest już w konsoli — nie dublujemy go.</summary>
    private AgentEvent? FullMessage(JsonElement data)
    {
        var text = Text(data, "content", "text");
        if (text is null)
        {
            return null;
        }

        var alreadyShown = _sawDelta;
        _sawDelta = false;
        LastMessage = text;
        return alreadyShown ? null : new AgentEvent.MessageDelta(text + Environment.NewLine);
    }

    private static TokenUsage Usage(JsonElement data) => new()
    {
        Model = Text(data, "model"),
        InputTokens = Number(data, "inputTokens", "input_tokens"),
        OutputTokens = Number(data, "outputTokens", "output_tokens"),
        CacheReadTokens = Number(data, "cacheReadTokens", "cache_read_tokens"),
        CacheWriteTokens = Number(data, "cacheWriteTokens", "cache_write_tokens"),
        Cost = (decimal)(Decimal(data, "cost") ?? 0),
    };

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

    private static double? Decimal(JsonElement element, params string[] names) =>
        Property(element, names) is { ValueKind: JsonValueKind.Number } value ? value.GetDouble() : null;

    private static bool? Flag(JsonElement element, params string[] names) =>
        Property(element, names) is { ValueKind: JsonValueKind.True or JsonValueKind.False } value ? value.GetBoolean() : null;
}
