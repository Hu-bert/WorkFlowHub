using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlowHub.Application.Settings;
using Microsoft.Extensions.Configuration;

namespace FlowHub.Infrastructure.Settings;

/// <summary>
/// Sekrety w pliku poza repo, zaszyfrowane DPAPI (Windows, bieżący użytkownik) — pliku nie da się
/// odczytać na innym koncie ani komputerze. Gdy sekretu nie ma w pliku, szukamy go w konfiguracji
/// (user-secrets / zmienne środowiskowe, np. Tfs__Token).
/// </summary>
public sealed class ProtectedSecretStore(IConfiguration configuration) : ISecretStore
{
    private static readonly Lock Gate = new();

    public bool Has(string key) => !string.IsNullOrEmpty(Get(key));

    public string? Get(string key)
    {
        lock (Gate)
        {
            return Load().TryGetValue(key, out var stored) ? Unprotect(stored) : configuration[key];
        }
    }

    public void Set(string key, string? value)
    {
        lock (Gate)
        {
            var secrets = Load();

            if (string.IsNullOrEmpty(value))
            {
                secrets.Remove(key);
            }
            else
            {
                secrets[key] = Protect(value);
            }

            var file = UserSettings.SecretsFile(configuration);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(secrets));
        }
    }

    private Dictionary<string, string> Load()
    {
        var file = UserSettings.SecretsFile(configuration);
        return File.Exists(file)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file)) ?? []
            : [];
    }

    private static string Protect(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);

        // Poza Windows nie ma DPAPI — aplikacja jest windowsowa; tam zostaje samo kodowanie i plik w profilu użytkownika.
        return Convert.ToBase64String(OperatingSystem.IsWindows()
            ? ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser)
            : bytes);
    }

    private static string? Unprotect(string stored)
    {
        try
        {
            var bytes = Convert.FromBase64String(stored);
            return Encoding.UTF8.GetString(OperatingSystem.IsWindows()
                ? ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser)
                : bytes);
        }
        catch (Exception error) when (error is CryptographicException or FormatException)
        {
            // Plik z innego konta / komputera — traktujemy jak brak sekretu.
            return null;
        }
    }
}
