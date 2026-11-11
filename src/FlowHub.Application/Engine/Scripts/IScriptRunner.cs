namespace FlowHub.Application.Engine.Scripts;

public sealed record ScriptRequest(
    string ScriptPath,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment);

/// <summary>Port: uruchamia skrypt i przekazuje jego wyjście linia po linii. Zwraca kod wyjścia.</summary>
public interface IScriptRunner
{
    Task<int> RunAsync(ScriptRequest request, Func<string, Task> onOutputLine, CancellationToken ct);
}
