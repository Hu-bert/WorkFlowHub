using System.Text.Json;
using FlowHub.Application.Settings;
using Microsoft.Extensions.Configuration;

namespace FlowHub.Infrastructure.Settings;

/// <summary>
/// Modele Copilota widziane w sesjach CLI, zapisane w katalogu ustawień (models.json).
/// Copilot CLI nie ma polecenia „wypisz modele”, więc lista rośnie z każdą prawdziwą sesją.
/// </summary>
public sealed class KnownModels(IConfiguration configuration) : IKnownModels
{
    private static readonly Lock Gate = new();

    private string File => Path.Combine(UserSettings.Directory(configuration), "models.json");

    public IReadOnlyList<string> All()
    {
        lock (Gate)
        {
            return Load();
        }
    }

    public void Remember(IEnumerable<string> models)
    {
        lock (Gate)
        {
            var known = Load();
            var merged = known.Concat(models.Where(model => !string.IsNullOrWhiteSpace(model)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (merged.Count == known.Count)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(File)!);
            System.IO.File.WriteAllText(File, JsonSerializer.Serialize(merged));
        }
    }

    private List<string> Load()
    {
        try
        {
            return System.IO.File.Exists(File) ? JsonSerializer.Deserialize<List<string>>(System.IO.File.ReadAllText(File)) ?? [] : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}