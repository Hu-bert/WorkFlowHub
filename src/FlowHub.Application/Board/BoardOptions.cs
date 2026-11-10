namespace FlowHub.Application.Board;

/// <summary>Kolumny tablicy kanban (appsettings.json → "Board"). Pierwsza kolumna = nowe zadania.</summary>
public sealed class BoardOptions
{
    public const string SectionName = "Board";

    public List<BoardColumn> Columns { get; init; } = [];

    public string DefaultColumn => Columns.Count > 0
        ? Columns[0].Key
        : throw new InvalidOperationException("Brak kolumn tablicy w konfiguracji (Board:Columns).");

    public bool HasColumn(string key) => Columns.Any(column => column.Key == key);
}

public sealed class BoardColumn
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string Icon { get; init; } = "";
}
