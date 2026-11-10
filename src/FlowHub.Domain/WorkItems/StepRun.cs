using FlowHub.Domain.Flows;

namespace FlowHub.Domain.WorkItems;

/// <summary>Wykonanie jednego kroku flow w zadaniu (dla kroków „per repo” — osobne na każde repo).</summary>
public sealed class StepRun
{
    private StepRun()
    {
    }

    public Guid Id { get; private set; }
    public Guid WorkItemId { get; private set; }

    /// <summary>Id bloczka z definicji flow.</summary>
    public string StepId { get; private set; } = "";

    public string Name { get; private set; } = "";
    public StepKind Kind { get; private set; }
    public int Order { get; private set; }

    /// <summary>Repo, którego dotyczy krok (tylko kroki „per repo”).</summary>
    public string? RepoName { get; private set; }

    public StepStatus Status { get; private set; } = StepStatus.Pending;
    public bool RequiresApproval { get; private set; }

    /// <summary>Co krok zrobi: prompt agenta, komenda skryptu albo checklista. Edytowalne przed uruchomieniem.</summary>
    public string? Proposal { get; private set; }

    public string? Model { get; private set; }
    public int TokenBudget { get; private set; }

    /// <summary>Krótkie podsumowanie wyniku (przekazywane kolejnym krokom zamiast całej rozmowy).</summary>
    public string? Result { get; private set; }

    public string? Error { get; private set; }

    /// <summary>Uwaga użytkownika przy „Powtórz z uwagą”.</summary>
    public string? Remark { get; private set; }

    public string Log { get; private set; } = "";
    public int Attempt { get; private set; } = 1;
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public TokenUsage Usage { get; private set; } = TokenUsage.Empty;

    public bool IsFinished => Status is StepStatus.Succeeded or StepStatus.Skipped;
    public bool IsOverBudget => TokenBudget > 0 && Usage.TotalTokens >= TokenBudget;
    public TimeSpan? Duration => FinishedAt - StartedAt;

    public static StepRun Create(Guid workItemId, StepDefinition definition, int order, string? repoName) => new()
    {
        Id = Guid.NewGuid(),
        WorkItemId = workItemId,
        StepId = definition.Id,
        Name = definition.Name,
        Kind = definition.Kind,
        Order = order,
        RepoName = repoName,
        RequiresApproval = definition.RequiresApproval,
        Model = definition.Model,
        TokenBudget = definition.TokenBudget,
    };

    public void Propose(string proposal, string? model)
    {
        TransitionTo(StepStatus.Proposed);
        Proposal = proposal;
        Model = model ?? Model;
    }

    public void EditProposal(string proposal, string? model, int? tokenBudget)
    {
        if (Status != StepStatus.Proposed)
        {
            throw new DomainException("Propozycję można edytować tylko przed zatwierdzeniem.");
        }

        if (tokenBudget < 0)
        {
            throw new DomainException("Budżet tokenów nie może być ujemny.");
        }

        Proposal = proposal;
        Model = model ?? Model;
        TokenBudget = tokenBudget ?? TokenBudget;
    }

    public void Approve() => TransitionTo(StepStatus.Approved);

    public void Skip() => TransitionTo(StepStatus.Skipped);

    public void Start(DateTime now)
    {
        TransitionTo(StepStatus.Running);
        StartedAt = now;
        FinishedAt = null;
        Result = null;
        Error = null;
        Log = "";
        Usage = TokenUsage.Empty;
    }

    public void Succeed(string result, DateTime now)
    {
        TransitionTo(StepStatus.Succeeded);
        Result = result;
        FinishedAt = now;
    }

    public void Fail(string error, DateTime now)
    {
        TransitionTo(StepStatus.Failed);
        Error = error;
        FinishedAt = now;
    }

    /// <summary>Wraca do propozycji, żeby krok wykonać ponownie (opcjonalnie z uwagą).</summary>
    public void Reopen(string? remark)
    {
        TransitionTo(StepStatus.Proposed);
        Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
        Attempt++;
    }

    public void AddUsage(TokenUsage usage) => Usage = Usage.Add(usage);

    public void AppendLog(string text) => Log += text;

    private void TransitionTo(StepStatus next)
    {
        StepStatusTransitions.EnsureAllowed(Status, next);
        Status = next;
    }
}
