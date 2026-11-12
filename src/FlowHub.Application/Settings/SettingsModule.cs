namespace FlowHub.Application.Settings;

public enum SettingKind
{
    Text,

    /// <summary>Token / hasło. Zapisywany zaszyfrowany, nigdy nie wraca do UI.</summary>
    Secret,

    Choice,
    Number,
    Folder,
}

public sealed record SettingChoice(string Value, string Label);

/// <summary>Jedno pole ustawień. Key = ścieżka w konfiguracji, np. "Agent:Runner".</summary>
public sealed record SettingField(
    string Key,
    string Label,
    SettingKind Kind = SettingKind.Text,
    string? Hint = null,
    IReadOnlyList<SettingChoice>? Choices = null,
    bool Suggest = false,
    bool Multiple = false);

/// <summary>Configured = false: nie ma czego testować, bo moduł nie jest jeszcze ustawiony (to nie błąd).</summary>
public sealed record ConnectionTest(bool Ok, string Message, bool Configured = true);

/// <summary>
/// Moduł ustawień = jedna karta na stronie „Ustawienia” (Copilot, TFS, TeamCity, …).
/// Nowe połączenie = nowa klasa z tym interfejsem + jedna linia w DI. UI rysuje pola samo.
/// </summary>
public interface ISettingsModule
{
    string Id { get; }

    string Title { get; }

    string Icon { get; }

    string Description { get; }

    IReadOnlyList<SettingField> Fields { get; }

    /// <summary>Czy moduł ma przycisk „Sprawdź połączenie”.</summary>
    bool CanTest => false;

    Task<ConnectionTest> TestAsync(CancellationToken ct) => Task.FromResult(new ConnectionTest(true, "Brak testu."));

    /// <summary>
    /// Wartości do wyboru dla pola z Suggest = true (np. środowiska z serwera wdrożeń, znane modele).
    /// Pole dalej przyjmuje wartość wpisaną ręcznie — lista jest tylko ułatwieniem.
    /// </summary>
    Task<IReadOnlyList<string>> SuggestAsync(string key, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
}

/// <summary>Port: ustawienia użytkownika (plik poza repo), nakładane na appsettings.json.</summary>
public interface ISettingsStore
{
    /// <summary>Aktualna wartość (z ustawień użytkownika albo domyślna z appsettings.json).</summary>
    string? Get(string key);

    /// <summary>Zapisuje wartości i odświeża konfigurację — bez restartu aplikacji. null = usuń nadpisanie.</summary>
    void Save(IReadOnlyDictionary<string, string?> values);
}

/// <summary>Port: modele Copilota widziane w dotychczasowych sesjach CLI (CLI nie ma polecenia „wypisz modele”).</summary>
public interface IKnownModels
{
    IReadOnlyList<string> All();

    void Remember(IEnumerable<string> models);
}

/// <summary>Port: sekrety (tokeny API). Nigdy w repo, logach ani odpowiedziach API.</summary>
public interface ISecretStore
{
    bool Has(string key);

    string? Get(string key);

    /// <summary>null lub pusty tekst = usuń sekret.</summary>
    void Set(string key, string? value);
}
