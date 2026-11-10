namespace FlowHub.Domain.Flows;

public enum StepKind
{
    /// <summary>Skrypt PowerShell — bez AI, 0 tokenów.</summary>
    Script,

    /// <summary>Wywołanie Copilot (custom agent / skill / prompt).</summary>
    Agent,

    /// <summary>Krok ręczny — checklista dla człowieka.</summary>
    Manual,

    /// <summary>Odczyt stanu z zewnątrz (PR, build, wdrożenie).</summary>
    Check,
}
