using System.Net;
using System.Text;
using FlowHub.Api.Endpoints;
using FlowHub.Application.Terminal;
using FlowHub.Application.WorkItems;
using FlowHub.Domain.WorkItems;
using FlowHub.Infrastructure.Terminal;

namespace FlowHub.Api.Tests;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class TerminalTests(FlowHubApiFactory factory) : IClassFixture<FlowHubApiFactory>
{
    [Fact]
    public async Task Program_in_the_pseudo_console_gets_keyboard_input_and_its_screen_can_be_read()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "ConPTY jest tylko na Windows.");

        using var process = PseudoConsoleProcess.Start("cmd.exe", ["/q", "/k", "echo gotowy"], Path.GetTempPath(), 100, 30);
        var screen = new StringBuilder();
        var reader = Task.Run(() =>
        {
            var buffer = new byte[4096];
            int read;
            while ((read = process.Output.Read(buffer, 0, buffer.Length)) > 0)
            {
                lock (screen)
                {
                    screen.Append(Encoding.UTF8.GetString(buffer, 0, read));
                }
            }
        }, TestContext.Current.CancellationToken);

        process.Input.Write("echo flow^hub\r\nexit\r\n"u8);
        process.Input.Flush();

        await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        process.Close();
        await reader.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Contains("gotowy", screen.ToString());
        Assert.Contains("flowhub", screen.ToString()); // wynik polecenia, nie echo wpisanych znaków („flow^hub”)
    }

    [Theory]
    [InlineData("--resume=abc", "copilot.exe --resume=abc")]
    [InlineData(@"C:\moje zadania\repo", @"copilot.exe ""C:\moje zadania\repo""")]
    [InlineData(@"C:\moje zadania\", @"copilot.exe ""C:\moje zadania\\""")]
    [InlineData(@"powiedz ""cześć""", @"copilot.exe ""powiedz \""cześć\""""")]
    public void Arguments_are_quoted_so_that_each_stays_one_argument(string argument, string expected)
    {
        Assert.Equal(expected, PseudoConsoleProcess.CommandLine("copilot.exe", [argument]));
    }

    [Theory]
    [InlineData("http://localhost:5080", true)]
    [InlineData("http://127.0.0.1:5173", true)]
    [InlineData("https://zla-strona.example", false)]
    [InlineData("http://localhost.zla-strona.example", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void Terminal_accepts_connections_only_from_pages_served_by_this_computer(string? origin, bool allowed)
    {
        Assert.Equal(allowed, TerminalEndpoints.IsLocalOrigin(origin));
    }

    [Fact]
    public async Task Terminal_cannot_be_opened_as_a_plain_request_or_before_the_folder_exists()
    {
        var client = factory.CreateClient();
        var created = await client.PostAsync("/api/work-items", new CreateWorkItemRequest(
            "TERM-1", "Terminal", null, "Technical", TaskSourceKind.Manual, null, ["Shared.Lib"]));
        var item = await created.ReadAsync<WorkItemDetails>();

        var plain = await client.GetAsync($"/ws/terminal?workItemId={item.Summary.Id}", TestContext.Current.CancellationToken);
        var beforeFlow = await client.GetAsync($"/api/work-items/{item.Summary.Id}/terminal", TestContext.Current.CancellationToken);
        var body = await beforeFlow.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, plain.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, beforeFlow.StatusCode);
        Assert.Contains("jeszcze nie istnieje", body);
    }

    [Fact]
    public async Task Terminal_starts_in_the_task_repo_with_access_to_the_task_folder()
    {
        var client = factory.CreateClient();
        var created = await client.PostAsync("/api/work-items", new CreateWorkItemRequest(
            "TERM-2", "Terminal w repo", null, "Technical", TaskSourceKind.Manual, null, ["Shared.Lib"]));
        var item = await created.ReadAsync<WorkItemDetails>();
        Directory.CreateDirectory(item.Repos[0].LocalPath);

        var inRepo = await client.GetAsync<TerminalLaunch>($"/api/work-items/{item.Summary.Id}/terminal");
        var inTaskFolder = await client.GetAsync<TerminalLaunch>($"/api/work-items/{item.Summary.Id}/terminal?repo=");
        var unknownStep = await client.GetAsync($"/api/work-items/{item.Summary.Id}/terminal?step={Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(item.Repos[0].LocalPath, inRepo.WorkingDirectory);
        Assert.Equal(["--add-dir", item.WorkDirectory], inRepo.Arguments);
        Assert.Equal(item.WorkDirectory, inTaskFolder.WorkingDirectory);
        Assert.Empty(inTaskFolder.Arguments);
        Assert.DoesNotContain(inRepo.Arguments, argument => argument.Contains("allow-all") || argument == "--yolo");
        Assert.Equal(HttpStatusCode.BadRequest, unknownStep.StatusCode);
    }
}