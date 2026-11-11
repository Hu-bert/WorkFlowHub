using System.Text.Json;
using System.Text.Json.Nodes;
using FlowHub.Application.Settings;
using Microsoft.Extensions.Configuration;

namespace FlowHub.Infrastructure.Settings;

/// <summary>Gdzie leżą ustawienia użytkownika — poza repo, żeby nic prywatnego nie trafiło do commita.</summary>
public static class UserSettings
{
    public const string DirectoryKey = "Settings:Directory";

    /// <summary>Katalog z konfiguracji ("Settings:Directory") albo %LOCALAPPDATA%\FlowHub.</summary>
    public static string Directory(IConfiguration configuration) =>
        configuration[DirectoryKey] is { Length: > 0 } configured
            ? Path.GetFullPath(configured)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlowHub");

    /// <summary>Plik nakładany na appsettings.json (te same sekcje: Agent, Models, Integrations, …).</summary>
    public static string SettingsFile(IConfiguration configuration) => Path.Combine(Directory(configuration), "settings.json");

    public static string SecretsFile(IConfiguration configuration) => Path.Combine(Directory(configuration), "secrets.json");
}

/// <summary>
/// Ustawienia jako plik JSON w tym samym kształcie co appsettings.json. Po zapisie konfiguracja
/// jest przeładowywana, więc zmiany działają od razu (opcje czytane są na bieżąco).
/// </summary>
public sealed class JsonSettingsStore(IConfiguration configuration) : ISettingsStore
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    private static readonly Lock Gate = new();

    public string? Get(string key) => configuration[key];

    public void Save(IReadOnlyDictionary<string, string?> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        lock (Gate)
        {
            var file = UserSettings.SettingsFile(configuration);
            var root = File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file)) as JsonObject ?? [] : [];

            foreach (var (key, value) in values)
            {
                Set(root, key.Split(':'), value);
            }

            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, root.ToJsonString(Indented));
        }

        (configuration as IConfigurationRoot)?.Reload();
    }

    private static void Set(JsonObject node, string[] path, string? value)
    {
        foreach (var segment in path[..^1])
        {
            if (node[segment] is not JsonObject child)
            {
                child = [];
                node[segment] = child;
            }

            node = child;
        }

        if (value is null)
        {
            node.Remove(path[^1]);
        }
        else
        {
            node[path[^1]] = value;
        }
    }
}
