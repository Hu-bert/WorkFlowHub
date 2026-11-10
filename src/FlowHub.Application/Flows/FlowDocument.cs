namespace FlowHub.Application.Flows;

/// <summary>Surowa treść pliku flow (YAML lub formularz edytora) — przed walidacją.</summary>
public sealed class FlowDocument
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public int Version { get; set; } = 1;
    public List<StepDocument> Flow { get; set; } = [];
}

public sealed class StepDocument
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Kind { get; set; }
    public string? Script { get; set; }
    public string? Agent { get; set; }
    public string? Prompt { get; set; }
    public string? Check { get; set; }
    public List<string> Checklist { get; set; } = [];
    public string? Model { get; set; }
    public int TokenBudget { get; set; }
    public bool RequiresApproval { get; set; } = true;
    public bool PerRepo { get; set; }
    public List<string> AllowedTools { get; set; } = [];
    public List<string> Inputs { get; set; } = [];
    public List<string> Outputs { get; set; } = [];
}
