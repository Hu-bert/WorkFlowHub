using System.Net.WebSockets;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using FlowHub.Application.Agents;
using FlowHub.Application.Terminal;
using FlowHub.Domain;
using FlowHub.Infrastructure.Agents;
using FlowHub.Infrastructure.Terminal;
using Microsoft.Extensions.Options;

namespace FlowHub.Api.Endpoints;

/// <summary>
/// Terminal Copilota w przeglądarce: WebSocket ↔ pseudokonsola z interaktywnym `copilot`.
/// Serwer → klient: surowe bajty ekranu. Klient → serwer: {"type":"input","data":"…"} albo {"type":"resize","cols":n,"rows":n}.
/// </summary>
public static class TerminalEndpoints
{
    public const string Route = "/ws/terminal";

    private const int MaxMessageSize = 64 * 1024;

    public static IEndpointRouteBuilder MapTerminalEndpoints(this IEndpointRouteBuilder app)
    {
        // Sprawdzenie przed otwarciem okna terminala — błąd (np. brak katalogu) wraca jako czytelny komunikat.
        app.MapGet("/api/work-items/{workItemId:guid}/terminal", async (
            Guid workItemId, string? repo, Guid? step, TerminalService terminals, CancellationToken ct) =>
            await terminals.PlanAsync(workItemId, repo, step, ct) is { } launch ? Results.Ok(launch) : Results.NotFound());

        app.Map(Route, async (
            HttpContext http, Guid workItemId, string? repo, Guid? step, int? cols, int? rows,
            TerminalService terminals, IOptions<AgentOptions> agent, ILoggerFactory loggers) =>
        {
            if (!http.WebSockets.IsWebSocketRequest)
            {
                return Results.BadRequest("Ten adres obsługuje tylko WebSocket.");
            }

            // Przeglądarka pozwala dowolnej stronie otworzyć WebSocket do localhost. Terminal uruchamia
            // polecenia na tym komputerze, więc wpuszczamy tylko strony otwarte z tego komputera.
            if (!IsLocalOrigin(http.Request.Headers.Origin))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!OperatingSystem.IsWindows())
            {
                return Results.Problem("Terminal wymaga Windows (ConPTY).", statusCode: StatusCodes.Status501NotImplemented);
            }

            var launch = await terminals.PlanAsync(workItemId, repo, step, http.RequestAborted);
            if (launch is null)
            {
                return Results.NotFound();
            }

            using var socket = await http.WebSockets.AcceptWebSocketAsync();
            var logger = loggers.CreateLogger(typeof(TerminalEndpoints));

            try
            {
                using var process = PseudoConsoleProcess.Start(
                    CopilotCli.Resolve(agent.Value.CliPath), launch.Arguments, launch.WorkingDirectory, cols ?? 120, rows ?? 30);

                logger.LogInformation("Terminal Copilota: {Directory}", launch.WorkingDirectory);
                await RunAsync(socket, process, http.RequestAborted);
            }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException)
            {
                logger.LogError(error, "Nie udało się uruchomić terminala");
                await SendTextAsync(socket, $"\r\nNie udało się uruchomić Copilot CLI ('{agent.Value.CliPath}'): {error.Message}\r\nSprawdź Ustawienia → GitHub Copilot CLI.\r\n");
            }

            await CloseAsync(socket);
            return Results.Empty;
        });

        return app;
    }

    /// <summary>Origin musi być adresem tego komputera (localhost / 127.0.0.1 / ::1) — brak nagłówka też odrzucamy.</summary>
    public static bool IsLocalOrigin(string? origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback;

    [SupportedOSPlatform("windows")]
    private static async Task RunAsync(WebSocket socket, PseudoConsoleProcess process, CancellationToken aborted)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(aborted);

        // Odczyt ekranu jest blokujący, więc ma własny wątek; kończy się, gdy konsola zostanie zamknięta.
        var screen = Task.Factory.StartNew(() => PumpScreen(socket, process), TaskCreationOptions.LongRunning);
        var keyboard = PumpKeyboardAsync(socket, process, stop.Token);
        var exited = process.WaitForExitAsync(stop.Token);

        await Task.WhenAny(keyboard, exited);
        await stop.CancelAsync();

        process.Close();
        await screen.WaitAsync(TimeSpan.FromSeconds(5)).ContinueWith(_ => { }, TaskScheduler.Default);
        await Task.WhenAll(keyboard, exited).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    [SupportedOSPlatform("windows")]
    private static void PumpScreen(WebSocket socket, PseudoConsoleProcess process)
    {
        var buffer = new byte[8192];

        try
        {
            int read;
            while ((read = process.Output.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (socket.State != WebSocketState.Open)
                {
                    continue; // Klient się rozłączył — czytamy dalej, żeby konsola mogła się zamknąć.
                }

                socket.SendAsync(buffer.AsMemory(0, read), WebSocketMessageType.Binary, true, CancellationToken.None)
                    .AsTask().GetAwaiter().GetResult();
            }
        }
        catch (Exception error) when (error is IOException or WebSocketException or ObjectDisposedException or OperationCanceledException)
        {
            // Konsola albo połączenie zamknięte.
        }
    }

    [SupportedOSPlatform("windows")]
    private static async Task PumpKeyboardAsync(WebSocket socket, PseudoConsoleProcess process, CancellationToken ct)
    {
        var buffer = new byte[MaxMessageSize];

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var length = 0;
                ValueWebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer.AsMemory(length), ct);
                    length += result.Count;
                }
                while (!result.EndOfMessage && length < buffer.Length);

                if (result.MessageType == WebSocketMessageType.Close || !result.EndOfMessage)
                {
                    return;
                }

                Handle(JsonSerializer.Deserialize<ClientMessage>(buffer.AsSpan(0, length), JsonSerializerOptions.Web), process);
            }
        }
        catch (Exception error) when (error is WebSocketException or OperationCanceledException or JsonException or IOException)
        {
            // Rozłączenie albo śmieci od klienta — kończymy sesję.
        }
    }

    [SupportedOSPlatform("windows")]
    private static void Handle(ClientMessage? message, PseudoConsoleProcess process)
    {
        switch (message)
        {
            case { Type: "input", Data: { Length: > 0 } data }:
                process.Input.Write(Encoding.UTF8.GetBytes(data));
                process.Input.Flush();
                break;

            case { Type: "resize", Cols: { } cols, Rows: { } rows }:
                process.Resize(cols, rows);
                break;
        }
    }

    private static Task SendTextAsync(WebSocket socket, string text) =>
        socket.State == WebSocketState.Open
            ? socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Binary, true, CancellationToken.None)
            : Task.CompletedTask;

    private static async Task CloseAsync(WebSocket socket)
    {
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Sesja zakończona", CancellationToken.None);
            }
        }
        catch (WebSocketException)
        {
            // Klient już zniknął.
        }
    }

    private sealed record ClientMessage(string? Type, string? Data, int? Cols, int? Rows);
}
