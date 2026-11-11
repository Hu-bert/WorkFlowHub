using FlowHub.Domain;

namespace FlowHub.Application.Settings;

/// <summary>Pole z aktualną wartością. Sekret nie ma wartości — tylko informację, czy jest ustawiony.</summary>
public sealed record SettingValue(SettingField Field, string? Value, bool HasValue);

public sealed record SettingsModuleView(
    string Id,
    string Title,
    string Icon,
    string Description,
    bool CanTest,
    IReadOnlyList<SettingValue> Fields);

/// <summary>Strona „Ustawienia”: lista modułów, zapis wartości, test połączenia.</summary>
public sealed class SettingsService(IEnumerable<ISettingsModule> modules, ISettingsStore settings, ISecretStore secrets)
{
    public IReadOnlyList<SettingsModuleView> List() => modules.Select(View).ToList();

    /// <summary>Zapisuje pola modułu. Sekret: null = bez zmian, pusty tekst = usuń.</summary>
    public SettingsModuleView Save(string moduleId, IReadOnlyDictionary<string, string?> values)
    {
        var module = Find(moduleId);
        var plain = new Dictionary<string, string?>();

        foreach (var (key, value) in values)
        {
            var field = module.Fields.FirstOrDefault(candidate => candidate.Key == key)
                ?? throw new DomainException($"Moduł '{module.Title}' nie ma pola '{key}'.");

            if (field.Kind == SettingKind.Secret)
            {
                if (value is not null)
                {
                    secrets.Set(key, value);
                }

                continue;
            }

            plain[key] = Validated(field, value?.Trim());
        }

        settings.Save(plain);
        return View(module);
    }

    public async Task<ConnectionTest> TestAsync(string moduleId, CancellationToken ct)
    {
        var module = Find(moduleId);
        if (!module.CanTest)
        {
            return new ConnectionTest(true, "Ten moduł nie ma testu połączenia.");
        }

        try
        {
            return await module.TestAsync(ct);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or InvalidOperationException or IOException or DomainException)
        {
            return new ConnectionTest(false, error.Message);
        }
    }

    private ISettingsModule Find(string moduleId) =>
        modules.FirstOrDefault(module => module.Id == moduleId)
        ?? throw new DomainException($"Nieznany moduł ustawień '{moduleId}'.");

    private SettingsModuleView View(ISettingsModule module) => new(
        module.Id,
        module.Title,
        module.Icon,
        module.Description,
        module.CanTest,
        module.Fields.Select(Value).ToList());

    private SettingValue Value(SettingField field) => field.Kind == SettingKind.Secret
        ? new SettingValue(field, null, secrets.Has(field.Key))
        : new SettingValue(field, settings.Get(field.Key), !string.IsNullOrEmpty(settings.Get(field.Key)));

    private static string? Validated(SettingField field, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return field.Kind switch
        {
            SettingKind.Choice when field.Choices?.Any(choice => choice.Value == value) != true =>
                throw new DomainException($"{field.Label}: niedozwolona wartość '{value}'."),
            SettingKind.Number when !long.TryParse(value, out var number) || number < 0 =>
                throw new DomainException($"{field.Label}: podaj liczbę całkowitą nie mniejszą od zera."),
            SettingKind.Folder when !Directory.Exists(value) =>
                throw new DomainException($"{field.Label}: katalog '{value}' nie istnieje."),
            _ => value,
        };
    }
}
