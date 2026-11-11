namespace FlowHub.Integrations;

/// <summary>Która implementacja każdej integracji jest aktywna. Domyślnie wszystko Fake.</summary>
public sealed class IntegrationsOptions
{
    public const string SectionName = "Integrations";

    /// <summary>Tracker zadań (nowa Jira).</summary>
    public string TaskSource { get; init; } = IntegrationMode.Fake;

    /// <summary>Pull requesty (TFS / Azure DevOps Server).</summary>
    public string PullRequests { get; init; } = IntegrationMode.Fake;

    /// <summary>Serwer CI (TeamCity).</summary>
    public string Ci { get; init; } = IntegrationMode.Fake;

    /// <summary>Serwer wdrożeń (Octopus).</summary>
    public string Deploy { get; init; } = IntegrationMode.Fake;
}
