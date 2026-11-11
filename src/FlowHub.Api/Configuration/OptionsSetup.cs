using System.Text.Json.Serialization;
using FlowHub.Application.Agents;
using FlowHub.Application.Board;
using FlowHub.Application.Integrations;
using FlowHub.Application.Repositories;
using FlowHub.Application.Workspace;
using FlowHub.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FlowHub.Api.Configuration;

public static class OptionsSetup
{
    /// <summary>Wiąże sekcje appsettings.json z opcjami i ustawia format JSON (enumy jako tekst).</summary>
    public static WebApplicationBuilder AddFlowHubOptions(this WebApplicationBuilder builder)
    {
        var contentRoot = builder.Environment.ContentRootPath;

        // Ustawienia ze strony „Ustawienia” (plik poza repo) nakładają się na appsettings.json.
        builder.Configuration.AddJsonFile(UserSettings.SettingsFile(builder.Configuration), optional: true, reloadOnChange: false);

        // IOptions<T> czyta wartości na bieżąco — zmiana ustawień działa bez restartu.
        builder.Services.Replace(ServiceDescriptor.Singleton(typeof(IOptions<>), typeof(LiveOptions<>)));

        builder.Services.Configure<BoardOptions>(builder.Configuration.GetSection(BoardOptions.SectionName));
        builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
        builder.Services.Configure<RepoStatusOptions>(builder.Configuration.GetSection(RepoStatusOptions.SectionName));
        builder.Services.Configure<ModelOptions>(builder.Configuration.GetSection(ModelOptions.SectionName));
        builder.Services.Configure<WorkspaceOptions>(builder.Configuration.GetSection(WorkspaceOptions.SectionName));
        builder.Services.Configure<LocalRepositoriesOptions>(builder.Configuration.GetSection(LocalRepositoriesOptions.SectionName));

        // Ścieżki względne w konfiguracji liczymy od katalogu aplikacji, nie od bieżącego katalogu procesu.
        builder.Services.PostConfigure<WorkspaceOptions>(options =>
        {
            options.Root = Path.GetFullPath(options.Root, contentRoot);
            options.WorkDirectory = string.IsNullOrWhiteSpace(options.WorkDirectory)
                ? null
                : Path.GetFullPath(options.WorkDirectory, contentRoot);
        });

        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        return builder;
    }

    private sealed class LiveOptions<T>(IOptionsMonitor<T> monitor) : IOptions<T>
        where T : class
    {
        public T Value => monitor.CurrentValue;
    }
}
