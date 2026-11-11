using FlowHub.Application.Blocks;
using FlowHub.Application.Workspace;
using FlowHub.Domain;

namespace FlowHub.Infrastructure.Blocks;

/// <summary>Biblioteka bloczków jako pliki w katalogu blocks/.</summary>
public sealed class FileBlockLibrary(WorkspacePaths paths) : IBlockLibrary
{
    private static readonly (string Kind, string Pattern)[] Kinds =
    [
        ("agents", "*.agent.md"),
        ("skills", "SKILL.md"),
        ("prompts", "*.md"),
        ("scripts", "*.ps1"),
        ("templates", "*.md"),
    ];

    private string Root => Path.GetFullPath(paths.BlocksDirectory) + Path.DirectorySeparatorChar;

    public string? ReadText(string relativePath)
    {
        var path = FullPath(relativePath);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public string FullPath(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(Root, relativePath));

        return path.StartsWith(Root, StringComparison.OrdinalIgnoreCase)
            ? path
            : throw new DomainException($"Ścieżka '{relativePath}' wychodzi poza katalog blocks/.");
    }

    public IReadOnlyList<BlockFile> List() => Kinds.SelectMany(FilesOf).ToList();

    private IEnumerable<BlockFile> FilesOf((string Kind, string Pattern) kind)
    {
        var directory = Path.Combine(Root, kind.Kind);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, kind.Pattern, SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(file => new BlockFile(kind.Kind, Path.GetRelativePath(Root, file).Replace('\\', '/'), NameOf(kind.Kind, file)));
    }

    /// <summary>Agent: nazwa pliku bez .agent.md; skill: nazwa katalogu; reszta: nazwa pliku bez rozszerzenia.</summary>
    private static string NameOf(string kind, string file) => kind switch
    {
        "agents" => Path.GetFileName(file).Replace(".agent.md", ""),
        "skills" => new DirectoryInfo(Path.GetDirectoryName(file)!).Name,
        _ => Path.GetFileNameWithoutExtension(file),
    };
}
