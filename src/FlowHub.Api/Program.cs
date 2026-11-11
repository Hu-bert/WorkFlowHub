using System.Text.Json.Serialization;
using FlowHub.Api.Configuration;
using FlowHub.Api.Endpoints;
using FlowHub.Api.Hosting;
using FlowHub.Api.Hubs;
using FlowHub.Application;
using FlowHub.Application.Engine;
using FlowHub.Infrastructure;
using FlowHub.Infrastructure.Persistence;
using FlowHub.Integrations;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger.ReadFrom.Configuration(context.Configuration));
builder.AddFlowHubOptions();

builder.Services
    .AddApplication()
    .AddInfrastructure()
    .AddIntegrations(builder.Configuration)
    .AddExceptionHandler<DomainExceptionHandler>()
    .AddProblemDetails()
    .AddSingleton<ILiveNotifier, SignalRLiveNotifier>()
    .AddHostedService<StepWorker>()
    .AddHostedService<RepoStatusRefresher>()
    .AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();

// Nowy moduł API = nowy plik w Endpoints/ + jedna linia tutaj.
app.MapHealthEndpoints();
app.MapFlowEndpoints();
app.MapRepositoryEndpoints();
app.MapWorkItemEndpoints();
app.MapStepEndpoints();
app.MapAgentEndpoints();
app.MapTokenEndpoints();
app.MapIntegrationEndpoints();
app.MapSettingsEndpoints();
app.MapTerminalEndpoints();
app.MapHub<LiveHub>(LiveHub.Route);
app.MapFallbackToFile("index.html");

await app.Services.InitializeDatabaseAsync();
await app.RunAsync();

public partial class Program;
