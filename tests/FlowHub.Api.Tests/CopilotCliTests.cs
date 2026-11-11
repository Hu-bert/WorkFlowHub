using FlowHub.Application.Agents;
using FlowHub.Infrastructure.Agents;

namespace FlowHub.Api.Tests;

/// <summary>
/// Parser i argumenty Copilot CLI. Przykładowe linie JSONL są ZAŁOŻONE na podstawie dokumentacji —
/// po pierwszym uruchomieniu prawdziwego CLI trzeba je porównać z rzeczywistym wyjściem.
/// </summary>
public sealed class CopilotCliTests
{
    private static AgentRequest Request() => new()
    {
        Prompt = "Zrób plan",
        Agent = "planowanie",
        Model = "gpt-5",
        WorkingDirectory = "C:/work/repo",
        AdditionalDirectories = ["C:/work"],
        AllowedTools = ["read", "shell(git:*)"],
        AskPermission = (_, _, _) => Task.FromResult(true),
    };

    [Fact]
    public void Arguments_use_only_the_tools_of_the_step_and_never_allow_all()
    {
        var arguments = CopilotCliAgentRunner.Arguments(Request()).ToList();

        Assert.Equal(["-p", "Zrób plan", "--output-format", "json"], arguments.Take(4));
        Assert.Contains("--no-ask-user", arguments);
        Assert.Equal("planowanie", arguments[arguments.IndexOf("--agent") + 1]);
        Assert.Equal("gpt-5", arguments[arguments.IndexOf("--model") + 1]);
        Assert.Equal(2, arguments.Count(argument => argument == "--allow-tool"));
        Assert.Equal("C:/work", arguments[arguments.IndexOf("--add-dir") + 1]);
        Assert.DoesNotContain(arguments, argument => argument is "--allow-all" or "--yolo" or "--allow-all-tools");
    }

    [Fact]
    public void Message_deltas_are_streamed_and_the_full_message_is_not_repeated()
    {
        var parser = new CopilotJsonlParser();

        var first = parser.Parse("""{"type":"assistant.message_delta","data":{"deltaContent":"Plan "}}""");
        var second = parser.Parse("""{"type":"assistant.message_delta","data":{"deltaContent":"gotowy."}}""");
        var full = parser.Parse("""{"type":"assistant.message","data":{"content":"Plan gotowy."}}""");

        Assert.Equal("Plan ", Assert.IsType<AgentEvent.MessageDelta>(first).Text);
        Assert.Equal("gotowy.", Assert.IsType<AgentEvent.MessageDelta>(second).Text);
        Assert.Null(full);
        Assert.Equal("Plan gotowy.", parser.LastMessage);
    }

    [Fact]
    public void Full_message_without_deltas_is_shown()
    {
        var parser = new CopilotJsonlParser();

        var message = parser.Parse("""{"type":"assistant.message","data":{"content":"Gotowe"}}""");

        Assert.StartsWith("Gotowe", Assert.IsType<AgentEvent.MessageDelta>(message).Text);
    }

    [Fact]
    public void Tools_usage_and_errors_are_mapped()
    {
        var parser = new CopilotJsonlParser();

        var started = parser.Parse("""{"type":"tool.execution_start","data":{"toolName":"shell","arguments":{"command":"git status"}}}""");
        var finished = parser.Parse("""{"type":"tool.execution_complete","data":{"toolName":"shell","success":false}}""");
        var usage = parser.Parse("""{"type":"assistant.usage","data":{"model":"gpt-5","inputTokens":1200,"outputTokens":300,"cacheReadTokens":800,"cost":0.0125}}""");
        var error = parser.Parse("""{"type":"session.error","data":{"message":"Rate limit"}}""");

        Assert.Contains("git status", Assert.IsType<AgentEvent.ToolStarted>(started).Detail);
        Assert.False(Assert.IsType<AgentEvent.ToolFinished>(finished).Succeeded);
        var tokens = Assert.IsType<AgentEvent.Usage>(usage).Tokens;
        Assert.Equal((1200, 300, 800, 0.0125m, "gpt-5"), (tokens.InputTokens, tokens.OutputTokens, tokens.CacheReadTokens, tokens.Cost, tokens.Model));
        Assert.Equal("Rate limit", Assert.IsType<AgentEvent.Failed>(error).Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("""{"type":"session.idle"}""")]
    [InlineData("""{"type":"coś.nowego","data":{"x":1}}""")]
    public void Unknown_events_and_empty_lines_are_ignored(string line)
    {
        Assert.Null(new CopilotJsonlParser().Parse(line));
    }

    [Fact]
    public void Plain_text_and_broken_json_are_shown_in_the_console_instead_of_crashing()
    {
        var parser = new CopilotJsonlParser();

        Assert.StartsWith("Logging in…", Assert.IsType<AgentEvent.MessageDelta>(parser.Parse("Logging in…")).Text);
        Assert.StartsWith("{broken", Assert.IsType<AgentEvent.MessageDelta>(parser.Parse("{broken")).Text);
    }
}
