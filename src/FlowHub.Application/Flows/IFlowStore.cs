namespace FlowHub.Application.Flows;

/// <summary>Plik flow po wczytaniu: dokument albo błąd składni.</summary>
public sealed record FlowFile(string FileName, FlowDocument? Document, string? ParseError);

/// <summary>Port: skąd biorą się definicje flow (pliki YAML w katalogu flows/).</summary>
public interface IFlowStore
{
    IReadOnlyList<FlowFile> LoadAll();

    /// <summary>Zapisuje flow do pliku i zwraca jego nazwę.</summary>
    string Save(FlowDocument document);
}
