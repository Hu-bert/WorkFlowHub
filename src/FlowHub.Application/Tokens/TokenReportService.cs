using FlowHub.Application.WorkItems;
using FlowHub.Domain.WorkItems;

namespace FlowHub.Application.Tokens;

/// <summary>Wiersz raportu: zużycie zsumowane dla jednej wartości wymiaru (modelu, typu, zadania, kroku, dnia).</summary>
public sealed record TokenReportRow(string Label, long InputTokens, long OutputTokens, long CacheReadTokens, long TotalTokens, decimal Cost, int Runs);

public sealed record TokenReport(
    TokenReportRow Total,
    IReadOnlyList<TokenReportRow> ByModel,
    IReadOnlyList<TokenReportRow> ByType,
    IReadOnlyList<TokenReportRow> ByWorkItem,
    IReadOnlyList<TokenReportRow> ByStep,
    IReadOnlyList<TokenReportRow> ByDay);

/// <summary>Raport tokenów i kosztu: per model, typ zadania, zadanie, krok i dzień (trend).</summary>
public sealed class TokenReportService(IWorkItemStore store)
{
    private sealed record Entry(WorkItem Item, StepRun Step);

    public async Task<TokenReport> BuildAsync(CancellationToken ct)
    {
        var entries = (await store.ListAsync(ct))
            .SelectMany(item => item.Steps.Select(step => new Entry(item, step)))
            .Where(entry => entry.Step.Usage.TotalTokens > 0)
            .ToList();

        return new TokenReport(
            Sum("Razem", entries),
            GroupBy(entries, entry => entry.Step.Usage.Model ?? "nieznany"),
            GroupBy(entries, entry => entry.Item.Type),
            GroupBy(entries, entry => $"{entry.Item.Key} — {entry.Item.Title}"),
            GroupBy(entries, entry => entry.Step.Name),
            GroupBy(entries, entry => Day(entry.Step)).OrderBy(row => row.Label).ToList());
    }

    private static List<TokenReportRow> GroupBy(IEnumerable<Entry> entries, Func<Entry, string> label) =>
        entries
            .GroupBy(label)
            .Select(group => Sum(group.Key, group))
            .OrderByDescending(row => row.TotalTokens)
            .ToList();

    private static TokenReportRow Sum(string label, IEnumerable<Entry> entries)
    {
        var steps = entries.Select(entry => entry.Step).ToList();
        var usage = steps.Aggregate(TokenUsage.Empty, (sum, step) => sum.Add(step.Usage));

        return new TokenReportRow(
            label, usage.InputTokens, usage.OutputTokens, usage.CacheReadTokens, usage.TotalTokens, usage.Cost, steps.Count);
    }

    private static string Day(StepRun step) => (step.FinishedAt ?? step.StartedAt ?? DateTime.UtcNow).ToLocalTime().ToString("yyyy-MM-dd");
}
