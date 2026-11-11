namespace FlowHub.Integrations;

/// <summary>Która implementacja każdej integracji jest aktywna. Domyślnie wszystko Fake.</summary>
public sealed class IntegrationsOptions
{
    public const string SectionName = "Integrations";

    public string TaskSource { get; init; } = IntegrationMode.Fake;
    public string PullRequests { get; init; } = IntegrationMode.Fake;
    public string Ci { get; init; } = IntegrationMode.Fake;
    public string Deploy { get; init; } = IntegrationMode.Fake;
    public string Docs { get; init; } = IntegrationMode.Fake;
}
