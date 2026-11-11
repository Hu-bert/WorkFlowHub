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
        ExpectedOutputs = ["C:/work/plan.md"],
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
        Assert.Equal(["read", "shell(git:*)", "write(C:/work/plan.md)"], AllowedTools(arguments));
        Assert.Equal("C:/work", arguments[arguments.IndexOf("--add-dir") + 1]);
        Assert.DoesNotContain(arguments, argument => argument is "--allow-all" or "--yolo" or "--allow-all-tools");
    }

    private static List<string> AllowedTools(List<string> arguments) =>
        arguments.Select((argument, index) => (argument, index))
            .Where(pair => pair.argument == "--allow-tool")
            .Select(pair => arguments[pair.index + 1])
            .ToList();

    // Linie poniżej to skrócone, prawdziwe zdarzenia z Copilot CLI 1.0.109 (copilot -p … --output-format json).

    [Fact]
    public void Message_deltas_are_streamed_and_the_full_message_only_ends_the_line()
    {
        var parser = new CopilotJsonlParser();

        var first = parser.Parse("""{"type":"assistant.message_delta","data":{"messageId":"3da3","deltaContent":"Pl"},"ephemeral":true}""");
        var second = parser.Parse("""{"type":"assistant.message_delta","data":{"messageId":"3da3","deltaContent":"ik gotowy."},"ephemeral":true}""");
        var full = parser.Parse("""{"type":"assistant.message","data":{"messageId":"3da3","model":"gpt-6-luna","content":"Plik gotowy.","toolRequests":[],"phase":"final_answer"}}""");

        Assert.Equal("Pl", Assert.IsType<AgentEvent.MessageDelta>(first).Text);
        Assert.Equal("ik gotowy.", Assert.IsType<AgentEvent.MessageDelta>(second).Text);
        Assert.Equal(Environment.NewLine, Assert.IsType<AgentEvent.MessageDelta>(full).Text);
        Assert.Equal("Plik gotowy.", parser.LastMessage);
    }

    [Fact]
    public void Full_message_without_deltas_is_shown()
    {
        var parser = new CopilotJsonlParser();

        var message = parser.Parse("""{"type":"assistant.message","data":{"content":"Gotowe"}}""");

        Assert.StartsWith("Gotowe", Assert.IsType<AgentEvent.MessageDelta>(message).Text);
    }

    [Fact]
    public void Message_that_only_calls_a_tool_is_skipped_and_keeps_the_last_answer()
    {
        var parser = new CopilotJsonlParser();
        parser.Parse("""{"type":"assistant.message","data":{"content":"Odpowiedź"}}""");

        var toolCall = parser.Parse("""{"type":"assistant.message","data":{"content":"","toolRequests":[{"toolCallId":"call_1","name":"view"}]}}""");

        Assert.Null(toolCall);
        Assert.Equal("Odpowiedź", parser.LastMessage);
    }

    [Fact]
    public void Tool_completion_takes_its_name_from_the_matching_start()
    {
        var parser = new CopilotJsonlParser();

        var started = parser.Parse("""{"type":"tool.execution_start","data":{"toolCallId":"call_1","toolName":"view","arguments":{"path":"C:\\work\\hello.txt"},"turnId":"0"}}""");
        var finished = parser.Parse("""{"type":"tool.execution_complete","data":{"toolCallId":"call_1","model":"gpt-6-luna","success":false,"result":{"content":"x"}}}""");

        var tool = Assert.IsType<AgentEvent.ToolStarted>(started);
        Assert.Equal("view", tool.Tool);
        Assert.Contains("hello.txt", tool.Detail);
        Assert.Equal(new AgentEvent.ToolFinished("view", false), finished);
    }

    [Fact]
    public void Session_id_and_errors_are_mapped()
    {
        var parser = new CopilotJsonlParser();

        var session = parser.Parse("""{"type":"session.start","data":{"sessionId":"fd967635-4ff9-4f43-92ac-4cbbffc83bff","copilotVersion":"1.0.109"}}""");
        var error = parser.Parse("""{"type":"session.error","data":{"message":"Rate limit"}}""");

        Assert.Equal("fd967635-4ff9-4f43-92ac-4cbbffc83bff", Assert.IsType<AgentEvent.SessionStarted>(session).SessionId);
        Assert.Equal("Rate limit", Assert.IsType<AgentEvent.Failed>(error).Error);
    }

    [Fact]
    public void Usage_file_gives_tokens_per_model_without_cheap_cache_reads()
    {
        const string File = """
            {
              "totalPremiumRequestCost": 1,
              "totalNanoAiu": 204289000,
              "modelMetrics": {
                "gpt-6-luna": {
                  "requests": { "count": 2, "cost": 1 },
                  "usage": { "inputTokens": 29500, "outputTokens": 93, "cacheReadTokens": 14704, "cacheWriteTokens": 14790, "reasoningTokens": 0 }
                }
              }
            }
            """;

        var usage = Assert.Single(CopilotJsonlParser.ParseUsageFile(File));

        Assert.Equal(("gpt-6-luna", 14796, 93, 14704, 14790), (usage.Model, usage.InputTokens, usage.OutputTokens, usage.CacheReadTokens, usage.CacheWriteTokens));
    }
    [Theory]
    [InlineData("")]
    [InlineData("""{"type":"assistant.idle","data":{},"ephemeral":true}""")]
    [InlineData("""{"type":"result","sessionId":"fd96","exitCode":0,"usage":{"premiumRequests":1}}""")]
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
