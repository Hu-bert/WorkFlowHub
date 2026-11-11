using FlowHub.Application.Integrations;
using FlowHub.Domain.Repositories;
using FlowHub.Integrations.Octopus;
using FlowHub.Integrations.TeamCity;
using Microsoft.Extensions.Options;

namespace FlowHub.Integrations;

/// <summary>
/// Adresy stron repo w TFS, TeamCity i Octopus — składane z adresu repo i adresów serwerów z Ustawień,
/// niezależnie od tego, czy dane pochodzą z atrapy. Brak adresu = brak linku.
/// </summary>
public sealed class RepositoryLinksBuilder(IOptions<TeamCityOptions> teamCity, IOptions<OctopusOptions> octopus) : IRepositoryLinks
{
    public RepositoryLinks For(CodeRepository repository, string? branch)
    {
        var web = WebUrl(repository.Url);

        return new RepositoryLinks(
            web is null ? null : string.IsNullOrEmpty(branch) ? web : $"{web}?version=GB{Uri.EscapeDataString(branch)}",
            web is null ? null : $"{web}/pullrequests",
            Builds(repository, branch),
            Deploy(repository));
    }

    /// <summary>Adres klonu HTTP(S) repo w TFS jest zarazem adresem strony repo.</summary>
    private static string? WebUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? url.TrimEnd('/')
            : null;

    private IReadOnlyList<NamedLink> Builds(CodeRepository repository, string? branch)
    {
        if (string.IsNullOrWhiteSpace(teamCity.Value.BaseUrl))
        {
            return [];
        }

        var baseUrl = teamCity.Value.BaseUrl.Trim().TrimEnd('/');
        var branchQuery = string.IsNullOrEmpty(branch) ? "" : $"&branch={Uri.EscapeDataString(branch)}";

        return repository.CiBuildConfigurations
            .Select(configuration => new NamedLink(configuration, $"{baseUrl}/buildConfiguration/{Uri.EscapeDataString(configuration)}?mode=builds{branchQuery}"))
            .ToList();
    }

    private string? Deploy(CodeRepository repository)
    {
        if (string.IsNullOrWhiteSpace(octopus.Value.BaseUrl))
        {
            return null;
        }

        var space = string.IsNullOrWhiteSpace(octopus.Value.Space) ? "" : $"/{octopus.Value.Space.Trim()}";
        return $"{octopus.Value.BaseUrl.Trim().TrimEnd('/')}/app#{space}/projects/{Uri.EscapeDataString(repository.DeployProject ?? repository.Name)}";
    }
}