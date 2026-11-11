using FlowHub.Domain.Flows;

namespace FlowHub.Application.Flows;

/// <summary>Zamienia zwalidowany dokument na definicję domenową i z powrotem.</summary>
public static class FlowMapper
{
    public static FlowDefinition ToDefinition(FlowDocument document) => new()
    {
        Type = document.Type!,
        Name = document.Name!,
        Version = document.Version,
        Steps = document.Flow.Select(ToDefinition).ToList(),
    };

    public static FlowDocument ToDocument(FlowDefinition flow) => new()
    {
        Type = flow.Type,
        Name = flow.Name,
        Version = flow.Version,
        Flow = flow.Steps.Select(ToDocument).ToList(),
    };

    private static StepDefinition ToDefinition(StepDocument step)
    {
        FlowValidator.TryParseKind(step.Kind, out var kind);

        return new StepDefinition
        {
            Id = step.Id!,
            Name = string.IsNullOrWhiteSpace(step.Name) ? step.Id! : step.Name,
            Kind = kind,
            Script = step.Script,
            Agent = step.Agent,
            Prompt = step.Prompt,
            Check = step.Check,
            Checklist = step.Checklist,
            FollowUp = string.IsNullOrWhiteSpace(step.FollowUp) ? null : step.FollowUp.Trim(),
            Model = step.Model,
            TokenBudget = step.TokenBudget,
            RequiresApproval = step.RequiresApproval,
            PerRepo = step.PerRepo,
            AllowedTools = step.AllowedTools,
            Inputs = step.Inputs,
            Outputs = step.Outputs,
        };
    }

    private static StepDocument ToDocument(StepDefinition step) => new()
    {
        Id = step.Id,
        Name = step.Name,
        Kind = step.Kind.ToString(),
        Script = step.Script,
        Agent = step.Agent,
        Prompt = step.Prompt,
        Check = step.Check,
        Checklist = [.. step.Checklist],
        FollowUp = step.FollowUp,
        Model = step.Model,
        TokenBudget = step.TokenBudget,
        RequiresApproval = step.RequiresApproval,
        PerRepo = step.PerRepo,
        AllowedTools = [.. step.AllowedTools],
        Inputs = [.. step.Inputs],
        Outputs = [.. step.Outputs],
    };
}
