using System.Text.Json.Serialization;
using FlowHub.Application.Board;
using FlowHub.Application.Workspace;

namespace FlowHub.Api.Configuration;

public static class OptionsSetup
{
    /// <summary>Wiąże sekcje appsettings.json z opcjami i ustawia format JSON (enumy jako tekst).</summary>
    public static WebApplicationBuilder AddFlowHubOptions(this WebApplicationBuilder builder)
    {
        var contentRoot = builder.Environment.ContentRootPath;

        builder.Services.Configure<BoardOptions>(builder.Configuration.GetSection(BoardOptions.SectionName));
        builder.Services.Configure<WorkspaceOptions>(builder.Configuration.GetSection(WorkspaceOptions.SectionName));

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
}
