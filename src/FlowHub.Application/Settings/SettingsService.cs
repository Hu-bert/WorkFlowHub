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

public enum ConnectionState
{
    /// <summary>Moduł działa na danych przykładowych.</summary>
    Fake,
    Ok,
    Failed,
}

/// <summary>Stan jednego połączenia — do paska statusu i listy w Ustawieniach.</summary>
public sealed record ConnectionStatus(string Id, string Title, string Icon, ConnectionState State, string Message, DateTime? CheckedAt);

/// <summary>Strona „Ustawienia”: lista modułów, zapis wartości, test połączenia.</summary>
public sealed class SettingsService(IEnumerable<ISettingsModule> modules, ISettingsStore settings, ISecretStore secrets, TimeProvider clock)
{
    private const string FakeChoice = "Fake";
    private static readonly TimeSpan TestLifetime = TimeSpan.FromMinutes(5);

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, TestRecord> _lastTests = new();

    private sealed record TestRecord(ConnectionTest Result, DateTime At);
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

        var result = await RunTestAsync(module, ct);
        _lastTests[module.Id] = new TestRecord(result, clock.GetUtcNow().UtcDateTime);
        return result;
    }

    /// <summary>
    /// Stan wszystkich połączeń: atrapa / działa / nie działa. Wynik testu jest pamiętany przez kilka minut,
    /// żeby strona nie odpytywała serwerów przy każdym otwarciu; refresh = sprawdź teraz.
    /// </summary>
    public async Task<IReadOnlyList<ConnectionStatus>> StatusAsync(bool refresh, CancellationToken ct)
    {
        var statuses = await Task.WhenAll(modules.Where(module => module.CanTest).Select(module => StatusAsync(module, refresh, ct)));
        return statuses;
    }

    private async Task<ConnectionStatus> StatusAsync(ISettingsModule module, bool refresh, CancellationToken ct)
    {
        if (UsesOnlyFake(module))
        {
            return new ConnectionStatus(module.Id, module.Title, module.Icon, ConnectionState.Fake, "Atrapa — dane przykładowe.", null);
        }

        var fresh = _lastTests.TryGetValue(module.Id, out var last) && clock.GetUtcNow().UtcDateTime - last.At < TestLifetime;
        if (refresh || !fresh)
        {
            await TestAsync(module.Id, ct);
            last = _lastTests[module.Id];
        }

        return new ConnectionStatus(module.Id, module.Title, module.Icon, last!.Result.Ok ? ConnectionState.Ok : ConnectionState.Failed, last.Result.Message, last.At);
    }

    /// <summary>Moduł działa na atrapie, gdy każde jego pole wyboru „źródło danych” ma wartość Fake.</summary>
    private bool UsesOnlyFake(ISettingsModule module)
    {
        var sources = module.Fields
            .Where(field => field.Kind == SettingKind.Choice && field.Choices?.Any(choice => choice.Value == FakeChoice) == true)
            .ToList();

        return sources.Count > 0 && sources.All(field => string.Equals(settings.Get(field.Key), FakeChoice, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<ConnectionTest> RunTestAsync(ISettingsModule module, CancellationToken ct)
    {
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
