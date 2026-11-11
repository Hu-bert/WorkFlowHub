namespace FlowHub.Integrations.Fake;

/// <summary>
/// Scenariusze danych testowych — po jednym na repo demonstracyjne, żeby w UI było widać
/// każdy stan: zielony (Shared.Lib), w toku (Billing.Api) i czerwony (Customer.Portal).
/// Dane są statyczne. Nieznane repo dostaje scenariusz „w toku”.
/// </summary>
internal enum FakeScenario
{
    InProgress,
    AllGreen,
    Broken,
}

internal static class FakeScenarios
{
    public const string TfsUrl = "https://tfs.example.local/DefaultCollection/Projekt/_git";
    public const string TeamCityUrl = "https://teamcity.example.local";
    public const string OctopusUrl = "https://octopus.example.local";

    public static FakeScenario For(string repositoryName) => repositoryName switch
    {
        "Shared.Lib" => FakeScenario.AllGreen,
        "Customer.Portal" => FakeScenario.Broken,
        _ => FakeScenario.InProgress,
    };

    /// <summary>Stały numer z nazwy — te same dane przy każdym odświeżeniu.</summary>
    public static int Number(string text, int min, int max) =>
        min + (int)(text.Aggregate(17u, (hash, c) => hash * 31 + c) % (uint)(max - min));
}
