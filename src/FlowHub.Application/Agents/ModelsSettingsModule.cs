using FlowHub.Application.Settings;

namespace FlowHub.Application.Agents;

/// <summary>Ustawienia: plan Copilota (z limitem / bez limitu) i routing modeli.</summary>
public sealed class ModelsSettingsModule(IKnownModels known) : ISettingsModule
{
    public string Id => "models";

    public string Title => "Modele i plan Copilot";

    public string Icon => "mdi-brain";

    public string Description =>
        "Plan bez limitu (np. Copilot Enterprise) wyłącza budżety tokenów i wycenę. Poziomy to modele używane przez kroki flow (tier:cheap / standard / premium); „auto” = wybór Copilota.";

    public IReadOnlyList<SettingField> Fields { get; } =
    [
        new("Models:Plan", "Plan", SettingKind.Choice, "Bez limitu: brak ostrzeżeń i zatrzymań na budżecie, koszt nie jest liczony.",
        [
            new(ModelOptions.MeteredPlan, "Z limitem — pilnuj budżetów i kosztu"),
            new(ModelOptions.UnlimitedPlan, "Bez limitu (Enterprise) — tylko licz tokeny"),
        ]),
        new("Models:DefaultTier", "Poziom domyślny", SettingKind.Choice, "Używany, gdy krok nie podaje modelu.",
        [
            new("cheap", "cheap"),
            new("standard", "standard"),
            new("premium", "premium"),
        ]),
        new("Models:Tiers:cheap", "Model: cheap", Hint: "auto = wybór Copilota. Lista podpowiada modele widziane w dotychczasowych sesjach CLI; inne id wpisz ręcznie (zobaczysz je w terminalu: /model).", Suggest: true),
        new("Models:Tiers:standard", "Model: standard", Suggest: true),
        new("Models:Tiers:premium", "Model: premium", Suggest: true),
        new("Models:TaskTokenBudget", "Budżet tokenów na zadanie", SettingKind.Number, "0 = bez limitu. Pomijany w planie bez limitu."),
    ];

    /// <summary>Copilot CLI nie umie wypisać modeli poza sesją — podpowiadamy „auto” i modele, które już widzieliśmy.</summary>
    public Task<IReadOnlyList<string>> SuggestAsync(string key, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(["auto", .. known.All().Where(model => model != "auto")]);
}
