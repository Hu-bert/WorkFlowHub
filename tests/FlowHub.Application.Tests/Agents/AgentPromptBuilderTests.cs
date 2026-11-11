using FlowHub.Application.Agents;
using FlowHub.Application.Blocks;
using FlowHub.Application.Engine;
using FlowHub.Domain.Flows;
using FlowHub.Domain.WorkItems;
using NSubstitute;

namespace FlowHub.Application.Tests.Agents;

public sealed class AgentPromptBuilderTests : IDisposable
{
    private readonly string _workRoot = Directory.CreateTempSubdirectory("flowhub-prompt-").FullName;
    private readonly IBlockLibrary _blocks = Substitute.For<IBlockLibrary>();
    private readonly AgentPromptBuilder _builder;

    public AgentPromptBuilderTests()
    {
        _blocks.FullPath(Arg.Any<string>()).Returns(call => Path.Combine("BLOCKS", call.Arg<string>()));
        _builder = new AgentPromptBuilder(_blocks);
    }

    public void Dispose() => Directory.Delete(_workRoot, recursive: true);

    private StepContext Context(StepDefinition definition, params string[] repos)
    {
        var item = WorkItem.Create(
            new NewWorkItem("PROJ-7", "Zła stawka VAT", "Opis", "Bug", TaskSourceKind.Manual, null), _workRoot, "todo", DateTime.UtcNow);
        foreach (var repo in repos.DefaultIfEmpty("Billing.Api"))
        {
            item.AddRepo(repo);
        }

        item.PlanSteps(new FlowDefinition { Type = "Bug", Name = "Błąd", Steps = [definition] });
        Directory.CreateDirectory(item.WorkDirectory);
        return new StepContext(item, item.Steps[0], definition);
    }

    private static StepDefinition Step(string? prompt = null, bool perRepo = false, string[]? inputs = null, string[]? outputs = null) => new()
    {
        Id = "step",
        Name = "Analiza",
        Kind = StepKind.Agent,
        Agent = "a",
        Prompt = prompt,
        PerRepo = perRepo,
        Inputs = inputs ?? [],
        Outputs = outputs ?? [],
    };

    [Fact]
    public void Template_placeholders_are_filled_with_task_and_repo_data()
    {
        _blocks.ReadText("prompts/x.md").Returns("Zadanie {{task.key}}: {{task.title}} w repo {{repo.name}} ({{repo.branch}}). Plik: {{task.file}}");

        var prompt = _builder.Build(Context(Step("prompts/x.md", perRepo: true)));

        Assert.StartsWith("Zadanie PROJ-7: Zła stawka VAT w repo Billing.Api (feature/PROJ-7-zla-stawka-vat).", prompt);
        Assert.EndsWith("task.md", prompt);
        Assert.DoesNotContain("{{", prompt);
    }

    [Fact]
    public void Missing_template_falls_back_to_a_default_prompt()
    {
        var prompt = _builder.Build(Context(Step("prompts/nie-ma.md")));

        Assert.Contains("Wykonaj krok „Analiza” dla zadania PROJ-7", prompt);
    }

    [Fact]
    public void Output_files_live_in_the_task_folder_and_use_the_repo_name()
    {
        var context = Context(Step(perRepo: true, outputs: ["context-{repo}.md"]));

        var outputs = _builder.OutputFiles(context);
        var prompt = _builder.Build(context);

        Assert.Equal([Path.Combine(context.Item.WorkDirectory, "context-Billing.Api.md")], outputs);
        Assert.Contains("Pliki wynikowe", prompt);
        Assert.Contains("context-Billing.Api.md", prompt);
    }

    [Fact]
    public void Inputs_list_existing_files_matching_patterns_and_templates_from_blocks()
    {
        var context = Context(Step(inputs: ["context-*.md", "plan.md", "templates/mail.md"]), "Billing.Api", "Shared.Lib");
        File.WriteAllText(Path.Combine(context.Item.WorkDirectory, "context-Billing.Api.md"), "");
        File.WriteAllText(Path.Combine(context.Item.WorkDirectory, "context-Shared.Lib.md"), "");

        var prompt = _builder.Build(context);

        Assert.Contains("context-Billing.Api.md", prompt);
        Assert.Contains("context-Shared.Lib.md", prompt);
        Assert.Contains(Path.Combine(context.Item.WorkDirectory, "plan.md"), prompt);
        Assert.Contains(Path.Combine("BLOCKS", "templates/mail.md"), prompt);
        Assert.Contains("przeczytaj zamiast skanować repo", prompt);
    }
}
