using FlowHub.Application.Agents;
using FlowHub.Application.Blocks;
using FlowHub.Domain;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FlowHub.Application.Tests;

public sealed class BlockGeneratorTests
{
    private const string ExampleSkill = "---\nname: retro\ndescription: Podsumowuje zadanie.\n---\n\n# Retro\n";

    private readonly IBlockLibrary _blocks = Substitute.For<IBlockLibrary>();
    private readonly StubRunner _runner = new();

    public BlockGeneratorTests()
    {
        _blocks.List().Returns(
        [
            new BlockFile("skills", "skills/retro/SKILL.md", "retro"),
            new BlockFile("skills", "skills/plan-realizacji/SKILL.md", "plan-realizacji"),
            new BlockFile("agents", "agents/retro.agent.md", "retro"),
        ]);
        _blocks.ReadText(Arg.Any<string>()).Returns((string?)null); // domyślnie: pliku nie ma
        _blocks.ReadText("skills/retro/SKILL.md").Returns(ExampleSkill);
        _blocks.ReadText("skills/plan-realizacji/SKILL.md").Returns("---\nname: plan-realizacji\n---\n");
    }

    private BlockGenerator Generator(string runner = AgentRunners.CopilotCli)
    {
        var models = Options.Create(new ModelOptions { Tiers = { ["cheap"] = "auto" } });
        return new BlockGenerator(_blocks, _runner, new ModelRouter(models), Options.Create(new AgentOptions { Runner = runner }));
    }

    private static GenerateBlockRequest Request(string kind = "skills", string name = "przeglad-bezpieczenstwa", string? example = null) =>
        new(kind, name, "Sprawdza zmiany pod kątem bezpieczeństwa.", example);

    [Fact]
    public async Task Agent_gets_the_expectations_and_an_existing_block_as_the_pattern_and_no_tools()
    {
        _runner.Answer = "```markdown\n---\nname: przeglad-bezpieczenstwa\n---\n\n# Przegląd\n```";

        var draft = await Generator().GenerateAsync(Request(), CancellationToken.None);

        Assert.Equal("skills/przeglad-bezpieczenstwa/SKILL.md", draft.Path);
        Assert.StartsWith("---\nname: przeglad-bezpieczenstwa", draft.Content); // bez ramki ```
        Assert.False(draft.Demo);

        var sent = Assert.Single(_runner.Requests);
        Assert.Contains("Sprawdza zmiany pod kątem bezpieczeństwa.", sent.Prompt);
        Assert.Contains(ExampleSkill, sent.Prompt);
        Assert.Empty(sent.AllowedTools);
        Assert.False(Directory.Exists(sent.WorkingDirectory)); // katalog tymczasowy posprzątany
    }

    [Fact]
    public async Task Nothing_is_saved_the_draft_waits_for_the_user()
    {
        _runner.Answer = "treść";

        await Generator().GenerateAsync(Request(), CancellationToken.None);

        _blocks.DidNotReceiveWithAnyArgs().WriteText(default!, default!);
    }

    [Fact]
    public async Task Chosen_pattern_is_used_when_it_is_of_the_same_kind()
    {
        _runner.Answer = "treść";

        var draft = await Generator().GenerateAsync(Request(example: "skills/plan-realizacji/SKILL.md"), CancellationToken.None);

        Assert.Equal("skills/plan-realizacji/SKILL.md", draft.ExamplePath);
        Assert.Contains("name: plan-realizacji", _runner.Requests.Single().Prompt);
    }

    [Fact]
    public async Task Without_copilot_the_draft_is_a_marked_copy_of_the_pattern()
    {
        var draft = await Generator(AgentRunners.Fake).GenerateAsync(Request(), CancellationToken.None);

        Assert.True(draft.Demo);
        Assert.Contains("name: przeglad-bezpieczenstwa", draft.Content);
        Assert.Contains("description: Sprawdza zmiany pod kątem bezpieczeństwa.", draft.Content);
        Assert.Contains("SZKIC DEMONSTRACYJNY", draft.Content);
        Assert.Empty(_runner.Requests);
    }

    [Theory]
    [InlineData("templates", "nowy", null)] // rodzaj spoza generatora
    [InlineData("skills", "Zła Nazwa", null)]
    [InlineData("skills", "../ucieczka", null)]
    [InlineData("skills", "retro", null)] // już istnieje
    [InlineData("skills", "nowy", "agents/retro.agent.md")] // wzorzec innego rodzaju
    [InlineData("scripts", "nowy", null)] // brak wzorca tego rodzaju
    public async Task Invalid_requests_are_rejected_before_the_agent_runs(string kind, string name, string? example)
    {
        await Assert.ThrowsAsync<DomainException>(() => Generator().GenerateAsync(Request(kind, name, example), CancellationToken.None));
        Assert.Empty(_runner.Requests);
    }

    [Fact]
    public async Task Agent_failure_is_reported_in_words()
    {
        _runner.Failure = "Rate limit";

        var error = await Assert.ThrowsAsync<DomainException>(() => Generator().GenerateAsync(Request(), CancellationToken.None));

        Assert.Contains("Rate limit", error.Message);
    }

    private sealed class StubRunner : IAgentRunner
    {
        public List<AgentRequest> Requests { get; } = [];
        public string Answer { get; set; } = "";
        public string? Failure { get; set; }

        public async IAsyncEnumerable<AgentEvent> RunAsync(AgentRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            Requests.Add(request);
            await Task.Yield();
            yield return Failure is null ? new AgentEvent.Completed(Answer) : new AgentEvent.Failed(Failure);
        }
    }
}