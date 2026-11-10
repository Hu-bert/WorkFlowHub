using FlowHub.Api.Endpoints;
using FlowHub.Api.Hubs;
using FlowHub.Application;
using FlowHub.Infrastructure;
using FlowHub.Infrastructure.Persistence;
using FlowHub.Integrations;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger.ReadFrom.Configuration(context.Configuration));

builder.Services
    .AddApplication()
    .AddInfrastructure()
    .AddIntegrations(builder.Configuration)
    .AddSignalR();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthEndpoints();
app.MapHub<LiveHub>(LiveHub.Route);
app.MapFallbackToFile("index.html");

await app.Services.InitializeDatabaseAsync();
await app.RunAsync();

public partial class Program;
