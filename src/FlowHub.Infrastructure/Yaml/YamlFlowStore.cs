using FlowHub.Application.Flows;
using FlowHub.Application.Workspace;
using YamlDotNet.Core;

namespace FlowHub.Infrastructure.Yaml;

/// <summary>Flow jako pliki YAML w katalogu flows/ (jeden plik = jeden typ zadania).</summary>
public sealed class YamlFlowStore(WorkspacePaths paths) : IFlowStore
{
    public IReadOnlyList<FlowFile> LoadAll()
    {
        if (!Directory.Exists(paths.FlowsDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(paths.FlowsDirectory, "*.yaml")
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(Load)
            .ToList();
    }

    public string Save(FlowDocument document)
    {
        Directory.CreateDirectory(paths.FlowsDirectory);
        var fileName = ExistingFileFor(document.Type) ?? $"{document.Type!.ToLowerInvariant()}.yaml";

        File.WriteAllText(Path.Combine(paths.FlowsDirectory, fileName), YamlFormat.Serializer.Serialize(document));
        return fileName;
    }

    private static FlowFile Load(string path)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            var document = YamlFormat.Deserializer.Deserialize<FlowDocument?>(File.ReadAllText(path));
            return new FlowFile(fileName, document, document is null ? "Plik jest pusty." : null);
        }
        catch (YamlException error)
        {
            return new FlowFile(fileName, null, YamlFormat.Describe(error));
        }
    }

    private string? ExistingFileFor(string? type) =>
        LoadAll()
            .FirstOrDefault(file => string.Equals(file.Document?.Type, type, StringComparison.OrdinalIgnoreCase))
            ?.FileName;
}
