using System.Globalization;
using System.Text;
using FlowHub.Domain.WorkItems;

namespace FlowHub.Application.Agents;

/// <summary>
/// Statystyki zadania do retro (placeholder {{stats}} w szablonie promptu):
/// tokeny, koszt, liczba prób, błędy i uwagi użytkownika — fakty, z których retro wyciąga wnioski.
/// </summary>
public static class TaskStatistics
{
    public static string For(WorkItem item, StepRun currentStep)
    {
        var steps = item.OrderedSteps.Where(step => step.Id != currentStep.Id && step.Status != StepStatus.Pending).ToList();
        var text = new StringBuilder();

        text.AppendLine("| Krok | Repo | Status | Prób | Tokeny | Koszt USD | Model |");
        text.AppendLine("|---|---|---|---|---|---|---|");

        foreach (var step in steps)
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"| {step.Name} | {step.RepoName ?? "—"} | {step.Status} | {step.Attempt} | {step.Usage.TotalTokens} | {step.Usage.Cost:0.####} | {step.Usage.Model ?? "—"} |");
        }

        var usage = item.Usage;
        text.AppendLine().AppendLine(CultureInfo.InvariantCulture, $"Razem: {usage.TotalTokens} tokenów, {usage.Cost:0.####} USD.");

        AppendNotes(text, "Uwagi użytkownika przy powtórkach", steps.Where(step => step.Remark is not null).Select(step => $"{step.Name}: {step.Remark}"));
        AppendNotes(text, "Błędy", steps.Where(step => step.Error is not null).Select(step => $"{step.Name}: {FirstLine(step.Error!)}"));

        return text.ToString().TrimEnd();
    }

    private static void AppendNotes(StringBuilder text, string title, IEnumerable<string> notes)
    {
        var list = notes.ToList();
        if (list.Count == 0)
        {
            return;
        }

        text.AppendLine().AppendLine($"{title}:");
        foreach (var note in list)
        {
            text.AppendLine($"- {note}");
        }
    }

    private static string FirstLine(string text) => text.Split('\n', 2)[0].Trim();
}
