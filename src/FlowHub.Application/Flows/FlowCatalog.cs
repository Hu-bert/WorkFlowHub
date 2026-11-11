using FlowHub.Domain.Flows;
using Microsoft.Extensions.Logging;

namespace FlowHub.Application.Flows;

/// <summary>Problem w pliku flow — pokazywany w UI i logowany przy starcie.</summary>
public sealed record FlowProblem(string FileName, IReadOnlyList<string> Errors);

/// <summary>Poprawne flow w pamięci + lista problemów. Niepoprawny plik nie blokuje pozostałych.</summary>
public sealed class FlowCatalog(IFlowStore store, ILogger<FlowCatalog> logger)
{
    private sealed record Snapshot(IReadOnlyList<FlowDefinition> Flows, IReadOnlyList<FlowProblem> Problems);

    private Snapshot? _snapshot;

    public IReadOnlyList<FlowDefinition> Flows => Current.Flows;
    public IReadOnlyList<FlowProblem> Problems => Current.Problems;

    private Snapshot Current => _snapshot ??= Load();

    public FlowDefinition? Find(string type) =>
        Flows.FirstOrDefault(flow => string.Equals(flow.Type, type, StringComparison.OrdinalIgnoreCase));

    public void Reload() => _snapshot = Load();

    /// <summary>Dokument flow do edycji (dokładnie to, co trafi do YAML).</summary>
    public FlowDocument? FindDocument(string type) => Find(type) is { } flow ? FlowMapper.ToDocument(flow) : null;

    /// <summary>Waliduje i zapisuje flow do pliku YAML. Zwraca błędy; pusta lista = zapisano.</summary>
    public IReadOnlyList<string> Save(FlowDocument document)
    {
        var errors = FlowValidator.Validate(document);

        if (errors.Count == 0)
        {
            var fileName = store.Save(document);
            logger.LogInformation("Zapisano flow {Type} do {File}", document.Type, fileName);
            Reload();
        }

        return errors;
    }

    private Snapshot Load()
    {
        var flows = new List<FlowDefinition>();
        var problems = new List<FlowProblem>();

        foreach (var file in store.LoadAll())
        {
            var errors = ErrorsOf(file, flows);

            if (errors.Count == 0)
            {
                flows.Add(FlowMapper.ToDefinition(file.Document!));
            }
            else
            {
                problems.Add(new FlowProblem(file.FileName, errors));
                logger.LogError("Flow {File} jest niepoprawny: {Errors}", file.FileName, string.Join(" | ", errors));
            }
        }

        logger.LogInformation("Wczytano {Count} flow, problemów: {Problems}", flows.Count, problems.Count);
        return new Snapshot(flows, problems);
    }

    private static IReadOnlyList<string> ErrorsOf(FlowFile file, List<FlowDefinition> loaded)
    {
        if (file.Document is null)
        {
            return [file.ParseError ?? "Nie udało się odczytać pliku."];
        }

        var errors = FlowValidator.Validate(file.Document).ToList();

        if (loaded.Any(flow => string.Equals(flow.Type, file.Document.Type, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add($"Typ '{file.Document.Type}' jest już zdefiniowany w innym pliku.");
        }

        return errors;
    }
}
