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

    public void WriteText(string relativePath, string content)
    {
        var path = FullPath(relativePath);
        var kind = relativePath.Replace('\\', '/').Split('/')[0];
        var extension = Path.GetExtension(path).ToLowerInvariant();

        if (Kinds.All(known => known.Kind != kind) || extension is not (".md" or ".ps1"))
        {
            throw new DomainException(
                $"Bloczek musi być plikiem .md lub .ps1 w jednym z katalogów: {string.Join(", ", Kinds.Select(known => known.Kind))}.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Skrypty z BOM — inaczej Windows PowerShell 5.1 zepsuje polskie znaki.
        File.WriteAllText(path, content, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: extension == ".ps1"));
    }

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
