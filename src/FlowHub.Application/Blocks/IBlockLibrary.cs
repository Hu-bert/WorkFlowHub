namespace FlowHub.Application.Blocks;

public sealed record BlockFile(string Kind, string Path, string Name);

/// <summary>Port: biblioteka bloczków (katalog blocks/: agenci, skille, prompty, skrypty, szablony).</summary>
public interface IBlockLibrary
{
    /// <summary>Treść pliku z blocks/ albo null, gdy go nie ma. Ścieżka względna, np. prompts/planowanie.md.</summary>
    string? ReadText(string relativePath);

    /// <summary>Pełna ścieżka pliku w blocks/ (plik nie musi istnieć).</summary>
    string FullPath(string relativePath);

    IReadOnlyList<BlockFile> List();

    /// <summary>Zapisuje plik bloczka (nowy albo istniejący). Dozwolone tylko katalogi i rozszerzenia biblioteki.</summary>
    void WriteText(string relativePath, string content);
}
