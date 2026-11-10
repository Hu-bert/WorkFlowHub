using FlowHub.Application.Flows;

namespace FlowHub.Application.Tests;

public sealed class FlowValidatorTests
{
    private static FlowDocument ValidFlow() => new()
    {
        Type = "Bug",
        Name = "Błąd",
        Flow =
        [
            new StepDocument { Id = "create-folder", Kind = "Script", Script = "scripts/create-folder.ps1" },
            new StepDocument { Id = "analyze", Kind = "Agent", Agent = "analiza", Model = "tier:cheap", TokenBudget = 1000 },
            new StepDocument { Id = "confirm", Kind = "Manual" },
            new StepDocument { Id = "pr", Kind = "Check", Check = "pull-request" },
        ],
    };

    [Fact]
    public void Valid_flow_has_no_errors()
    {
        Assert.Empty(FlowValidator.Validate(ValidFlow()));
    }

    [Fact]
    public void Type_name_and_steps_are_required()
    {
        var errors = FlowValidator.Validate(new FlowDocument());

        Assert.Contains(errors, error => error.Contains("'type'"));
        Assert.Contains(errors, error => error.Contains("'name'"));
        Assert.Contains(errors, error => error.Contains("co najmniej jeden krok"));
    }

    [Fact]
    public void Unknown_kind_lists_the_allowed_ones()
    {
        var flow = ValidFlow();
        flow.Flow[0].Kind = "Skrypt";

        var error = Assert.Single(FlowValidator.Validate(flow));

        Assert.Contains("'create-folder'", error);
        Assert.Contains("Script, Agent, Manual, Check", error);
    }

    [Theory]
    [InlineData("Script", "script")]
    [InlineData("Agent", "agent lub prompt")]
    [InlineData("Check", "check")]
    public void Each_kind_requires_its_own_field(string kind, string missingField)
    {
        var flow = ValidFlow();
        flow.Flow = [new StepDocument { Id = "step", Kind = kind }];

        var error = Assert.Single(FlowValidator.Validate(flow));

        Assert.Contains($"'{missingField}'", error);
    }

    [Fact]
    public void Duplicate_step_ids_are_reported()
    {
        var flow = ValidFlow();
        flow.Flow[1].Id = "create-folder";

        Assert.Contains(FlowValidator.Validate(flow), error => error.Contains("powtarza się 2 razy"));
    }

    [Theory]
    [InlineData("Create Folder")]
    [InlineData("create_folder")]
    [InlineData("")]
    public void Step_id_must_be_kebab_case(string id)
    {
        var flow = ValidFlow();
        flow.Flow[0].Id = id;

        Assert.Contains(FlowValidator.Validate(flow), error => error.Contains("'id'"));
    }

    [Fact]
    public void Negative_budget_and_empty_model_are_errors()
    {
        var flow = ValidFlow();
        flow.Flow[1].TokenBudget = -1;
        flow.Flow[1].Model = "tier:";

        var errors = FlowValidator.Validate(flow);

        Assert.Contains(errors, error => error.Contains("tokenBudget"));
        Assert.Contains(errors, error => error.Contains("'model'"));
    }

    [Fact]
    public void Document_round_trips_through_the_domain_definition()
    {
        var definition = FlowMapper.ToDefinition(ValidFlow());
        var document = FlowMapper.ToDocument(definition);

        Assert.Equal("Bug", definition.Type);
        Assert.Equal("create-folder", definition.Steps[0].Name);
        Assert.Empty(FlowValidator.Validate(document));
        Assert.Equal(4, document.Flow.Count);
    }
}
