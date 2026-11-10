using FlowHub.Api.Configuration;
using FlowHub.Api.Endpoints;
using FlowHub.Api.Hubs;
using FlowHub.Application;
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
    .AddSignalR();

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseDefaultFiles();
app.UseStaticFiles();

// Nowy moduł API = nowy plik w Endpoints/ + jedna linia tutaj.
app.MapHealthEndpoints();
app.MapFlowEndpoints();
app.MapRepositoryEndpoints();
app.MapWorkItemEndpoints();
app.MapHub<LiveHub>(LiveHub.Route);
app.MapFallbackToFile("index.html");

await app.Services.InitializeDatabaseAsync();
await app.RunAsync();

public partial class Program;
