namespace FlowHub.Application.Agents;

/// <summary>Ustawienia agenta (appsettings.json → "Agent").</summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>"Fake" (symulacja, bez Copilota) albo "CopilotCli".</summary>
    public string Runner { get; init; } = "Fake";

    /// <summary>Polecenie Copilot CLI (nazwa z PATH albo pełna ścieżka).</summary>
    public string CliPath { get; init; } = "copilot";

    /// <summary>Fake: przerwa między fragmentami odpowiedzi, żeby było widać strumień na żywo.</summary>
    public int FakeDelayMs { get; init; } = 25;
}
