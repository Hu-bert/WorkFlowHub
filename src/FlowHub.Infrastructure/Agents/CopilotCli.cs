using System.Diagnostics;
using System.Text;

namespace FlowHub.Infrastructure.Agents;

/// <summary>Wspólne dla runnera, terminala i testu połączenia: gdzie jest Copilot CLI i jak go uruchomić.</summary>
public static class CopilotCli
{
    /// <summary>
    /// Pełna ścieżka polecenia. Sama nazwa („copilot”) jest szukana w PATH, a potem w domyślnym
    /// katalogu instalatora (%LOCALAPPDATA%\GitHubCopilotCLI) — PATH bywa nieodświeżony tuż po instalacji.
    /// </summary>
    public static string Resolve(string configured)
    {
        if (configured.Contains(Path.DirectorySeparatorChar) || configured.Contains(Path.AltDirectorySeparatorChar))
        {
            return configured;
        }

        return Candidates(configured).FirstOrDefault(File.Exists) ?? configured;
    }

    public static ProcessStartInfo StartInfo(string configuredPath, string workingDirectory, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(Resolve(configuredPath))
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        return info;
    }

    private static IEnumerable<string> Candidates(string name)
    {
        string[] extensions = OperatingSystem.IsWindows() ? [".exe", ".cmd", ".bat"] : [""];
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitHubCopilotCLI"));

        return directories.SelectMany(directory => extensions.Select(extension => Path.Combine(directory, name + extension)));
    }
}
