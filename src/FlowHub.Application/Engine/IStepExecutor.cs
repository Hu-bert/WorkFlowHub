using FlowHub.Domain;
using FlowHub.Domain.Flows;

namespace FlowHub.Application.Engine;

/// <summary>Wyjście kroku na żywo (konsola w UI + log kroku).</summary>
public interface IStepLog
{
    Task WriteAsync(string text, CancellationToken ct = default);
}

/// <summary>
/// Wykonawca jednego rodzaju kroku. Nowy rodzaj kroku = nowa klasa + rejestracja w DI,
/// silnik flow się nie zmienia.
/// </summary>
public interface IStepExecutor
{
    StepKind Kind { get; }

    /// <summary>Tekst propozycji pokazywany użytkownikowi przed uruchomieniem (edytowalny).</summary>
    Task<string> ProposeAsync(StepContext context, CancellationToken ct);

    Task<StepOutcome> ExecuteAsync(StepContext context, IStepLog log, CancellationToken ct);
}

public sealed class StepExecutorRegistry(IEnumerable<IStepExecutor> executors)
{
    private readonly Dictionary<StepKind, IStepExecutor> _byKind = executors.ToDictionary(executor => executor.Kind);

    public IStepExecutor For(StepKind kind) =>
        _byKind.GetValueOrDefault(kind)
        ?? throw new DomainException($"Kroki typu {kind} nie są jeszcze obsługiwane.");
}
